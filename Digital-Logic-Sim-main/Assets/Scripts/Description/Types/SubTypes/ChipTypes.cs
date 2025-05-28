namespace DLS.Description
{
	public enum ChipType
	{
		Custom,

		// ---- Bulit in computer chips ----
		EdgeFunction,
		ColorInterpolationMath,
		SolidStateDrive,

		// ---- Basic Chips ----
		Nand,
		TriStateBuffer,
		Clock,
		Pulse,

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