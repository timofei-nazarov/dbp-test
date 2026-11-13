using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace TaskManager.Web.Tests;

public sealed class StylesheetTests
{
    private static readonly string Css = ReadStylesheet();

    private static string ReadStylesheet()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "TaskManager.Web", "wwwroot", "board.css");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("board.css not found");
    }

    private static string RootBlock() => Regex.Match(Css, @":root\s*\{(?<body>.*?)\n\}", RegexOptions.Singleline).Groups["body"].Value;

    private static string WithoutRoot() => Css.Replace(RootBlock(), "");

    private static string Token(string name)
    {
        var match = Regex.Match(RootBlock(), @"--" + Regex.Escape(name) + @":\s*(?<value>[^;]+);");
        Assert.True(match.Success, $"token --{name} not found");
        return match.Groups["value"].Value.Trim();
    }

    private static double Luminance(string hex)
    {
        double Channel(int offset)
        {
            var value = int.Parse(hex.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        var start = hex.StartsWith('#') ? 1 : 0;
        return 0.2126 * Channel(start) + 0.7152 * Channel(start + 2) + 0.0722 * Channel(start + 4);
    }

    private static double Contrast(string foreground, string background)
    {
        var first = Luminance(Resolve(foreground));
        var second = Luminance(Resolve(background));
        return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
    }

    private static string Resolve(string token)
    {
        var value = Token(token);
        var reference = Regex.Match(value, @"^var\(--(?<name>[a-z-]+)\)$");
        return reference.Success ? Resolve(reference.Groups["name"].Value) : value;
    }

    private static string Rule(string selector)
    {
        var match = Regex.Match(Css, Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}");
        Assert.True(match.Success, $"rule {selector} not found");
        return match.Groups["body"].Value;
    }

    [Fact]
    public void Stylesheet_HasNoColorLiteralsOutsideRoot()
    {
        var rest = WithoutRoot();

        Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b", rest);
        Assert.DoesNotMatch(@"\b(rgb|rgba|hsl|hsla)\(", rest);
        Assert.DoesNotMatch(@"(?i)(color|background|border[a-z-]*|outline[a-z-]*|fill|stroke)\s*:[^;{}]*\b(white|black|red|green|blue|gray|grey|yellow|orange|purple)\b", rest);
    }

    [Theory]
    [InlineData("color-page", "#F6F7F9")]
    [InlineData("color-card", "#FFFFFF")]
    [InlineData("color-column", "#ECEEF3")]
    [InlineData("color-border", "#E5E7EB")]
    [InlineData("color-text", "#111827")]
    [InlineData("color-text-secondary", "#4B5563")]
    [InlineData("color-text-muted", "#6B7280")]
    [InlineData("color-accent", "#4F46E5")]
    [InlineData("color-accent-soft", "#EEF2FF")]
    [InlineData("color-accent-ring", "#C7D2FE")]
    [InlineData("color-danger", "#DC2626")]
    [InlineData("color-danger-text", "#B91C1C")]
    [InlineData("color-danger-soft", "#FEE2E2")]
    [InlineData("color-danger-softer", "#FEF2F2")]
    [InlineData("color-count", "#DDE1E8")]
    [InlineData("color-scroll-thumb", "#A5B4FC")]
    [InlineData("color-accent-strong", "#4338CA")]
    [InlineData("font-size-xs", "0.75rem")]
    [InlineData("font-size-meta", "0.8125rem")]
    [InlineData("font-size-avatar", "0.625rem")]
    [InlineData("size-dialog", "380px")]
    [InlineData("status-new", "#9CA3AF")]
    [InlineData("status-in-development", "#3B82F6")]
    [InlineData("status-in-qa", "#F59E0B")]
    [InlineData("status-in-code-review", "#8B5CF6")]
    [InlineData("status-ready-for-release", "#14B8A6")]
    [InlineData("status-released", "#22C55E")]
    [InlineData("status-archived", "#6B7280")]
    [InlineData("radius-card", "10px")]
    [InlineData("radius-column", "14px")]
    [InlineData("radius-control", "10px")]
    [InlineData("radius-dialog", "18px")]
    [InlineData("radius-pill", "999px")]
    public void Stylesheet_RootDeclaresTaskTokens(string name, string value)
    {
        Assert.Equal(value, Token(name), ignoreCase: true);
    }

    [Theory]
    [InlineData(0, "#E0E7FF", "#3730A3")]
    [InlineData(1, "#DBEAFE", "#1E40AF")]
    [InlineData(2, "#FEF3C7", "#92400E")]
    [InlineData(3, "#EDE9FE", "#5B21B6")]
    [InlineData(4, "#CCFBF1", "#115E59")]
    [InlineData(5, "#DCFCE7", "#166534")]
    [InlineData(6, "#FCE7F3", "#9D174D")]
    [InlineData(7, "#FFEDD5", "#9A3412")]
    public void Stylesheet_AvatarPaletteHasEightPairsWithContrast(int index, string background, string foreground)
    {
        Assert.Equal(background, Token($"avatar-{index}-bg"), ignoreCase: true);
        Assert.Equal(foreground, Token($"avatar-{index}-fg"), ignoreCase: true);
        Assert.True(Contrast($"avatar-{index}-fg", $"avatar-{index}-bg") >= 4.5);
        var rule = Rule($".avatar-tone-{index}");
        Assert.Contains($"background: var(--avatar-{index}-bg);", rule);
        Assert.Contains($"color: var(--avatar-{index}-fg);", rule);
    }

    [Theory]
    [InlineData("color-text", "color-page", 4.5)]
    [InlineData("color-accent-strong", "color-accent-soft", 4.5)]
    [InlineData("color-text-secondary", "color-count", 4.5)]
    [InlineData("color-on-accent", "color-danger", 4.5)]
    [InlineData("color-control-border", "color-page", 3)]
    [InlineData("color-text", "color-card", 4.5)]
    [InlineData("color-text", "color-column", 4.5)]
    [InlineData("color-text-secondary", "color-page", 4.5)]
    [InlineData("color-text-secondary", "color-card", 4.5)]
    [InlineData("color-text-secondary", "color-column", 4.5)]
    [InlineData("color-text-muted", "color-card", 4.5)]
    [InlineData("color-text-muted", "color-page", 4.5)]
    [InlineData("color-on-accent", "color-accent", 4.5)]
    [InlineData("color-accent", "color-accent-soft", 4.5)]
    [InlineData("color-danger-text", "color-danger-soft", 4.5)]
    [InlineData("color-danger-text", "color-danger-softer", 4.5)]
    [InlineData("color-danger-text", "color-card", 4.5)]
    [InlineData("color-accent", "color-card", 3)]
    [InlineData("color-danger", "color-card", 3)]
    [InlineData("color-control-border", "color-card", 3)]
    public void Stylesheet_TokenPairsMeetContrast(string foreground, string background, double minimum)
    {
        Assert.True(Contrast(foreground, background) >= minimum, $"{foreground} on {background} is {Contrast(foreground, background):0.00}:1");
    }

    [Fact]
    public void Stylesheet_FocusRingIsSolidAccentWithOffset()
    {
        var rule = Rule(":focus-visible");

        Assert.Contains("outline: 2px solid var(--color-accent);", rule);
        Assert.Contains("outline-offset: 2px;", rule);
        Assert.DoesNotContain("dashed", rule);
        Assert.DoesNotContain("outline: none", Css);
        Assert.DoesNotContain("outline-color", Css);
    }

    [Fact]
    public void Stylesheet_ErrorStateDoesNotTouchOutline()
    {
        var rule = Rule(@".field input[aria-invalid=""true""]");

        Assert.Contains("border-color:", rule);
        Assert.DoesNotContain("outline", rule);
    }

    [Fact]
    public void Stylesheet_HasBreakpointsAt640And1024()
    {
        Assert.Contains("@media (max-width: 639.98px)", Css);
        Assert.Contains("@media (max-width: 1023.98px)", Css);
    }

    [Fact]
    public void Stylesheet_HasReducedMotionBlock()
    {
        Assert.Contains("@media (prefers-reduced-motion: reduce)", Css);
    }

    [Fact]
    public void Stylesheet_ReducedMotionDisablesSkeletonAndSheetAnimation()
    {
        var start = Css.IndexOf("@media (prefers-reduced-motion: reduce)", StringComparison.Ordinal);
        var block = Css[start..];

        Assert.Matches(@"\.skeleton,\s*\.sheet\s*\{\s*animation:\s*none;", block);
    }

    [Fact]
    public void Stylesheet_HasNoWebfontOrImport()
    {
        Assert.DoesNotContain("@font-face", Css);
        Assert.DoesNotContain("@import", Css);
        Assert.DoesNotContain("url(", Css);
    }
}
