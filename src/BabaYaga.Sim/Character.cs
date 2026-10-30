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
    }

    public int TotalStr => Profile.StartStr + StrAlloc + StrFromItems;
    public int TotalDex => Profile.StartDex + DexAlloc + DexFromItems;
    public int TotalVit => Profile.StartVit + VitAlloc + VitFromItems;
    public int TotalEner => Profile.StartEner + EnerAlloc + EnerFromItems;

    public long MaxLife => Stats.MaxLife(
        Profile, Level, VitAlloc, VitFromItems, FlatLife, IncreaseMaxLifePercent);

    public long MaxMana => Stats.MaxMana(
        Profile, Level, EnerAlloc, EnerFromItems, FlatMana, IncreaseMaxManaPercent);

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

    public bool TrySpendSkillPoint()
    {
        if (SkillPoints <= 0) return false;
        SkillPoints--;
        return true;
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
