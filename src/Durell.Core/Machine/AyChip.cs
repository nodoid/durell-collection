using System;
using System.Collections.Generic;

namespace Durell.Machine;

/// <summary>
/// AY-3-8912 sound generator (1 MHz, as in the Oric): three square-wave tone
/// channels, a noise generator, the envelope generator and the logarithmic
/// volume steps.  Register writes are applied at the cycle the program made
/// them.  Two outputs: ORIGINAL (mono, the raw chip) and ENHANCED (band-
/// limited, stereo spread, warmed with a low-pass and a little room reverb).
/// </summary>
internal sealed class AyChip
{
    public const int Rate = 44100;
    private const double Clock = 1_000_000;
    private const int Sub = 8;                       // internal steps per tone tick (clock / 8)

    private static readonly float[] Vol =
    {
        0f, 0.0137f, 0.0205f, 0.0291f, 0.0423f, 0.0618f, 0.0847f, 0.1369f,
        0.1691f, 0.2647f, 0.3527f, 0.4499f, 0.5704f, 0.6873f, 0.8482f, 1f,
    };

    private readonly int[] _r = new int[16];
    private readonly int[] _toneCount = new int[3];
    private readonly int[] _toneOut = { 1, 1, 1 };
    private int _noiseCount, _noiseLfsr = 1, _noiseOut = 1;
    private int _envCount, _envStep, _envHold, _envAlt, _envAttack, _envVol;
    private bool _envDone;
    private double _phase;                           // AY ticks (clock/8) owed to the next sample

    // enhanced output state
    private float _lpL, _lpR, _hpL, _hpR, _hpInL, _hpInR, _dcO;
    private readonly float[] _revL = new float[4410], _revR = new float[3527];
    private int _revPosL, _revPosR;
    private readonly float[] _smooth = new float[3];

    public void Reset()
    {
        Array.Clear(_r);
        _r[7] = 0xFF;
        _envDone = true;
    }

    public void Write(int reg, int v)
    {
        reg &= 15;
        _r[reg] = v & 0xFF;
        if (reg == 13)
        {
            _envStep = 0;
            _envDone = false;
            _envCount = 0;
            _envAttack = (v & 4) != 0 ? 15 : 0;
            _envHold = v & 1;
            _envAlt = v & 2;
            if ((v & 8) == 0) _envHold = 1;          // \___ or /___: one ramp, then silence
            _envVol = _envAttack != 0 ? 0 : 15;
        }
    }

    /// <summary>
    /// Renders <paramref name="n"/> stereo samples (interleaved L R) covering
    /// cycles [start, start + cycles), applying the logged writes on time.
    /// </summary>
    public void Render(Span<float> dst, int n, long start, double cycles, List<OricBus.AyWrite> log, ref int logPos, bool enhanced)
    {
        double cyclesPerSample = cycles / n;
        double ticksPerSample = Clock / Sub / Rate * (cyclesPerSample / (Clock / Rate));
        for (int i = 0; i < n; i++)
        {
            long now = start + (long)(i * cyclesPerSample);
            while (logPos < log.Count && log[logPos].Cycle <= now)
            {
                Write(log[logPos].Reg, log[logPos].Value);
                logPos++;
            }
            _phase += ticksPerSample;
            int steps = (int)_phase;
            _phase -= steps;
            float a = 0, b = 0, c = 0;
            for (int s = 0; s < steps; s++)
            {
                Tick();
                a += Level(0);
                b += Level(1);
                c += Level(2);
            }
            if (steps > 0)
            {
                a /= steps;
                b /= steps;
                c /= steps;
            }
            if (!enhanced)
            {
                float m = (a + b + c) * 0.45f;
                // remove DC like the Oric's output capacitor
                _dcO = _dcO * 0.9995f + m * 0.0005f;
                m -= _dcO;
                dst[i * 2] = dst[i * 2 + 1] = m;
            }
            else
            {
                // soften the edges of each channel a little (anti-alias / warmth)
                _smooth[0] += (a - _smooth[0]) * 0.55f;
                _smooth[1] += (b - _smooth[1]) * 0.55f;
                _smooth[2] += (c - _smooth[2]) * 0.55f;
                float l = _smooth[0] * 0.75f + _smooth[1] * 0.5f + _smooth[2] * 0.25f;
                float r = _smooth[0] * 0.25f + _smooth[1] * 0.5f + _smooth[2] * 0.75f;
                _lpL += (l - _lpL) * 0.35f;
                _lpR += (r - _lpR) * 0.35f;
                // DC blocker
                float hl = _lpL - _hpInL + 0.9993f * _hpL;
                _hpInL = _lpL;
                _hpL = hl;
                float hr = _lpR - _hpInR + 0.9993f * _hpR;
                _hpInR = _lpR;
                _hpR = hr;
                // small room: two cross-fed comb delays
                float dl = _revL[_revPosL], dr = _revR[_revPosR];
                _revL[_revPosL] = hl * 0.5f + dr * 0.42f;
                _revR[_revPosR] = hr * 0.5f + dl * 0.42f;
                _revPosL = (_revPosL + 1) % _revL.Length;
                _revPosR = (_revPosR + 1) % _revR.Length;
                dst[i * 2] = (hl * 0.8f + dl * 0.22f) * 0.6f;
                dst[i * 2 + 1] = (hr * 0.8f + dr * 0.22f) * 0.6f;
            }
        }
        while (logPos < log.Count && log[logPos].Cycle < start + (long)cycles)
        {
            Write(log[logPos].Reg, log[logPos].Value);
            logPos++;
        }
    }

    private float Level(int ch)
    {
        int mix = _r[7];
        bool toneOff = (mix & (1 << ch)) != 0, noiseOff = (mix & (8 << ch)) != 0;
        int on = (toneOff ? 1 : _toneOut[ch]) & (noiseOff ? 1 : _noiseOut);
        if (on == 0) return 0f;
        int amp = _r[8 + ch];
        return (amp & 0x10) != 0 ? Vol[_envVol] : Vol[amp & 15];
    }

    private void Tick()
    {
        // tone: the output flips every period * 8 clocks (one tick here = 8 clocks)
        for (int ch = 0; ch < 3; ch++)
        {
            int period = _r[ch * 2] | (_r[ch * 2 + 1] & 15) << 8;
            if (period == 0) period = 1;
            if (++_toneCount[ch] >= period)
            {
                _toneCount[ch] = 0;
                _toneOut[ch] ^= 1;
            }
        }
        // noise: clocked at half the tone rate
        int np = _r[6] & 31;
        if (np == 0) np = 1;
        if (++_noiseCount >= np * 2)
        {
            _noiseCount = 0;
            int bit = (_noiseLfsr ^ (_noiseLfsr >> 3)) & 1;
            _noiseLfsr = (_noiseLfsr >> 1) | (bit << 16);
            _noiseOut = _noiseLfsr & 1;
        }
        // envelope: 16 steps per period * 256 clocks, so a step every period * 2 ticks
        int ep = _r[11] | _r[12] << 8;
        if (ep == 0) ep = 1;
        if (++_envCount >= ep * 2)
        {
            _envCount = 0;
            if (!_envDone)
            {
                _envStep++;
                if (_envStep > 15)
                {
                    if (_envHold != 0)
                    {
                        _envDone = true;
                        int last = _envAttack != 0 ? 15 : 0;
                        if (_envAlt != 0) last ^= 15;
                        if ((_r[13] & 8) == 0) last = 0;
                        _envVol = last;
                        return;
                    }
                    _envStep = 0;
                    if (_envAlt != 0) _envAttack ^= 15;
                }
                _envVol = _envAttack != 0 ? _envStep : 15 - _envStep;
            }
        }
    }
}
