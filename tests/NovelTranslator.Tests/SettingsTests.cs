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

    [Fact]
    public void CustomFontService_ReadsRealTtfAndManagesInstallAndDelete()
    {
        string fontsAssetDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "NovelTranslator.App", "Assets", "Fonts"));
        string regularTtf = Path.Combine(fontsAssetDir, "tahoma.ttf");
        string boldTtf = Path.Combine(fontsAssetDir, "tahomabd.ttf");
        Assert.True(File.Exists(regularTtf));
        Assert.True(File.Exists(boldTtf));

        string tempDir = Path.Combine(Path.GetTempPath(), $"nt_fonts_{Path.GetRandomFileName()}");
        try
        {
            var (fam1, _) = CustomFontService.InstallFontFile(regularTtf, tempDir);
            var (fam2, _) = CustomFontService.InstallFontFile(boldTtf, tempDir);
            Assert.Equal("Tahoma", fam1);
            Assert.Equal("Tahoma", fam2);

            var groups = CustomFontService.GetCustomFonts(tempDir);
            Assert.Single(groups);
            Assert.Equal("Tahoma", groups[0].FamilyName);
            Assert.Equal(2, groups[0].FileCount);

            int deleted = CustomFontService.DeleteCustomFontFamily("Tahoma", tempDir);
            Assert.Equal(2, deleted);
            Assert.Empty(CustomFontService.GetCustomFonts(tempDir));

            string badFile = Path.Combine(tempDir, "broken.ttf");
            File.WriteAllBytes(badFile, new byte[] { 1, 2, 3, 4 });
            Assert.Throws<InvalidDataException>(() => CustomFontService.InstallFontFile(badFile, tempDir));

            string txtFile = Path.Combine(tempDir, "notes.txt");
            File.WriteAllText(txtFile, "not a font");
            Assert.Throws<InvalidDataException>(() => CustomFontService.InstallFontFile(txtFile, tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SettingsViewModel_UploadAndDeleteCustomFont_UpdatesListsAndFallsBackToTahoma()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"nt_vmfonts_{Path.GetRandomFileName()}");
        string sourceDir = Path.Combine(Path.GetTempPath(), $"nt_srcfonts_{Path.GetRandomFileName()}");
        Directory.CreateDirectory(sourceDir);
        try
        {
            string testFontPath = Path.Combine(sourceDir, "CustomNovelFont.otf");
            File.WriteAllBytes(testFontPath, CreateMinimalOpenTypeNameFont("CustomNovelFont"));

            var settings = new AppSettings { EditorFontFamily = "Tahoma" };
            var vm = new SettingsViewModel(settings, tempDir);

            Assert.Empty(vm.UploadedCustomFonts);
            Assert.True(vm.AddCustomFontFromFile(testFontPath));

            Assert.True(vm.HasUploadedCustomFonts);
            Assert.Single(vm.UploadedCustomFonts);
            Assert.Equal("CustomNovelFont", vm.UploadedCustomFonts[0].FamilyName);
            Assert.Contains("CustomNovelFont", vm.AvailableEditorFonts);
            Assert.Contains("CustomNovelFont", AppSettings.GetAllEditorFonts(tempDir));
            Assert.Equal("CustomNovelFont", vm.EditorFontFamily);

            vm.ApplyToSettings();
            Assert.Equal("CustomNovelFont", settings.EditorFontFamily);

            vm.DeleteCustomFont(vm.UploadedCustomFonts[0]);
            Assert.False(vm.HasUploadedCustomFonts);
            Assert.DoesNotContain("CustomNovelFont", vm.AvailableEditorFonts);
            Assert.Equal("Tahoma", vm.EditorFontFamily);
            Assert.Equal("Tahoma", settings.EditorFontFamily);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            if (Directory.Exists(sourceDir)) Directory.Delete(sourceDir, recursive: true);
        }
    }

    private static byte[] CreateMinimalOpenTypeNameFont(string familyName)
    {
        byte[] strBytes = System.Text.Encoding.BigEndianUnicode.GetBytes(familyName);
        int nameTableOffset = 12 + 16; // header + 1 table record
        int nameTableLength = 6 + 12 + strBytes.Length;
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        static void WriteU16BE(BinaryWriter w, ushort v) { w.Write((byte)(v >> 8)); w.Write((byte)v); }
        static void WriteU32BE(BinaryWriter w, uint v)
        {
            w.Write((byte)(v >> 24)); w.Write((byte)(v >> 16));
            w.Write((byte)(v >> 8)); w.Write((byte)v);
        }

        WriteU32BE(bw, 0x4F54544F); // OTTO
        WriteU16BE(bw, 1); // numTables
        WriteU16BE(bw, 16); WriteU16BE(bw, 0); WriteU16BE(bw, 0);

        WriteU32BE(bw, 0x6E616D65); // 'name'
        WriteU32BE(bw, 0);
        WriteU32BE(bw, (uint)nameTableOffset);
        WriteU32BE(bw, (uint)nameTableLength);

        WriteU16BE(bw, 0); // format
        WriteU16BE(bw, 1); // count
        WriteU16BE(bw, 18); // stringOffset
        WriteU16BE(bw, 3); WriteU16BE(bw, 1); WriteU16BE(bw, 1033); WriteU16BE(bw, 1);
        WriteU16BE(bw, (ushort)strBytes.Length); WriteU16BE(bw, 0);
        bw.Write(strBytes);

        return ms.ToArray();
    }

    [Fact]
    public void AppSettings_SanitizeApiKey_RemovesWhitespaceInvisibleCharsAndFlagsNonAscii()
    {
        string clean = AppSettings.SanitizeApiKey(" \t\r\nBearer sk-proj\u200B-\uFEFF123\u00A0abc\n", out bool removed, out bool hasUnsupported);
        Assert.Equal("sk-proj-123abc", clean);
        Assert.True(removed);
        Assert.False(hasUnsupported);

        string alreadyClean = AppSettings.SanitizeApiKey("AIzaSyD-12345_xyz", out removed, out hasUnsupported);
        Assert.Equal("AIzaSyD-12345_xyz", alreadyClean);
        Assert.False(removed);
        Assert.False(hasUnsupported);

        string withThai = AppSettings.SanitizeApiKey(" sk-กข123\u200B ", out removed, out hasUnsupported);
        Assert.Equal("sk-กข123", withThai);
        Assert.True(removed);
        Assert.True(hasUnsupported);
    }

    [Fact]
    public void SettingsViewModel_ApiKey_AutoCleansAndFlagsUnsupportedChars()
    {
        var vm = new SettingsViewModel(new AppSettings());

        vm.ApiKey = "  sk-test\u200B\r\n999  ";
        Assert.Equal("sk-test999", vm.ApiKey);
        Assert.True(vm.HasApiKeyInfo);
        Assert.False(vm.HasApiKeyError);

        vm.ApiKey = "sk-ทดสอบ";
        Assert.True(vm.HasApiKeyError);
        Assert.False(vm.HasApiKeyInfo);

        vm.ApiKey = "sk-clean-key";
        Assert.False(vm.HasApiKeyError);
        Assert.False(vm.HasApiKeyInfo);
    }

    [Fact]
    public void SettingsViewModel_ModelSelection_AutoAddsTypedModelsToDropdownWithoutResetting()
    {
        var settings = new AppSettings
        {
            ModelName = "custom/main-model",
            PolishModelName = "custom/polish-model",
            GlossaryModelName = "custom/glossary-model"
        };
        var vm = new SettingsViewModel(settings);

        Assert.Contains("custom/main-model", vm.AvailableModels);
        Assert.Contains("custom/polish-model", vm.AvailableModels);
        Assert.Contains("custom/glossary-model", vm.AvailableModels);

        vm.ModelName = "  new-typed/model-v2  ";
        vm.CommitTypedModelNames();
        Assert.Equal("new-typed/model-v2", vm.ModelName);
        Assert.Contains("new-typed/model-v2", vm.AvailableModels);

        vm.PopulateAvailableModels(new System.Collections.Generic.List<NovelTranslator.Core.Llm.ModelInfo>
        {
            new() { Id = "server-model-a" },
            new() { Id = "server-model-b" }
        });

        Assert.Equal("new-typed/model-v2", vm.ModelName);
        Assert.Equal("custom/polish-model", vm.PolishModelName);
        Assert.Equal("custom/glossary-model", vm.GlossaryModelName);
        Assert.Contains("new-typed/model-v2", vm.AvailableModels);
        Assert.Contains("server-model-a", vm.AvailableModels);
    }

    [Fact]
    public async System.Threading.Tasks.Task LlmClient_SanitizesDirtyApiKeyAndRejectsNonAsciiWithoutCrashing()
    {
        string? capturedAuthHeader = null;
        using var okHttpClient = new System.Net.Http.HttpClient(new StubModelHandler(req =>
        {
            capturedAuthHeader = req.Headers.Authorization?.ToString();
            return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent("{\"data\":[{\"id\":\"m1\"}]}", System.Text.Encoding.UTF8, "application/json")
            };
        }));

        var dirtyKeyClient = new NovelTranslator.Core.Llm.LlmClient(
            new AppSettings { BaseUrl = "http://127.0.0.1:1234/v1", ApiKey = " sk-ok\r\n\u200B123 " },
            okHttpClient);
        var (ok, _) = await dirtyKeyClient.TestConnectionAsync();
        Assert.True(ok);
        Assert.Equal("Bearer sk-ok123", capturedAuthHeader);

        var badAsciiClient = new NovelTranslator.Core.Llm.LlmClient(
            new AppSettings { BaseUrl = "http://127.0.0.1:1234/v1", ApiKey = "sk-ภาษาไทย" },
            okHttpClient);
        var (fail, errMsg) = await badAsciiClient.TestConnectionAsync();
        Assert.False(fail);
        Assert.Contains("ASCII", errMsg);
    }

    private sealed class StubModelHandler : System.Net.Http.HttpMessageHandler
    {
        private readonly Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> _handler;
        public StubModelHandler(Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> handler) => _handler = handler;

        protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request,
            System.Threading.CancellationToken cancellationToken)
            => System.Threading.Tasks.Task.FromResult(_handler(request));
    }
}
