using Avalonia.Controls;
using NovelTranslator.App.ViewModels;

namespace NovelTranslator.App.Views;

public partial class SearchReplaceWindow : Window
{
    public SearchReplaceWindow()
    {
        InitializeComponent();
    }

    public SearchReplaceWindow(SearchReplaceViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose = Close;
    }
}
