using System.Text.Json;

namespace BabaYaga.Sim;

/// <summary>
/// The ONE serializer for save files and session dumps (docs/ARCHITECTURE.md data
/// stores: headless and engine paths share this). A foreign Version is refused;
/// a structurally broken save becomes an InvalidDataException, never a partial load.
/// </summary>
public static class SaveSerializer
{
    /// <summary>Save-format version 3 (DA round-2 P1-N1): the DTO gained the
    /// explicit unspentSkillPoints pool — the pool restores FROM THE SAVE, never
    /// re-derived (quest rewards deliberately skip replay). Version 2 was the
    /// round-1 stream-state fix; a foreign Version is refused rather than
    /// silently half-restored. Version 4 (row 69, M4-S5): the current life/mana
    /// pools, the third (enemy-counter) stream word, and the belt as carried
    /// indices — a loaded belt re-points at the restored copies, never at the
    /// pre-save instances. Version 5 (§6.7 rows 65–66, M4-S11): the hireling
    /// block — the one mercenary slot as saved, null when none rides.
    /// Version 6 (row 74, M4-S12): the town stash — per-tab item rows on their
    /// anchor cells, riding the SAME ItemSave serialization as the carried pack;
    /// the unlock count already rode StashTabs. A v5 save is refused by name.
    /// Version 7 (row 66 gear half, M4-S15, card/11903): the hireling block
    /// gained its WORN GEAR — the 4-slot panel items (§6.7b, MC 11898), slot-
    /// named, riding the SAME ItemSave serialization as pack and panel; the
    /// addition sits inside the LAST block, so no older field moves. A v6 save
    /// is refused by name.</summary>
    public const int SaveVersion = 7;

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
            CombatRngState = s.CombatRng.State,
            DropRngState = s.DropRng.State,
            EnemyRngState = s.EnemyRng.State,
            ClassKey = c.Profile.Key,
            Name = c.Name,
            Xp = c.Xp,
            StrAlloc = c.StrAlloc, DexAlloc = c.DexAlloc, VitAlloc = c.VitAlloc, EnerAlloc = c.EnerAlloc,
            FlatLife = c.FlatLife, FlatMana = c.FlatMana,
            Life = c.Life, Mana = c.Mana,
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
            UnspentSkillPoints = c.SkillPoints,
            StashTabs = s.StashTabs,
            OtherRewards = s.OtherRewards,
            LastLootedIdx = s.LastLooted is null ? -1 : s.Carried.IndexOf(s.LastLooted),
            Belt = [.. s.Belt.Select(i => i is null ? -1 : s.Carried.IndexOf(i))],
            EngagedKey = s.EngagedKey,
            EngagedLife = s.Engaged?.Life ?? 0,
            Carried = [.. s.Carried.Select(i => ItemDto(i, s.CarriedSpots.GetValueOrDefault(i, (-1, -1))))],
            Equipment = [.. s.Equipment.Equipped
                .OrderBy(kv => (int)kv.Key)
                .Select(kv => new EquipSave(kv.Key.ToString(), ItemDto(kv.Value, (-1, -1))))],
            PendingDrops = [.. s.Drops.PendingItems.Select(i => ItemDto(i, (-1, -1)))],
            PendingGold = s.Drops.Gold,
            // v7: the worn merc panel rides slot-ordered (byte-stable, the
            // Equipment idiom); an empty panel rides as an empty list.
            Merc = s.Merc is null ? null : new HirelingSave(
                s.Merc.Id, s.Merc.Level, s.Merc.Xp, s.Merc.Life, s.Merc.Area, s.Merc.CombatDistance,
                [.. s.Merc.Gear.OrderBy(kv => (int)kv.Key)
                    .Select(kv => new MercGearSave(kv.Key.ToString(), ItemDto(kv.Value, (-1, -1))))]),
            // v6 LAST (the v5 law: older fields keep their dump positions): opened-
            // but-EMPTY tabs are NOT saved — StashTabs already carries the unlock
            // truth; tabs sort by number, items in placement order (byte-stable).
            Stash = [.. s.StashForSave
                .Where(kv => kv.Value.Items.Count > 0)
                .OrderBy(kv => kv.Key)
                .Select(kv => new StashTabSave(kv.Key, [.. kv.Value.Items
                    .Select(i => ItemDto(i, kv.Value.Spots.GetValueOrDefault(i, (-1, -1))))]))],
        };
    }

    private static void Apply(GameSession s, SessionSave d)
    {
        try
        {
            // Stream state first (DA P1-1): everything below restores without
            // consuming, and the loaded session continues where it left off.
            s.CombatRng.State = d.CombatRngState;
            s.DropRng.State = d.DropRngState;
            s.EnemyRng.State = d.EnemyRngState;

            var c = s.Character;
            c.GrantXp(d.Xp, s.Ladder);
            Allocate(c, AllocatableStat.Strength, d.StrAlloc);
            Allocate(c, AllocatableStat.Dexterity, d.DexAlloc);
            Allocate(c, AllocatableStat.Vitality, d.VitAlloc);
            Allocate(c, AllocatableStat.Energy, d.EnerAlloc);
            c.FlatLife = d.FlatLife;
            c.FlatMana = d.FlatMana;
            c.Life = d.Life; // absolute pool restore (v4) — Max stays computed
            c.Mana = d.Mana;
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
            // The pool is set to the EARNED total — unspent + already-spent (DA
            // round-2 P1-N1): level grants alone cannot fund the replay when a
            // quest point was spent (total hard points may exceed the grants), and
            // the replay's debits land the pool on the saved unspent value — an
            // absolute restore, so the GrantXp replay above adds no double-count.
            c.RestoreSkillPoints(d.UnspentSkillPoints + d.Skills.Sum(p => p.Points));
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
            s.RestoreBelt(d.Belt); // AFTER the carried rebuild — slots point at restored copies
            foreach (var eq in d.Equipment)
                if (!s.Equipment.Equip(BuildItem(s, eq.Item), Enum.Parse<EquipSlot>(eq.Slot)))
                    throw new InvalidDataException($"equipment slot '{eq.Slot}' refused its item");

            s.Drops.Enqueue(new TreasureClassDrop(
                [.. d.PendingDrops.Select(p => BuildItem(s, p))], d.PendingGold));
            s.SetEngaged(d.EngagedKey, d.EngagedLife);
            if (d.Merc is not null)
            {
                s.RestoreHireling(d.Merc); // v5: rebuild via the hireling table
                // v7: the worn panel lands through the authoring door (the
                // Equipment idiom — the merc slot is not a grid cell, there is
                // nothing to refit): slot-named, save order preserved.
                foreach (var g in d.Merc.Gear)
                    s.Merc!.Gear[Enum.Parse<MercGearSlot>(g.Slot)] = BuildItem(s, g.Item);
            }
            // v6: the stash re-materializes tab by tab (number order = save order).
            // An item is restored through the tab's own InventoryGrid — the same
            // fits-or-refuses law the carried rebuild runs, no second engine.
            foreach (var tabSave in d.Stash)
            {
                if (!s.IsStashTabUnlocked(tabSave.Tab))
                    throw new InvalidDataException(
                        $"stash tab {tabSave.Tab} holds items but only {s.StashTabs + 1} stand unlocked");
                var tab = s.StashTabForSave(tabSave.Tab);
                foreach (var save in tabSave.Items)
                {
                    var item = BuildItem(s, save);
                    if (!tab.Grid.TryPlace(item, save.Row, save.Col))
                        throw new InvalidDataException(
                            $"stash item '{save.Base}' in tab {tabSave.Tab} no longer fits its cells");
                    tab.Items.Add(item);
                    tab.Spots[item] = (save.Row, save.Col);
                }
            }
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
