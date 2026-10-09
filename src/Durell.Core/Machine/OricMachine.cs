using System;
using System.IO;
using System.IO.Compression;

namespace Durell.Machine;

/// <summary>
/// One translated Oric program with the hardware it talks to: memory, the
/// VIA/AY I/O page, the video decoder and the sound chip.  Runs one 50 Hz
/// frame at a time.
/// </summary>
internal sealed class OricMachine
{
    public const int CyclesPerFrame = 19968;        // 312 lines x 64 us
    public const int SamplesPerFrame = AyChip.Rate / 50;

    public readonly Cpu6502 Cpu;
    public readonly OricBus Bus = new();
    public readonly OricVideo Video = new();
    public readonly AyChip Ay = new();
    private readonly byte[] _snapshot;
    private long _frameStart;
    private int _logPos;
    public long Frame { get; private set; }

    public byte[] M => Cpu.M;

    public OricMachine(Cpu6502 cpu, byte[] snapshot)
    {
        Cpu = cpu;
        Cpu.Bus = Bus;
        _snapshot = snapshot;
        Reset();
    }

    public static byte[] LoadSnapshot(Stream s)
    {
        using var z = new ZLibStream(s, CompressionMode.Decompress);
        using var ms = new MemoryStream();
        z.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Puts the machine back to the moment the program had loaded.</summary>
    public void Reset()
    {
        var b = _snapshot;
        if (b[0] != 'D' || b[1] != 'S' || b[2] != 'N' || b[3] != '1') throw new InvalidDataException("bad snapshot");
        int p = 4;
        int pc = b[p] | b[p + 1] << 8;
        p += 2;
        int a = b[p++], x = b[p++], y = b[p++], sp = b[p++], flags = b[p++];
        int ier = b[p++], acr = b[p++], pcr = b[p++];
        int t1 = b[p] | b[p + 1] << 8;
        p += 2;
        int orb = b[p++], ora = b[p++], ddra = b[p++], ddrb = b[p++];
        var ay = new int[16];
        for (int i = 0; i < 16; i++) ay[i] = b[p++];
        Buffer.BlockCopy(b, p, Cpu.M, 0, 0x10000);
        Cpu.Cy = 0;
        Cpu.Reset(pc, a, x, y, sp, flags);
        Bus.LoadState(ier, acr, pcr, t1, orb, ora, ddra, ddrb, ay, 0);
        Bus.AyLog.Clear();
        Bus.ClearKeys();
        Ay.Reset();
        for (int i = 0; i < 14; i++) Ay.Write(i, ay[i]);
        Video.Reset(Cpu.M);
        _frameStart = 0;
        _logPos = 0;
        Frame = 0;
    }

    /// <summary>A complete copy of the machine (memory, registers, I/O), for save states.</summary>
    public sealed class State
    {
        internal byte[] Mem = new byte[0x10000];
        internal int A, X, Y, S, P, PC;
        internal long Cy, Frame;
        internal OricBus Bus = null!;
        internal byte[] Index = Array.Empty<byte>();
    }

    public State Save()
    {
        var st = new State
        {
            A = Cpu.A, X = Cpu.X, Y = Cpu.Y, S = Cpu.S, P = Cpu.GetP(), PC = Cpu.PC, Cy = Cpu.Cy, Frame = Frame,
            Bus = Bus.Clone(), Index = (byte[])Video.Index.Clone(),
        };
        Buffer.BlockCopy(Cpu.M, 0, st.Mem, 0, 0x10000);
        return st;
    }

    public void Load(State st)
    {
        Buffer.BlockCopy(st.Mem, 0, Cpu.M, 0, 0x10000);
        Cpu.Reset(st.PC, st.A, st.X, st.Y, st.S, st.P);
        Cpu.Cy = st.Cy;
        Frame = st.Frame;
        Bus.CopyFrom(st.Bus);
        Buffer.BlockCopy(st.Index, 0, Video.Index, 0, st.Index.Length);
        _logPos = 0;
    }

    /// <summary>Runs the program for one frame and decodes the picture.</summary>
    public void RunFrame()
    {
        _frameStart = Frame * CyclesPerFrame;
        Cpu.Run(_frameStart + CyclesPerFrame);
        Video.Render(Cpu.M);
        Frame++;
    }

    /// <summary>Renders the sound of the frame just run (interleaved stereo, <see cref="SamplesPerFrame"/> pairs).</summary>
    public void RenderAudio(Span<float> stereo, bool enhanced)
    {
        Ay.Render(stereo, SamplesPerFrame, _frameStart, CyclesPerFrame, Bus.AyLog, ref _logPos, enhanced);
        // drop the writes already applied
        if (_logPos > 0)
        {
            Bus.AyLog.RemoveRange(0, _logPos);
            _logPos = 0;
        }
    }

    /// <summary>Skips the audio of the frame just run (keeps the chip state in step).</summary>
    public void SkipAudio()
    {
        foreach (var w in Bus.AyLog)
            if (w.Cycle < _frameStart + CyclesPerFrame) Ay.Write(w.Reg, w.Value);
        Bus.AyLog.RemoveAll(w => w.Cycle < _frameStart + CyclesPerFrame);
    }
}
