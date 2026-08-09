using System.Collections.Generic;

namespace VSLockAndKey;

/// <summary>
/// Bronze is one gating tier but three real vanilla alloys (tinbronze/bismuthbronze/
/// blackbronze) - each is its own item variant state so it keeps its own texture via
/// the {metal} wildcard, but all three rank equally here for file/key tier checks.
/// </summary>
public static class MetalTier
{
    public const string TinBronze = "tinbronze";
    public const string BismuthBronze = "bismuthbronze";
    public const string BlackBronze = "blackbronze";
    public const string Iron = "iron";
    public const string MeteoricIron = "meteoriciron";
    public const string Steel = "steel";

    public static readonly string[] AllMetals = { TinBronze, BismuthBronze, BlackBronze, Iron, MeteoricIron, Steel };

    static readonly Dictionary<string, int> order = new()
    {
        [TinBronze] = 0,
        [BismuthBronze] = 0,
        [BlackBronze] = 0,
        [Iron] = 1,
        [MeteoricIron] = 2,
        [Steel] = 3
    };

    public static int RankOf(string metal)
    {
        return order.TryGetValue(metal, out int rank) ? rank : -1;
    }

    public static bool IsAtLeast(string fileMetal, string keyMetal)
    {
        return RankOf(fileMetal) >= RankOf(keyMetal);
    }
}
