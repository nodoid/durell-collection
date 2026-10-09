using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Durell.Graphics.ThreeD;

/// <summary>A lit, coloured vertex.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct VertexPositionNormalColor : IVertexType
{
    public Vector3 Position;
    public Vector3 Normal;
    public Color Color;

    public static readonly VertexDeclaration Declaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));

    VertexDeclaration IVertexType.VertexDeclaration => Declaration;

    public VertexPositionNormalColor(Vector3 p, Vector3 n, Color c)
    {
        Position = p;
        Normal = n;
        Color = c;
    }
}

/// <summary>Builds triangle lists from simple solids (boxes, wedges, cylinders, cones), flat-shaded.</summary>
internal sealed class MeshBuilder
{
    private VertexPositionNormalColor[] _v = new VertexPositionNormalColor[4096];
    public int Count { get; private set; }
    public VertexPositionNormalColor[] Vertices => _v;

    public void Clear() => Count = 0;

    private void Push(Vector3 p, Vector3 n, Color c)
    {
        if (Count == _v.Length) Array.Resize(ref _v, _v.Length * 2);
        _v[Count++] = new VertexPositionNormalColor(p, n, c);
    }

    public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
    {
        var n = Vector3.Cross(b - a, c - a);
        if (n.LengthSquared() < 1e-12f) return;
        n.Normalize();
        Push(a, n, col);
        Push(b, n, col);
        Push(c, n, col);
    }

    /// <summary>A quad a-b-c-d (anticlockwise seen from the front).</summary>
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
    {
        Tri(a, b, c, col);
        Tri(a, c, d, col);
    }

    /// <summary>A quad with its own per-corner colours and a given normal (smooth terrain).</summary>
    public void QuadSmooth(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Color ca, Color cb, Color cc, Color cd)
    {
        Push(a, na, ca); Push(b, nb, cb); Push(c, nc, cc);
        Push(a, na, ca); Push(c, nc, cc); Push(d, nd, cd);
    }

    /// <summary>A box from its 8 corners transformed by <paramref name="m"/> (unit cube -0.5..0.5).</summary>
    public void Box(Matrix m, Color col, Color? top = null)
    {
        Vector3 P(float x, float y, float z) => Vector3.Transform(new Vector3(x, y, z), m);
        var p000 = P(-.5f, -.5f, -.5f); var p100 = P(.5f, -.5f, -.5f); var p010 = P(-.5f, .5f, -.5f); var p110 = P(.5f, .5f, -.5f);
        var p001 = P(-.5f, -.5f, .5f); var p101 = P(.5f, -.5f, .5f); var p011 = P(-.5f, .5f, .5f); var p111 = P(.5f, .5f, .5f);
        var t = top ?? col;
        Quad(p001, p101, p111, p011, col);   // +z
        Quad(p100, p000, p010, p110, col);   // -z
        Quad(p101, p100, p110, p111, col);   // +x
        Quad(p000, p001, p011, p010, col);   // -x
        Quad(p011, p111, p110, p010, t);     // +y
        Quad(p000, p100, p101, p001, col);   // -y
    }

    public void Box(Vector3 centre, Vector3 size, Color col, Color? top = null) =>
        Box(Matrix.CreateScale(size) * Matrix.CreateTranslation(centre), col, top);

    /// <summary>A wedge (roof): a box whose top edge is a ridge along x.</summary>
    public void Wedge(Matrix m, Color col, Color roof)
    {
        Vector3 P(float x, float y, float z) => Vector3.Transform(new Vector3(x, y, z), m);
        var a = P(-.5f, -.5f, -.5f); var b = P(.5f, -.5f, -.5f); var c = P(.5f, -.5f, .5f); var d = P(-.5f, -.5f, .5f);
        var r0 = P(-.5f, .5f, 0); var r1 = P(.5f, .5f, 0);
        Quad(d, c, r1, r0, roof);
        Quad(b, a, r0, r1, roof);
        Tri(c, b, r1, col);
        Tri(a, d, r0, col);
        Quad(a, b, c, d, col);
    }

    /// <summary>A cylinder along x (fuselages, barrels, missiles), optionally tapered to a point.</summary>
    public void Cylinder(Matrix m, Color col, int sides = 8, float endScale = 1, bool cap = true)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * MathF.Tau / sides, a1 = (i + 1) * MathF.Tau / sides;
            Vector3 R(float x, float a, float s) => Vector3.Transform(new Vector3(x, MathF.Cos(a) * 0.5f * s, MathF.Sin(a) * 0.5f * s), m);
            var p0 = R(-.5f, a0, 1); var p1 = R(-.5f, a1, 1); var q0 = R(.5f, a0, endScale); var q1 = R(.5f, a1, endScale);
            if (endScale > 0.001f) Quad(p0, q0, q1, p1, col);
            else Tri(p0, q0, p1, col);
            if (cap)
            {
                var c0 = Vector3.Transform(new Vector3(-.5f, 0, 0), m);
                Tri(c0, p0, p1, col);
                if (endScale > 0.001f)
                {
                    var c1 = Vector3.Transform(new Vector3(.5f, 0, 0), m);
                    Tri(c1, q1, q0, col);
                }
            }
        }
    }

    /// <summary>A flat polygon in the x/y plane (wings, fins), given thickness along z or y.</summary>
    public void Plate(Matrix m, Color col, params Vector2[] outline)
    {
        // a thin prism: front, back and edges
        var front = new Vector3[outline.Length];
        var back = new Vector3[outline.Length];
        for (int i = 0; i < outline.Length; i++)
        {
            front[i] = Vector3.Transform(new Vector3(outline[i].X, outline[i].Y, 0.5f), m);
            back[i] = Vector3.Transform(new Vector3(outline[i].X, outline[i].Y, -0.5f), m);
        }
        for (int i = 1; i < outline.Length - 1; i++)
        {
            Tri(front[0], front[i], front[i + 1], col);
            Tri(back[0], back[i + 1], back[i], col);
        }
        for (int i = 0; i < outline.Length; i++)
        {
            int j = (i + 1) % outline.Length;
            Quad(front[i], back[i], back[j], front[j], col);
        }
    }

    /// <summary>Copies a prebuilt model in, transformed.</summary>
    public void Add(MeshBuilder model, Matrix m)
    {
        var nm = Matrix.Transpose(Matrix.Invert(m));
        for (int i = 0; i < model.Count; i++)
        {
            var v = model._v[i];
            var n = Vector3.TransformNormal(v.Normal, nm);
            if (n.LengthSquared() > 0) n.Normalize();
            Push(Vector3.Transform(v.Position, m), n, v.Color);
        }
    }

    public void Draw(GraphicsDevice device, Effect effect)
    {
        if (Count < 3) return;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            // stay well inside the per-call primitive limits of mobile GPUs
            const int chunk = 3 * 20000;
            for (int start = 0; start < Count; start += chunk)
            {
                int n = Math.Min(chunk, Count - start);
                device.DrawUserPrimitives(PrimitiveType.TriangleList, _v, start, n / 3, VertexPositionNormalColor.Declaration);
            }
        }
    }
}
