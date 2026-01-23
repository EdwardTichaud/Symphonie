using UnityEngine;

/// <summary>
/// Centralizes reading/writing Munin's name via PlayerPrefs + GameData.
/// </summary>
public static class MuninNameStore
{
    public const string PlayerPrefsKey = "MuninName";
    public const string DefaultName = "Munin";

    public static bool HasSavedName()
    {
        return PlayerPrefs.HasKey(PlayerPrefsKey);
    }

    public static string GetName()
    {
        string name = GetNameFromGameData();
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        return GetNameFromPrefs(DefaultName);
    }

    public static string GetNameFromPrefs(string fallback = DefaultName)
    {
        string stored = PlayerPrefs.GetString(PlayerPrefsKey, fallback);
        if (string.IsNullOrWhiteSpace(stored))
            return string.IsNullOrWhiteSpace(fallback) ? DefaultName : fallback;

        return stored;
    }

    public static void SetName(string name, string fallback = DefaultName)
    {
        string sanitized = SanitizeName(name, fallback);

        if (GameManager.Instance != null && GameManager.Instance.gameData != null)
            GameManager.Instance.gameData.muninName = sanitized;

        PlayerPrefs.SetString(PlayerPrefsKey, sanitized);
        PlayerPrefs.Save();
    }

    public static string SanitizeName(string name, string fallback = DefaultName)
    {
        string safeFallback = string.IsNullOrWhiteSpace(fallback) ? DefaultName : fallback;

        if (string.IsNullOrWhiteSpace(name))
            return safeFallback;

        return name.Trim();
    }

    private static string GetNameFromGameData()
    {
        if (GameManager.Instance == null || GameManager.Instance.gameData == null)
            return string.Empty;

        return GameManager.Instance.gameData.muninName;
    }
}
