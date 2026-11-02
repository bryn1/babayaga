using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BabaYaga.Sim.Cli;

namespace BabaYaga.Sim.Tests;

/// <summary>
/// M5-S3a CLI TWINS (MC 11933, plan-c2 S3a — the replay-gate spine head): the
/// four verbs belt_add / use / cast / respawn are THIN calls onto the existing
/// public session doors (AddToBelt / UsePotion / Cast / Respawn) — the MC 11896
/// EngineDoorVerbs idiom: the CLI owns ZERO sim semantics, every gate stays the
/// engine's, and a refusal rides the named SessionRejectedException into the
/// ERROR line + exit 1. Green rows: belt_add moves the carried potion into the
/// belt cell (echo + dump agree), use heals with the exact spec delta (40 % of
/// Max, integer floor, clamped — BeltPotionTests' law), cast pays mana by
/// exactly the data-file cost and reports (a survivor of a cast does NOT
/// counter — Casting.cs header, R1c), respawn raises the hero at full life in
/// town after a SCRIPTED death. Refusal rows: each the ENGINE'S named error and
/// exit 1 (occupied belt cell, empty slot, unknown monster, never-woven weave,
/// respawn while alive). RNG law (DA F4 — replay synthesizes "new koldun" +
/// rest feeds, so the dispatch layer may consume nothing): belt_add/use/respawn
/// consume NO rng — same-seed script twins-vs-none leaves all three rng words
/// equal AND the twin-none run byte-identical to a capture taken FROM THE BASE
/// BUILD 4b47d82 (rng words + save sha256, captured 2026-11-02 in this
/// worktree, pre-S3a). The cast divergence is pinned HONESTLY: the door's own
/// CombatRng ember roll uniform(min..max) advances the combat stream (a kill
/// would also run the KillFlow drop chain on DropRng); the enemy-counter stream
/// advances NEITHER for attacks-none-here nor for a cast survivor (no counter
/// this beat). Seeds pin every scripted journey; expected numbers come from the
/// spec formulas written OUT here, never read back from the implementation.
/// </summary>
public class CliTwinVerbsTests
{
    public const ulong Seed = 5;

    // Base-build captures (4b47d82 CLI, this worktree before S3a, seed 5):
    // S1_OFF = new koldun / buy napitek-zycia / buy napitek-manny / attack
    //          pokrzywnik 12 / loot 20 / dump / save   (life 45, xp 120, gold 894)
    private static readonly (ulong Combat, ulong Drop, ulong Enemy) S1Words =
        (3393193452459425455, 6363588114545810229, 3654954491068147603);
    private const string S1SaveSha = "cdc8a14bafb4f35545ee9fbabbc33e92c2a583cacf28e57934febf4c96d4576c";
    // S2_OFF = new koldun / levelto 2 / spend iskra / dump / save
    // (levelto and spend are pure state moves — the words are the raw seed trio)
    private static readonly (ulong Combat, ulong Drop, ulong Enemy) S2Words =
        (15249948049332889875, 8742784102034189314, 14215131991883657834);
    // S3_OFF = new koldun / attack pokrzywnik 1024 / dump / save   (hero DEAD, life 0)
    private static readonly (ulong Combat, ulong Drop, ulong Enemy) S3Words =
        (17233689509082196882, 2471701537080992276, 6334103099445964418);
    private const string S3SaveSha = "c39eb1ccfa127bbb63d3bc34b4f231ec763e389728b5a48531f1654a48b8cbd1";

    private static (int Exit, List<string> Lines) RunScript(string body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"twin-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, body);
        try
        {
            var output = new StringWriter();
            int exit = CommandRunner.Run(path, Seed, TestData.DataDir(), output);
            return (exit, output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList());
        }
        finally { File.Delete(path); }
    }

    /// Runs a body whose "{0}" is a fresh save path and returns the save's sha256.
    private static (int Exit, List<string> Lines, string SaveSha) RunWithSave(string bodyFormat)
    {
        var save = Path.Combine(Path.GetTempPath(), $"twin-{Guid.NewGuid():N}.save");
        try
        {
            var (exit, lines) = RunScript(string.Format(bodyFormat, save));
            string sha = File.Exists(save)
                ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(save))).ToLowerInvariant()
                : "NO-SAVE";
            return (exit, lines, sha);
        }
        finally { File.Delete(save); }
    }

    private static JsonNode Line(List<string> lines, int i) => JsonNode.Parse(lines[i])!;
    private static IEnumerable<JsonNode> Parsed(List<string> lines) => lines.Select(l => JsonNode.Parse(l)!);
    private static JsonNode Node(List<string> lines, string cmd) =>
        Parsed(lines).First(n => n["CMD"]?.GetValue<string>() == cmd);
    private static List<JsonNode> Dumps(List<string> lines) =>
        Parsed(lines).Where(n => n["CMD"]?.GetValue<string>() == "dump").ToList();
    private static (ulong Combat, ulong Drop, ulong Enemy) Words(JsonNode dump)
    {
        var s = dump["STATE"]!;
        return (s["combatRngState"]!.GetValue<ulong>(),
                s["dropRngState"]!.GetValue<ulong>(),
                s["enemyRngState"]!.GetValue<ulong>());
    }

    // ---- GREEN belt_add: the carried copy lands in the cell, echo + dump agree ----

    [Fact]
    public void BeltAddVerb_MovesCarriedPotion_IntoBeltCell_EchoAndDumpAgree()
    {
        var (exit, lines) = RunScript("new koldun\nbuy napitek-zycia\nbelt_add 0 1\ndump\n");
        Assert.Equal(0, exit);
        var echo = Node(lines, "belt_add");
        Assert.Equal(0, echo["CARRIED"]!.GetValue<int>());
        Assert.Equal(1, echo["SLOT"]!.GetValue<int>());
        Assert.Equal("napitek-zycia", echo["BASE"]!.GetValue<string>());
        Assert.Equal("Napitek Zycia", echo["ITEM"]!.GetValue<string>());
        var belt = Node(lines, "dump")["STATE"]!["belt"]!;
        Assert.Equal(0, belt[0]!.GetValue<int>()); // the cell holds carried index 0
        Assert.Equal(-1, belt[1]!.GetValue<int>()); // the other cells stay empty
        Assert.Equal(-1, belt[3]!.GetValue<int>());
    }

    // ---- GREEN use: the scripted wound heals by the EXACT spec delta ----

    [Fact]
    public void UseVerb_Heals_ExactSpecDelta_AndThePotionAndSlotAreGone()
    {
        var (exit, lines) = RunScript(
            "new koldun\nbuy napitek-zycia\nattack pokrzywnik 12\nbelt_add 0 1\ndump\nuse 1\ndump\n");
        Assert.Equal(0, exit);
        Assert.Equal(45, Dumps(lines)[0]["STATE"]!["life"]!.GetValue<long>()); // scripted wound (base capture: attack 12 costs 5 life)
        var use = Node(lines, "use");
        Assert.Equal(1, use["SLOT"]!.GetValue<int>());
        Assert.Equal("life", use["KIND"]!.GetValue<string>());
        long before = use["BEFORE"]!.GetValue<long>(), after = use["AFTER"]!.GetValue<long>(), max = use["MAX"]!.GetValue<long>();
        Assert.Equal(45, before);
        // The belt law, written OUT (Belt.cs header): 40 % of Max, integer floor,
        // clamped to Max — here it clamps (45 + 20 > 50), which IS the law.
        Assert.Equal(Math.Min(before + max * 40 / 100, max), after);
        Assert.True(after > before, "use did not heal at all");
        var post = Dumps(lines)[1]["STATE"]!;
        Assert.Equal(after, post["life"]!.GetValue<long>()); // the dump agrees
        Assert.Equal(-1, post["belt"]![0]!.GetValue<int>());  // the slot emptied...
        Assert.Empty(post["carried"]!.AsArray());             // ...and the carried copy was consumed
    }

    // ---- GREEN cast: mana pays exactly the data cost; the survivor does NOT counter ----

    [Fact]
    public void CastVerb_PaysTheDataCost_Reports_AndSurvivorDoesNotCounter()
    {
        var (exit, lines) = RunScript(
            "new koldun\nlevelto 2\nspend iskra\ndump\ncast iskra pokrzywnik\ndump\n");
        Assert.Equal(0, exit);
        long cost = TestData.KoldunSkills().Find("iskra")!.ManaCost; // DATA file, never hardcoded (BeltCastTests idiom)
        var cast = Node(lines, "cast");
        Assert.Equal("iskra", cast["SKILL"]!.GetValue<string>());
        Assert.Equal("pokrzywnik", cast["MONSTER"]!.GetValue<string>());
        long manaBefore = cast["MANA"]!.GetValue<long>(), manaAfter = cast["MANA_AFTER"]!.GetValue<long>();
        Assert.Equal(manaBefore - cost, manaAfter); // the debit is EXACTLY the row's cost
        var def = TestData.KoldunSkills().Find("iskra")!;
        long damage = cast["DAMAGE"]!.GetValue<long>();
        Assert.InRange(damage, def.DamageMin, def.DamageMax); // iskra carries no synergy — raw roll
        var pre = Dumps(lines)[0]["STATE"]!;
        var post = Dumps(lines)[1]["STATE"]!;
        Assert.Equal(manaBefore, pre["mana"]!.GetValue<long>());
        Assert.Equal(manaAfter, post["mana"]!.GetValue<long>());  // the dump agrees on the debit
        Assert.Equal(pre["life"]!.GetValue<long>(), post["life"]!.GetValue<long>()); // R1c: a cast survivor never counters — zero hero damage this beat
        if (!cast["KILLED"]!.GetValue<bool>())
            Assert.Equal(post["engagedLife"]!.GetValue<long>(), cast["TARGET_LIFE"]!.GetValue<long>()); // engaged lands as reported
    }

    // ---- GREEN respawn: the scripted corpse is raised at full life in town ----

    [Fact]
    public void RespawnVerb_AfterScriptedDeath_RaisesTheHeroFull_InTown()
    {
        var (exit, lines) = RunScript("new koldun\nattack pokrzywnik 1024\ndump\nrespawn\ndump\n");
        Assert.Equal(0, exit);
        Assert.Equal(0, Dumps(lines)[0]["STATE"]!["life"]!.GetValue<long>()); // the SCRIPT killed the hero (CliVerbsTests idiom)
        var echo = Node(lines, "respawn");
        Assert.Equal("grodzisko", echo["AREA"]!.GetValue<string>()); // the lit stones raise at the last discovered town
        long life = echo["LIFE"]!.GetValue<long>();
        Assert.True(life > 0, "respawn left the hero on the slab");
        var post = Dumps(lines)[1]["STATE"]!;
        Assert.Equal(life, post["life"]!.GetValue<long>());   // the dump agrees: alive
        Assert.Equal("grodzisko", post["currentArea"]!.GetValue<string>());
    }

    // ---- REFUSALS: the ENGINE'S named error + exit 1, per the CLI idiom ----

    [Fact]
    public void BeltAdd_OccupiedSlot_PrintsTheEnginesNamedError_AndExits1()
    {
        var (exit, lines) = RunScript(
            "new koldun\nbuy napitek-zycia\nbuy napitek-manny\nbelt_add 0 1\nbelt_add 1 1\n");
        Assert.Equal(1, exit);
        Assert.Equal("belt slot 1 already holds 'Napitek Zycia'",
            Line(lines, lines.Count - 1)["ERROR"]!.GetValue<string>());
        Assert.DoesNotContain(lines, l => l.Contains("RESULT"));
    }

    [Fact]
    public void Use_EmptySlot_PrintsTheEnginesNamedError_AndExits1()
    {
        var (exit, lines) = RunScript("new koldun\nbuy napitek-zycia\nbelt_add 0 1\nuse 2\n");
        Assert.Equal(1, exit);
        Assert.Equal("belt slot 2 is empty",
            Line(lines, lines.Count - 1)["ERROR"]!.GetValue<string>());
    }

    [Fact]
    public void Cast_UnknownMonster_PrintsTheEnginesNamedError_AndExits1()
    {
        var (exit, lines) = RunScript("new koldun\nlevelto 2\nspend iskra\ncast iskra smok\n");
        Assert.Equal(1, exit);
        Assert.Equal("unknown monster 'smok'",
            Line(lines, lines.Count - 1)["ERROR"]!.GetValue<string>());
    }

    [Fact]
    public void Cast_NeverWoven_PrintsTheEnginesNamedError_AndExits1()
    {
        var (exit, lines) = RunScript("new koldun\ncast iskra pokrzywnik\n");
        Assert.Equal(1, exit);
        Assert.Equal("'iskra' was never woven — spend a skill point into it first",
            Line(lines, lines.Count - 1)["ERROR"]!.GetValue<string>());
    }

    [Fact]
    public void Respawn_WhileAlive_PrintsTheEnginesNamedError_AndExits1()
    {
        var (exit, lines) = RunScript("new koldun\nrespawn\n");
        Assert.Equal(1, exit);
        Assert.Equal("you are not dead — nothing to resurrect",
            Line(lines, lines.Count - 1)["ERROR"]!.GetValue<string>());
    }

    // ---- RNG LAW (DA F4): belt_add / use / respawn consume NOTHING ----
    // Same seed: the twin-none run equals the BASE-BUILD capture byte-identical
    // (words + save sha), and inserting the twins leaves all three rng words
    // where they were — the door-internal rolls (belt math, death penalty) are
    // roll-free by their own headers; any consumption would move a word.

    [Fact]
    public void BeltAddUse_Twins_ConsumeNoRng_WordsAndSaveMatchBaseCapture()
    {
        var (exitOff, linesOff, shaOff) = RunWithSave(
            "new koldun\nbuy napitek-zycia\nbuy napitek-manny\nattack pokrzywnik 12\nloot 20\ndump\nsave {0}\n");
        Assert.Equal(0, exitOff);
        Assert.Equal(S1Words, Words(Dumps(linesOff)[0])); // the twin-none build equals the base capture
        Assert.Equal(S1SaveSha, shaOff);                   // byte-identical save

        var (exitOn, linesOn) = RunScript(
            "new koldun\nbuy napitek-zycia\nbuy napitek-manny\nattack pokrzywnik 12\nloot 20\nbelt_add 0 1\nuse 1\ndump\n");
        Assert.Equal(0, exitOn);
        Assert.Equal(S1Words, Words(Dumps(linesOn)[0])); // twins inserted — every word stays put
    }

    [Fact]
    public void Respawn_Twin_ConsumesNoRng_WordsAndSaveMatchBaseCapture()
    {
        var (exitOff, linesOff, shaOff) = RunWithSave(
            "new koldun\nattack pokrzywnik 1024\ndump\nsave {0}\n");
        Assert.Equal(0, exitOff);
        Assert.Equal(S3Words, Words(Dumps(linesOff)[0]));
        Assert.Equal(S3SaveSha, shaOff);

        var (exitOn, linesOn) = RunScript("new koldun\nattack pokrzywnik 1024\nrespawn\ndump\n");
        Assert.Equal(0, exitOn);
        Assert.Equal(S3Words, Words(Dumps(linesOn)[0])); // the stone raise rolls nothing (DeathPenalty is pure math)
    }

    // ---- cast divergence, pinned HONESTLY: the twin DOES consume rng, and the
    // consumption is exactly the DOOR's own rolls (never the dispatch layer):
    // CombatRng pays the ember roll uniform(DamageMin..DamageMax) (Casting.cs
    // §"ember damage is a uniform roll on the seeded combat stream"); a KILL
    // would also run the KillFlow drop chain on DropRng. The enemy-counter
    // stream moves on ATTACK counters only — a cast survivor never counters
    // (Casting.cs:44-45), so EnemyRng must NOT move here. ----

    [Fact]
    public void Cast_Twin_DivergenceIsPinned_ToTheDoorsOwnCombatRoll()
    {
        var (exitOff, linesOff) = RunScript("new koldun\nlevelto 2\nspend iskra\ndump\n");
        Assert.Equal(0, exitOff);
        Assert.Equal(S2Words, Words(Dumps(linesOff)[0])); // levelto/spend are pure moves (base capture)

        var (exitOn, linesOn) = RunScript("new koldun\nlevelto 2\nspend iskra\ncast iskra pokrzywnik\ndump\n");
        Assert.Equal(0, exitOn);
        var words = Words(Dumps(linesOn)[0]);
        Assert.NotEqual(S2Words.Combat, words.Combat);   // the ember roll consumed a combat-stream draw
        Assert.Equal(S2Words.Drop, words.Drop);          // this seed's weave did not kill — no KillFlow draw
        Assert.Equal(S2Words.Enemy, words.Enemy);        // no counter, no enemy-stream draw (R1c)
        Assert.Equal(CastDivergentWords, words);         // pinned deterministic divergence
    }

    // Pinned on the S3a build (2026-11-02): seed 5, "new koldun / levelto 2 /
    // spend iskra / cast iskra pokrzywnik / dump". Combat advanced — the ONE
    // ember roll (DAMAGE 5 here); DropRng stayed put because this seed's roll
    // did NOT kill (TARGET_LIFE 9 > 0 — a kill would also run the KillFlow drop
    // chain on DropRng); Enemy stayed — the survivor never counters. Honest
    // state, see header above.
    private static readonly (ulong Combat, ulong Drop, ulong Enemy) CastDivergentWords =
        (11877244888447772326, 8742784102034189314, 14215131991883657834);
}
