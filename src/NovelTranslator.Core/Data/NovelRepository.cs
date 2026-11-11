using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using NovelTranslator.Core.Models;

namespace NovelTranslator.Core.Data;

public class NovelRepository
{
    private readonly NovelDatabase _database;

    public NovelRepository(NovelDatabase database)
    {
        _database = database;
    }

    // ================= Stories =================

    public async Task<List<Story>> GetAllStoriesAsync()
    {
        using var conn = _database.CreateConnection();
        var stories = await conn.QueryAsync<Story>("SELECT * FROM Stories ORDER BY Id DESC");
        return stories.ToList();
    }

    public async Task<Story?> GetStoryByIdAsync(long id)
    {
        using var conn = _database.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Story>("SELECT * FROM Stories WHERE Id = @Id", new { Id = id });
    }

    public async Task<long> InsertStoryAsync(Story story)
    {
        using var conn = _database.CreateConnection();
        const string sql = @"
            INSERT INTO Stories (Title, SourceLang, TargetLang, StyleNotes, CreatedAt)
            VALUES (@Title, @SourceLang, @TargetLang, @StyleNotes, @CreatedAt);
            SELECT last_insert_rowid();";
        story.Id = await conn.ExecuteScalarAsync<long>(sql, story);
        return story.Id;
    }

    public async Task UpdateStoryAsync(Story story)
    {
        using var conn = _database.CreateConnection();
        const string sql = @"
            UPDATE Stories
            SET Title = @Title, SourceLang = @SourceLang, TargetLang = @TargetLang, StyleNotes = @StyleNotes
            WHERE Id = @Id";
        await conn.ExecuteAsync(sql, story);
    }

    public async Task DeleteStoryAsync(long id)
    {
        using var conn = _database.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Stories WHERE Id = @Id", new { Id = id });
    }

    // ================= Chapters =================

    public async Task<List<Chapter>> GetChaptersByStoryAsync(long storyId)
    {
        using var conn = _database.CreateConnection();
        var chapters = await conn.QueryAsync<Chapter>(
            "SELECT * FROM Chapters WHERE StoryId = @StoryId ORDER BY Ordinal ASC",
            new { StoryId = storyId });
        return chapters.ToList();
    }

    public async Task<Chapter?> GetChapterByIdAsync(long id)
    {
        using var conn = _database.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Chapter>("SELECT * FROM Chapters WHERE Id = @Id", new { Id = id });
    }

    public async Task<long> InsertChapterAsync(Chapter chapter)
    {
        using var conn = _database.CreateConnection();
        const string sql = @"
            INSERT INTO Chapters (StoryId, Ordinal, Title, OriginalText, TranslatedText, Status, UpdatedAt)
            VALUES (@StoryId, @Ordinal, @Title, @OriginalText, @TranslatedText, @Status, @UpdatedAt);
            SELECT last_insert_rowid();";
        chapter.Id = await conn.ExecuteScalarAsync<long>(sql, chapter);
        return chapter.Id;
    }

    public async Task UpdateChapterAsync(Chapter chapter)
    {
        using var conn = _database.CreateConnection();
        chapter.UpdatedAt = DateTime.UtcNow;
        const string sql = @"
            UPDATE Chapters
            SET Title = @Title, Ordinal = @Ordinal, OriginalText = @OriginalText,
                TranslatedText = @TranslatedText, Status = @Status, UpdatedAt = @UpdatedAt
            WHERE Id = @Id";
        await conn.ExecuteAsync(sql, chapter);
    }

    public async Task UpdateChapterOrdinalsAsync(IEnumerable<(long Id, int Ordinal)> ordinals)
    {
        using var conn = _database.CreateConnection();
        using var transaction = conn.BeginTransaction();
        foreach (var (id, ord) in ordinals)
        {
            await conn.ExecuteAsync(
                "UPDATE Chapters SET Ordinal = @Ordinal WHERE Id = @Id",
                new { Id = id, Ordinal = ord },
                transaction);
        }
        transaction.Commit();
    }

    public async Task DeleteChapterAsync(long id)
    {
        using var conn = _database.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM Chapters WHERE Id = @Id", new { Id = id });
    }

    // ================= Chapter Chunks (สำหรับการ Optimize แปลต่อจากจุดค้าง) =================

    public async Task<List<ChapterChunk>> GetChunksByChapterAsync(long chapterId)
    {
        using var conn = _database.CreateConnection();
        var chunks = await conn.QueryAsync<ChapterChunk>(
            "SELECT * FROM ChapterChunks WHERE ChapterId = @ChapterId ORDER BY ChunkIndex ASC",
            new { ChapterId = chapterId });
        return chunks.ToList();
    }

    public async Task SaveChunksAsync(long chapterId, List<string> chunkTexts, bool forceReset = false)
    {
        using var conn = _database.CreateConnection();
        using var transaction = conn.BeginTransaction();

        // ดึง chunks เดิมของตอนนี้มาตรวจสอบว่ามีส่วนใดที่แปลเสร็จแล้วบ้าง
        var existingChunks = (await conn.QueryAsync<ChapterChunk>(
            "SELECT * FROM ChapterChunks WHERE ChapterId = @ChapterId ORDER BY ChunkIndex ASC",
            new { ChapterId = chapterId },
            transaction)).ToList();

        // ล้าง chunks เก่าของตอนนี้ออกก่อนสร้างใหม่
        await conn.ExecuteAsync(
            "DELETE FROM ChapterChunks WHERE ChapterId = @ChapterId",
            new { ChapterId = chapterId },
            transaction);

        const string sql = @"
            INSERT INTO ChapterChunks (ChapterId, ChunkIndex, SourceText, TranslatedText, IsCompleted, HasWarning, WarningMessage)
            VALUES (@ChapterId, @ChunkIndex, @SourceText, @TranslatedText, @IsCompleted, @HasWarning, @WarningMessage)";

        for (int i = 0; i < chunkTexts.Count; i++)
        {
            string source = chunkTexts[i];
            string translated = string.Empty;
            bool isCompleted = false;
            bool hasWarning = false;
            string warningMessage = string.Empty;

            if (!forceReset)
            {
                // ตรวจสอบว่ามี chunk เดิมที่ข้อความต้นฉบับตรงกันและแปลเสร็จแล้วหรือไม่ (เน้น chunk ลำดับตรงกันก่อน หรือหาตามเนื้อหา)
                var matched = existingChunks.FirstOrDefault(c => c.ChunkIndex == i && c.SourceText == source && c.IsCompleted)
                              ?? existingChunks.FirstOrDefault(c => c.SourceText == source && c.IsCompleted);

                if (matched != null)
                {
                    translated = matched.TranslatedText;
                    isCompleted = true;
                    hasWarning = matched.HasWarning;
                    warningMessage = matched.WarningMessage;
                }
            }

            await conn.ExecuteAsync(sql, new
            {
                ChapterId = chapterId,
                ChunkIndex = i,
                SourceText = source,
                TranslatedText = translated,
                IsCompleted = isCompleted ? 1 : 0,
                HasWarning = hasWarning ? 1 : 0,
                WarningMessage = warningMessage
            }, transaction);
        }

        transaction.Commit();
    }

    public async Task UpdateChunkAsync(ChapterChunk chunk)
    {
        using var conn = _database.CreateConnection();
        const string sql = @"
            UPDATE ChapterChunks
            SET TranslatedText = @TranslatedText, IsCompleted = @IsCompleted,
                HasWarning = @HasWarning, WarningMessage = @WarningMessage
            WHERE Id = @Id";
        await conn.ExecuteAsync(sql, chunk);
    }

    // ================= Glossary =================

    public async Task<List<GlossaryEntry>> GetGlossaryByStoryAsync(long storyId)
    {
        using var conn = _database.CreateConnection();
        var entries = await conn.QueryAsync<GlossaryEntry>(
            "SELECT * FROM GlossaryEntries WHERE StoryId = @StoryId ORDER BY SourceTerm ASC",
            new { StoryId = storyId });
        return entries.ToList();
    }

    public async Task<long> InsertGlossaryEntryAsync(GlossaryEntry entry)
    {
        using var conn = _database.CreateConnection();
        const string sql = @"
            INSERT INTO GlossaryEntries (StoryId, SourceTerm, TargetTerm, Category, Notes)
            VALUES (@StoryId, @SourceTerm, @TargetTerm, @Category, @Notes);
            SELECT last_insert_rowid();";
        entry.Id = await conn.ExecuteScalarAsync<long>(sql, entry);
        return entry.Id;
    }

    public async Task UpdateGlossaryEntryAsync(GlossaryEntry entry)
    {
        using var conn = _database.CreateConnection();
        const string sql = @"
            UPDATE GlossaryEntries
            SET SourceTerm = @SourceTerm, TargetTerm = @TargetTerm, Category = @Category, Notes = @Notes
            WHERE Id = @Id";
        await conn.ExecuteAsync(sql, entry);
    }

    public async Task DeleteGlossaryEntryAsync(long id)
    {
        using var conn = _database.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM GlossaryEntries WHERE Id = @Id", new { Id = id });
    }

    public async Task BulkInsertGlossaryEntriesAsync(IEnumerable<GlossaryEntry> entries)
    {
        using var conn = _database.CreateConnection();
        using var transaction = conn.BeginTransaction();
        const string sql = @"
            INSERT INTO GlossaryEntries (StoryId, SourceTerm, TargetTerm, Category, Notes)
            VALUES (@StoryId, @SourceTerm, @TargetTerm, @Category, @Notes)";
        foreach (var entry in entries)
        {
            await conn.ExecuteAsync(sql, entry, transaction);
        }
        transaction.Commit();
    }
}
