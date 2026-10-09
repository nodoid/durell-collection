using System;

namespace Durell.Audio;

/// <summary>
/// The sound of Harrier Attack 3D, made in code and driven by the game's state each frame: the jet's
/// engine (turbine whine and roar following the speed, swelling on take-off), the wind, rockets,
/// bombs (release clunk and falling whistle), explosions, flak, enemy jets flying past, missile-lock
/// and low-fuel warnings, a terrain warning when flying too low, the touchdown on the deck, a
/// fanfare for a landing and the engine winding down.
/// </summary>
internal sealed class HarrierSound
{
    /// <summary>What the game is doing this frame (filled in by the 3D look).</summary>
    public struct State
    {
        public bool Playing;
        public int Phase;               // $216F
        public int Speed;               // $2113: 10..200
        public int Fuel;                // $216C/D
        public float PlaneX, PlaneRow;  // screen column / text row
        public bool Climbing;
        public bool Rockets;            // a rocket streak this frame
        public bool Bomb;               // bomb slot active
        public float BombRow, BombX;
        public bool Jet;
        public float JetX;
        public bool Missile, Sam;
        public float MissileX, SamX;
        public int Debris;              // debris cells in view
        public int Flak;
        public bool Crashed;
        public int Landing;             // $217A: 0 flying, 1 landed, 2 crashed
        public float Clearance;         // world units between the plane and the land below (big over the sea)
        public int Score;
    }

    private readonly Synth _synth = new();
    private State _prev;
    private bool _first = true;

    // the continuous voices are rendered here directly
    private float _enginePhase, _whinePhase, _roarLp, _roarLp2, _windLp, _whistlePhase;
    private float _engineLevel, _enginePitch, _wind, _whistle, _bombPitch;
    private float _spool;               // 0 idle .. 1 full power (eases)
    private float _warnTimer, _fuelTimer, _flakTimer, _missileTimer;
    private float _jetLevel, _jetPan, _jetPhase, _jetLp;
    private float _shutdown;            // after landing: the engine winding down
    private readonly Random _rng = new(7);

    private static float Pan(float x) => Math.Clamp(x / 20f - 1, -1, 1);

    public void Update(in State s, float dt)
    {
        if (!s.Playing)
        {
            _engineLevel = 0;
            _first = true;
            return;
        }
        if (_first)
        {
            _prev = s;
            _first = false;
        }
        // take-off and speed set the engine; the landing (and the menu) wind it down
        float want = s.Phase == 0 ? 0.35f : 0.45f + s.Speed / 200f * 0.55f;
        if (s.Climbing) want += 0.15f;
        if (s.Landing == 1) _shutdown = MathF.Min(1, _shutdown + dt / 4);
        else if (s.Landing == 0) _shutdown = 0;
        if (s.Crashed || s.Landing == 2) want = 0;
        want *= 1 - _shutdown;
        _spool += (want - _spool) * MathF.Min(1, dt * (want > _spool ? 1.4f : 0.8f));

        float pan = Pan(s.PlaneX);
        if (s.Rockets && !_prev.Rockets)
        {
            _synth.Play(new RocketWhoosh(pan - 0.1f));
            _synth.Play(new RocketWhoosh(pan + 0.1f) { Gain = 0.3f });
        }
        if (s.Bomb && !_prev.Bomb)
        {
            _synth.Play(new Clunk(pan));
            _bombPitch = 1;
        }
        if (s.Debris > _prev.Debris) _synth.Play(new Boom(0.5f, Pan(s.BombX)));
        else if (!s.Bomb && _prev.Bomb) _synth.Play(new Boom(0.15f, Pan(_prev.BombX)) { Gain = 0.3f });   // a splash or a miss
        if (s.Crashed && !_prev.Crashed || s.Landing == 2 && _prev.Landing != 2)
        {
            _synth.Play(new Boom(1, pan));
            _synth.Play(new Boom(0.6f, pan + 0.3f) { Time = -0.3f });
        }
        if (s.Jet && !_prev.Jet) _jetLevel = 0.01f;
        if (s.Missile && !_prev.Missile) _synth.Play(new RocketWhoosh(Pan(s.MissileX)) { Gain = 0.3f });
        if (s.Sam && !_prev.Sam) _synth.Play(new RocketWhoosh(Pan(s.SamX)) { Gain = 0.35f });
        if (s.Landing == 1 && _prev.Landing != 1)
        {
            _synth.Play(new Screech());
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int i = 0; i < notes.Length; i++)
                _synth.Play(new Tone(notes[i], 0.5f, 0.22f, 0, 0, 0.01f, 0.25f) { Time = -0.6f - i * 0.16f });
        }
        if (s.Score > _prev.Score && s.Debris <= _prev.Debris)
            _synth.Play(new Tone(1320, 0.12f, 0.12f, pan, 1760, 0.003f, 0.05f));

        // warnings
        _warnTimer -= dt;
        if (s.Phase != 0 && s.Landing == 0 && !s.Crashed && s.Clearance < 14 && _warnTimer <= 0)
        {
            // terrain: fast urgent beeps
            _synth.Play(new Tone(980, 0.08f, 0.16f, 0, 0, 0.002f, 0, 0.6f));
            _warnTimer = 0.2f;
        }
        _missileTimer -= dt;
        if ((s.Missile || s.Sam) && _missileTimer <= 0)
        {
            // missile lock: an alternating two-tone warble
            _synth.Play(new Tone(1250, 0.07f, 0.12f, 0, 0, 0.002f, 0, 0.5f));
            _synth.Play(new Tone(950, 0.07f, 0.12f, 0, 0, 0.002f, 0, 0.5f) { Time = -0.09f });
            _missileTimer = 0.25f;
        }
        _fuelTimer -= dt;
        if (s.Fuel < 712 && s.Phase is 2 or 3 or 4 or 5 && s.Landing == 0 && !s.Crashed && _fuelTimer <= 0)
        {
            _synth.Play(new Tone(1000, 0.3f, 0.14f, 0, 0, 0.005f, 0.12f));
            _synth.Play(new Tone(750, 0.4f, 0.14f, 0, 0, 0.005f, 0.15f) { Time = -0.32f });
            _fuelTimer = 2.2f;
        }
        _flakTimer -= dt;
        if (s.Flak > 0 && _flakTimer <= 0)
        {
            _synth.Play(new Boom(0.05f, (float)_rng.NextDouble() * 1.6f - 0.8f) { Gain = 0.18f });
            _flakTimer = 0.35f + (float)_rng.NextDouble() * 0.6f;
        }

        // continuous sounds' targets
        _whistle = s.Bomb ? 1 : 0;
        if (s.Bomb) _bombPitch = MathF.Max(0.35f, 1 - (s.BombRow - _prev.BombRow) * 0 - (s.BombRow / 24f) * 0.6f);
        _wind = 0.15f + s.Speed / 200f * 0.25f;
        if (s.Jet) _jetPan = Pan(s.JetX);
        _prev = s;
    }

    /// <summary>Renders one frame of sound (interleaved stereo) - replaces the Oric's.</summary>
    public void Render(Span<float> stereo)
    {
        stereo.Clear();
        float dt = 1f / Synth.Rate;
        for (int i = 0; i < stereo.Length; i += 2)
        {
            _engineLevel += (_spool - _engineLevel) * 0.0005f;
            _enginePitch += (_spool - _enginePitch) * 0.0003f;
            float level = _engineLevel;
            // turbine: a buzzy fundamental through a low-pass, a roar of filtered noise and a high whine
            _enginePhase += MathF.Tau * (55 + 70 * _enginePitch) * dt;
            float saw = (_enginePhase / MathF.Tau % 1) * 2 - 1;
            float n = _synthNoise();
            float roar = LowPassStatic(ref _roarLp, n, 300 + 1500 * level);
            roar = LowPassStatic(ref _roarLp2, roar, 500 + 2000 * level);
            _whinePhase += MathF.Tau * (1800 + 2600 * _enginePitch) * dt;
            float whine = MathF.Sin(_whinePhase) * 0.05f * level;
            float engine = (saw * 0.12f + roar * 1.6f + whine) * level;
            float wind = LowPassStatic(ref _windLp, n, 700) * _wind * 0.6f;
            // the bomb's falling whistle
            float whistle = 0;
            if (_whistle > 0)
            {
                _whistlePhase += MathF.Tau * (500 + 1400 * _bombPitch) * dt;
                whistle = MathF.Sin(_whistlePhase) * 0.07f;
            }
            // an enemy jet roaring past
            float jet = 0;
            if (_jetLevel > 0)
            {
                _jetLevel = MathF.Max(0, _jetLevel + (_jetLevel < 0.6f && _jetPan > -0.9f ? 0.00004f : -0.00002f));
                _jetPhase += MathF.Tau * (90 + 60 * (_jetPan + 1)) * dt;
                jet = (LowPassStatic(ref _jetLp, n, 1200) * 1.4f + ((_jetPhase / MathF.Tau % 1) - 0.5f) * 0.1f) * _jetLevel;
            }
            float jl = MathF.Sqrt(0.5f * (1 - _jetPan)), jr = MathF.Sqrt(0.5f * (1 + _jetPan));
            stereo[i] = engine * 0.55f + wind + whistle + jet * jl;
            stereo[i + 1] = engine * 0.55f + wind * 0.9f + whistle + jet * jr;
        }
        _synth.Render(stereo);
    }

    private float _synthNoise() => _synth.NextNoise();

    private static float LowPassStatic(ref float state, float input, float cutoffHz)
    {
        float a = 1 - MathF.Exp(-MathF.Tau * cutoffHz / Synth.Rate);
        state += (input - state) * a;
        return state;
    }
}
