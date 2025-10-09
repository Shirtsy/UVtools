/*
 *                     GNU AFFERO GENERAL PUBLIC LICENSE
 *                       Version 3, 19 November 2007
 *  Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>
 *  Everyone is permitted to copy and distribute verbatim copies
 *  of this license document, but changing it is not allowed.
 */

using Emgu.CV;
using Emgu.CV.CvEnum;
using System;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using UVtools.Core;
using UVtools.Core.Extensions;
using UVtools.Core.Scripting;

namespace UVtools.ScriptSample;

/// <summary>
/// Performs a black inset around objects
/// </summary>
public class ScriptSmolTest1 : ScriptGlobals
{
    readonly ScriptNumericalInput<ushort> InsetMarginFromEdge = new()
    {
        Label = "Inset from edge",
        ToolTip = "Margin in pixels to inset from object edge",
        Unit = "px",
        Minimum = 1,
        Maximum = ushort.MaxValue,
        Increment = 1,
        Value = 10
    };

    readonly ScriptNumericalInput<ushort> InsetThickness = new()
    {
        Label = "Inset line thickness",
        ToolTip = "Inset line thickness in pixels",
        Unit = "px",
        Minimum = 1,
        Maximum = ushort.MaxValue,
        Increment = 1,
        Value = 5
    };

    /// <summary>
    /// Set configurations here, this function trigger just after load a script
    /// </summary>
    public void ScriptInit()
    {
        Script.Name = "Smol Test 1";
        Script.Description = "Does some fun unpredictable thngs!";
        Script.Author = "Smol";
        Script.Version = new Version(0, 1);
        Script.UserInputs.AddRange([
            InsetMarginFromEdge,
            InsetThickness
        ]);
    }

    /// <summary>
    /// Validate user inputs here, this function trigger when user click on execute
    /// </summary>
    /// <returns>A error message, empty or null if validation passes.</returns>
    public string? ScriptValidate() => null;

    /// <summary>
    /// Execute the script, this function trigger when when user click on execute and validation passes
    /// </summary>
    /// <returns>True if executes successfully to the end, otherwise false.</returns>
    public bool ScriptExecute()
    {
        
        
        // return true if not cancelled by user
        return !Progress.Token.IsCancellationRequested;
    }
}