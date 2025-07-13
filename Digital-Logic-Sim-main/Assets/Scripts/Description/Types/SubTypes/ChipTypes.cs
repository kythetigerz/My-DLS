namespace DLS.Description
{
	public enum ChipType
	{
		Custom,

		// ---- Bulit in computer chips ----
		EdgeFunction,
		EdgeFunction3Merge32Bit,
		ColorInterpolationMath,
		SolidStateDrive,
		Multiplication32,
		Division32,
		Addition32,

		// ---- Basic Chips ----
		Nand,
		TriStateBuffer,
		Clock,
		Pulse,
		Keyboard,
		Mouse,

		// ---- Basic Logic Gates (Builtin) ----
		B_And_1Bit,
		B_And_4Bit,
		B_And_8Bit,
		B_And_16Bit,
		B_And_32Bit,
		B_Not_1Bit,
		B_Not_4Bit,
		B_Not_8Bit,
		B_Not_16Bit,
		B_Not_32Bit,
		B_Or_1Bit,
		B_Or_4Bit,
		B_Or_8Bit,
		B_Or_16Bit,
		B_Or_32Bit,
		B_Xor_1Bit,
		B_Xor_4Bit,
		B_Xor_8Bit,
		B_Xor_16Bit,
		B_Xor_32Bit,
		B_Xnor_1Bit,
		B_Xnor_4Bit,
		B_Xnor_8Bit,
		B_Xnor_16Bit,
		B_Xnor_32Bit,
		B_Nor_1Bit,
		B_Nor_4Bit,
		B_Nor_8Bit,
		B_Nor_16Bit,
		B_Nor_32Bit,
		B_TriStateBuffer_1Bit,
		B_TriStateBuffer_4Bit,
		B_TriStateBuffer_8Bit,
		B_TriStateBuffer_16Bit,
		B_TriStateBuffer_32Bit,

		// ---- Counters ----
		B_Counter_4Bit,
		B_Counter_8Bit,
		B_Counter_16Bit,
		B_Counter_32Bit,
		B_Counter_64Bit,

		// ---- Comparators ----
		B_Equals_4Bit,
		B_Equals_8Bit,
		B_Equals_16Bit,
		B_Equals_32Bit,
		B_Equals_64Bit,

		// ---- Utility ----
		B_FirstTick,

		// ---- Memory ----
		dev_Ram_8Bit,
		Rom_256x16,

		// ---- Displays ----
		SevenSegmentDisplay,
		DisplayRGB,
		Display1080RGB,
		DisplayDot,
		DisplayLED,

		// ---- Merge / Split ----
		Merge_1To4Bit,
		Merge_1To8Bit,
		Merge_4To8Bit,
		Split_4To1Bit,
		Split_8To4Bit,
		Split_8To1Bit,
		Merge_1To16Bit,
		Merge_4To16Bit,
		Merge_8To16Bit,
		Split_16To1Bit,
		Split_16To4Bit,
		Split_16To8Bit,
		Merge_1To32Bit,
		Merge_1To64Bit,
		Merge_4To32Bit,
		Merge_4To64Bit,
		Merge_8To32Bit,
		Merge_8To64Bit,
		Merge_16To32Bit,
		Merge_16To64Bit,
		Split_32To1Bit,
		Split_32To4Bit,
		Split_32To8Bit,
		Split_32To16Bit,
		Merge_32To64Bit,
		Split_64To1Bit,
		Split_64To4Bit,
		Split_64To8Bit,
		Split_64To16Bit,
		Split_64To32Bit,

		// ---- In / Out Pins ----
		In_1Bit,
		In_4Bit,
		In_8Bit,
		In_16Bit,
		In_32Bit,
		In_64Bit,

		Out_1Bit,
		Out_4Bit,
		Out_8Bit,
		Out_16Bit,
		Out_32Bit,
		Out_64Bit,

		Key,

		// ---- Buses ----
		Bus_1Bit,
		BusTerminus_1Bit,
		Bus_4Bit,
		BusTerminus_4Bit,
		Bus_8Bit,
		BusTerminus_8Bit,
		Bus_16Bit,
		BusTerminus_16Bit,
		Bus_32Bit,
		BusTerminus_32Bit,
		Bus_64Bit,
		BusTerminus_64Bit
	}
}