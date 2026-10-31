using BabaYaga.Sim;
using Godot;

/// <summary>
/// Grid space ↔ screen space for the 2:1 iso map. CELL = 128×64 px (art is
/// 256×128 drawn at scale 0.5 — the TILE SCALE CONTRACT, §3); any range/range-
/// check math calls WorldPos.DistanceTo — Chebyshev, the Sim's single distance
/// metric, reused and never re-implemented. Drag-clamped to the area's W×H.
/// </summary>
public partial class IsoCamera : Camera2D
{
    public const int TileW = 128;
    public const int TileH = 64;

    /// <summary>Grid cell → world px: sx=(x−y)·TILE_W/2, sy=(x+y)·TILE_H/2.</summary>
    public static Vector2 GridToScreen(Vector2I c) =>
        new((c.X - c.Y) * (TileW / 2f), (c.X + c.Y) * (TileH / 2f));

    /// <summary>World px → the cell that contains it (inverse of GridToScreen).</summary>
    public static Vector2I ScreenToGrid(Vector2 p)
    {
        float a = p.X / (TileW / 2f), b = p.Y / (TileH / 2f);
        return new Vector2I(Mathf.RoundToInt((a + b) * 0.5f), Mathf.RoundToInt((b - a) * 0.5f));
    }

    /// <summary>The ONE metric: reuse of the Sim's Chebyshev distance — no second one.</summary>
    public static bool InRange(WorldPos a, WorldPos b, int cells) => a.DistanceTo(b) <= cells;

    public void FocusOn(Vector2I cell, int w, int h)
    {
        Position = ClampToGrid(GridToScreen(cell), w, h);
        MakeCurrent();
    }

    /// <summary>Clamp a world point into the parallelogram the W×H grid spans.</summary>
    public static Vector2 ClampToGrid(Vector2 p, int w, int h)
    {
        float minX = -(h - 1) * (TileW / 2f);
        float maxX = (w - 1) * (TileW / 2f);
        float minY = 0f;
        float maxY = (w + h - 2) * (TileH / 2f);
        return new Vector2(Mathf.Clamp(p.X, minX, maxX), Mathf.Clamp(p.Y, minY, maxY));
    }
}
