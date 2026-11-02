using BabaYaga.Sim;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// LEDGER ROW 4 (MC 11950) — the life formula's WORN-ITEM TERMS wired into the
/// real derivation at the read, mirroring the merged mana idiom (MC 11949) and
/// the Hireling.MaxLife gear-stat precedent (M4-S16, §6.7c).
/// THE ORACLE LINES IMPLEMENTED (T0b-mechanics-spec P1 §1.3, quoted verbatim):
///   "max_life    = (base_life + flat "+Life") × (1 + IncreaseMaxLife% / 100)
///                   + life_per_vit × vit_from_items + "+Life (based on character level)""
///   VERIFIED — https://d2r.org/mechanics/life-and-vitality — "maximum life =
///   (base life + flat "+X to Life") × (1 + "Increase Maximum Life %" ÷ 100) +
///   life per Vitality × Vitality from items + "+Life (based on character level) "".
/// So the spec DOES place the worn terms: flat "+Life" sits INSIDE the
/// pre-multiplier parens (it rides Stats.BaseLife's flat term into × (1 +
/// IncreaseMaxLife%)); worn "+Vit" IS the "Vitality from items" term — it joins
/// AFTER the multiplier as LifePerVit × vit and is never multiplied. No
/// placement guesswork here, no ORIGINAL DESIGN fence: the placement is the
/// quoted spec line, the same shape the mana twin already obeys. The
/// "+Life (based on character level)" term has NO data row and no Stats
/// parameter (data-future, named not invented).
/// THE WIRING (this card): the hero's WORN panel is the supplier —
/// Character.WornGear (the GameSession's own Equipment panel, assigned at
/// construction) — both terms DERIVED at the read: never a save field, save
/// format v7 UNCHANGED. Carried gear feeds NOTHING (worn-only, the merc law);
/// a no-gear hero's MaxLife never moves (anchors: story bytes, CLI seed-42/43
/// journey + no-gear dumps, save bytes — all cmp EMPTY base-vs-card).
/// DATA STATE (measured): data/affixes.json carries BOTH rows — "life" (1,10-20
/// / 18,21-40) and "vit" (3,3-6) — unlike the mana card's energy precedent the
/// doors here are code-live AND data-live; the tests still author the exact
/// values through the public Item.Mods door to pin the arithmetic.
/// RNG LAW: zero new rolls. CURRENT Life is damage state — equipping raises the
/// MAX only, the pool never re-tops upward (the D2 behavior, pinned below).
/// </summary>
public sealed class LifeItemTermTests
{
    private const string Ring = "pierscien"; // data/item-bases.json itype "rings"

    private static GameSession Fresh(ulong seed = 42) =>
        GameSession.New(seed, "koldun", TestData.DataDir());

    /// Author a plain ring carrying one stat line through the public mods door.
    private static Item LifeRing(GameSession s, string stat, int value)
    {
        var item = new Item(s.Bases.For(Ring), 1);
        item.Mods.Add(new ItemMod(stat, value));
        return item;
    }

    // ---- (a) TERM ARITHMETIC — the pure formula rows, spec line quoted ----

    [Fact]
    public void BaseLife_FlatLifeTerm_RidesInsideTheParens()
    {
        // §1.3 shape: base_life = life@1 + LifePerLevel×(level−1) + LifePerVit×alloc + flat "+Life".
        // Koldun (Stats.cs design row): life@1 = StartVit 20 + flat 30 = 50, LifePerLevel 2.
        Assert.Equal(50, Stats.BaseLife(Stats.Koldun, 1, 0, 0));           // "Base life at level 1 = Vitality + flat 30" (§1.2, VERIFIED)
        Assert.Equal(90, Stats.BaseLife(Stats.Koldun, 1, 0, 40));          // 50 + 40 flat "+Life"
        Assert.Equal(68, Stats.BaseLife(Stats.Koldun, 10, 0, 0));          // 50 + 2×9 per level
        Assert.Equal(60, Stats.BaseLife(Stats.Koldun, 1, 5, 0));           // 50 + 2×5 allocated Vit (LifePerVit 2)
        Assert.Equal(118, Stats.BaseLife(Stats.Koldun, 10, 5, 40));         // 50 + 18 + 10 + 40 — the four terms, one sum
    }

    [Fact]
    public void MaxLife_Multiplier_MultipliesTheFlatLifeTermViaBaseLife()
    {
        // The quoted line: "(base life + flat "+X to Life") × (1 + "Increase Maximum Life %" ÷ 100)"
        // — the flat "+Life" is INSIDE the parens: (50 + 40) × (100 + 50) / 100 = 135.
        Assert.Equal(135, Stats.MaxLife(Stats.Koldun, 1, 0, 0, 40, 50));
        // Multipliers floor to integer (§1.3 header): (51) × 150/100 = 76.5 → 76.
        Assert.Equal(76, Stats.MaxLife(Stats.Koldun, 1, 0, 0, 1, 50));
    }

    [Fact]
    public void MaxLife_VitFromItems_JoinsAfterTheMultiplier_NeverMultiplied()
    {
        // THE STRUCTURE ROW — "+ life_per_vit × vit_from_items" sits AFTER the ×
        // (1 + IncreaseMaxLife%) in the quoted line ("life per Vitality × Vitality
        // from items" is a post-multiplier addend).
        // base 50 ×1.5 = 75, THEN LifePerVit(2) × 10 worn Vit = 20 ⇒ 95.
        Assert.Equal(95, Stats.MaxLife(Stats.Koldun, 1, 0, 10, 0, 50));
        // At 0 % the same input is 70 — the delta the % would have wrongly paid is 5:
        // post-multiplier placement pinned by the arithmetic, not by shape alone.
        Assert.Equal(70, Stats.MaxLife(Stats.Koldun, 1, 0, 10, 0, 0));
        // Both terms at once: (50+40)×1.5 = 135, + 2×10 = 155 — flat INSIDE, Vit OUTSIDE.
        Assert.Equal(155, Stats.MaxLife(Stats.Koldun, 1, 0, 10, 40, 50));
    }

    // ---- (b) CALL SITES — the derivation READS through Stats.BaseLife from gear ----

    [Fact]
    public void WornLifeItem_MovesDerivedLife_ByExactSpecArithmetic()
    {
        var s = Fresh();
        Assert.Equal(50, s.Character.MaxLife); // level-1 koldun: 20 Vit + flat 30, no gear
        var ring = LifeRing(s, "life", 40);    // the shipped Item.Mods authoring door
        Assert.True(s.Equipment.Equip(ring, EquipSlot.RingLeft));
        // WORN +40 flat "+Life" rides BaseLife's flat term → (50+40)×(100+0)/100 = 90.
        Assert.Equal(90, s.Character.MaxLife);
        // With the % door open (public authoring setter) the SAME worn item moves
        // the pool by the ×1.5 arithmetic — the proof the derivation reads through
        // BaseLife (pre-multiplier), not beside it: 50×1.5=75 → (50+40)×1.5=135.
        s.Character.IncreaseMaxLifePercent = 50;
        Assert.Equal(135, s.Character.MaxLife);
        // DERIVED, never cached: strip the ring and the number walks back on read.
        Assert.True(s.Equipment.Unequip(EquipSlot.RingLeft));
        Assert.Equal(75, s.Character.MaxLife);
    }

    [Fact]
    public void WornVitalityItem_RaisesLife_PostMultiplier_EvenWhilePercentIsOpen()
    {
        var s = Fresh();
        s.Character.IncreaseMaxLifePercent = 50;
        Assert.Equal(75, s.Character.MaxLife); // 50 × 1.5, no gear
        Assert.True(s.Equipment.Equip(LifeRing(s, "vit", 10), EquipSlot.RingLeft));
        // 75 + LifePerVit(2)×10 = 95 — the +20 is NOT multiplied (quoted line above).
        Assert.Equal(95, s.Character.MaxLife);
    }

    [Fact]
    public void NoGearHero_MaxLifeStaysPut_CarriedGearFeedsNothing()
    {
        var s = Fresh();
        var before = s.Character.MaxLife;
        // A life ring CARRIED, bought through the shipped vendor door and given the
        // stat through the mods door, still sits in the pack: worn-only law — a
        // carried life item feeds NOTHING (the same law the merc panel obeys).
        s.Buy(Ring);
        var loose = s.Carried[^1];
        loose.Mods.Add(new ItemMod("life", 40));
        Assert.NotEmpty(s.Carried);                   // it IS carried…
        Assert.Empty(s.Equipment.Equipped);           // …and nothing is worn
        Assert.Equal(before, s.Character.MaxLife);    // carried: unmoved
        Assert.Equal(before, s.Character.Life);       // the pool unmoved too
        // And the no-gear twin never moves across arithmetic:
        var twin = Fresh();
        Assert.Equal(50, twin.Character.MaxLife);
    }

    // ---- (c) CURRENT LIFE — damage state, never re-topped by equipping ----

    [Fact]
    public void Equipping_RaisesMaxOnly_CurrentLifePoolStaysPut()
    {
        // RNG LAW row: the CURRENT pool is damage state (row 69) — the derivation
        // is a READ; equipping raises the ceiling, the pool never rides up (D2:
        // +max to life does not heal). MaxLife and Life are separate doors.
        var s = Fresh();
        s.Character.TakeDamage(10);
        Assert.Equal(40, s.Character.Life);
        Assert.True(s.Equipment.Equip(LifeRing(s, "life", 40), EquipSlot.RingLeft));
        Assert.Equal(90, s.Character.MaxLife); // the MAX moved…
        Assert.Equal(40, s.Character.Life);    // …the CURRENT pool did not
    }

    // ---- (d) LEVEL-UP — the derived worn term SURVIVES the level recompute ----

    [Fact]
    public void WornLifeTerm_SurvivesLevelUpRecompute_MercPrecedentIdiom()
    {
        // The Hireling.MaxLife §6.7c law: "the level-up recompute rewrites the
        // base list only, so the gear bonus survives it" — the hero twin: MaxLife
        // is recomputed from the NEW level through the same read, and the worn
        // term is still in it. Koldun L5 base: 50 + 2×4 = 58; worn +40 INSIDE the
        // parens at 0 %: 58 + 40 = 98 (if the term had vanished on recompute this
        // reads 58; if it sat post-multiplier the 50 % leg below would read 87).
        var s = Fresh();
        Assert.True(s.Equipment.Equip(LifeRing(s, "life", 40), EquipSlot.RingLeft));
        s.LevelTo(5);
        Assert.Equal(98, s.Character.MaxLife);
        s.Character.IncreaseMaxLifePercent = 50; // post-recompute placement still pinned
        Assert.Equal(147, s.Character.MaxLife);  // (58+40)×1.5 = 147, floor
        Assert.Equal(50, s.Character.Life);      // and the pool still never rode up (hero has no level-up heal — merc rule §4 Q3)
    }
}
