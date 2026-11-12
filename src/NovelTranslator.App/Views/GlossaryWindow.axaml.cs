using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NovelTranslator.App.ViewModels;

namespace NovelTranslator.App.Views;

public partial class GlossaryWindow : Window
{
    private GlossaryViewModel? ViewModel => DataContext as GlossaryViewModel;

    public GlossaryWindow()
    {
        InitializeComponent();
        KeyDown += (s, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        };
    }

    public GlossaryWindow(GlossaryViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose = Close;
        Loaded += async (_, _) => await viewModel.LoadEntriesAsync();
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
