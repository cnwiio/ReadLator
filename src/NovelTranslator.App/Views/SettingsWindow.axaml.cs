using Avalonia.Controls;
using NovelTranslator.App.ViewModels;

namespace NovelTranslator.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose = Close;
        Closing += (_, _) => viewModel.SaveSettings();
    }
}
