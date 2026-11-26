using System.Runtime.InteropServices;

namespace DepotUploader;

internal static class WindowTheme
{
    private const int DarkTitleBarAttribute = 20;
    private const int DarkTitleBarAttributeBeforeWindows10Version2004 = 19;
    private const int TitleBarColorAttribute = 35;
    private const int TitleBarTextColorAttribute = 36;
    private const int QueryUiStateMessage = 0x0129;
    private const int FocusCuesAreHiddenFlag = 0x1;

    public static readonly bool DrawsControls = !SystemInformation.HighContrast;
    public static readonly int LineHeight = SystemFonts.MessageBoxFont.Height;
    public static readonly int SpaceInsideOutline = LineHeight / 4 + 1;
    public static readonly int ControlHeight = LineHeight + 2 * SpaceInsideOutline;
    public static readonly float CornerRadius = 0.2f * ControlHeight;

    public static bool IsDark { get; private set; } = true;

    public static ThemeColors Colors => IsDark ? ThemeColors.BlenderDark : ThemeColors.BlenderLight;

    public static void ApplyToNewWindow(Form window)
    {
        if (!DrawsControls)
        {
            return;
        }

        foreach (Control control in WindowAndControlsIn(window))
        {
            control.HandleCreated += (_, _) => ShowCurrentColorsInPartsDrawnByWindows(control);
        }

        ShowCurrentColorsIn(window);
    }

    public static void SwitchToDarkOrLight(bool dark, Form window)
    {
        IsDark = dark;
        ShowCurrentColorsIn(window);
    }

    public static bool KeyboardFocusIsShownIn(Control control)
    {
        int uiState = SendMessage(control.Handle, QueryUiStateMessage, IntPtr.Zero, IntPtr.Zero).ToInt32();

        return (uiState & FocusCuesAreHiddenFlag) == 0;
    }

    public static void DrawTooltip(DrawToolTipEventArgs tooltip)
    {
        using (SolidBrush fill = new SolidBrush(Colors.TooltipFill))
        using (Pen outline = new Pen(Colors.TooltipOutline))
        {
            tooltip.Graphics.FillRectangle(fill, tooltip.Bounds);
            tooltip.Graphics.DrawRectangle(outline, tooltip.Bounds.Left, tooltip.Bounds.Top,
                tooltip.Bounds.Width - 1, tooltip.Bounds.Height - 1);
        }

        TextRenderer.DrawText(tooltip.Graphics, tooltip.ToolTipText, tooltip.Font, tooltip.Bounds,
            Colors.TooltipText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private static List<Control> WindowAndControlsIn(Control windowOrParent)
    {
        List<Control> controls = new List<Control> { windowOrParent };
        foreach (Control child in windowOrParent.Controls)
        {
            controls.AddRange(WindowAndControlsIn(child));
        }

        return controls;
    }

    private static void ShowCurrentColorsIn(Form window)
    {
        window.BackColor = Colors.WindowFill;
        window.ForeColor = Colors.WindowText;
        foreach (Control control in WindowAndControlsIn(window))
        {
            switch (control)
            {
                case TextFieldFrame textField:
                    textField.ShowCurrentColors();
                    break;
                case TextBoxBase { Multiline: true } log:
                    log.BackColor = Colors.LogFill;
                    log.ForeColor = Colors.LogText;
                    break;
                case ListBox list:
                    list.BackColor = Colors.ListFill;
                    list.ForeColor = Colors.ListItemText;
                    break;
                case ComboBox dropDown:
                    dropDown.BackColor = Colors.DropDownItemFill;
                    dropDown.ForeColor = Colors.DropDownItemText;
                    break;
                case DataGridView grid:
                    ShowCurrentColorsIn(grid);
                    break;
            }

            if (control.IsHandleCreated)
            {
                ShowCurrentColorsInPartsDrawnByWindows(control);
            }
        }

        window.Invalidate(true);
    }

    private static void ShowCurrentColorsIn(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.None;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.BackgroundColor = Colors.ListFill;
        grid.BackColor = Colors.ListFill;
        grid.DefaultCellStyle.BackColor = Colors.ListFill;
        grid.DefaultCellStyle.ForeColor = Colors.ListItemText;
        grid.DefaultCellStyle.SelectionBackColor = Colors.SelectedFill;
        grid.DefaultCellStyle.SelectionForeColor = Colors.SelectedText;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Colors.ListFill;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Colors.ListHeaderText;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Colors.ListFill;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Colors.ListHeaderText;
        grid.AutoResizeColumnHeadersHeight();
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (column.InheritedAutoSizeMode == DataGridViewAutoSizeColumnMode.AllCells)
            {
                grid.AutoResizeColumn(column.Index);
            }
        }
    }

    private static void ShowCurrentColorsInPartsDrawnByWindows(Control control)
    {
        switch (control)
        {
            case Form:
                ShowCurrentColorsInTitleBar(control.Handle);
                break;
            case ComboBox:
                SetWindowTheme(control.Handle, IsDark ? "DarkMode_CFD" : null, null);
                break;
            case TextBoxBase { Multiline: true } or ListBox or ScrollBar:
                SetWindowTheme(control.Handle, IsDark ? "DarkMode_Explorer" : null, null);
                break;
        }
    }

    private static void ShowCurrentColorsInTitleBar(IntPtr window)
    {
        int useDarkTitleBar = IsDark ? 1 : 0;
        if (DwmSetWindowAttribute(window, DarkTitleBarAttribute, ref useDarkTitleBar, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(window, DarkTitleBarAttributeBeforeWindows10Version2004, ref useDarkTitleBar,
                sizeof(int));
        }

        int titleBarColor = ColorTranslator.ToWin32(Colors.WindowFill);
        DwmSetWindowAttribute(window, TitleBarColorAttribute, ref titleBarColor, sizeof(int));
        int titleBarTextColor = ColorTranslator.ToWin32(Colors.WindowText);
        DwmSetWindowAttribute(window, TitleBarTextColorAttribute, ref titleBarTextColor, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr window, string? themeName, string? idList);
}
