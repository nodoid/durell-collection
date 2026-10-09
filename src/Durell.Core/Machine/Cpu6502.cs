using System;
using System.Collections.Generic;

namespace Durell.Machine;

/// <summary>
/// Register file, flags and dispatcher shared by the translated programs.
/// The program code itself is C# generated from the original 6502 machine
/// code (tools/recomp); each generated page method runs straight-line code
/// and returns the address it continues at whenever control leaves the page,
/// an indirect jump or return is taken, or an event is due.
/// </summary>
internal abstract class Cpu6502
{
    public readonly byte[] M = new byte[0x10000];
    public int A, X, Y, S = 0xFF;
    public int C, V, I = 1, D;
    /// <summary>Z flag is ZR == 0; N flag is bit 7 of NR.</summary>
    public int ZR = 1, NR;
    public int PC;
    /// <summary>Cycle counter, and the cycle at which the dispatcher must look at events.</summary>
    public long Cy, Ev;

    public IOricBus Bus = null!;
    /// <summary>Writes at or above this address are ignored (the ROM).</summary>
    public int WriteLimit = 0xC000;

    private readonly bool[] _code = new bool[0x10000];
    public readonly HashSet<int> Misses = new();
    public readonly HashSet<int> CodeWrites = new();
    /// <summary>Values written into code bytes (address &lt;&lt; 8 | value), when tracing.</summary>
    public readonly HashSet<int> CodeWriteValues = new();
    public bool TraceCodeWrites;
    /// <summary>The last 64 addresses the dispatcher continued at (diagnostics).</summary>
    public readonly int[] History = new int[64];
    private int _hist;
    public int HistoryPos => _hist;

    protected Cpu6502()
    {
        MarkCode(_code);
    }

    protected abstract int Page(int pc);
    protected abstract void MarkCode(bool[] map);

    /// <summary>Thrown when the program reaches an address that was not translated.</summary>
    public sealed class UntranslatedCodeException : Exception
    {
        public int Address { get; }
        public UntranslatedCodeException(int a) : base($"no translated code at ${a:X4}") => Address = a;
    }

    protected int Miss(int pc)
    {
        Misses.Add(pc);
        throw new UntranslatedCodeException(pc);
    }

    protected int Bad(int pc) => Miss(pc);

    /// <summary>Told when the program reaches one of its probe addresses (games.json "probes").</summary>
    public Action<int>? ProbeHandler;

    protected void Probe(int address) => ProbeHandler?.Invoke(address);

    // --- memory ----------------------------------------------------------
    protected int Rd(int a, int off)
    {
        if ((a & 0xFF00) == 0x0300) return Bus.Read(a, Cy + off);
        return M[a];
    }

    protected int IoRd(int a, int off) => Bus.Read(a, Cy + off);

    protected void Wr(int a, int v, int off)
    {
        if ((a & 0xFF00) == 0x0300)
        {
            Bus.Write(a, v & 0xFF, Cy + off);
            return;
        }
        if (a >= WriteLimit) return;
        M[a] = (byte)v;
        if (_code[a] && TraceCodeWrites)
        {
            CodeWrites.Add(a);
            CodeWriteValues.Add(a << 8 | (v & 0xFF));
        }
    }

    protected void IoWr(int a, int v, int off) => Bus.Write(a, v & 0xFF, Cy + off);

    protected void WrCode(int a, int v)
    {
        M[a] = (byte)v;
        if (TraceCodeWrites)
        {
            CodeWrites.Add(a);
            CodeWriteValues.Add(a << 8 | (v & 0xFF));
        }
    }

    // --- stack and flags -----------------------------------------------------
    protected void Push(int v)
    {
        M[0x100 | S] = (byte)v;
        S = (S - 1) & 0xFF;
    }

    protected int Pop()
    {
        S = (S + 1) & 0xFF;
        return M[0x100 | S];
    }

    public int GetP() =>
        (NR & 0x80) | (V << 6) | 0x20 | (D << 3) | (I << 2) | (ZR == 0 ? 2 : 0) | C;

    public void SetP(int p)
    {
        NR = p & 0x80;
        V = (p >> 6) & 1;
        D = (p >> 3) & 1;
        I = (p >> 2) & 1;
        ZR = (p & 2) != 0 ? 0 : 1;
        C = p & 1;
    }

    protected void Adc(int v)
    {
        if (D == 0)
        {
            int t = A + v + C;
            V = (~(A ^ v) & (A ^ t) & 0x80) != 0 ? 1 : 0;
            C = t >> 8;
            A = t & 0xFF;
            ZR = NR = A;
            return;
        }
        // NMOS decimal mode
        int lo = (A & 0x0F) + (v & 0x0F) + C;
        int bin = (A + v + C) & 0xFF;
        if (lo > 9) lo += 6;
        int hi = (A >> 4) + (v >> 4) + (lo > 0x0F ? 1 : 0);
        ZR = bin;
        NR = (hi << 4) & 0xFF;
        V = (~(A ^ v) & (A ^ (hi << 4)) & 0x80) != 0 ? 1 : 0;
        if (hi > 9) hi += 6;
        C = hi > 0x0F ? 1 : 0;
        A = ((hi << 4) | (lo & 0x0F)) & 0xFF;
    }

    protected void Sbc(int v)
    {
        int t = A - v - (1 - C);
        V = ((A ^ v) & (A ^ t) & 0x80) != 0 ? 1 : 0;
        if (D == 0)
        {
            C = t >= 0 ? 1 : 0;
            A = t & 0xFF;
            ZR = NR = A;
            return;
        }
        int lo = (A & 0x0F) - (v & 0x0F) - (1 - C);
        int hi = (A >> 4) - (v >> 4);
        if (lo < 0)
        {
            lo -= 6;
            hi--;
        }
        if (hi < 0) hi -= 6;
        C = t >= 0 ? 1 : 0;
        ZR = NR = t & 0xFF;
        A = ((hi << 4) | (lo & 0x0F)) & 0xFF;
    }

    protected int Brk(int ret)
    {
        Push(ret >> 8);
        Push(ret & 0xFF);
        Push(GetP() | 0x30);
        I = 1;
        return M[0xFFFE] | M[0xFFFF] << 8;
    }

    // --- dispatcher ------------------------------------------------------------
    /// <summary>Runs the program until the cycle counter reaches <paramref name="until"/>.</summary>
    public void Run(long until)
    {
        int pc = PC;
        try
        {
            while (true)
            {
                if (Cy >= Ev)
                {
                    if (Cy >= until) break;
                    long next = Bus.Service(Cy);
                    if (I == 0 && Bus.IrqLine)
                    {
                        Push(pc >> 8);
                        Push(pc & 0xFF);
                        Push(GetP() & 0xEF);
                        I = 1;
                        Cy += 7;
                        pc = M[0xFFFE] | M[0xFFFF] << 8;
                    }
                    Ev = Math.Min(next, until);
                }
                History[_hist++ & 63] = pc;
                if (pc == _stopPc) break;
                pc = Page(pc);
            }
        }
        finally
        {
            PC = pc;
        }
    }

    private int _stopPc = -1;

    /// <summary>
    /// Calls the translated subroutine at <paramref name="address"/> from native code and returns
    /// when it does (interrupts masked).
    /// </summary>
    public void Call(int address)
    {
        const int trap = 0x0001;            // never code: the return lands here
        Push((trap - 1) >> 8);
        Push((trap - 1) & 0xFF);
        int saveI = I;
        I = 1;
        PC = address;
        _stopPc = trap;
        try
        {
            Run(Cy + 2_000_000);
        }
        finally
        {
            _stopPc = -1;
            I = saveI;
        }
    }

    public void Reset(int pc, int a, int x, int y, int s, int p)
    {
        PC = pc;
        A = a;
        X = x;
        Y = y;
        S = s;
        SetP(p);
        Ev = 0;
    }
}

/// <summary>The machine around the CPU: I/O page, timers and the interrupt line.</summary>
internal interface IOricBus
{
    int Read(int address, long cycle);
    void Write(int address, int value, long cycle);
    /// <summary>Brings timers up to <paramref name="cycle"/>; returns the cycle of the next event.</summary>
    long Service(long cycle);
    bool IrqLine { get; }
}
