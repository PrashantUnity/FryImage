using System;
using System.Collections.Generic;
using System.Text;

namespace PdfEditorApp.Plugins.ImageEditor.Core.Text;

/// <summary>
/// Resolves PostScript font names to family name, weight and slant.
/// Preserves hyphenated vendor and family prefixes (e.g. 'FOT-NewRodinPro-Regular',
/// 'Vendor-FamilyPro-BoldIt') by splitting on the rightmost hyphen (#3878).
/// </summary>
public static class FontResolver
{
    private static readonly HashSet<string> StandardWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        "thin", "hairline", "extralight", "ultralight", "light", "regular",
        "roman", "book", "medium", "semibold", "demibold", "demi",
        "bold", "extrabold", "ultrabold", "heavy", "black"
    };

    public record ResolvedFont(string Family, int Weight, bool Italic);

    public static ResolvedFont GuessFromPostScriptName(string ps)
    {
        if (string.IsNullOrWhiteSpace(ps)) return new ResolvedFont("Arial", 400, false);

        int lastDash = ps.LastIndexOf('-');
        string pro = lastDash >= 0 ? ps[..lastDash] : ps;
        string style = lastDash >= 0 ? ps[(lastDash + 1)..] : "";

        // Keep compound style suffixes like Bold-It or Bold-Italic (#3878)
        string styleLower = style.ToLowerInvariant();
        if ((styleLower is "it" or "italic" or "oblique") && pro.Contains('-'))
        {
            int prevDash = pro.LastIndexOf('-');
            string candidateWeight = pro[(prevDash + 1)..];
            if (StandardWeights.Contains(candidateWeight))
            {
                pro = pro[..prevDash];
                style = ps[(prevDash + 1)..];
            }
        }

        string cleanFamily = pro;
        if (cleanFamily.EndsWith("Pro", StringComparison.OrdinalIgnoreCase))
            cleanFamily = cleanFamily[..^3].TrimEnd();
        else if (cleanFamily.EndsWith("Std", StringComparison.OrdinalIgnoreCase))
            cleanFamily = cleanFamily[..^3].TrimEnd();
        else if (cleanFamily.EndsWith("MT", StringComparison.OrdinalIgnoreCase))
            cleanFamily = cleanFamily[..^2].TrimEnd();
        else if (cleanFamily.EndsWith("PS", StringComparison.OrdinalIgnoreCase))
            cleanFamily = cleanFamily[..^2].TrimEnd();

        // Convert concatenated CamelCase words: e.g. "NewRodin" -> "New Rodin"
        var sb = new StringBuilder();
        for (int i = 0; i < cleanFamily.Length; i++)
        {
            char c = cleanFamily[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(cleanFamily[i - 1]) && cleanFamily[i - 1] != '-' && cleanFamily[i - 1] != ' ')
            {
                sb.Append(' ');
            }
            sb.Append(c);
        }
        string familyName = sb.ToString();
        if (pro.EndsWith("Pro", StringComparison.OrdinalIgnoreCase)) familyName += " Pro";
        else if (pro.EndsWith("Std", StringComparison.OrdinalIgnoreCase)) familyName += " Std";

        string fullStyle = style.ToLowerInvariant();
        bool italic = fullStyle.Contains("it") || fullStyle.Contains("oblique");

        int weight = 400;
        if (fullStyle.Contains("bold") || fullStyle.Contains("black") || fullStyle.Contains("heavy"))
            weight = (fullStyle.Contains("extra") || fullStyle.Contains("ultra") || fullStyle.Contains("black")) ? 900 : 700;
        else if (fullStyle.Contains("semibold") || fullStyle.Contains("demi"))
            weight = 600;
        else if (fullStyle.Contains("medium"))
            weight = 500;
        else if (fullStyle.Contains("light") || fullStyle.Contains("thin") || fullStyle.Contains("hairline"))
            weight = (fullStyle.Contains("extra") || fullStyle.Contains("ultra") || fullStyle.Contains("thin")) ? 100 : 300;

        return new ResolvedFont(familyName, weight, italic);
    }
}
