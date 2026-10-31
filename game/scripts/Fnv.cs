using System.Text;

/// <summary>
/// The ONE FNV helper (mechanism-monogamy): FNV-1a 32-bit over the string's UTF-8
/// bytes — offset 2166136261, prime 16777619, uint32 wrap. AreaRenderer and
/// MonsterField call Fnv.Hash(s) ONLY; no second implementation anywhere.
/// Seeds are display-only (placeholder layout, spawner), never persisted state.
/// </summary>
public static class Fnv
{
    public static uint Hash(string s)
    {
        uint h = 2166136261;
        foreach (byte b in Encoding.UTF8.GetBytes(s))
            h = unchecked((h ^ b) * 16777619);
        return h;
    }
}
