using BabaYaga.Sim;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// ROW 5 (ARCH-verdict §4 P2-1, closed by MC 11949) — the mana formula's ITEM
/// TERMS wired into the real derivation through Stats.BaseMana and pinned.
/// THE ORACLE LINES IMPLEMENTED (T0b-mechanics-spec P1 §1.3, the life/mana pair
/// + its mana twin, quoted verbatim):
///   "max_life    = (base_life + flat "+Life") × (1 + IncreaseMaxLife% / 100)
///                   + life_per_vit × vit_from_items + "+Life (based on character level)""
///   "Mana: same shape with the Energy columns (start mana = Energy; item Energy
///    and the per-level +Mana are added AFTER the Increase-Max-Mana % multiplier)."
///   VERIFIED — https://d2r.org/mechanics/mana: "Increase Maximum Mana … multiplies
///   your base mana … It does not multiply mana from Energy on items or the
///   per-level +Mana."
/// So the flat "+Mana" item term sits INSIDE the pre-multiplier parens — it rides
/// Stats.BaseMana's flat term into × (1 + IncreaseMaxMana%); the item-Energy term
/// (ManaPerEner × Ener-from-items) joins AFTER the multiplier and is never
/// multiplied. THE WIRING (this card): the hero's WORN panel is the supplier —
/// Character.WornGear (the GameSession's own Equipment panel, assigned at
/// construction) — and both terms are DERIVED at the read, mirroring
/// Hireling.MaxLife's GearStat precedent (M4-S16/11914): never a save field,
/// save format v7 UNCHANGED. Carried gear feeds NOTHING (worn-only — the same
/// law the merc panel obeys); a no-gear hero's MaxMana never moves (anchors:
/// story bytes, CLI seed-42/43 dumps, save bytes — all captured on the BASE
/// build, all cmp EMPTY after the wire).
/// THE REGEN TWIN (AMENDED §1.8 block, MC 11873, quoted): "each frame adds the
/// whole part of maximum mana in 256ths ÷ (25 × ManaRegen), at least 1, times
/// (100 + Regenerate Mana %) ÷ 100, again floored" and "Energy raises regen
/// ONLY via max mana — the quantum is a share of the CURRENT pool, never a
/// cached constant" — so a pool raised by worn gear raises the quantum through
/// the SAME live MaxMana read (the rest leg below pins the arithmetic).
/// DATA STATE (measured): data/affixes.json carries the "mana" stat rows
/// (5,5-12 / 20,20-40 / 41,41-70) and "energy" carries NO data row today — the
/// energy door is the §1.8 RegenManaPercent precedent ("the code path is live
/// the day an item carries the mod"), exercised here through the public
/// Item.Mods authoring door exactly as HirelingStatTests authors gear stats.
/// RNG LAW: zero new rolls — every row here is pure arithmetic and pool reads.
/// </summary>
public sealed class ManaItemTermTests
{
    private const string Ring = "pierscien"; // data/item-bases.json itype "rings"

    private static GameSession Fresh(ulong seed = 42) =>
        GameSession.New(seed, "koldun", TestData.DataDir());

    /// Author a plain ring carrying one stat line through the public mods door.
    private static Item ManaRing(GameSession s, string stat, int value)
    {
        var item = new Item(s.Bases.For(Ring), 1);
        item.Mods.Add(new ItemMod(stat, value));
        return item;
    }

    // ---- (a) TERM ARITHMETIC — the pure formula rows, spec structure quoted ----

    [Fact]
    public void BaseMana_FlatTerm_RidesInsideTheParens()
    {
        // §1.3 shape: base_mana = mana@1 + ManaPerLevel×(level−1) + ManaPerEner×alloc + flat "+Mana".
        // Koldun (Stats.cs design row): mana@1 = StartEner 30, ManaPerLevel 2.
        Assert.Equal(30, Stats.BaseMana(Stats.Koldun, 1, 0, 0));           // starting mana is the Energy (§1.2)
        Assert.Equal(70, Stats.BaseMana(Stats.Koldun, 1, 0, 40));          // 30 + 40 flat "+Mana"
        Assert.Equal(48, Stats.BaseMana(Stats.Koldun, 10, 0, 0));          // 30 + 2×9 per level
        Assert.Equal(40, Stats.BaseMana(Stats.Koldun, 1, 5, 0));           // 30 + 2×5 allocated Ener (ManaPerEner 2)
        Assert.Equal(98, Stats.BaseMana(Stats.Koldun, 10, 5, 40));         // 30 + 18 + 10 + 40 — the four terms, one sum
    }

    [Fact]
    public void MaxMana_Multiplier_MultipliesTheFlatTermViaBaseMana()
    {
        // "Increase Maximum Mana … multiplies your base mana" (§1.3 VERIFIED line) —
        // and the flat "+Mana" is part of base mana (inside the parens):
        // (30 + 40) × (100 + 50) / 100 = 105.
        Assert.Equal(105, Stats.MaxMana(Stats.Koldun, 1, 0, 0, 40, 50));
        // Multipliers floor to integer (§1.3 header): (31) × 150/100 = 46.5 → 46.
        Assert.Equal(46, Stats.MaxMana(Stats.Koldun, 1, 0, 0, 1, 50));
    }

    [Fact]
    public void MaxMana_EnergyFromItems_JoinsAfterTheMultiplier_NeverMultiplied()
    {
        // THE STRUCTURE ROW — "item Energy … are added AFTER the Increase-Max-Mana %
        // multiplier" / "It does not multiply mana from Energy on items" (§1.3, VERIFIED).
        // base 30 + 0 flat ×1.5 = 45, THEN ManaPerEner(2) × 10 worn Ener = 20 ⇒ 65.
        Assert.Equal(65, Stats.MaxMana(Stats.Koldun, 1, 0, 10, 0, 50));
        // At 0 % the same input is 50 — the delta the % would have wrongly paid is 10:
        // post-multiplier placement pinned by the arithmetic, not by shape alone.
        Assert.Equal(50, Stats.MaxMana(Stats.Koldun, 1, 0, 10, 0, 0));
        // Both terms at once: (30+40)×1.5 = 105, + 2×10 = 125 — flat INSIDE, Energy OUTSIDE.
        Assert.Equal(125, Stats.MaxMana(Stats.Koldun, 1, 0, 10, 40, 50));
    }

    // ---- (b) CALL SITES — the derivation READS through Stats.BaseMana from gear ----

    [Fact]
    public void WornManaItem_MovesDerivedMana_ByExactSpecArithmetic()
    {
        var s = Fresh();
        Assert.Equal(30, s.Character.MaxMana); // level-1 koldun: mana@1 = 30, no gear
        var ring = ManaRing(s, "mana", 40);    // the shipped Item.Mods authoring door
        Assert.True(s.Equipment.Equip(ring, EquipSlot.RingLeft));
        // WORN +40 flat "+Mana" rides BaseMana's flat term → (30+40)×(100+0)/100 = 70.
        Assert.Equal(70, s.Character.MaxMana);
        // With the % door open (public authoring setter) the SAME worn item moves
        // the pool by the ×1.5 arithmetic — the proof the derivation reads through
        // BaseMana (pre-multiplier), not beside it: 30×1.5=45 → (30+40)×1.5=105.
        s.Character.IncreaseMaxManaPercent = 50;
        Assert.Equal(105, s.Character.MaxMana);
        // DERIVED, never cached: strip the ring and the number walks back on read.
        Assert.True(s.Equipment.Unequip(EquipSlot.RingLeft));
        Assert.Equal(45, s.Character.MaxMana);
    }

    [Fact]
    public void WornEnergyItem_RaisesMana_PostMultiplier_EvenWhilePercentIsOpen()
    {
        var s = Fresh();
        s.Character.IncreaseMaxManaPercent = 50;
        Assert.Equal(45, s.Character.MaxMana); // 30 × 1.5, no gear
        Assert.True(s.Equipment.Equip(ManaRing(s, "energy", 10), EquipSlot.RingLeft));
        // 45 + ManaPerEner(2)×10 = 65 — the +20 is NOT multiplied (d2r quote above).
        Assert.Equal(65, s.Character.MaxMana);
    }

    [Fact]
    public void NoGearHero_MaxManaStaysPut_CarriedGearFeedsNothing()
    {
        var s = Fresh();
        var before = s.Character.MaxMana;
        // A mana ring CARRIED, bought through the shipped vendor door and given the
        // stat through the mods door, still sits in the pack: worn-only law — a
        // carried mana item feeds NOTHING (the same law the merc panel obeys).
        s.Buy(Ring);
        var loose = s.Carried[^1];
        loose.Mods.Add(new ItemMod("mana", 40));
        Assert.NotEmpty(s.Carried);                   // it IS carried…
        Assert.Empty(s.Equipment.Equipped);           // …and nothing is worn
        Assert.Equal(before, s.Character.MaxMana);    // carried: unmoved
        Assert.Equal(before, s.Character.Mana);       // the pool unmoved too
        // And the no-gear twin never moves across rest/casts of arithmetic:
        var twin = Fresh();
        Assert.Equal(30, twin.Character.MaxMana);
    }

    [Fact]
    public void RegenQuantum_FollowsTheWornRaisedPool_LiveMaxManaRead()
    {
        // AMENDED §1.8 (MC 11873): "the quantum is a share of the CURRENT pool,
        // never a cached constant" — worn +mana raises the pool, the quantum follows.
        // Gear: max 70 → q256 = floor(70×256 / (25×120)) = floor(17920/3000) = 5.
        // 256 frames × 5/256 = exactly 5 mana. No-gear: max 30 → floor(7680/3000)=2 → 2 mana.
        var s = Fresh();
        Assert.True(s.Equipment.Equip(ManaRing(s, "mana", 40), EquipSlot.RingLeft));
        Assert.Equal(70, s.Character.MaxMana);
        s.Character.Mana = 0;
        s.RestTick(256);
        Assert.Equal(5, s.Character.Mana);

        var bare = Fresh();
        bare.Character.Mana = 0;
        bare.RestTick(256);
        Assert.Equal(2, bare.Character.Mana); // the base pool's quantum — no-gear stays put
    }
}
