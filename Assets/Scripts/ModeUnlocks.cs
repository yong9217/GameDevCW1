using UnityEngine;

// Infinite Mode is locked until the Campaign has been won once on this computer.
// Saved in PlayerPrefs, like the Infinite leaderboard, so every computer starts locked.
public static class ModeUnlocks
{
    const string UnlockedKey = "KingdomSentinels.InfiniteUnlocked";
    const string CelebrateKey = "KingdomSentinels.InfiniteUnlockPending";

    public static bool InfiniteUnlocked => PlayerPrefs.GetInt(UnlockedKey, 0) == 1;

    // True from the first Campaign win until the main menu has shown the congratulations.
    public static bool CelebrationPending => PlayerPrefs.GetInt(CelebrateKey, 0) == 1;

    public static void MarkCampaignWon()
    {
        if (InfiniteUnlocked) return;
        PlayerPrefs.SetInt(UnlockedKey, 1);
        PlayerPrefs.SetInt(CelebrateKey, 1);
        PlayerPrefs.Save();
    }

    public static void MarkCelebrated()
    {
        PlayerPrefs.SetInt(CelebrateKey, 0);
        PlayerPrefs.Save();
    }

    // Hidden main-menu shortcut (Shift + B, L, G): unlock without the congratulations.
    public static void UnlockSilently()
    {
        PlayerPrefs.SetInt(UnlockedKey, 1);
        PlayerPrefs.SetInt(CelebrateKey, 0);
        PlayerPrefs.Save();
    }
}
