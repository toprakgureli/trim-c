// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using TrimC.Desktop.ViewModels;

namespace TrimC.Desktop.Views
{
    /// <summary>
    /// The main application window.
    /// </summary>
    /// <remarks>
    /// Code-behind is limited to input concerns that XAML bindings cannot express: keyboard shortcuts that must yield
    /// to text input, and file drops. Both translate directly into view model commands.
    /// </remarks>
    internal sealed partial class MainWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AddHandler(DragDrop.DropEvent, OnDrop);
        }

        private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Letters and arrows belong to a focused text box, for example while a segment label is being typed.
            if (e.Handled || ViewModel is not MainWindowViewModel vm || FocusManager?.GetFocusedElement() is TextBox)
            {
                return;
            }

            bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            ICommand? command = e.Key switch
            {
                Key.Space => vm.TogglePlaybackCommand,
                Key.Left when ctrl => vm.PreviousKeyframeCommand,
                Key.Right when ctrl => vm.NextKeyframeCommand,
                Key.Left => vm.StepBackwardCommand,
                Key.Right => vm.StepForwardCommand,
                Key.I when shift => vm.SetSelectedStartCommand,
                Key.O when shift => vm.SetSelectedEndCommand,
                Key.I => vm.SetMarkInCommand,
                Key.O when ctrl => vm.OpenFileCommand,
                Key.O => vm.SetMarkOutCommand,
                Key.S => vm.SplitCommand,
                Key.X => vm.CutOutCommand,
                Key.Delete => vm.RemoveSelectedCommand,
                Key.E when ctrl => vm.ExportCommand,
                Key.Escape when vm.IsExporting => vm.ExportCancelCommand,
                _ => null,
            };

            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
                e.Handled = true;
            }
        }

        private void OnDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void OnDrop(object? sender, DragEventArgs e)
        {
            string? path = e.DataTransfer.TryGetFile()?.TryGetLocalPath();
            if (path is not null && ViewModel?.OpenFileCommand.CanExecute(path) == true)
            {
                ViewModel.OpenFileCommand.Execute(path);
            }
        }
    }
}
