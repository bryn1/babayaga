using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>
/// The save-file DTO — the whole session as one JSON document at a caller-given
/// path (P3 §6: stones, rites and loot persist WITH the character). Property
/// order below IS the dump order — fixed, so save -> load -> dump is
/// byte-identical. The RNG streams persist as their raw PCG state words, not
/// the seed: a loaded session CONTINUES the streams it had at save time, it
/// never replays rolls already consumed (DA round-1 P1-1).
/// </summary>
public sealed class SessionSave
{
    public int Version { get; init; } = SaveSerializer.SaveVersion;
    public ulong Seed { get; init; }
    public ulong CombatRngState { get; init; }
    public ulong DropRngState { get; init; }
    /// <summary>Third stream (v4, row 69): monster counter rolls; restored like
    /// the other two so a loaded session continues it, never replays it.</summary>
    public ulong EnemyRngState { get; init; }
    public string ClassKey { get; init; } = "";
    public string Name { get; init; } = "";
    public long Xp { get; init; }
    public int StrAlloc { get; init; }
    public int DexAlloc { get; init; }
    public int VitAlloc { get; init; }
    public int EnerAlloc { get; init; }
    public long FlatLife { get; init; }
    public long FlatMana { get; init; }
    /// <summary>CURRENT pools (v4, row 69): the belt's life/mana as saved —
    /// MaxLife/MaxMana stay computed, never stored.</summary>
    public long Life { get; init; }
    public long Mana { get; init; }
    public int IncLifePercent { get; init; }
    public int IncManaPercent { get; init; }
    public long WeaponBaseAr { get; init; }
    public long FlatAr { get; init; }
    public int ArPercent { get; init; }
    public int ShieldBlockBase { get; init; }
    public int BlockBonus { get; init; }
    public int StrFromItems { get; init; }
    public int DexFromItems { get; init; }
    public int VitFromItems { get; init; }
    public int EnerFromItems { get; init; }
    public int PlusSkillLevels { get; init; }
    public string CurrentArea { get; init; } = "";
    public long Gold { get; init; }
    public IReadOnlyList<string> Waypoints { get; init; } = [];
    public IReadOnlyList<int> QuestPhases { get; init; } = [];
    public IReadOnlyList<SkillPointsSave> Skills { get; init; } = [];
    /// <summary>Unspent skill-point pool AT SAVE TIME (DA round-2 P1-N1): quest
    /// rewards deliberately skip phase replay, so the pool cannot be re-derived
    /// from level grants — the save carries it.</summary>
    public int UnspentSkillPoints { get; init; }
    public int StashTabs { get; init; }
    public int OtherRewards { get; init; }
    public int LastLootedIdx { get; init; } = -1;
    /// <summary>Belt cells as carried indices, -1 for an empty slot (v4, row 69) —
    /// the LastLootedIdx pattern: the slot re-points at the RESTORED item copy.</summary>
    public IReadOnlyList<int> Belt { get; init; } = [];
    public string? EngagedKey { get; init; }
    public long EngagedLife { get; init; }
    public IReadOnlyList<ItemSave> Carried { get; init; } = [];
    public IReadOnlyList<EquipSave> Equipment { get; init; } = [];
    public IReadOnlyList<ItemSave> PendingDrops { get; init; } = [];
    public long PendingGold { get; init; }
    /// <summary>The hired mercenary as saved (v5, §6.7 rows 65–66) — null when
    /// none rides. The v5 additions sit LAST so every older field keeps its
    /// dump position; the block rides AS SAVED (level, cumulative XP, current
    /// life, the travel-mirror area, the in-combat stand-off) — the mercenary
    /// accumulator fraction is not persisted, same disclosed gap as the mana
    /// accumulator's (GameSession.Hireling header).</summary>
    public HirelingSave? Merc { get; init; }
    /// <summary>The town stash as saved (v6, row 74 / M4-S12) — tab by tab, tabs
    /// sorted by number, items riding the SAME ItemSave rows with their anchor
    /// cells. Opened-but-EMPTY tabs are deliberately absent: StashTabs above
    /// already carries the unlock truth (the empty-never-pays rule), and the
    /// v6 addition sits LAST so every older field keeps its dump position.</summary>
    public IReadOnlyList<StashTabSave> Stash { get; init; } = [];
}

/// v6: one stash tab at save time (SaveSerializer re-places every item through
/// the tab's own InventoryGrid — the fits-or-refuses law the carried rebuild runs).
public sealed record StashTabSave(int Tab, IReadOnlyList<ItemSave> Items);

/// v5: one hireling at save time (SaveSerializer.RestoreHireling rebuilds it
/// through the hireling table — a save naming a vanished id fails the load).
public sealed record HirelingSave(string Id, int Level, long Xp, long Life, string Area, int CombatDistance);

public sealed record SkillPointsSave(string Id, int Points);
public sealed record ModSave(string Stat, int Value);

/// One finished item; Row/Col are its grid cells (-1 for unplaced pending drops).
public sealed record ItemSave(
    string Base, int Ilvl, int Quality, bool Ethereal, int Sockets,
    IReadOnlyList<string> Runes, string? Runeword, string? Unique, string? Set,
    string? Prefix, string? Suffix, int LevelReq, int ReqStr, int ReqDex, int Durability,
    IReadOnlyList<ModSave> Mods, IReadOnlyList<string> AffixIds, int Row, int Col);

public sealed record EquipSave(string Slot, ItemSave Item);

