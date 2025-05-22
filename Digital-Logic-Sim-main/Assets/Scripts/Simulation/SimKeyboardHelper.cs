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
			// Letters
			KeyCode.A, KeyCode.B, KeyCode.C, KeyCode.D, KeyCode.E, KeyCode.F, KeyCode.G,
			KeyCode.H, KeyCode.I, KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.M, KeyCode.N,
			KeyCode.O, KeyCode.P, KeyCode.Q, KeyCode.R, KeyCode.S, KeyCode.T, KeyCode.U,
			KeyCode.V, KeyCode.W, KeyCode.X, KeyCode.Y, KeyCode.Z,

			// Numbers (top row)
			KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
			KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
			
			// Special characters and symbols
			KeyCode.Minus, KeyCode.Equals, KeyCode.LeftBracket, KeyCode.RightBracket,
			KeyCode.Backslash, KeyCode.Semicolon, KeyCode.Quote, KeyCode.Comma,
			KeyCode.Period, KeyCode.Slash, KeyCode.BackQuote,

			// Mouse
			KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2, KeyCode.Mouse3, KeyCode.Mouse4,
			
			// Modifier keys
			KeyCode.LeftShift, KeyCode.RightShift,
			KeyCode.LeftControl, KeyCode.RightControl,
			KeyCode.LeftAlt, KeyCode.RightAlt,
			KeyCode.LeftMeta, KeyCode.RightMeta, // Windows/Command keys
			
			// Special keys
			KeyCode.Space, KeyCode.Return, KeyCode.Tab, KeyCode.Backspace, KeyCode.Delete,
			KeyCode.Escape, KeyCode.Home, KeyCode.End, KeyCode.Insert, KeyCode.Menu,
			
			// Arrow keys
			KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
			KeyCode.PageUp, KeyCode.PageDown,
			
			// Function keys
			KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6,
			KeyCode.F7, KeyCode.F8, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12,
			KeyCode.F13, KeyCode.F14, KeyCode.F15,
			
			// Numpad
			KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4,
			KeyCode.Keypad5, KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9,
			KeyCode.KeypadPeriod, KeyCode.KeypadDivide, KeyCode.KeypadMultiply,
			KeyCode.KeypadMinus, KeyCode.KeypadPlus, KeyCode.KeypadEnter, KeyCode.KeypadEquals,
			KeyCode.Numlock,
			
			// Other control keys
			KeyCode.CapsLock, KeyCode.ScrollLock, KeyCode.Print, KeyCode.Break, KeyCode.Pause
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
			
			// Check if this hash matches any of our known key identifiers
			foreach (KeyCode key in ValidInputKeys)
			{
				string keyId = GetKeyIdentifier(key);
				if ((keyId.GetHashCode() & 0x7FFFFFFF) == hash)
				{
					// Store this for future lookups
					RegisterKeyString(hash, keyId);
					return keyId;
				}
			}
			
			// If we can't find a match, return a default
			return hash.ToString();
		}

		// Get a string identifier for a key
		private static string GetKeyIdentifier(KeyCode key)
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
