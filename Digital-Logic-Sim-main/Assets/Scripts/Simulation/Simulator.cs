using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DLS.Description;
using DLS.Game;
using Random = System.Random;

namespace DLS.Simulation
{
	public static class Simulator
	{
		// Dictionary to track chip input states over time
		private static readonly Dictionary<SimChip, ChipInputTracker> chipInputTrackers = new Dictionary<SimChip, ChipInputTracker>();

		private class ChipInputTracker
		{
			// Store a history of input states for each pin
			public List<ulong[]> InputStateHistory;
			public int MaxHistoryLength;
			public int UnchangedTickCount;
			
			public ChipInputTracker(SimPin[] inputPins, int historyLength = 10)
			{
				MaxHistoryLength = historyLength;
				InputStateHistory = new List<ulong[]>(MaxHistoryLength);
				
				// Initialize with current state
				ulong[] initialState = new ulong[inputPins.Length];
				for (int i = 0; i < inputPins.Length; i++)
				{
					initialState[i] = inputPins[i].State;
				}
				InputStateHistory.Add(initialState);
				UnchangedTickCount = 0;
			}
			
			public bool CheckAndUpdateInputs(SimPin[] inputPins)
			{
				bool inputsChanged = false;
				ulong[] currentState = InputStateHistory[0]; // Most recent state
				
				// Check if current inputs differ from most recent recorded state
				for (int i = 0; i < inputPins.Length; i++)
				{
					if (i < currentState.Length && currentState[i] != inputPins[i].State)
					{
						inputsChanged = true;
						break;
					}
				}
				
				if (inputsChanged)
				{
					// Inputs changed, add new state to history
					ulong[] newState = new ulong[inputPins.Length];
					for (int i = 0; i < inputPins.Length; i++)
					{
						newState[i] = inputPins[i].State;
					}
					
					// Add to front of history
					InputStateHistory.Insert(0, newState);
					
					// Trim history if it exceeds maximum length
					if (InputStateHistory.Count > MaxHistoryLength)
					{
						InputStateHistory.RemoveAt(InputStateHistory.Count - 1);
					}
					
					UnchangedTickCount = 0;
					return true;
				}
				else
				{
					// Inputs unchanged, increment counter
					UnchangedTickCount++;
					return false;
				}
			}
			
			// Get input state from n ticks ago (0 = current, 1 = previous, etc.)
			public ulong[] GetHistoricalState(int ticksAgo)
			{
				if (ticksAgo < InputStateHistory.Count)
				{
					return InputStateHistory[ticksAgo];
				}
				// If requesting history beyond what we have, return oldest available
				return InputStateHistory[InputStateHistory.Count - 1];
			}
		}
		
		public static readonly Random rng = new();
		public static int stepsPerClockTransition;
		public static int simulationFrame;
		static uint pcg_rngState;

		// When sim is first built, or whenever modified, it needs to run a less efficient pass in which the traversal order of the chips is determined
		public static bool needsOrderPass;

		// Every n frames the simulation permits some random modifications to traversal order of sequential chips (to randomize outcome of race conditions)
		public static bool canDynamicReorderThisFrame;

		static SimChip prevRootSimChip;

		// Modifications to the sim are made from the main thread, but only applied on the sim thread to avoid conflicts
		static readonly ConcurrentQueue<SimModifyCommand> modificationQueue = new();

		public static void UpdateKeyboardInputFromMainThread()
		{
			SimKeyboardHelper.RefreshInputState();
		}


		// ---- Simulation outline ----
		// 1) Forward the initial player-controlled input states to all connected pins.
		// 2) Loop over all subchips not yet processed this frame, and process them if they are ready (i.e. all input pins have received all their inputs)
		//    * Note: this means that the input pins must be aware of how many input connections they have (pins choose randomly between conflicting inputs)
		//    * Note: if a pin has zero input connections, it should be considered as always ready
		// 3) Forward the outputs of the processed subchips to their connected pins, and repeat steps 2 & 3 until no more subchips are ready for processing.
		// 4) If all subchips have now been processed, then we're done. This is not necessarily the case though, since if an input pin depends on the output of its parent chip
		//    (directly or indirectly), then it won't receive all its inputs until the chip has already been run, meaning that the chip must be processed before it is ready.
		//    In this case we process one of the remaining unprocessed (and non-ready) subchips at random, and return to step 3.
		//
		// Optimization ideas (todo):
		// * Compute lookup table for combinational chips
		// * Ignore chip if inputs are same as last frame, and no internal pins changed state last frame.
		//   (would have to make exception for chips containing things like clock or key chip, which can activate 'spontaneously')
		// * Create simplified connections network allowing only builtin chips to be processed during simulation

		public static void RunSimulationStep(SimChip rootSimChip, DevPinInstance[] inputPins)
		{
			if (rootSimChip != prevRootSimChip)
			{
				needsOrderPass = true;
				prevRootSimChip = rootSimChip;
			}

			pcg_rngState = (uint)rng.Next();
			canDynamicReorderThisFrame = simulationFrame % 100 == 0;
			simulationFrame++; //

			// Step 1) Get player-controlled input states and copy values to the sim
			foreach (DevPinInstance input in inputPins)
			{
				try
				{
					SimPin simPin = rootSimChip.GetSimPinFromAddress(input.Pin.Address);
					PinState.Set(ref simPin.State, input.Pin.PlayerInputState);

					input.Pin.State = input.Pin.PlayerInputState;
				}
				catch (Exception)
				{
					// Possible for sim to be temporarily out of sync since running on separate threads, so just ignore failure to find pin.
				}
			}

			// Process
			if (needsOrderPass)
			{
				StepChipReorder(rootSimChip);
				needsOrderPass = false;
			}
			else
			{
				StepChip(rootSimChip);
			}
		}

		// Recursively propagate signals through this chip and its subchips
		static void StepChip(SimChip chip)
		{
			// Propagate signal from all input dev-pins to all their connected pins
			chip.Sim_PropagateInputs();

			// Check if the chip is frozen (freeze pin is high)
			bool isChipFrozen = FreezeChip.IsChipFrozen(chip);

			// If chip is frozen, don't process any subchips at all
			if (isChipFrozen)
			{
				return;
			}
			// Check if auto-freeze is enabled
			bool autoFreezeEnabled = Project.ActiveProject != null && 
									 Project.ActiveProject.description.Prefs_FreezeAuto;
			
			// Check for auto-freeze if not already frozen
			bool skipDueToAutoFreeze = false;
			if (autoFreezeEnabled && chip.InputPins.Length > 0)
			{
				// Don't auto-freeze chips that need to update every frame
				bool chipRequiresConstantUpdates = 
					chip.ChipType == ChipType.Clock || 
					chip.ChipType == ChipType.Key ||
					ContainsChipThatRequiresConstantUpdates(chip);
				
				if (!chipRequiresConstantUpdates)
				{
					int freezeAutoTickRate = Project.ActiveProject.description.Prefs_FreezeAutoTickRate;
					
					// Get or create tracker for this chip
					if (!chipInputTrackers.TryGetValue(chip, out ChipInputTracker tracker))
					{
						tracker = new ChipInputTracker(chip.InputPins);
						chipInputTrackers[chip] = tracker;
					}
					
					// Check if inputs have changed
					bool inputsChanged = tracker.CheckAndUpdateInputs(chip.InputPins);
					
					// Skip processing if inputs haven't changed for the specified number of ticks
					if (!inputsChanged && tracker.UnchangedTickCount >= freezeAutoTickRate)
					{
						skipDueToAutoFreeze = true;
					}
				}
			}

			// Skip processing if auto-frozen
			if (skipDueToAutoFreeze)
			{
				// Still need to propagate outputs even if auto-frozen
				for (int i = chip.SubChips.Length - 1; i >= 0; i--)
				{
					chip.SubChips[i].Sim_PropagateOutputs();
				}
				return;
			}

			// NOTE: subchips are assumed to have been sorted in reverse order of desired visitation
			for (int i = chip.SubChips.Length - 1; i >= 0; i--)
			{
				SimChip nextSubChip = chip.SubChips[i];

				// Every n frames (for performance reasons) the simulation permits some random modifications to the chip traversal order.
				// Here two chips may be swapped if they are not 'ready' (i.e. all inputs have not yet been received for this
				// frame; indicating that the input relies on the output). The purpose of this reordering is to allow some variety in
				// the outcomes of race-conditions (such as an SR latch having both inputs enabled, and then released).
				if (canDynamicReorderThisFrame && i > 0 && !nextSubChip.Sim_IsReady() && RandomBool())
				{
					SimChip potentialSwapChip = chip.SubChips[i - 1];
					if (!ChipTypeHelper.IsBusOriginType(potentialSwapChip.ChipType))
					{
						nextSubChip = potentialSwapChip;
						(chip.SubChips[i], chip.SubChips[i - 1]) = (chip.SubChips[i - 1], chip.SubChips[i]);
					}
				}

				if (nextSubChip.IsBuiltin) ProcessBuiltinChip(nextSubChip); // We've reached a built-in chip, so process it directly
				else StepChip(nextSubChip); // Recursively process custom chip

				// Step 3) Forward the outputs of the processed subchip to connected pins
				nextSubChip.Sim_PropagateOutputs();
			}
		}

		// Recursively propagate signals through this chip and its subchips
		// In the process, reorder all subchips based on order in which they become ready for processing (have received all their inputs)
		// Note: the order here is reversed, so those ready first will be at the end of the array
		static void StepChipReorder(SimChip chip)
		{
			chip.Sim_PropagateInputs();

			SimChip[] subChips = chip.SubChips;
			int numRemaining = subChips.Length;

			while (numRemaining > 0)
			{
				int nextSubChipIndex = ChooseNextSubChip(subChips, numRemaining);
				SimChip nextSubChip = subChips[nextSubChipIndex];

				// "Remove" the chosen subchip from remaining sub chips.
				// This is done by moving it to the end of the array and reducing the length of the span by one.
				// This also places the subchip into (reverse) order, so that the traversal order need to be determined again on the next pass.
				(subChips[nextSubChipIndex], subChips[numRemaining - 1]) = (subChips[numRemaining - 1], subChips[nextSubChipIndex]);
				numRemaining--;

				// Process chosen subchip
				if (nextSubChip.ChipType == ChipType.Custom) StepChipReorder(nextSubChip); // Recursively process custom chip
				else ProcessBuiltinChip(nextSubChip); // We've reached a built-in chip, so process it directly 

				// Step 3) Forward the outputs of the processed subchip to connected pins
				// We still need to propagate outputs even if frozen, to maintain connections
				nextSubChip.Sim_PropagateOutputs();
			}
		}

		static int ChooseNextSubChip(SimChip[] subChips, int num)
		{
			bool noSubChipsReady = true;
			bool isNonBusChipRemaining = false;
			int nextSubChipIndex = -1;

			// Step 2) Loop over all subchips not yet processed this frame, and process them if they are ready
			for (int i = 0; i < num; i++)
			{
				SimChip subChip = subChips[i];
				if (subChip.Sim_IsReady())
				{
					noSubChipsReady = false;
					nextSubChipIndex = i;
					break;
				}

				isNonBusChipRemaining |= !ChipTypeHelper.IsBusOriginType(subChip.ChipType);
			}

			// Step 4) if no sub chip is ready to be processed, pick one at random (but save buses for last)
			if (noSubChipsReady)
			{
				nextSubChipIndex = rng.Next(0, num);

				// If processing in random order, save buses for last (since we must know all their inputs to display correctly)
				if (isNonBusChipRemaining)
				{
					for (int i = 0; i < num; i++)
					{
						if (!ChipTypeHelper.IsBusOriginType(subChips[nextSubChipIndex].ChipType)) break;
						nextSubChipIndex = (nextSubChipIndex + 1) % num;
					}
				}
			}

			return nextSubChipIndex;
		}

		public static bool RandomBool()
		{
			pcg_rngState = pcg_rngState * 747796405 + 2891336453;
			uint result = ((pcg_rngState >> (int)((pcg_rngState >> 28) + 4)) ^ pcg_rngState) * 277803737;
			result = (result >> 22) ^ result;
			return result < uint.MaxValue / 2;
		}

		static void ProcessBuiltinChip(SimChip chip)
		{
			switch (chip.ChipType)
			{
				// ---- Process Built-in chips ----
				case ChipType.Nand:
				{
					ulong nandOp = 1 ^ (chip.InputPins[0].State & chip.InputPins[1].State);
					chip.OutputPins[0].State = (ushort)(nandOp & 1);
					break;
				}				case ChipType.Clock:
				{
					bool high = stepsPerClockTransition != 0 && ((simulationFrame / stepsPerClockTransition) & 1) == 0;
					PinState.Set(ref chip.OutputPins[0].State, high ? PinState.LogicHigh : PinState.LogicLow);
					break;
				}
				case ChipType.Pulse:
				{
					const int pulseDurationIndex = 0;
					const int pulseTicksRemainingIndex = 1;
					const int pulseInputOldIndex = 2;

					ulong inputState = chip.InputPins[0].State;
					bool pulseInputHigh = PinState.FirstBitHigh(inputState);
					ulong pulseTicksRemaining = chip.InternalState[pulseTicksRemainingIndex];

					if (pulseTicksRemaining == 0)
					{
						bool isRisingEdge = pulseInputHigh && chip.InternalState[pulseInputOldIndex] == 0;
						if (isRisingEdge)
						{
							pulseTicksRemaining = chip.InternalState[pulseDurationIndex];
							chip.InternalState[pulseTicksRemainingIndex] = pulseTicksRemaining;
						}
					}

					ulong outputState = PinState.LogicLow;
					if (pulseTicksRemaining > 0)
					{
						chip.InternalState[1]--;
						outputState = PinState.LogicHigh;
					}
					else if (PinState.GetTristateFlags(inputState) != 0)
					{
						PinState.SetAllDisconnected(ref outputState);
					}

					chip.OutputPins[0].State = outputState;
					chip.InternalState[pulseInputOldIndex] = pulseInputHigh ? 1u : 0;

					break;
				}
				
				case ChipType.Split_4To1Bit:
				{
					ulong inState4Bit = chip.InputPins[0].State;
					chip.OutputPins[0].State = (inState4Bit >> 3) & PinState.SingleBitMask;
					chip.OutputPins[1].State = (inState4Bit >> 2) & PinState.SingleBitMask;
					chip.OutputPins[2].State = (inState4Bit >> 1) & PinState.SingleBitMask;
					chip.OutputPins[3].State = (inState4Bit >> 0) & PinState.SingleBitMask;
					break;
				}
				case ChipType.Merge_1To4Bit:
				{
					ulong stateA = chip.InputPins[3].State & PinState.SingleBitMask; // lsb
					ulong stateB = chip.InputPins[2].State & PinState.SingleBitMask;
					ulong stateC = chip.InputPins[1].State & PinState.SingleBitMask;
					ulong stateD = chip.InputPins[0].State & PinState.SingleBitMask;
					chip.OutputPins[0].State = stateA | stateB << 1 | stateC << 2 | stateD << 3;
					break;
				}
				case ChipType.Merge_1To8Bit:
				{
					ulong stateA = chip.InputPins[7].State & PinState.SingleBitMask; // lsb
					ulong stateB = chip.InputPins[6].State & PinState.SingleBitMask;
					ulong stateC = chip.InputPins[5].State & PinState.SingleBitMask;
					ulong stateD = chip.InputPins[4].State & PinState.SingleBitMask;
					ulong stateE = chip.InputPins[3].State & PinState.SingleBitMask;
					ulong stateF = chip.InputPins[2].State & PinState.SingleBitMask;
					ulong stateG = chip.InputPins[1].State & PinState.SingleBitMask;
					ulong stateH = chip.InputPins[0].State & PinState.SingleBitMask;
					chip.OutputPins[0].State = stateA | stateB << 1 | stateC << 2 | stateD << 3 | stateE << 4 | stateF << 5 | stateG << 6 | stateH << 7;
					break;
				}
				case ChipType.Merge_4To8Bit:
				{
					SimPin in4A = chip.InputPins[0];
					SimPin in4B = chip.InputPins[1];
					SimPin out8 = chip.OutputPins[0];
					PinState.Set8BitFrom4BitSources(ref out8.State, in4B.State, in4A.State);
					break;
				}
				case ChipType.Split_8To4Bit:
				{
					SimPin in8 = chip.InputPins[0];
					SimPin out4A = chip.OutputPins[0];
					SimPin out4B = chip.OutputPins[1];
					PinState.Set4BitFrom8BitSource(ref out4A.State, in8.State, false);
					PinState.Set4BitFrom8BitSource(ref out4B.State, in8.State, true);
					break;
				} 
				case ChipType.Split_8To1Bit:
				{
					ulong in8 = chip.InputPins[0].State;
					chip.OutputPins[0].State = (in8 >> 7) & PinState.SingleBitMask;
					chip.OutputPins[1].State = (in8 >> 6) & PinState.SingleBitMask;
					chip.OutputPins[2].State = (in8 >> 5) & PinState.SingleBitMask;
					chip.OutputPins[3].State = (in8 >> 4) & PinState.SingleBitMask;
					chip.OutputPins[4].State = (in8 >> 3) & PinState.SingleBitMask;
					chip.OutputPins[5].State = (in8 >> 2) & PinState.SingleBitMask;
					chip.OutputPins[6].State = (in8 >> 1) & PinState.SingleBitMask;
					chip.OutputPins[7].State = (in8 >> 0) & PinState.SingleBitMask;
					break;
				}
				case ChipType.TriStateBuffer:
				{
					SimPin dataPin = chip.InputPins[0];
					SimPin enablePin = chip.InputPins[1];
					SimPin outputPin = chip.OutputPins[0];

					if (PinState.FirstBitHigh(enablePin.State)) outputPin.State = dataPin.State;
					else PinState.SetAllDisconnected(ref outputPin.State);

					break;
				}
				case ChipType.Key:
				{
					// Get the key identifier from the chip's activation key string
					string keyIdentifier = "";
					
					// For backward compatibility, check if it's a character or a string
					if (chip.InternalState[0] < 128) // ASCII range
					{
						keyIdentifier = ((char)chip.InternalState[0]).ToString().ToUpper();
					}
					else
					{
						// For special keys, we need to look up the identifier from the hash
						// This is a simplified approach - you might need to store the actual string somewhere
						keyIdentifier = SimKeyboardHelper.GetKeyIdentifierFromHash((int)chip.InternalState[0]);
					}
					
					// Use the string-based KeyIsHeld method
					bool isHeld = SimKeyboardHelper.KeyIsHeld(keyIdentifier);					
					// Set output pin state based on whether key is held
					chip.OutputPins[0].State = isHeld ? PinState.LogicHigh : PinState.LogicLow;
					break;
				}

				case ChipType.DisplayRGB:
				{
					const ulong addressSpace = 256;
					ulong addressPin = chip.InputPins[0].State;
					ulong redPin = chip.InputPins[1].State;
					ulong greenPin = chip.InputPins[2].State;
					ulong bluePin = chip.InputPins[3].State;
					ulong resetPin = chip.InputPins[4].State;
					ulong writePin = chip.InputPins[5].State;
					ulong refreshPin = chip.InputPins[6].State;
					ulong clockPin = chip.InputPins[7].State;

					// Detect clock rising edge
					bool clockHigh = PinState.FirstBitHigh(clockPin);
					bool isRisingEdge = clockHigh && chip.InternalState[^1] == 0;
					chip.InternalState[^1] = clockHigh ? 1u : 0;

					if (isRisingEdge)
					{
						// Clear back buffer
						if (PinState.FirstBitHigh(resetPin))
						{
							for (ulong i = 0; i < addressSpace; i++)
							{
								// Make sure we don't go out of bounds
								if (i + addressSpace < (ulong)chip.InternalState.Length)
								{
									chip.InternalState[i + addressSpace] = 0;
								}
							}
						}
						// Write to back-buffer
						else if (PinState.FirstBitHigh(writePin))
						{
							ulong address = PinState.GetBitStates(addressPin);
							// Ensure address is within valid range
							address = address % addressSpace; // Wrap around if too large
							ulong addressIndex = address + addressSpace;
							
							// Check if the calculated index is within bounds
							if (addressIndex < (ulong)chip.InternalState.Length)
							{
								ulong data = (PinState.GetBitStates(redPin) | 
											 (PinState.GetBitStates(greenPin) << 4) | 
											 (PinState.GetBitStates(bluePin) << 8));
								chip.InternalState[addressIndex] = data;
							}
						}

						// Copy back-buffer to display buffer
						if (PinState.FirstBitHigh(refreshPin))
						{
							for (ulong i = 0; i < addressSpace; i++)
							{
								// Make sure both indices are within bounds
								if (i < (ulong)chip.InternalState.Length && 
									i + addressSpace < (ulong)chip.InternalState.Length)
								{
									chip.InternalState[i] = chip.InternalState[i + addressSpace];
								}
							}
						}
					}

					// Output current pixel colour
					// Before accessing the array, add bounds checking
					ulong addressValue = PinState.GetBitStates(addressPin);
					// Ensure address is within valid range
					addressValue = addressValue % addressSpace; // Wrap around if too large
					
					if (addressValue < (ulong)chip.InternalState.Length)
					{
						ulong colData = chip.InternalState[addressValue];
						chip.OutputPins[0].State = (ushort)((colData >> 0) & 0b1111); // red
						chip.OutputPins[1].State = (ushort)((colData >> 4) & 0b1111); // green
						chip.OutputPins[2].State = (ushort)((colData >> 8) & 0b1111); // blue
					}
					else
					{
						// Handle out-of-bounds access with default values
						chip.OutputPins[0].State = 0; // red
						chip.OutputPins[1].State = 0; // green
						chip.OutputPins[2].State = 0; // blue
					}
					break;
				}
				case ChipType.Display1080RGB:
				{
					const uint pixelCount = 1080 * 1920;
					ulong addressPin = chip.InputPins[0].State;
					ulong redPin = chip.InputPins[1].State;
					ulong greenPin = chip.InputPins[2].State;
					ulong bluePin = chip.InputPins[3].State;
					ulong resetPin = chip.InputPins[4].State;
					ulong writePin = chip.InputPins[5].State;
					ulong refreshPin = chip.InputPins[6].State;
					ulong clockPin = chip.InputPins[7].State;

					// Detect clock rising edge
					bool clockHigh = PinState.FirstBitHigh(clockPin);
					bool isRisingEdge = clockHigh && chip.InternalState[^1] == 0;
					chip.InternalState[^1] = clockHigh ? 1u : 0;

					if (isRisingEdge)
					{
						// Clear back buffer
						if (PinState.FirstBitHigh(resetPin))
						{
							Random rnd = new Random();
							for (int i = 0; i < pixelCount; i++)
							{
									chip.InternalState[i] = 0;
							}
						}
						// Write to back-buffer
						else if (PinState.FirstBitHigh(writePin))
						{
							// Get the full 32-bit address value
							ulong addressIndex = PinState.GetBitStates(addressPin);
							
							// Ensure address is within bounds
							if (addressIndex < pixelCount)
							{
								// Combine 8-bit RGB values into a 24-bit color value
								ulong data = (uint)(
									PinState.GetBitStates(redPin) | 
									(PinState.GetBitStates(greenPin) << 8) | 
									(PinState.GetBitStates(bluePin) << 16)
								);
								chip.InternalState[addressIndex] = data;
							}
						}

						// Copy back-buffer to display buffer
						if (PinState.FirstBitHigh(refreshPin))
						{
							for (int i = 0; i < pixelCount; i++)
							{
								chip.InternalState[i] = chip.InternalState[i];
							}
						}
					}
					
					// Output current pixel colour
					ulong addressValue = PinState.GetBitStates(addressPin);
					ulong colData = 0;
					if (addressValue < pixelCount)
					{
						colData = chip.InternalState[addressValue];
					}
					
					// Output 8-bit color values
					chip.OutputPins[0].State = (ushort)((colData >> 0) & 0xFF);  // red (8 bits)
					chip.OutputPins[1].State = (ushort)((colData >> 8) & 0xFF);  // green (8 bits)
					chip.OutputPins[2].State = (ushort)((colData >> 16) & 0xFF); // blue (8 bits)

					break;
				}
				case ChipType.DisplayDot:
				{
					const uint addressSpace = 256;
					ulong addressPin = chip.InputPins[0].State;
					ulong pixelInputPin = chip.InputPins[1].State;
					ulong resetPin = chip.InputPins[2].State;
					ulong writePin = chip.InputPins[3].State;
					ulong refreshPin = chip.InputPins[4].State;
					ulong clockPin = chip.InputPins[5].State;

					// Detect clock rising edge
					bool clockHigh = PinState.FirstBitHigh(clockPin);
					bool isRisingEdge = clockHigh && chip.InternalState[^1] == 0;
					chip.InternalState[^1] = clockHigh ? 1u : 0;

					if (isRisingEdge)
					{
						// Clear back buffer
						if (PinState.FirstBitHigh(resetPin))
						{
							for (int i = 0; i < addressSpace; i++)
							{
								chip.InternalState[i + addressSpace] = 0;
							}
						}
						// Write to back-buffer
						else if (PinState.FirstBitHigh(writePin))
						{
							ulong addressIndex = PinState.GetBitStates(addressPin) + addressSpace;
							ulong data = PinState.GetBitStates(pixelInputPin);
							chip.InternalState[addressIndex] = data;
						}

						// Copy back-buffer to display buffer
						if (PinState.FirstBitHigh(refreshPin))
						{
							for (int i = 0; i < addressSpace; i++)
							{
								chip.InternalState[i] = chip.InternalState[i + addressSpace];
							}
						}
					}

					// Output current pixel colour
					ushort pixelState = (ushort)chip.InternalState[PinState.GetBitStates(addressPin)];
					chip.OutputPins[0].State = pixelState;

					break;
				}
				case ChipType.dev_Ram_8Bit:
				{
					ulong addressPin = chip.InputPins[0].State;
					ulong dataPin = chip.InputPins[1].State;
					ulong writeEnablePin = chip.InputPins[2].State;
					ulong resetPin = chip.InputPins[3].State;
					ulong clockPin = chip.InputPins[4].State;

					// Detect clock rising edge
					bool clockHigh = PinState.FirstBitHigh(clockPin);
					bool isRisingEdge = clockHigh && chip.InternalState[^1] == 0;
					chip.InternalState[^1] = clockHigh ? 1u : 0;

					// Write/Reset on rising edge
					if (isRisingEdge)
					{
						if (PinState.FirstBitHigh(resetPin))
						{
							for (int i = 0; i < 256; i++)
							{
								chip.InternalState[i] = 0;
							}
						}
						else if (PinState.FirstBitHigh(writeEnablePin))
						{
							chip.InternalState[PinState.GetBitStates(addressPin)] = PinState.GetBitStates(dataPin);
						}
					}

					// Output data at current address
					chip.OutputPins[0].State = (ushort)chip.InternalState[PinState.GetBitStates(addressPin)];

					break;
				}
				case ChipType.Rom_256x16:
				{
					// Ensure address is within valid range (0-29999)
					ulong address = PinState.GetBitStates(chip.InputPins[0].State) & 0xFFFF; // Limit to 16 bits
					
					// Safely access internal state
					ulong highBits = 0, lowBits = 0;
					if (address < 30000 && address * 4 + 3 < (ulong)chip.InternalState.Length)
					{
						// Each row is 4 uint values (128 bits total)
						highBits = ((ulong)chip.InternalState[address * 4] << 32) | (ulong)chip.InternalState[address * 4 + 1];
						lowBits = ((ulong)chip.InternalState[address * 4 + 2] << 32) | (ulong)chip.InternalState[address * 4 + 3];
					}
					
					// Output the 128 bits as 2x 64-bit values
					chip.OutputPins[0].State = highBits; // Most significant 64 bits
					chip.OutputPins[1].State = lowBits;  // Least significant 64 bits
					break;
				}
				// Bulit in stuff
				case ChipType.EdgeFunction:
				{
					// Get input values (all 4-bit)
					ulong x = PinState.GetBitStates(chip.InputPins[5].State);   // X
					ulong y = PinState.GetBitStates(chip.InputPins[4].State);   // Y
					ulong ax = PinState.GetBitStates(chip.InputPins[3].State);  // AX
					ulong ay = PinState.GetBitStates(chip.InputPins[2].State);  // AY
					ulong bx = PinState.GetBitStates(chip.InputPins[1].State);  // BX
					ulong by = PinState.GetBitStates(chip.InputPins[0].State);  // BY
					
					// Calculate E1(x, y) = (x - Ax) * (By - Ay) - (y - Ay) * (Bx - Ax)
					ulong term1 = (x - ax) * (by - ay);
					ulong term2 = (y - ay) * (bx - ax);
					ulong result = term1 - term2;

					// Convert result to 16-bit unsigned (with proper wrapping)
					ulong output = result & 0xFFFF;
					
					chip.OutputPins[0].State = output;
					break;
				}
				case ChipType.ColorInterpolationMath:
				{
					// Get input values based on the updated pin layout from CreateColorInterpolationMath
					ulong bcp = PinState.GetBitStates(chip.InputPins[0].State);  // BCP (16-bit) - pin 1
					ulong cap = PinState.GetBitStates(chip.InputPins[1].State);  // CAP (16-bit) - pin 2
					ulong abp = PinState.GetBitStates(chip.InputPins[2].State);  // ABP (16-bit) - pin 3
					ulong bx = PinState.GetBitStates(chip.InputPins[3].State);   // BX (4-bit) - pin 4
					ulong ax = PinState.GetBitStates(chip.InputPins[4].State);   // AX (4-bit) - pin 5
					ulong cy = PinState.GetBitStates(chip.InputPins[5].State);   // CY (4-bit) - pin 6
					ulong ay = PinState.GetBitStates(chip.InputPins[6].State);   // Ay (4-bit) - pin 7
					ulong by = PinState.GetBitStates(chip.InputPins[7].State);   // BY (4-bit) - pin 8
					ulong cx = PinState.GetBitStates(chip.InputPins[8].State);   // CX (4-bit) - pin 9
					
					// Color components for each vertex
					ulong colourAR = PinState.GetBitStates(chip.InputPins[9].State);  // colorAR (4-bit) - pin 10
					ulong colourAG = PinState.GetBitStates(chip.InputPins[10].State); // colorAG (4-bit) - pin 11
					ulong colourAB = PinState.GetBitStates(chip.InputPins[11].State); // colorAB (4-bit) - pin 12
					ulong colourBR = PinState.GetBitStates(chip.InputPins[12].State); // colorBR (4-bit) - pin 13
					ulong colourBG = PinState.GetBitStates(chip.InputPins[13].State); // colorBG (4-bit) - pin 14
					ulong colourBB = PinState.GetBitStates(chip.InputPins[14].State); // colorBB (4-bit) - pin 15
					ulong colourCR = PinState.GetBitStates(chip.InputPins[15].State); // colorCR (4-bit) - pin 16
					ulong colourCG = PinState.GetBitStates(chip.InputPins[16].State); // colorCG (4-bit) - pin 17
					ulong colourCB = PinState.GetBitStates(chip.InputPins[17].State); // colorCB (4-bit) - pin 18

					// =============================
					// Step 1: compute signed area of ABC (twice the area)
					// (Exactly as you had it:)
					long term1 = (long)((bx - ax) * (cy - ay));
					long term2 = (long)((by - ay) * (cx - ax));
					long abcSigned = term1 - term2;

					// Step 2: take absolute value for denominator so weights always add to +1
					double abc = Math.Abs((double)abcSigned);

					// Step 3: bail out if the triangle is degenerate (area = 0)
					double redF = 0, greenF = 0, blueF = 0;
					if (abc > 0.0)
					{
						// Compute floating‐point barycentric “areas” from your 16-bit inputs:
						// BCP/ABC → weight for A, etc.
						double weightA = (double)(long)bcp / abc;
						double weightB = (double)(long)cap / abc;
						double weightC = (double)(long)abp / abc;

						// Now interpolate each channel in floating point
						// colourAR, colourAG, colourAB, etc. are 0…15
						redF   = (double)(long)colourAR * weightA
							+ (double)(long)colourBR * weightB
							+ (double)(long)colourCR * weightC;

						greenF = (double)(long)colourAG * weightA
							+ (double)(long)colourBG * weightB
							+ (double)(long)colourCG * weightC;

						blueF  = (double)(long)colourAB * weightA
							+ (double)(long)colourBB * weightB
							+ (double)(long)colourCB * weightC;
					}

					// Step 4: clamp each channel to [0,15] and convert back to ulong
					// Optional: round to nearest integer before clamping
					ulong red   = (ulong)Math.Max(0, Math.Min(15, (int)Math.Round(redF)));
					ulong green = (ulong)Math.Max(0, Math.Min(15, (int)Math.Round(greenF)));
					ulong blue  = (ulong)Math.Max(0, Math.Min(15, (int)Math.Round(blueF)));

					// Step 5: write to output pins (still 4-bit)
					// note: I kept the same output-pin ordering you had
					chip.OutputPins[0].State = red;   // RED (4-bit)  - pin 15
					chip.OutputPins[1].State = green; // GREEN (4-bit) - pin 14
					chip.OutputPins[2].State = blue;  // BLUE (4-bit)  - pin 13
					break;
				}
				case ChipType.Merge_1To16Bit:
				{
					ulong result = 0;
					for (int i = 0; i < 16; i++)
					{
						ulong bitState = chip.InputPins[15 - i].State & PinState.SingleBitMask;
						result |= bitState << i;
					}
					chip.OutputPins[0].State = result;
					break;
				}
				case ChipType.Split_16To1Bit:
				{
					ulong in16 = chip.InputPins[0].State;
					for (int i = 0; i < 16; i++)
					{
						chip.OutputPins[i].State = (in16 >> (15 - i)) & PinState.SingleBitMask;
					}
					break;
				}
				case ChipType.Merge_4To16Bit:
				{
					SimPin in4A = chip.InputPins[0]; // Most significant 4 bits
					SimPin in4B = chip.InputPins[1];
					SimPin in4C = chip.InputPins[2];
					SimPin in4D = chip.InputPins[3]; // Least significant 4 bits
					SimPin out16 = chip.OutputPins[0];
					
					ulong result = 0;
					result |= (in4A.State & 0xF) << 12;
					result |= (in4B.State & 0xF) << 8;
					result |= (in4C.State & 0xF) << 4;
					result |= (in4D.State & 0xF);
					
					out16.State = result;
					break;
				}
				case ChipType.Split_16To4Bit:
				{
					SimPin in16 = chip.InputPins[0];
					SimPin out4A = chip.OutputPins[0]; // Most significant 4 bits
					SimPin out4B = chip.OutputPins[1];
					SimPin out4C = chip.OutputPins[2];
					SimPin out4D = chip.OutputPins[3]; // Least significant 4 bits
					
					ulong value = in16.State;
					out4A.State = (value >> 12) & 0xF;
					out4B.State = (value >> 8) & 0xF;
					out4C.State = (value >> 4) & 0xF;
					out4D.State = value & 0xF;
					break;
				}
				case ChipType.Merge_8To16Bit:
				{
					SimPin in8A = chip.InputPins[0]; // Most significant byte
					SimPin in8B = chip.InputPins[1]; // Least significant byte
					SimPin out16 = chip.OutputPins[0];
					
					ulong result = 0;
					result |= (in8A.State & 0xFF) << 8;
					result |= (in8B.State & 0xFF);
					
					out16.State = result;
					break;
				}
				case ChipType.Split_16To8Bit:
				{
					SimPin in16 = chip.InputPins[0];
					SimPin out8A = chip.OutputPins[0]; // Most significant byte
					SimPin out8B = chip.OutputPins[1]; // Least significant byte
					
					ulong value = in16.State;
					out8A.State = (value >> 8) & 0xFF;
					out8B.State = value & 0xFF;
					break;
				}

				case ChipType.Merge_1To32Bit:
				{
					ulong result = 0;
					for (int i = 0; i < 32; i++)
					{
						// Use safe bit shifting to avoid overflow
						ulong bitState = chip.InputPins[31 - i].State & PinState.SingleBitMask;
						if (i < 31) // Ensure we don't shift beyond 31 bits for a 32-bit value
						{
							result |= bitState << i;
						}
						else
						{
							result |= bitState << 31; // Cap at 31 for the highest bit
						}
					}
					chip.OutputPins[0].State = result;
					break;
				}
				case ChipType.Split_32To1Bit:
				{
					ulong in32 = chip.InputPins[0].State;
					for (int i = 0; i < 32; i++)
					{
						chip.OutputPins[i].State = (in32 >> (31 - i)) & PinState.SingleBitMask;
					}
					break;
				}
				case ChipType.Merge_8To32Bit:
				{
					SimPin in8A = chip.InputPins[0]; // Most significant byte
					SimPin in8B = chip.InputPins[1];
					SimPin in8C = chip.InputPins[2];
					SimPin in8D = chip.InputPins[3]; // Least significant byte
					SimPin out32 = chip.OutputPins[0];
					
					ulong result = 0;
					result |= (in8A.State & 0xFF) << 24;
					result |= (in8B.State & 0xFF) << 16;
					result |= (in8C.State & 0xFF) << 8;
					result |= (in8D.State & 0xFF);
					
					out32.State = result;
					break;
				}
				case ChipType.Split_32To8Bit:
				{
					SimPin in32 = chip.InputPins[0];
					SimPin out8A = chip.OutputPins[0]; // Most significant byte
					SimPin out8B = chip.OutputPins[1];
					SimPin out8C = chip.OutputPins[2];
					SimPin out8D = chip.OutputPins[3]; // Least significant byte
					
					ulong value = in32.State;
					out8A.State = (value >> 24) & 0xFF;
					out8B.State = (value >> 16) & 0xFF;
					out8C.State = (value >> 8) & 0xFF;
					out8D.State = value & 0xFF;
					break;
				}
				case ChipType.Merge_4To32Bit:
				{
					SimPin in4A = chip.InputPins[0]; // Most significant 4 bits
					SimPin in4B = chip.InputPins[1];
					SimPin in4C = chip.InputPins[2];
					SimPin in4D = chip.InputPins[3];
					SimPin in4E = chip.InputPins[4];
					SimPin in4F = chip.InputPins[5];
					SimPin in4G = chip.InputPins[6];
					SimPin in4H = chip.InputPins[7]; // Least significant 4 bits
					SimPin out32 = chip.OutputPins[0];
					
					ulong result = 0;
					result |= (in4A.State & 0xF) << 28;
					result |= (in4B.State & 0xF) << 24;
					result |= (in4C.State & 0xF) << 20;
					result |= (in4D.State & 0xF) << 16;
					result |= (in4E.State & 0xF) << 12;
					result |= (in4F.State & 0xF) << 8;
					result |= (in4G.State & 0xF) << 4;
					result |= (in4H.State & 0xF);
					
					out32.State = result;
					break;
				}
				case ChipType.Split_32To4Bit:
				{
					SimPin in32 = chip.InputPins[0];
					SimPin out4A = chip.OutputPins[0]; // Most significant 4 bits
					SimPin out4B = chip.OutputPins[1];
					SimPin out4C = chip.OutputPins[2];
					SimPin out4D = chip.OutputPins[3];
					SimPin out4E = chip.OutputPins[4];
					SimPin out4F = chip.OutputPins[5];
					SimPin out4G = chip.OutputPins[6];
					SimPin out4H = chip.OutputPins[7]; // Least significant 4 bits
					
					ulong value = in32.State;
					out4A.State = (value >> 28) & 0xF;
					out4B.State = (value >> 24) & 0xF;
					out4C.State = (value >> 20) & 0xF;
					out4D.State = (value >> 16) & 0xF;
					out4E.State = (value >> 12) & 0xF;
					out4F.State = (value >> 8) & 0xF;
					out4G.State = (value >> 4) & 0xF;
					out4H.State = value & 0xF;
					break;
				}
				case ChipType.Merge_1To64Bit:
				{
					ulong result = 0;
					for (int i = 0; i < 64; i++)
					{
						ulong bitState = chip.InputPins[63 - i].State & PinState.SingleBitMask;
						// For 64-bit values, we need to be careful with shifts near the max value
						if (i < 63) // Safe shift for bits 0-62
						{
							result |= bitState << i;
						}
						else
						{
							// For the highest bit (63), use a different approach to avoid overflow
							if (bitState > 0)
							{
								result |= 0x8000000000000000; // Set the highest bit directly
							}
						}
					}
					chip.OutputPins[0].State = result;
					break;
				}
				case ChipType.Split_64To1Bit:
				{
					ulong in64 = chip.InputPins[0].State;
					for (int i = 0; i < 64; i++)
					{
						chip.OutputPins[i].State = (in64 >> (63 - i)) & PinState.SingleBitMask;
					}
					break;
				}
				case ChipType.Merge_8To64Bit:
				{
					ulong result = 0;
					for (int i = 0; i < 8; i++)
					{
						ulong byteState = chip.InputPins[i].State & 0xFF;
						int shiftAmount = (7 - i) * 8;
						// Ensure we don't shift beyond what's safe for ulong
						if (shiftAmount < 64)
						{
							result |= byteState << shiftAmount;
						}
					}
					chip.OutputPins[0].State = result;
					break;
				}
				case ChipType.Split_64To8Bit:
				{
					ulong in64 = chip.InputPins[0].State;
					for (int i = 0; i < 8; i++)
					{
						chip.OutputPins[i].State = (in64 >> ((7 - i) * 8)) & 0xFF;
					}
					break;
				}
				case ChipType.Merge_32To64Bit:
				{
					SimPin in32A = chip.InputPins[0]; // Most significant 32 bits
					SimPin in32B = chip.InputPins[1]; // Least significant 32 bits
					SimPin out64 = chip.OutputPins[0];
					
					ulong result = 0;
					// Cast to ulong before shifting to avoid overflow
					result |= ((ulong)(in32A.State & 0xFFFFFFFF)) << 32;
					result |= (in32B.State & 0xFFFFFFFF);
					
					out64.State = result;
					break;
				}
				case ChipType.Split_64To32Bit:
				{
					SimPin in64 = chip.InputPins[0];
					SimPin out32A = chip.OutputPins[0]; // Most significant 32 bits
					SimPin out32B = chip.OutputPins[1]; // Least significant 32 bits
					
					ulong value = in64.State;
					out32A.State = (value >> 32) & 0xFFFFFFFF;
					out32B.State = value & 0xFFFFFFFF;
					break;
				}
				case ChipType.Merge_16To32Bit:
				{
					SimPin in16A = chip.InputPins[0]; // Most significant 16 bits
					SimPin in16B = chip.InputPins[1]; // Least significant 16 bits
					SimPin out32 = chip.OutputPins[0];
					
					ulong result = 0;
					result |= (in16A.State & 0xFFFF) << 16;
					result |= (in16B.State & 0xFFFF);
					
					out32.State = result;
					break;
				}
				case ChipType.Split_32To16Bit:
				{
					SimPin in32 = chip.InputPins[0];
					SimPin out16A = chip.OutputPins[0]; // Most significant 16 bits
					SimPin out16B = chip.OutputPins[1]; // Least significant 16 bits
					
					ulong value = in32.State;
					out16A.State = (value >> 16) & 0xFFFF;
					out16B.State = value & 0xFFFF;
					break;
				}
				case ChipType.Merge_16To64Bit:
				{
					ulong result = 0;
					for (int i = 0; i < 4; i++)
					{
						ulong wordState = chip.InputPins[i].State & 0xFFFF;
						int shiftAmount = (3 - i) * 16;
						// Ensure we don't shift beyond what's safe for ulong
						if (shiftAmount < 64)
						{
							result |= wordState << shiftAmount;
						}
					}
					chip.OutputPins[0].State = result;
					break;
				}
				case ChipType.Split_64To16Bit:
				{
					ulong in64 = chip.InputPins[0].State;
					for (int i = 0; i < 4; i++)
					{
						chip.OutputPins[i].State = (in64 >> ((3 - i) * 16)) & 0xFFFF;
					}
					break;
				}
				// ---- Bus types ----
				default:
				{
					if (ChipTypeHelper.IsBusOriginType(chip.ChipType))
					{
						SimPin inputPin = chip.InputPins[0];
						PinState.Set(ref chip.OutputPins[0].State, inputPin.State);
					}

					break;
				}
			}
		}

		public static SimChip BuildSimChip(ChipDescription chipDesc, ChipLibrary library)
		{
			return BuildSimChip(chipDesc, library, -1, null);
		}

		public static SimChip BuildSimChip(ChipDescription chipDesc, ChipLibrary library, int subChipID, uint[] internalState)
		{
			SimChip simChip = BuildSimChipRecursive(chipDesc, library, subChipID, internalState);
			return simChip;
		}

		// Recursively build full representation of chip from its description for simulation.
		static SimChip BuildSimChipRecursive(ChipDescription chipDesc, ChipLibrary library, int subChipID, uint[] internalState)
		{
			// Recursively create subchips
			SimChip[] subchips = chipDesc.SubChips.Length == 0 ? Array.Empty<SimChip>() : new SimChip[chipDesc.SubChips.Length];

			for (int i = 0; i < chipDesc.SubChips.Length; i++)
			{
				SubChipDescription subchipDesc = chipDesc.SubChips[i];
				ChipDescription subchipFullDesc = library.GetChipDescription(subchipDesc.Name);
				SimChip subChip = BuildSimChipRecursive(subchipFullDesc, library, subchipDesc.ID, subchipDesc.InternalData);
				subchips[i] = subChip;
			}

			SimChip simChip = new(chipDesc, subChipID, internalState, subchips);


			// Create connections
			for (int i = 0; i < chipDesc.Wires.Length; i++)
			{
				simChip.AddConnection(chipDesc.Wires[i].SourcePinAddress, chipDesc.Wires[i].TargetPinAddress);
			}

			return simChip;
		}

		public static void AddPin(SimChip simChip, int pinID, bool isInputPin)
		{
			SimModifyCommand command = new()
			{
				type = SimModifyCommand.ModificationType.AddPin,
				modifyTarget = simChip,
				simPinToAdd = new SimPin(pinID, isInputPin, simChip),
				pinIsInputPin = isInputPin
			};
			modificationQueue.Enqueue(command);
		}

		public static void RemovePin(SimChip simChip, int pinID)
		{
			SimModifyCommand command = new()
			{
				type = SimModifyCommand.ModificationType.RemovePin,
				modifyTarget = simChip,
				removePinID = pinID
			};
			modificationQueue.Enqueue(command);
		}

		public static void AddSubChip(SimChip simChip, ChipDescription desc, ChipLibrary chipLibrary, int subChipID, uint[] subChipInternalData)
		{
			SimModifyCommand command = new()
			{
				type = SimModifyCommand.ModificationType.AddSubchip,
				modifyTarget = simChip,
				chipDesc = desc,
				lib = chipLibrary,
				subChipID = subChipID,
				subChipInternalData = subChipInternalData
			};
			modificationQueue.Enqueue(command);
		}

		public static void AddConnection(SimChip simChip, PinAddress source, PinAddress target)
		{
			SimModifyCommand command = new()
			{
				type = SimModifyCommand.ModificationType.AddConnection,
				modifyTarget = simChip,
				sourcePinAddress = source,
				targetPinAddress = target
			};
			modificationQueue.Enqueue(command);
		}

		public static void RemoveConnection(SimChip simChip, PinAddress source, PinAddress target)
		{
			SimModifyCommand command = new()
			{
				type = SimModifyCommand.ModificationType.RemoveConnection,
				modifyTarget = simChip,
				sourcePinAddress = source,
				targetPinAddress = target
			};
			modificationQueue.Enqueue(command);
		}

		public static void RemoveSubChip(SimChip simChip, int id)
		{
			SimModifyCommand command = new()
			{
				type = SimModifyCommand.ModificationType.RemoveSubChip,
				modifyTarget = simChip,
				removeSubChipID = id
			};
			modificationQueue.Enqueue(command);
		}

		// Note: this should only be called from the sim thread
		public static void ApplyModifications()
		{
			while (modificationQueue.Count > 0)
			{
				needsOrderPass = true;

				if (modificationQueue.TryDequeue(out SimModifyCommand cmd))
				{
					if (cmd.type == SimModifyCommand.ModificationType.AddSubchip)
					{
						SimChip newSubChip = BuildSimChip(cmd.chipDesc, cmd.lib, cmd.subChipID, cmd.subChipInternalData);
						cmd.modifyTarget.AddSubChip(newSubChip);
					}
					else if (cmd.type == SimModifyCommand.ModificationType.RemoveSubChip)
					{
						cmd.modifyTarget.RemoveSubChip(cmd.removeSubChipID);
					}
					else if (cmd.type == SimModifyCommand.ModificationType.AddConnection)
					{
						cmd.modifyTarget.AddConnection(cmd.sourcePinAddress, cmd.targetPinAddress);
					}
					else if (cmd.type == SimModifyCommand.ModificationType.RemoveConnection)
					{
						cmd.modifyTarget.RemoveConnection(cmd.sourcePinAddress, cmd.targetPinAddress); //
					}
					else if (cmd.type == SimModifyCommand.ModificationType.AddPin)
					{
						cmd.modifyTarget.AddPin(cmd.simPinToAdd, cmd.pinIsInputPin);
					}
					else if (cmd.type == SimModifyCommand.ModificationType.RemovePin)
					{
						cmd.modifyTarget.RemovePin(cmd.removePinID);
					}
				}
			}
		}

		public static void Reset()
		{
			simulationFrame = 0;
			modificationQueue?.Clear();
		}

		struct SimModifyCommand
		{
			public enum ModificationType
			{
				AddSubchip,
				RemoveSubChip,
				AddConnection,
				RemoveConnection,
				AddPin,
				RemovePin
			}

			public ModificationType type;
			public SimChip modifyTarget;
			public ChipDescription chipDesc;
			public ChipLibrary lib;
			public int subChipID;
			public uint[] subChipInternalData;
			public PinAddress sourcePinAddress;
			public PinAddress targetPinAddress;
			public SimPin simPinToAdd;
			public bool pinIsInputPin;
			public int removePinID;
			public int removeSubChipID;
		}

		// Check if a chip contains any subchips that require constant updates
		static bool ContainsChipThatRequiresConstantUpdates(SimChip chip)
		{
			// For custom chips, check all subchips recursively
			if (chip.ChipType == ChipType.Custom)
			{
				foreach (SimChip subChip in chip.SubChips)
				{
					if (subChip.ChipType == ChipType.Clock || 
						subChip.ChipType == ChipType.Key ||
						ContainsChipThatRequiresConstantUpdates(subChip))
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}
