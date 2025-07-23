using System;
using System.Linq;
using DLS.Description;

namespace DLS.Simulation
{
	public class SimChip
	{
		public readonly ChipType ChipType;
		public readonly int ID;

		// Some builtin chips, such as RAM, require an internal state for memory
		// (can also be used for other arbitrary chip-specific data)
		public readonly ulong[] InternalState = Array.Empty<ulong>();
		public readonly bool IsBuiltin;
		public SimPin[] InputPins = Array.Empty<SimPin>();
		public int numConnectedInputs;

		public int numInputsReady;
		public SimPin[] OutputPins = Array.Empty<SimPin>();
		public SimChip[] SubChips = Array.Empty<SimChip>();


		public SimChip()
		{
			ID = -1;
		}

		public SimChip(ChipDescription desc, int id, uint[] internalState, SimChip[] subChips)
		{
			SubChips = subChips;
			ID = id;
			ChipType = desc.ChipType;
			IsBuiltin = ChipType != ChipType.Custom;

			// ---- Create pins (don't allocate unnecessarily as very many sim chips maybe created!) ----
			if (desc.InputPins.Length > 0)
			{
				InputPins = new SimPin [desc.InputPins.Length];
				for (int i = 0; i < InputPins.Length; i++)
				{
					InputPins[i] = CreateSimPinFromDescription(desc.InputPins[i], true, this);
				}
			}

			if (desc.OutputPins.Length > 0)
			{
				OutputPins = new SimPin [desc.OutputPins.Length];
				for (int i = 0; i < OutputPins.Length; i++)
				{
					OutputPins[i] = CreateSimPinFromDescription(desc.OutputPins[i], false, this);
				}
			}

			// ---- Initialize internal state ----
			const int addressSize_8Bit = 256;

			if (ChipType is ChipType.DisplayRGB)
			{
				// first 256 bits = display buffer, next 256 bits = back buffer, last bit = clock state (to allow edge-trigger behaviour)
				InternalState = new ulong[addressSize_8Bit * 2 + 1];
			}
			else if (ChipType is ChipType.Display1080RGB)
			{
				// For 1080x1920 display, we need 1080*1920 = 2,073,600 pixels
				// Each pixel needs 24 bits (8 bits per RGB channel)
				// We'll use a 32-bit address space (21 bits needed for 2M pixels)
				// +1 for clock state (to allow edge-trigger behaviour)
				const int pixelCount = 1080 * 1920;
				InternalState = new ulong[pixelCount * 1 + 1]; // Double buffer + clock state
			}
			else if (ChipType is ChipType.DisplayDot)
			{
				// first 256 bits = display buffer, next 256 bits = back buffer, last bit = clock state (to allow edge-trigger behaviour)
				InternalState = new ulong[addressSize_8Bit * 2 + 1];
			}
			else if (ChipType is ChipType.dev_Ram_8Bit)
			{
				InternalState = new ulong[addressSize_8Bit + 1]; // +1 for clock state (to allow edge-trigger behaviour)

				// Initialize memory contents to random state
				Span<byte> randomBytes = stackalloc byte[4];
				for (int i = 0; i < InternalState.Length - 1; i++)
				{
					Simulator.rng.NextBytes(randomBytes);
					InternalState[i] = BitConverter.ToUInt32(randomBytes);
				}
			}
			else if (ChipType is ChipType.Stack_8Bit)
			{
				// [0-255] = stack registers, [256] = stack pointer, [257] = clock state
				InternalState = new ulong[addressSize_8Bit + 2];
				
				// Initialize stack registers to zero
				for (int i = 0; i < addressSize_8Bit; i++)
				{
					InternalState[i] = 0;
				}
				
				InternalState[256] = 0; // Stack pointer starts at 0 (empty stack)
				InternalState[257] = 0; // Previous clock state starts low
			}
			else if (ChipType is ChipType.B_Counter_4Bit or ChipType.B_Counter_8Bit or ChipType.B_Counter_16Bit or ChipType.B_Counter_32Bit or ChipType.B_Counter_64Bit)
			{
				// [0] = counter value, [1] = previous clock state
				InternalState = new ulong[2];
				InternalState[0] = 0; // Start counter at 0
				InternalState[1] = 0; // Previous clock state starts low
			}
			else if (ChipType is ChipType.B_FirstTick)
			{
				// [0] = first tick state (starts as 1/true), [1] = previous clock state
				InternalState = new ulong[2];
				InternalState[0] = 1; // First tick starts as true/on
				InternalState[1] = 0; // Previous clock state starts low
			}
			else if (ChipType is ChipType.B_CPU)
			{
				// CPU Internal State Layout:
				// [0-24] = Registers A-Y (25 registers)
				// [25] = Program Counter
				// [26] = Return Stack Pointer
				// [27] = Function Parameters Stack Pointer  
				// [28] = General Purpose Stack Pointer
				// [29-284] = RAM (256 bytes)
				// [285-540] = Return Stack (256 bytes)
				// [541-796] = Function Parameters Stack (256 bytes)
				// [797-1052] = General Purpose Stack (256 bytes)
				// [1053] = Previous Clock State
				// [1054] = Previous Run State
				// [1055] = Previous Step State
				// [1056] = CPU Flags (bit 0: carry, bit 1: zero, bit 2: halt)
				// [1057] = Screen X coordinate
				// [1058] = Screen Y coordinate
				InternalState = new ulong[1059];
				
				// Initialize registers to 0
				for (int i = 0; i < 25; i++)
				{
					InternalState[i] = 0;
				}
				
				// Initialize program counter and stack pointers
				InternalState[25] = 0; // Program Counter
				InternalState[26] = 0; // Return Stack Pointer
				InternalState[27] = 0; // Function Parameters Stack Pointer
				InternalState[28] = 0; // General Purpose Stack Pointer
				
				// Initialize RAM to random values
				Span<byte> randomBytes = stackalloc byte[4];
				for (int i = 29; i < 285; i++)
				{
					Simulator.rng.NextBytes(randomBytes);
					InternalState[i] = BitConverter.ToUInt32(randomBytes);
				}
				
				// Initialize stacks to 0
				for (int i = 285; i < 1053; i++)
				{
					InternalState[i] = 0;
				}
				
				// Initialize control states
				InternalState[1053] = 0; // Previous Clock State
				InternalState[1054] = 0; // Previous Run State
				InternalState[1055] = 0; // Previous Step State
				InternalState[1056] = 0; // CPU Flags
				InternalState[1057] = 0; // Screen X coordinate
				InternalState[1058] = 0; // Screen Y coordinate
			}
			// Load in serialized persistent state (rom data, etc.)
			else if (internalState is { Length: > 0 })
			{
				InternalState = new ulong[internalState.Length];
				UpdateInternalState(internalState);
			}
		}

		public void UpdateInternalState(uint[] source) => Array.Copy(source, InternalState, InternalState.Length);


		public void Sim_PropagateInputs()
		{
			int length = InputPins.Length;

			for (int i = 0; i < length; i++)
			{
				InputPins[i].PropagateSignal();
			}
		}

		public void Sim_PropagateOutputs()
		{
			int length = OutputPins.Length;

			for (int i = 0; i < length; i++)
			{
				OutputPins[i].PropagateSignal();
			}

			numInputsReady = 0; // Reset for next frame
		}

		public bool Sim_IsReady() => numInputsReady == numConnectedInputs;
		
		public (bool success, SimChip chip) TryGetSubChipFromID(int id)
		{
			// Todo: address possible errors if accessing from main thread while being modified on sim thread?
			foreach (SimChip s in SubChips)
			{
				if (s?.ID == id)
				{
					return (true, s);
				}
			}

			return (false, null);
		}

		public SimChip GetSubChipFromID(int id)
		{
			(bool success, SimChip chip) = TryGetSubChipFromID(id);
			if (success) return chip;

			throw new Exception("Failed to find subchip with id " + id);
		}

		public (SimPin pin, SimChip chip) GetSimPinFromAddressWithChip(PinAddress address)
		{
			foreach (SimChip s in SubChips)
			{
				if (s.ID == address.PinOwnerID)
				{
					foreach (SimPin pin in s.InputPins)
					{
						if (pin.ID == address.PinID) return (pin, s);
					}

					foreach (SimPin pin in s.OutputPins)
					{
						if (pin.ID == address.PinID) return (pin, s);
					}
				}
			}

			foreach (SimPin pin in InputPins)
			{
				if (pin.ID == address.PinOwnerID) return (pin, null);
			}

			foreach (SimPin pin in OutputPins)
			{
				if (pin.ID == address.PinOwnerID) return (pin, null);
			}

			throw new Exception("Failed to find pin with address: " + address.PinID + ", " + address.PinOwnerID);
		}

		public SimPin GetSimPinFromAddress(PinAddress address)
		{
			// Todo: address possible errors if accessing from main thread while being modified on sim thread?
			foreach (SimChip s in SubChips)
			{
				if (s.ID == address.PinOwnerID)
				{
					foreach (SimPin pin in s.InputPins)
					{
						if (pin.ID == address.PinID) return pin;
					}

					foreach (SimPin pin in s.OutputPins)
					{
						if (pin.ID == address.PinID) return pin;
					}
				}
			}

			foreach (SimPin pin in InputPins)
			{
				if (pin.ID == address.PinOwnerID) return pin;
			}

			foreach (SimPin pin in OutputPins)
			{
				if (pin.ID == address.PinOwnerID) return pin;
			}

			throw new Exception("Failed to find pin with address: " + address.PinID + ", " + address.PinOwnerID);
		}


		public void RemoveSubChip(int id)
		{
			SubChips = SubChips.Where(s => s.ID != id).ToArray();
		}


		public void AddPin(SimPin pin, bool isInput)
		{
			if (isInput)
			{
				Array.Resize(ref InputPins, InputPins.Length + 1);
				InputPins[^1] = pin;
			}
			else
			{
				Array.Resize(ref OutputPins, OutputPins.Length + 1);
				OutputPins[^1] = pin;
			}
		}

		static SimPin CreateSimPinFromDescription(PinDescription desc, bool isInput, SimChip parent) => new(desc.ID, isInput, parent);

		public void RemovePin(int removePinID)
		{
			InputPins = InputPins.Where(p => p.ID != removePinID).ToArray();
			OutputPins = OutputPins.Where(p => p.ID != removePinID).ToArray();
		}

		public void AddSubChip(SimChip subChip)
		{
			Array.Resize(ref SubChips, SubChips.Length + 1);
			SubChips[^1] = subChip;
		}

		public void AddConnection(PinAddress sourcePinAddress, PinAddress targetPinAddress)
		{
			try
			{
				SimPin sourcePin = GetSimPinFromAddress(sourcePinAddress);
				(SimPin targetPin, SimChip targetChip) = GetSimPinFromAddressWithChip(targetPinAddress);


				Array.Resize(ref sourcePin.ConnectedTargetPins, sourcePin.ConnectedTargetPins.Length + 1);
				sourcePin.ConnectedTargetPins[^1] = targetPin;
				targetPin.numInputConnections++;
				if (targetPin.numInputConnections == 1 && targetChip != null) targetChip.numConnectedInputs++;
			}
			catch (Exception)
			{
				// Can fail to find pin if player has edited an existing chip to remove the pin, and then a chip is opened which uses the old version of that modified chip.
				// In that case we just ignore the failure and no connection is made.
			}
		}

		public void RemoveConnection(PinAddress sourcePinAddress, PinAddress targetPinAddress)
		{
			SimPin sourcePin = GetSimPinFromAddress(sourcePinAddress);
			(SimPin removeTargetPin, SimChip targetChip) = GetSimPinFromAddressWithChip(targetPinAddress);

			// Remove first matching connection
			for (int i = 0; i < sourcePin.ConnectedTargetPins.Length; i++)
			{
				if (sourcePin.ConnectedTargetPins[i] == removeTargetPin)
				{
					SimPin[] newArray = new SimPin[sourcePin.ConnectedTargetPins.Length - 1];
					Array.Copy(sourcePin.ConnectedTargetPins, 0, newArray, 0, i);
					Array.Copy(sourcePin.ConnectedTargetPins, i + 1, newArray, i, sourcePin.ConnectedTargetPins.Length - i - 1);
					sourcePin.ConnectedTargetPins = newArray;

					removeTargetPin.numInputConnections -= 1;
					if (removeTargetPin.numInputConnections == 0)
					{
						PinState.SetAllDisconnected(ref removeTargetPin.State);
						removeTargetPin.latestSourceID = -1;
						removeTargetPin.latestSourceParentChipID = -1;
						if (targetChip != null) removeTargetPin.parentChip.numConnectedInputs--;
					}

					break;
				}
			}
		}
	}
}