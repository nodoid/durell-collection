using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Durell.Persistence;

/// <summary>What one game keeps between launches.</summary>
public sealed class GameSave
{
    /// <summary>The game's own high-score memory (hex), written back into it when it starts.</summary>
    public string Memory { get; set; } = "";
    /// <summary>Best score, for the collection menu.</summary>
    public int Best { get; set; }
    /// <summary>Who holds the best score, when the game keeps names (Turbo Esprit).</summary>
    public string BestName { get; set; } = "";
    public int Plays { get; set; }
    /// <summary>ISO date of the best score.</summary>
    public string BestDate { get; set; } = "";
    /// <summary>Redefined keys: Oric key name to the host key name that presses it.</summary>
    public Dictionary<string, string> Keys { get; set; } = new();
}

/// <summary>Everything that survives between launches: preferences and every game's high scores.</summary>
public sealed class SaveData
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    /// <summary>"Enhanced" (new graphics and sound) or "Original" (as it was on the Oric).</summary>
    public string Look { get; set; } = "Enhanced";
    public bool Sound { get; set; } = true;
    /// <summary>0..1, in steps of 0.1 (the menu's SOUND - and + buttons).</summary>
    public float Volume { get; set; } = 0.8f;
    /// <summary>Scanlines over the ORIGINAL picture.</summary>
    public bool Scanlines { get; set; }
    /// <summary>Phones and tablets: steer by tilting the device (in place of the on-screen arrows).</summary>
    public bool Tilt { get; set; } = true;
    /// <summary>The game last chosen on the menu.</summary>
    public string LastGame { get; set; } = "harrier";
    public Dictionary<string, GameSave> Games { get; set; } = new();

    [JsonIgnore]
    public bool Enhanced => Look != "Original";

    public GameSave For(string id)
    {
        if (!Games.TryGetValue(id, out var g)) Games[id] = g = new GameSave();
        return g;
    }

    /// <summary>Repairs anything a hand-edited or older save might have wrong.</summary>
    public void Normalize()
    {
        if (Look != "Original") Look = "Enhanced";
        Volume = Math.Clamp(Volume, 0f, 1f);
        Games ??= new Dictionary<string, GameSave>();
        foreach (var g in Games.Values)
        {
            g.Memory ??= "";
            g.BestName ??= "";
            g.BestDate ??= "";
            g.Keys ??= new Dictionary<string, string>();
            if (g.Best < 0) g.Best = 0;
        }
        LastGame ??= "harrier";
        Version = CurrentVersion;
    }
}

[JsonSerializable(typeof(SaveData))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class SaveJsonContext : JsonSerializerContext
{
}
