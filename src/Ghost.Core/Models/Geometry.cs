namespace Ghost.Core.Models;

/// <summary>
/// A single point in physical-pixel virtual-desktop space. See docs/coordinate-system.md.
/// </summary>
public readonly record struct PhysicalPoint(int X, int Y);

/// <summary>
/// A rectangle in physical-pixel virtual-desktop space. This is the canonical geometry type
/// used everywhere inside Ghost.Core; it is never converted to DIPs outside Ghost.App.
/// </summary>
public readonly record struct PhysicalRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
    public PhysicalPoint Center => new(Left + Width / 2, Top + Height / 2);
    public int Area => Width * Height;
    public bool IsDegenerate => Width <= 0 || Height <= 0;

    public bool Contains(PhysicalPoint p, int tolerance = 0) =>
        p.X >= Left - tolerance && p.X <= Right + tolerance &&
        p.Y >= Top - tolerance && p.Y <= Bottom + tolerance;

    /// <summary>Euclidean distance from the point to the nearest edge; 0 if the point is inside.</summary>
    public int DistanceTo(PhysicalPoint p)
    {
        if (Contains(p))
        {
            return 0;
        }

        var dx = Math.Max(Math.Max(Left - p.X, 0), p.X - Right);
        var dy = Math.Max(Math.Max(Top - p.Y, 0), p.Y - Bottom);
        return (int)Math.Round(Math.Sqrt((double)dx * dx + (double)dy * dy));
    }
}
