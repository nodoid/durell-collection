using System;
using Microsoft.Xna.Framework;

namespace Durell.Input;

/// <summary>
/// Tilting a phone or tablet as a joystick. The platform head supplies gravity in screen axes
/// (x to the right of the picture, y to its top, z out of the glass; in g); this turns it into
/// angles from a neutral position (taken when play starts or resumes, or on CENTRE) and into the
/// four directions, with a dead zone and hysteresis so a steady hand doesn't flicker.
/// </summary>
public sealed class Tilt
{
    /// <summary>Degrees from neutral before a direction engages, and below which it lets go.</summary>
    public const float On = 9, Off = 5;

    private Vector3 _g;
    private bool _have, _centreNext = true;
    private float _roll0, _pitch0;

    /// <summary>Gravity in screen axes, or null when there is no accelerometer.</summary>
    public Func<Vector3?>? Provider { get; set; }
    public bool Available => _have;
    /// <summary>Degrees from neutral: + is the right side down.</summary>
    public float Roll { get; private set; }
    /// <summary>Degrees from neutral: + is the top edge tipped away from you.</summary>
    public float Pitch { get; private set; }
    public bool Left { get; private set; }
    public bool Right { get; private set; }
    public bool Up { get; private set; }
    public bool Down { get; private set; }

    /// <summary>Makes the way the device is held now the neutral position (on the next reading).</summary>
    public void Centre() => _centreNext = true;

    public void Update()
    {
        var reading = Provider?.Invoke();
        if (reading is not Vector3 g || g.LengthSquared() < 0.04f)
        {
            _have = false;
            Left = Right = Up = Down = false;
            return;
        }
        // a light low-pass: sensors are noisy
        _g = _have ? Vector3.Lerp(_g, g, 0.35f) : g;
        _have = true;
        float roll = MathF.Atan2(_g.X, MathF.Sqrt(_g.Y * _g.Y + _g.Z * _g.Z));
        float pitch = MathF.Atan2(-_g.Y, -_g.Z);
        if (_centreNext)
        {
            _centreNext = false;
            _roll0 = roll;
            _pitch0 = pitch;
        }
        Roll = MathHelper.ToDegrees(MathHelper.WrapAngle(roll - _roll0));
        Pitch = MathHelper.ToDegrees(MathHelper.WrapAngle(_pitch0 - pitch));
        Right = Engage(Right, Roll);
        Left = Engage(Left, -Roll);
        Up = Engage(Up, Pitch);
        Down = Engage(Down, -Pitch);
    }

    private static bool Engage(bool was, float degrees) => degrees > (was ? Off : On);

    /// <summary>Tipping the top edge towards you as a 0-9 power setting (Lunar Lander's motors).</summary>
    public int Power()
    {
        float back = -Pitch;
        return Math.Clamp((int)MathF.Round((back - 4) / 26f * 9), 0, 9);
    }
}
