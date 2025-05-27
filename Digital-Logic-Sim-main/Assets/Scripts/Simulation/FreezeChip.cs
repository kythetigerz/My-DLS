using System;
using DLS.Description;
using DLS.Simulation;

namespace DLS.Simulation
{
    public static class FreezeChip
    {     
        // Check if a chip is frozen (freeze pin is high)
        public static bool IsChipFrozen(SimChip chip)
        {
            // Go through every pin and check if it's pin id is -1 (freeze pin)
            foreach (var pin in chip.InputPins)
            {
                if (pin.ID == -1)
                {
                    // Get pin state, and if it's state is on, then return true
                    if (pin.State == 1)
                    {
                        return true;
                    }
                }
            }
            
            // If no freeze pin found or freeze pin is off, return false
            return false;
        }      
        // yeah I have no clue, had to ask chatgpt to write this
        private const uint FreezeFlagMask = 0x80000000; // Using the highest bit as the freeze flag
        
        // sets freeze feature (you dumb if you don't get this)
        public static void SetFreezeFeature(SimChip chip, bool enabled)
        {
            // very cool
            if (chip.InternalState == null || chip.InternalState.Length == 0)
            {
                // forgot, but very cool
                return;
            }
                
            if (enabled)
                chip.InternalState[0] |= FreezeFlagMask;
            else
                chip.InternalState[0] &= ~FreezeFlagMask;
        }
        
        // something with to do with "Ensure Internal State And Set Freeze"
        public static bool EnsureInternalStateAndSetFreeze(SimChip chip, bool enabled)
        {
            // mr. if statement
            if (chip.InternalState == null || chip.InternalState.Length == 0)
            {
                // return false to indicate failure
                return false;
            }
            
            // set freeze :chill:
            SetFreezeFeature(chip, enabled);
            return true;
        }
    }
}
