namespace BabaYaga.Sim;

/// <summary>A command the session refuses: unknown reference, an invalid
/// transition, or a purse/pool that cannot pay. The CLI turns one into an
/// ERROR line and a nonzero exit; nothing half-applied stays behind.</summary>
public sealed class SessionRejectedException(string reason) : Exception(reason);

/// <summary>
/// The plain-C# session object the CLI and pytest drive (and the Godot path will):
/// one character + the Act-I world state (area, stones, rites) + the seeded RNG
/// streams, with Save/Load to a caller-given path through SaveSerializer.
/// Construction loads the original data tables from a caller-injected data dir;
/// every command either fully applies or is rejected with no state change.
/// </summary>
public sealed partial class GameSession
{
    /// The coin purse a new hedge-witch arrives with (original design row).
    public const long StartGold = 1000;
    /// Potion of Life: +20 base life, once per difficulty (P3 §6.4).
    public const int PotionOfLifeFlat = 20;

    public string DataDir { get; }
    public XpTable Ladder { get; }
    public ClassSkillData SkillData { get; }
    public ItemBaseTable Bases { get; }
    public MonsterTable Monsters { get; }
    public PackData Packs { get; }
    public TreasureClassTable Classes { get; }
    public AreaGraph Areas { get; }
    public WaypointGraph Waypoints { get; }
    public QuestLog Quests { get; }

    public ulong Seed { get; }
    public Character Character { get; }
    public SkillTree Skills { get; }
    public Purse Purse { get; private set; }
    public InventoryGrid Grid { get; } = new();
    public List<Item> Carried { get; } = [];

    /// Grid cell of each carried item — kept in lockstep with Carried so the
    /// save can restore byte-identical placement (the grid itself never reports back).
    internal Dictionary<Item, (int Row, int Col)> CarriedSpots { get; } = new(ReferenceEqualityComparer.Instance);
    public EquipmentPanel Equipment { get; } = new();
    public DropQueue Drops { get; } = new();
    public string CurrentArea { get; private set; }
    public int StashTabs { get; internal set; }
    public int OtherRewards { get; internal set; }
    public Item? LastLooted { get; internal set; }
    public string? EngagedKey { get; private set; }
    internal Monster? Engaged { get; private set; }

    /// Combat and drop rolls run on separate seeded streams (T0c determinism);
    /// monster COUNTER rolls run on a third one (row 69), so the player-side
    /// combat stream stays bit-identical as the counter mechanic evolves.
    internal Rng CombatRng { get; }
    internal Rng DropRng { get; }
    internal Rng EnemyRng { get; }

    private readonly KillFlow _flow;
    private readonly GambleStore _gambler;

    /// The M2 economy band in force (sell caps, vendor terms).
    public Difficulty EconomyBand => Difficulty.Dusk;

    /// <summary>The world band New stamps — optional parameter, default Ordinary
    /// (the M4 demo). This beat reads it ONLY for the death penalty (Respawn);
    /// spawn and pack rolls stay pinned to Ordinary until a world-band adoption
    /// beat, so choosing Hell changes the death cost, nothing else yet.</summary>
    public Difficulty Band { get; }

    public static GameSession New(ulong seed, string classKey, string dataDir,
        Difficulty band = Difficulty.Ordinary) =>
        new(seed, classKey, dataDir, band);

    private GameSession(ulong seed, string classKey, string dataDir, Difficulty band)
    {
        DataDir = dataDir;
        Seed = seed;
        Band = band;
        string D(string file) => Path.Combine(dataDir, file);
        var profile = Stats.For(classKey); // unknown class → ArgumentException
        Ladder = XpTable.Load(D("xp-ladder.json"));
        SkillData = SkillDataLoader.Load(D($"{profile.Key}-skills.json"));
        Character = new Character(profile, SkillData.ClassName);
        Skills = new SkillTree(SkillData);
        Bases = ItemBaseTable.Load(D("item-bases.json"));
        var affixes = AffixPool.Load(D("affixes.json"));
        var uniques = UniqueSetTable.Load(D("uniques.json"));
        Monsters = MonsterTable.Load(D("monsters.json"));
        Classes = TreasureClassTable.Load(D("treasureclasses.json"));
        Areas = AreaGraph.Load(D("act1-areas.json"));
        Waypoints = WaypointGraph.Load(Areas, D("waypoints.json"));
        Quests = QuestLog.Load(D("act1-quests.json"));
        _flow = new KillFlow(Classes, new ItemForge(Bases, affixes, uniques), Drops);
        _gambler = new GambleStore(Bases, uniques, affixes);
        Purse = new Purse(StartGold);
        CurrentArea = Areas.TownKey;
        Waypoints.Discover(Areas.TownKey); // the town stone lights on arrival
        CombatRng = new Rng(seed);
        DropRng = new Rng(seed ^ 0x9E3779B97F4A7C15UL);
        EnemyRng = new Rng(seed ^ 0x2545F4914F6CDD1DUL);
        Packs = PackData.Load(D("packs.json"));
    }

    public int Level => Character.Level;
    public long Gold => Purse.Amount;

    /// The scripted sim's staff-strike envelope — melee rules join with the
    /// skills phase; commands need one deterministic damage source meanwhile.
    public int AttackDamageMin => 2 + Character.Level / 2;
    public int AttackDamageMax => 6 + 2 * Character.Level;

    /// Summed item mods across everything carried or worn. MF feeds quality only
    /// (never the walk, P2 §4.8); GF feeds the gold pile, never quality.
    public int MagicFind => Carried.Concat(Equipment.Equipped.Values).Sum(i => i.ModSum("magic_find"));
    public int GoldFind => Carried.Concat(Equipment.Equipped.Values).Sum(i => i.ModSum("gold_find"));

    // ---- commands (each either fully applies or throws SessionRejectedException) ----

    public void LevelTo(int target)
    {
        if (target <= Character.Level)
            throw new SessionRejectedException($"levelto {target} cannot lower or repeat level {Character.Level}");
        if (target > Ladder.MaxLevel)
            throw new SessionRejectedException($"levelto {target} is above the ladder cap {Ladder.MaxLevel}");
        Character.GrantXp(Ladder.TotalXpForLevel(target) - Character.Xp, Ladder);
    }

    public void SpendSkill(string skillId) => Skills.Spend(Character, skillId);

    public sealed record AttackReport(int Strikes, int Kills, long Xp, int DropsPending, long GoldPending);

    /// Strikes the named monster row (fresh spawn per kill; switching target
    /// abandons the current fight — no flee rules exist yet). Each kill runs the
    /// KillFlow chain: XP already paid by the resolver, event + walk + queue.
    /// A monster that survives the strike counters once (row 69 — EnemyRng only).
    /// The DEAD gate opens the command and stops the volley: a counter that lands
    /// the killing blow ends this call mid-times, and no strike is paid from a
    /// corpse — the report carries the strikes that actually happened.
    public AttackReport Attack(string monsterKey, int times)
    {
        RaiseIfDead();
        if (times < 1) throw new SessionRejectedException("attack times must be at least 1");
        var row = Monsters.Rows.FirstOrDefault(r => r.Key == monsterKey)
            ?? throw new SessionRejectedException($"unknown monster '{monsterKey}'");
        if (Engaged is not null && EngagedKey != monsterKey) { Engaged = null; EngagedKey = null; }
        var area = Areas.Get(CurrentArea);
        int strikes = 0, kills = 0;
        long xp = 0;
        for (int i = 0; i < times; i++)
        {
            if (IsDead) break; // died to a counter: the DEAD gate stops the volley mid-call
            Engaged ??= Spawn(row, area);
            var (outcome, _) = _flow.Strike(CombatRng, DropRng, Character, Engaged, Ladder,
                AttackDamageMin, AttackDamageMax, TcKeyFor(area.Level), EngagedKind, EngagedAffixKeys,
                area.Level, AreaSeed(area), MagicFind, GoldFind);
            strikes++;
            if (outcome.Killed) { kills++; xp += outcome.XpGranted; Engaged = null; EngagedKey = null; }
            else CounterStrike(Engaged!); // the survivor hits back once (row 69)
        }
        return new AttackReport(strikes, kills, xp, Drops.Pending, Drops.Gold);
    }

    public sealed record LootReport(int Taken, long GoldCollected, IReadOnlyList<Item> Items);

    /// Takes up to count drops the pack can still hold (a full pack never
    /// swallows the rest) and collects the gold pile into the purse.
    public LootReport Loot(int count)
    {
        RaiseIfDead();
        if (count < 1) throw new SessionRejectedException("loot count must be at least 1");
        var taken = Drops.TakeUpTo(count, TryCarry);
        long gold = Drops.CollectGold();
        Purse.Receive(gold);
        return new LootReport(taken.Count, gold, taken);
    }

    /// Wears the most recently looted item in the first slot its type allows.
    public Item EquipLastDrop()
    {
        var item = LastLooted ?? throw new SessionRejectedException("nothing looted yet to equip");
        var slots = EquipSlots.For(item.Base);
        if (slots.Count == 0)
            throw new SessionRejectedException($"'{item.DisplayName}' is not wearable");
        var slot = slots.OrderBy(s => (int)s).First();
        Grid.Remove(item);
        Carried.Remove(item);
        CarriedSpots.Remove(item);
        if (!Equipment.Equip(item, slot)) throw new SessionRejectedException("the equipment panel refused the item");
        return item;
    }

    public long Buy(string baseId)
    {
        RaiseIfDead();
        var baseRow = Bases.Find(baseId) ?? throw new SessionRejectedException($"item base '{baseId}' unknown");
        long price = Vendor.BuyPrice(baseRow, Vendor.Standard);
        if (!Purse.TrySpend(price))
            throw new SessionRejectedException($"not enough gold: {Purse.Amount} below the price {price}");
        var item = new Item(baseRow, itemLevel: Character.Level);
        if (!TryCarry(item))
        {
            Purse.Receive(price); // nothing sold into a full pack — the coin returns
            throw new SessionRejectedException("the pack has no cell left");
        }
        return price;
    }

    public long SellLast()
    {
        RaiseIfDead();
        if (Carried.Count == 0) throw new SessionRejectedException("nothing carried to sell");
        var item = Carried[^1];
        long price = Vendor.SellPrice(item, EconomyBand);
        Grid.Remove(item);
        Carried.RemoveAt(Carried.Count - 1);
        CarriedSpots.Remove(item);
        Unbelt(item); // a sold potion must never leave a filled belt slot behind
        if (ReferenceEquals(LastLooted, item)) LastLooted = Carried.Count > 0 ? Carried[^1] : null;
        Purse.Receive(price);
        return price;
    }

    public (long Price, ItemQuality Quality) Gamble()
    {
        RaiseIfDead();
        var offer = _gambler.Next(DropRng, Character.Level);
        var item = _gambler.Buy(DropRng, offer, Purse)
            ?? throw new SessionRejectedException($"cannot afford the gamble: {offer.Price} gold");
        if (!TryCarry(item))
        {
            Purse.Receive(offer.Price); // refunded like the vendor path: no room, no sale
            throw new SessionRejectedException("the pack has no cell left");
        }
        return (offer.Price, item.Quality);
    }

    // ---- shared helpers ----

    private Monster Spawn(MonsterBaseRow row, AreaRow area)
    {
        EngagedKey = row.Key;
        EngagedKind = SpecialKind.Plain;
        EngagedAffixKeys = [];
        Engaged = Monsters.Spawn(row, MonsterTable.ResolveSpawnLevel(row, Difficulty.Ordinary, area));
        return Engaged;
    }

    /// One counter by the engaged survivor (row 69): its OWN to-hit row and damage
    /// dice (P3 §5.1 columns), rolled on the third stream so player-side streams
    /// keep their offsets — the same separation DropRng exists for. The defense
    /// term is the hero's own Attack Rating (P1 §1.4 convention); a counter that
    /// empties the Life pool kills the hero — the DEAD gate and Respawn live in
    /// GameSession.Death.cs (row 64).
    private void CounterStrike(Monster attacker)
    {
        if (attacker.DamageMax < 1) return; // dice-less foundation builds never counter
        if (!CombatResolver.RollHit(EnemyRng, attacker.AttackRating, Character.AttackRating,
                attacker.Level, Character.Level))
            return;
        Character.TakeDamage(EnemyRng.NextInRange(attacker.DamageMin, attacker.DamageMax));
    }

    private bool TryCarry(Item item)
    {
        for (int r = 0; r < InventoryGrid.Rows; r++)
            for (int c = 0; c < InventoryGrid.Columns; c++)
                if (Grid.CanPlace(item, r, c))
                {
                    Grid.TryPlace(item, r, c);
                    Carried.Add(item);
                    CarriedSpots[item] = (r, c);
                    LastLooted = item;
                    return true;
                }
        return false;
    }

    /// A plain area seed for the death event: stable across processes.
    internal int AreaSeed(AreaRow area) =>
        unchecked((int)(Seed ^ ((uint)area.Level << 8) ^ 0x5EED));

    /// Area-level → the class table's offering tier (the content map for M1).
    internal string TcKeyFor(int areaLevel) =>
        areaLevel <= 3 ? "lup_lezny" : areaLevel <= 6 ? "lup_kopcowy" : "skarb_leszego";

}
