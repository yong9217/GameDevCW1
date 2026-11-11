using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Local top-5 of Infinite mode: the longest times this computer has seen, longest first.
// Saved as a small JSON file in the game's data folder on this computer, so every computer keeps its own board.
// The editor and a build on the same computer share it.
public static class InfiniteLeaderboard
{
    public const int Size = 5;
    public const int MaxNameLength = 12;

    [Serializable]
    public class Entry
    {
        public string name;
        public float seconds;
    }

    [Serializable]
    class Board
    {
        public List<Entry> entries = new List<Entry>();
    }

    static string FilePath => Path.Combine(Application.persistentDataPath, "infinite_leaderboard.json");

    public static List<Entry> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var board = JsonUtility.FromJson<Board>(File.ReadAllText(FilePath));
                if (board?.entries != null)
                {
                    board.entries.RemoveAll(e => e == null);
                    board.entries.Sort((a, b) => b.seconds.CompareTo(a.seconds));
                    if (board.entries.Count > Size) board.entries.RemoveRange(Size, board.entries.Count - Size);
                    return board.entries;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("InfiniteLeaderboard: could not read the leaderboard: " + e.Message);
        }
        return new List<Entry>();
    }

    // True when this time would get onto the board.
    public static bool Qualifies(float seconds)
    {
        if (seconds <= 0f) return false;
        var entries = Load();
        return entries.Count < Size || seconds > entries[entries.Count - 1].seconds;
    }

    // Adds a time and returns its place on the board (0 = first), or -1 if it did not make the top 5.
    // A tie goes below the older record: whoever got there first keeps the higher place.
    public static int Add(string name, float seconds)
    {
        var entries = Load();
        name = CleanName(name);
        int index = entries.FindIndex(e => seconds > e.seconds);
        if (index < 0) index = entries.Count;
        if (index >= Size) return -1;
        entries.Insert(index, new Entry { name = name, seconds = seconds });
        if (entries.Count > Size) entries.RemoveRange(Size, entries.Count - Size);
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(new Board { entries = entries }, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("InfiniteLeaderboard: could not save the leaderboard: " + e.Message);
        }
        return index;
    }

    public static string CleanName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length > MaxNameLength) name = name.Substring(0, MaxNameLength);
        return name.Length == 0 ? "Anonymous" : name;
    }
}
