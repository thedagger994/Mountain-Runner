using UnityEngine;

public enum UpgradeType
{
    Grip,
    Steering,
    Momentum,
    Fortune,
    Shield
}

// Persistent meta-progression: the shard balance and purchased upgrade levels, stored in
// PlayerPrefs so they survive between runs and sessions.
//
// Effects are exposed as plain multipliers and bonuses that each gameplay script folds
// into its own serialized baseline at Awake. That keeps every upgrade rule in one file
// and means no gameplay script needs to know the shop exists.
public static class PlayerProgress
{
    public readonly struct UpgradeDef
    {
        public readonly string Name;
        public readonly string Description;
        public readonly int MaxLevel;
        public readonly int BaseCost;
        public readonly float CostGrowth;

        public UpgradeDef(string name, string description, int maxLevel, int baseCost, float costGrowth)
        {
            Name = name;
            Description = description;
            MaxLevel = maxLevel;
            BaseCost = baseCost;
            CostGrowth = costGrowth;
        }
    }

    // Index order must match the UpgradeType enum.
    private static readonly UpgradeDef[] Definitions =
    {
        new UpgradeDef("GRIP",     "Sharper lateral control",    5, 25,  1.7f),
        new UpgradeDef("STEERING", "Wider turning limit",        5, 30,  1.7f),
        new UpgradeDef("MOMENTUM", "More points per second",     5, 35,  1.7f),
        new UpgradeDef("FORTUNE",  "More shards earned per run", 5, 40,  1.7f),
        new UpgradeDef("SHIELD",   "Survive a tree hit",         3, 120, 2.2f)
    };

    public static readonly UpgradeType[] All =
    {
        UpgradeType.Grip,
        UpgradeType.Steering,
        UpgradeType.Momentum,
        UpgradeType.Fortune,
        UpgradeType.Shield
    };

    private const string CurrencyKey = "MountainRunner.Shards";
    private const string LevelKeyPrefix = "MountainRunner.Upgrade.";

    private static readonly int[] levels = new int[5];
    private static bool loaded;
    private static int currency;

    public static int Currency
    {
        get { Load(); return currency; }
    }

    public static UpgradeDef Definition(UpgradeType type) => Definitions[(int)type];

    public static int GetLevel(UpgradeType type)
    {
        Load();
        return levels[(int)type];
    }

    public static bool IsMaxed(UpgradeType type) => GetLevel(type) >= Definition(type).MaxLevel;

    public static int GetCost(UpgradeType type)
    {
        if (IsMaxed(type)) return 0;
        UpgradeDef def = Definition(type);
        return Mathf.RoundToInt(def.BaseCost * Mathf.Pow(def.CostGrowth, GetLevel(type)));
    }

    public static bool CanBuy(UpgradeType type) => !IsMaxed(type) && Currency >= GetCost(type);

    public static bool Buy(UpgradeType type)
    {
        if (!CanBuy(type)) return false;

        currency -= GetCost(type);
        levels[(int)type]++;
        Save();
        return true;
    }

    public static void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        Load();
        currency += amount;
        Save();
    }

    public static void ResetAll()
    {
        Load();
        currency = 0;
        for (int i = 0; i < levels.Length; i++) levels[i] = 0;
        Save();
    }

    public static float GripMultiplier => 1f + 0.18f * GetLevel(UpgradeType.Grip);
    public static float SteerRangeBonus => 8f * GetLevel(UpgradeType.Steering);
    public static float ScoreMultiplier => 1f + 0.15f * GetLevel(UpgradeType.Momentum);
    public static float FortuneMultiplier => 1f + 0.25f * GetLevel(UpgradeType.Fortune);
    public static int ShieldCharges => GetLevel(UpgradeType.Shield);

    private static void Load()
    {
        if (loaded) return;
        loaded = true;

        currency = PlayerPrefs.GetInt(CurrencyKey, 0);
        for (int i = 0; i < levels.Length; i++)
            levels[i] = PlayerPrefs.GetInt(LevelKeyPrefix + (UpgradeType)i, 0);
    }

    private static void Save()
    {
        PlayerPrefs.SetInt(CurrencyKey, currency);
        for (int i = 0; i < levels.Length; i++)
            PlayerPrefs.SetInt(LevelKeyPrefix + (UpgradeType)i, levels[i]);
        PlayerPrefs.Save();
    }
}
