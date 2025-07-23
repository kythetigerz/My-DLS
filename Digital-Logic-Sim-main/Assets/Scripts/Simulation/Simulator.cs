using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DLS.Description;
using DLS.Game;
using UnityEngine;
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

                public static void UpdateInputFromMainThread()
                {
                        SimKeyboardHelper.RefreshInputState();
                        SimMouseHelper.RefreshInputState();
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
                                                chip.ChipType == ChipType.Keyboard ||
                                                chip.ChipType == ChipType.Mouse ||
                                                chip.ChipType == ChipType.B_CPU ||
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
                                }                               case ChipType.Clock:
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
                                
                                case ChipType.Keyboard:
                                {
                                        // Create a 64-bit output value
                                        ulong outputValue = 0;
                                        
                                        // Track how many keys we've found (max 7)
                                        int keyCount = 0;
                                        
                                        // Check all valid input keys
                                        for (int i = 0; i < SimKeyboardHelper.ValidInputKeys.Length; i++)
                                        {
                                                KeyCode key = SimKeyboardHelper.ValidInputKeys[i];
                                                
                                                // Skip mouse keys for the keyboard chip
                                                if ((int)key >= (int)KeyCode.Mouse0 && (int)key <= (int)KeyCode.Mouse6)
                                                        continue;
                                                        
                                                // Check if key is held using SimKeyboardHelper
                                                string keyIdentifier = SimKeyboardHelper.GetKeyIdentifier(key);
                                                if (SimKeyboardHelper.KeyIsHeld(keyIdentifier) && keyCount < 7)
                                                {
                                                        // Get a unique byte code for this key (1-255, 0 means no key)
                                                        // Use the index in the ValidInputKeys array + 1 (to avoid 0)
                                                        byte keyCode = (byte)((i % 255) + 1);
                                                        
                                                        // Place the key code in the appropriate byte position
                                                        // Bytes are ordered from least significant to most significant
                                                        // First byte (LSB) is reserved for control flags
                                                        outputValue |= ((ulong)keyCode << (8 * (keyCount + 1)));
                                                        
                                                        keyCount++;
                                                }
                                        }
                                        
                                        // Set the control byte (first byte)
                                        // 255 = don't store, 0 = store
                                        byte controlByte = (byte)(keyCount > 0 ? 0 : 255);
                                        outputValue |= controlByte;
                                        
                                        // Set the output pin state
                                        chip.OutputPins[0].State = outputValue;
                                        break;
                                }
                                
                                case ChipType.Mouse:
                                {
                                        // Get mouse state as a byte
                                        byte mouseState = SimMouseHelper.GetMouseStateByte();
                                        
                                        // Set the output pin state
                                        chip.OutputPins[0].State = mouseState;
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
                                        const int ByteMask = 0b11111111;
                                        uint address = (uint)PinState.GetBitStates(chip.InputPins[0].State);
                                        uint data = (uint)chip.InternalState[address];
                                        chip.OutputPins[0].State = (ushort)((data >> 8) & ByteMask);
                                        chip.OutputPins[1].State = (ushort)(data & ByteMask);
                                        break;
                                }
                                case ChipType.Stack_8Bit:
                                {
                                        ulong dataPin = chip.InputPins[0].State;
                                        ulong pushPin = chip.InputPins[1].State;
                                        ulong popPin = chip.InputPins[2].State;
                                        ulong clockPin = chip.InputPins[3].State;

                                        // Detect clock rising edge
                                        bool clockHigh = PinState.FirstBitHigh(clockPin);
                                        bool isRisingEdge = clockHigh && chip.InternalState[257] == 0;
                                        chip.InternalState[257] = clockHigh ? 1u : 0;

                                        // Stack pointer is stored in InternalState[256] (index 0-255 are stack registers)
                                        // Stack grows upward, so SP points to next available slot
                                        if (isRisingEdge)
                                        {
                                                bool pushHigh = PinState.FirstBitHigh(pushPin);
                                                bool popHigh = PinState.FirstBitHigh(popPin);
                                                
                                                if (pushHigh && !popHigh)
                                                {
                                                        // Push operation - only if stack is not full
                                                        if (chip.InternalState[256] < 256)
                                                        {
                                                                chip.InternalState[chip.InternalState[256]] = PinState.GetBitStates(dataPin);
                                                                chip.InternalState[256]++; // Increment stack pointer
                                                        }
                                                }
                                                else if (popHigh && !pushHigh)
                                                {
                                                        // Pop operation - only if stack is not empty
                                                        if (chip.InternalState[256] > 0)
                                                        {
                                                                chip.InternalState[256]--; // Decrement stack pointer
                                                        }
                                                }
                                        }

                                        // Output top of stack (register 1, or 0 if empty)
                                        if (chip.InternalState[256] > 0)
                                        {
                                                chip.OutputPins[0].State = (ushort)chip.InternalState[chip.InternalState[256] - 1];
                                        }
                                        else
                                        {
                                                chip.OutputPins[0].State = 0;
                                        }

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
                                                double weightA = (long)bcp / abc;
                                                double weightB = (long)cap / abc;
                                                double weightC = (long)abp / abc;

                                                // Now interpolate each channel in floating point
                                                // colourAR, colourAG, colourAB, etc. are 0…15
                                                redF   = (long)colourAR * weightA
                                                        + (long)colourBR * weightB
                                                        + (long)colourCR * weightC;

                                                greenF = (long)colourAG * weightA
                                                        + (long)colourBG * weightB
                                                        + (long)colourCG * weightC;

                                                blueF  = (long)colourAB * weightA
                                                        + (long)colourBB * weightB
                                                        + (long)colourCB * weightC;
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

                                case ChipType.Addition32:
                                {
                                        ulong inputA = chip.InputPins[0].State;
                                        ulong inputB = chip.InputPins[1].State;
                                        
                                        // Get 32-bit values
                                        ulong valueA = PinState.GetBitStates(inputA) & 0xFFFFFFFF;
                                        ulong valueB = PinState.GetBitStates(inputB) & 0xFFFFFFFF;
                                        
                                        // Perform addition with overflow handling
                                        ulong result = (valueA + valueB) & 0xFFFFFFFF;
                                        
                                        chip.OutputPins[0].State = result;
                                        break;
                                }
                                
                                case ChipType.Multiplication32:
                                {
                                        ulong inputA = chip.InputPins[0].State;
                                        ulong inputB = chip.InputPins[1].State;
                                        
                                        // Get 32-bit values
                                        ulong valueA = PinState.GetBitStates(inputA) & 0xFFFFFFFF;
                                        ulong valueB = PinState.GetBitStates(inputB) & 0xFFFFFFFF;
                                        
                                        // Perform multiplication with overflow handling
                                        ulong result = (valueA * valueB) & 0xFFFFFFFF;
                                        
                                        chip.OutputPins[0].State = result;
                                        break;
                                }
                                
                                case ChipType.Division32:
                                {
                                        ulong inputA = chip.InputPins[0].State;
                                        ulong inputB = chip.InputPins[1].State;
                                        
                                        // Get 32-bit values
                                        ulong valueA = PinState.GetBitStates(inputA) & 0xFFFFFFFF;
                                        ulong valueB = PinState.GetBitStates(inputB) & 0xFFFFFFFF;
                                        
                                        // Handle division by zero
                                        ulong quotient = 0;
                                        ulong remainder = 0;
                                        if (valueB != 0)
                                        {
                                                quotient = valueA / valueB;
                                                remainder = valueA % valueB;
                                        }
                                        
                                        chip.OutputPins[0].State = quotient & 0xFFFFFFFF;  // Quotient output
                                        chip.OutputPins[1].State = remainder & 0xFFFFFFFF; // Remainder output
                                        break;
                                }


                                // ---- Basic Logic Gates ----
                                case ChipType.B_And_1Bit:
                                case ChipType.B_And_4Bit:
                                case ChipType.B_And_8Bit:
                                case ChipType.B_And_16Bit:
                                case ChipType.B_And_32Bit:
                                {
                                        ulong inputA = chip.InputPins[0].State;
                                        ulong inputB = chip.InputPins[1].State;
                                        
                                        // Check if either input is tristate (disconnected)
                                        ulong tristateA = PinState.GetTristateFlags(inputA);
                                        ulong tristateB = PinState.GetTristateFlags(inputB);
                                        
                                        // If either input is tristate, treat it as logic low for AND operation
                                        ulong valueA = tristateA != 0 ? 0 : PinState.GetBitStates(inputA);
                                        ulong valueB = tristateB != 0 ? 0 : PinState.GetBitStates(inputB);
                                        
                                        ulong result = valueA & valueB;
                                        chip.OutputPins[0].State = result;
                                        break;
                                }

                                case ChipType.B_Not_1Bit:
                                case ChipType.B_Not_4Bit:
                                case ChipType.B_Not_8Bit:
                                case ChipType.B_Not_16Bit:
                                case ChipType.B_Not_32Bit:
                                {
                                        ulong input = chip.InputPins[0].State;
                                        ulong tristateFlags = PinState.GetTristateFlags(input);
                                        
                                        ulong mask = chip.ChipType switch
                                        {
                                                ChipType.B_Not_1Bit => 0x1UL,
                                                ChipType.B_Not_4Bit => 0xFUL,
                                                ChipType.B_Not_8Bit => 0xFFUL,
                                                ChipType.B_Not_16Bit => 0xFFFFUL,
                                                ChipType.B_Not_32Bit => 0xFFFFFFFFUL,
                                                _ => 0xFFFFFFFFFFFFFFFFUL
                                        };
                                        
                                        // If input is tristate, treat as logic low, so NOT of low is high
                                        ulong value = tristateFlags != 0 ? 0 : PinState.GetBitStates(input);
                                        chip.OutputPins[0].State = (~value) & mask;
                                        break;
                                }

                                case ChipType.B_Or_1Bit:
                                case ChipType.B_Or_4Bit:
                                case ChipType.B_Or_8Bit:
                                case ChipType.B_Or_16Bit:
                                case ChipType.B_Or_32Bit:
                                {
                                        ulong inputA = chip.InputPins[0].State;
                                        ulong inputB = chip.InputPins[1].State;
                                        
                                        // Check if either input is tristate (disconnected)
                                        ulong tristateA = PinState.GetTristateFlags(inputA);
                                        ulong tristateB = PinState.GetTristateFlags(inputB);
                                        
                                        // If either input is tristate, treat it as logic low for OR operation
                                        ulong valueA = tristateA != 0 ? 0 : PinState.GetBitStates(inputA);
                                        ulong valueB = tristateB != 0 ? 0 : PinState.GetBitStates(inputB);
                                        
                                        ulong result = valueA | valueB;
                                        chip.OutputPins[0].State = result;
                                        break;
                                }

                                case ChipType.B_Xor_1Bit:
                                case ChipType.B_Xor_4Bit:
                                case ChipType.B_Xor_8Bit:
                                case ChipType.B_Xor_16Bit:
                                case ChipType.B_Xor_32Bit:
                                {
                                        ulong inputA = chip.InputPins[0].State;
                                        ulong inputB = chip.InputPins[1].State;
                                        
                                        // Check if either input is tristate (disconnected)
                                        ulong tristateA = PinState.GetTristateFlags(inputA);
                                        ulong tristateB = PinState.GetTristateFlags(inputB);
                                        
                                        // If either input is tristate, treat it as logic low for XOR operation
                                        ulong valueA = tristateA != 0 ? 0 : PinState.GetBitStates(inputA);
                                        ulong valueB = tristateB != 0 ? 0 : PinState.GetBitStates(inputB);
                                        
                                        ulong result = valueA ^ valueB;
                                        chip.OutputPins[0].State = result;
                                        break;
                                }

                                case ChipType.B_Xnor_1Bit:
                                case ChipType.B_Xnor_4Bit:
                                case ChipType.B_Xnor_8Bit:
                                case ChipType.B_Xnor_16Bit:
                                case ChipType.B_Xnor_32Bit:
                                {
                                        ulong inputA = chip.InputPins[0].State;
                                        ulong inputB = chip.InputPins[1].State;
                                        
                                        // Check if either input is tristate (disconnected)
                                        ulong tristateA = PinState.GetTristateFlags(inputA);
                                        ulong tristateB = PinState.GetTristateFlags(inputB);
                                        
                                        // If either input is tristate, treat it as logic low for XNOR operation
                                        ulong valueA = tristateA != 0 ? 0 : PinState.GetBitStates(inputA);
                                        ulong valueB = tristateB != 0 ? 0 : PinState.GetBitStates(inputB);
                                        
                                        ulong xorResult = valueA ^ valueB;
                                        ulong mask = chip.ChipType switch
                                        {
                                                ChipType.B_Xnor_1Bit => 0x1UL,
                                                ChipType.B_Xnor_4Bit => 0xFUL,
                                                ChipType.B_Xnor_8Bit => 0xFFUL,
                                                ChipType.B_Xnor_16Bit => 0xFFFFUL,
                                                ChipType.B_Xnor_32Bit => 0xFFFFFFFFUL,
                                                _ => 0xFFFFFFFFFFFFFFFFUL
                                        };
                                        chip.OutputPins[0].State = (~xorResult) & mask;
                                        break;
                                }

                                case ChipType.B_Nor_1Bit:
                                case ChipType.B_Nor_4Bit:
                                case ChipType.B_Nor_8Bit:
                                case ChipType.B_Nor_16Bit:
                                case ChipType.B_Nor_32Bit:
                                {
                                        ulong inputA = chip.InputPins[0].State;
                                        ulong inputB = chip.InputPins[1].State;
                                        
                                        // Check if either input is tristate (disconnected)
                                        ulong tristateA = PinState.GetTristateFlags(inputA);
                                        ulong tristateB = PinState.GetTristateFlags(inputB);
                                        
                                        // If either input is tristate, treat it as logic low for NOR operation
                                        ulong valueA = tristateA != 0 ? 0 : PinState.GetBitStates(inputA);
                                        ulong valueB = tristateB != 0 ? 0 : PinState.GetBitStates(inputB);
                                        
                                        ulong orResult = valueA | valueB;
                                        ulong mask = chip.ChipType switch
                                        {
                                                ChipType.B_Nor_1Bit => 0x1UL,
                                                ChipType.B_Nor_4Bit => 0xFUL,
                                                ChipType.B_Nor_8Bit => 0xFFUL,
                                                ChipType.B_Nor_16Bit => 0xFFFFUL,
                                                ChipType.B_Nor_32Bit => 0xFFFFFFFFUL,
                                                _ => 0xFFFFFFFFFFFFFFFFUL
                                        };
                                        chip.OutputPins[0].State = (~orResult) & mask;
                                        break;
                                }

                                case ChipType.B_TriStateBuffer_1Bit:
                                case ChipType.B_TriStateBuffer_4Bit:
                                case ChipType.B_TriStateBuffer_8Bit:
                                case ChipType.B_TriStateBuffer_16Bit:
                                case ChipType.B_TriStateBuffer_32Bit:
                                {
                                        SimPin dataPin = chip.InputPins[0];
                                        SimPin enablePin = chip.InputPins[1];
                                        SimPin outputPin = chip.OutputPins[0];

                                        if (PinState.FirstBitHigh(enablePin.State)) 
                                                outputPin.State = dataPin.State;
                                        else 
                                                PinState.SetAllDisconnected(ref outputPin.State);
                                        break;
                                }

                                case ChipType.B_Counter_4Bit:
                                case ChipType.B_Counter_8Bit:
                                case ChipType.B_Counter_16Bit:
                                case ChipType.B_Counter_32Bit:
                                case ChipType.B_Counter_64Bit:
                                {
                                        ulong clockPin = chip.InputPins[0].State;
                                        ulong resetPin = chip.InputPins[1].State;

                                        // Detect clock rising edge
                                        bool clockHigh = PinState.FirstBitHigh(clockPin);
                                        bool isRisingEdge = clockHigh && chip.InternalState[1] == 0;
                                        chip.InternalState[1] = clockHigh ? 1u : 0;
                                        
                                        if (PinState.FirstBitHigh(resetPin))
                                        {
                                                chip.InternalState[0] = 0;
                                        }

                                        if (isRisingEdge)
                                        {
                                                if (chip.ChipType == ChipType.B_Counter_64Bit)
                                                {
                                                        // Handle 64-bit counter separately to avoid overflow
                                                        chip.InternalState[0] = chip.InternalState[0] + 1;
                                                }
                                                else
                                                {
                                                        ulong maxValue = chip.ChipType switch
                                                        {
                                                                ChipType.B_Counter_4Bit => 15u,           // 2^4 - 1
                                                                ChipType.B_Counter_8Bit => 255u,          // 2^8 - 1
                                                                ChipType.B_Counter_16Bit => 65535u,       // 2^16 - 1
                                                                ChipType.B_Counter_32Bit => 4294967295u,  // 2^32 - 1
                                                                _ => 255u
                                                        };
                                                        chip.InternalState[0] = (chip.InternalState[0] + 1) % (maxValue + 1);
                                                }
                                        }
                                        chip.OutputPins[0].State = chip.InternalState[0];
                                        break;
                                }

                                case ChipType.B_Equals_4Bit:
                                case ChipType.B_Equals_8Bit:
                                case ChipType.B_Equals_16Bit:
                                case ChipType.B_Equals_32Bit:
                                case ChipType.B_Equals_64Bit:
                                {
                                        ulong inputA = chip.InputPins[0].State;
                                        ulong inputB = chip.InputPins[1].State;
                                        
                                        ulong mask = chip.ChipType switch
                                        {
                                                ChipType.B_Equals_4Bit => 0xFUL,           // 4 bits
                                                ChipType.B_Equals_8Bit => 0xFFUL,          // 8 bits
                                                ChipType.B_Equals_16Bit => 0xFFFFUL,       // 16 bits
                                                ChipType.B_Equals_32Bit => 0xFFFFFFFFUL,   // 32 bits
                                                ChipType.B_Equals_64Bit => 0xFFFFFFFFFFFFFFFFUL, // 64 bits (ulong.MaxValue)
                                                _ => 0xFFUL
                                        };
                                        
                                        bool areEqual = (inputA & mask) == (inputB & mask);
                                        chip.OutputPins[0].State = areEqual ? PinState.LogicHigh : PinState.LogicLow;
                                        break;
                                }

                                case ChipType.B_Decoder_8Bit:
                                {
                                        ulong inputValue = chip.InputPins[0].State;
                                        
                                        // Check if input is tristate (disconnected)
                                        ulong tristateFlags = PinState.GetTristateFlags(inputValue);
                                        
                                        // If input is tristate, treat as 0
                                        ulong decodedValue = tristateFlags != 0 ? 0 : PinState.GetBitStates(inputValue);
                                        
                                        // Mask to 8 bits (0-255)
                                        decodedValue &= 0xFF;
                                        
                                        // Set all outputs to low first
                                        for (int i = 0; i < chip.OutputPins.Length; i++)
                                        {
                                                chip.OutputPins[i].State = PinState.LogicLow;
                                        }
                                        
                                        // Set only the selected output to high (in reverse order)
                                        // Input 0 activates the bottom pin, input 255 activates the top pin
                                        if (decodedValue < (ulong)chip.OutputPins.Length)
                                        {
                                                int reversedIndex = chip.OutputPins.Length - 1 - (int)decodedValue;
                                                chip.OutputPins[reversedIndex].State = PinState.LogicHigh;
                                        }
                                        
                                        break;
                                }


                                case ChipType.B_FirstTick:
                                {
                                        ulong onPin = chip.InputPins[0].State;
                                        ulong resetPin = chip.InputPins[1].State;
                                        ulong clockPin = chip.InputPins[2].State;

                                        bool onHigh = PinState.FirstBitHigh(onPin);
                                        bool resetHigh = PinState.FirstBitHigh(resetPin);
                                        bool clockHigh = PinState.FirstBitHigh(clockPin);

                                        // Internal state: [0] = firstTickState, [1] = previousClockState
                                        bool previousClockHigh = chip.InternalState[1] != 0;

                                        // If reset is high, first tick is on
                                        if (resetHigh)
                                        {
                                                chip.InternalState[0] = 1;
                                        }
                                        // If on is on and clock goes from high to low, turn off first tick
                                        else if (onHigh && previousClockHigh && !clockHigh)
                                        {
                                                chip.InternalState[0] = 0;
                                        }

                                        // Update previous clock state
                                        chip.InternalState[1] = clockHigh ? 1u : 0;

                                        // Output logic: if all 3 are on, output should be on, otherwise use internal state
                                        bool outputHigh = (onHigh && resetHigh && clockHigh) || 
                                                                        (chip.InternalState[0] != 0 && onHigh);

                                        chip.OutputPins[0].State = outputHigh ? PinState.LogicHigh : PinState.LogicLow;
                                        break;
                                }
                                case ChipType.EdgeFunction3Merge32Bit:
                                {
                                        // Get input values (all 32-bit)
                                        ulong x = PinState.GetBitStates(chip.InputPins[7].State);   // X
                                        ulong y = PinState.GetBitStates(chip.InputPins[6].State);   // Y
                                        ulong ax = PinState.GetBitStates(chip.InputPins[5].State);  // AX
                                        ulong ay = PinState.GetBitStates(chip.InputPins[4].State);  // AY
                                        ulong bx = PinState.GetBitStates(chip.InputPins[3].State);  // BX
                                        ulong by = PinState.GetBitStates(chip.InputPins[2].State);  // BY
                                        ulong cx = PinState.GetBitStates(chip.InputPins[1].State);  // CX
                                        ulong cy = PinState.GetBitStates(chip.InputPins[0].State);  // CY
                                        
                                        long sx = (long)x;
                                        long sy = (long)y;
                                        long sax = (long)ax;
                                        long say = (long)ay;
                                        long sbx = (long)bx;
                                        long sby = (long)by;
                                        long scx = (long)cx;
                                        long scy = (long)cy;

                                        // E1 = (x - Ax)*(By - Ay) - (y - Ay)*(Bx - Ax)
                                        long e1 = (sx - sax) * (sby - say) - (sy - say) * (sbx - sax);

                                        // E2 = (x - Bx)*(Cy - By) - (y - By)*(Cx - Bx)
                                        long e2 = (sx - sbx) * (scy - sby) - (sy - sby) * (scx - sbx);

                                        // E3 = (x - Cx)*(Ay - Cy) - (y - Cy)*(Ax - Cx)
                                        long e3 = (sx - scx) * (say - scy) - (sy - scy) * (sax - scx);

                                        // Optional degenerate triangle check
                                        bool isDegenerate = (sax == sbx && say == sby) ||
                                                                                (sbx == scx && sby == scy) ||
                                                                                (scx == sax && scy == say);

                                        // Final inside check
                                        bool sameSign = !isDegenerate &&
                                                                        ((e1 >= 0 && e2 >= 0 && e3 >= 0) ||
                                                                        (e1 <= 0 && e2 <= 0 && e3 <= 0));

                                        chip.OutputPins[0].State = sameSign ? PinState.LogicHigh : PinState.LogicLow;
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
                                
                                case ChipType.B_CPU:
                                {
                                        ProcessBCPU(chip);
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

                static void ProcessBCPU(SimChip chip)
                {
                        // Input pins: DATA3, DATA2, DATA1, OPCODE, CLOCK, RUN, STEP
                        ulong data3 = PinState.GetBitStates(chip.InputPins[0].State);
                        ulong data2 = PinState.GetBitStates(chip.InputPins[1].State);
                        ulong data1 = PinState.GetBitStates(chip.InputPins[2].State);
                        ulong opcode = PinState.GetBitStates(chip.InputPins[3].State);
                        bool clock = PinState.FirstBitHigh(chip.InputPins[4].State);
                        bool run = PinState.FirstBitHigh(chip.InputPins[5].State);
                        bool step = PinState.FirstBitHigh(chip.InputPins[6].State);

                        // Get previous states
                        bool prevClock = chip.InternalState[1053] != 0;
                        bool prevRun = chip.InternalState[1054] != 0;
                        bool prevStep = chip.InternalState[1055] != 0;

                        // Update previous states
                        chip.InternalState[1053] = clock ? 1UL : 0UL;
                        chip.InternalState[1054] = run ? 1UL : 0UL;
                        chip.InternalState[1055] = step ? 1UL : 0UL;

                        // Check for halt flag
                        bool isHalted = (chip.InternalState[1056] & 0x4) != 0;

                        // Execute instruction on rising edge of clock and if not halted
                        bool shouldExecute = false;
                        if (!isHalted)
                        {
                                if (run && clock && !prevClock) // Rising edge while running
                                {
                                        shouldExecute = true;
                                }
                                else if (step && !prevStep) // Rising edge of step
                                {
                                        shouldExecute = true;
                                }
                        }

                        if (shouldExecute)
                        {
                                ExecuteCPUInstruction(chip, data3, data2, data1, opcode);
                        }

                        // Update output pins with current register values and state
                        UpdateCPUOutputs(chip);
                }

                static void ExecuteCPUInstruction(SimChip chip, ulong data3, ulong data2, ulong data1, ulong opcode)
                {
                        // Clear flags
                        chip.InternalState[1056] &= ~0x3UL; // Clear carry and zero flags

                        switch (opcode)
                        {
                                case 0x00: // NOP
                                        break;

                                case 0x01: // Math operation
                                        {
                                                byte op = (byte)(data3 >> 4);
                                                byte reg1 = (byte)(data3 & 0xF);
                                                byte reg2Type = (byte)((data2 >> 6) & 0x3);
                                                byte reg1Type = (byte)((data2 >> 4) & 0x3);
                                                byte outReg = (byte)(data2 & 0xF);
                                                byte reg2 = (byte)(data1 & 0xF);

                                                ulong val1 = GetCPUValue(chip, reg1, reg1Type);
                                                ulong val2 = GetCPUValue(chip, reg2, reg2Type);
                                                ulong result = PerformMathOperation(chip, op, val1, val2);

                                                if (outReg < 25) chip.InternalState[outReg] = result & 0xFF;
                                                break;
                                        }

                                case 0x02: // Store register to RAM
                                        {
                                                byte reg = (byte)data2;
                                                byte addr = (byte)data1;
                                                if (reg < 25)
                                                {
                                                        chip.InternalState[29 + addr] = chip.InternalState[reg];
                                                }
                                                break;
                                        }

                                case 0x03: // Load RAM to register
                                        {
                                                byte outReg = (byte)data2;
                                                byte addr = (byte)data1;
                                                if (outReg < 25)
                                                {
                                                        chip.InternalState[outReg] = chip.InternalState[29 + addr];
                                                }
                                                break;
                                        }

                                case 0x04: // Set register to value
                                        {
                                                byte reg = (byte)data2;
                                                byte value = (byte)data1;
                                                if (reg < 25)
                                                {
                                                        chip.InternalState[reg] = value;
                                                }
                                                break;
                                        }

                                case 0x05: // Jump
                                        {
                                                byte useReg = (byte)(data3 >> 7);
                                                byte key = (byte)(data3 & 0x7F);
                                                byte reg = (byte)((data2 >> 4) & 0xF);
                                                byte jmpType = (byte)(data2 & 0xF);
                                                byte addr = (byte)data1;

                                                bool shouldJump = ShouldJump(chip, jmpType, key, useReg != 0 ? reg : (byte)255);
                                                if (shouldJump)
                                                {
                                                        chip.InternalState[25] = addr; // Set PC
                                                        return; // Don't increment PC
                                                }
                                                break;
                                        }

                                case 0x06: // Draw pixel
                                        {
                                                byte addrType = (byte)(data3 >> 7);
                                                byte rType = (byte)((data3 >> 6) & 1);
                                                byte gType = (byte)((data3 >> 5) & 1);
                                                byte bType = (byte)((data3 >> 4) & 1);
                                                byte blue = (byte)(data3 & 0xF);
                                                byte red = (byte)((data2 >> 4) & 0xF);
                                                byte green = (byte)(data2 & 0xF);
                                                byte addr = (byte)data1;

                                                // Get actual values based on types
                                                ulong screenAddr = addrType == 0 ? addr : (addr < 25 ? chip.InternalState[addr] : 0);
                                                ulong redVal = rType == 0 ? red : (red < 25 ? chip.InternalState[red] : 0);
                                                ulong greenVal = gType == 0 ? green : (green < 25 ? chip.InternalState[green] : 0);
                                                ulong blueVal = bType == 0 ? blue : (blue < 25 ? chip.InternalState[blue] : 0);

                                                // Store screen coordinates and colors for output
                                                chip.InternalState[1057] = screenAddr & 0xF; // X (lower nibble)
                                                chip.InternalState[1058] = (screenAddr >> 4) & 0xF; // Y (upper nibble)
                                                
                                                // Set screen output values (will be output in UpdateCPUOutputs)
                                                break;
                                        }

                                case 0x07: // Clear screen
                                        // Implementation would depend on how screen clearing is handled
                                        break;

                                case 0x08: // Refresh screen
                                        // Set refresh flag
                                        break;

                                case 0x09: // Random number
                                        {
                                                byte reg = (byte)data2;
                                                if (reg < 25)
                                                {
                                                        chip.InternalState[reg] = (ulong)(rng.Next(256));
                                                }
                                                break;
                                        }

                                case 0x0A: // Stack operation
                                        {
                                                byte type = (byte)((data3 >> 6) & 0x3);
                                                byte stack = (byte)((data3 >> 4) & 0x3);
                                                byte exType = (byte)((data2 >> 4) & 0xF);
                                                byte reg = (byte)(data2 & 0xF);
                                                byte value = (byte)data1;

                                                PerformStackOperation(chip, type, stack, reg, value, exType);
                                                break;
                                        }

                                case 0xFF: // Halt
                                        chip.InternalState[1056] |= 0x4; // Set halt flag
                                        break;
                        }

                        // Increment program counter (unless it was a jump that executed)
                        if (opcode != 0x05 || !ShouldJump(chip, (byte)(data2 & 0xF), (byte)(data3 & 0x7F), (byte)(data3 >> 7) != 0 ? (byte)((data2 >> 4) & 0xF) : (byte)255))
                        {
                                chip.InternalState[25] = (chip.InternalState[25] + 1) & 0xFF;
                        }
                }

                static ulong GetCPUValue(SimChip chip, byte reg, byte type)
                {
                        return type switch
                        {
                                0 => reg < 25 ? chip.InternalState[reg] : 0, // Register
                                1 => reg, // Built-in value
                                2 => chip.InternalState[29 + reg], // RAM address (reg is byte, so always < 256)
                                _ => 0
                        };
                }

                static ulong PerformMathOperation(SimChip chip, byte op, ulong val1, ulong val2)
                {
                        ulong result = op switch
                        {
                                0 => val1 + val2, // Addition
                                1 => val1 - val2, // Subtraction
                                2 => val1 * val2, // Multiplication
                                3 => val2 != 0 ? val1 / val2 : 0, // Division
                                4 => ~(val1 & val2), // NAND
                                5 => val1 & val2, // AND
                                6 => ~val1, // NOT
                                7 => val1 | val2, // OR
                                8 => ~(val1 | val2), // NOR
                                9 => val1 ^ val2, // XOR
                                10 => ~(val1 ^ val2), // XNOR
                                11 => val1 == val2 ? 1UL : 0UL, // Compare
                                12 => (long)val1 == (long)val2 ? 1UL : 0UL, // Compare Signed
                                _ => 0
                        };

                        // Set flags
                        if (result == 0) chip.InternalState[1056] |= 0x2; // Zero flag
                        if (result > 0xFF) chip.InternalState[1056] |= 0x1; // Carry flag

                        return result & 0xFF;
                }

                static bool ShouldJump(SimChip chip, byte jmpType, byte key, byte reg)
                {
                        return jmpType switch
                        {
                                0 => true, // JUMP
                                1 => (chip.InternalState[1056] & 0x2) != 0, // ZERO
                                2 => (chip.InternalState[1056] & 0x1) != 0, // CARRY
                                3 => IsKeyPressed(key), // KEY
                                0xF => IsAnyKeyPressed(), // ANY KEY
                                _ => false
                        };
                }

                static bool IsKeyPressed(byte key)
                {
                        // This would need to interface with the keyboard system
                        // For now, return false
                        return false;
                }

                static bool IsAnyKeyPressed()
                {
                        // This would need to interface with the keyboard system
                        // For now, return false
                        return false;
                }

                static void PerformStackOperation(SimChip chip, byte type, byte stack, byte reg, byte value, byte exType)
                {
                        int stackBase = stack switch
                        {
                                0 => 285, // Return stack
                                1 => 541, // Function parameters stack
                                2 => 797, // General purpose stack
                                _ => 285
                        };

                        int stackPtrIndex = 26 + stack; // Stack pointer indices: 26, 27, 28

                        if (type == 0) // Push
                        {
                                ulong stackPtr = chip.InternalState[stackPtrIndex];
                                if (stackPtr < 256)
                                {
                                        ulong pushValue = reg == 0xF ? value : (reg < 25 ? chip.InternalState[reg] : 0);
                                        chip.InternalState[(ulong)stackBase + stackPtr] = pushValue;
                                        chip.InternalState[stackPtrIndex] = stackPtr + 1;

                                        if (exType == 1) // Add to program counter
                                        {
                                                chip.InternalState[25] = (chip.InternalState[25] + value) & 0xFF;
                                        }
                                }
                        }
                        else if (type == 1) // Pop
                        {
                                ulong stackPtr = chip.InternalState[stackPtrIndex];
                                if (stackPtr > 0)
                                {
                                        stackPtr--;
                                        ulong popValue = chip.InternalState[(ulong)stackBase + stackPtr];
                                        if (reg < 25)
                                        {
                                                chip.InternalState[reg] = popValue;
                                        }
                                        chip.InternalState[stackPtrIndex] = stackPtr;

                                        if (exType == 1) // Add to program counter
                                        {
                                                chip.InternalState[25] = (chip.InternalState[25] + popValue) & 0xFF;
                                        }
                                }
                        }
                }

                static void UpdateCPUOutputs(SimChip chip)
                {
                        // Screen outputs
                        chip.OutputPins[0].State = chip.InternalState[1057] | (chip.InternalState[1058] << 4); // S ADDRESS (X + Y*16)
                        chip.OutputPins[1].State = 0; // S RED (would be set during draw operations)
                        chip.OutputPins[2].State = 0; // S GREEN
                        chip.OutputPins[3].State = 0; // S BLUE
                        chip.OutputPins[4].State = 0; // S WRITE
                        chip.OutputPins[5].State = 0; // S REFRESH
                        chip.OutputPins[6].State = 0; // S CLOCK

                        // Program counter
                        chip.OutputPins[7].State = chip.InternalState[25]; // PC ADDRESS

                        // Registers A-Y (pins 8-32, internal state 0-24)
                        for (int i = 0; i < 25; i++)
                        {
                                chip.OutputPins[8 + i].State = chip.InternalState[i];
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
                                                        subChip.ChipType == ChipType.Keyboard ||
                                                        subChip.ChipType == ChipType.Mouse ||
                                                        subChip.ChipType == ChipType.B_CPU ||
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
