using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace NovelTranslator.Core.Data;

public class NovelDatabase
{
    private readonly string _connectionString;

    public NovelDatabase(string? dbPath = null)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "NovelTranslator");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            dbPath = Path.Combine(folder, "novels.db");
        }
        _connectionString = $"Data Source={dbPath};Foreign Keys=True";
    }

    public SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public void Initialize()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        // เปิดโหมด WAL เพื่อความปลอดภัยต่อข้อมูลเมื่อปิดโปรแกรมฉับพลัน
        cmd.CommandText = @"
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;

            CREATE TABLE IF NOT EXISTS Stories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                SourceLang TEXT NOT NULL DEFAULT 'English',
                TargetLang TEXT NOT NULL DEFAULT 'Thai',
                StyleNotes TEXT NOT NULL DEFAULT '',
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Chapters (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StoryId INTEGER NOT NULL,
                Ordinal INTEGER NOT NULL,
                Title TEXT NOT NULL,
                OriginalText TEXT NOT NULL DEFAULT '',
                TranslatedText TEXT NOT NULL DEFAULT '',
                Status INTEGER NOT NULL DEFAULT 0,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (StoryId) REFERENCES Stories(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS ChapterChunks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChapterId INTEGER NOT NULL,
                ChunkIndex INTEGER NOT NULL,
                SourceText TEXT NOT NULL,
                TranslatedText TEXT NOT NULL DEFAULT '',
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                HasWarning INTEGER NOT NULL DEFAULT 0,
                WarningMessage TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (ChapterId) REFERENCES Chapters(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS GlossaryEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StoryId INTEGER NOT NULL,
                SourceTerm TEXT NOT NULL,
                TargetTerm TEXT NOT NULL,
                Category INTEGER NOT NULL DEFAULT 2,
                Notes TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (StoryId) REFERENCES Stories(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_chapters_story ON Chapters(StoryId, Ordinal);
            CREATE INDEX IF NOT EXISTS idx_chunks_chapter ON ChapterChunks(ChapterId, ChunkIndex);
            CREATE INDEX IF NOT EXISTS idx_glossary_story ON GlossaryEntries(StoryId);

            -- ================= SQLite FTS5 Full-Text Search =================
            CREATE VIRTUAL TABLE IF NOT EXISTS Chapters_Fts USING fts5(
                ChapterId UNINDEXED,
                StoryId UNINDEXED,
                Title,
                OriginalText,
                TranslatedText,
                tokenize = 'unicode61'
            );

            CREATE TRIGGER IF NOT EXISTS trg_chapters_ai AFTER INSERT ON Chapters BEGIN
                INSERT INTO Chapters_Fts(ChapterId, StoryId, Title, OriginalText, TranslatedText)
                VALUES (new.Id, new.StoryId, new.Title, new.OriginalText, new.TranslatedText);
            END;

            CREATE TRIGGER IF NOT EXISTS trg_chapters_au AFTER UPDATE ON Chapters BEGIN
                DELETE FROM Chapters_Fts WHERE ChapterId = old.Id;
                INSERT INTO Chapters_Fts(ChapterId, StoryId, Title, OriginalText, TranslatedText)
                VALUES (new.Id, new.StoryId, new.Title, new.OriginalText, new.TranslatedText);
            END;

            CREATE TRIGGER IF NOT EXISTS trg_chapters_ad AFTER DELETE ON Chapters BEGIN
                DELETE FROM Chapters_Fts WHERE ChapterId = old.Id;
            END;
        ";
        cmd.ExecuteNonQuery();

        // ตรวจสอบและ Backfill ข้อมูลลง Chapters_Fts หากตาราง FTS ยังว่างเปล่าแต่มี Chapters อยู่
        using var backfillCmd = conn.CreateCommand();
        backfillCmd.CommandText = @"
            INSERT INTO Chapters_Fts(ChapterId, StoryId, Title, OriginalText, TranslatedText)
            SELECT Id, StoryId, Title, OriginalText, TranslatedText
            FROM Chapters
            WHERE (SELECT count(*) FROM Chapters_Fts) = 0;
        ";
        backfillCmd.ExecuteNonQuery();
    }

    public bool CheckIntegrity()
    {
        try
        {
            using var conn = CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var result = cmd.ExecuteScalar()?.ToString();
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public void BackupTo(string destinationPath)
    {
        using var sourceConn = CreateConnection();
        using var destConn = new SqliteConnection($"Data Source={destinationPath}");
        destConn.Open();
        sourceConn.BackupDatabase(destConn);
    }

    public void RestoreFrom(string sourceBackupPath)
    {
        using var sourceConn = new SqliteConnection($"Data Source={sourceBackupPath}");
        sourceConn.Open();
        using var destConn = CreateConnection();
        sourceConn.BackupDatabase(destConn);
    }
}
