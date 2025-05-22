using DLS.Game;
using DLS.Simulation;
using Seb.Helpers;
using Seb.Vis;
using Seb.Vis.UI;
using System.Collections.Generic;
using UnityEngine;

namespace DLS.Graphics
{
	public static class RebindKeyChipMenu
	{
		// We'll replace the simple allowed chars with a more comprehensive system
		private static readonly Dictionary<KeyCode, string> KeyDisplayNames = new Dictionary<KeyCode, string>
		{
			// Letters and numbers are handled automatically
			
			// Modifier keys
			{ KeyCode.LeftShift, "LSHIFT" },
			{ KeyCode.RightShift, "RSHIFT" },
			{ KeyCode.LeftControl, "LCTRL" },
			{ KeyCode.RightControl, "RCTRL" },
			{ KeyCode.LeftAlt, "LALT" },
			{ KeyCode.RightAlt, "RALT" },
			{ KeyCode.LeftMeta, "LWIN" },
			{ KeyCode.RightMeta, "RWIN" },

			// Mouse
			{ KeyCode.Mouse0, "Mouse0" },
			{ KeyCode.Mouse1, "Mouse1" },
			{ KeyCode.Mouse2, "Mouse2" },
			{ KeyCode.Mouse3, "Mouse3" },
			{ KeyCode.Mouse4, "Mouse4" },
			{ KeyCode.Mouse5, "Mouse5" },
			{ KeyCode.Mouse6, "Mouse6" },
			
			// Special keys
			{ KeyCode.Space, "SPACE" },
			{ KeyCode.Return, "ENTER" },
			{ KeyCode.Tab, "TAB" },
			{ KeyCode.Backspace, "BKSP" },
			{ KeyCode.Delete, "DEL" },
			{ KeyCode.Escape, "ESC" },
			{ KeyCode.UpArrow, "UP" },
			{ KeyCode.DownArrow, "DOWN" },
			{ KeyCode.LeftArrow, "LEFT" },
			{ KeyCode.RightArrow, "RIGHT" },
			{ KeyCode.Home, "HOME" },
			{ KeyCode.End, "END" },
			{ KeyCode.Insert, "INS" },
			{ KeyCode.PageUp, "PGUP" },
			{ KeyCode.PageDown, "PGDN" },
			{ KeyCode.Menu, "MENU" },
			{ KeyCode.CapsLock, "CAPS" },
			{ KeyCode.Numlock, "NUMLK" },
			{ KeyCode.ScrollLock, "SCRLK" },
			{ KeyCode.Print, "PRINT" },
			{ KeyCode.Pause, "PAUSE" },
			{ KeyCode.Break, "BREAK" },
			
			// Function keys
			{ KeyCode.F1, "F1" },
			{ KeyCode.F2, "F2" },
			{ KeyCode.F3, "F3" },
			{ KeyCode.F4, "F4" },
			{ KeyCode.F5, "F5" },
			{ KeyCode.F6, "F6" },
			{ KeyCode.F7, "F7" },
			{ KeyCode.F8, "F8" },
			{ KeyCode.F9, "F9" },
			{ KeyCode.F10, "F10" },
			{ KeyCode.F11, "F11" },
			{ KeyCode.F12, "F12" },
			
			// Numpad keys
			{ KeyCode.KeypadEnter, "KPENT" },
			{ KeyCode.KeypadDivide, "KP/" },
			{ KeyCode.KeypadMultiply, "KP*" },
			{ KeyCode.KeypadMinus, "KP-" },
			{ KeyCode.KeypadPlus, "KP+" },
			{ KeyCode.KeypadPeriod, "KP." },
			{ KeyCode.KeypadEquals, "KP=" },
			
			// Symbol keys
			{ KeyCode.BackQuote, "`" },
			{ KeyCode.Minus, "-" },
			{ KeyCode.Equals, "=" },
			{ KeyCode.LeftBracket, "[" },
			{ KeyCode.RightBracket, "]" },
			{ KeyCode.Backslash, "\\" },
			{ KeyCode.Semicolon, ";" },
			{ KeyCode.Quote, "'" },
			{ KeyCode.Comma, "," },
			{ KeyCode.Period, "." },
			{ KeyCode.Slash, "/" },
		};
		
		static SubChipInstance keyChip;
		static string chosenKey;
		static bool keySelected = false;

		public static void DrawMenu()
		{
			MenuHelper.DrawBackgroundOverlay();
			Draw.ID panelID = UI.ReservePanel();
			DrawSettings.UIThemeDLS theme = DrawSettings.ActiveUITheme;

			Vector2 pos = UI.Centre + Vector2.up * (UI.HalfHeight * 0.25f);

			using (UI.BeginBoundsScope(true))
			{
				// Only check for key input if we haven't selected a key yet
				if (InputHelper.AnyKeyOrMouseDownThisFrame && !keySelected)
				{
					// Check for special keys first
					foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
					{
						if (InputHelper.IsKeyDownThisFrame(key))
						{
							// Handle the key
							if (TryGetKeyDisplayName(key, out string displayName))
							{
								chosenKey = displayName;
								keySelected = true;
								break;
							}
						}
					}
					
					// Also check for direct character input (for backward compatibility)
					if (!string.IsNullOrEmpty(InputHelper.InputStringThisFrame))
					{
						char activeChar = char.ToUpper(InputHelper.InputStringThisFrame[0]);
						chosenKey = activeChar.ToString();
						keySelected = true;
					}
				}

				string instructionText = keySelected ? "Key selected. Click Confirm to save." : "Press a key to rebind";
				UI.DrawText(instructionText, theme.FontBold, theme.FontSizeRegular, pos, Anchor.TextCentre, Color.white * 0.8f);

				UI.DrawPanel(UI.PrevBounds.CentreBottom + Vector2.down, Vector2.one * 3.5f, new Color(0.1f, 0.1f, 0.1f), Anchor.CentreTop);
				UI.DrawText(chosenKey, theme.FontBold, theme.FontSizeRegular * 1.5f, UI.PrevBounds.Centre, Anchor.TextCentre, Color.white);

				MenuHelper.CancelConfirmResult result = MenuHelper.DrawCancelConfirmButtons(UI.GetCurrentBoundsScope().BottomLeft, UI.GetCurrentBoundsScope().Width, true, false);
				MenuHelper.DrawReservedMenuPanel(panelID, UI.GetCurrentBoundsScope());

				if (result == MenuHelper.CancelConfirmResult.Cancel)
				{
					UIDrawer.SetActiveMenu(UIDrawer.MenuType.None);
				}
				else if (result == MenuHelper.CancelConfirmResult.Confirm)
				{
					// Store the key binding as a string instead of a char
					Project.ActiveProject.NotifyKeyChipBindingChanged(keyChip, chosenKey);
					UIDrawer.SetActiveMenu(UIDrawer.MenuType.None);
				}
			}
		}
		
		private static bool IsMouseButton(KeyCode key)
		{
			return key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6;
		}
		
		private static bool TryGetKeyDisplayName(KeyCode key, out string displayName)
		{
			// Check if we have a special mapping
			if (KeyDisplayNames.TryGetValue(key, out displayName))
			{
				return true;
			}
			
			// Handle letter keys
			if (key >= KeyCode.A && key <= KeyCode.Z)
			{
				displayName = ((char)key).ToString().ToUpper();
				return true;
			}
			
			// Handle number keys (top row)
			if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
			{
				displayName = ((char)('0' + (key - KeyCode.Alpha0))).ToString();
				return true;
			}
			
			// Handle numpad keys
			if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
			{
				displayName = "KP" + (key - KeyCode.Keypad0);
				return true;
			}
			
			// Handle other keys with direct character representation
			if ((int)key >= 32 && (int)key <= 126) // ASCII printable range
			{
				displayName = ((char)key).ToString();
				return true;
			}
			
			// If we get here, we don't have a display name for this key
			displayName = key.ToString();
			return true; // Return true anyway to allow all keys
		}

		public static void OnMenuOpened()
		{
			keyChip = (SubChipInstance)ContextMenu.interactionContext;
			chosenKey = ""; // Start with an empty key instead of the current one
			keySelected = false; // Not selected yet
		}
	}
}
