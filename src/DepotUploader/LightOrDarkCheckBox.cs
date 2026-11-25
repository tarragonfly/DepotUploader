using System.Windows.Forms.VisualStyles;

namespace DepotUploader;

internal sealed class LightOrDarkCheckBox : CheckBox
{
    protected override void OnPaint(PaintEventArgs paint)
    {
        if (!DarkMode.IsOn)
        {
            base.OnPaint(paint);

            return;
        }

        Graphics graphics = paint.Graphics;
        graphics.Clear(BackColor);
        Size boxSize = CheckBoxRenderer.GetGlyphSize(graphics, CheckBoxState.UncheckedNormal);
        Rectangle boxBounds = new Rectangle(0, (ClientSize.Height - boxSize.Height) / 2, boxSize.Width,
            boxSize.Height);
        DarkMode.DrawCheckBox(graphics, boxBounds, BoxState());
        Rectangle textBounds = Rectangle.FromLTRB(boxBounds.Right + boxSize.Width / 4, 0, ClientSize.Width,
            ClientSize.Height);
        TextRenderer.DrawText(graphics, Text, Font, textBounds,
            Enabled ? DarkMode.TextColor : DarkMode.DisabledTextColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(graphics, textBounds, DarkMode.TextColor, BackColor);
        }
    }

    private CheckBoxState BoxState()
    {
        if (!Enabled)
        {
            return Checked ? CheckBoxState.CheckedDisabled : CheckBoxState.UncheckedDisabled;
        }

        if (ClientRectangle.Contains(PointToClient(MousePosition)))
        {
            return Checked ? CheckBoxState.CheckedHot : CheckBoxState.UncheckedHot;
        }

        return Checked ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal;
    }
}
