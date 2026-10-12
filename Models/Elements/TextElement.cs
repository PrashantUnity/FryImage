using Avalonia;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.ImageEditor.Models;

public enum TextCaseTransform
{
    None,
    Uppercase,
    TitleCase,
    Lowercase,
}

/// <summary>
/// A text label element on the canvas.
/// </summary>
public class TextElement : CanvasElement
{
    public override string ElementType => "Text";

    private string _text = "Text";
    private double _fontSize = 24;
    private FontWeight _fontWeight = FontWeight.Normal;
    private FontStyle _fontStyle = FontStyle.Normal;
    private TextCaseTransform _caseTransform = TextCaseTransform.None;
    private bool _isWrapEnabled;

    /// <summary>When true (the default), Width is recomputed from the text measurement
    /// whenever Text/FontSize/FontWeight/FontStyle/TextCaseTransform change. Suppressed
    /// (independently of height) the moment the user manually drags a width-affecting handle.</summary>
    public bool IsWidthAutoFitEnabled { get; set; } = true;

    /// <summary>Same as <see cref="IsWidthAutoFitEnabled"/> but for Height — kept independent
    /// so a manual width resize doesn't block height from still growing to fit wrapped lines.</summary>
    public bool IsHeightAutoFitEnabled { get; set; } = true;

    public string Text
    {
        get => _text;
        set { if (_text == value) return; _text = value; RemeasureIfAutoFit(); }
    }

    public double FontSize
    {
        get => _fontSize;
        set { if (_fontSize == value) return; _fontSize = value; RemeasureIfAutoFit(); }
    }

    public FontFamily FontFamily { get; set; } = new FontFamily("Inter,Segoe UI,Arial,sans-serif");

    public FontWeight FontWeight
    {
        get => _fontWeight;
        set { if (_fontWeight == value) return; _fontWeight = value; RemeasureIfAutoFit(); }
    }

    public FontStyle FontStyle
    {
        get => _fontStyle;
        set { if (_fontStyle == value) return; _fontStyle = value; RemeasureIfAutoFit(); }
    }

    public Color ForegroundColor { get; set; } = Colors.Black;

    /// <summary>Optional filled rect drawn behind the glyphs (Canva-style text highlight). Null = none.</summary>
    public Color? BackgroundColor { get; set; }

    public TextAlignment Alignment { get; set; } = TextAlignment.Left;
    public bool IsUnderline { get; set; }
    public bool IsStrikethrough { get; set; }

    /// <summary>Non-destructive display transform — Text itself is never mutated, so the
    /// original casing survives re-editing (including via the inline double-click editor).</summary>
    public TextCaseTransform TextCaseTransform
    {
        get => _caseTransform;
        set { if (_caseTransform == value) return; _caseTransform = value; RemeasureIfAutoFit(); }
    }

    /// <summary>When true, text wraps at the element's current Width instead of growing
    /// width to fit a single line.</summary>
    public bool IsWrapEnabled
    {
        get => _isWrapEnabled;
        set { if (_isWrapEnabled == value) return; _isWrapEnabled = value; RemeasureIfAutoFit(); }
    }

    /// <summary>0 = natural line height.</summary>
    public double LineHeight { get; set; }

    // OpenType feature flags (Photoshop TYPE-218-5)
    public bool StandardLigatures { get; set; } = true;    // liga (fi)
    public bool ContextualAlternates { get; set; } = true; // calt (st)
    public bool DiscretionaryLigatures { get; set; }        // dlig (ct)
    public bool Swash { get; set; }                         // swsh (A~)
    public bool StylisticAlternates { get; set; }          // salt (aa)
    public bool TitlingAlternates { get; set; }            // titl (T)
    public bool Ordinals { get; set; }                     // ordn (1st)
    public bool Fractions { get; set; }                    // frac (1/2)
    public bool FauxSmallCaps { get; set; }                // smcp (TT)
    public bool FauxSuperscript { get; set; }              // sups (T¹)
    public bool FauxSubscript { get; set; }                // subs (T₁)

    public TextElement()
    {
        Width = 160;
        Height = 40;
    }

    private static string ApplyCaseTransform(string text, TextCaseTransform transform) => transform switch
    {
        TextCaseTransform.Uppercase => text.ToUpperInvariant(),
        TextCaseTransform.Lowercase => text.ToLowerInvariant(),
        TextCaseTransform.TitleCase => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLowerInvariant()),
        _ => text,
    };

    private TextDecorationCollection? BuildTextDecorations()
    {
        if (!IsUnderline && !IsStrikethrough) return null;
        var combined = new TextDecorationCollection();
        if (IsUnderline) combined.AddRange(TextDecorations.Underline);
        if (IsStrikethrough) combined.AddRange(TextDecorations.Strikethrough);
        return combined;
    }

    private void RemeasureIfAutoFit()
    {
        if ((!IsWidthAutoFitEnabled && !IsHeightAutoFitEnabled) || string.IsNullOrEmpty(Text)) return;

        var typeface = new Typeface(FontFamily, FontStyle, FontWeight);
        var displayText = ApplyCaseTransform(Text, TextCaseTransform);
        var ft = new FormattedText(
            displayText,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            Brushes.Transparent);

        if (IsWrapEnabled) ft.MaxTextWidth = Width;
        if (LineHeight > 0) ft.LineHeight = LineHeight;

        if (IsWidthAutoFitEnabled && !IsWrapEnabled) Width = Math.Max(ft.Width + 8, 40);
        if (IsHeightAutoFitEnabled) Height = Math.Max(ft.Height + 4, 20);
    }

    /// <summary>
    /// Non-destructive warp deformation applied to the text glyphs.
    /// </summary>
    public TextWarpSettings Warp { get; set; } = new();

    public override void Draw(DrawingContext dc)
    {
        if (string.IsNullOrEmpty(Text)) return;

        if (BackgroundColor is { } bg)
        {
            dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(X, Y, Width, Height));
        }

        var typeface = new Typeface(FontFamily, FontStyle, FontWeight);
        var displayText = ApplyCaseTransform(Text, TextCaseTransform);
        var ft = new FormattedText(
            displayText,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            new SolidColorBrush(ForegroundColor));

        ft.TextAlignment = Alignment;
        if (IsWrapEnabled) ft.MaxTextWidth = Width;
        if (LineHeight > 0) ft.LineHeight = LineHeight;

        var decorations = BuildTextDecorations();
        if (decorations != null) ft.SetTextDecorations(decorations);

        if (Warp != null && Warp.IsActive)
        {
            DrawWarpedText(dc, ft);
        }
        else
        {
            dc.DrawText(ft, new Point(X + 4, Y + 2));
        }
    }

    private void DrawWarpedText(DrawingContext dc, FormattedText ft)
    {
        var bounds = new Rect(X + 4, Y + 2, Math.Max(20, Width - 8), Math.Max(16, Height - 4));
        int slices = Math.Clamp((int)(bounds.Width / 5.0), 20, 60);

        for (int i = 0; i < slices; i++)
        {
            double u0 = (double)i / slices;
            double u1 = (double)(i + 1) / slices;
            double stripX = bounds.X + u0 * bounds.Width;
            double stripW = (u1 - u0) * bounds.Width;

            var centerPt = new Point(stripX + stripW / 2.0, bounds.Center.Y);
            var topPt = new Point(stripX + stripW / 2.0, bounds.Top);
            var bottomPt = new Point(stripX + stripW / 2.0, bounds.Bottom);

            var warpedCenter = Core.Warp.TextWarpEngine.TransformPoint(centerPt, bounds, Warp);
            var warpedTop = Core.Warp.TextWarpEngine.TransformPoint(topPt, bounds, Warp);
            var warpedBottom = Core.Warp.TextWarpEngine.TransformPoint(bottomPt, bounds, Warp);

            double dx = warpedCenter.X - centerPt.X;
            double dy = warpedCenter.Y - centerPt.Y;
            double stripHeight = Math.Max(1.0, warpedBottom.Y - warpedTop.Y);
            double scaleY = Math.Clamp(stripHeight / bounds.Height, 0.1, 4.0);

            var nextCenter = Core.Warp.TextWarpEngine.TransformPoint(new Point(stripX + stripW + 2, bounds.Center.Y), bounds, Warp);
            var prevCenter = Core.Warp.TextWarpEngine.TransformPoint(new Point(stripX - 2, bounds.Center.Y), bounds, Warp);
            double angleRad = Math.Atan2(nextCenter.Y - prevCenter.Y, nextCenter.X - prevCenter.X);

            var stripTransform = Matrix.CreateTranslation(-centerPt.X, -centerPt.Y)
                                 * Matrix.CreateScale(1.0, scaleY)
                                 * Matrix.CreateRotation(angleRad)
                                 * Matrix.CreateTranslation(centerPt.X + dx, centerPt.Y + dy);

            using (dc.PushTransform(stripTransform))
            using (dc.PushClip(new Rect(stripX - 0.5, bounds.Y - 150, stripW + 1.0, bounds.Height + 300)))
            {
                dc.DrawText(ft, new Point(X + 4, Y + 2));
            }
        }
    }

    public override bool HitTest(Point pt)
    {
        return new Rect(X, Y, Width, Height).Contains(pt);
    }

    public override CanvasElement Clone()
    {
        // Guard auto-fit off first so setting Text/FontSize below (which normally trigger a
        // remeasure) don't clobber the dimensions we're about to restore explicitly.
        var clone = new TextElement { IsWidthAutoFitEnabled = false, IsHeightAutoFitEnabled = false };
        clone.FontFamily = FontFamily;
        clone.Text = Text;
        clone.FontSize = FontSize;
        clone.FontWeight = FontWeight;
        clone.FontStyle = FontStyle;
        clone.TextCaseTransform = TextCaseTransform;

        CopyBasePropertiesTo(clone);
        clone.IsLocked = false;
        clone.GroupId = null;
        clone.ForegroundColor = ForegroundColor; clone.BackgroundColor = BackgroundColor;
        clone.Alignment = Alignment; clone.IsUnderline = IsUnderline; clone.IsStrikethrough = IsStrikethrough;
        clone.IsWrapEnabled = IsWrapEnabled; clone.LineHeight = LineHeight;

        clone.StandardLigatures = StandardLigatures;
        clone.ContextualAlternates = ContextualAlternates;
        clone.DiscretionaryLigatures = DiscretionaryLigatures;
        clone.Swash = Swash;
        clone.StylisticAlternates = StylisticAlternates;
        clone.TitlingAlternates = TitlingAlternates;
        clone.Ordinals = Ordinals;
        clone.Fractions = Fractions;
        clone.FauxSmallCaps = FauxSmallCaps;
        clone.FauxSuperscript = FauxSuperscript;
        clone.FauxSubscript = FauxSubscript;

        clone.IsWidthAutoFitEnabled = IsWidthAutoFitEnabled;
        clone.IsHeightAutoFitEnabled = IsHeightAutoFitEnabled;
        clone.Warp = Warp?.Clone() ?? new TextWarpSettings();
        return clone;
    }
}
