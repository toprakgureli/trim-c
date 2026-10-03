// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

namespace TrimC.Desktop.ViewModels
{
    /// <summary>
    /// A selectable value paired with its display text, used to populate combo boxes from enumerations.
    /// </summary>
    /// <typeparam name="T">The type of the underlying value.</typeparam>
    /// <param name="Value">The value applied when the option is selected.</param>
    /// <param name="Label">The text shown to the user.</param>
    internal sealed record ChoiceOption<T>(T Value, string Label)
    {
        /// <inheritdoc/>
        public override string ToString() => Label;
    }
}
