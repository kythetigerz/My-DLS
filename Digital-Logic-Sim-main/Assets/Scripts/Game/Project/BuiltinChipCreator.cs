using System;
using System.Collections.Generic;
using DLS.Description;
using UnityEngine;
using static DLS.Graphics.DrawSettings;

namespace DLS.Game
{
	public static class BuiltinChipCreator
	{
		static readonly Color ChipCol_SplitMerge = new(0.1f, 0.1f, 0.1f); //new(0.8f, 0.8f, 0.8f);

		public static ChipDescription[] CreateAllBuiltinChipDescriptions()
		{
			return new[]
			{
				// ---- I/O Pins ----
				CreateInputOrOutputPin(ChipType.In_1Bit),
				CreateInputOrOutputPin(ChipType.Out_1Bit),
				CreateInputOrOutputPin(ChipType.In_4Bit),
				CreateInputOrOutputPin(ChipType.Out_4Bit),
				CreateInputOrOutputPin(ChipType.In_8Bit),
				CreateInputOrOutputPin(ChipType.Out_8Bit),
				CreateInputOrOutputPin(ChipType.In_16Bit),
				CreateInputOrOutputPin(ChipType.Out_16Bit),
				CreateInputOrOutputPin(ChipType.In_32Bit),
				CreateInputOrOutputPin(ChipType.Out_32Bit),
				CreateInputOrOutputPin(ChipType.In_64Bit),
				CreateInputOrOutputPin(ChipType.Out_64Bit),
				CreateInputKeyChip(),
				// ---- Basic Chips ----
				CreateNand(),
				CreateTristateBuffer(),
				CreateClock(),
				CreatePulse(),
				// ---- Basic Logic Gates (Builtin) ----
				CreateBasicLogicGate(ChipType.B_And_1Bit, "AND", PinBitCount.Bit1),
				CreateBasicLogicGate(ChipType.B_And_4Bit, "AND", PinBitCount.Bit4),
				CreateBasicLogicGate(ChipType.B_And_8Bit, "AND", PinBitCount.Bit8),
				CreateBasicLogicGate(ChipType.B_And_16Bit, "AND", PinBitCount.Bit16),
				CreateBasicLogicGate(ChipType.B_And_32Bit, "AND", PinBitCount.Bit32),
				CreateBasicLogicGate(ChipType.B_Not_1Bit, "NOT", PinBitCount.Bit1),
				CreateBasicLogicGate(ChipType.B_Not_4Bit, "NOT", PinBitCount.Bit4),
				CreateBasicLogicGate(ChipType.B_Not_8Bit, "NOT", PinBitCount.Bit8),
				CreateBasicLogicGate(ChipType.B_Not_16Bit, "NOT", PinBitCount.Bit16),
				CreateBasicLogicGate(ChipType.B_Not_32Bit, "NOT", PinBitCount.Bit32),
				CreateBasicLogicGate(ChipType.B_Or_1Bit, "OR", PinBitCount.Bit1),
				CreateBasicLogicGate(ChipType.B_Or_4Bit, "OR", PinBitCount.Bit4),
				CreateBasicLogicGate(ChipType.B_Or_8Bit, "OR", PinBitCount.Bit8),
				CreateBasicLogicGate(ChipType.B_Or_16Bit, "OR", PinBitCount.Bit16),
				CreateBasicLogicGate(ChipType.B_Or_32Bit, "OR", PinBitCount.Bit32),
				CreateBasicLogicGate(ChipType.B_Xor_1Bit, "XOR", PinBitCount.Bit1),
				CreateBasicLogicGate(ChipType.B_Xor_4Bit, "XOR", PinBitCount.Bit4),
				CreateBasicLogicGate(ChipType.B_Xor_8Bit, "XOR", PinBitCount.Bit8),
				CreateBasicLogicGate(ChipType.B_Xor_16Bit, "XOR", PinBitCount.Bit16),
				CreateBasicLogicGate(ChipType.B_Xor_32Bit, "XOR", PinBitCount.Bit32),
				CreateBasicLogicGate(ChipType.B_Xnor_1Bit, "XNOR", PinBitCount.Bit1),
				CreateBasicLogicGate(ChipType.B_Xnor_4Bit, "XNOR", PinBitCount.Bit4),
				CreateBasicLogicGate(ChipType.B_Xnor_8Bit, "XNOR", PinBitCount.Bit8),
				CreateBasicLogicGate(ChipType.B_Xnor_16Bit, "XNOR", PinBitCount.Bit16),
				CreateBasicLogicGate(ChipType.B_Xnor_32Bit, "XNOR", PinBitCount.Bit32),
				CreateBasicLogicGate(ChipType.B_Nor_1Bit, "NOR", PinBitCount.Bit1),
				CreateBasicLogicGate(ChipType.B_Nor_4Bit, "NOR", PinBitCount.Bit4),
				CreateBasicLogicGate(ChipType.B_Nor_8Bit, "NOR", PinBitCount.Bit8),
				CreateBasicLogicGate(ChipType.B_Nor_16Bit, "NOR", PinBitCount.Bit16),
				CreateBasicLogicGate(ChipType.B_Nor_32Bit, "NOR", PinBitCount.Bit32),
				CreateBuiltinTristateBuffer(ChipType.B_TriStateBuffer_1Bit, PinBitCount.Bit1),
				CreateBuiltinTristateBuffer(ChipType.B_TriStateBuffer_4Bit, PinBitCount.Bit4),
				CreateBuiltinTristateBuffer(ChipType.B_TriStateBuffer_8Bit, PinBitCount.Bit8),
				CreateBuiltinTristateBuffer(ChipType.B_TriStateBuffer_16Bit, PinBitCount.Bit16),
				CreateBuiltinTristateBuffer(ChipType.B_TriStateBuffer_32Bit, PinBitCount.Bit32),
				// ---- Counters ----
				CreateCounter(ChipType.B_Counter_4Bit, PinBitCount.Bit4),
				CreateCounter(ChipType.B_Counter_8Bit, PinBitCount.Bit8),
				CreateCounter(ChipType.B_Counter_16Bit, PinBitCount.Bit16),
				CreateCounter(ChipType.B_Counter_32Bit, PinBitCount.Bit32),
				CreateCounter(ChipType.B_Counter_64Bit, PinBitCount.Bit64),
				// ---- Comparators ----
				CreateEquals(ChipType.B_Equals_4Bit, PinBitCount.Bit4),
				CreateEquals(ChipType.B_Equals_8Bit, PinBitCount.Bit8),
				CreateEquals(ChipType.B_Equals_16Bit, PinBitCount.Bit16),
				CreateEquals(ChipType.B_Equals_32Bit, PinBitCount.Bit32),
				CreateEquals(ChipType.B_Equals_64Bit, PinBitCount.Bit64),
				// ---- Utility ----
				CreateFirstTick(),
				// ---- Memory ----
				dev_CreateRAM_8(),
				CreateROM_8(),
				// ---- Merge / Split ----
				CreateBitConversionChip(ChipType.Merge_1To4Bit, PinBitCount.Bit1, PinBitCount.Bit4, 4, 1),
				CreateBitConversionChip(ChipType.Merge_1To8Bit, PinBitCount.Bit1, PinBitCount.Bit8, 8, 1),
				CreateBitConversionChip(ChipType.Merge_1To16Bit, PinBitCount.Bit1, PinBitCount.Bit16, 16, 1),
				CreateBitConversionChip(ChipType.Merge_1To32Bit, PinBitCount.Bit1, PinBitCount.Bit32, 32, 1),
				CreateBitConversionChip(ChipType.Merge_1To64Bit, PinBitCount.Bit1, PinBitCount.Bit64, 64, 1),

				CreateBitConversionChip(ChipType.Split_4To1Bit, PinBitCount.Bit4, PinBitCount.Bit1, 1, 4),
				CreateBitConversionChip(ChipType.Merge_4To8Bit, PinBitCount.Bit4, PinBitCount.Bit8, 2, 1),
				CreateBitConversionChip(ChipType.Merge_4To16Bit, PinBitCount.Bit4, PinBitCount.Bit16, 4, 1),
				CreateBitConversionChip(ChipType.Merge_4To32Bit, PinBitCount.Bit4, PinBitCount.Bit32, 8, 1),
				CreateBitConversionChip(ChipType.Merge_4To64Bit, PinBitCount.Bit4, PinBitCount.Bit64, 16, 1),

				CreateBitConversionChip(ChipType.Split_8To1Bit, PinBitCount.Bit8, PinBitCount.Bit1, 1, 8),
				CreateBitConversionChip(ChipType.Split_8To4Bit, PinBitCount.Bit8, PinBitCount.Bit4, 1, 2),
				CreateBitConversionChip(ChipType.Merge_8To16Bit, PinBitCount.Bit8, PinBitCount.Bit16, 2, 1),
				CreateBitConversionChip(ChipType.Merge_8To32Bit, PinBitCount.Bit8, PinBitCount.Bit32, 4, 1),
				CreateBitConversionChip(ChipType.Merge_8To64Bit, PinBitCount.Bit8, PinBitCount.Bit64, 8, 1),

				CreateBitConversionChip(ChipType.Split_16To1Bit, PinBitCount.Bit16, PinBitCount.Bit1, 1, 16),
				CreateBitConversionChip(ChipType.Split_16To4Bit, PinBitCount.Bit16, PinBitCount.Bit4, 1, 4),
				CreateBitConversionChip(ChipType.Split_16To8Bit, PinBitCount.Bit16, PinBitCount.Bit8, 1, 2),
				CreateBitConversionChip(ChipType.Merge_16To32Bit, PinBitCount.Bit16, PinBitCount.Bit32, 2, 1),
				CreateBitConversionChip(ChipType.Merge_16To64Bit, PinBitCount.Bit16, PinBitCount.Bit64, 4, 1),

				CreateBitConversionChip(ChipType.Split_32To1Bit, PinBitCount.Bit32, PinBitCount.Bit1, 1, 32),
				CreateBitConversionChip(ChipType.Split_32To4Bit, PinBitCount.Bit32, PinBitCount.Bit4, 1, 8),
				CreateBitConversionChip(ChipType.Split_32To8Bit, PinBitCount.Bit32, PinBitCount.Bit8, 1, 4),
				CreateBitConversionChip(ChipType.Split_32To16Bit, PinBitCount.Bit32, PinBitCount.Bit16, 1, 2),
				CreateBitConversionChip(ChipType.Merge_32To64Bit, PinBitCount.Bit32, PinBitCount.Bit64, 2, 1),

				CreateBitConversionChip(ChipType.Split_64To1Bit, PinBitCount.Bit64, PinBitCount.Bit1, 1, 64),
				CreateBitConversionChip(ChipType.Split_64To4Bit, PinBitCount.Bit64, PinBitCount.Bit4, 1, 16),
				CreateBitConversionChip(ChipType.Split_64To8Bit, PinBitCount.Bit64, PinBitCount.Bit8, 1, 8),
				CreateBitConversionChip(ChipType.Split_64To16Bit, PinBitCount.Bit64, PinBitCount.Bit16, 1, 4),
				CreateBitConversionChip(ChipType.Split_64To32Bit, PinBitCount.Bit64, PinBitCount.Bit32, 1, 2),
				// ---- Displays ----
				CreateDisplay7Seg(),
				CreateDisplayRGB(),
				CreateDisplayDot(),
				CreateDisplayLED(),
				CreateDisplay1080RGB8bit(),
				// ---- Built in items for computer----
				CreateEdgeFunction(),
				CreateColorInterpolationMath(),
				CreateSolidStateDrive(),
				CreateEdgeFunction3Merge32Bit(),
				// ---- Math Operations ----
				CreateAddition32(),
				CreateMultiplication32(),
				CreateDivision32(),
				// ---- Bus ----
				CreateBus(PinBitCount.Bit1),
				CreateBusTerminus(PinBitCount.Bit1),
				CreateBus(PinBitCount.Bit4),
				CreateBusTerminus(PinBitCount.Bit4),
				CreateBus(PinBitCount.Bit8),
				CreateBusTerminus(PinBitCount.Bit8),
				CreateBus(PinBitCount.Bit16),
				CreateBusTerminus(PinBitCount.Bit16),
				CreateBus(PinBitCount.Bit32),
				CreateBusTerminus(PinBitCount.Bit32),
				CreateBus(PinBitCount.Bit64),
				CreateBusTerminus(PinBitCount.Bit64)
			};
		}

		static ChipDescription CreateNand()
		{
			Color col = new(0.73f, 0.26f, 0.26f);
			Vector2 size = new(CalculateGridSnappedWidth(GridSize * 8), GridSize * 4);

			PinDescription[] inputPins = { CreatePinDescription("IN B", 0), CreatePinDescription("IN A", 1) };
			PinDescription[] outputPins = { CreatePinDescription("OUT", 2) };

			return CreateBuiltinChipDescription(ChipType.Nand, size, col, inputPins, outputPins);
		}

		static ChipDescription dev_CreateRAM_8()
		{
			Color col = new(0.85f, 0.45f, 0.3f);

			PinDescription[] inputPins =
			{
				CreatePinDescription("ADDRESS", 0, PinBitCount.Bit8),
				CreatePinDescription("DATA", 1, PinBitCount.Bit8),
				CreatePinDescription("WRITE", 2),
				CreatePinDescription("RESET", 3),
				CreatePinDescription("CLOCK", 4)
			};
			PinDescription[] outputPins = { CreatePinDescription("OUT", 5, PinBitCount.Bit8) };
			Vector2 size = new(GridSize * 10, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

			return CreateBuiltinChipDescription(ChipType.dev_Ram_8Bit, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateROM_8()
		{
			PinDescription[] inputPins =
			{
				CreatePinDescription("ADDRESS", 0, PinBitCount.Bit8)
			};
			PinDescription[] outputPins =
			{
				CreatePinDescription("OUT B", 1, PinBitCount.Bit8),
				CreatePinDescription("OUT A", 2, PinBitCount.Bit8)
			};

			Color col = new(0.25f, 0.35f, 0.5f);
			Vector2 size = new(GridSize * 12, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

			return CreateBuiltinChipDescription(ChipType.Rom_256x16, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateInputKeyChip()
		{
			Color col = new(0.1f, 0.1f, 0.1f);
			Vector2 size = new Vector2(GridSize, GridSize) * 3;

			PinDescription[] outputPins = { CreatePinDescription("OUT", 0) };

			return CreateBuiltinChipDescription(ChipType.Key, size, col, null, outputPins);
		}


		static ChipDescription CreateTristateBuffer()
		{
			Color col = new(0.1f, 0.1f, 0.1f);
			Vector2 size = new(CalculateGridSnappedWidth(1.5f), GridSize * 5);

			PinDescription[] inputPins = { CreatePinDescription("IN", 0), CreatePinDescription("ENABLE", 1) };
			PinDescription[] outputPins = { CreatePinDescription("OUT", 2) };

			return CreateBuiltinChipDescription(ChipType.TriStateBuffer, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateClock()
		{
			Vector2 size = new(GridHelper.SnapToGrid(1), GridSize * 3);
			Color col = new(0.1f, 0.1f, 0.1f);
			PinDescription[] outputPins = { CreatePinDescription("CLK", 0) };

			return CreateBuiltinChipDescription(ChipType.Clock, size, col, null, outputPins);
		}

		static ChipDescription CreatePulse()
		{
			Vector2 size = new(GridHelper.SnapToGrid(1), GridSize * 3);
			Color col = new(0.1f, 0.1f, 0.1f);
			PinDescription[] inputPins = { CreatePinDescription("IN", 0) };
			PinDescription[] outputPins = { CreatePinDescription("PULSE", 1) };

			return CreateBuiltinChipDescription(ChipType.Pulse, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateBitConversionChip(ChipType chipType, PinBitCount bitCountIn, PinBitCount bitCountOut, int numIn, int numOut)
		{
			PinDescription[] inputPins = new PinDescription[numIn];
			PinDescription[] outputPins = new PinDescription[numOut];

			for (int i = 0; i < numIn; i++)
			{
				string pinName = GetPinName(i, numIn, true);
				inputPins[i] = CreatePinDescription(pinName, i, bitCountIn);
			}

			for (int i = 0; i < numOut; i++)
			{
				string pinName = GetPinName(i, numOut, false);
				outputPins[i] = CreatePinDescription(pinName, numIn + i, bitCountOut);
			}

			float height = SubChipInstance.MinChipHeightForPins(inputPins, outputPins);
			Vector2 size = new(GridSize * 9, height);

			return CreateBuiltinChipDescription(chipType, size, ChipCol_SplitMerge, inputPins, outputPins);
		}

		static string GetPinName(int pinIndex, int pinCount, bool isInput)
		{
			string letter = " " + (char)('A' + pinCount - pinIndex - 1);
			if (pinCount == 1) letter = "";
			return (isInput ? "IN" : "OUT") + letter;
		}

		static ChipDescription CreateDisplay7Seg()
		{
			PinDescription[] inputPins =
			{
				CreatePinDescription("A", 0),
				CreatePinDescription("B", 1),
				CreatePinDescription("C", 2),
				CreatePinDescription("D", 3),
				CreatePinDescription("E", 4),
				CreatePinDescription("F", 5),
				CreatePinDescription("G", 6),
				CreatePinDescription("COL", 7)
			};

			Color col = new(0.1f, 0.1f, 0.1f);
			float height = SubChipInstance.MinChipHeightForPins(inputPins, null);
			Vector2 size = new(GridSize * 10, height);
			float displayWidth = size.x - GridSize * 2;

			DisplayDescription[] displays =
			{
				new()
				{
					Position = Vector2.right * PinRadius / 3 * 0,
					Scale = displayWidth,
					SubChipID = -1
				}
			};
			return CreateBuiltinChipDescription(ChipType.SevenSegmentDisplay, size, col, inputPins, null, displays, NameDisplayLocation.Hidden);
		}

		static ChipDescription CreateDisplayRGB()
		{
			float height = GridSize * 21;
			float width = height;
			float displayWidth = height - GridSize * 2;

			Color col = new(0.1f, 0.1f, 0.1f);
			Vector2 size = new(width, height);

			PinDescription[] inputPins =
			{
				CreatePinDescription("ADDRESS", 0, PinBitCount.Bit8),
				CreatePinDescription("RED", 1, PinBitCount.Bit4),
				CreatePinDescription("GREEN", 2, PinBitCount.Bit4),
				CreatePinDescription("BLUE", 3, PinBitCount.Bit4),
				CreatePinDescription("RESET", 4),
				CreatePinDescription("WRITE", 5),
				CreatePinDescription("REFRESH", 6),
				CreatePinDescription("CLOCK", 7)
			};

			PinDescription[] outputPins =
			{
				CreatePinDescription("R OUT", 8, PinBitCount.Bit4),
				CreatePinDescription("G OUT", 9, PinBitCount.Bit4),
				CreatePinDescription("B OUT", 10, PinBitCount.Bit4)
			};

			DisplayDescription[] displays =
			{
				new()
				{
					Position = Vector2.zero,
					Scale = displayWidth,
					SubChipID = -1
				}
			};

			return CreateBuiltinChipDescription(ChipType.DisplayRGB, size, col, inputPins, outputPins, displays, NameDisplayLocation.Hidden);
		}

		static ChipDescription CreateDisplay1080RGB8bit()
		{
			float height = GridSize * 86;
			float width = height * 18 / 9; // 16:9 aspect ratio for 1920x1080
			float displayWidth = height - GridSize * 2;

			Color col = new(0.1f, 0.1f, 0.1f);
			Vector2 size = new(width, height);

			PinDescription[] inputPins =
			{
				CreatePinDescription("ADDRESS", 0, PinBitCount.Bit32), // 32-bit address for 1080x1920 pixels
				CreatePinDescription("RED", 1, PinBitCount.Bit8),      // 8-bit color channels
				CreatePinDescription("GREEN", 2, PinBitCount.Bit8),
				CreatePinDescription("BLUE", 3, PinBitCount.Bit8),
				CreatePinDescription("RESET", 4),
				CreatePinDescription("WRITE", 5),
				CreatePinDescription("REFRESH", 6),
				CreatePinDescription("CLOCK", 7)
			};

			PinDescription[] outputPins =
			{
				CreatePinDescription("R OUT", 8, PinBitCount.Bit8),    // 8-bit color outputs
				CreatePinDescription("G OUT", 9, PinBitCount.Bit8),
				CreatePinDescription("B OUT", 10, PinBitCount.Bit8)
			};

			DisplayDescription[] displays =
			{
				new()
				{
					Position = Vector2.zero,
					Scale = displayWidth,
					SubChipID = -1
				}
			};

			return CreateBuiltinChipDescription(ChipType.Display1080RGB, size, col, inputPins, outputPins, displays, NameDisplayLocation.Hidden);
		}

		static ChipDescription CreateDisplayDot()
		{
			PinDescription[] inputPins =
			{
				CreatePinDescription("ADDRESS", 0, PinBitCount.Bit8),
				CreatePinDescription("PIXEL IN", 1),
				CreatePinDescription("RESET", 2),
				CreatePinDescription("WRITE", 3),
				CreatePinDescription("REFRESH", 4),
				CreatePinDescription("CLOCK", 5)
			};

			PinDescription[] outputPins =
			{
				CreatePinDescription("PIXEL OUT", 6)
			};

			float height = SubChipInstance.MinChipHeightForPins(inputPins, null);
			float width = height;
			float displayWidth = height - GridSize * 2;

			Color col = new(0.1f, 0.1f, 0.1f);
			Vector2 size = new(width, height);


			DisplayDescription[] displays =
			{
				new()
				{
					Position = Vector2.right * PinRadius / 3 * 0,
					Scale = displayWidth,
					SubChipID = -1
				}
			};

			return CreateBuiltinChipDescription(ChipType.DisplayDot, size, col, inputPins, outputPins, displays, NameDisplayLocation.Hidden);
		}

		static ChipDescription CreateEdgeFunction()
		{
			Color col = new Color(0.992f, 0.667f, 0.071f); // #FDAA12

			PinDescription[] inputPins =
			{
				CreatePinDescription("BY", 5, PinBitCount.Bit4),
				CreatePinDescription("BX", 4, PinBitCount.Bit4),
				CreatePinDescription("AY", 3, PinBitCount.Bit4),
				CreatePinDescription("AX", 2, PinBitCount.Bit4),
				CreatePinDescription("Y", 1, PinBitCount.Bit4),
				CreatePinDescription("X", 0, PinBitCount.Bit4),
			};

			PinDescription[] outputPins = { CreatePinDescription("OUT", 6, PinBitCount.Bit16) };

			Vector2 size = new(GridSize * 10, SubChipInstance.MinChipHeightForPins(inputPins, outputPins) + .75f);

			return CreateBuiltinChipDescription(ChipType.EdgeFunction, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateEdgeFunction3Merge32Bit()
		{
			Color col = new Color(0.992f, 0.667f, 0.071f); // #FDAA12

			PinDescription[] inputPins =
			{
				CreatePinDescription("CY", 11, PinBitCount.Bit32),
				CreatePinDescription("CX", 10, PinBitCount.Bit32),
				CreatePinDescription("BY", 9, PinBitCount.Bit32),
				CreatePinDescription("BX", 8, PinBitCount.Bit32),
				CreatePinDescription("AY", 7, PinBitCount.Bit32),
				CreatePinDescription("AX", 6, PinBitCount.Bit32),
				CreatePinDescription("Y", 1, PinBitCount.Bit32),
				CreatePinDescription("X", 0, PinBitCount.Bit32),
			};

			PinDescription[] outputPins = { CreatePinDescription("OUT", 12, PinBitCount.Bit1) };

			Vector2 size = new(GridSize * 12, SubChipInstance.MinChipHeightForPins(inputPins, outputPins) + .75f);

			return CreateBuiltinChipDescription(ChipType.EdgeFunction3Merge32Bit, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateColorInterpolationMath()
		{
			Color col = new Color(0.615f, 0.807f, 0.074f); //rgb(157, 207, 19)
														   //ABC = (Bx - Ax) * (Cy - Ay) - (By - Ay) * (Cx - Ax)
														   //weightA = BCP / ABC;
														   //weightB = CAP / ABC;
														   //weightC = ABP / ABC;
														   //r = colourA.r * weightA + colourB.r * weightB + colourC.r * weightC;
														   //g = colourA.g * weightA + colourB.g * weightB + colourC.g * weightC;
														   //b = colourA.b * weightA + colourB.b * weightB + colourC.b * weightC;


			PinDescription[] inputPins =
			{
				CreatePinDescription("BCP", 0, PinBitCount.Bit16),
				CreatePinDescription("CAP", 1, PinBitCount.Bit16),
				CreatePinDescription("ABP", 2, PinBitCount.Bit16),
				CreatePinDescription("BX", 3, PinBitCount.Bit4),
				CreatePinDescription("AX", 4, PinBitCount.Bit4),
				CreatePinDescription("CY", 5, PinBitCount.Bit4),
				CreatePinDescription("Ay", 6, PinBitCount.Bit4),
				CreatePinDescription("BY", 7, PinBitCount.Bit4),
				CreatePinDescription("CX", 8, PinBitCount.Bit4),
				CreatePinDescription("colorAR", 9, PinBitCount.Bit4),
				CreatePinDescription("colorAG", 10, PinBitCount.Bit4),
				CreatePinDescription("colorAB", 11, PinBitCount.Bit4),
				CreatePinDescription("colorBR", 12, PinBitCount.Bit4),
				CreatePinDescription("colorBG", 13, PinBitCount.Bit4),
				CreatePinDescription("colorBB", 14, PinBitCount.Bit4),
				CreatePinDescription("colorCR", 15, PinBitCount.Bit4),
				CreatePinDescription("colorCG", 16, PinBitCount.Bit4),
				CreatePinDescription("colorCB", 17, PinBitCount.Bit4),
			};

			PinDescription[] outputPins = {
				CreatePinDescription("BLUE", 18, PinBitCount.Bit4),
				CreatePinDescription("GREEN", 19, PinBitCount.Bit4),
				CreatePinDescription("RED", 20, PinBitCount.Bit4)
			};

			Vector2 size = new(GridSize * 23, SubChipInstance.MinChipHeightForPins(inputPins, outputPins) + 2f);

			return CreateBuiltinChipDescription(ChipType.ColorInterpolationMath, size, col, inputPins, outputPins);
		}

		public static ChipDescription CreateSolidStateDrive()
		{
			Color col = new Color(0.949f, 0.839f, 0.008f); //rgb(241, 214, 2)

			PinDescription[] inputPins =
			{
				// 8
				CreatePinDescription("Address 8", 41, PinBitCount.Bit32),
				CreatePinDescription("Data 8", 40, PinBitCount.Bit4),
				CreatePinDescription("Full Clear 8", 39, PinBitCount.Bit4),
				CreatePinDescription("Deallocate 8", 38, PinBitCount.Bit1),
				CreatePinDescription("Write 8", 37, PinBitCount.Bit1),
				// 7
				CreatePinDescription("Address 7", 36, PinBitCount.Bit32),
				CreatePinDescription("Data 7", 35, PinBitCount.Bit4),
				CreatePinDescription("Full Clear 7", 34, PinBitCount.Bit4),
				CreatePinDescription("Deallocate 7", 33, PinBitCount.Bit1),
				CreatePinDescription("Write 7", 32, PinBitCount.Bit1),
				// 6
				CreatePinDescription("Address 6", 31, PinBitCount.Bit32),
				CreatePinDescription("Data 6", 30, PinBitCount.Bit4),
				CreatePinDescription("Full Clear 6", 29, PinBitCount.Bit4),
				CreatePinDescription("Deallocate 6", 28, PinBitCount.Bit1),
				CreatePinDescription("Write 6", 27, PinBitCount.Bit1),
				// 5
				CreatePinDescription("Address 5", 26, PinBitCount.Bit32),
				CreatePinDescription("Data 5", 25, PinBitCount.Bit4),
				CreatePinDescription("Full Clear 5", 24, PinBitCount.Bit4),
				CreatePinDescription("Deallocate 5", 23, PinBitCount.Bit1),
				CreatePinDescription("Write 5", 22, PinBitCount.Bit1),
				// 4
				CreatePinDescription("Address 4", 21, PinBitCount.Bit32),
				CreatePinDescription("Data 4", 20, PinBitCount.Bit4),
				CreatePinDescription("Full Clear 4", 19, PinBitCount.Bit4),
				CreatePinDescription("Deallocate 4", 18, PinBitCount.Bit1),
				CreatePinDescription("Write 4", 17, PinBitCount.Bit1),
				// 3
				CreatePinDescription("Address 3", 16, PinBitCount.Bit32),
				CreatePinDescription("Data 3", 15, PinBitCount.Bit4),
				CreatePinDescription("Full Clear 3", 14, PinBitCount.Bit4),
				CreatePinDescription("Deallocate 3", 13, PinBitCount.Bit1),
				CreatePinDescription("Write 3", 12, PinBitCount.Bit1),
				// 2
				CreatePinDescription("Address 2", 11, PinBitCount.Bit32),
				CreatePinDescription("Data 2", 10, PinBitCount.Bit4),
				CreatePinDescription("Full Clear 2", 9, PinBitCount.Bit4),
				CreatePinDescription("Deallocate 2", 8, PinBitCount.Bit1),
				CreatePinDescription("Write 2", 7, PinBitCount.Bit1),
				// 1
				CreatePinDescription("Address 1", 6, PinBitCount.Bit32),
				CreatePinDescription("Data 1", 5, PinBitCount.Bit4),
				CreatePinDescription("Full Clear 1", 4, PinBitCount.Bit4),
				CreatePinDescription("Deallocate 1", 3, PinBitCount.Bit1),
				CreatePinDescription("Write 1", 2, PinBitCount.Bit1),

				CreatePinDescription("Clear All", 1, PinBitCount.Bit1),
			};

			PinDescription[] outputPins = {
				CreatePinDescription("RED", 44, PinBitCount.Bit4),
				CreatePinDescription("GREEN", 43, PinBitCount.Bit4),
				CreatePinDescription("BLUE", 42, PinBitCount.Bit4)
			};

			Vector2 size = new(GridSize * 23, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

			return CreateBuiltinChipDescription(ChipType.SolidStateDrive, size, col, inputPins, outputPins);
		}

		// (Not a chip, but convenient to treat it as one)
		public static ChipDescription CreateInputOrOutputPin(ChipType type)
		{
			(bool isInput, bool isOutput, PinBitCount numBits) = ChipTypeHelper.IsInputOrOutputPin(type);
			string name = isInput ? "IN" : "OUT";
			PinDescription[] pin = { CreatePinDescription(name, 0, numBits) };

			PinDescription[] inputs = isInput ? pin : null;
			PinDescription[] outputs = isOutput ? pin : null;

			return CreateBuiltinChipDescription(type, Vector2.zero, Color.clear, inputs, outputs);
		}

		static Vector2 BusChipSize(PinBitCount bitCount)
		{
			return bitCount switch
			{
				PinBitCount.Bit1 => new Vector2(GridSize * 2, GridSize * 2),
				PinBitCount.Bit4 => new Vector2(GridSize * 2, GridSize * 3),
				PinBitCount.Bit8 => new Vector2(GridSize * 2, GridSize * 4),
				PinBitCount.Bit16 => new Vector2(GridSize * 2, GridSize * 5),
				PinBitCount.Bit32 => new Vector2(GridSize * 2, GridSize * 6),
				PinBitCount.Bit64 => new Vector2(GridSize * 2, GridSize * 7),
				_ => throw new Exception("Bus bit count not implemented")
			};
		}

		static ChipDescription CreateBus(PinBitCount bitCount)
		{
			ChipType type = bitCount switch
			{
				PinBitCount.Bit1 => ChipType.Bus_1Bit,
				PinBitCount.Bit4 => ChipType.Bus_4Bit,
				PinBitCount.Bit8 => ChipType.Bus_8Bit,
				PinBitCount.Bit16 => ChipType.Bus_16Bit,
				PinBitCount.Bit32 => ChipType.Bus_32Bit,
				PinBitCount.Bit64 => ChipType.Bus_64Bit,
				_ => throw new Exception("Bus bit count not implemented")
			};

			string name = ChipTypeHelper.GetName(type);

			PinDescription[] inputs = { CreatePinDescription(name + " (Hidden)", 0, bitCount) };
			PinDescription[] outputs = { CreatePinDescription(name, 1, bitCount) };

			Color col = new(0.1f, 0.1f, 0.1f);

			return CreateBuiltinChipDescription(type, BusChipSize(bitCount), col, inputs, outputs);
		}

		static ChipDescription CreateDisplayLED()
		{
			PinDescription[] inputPins =
			{
				CreatePinDescription("IN", 0)
			};

			float height = SubChipInstance.MinChipHeightForPins(inputPins, null);
			float width = height;
			float displayWidth = height - GridSize * 0.5f;

			Color col = new(0.1f, 0.1f, 0.1f);
			Vector2 size = new(width, height);


			DisplayDescription[] displays =
			{
				new()
				{
					Position = Vector2.right * PinRadius / 3 * 0,
					Scale = displayWidth,
					SubChipID = -1
				}
			};

			return CreateBuiltinChipDescription(ChipType.DisplayLED, size, col, inputPins, null, displays, NameDisplayLocation.Hidden);
		}


		static ChipDescription CreateBusTerminus(PinBitCount bitCount)
		{
			ChipType type = bitCount switch
			{
				PinBitCount.Bit1 => ChipType.BusTerminus_1Bit,
				PinBitCount.Bit4 => ChipType.BusTerminus_4Bit,
				PinBitCount.Bit8 => ChipType.BusTerminus_8Bit,
				PinBitCount.Bit16 => ChipType.BusTerminus_16Bit,
				PinBitCount.Bit32 => ChipType.BusTerminus_32Bit,
				PinBitCount.Bit64 => ChipType.BusTerminus_64Bit,
				_ => throw new Exception("Bus bit count not implemented")
			};

			ChipDescription busOrigin = CreateBus(bitCount);
			PinDescription[] inputs = { CreatePinDescription(busOrigin.Name, 0, bitCount) };

			return CreateBuiltinChipDescription(type, BusChipSize(bitCount), busOrigin.Colour, inputs, null, null, NameDisplayLocation.Hidden);
		}


		static ChipDescription CreateBuiltinChipDescription(ChipType type, Vector2 size, Color col, PinDescription[] inputs, PinDescription[] outputs, DisplayDescription[] displays = null, NameDisplayLocation nameLoc = NameDisplayLocation.Centre)
		{
			string name = ChipTypeHelper.GetName(type);
			ValidatePinIDs(inputs, outputs, name);

			return new ChipDescription
			{
				Name = name,
				NameLocation = nameLoc,
				Colour = col,
				Size = new Vector2(size.x, size.y),
				InputPins = inputs ?? Array.Empty<PinDescription>(),
				OutputPins = outputs ?? Array.Empty<PinDescription>(),
				SubChips = Array.Empty<SubChipDescription>(),
				Wires = Array.Empty<WireDescription>(),
				Displays = displays,
				ChipType = type
			};
		}

		static PinDescription CreatePinDescription(string name, int id, PinBitCount bitCount = PinBitCount.Bit1) =>
			new(
				name,
				id,
				Vector2.zero,
				bitCount,
				PinColour.Red,
				PinValueDisplayMode.Off
			);

		static float CalculateGridSnappedWidth(float desiredWidth) =>
			// Calculate width such that spacing between an input and output pin on chip will align with grid
			GridHelper.SnapToGridForceEven(desiredWidth) - (ChipOutlineWidth - 2 * SubChipPinInset);

		static void ValidatePinIDs(PinDescription[] inputs, PinDescription[] outputs, string chipName)
		{
			HashSet<int> pinIDs = new();

			AddPins(inputs);
			AddPins(outputs);
			return;

			void AddPins(PinDescription[] pins)
			{
				if (pins == null) return;
				foreach (PinDescription pin in pins)
				{
					if (!pinIDs.Add(pin.ID))
					{
						throw new Exception($"Pin has duplicate ID ({pin.ID}) in builtin chip: {chipName}");
					}
				}
			}
		}

		static ChipDescription CreateBasicLogicGate(ChipType chipType, string gateName, PinBitCount bitCount)
		{
			Color col = new(0.4f, 0.6f, 0.8f);

			PinDescription[] inputPins;
			PinDescription[] outputPins = { CreatePinDescription("OUT", 2, bitCount) };

			if (gateName == "NOT")
			{
				inputPins = new[] { CreatePinDescription("IN", 0, bitCount) };
			}
			else
			{
				inputPins = new[]
				{
					CreatePinDescription("IN A", 0, bitCount),
					CreatePinDescription("IN B", 1, bitCount)
				};
			}

			Vector2 size = new(GridSize * 8, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

			return CreateBuiltinChipDescription(chipType, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateBuiltinTristateBuffer(ChipType chipType, PinBitCount bitCount)
		{
			Color col = new(0.4f, 0.6f, 0.8f);

			PinDescription[] inputPins =
			{
				CreatePinDescription("IN", 0, bitCount),
				CreatePinDescription("ENABLE", 1)
			};
			PinDescription[] outputPins = { CreatePinDescription("OUT", 2, bitCount) };

			Vector2 size = new(GridSize * 8, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

			return CreateBuiltinChipDescription(chipType, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateCounter(ChipType chipType, PinBitCount bitCount)
		{
			Color col = new(0.8f, 0.6f, 0.2f);

			PinDescription[] inputPins =
			{
				CreatePinDescription("CLOCK", 0),
				CreatePinDescription("RESET", 1)
			};
			PinDescription[] outputPins = { CreatePinDescription("COUNT", 2, bitCount) };

			Vector2 size = new(GridSize * 10, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

			return CreateBuiltinChipDescription(chipType, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateEquals(ChipType chipType, PinBitCount bitCount)
		{
			Color col = new(0.6f, 0.8f, 0.4f);

			PinDescription[] inputPins =
			{
				CreatePinDescription("IN A", 0, bitCount),
				CreatePinDescription("IN B", 1, bitCount)
			};
			PinDescription[] outputPins = { CreatePinDescription("EQUAL", 2) };

			Vector2 size = new(GridSize * 10, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

			return CreateBuiltinChipDescription(chipType, size, col, inputPins, outputPins);
		}

		static ChipDescription CreateFirstTick()
		{
			Color col = new(0.8f, 0.4f, 0.6f);

			PinDescription[] inputPins =
			{
				CreatePinDescription("ON", 0),
				CreatePinDescription("RESET", 1),
				CreatePinDescription("CLOCK", 2)
			};
			PinDescription[] outputPins = { CreatePinDescription("FIRST TICK", 3) };

			Vector2 size = new(GridSize * 12, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

			return CreateBuiltinChipDescription(ChipType.B_FirstTick, size, col, inputPins, outputPins);
		}
		static ChipDescription CreateMath(ChipType chipType, PinBitCount bitCount)
		{
			Color col = new(0.2f, 0.8f, 0.6f); // Teal color for math operations
			
			string operationName = chipType switch
			{
				ChipType.Addition32 => "ADD",
				ChipType.Multiplication32 => "MUL", 
				ChipType.Division32 => "DIV",
				_ => "MATH"
			};

			PinDescription[] inputPins =
			{
				CreatePinDescription("A", 0, bitCount),
				CreatePinDescription("B", 1, bitCount)
			};
			PinDescription[] outputPins = { CreatePinDescription("OUT", 2, bitCount) };

			Vector2 size = new(GridSize * 10, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

			return CreateBuiltinChipDescription(chipType, size, col, inputPins, outputPins);
		}
		
		static ChipDescription CreateAddition32()
		{
			return CreateMath(ChipType.Addition32, PinBitCount.Bit32);
		}
		
		static ChipDescription CreateMultiplication32()
		{
			return CreateMath(ChipType.Multiplication32, PinBitCount.Bit32);
		}
		
		static ChipDescription CreateDivision32()
		{
			Color col = new(0.2f, 0.8f, 0.6f); // Teal color for math operations

			PinDescription[] inputPins =
			{
				CreatePinDescription("A", 0, PinBitCount.Bit32),
				CreatePinDescription("B", 1, PinBitCount.Bit32)
			};
			PinDescription[] outputPins = { 
				CreatePinDescription("QUOTIENT", 2, PinBitCount.Bit32),
				CreatePinDescription("REMAINDER", 3, PinBitCount.Bit32)
			};

			Vector2 size = new(GridSize * 12, SubChipInstance.MinChipHeightForPins(inputPins, outputPins));

			return CreateBuiltinChipDescription(ChipType.Division32, size, col, inputPins, outputPins);
		}

	}
}