using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MultiSignpost.Config;

public class MultiSignpostConfig
{
    public static int Version = 2;

    public const string FallbackFontName = "sans-serif";

    public static MultiSignpostConfig Current = new MultiSignpostConfig();

    [Range(1, 128)]
    [Description("Maximum height in blocks for a signpost.")]
    public int MaxExtensions = 5;

    [Range(0.1, 8.0)]
    [Description("Minimum allowed signpost scale.")]
    public float MinScale = 0.5f;

    [Range(0.1, 8.0)]
    [Description("Maximum allowed signpost scale. If equal to MinScale, the scale slider is hidden.")]
    public float MaxScale = 3f;

    [Range(0.1, 8.0)]
    [Description("Default scale for newly placed signposts.")]
    public float DefaultScale = 1f;

    [Description("Allowed signpost font families. If only one font is listed, the font dropdown is hidden. Default is sans-serif.")]
    public string[] AllowedFonts = new[]
    {
        "sans-serif",
        "Lora",
        "Almendra",
        "Montserrat"
    };

    public void Sanitize()
    {
        if (MaxExtensions < 1)
        {
            MaxExtensions = 1;
        }

        if (MaxExtensions > 128)
        {
            MaxExtensions = 128;
        }

        MinScale = Clamp(MinScale, 0.1f, 8f);
        MaxScale = Clamp(MaxScale, 0.1f, 8f);

        if (MaxScale < MinScale)
        {
            float temp = MinScale;
            MinScale = MaxScale;
            MaxScale = temp;
        }

        DefaultScale = Clamp(DefaultScale, MinScale, MaxScale);
        AllowedFonts = SanitizeAllowedFonts(AllowedFonts);
    }

    public string GetDefaultFontName()
    {
        return SanitizeAllowedFonts(AllowedFonts)[0];
    }

    public string NormalizeFontName(string fontName)
    {
        return NormalizeFontName(fontName, AllowedFonts);
    }

    public static string NormalizeFontName(string fontName, string[] allowedFonts)
    {
        string[] sanitizedAllowedFonts = SanitizeAllowedFonts(allowedFonts);
        string normalized = (fontName ?? "").Trim();

        foreach (string allowedFont in sanitizedAllowedFonts)
        {
            if (string.Equals(allowedFont, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return allowedFont;
            }
        }

        return sanitizedAllowedFonts[0];
    }

    public static string[] SanitizeAllowedFonts(string[] allowedFonts)
    {
        List<string> result = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (allowedFonts != null)
        {
            foreach (string fontName in allowedFonts)
            {
                string trimmed = (fontName ?? "").Trim();

                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (seen.Add(trimmed))
                {
                    result.Add(trimmed);
                }
            }
        }

        if (result.Count == 0)
        {
            result.Add(FallbackFontName);
        }

        return result.ToArray();
    }

    private static float Clamp(float value, float min, float max)
    {
        return Math.Max(min, Math.Min(max, value));
    }
}