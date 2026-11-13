using System.Collections.Generic;
using System.IO;
using NovelTranslator.Core.Glossary;
using NovelTranslator.Core.Models;
using Xunit;

namespace NovelTranslator.Tests;

public class GlossaryServiceTests
{
    [Fact]
    public void FindMatchingEntries_MatchesPresentTerms_IgnoresAbsent()
    {
        var entries = new List<GlossaryEntry>
        {
            new() { SourceTerm = "Arthur", TargetTerm = "อาเธอร์", Category = GlossaryCategory.Person },
            new() { SourceTerm = "Excalibur", TargetTerm = "เอ็กซ์คาลิเบอร์", Category = GlossaryCategory.Term },
            new() { SourceTerm = "Camelot", TargetTerm = "คาเมล็อต", Category = GlossaryCategory.Location }
        };

        string text = "Arthur unsheathed his legendary sword Excalibur in the battlefield.";
        var matched = GlossaryService.FindMatchingEntries(text, entries);

        Assert.Equal(2, matched.Count);
        Assert.Contains(matched, m => m.SourceTerm == "Arthur");
        Assert.Contains(matched, m => m.SourceTerm == "Excalibur");
        Assert.DoesNotContain(matched, m => m.SourceTerm == "Camelot");
    }

    [Fact]
    public void ExportAndImportCsv_RoundTrip_PreservesData()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_glossary_{Path.GetRandomFileName()}.csv");
        try
        {
            var original = new List<GlossaryEntry>
            {
                new() { SourceTerm = "Mana", TargetTerm = "มานา", Category = GlossaryCategory.Term, Notes = "พลังเวท" },
                new() { SourceTerm = "Alice", TargetTerm = "อลิซ", Category = GlossaryCategory.Person, Notes = "นางเอก" }
            };

            GlossaryService.ExportToCsv(tempFile, original);
            Assert.True(File.Exists(tempFile));

            var imported = GlossaryService.ImportFromCsv(tempFile, storyId: 1);
            Assert.Equal(2, imported.Count);
            Assert.Equal("Mana", imported[0].SourceTerm);
            Assert.Equal("มานา", imported[0].TargetTerm);
            Assert.Equal(GlossaryCategory.Term, imported[0].Category);
            Assert.Equal("พลังเวท", imported[0].Notes);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ExportAndImportCsv_RoundTrip_PreservesMultilineNotes()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_glossary_ml_{Path.GetRandomFileName()}.csv");
        try
        {
            var original = new List<GlossaryEntry>
            {
                new() { SourceTerm = "Mana", TargetTerm = "มานา", Category = GlossaryCategory.Term, Notes = "บรรทัด 1\nบรรทัด 2 \"อ้างอิง\"" },
                new() { SourceTerm = "Alice", TargetTerm = "อลิซ", Category = GlossaryCategory.Person, Notes = "นางเอก" }
            };

            GlossaryService.ExportToCsv(tempFile, original);
            var imported = GlossaryService.ImportFromCsv(tempFile, storyId: 1);

            Assert.Equal(2, imported.Count);
            Assert.Equal("Mana", imported[0].SourceTerm);
            Assert.Equal("บรรทัด 1\nบรรทัด 2 \"อ้างอิง\"", imported[0].Notes);
            Assert.Equal("Alice", imported[1].SourceTerm);
            Assert.Equal("นางเอก", imported[1].Notes);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
