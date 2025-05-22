using System;
using System.Collections.Generic;
using System.Linq;

namespace Seb.Helpers
{
	public static class StringHelper
	{
		static readonly string[] newLineStrings = { "\r\n", "\r", "\n" };

		public static string[] SplitByLine(string text, bool removeEmptyEntries = false)
		{
			StringSplitOptions options = removeEmptyEntries ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None;
			return text.Split(newLineStrings, options);
		}

		public static string CreateBinaryString(ulong value, bool removeLeadingZeroes = false)
		{
			// Convert ulong to binary string using Convert.ToString with long cast or custom approach
			string binary = Convert.ToString((long)value, 2);
			
			// For values that exceed long.MaxValue, we need a different approach
			if (value > long.MaxValue)
			{
				// Manual conversion for ulong values
				binary = "";
				ulong tempValue = value;
				do
				{
					binary = (tempValue & 1) + binary;
					tempValue >>= 1;
				} while (tempValue > 0);
			}
			
			if (!removeLeadingZeroes)
			{
				binary = binary.PadLeft(32, '0');
			}
			else
			{
				int paddedLength = (binary.Length + 7) / 8 * 8;
				binary = binary.PadLeft(paddedLength, '0');
			}

			IEnumerable<string> grouped = Enumerable.Range(0, binary.Length / 8)
				.Select(i => binary.Substring(i * 8, 8));

			return string.Join(" ", grouped);
		}

		public static int CreateIntegerStringNonAlloc(char[] charArray, ulong value)
		{
			// Check if the char array is large enough
			if (charArray == null || charArray.Length == 0)
			{
				return 0;
			}
			
			// We don't handle negative values here - this is for unsigned display
			// The DevPinInstance.GetStateDecimalDisplayValue() method handles signed/unsigned conversion
			//bool isNegative = false;
			
			// Special case for zero
			if (value == 0)
			{
				charArray[0] = '0';
				return 1;
			}
			
			// For ulong, we always treat it as unsigned (no negative sign)
			string valueStr = value.ToString();
			int digitCount = valueStr.Length;
			
			// Make sure the array is large enough
			if (charArray.Length < digitCount)
			{
				return 0;
			}
			
			// Copy the digits from the string representation
			for (int i = 0; i < digitCount; i++)
			{
				charArray[i] = valueStr[i];
			}
			
			return digitCount;
		}

		public static int CreateHexStringNonAlloc(char[] charArray, ulong value, bool upperCase = true)
		{
			// Check if the char array is large enough
			if (charArray == null || charArray.Length == 0)
			{
				return 0;
			}
			
			// Special case for zero
			if (value == 0)
			{
				charArray[0] = '0';
				return 1;
			}
			
			// Use ToString for reliable conversion
			string hexString = value.ToString(upperCase ? "X" : "x");
			int length = hexString.Length;
			
			// Make sure the array is large enough
			if (charArray.Length < length)
			{
				return 0;
			}
			
			// Copy the hex string to the char array
			for (int i = 0; i < length; i++)
			{
				charArray[i] = hexString[i];
			}
			
			return length;
		}
	}
}