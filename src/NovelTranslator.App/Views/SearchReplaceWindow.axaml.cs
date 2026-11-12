using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NovelTranslator.App.ViewModels;

namespace NovelTranslator.App.Views;

public partial class SearchReplaceWindow : Window
{
    public SearchReplaceWindow()
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

    public SearchReplaceWindow(SearchReplaceViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose = Close;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
