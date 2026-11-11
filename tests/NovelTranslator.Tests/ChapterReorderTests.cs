using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NovelTranslator.Core.Data;
using NovelTranslator.Core.Models;
using Xunit;

namespace NovelTranslator.Tests;

public class ChapterReorderTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly NovelDatabase _database;
    private readonly NovelRepository _repository;

    public ChapterReorderTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_reorder_{Path.GetRandomFileName()}.db");
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
    public async Task ReorderChapters_MultipleItems_MovesTogetherPreservingOrder()
    {
        var story = new Story { Title = "Multi Drag Story" };
        long storyId = await _repository.InsertStoryAsync(story);

        var chapters = new List<Chapter>();
        for (int i = 1; i <= 5; i++)
        {
            var c = new Chapter { StoryId = storyId, Ordinal = i, Title = $"ตอนที่ {i}" };
            c.Id = await _repository.InsertChapterAsync(c);
            chapters.Add(c);
        }

        // Simulate moving chapters 2 and 4 to after chapter 5
        var moving = new List<Chapter> { chapters[1], chapters[3] }; // ตอนที่ 2, ตอนที่ 4
        var target = chapters[4]; // ตอนที่ 5
        bool dropAfter = true;

        // Perform reorder logic
        var toMove = chapters.Where(c => moving.Contains(c)).ToList();
        foreach (var c in toMove) chapters.Remove(c);

        int targetIdx = chapters.IndexOf(target);
        if (dropAfter) targetIdx++;

        for (int i = 0; i < toMove.Count; i++)
        {
            chapters.Insert(targetIdx + i, toMove[i]);
        }

        // Resequence
        var updates = new List<(long Id, int Ordinal)>();
        for (int i = 0; i < chapters.Count; i++)
        {
            chapters[i].Ordinal = i + 1;
            updates.Add((chapters[i].Id, i + 1));
        }
        await _repository.UpdateChapterOrdinalsAsync(updates);

        var inDb = await _repository.GetChaptersByStoryAsync(storyId);
        // Expected order: ตอนที่ 1, ตอนที่ 3, ตอนที่ 5, ตอนที่ 2, ตอนที่ 4
        Assert.Equal("ตอนที่ 1", inDb[0].Title);
        Assert.Equal("ตอนที่ 3", inDb[1].Title);
        Assert.Equal("ตอนที่ 5", inDb[2].Title);
        Assert.Equal("ตอนที่ 2", inDb[3].Title);
        Assert.Equal("ตอนที่ 4", inDb[4].Title);

        for (int i = 0; i < inDb.Count; i++)
        {
            Assert.Equal(i + 1, inDb[i].Ordinal);
        }
    }

    [Fact]
    public async Task InsertChapterBeforeAndAfter_MaintainsOrdinalSequence()
    {
        var story = new Story { Title = "Insert Test" };
        long storyId = await _repository.InsertStoryAsync(story);

        var ch1 = new Chapter { StoryId = storyId, Ordinal = 1, Title = "ตอนที่ 1" };
        var ch2 = new Chapter { StoryId = storyId, Ordinal = 2, Title = "ตอนที่ 2" };
        ch1.Id = await _repository.InsertChapterAsync(ch1);
        ch2.Id = await _repository.InsertChapterAsync(ch2);

        var chapters = new List<Chapter> { ch1, ch2 };

        // Insert between 1 and 2 (after ch1)
        var inserted = new Chapter { StoryId = storyId, Ordinal = 2, Title = "ตอนที่ 1.5" };
        inserted.Id = await _repository.InsertChapterAsync(inserted);
        chapters.Insert(1, inserted);

        var updates = new List<(long Id, int Ordinal)>();
        for (int i = 0; i < chapters.Count; i++)
        {
            chapters[i].Ordinal = i + 1;
            updates.Add((chapters[i].Id, i + 1));
        }
        await _repository.UpdateChapterOrdinalsAsync(updates);

        var inDb = await _repository.GetChaptersByStoryAsync(storyId);
        Assert.Equal(3, inDb.Count);
        Assert.Equal("ตอนที่ 1", inDb[0].Title);
        Assert.Equal("ตอนที่ 1.5", inDb[1].Title);
        Assert.Equal("ตอนที่ 2", inDb[2].Title);

        Assert.Equal(1, inDb[0].Ordinal);
        Assert.Equal(2, inDb[1].Ordinal);
        Assert.Equal(3, inDb[2].Ordinal);
    }

    [Fact]
    public async Task BatchAddChapters_CreatesSequentialOrdinals()
    {
        var story = new Story { Title = "Batch Add Test" };
        long storyId = await _repository.InsertStoryAsync(story);

        int batchCount = 5;
        for (int i = 1; i <= batchCount; i++)
        {
            var ch = new Chapter { StoryId = storyId, Ordinal = i, Title = $"ตอนที่ {i}" };
            ch.Id = await _repository.InsertChapterAsync(ch);
        }

        var inDb = await _repository.GetChaptersByStoryAsync(storyId);
        Assert.Equal(5, inDb.Count);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(i + 1, inDb[i].Ordinal);
            Assert.Equal($"ตอนที่ {i + 1}", inDb[i].Title);
        }
    }
}
