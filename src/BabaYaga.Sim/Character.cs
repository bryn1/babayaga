namespace BabaYaga.Sim;

/// <summary>
/// A playable character: class profile, level/XP state, attribute allocation, and the
/// derived numbers the engine reads (life/mana/AR/block factor). XP intake drives the
/// level-up grants (5 stat + 1 skill point, P1 §1.1); combat never writes level directly.
/// </summary>
public sealed class Character
{
    public ClassProfile Profile { get; }
    public string Name { get; }

    public int Level { get; private set; } = 1;
    public long Xp { get; private set; }

    /// Unspent reward pools (P1 §1.1: 5 stat + 1 skill point per level).
    public int StatPoints { get; private set; }
    public int SkillPoints { get; private set; }

    /// Allocated attribute points (1 point = +1 attribute, P1 §1.1).
    public int StrAlloc { get; private set; }
    public int DexAlloc { get; private set; }
    public int VitAlloc { get; private set; }
    public int EnerAlloc { get; private set; }

    /// Item-sourced attributes (M2 items will set these; foundation defaults to zero).
    public int StrFromItems { get; set; }
    public int DexFromItems { get; set; }
    public int VitFromItems { get; set; }
    public int EnerFromItems { get; set; }

    /// Multiplier slots and flat bonuses the formulas consume (P1 §1.3).
    public long FlatLife { get; set; }
    public long FlatMana { get; set; }
    public int IncreaseMaxLifePercent { get; set; }
    public int IncreaseMaxManaPercent { get; set; }

    /// <summary>The hero's worn panel, assigned ONCE by the owning GameSession
    /// (the panel object itself, never a copy). Mana item terms are DERIVED at
    /// the read through it — the Hireling.MaxLife GearStat precedent (M4-S16,
    /// §6.7c): never a save field, save format v7 UNCHANGED. Null for a
    /// standalone Character (CLI debug verbs, unit fixtures) — it derives
    /// gear-free, exactly as before this row.</summary>
    public EquipmentPanel? WornGear { get; set; }

    /// <summary>The CURRENT pools the belt potions raise (LEDGER row 69) — start
    /// full, lowered by TakeDamage, floored at 0. The setter is the restore/
    /// authoring door (the same public-setter idiom as Monster.Life and FlatLife);
    /// MaxLife/MaxMana stay computed formulas (rows 4–5, untouched here).</summary>
    public long Life { get; set; }
    public long Mana { get; set; }

    /// Weapon/AR inputs for derived Attack Rating (P1 §1.4).
    public long WeaponBaseAr { get; set; }
    public long FlatAr { get; set; }
    public int ArPercent { get; set; }

    /// Shield block base chance from the shield row (0 = no shield → no block, P1 §1.5).
    public int ShieldBlockBase { get; set; }
    public int BlockBonus { get; set; }

    public Character(ClassProfile profile, string name = "")
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Name = name;
        Life = MaxLife; // the session's hero arrives full; the pools ride saves (DTO v4)
        Mana = MaxMana;
    }

    /// <summary>Monster counter-damage (LEDGER row 69): the pool floors at 0 —
    /// and hitting 0 IS the hero's death (row 64, M4-S6): IsDead reads FROM the
    /// pool, so this floor and the DEAD flag are the same fact. Only TakeDamage
    /// is the pinned trigger; the setter stays the restore/authoring door.</summary>
    public void TakeDamage(long amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Life = Math.Max(0, Life - amount);
    }

    /// <summary>DEAD as the pool reads (row 64): Life at 0. The session mirrors
    /// this on GameSession.IsDead; Respawn clears it by refilling the pool.</summary>
    public bool IsDead => Life == 0;

    /// <summary>Death-penalty landing door (row 64): the session writes the
    /// XpAfter the DeathPenalty helper COMPUTED — the door never re-derives the
    /// percent. The helper clamps at the level's start, so a level can never
    /// drop; the save path keeps its own GrantXp replay untouched.</summary>
    internal void ApplyDeathXp(long xpAfter)
    {
        if (xpAfter < 0 || xpAfter > Xp)
            throw new ArgumentOutOfRangeException(nameof(xpAfter));
        Xp = xpAfter;
    }

    public int TotalStr => Profile.StartStr + StrAlloc + StrFromItems;
    public int TotalDex => Profile.StartDex + DexAlloc + DexFromItems;
    public int TotalVit => Profile.StartVit + VitAlloc + VitFromItems;
    public int TotalEner => Profile.StartEner + EnerAlloc + EnerFromItems;

    public long MaxLife => Stats.MaxLife(
        Profile, Level, VitAlloc, VitFromItems, FlatLife, IncreaseMaxLifePercent);

    /// Σ of one mod stat over WORN gear only — the Hireling.GearStat summing
    /// idiom (the same worn-only law the merc panel obeys: carried feeds
    /// nothing; SumMod's carried+worn door stays the MF/GF/regen-% door).
    private int WornStat(string stat) =>
        WornGear is null ? 0 : WornGear.Equipped.Values.Sum(i => i.ModSum(stat));

    /// <summary>The max-mana derivation with the row-5 ITEM TERMS (P1 §1.3, the
    /// mana twin of the quoted life formula — "item Energy and the per-level
    /// +Mana are added AFTER the Increase-Max-Mana % multiplier"; VERIFIED,
    /// d2r.org/mechanics/mana: "Increase Maximum Mana … multiplies your base
    /// mana … It does not multiply mana from Energy on items or the per-level
    /// +Mana"). Flat "+Mana" from worn gear rides Stats.BaseMana's flat term —
    /// INSIDE the parens, so the % multiplies it; "+Energy" from worn gear joins
    /// AFTER the multiplier as ManaPerEner × Ener. Both sums are DERIVED at the
    /// read from WornGear (never a save field); they ADD to the public authoring
    /// fields (save-restore and quest flats keep their own doors).</summary>
    public long MaxMana => Stats.MaxMana(
        Profile, Level, EnerAlloc,
        EnerFromItems + WornStat("energy"),
        FlatMana + WornStat("mana"),
        IncreaseMaxManaPercent);

    public long AttackRating => Stats.AttackRating(TotalDex, WeaponBaseAr, FlatAr, ArPercent);

    /// Adds XP and pays out every level crossed (ladder-driven, multi-level safe).
    /// Level 99 gains nothing further (P1 §1.7). Returns levels gained.
    public int GrantXp(long amount, XpTable ladder)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (Level >= ladder.MaxLevel) return 0;
        Xp += amount;
        int gained = 0;
        while (Level < ladder.MaxLevel && Xp >= ladder.TotalXpForLevel(Level + 1))
        {
            Level++;
            StatPoints += ladder.StatGrantPerLevel;
            SkillPoints += ladder.SkillGrantPerLevel;
            gained++;
        }
        return gained;
    }

    /// Moves points from the reward pool into an attribute. Returns false when the
    /// pool cannot cover the request (invalid spend is rejected, never debt).
    public bool TryAllocateStat(AllocatableStat stat, int points)
    {
        if (points <= 0 || points > StatPoints) return false;
        switch (stat)
        {
            case AllocatableStat.Strength: StrAlloc += points; break;
            case AllocatableStat.Dexterity: DexAlloc += points; break;
            case AllocatableStat.Vitality: VitAlloc += points; break;
            case AllocatableStat.Energy: EnerAlloc += points; break;
            default: throw new ArgumentOutOfRangeException(nameof(stat));
        }
        StatPoints -= points;
        return true;
    }

    /// <summary>Adds EARNED reward skill points to the spendable pool (P1 §1.1's
    /// 5 quest pts, DA round-1 P1-2). Debit stays exclusively TrySpendSkillPoint's.</summary>
    public void GrantSkillPoints(int points)
    {
        if (points < 0) throw new ArgumentOutOfRangeException(nameof(points));
        SkillPoints += points;
    }

    public bool TrySpendSkillPoint()
    {
        if (SkillPoints <= 0) return false;
        SkillPoints--;
        return true;
    }

    /// <summary>Serializer restore hook (DA round-2 P1-N1, same pattern as the
    /// session's RestorePurse): the pool lands AS SAVED. SaveSerializer is the only
    /// caller; the game-rule doors stay GrantSkillPoints/TrySpendSkillPoint.</summary>
    internal void RestoreSkillPoints(int points)
    {
        if (points < 0) throw new ArgumentOutOfRangeException(nameof(points));
        SkillPoints = points;
    }
}

/// <summary>The four allocatable primary attributes (P1 §1.1).</summary>
public enum AllocatableStat
{
    Strength,
    Dexterity,
    Vitality,
    Energy,
}
