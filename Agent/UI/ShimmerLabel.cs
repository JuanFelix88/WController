using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WController.Agent.UI;

public class ShimmerLabel : Control
{
    private Timer shimmerTimer;
    private float shimmerOffset;
    private bool isShimmering;
    private string shimmerText = "Thinking...";

    private static readonly Color BaseColor = Color.FromArgb(100, 100, 100);
    private static readonly Color ShimmerColor = Color.FromArgb(180, 180, 180);

    public string ShimmerText
    {
        get => shimmerText;
        set { shimmerText = value; Invalidate(); }
    }

    public bool IsShimmering
    {
        get => isShimmering;
        set
        {
            isShimmering = value;
            shimmerTimer.Enabled = value;
            shimmerOffset = 0;
            Visible = value;
            Invalidate();
        }
    }

    public ShimmerLabel()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Font = new Font("Segoe UI", 10f, FontStyle.Italic);
        Height = 24;

        shimmerTimer = new Timer { Interval = 40 };
        shimmerTimer.Tick += (s, e) =>
        {
            shimmerOffset += 0.03f;
            if (shimmerOffset > 2f) shimmerOffset = -1f;
            Invalidate();
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (string.IsNullOrEmpty(shimmerText)) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var textSize = g.MeasureString(shimmerText, Font);
        float totalWidth = textSize.Width + 40;

        float gradientStart = shimmerOffset * totalWidth - totalWidth * 0.3f;
        float gradientEnd = gradientStart + totalWidth * 0.4f;

        using (var brush = new LinearGradientBrush(
            new PointF(gradientStart, 0),
            new PointF(gradientEnd, 0),
            BaseColor,
            ShimmerColor))
        {
            var blend = new ColorBlend(3);
            blend.Colors = new[] { BaseColor, ShimmerColor, BaseColor };
            blend.Positions = new[] { 0f, 0.5f, 1f };
            brush.InterpolationColors = blend;

            g.DrawString(shimmerText, Font, brush, 4, (Height - textSize.Height) / 2);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            shimmerTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
