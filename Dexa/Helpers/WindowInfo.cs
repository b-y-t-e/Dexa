namespace Dexa;

/// Przechowuje wszystkie parametry okna, które chcemy odczytać lub ustawić.
/// </summary>
public class WindowInfo
{
    public Rectangle Bounds { get; set; }

    public double Height => Bounds.Height;
    public double Width => Bounds.Width;

    public FormWindowState State { get; set; }
}
