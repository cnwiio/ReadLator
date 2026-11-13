using System.IO;
using System.Text;
using System.Threading.Tasks;
using NovelTranslator.Core.Models;

namespace NovelTranslator.Core.Export;

public class TextExportImportService
{
    /// <summary>
    /// ส่งออกตอนเดียวเป็นไฟล์ .txt
    /// </summary>
    public static async Task ExportChapterAsync(string filePath, Chapter chapter)
    {
        var sb = new StringBuilder();
        sb.AppendLine(chapter.Title);
        sb.AppendLine(new string('=', chapter.Title.Length));
        sb.AppendLine();
        sb.AppendLine(chapter.TranslatedText);

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// ส่งออกทั้งเรื่องรวมเป็นไฟล์เดียว .txt
    /// </summary>
    public static async Task ExportStorySingleFileAsync(string filePath, Story story, IEnumerable<Chapter> chapters)
    {
        var sb = new StringBuilder();
        sb.AppendLine(story.Title);
        sb.AppendLine(new string('=', story.Title.Length));
        sb.AppendLine();

        foreach (var chap in chapters.OrderBy(c => c.Ordinal))
        {
            sb.AppendLine($"--- {chap.Title} ---");
            sb.AppendLine();
            sb.AppendLine(chap.TranslatedText);
            sb.AppendLine();
            sb.AppendLine();
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// นำเข้าไฟล์ .txt (1 ไฟล์ = 1 ตอน)
    /// </summary>
    public static async Task<Chapter> ImportChapterFromFileAsync(string filePath, long storyId, int nextOrdinal)
    {
        string content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

        return new Chapter
        {
            StoryId = storyId,
            Ordinal = nextOrdinal,
            Title = fileNameWithoutExt,
            OriginalText = content,
            TranslatedText = string.Empty,
            Status = ChapterStatus.NotTranslated
        };
    }
}
