// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

            // Zooming changes only what the timeline shows, so it is handled by the control rather than the view model.
            Action? zoom = e.Key switch
            {
                Key.OemPlus or Key.Add when ctrl => Timeline.ZoomIn,
                Key.OemMinus or Key.Subtract when ctrl => Timeline.ZoomOut,
                Key.D0 or Key.NumPad0 when ctrl => Timeline.ZoomToFit,
                _ => null,
            };

            if (zoom is not null)
            {
                zoom();
                e.Handled = true;
                return;
            }

            ICommand? command = e.Key switch
            {
                Key.Z when ctrl && shift => vm.RedoCommand,
                Key.Z when ctrl => vm.UndoCommand,
                Key.Y when ctrl => vm.RedoCommand,
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
                Key.S when !ctrl => vm.SplitCommand,
                Key.X => vm.CutOutCommand,
                Key.M when !ctrl => vm.ToggleSoundCommand,
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

        private void OnZoomIn(object? sender, RoutedEventArgs e) => Timeline.ZoomIn();

        private void OnZoomOut(object? sender, RoutedEventArgs e) => Timeline.ZoomOut();

        private void OnZoomToFit(object? sender, RoutedEventArgs e) => Timeline.ZoomToFit();

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
