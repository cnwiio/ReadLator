using System;
using System.IO;
using System.Threading.Tasks;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Models;
using Xunit;

namespace NovelTranslator.Tests;

public class FtsAndGlossaryExtractorTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly NovelDatabase _database;
    private readonly NovelRepository _repository;

    public FtsAndGlossaryExtractorTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fts_{Path.GetRandomFileName()}.db");
        _database = new NovelDatabase(_tempDbPath);
        _database.Initialize();
        _repository = new NovelRepository(_database);
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public async Task FtsSearch_FindsMatchesInTranslatedAndOriginalText()
    {
        var story = new Story { Title = "FTS Story" };
        long storyId = await _repository.InsertStoryAsync(story);

        var ch1 = new Chapter
        {
            StoryId = storyId,
            Ordinal = 1,
            Title = "Chapter 1",
            OriginalText = "The dragon soared into the night sky.",
            TranslatedText = "มังกรทะยานขึ้นสู่ท้องฟ้ายามค่ำคืนอย่างสง่างาม"
        };
        ch1.Id = await _repository.InsertChapterAsync(ch1);

        // 1. Search Translated Text
        var transResults = await _repository.SearchFtsAsync(storyId, "มังกร", searchTranslated: true);
        Assert.NotEmpty(transResults);
        Assert.Equal(ch1.Id, transResults[0].ChapterId);
        Assert.Contains("มังกร", transResults[0].Snippet);

        // 2. Search Original Text
        var origResults = await _repository.SearchFtsAsync(storyId, "dragon", searchTranslated: false);
        Assert.NotEmpty(origResults);
        Assert.Equal(ch1.Id, origResults[0].ChapterId);
    }

    [Fact]
    public void GlossaryExtractor_ParsesJsonAndFiltersExistingTerms()
    {
        string json = @"[
            { ""source"": ""Lin Feng"", ""target"": ""หลินเฟิง"", ""category"": ""Person"", ""notes"": ""ตัวเอก"" },
            { ""source"": ""Sword Sect"", ""target"": ""สำนักกระบี่"", ""category"": ""Location"", ""notes"": ""นิกายหลัก"" },
            { ""source"": ""Old Guy"", ""target"": ""ตาแก่"", ""category"": ""Person"", ""notes"": ""มีอยู่แล้ว"" }
        ]";

        var existing = new System.Collections.Generic.HashSet<string> { "old guy" };
        var terms = NovelTranslator.Core.Glossary.GlossaryExtractor.ParseAndFilterResults(json, existing);

        Assert.Equal(2, terms.Count);
        Assert.Equal("Lin Feng", terms[0].SourceTerm);
        Assert.Equal("หลินเฟิง", terms[0].TargetTerm);
        Assert.Equal(GlossaryCategory.Person, terms[0].Category);

        Assert.Equal("Sword Sect", terms[1].SourceTerm);
        Assert.Equal(GlossaryCategory.Location, terms[1].Category);
    }

    [Fact]
    public void GlossaryExtractor_IgnoresTrailingBracketedNotes()
    {
        string jsonWithNote = """
            ```json
            [
                { "source": "Jade Seal", "target": "ตราหยก", "category": "Term", "notes": "ของวิเศษ" }
            ]
            ```
            [หมายเหตุ: ดึงเฉพาะคำสำคัญจากเนื้อหา]
            """;

        var terms = NovelTranslator.Core.Glossary.GlossaryExtractor.ParseAndFilterResults(jsonWithNote, new System.Collections.Generic.HashSet<string>());

        Assert.Single(terms);
        Assert.Equal("Jade Seal", terms[0].SourceTerm);
    }

    [Fact]
    public void GlossaryExtractor_StripsThinkTagsAndBuildsCustomPrompt()
    {
        string thinkResponse = """
            <think>
            Let me analyze [character names] and [skills] first.
            </think>
            [
                { "source": "Azure Flame", "target": "เพลิงคราม", "category": "Term", "notes": "ธาตุไฟ" }
            ]
            """;

        var terms = NovelTranslator.Core.Glossary.GlossaryExtractor.ParseAndFilterResults(
            thinkResponse,
            new System.Collections.Generic.HashSet<string>());

        Assert.Single(terms);
        Assert.Equal("Azure Flame", terms[0].SourceTerm);

        string withPlaceholders = NovelTranslator.Core.Glossary.GlossaryExtractor.BuildExtractionPrompt(
            "Custom extract:\n{SourceText}\n=>\n{TranslatedText}",
            "SRC",
            "TGT");
        Assert.Equal("Custom extract:\nSRC\n=>\nTGT", withPlaceholders);

        string omittedPlaceholders = NovelTranslator.Core.Glossary.GlossaryExtractor.BuildExtractionPrompt(
            "Only extract character names.",
            "SRC",
            "TGT");
        Assert.Contains("[ต้นฉบับ]\nSRC", omittedPlaceholders);
        Assert.Contains("[คำแปล]\nTGT", omittedPlaceholders);
    }
}
