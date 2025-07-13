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
						UI.GetInputFieldState(IDS_inputRow[nextFocusedRowIndex]).SetFocus(true);
						focusedRowIndex = nextFocusedRowIndex;
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

			InputFieldState inputFieldOld = UI.GetInputFieldState(IDS_inputRow[rowIndex]);
			inputFieldOld.SetText(AutoFormatInputString(inputFieldOld.text), focus: false);
		}

		static string AutoFormatInputString(string input)
		{
			// Try to parse string in current format
			if (!TryParseDisplayStringToULong(input, dataDisplayMode, ActiveRomDataBitCount, out ulong ulongValue))
			{
				// If failed to parse in current format, fall back to trying all possible formats
				// (for example, if player enters -1 in unsigned mode, we can recognize it as signed input and convert to unsigned automatically)
				foreach (DataDisplayMode fallbackMode in allDisplayModes)
				{
					if (TryParseDisplayStringToULong(input, fallbackMode, ActiveRomDataBitCount, out ulong fallbackULongValue))
					{
						ulongValue = fallbackULongValue;
						break;
					}
				}
			}

			return ULongToDisplayString(ulongValue, dataDisplayMode, ActiveRomDataBitCount);
		}

		static void CopyAll()
		{
			StringBuilder sb = new();
			for (int i = 0; i < IDS_inputRow.Length; i++)
			{
				InputFieldState state = UI.GetInputFieldState(IDS_inputRow[i]);
				sb.AppendLine(state.text);
			}

			InputHelper.CopyToClipboard(sb.ToString());
		}

		static void PasteAll()
		{
			string[] pasteStrings = StringHelper.SplitByLine(InputHelper.GetClipboardContents());
			for (int i = 0; i < Mathf.Min(IDS_inputRow.Length, pasteStrings.Length); i++)
			{
				string pasteString = AutoFormatInputString(pasteStrings[i]);
				InputFieldState state = UI.GetInputFieldState(IDS_inputRow[i]);
				state.SetText(pasteString, state.focused);
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
				TryParseDisplayStringToULong(state.text, modeCurr, ActiveRomDataBitCount, out ulong ulongValue);
				state.SetText(ULongToDisplayString(ulongValue, modeNew, ActiveRomDataBitCount), false);
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

		// Convert from ulong to display string with given display mode
		static string ULongToDisplayString(ulong raw, DataDisplayMode displayFormat, int bitCount)
		{
			return displayFormat switch
			{
				DataDisplayMode.Binary => Convert.ToString((long)raw, 2).PadLeft(bitCount, '0'),
				DataDisplayMode.DecimalSigned => TwosComplementULong(raw, bitCount) + "",
				DataDisplayMode.DecimalUnsigned => raw + "",
				DataDisplayMode.HEX => raw.ToString("X").PadLeft((bitCount + 3) / 4, '0'),
				_ => throw new NotImplementedException("Unsupported display format: " + displayFormat)
			};
		}

		// Helper method for two's complement conversion for ulong
		static long TwosComplementULong(ulong value, int bitCount)
		{
			if (bitCount >= 64) return (long)value;
			
			ulong signBit = 1UL << (bitCount - 1);
			if ((value & signBit) != 0)
			{
				// Negative number - extend sign bits
				ulong mask = ~((1UL << bitCount) - 1);
				return (long)(value | mask);
			}
			else
			{
				// Positive number
				return (long)value;
			}
		}

		// Convert string with given format to ulong
		static ulong DisplayStringToULong(string displayString, DataDisplayMode stringFormat, int bitCount)
		{
			displayString = displayString.Replace(" ", string.Empty);
			ulong ulongVal;

			switch (stringFormat)
			{
				case DataDisplayMode.Binary:
					ulongVal = Convert.ToUInt64(displayString, 2);
					break;
				case DataDisplayMode.DecimalSigned:
				{
					long signedValue = long.Parse(displayString);
					if (signedValue < 0)
					{
						// Convert negative signed value to unsigned representation
						if (bitCount < 64)
						{
							ulong unsignedRange = 1UL << bitCount;
							ulongVal = (ulong)(signedValue + (long)unsignedRange);
						}
						else
						{
							ulongVal = (ulong)signedValue;
						}
					}
					else
					{
						ulongVal = (ulong)signedValue;
					}
					break;
				}
				case DataDisplayMode.DecimalUnsigned:
					ulongVal = ulong.Parse(displayString);
					break;
				case DataDisplayMode.HEX:
					ulongVal = Convert.ToUInt64(displayString, 16);
					break;
				default:
					throw new NotImplementedException("Unsupported display format: " + stringFormat);
			}

			return ulongVal;
		}

		static bool TryParseDisplayStringToULong(string displayString, DataDisplayMode stringFormat, int bitCount, out ulong raw)
		{
			try
			{
				raw = DisplayStringToULong(displayString, stringFormat, bitCount);
				
				if (bitCount < 64)
				{
					ulong maxVal = (1UL << bitCount) - 1;
					// If value is too large to fit in given bit-count, clamp the result and return failure
					// (note: maybe makes more sense to wrap the result, but I think it's more obvious to player what happened if it just clamps)
					if (raw > maxVal)
					{
						raw = maxVal;
						return false;
					}
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
			for (int i = 0; i < RowCount; i++)
			{
				string displayString = UI.GetInputFieldState(IDS_inputRow[i]).text;
				TryParseDisplayStringToULong(displayString, dataDisplayMode, ActiveRomDataBitCount, out ulong newValue);
				romChip.InternalData[i] = (uint)newValue;
			}

			Project.ActiveProject.NotifyRomContentsEdited(romChip);
		}

		static void DrawScrollEntry(Vector2 topLeft, float width, int index, bool isLayoutPass)
		{
			Vector2 panelSize = new(width, height);
			Bounds2D entryBounds = Bounds2D.CreateFromTopLeftAndSize(topLeft, panelSize);

			if (entryBounds.Overlaps(scrollViewBounds) && !isLayoutPass) // don't bother with draw stuff if outside of scroll view / in layout pass
			{
				UIHandle inputFieldID = IDS_inputRow[index];
				InputFieldState inputFieldState = UI.GetInputFieldState(inputFieldID);

				// Alternating colour for each row
				Color col = index % 2 == 0 ? ColHelper.MakeCol(0.17f) : ColHelper.MakeCol(0.13f);
				// Highlight row if it has focus
				if (inputFieldState.focused)
				{
					if (focusedRowIndex != index)
					{
						OnFieldLostFocus(focusedRowIndex);
						focusedRowIndex = index;
					}

					col = new Color(0.33f, 0.55f, 0.34f);
				}

				InputFieldTheme inputTheme = MenuHelper.Theme.ChipNameInputField;
				inputTheme.fontSize = MenuHelper.Theme.FontSizeRegular;
				inputTheme.bgCol = col;
				inputTheme.focusBorderCol = Color.clear;


				UI.InputField(inputFieldID, inputTheme, topLeft, panelSize, "0", Anchor.TopLeft, 5, inputStringValidator);

				// Draw line index
				Color lineNumCol = inputFieldState.focused ? new Color(0.53f, 0.8f, 0.57f) : ColHelper.MakeCol(0.32f);
				UI.DrawText(rowNumberStrings[index], MenuHelper.Theme.FontBold, MenuHelper.Theme.FontSizeRegular, entryBounds.CentreLeft + Vector2.right * textPad, Anchor.TextCentreLeft, lineNumCol);
			}

			// Set bounding box of scroll list element 
			UI.OverridePreviousBounds(entryBounds);
		}

		public static void OnMenuOpened()
		{
			romChip = (SubChipInstance)ContextMenu.interactionContext;
			RowCount = romChip.InternalData.Length;
			ActiveRomDataBitCount = 16; //

			ID_DataDisplayMode = new UIHandle("ROM_DataDisplayMode", romChip.ID);
			ID_scrollbar = new UIHandle("ROM_EditScrollbar", romChip.ID);

			allDisplayModes = (DataDisplayMode[])Enum.GetValues(typeof(DataDisplayMode));
			focusedRowIndex = 0;
			IDS_inputRow = new UIHandle[RowCount];
			rowNumberStrings = new string[RowCount];
			dataDisplayMode = (DataDisplayMode)UI.GetWheelSelectorState(ID_DataDisplayMode).index;

			int lineNumberPadLength = RowCount.ToString().Length;

			for (int i = 0; i < IDS_inputRow.Length; i++)
			{
				IDS_inputRow[i] = new UIHandle("ROM_rowInputField", i);
				InputFieldState state = UI.GetInputFieldState(IDS_inputRow[i]);

				string displayString = ULongToDisplayString(romChip.InternalData[i], dataDisplayMode, ActiveRomDataBitCount);
				state.SetText(displayString, i == focusedRowIndex);

				rowNumberStrings[i] = (i + ":").PadLeft(lineNumberPadLength + 1, '0');
			}
		}

		public static void Reset()
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
	}
}