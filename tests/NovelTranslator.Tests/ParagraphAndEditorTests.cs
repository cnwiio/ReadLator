using System.Collections.Generic;
using NovelTranslator.App.ViewModels;
using NovelTranslator.Core.Models;
using Xunit;

namespace NovelTranslator.Tests;

public class ParagraphAndEditorTests
{
    [Fact]
    public void SplitParagraphs_SplitsCorrectlyByDoubleNewlines()
    {
        string text = "Paragraph 1\r\n\r\nParagraph 2\n\nParagraph 3";
        var list = EditorViewModel.SplitParagraphs(text);
        Assert.Equal(3, list.Count);
        Assert.Equal("Paragraph 1", list[0]);
        Assert.Equal("Paragraph 2", list[1]);
        Assert.Equal("Paragraph 3", list[2]);
    }

    [Fact]
    public void SyncParagraphPairs_AlignsMatchingCountsAndHandlesMismatches()
    {
        var dummyDb = new NovelTranslator.Core.Data.NovelDatabase();
        var repo = new NovelTranslator.Core.Data.NovelRepository(dummyDb);
        var editor = new EditorViewModel(repo);

        string source = "P1\n\nP2\n\nP3";
        string translated = "ย่อหน้าที่ 1\n\nย่อหน้าที่ 2";

        editor.SyncParagraphPairs(source, translated);
        Assert.Equal(3, editor.ParagraphPairs.Count);
        Assert.Equal("P1", editor.ParagraphPairs[0].SourceText);
        Assert.Equal("ย่อหน้าที่ 1", editor.ParagraphPairs[0].TranslatedText);

        Assert.Equal("P3", editor.ParagraphPairs[2].SourceText);
        Assert.Equal("", editor.ParagraphPairs[2].TranslatedText);

        // Edit paragraph 3
        editor.ParagraphPairs[2].TranslatedText = "ย่อหน้าที่ 3";
        string combined = editor.CombineTranslatedParagraphs();
        Assert.Equal("ย่อหน้าที่ 1\n\nย่อหน้าที่ 2\n\nย่อหน้าที่ 3", combined);
    }

    [Fact]
    public void CombineAndSyncParagraphs_PreservesMiddleEmptySlotWithoutShifting()
    {
        var dummyDb = new NovelTranslator.Core.Data.NovelDatabase();
        var repo = new NovelTranslator.Core.Data.NovelRepository(dummyDb);
        var editor = new EditorViewModel(repo);

        string source = "P1\n\nP2\n\nP3";
        editor.SyncParagraphPairs(source, "คำแปล 1");
        Assert.Equal(3, editor.ParagraphPairs.Count);

        // User leaves paragraph 2 empty and translates paragraph 3
        editor.ParagraphPairs[2].TranslatedText = "คำแปล 3";
        string combined = editor.CombineTranslatedParagraphs();
        Assert.Equal("คำแปล 1\n\n\n\nคำแปล 3", combined);

        // Re-syncing keeps paragraph 2 empty and paragraph 3 aligned
        editor.SyncParagraphPairs(source, combined);
        Assert.Equal(3, editor.ParagraphPairs.Count);
        Assert.Equal("คำแปล 1", editor.ParagraphPairs[0].TranslatedText);
        Assert.Equal(string.Empty, editor.ParagraphPairs[1].TranslatedText);
        Assert.Equal("คำแปล 3", editor.ParagraphPairs[2].TranslatedText);
    }
}
