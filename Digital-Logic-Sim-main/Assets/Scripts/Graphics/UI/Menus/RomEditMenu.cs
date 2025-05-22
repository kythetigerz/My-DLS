using System;
using System.Text;
using DLS.Game;
using Seb.Helpers;
using Seb.Types;
using Seb.Vis;
using Seb.Vis.UI;
using UnityEngine;

namespace DLS.Graphics
{
	public static class RomEditMenu
	{
		static int ActiveRomDataBitCount;
		static int RowCount;

		static UIHandle ID_scrollbar;
		static UIHandle ID_DataDisplayMode;
		static int focusedRowIndex;
		static UIHandle[] IDS_inputRow;
		static string[] rowNumberStrings;

		static SubChipInstance romChip;


		static readonly string[] DataDisplayOptions =
		{
			"Unsigned Decimal",
			"Signed Decimal",
			"Binary",
			"HEX"
		};

		static DataDisplayMode[] allDisplayModes;

		static DataDisplayMode dataDisplayMode;
		static readonly UI.ScrollViewDrawElementFunc scrollViewDrawElementFunc = DrawScrollEntry;
		static readonly Func<string, bool> inputStringValidator = ValidateInputString;

		static Bounds2D scrollViewBounds;

		static float textPad => 0.52f;
		static float height => 2.5f;

		private const int TotalRowCount = 30000;
		private const int InitialLoadCount = 1000;
		private const int LoadAheadCount = 100;
		
		private static int loadedStartIndex = 0;
		private static int loadedEndIndex = 0;
		private static bool isInitialized = false;
		
		private static void LoadRowRange(int startIndex, int endIndex)
		{
			startIndex = Mathf.Clamp(startIndex, 0, TotalRowCount - 1);
			endIndex = Mathf.Clamp(endIndex, startIndex, TotalRowCount - 1);
			
			// Make sure we don't exceed the array size
			int visibleRowCount = Math.Min(endIndex - startIndex + 1, IDS_inputRow.Length);
			endIndex = startIndex + visibleRowCount - 1;
			
			// Update or create input fields for the visible range
			for (int i = 0; i < visibleRowCount; i++)
			{
				int rowIndex = startIndex + i;
				
				// Create UI handle for this row
				IDS_inputRow[i] = new UIHandle("ROM_rowInputField", rowIndex);
				InputFieldState state = UI.GetInputFieldState(IDS_inputRow[i]);
				
				// Calculate the actual index in the ROM data (4 uint values per row)
				int romDataIndex = rowIndex * 4;
				
				// Combine the 4 uint values into a 128-bit representation
				string displayString = "0";
				if (romDataIndex + 3 < romChip.InternalData.Length)
				{
					uint valueA = romChip.InternalData[romDataIndex];
					uint valueB = romChip.InternalData[romDataIndex + 1];
					uint valueC = romChip.InternalData[romDataIndex + 2];
					uint valueD = romChip.InternalData[romDataIndex + 3];
					
					// Format based on display mode
					displayString = Format128BitValue(valueA, valueB, valueC, valueD, dataDisplayMode);
				}
				
				state.SetText(displayString, rowIndex == focusedRowIndex);
				
				// Format row number with leading zeros for consistent width
				int padLength = TotalRowCount.ToString().Length + 1;
				rowNumberStrings[i] = (rowIndex + ":").PadLeft(padLength, '0');
			}
			
			loadedStartIndex = startIndex;
			loadedEndIndex = endIndex;
			isInitialized = true;
		}
		
		public static void Reset()
		{
			//dataDisplayModeIndex = 0;
		}
		
		private static string Format128BitValue(uint valueA, uint valueB, uint valueC, uint valueD, DataDisplayMode mode)
		{
			switch (mode)
			{
				case DataDisplayMode.Binary:
					// Show full 128-bit binary representation
					string binA = Convert.ToString(valueA, 2).PadLeft(32, '0');
					string binB = Convert.ToString(valueB, 2).PadLeft(32, '0');
					string binC = Convert.ToString(valueC, 2).PadLeft(32, '0');
					string binD = Convert.ToString(valueD, 2).PadLeft(32, '0');

					// Return the complete 128-bit binary string
					return $"{binA}{binB}{binC}{binD}";

				case DataDisplayMode.HEX:
					return $"{valueA:X8}{valueB:X8}{valueC:X8}{valueD:X8}";

				case DataDisplayMode.DecimalUnsigned:
					// Try to show as decimal if it fits in ulong
					if (valueA == 0 && valueB == 0)
					{
						// Can fit in 64-bit ulong
						ulong value = ((ulong)valueC << 32) | valueD;
						return value.ToString();
					}
					// Otherwise show as hex with 'h' suffix
					return $"{valueA:X8}{valueB:X8}{valueC:X8}{valueD:X8}h";

				case DataDisplayMode.DecimalSigned:
					// Try to show as decimal if it fits in long
					if ((valueA == 0 && valueB == 0 && (valueC & 0x80000000) == 0) ||
						(valueA == 0xFFFFFFFF && valueB == 0xFFFFFFFF && (valueC & 0x80000000) != 0))
					{
						// Can fit in 64-bit long (positive or negative)
						long value = ((long)((ulong)valueC << 32) | valueD);
						return value.ToString();
					}
					// Otherwise show as hex with 'h' suffix
					return $"{valueA:X8}{valueB:X8}{valueC:X8}{valueD:X8}h";

				default:
					return $"{valueA:X8}{valueB:X8}{valueC:X8}{valueD:X8}h";
			}
		}

		public static void DrawMenu()
		{
			MenuHelper.DrawBackgroundOverlay();

			// ---- Draw ROM contents ----
			scrollViewBounds = Bounds2D.CreateFromCentreAndSize(UI.Centre, new Vector2(UI.Width * 0.4f, UI.Height * 0.8f));

			ScrollViewTheme scrollTheme = DrawSettings.ActiveUITheme.ScrollTheme;
			UI.DrawScrollView(ID_scrollbar, scrollViewBounds.TopLeft, scrollViewBounds.Size, 0, Anchor.TopLeft, scrollTheme, scrollViewDrawElementFunc, RowCount);

			if (focusedRowIndex >= 0)
			{
				// Focus next/prev field with keyboard shortcuts
				bool changeLine = KeyboardShortcuts.ConfirmShortcutTriggered || InputHelper.IsKeyDownThisFrame(KeyCode.Tab);

				if (changeLine)
				{
					bool goPrevLine = InputHelper.ShiftIsHeld;
					int jumpToRowIndex = focusedRowIndex + (goPrevLine ? -1 : 1);

					if (jumpToRowIndex >= 0 && jumpToRowIndex < RowCount)
					{
						OnFieldLostFocus(focusedRowIndex);
						int nextFocusedRowIndex = focusedRowIndex + (goPrevLine ? -1 : 1);
						
						// Make sure the target row is loaded
						if (nextFocusedRowIndex < loadedStartIndex || nextFocusedRowIndex > loadedEndIndex)
						{
							// Load the appropriate range to include the target row
							int newStart = Math.Max(0, nextFocusedRowIndex - LoadAheadCount/2);
							int newEnd = Math.Min(TotalRowCount - 1, nextFocusedRowIndex + LoadAheadCount/2);
							LoadRowRange(newStart, newEnd);
						}
						
						int adjustedIndex = nextFocusedRowIndex - loadedStartIndex;
						if (adjustedIndex >= 0 && adjustedIndex < IDS_inputRow.Length)
						{
							UI.GetInputFieldState(IDS_inputRow[adjustedIndex]).SetFocus(true);
							focusedRowIndex = nextFocusedRowIndex;
						}
					}
				}
			}

			// --- Draw side panel with buttons ----
			Vector2 sidePanelSize = new(UI.Width * 0.2f, UI.Height * 0.8f);
			Vector2 sidePanelTopLeft = scrollViewBounds.TopRight + Vector2.right * (UI.Width * 0.05f);
			Draw.ID sidePanelID = UI.ReservePanel();

			using (UI.BeginBoundsScope(true))
			{
				const float buttonSpacing = 0.75f;

				// Display mode
				DataDisplayMode modeNew = (DataDisplayMode)UI.WheelSelector(ID_DataDisplayMode, DataDisplayOptions, sidePanelTopLeft, new Vector2(sidePanelSize.x, DrawSettings.SelectorWheelHeight), MenuHelper.Theme.OptionsWheel, Anchor.TopLeft);
				Vector2 buttonTopleft = new(sidePanelTopLeft.x, UI.PrevBounds.Bottom - buttonSpacing);

				int copyPasteButtonIndex = MenuHelper.DrawButtonPair("COPY ALL", "PASTE ALL", buttonTopleft, sidePanelSize.x, false);
				buttonTopleft = UI.PrevBounds.BottomLeft + Vector2.down * buttonSpacing;
				bool clearAll = UI.Button("CLEAR ALL", MenuHelper.Theme.ButtonTheme, buttonTopleft, new Vector2(sidePanelSize.x, 0), true, false, true, Anchor.TopLeft);
				buttonTopleft = UI.PrevBounds.BottomLeft + Vector2.down * (buttonSpacing * 2f);
				MenuHelper.CancelConfirmResult result = MenuHelper.DrawCancelConfirmButtons(buttonTopleft, sidePanelSize.x, false, false);

				MenuHelper.DrawReservedMenuPanel(sidePanelID, UI.GetCurrentBoundsScope());

				// ---- Handle button inputs ----
				if (copyPasteButtonIndex == 0) CopyAll();
				else if (copyPasteButtonIndex == 1) PasteAll();
				else if (clearAll) ClearAll();

				if (result == MenuHelper.CancelConfirmResult.Cancel || KeyboardShortcuts.CancelShortcutTriggered)
				{
					UIDrawer.SetActiveMenu(UIDrawer.MenuType.None);
				}
				else if (result == MenuHelper.CancelConfirmResult.Confirm)
				{
					SaveChangesToROM();
					UIDrawer.SetActiveMenu(UIDrawer.MenuType.None);
				}

				if (dataDisplayMode != modeNew)
				{
					ConvertDisplayData(dataDisplayMode, modeNew);
					dataDisplayMode = modeNew;
				}
			}
		}

		static void OnFieldLostFocus(int rowIndex)
		{
			if (rowIndex < 0) return;
			
			int adjustedIndex = rowIndex - loadedStartIndex;
			if (adjustedIndex < 0 || adjustedIndex >= IDS_inputRow.Length) return;

			InputFieldState inputFieldOld = UI.GetInputFieldState(IDS_inputRow[adjustedIndex]);
			
			// Only update the text if this field actually exists and isn't currently focused
			if (inputFieldOld != null && !inputFieldOld.focused)
			{
				inputFieldOld.SetText(AutoFormatInputString(inputFieldOld.text), focus: false);
			}
		}

		static string AutoFormatInputString(string input)
		{
			// For hex values with 'h' suffix
			if (input.EndsWith("h", StringComparison.OrdinalIgnoreCase))
			{
				input = input.Substring(0, input.Length - 1);
				if (TryParseHexString(input, out uint parsedA2, out uint parsedB2, out uint parsedC2, out uint parsedD2))
				{
					return Format128BitValue(parsedA2, parsedB2, parsedC2, parsedD2, dataDisplayMode);
				}
			}
			
			// Try to parse string in current format
			if (TryParseDisplayStringToValues(input, dataDisplayMode, out uint parsedA, out uint parsedB, out uint parsedC, out uint parsedD))
			{
				return Format128BitValue(parsedA, parsedB, parsedC, parsedD, dataDisplayMode);
			}
			
			// If all else fails, return a default value
			return Format128BitValue(0, 0, 0, 0, dataDisplayMode);
		}

		static void CopyAll()
		{
			StringBuilder sb = new();
			for (int i = loadedStartIndex; i <= loadedEndIndex; i++)
			{
				int adjustedIndex = i - loadedStartIndex;
				if (adjustedIndex >= 0 && adjustedIndex < IDS_inputRow.Length)
				{
					InputFieldState state = UI.GetInputFieldState(IDS_inputRow[adjustedIndex]);
					sb.AppendLine(state.text);
				}
			}

			InputHelper.CopyToClipboard(sb.ToString());
		}

		static void PasteAll()
		{
			string[] pasteStrings = StringHelper.SplitByLine(InputHelper.GetClipboardContents());
			int pasteCount = Math.Min(pasteStrings.Length, TotalRowCount - loadedStartIndex);
			
			for (int i = 0; i < pasteCount; i++)
			{
				int rowIndex = loadedStartIndex + i;
				int adjustedIndex = rowIndex - loadedStartIndex;
				
				if (adjustedIndex >= 0 && adjustedIndex < IDS_inputRow.Length)
				{
					string pasteString = AutoFormatInputString(pasteStrings[i]);
					InputFieldState state = UI.GetInputFieldState(IDS_inputRow[adjustedIndex]);
					state.SetText(pasteString, state.focused);
				}
			}
		}

		static void ClearAll()
		{
			for (int i = 0; i < IDS_inputRow.Length; i++)
			{
				InputFieldState state = UI.GetInputFieldState(IDS_inputRow[i]);
				state.SetText("0", state.focused);
			}
		}

		static void ConvertDisplayData(DataDisplayMode modeCurr, DataDisplayMode modeNew)
		{
			for (int i = 0; i < IDS_inputRow.Length; i++)
			{
				InputFieldState state = UI.GetInputFieldState(IDS_inputRow[i]);
				
				// Parse the current display string to get the 4 uint values
				if (TryParseDisplayStringToValues(state.text, modeCurr, out uint valueA, out uint valueB, out uint valueC, out uint valueD))
				{
					// Format with the new display mode
					state.SetText(Format128BitValue(valueA, valueB, valueC, valueD, modeNew), false);
				}
				else
				{
					// If parsing fails, set to default zero value
					state.SetText(Format128BitValue(0, 0, 0, 0, modeNew), false);
				}
			}
		}

		static bool ValidateInputString(string text)
		{
			if (string.IsNullOrEmpty(text)) return true;
			if (text.Length > 34) return false;

			foreach (char c in text)
			{
				if (c == ' ') continue; //ignore white space

				// If in binary mode, only 0s or 1s allowed
				if (dataDisplayMode == DataDisplayMode.Binary && c is not ('0' or '1')) return false;

				if (c == '-') continue; // allow negative sign (even in unsigned field as we'll do automatic conversion)
				if (dataDisplayMode == DataDisplayMode.HEX && Uri.IsHexDigit(c)) continue;
				if (!char.IsDigit(c)) return false;
			}

			return true;
		}

		// Convert from uint to display string with given display mode
		static string UIntToDisplayString(uint raw, DataDisplayMode displayFormat, ulong bitCount)
		{
			return displayFormat switch
			{
				DataDisplayMode.Binary => Convert.ToString(raw, 2).PadLeft((int)bitCount, '0'),
				DataDisplayMode.DecimalSigned => Maths.TwosComplement(raw, bitCount) + "",
				DataDisplayMode.DecimalUnsigned => raw + "",
				DataDisplayMode.HEX => raw.ToString("X").PadLeft((int)(bitCount / 4), '0'),
				_ => throw new NotImplementedException("Unsupported display format: " + displayFormat)
			};
		}

		// Convert string with given format to uint
		static uint DisplayStringToUInt(string displayString, DataDisplayMode stringFormat, int bitCount)
		{
			displayString = displayString.Replace(" ", string.Empty);
			uint uintVal;

			switch (stringFormat)
			{
				case DataDisplayMode.Binary:
					uintVal = Convert.ToUInt32(displayString, 2);
					break;
				case DataDisplayMode.DecimalSigned:
				{
					int signedValue = int.Parse(displayString);
					uint unsignedRange = 1u << bitCount;
					if (signedValue < 0)
					{
						uintVal = (uint)(signedValue + unsignedRange);
					}
					else
					{
						uintVal = (uint)signedValue;
					}

					break;
				}
				case DataDisplayMode.DecimalUnsigned:
					uintVal = uint.Parse(displayString);
					break;
				case DataDisplayMode.HEX:
					int value = Convert.ToInt32(displayString, 16);
					uintVal = (uint)value;
					break;
				default:
					throw new NotImplementedException("Unsupported display format: " + stringFormat);
			}

			return uintVal;
		}

		static bool TryParseDisplayStringToUInt(string displayString, DataDisplayMode stringFormat, int bitCount, out uint raw)
		{
			try
			{
				raw = DisplayStringToUInt(displayString, stringFormat, bitCount);
				uint maxVal = (1u << bitCount) - 1;

				// If value is too large to fit in given bit-count, clamp the result and return failure
				// (note: maybe makes more sense to wrap the result, but I think it's more obvious to player what happened if it just clamps)
				if (raw > maxVal)
				{
					raw = maxVal;
					return false;
				}

				return true;
			}
			catch (Exception)
			{
				raw = 0;
				return false;
			}
		}

		static void SaveChangesToROM()
		{
			Project.ActiveProject.NotifyRomContentsEdited(romChip);
		}

		private static bool Parse128BitValue(string displayString, DataDisplayMode mode, out ulong valueA, out ulong valueB, out ulong valueC, out ulong valueD)
		{
			valueA = valueB = valueC = valueD = 0;
			
			try
			{
				switch (mode)
				{
					case DataDisplayMode.HEX:
						// Parse 32 hex characters (128 bits)
						if (displayString.Length <= 32)
						{
							string paddedHex = displayString.PadLeft(32, '0');
							string hexA = paddedHex.Substring(0, 8);
							string hexB = paddedHex.Substring(8, 8);
							string hexC = paddedHex.Substring(16, 8);
							string hexD = paddedHex.Substring(24, 8);
							
							valueA = Convert.ToUInt64(hexA, 16);
							valueB = Convert.ToUInt64(hexB, 16);
							valueC = Convert.ToUInt64(hexC, 16);
							valueD = Convert.ToUInt64(hexD, 16);
							return true;
						}
						break;
						
					// Add other parsing modes as needed
					
					default:
						// Default fallback to hex parsing
						if (displayString.EndsWith("h"))
						{
							displayString = displayString.TrimEnd('h');
						}
						
						if (displayString.Length <= 32)
						{
							string paddedHex = displayString.PadLeft(32, '0');
							string hexA = paddedHex.Substring(0, 8);
							string hexB = paddedHex.Substring(8, 8);
							string hexC = paddedHex.Substring(16, 8);
							string hexD = paddedHex.Substring(24, 8);
							
							valueA = Convert.ToUInt64(hexA, 16);
							valueB = Convert.ToUInt64(hexB, 16);
							valueC = Convert.ToUInt64(hexC, 16);
							valueD = Convert.ToUInt64(hexD, 16);
							return true;
						}
						break;
				}
			}
			catch (Exception)
			{
				return false;
			}
			
			return false;
		}

		static void DrawScrollEntry(Vector2 topLeft, float width, int index, bool isLayoutPass)
		{
			// Calculate the actual row index in the ROM data - index is already in correct order (0 to TotalRowCount-1)
			int visibleIndex = index;
			
			// Check if we need to load more rows
			if (!isInitialized || visibleIndex < loadedStartIndex || visibleIndex > loadedEndIndex)
			{
				// Calculate new range to load, centered around the current visible index
				int newStart = Math.Max(0, visibleIndex - LoadAheadCount/2);
				int newEnd = Math.Min(TotalRowCount - 1, newStart + InitialLoadCount - 1);
				
				// Ensure we don't exceed array bounds
				if (newEnd - newStart + 1 > IDS_inputRow.Length)
					newEnd = newStart + IDS_inputRow.Length - 1;
					
				LoadRowRange(newStart, newEnd);
			}
			
			// Adjust index to the loaded range
			int adjustedIndex = visibleIndex - loadedStartIndex;
			
			// Safety check to prevent index out of range
			if (adjustedIndex < 0 || adjustedIndex >= IDS_inputRow.Length)
				return;
				
			Vector2 panelSize = new(width, height);
			Bounds2D entryBounds = Bounds2D.CreateFromTopLeftAndSize(topLeft, panelSize);

			if (entryBounds.Overlaps(scrollViewBounds) && !isLayoutPass) // don't bother with draw stuff if outside of scroll view / in layout pass
			{
				UIHandle inputFieldID = IDS_inputRow[adjustedIndex];
				InputFieldState inputFieldState = UI.GetInputFieldState(inputFieldID);

				// Alternating colour for each row
				Color col = (visibleIndex % 2 == 0) ? ColHelper.MakeCol(0.17f) : ColHelper.MakeCol(0.13f);
				
				// Highlight row if it has focus
				if (inputFieldState.focused)
				{
					// ALWAYS update focusedRowIndex when a field gains focus
					focusedRowIndex = visibleIndex; // This line is crucial
					col = new Color(0.33f, 0.55f, 0.34f);
				}
				else if (focusedRowIndex == visibleIndex) // Field just lost focus
				{
					// Immediately update the value when focus is lost
					string newValue = AutoFormatInputString(inputFieldState.text);
					inputFieldState.SetText(newValue, focus: false);
					//SaveRowValueToROM(visibleIndex, newValue); // Save immediately
					focusedRowIndex = -1; // Reset focused row index
				}
				else
				{
					col = ColHelper.MakeCol(0.13f);
				}
				
				InputFieldTheme inputTheme = MenuHelper.Theme.ChipNameInputField;
				inputTheme.fontSize = MenuHelper.Theme.FontSizeRegular;
				inputTheme.bgCol = col;
				inputTheme.focusBorderCol = Color.clear;

				// No font size adjustment - keep the original size for all display modes
				// This ensures arrow key navigation works properly across all digits

				UI.InputField(inputFieldID, inputTheme, topLeft, panelSize, "0", Anchor.TopLeft, 5, inputStringValidator);

				// Draw line index
				Color lineNumCol = inputFieldState.focused ? new (0.53f, 0.8f, 0.57f) : ColHelper.MakeCol(0.32f);
				UI.DrawText(rowNumberStrings[adjustedIndex], MenuHelper.Theme.FontBold, MenuHelper.Theme.FontSizeRegular, entryBounds.CentreLeft + Vector2.right * textPad, Anchor.TextCentreLeft, lineNumCol);
			}

			// Set bounding box of scroll list element 
			UI.OverridePreviousBounds(entryBounds);
		}

		public static void OnMenuOpened()
		{
			romChip = (SubChipInstance)ContextMenu.interactionContext;
			RowCount = TotalRowCount; // Set to the full count for scrolling
			ActiveRomDataBitCount = 128; // 128-bit data

			ID_DataDisplayMode = new UIHandle("ROM_DataDisplayMode", romChip.ID);
			ID_scrollbar = new UIHandle("ROM_EditScrollbar", romChip.ID);

			allDisplayModes = (DataDisplayMode[])Enum.GetValues(typeof(DataDisplayMode));
			focusedRowIndex = -1;
			
			// Initialize arrays for the visible portion only
			IDS_inputRow = new UIHandle[InitialLoadCount];
			rowNumberStrings = new string[InitialLoadCount];
			dataDisplayMode = (DataDisplayMode)UI.GetWheelSelectorState(ID_DataDisplayMode).index;

			// Reset loading state
			loadedStartIndex = 0;
			loadedEndIndex = 0;
			isInitialized = false;
			
			// Load initial set of rows - starting from index 0
			LoadRowRange(0, InitialLoadCount - 1);
			
			// Set initial scroll position to top
			UI.GetScrollbarState(ID_scrollbar).scrollY = 0;
		}

		public static void Close()
		{
			//dataDisplayModeIndex = 0;
		}

		enum DataDisplayMode
		{
			DecimalUnsigned,
			DecimalSigned,
			Binary,
			HEX
		}

		// Method to parse a hex string into four uint values
		private static bool TryParseHexString(string hexString, out uint valueA, out uint valueB, out uint valueC, out uint valueD)
		{
			valueA = valueB = valueC = valueD = 0;
			
			// Remove any spaces or other formatting
			hexString = hexString.Replace(" ", "").Replace("-", "").Replace("_", "");
			
			// Ensure the string is not too long
			if (hexString.Length > 32)
				return false;
			
			// Pad the string to 32 characters (128 bits)
			hexString = hexString.PadLeft(32, '0');
			
			try
			{
				// Parse each 8-character segment (32 bits) into a uint
				string hexA = hexString.Substring(0, 8);
				string hexB = hexString.Substring(8, 8);
				string hexC = hexString.Substring(16, 8);
				string hexD = hexString.Substring(24, 8);
				
				valueA = Convert.ToUInt32(hexA, 16);
				valueB = Convert.ToUInt32(hexB, 16);
				valueC = Convert.ToUInt32(hexC, 16);
				valueD = Convert.ToUInt32(hexD, 16);
				
				return true;
			}
			catch
			{
				return false;
			}
		}

		// Method to parse a display string based on the current display mode
		private static bool TryParseDisplayStringToValues(string input, DataDisplayMode displayMode, out uint valueA, out uint valueB, out uint valueC, out uint valueD)
		{
			valueA = valueB = valueC = valueD = 0;
			
			switch (displayMode)
			{
				case DataDisplayMode.HEX:
					// For hex mode, just use the hex parser
					return TryParseHexString(input, out valueA, out valueB, out valueC, out valueD);
					
				case DataDisplayMode.Binary:
					// For binary, we need to parse a binary string
					try
					{
						// Remove any formatting
						input = input.Replace(" ", "").Replace(".", "").Replace("-", "");
						
						// Ensure the string is not too long
						if (input.Length > 128)
							return false;
						
						// Pad the string to 128 characters
						input = input.PadLeft(128, '0');
						
						// Parse each 32-character segment into a uint
						string binA = input.Substring(0, 32);
						string binB = input.Substring(32, 32);
						string binC = input.Substring(64, 32);
						string binD = input.Substring(96, 32);
						
						valueA = Convert.ToUInt32(binA, 2);
						valueB = Convert.ToUInt32(binB, 2);
						valueC = Convert.ToUInt32(binC, 2);
						valueD = Convert.ToUInt32(binD, 2);
						
						return true;
					}
					catch
					{
						return false;
					}
					
				case DataDisplayMode.DecimalUnsigned:
				case DataDisplayMode.DecimalSigned:
					// For decimal modes, we'll handle hex strings with 'h' suffix
					if (input.EndsWith("h", StringComparison.OrdinalIgnoreCase))
					{
						return TryParseHexString(input.Substring(0, input.Length - 1), out valueA, out valueB, out valueC, out valueD);
					}
					
					// For regular decimal, we'll try to parse as a single number if possible
					try
					{
						if (ulong.TryParse(input, out ulong value))
						{
							// Convert the single value to our 4 uints
							valueD = (uint)(value & 0xFFFFFFFF);
							valueC = (uint)((value >> 32) & 0xFFFFFFFF);
							// Higher bits are 0
							return true;
						}
						
						// If it's signed, try parsing as signed
						if (displayMode == DataDisplayMode.DecimalSigned && long.TryParse(input, out long signedValue))
						{
							// Convert the signed value to unsigned representation
							ulong unsignedValue = (ulong)signedValue;
							valueD = (uint)(unsignedValue & 0xFFFFFFFF);
							valueC = (uint)((unsignedValue >> 32) & 0xFFFFFFFF);
							
							// If negative, set all higher bits to 1
							if (signedValue < 0)
							{
								valueA = valueB = 0xFFFFFFFF;
							}
							
							return true;
						}
					}
					catch
					{
						// Fall back to hex parsing
						return TryParseHexString(input, out valueA, out valueB, out valueC, out valueD);
					}
					
					// If all else fails, try hex parsing
					return TryParseHexString(input, out valueA, out valueB, out valueC, out valueD);
					
				default:
					// For any other mode, try hex parsing
					return TryParseHexString(input, out valueA, out valueB, out valueC, out valueD);
			}
		}
	}
}