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

## Build & run (copied from T0c; `<project>` = `game/`)

```bash
GODOT=/home/claudecode/Games/.tools/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64
# once per added/changed asset or script:
"$GODOT" --headless --path <project> --import
# build C#:
dotnet build <project>/babayaga.csproj        # Sdk="Godot.NET.Sdk/4.7.2", TargetFramework=net8.0
# engine sim leg (the pytest acceptance gate): exit code + named GD.Print markers
"$GODOT" --headless --path <project> -- <mode-arg>
# hygiene when script-class lookup mysteriously fails:
rm -rf <project>/.godot/mono/temp && dotnet build <project>/babayaga.csproj
```

Headless scripted session + rule tests (no engine — current gate):

```bash
dotnet build BabaYaga.sln
dotnet test BabaYaga.sln            # xunit rule contracts
dotnet run --project src/BabaYaga.Sim.Cli -- --seed 2 --script tests/journey.txt --data data
```

The CLI prints one JSON line per command and a final `{"RESULT":"OK"}` (exit 0);
any rejection prints one `{"ERROR":"…"}` line and exits nonzero. The planned
`pytest tests/` marker suite (same CLI, more journeys) is NOT-BUILT yet — see LEDGER.

Note: `dotnet` SDK 8.0.133 at `/usr/bin/dotnet` is sufficient (T0c verified; no new install needed).
