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

        // 3. Translation Memory query
        var tmMatches = await _repository.FindSimilarTranslationAsync(storyId, "dragon soared high", limit: 3);
        Assert.NotEmpty(tmMatches);
        Assert.Equal("มังกรทะยานขึ้นสู่ท้องฟ้ายามค่ำคืนอย่างสง่างาม", tmMatches[0].MatchedTranslation);
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
}
