namespace BabaYaga.Sim;

/// <summary>One kill's full output: the death-drop event, the XP the chain paid
/// (0 when the kill came from a strike resolved without a ladder), and the drop.</summary>
public sealed record KillFlowResult(DeathDropEvent Event, long XpGranted, TreasureClassDrop Drop);

/// <summary>
/// The M1 close-out kill chain, composing the siblings (P2 §4.9 pipeline):
/// a CombatResolver strike — whose kill pays the XpTable grant through
/// Character.GrantXp (M1 foundation contract) — plus the M3 DeathDropEvent,
/// then the TreasureClass walk feeding the DropQueue. A non-kill strike emits
/// NOTHING: no event, no walk, no RNG consumed downstream of the strike.
/// </summary>
public sealed class KillFlow
{
    private readonly TreasureClassTable _classes;
    private readonly ItemForge _forge;

    public DropQueue Queue { get; }

    public KillFlow(TreasureClassTable classes, ItemForge forge, DropQueue queue)
    {
        _classes = classes;
        _forge = forge;
        Queue = queue;
    }

    /// Post-kill half of the chain: event → walk → queue. Null (and no state
    /// touched) when the outcome is not a kill.
    public KillFlowResult? OnKill(
        Rng dropRng, CombatResolver.StrikeOutcome outcome, SpecialKind kind, IReadOnlyList<string> affixKeys,
        int areaLevel, int areaSeed, string tcKey, int magicFind = 0, int goldFind = 0)
    {
        if (!outcome.Killed) return null;
        var death = DeathDropEvent.ForKill(kind, affixKeys, areaLevel, areaSeed);
        var drop = _classes.Walk(dropRng, tcKey, death.Ilvl, magicFind, goldFind, _forge);
        Queue.Enqueue(drop);
        return new KillFlowResult(death, outcome.XpGranted, drop);
    }

    /// The whole chain in one call: strike through CombatResolver (combat RNG),
    /// and if it killed, the drop half runs on the separate drop RNG so combat
    /// replays stay stable when drop content changes.
    public (CombatResolver.StrikeOutcome Outcome, KillFlowResult? Flow) Strike(
        Rng combatRng, Rng dropRng, Character attacker, Monster defender, XpTable ladder,
        int damageMin, int damageMax, string tcKey, SpecialKind kind,
        IReadOnlyList<string> affixKeys, int areaLevel, int areaSeed,
        int magicFind = 0, int goldFind = 0)
    {
        CombatResolver.StrikeOutcome outcome = CombatResolver.ResolvePlayerStrike(
            combatRng, attacker, defender, damageMin, damageMax, ladder: ladder);
        return (outcome, OnKill(
            dropRng, outcome, kind, affixKeys, areaLevel, areaSeed, tcKey, magicFind, goldFind));
    }
}
