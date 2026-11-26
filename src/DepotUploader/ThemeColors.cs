namespace DepotUploader;

internal sealed class ThemeColors
{
    public static readonly Color SuccessFill = FromRgb(0x188625);
    public static readonly Color FailureFill = FromRgb(0x771111);
    public static readonly Color WarningFill = FromRgb(0xac8737);
    public static readonly Color IconOnStatusFill = Color.White;

    public const double LowestTextContrast = 4.5;

    public static readonly ThemeColors BlenderDark = new ThemeColors
    {
        WindowFill = FromRgb(0x303030),
        WindowText = FromRgb(0xe6e6e6),
        Outline = FromRgb(0x3d3d3d),
        ButtonFill = FromRgb(0x545454),
        ButtonText = FromRgb(0xe6e6e6),
        SelectedFill = FromRgb(0x4772b3),
        SelectedText = FromRgb(0xffffff),
        TextFieldFill = FromRgb(0x1d1d1d),
        TextFieldText = FromRgb(0xe6e6e6),
        EditedTextFieldFill = FromRgb(0x181818),
        CheckBoxFill = FromRgb(0x545454),
        CheckMark = FromRgb(0xffffff),
        DropDownFill = FromRgb(0x282828),
        OpenDropDownFill = FromRgb(0x3e5c89),
        DropDownText = FromRgb(0xe6e6e6),
        DropDownArrow = FromRgb(0xd9d9d9),
        DropDownItemFill = FromRgb(0x181818),
        DropDownItemText = FromRgb(0xdddddd),
        ListFill = FromRgb(0x262626),
        ListItemText = FromRgb(0xcccccc),
        ListHeaderText = FromRgb(0x989898),
        LogFill = FromRgb(0x1d1d1d),
        LogText = FromRgb(0xc3c3c3),
        TooltipFill = FromRgb(0x1d1d1d),
        TooltipOutline = FromRgb(0x242424),
        TooltipText = FromRgb(0xd9d9d9),
        FolderIcon = FromRgb(0xe3c16e),
    };

    public static readonly ThemeColors BlenderLight = new ThemeColors
    {
        WindowFill = FromRgb(0xb3b3b3),
        WindowText = FromRgb(0x1a1a1a),
        Outline = FromRgb(0x4d4d4d),
        ButtonFill = FromRgb(0xdbdbdb),
        ButtonText = FromRgb(0x1a1a1a),
        SelectedFill = FromRgb(0x4772b3),
        SelectedText = FromRgb(0xffffff),
        TextFieldFill = FromRgb(0x282828),
        TextFieldText = FromRgb(0xdddddd),
        EditedTextFieldFill = FromRgb(0x333333),
        CheckBoxFill = FromRgb(0x3c3c3c),
        CheckMark = FromRgb(0xffffff),
        DropDownFill = FromRgb(0x3b3b3b),
        OpenDropDownFill = FromRgb(0x595959),
        DropDownText = FromRgb(0xd9d9d9),
        DropDownArrow = FromRgb(0x999999),
        DropDownItemFill = FromRgb(0xc0c0c0),
        DropDownItemText = FromRgb(0x1a1a1a),
        ListFill = FromRgb(0x999999),
        ListItemText = FromRgb(0x1a1a1a),
        ListHeaderText = FromRgb(0x262626),
        LogFill = FromRgb(0x808080),
        LogText = FromRgb(0x000000),
        TooltipFill = FromRgb(0x19191a),
        TooltipOutline = FromRgb(0x19191a),
        TooltipText = FromRgb(0xe6e6e6),
        FolderIcon = FromRgb(0x8a6a1c),
    };

    public Color WindowFill { get; private init; }
    public Color WindowText { get; private init; }
    public Color Outline { get; private init; }
    public Color ButtonFill { get; private init; }
    public Color ButtonText { get; private init; }
    public Color SelectedFill { get; private init; }
    public Color SelectedText { get; private init; }
    public Color TextFieldFill { get; private init; }
    public Color TextFieldText { get; private init; }
    public Color EditedTextFieldFill { get; private init; }
    public Color CheckBoxFill { get; private init; }
    public Color CheckMark { get; private init; }
    public Color DropDownFill { get; private init; }
    public Color OpenDropDownFill { get; private init; }
    public Color DropDownText { get; private init; }
    public Color DropDownArrow { get; private init; }
    public Color DropDownItemFill { get; private init; }
    public Color DropDownItemText { get; private init; }
    public Color ListFill { get; private init; }
    public Color ListItemText { get; private init; }
    public Color ListHeaderText { get; private init; }
    public Color LogFill { get; private init; }
    public Color LogText { get; private init; }
    public Color TooltipFill { get; private init; }
    public Color TooltipOutline { get; private init; }
    public Color TooltipText { get; private init; }
    public Color FolderIcon { get; private init; }

    public Color ReadOnlyTextFieldFill => Mixed(TextFieldFill, WindowFill, 0.75);

    public static Color Mixed(Color first, Color second, double shareOfFirst)
    {
        double shareOfSecond = 1 - shareOfFirst;

        return Color.FromArgb(
            (int)Math.Round(first.R * shareOfFirst + second.R * shareOfSecond),
            (int)Math.Round(first.G * shareOfFirst + second.G * shareOfSecond),
            (int)Math.Round(first.B * shareOfFirst + second.B * shareOfSecond));
    }

    public static Color OfDisabledControl(Color color, Color background)
    {
        return Mixed(color, background, 0.5);
    }

    public static Color FillUnderMouse(Color fill, Color textOnFill)
    {
        double brightening = textOnFill.GetBrightness() > fill.GetBrightness() ? 1.2 : 1.1;
        Color brighterFill = Scaled(fill, brightening);

        return Contrast(brighterFill, textOnFill) >= LowestTextContrast ? brighterFill : Scaled(fill, 1 / brightening);
    }

    public static double Contrast(Color first, Color second)
    {
        double lighter = Math.Max(RelativeLuminance(first), RelativeLuminance(second));
        double darker = Math.Min(RelativeLuminance(first), RelativeLuminance(second));

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static Color Scaled(Color color, double factor)
    {
        return Color.FromArgb(
            Math.Min(255, (int)Math.Round(color.R * factor)),
            Math.Min(255, (int)Math.Round(color.G * factor)),
            Math.Min(255, (int)Math.Round(color.B * factor)));
    }

    private static double RelativeLuminance(Color color)
    {
        return 0.2126 * LinearLight(color.R) + 0.7152 * LinearLight(color.G) + 0.0722 * LinearLight(color.B);
    }

    private static double LinearLight(byte channel)
    {
        double share = channel / 255.0;

        return share <= 0.04045 ? share / 12.92 : Math.Pow((share + 0.055) / 1.055, 2.4);
    }

    private static Color FromRgb(int rgb)
    {
        return Color.FromArgb(255, Color.FromArgb(rgb));
    }
}
