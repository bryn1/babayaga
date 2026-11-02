using Godot;
using BabaYaga.Sim;

/// <summary>
/// The ONE session holder passed between scenes — a plain static, no autoload
/// singleton. Holds the GameSession the CLI already proves; scenes never build a
/// second one. user://save is the Continue path (the real 2-arg static
/// GameSession.Load(string path, string dataDir); a 1-arg call does not exist).
/// </summary>
public static class GameContext
{
    /// <summary>The live session, or null before New/Load ran.</summary>
    public static GameSession? Session { get; private set; }

    /// <summary>GameSession dataDir: the one-way copy of repo data/*.json.</summary>
    public static string DataDir() => ProjectSettings.GlobalizePath("res://data");

    /// <summary>Globalized user://save — the Continue path's save file.</summary>
    public static string SavePath() => ProjectSettings.GlobalizePath("user://save");

    public static GameSession New(ulong seed, string classKey = "koldun")
    {
        var session = Session = GameSession.New(seed, classKey, DataDir());
        // M5-S3 bridge (MC 11934): session creation precedes the log (DA F4), so
        // the head line is SYNTHESIZED here — and only when BABA_LOG says so;
        // env unset, Ambient stays null and nothing anywhere writes.
        CommandLog.Ambient = CommandLog.FromEnvironment();
        CommandLog.Ambient?.Open($"new {classKey}");
        return session;
    }

    public static void Save() => Session?.Save(SavePath());

    /// <summary>Loads user://save when it exists; a New session otherwise.</summary>
    public static GameSession Load(ulong fallbackSeed = 1)
    {
        var session = Session = FileAccess.FileExists("user://save")
            ? GameSession.Load(SavePath(), DataDir())
            : GameSession.New(fallbackSeed, "koldun", DataDir());
        // Continue attaches to a mid-stream session no replay can re-bootstrap —
        // the log opens on a comment head (honest: the replay contract is New
        // Game sessions, the F4 head; lines still record for the trail).
        CommandLog.Ambient = CommandLog.FromEnvironment();
        CommandLog.Ambient?.Open($"# continue-from-save {SavePath()}");
        return session;
    }
}
