using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Durell.Games;

/// <summary>One thing in a game's world this frame, read from the game's memory.</summary>
internal sealed class SceneObject
{
    /// <summary>Stays the same for the same object from frame to frame (its slot in the game's table).</summary>
    public int Id;
    public string Kind = "";
    /// <summary>World position in Oric pixels (what the point means - centre, nose - is up to the game's look).</summary>
    public float X, Y;
    public int Frame;
    public bool FlipX;
    public float Angle;
    public float Size = 1;
    /// <summary>Anything else the artwork needs (e.g. a colour, a state).</summary>
    public int Data;
}

/// <summary>
/// What a game is showing, in its own terms: the camera over its world and the objects in it. Read
/// from RAM after every game frame by the game's <see cref="Look"/>, and drawn - with positions
/// blended between the last two frames - as high-resolution artwork in the ENHANCED look.
/// </summary>
internal sealed class Scene
{
    /// <summary>False when the game is on a screen the look doesn't redraw (menus, titles): the original picture is shown.</summary>
    public bool Active;
    /// <summary>Camera: the world position (Oric pixels) of the playfield's top-left corner.</summary>
    public float CameraX, CameraY;
    public readonly List<SceneObject> Objects = new();
    private int _used;
    /// <summary>Per-game numbers (level, phase, speed...).</summary>
    public readonly Dictionary<string, float> Values = new();

    public void Clear()
    {
        Active = false;
        CameraX = CameraY = 0;
        _used = 0;
        Objects.Clear();
        Values.Clear();
    }

    public SceneObject Add(int id, string kind, float x, float y, int frame = 0)
    {
        SceneObject o;
        if (_used < _pool.Count) o = _pool[_used];
        else
        {
            o = new SceneObject();
            _pool.Add(o);
        }
        _used++;
        o.Id = id;
        o.Kind = kind;
        o.X = x;
        o.Y = y;
        o.Frame = frame;
        o.FlipX = false;
        o.Angle = 0;
        o.Size = 1;
        o.Data = 0;
        Objects.Add(o);
        return o;
    }

    private readonly List<SceneObject> _pool = new();

    public float Value(string key, float fallback = 0) => Values.TryGetValue(key, out var v) ? v : fallback;

    public SceneObject? Find(int id, string kind)
    {
        foreach (var o in Objects)
            if (o.Id == id && o.Kind == kind) return o;
        return null;
    }
}

/// <summary>The last two scenes and how far the display is between them.</summary>
internal sealed class SceneTrack
{
    private Scene _prev = new(), _cur = new();

    public Scene Previous => _prev;
    public Scene Current => _cur;
    /// <summary>0 = showing the previous game frame, 1 = the current one.</summary>
    public float Blend = 1;
    /// <summary>Movements larger than this (Oric pixels per frame) are jumps, not motion: not blended.</summary>
    public float MaxStep = 24;

    public Scene Next()
    {
        (_prev, _cur) = (_cur, _prev);
        _cur.Clear();
        return _cur;
    }

    public float Lerp(float a, float b) => Math.Abs(b - a) > MaxStep ? b : a + (b - a) * Blend;

    public float CameraX => _prev.Active ? Lerp(_prev.CameraX, _cur.CameraX) : _cur.CameraX;
    public float CameraY => _prev.Active ? Lerp(_prev.CameraY, _cur.CameraY) : _cur.CameraY;

    /// <summary>An object's blended world position.</summary>
    public Vector2 Position(SceneObject o)
    {
        var p = _prev.Find(o.Id, o.Kind);
        if (p == null) return new Vector2(o.X, o.Y);
        return new Vector2(Lerp(p.X, o.X), Lerp(p.Y, o.Y));
    }

    public float Value(string key, float fallback = 0)
    {
        float b = _cur.Value(key, fallback);
        if (!_prev.Values.TryGetValue(key, out var a)) return b;
        return Lerp(a, b);
    }
}

/// <summary>
/// Smooths things that the game moves in whole steps at their own irregular rates: each time a
/// tracked position changes, the displayed position glides from where it is to the new one over
/// the time the last step took (so a creature stepping every 9 frames glides at that speed).
/// Jumps larger than <see cref="MaxStep"/> (a wrap, a respawn) are taken at once.
/// </summary>
internal sealed class Tweener
{
    private sealed class Track
    {
        public Vector2 From, To;
        public float Start, Duration = 0.15f, LastChange = -1;
        public float Seen;
    }

    private readonly Dictionary<int, Track> _tracks = new();
    public float MaxStep = 30;
    public float MinDuration = 0.04f, MaxDuration = 0.6f;

    /// <summary>Reports where the game has a thing at time <paramref name="t"/> (seconds).</summary>
    public void Set(int id, Vector2 p, float t)
    {
        if (!_tracks.TryGetValue(id, out var k))
        {
            _tracks[id] = new Track { From = p, To = p, Start = t, LastChange = t, Seen = t };
            return;
        }
        k.Seen = t;
        if (p == k.To) return;
        var now = Get(k, t);
        if (Vector2.Distance(p, k.To) > MaxStep)
        {
            k.From = k.To = p;
            k.Start = t;
            k.LastChange = t;
            return;
        }
        k.Duration = Math.Clamp(t - k.LastChange, MinDuration, MaxDuration);
        k.LastChange = t;
        k.From = now;
        k.To = p;
        k.Start = t;
    }

    public Vector2 Get(int id, float t) => _tracks.TryGetValue(id, out var k) ? Get(k, t) : Vector2.Zero;

    private static Vector2 Get(Track k, float t)
    {
        float u = Math.Clamp((t - k.Start) / k.Duration, 0, 1);
        return Vector2.Lerp(k.From, k.To, u);
    }

    /// <summary>Forgets things not reported since <paramref name="before"/>.</summary>
    public void Prune(float before)
    {
        List<int>? drop = null;
        foreach (var (id, k) in _tracks)
            if (k.Seen < before) (drop ??= new List<int>()).Add(id);
        if (drop != null) foreach (var id in drop) _tracks.Remove(id);
    }
}
