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

    public static GameSession New(ulong seed, string classKey = "koldun") =>
        Session = GameSession.New(seed, classKey, DataDir());

    public static void Save() => Session?.Save(SavePath());

    /// <summary>Loads user://save when it exists; a New session otherwise.</summary>
    public static GameSession Load(ulong fallbackSeed = 1) =>
        Session = FileAccess.FileExists("user://save")
            ? GameSession.Load(SavePath(), DataDir())
            : GameSession.New(fallbackSeed, "koldun", DataDir());
}
