using System;
using System.Collections.Generic;

namespace DLS.Description
{
	public static class ChipTypeHelper
	{
		const string mulSymbol = "\u00d7";

		static readonly Dictionary<ChipType, string> Names = new()
		{
			// ---- Bulit in computer chips ----
			{ ChipType.EdgeFunction, "EDGE FUNCTION " },
			{ ChipType.ColorInterpolationMath, "Color Interpolation Math" },
			{ ChipType.EdgeFunction3Merge32Bit, "EDGE FUNCTION 3-MERGE 32-BIT" },
			{ ChipType.EdgeFunction3Merge32BitCHUNK, "EDGE FUNCTION 3-MERGE 32-BIT CHUNK" },
			{ ChipType.SolidStateDrive, "Solid State Drive" },
			// ---- Basic Chips ----
			{ ChipType.Nand, "NAND" },
			{ ChipType.Clock, "CLOCK" },
			{ ChipType.Pulse, "PULSE" },
			{ ChipType.TriStateBuffer, "3-STATE BUFFER" },
			// ---- Basic Logic Gates (Builtin) ----
			{ ChipType.B_And_1Bit, "B-AND-1" },
			{ ChipType.B_And_4Bit, "B-AND-4" },
			{ ChipType.B_And_8Bit, "B-AND-8" },
			{ ChipType.B_And_16Bit, "B-AND-16" },
			{ ChipType.B_And_32Bit, "B-AND-32" },
			{ ChipType.B_Not_1Bit, "B-NOT-1" },
			{ ChipType.B_Not_4Bit, "B-NOT-4" },
			{ ChipType.B_Not_8Bit, "B-NOT-8" },
			{ ChipType.B_Not_16Bit, "B-NOT-16" },
			{ ChipType.B_Not_32Bit, "B-NOT-32" },
			{ ChipType.B_Or_1Bit, "B-OR-1" },
			{ ChipType.B_Or_4Bit, "B-OR-4" },
			{ ChipType.B_Or_8Bit, "B-OR-8" },
			{ ChipType.B_Or_16Bit, "B-OR-16" },
			{ ChipType.B_Or_32Bit, "B-OR-32" },
			{ ChipType.B_Xor_1Bit, "B-XOR-1" },
			{ ChipType.B_Xor_4Bit, "B-XOR-4" },
			{ ChipType.B_Xor_8Bit, "B-XOR-8" },
			{ ChipType.B_Xor_16Bit, "B-XOR-16" },
			{ ChipType.B_Xor_32Bit, "B-XOR-32" },
			{ ChipType.B_Xnor_1Bit, "B-XNOR-1" },
			{ ChipType.B_Xnor_4Bit, "B-XNOR-4" },
			{ ChipType.B_Xnor_8Bit, "B-XNOR-8" },
			{ ChipType.B_Xnor_16Bit, "B-XNOR-16" },
			{ ChipType.B_Xnor_32Bit, "B-XNOR-32" },
			{ ChipType.B_Nor_1Bit, "B-NOR-1" },
			{ ChipType.B_Nor_4Bit, "B-NOR-4" },
			{ ChipType.B_Nor_8Bit, "B-NOR-8" },
			{ ChipType.B_Nor_16Bit, "B-NOR-16" },
			{ ChipType.B_Nor_32Bit, "B-NOR-32" },
			{ ChipType.B_TriStateBuffer_1Bit, "B-3STATE-1" },
			{ ChipType.B_TriStateBuffer_4Bit, "B-3STATE-4" },
			{ ChipType.B_TriStateBuffer_8Bit, "B-3STATE-8" },
			{ ChipType.B_TriStateBuffer_16Bit, "B-3STATE-16" },
			{ ChipType.B_TriStateBuffer_32Bit, "B-3STATE-32" },
			// ---- Counters ----
			{ ChipType.B_Counter_4Bit, "B-COUNTER-4" },
			{ ChipType.B_Counter_8Bit, "B-COUNTER-8" },
			{ ChipType.B_Counter_16Bit, "B-COUNTER-16" },
			{ ChipType.B_Counter_32Bit, "B-COUNTER-32" },
			{ ChipType.B_Counter_64Bit, "B-COUNTER-64" },
			// ---- Comparators ----
			{ ChipType.B_Equals_4Bit, "B-EQUALS-4" },
			{ ChipType.B_Equals_8Bit, "B-EQUALS-8" },
			{ ChipType.B_Equals_16Bit, "B-EQUALS-16" },
			{ ChipType.B_Equals_32Bit, "B-EQUALS-32" },
			{ ChipType.B_Equals_64Bit, "B-EQUALS-64" },
			// ---- Utility ----
			{ ChipType.B_FirstTick, "B-FIRST-TICK" },
			// ---- Memory ----
			{ ChipType.dev_Ram_8Bit, "dev.RAM-8" },
			{ ChipType.Rom_256x16, $"ROM 256{mulSymbol}16" },
			// ---- Split / Merge ----
			{ ChipType.Split_4To1Bit, "4-1BIT" },
			{ ChipType.Split_8To1Bit, "8-1BIT" },
			{ ChipType.Split_8To4Bit, "8-4BIT" },
			{ ChipType.Merge_4To8Bit, "4-8BIT" },
			{ ChipType.Merge_1To8Bit, "1-8BIT" },
			{ ChipType.Merge_1To4Bit, "1-4BIT" },

			{ ChipType.Merge_1To16Bit, "1-16BIT" },
			{ ChipType.Split_16To1Bit, "16-1BIT" },
			{ ChipType.Split_16To4Bit, "16-4BIT" },
			{ ChipType.Split_16To8Bit, "16-8BIT" },
			{ ChipType.Merge_4To16Bit, "4-16BIT" },
			{ ChipType.Merge_8To16Bit, "8-16BIT" },

			{ ChipType.Split_64To1Bit, "64-1BIT" },
			{ ChipType.Split_64To4Bit, "64-4BIT" },
			{ ChipType.Split_64To8Bit, "64-8BIT" },
			{ ChipType.Split_64To16Bit, "64-16BIT" },
			{ ChipType.Split_64To32Bit, "64-32BIT" },
			{ ChipType.Merge_32To64Bit, "32-64BIT" },
			{ ChipType.Merge_16To64Bit, "16-64BIT" },
			{ ChipType.Merge_8To64Bit, "8-64BIT" },
			{ ChipType.Merge_4To64Bit, "4-64BIT" },
			{ ChipType.Merge_1To64Bit, "1-64BIT" },

			{ ChipType.Split_32To1Bit, "32-1BIT" },
			{ ChipType.Split_32To4Bit, "32-4BIT" },
			{ ChipType.Split_32To8Bit, "32-8BIT" },
			{ ChipType.Split_32To16Bit, "32-16BIT" },
			{ ChipType.Merge_16To32Bit, "16-32BIT" },
			{ ChipType.Merge_8To32Bit, "8-32BIT" },
			{ ChipType.Merge_4To32Bit, "4-32BIT" },
			{ ChipType.Merge_1To32Bit, "1-32BIT" },

			// ---- Displays -----
			{ ChipType.DisplayRGB, "RGB DISPLAY" },
			{ ChipType.Display1080RGB, "1080 RGB DISPLAY" },
			{ ChipType.DisplayDot, "DOT DISPLAY" },
			{ ChipType.SevenSegmentDisplay, "7-SEGMENT" },
			{ ChipType.DisplayLED, "LED" },

			// ---- Not really chips (but convenient to treat them as such anyway) ----

			// ---- Inputs/Outputs ----
			{ ChipType.In_1Bit, "IN-1" },
			{ ChipType.In_4Bit, "IN-4" },
			{ ChipType.In_8Bit, "IN-8" },
			{ ChipType.In_16Bit, "IN-16" },
			{ ChipType.In_32Bit, "IN-32" },
			{ ChipType.In_64Bit, "IN-64" },
			{ ChipType.Out_1Bit, "OUT-1" },
			{ ChipType.Out_4Bit, "OUT-4" },
			{ ChipType.Out_8Bit, "OUT-8" },
			{ ChipType.Out_16Bit, "OUT-16" },
			{ ChipType.Out_32Bit, "OUT-32" },
			{ ChipType.Out_64Bit, "OUT-64" },
			{ ChipType.Key, "KEY" },
			// ---- Buses ----
			{ ChipType.Bus_1Bit, "BUS-1" },
			{ ChipType.Bus_4Bit, "BUS-4" },
			{ ChipType.Bus_8Bit, "BUS-8" },
			{ ChipType.Bus_16Bit, "BUS-16" },
			{ ChipType.Bus_32Bit, "BUS-32" },
			{ ChipType.Bus_64Bit, "BUS-64" },
			{ ChipType.BusTerminus_1Bit, "BUS-TERMINUS-1" },
			{ ChipType.BusTerminus_4Bit, "BUS-TERMINUS-4" },
			{ ChipType.BusTerminus_8Bit, "BUS-TERMINUS-8" },
			{ ChipType.BusTerminus_16Bit, "BUS-TERMINUS-16" },
			{ ChipType.BusTerminus_32Bit, "BUS-TERMINUS-32" },
			{ ChipType.BusTerminus_64Bit, "BUS-TERMINUS-64" }
		};

		public static string GetName(ChipType type) => Names[type];

		public static bool IsBusType(ChipType type) => IsBusOriginType(type) || IsBusTerminusType(type);

		public static bool IsBusOriginType(ChipType type) => type is ChipType.Bus_1Bit or ChipType.Bus_4Bit or ChipType.Bus_8Bit or ChipType.Bus_16Bit or ChipType.Bus_32Bit or ChipType.Bus_64Bit;

		public static bool IsBusTerminusType(ChipType type) => type is ChipType.BusTerminus_1Bit or ChipType.BusTerminus_4Bit or ChipType.BusTerminus_8Bit or ChipType.BusTerminus_16Bit or ChipType.BusTerminus_32Bit or ChipType.BusTerminus_64Bit;

		public static bool IsRomType(ChipType type) => type == ChipType.Rom_256x16;

		public static ChipType GetCorrespondingBusTerminusType(ChipType type)
		{
			return type switch
			{
				ChipType.Bus_1Bit => ChipType.BusTerminus_1Bit,
				ChipType.Bus_4Bit => ChipType.BusTerminus_4Bit,
				ChipType.Bus_8Bit => ChipType.BusTerminus_8Bit,
				ChipType.Bus_16Bit => ChipType.BusTerminus_16Bit,
				ChipType.Bus_32Bit => ChipType.BusTerminus_32Bit,
				ChipType.Bus_64Bit => ChipType.BusTerminus_64Bit,
				_ => throw new Exception("No corresponding bus terminus found for type: " + type)
			};
		}

		public static ChipType GetPinType(bool isInput, PinBitCount numBits)
		{
			if (isInput)
			{
				return numBits switch
				{
					PinBitCount.Bit1 => ChipType.In_1Bit,
					PinBitCount.Bit4 => ChipType.In_4Bit,
					PinBitCount.Bit8 => ChipType.In_8Bit,
					PinBitCount.Bit16 => ChipType.In_16Bit,
					PinBitCount.Bit32 => ChipType.In_32Bit,
					PinBitCount.Bit64 => ChipType.In_64Bit,
					_ => throw new Exception("No input pin type found for bitcount: " + numBits)
				};
			}

			return numBits switch
			{
				PinBitCount.Bit1 => ChipType.Out_1Bit,
				PinBitCount.Bit4 => ChipType.Out_4Bit,
				PinBitCount.Bit8 => ChipType.Out_8Bit,
				PinBitCount.Bit16 => ChipType.Out_16Bit,
				PinBitCount.Bit32 => ChipType.Out_32Bit,
				PinBitCount.Bit64 => ChipType.Out_64Bit,
				_ => throw new Exception("No output pin type found for bitcount: " + numBits)
			};
		}

		public static (bool isInput, bool isOutput, PinBitCount numBits) IsInputOrOutputPin(ChipType type)
		{
			return type switch
			{
				ChipType.In_1Bit => (true, false, PinBitCount.Bit1),
				ChipType.Out_1Bit => (false, true, PinBitCount.Bit1),
				ChipType.In_4Bit => (true, false, PinBitCount.Bit4),
				ChipType.Out_4Bit => (false, true, PinBitCount.Bit4),
				ChipType.In_8Bit => (true, false, PinBitCount.Bit8),
				ChipType.Out_8Bit => (false, true, PinBitCount.Bit8),
				ChipType.In_16Bit => (true, false, PinBitCount.Bit16),
				ChipType.Out_16Bit => (false, true, PinBitCount.Bit16),
				ChipType.In_32Bit => (true, false, PinBitCount.Bit32),
				ChipType.Out_32Bit => (false, true, PinBitCount.Bit32),
				ChipType.In_64Bit => (true, false, PinBitCount.Bit64),
				ChipType.Out_64Bit => (false, true, PinBitCount.Bit64),
				_ => (false, false, PinBitCount.Bit1)
			};
		}
	}
}