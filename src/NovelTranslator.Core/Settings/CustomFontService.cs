using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NovelTranslator.Core.Settings;

public sealed record CustomFontInfo(string FamilyName, IReadOnlyList<string> FilePaths)
{
    public int FileCount => FilePaths.Count;
    public string DisplayLabel => FileCount > 1 ? $"{FamilyName} ({FileCount} ไฟล์)" : FamilyName;
}

public static class CustomFontService
{
    private const long MaxFontFileBytes = 50L * 1024 * 1024;
    private const uint SfntTrueType = 0x00010000;
    private const uint SfntOpenTypeCff = 0x4F54544F; // OTTO
    private const uint SfntAppleTrueType = 0x74727565; // true
    private const uint NameTableTag = 0x6E616D65; // name

    public static string GetFontsDirectory()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "NovelTranslator", "fonts");
    }

    public static bool IsSupportedFontExtension(string? path)
    {
        string ext = Path.GetExtension(path ?? string.Empty);
        return ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".otf", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<CustomFontInfo> GetCustomFonts(string? fontsDirectory = null)
    {
        string dir = fontsDirectory ?? GetFontsDirectory();
        if (!Directory.Exists(dir))
        {
            return Array.Empty<CustomFontInfo>();
        }

        var grouped = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (string filePath in Directory.EnumerateFiles(dir)
                     .Where(IsSupportedFontExtension)
                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (!TryReadFontFamilyName(filePath, out string? familyName) || string.IsNullOrWhiteSpace(familyName))
            {
                continue;
            }

            if (!grouped.TryGetValue(familyName, out var list))
            {
                list = new List<string>();
                grouped[familyName] = list;
            }
            list.Add(filePath);
        }

        return grouped
            .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new CustomFontInfo(kv.Key, kv.Value.AsReadOnly()))
            .ToList();
    }

    public static void SyncFontList(IList<string> target, IReadOnlyList<string> updated)
    {
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!updated.Contains(target[i], StringComparer.OrdinalIgnoreCase))
            {
                target.RemoveAt(i);
            }
        }

        for (int i = 0; i < updated.Count; i++)
        {
            string font = updated[i];
            bool exists = target.Any(existing => string.Equals(existing, font, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                target.Insert(Math.Min(i, target.Count), font);
            }
        }
    }

    public static (string FamilyName, string InstalledPath) InstallFontFile(string sourceFilePath, string? fontsDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("ไม่พบไฟล์ฟอนต์ที่เลือก", sourceFilePath);
        }

        if (!IsSupportedFontExtension(sourceFilePath))
        {
            throw new InvalidDataException("รองรับเฉพาะไฟล์ฟอนต์ .ttf และ .otf เท่านั้น");
        }

        var fileInfo = new FileInfo(sourceFilePath);
        if (fileInfo.Length <= 0 || fileInfo.Length > MaxFontFileBytes)
        {
            throw new InvalidDataException("ขนาดไฟล์ฟอนต์ไม่ถูกต้อง (ต้องอยู่ระหว่าง 1 ไบต์ ถึง 50 MB)");
        }

        if (!TryReadFontFamilyName(sourceFilePath, out string? familyName) || string.IsNullOrWhiteSpace(familyName))
        {
            throw new InvalidDataException("ไฟล์ที่เลือกไม่ใช่ฟอนต์ .ttf/.otf ที่ถูกต้อง หรือไม่สามารถอ่านชื่อฟอนต์ได้");
        }

        string dir = fontsDirectory ?? GetFontsDirectory();
        Directory.CreateDirectory(dir);

        string safeFileName = Path.GetFileName(sourceFilePath);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            throw new InvalidDataException("ชื่อไฟล์ฟอนต์ไม่ถูกต้อง");
        }

        string destPath = Path.Combine(dir, safeFileName);
        if (!string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
        {
            string tempPath = destPath + ".tmp";
            File.Copy(sourceFilePath, tempPath, overwrite: true);
            File.Move(tempPath, destPath, overwrite: true);
        }

        return (familyName, destPath);
    }

    public static int DeleteCustomFontFamily(string familyName, string? fontsDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(familyName)) return 0;

        string dir = fontsDirectory ?? GetFontsDirectory();
        if (!Directory.Exists(dir)) return 0;

        int deleted = 0;
        foreach (string filePath in Directory.EnumerateFiles(dir).Where(IsSupportedFontExtension).ToList())
        {
            if (TryReadFontFamilyName(filePath, out string? parsedName) &&
                string.Equals(parsedName, familyName, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(filePath);
                deleted++;
            }
        }
        return deleted;
    }

    public static bool TryReadFontFamilyName(string filePath, out string? familyName)
    {
        familyName = null;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return TryReadFontFamilyName(stream, out familyName);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool TryReadFontFamilyName(Stream stream, out string? familyName)
    {
        familyName = null;
        if (stream == null || !stream.CanRead || !stream.CanSeek || stream.Length < 12)
        {
            return false;
        }

        try
        {
            stream.Position = 0;
            using var br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            uint sfntVersion = ReadUInt32BE(br);
            if (sfntVersion != SfntTrueType && sfntVersion != SfntOpenTypeCff && sfntVersion != SfntAppleTrueType)
            {
                return false;
            }

            ushort numTables = ReadUInt16BE(br);
            if (numTables == 0 || numTables > 256 || 12L + numTables * 16L > stream.Length)
            {
                return false;
            }

            br.BaseStream.Seek(6, SeekOrigin.Current); // searchRange, entrySelector, rangeShift

            long nameTableOffset = -1;
            long nameTableLength = 0;
            for (int i = 0; i < numTables; i++)
            {
                uint tag = ReadUInt32BE(br);
                ReadUInt32BE(br); // checkSum
                uint offset = ReadUInt32BE(br);
                uint length = ReadUInt32BE(br);
                if (tag == NameTableTag)
                {
                    nameTableOffset = offset;
                    nameTableLength = length;
                    break;
                }
            }

            if (nameTableOffset < 0 || nameTableLength < 6 || nameTableOffset + 6 > stream.Length)
            {
                return false;
            }

            stream.Position = nameTableOffset;
            ReadUInt16BE(br); // format
            ushort recordCount = ReadUInt16BE(br);
            ushort stringOffset = ReadUInt16BE(br);

            if (recordCount == 0 || recordCount > 4096 || nameTableOffset + 6L + recordCount * 12L > stream.Length)
            {
                return false;
            }

            int bestScore = int.MinValue;
            string? bestName = null;

            for (int i = 0; i < recordCount; i++)
            {
                stream.Position = nameTableOffset + 6L + i * 12L;
                ushort platformId = ReadUInt16BE(br);
                ReadUInt16BE(br); // encodingId
                ushort languageId = ReadUInt16BE(br);
                ushort nameId = ReadUInt16BE(br);
                ushort strLen = ReadUInt16BE(br);
                ushort strOff = ReadUInt16BE(br);

                if ((nameId != 1 && nameId != 16) || strLen == 0 || strLen > 1024)
                {
                    continue;
                }

                long absoluteStrPos = nameTableOffset + stringOffset + strOff;
                if (absoluteStrPos < nameTableOffset || absoluteStrPos + strLen > stream.Length)
                {
                    continue;
                }

                stream.Position = absoluteStrPos;
                byte[] bytes = br.ReadBytes(strLen);
                if (bytes.Length != strLen) continue;

                string? decoded = DecodeNameString(platformId, bytes);
                if (string.IsNullOrWhiteSpace(decoded)) continue;

                int score = (nameId == 16 ? 100 : 50)
                          + (platformId == 3 ? 20 : platformId == 0 ? 15 : platformId == 1 ? 5 : 0)
                          + (languageId == 1033 || languageId == 0 ? 10 : 0);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestName = decoded.Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(bestName)) return false;
            familyName = bestName;
            return true;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }

    private static string? DecodeNameString(ushort platformId, byte[] bytes)
    {
        string raw = platformId switch
        {
            3 or 0 => Encoding.BigEndianUnicode.GetString(bytes),
            1 => Encoding.Latin1.GetString(bytes),
            _ => string.Empty
        };

        string clean = raw.Replace("\0", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(clean)) return null;
        return clean.Any(char.IsControl) ? null : clean;
    }

    private static ushort ReadUInt16BE(BinaryReader br)
    {
        byte b0 = br.ReadByte();
        byte b1 = br.ReadByte();
        return (ushort)((b0 << 8) | b1);
    }

    private static uint ReadUInt32BE(BinaryReader br)
    {
        uint b0 = br.ReadByte();
        uint b1 = br.ReadByte();
        uint b2 = br.ReadByte();
        uint b3 = br.ReadByte();
        return (b0 << 24) | (b1 << 16) | (b2 << 8) | b3;
    }
}
