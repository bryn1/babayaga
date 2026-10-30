namespace BabaYaga.Sim.Tests;

/// <summary>
/// Locates the repo's data/ directory by walking up from the test assembly output
/// folder, so tests read the same JSON the shipped game will read.
/// </summary>
internal static class TestData
{
    public static string DataDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "data")))
            dir = dir.Parent;
        return dir is null
            ? throw new DirectoryNotFoundException("no data/ directory above the test assembly")
            : Path.Combine(dir.FullName, "data");
    }

    public static XpTable Ladder() => XpTable.Load(Path.Combine(DataDir(), "xp-ladder.json"));

    public static ClassSkillData KoldunSkills() =>
        SkillDataLoader.Load(Path.Combine(DataDir(), "koldun-skills.json"));

    public static Character Koldun() => new(Stats.Koldun, "test-koldun");
}
