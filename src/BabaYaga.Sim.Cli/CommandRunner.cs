using System.Text.Json;

namespace BabaYaga.Sim.Cli;

/// <summary>
/// Reads a line-command script and drives one ScriptedSession: every line prints
/// exactly one JSON object, the whole run ends with {"RESULT":"OK"} (exit 0), and
/// the FIRST rejection stops the script with one {"ERROR":"…"} line (exit 1) —
/// the stdout-markers + exit-codes contract the pytest phase reads. No stderr,
/// no prompts, nothing else on the stream.
/// </summary>
public static class CommandRunner
{
    private static readonly JsonSerializerOptions LineOpts = new();

    /// <summary>Runs one script file. Returns the process exit code.</summary>
    public static int Run(string scriptPath, ulong seed, string dataDir, TextWriter output)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(scriptPath);
        }
        catch (Exception e)
        {
            Emit(output, new Dictionary<string, object?> { ["ERROR"] = $"cannot read script: {e.Message}" });
            return 2;
        }

        var session = new ScriptedSession(seed, dataDir);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            try
            {
                Emit(output, session.Execute(line));
            }
            catch (Exception e)
            {
                // Any rejection or crash is the ERROR line + nonzero exit; the
                // driver never prints a half-finished script as success.
                Emit(output, new Dictionary<string, object?> { ["ERROR"] = e.Message });
                return 1;
            }
        }
        Emit(output, new Dictionary<string, object?> { ["RESULT"] = "OK" });
        return 0;
    }

    private static void Emit(TextWriter output, Dictionary<string, object?> payload) =>
        output.WriteLine(JsonSerializer.Serialize(payload, LineOpts));
}
