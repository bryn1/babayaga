# Baba Yaga

Single-player isometric ARPG with D2:LoD-class mechanics, built as an ORIGINAL-IP product:
"Baba Yaga" — a medieval Slavic witch-fantasy world of the Baba Yaga myth. No Blizzard-derived
names, art, or table content: mechanics are re-expressed under original names (clean-room rule,
docs/ARCHITECTURE.md §R2).

- Engine: **Godot 4.7.2-stable, official .NET (mono) build** (pinned by run T0c). Language: **C# / net8.0**.
- Shape: game rules live in a pure-C# library (`src/BabaYaga.Sim/`, zero Godot references);
  Godot (`game/`) is presentation only. The headless CLI (`src/BabaYaga.Sim.Cli/`) drives
  scripted sessions (`tests/journey.txt`); rule contracts run as xunit — no engine process
  needed for rule tests.
- Status: see `LEDGER.md`. Architecture (tree map, dependency rule, modules, data stores):
  `docs/ARCHITECTURE.md`.

## Build & run (repo root — everything that exists today)

```bash
dotnet build BabaYaga.sln         # Sim + Cli + Tests; 0 warnings is the bar
dotnet test BabaYaga.sln          # xunit rule contracts
# scripted journey (one JSON line per command; last line {"RESULT":"OK"}, exit 0):
dotnet run --project src/BabaYaga.Sim.Cli -- --seed 2 --script tests/journey.txt --data data
# DA round-1 probes (same CLI; also exercised by xunit):
dotnet run --project src/BabaYaga.Sim.Cli -- --seed 2 --script tests/probe-p12.txt --data data
```

`dotnet run` occasionally misses a just-built dll; the reliable form is
`dotnet src/BabaYaga.Sim.Cli/bin/Debug/net8.0/BabaYaga.Sim.Cli.dll --seed 2 --script tests/journey.txt --data data`.
Any rejection prints one `{"ERROR":"…"}` line and exits nonzero. The planned
`pytest tests/` marker suite (same CLI, more journeys) is NOT-BUILT yet — see LEDGER.
Note: `dotnet` SDK 8.0.133 at `/usr/bin/dotnet` is sufficient (T0c verified; no new install needed).

## Known limits

- **Mid-special-fight save keeps plain-restore (documented, intentional — DA round-2 P4-3).**
  A save taken while a `special` group is engaged stores the engagement's current
  bonus-scaled LIFE but no special kind/affixes; on load the monster is restored PLAIN,
  so the next kill pays the plain XP row (not the ×3/×5 band multiplier). Consistent by
  construction (dump→save→load→dump is byte-identical, exit 0); LEDGER DA-ROUND-2-FIX
  row carries the OPEN line.
- Save files are read at the build's own version only (`save version N unsupported`
  refuses foreign versions, hand-built v1 pinned by xunit); there is no upgrade path.

## Engine leg (PLANNED — `game/` is not in the tree yet, row 8 of the LEDGER)

Godot build/acceptance commands from T0c, for when `game/` lands:
`<project>` = `game/`, `GODOT=/home/claudecode/Games/.tools/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64`;
`"$GODOT" --headless --path <project> --import` after asset/script changes;
`dotnet build <project>/babayaga.csproj` (Sdk=`Godot.NET.Sdk/4.7.2`, net8.0);
engine sim leg `"$GODOT" --headless --path <project> -- <mode-arg>`.
If script-class lookup fails: `rm -rf <project>/.godot/mono/temp && dotnet build <project>/babayaga.csproj`.
