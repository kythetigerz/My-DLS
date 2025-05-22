using System;

namespace DLS.Simulation
{
	public class SimPin
	{
		public readonly int ID;
		public readonly SimChip parentChip;
		public readonly bool isInput;
		public ulong State;

		public SimPin[] ConnectedTargetPins = Array.Empty<SimPin>();

		// Simulation frame index on which pin last received an input
		public int lastUpdatedFrameIndex;

		// Address of pin from where this pin last received its input
		public int latestSourceID;
		public int latestSourceParentChipID;

		// Number of wires that input their signal to this pin.
		// (In the case of conflicting signals, the pin chooses randomly)
		public int numInputConnections;
		public int numInputsReceivedThisFrame;

		public SimPin(int id, bool isInput, SimChip parentChip)
		{
			this.parentChip = parentChip;
			this.isInput = isInput;
			ID = id;
			latestSourceID = -1;
			latestSourceParentChipID = -1;

			PinState.SetAllDisconnected(ref State);
		}

		public bool FirstBitHigh => PinState.FirstBitHigh(State);

		public void PropagateSignal()
		{
			int length = ConnectedTargetPins.Length;
			for (int i = 0; i < length; i++)
			{
				ConnectedTargetPins[i].ReceiveInput(this);
			}
		}

		// Called on sub-chip input pins, or chip dev-pins
		void ReceiveInput(SimPin source)
		{
			// If this is the first input of the frame, reset the received inputs counter to zero
			if (lastUpdatedFrameIndex != Simulator.simulationFrame)
			{
				lastUpdatedFrameIndex = Simulator.simulationFrame;
				numInputsReceivedThisFrame = 0;
			}

			bool set = false;

			// Check if source is fully disconnected
			bool sourceIsDisconnected = PinState.IsFullyDisconnected(source.State);

			if (numInputsReceivedThisFrame > 0)
			{
				// Has already received input this frame
				
				// Check if current state is fully disconnected
				bool currentIsDisconnected = PinState.IsFullyDisconnected(State);
				
				if (sourceIsDisconnected && currentIsDisconnected)
				{
					// Both are disconnected, keep disconnected state
					// No change needed, State remains disconnected
				}
				else if (sourceIsDisconnected)
				{
					// Source is disconnected but current is not, keep current state
					// No change needed, State remains as is
				}
				else if (currentIsDisconnected)
				{
					// Current is disconnected but source is not, use source state
					State = source.State;
					set = true;
				}
				else
				{
					// Neither is disconnected, handle normally with random selection for conflicts
					ulong sourceBitStates = PinState.GetBitStates(source.State);
					ulong currentBitStates = PinState.GetBitStates(State);
					ulong sourceTristateFlags = PinState.GetTristateFlags(source.State);
					ulong currentTristateFlags = PinState.GetTristateFlags(State);
					
					// For bits where either input is disconnected, preserve the connected value
					ulong combinedBitStates = currentBitStates;
					ulong combinedTristateFlags = currentTristateFlags;
					
					// For bits where both inputs are connected, randomly choose between them if they conflict
					for (int i = 0; i < 32; i++) // Handle up to 32 bits
					{
						ulong bitMask = 1UL << i;
						ulong triMask = 1UL << i;
						
						bool sourceDisconnected = ((sourceTristateFlags & triMask) != 0);
						bool currentDisconnected = ((currentTristateFlags & triMask) != 0);
						
						if (!sourceDisconnected && !currentDisconnected)
						{
							// Both connected, check if they conflict
							bool sourceBit = ((sourceBitStates & bitMask) != 0);
							bool currentBit = ((currentBitStates & bitMask) != 0);
							
							if (sourceBit != currentBit)
							{
								// Conflict - randomly choose
								if (Simulator.RandomBool())
								{
									// Choose source bit
									if (sourceBit)
										combinedBitStates |= bitMask;
									else
										combinedBitStates &= ~bitMask;
								}
								// Else keep current bit
							}
						}
						else if (!sourceDisconnected)
						{
							// Source connected, current disconnected - use source
							combinedTristateFlags &= ~triMask; // Clear tristate flag
							if ((sourceBitStates & bitMask) != 0)
								combinedBitStates |= bitMask;
							else
								combinedBitStates &= ~bitMask;
						}
						// Else keep current state (either both disconnected or source disconnected)
					}
					
					PinState.Set(ref State, combinedBitStates, (uint)combinedTristateFlags);
					set = true;
				}
			}
			else
			{
				// First input source this frame, so accept it.
				State = source.State;
				set = true;
			}

			if (set)
			{
				latestSourceID = source.ID;
				latestSourceParentChipID = source.parentChip.ID;
			}

			numInputsReceivedThisFrame++;

			// If this is a sub-chip input pin, and has received all of its connections, notify the sub-chip that the input is ready
			if (isInput && numInputsReceivedThisFrame == numInputConnections)
			{
				parentChip.numInputsReady++;
			}
		}
	}
}