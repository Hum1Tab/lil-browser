namespace PxBrowser;

internal static class Theme
{
    public static bool IsLight { get; private set; }
    public static Color Ink { get; private set; } = Color.FromArgb(23, 24, 23);
    public static Color InkSoft { get; private set; } = Color.FromArgb(35, 37, 34);
    public static Color Cream { get; private set; } = Color.FromArgb(247, 241, 231);
    public static Color Gold { get; private set; } = Color.FromArgb(211, 170, 103);
    public static Color Muted { get; private set; } = Color.FromArgb(164, 163, 151);
    public static Color Line { get; private set; } = Color.FromArgb(42, 45, 41);

    public static void Apply(string? theme)
    {
        IsLight = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (IsLight)
        {
            Ink = Color.FromArgb(247, 241, 231);
            InkSoft = Color.FromArgb(236, 229, 215);
            Cream = Color.FromArgb(23, 24, 23);
            Gold = Color.FromArgb(170, 124, 56);
            Muted = Color.FromArgb(111, 106, 94);
            Line = Color.FromArgb(216, 208, 194);
        }
        else
        {
            Ink = Color.FromArgb(23, 24, 23);
            InkSoft = Color.FromArgb(35, 37, 34);
            Cream = Color.FromArgb(247, 241, 231);
            Gold = Color.FromArgb(211, 170, 103);
            Muted = Color.FromArgb(164, 163, 151);
            Line = Color.FromArgb(42, 45, 41);
        }
    }
}
