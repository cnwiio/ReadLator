using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using NovelTranslator.App.ViewModels;

namespace NovelTranslator.App.Views;

public partial class SettingsWindow : Window
{
    private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    public SettingsWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, (_, e) => App.HandleOutsideTextBoxPointerPressed(this, e), RoutingStrategies.Tunnel);
    }

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose = Close;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel == null) return;

        if (string.IsNullOrEmpty(ViewModel.RecordingAction))
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
            return;
        }

        // หากกด Escape ให้ยกเลิกการบันทึก
        if (e.Key == Key.Escape)
        {
            ViewModel.RecordingAction = null;
            e.Handled = true;
            return;
        }

        // ข้ามหากกดเฉพาะปุ่ม Modifier เดี่ยวๆ (Ctrl, Alt, Shift, Meta)
        if (IsModifierKey(e.Key))
        {
            return;
        }

        string gestureStr = BuildKeyGestureString(e.KeyModifiers, e.Key);
        if (!string.IsNullOrEmpty(gestureStr))
        {
            ViewModel.SetShortcut(ViewModel.RecordingAction, gestureStr);
            e.Handled = true;
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

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl
                   or Key.LeftAlt or Key.RightAlt
                   or Key.LeftShift or Key.RightShift
                   or Key.LWin or Key.RWin;
    }

    private static string BuildKeyGestureString(KeyModifiers modifiers, Key key)
    {
        var sb = new StringBuilder();
        if ((modifiers & KeyModifiers.Control) != 0) sb.Append("Ctrl+");
        if ((modifiers & KeyModifiers.Alt) != 0) sb.Append("Alt+");
        if ((modifiers & KeyModifiers.Shift) != 0) sb.Append("Shift+");

        string keyName = FormatKeyName(key);
        if (string.IsNullOrEmpty(keyName)) return string.Empty;

        sb.Append(keyName);
        return sb.ToString();
    }

    private static string FormatKeyName(Key key)
    {
        return key switch
        {
            Key.Return => "Enter",
            Key.Back => "Backspace",
            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",
            _ => key.ToString()
        };
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
