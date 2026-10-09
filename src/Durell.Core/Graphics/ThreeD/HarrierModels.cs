using Microsoft.Xna.Framework;

namespace Durell.Graphics.ThreeD;

/// <summary>
/// Low-poly models for Harrier Attack 3D, in world units (1 = one Oric pixel); x points the way the
/// model faces, y up.
/// </summary>
internal static class HarrierModels
{
    private static Matrix S(float x, float y, float z) => Matrix.CreateScale(x, y, z);
    private static Matrix T(float x, float y, float z) => Matrix.CreateTranslation(x, y, z);

    private static readonly Color Grey = new(92, 102, 112), Green = new(70, 84, 60), Light = new(160, 168, 176);

    /// <summary>The Harrier GR.3: about 22 long, nose at +x.</summary>
    public static readonly MeshBuilder Jet = BuildJet();

    private static MeshBuilder BuildJet()
    {
        var m = new MeshBuilder();
        m.Cylinder(S(16, 3.4f, 3.2f) * T(1, 0, 0), Grey);                       // fuselage
        m.Cylinder(S(5, 3.4f, 3.2f) * T(11.5f, 0, 0), Grey, 8, 0.05f);           // nose cone
        m.Cylinder(S(5, 2.4f, 2.2f) * T(-9.5f, 0.3f, 0), Grey, 8, 0.5f);         // tail cone
        m.Box(new Vector3(5.5f, 1.6f, 0), new Vector3(4.5f, 1.8f, 1.6f), new Color(50, 80, 110));   // canopy
        m.Box(new Vector3(3.5f, -0.2f, 0), new Vector3(3, 2.4f, 4.6f), Grey);    // intakes
        // wings (anhedral) and tailplane
        var wing = new[] { new Vector2(-4, 0), new Vector2(3, 0), new Vector2(-1, 9), new Vector2(-4, 9) };
        m.Plate(Matrix.CreateRotationX(-MathHelper.PiOver2 + 0.12f) * T(0, 1.2f, 0.4f), Green, wing);
        m.Plate(Matrix.CreateRotationX(MathHelper.PiOver2 - 0.12f) * T(0, 1.2f, -0.4f), Green,
            new Vector2(-4, 0), new Vector2(-4, 9), new Vector2(-1, 9), new Vector2(3, 0));
        var tp = new[] { new Vector2(-12, 0), new Vector2(-8.5f, 0), new Vector2(-10.5f, 4), new Vector2(-12, 4) };
        m.Plate(Matrix.CreateRotationX(-MathHelper.PiOver2) * S(1, 1, 0.5f) * T(0, 0.5f, 0.3f), Green, tp);
        m.Plate(Matrix.CreateRotationX(MathHelper.PiOver2) * S(1, 1, 0.5f) * T(0, 0.5f, -0.3f), Green,
            new Vector2(-12, 0), new Vector2(-12, 4), new Vector2(-10.5f, 4), new Vector2(-8.5f, 0));
        // fin
        m.Plate(S(1, 1, 0.5f) * T(0, 1, 0), Green, new Vector2(-12, 0), new Vector2(-7, 0), new Vector2(-10, 5), new Vector2(-12.5f, 5));
        // vectoring nozzles
        foreach (float x in new[] { -1.5f, 3f })
            foreach (float z in new[] { -2.2f, 2.2f })
                m.Box(Matrix.CreateScale(1.6f, 2.2f, 1) * Matrix.CreateRotationZ(-0.5f) * T(x, -1.8f, z), new Color(50, 52, 56));
        return m;
    }

    /// <summary>The enemy fighter, nose at +x (turned to face the player when drawn).</summary>
    public static readonly MeshBuilder Mig = BuildMig();

    private static MeshBuilder BuildMig()
    {
        var m = new MeshBuilder();
        var skin = new Color(140, 140, 120);
        m.Cylinder(S(18, 3, 3) * T(0, 0, 0), skin);
        m.Cylinder(S(6, 3, 3) * T(12, 0, 0), skin, 8, 0.1f);
        m.Box(new Vector3(7, 1.5f, 0), new Vector3(4, 1.6f, 1.4f), new Color(50, 70, 60));
        var wing = new[] { new Vector2(-3, 0), new Vector2(4, 0), new Vector2(-4, 8), new Vector2(-6, 8) };
        m.Plate(Matrix.CreateRotationX(-MathHelper.PiOver2) * T(0, 0, 0.5f), skin, wing);
        m.Plate(Matrix.CreateRotationX(MathHelper.PiOver2) * T(0, 0, -0.5f), skin,
            new Vector2(-3, 0), new Vector2(-6, 8), new Vector2(-4, 8), new Vector2(4, 0));
        m.Plate(S(1, 1, 0.5f) * T(0, 1.2f, 0), skin, new Vector2(-9, 0), new Vector2(-4, 0), new Vector2(-8, 6), new Vector2(-10, 6));
        m.Box(new Vector3(-9.5f, 0, 0), new Vector3(1, 2.2f, 2.2f), new Color(255, 150, 60));   // afterburner
        return m;
    }

    /// <summary>A missile or rocket, 8 long, nose at +x.</summary>
    public static readonly MeshBuilder Missile = BuildMissile();

    private static MeshBuilder BuildMissile()
    {
        var m = new MeshBuilder();
        m.Cylinder(S(6, 1, 1), Light);
        m.Cylinder(S(2, 1, 1) * T(4, 0, 0), new Color(200, 60, 50), 6, 0.05f);
        m.Plate(S(1, 1, 0.3f), Light, new Vector2(-3, 0), new Vector2(-1.5f, 0), new Vector2(-3, 1.6f));
        m.Plate(Matrix.CreateRotationX(MathHelper.PiOver2) * S(1, 1, 0.3f), Light, new Vector2(-3, 0), new Vector2(-1.5f, 0), new Vector2(-3, 1.6f));
        return m;
    }

    /// <summary>A bomb, 6 long, nose at +x.</summary>
    public static readonly MeshBuilder Bomb = BuildBomb();

    private static MeshBuilder BuildBomb()
    {
        var m = new MeshBuilder();
        var c = new Color(70, 80, 60);
        m.Cylinder(S(4, 1.8f, 1.8f), c);
        m.Cylinder(S(1.5f, 1.8f, 1.8f) * T(2.7f, 0, 0), c, 8, 0.2f);
        m.Box(new Vector3(-2.6f, 0, 0), new Vector3(1.2f, 2.6f, 0.3f), c);
        m.Box(new Vector3(-2.6f, 0, 0), new Vector3(1.2f, 0.3f, 2.6f), c);
        return m;
    }

    /// <summary>Anti-aircraft gun on a sandbagged pad (kind 0-2 differ in barrels).</summary>
    public static MeshBuilder Gun(int kind)
    {
        var m = new MeshBuilder();
        var sand = new Color(150, 130, 90);
        m.Cylinder(Matrix.CreateScale(2.5f, 9, 9) * Matrix.CreateRotationZ(MathHelper.PiOver2) * T(0, 1.2f, 0), sand, 10);
        m.Box(new Vector3(0, 3.6f, 0), new Vector3(3.5f, 2.4f, 3.5f), new Color(70, 76, 66));
        int barrels = kind == 1 ? 2 : 1;
        for (int b = 0; b < barrels; b++)
        {
            float z = barrels == 1 ? 0 : (b == 0 ? -0.7f : 0.7f);
            m.Cylinder(Matrix.CreateScale(7, 0.7f, 0.7f) * Matrix.CreateRotationZ(kind == 2 ? 1.0f : 0.75f) * T(2, 6, z), new Color(50, 54, 50), 6);
        }
        return m;
    }

    /// <summary>The aircraft carrier: deck length 1 along x (scaled to the run), deck top at y = 0.</summary>
    public static readonly MeshBuilder Carrier = BuildCarrier();

    private static MeshBuilder BuildCarrier()
    {
        var m = new MeshBuilder();
        var hull = new Color(96, 104, 114);
        var deck = new Color(70, 74, 80);
        // hull below the deck, pointed bow at +x
        m.Box(Matrix.CreateScale(0.86f, 10, 22) * T(-0.07f, -5, 0), hull, deck);
        m.Wedge(Matrix.CreateScale(0.14f, 22, 10) * Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(0) * T(0.43f, -5, 0), hull, hull);
        // ski-jump
        m.Plate(Matrix.CreateScale(1, 1, 22) * T(0, 0, 0), deck, new Vector2(0.38f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 2.5f));
        // island on the starboard side (towards the camera: -z)
        m.Box(new Vector3(0.1f, 5, -8), new Vector3(0.16f, 10, 5), new Color(110, 116, 124));
        m.Box(new Vector3(0.12f, 12, -8), new Vector3(0.05f, 4, 3), new Color(80, 84, 90));
        m.Box(new Vector3(0.14f, 16, -8), new Vector3(0.008f, 8, 0.6f), new Color(60, 60, 64));
        // deck markings
        m.Box(new Vector3(0, 0.15f, 0), new Vector3(0.8f, 0.2f, 0.6f), new Color(230, 230, 220));
        return m;
    }

    /// <summary>An enemy frigate, about 26 long, waterline at y = 0, bow at -x.</summary>
    public static readonly MeshBuilder Frigate = BuildFrigate();

    private static MeshBuilder BuildFrigate()
    {
        var m = new MeshBuilder();
        var grey = new Color(118, 124, 120);
        m.Box(new Vector3(1, 2, 0), new Vector3(22, 4, 7), grey, new Color(90, 94, 90));
        m.Wedge(Matrix.CreateScale(4, 7, 4) * Matrix.CreateRotationX(-MathHelper.PiOver2) * Matrix.CreateRotationY(MathHelper.Pi) * T(-12, 2, 0), grey, grey);
        m.Box(new Vector3(2, 6, 0), new Vector3(8, 4, 5), new Color(130, 136, 132));
        m.Box(new Vector3(3, 10, 0), new Vector3(0.6f, 6, 0.6f), new Color(70, 72, 70));
        m.Box(new Vector3(-6, 5, 0), new Vector3(3, 2, 3), grey);
        m.Cylinder(Matrix.CreateScale(4, 0.6f, 0.6f) * T(-8.5f, 5.3f, 0), new Color(60, 62, 60), 6);
        return m;
    }
}
