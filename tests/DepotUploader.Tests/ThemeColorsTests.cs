namespace DepotUploader.Tests;

public class ThemeColorsTests
{
    private const double LowestIconContrast = 3;

    [TestCase("Blender Dark")]
    [TestCase("Blender Light")]
    public void EveryTextColor_HasTheLowestTextContrastOnItsFill(string themeName)
    {
        ThemeColors colors = ThemeNamed(themeName);
        (string Name, Color Text, Color Fill)[] textOnFill =
        {
            ("label on the window", colors.WindowText, colors.WindowFill),
            ("button", colors.ButtonText, colors.ButtonFill),
            ("confirm button, selected list row and selected drop-down item", colors.SelectedText, colors.SelectedFill),
            ("text field", colors.TextFieldText, colors.TextFieldFill),
            ("text field with the focus", colors.TextFieldText, colors.EditedTextFieldFill),
            ("read-only text field", colors.TextFieldText, colors.ReadOnlyTextFieldFill),
            ("drop-down list", colors.DropDownText, colors.DropDownFill),
            ("open drop-down list", colors.DropDownText, colors.OpenDropDownFill),
            ("drop-down item", colors.DropDownItemText, colors.DropDownItemFill),
            ("list item", colors.ListItemText, colors.ListFill),
            ("table header", colors.ListHeaderText, colors.ListFill),
            ("log", colors.LogText, colors.LogFill),
            ("tooltip", colors.TooltipText, colors.TooltipFill),
        };

        Assert.Multiple(() =>
        {
            foreach ((string name, Color text, Color fill) in textOnFill)
            {
                Assert.That(ThemeColors.Contrast(text, fill), Is.GreaterThanOrEqualTo(ThemeColors.LowestTextContrast),
                    name);
                Assert.That(ThemeColors.Contrast(text, ThemeColors.FillUnderMouse(fill, text)),
                    Is.GreaterThanOrEqualTo(ThemeColors.LowestTextContrast), $"{name} under the mouse");
            }
        });
    }

    [TestCase("Blender Dark")]
    [TestCase("Blender Light")]
    public void EveryIconColor_HasTheLowestIconContrastOnItsFill(string themeName)
    {
        ThemeColors colors = ThemeNamed(themeName);
        (string Name, Color Icon, Color Fill)[] iconOnFill =
        {
            ("folder icon on a depot row button", colors.FolderIcon, colors.ButtonFill),
            ("folder icon on a depot row button under the mouse", colors.FolderIcon,
                ThemeColors.FillUnderMouse(colors.ButtonFill, colors.ButtonText)),
            ("arrow of the drop-down list", colors.DropDownArrow, colors.DropDownFill),
            ("arrow of the drop-down list under the mouse", colors.DropDownArrow,
                ThemeColors.FillUnderMouse(colors.DropDownFill, colors.DropDownText)),
            ("check mark", colors.CheckMark, colors.SelectedFill),
            ("check mark under the mouse", colors.CheckMark,
                ThemeColors.FillUnderMouse(colors.SelectedFill, colors.CheckMark)),
            ("icon on the success fill", ThemeColors.IconOnStatusFill, ThemeColors.SuccessFill),
            ("icon on the failure fill", ThemeColors.IconOnStatusFill, ThemeColors.FailureFill),
            ("icon on the warning fill", ThemeColors.IconOnStatusFill, ThemeColors.WarningFill),
        };

        Assert.Multiple(() =>
        {
            foreach ((string name, Color icon, Color fill) in iconOnFill)
            {
                Assert.That(ThemeColors.Contrast(icon, fill), Is.GreaterThanOrEqualTo(LowestIconContrast), name);
            }
        });
    }

    [Test]
    public void Contrast_OfBlackAndWhiteIsTwentyOne()
    {
        Assert.That(ThemeColors.Contrast(Color.Black, Color.White), Is.EqualTo(21).Within(0.001));
    }

    [Test]
    public void FillUnderMouse_DarkensTheFillWhenABrighterOneWouldLowerTheTextContrastBelowTheLowest()
    {
        Color selectedFill = ThemeColors.BlenderDark.SelectedFill;

        Color fillUnderMouse = ThemeColors.FillUnderMouse(selectedFill, ThemeColors.BlenderDark.SelectedText);

        Assert.That(fillUnderMouse.GetBrightness(), Is.LessThan(selectedFill.GetBrightness()));
    }

    private static ThemeColors ThemeNamed(string themeName)
    {
        return themeName == "Blender Dark" ? ThemeColors.BlenderDark : ThemeColors.BlenderLight;
    }
}
