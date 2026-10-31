// Headless scripted session driver — the pytest gate of docs/ARCHITECTURE.md
// ("pure dotnet, NO engine process"). Usage:
//   dotnet run --project src/BabaYaga.Sim.Cli -- --seed N --script <path> [--data <dir>]
// One JSON line per command on stdout, final {"RESULT":"OK"} + exit 0 on success;
// any rejection prints one {"ERROR":"…"} line and exits 1.

using BabaYaga.Sim.Cli;

ulong seed = 1;
string? script = null;
var dataDir = "data";

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--seed" when i + 1 < args.Length && ulong.TryParse(args[++i], out var s):
            seed = s;
            break;
        case "--script" when i + 1 < args.Length:
            script = args[++i];
            break;
        case "--data" when i + 1 < args.Length:
            dataDir = args[++i];
            break;
        default:
            Console.WriteLine($$"""{"ERROR":"bad or missing argument '{{args[i]}}' (want --seed N --script <path> [--data <dir>])"}""");
            return 2;
    }
}

if (script is null)
{
    Console.WriteLine("""{"ERROR":"--script <path> is required"}""");
    return 2;
}

return CommandRunner.Run(script, seed, dataDir, Console.Out);
