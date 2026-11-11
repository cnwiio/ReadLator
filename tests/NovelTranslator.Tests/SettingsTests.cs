using System;
using System.IO;
using NovelTranslator.Core.Settings;
using Xunit;

namespace NovelTranslator.Tests;

public class SettingsTests
{
    [Fact]
    public void AppSettings_ShortkeysAndSystemDefaults_AreInitialized()
    {
        var settings = new AppSettings();

        Assert.Equal("Tahoma", settings.EditorFontFamily);
        Assert.Equal(20, settings.EditorFontSize);
        Assert.Equal(1.5, settings.AutoSaveDelaySeconds);

        Assert.NotNull(settings.Shortkeys);
        Assert.Equal("Ctrl+Enter", settings.Shortkeys.TranslateKey);
        Assert.Equal("Ctrl+Shift+Enter", settings.Shortkeys.TranslateAllKey);
        Assert.Equal("Ctrl+F", settings.Shortkeys.SearchReplaceKey);
        Assert.Equal("Ctrl+S", settings.Shortkeys.SaveKey);
        Assert.Equal("F11", settings.Shortkeys.FullScreenKey);
        Assert.Equal("F10", settings.Shortkeys.FocusModeKey);
        Assert.Equal("Alt+Left", settings.Shortkeys.PrevChapterKey);
        Assert.Equal("Alt+Right", settings.Shortkeys.NextChapterKey);
        Assert.Equal("Ctrl+B", settings.Shortkeys.ToggleSidebarKey);
    }
}
