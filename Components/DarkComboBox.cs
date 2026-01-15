using System;
using System.Drawing;
using System.Windows.Forms;

namespace WController.Components;

public class DarkComboBox : ComboBox
{
    private readonly Color DarkBackColor = Color.FromArgb(50, 50, 50);
    private readonly Color DarkForeColor = Color.White;
    private readonly Color BorderColor = Color.FromArgb(70, 70, 70);

    public Func<object, (Image? Icon, string Title)>? ItemMapper { get; set; }

    public DarkComboBox()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        BackColor = DarkBackColor;
        ForeColor = DarkForeColor;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;

        e.DrawBackground();

        object item = Items[e.Index];
        Image? icon = null;
        string text = item.ToString() ?? string.Empty;

        if (ItemMapper != null)
        {
            var mapped = ItemMapper(item);
            icon = mapped.Icon;
            text = mapped.Title;
        }

        Color backColor = (e.State & DrawItemState.Selected) == DrawItemState.Selected
            ? Color.FromArgb(70, 70, 70)
            : DarkBackColor;

        Color foreColor = DarkForeColor;

        using (SolidBrush bgBrush = new SolidBrush(backColor))
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

        int textX = e.Bounds.X + 2;

        if (icon != null)
        {
            int iconSize = e.Bounds.Height - 4;
            Rectangle iconRect = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, iconSize, iconSize);
            e.Graphics.DrawImage(icon, iconRect);
            textX = iconRect.Right + 4;
        }

        Rectangle textRect = new Rectangle(textX, e.Bounds.Y, e.Bounds.Width - textX, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, Font, textRect, foreColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

        e.DrawFocusRectangle();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
    }
}
