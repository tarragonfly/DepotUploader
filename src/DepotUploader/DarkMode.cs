using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms.VisualStyles;
using Microsoft.Win32;

namespace DepotUploader;

internal static class DarkMode
{
    private const int FirstWindows11Build = 22000;
    private const int DarkTitleBarAttribute = 20;
    private const string DarkButtonThemeClass = "DarkMode_Explorer::Button";
    private const int PushButtonThemePart = 1;
    private const int CheckBoxThemePart = 3;

    public static readonly bool IsSupported =
        Environment.OSVersion.Version.Build >= FirstWindows11Build && !SystemInformation.HighContrast
        && WindowsCanDrawDarkButtonsAndCheckBoxes();

    public static readonly Color TextColor = Color.White;
    public static readonly Color DisabledTextColor = Color.FromArgb(120, 120, 120);

    private static readonly Color WindowBackColor = Color.FromArgb(32, 32, 32);
    private static readonly Color FieldBackColor = Color.FromArgb(43, 43, 43);
    private static readonly Color GridLineColor = Color.FromArgb(64, 64, 64);

    public static bool IsOn { get; private set; } = IsSupported && WindowsShowsAppsDark();

    public static void ApplyToNewWindow(Form window)
    {
        if (!IsSupported)
        {
            return;
        }

        foreach (Control control in WindowAndControlsIn(window))
        {
            control.HandleCreated += (_, _) => UseWindowsThemeOfCurrentMode(control);
        }

        if (IsOn)
        {
            ShowCurrentModeIn(window);
        }
    }

    public static void SwitchOnOrOff(bool on, Form window)
    {
        IsOn = on;
        ShowCurrentModeIn(window);
    }

    public static void DrawPushButton(Graphics graphics, Rectangle bounds, PushButtonState state)
    {
        new VisualStyleRenderer(DarkButtonThemeClass, PushButtonThemePart, (int)state)
            .DrawBackground(graphics, bounds);
    }

    public static void DrawCheckBox(Graphics graphics, Rectangle bounds, CheckBoxState state)
    {
        new VisualStyleRenderer(DarkButtonThemeClass, CheckBoxThemePart, (int)state)
            .DrawBackground(graphics, bounds);
    }

    private static bool WindowsCanDrawDarkButtonsAndCheckBoxes()
    {
        return VisualStyleRenderer.IsSupported
            && VisualStyleRenderer.IsElementDefined(
                VisualStyleElement.CreateElement(DarkButtonThemeClass, PushButtonThemePart, 0))
            && VisualStyleRenderer.IsElementDefined(
                VisualStyleElement.CreateElement(DarkButtonThemeClass, CheckBoxThemePart, 0));
    }

    private static bool WindowsShowsAppsDark()
    {
        try
        {
            using RegistryKey? personalization =
                Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            return personalization?.GetValue("AppsUseLightTheme") is 0;
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException
            or IOException)
        {
            return false;
        }
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

    private static void ShowCurrentModeIn(Form window)
    {
        window.BackColor = IsOn ? WindowBackColor : Color.Empty;
        window.ForeColor = IsOn ? TextColor : Color.Empty;
        foreach (Control control in WindowAndControlsIn(window))
        {
            switch (control)
            {
                case TextBoxBase { Multiline: true } multilineTextBox:
                    multilineTextBox.BorderStyle = IsOn ? BorderStyle.None : BorderStyle.Fixed3D;
                    ShowCurrentModeInField(control);
                    break;
                case ListBox list:
                    list.BorderStyle = IsOn ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                    ShowCurrentModeInField(control);
                    break;
                case TextBoxBase or ComboBox:
                    ShowCurrentModeInField(control);
                    break;
                case DataGridView grid:
                    ShowCurrentModeIn(grid);
                    break;
            }

            if (control.IsHandleCreated)
            {
                UseWindowsThemeOfCurrentMode(control);
            }
        }

        window.Invalidate(true);
    }

    private static void ShowCurrentModeInField(Control field)
    {
        if (!IsOn)
        {
            field.BackColor = Color.Empty;
            field.ForeColor = Color.Empty;

            return;
        }

        bool hasTheWindowColorInLightMode = field.BackColor == SystemColors.Control;
        field.BackColor = hasTheWindowColorInLightMode ? WindowBackColor : FieldBackColor;
        field.ForeColor = TextColor;
    }

    private static void ShowCurrentModeIn(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = !IsOn;
        grid.ColumnHeadersBorderStyle =
            IsOn ? DataGridViewHeaderBorderStyle.Single : DataGridViewHeaderBorderStyle.Raised;
        grid.BackgroundColor = IsOn ? FieldBackColor : SystemColors.AppWorkspace;
        grid.GridColor = IsOn ? GridLineColor : SystemColors.ControlDark;
        grid.DefaultCellStyle.BackColor = IsOn ? FieldBackColor : SystemColors.Window;
        grid.DefaultCellStyle.ForeColor = IsOn ? TextColor : SystemColors.ControlText;
        grid.ColumnHeadersDefaultCellStyle.BackColor = IsOn ? WindowBackColor : SystemColors.Control;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = IsOn ? TextColor : SystemColors.WindowText;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = IsOn ? WindowBackColor : SystemColors.Highlight;
        grid.AutoResizeColumnHeadersHeight();
        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (column.InheritedAutoSizeMode == DataGridViewAutoSizeColumnMode.AllCells)
            {
                grid.AutoResizeColumn(column.Index);
            }
        }
    }

    private static void UseWindowsThemeOfCurrentMode(Control control)
    {
        switch (control)
        {
            case Form:
                int useDarkTitleBar = IsOn ? 1 : 0;
                DwmSetWindowAttribute(control.Handle, DarkTitleBarAttribute, ref useDarkTitleBar, sizeof(int));
                break;
            case TextBoxBase { Multiline: false } or ComboBox:
                SetWindowTheme(control.Handle, IsOn ? "DarkMode_CFD" : null, null);
                break;
            case TextBoxBase or ListBox or ScrollBar:
                SetWindowTheme(control.Handle, IsOn ? "DarkMode_Explorer" : null, null);
                break;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int valueSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr window, string? themeName, string? idList);
}
