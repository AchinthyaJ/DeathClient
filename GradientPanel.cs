namespace OfflineMinecraftLauncher;

internal sealed class GradientPanel : Panel
{
    public Color StartColor { get; set; } = Color.FromArgb(24, 28, 46);
    public Color EndColor { get; set; } = Color.FromArgb(10, 12, 24);
    public float Angle { get; set; } = 25f;

    public GradientPanel()
    {
        DoubleBuffered = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle, StartColor, EndColor, Angle);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}
