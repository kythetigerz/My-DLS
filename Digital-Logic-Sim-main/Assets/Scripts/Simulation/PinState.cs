namespace DLS.Simulation
{
	// Helper class for dealing with pin state.
	// Pin state is stored as a ulong (64 bits), with format:
	// Tristate flags (most significant 32 bits) | Bit states (least significant 32 bits)
	public static class PinState
	{
		// Each bit has three possible states (tri-state logic):
		public const uint LogicLow = 0;
		public const uint LogicHigh = 1;
		public const uint LogicDisconnected = 2;

		// Mask for single bit value (bit state, and tristate flag)
		public const ulong SingleBitMask = 1UL | (1UL << 32);
		
		public static ulong GetBitStates(ulong state) => state & 0xFFFFFFFFFFFFFFFF; // Use full 64-bit mask
		public static ulong GetTristateFlags(ulong state) => state >> 32; // Return ulong, not uint

		public static void Set(ref ulong state, ulong bitStates, uint tristateFlags)
		{
			state = bitStates | ((ulong)tristateFlags << 32);
		}

		public static void Set(ref ulong state, ulong other) => state = other;

		public static ushort GetBitTristatedValue(ulong state, int bitIndex)
		{
			// Ensure bitIndex is valid (0-63)
			if (bitIndex < 0 || bitIndex > 63)
			{
				return (ushort)PinState.LogicDisconnected; // Return disconnected for invalid bit indices
			}
			
			ushort bitState = (ushort)((GetBitStates(state) >> bitIndex) & 1);
			ushort tri = (ushort)((GetTristateFlags(state) >> bitIndex) & 1);
			return (ushort)(bitState | (tri << 1)); // Combine to form tri-stated value: 0 = LOW, 1 = HIGH, 2 = DISCONNECTED
		}

		public static bool FirstBitHigh(ulong state) => (state & 1) == LogicHigh;

		public static void Set4BitFrom8BitSource(ref ulong state, ulong source8bit, bool firstNibble)
		{
			ulong sourceBitStates = GetBitStates(source8bit);
			ulong sourceTristateFlags = GetTristateFlags(source8bit);

			if (firstNibble)
			{
				const uint mask = 0b1111;
				Set(ref state, (uint)(sourceBitStates & mask), (uint)(sourceTristateFlags & mask));
			}
			else
			{
				const uint mask = 0b11110000;
				Set(ref state, (uint)((sourceBitStates & mask) >> 4), (uint)((sourceTristateFlags & mask) >> 4));
			}
		}

		public static void Set8BitFrom4BitSources(ref ulong state, ulong a, ulong b)
		{
			uint bitStates = (uint)(GetBitStates(a) | (GetBitStates(b) << 4));
			uint tristateFlags = (uint)((GetTristateFlags(a) & 0b1111) | ((GetTristateFlags(b) & 0b1111) << 4));
			Set(ref state, bitStates, tristateFlags);
		}

		public static void Toggle(ref ulong state, int bitIndex)
		{
			// Ensure bitIndex is valid (0-63)
			if (bitIndex < 0 || bitIndex > 63)
			{
				return; // Do nothing for invalid bit indices
			}
			
			ulong bitStates = GetBitStates(state);
			
			// Use ulong for the bit mask to support all 64 bits
			bitStates ^= (1UL << bitIndex);

			// Clear tristate flags (can't be disconnected if toggling as only input dev pins are allowed)
			Set(ref state, bitStates, 0);
		}

		public static void SetAllDisconnected(ref ulong state) => Set(ref state, 0, uint.MaxValue);

		// Check if all bits are disconnected
		public static bool IsFullyDisconnected(ulong state)
		{
			// Check if all tristate flags are set to 1 (all bits are disconnected)
			return GetTristateFlags(state) == 0xFFFFFFFF;
		}
	}
}