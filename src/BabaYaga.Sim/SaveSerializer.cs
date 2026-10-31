using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>
/// The ONE serializer for save files and session dumps (docs/ARCHITECTURE.md data
/// stores: headless and engine paths share this). A foreign Version is refused;
/// a structurally broken save becomes an InvalidDataException, never a partial load.
/// </summary>
public static class SaveSerializer
{
    public const int SaveVersion = 1;

    private static readonly JsonSerializerOptions FileOpts = new()
    { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly JsonSerializerOptions DumpOpts = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Dump(GameSession session) => JsonSerializer.Serialize(ToDto(session), DumpOpts);

    public static void SaveFile(GameSession session, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("save path required");
        File.WriteAllText(path, JsonSerializer.Serialize(ToDto(session), FileOpts));
    }

    public static GameSession LoadSession(string path, string dataDir)
    {
        var dto = JsonSerializer.Deserialize<SessionSave>(File.ReadAllText(path), FileOpts)
            ?? throw new InvalidDataException("save file holds no session");
        if (dto.Version != SaveVersion)
            throw new InvalidDataException($"save version {dto.Version} unsupported (this build reads {SaveVersion})");
        var session = GameSession.New(dto.Seed, dto.ClassKey, dataDir);
        Apply(session, dto);
        return session;
    }

    private static SessionSave ToDto(GameSession s)
    {
        var c = s.Character;
        return new SessionSave
        {
            Seed = s.Seed,
            ClassKey = c.Profile.Key,
            Name = c.Name,
            Xp = c.Xp,
            StrAlloc = c.StrAlloc, DexAlloc = c.DexAlloc, VitAlloc = c.VitAlloc, EnerAlloc = c.EnerAlloc,
            FlatLife = c.FlatLife, FlatMana = c.FlatMana,
            IncLifePercent = c.IncreaseMaxLifePercent, IncManaPercent = c.IncreaseMaxManaPercent,
            WeaponBaseAr = c.WeaponBaseAr, FlatAr = c.FlatAr, ArPercent = c.ArPercent,
            ShieldBlockBase = c.ShieldBlockBase, BlockBonus = c.BlockBonus,
            StrFromItems = c.StrFromItems, DexFromItems = c.DexFromItems,
            VitFromItems = c.VitFromItems, EnerFromItems = c.EnerFromItems,
            PlusSkillLevels = s.Skills.PlusSkillLevels,
            CurrentArea = s.CurrentArea,
            Gold = s.Purse.Amount,
            Waypoints = [.. s.Waypoints.Nodes.Where(n => s.Waypoints.IsDiscovered(n.AreaKey))
                .Select(n => n.AreaKey).OrderBy(k => k, StringComparer.Ordinal)],
            QuestPhases = [.. s.Quests.Quests.Select(q => (int)q.Phase)],
            Skills = [.. s.SkillData.Skills
                .Select(def => new SkillPointsSave(def.Id, s.Skills.HardPoints(def.Id)))
                .Where(p => p.Points > 0)
                .OrderBy(p => s.SkillData.Find(p.Id)!.Row).ThenBy(p => p.Id, StringComparer.Ordinal)],
            QuestSkillRewards = s.QuestSkillRewards,
            StashTabs = s.StashTabs,
            OtherRewards = s.OtherRewards,
            LastLootedIdx = s.LastLooted is null ? -1 : s.Carried.IndexOf(s.LastLooted),
            EngagedKey = s.EngagedKey,
            EngagedLife = s.Engaged?.Life ?? 0,
            Carried = [.. s.Carried.Select(i => ItemDto(i, s.CarriedSpots.GetValueOrDefault(i, (-1, -1))))],
            Equipment = [.. s.Equipment.Equipped
                .OrderBy(kv => (int)kv.Key)
                .Select(kv => new EquipSave(kv.Key.ToString(), ItemDto(kv.Value, (-1, -1))))],
            PendingDrops = [.. s.Drops.PendingItems.Select(i => ItemDto(i, (-1, -1)))],
            PendingGold = s.Drops.Gold,
        };
    }

    private static void Apply(GameSession s, SessionSave d)
    {
        try
        {
            var c = s.Character;
            c.GrantXp(d.Xp, s.Ladder);
            Allocate(c, AllocatableStat.Strength, d.StrAlloc);
            Allocate(c, AllocatableStat.Dexterity, d.DexAlloc);
            Allocate(c, AllocatableStat.Vitality, d.VitAlloc);
            Allocate(c, AllocatableStat.Energy, d.EnerAlloc);
            c.FlatLife = d.FlatLife;
            c.FlatMana = d.FlatMana;
            c.IncreaseMaxLifePercent = d.IncLifePercent;
            c.IncreaseMaxManaPercent = d.IncManaPercent;
            c.WeaponBaseAr = d.WeaponBaseAr;
            c.FlatAr = d.FlatAr;
            c.ArPercent = d.ArPercent;
            c.ShieldBlockBase = d.ShieldBlockBase;
            c.BlockBonus = d.BlockBonus;
            c.StrFromItems = d.StrFromItems;
            c.DexFromItems = d.DexFromItems;
            c.VitFromItems = d.VitFromItems;
            c.EnerFromItems = d.EnerFromItems;
            s.Skills.PlusSkillLevels = d.PlusSkillLevels;
            // Hard points replay in grid order: prereq edges only ever point up-row.
            foreach (var (id, points) in d.Skills)
                for (int i = 0; i < points; i++) s.Skills.Spend(c, id);

            s.RestorePurse(d.Gold);
            s.SetArea(d.CurrentArea);
            foreach (var area in d.Waypoints)
                if (s.Waypoints.HasNode(area)) s.Waypoints.Discover(area);
            for (int i = 0; i < d.QuestPhases.Count && i < s.Quests.Quests.Count; i++)
            {
                var quest = s.Quests.Quests[i];
                for (int p = 0; p < d.QuestPhases[i]; p++)
                {
                    var moved = p switch
                    {
                        0 => quest.TryActivate(),
                        1 => quest.TryMarkCompletable(),
                        2 => quest.TryRequestReward(),
                        _ => quest.TryClaimReward(),
                    };
                    if (!moved) throw new InvalidDataException($"quest '{quest.Row.Key}' phase {p} unrecoverable");
                }
            }
            if (d.QuestPhases.Count != s.Quests.Quests.Count)
                throw new InvalidDataException("save lists a different quest count than the table");

            s.QuestSkillRewards = d.QuestSkillRewards;
            s.StashTabs = d.StashTabs;
            s.OtherRewards = d.OtherRewards;

            foreach (var save in d.Carried)
            {
                var item = BuildItem(s, save);
                if (!s.Grid.TryPlace(item, save.Row, save.Col))
                    throw new InvalidDataException($"carried item '{save.Base}' no longer fits its cells");
                s.Carried.Add(item);
                s.CarriedSpots[item] = (save.Row, save.Col);
            }
            if (d.LastLootedIdx >= 0)
            {
                if (d.LastLootedIdx >= s.Carried.Count)
                    throw new InvalidDataException("lastLootedIdx outside the carried list");
                s.LastLooted = s.Carried[d.LastLootedIdx];
            }
            foreach (var eq in d.Equipment)
                if (!s.Equipment.Equip(BuildItem(s, eq.Item), Enum.Parse<EquipSlot>(eq.Slot)))
                    throw new InvalidDataException($"equipment slot '{eq.Slot}' refused its item");

            s.Drops.Enqueue(new TreasureClassDrop(
                [.. d.PendingDrops.Select(p => BuildItem(s, p))], d.PendingGold));
            s.SetEngaged(d.EngagedKey, d.EngagedLife);
        }
        catch (Exception e) when (e is SkillSpendRejectedException or ArgumentException or KeyNotFoundException)
        {
            throw new InvalidDataException($"save file cannot be restored: {e.Message}", e);
        }
    }

    private static void Allocate(Character c, AllocatableStat stat, int points)
    {
        if (points == 0) return;
        if (!c.TryAllocateStat(stat, points))
            throw new InvalidDataException($"cannot restore {points} {stat} point(s)");
    }

    private static Item BuildItem(GameSession s, ItemSave d)
    {
        var item = new Item(s.Bases.For(d.Base), d.Ilvl)
        {
            Quality = (ItemQuality)d.Quality,
            Ethereal = d.Ethereal,
            Sockets = d.Sockets,
            RunewordId = d.Runeword,
            UniqueId = d.Unique,
            SetId = d.Set,
            PrefixName = d.Prefix,
            SuffixName = d.Suffix,
            LevelRequirement = d.LevelReq,
            RequiredStrength = d.ReqStr,
            RequiredDexterity = d.ReqDex,
            CurrentDurability = d.Durability,
        };
        item.SocketedRunes.AddRange(d.Runes);
        item.Mods.AddRange(d.Mods.Select(m => new ItemMod(m.Stat, m.Value)));
        item.AffixIds.AddRange(d.AffixIds);
        return item;
    }

    private static ItemSave ItemDto(Item i, (int Row, int Col) spot) => new(
        i.Base.Id, i.ItemLevel, (int)i.Quality, i.Ethereal, i.Sockets,
        [.. i.SocketedRunes], i.RunewordId, i.UniqueId, i.SetId,
        i.PrefixName, i.SuffixName, i.LevelRequirement, i.RequiredStrength, i.RequiredDexterity,
        i.CurrentDurability, [.. i.Mods.Select(m => new ModSave(m.Stat, m.Value))], [.. i.AffixIds],
        spot.Row, spot.Col);
}
