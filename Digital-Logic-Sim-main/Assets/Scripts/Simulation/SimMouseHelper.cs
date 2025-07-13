using System.Collections.Generic;
using Seb.Helpers;
using UnityEngine;

namespace DLS.Simulation
{
    public static class SimMouseHelper
    {
        // Mouse buttons and actions
        public enum MouseInput
        {
            LeftButton,
            RightButton,
            MiddleButton,
            SideButton1,
            SideButton2,
            ScrollUp,
            ScrollDown
        }

        // Map mouse inputs to KeyCodes
        private static readonly Dictionary<MouseInput, KeyCode> MouseInputToKeyCode = new Dictionary<MouseInput, KeyCode>
        {
            { MouseInput.LeftButton, KeyCode.Mouse0 },
            { MouseInput.RightButton, KeyCode.Mouse1 },
            { MouseInput.MiddleButton, KeyCode.Mouse2 },
            { MouseInput.SideButton1, KeyCode.Mouse3 },
            { MouseInput.SideButton2, KeyCode.Mouse4 }
        };

        // Track mouse button states
        private static readonly Dictionary<MouseInput, bool> MouseInputStates = new Dictionary<MouseInput, bool>
        {
            { MouseInput.LeftButton, false },
            { MouseInput.RightButton, false },
            { MouseInput.MiddleButton, false },
            { MouseInput.SideButton1, false },
            { MouseInput.SideButton2, false },
            { MouseInput.ScrollUp, false },
            { MouseInput.ScrollDown, false }
        };

        private static float lastScrollDelta = 0f;
        private static readonly float scrollThreshold = 0.1f;

        // Call from Main Thread to update mouse state
        public static void RefreshInputState()
        {
            lock (MouseInputStates)
            {
                // Update button states
                foreach (var pair in MouseInputToKeyCode)
                {
                    MouseInputStates[pair.Key] = InputHelper.IsKeyHeld(pair.Value);
                }

                // Handle scroll wheel
                float scrollDelta = InputHelper.InputSource.MouseScrollDelta.y;
                
                // Only register scroll if it exceeds threshold
                if (Mathf.Abs(scrollDelta) > scrollThreshold)
                {
                    // Detect scroll direction
                    MouseInputStates[MouseInput.ScrollUp] = scrollDelta > 0;
                    MouseInputStates[MouseInput.ScrollDown] = scrollDelta < 0;
                }
                else
                {
                    // Reset scroll states if no significant scrolling
                    MouseInputStates[MouseInput.ScrollUp] = false;
                    MouseInputStates[MouseInput.ScrollDown] = false;
                }
                
                lastScrollDelta = scrollDelta;
            }
        }

        // Call from Sim Thread to check if a mouse input is active
        public static bool IsMouseInputActive(MouseInput input)
        {
            lock (MouseInputStates)
            {
                return MouseInputStates.TryGetValue(input, out bool isActive) && isActive;
            }
        }

        // Get a byte representing all mouse inputs (for use in the Mouse chip)
        public static byte GetMouseStateByte()
        {
            byte result = 0;
            lock (MouseInputStates)
            {
                if (MouseInputStates[MouseInput.LeftButton]) result |= 1;
                if (MouseInputStates[MouseInput.RightButton]) result |= 2;
                if (MouseInputStates[MouseInput.MiddleButton]) result |= 4;
                if (MouseInputStates[MouseInput.ScrollUp]) result |= 8;
                if (MouseInputStates[MouseInput.ScrollDown]) result |= 16;
                if (MouseInputStates[MouseInput.SideButton1]) result |= 32;
                if (MouseInputStates[MouseInput.SideButton2]) result |= 64;
            }
            return result;
        }
    }
}