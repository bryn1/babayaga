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
    public string ClassKey { get; init; } = "";
    public string Name { get; init; } = "";
    public long Xp { get; init; }
    public int StrAlloc { get; init; }
    public int DexAlloc { get; init; }
    public int VitAlloc { get; init; }
    public int EnerAlloc { get; init; }
    public long FlatLife { get; init; }
    public long FlatMana { get; init; }
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
    public string? EngagedKey { get; init; }
    public long EngagedLife { get; init; }
    public IReadOnlyList<ItemSave> Carried { get; init; } = [];
    public IReadOnlyList<EquipSave> Equipment { get; init; } = [];
    public IReadOnlyList<ItemSave> PendingDrops { get; init; } = [];
    public long PendingGold { get; init; }
}

public sealed record SkillPointsSave(string Id, int Points);
public sealed record ModSave(string Stat, int Value);

/// One finished item; Row/Col are its grid cells (-1 for unplaced pending drops).
public sealed record ItemSave(
    string Base, int Ilvl, int Quality, bool Ethereal, int Sockets,
    IReadOnlyList<string> Runes, string? Runeword, string? Unique, string? Set,
    string? Prefix, string? Suffix, int LevelReq, int ReqStr, int ReqDex, int Durability,
    IReadOnlyList<ModSave> Mods, IReadOnlyList<string> AffixIds, int Row, int Col);

public sealed record EquipSave(string Slot, ItemSave Item);

