using BabaYaga.Sim;

namespace BabaYaga.Sim.Cli;

/// <summary>
/// Read-only debug verbs for the oracle-parity harness (MC 11777): pure pass-throughs
/// into the existing sim classes — the CLI owns no math here, and a verb neither needs
/// nor mutates a session ('new' is not required). Grammar → owning class:
///   xpfor &lt;level&gt; → XpTable.TotalXpForLevel · tohit &lt;ar&gt; &lt;def&gt; &lt;alvl&gt; &lt;dlvl&gt; → CombatResolver.ChanceToHit
///   block &lt;shield&gt; &lt;bonus&gt; &lt;classfactor&gt; &lt;dex&gt; &lt;clvl&gt; &lt;running01&gt; → CombatResolver.ChanceToBlock
///   cost &lt;base&gt; [stack] [reduce%] [questmult] → Vendor.BuyPrice (Standard profile)
///   gambleroll &lt;clvl&gt; &lt;n&gt; [seed] → GambleStore.IlvlFor (seed defaults to the run seed)
///   xpshare &lt;basexp&gt; &lt;monlvl&gt; &lt;lvl&gt;… → XpShare.Shares + Pot · deathpen &lt;diff 0|1|2&gt; &lt;curxp&gt; &lt;lvl&gt;
///   → DeathPenalty.Apply (the level's start row is the ladder's, echoed as LVLSTART)
///   qualityroll &lt;ilvl&gt; &lt;mf&gt; &lt;trials&gt; &lt;seed&gt; [base] → QualityRoll.Roll, unique candidate
///   pinned true / set pinned false; NORMAL counts the white family (N/Superior/Low).
/// </summary>
public sealed partial class ScriptedSession
{
    private XpTable? _ladder;
    private ItemBaseTable? _baseTable;
    private XpTable Ladder => _ladder ??= XpTable.Load(Path.Combine(_dataDir, "xp-ladder.json"));
    private ItemBaseTable Bases => _baseTable ??= ItemBaseTable.Load(Path.Combine(_dataDir, "item-bases.json"));

    /// Debug-verb route table; null = not a debug verb, so the session grammar runs.
    private Dictionary<string, object?>? DebugVerb(Dictionary<string, object?> p, string[] parts) => parts[0] switch
    {
        "xpfor" => XpFor(p, parts),
        "tohit" => ToHit(p, parts),
        "block" => Block(p, parts),
        "cost" => Cost(p, parts),
        "gambleroll" => GambleRoll(p, parts),
        "xpshare" => XpShareVerb(p, parts),
        "deathpen" => DeathPen(p, parts),
        "qualityroll" => QualityRollVerb(p, parts),
        _ => null,
    };

    private Dictionary<string, object?> XpFor(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 2, "xpfor <level>");
        p["XP"] = Ladder.TotalXpForLevel(Num(parts, 1, 1, "xpfor"));
        return p;
    }

    private static Dictionary<string, object?> ToHit(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 5, "tohit <ar> <def> <alvl> <dlvl>");
        p["TOHIT"] = CombatResolver.ChanceToHit(LongNum(parts, 1, "tohit"), LongNum(parts, 2, "tohit"),
            Num(parts, 3, 1, "tohit"), Num(parts, 4, 1, "tohit"));
        return p;
    }

    private static Dictionary<string, object?> Block(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 7, "block <shield> <bonus> <classfactor> <dex> <clvl> <running01>");
        p["BLOCK"] = CombatResolver.ChanceToBlock(Num(parts, 1, 0, "block"), Num(parts, 2, 0, "block"),
            Num(parts, 3, 0, "block"), Num(parts, 4, 0, "block"), Num(parts, 5, 1, "block"), Num(parts, 6, 0, "block") != 0);
        return p;
    }

    private Dictionary<string, object?> Cost(Dictionary<string, object?> p, string[] parts)
    {
        ArgCount(parts, "cost <base> [stack] [reduce%] [questmult]", 2, 3, 4, 5);
        var baseRow = Bases.For(parts[1]);
        p["COST"] = baseRow.Cost;
        p["PRICE"] = Vendor.BuyPrice(baseRow, Vendor.Standard, Num(parts, 2, 1, "cost"),
            Num(parts, 4, 1024, "cost"), Num(parts, 3, 0, "cost"));
        return p;
    }

    private Dictionary<string, object?> GambleRoll(Dictionary<string, object?> p, string[] parts)
    {
        ArgCount(parts, "gambleroll <clvl> <n> [seed]", 3, 4);
        int clvl = Num(parts, 1, 1, "gambleroll"), n = Num(parts, 2, 1, "gambleroll");
        if (n is < 1 or > 100_000) throw new SessionRejectedException("gambleroll n must be 1..100000");
        var rng = new Rng(WordNum(parts, 3, _seed, "gambleroll"));
        p["ILVLS"] = Enumerable.Range(0, n).Select(_ => GambleStore.IlvlFor(rng, clvl)).ToArray();
        return p;
    }

    private Dictionary<string, object?> XpShareVerb(Dictionary<string, object?> p, string[] parts)
    {
        ArgCount(parts, "xpshare <basexp> <monlvl> <lvl> [lvl…]", 4, 5, 6, 7); // 1..4 members
        var ladder = Ladder;
        var at = new WorldPos(0, 0);
        var party = new List<PartyMember>();
        for (int i = 3; i < parts.Length; i++)
        {
            var hero = new Character(Stats.Koldun);
            hero.GrantXp(ladder.TotalXpForLevel(Num(parts, i, 1, "xpshare")), ladder); // lands exactly on the row
            party.Add(new PartyMember(hero, at));
        }
        long baseXp = LongNum(parts, 1, "xpshare");
        var shares = XpShare.Shares(ladder, baseXp, KillType.Normal, Num(parts, 2, 1, "xpshare"), at, party);
        p["KILLXP"] = baseXp;
        p["POT"] = XpShare.Pot(baseXp, party.Count);
        p["SHARES"] = shares;
        p["SHARESUM"] = shares.Sum();
        return p;
    }

    private Dictionary<string, object?> DeathPen(Dictionary<string, object?> p, string[] parts)
    {
        Arg(parts, 4, "deathpen <difficulty 0|1|2> <curxp> <lvl>");
        int d = Num(parts, 1, 0, "deathpen"), lvl = Num(parts, 3, 1, "deathpen");
        if (d is < 0 or > 2) throw new SessionRejectedException("deathpen difficulty must be 0 Ordinary, 1 Nightmare or 2 Hell");
        var res = DeathPenalty.Apply(Ladder, LongNum(parts, 2, "deathpen"), lvl, (Difficulty)d);
        p["DIFFICULTY"] = d;
        p["LVLSTART"] = Ladder.TotalXpForLevel(lvl);
        p["XPAFTER"] = res.XpAfter;
        p["LOST"] = res.Lost;
        p["CORPSEXP"] = res.CorpseXp;
        return p;
    }

    private Dictionary<string, object?> QualityRollVerb(Dictionary<string, object?> p, string[] parts)
    {
        ArgCount(parts, "qualityroll <ilvl> <mf> <trials> <seed> [base]", 5, 6);
        int ilvl = Num(parts, 1, 1, "qualityroll"), mf = Num(parts, 2, 0, "qualityroll");
        int trials = Num(parts, 3, 1, "qualityroll");
        if (trials is < 1 or > 400_000) throw new SessionRejectedException("qualityroll trials must be 1..400000");
        var rng = new Rng(WordNum(parts, 4, 0, "qualityroll"));
        var baseRow = parts.Length > 5 ? Bases.For(parts[5]) : Bases.Stock[0];
        int unique = 0, rare = 0, magic = 0, white = 0;
        for (int i = 0; i < trials; i++)
            switch (QualityRoll.Roll(rng, baseRow, ilvl, magicFind: mf, uniqueCandidateExists: true))
            {
                case ItemQuality.Unique: unique++; break;
                case ItemQuality.Rare: rare++; break;
                case ItemQuality.Magic: magic++; break;
                default: white++; break; // white family: Normal/Superior/LowQuality (set pinned absent)
            }
        p["BASE"] = baseRow.Id;
        p["QLVL"] = baseRow.Qlvl;
        p["UNIQUE"] = unique;
        p["RARE"] = rare;
        p["MAGIC"] = magic;
        p["NORMAL"] = white;
        return p;
    }

    private static long LongNum(string[] parts, int index, string cmd) =>
        long.TryParse(parts[index], out var n)
            ? n
            : throw new SessionRejectedException($"'{cmd}': '{parts[index]}' is not a number");

    private static ulong WordNum(string[] parts, int index, ulong fallback, string cmd) =>
        index >= parts.Length ? fallback
            : ulong.TryParse(parts[index], out var n)
                ? n
                : throw new SessionRejectedException($"'{cmd}': '{parts[index]}' is not a number");
}
