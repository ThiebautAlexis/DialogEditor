﻿namespace DialogueEditor
{
    /// <summary>
    /// Enum used in the dialog sets to know what kind of dialog set has to be drawn and what behavior it has to apply. 
    /// </summary>
    public enum DialogSetType
    {
        BasicType,
        PlayerAnswer
    }

    /// <summary>
    /// Enum used in the dialog lines to know how to proceed to the next dialog Line 
    /// </summary>
    public enum WaitingType
    {
        None,
        WaitForClick,
        WaitForTime
    }


    /// <summary>
    /// Enum used in Dialog Starter 
    /// Create an exit for the node for each entry of the enum
    /// </summary>
    public enum DialogStarterEnum
    {
        Default, // Default 
    }
}