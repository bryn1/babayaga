namespace BabaYaga.Sim;

/// <summary>
/// The ONE 256ths fixed-point accumulator mechanism (audit MC 11907 D-1): the
/// two regen paths — the mana pool (GameSession.Regen) and the mercenary life
/// pool (GameSession.Hireling) — were line-for-line isomorphic loops (the
/// max256/pool256/fixpoint-break/`+=Math.Min`/`+= /256`/`%=256` shape); only
/// the per-frame quantum formula and the pool they drain differed. This is
/// that shared shape: add the per-frame quantum (clamped to the missing pool)
/// into the caller's sub-point 256ths, drain whole units out, and stop at the
/// full-pool fixpoint. The quantum is computed once by the caller (nothing
/// inside the frame loop moves MaxLife/MaxMana), so the per-frame value is
/// identical by construction — the exact same reasoning that kept it hoisted
/// out of each original loop.
///
/// BEHAVIOR-PRESERVING BY CONSTRUCTION: this returns the whole points drained
/// rather than writing a pool, so each owner keeps persisting its own two ints
/// (mana and merc life live on Character/Merc, not here) and the sub-point
/// remainder rides the caller's OWN `ref long` (a per-path field that is NOT a
/// save field — the pool round-trips may drop < 1 of fraction, the disclosed
/// gap each owner's header names). A local copy of the whole pool advances by
/// the same whole units the original property did each frame, so every
/// intermediate pool256 and the final whole value are byte-identical.
/// </summary>
internal static class Fixed256Accumulator
{
    /// <summary>Add <paramref name="quantum256"/> per frame into the caller's
    /// 256ths accumulator (clamped to the missing pool), draining whole units;
    /// <paramref name="poolWhole"/> is the pool as it stands and
    /// <paramref name="sub256"/> the caller's persistent sub-point remainder.
    /// Returns the whole units to add to the caller's pool (0..nothing moved
    /// when the pool is full, the quantum is 0, or there is no pool). The loop
    /// guards (frames ≥ 0, quantum > 0, max256 > 0) and the full-pool fixpoint
    /// break mirror the original loops exactly.</summary>
    internal static long Tick(int frames, long max256, long quantum256, long poolWhole, ref long sub256)
    {
        long pool = poolWhole;
        long drained = 0;
        for (int f = 0; f < frames && quantum256 > 0 && max256 > 0; f++)
        {
            long pool256 = pool * 256 + sub256;
            if (pool256 >= max256) break; // full is a fixpoint — the clamp holds
            sub256 += Math.Min(quantum256, max256 - pool256); // add, clamped to the missing pool
            long whole = sub256 / 256;                        // whole units leave the accumulator…
            pool += whole;
            drained += whole;
            sub256 %= 256;                                    // …the sub-point rides on (exact)
        }
        return drained;
    }
}
