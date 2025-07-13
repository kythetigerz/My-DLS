using System.Collections.Generic;
using Seb.Helpers;
using UnityEngine;

namespace DLS.Simulation
{
	public static class SimKeyboardHelper
	{
		// Expand to include all keyboard keys
		public static readonly KeyCode[] ValidInputKeys =
		{
			// Row 1 - Function keys row
			KeyCode.Escape, 
			KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
			KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12,
			KeyCode.Print, KeyCode.Insert, KeyCode.Delete,
			
			// Row 2 - Number row
			KeyCode.BackQuote, 
			KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, 
			KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
			KeyCode.Minus, KeyCode.Equals, KeyCode.Backspace,
			KeyCode.Numlock,
			
			// Row 3 - QWERTY row
			KeyCode.Tab,
			KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y, KeyCode.U,
			KeyCode.I, KeyCode.O, KeyCode.P, KeyCode.LeftBracket, KeyCode.RightBracket, KeyCode.Backslash,
			KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9,
			
			// Row 4 - ASDF row
			KeyCode.CapsLock,
			KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.J,
			KeyCode.K, KeyCode.L, KeyCode.Semicolon, KeyCode.Quote, KeyCode.Return,
			KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6,
			
			// Row 5 - ZXCV row
			KeyCode.LeftShift,
			KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.B, KeyCode.N, KeyCode.M,
			KeyCode.Comma, KeyCode.Period, KeyCode.Slash, KeyCode.RightShift,
			KeyCode.UpArrow,
			KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3,
			
			// Row 6 - Bottom row
			KeyCode.LeftControl, KeyCode.LeftMeta, KeyCode.LeftAlt, 
			KeyCode.Space, 
			KeyCode.RightAlt, KeyCode.RightControl, KeyCode.Menu,
			KeyCode.LeftArrow, KeyCode.DownArrow, KeyCode.RightArrow,
			KeyCode.Keypad0, KeyCode.KeypadPeriod, KeyCode.KeypadEnter,
			
			// Additional keys
			KeyCode.Home, KeyCode.End, KeyCode.PageUp, KeyCode.PageDown,
			KeyCode.F13, KeyCode.F14, KeyCode.F15,
			KeyCode.KeypadDivide, KeyCode.KeypadMultiply, KeyCode.KeypadMinus, 
			KeyCode.KeypadPlus, KeyCode.KeypadEquals,
			KeyCode.ScrollLock, KeyCode.Break, KeyCode.Pause,
			
			// Mouse
			KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2, KeyCode.Mouse3, KeyCode.Mouse4
		};

		// Dictionary to map KeyCodes to unique identifiers
		private static readonly Dictionary<KeyCode, string> KeyCodeToIdentifier = new Dictionary<KeyCode, string>
		{
			// Modifier keys
			{ KeyCode.LeftShift, "LSHIFT" },
			{ KeyCode.RightShift, "RSHIFT" },
			{ KeyCode.LeftControl, "LCTRL" },
			{ KeyCode.RightControl, "RCTRL" },
			{ KeyCode.LeftAlt, "LALT" },
			{ KeyCode.RightAlt, "RALT" },
			{ KeyCode.LeftMeta, "LWIN" },
			{ KeyCode.RightMeta, "RWIN" },
			
			// Special mappings for keys that don't have a direct character representation
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
			{ KeyCode.KeypadEnter, "KPENT" },
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
			{ KeyCode.F13, "F13" },
			{ KeyCode.F14, "F14" },
			{ KeyCode.F15, "F15" },
			
			// Numpad special keys
			{ KeyCode.KeypadDivide, "KP/" },
			{ KeyCode.KeypadMultiply, "KP*" },
			{ KeyCode.KeypadMinus, "KP-" },
			{ KeyCode.KeypadPlus, "KP+" },
			{ KeyCode.KeypadPeriod, "KP." },
			{ KeyCode.KeypadEquals, "KP=" },
			
			// Symbol keys with special identifiers for clarity
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

		static readonly HashSet<string> KeyLookup = new();
		static bool HasAnyInput;

		// Call from Main Thread
		public static void RefreshInputState()
		{
			lock (KeyLookup)
			{
				KeyLookup.Clear();
				HasAnyInput = false;

				if (!InputHelper.AnyKeyOrMouseHeldThisFrame) return; // early exit if no key held
				
				// Remove the modifier key check to allow all keys including modifiers
				// if (InputHelper.CtrlIsHeld || InputHelper.ShiftIsHeld || InputHelper.AltIsHeld) return;

				foreach (KeyCode key in ValidInputKeys)
				{
					if (InputHelper.IsKeyHeld(key))
					{
						string keyIdentifier = GetKeyIdentifier(key);
						KeyLookup.Add(keyIdentifier);
						HasAnyInput = true;
					}
				}
			}
		}
		
		// Dictionary to store hash-to-string mappings
		private static readonly Dictionary<int, string> HashToKeyString = new Dictionary<int, string>();

		public static void RegisterKeyString(int hash, string keyString)
		{
			lock (KeyLookup)
			{
				HashToKeyString[hash] = keyString;
			}
		}

		public static string GetKeyIdentifierFromHash(int hash)
		{
			lock (KeyLookup)
			{
				if (HashToKeyString.TryGetValue(hash, out string keyString))
				{
					return keyString;
				}
			}
			
			// Check if this hash corresponds to an index in our ValidInputKeys array
			// The hash is (index % 255) + 1, so we need to subtract 1 and check if it's in range
			int keyIndex = hash - 1;
			if (keyIndex >= 0 && keyIndex < ValidInputKeys.Length)
			{
				KeyCode key = ValidInputKeys[keyIndex];
				string keyId = GetKeyIdentifier(key);
				
				// Store this for future lookups
				RegisterKeyString(hash, keyId);
				return keyId;
			}
			
			// If we can't find a match, return a default
			return hash.ToString();
		}

		// Get a string identifier for a key
		public static string GetKeyIdentifier(KeyCode key)
		{
			// Check if we have a special mapping for this key
			if (KeyCodeToIdentifier.TryGetValue(key, out string identifier))
			{
				return identifier;
			}

			// Handle letter keys
			if (key >= KeyCode.A && key <= KeyCode.Z)
			{
				return ((char)key).ToString().ToUpper();
			}

			// Handle number keys (top row)
			if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
			{
				return ((char)('0' + (key - KeyCode.Alpha0))).ToString();
			}

			// Handle numpad keys
			if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
			{
				return "KP" + (key - KeyCode.Keypad0);
			}

			// Handle other keys with direct character representation
			if ((int)key >= 32 && (int)key <= 126) // ASCII printable range
			{
				return ((char)key).ToString();
			}

			// Fallback for any other keys
			return key.ToString();
		}

		// Call from Sim Thread
		public static bool KeyIsHeld(string keyIdentifier)
		{
			bool isHeld;

			lock (KeyLookup)
			{
				isHeld = HasAnyInput && KeyLookup.Contains(keyIdentifier);
			}

			return isHeld;
		}
		
		// For backward compatibility with existing code
		public static bool KeyIsHeld(char key)
		{
			return KeyIsHeld(key.ToString().ToUpper());
		}
	}
}