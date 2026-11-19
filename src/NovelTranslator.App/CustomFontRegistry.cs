using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using NovelTranslator.Core.Settings;

namespace NovelTranslator.App;

public static class CustomFontRegistry
{
    public static bool TryRegisterFontFile(string filePath, out string? familyName)
    {
        familyName = null;
        if (Application.Current == null || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            if (FontManager.Current.SystemFonts is not FontCollectionBase systemFonts)
            {
                return false;
            }

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (!systemFonts.TryAddGlyphTypeface(stream, out var glyphTypeface) || glyphTypeface == null)
            {
                return false;
            }

            familyName = !string.IsNullOrWhiteSpace(glyphTypeface.TypographicFamilyName)
                ? glyphTypeface.TypographicFamilyName
                : glyphTypeface.FamilyName;
            return !string.IsNullOrWhiteSpace(familyName);
        }
        catch
        {
            return false;
        }
    }

    public static void RegisterAllInstalledFonts(string? fontsDirectory = null)
    {
        if (Application.Current == null) return;

        foreach (var font in CustomFontService.GetCustomFonts(fontsDirectory))
        {
            foreach (string path in font.FilePaths.Where(File.Exists))
            {
                TryRegisterFontFile(path, out _);
            }
        }
    }
}
