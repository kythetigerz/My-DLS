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

		public static int CreateIntegerStringNonAlloc(char[] charArray, long value)
		{
			// Check if the char array is large enough
			if (charArray == null || charArray.Length == 0)
			{
				return 0;
			}
			
			// Handle negative values
			bool isNegative = value < 0;
			ulong absValue = isNegative ? (ulong)(-value) : (ulong)value;
			
			// Special case for zero
			if (absValue == 0)
			{
				charArray[0] = '0';
				return 1;
			}
			
			// Convert digits manually without string allocation
			int digitCount = 0;
			ulong tempValue = absValue;
			
			// Count digits
			while (tempValue > 0)
			{
				tempValue /= 10;
				digitCount++;
			}
			
			int totalLength = isNegative ? digitCount + 1 : digitCount;
			
			// Make sure the array is large enough
			if (charArray.Length < totalLength)
			{
				return 0;
			}
			
			int startIndex = 0;
			
			// Add minus sign if negative
			if (isNegative)
			{
				charArray[0] = '-';
				startIndex = 1;
			}
			
			// Convert digits from right to left
			tempValue = absValue;
			for (int i = digitCount - 1; i >= 0; i--)
			{
				charArray[startIndex + i] = (char)('0' + (tempValue % 10));
				tempValue /= 10;
			}
			
			return totalLength;
		}

		// Keep the original unsigned version for backward compatibility
		public static int CreateIntegerStringNonAlloc(char[] charArray, ulong value)
		{
			return CreateIntegerStringNonAlloc(charArray, (long)value);
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