// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TrimC.Desktop.Views
{
    /// <summary>
    /// The modal dialog that collects the export settings. It closes with <see langword="true"/> when the user confirms.
    /// </summary>
    internal sealed partial class ExportDialog : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExportDialog"/> class.
        /// </summary>
        public ExportDialog()
        {
            InitializeComponent();
        }

        private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);

        private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
    }
}
