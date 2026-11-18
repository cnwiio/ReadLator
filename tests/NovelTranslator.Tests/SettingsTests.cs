using System;
using System.IO;
using NovelTranslator.App.ViewModels;
using NovelTranslator.Core.Glossary;
using NovelTranslator.Core.Settings;
using NovelTranslator.Core.Translation;
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

    [Fact]
    public void AppSettings_Shortkeys_CustomValuesPersist()
    {
        var settings = new AppSettings();
        settings.Shortkeys.TranslateKey = "F5";
        settings.Shortkeys.FullScreenKey = "F12";
        settings.EditorFontFamily = "Leelawadee UI";

        Assert.Equal("F5", settings.Shortkeys.TranslateKey);
        Assert.Equal("F12", settings.Shortkeys.FullScreenKey);
        Assert.Equal("Leelawadee UI", settings.EditorFontFamily);

        var defaults = new ShortkeysSettings();
        Assert.Equal("Ctrl+Enter", defaults.TranslateKey);
        Assert.Equal("F11", defaults.FullScreenKey);
    }

    [Fact]
    public void Chapter_InlineRenameProperties_WorkAsExpected()
    {
        var chapter = new NovelTranslator.Core.Models.Chapter { Title = "ตอนที่ 1" };
        Assert.False(chapter.IsEditingTitle);
        Assert.Equal(string.Empty, chapter.EditingTitle);

        chapter.EditingTitle = "ตอนที่ 1: การเริ่มต้นใหม่";
        chapter.IsEditingTitle = true;

        Assert.True(chapter.IsEditingTitle);
        Assert.Equal("ตอนที่ 1: การเริ่มต้นใหม่", chapter.EditingTitle);
    }

    [Fact]
    public void TranslationProgressInfo_StreamedWordCount_CanBeRecorded()
    {
        var info = new NovelTranslator.Core.Translation.TranslationProgressInfo
        {
            CurrentChunkIndex = 1,
            TotalChunks = 3,
            StreamedWordCount = 120,
            StatusMessage = "กำลังแปลส่วนที่ 1/3 (สตรีมสด 120 คำ)..."
        };

        Assert.Equal(120, info.StreamedWordCount);
        Assert.Contains("120 คำ", info.StatusMessage);
    }

    [Fact]
    public void AppSettings_Save_WritesAtomicallyWithoutLeavingTempFile()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"nt_settings_{Path.GetRandomFileName()}");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");

        try
        {
            var initial = new AppSettings { ApiKey = "old-key", ModelName = "m1" };
            initial.Save(path);

            var updated = new AppSettings { ApiKey = "new-key", ModelName = "m2" };
            updated.Save(path);

            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
            string json = File.ReadAllText(path);
            Assert.Contains("new-key", json);
            Assert.DoesNotContain("old-key", json);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AppSettings_PolishMode_DefaultsAndPersistence()
    {
        var defaults = new AppSettings();
        Assert.False(defaults.EnablePolishMode);
        Assert.Equal(string.Empty, defaults.PolishModelName);

        string dir = Path.Combine(Path.GetTempPath(), $"nt_polish_{Path.GetRandomFileName()}");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");

        try
        {
            var saved = new AppSettings
            {
                EnablePolishMode = true,
                PolishModelName = "qwen2.5:14b"
            };
            saved.Save(path);

            string json = File.ReadAllText(path);
            var reloaded = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

            Assert.NotNull(reloaded);
            Assert.True(reloaded!.EnablePolishMode);
            Assert.Equal("qwen2.5:14b", reloaded.PolishModelName);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AppSettings_BackendPipelineCustomization_DefaultsAndPersistence()
    {
        var defaults = new AppSettings();
        Assert.Equal(1.0f, defaults.TopP);
        Assert.Equal(0, defaults.MaxTokens);
        Assert.Equal(0.0f, defaults.FrequencyPenalty);
        Assert.Equal(300, defaults.RequestTimeoutSeconds);
        Assert.True(defaults.EnableCrossChapterContext);
        Assert.True(defaults.EnableLookAheadContext);
        Assert.Equal(PovDetectionMode.Auto, defaults.PovMode);
        Assert.True(defaults.StripThinkTags);
        Assert.Equal(25, defaults.MaxEnglishRatioPercent);
        Assert.Equal(1, defaults.MaxRetryAttempts);
        Assert.Equal(1500, defaults.GlossaryScanMaxWords);
        Assert.True(defaults.EnableAutoGlossaryScan);

        string dir = Path.Combine(Path.GetTempPath(), $"nt_pipeline_cfg_{Path.GetRandomFileName()}");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");

        try
        {
            var saved = new AppSettings
            {
                CustomPolishSystemPrompt = "Polish {SourceLang} to {TargetLang}\n{StyleNotes}",
                CustomGlossaryPrompt = "Extract {SourceText} / {TranslatedText}",
                TopP = 0.85f,
                MaxTokens = 2048,
                FrequencyPenalty = 0.4f,
                RequestTimeoutSeconds = 120,
                EnableCrossChapterContext = false,
                EnableLookAheadContext = false,
                PovMode = PovDetectionMode.FirstPerson,
                StripThinkTags = false,
                MaxEnglishRatioPercent = 60,
                MaxRetryAttempts = 3,
                CustomStripPrefixes = "Note:",
                GlossaryScanMaxWords = 0,
                GlossaryModelName = "fast-model",
                EnableAutoGlossaryScan = false
            };
            saved.Save(path);

            var reloaded = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
            Assert.NotNull(reloaded);
            Assert.Equal("Polish {SourceLang} to {TargetLang}\n{StyleNotes}", reloaded!.CustomPolishSystemPrompt);
            Assert.Equal("Extract {SourceText} / {TranslatedText}", reloaded.CustomGlossaryPrompt);
            Assert.Equal(0.85f, reloaded.TopP);
            Assert.Equal(2048, reloaded.MaxTokens);
            Assert.Equal(0.4f, reloaded.FrequencyPenalty);
            Assert.Equal(120, reloaded.RequestTimeoutSeconds);
            Assert.False(reloaded.EnableCrossChapterContext);
            Assert.False(reloaded.EnableLookAheadContext);
            Assert.Equal(PovDetectionMode.FirstPerson, reloaded.PovMode);
            Assert.False(reloaded.StripThinkTags);
            Assert.Equal(60, reloaded.MaxEnglishRatioPercent);
            Assert.Equal(3, reloaded.MaxRetryAttempts);
            Assert.Equal("Note:", reloaded.CustomStripPrefixes);
            Assert.Equal(0, reloaded.GlossaryScanMaxWords);
            Assert.Equal("fast-model", reloaded.GlossaryModelName);
            Assert.False(reloaded.EnableAutoGlossaryScan);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SettingsViewModel_MigratesLegacyPromptAndResetsToDefaults()
    {
        var settings = new AppSettings
        {
            CustomSystemPrompt = "ห้ามทับศัพท์",
            Temperature = 0.9f,
            TopP = 0.7f,
            MaxTokens = 1024,
            FrequencyPenalty = 0.5f,
            RequestTimeoutSeconds = 90,
            MaxChunkWords = 900,
            StripThinkTags = false,
            MaxEnglishRatioPercent = 80,
            MaxRetryAttempts = 4,
            CustomStripPrefixes = "Translator Note:"
        };

        var vm = new SettingsViewModel(settings);

        // Legacy short prompt is appended to full default template on load
        Assert.Contains("Anti-Translationese", vm.CustomSystemPrompt);
        Assert.Contains("คำสั่งพิเศษจากผู้ใช้: ห้ามทับศัพท์", vm.CustomSystemPrompt);
        Assert.Equal(PromptBuilder.DefaultPolishSystemPromptTemplate, vm.CustomPolishSystemPrompt);
        Assert.Equal(GlossaryExtractor.DefaultGlossaryPromptTemplate, vm.CustomGlossaryPrompt);

        // Edit prompts and invoke reset commands
        vm.CustomSystemPrompt = "Custom override";
        vm.CustomPolishSystemPrompt = "Custom polish";
        vm.CustomGlossaryPrompt = "Custom glossary";

        vm.ResetSystemPromptCommand.Execute(null);
        vm.ResetPolishPromptCommand.Execute(null);
        vm.ResetGlossaryPromptCommand.Execute(null);
        vm.ResetAiParametersCommand.Execute(null);
        vm.ResetValidatorSettingsCommand.Execute(null);

        Assert.Equal(PromptBuilder.DefaultSystemPromptTemplate, vm.CustomSystemPrompt);
        Assert.Equal(PromptBuilder.DefaultPolishSystemPromptTemplate, vm.CustomPolishSystemPrompt);
        Assert.Equal(GlossaryExtractor.DefaultGlossaryPromptTemplate, vm.CustomGlossaryPrompt);
        Assert.Equal(0.3f, vm.Temperature);
        Assert.Equal(1.0f, vm.TopP);
        Assert.Equal(0, vm.MaxTokens);
        Assert.Equal(0.0f, vm.FrequencyPenalty);
        Assert.Equal(300, vm.RequestTimeoutSeconds);
        Assert.Equal(600, vm.MaxChunkWords);
        Assert.True(vm.StripThinkTags);
        Assert.Equal(25, vm.MaxEnglishRatioPercent);
        Assert.Equal(1, vm.MaxRetryAttempts);
        Assert.Equal(string.Empty, vm.CustomStripPrefixes);
    }

    [Fact]
    public void SettingsViewModel_PopulateModelsAndApplyToSettings_SkipsEmbeddingAndClampsBounds()
    {
        var settings = new AppSettings
        {
            BaseUrl = "  http://127.0.0.1:1234/v1/  ",
            ModelName = "",
            PolishModelName = "custom-polish",
            GlossaryModelName = "custom-glossary",
            MaxChunkWords = 0,
            ContextParagraphCount = -3
        };
        var vm = new SettingsViewModel(settings);

        vm.PopulateAvailableModels(new System.Collections.Generic.List<NovelTranslator.Core.Llm.ModelInfo>
        {
            new() { Id = "text-embedding-nomic-embed-text-v1.5" },
            new() { Id = "typhoon2.1-gemma3-4b" }
        });

        Assert.Equal("typhoon2.1-gemma3-4b", vm.ModelName);
        Assert.Equal("custom-polish", vm.PolishModelName);
        Assert.Equal("custom-glossary", vm.GlossaryModelName);

        vm.ApplyToSettings();
        Assert.Equal("http://127.0.0.1:1234/v1/", settings.BaseUrl);
        Assert.Equal(600, settings.MaxChunkWords);
        Assert.Equal(0, settings.ContextParagraphCount);
    }

    [Fact]
    public void SettingsViewModel_EditingWithoutSave_DoesNotMutateAppSettings()
    {
        var settings = new AppSettings
        {
            Backend = BackendType.Ollama,
            BaseUrl = "http://localhost:11434/v1",
            EditorFontSize = 20,
            Temperature = 0.3f
        };
        var vm = new SettingsViewModel(settings);

        vm.EditorFontSize = 26;
        vm.Temperature = 0.8f;
        vm.MmbScrollSensitivity = 2.5;
        vm.SetShortcut(nameof(vm.TranslateKey), "F9");
        vm.ResetShortcut(nameof(vm.SaveKey));
        vm.BackendIndex = 1;

        Assert.False(vm.IsSaved);
        Assert.Equal(BackendType.Ollama, settings.Backend);
        Assert.Equal("http://localhost:11434/v1", settings.BaseUrl);
        Assert.Equal(20, settings.EditorFontSize);
        Assert.Equal(0.3f, settings.Temperature);
        Assert.Equal(1.0, settings.MmbScrollSensitivity);
        Assert.Equal("Ctrl+Enter", settings.Shortkeys.TranslateKey);
    }
}
