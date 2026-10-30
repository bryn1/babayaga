namespace BabaYaga.Sim;

/// <summary>
/// The plain event the sim EMITS when a monster dies (M3 wiring contract): which
/// affix kind stood on the corpse, the item level it carries, and the area seed
/// the drop must be reproducible from. Deliberately NO item-generation call —
/// the item system is a sibling module; a later phase feeds this record into it.
/// </summary>
public readonly record struct DeathDropEvent(string AffixKind, int Ilvl, int AreaSeed)
{
    /// ilvl sources (P2 §4.4 shape): champion packs roll at area level +2, uniques
    /// at +3, plain monsters at the area level itself. AffixKind names the leader
    /// affix; a plain kill carries "plain".
    public static DeathDropEvent ForKill(
        SpecialKind kind, IReadOnlyList<string> affixKeys, int areaLevel, int areaSeed)
    {
        int bump = kind switch
        {
            SpecialKind.Unique => 3,
            SpecialKind.Champion => 2,
            _ => 0,
        };
        return new DeathDropEvent(
            AffixKind: affixKeys.Count > 0 ? affixKeys[0] : "plain",
            Ilvl: areaLevel + bump,
            AreaSeed: areaSeed);
    }
}
