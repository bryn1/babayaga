using BabaYaga.Sim;
using Godot;

/// <summary>
/// Grid space ↔ screen space for the 2:1 iso map. CELL = 128×64 px (art is
/// 256×128 drawn at scale 0.5 — the TILE SCALE CONTRACT, §3); any range/range-
/// check math calls WorldPos.DistanceTo — Chebyshev, the Sim's single distance
/// metric, reused and never re-implemented. Drag-clamped to the area's W×H.
/// MC 12144 (owner report: "the camera fixed on the hero ... a lot more zoomed
/// in than diablo 2"): ONE Camera2D zoom property scales ground+entities
/// together — ZOOM LEVEL DERIVATION: classic D2 tiles are 32×16 px, so its
/// 640-wide viewport shows 640/32 = 20 tiles across; ours are TileW=128 world
/// px and a Godot zoom z shows a viewport×z world window, so cells across =
/// 1280·z/128 = 10z — the current z=1 is the owner's ~10-across frame. True
/// D2 density needs z≈2–4, but our areas are 16×16 cells (extents 1920×960
/// world px): at z≥1.5 the whole map fits in the window — a postage stamp
/// with void margins, and the follow has nothing left to scroll. The HONEST
/// value for this map size: the largest zoom-out whose window (1280z×720z)
/// still fits INSIDE the area extents, so the floor fills the screen under
/// the lock-on camera → z ≤ min(1920/1280, 960/720) = 1.33; z = 1.25 keeps
/// margin (window 1600×900) and yields ≥20 floor cells across the viewport
/// scan-line (CamLeg ZOOM-COUNT, measured from GridToScreen bounds).
/// </summary>
public partial class IsoCamera : Camera2D
{
    public const int TileW = 128;
    public const int TileH = 64;

    /// <summary>MC 12144 ZOOM — the ONE scale: ground + entities ride it
    /// together (derivation in the header; never resize sprites per-node).</summary>
    public const float ZoomLevel = 1.25f;

    // MC 12144: the area extents stashed at build (FocusOn) — FOLLOW clamps to
    // the SAME extents the arrival framing uses, one mechanism, never a second.
    private int _extentW = 1, _extentH = 1;

    public override void _Ready() => Zoom = Vector2.One * ZoomLevel;

    /// <summary>Grid cell → world px: sx=(x−y)·TILE_W/2, sy=(x+y)·TILE_H/2.</summary>
    public static Vector2 GridToScreen(Vector2I c) =>
        new((c.X - c.Y) * (TileW / 2f), (c.X + c.Y) * (TileH / 2f));

    /// <summary>World px → the cell that contains it (inverse of GridToScreen).
    /// M5-S4: the math lives in ClickMap.ScreenToCell (the xunit-pinned home);
    /// this DELEGATES — one implementation per concern, same behaviour.</summary>
    public static Vector2I ScreenToGrid(Vector2 p)
    {
        var (x, y) = ClickMap.ScreenToCell(p.X, p.Y, TileW, TileH);
        return new Vector2I(x, y);
    }

    /// <summary>The ONE metric: reuse of the Sim's Chebyshev distance — no second one.</summary>
    public static bool InRange(WorldPos a, WorldPos b, int cells) => a.DistanceTo(b) <= cells;

    public void FocusOn(Vector2I cell, int w, int h)
    {
        _extentW = w; _extentH = h; // stash for Follow — the SAME extents, one clamp source
        Position = ClampToGrid(GridToScreen(cell), w, h);
        MakeCurrent();
    }

    /// <summary>MC 12144 D2 LOCK-ON; MC 12149 consumes the hero's INTERPOLATED
    /// visual world px every frame (Act pulls) so the camera GLIDES with the
    /// hero — dead-center except where the area extents clamp it (the extents
    /// FocusOn stashed at build; no second extent math, the hero's committed
    /// cell stays the only sim-side position).</summary>
    public void Follow(Vector2 heroWorldPos) => Position = ClampToGrid(heroWorldPos, _extentW, _extentH);

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
