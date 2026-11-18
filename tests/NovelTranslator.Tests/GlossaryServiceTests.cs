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
    public void FindMatchingEntries_RespectsWordBoundariesForAsciiTerms()
    {
        var entries = new List<GlossaryEntry>
        {
            new() { SourceTerm = "cat", TargetTerm = "แมว", Category = GlossaryCategory.Term }
        };

        var matched = GlossaryService.FindMatchingEntries("A caterpillar crawled across the leaf.", entries);
        Assert.Empty(matched);
    }

    [Fact]
    public void ExtractedTerm_IsSelected_RaisesPropertyChanged()
    {
        var term = new ExtractedTerm { SourceTerm = "Mana", TargetTerm = "มานา" };
        string? changedProp = null;
        term.PropertyChanged += (_, e) => changedProp = e.PropertyName;

        term.IsSelected = false;

        Assert.Equal(nameof(ExtractedTerm.IsSelected), changedProp);
        Assert.False(term.IsSelected);
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

    [Fact]
    public async System.Threading.Tasks.Task GlossaryViewModel_ImportCsvAsync_DeduplicatesAndUpdatesExistingTerms()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"test_gl_vm_{Path.GetRandomFileName()}.db");
        string csvPath = Path.Combine(Path.GetTempPath(), $"test_gl_vm_{Path.GetRandomFileName()}.csv");
        try
        {
            var db = new NovelTranslator.Core.Data.NovelDatabase(dbPath);
            db.Initialize();
            var repo = new NovelTranslator.Core.Data.NovelRepository(db);
            var story = new Story { Title = "CSV Story" };
            story.Id = await repo.InsertStoryAsync(story);

            await repo.InsertGlossaryEntryAsync(new GlossaryEntry
            {
                StoryId = story.Id,
                SourceTerm = "Arthur",
                TargetTerm = "อาเธอร์เดิม",
                Category = GlossaryCategory.Person
            });

            var vm = new NovelTranslator.App.ViewModels.GlossaryViewModel(repo, story);
            await vm.LoadEntriesAsync();

            File.WriteAllText(csvPath, "SourceTerm,TargetTerm,Category,Notes\nArthur,อาเธอร์ใหม่,Person,อัปเดต\nExcalibur,เอ็กซ์คาลิเบอร์,Term,ดาบ\nexcalibur,เอ็กซ์คาลิเบอร์ 2,Term,ล่าสุด\n");

            await vm.ImportCsvAsync(csvPath);

            var all = await repo.GetGlossaryByStoryAsync(story.Id);
            Assert.Equal(2, all.Count);
            Assert.Equal("อาเธอร์ใหม่", all.Find(e => e.SourceTerm == "Arthur")!.TargetTerm);
            Assert.Equal("เอ็กซ์คาลิเบอร์ 2", all.Find(e => e.SourceTerm.Equals("Excalibur", System.StringComparison.OrdinalIgnoreCase))!.TargetTerm);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task GlossaryViewModel_EditEntry_MergesWhenRenamedToExistingSourceTerm()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"test_gl_dup_{Path.GetRandomFileName()}.db");
        try
        {
            var db = new NovelTranslator.Core.Data.NovelDatabase(dbPath);
            db.Initialize();
            var repo = new NovelTranslator.Core.Data.NovelRepository(db);
            var story = new Story { Title = "Dup Story" };
            story.Id = await repo.InsertStoryAsync(story);

            await repo.InsertGlossaryEntryAsync(new GlossaryEntry { StoryId = story.Id, SourceTerm = "Arthur", TargetTerm = "อาเธอร์เดิม", Category = GlossaryCategory.Person });
            await repo.InsertGlossaryEntryAsync(new GlossaryEntry { StoryId = story.Id, SourceTerm = "Artoria", TargetTerm = "อาเธอเรีย", Category = GlossaryCategory.Person });

            var vm = new NovelTranslator.App.ViewModels.GlossaryViewModel(repo, story);
            await vm.LoadEntriesAsync();

            vm.SelectedEntry = System.Linq.Enumerable.First(vm.Entries, e => e.SourceTerm == "Artoria");
            vm.SourceTerm = "Arthur";
            vm.TargetTerm = "อาเธอร์ที่แก้แล้ว";
            await vm.SaveEntryCommand.ExecuteAsync(null);

            var all = await repo.GetGlossaryByStoryAsync(story.Id);
            Assert.Single(all);
            Assert.Equal("อาเธอร์ที่แก้แล้ว", all[0].TargetTerm);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
