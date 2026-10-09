using System;
using System.Collections.Generic;

namespace Durell.Audio;

/// <summary>
/// A small real-time synthesiser for sound effects made from scratch (no recordings): one-shot
/// voices built from oscillators, filtered noise and envelopes, mixed in stereo at 44.1 kHz.
/// </summary>
internal sealed class Synth
{
    public const int Rate = 44100;
    private readonly List<Voice> _voices = new();
    private uint _seed = 0x1234567;

    public float NextNoise()
    {
        _seed ^= _seed << 13;
        _seed ^= _seed >> 17;
        _seed ^= _seed << 5;
        return (_seed & 0xFFFFFF) / (float)0x800000 - 1;
    }

    public void Play(Voice v)
    {
        v.Synth = this;
        if (_voices.Count < 48) _voices.Add(v);
    }

    public int Count(Type t)
    {
        int n = 0;
        foreach (var v in _voices) if (v.GetType() == t) n++;
        return n;
    }

    /// <summary>Adds every voice into an interleaved stereo buffer.</summary>
    public void Render(Span<float> stereo)
    {
        for (int i = _voices.Count - 1; i >= 0; i--)
        {
            var v = _voices[i];
            v.Render(stereo);
            if (v.Done) _voices.RemoveAt(i);
        }
    }

    public void StopAll() => _voices.Clear();
}

/// <summary>One sound. <see cref="Sample"/> returns the next mono sample; it is panned by <see cref="Pan"/>.</summary>
internal abstract class Voice
{
    public Synth Synth = null!;
    public float Gain = 1;
    /// <summary>-1 left .. +1 right.</summary>
    public float Pan;
    public float Time;
    public float Length = 1;
    public bool Done;
    protected const float Dt = 1f / Synth.Rate;

    public virtual void Render(Span<float> stereo)
    {
        float l = Gain * MathF.Sqrt(0.5f * (1 - Pan)), r = Gain * MathF.Sqrt(0.5f * (1 + Pan));
        for (int i = 0; i < stereo.Length; i += 2)
        {
            if (Time >= Length)
            {
                Done = true;
                return;
            }
            if (Time < 0)
            {
                // a delayed start: silence until then
                Time += Dt;
                continue;
            }
            float s = Sample();
            stereo[i] += s * l;
            stereo[i + 1] += s * r;
            Time += Dt;
        }
    }

    protected abstract float Sample();

    protected float Noise() => Synth.NextNoise();

    /// <summary>One-pole low-pass state helper.</summary>
    protected static float LowPass(ref float state, float input, float cutoffHz)
    {
        float a = 1 - MathF.Exp(-MathF.Tau * cutoffHz / Synth.Rate);
        state += (input - state) * a;
        return state;
    }

    protected static float Env(float t, float attack, float decay) =>
        t < attack ? t / attack : MathF.Exp(-(t - attack) / decay);
}

/// <summary>An explosion: a noise blast whose brightness falls away, a deep thump, and crackle.</summary>
internal sealed class Boom : Voice
{
    private float _lp1, _lp2, _phase;
    private readonly float _size;

    public Boom(float size, float pan)
    {
        _size = size;
        Pan = pan;
        Length = 1.2f + size * 1.4f;
        Gain = 0.55f + size * 0.45f;
    }

    protected override float Sample()
    {
        float t = Time;
        float cutoff = 3000 * MathF.Exp(-t * (3.5f - _size)) + 80;
        float n = LowPass(ref _lp1, Noise(), cutoff);
        n = LowPass(ref _lp2, n, cutoff * 1.5f);
        float env = Env(t, 0.004f, 0.25f + _size * 0.45f);
        _phase += MathF.Tau * (55 - t * 20) * Dt;
        float thump = MathF.Sin(_phase) * MathF.Exp(-t * 6) * 0.9f;
        float crackle = Noise() > 0.995f - _size * 0.004f ? Noise() * 0.6f * MathF.Exp(-t * 2) : 0;
        return (n * 2.2f + thump) * env + crackle;
    }
}

/// <summary>A rocket leaving the rail: a sharp crack, then a hissing whoosh that rises then fades.</summary>
internal sealed class RocketWhoosh : Voice
{
    private float _lp, _hp;

    public RocketWhoosh(float pan)
    {
        Pan = pan;
        Length = 0.7f;
        Gain = 0.4f;
    }

    protected override float Sample()
    {
        float t = Time;
        float n = Noise();
        float lp = LowPass(ref _lp, n, 2500 + 4000 * MathF.Exp(-t * 4));
        _hp = LowPass(ref _hp, lp, 300);
        float whoosh = (lp - _hp) * Env(t, 0.02f, 0.25f);
        float crack = t < 0.012f ? n * (1 - t / 0.012f) * 1.5f : 0;
        return whoosh * 1.6f + crack;
    }
}

/// <summary>A bomb leaving its rack: a metallic clunk.</summary>
internal sealed class Clunk : Voice
{
    private float _p1, _p2;

    public Clunk(float pan)
    {
        Pan = pan;
        Length = 0.25f;
        Gain = 0.35f;
    }

    protected override float Sample()
    {
        _p1 += MathF.Tau * 420 * Dt;
        _p2 += MathF.Tau * 1130 * Dt;
        return (MathF.Sin(_p1) + 0.5f * MathF.Sin(_p2)) * Env(Time, 0.002f, 0.05f) + Noise() * 0.3f * Env(Time, 0.001f, 0.01f);
    }
}

/// <summary>A tone: a beep, chime or note, with harmonics and a pitch slide.</summary>
internal sealed class Tone : Voice
{
    private readonly float _f0, _f1, _attack, _decay, _square;
    private float _phase;

    public Tone(float freq, float length, float gain, float pan = 0, float endFreq = 0, float attack = 0.005f, float decay = 0, float square = 0)
    {
        _f0 = freq;
        _f1 = endFreq > 0 ? endFreq : freq;
        Length = length;
        Gain = gain;
        Pan = pan;
        _attack = attack;
        _decay = decay;
        _square = square;
    }

    protected override float Sample()
    {
        float k = Time / Length;
        float f = _f0 + (_f1 - _f0) * k;
        _phase += MathF.Tau * f * Dt;
        float s = MathF.Sin(_phase) + 0.25f * MathF.Sin(_phase * 2) + 0.1f * MathF.Sin(_phase * 3);
        if (_square > 0) s = s * (1 - _square) + MathF.Sign(MathF.Sin(_phase)) * _square * 0.6f;
        float env = Time < _attack ? Time / _attack : 1;
        if (_decay > 0) env *= MathF.Exp(-(Time - _attack) / _decay);
        float release = MathF.Min(1, (Length - Time) / 0.01f);
        return s * env * release * 0.5f;
    }
}

/// <summary>Tyres meeting the deck: a short squeal and a bump.</summary>
internal sealed class Screech : Voice
{
    private float _bp1, _bp2, _p;

    public Screech()
    {
        Length = 0.6f;
        Gain = 0.4f;
    }

    protected override float Sample()
    {
        float n = Noise();
        float a = LowPass(ref _bp1, n, 3200);
        float b = LowPass(ref _bp2, a, 1800);
        _p += MathF.Tau * 70 * Dt;
        return (a - b) * 3 * Env(Time, 0.01f, 0.15f) + MathF.Sin(_p) * Env(Time, 0.002f, 0.08f);
    }
}
