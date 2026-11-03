using Godot;

/// <summary>
/// Entry scene script (res://Main.tscn root). Headless → the pinned M4 marker leg:
/// a scripted GameSession (seed 42, "koldun", res://data) + three REAL gold-moving
/// verbs (Buy / SellLast / Gamble), one SIM_TICK line per verb read FROM THE SESSION
/// OBJECT EVERY TICK; then ONE scenes/Act.tscn instantiate through the placeholder
/// pass (DA_SCENE_ACT_OK prints only after a NON-NULL instantiate — a null scene
/// goes to PushError and never prints the marker), and the retained Act rides the
/// M4-SLICE journey legs (JourneyLeg: HERO_STEP walk + one MONSTER_DEFEATED per
/// kind the data table spawns in mchy + the XP_EXPECT honesty leg + the M4-S4
/// level-up beat (LEVEL_UP on the runtime ladder threshold, HUD_LEVEL pulled from
/// the live Hud), bridge-only) → the M4-S2 vendor trade beat (TradeLeg: stone home to the
/// town, Buy + SellLast, every value read from the session) → the M4-S5 potion belt beat
/// (BeltLeg) → the M4-S6 DEATH BEAT (DeathLeg: real counter-damage kills the hero in
/// mchy, the DEAD session rejects Attack, the lit stones raise him in town with the
/// DeathPenalty paid through the Sim door — DEATH_OK) → the M4-S8 QUEST BEAT
/// (QuestLeg: Zaginione Gęsi accepted on the FSM's own rules, the goal ground
/// visited for real, the step taken ON it, the rite completed — QUEST_ACCEPT/
/// QUEST_STEP/QUEST_COMPLETE + QUEST_REWARD points_before/after read around the
/// payment + QUEST_STATE×3 pulled from the live Hud — the paid SkillPoint spends
/// into iskra and ONE reward-bought weave lands (the CAST-after-reward proof);
/// the leg carries the hero home, so the cast leg's town start is unchanged —
/// QUEST_OK) → the M4-S7 CAST BEAT
/// (CastLeg: the iskra weave the QUEST rite paid for is ASSERTED learned (the
/// S7 leg no longer mints its own point — S8's real quest loop owns the rite),
/// the hero weaves a pokrzywnik dead in
/// mchy — CAST/CAST_DEFEATED — drains the pool to a MANA_REJECT hint, quaffs the belted
/// napitek-manny and weaves green — MANA_OK) → the M4-S10b REGEN BEAT
/// (RegenLeg: the pool is drained with REAL weaves on an EMPTY belt, RestTick(3000)
/// — the explicit headless rest driver (ORIGINAL DESIGN) — advances the D2 mana
/// quantum of the AMENDED spec §1.8 (MC 11873), printed session-side as REGEN
/// mana_before/mana_after; the next weave succeeds with NO potion between (the
/// refill is the rest) → real counter-damage lands and a second RestTick leaves the
/// Life pool at exactly life_before — LIFE_STATIC, the D2 rule that life never
/// regenerates on its own → REGEN_OK) → the M4-S9b ACT-1-OPENS BEAT
/// (Act1OpenLeg: the ONE travel door has evolved per ORCH-RULING 2026-11-01 +
/// the D2 oracle grant — WALK a linkedTo edge from known ground, or RIDE the
/// lit stones, unchanged — so the stone-less rites go LIVE: wrzeciono walked to
/// pokrzywlasy (WALK_STEP, lit-set equal before/after, kill on the goal ground,
/// QUEST2_REWARD maxlife delta = PotionOfLifeFlat through the constant path),
/// dary walked hop-by-hop grodzisko→mchy→krecie→kopcowisko (krecie arrived
/// dark, order-contract Discover, kopcowisko walked-in DARK), the WaypointUnlock
/// reward lights the stone (NEW_STONE before=False lit=True), and the
/// edgeless kopcowisko↔grodzisko moves prove the RIDE half — ACT1_OPEN_OK) →
/// the M4-S11 HIRELINGS BEAT (HireLeg: the captain's doors per the AMENDED §6.7
/// (MC 11883) — HIRE at town with the carried-forward price formula stated at
/// the hired rookie's level, the merc's area mirror on travel (HIRE_FOLLOW), a
/// mercenary kill paying its capped full chain at its OWN level (HIRE_XP own)
/// while its stand-in front holds every counter off the hero (HIRE_LIFE), the
/// hero-kill share at 86/256 of the capped figure (HIRE_XP shared), a
/// deterministic death on the leszy's dice (HIRE_DEAD — dead mercs strike not
/// and regen not), the town revive at min(15·L²/2, 50000) restoring the
/// SAME mercenary at full life (REVIVE + HIRE_ALIVE), and the M4-S14 HERO-
/// DEATH LAW (§6.7b AMENDED, MC 11898 — D2 KillPlayerPets: the living merc
/// dies WITH the hero on the counters (MERC_DIES), the stone raises him home
/// and leaves the corpse at mchy (HERO_RESPAWN), and the captain's EXACT
/// cost raises the same mercenary again (REVIVE_AFTER_HERO_DEATH)) —
/// HIRE_OK) →
/// the M4-S12 TOWN STASH BEAT (StashQuestLeg: glowa's StashTab reward opens
/// tab 2 LIVE (row 74) — the rite accepted in town, kopcowisko ridden, its
/// data row's kill taken on the road, czelusc WALKED in dark (the lair stands
/// GUARDED since MC 11954 — the leg's NONE line is the empty-field guard
/// only, silent on this lair), the step on the goal ground,
/// NEW_TAB before/after around the payment + the pay-once QUEST4_REFUSED —
/// then the town chest: store into tab 2 and take back, every position read
/// from the session's own reports — STASH_OK) →
/// ORCH_M4_OK → Quit(0).
/// Windowed → MainMenu.tscn (the scene change DEFERRED out of _Ready — LEDGER
/// row 86 cured by MC 11957) — or, when BABA_S4_LEG names a leg dir, the M5-S4
/// windowed INPUT driver (MC 11937: synthesized clicks through the real input
/// pipeline + PNG evidence; env unset, that branch is dead code), or when
/// BABA_S6_LEG names one, the M5-S6 town-panel driver (MC 11940, same shape),
/// or when BABA_MENU_LEG names one, the MC 11957 game-menu/interlock driver
/// (same shape again), or when BABA_HUD_LEG names one, the MC 11959 HUD
/// click-through PASSIVE state reader the xdotool driver reads (same shape).
/// Zero rules
/// live here (Game → Sim → nothing).
/// </summary>
public partial class Boot : Node
{
    public override void _Ready()
    {
        // The EXACT headless check (DisplayServer.Headless is NOT in the 4.7.2 bindings).
        if (DisplayServer.GetName() == "headless")
            RunMarkerLeg();
        else if (System.Environment.GetEnvironmentVariable(S4InputLeg.EnvVar) is { Length: > 0 } legDir)
            RunWindowedInputLeg(legDir); // M5-S4 evidence driver — env-gated, windowed-only (the SessionClockNode pattern)
        else if (System.Environment.GetEnvironmentVariable(S6InputLeg.EnvVar) is { Length: > 0 } s6Dir)
            RunTownPanelLeg(s6Dir); // M5-S6 town-panel evidence driver (MC 11940) — same gate shape
        else if (System.Environment.GetEnvironmentVariable(EquipCellLeg.EnvVar) is { Length: > 0 } eqDir)
            RunEquipCellLeg(eqDir); // M5 hero-equip evidence driver (MC 11952) — same gate shape
        else if (System.Environment.GetEnvironmentVariable(StashChestLeg.EnvVar) is { Length: > 0 } stDir)
            RunStashChestLeg(stDir); // M5 town-chest evidence driver (MC 11953) — same gate shape
        else if (System.Environment.GetEnvironmentVariable(GameMenuLeg.EnvVar) is { Length: > 0 } gmDir)
            RunGameMenuLeg(gmDir); // MC 11957 game-menu evidence driver — same gate shape
        else if (System.Environment.GetEnvironmentVariable(HudClickLeg.EnvVar) is { Length: > 0 })
            RunHudClickLeg(); // MC 11959 HUD click-through state reader — same gate shape
        else
            // LEDGER row 86 CURE (MC 11957): a SYNCHRONOUS ChangeSceneToFile from
            // inside _Ready made the engine remove_child the boot scene while the
            // root was still busy adding children — one engine ERROR line per
            // windowed boot (measured 2026-11-03 before this line: 1 hit, C#
            // backtrace naming Boot._Ready verbatim; root cause is the call SITE,
            // not the engine). The engine's own hint names the cure shape: the
            // scene change rides CallDeferred — the idle frame is not processing
            // the parent, the swap lands clean, the line is GONE (never
            // cosmetically swallowed — after: 0 hits, the menu still paints, the
            // headless story bytes never reach this branch).
            Callable.From(() => GetTree().ChangeSceneToFile("res://scenes/MainMenu.tscn")).CallDeferred();
    }

    /// <summary>M5-S4 INPUT LEG (MC 11937, DoD 2/3): windowed ONLY and BABA_S4_LEG-gated —
    /// unset (every windowed game AND every headless anchor) this branch is dead code.
    /// It New's the ONE session at the fixed leg seed (the log head rides
    /// GameContext.New; the driver prints the seed), or LOAD's user://save when
    /// BABA_S4_CONTINUE says on — the S3-named Continue exception riding its
    /// comment head — instantiates the ACT scene for real, and hands it to the
    /// driver that plays the DoD sequence through SYNTHESIZED input events only
    /// (the same _UnhandledInput pipeline a human click rides) while PNG
    /// screenshots land in the leg dir.</summary>
    private void RunWindowedInputLeg(string legDir)
    {
        if (System.Environment.GetEnvironmentVariable(S4InputLeg.ContinueVar) == "1") GameContext.Load();
        else GameContext.New(S4InputLeg.LegSeed);
        var act = InstantiateActSmoke();
        if (act is null) { GetTree().Quit(1); return; }
        AddChild(new S4InputLeg { OutDir = legDir, Act = act });
    }

    /// <summary>M5-S6 TOWN-PANEL LEG (MC 11940, DoD 2): the same gate shape —
    /// windowed ONLY, BABA_S6_LEG-gated, unset = dead code. New session at the
    /// leg seed, the REAL Act, and the driver that clicks the TownPanel buttons
    /// (buy → belt → hire → dismiss), walks out, and presses the HUD's own
    /// belt hotkey — every state move a synthesized input, never a verb.</summary>
    private void RunTownPanelLeg(string legDir)
    {
        GameContext.New(S6InputLeg.LegSeed);
        var act = InstantiateActSmoke();
        if (act is null) { GetTree().Quit(1); return; }
        AddChild(new S6InputLeg { OutDir = legDir, Act = act });
    }

    /// <summary>M5 HERO-EQUIP LEG (MC 11952): the same gate shape — windowed
    /// ONLY, BABA_EQ_LEG-gated, unset = dead code. New session at the leg seed,
    /// the REAL Act, and the driver that buys at the pedlar, opens the pack and
    /// CLICKS THE PAINTED ANCHOR CELL (click-to-wear, the declared control-
    /// scheme translation of D2's cursor drag) — every state move a synthesized
    /// input, never a verb.</summary>
    private void RunEquipCellLeg(string legDir)
    {
        GameContext.New(EquipCellLeg.LegSeed);
        var act = InstantiateActSmoke();
        if (act is null) { GetTree().Quit(1); return; }
        AddChild(new EquipCellLeg { OutDir = legDir, Act = act });
    }

    /// <summary>M5 TOWN-CHEST LEG (MC 11953, DoD 2): the same gate shape —
    /// windowed ONLY, BABA_STASH_LEG-gated, unset = dead code. New session at
    /// the leg seed, the REAL Act, and the driver that buys at the pedlar,
    /// opens the chest with the C key and CLICKS THE PAINTED ANCHORS — store
    /// on the pack side, take on the tab side — then the LOCKED tab click;
    /// every state move a synthesized input, never a verb.</summary>
    private void RunStashChestLeg(string legDir)
    {
        GameContext.New(StashChestLeg.LegSeed);
        var act = InstantiateActSmoke();
        if (act is null) { GetTree().Quit(1); return; }
        AddChild(new StashChestLeg { OutDir = legDir, Act = act });
    }

    /// <summary>MC 11957 GAME-MENU LEG (DoD 2): the same gate shape — windowed
    /// ONLY, BABA_MENU_LEG-gated, unset = dead code. New session at the leg
    /// seed (or LOAD of the runner-staged user://save when BABA_MENU_CONTINUE
    /// says on — the S4 ContinueVar shape, here to stage the DEAD hero the
    /// interlock row must press ESC against), the REAL Act, and the driver
    /// that presses ESC and CLICKS THE PAINTED MENU BUTTONS — every state
    /// move a synthesized input, never a verb.</summary>
    private void RunGameMenuLeg(string legDir)
    {
        if (System.Environment.GetEnvironmentVariable(GameMenuLeg.ContinueVar) == "1") GameContext.Load();
        else GameContext.New(GameMenuLeg.LegSeed);
        var act = InstantiateActSmoke();
        if (act is null) { GetTree().Quit(1); return; }
        AddChild(new GameMenuLeg { OutDir = legDir, Act = act });
    }

    /// <summary>MC 11959 HUD CLICK-THROUGH LEG (DoD 2): the same gate shape —
    /// windowed ONLY, BABA_HUD_LEG-gated, unset = dead code. New session at
    /// the leg seed, the REAL Act, and the PASSIVE reader that streams hero/area/
    /// gold/panel state lines while the committed xdotool driver lands the real
    /// X clicks (the leg itself plays no part in any click — HudClickLeg).</summary>
    private void RunHudClickLeg()
    {
        GameContext.New(HudClickLeg.LegSeed);
        var act = InstantiateActSmoke();
        if (act is null) { GetTree().Quit(1); return; }
        AddChild(new HudClickLeg { Act = act });
    }

    /// <summary>The DoD leg 3/3b gate + the slice journey legs. The legs below
    /// perform NO waypoint verb after any travel (the WAYPOINT ORDER CONTRACT
    /// lights the DESTINATION stone BEFORE the travel — HeroController and
    /// JourneyLeg both honour it; discover-after-travel is nowhere).</summary>
    private void RunMarkerLeg()
    {
        var session = GameContext.New(42);

        // Three real gold-moving verbs from the §2 bridge surface; a dead session
        // cannot fake the three DISTINCT gold values the ticks below report.
        session.Buy("palica");
        PrintTick(1, session);
        session.SellLast();
        PrintTick(2, session);
        session.Gamble();
        PrintTick(3, session);

        // The slice legs ride the SAME instantiated Act the scene smoke produces.
        var act = InstantiateActSmoke();
        if (act is not null)
        {
            JourneyLeg.Run(session, act);
            TradeLeg.Run(session, act); // M4-S2: town vendor trade riding the journey's tail
            BeltLeg.Run(session, act); // M4-S5: potion belt leg riding the trade leg's town tail
            DeathLeg.Run(session, act); // M4-S6: death + stone-resurrection riding the belt leg's empty belt
            QuestLeg.Run(session, act); // M4-S8: the gesi loop pays the point that weaves iskra (rides the death leg's town tail)
            CastLeg.Run(session, act); // M4-S7: the iskra cast beat riding the quest leg's town tail (the weave is already learned)
            RegenLeg.Run(session, act); // M4-S10b: REGEN/LIFE_STATIC on the cast leg's mchy tail — mana regens by the D2 quantum (amended §1.8, MC 11873), life NEVER does
            Act1OpenLeg.Run(session, act); // M4-S9b: wrzeciono + dary go LIVE — the stone-less world is WALKED (ORCH-RULING 2026-11-01), the lit-stone RIDE unchanged — ACT1_OPEN_OK
            HireLeg.Run(session, act); // M4-S11: the captain's doors per the AMENDED §6.7 (MC 11883) — hire price formula at the rookie, 86/256 chain, revive cost, merc regen rule, nearest-target — HIRE_OK
            StashQuestLeg.Run(session, act); // M4-S12: glowa's StashTab opens tab 2 LIVE (row 74) — the walked rite, the pays-once reward, the town chest store/take read from the session reports — STASH_OK
        }

        // M5-S3 REPLAY-PROOF leg (MC 11934, DoD 2 game leg): runs ONLY when the
        // BABA_LOG env names a path — env unset (every anchor run) this branch
        // is dead code and the story bytes are untouched. It opens a fresh
        // session, drives it through the real CommandLog + SessionClock, and
        // prints the CLI-shape dump the evidence script replays byte-for-byte.
        if (System.Environment.GetEnvironmentVariable(CommandLog.EnvVar) is { Length: > 0 })
            BridgeLeg.Run();

        GD.Print("ORCH_M4_OK");
        GetTree().Quit(0);
    }

    /// <summary>PINNED format: SIM_TICK n=&lt;int&gt; area=&lt;string&gt; gold=&lt;long&gt; level=&lt;int&gt;.
    /// area/gold/level are read from the session object on every call —
    /// session.CurrentArea / .Gold / .Level.</summary>
    private static void PrintTick(int n, BabaYaga.Sim.GameSession session) =>
        GD.Print($"SIM_TICK n={n} area={session.CurrentArea} gold={session.Gold} level={session.Level}");

    /// <summary>Scene-execution smoke (leg 3b): compile is not execution — this
    /// proves the scene scripts run headless through the placeholder pass
    /// (missing art = PushWarning, never a crash). The instance is RETAINED: the
    /// slice journey legs ride it; Quit(0) frees it with the tree.</summary>
    private Act? InstantiateActSmoke()
    {
        var packed = GD.Load<PackedScene>("res://scenes/Act.tscn");
        if (packed is null)
        {
            GD.PushError("scene-exec smoke: res://scenes/Act.tscn loaded NULL");
            return null; // no marker on a null scene — the gate's conditional, pinned
        }
        if (packed.Instantiate() is not Act act)
        {
            GD.PushError("scene-exec smoke: Act.tscn instantiated NULL");
            return null;
        }
        AddChild(act); // Act._Ready runs here — the placeholder pass executes headless
        GD.Print("DA_SCENE_ACT_OK");
        return act;
    }
}
