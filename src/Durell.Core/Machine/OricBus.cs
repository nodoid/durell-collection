using System;
using System.Collections.Generic;

namespace Durell.Machine;

/// <summary>
/// The Oric's I/O page as seen by a program: the 6522 VIA (timers, keyboard
/// row select and sense, the AY bus control lines) and the AY-3-8912 sound
/// chip's registers.  The AY register writes are kept with their cycle so the
/// audio can be rendered exactly as the program timed them.
/// </summary>
internal sealed class OricBus : IOricBus
{
    // VIA
    private int _orb, _ora, _ddrb, _ddra, _acr, _pcr, _ifr, _ier, _sr;
    private int _t1Latch = 0xFFFF;
    private long _t1Next = long.MaxValue;       // cycle of the next T1 underflow
    private bool _t1Armed;
    private int _t2Latch = 0xFF;
    private long _t2Load;
    private int _t2Value = 0xFFFF;
    private bool _t2Armed;

    // AY
    public int[] Ay = new int[16];
    private int _ayLatch;
    public List<AyWrite> AyLog = new();

    /// <summary>Keys held down, by matrix row (bit = column).</summary>
    public byte[] Keys = new byte[8];

    public readonly struct AyWrite
    {
        public readonly long Cycle;
        public readonly byte Reg, Value;

        public AyWrite(long cycle, int reg, int value)
        {
            Cycle = cycle;
            Reg = (byte)reg;
            Value = (byte)value;
        }
    }

    public bool IrqLine => (_ifr & _ier & 0x7F) != 0;

    /// <summary>A copy of the I/O state (for save states).</summary>
    public OricBus Clone()
    {
        var c = (OricBus)MemberwiseClone();
        c.CopyArraysFrom(this);
        return c;
    }

    public void CopyFrom(OricBus o)
    {
        _orb = o._orb; _ora = o._ora; _ddrb = o._ddrb; _ddra = o._ddra; _acr = o._acr; _pcr = o._pcr;
        _ifr = o._ifr; _ier = o._ier; _sr = o._sr; _t1Latch = o._t1Latch; _t1Next = o._t1Next; _t1Armed = o._t1Armed;
        _t2Latch = o._t2Latch; _t2Load = o._t2Load; _t2Value = o._t2Value; _t2Armed = o._t2Armed; _ayLatch = o._ayLatch;
        CopyArraysFrom(o);
    }

    private void CopyArraysFrom(OricBus o)
    {
        Ay = (int[])o.Ay.Clone();
        Keys = (byte[])o.Keys.Clone();
        AyLog = new List<AyWrite>(o.AyLog);
    }

    public void ClearKeys() => Array.Clear(Keys);

    public void SetKey(OricKey k, bool down)
    {
        int row = (int)k >> 3, col = (int)k & 7;
        if (down) Keys[row] |= (byte)(1 << col);
        else Keys[row] &= (byte)~(1 << col);
    }

    public void LoadState(int ier, int acr, int pcr, int t1Latch, int orb, int ora, int ddra, int ddrb, int[] ay, long cycle)
    {
        _ier = ier & 0x7F;
        _acr = acr;
        _pcr = pcr;
        _t1Latch = t1Latch;
        _orb = orb;
        _ora = ora;
        _ddra = ddra;
        _ddrb = ddrb;
        Array.Copy(ay, Ay, 16);
        _t1Armed = true;
        _t1Next = cycle + _t1Latch + 2;
    }

    private int KeySense()
    {
        int row = _orb & 7;
        int cols = ~Ay[14] & 0xFF;
        return (Keys[row] & cols) != 0 ? 0x08 : 0;
    }

    private void UpdateT1(long cycle)
    {
        if (!_t1Armed || cycle < _t1Next) return;
        _ifr |= 0x40;
        if ((_acr & 0x40) != 0)
        {
            long period = _t1Latch + 2;
            long n = (cycle - _t1Next) / period + 1;
            _t1Next += n * period;
        }
        else
        {
            _t1Armed = false;
            _t1Next = long.MaxValue;
        }
    }

    private int T2Count(long cycle) => (int)((_t2Value - (cycle - _t2Load)) & 0xFFFF);

    private void UpdateT2(long cycle)
    {
        if (_t2Armed && cycle - _t2Load > _t2Value)
        {
            _ifr |= 0x20;
            _t2Armed = false;
        }
    }

    public long Service(long cycle)
    {
        UpdateT1(cycle);
        UpdateT2(cycle);
        long next = _t1Armed ? _t1Next : long.MaxValue;
        if (_t2Armed) next = Math.Min(next, _t2Load + _t2Value + 1);
        return next;
    }

    public int Read(int a, long cycle)
    {
        // (the VIA answers on the whole I/O page of a stock Oric: $0300-$03FF, mirrored every 16 bytes)
        UpdateT1(cycle);
        UpdateT2(cycle);
        switch (a & 15)
        {
            case 0:
                return (_orb & _ddrb) | ((0xF7 | KeySense()) & ~_ddrb & 0xFF);
            case 1:
            case 15:
                if ((_pcr & 0xE0) != 0xE0 && (_pcr & 0x0E) == 0x0E) return Ay[_ayLatch];  // AY read
                return _ora;
            case 2: return _ddrb;
            case 3: return _ddra;
            case 4:
                _ifr &= ~0x40;
                return (int)(T1Count(cycle) & 0xFF);
            case 5: return (int)(T1Count(cycle) >> 8) & 0xFF;
            case 6: return _t1Latch & 0xFF;
            case 7: return _t1Latch >> 8;
            case 8:
                _ifr &= ~0x20;
                return T2Count(cycle) & 0xFF;
            case 9: return T2Count(cycle) >> 8;
            case 10: return _sr;
            case 11: return _acr;
            case 12: return _pcr;
            case 13: return (_ifr & 0x7F) | (IrqLine ? 0x80 : 0);
            case 14: return _ier | 0x80;
        }
        return 0xFF;
    }

    private long T1Count(long cycle)
    {
        if (!_t1Armed) return 0xFFFF;
        long left = _t1Next - cycle - 2;
        return left < 0 ? 0xFFFF : left & 0xFFFF;
    }

    public void Write(int a, int v, long cycle)
    {
        UpdateT1(cycle);
        UpdateT2(cycle);
        switch (a & 15)
        {
            case 0: _orb = v; break;
            case 1:
            case 15: _ora = v; break;
            case 2: _ddrb = v; break;
            case 3: _ddra = v; break;
            case 4:
            case 6: _t1Latch = (_t1Latch & 0xFF00) | v; break;
            case 5:
                _t1Latch = (_t1Latch & 0x00FF) | v << 8;
                _ifr &= ~0x40;
                _t1Armed = true;
                _t1Next = cycle + _t1Latch + 2;
                break;
            case 7:
                _t1Latch = (_t1Latch & 0x00FF) | v << 8;
                _ifr &= ~0x40;
                break;
            case 8: _t2Latch = v; break;
            case 9:
                _t2Value = _t2Latch | v << 8;
                _t2Load = cycle;
                _t2Armed = true;
                _ifr &= ~0x20;
                break;
            case 10: _sr = v; break;
            case 11: _acr = v; break;
            case 12:
                _pcr = v;
                bool bdir = (v & 0xE0) == 0xE0, bc1 = (v & 0x0E) == 0x0E;
                if (bdir && bc1) _ayLatch = _ora & 15;
                else if (bdir) WriteAy(_ayLatch, _ora, cycle);
                break;
            case 13: _ifr &= ~(v & 0x7F); break;
            case 14:
                if ((v & 0x80) != 0) _ier |= v & 0x7F;
                else _ier &= ~v & 0x7F;
                break;
        }
    }

    private void WriteAy(int reg, int v, long cycle)
    {
        Ay[reg] = v;
        if (reg < 14) AyLog.Add(new AyWrite(cycle, reg, v));
    }
}

/// <summary>Oric keyboard matrix positions: row * 8 + column.</summary>
public enum OricKey
{
    D7 = 0, N = 1, D5 = 2, V = 3, RCtrl = 4, D1 = 5, X = 6, D3 = 7,
    J = 8, T = 9, R = 10, F = 11, Esc = 13, Q = 14, D = 15,
    M = 16, D6 = 17, B = 18, D4 = 19, Ctrl = 20, Z = 21, D2 = 22, C = 23,
    K = 24, D9 = 25, Semicolon = 26, Minus = 27, Backslash = 30, Quote = 31,
    Space = 32, Comma = 33, Period = 34, Up = 35, LShift = 36, Left = 37, Down = 38, Right = 39,
    U = 40, I = 41, O = 42, P = 43, Funct = 44, Del = 45, RBracket = 46, LBracket = 47,
    Y = 48, H = 49, G = 50, E = 51, A = 53, S = 54, W = 55,
    D8 = 56, L = 57, D0 = 58, Slash = 59, RShift = 60, Return = 61, Equals = 63,
}
