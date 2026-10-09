using System;
using System.Diagnostics;
using Microsoft.Xna.Framework.Audio;

namespace Durell.Audio;

/// <summary>
/// Streams the games' sound (stereo, 44.1 kHz) to the sound card, one 50 Hz frame at a time,
/// keeping the queue short so the sound stays in step with the picture. The capture tool can take
/// the samples instead (<see cref="OfflineSink"/>).
/// </summary>
public sealed class AudioOut : IDisposable
{
    public const int Rate = 44100;
    private const int MaxPending = 4;
    private DynamicSoundEffectInstance? _out;
    private byte[] _pcm = new byte[8192];
    private float _gain = 1f;
    private float _fade = 1f;

    public bool Available { get; private set; }
    public bool Muted { get; set; }
    public float Volume { get; set; } = 0.9f;
    public Action<ReadOnlySpan<float>>? OfflineSink { get; set; }

    public void Start()
    {
        try
        {
            _out = new DynamicSoundEffectInstance(Rate, AudioChannels.Stereo);
            _out.Play();
            Available = true;
        }
        catch (Exception ex) when (ex is NoAudioHardwareException or InvalidOperationException or PlatformNotSupportedException
                                       or TypeInitializationException or DllNotFoundException)
        {
            Debug.WriteLine("Durell: no audio: " + ex.Message);
            Available = false;
        }
    }

    /// <summary>True when the card has enough queued that this frame's sound can be dropped.</summary>
    public bool Saturated => OfflineSink == null && _out != null && _out.PendingBufferCount >= MaxPending;

    /// <summary>Queues interleaved stereo samples.</summary>
    public void Submit(ReadOnlySpan<float> stereo)
    {
        if (OfflineSink != null)
        {
            OfflineSink(stereo);
            return;
        }
        if (_out == null) return;
        if (_out.PendingBufferCount >= MaxPending) return;
        if (_out.PendingBufferCount == 0)
        {
            // starved (a hitch, or just started): a short cushion of silence first
            Array.Clear(_pcm, 0, 1764 * 2);
            _out.SubmitBuffer(_pcm, 0, 1764 * 2);
        }
        int n = stereo.Length;
        if (_pcm.Length < n * 2) _pcm = new byte[n * 2];
        float target = Muted ? 0 : Volume;
        for (int i = 0; i < n; i++)
        {
            _fade += (target - _fade) * 0.001f;
            float x = stereo[i] * _fade * 1.6f;
            // gentle limiter, then soft clip
            float a = MathF.Abs(x) * _gain;
            if (a > 0.95f) _gain = 0.95f / MathF.Abs(x);
            else _gain = MathF.Min(1f, _gain + 0.00002f);
            x = MathF.Tanh(x * _gain);
            short s = (short)(x * short.MaxValue);
            _pcm[i * 2] = (byte)s;
            _pcm[i * 2 + 1] = (byte)(s >> 8);
        }
        _out.SubmitBuffer(_pcm, 0, n * 2);
    }

    /// <summary>A short Oric-style beep, so a new volume can be heard (on the menu, where nothing else plays).</summary>
    public void Beep()
    {
        if (OfflineSink != null || _out == null || _out.PendingBufferCount > 1) return;
        var s = new float[Rate / 9 * 2];
        for (int i = 0; i < s.Length / 2; i++)
        {
            float env = MathF.Min(1, i / 200f) * MathF.Min(1, (s.Length / 2 - i) / 600f);
            float sq = (i / (Rate / 1760 / 2)) % 2 == 0 ? 0.22f : -0.22f;     // A6 square wave, like the AY chip
            s[i * 2] = s[i * 2 + 1] = sq * env;
        }
        _fade = Muted ? 0 : Volume;
        Submit(s);
    }

    public void Pause() => _out?.Pause();
    public void Resume() => _out?.Resume();

    public void Dispose()
    {
        _out?.Stop();
        _out?.Dispose();
        _out = null;
    }
}
