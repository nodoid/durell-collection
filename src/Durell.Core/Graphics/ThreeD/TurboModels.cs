using Microsoft.Xna.Framework;

namespace Durell.Graphics.ThreeD;

/// <summary>Low-poly models for Turbo Esprit's city (units: lane-grid rows; x right, y up, -z forward).</summary>
internal static class TurboModels
{
    private static Matrix T(float x, float y, float z) => Matrix.CreateTranslation(x, y, z);

    /// <summary>The Lotus Esprit Turbo: a low red wedge, 7.5 long, nose towards -z.</summary>
    public static readonly MeshBuilder Lotus = BuildLotus();

    private static MeshBuilder BuildLotus()
    {
        var m = new MeshBuilder();
        var red = new Color(200, 24, 28);
        var dark = new Color(30, 30, 34);
        // the wedge: low nose rising to the rear deck
        var nose = new Vector3(0, 0.55f, -3.8f);
        Vector3 P(float x, float y, float z) => new(x, y, z);
        var fl = P(-1.45f, 0.4f, -3.7f); var fr = P(1.45f, 0.4f, -3.7f);
        var fl2 = P(-1.5f, 0.75f, -3.6f); var fr2 = P(1.5f, 0.75f, -3.6f);
        var wl = P(-1.45f, 1.05f, -1.0f); var wr = P(1.45f, 1.05f, -1.0f);       // windscreen base
        var rl = P(-1.2f, 1.75f, 0.6f); var rr = P(1.2f, 1.75f, 0.6f);            // roof
        var bl = P(-1.5f, 1.25f, 3.7f); var br = P(1.5f, 1.25f, 3.7f);            // rear deck
        var bl0 = P(-1.5f, 0.4f, 3.8f); var br0 = P(1.5f, 0.4f, 3.8f);
        m.Quad(fl2, fr2, wr, wl, red);                                              // bonnet
        m.Quad(wl, wr, rr, rl, new Color(40, 60, 80));                              // windscreen
        m.Quad(rl, rr, br, bl, red);                                                // roof and deck
        m.Quad(fl, fr, fr2, fl2, red);                                              // nose
        m.Quad(bl0, br0, br, bl, red);                                              // tail
        m.Quad(fl, fl2, wl, P(-1.5f, 0.4f, -1f), red);
        m.Quad(P(-1.5f, 0.4f, -1f), wl, rl, P(-1.5f, 0.4f, 0.6f), red);
        m.Quad(P(-1.5f, 0.4f, 0.6f), rl, bl, bl0, red);
        m.Quad(fr2, fr, P(1.5f, 0.4f, -1f), wr, red);
        m.Quad(wr, P(1.5f, 0.4f, -1f), P(1.5f, 0.4f, 0.6f), rr, red);
        m.Quad(rr, P(1.5f, 0.4f, 0.6f), br0, br, red);
        m.Quad(P(-1.2f, 1.2f, -0.2f), P(-1.2f, 1.62f, 0.5f), P(-1.45f, 1.3f, 0.5f), P(-1.45f, 1.0f, -0.3f), new Color(40, 60, 80));
        m.Quad(P(1.2f, 1.2f, -0.2f), P(1.45f, 1.0f, -0.3f), P(1.45f, 1.3f, 0.5f), P(1.2f, 1.62f, 0.5f), new Color(40, 60, 80));
        _ = nose;
        // wheels, lights, spoiler
        foreach (float x in new[] { -1.45f, 1.45f })
            foreach (float z in new[] { -2.4f, 2.4f })
                m.Cylinder(Matrix.CreateScale(0.5f, 1.3f, 1.3f) * T(x, 0.6f, z), dark, 10);
        m.Box(new Vector3(0, 0.9f, 3.82f), new Vector3(2.4f, 0.25f, 0.05f), new Color(230, 40, 40));
        m.Box(new Vector3(-0.95f, 0.62f, -3.75f), new Vector3(0.6f, 0.15f, 0.05f), new Color(255, 250, 220));
        m.Box(new Vector3(0.95f, 0.62f, -3.75f), new Vector3(0.6f, 0.15f, 0.05f), new Color(255, 250, 220));
        m.Box(new Vector3(0, 1.55f, 3.5f), new Vector3(2.9f, 0.1f, 0.5f), red);
        return m;
    }

    /// <summary>A saloon car, 6.5 long, nose towards -z (coloured when added).</summary>
    public static MeshBuilder Saloon(Color body)
    {
        var m = new MeshBuilder();
        var glass = new Color(50, 70, 90);
        m.Box(new Vector3(0, 0.95f, 0), new Vector3(2.7f, 1.0f, 6.4f), body);
        m.Box(new Vector3(0, 1.85f, 0.4f), new Vector3(2.4f, 0.85f, 3.2f), glass, body);
        foreach (float x in new[] { -1.3f, 1.3f })
            foreach (float z in new[] { -2.1f, 2.1f })
                m.Cylinder(Matrix.CreateScale(0.45f, 1.2f, 1.2f) * T(x, 0.6f, z), new Color(25, 25, 28), 8);
        m.Box(new Vector3(0, 1.05f, 3.21f), new Vector3(2.2f, 0.25f, 0.05f), new Color(220, 40, 40));
        m.Box(new Vector3(0, 1.05f, -3.21f), new Vector3(2.2f, 0.25f, 0.05f), new Color(255, 250, 220));
        return m;
    }

    /// <summary>A pedestrian, about 3.4 tall.</summary>
    public static MeshBuilder Walker(Color coat, float stride)
    {
        var m = new MeshBuilder();
        m.Box(Matrix.CreateScale(0.35f, 1.5f, 0.35f) * Matrix.CreateRotationX(stride) * T(-0.25f, 0.75f, 0), new Color(40, 40, 60));
        m.Box(Matrix.CreateScale(0.35f, 1.5f, 0.35f) * Matrix.CreateRotationX(-stride) * T(0.25f, 0.75f, 0), new Color(40, 40, 60));
        m.Box(new Vector3(0, 2.2f, 0), new Vector3(0.9f, 1.4f, 0.5f), coat);
        m.Box(new Vector3(0, 3.15f, 0), new Vector3(0.5f, 0.5f, 0.5f), new Color(220, 180, 150));
        return m;
    }

    /// <summary>A street lamp, 10 tall, its arm over the road (towards -x).</summary>
    public static readonly MeshBuilder Lamp = BuildLamp();

    private static MeshBuilder BuildLamp()
    {
        var m = new MeshBuilder();
        var steel = new Color(70, 76, 80);
        m.Box(new Vector3(0, 5, 0), new Vector3(0.3f, 10, 0.3f), steel);
        m.Box(new Vector3(-1.2f, 10, 0), new Vector3(2.6f, 0.25f, 0.25f), steel);
        m.Box(new Vector3(-2.4f, 9.8f, 0), new Vector3(1, 0.3f, 0.6f), new Color(255, 240, 200));
        return m;
    }

    /// <summary>A traffic light on its pole; <paramref name="state"/> 0 red, 1 red+amber, 2 green, 3 amber.</summary>
    public static MeshBuilder Light(int state)
    {
        var m = new MeshBuilder();
        m.Box(new Vector3(0, 3, 0), new Vector3(0.3f, 6, 0.3f), new Color(50, 54, 56));
        m.Box(new Vector3(0, 7, 0), new Vector3(0.9f, 2.4f, 0.6f), new Color(30, 32, 34));
        Color Lit(bool on, Color c) => on ? c : new Color(c.R / 5, c.G / 5, c.B / 5);
        m.Box(new Vector3(0, 7.75f, 0.31f), new Vector3(0.55f, 0.55f, 0.05f), Lit(state is 0 or 1, new Color(255, 40, 30)));
        m.Box(new Vector3(0, 7.0f, 0.31f), new Vector3(0.55f, 0.55f, 0.05f), Lit(state is 1 or 3, new Color(255, 180, 20)));
        m.Box(new Vector3(0, 6.25f, 0.31f), new Vector3(0.55f, 0.55f, 0.05f), Lit(state == 2, new Color(40, 255, 80)));
        return m;
    }
}
