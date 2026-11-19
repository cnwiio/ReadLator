using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NovelTranslator.App.ViewModels;
using NovelTranslator.Core.Models;

namespace NovelTranslator.App.Views;

public partial class GlossaryWindow : Window
{
    private GlossaryViewModel? ViewModel => DataContext as GlossaryViewModel;
    private bool _isSyncingSelection;

    public GlossaryWindow()
    {
        InitializeComponent();
        GlossaryDataGrid.SelectionChanged += OnGlossarySelectionChanged;
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    public GlossaryWindow(GlossaryViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose = Close;
        viewModel.RequestConfirmDialog = msg => MainWindow.ShowConfirmDialogAsync(this, msg);
        viewModel.RequestClearGridSelection = () =>
        {
            _isSyncingSelection = true;
            try
            {
                GlossaryDataGrid.SelectedItems.Clear();
                GlossaryDataGrid.SelectedItem = null;
            }
            finally
            {
                _isSyncingSelection = false;
            }
        };
        Loaded += async (_, _) => await viewModel.LoadEntriesAsync();
    }

    private void OnGlossarySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null || _isSyncingSelection) return;
        var selected = GlossaryDataGrid.SelectedItems?.OfType<GlossaryEntry>().ToList() ?? new List<GlossaryEntry>();
        ViewModel.SetSelectedEntries(selected);
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        if (ViewModel == null) return;
        if (GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        if (e.Key == Key.A && e.KeyModifiers == KeyModifiers.Control)
        {
            _isSyncingSelection = true;
            try
            {
                GlossaryDataGrid.SelectedItems.Clear();
                foreach (var entry in ViewModel.Entries)
                {
                    GlossaryDataGrid.SelectedItems.Add(entry);
                }
            }
            finally
            {
                _isSyncingSelection = false;
            }
            ViewModel.SetSelectedEntries(ViewModel.Entries.ToList());
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && ViewModel.CanDeleteSelected)
        {
            e.Handled = true;
            await ViewModel.DeleteEntryAsync();
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnImportCsvClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "เลือกไฟล์ CSV สำหรับนำเข้า Glossary",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } }
            }
        });

        if (files.Count > 0)
        {
            string path = files[0].Path.LocalPath;
            await ViewModel.ImportCsvAsync(path);
        }
    }

    private async void OnExportCsvClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "บันทึกไฟล์ Glossary CSV",
            DefaultExtension = "csv",
            SuggestedFileName = "glossary.csv",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } }
            }
        });

        if (file != null)
        {
            ViewModel.ExportCsv(file.Path.LocalPath);
        }
    }
}
