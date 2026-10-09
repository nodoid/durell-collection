#!/usr/bin/env python3
"""Dev-time only: boot the Atmos ROM on py65, CLOAD a .tap (tape routines
intercepted at byte level) and snapshot the machine when the program has
loaded and control is back in RAM.  Output: <out>.ram (64K incl. ROM image
at C000) + <out>.json (cpu state, VIA state)."""
import sys, os, json
from py65.devices.mpu6502 import MPU

ROMS = {'11': (os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../originals/roms/basic11b.rom'), 0xE735, (0xE6C9,), 0xE8BC),
        '10': (os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../originals/roms/basic10.rom'), 0xE696, (0xE630,), None)}
MATRIX = [['7','N','5','V','RCTL','1','X','3'],['J','T','R','F',None,'ESC','Q','D'],['M','6','B','4','CTRL','Z','2','C'],['K','9',';','-',None,None,'\\',"'"],['SPACE',',','.','UP','LSHIFT','LEFT','DOWN','RIGHT'],['U','I','O','P','FUNCT','DEL',']','['],['Y','H','G','E',None,'A','S','W'],['8','L','0','/','RSHIFT','RETURN',None,'=']]
KEYMAP = {k:(r,c) for r,row in enumerate(MATRIX) for c,k in enumerate(row) if k}
T1 = 10000

class Mem:
    def __init__(s, m): s.m = m
    def __getitem__(s, a):
        if isinstance(a, slice): return [s[i] for i in range(*a.indices(65536))]
        if 0x0300 <= a <= 0x03FF: return s.m.io_read(a)
        return s.m.ram[a]
    def __setitem__(s, a, v):
        if 0x0300 <= a <= 0x03FF: s.m.io_write(a, v); return
        if a >= 0xC000: return
        s.m.ram[a] = v
    def __len__(s): return 65536

class Machine:
    def __init__(s, rom):
        s.ram = bytearray(65536)
        s.ram[0xC000:] = open(rom, 'rb').read()
        s.cycles = 0; s.t1_latch = T1; s.t1_next = None; s.ifr = 0; s.ier = 0
        s.orb = 0; s.ora = 0; s.ddra = 0; s.ddrb = 0; s.pcr = 0; s.acr = 0
        s.ay = [0]*16; s.ay_latch = 0; s.pressed = set()
        s.t2 = 0xFFFF; s.t2_start = 0; s.t2l = 0xFF
        s.mpu = MPU(memory=Mem(s)); s.mpu.memory = Mem(s)
    def io_read(s, a):
        r = a & 15
        if a >= 0x0310: return 0xFF
        if r == 0:
            row = s.orb & 7; cols = (~s.ay[14]) & 0xFF; k = 0
            for (rr, c) in s.pressed:
                if rr == row and cols & (1 << c): k = 8
            return (s.orb & 0xF7) | k
        if r == 4: s.ifr &= ~0x40; return 0
        if r == 5: return 0
        if r == 8: s.ifr &= ~0x20; return (s.t2 - (s.cycles - s.t2_start)) & 0xFF
        if r == 9: return ((s.t2 - (s.cycles - s.t2_start)) >> 8) & 0xFF
        if r == 13:
            f = s.ifr & 0x7F
            return f | (0x80 if f & s.ier else 0)
        if r == 14: return s.ier | 0x80
        if r in (1, 15): return s.ora
        return 0
    def io_write(s, a, v):
        r = a & 15
        if a >= 0x0310: return
        if r == 0: s.orb = v
        elif r in (1, 15): s.ora = v
        elif r == 2: s.ddrb = v
        elif r == 3: s.ddra = v
        elif r in (4, 6): s.t1_latch = (s.t1_latch & 0xFF00) | v
        elif r == 5:
            s.t1_latch = (s.t1_latch & 0xFF) | v << 8; s.ifr &= ~0x40
            s.t1_next = s.cycles + s.t1_latch + 2
        elif r == 7: s.t1_latch = (s.t1_latch & 0xFF) | v << 8; s.ifr &= ~0x40
        elif r == 8: s.t2l = v
        elif r == 9: s.t2 = s.t2l | v << 8; s.t2_start = s.cycles; s.ifr &= ~0x20
        elif r == 11: s.acr = v
        elif r == 12:
            s.pcr = v
            bdir = (v & 0xE0) == 0xE0; bc1 = (v & 0x0E) == 0x0E
            if bdir and bc1: s.ay_latch = s.ora & 15
            elif bdir: s.ay[s.ay_latch] = s.ora
        elif r == 13: s.ifr &= ~(v & 0x7F)
        elif r == 14:
            if v & 0x80: s.ier |= v & 0x7F
            else: s.ier &= ~v & 0x7F

def run(tap, out, romv='11', stopmax=200_000_000):
    rom, SYNC, GETB, BLKDONE = ROMS[romv]
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    m = Machine(rom); mpu = m.mpu
    tape = open(tap, 'rb').read(); tp = 0
    mpu.pc = m.ram[0xFFFC] | m.ram[0xFFFD] << 8
    seq = []
    for ch in ['C','L','O','A','D',('LSHIFT',"'"),('LSHIFT',"'"),'RETURN']:
        seq += [ch if isinstance(ch, tuple) else (ch,), ()]
    next_key = 6_000_000
    blk_ends = []
    q = 0
    for b in __import__('tap').parse(tape): pass
    import re
    # byte offsets in the tape where each block's data ends
    i = 0
    while i < len(tape):
        while i < len(tape) and tape[i] == 0x16: i += 1
        if i >= len(tape): break
        if tape[i] != 0x24: i += 1; continue
        h = tape[i+1:i+10]; i += 10
        while tape[i] != 0: i += 1
        i += 1
        n = ((h[4]<<8|h[5]) - (h[6]<<8|h[7])) + 1; i += n; blk_ends.append(i)
    nblocks = sum(1 for _ in __import__('tap').parse(tape))
    blocks_done = 0; ram_pcs_after = 0; steps = 0; load_end = None
    rom_calls = {}
    while steps < stopmax:
        pc = mpu.pc
        if pc == SYNC:          # SyncTape: skip to after a run of >=3 $16
            while tp < len(tape) and not (tape[tp] == 0x16 and tape[tp+1:tp+3] == b'\x16\x16'): tp += 1
            while tp < len(tape) and tape[tp] == 0x16: tp += 1
            mpu.pc = (mpu.memory[0x100 + ((mpu.sp + 1) & 0xFF)] | mpu.memory[0x100 + ((mpu.sp + 2) & 0xFF)] << 8) + 1
            mpu.sp = (mpu.sp + 2) & 0xFF; continue
        if pc in GETB:  # GetTapeByte
            if tp >= len(tape): print('tape exhausted at', hex(pc)); break
            b = tape[tp]; tp += 1
            m.ram[0x2F] = b; mpu.a = b
            mpu.p = (mpu.p & ~0x83) | (0x80 if b & 0x80 else 0) | (0x02 if b == 0 else 0)
            mpu.pc = (mpu.memory[0x100 + ((mpu.sp + 1) & 0xFF)] | mpu.memory[0x100 + ((mpu.sp + 2) & 0xFF)] << 8) + 1
            mpu.sp = (mpu.sp + 2) & 0xFF
            if tp >= blk_ends[min(blocks_done, len(blk_ends)-1)]: blocks_done += 1
            continue
        if m.cycles >= next_key and seq:
            k = seq.pop(0); m.pressed = set(KEYMAP[x] for x in k); next_key = m.cycles + 40000
        if blocks_done >= nblocks and (pc < 0xC000 or AFTER) and not (0x0100 <= pc < 0x0300):
            ram_pcs_after += 1
            if ram_pcs_after == 1:
                print(f'snapshot at PC={pc:04X} after {steps} steps, cycles={m.cycles}')
                if not AFTER:
                    break
                load_end = m.cycles
        if AFTER and load_end is not None:
            t = m.cycles - load_end
            if t > AFTER:
                break
            if WATCH:
                a, n = WATCH
                cur = bytes(m.ram[a:a + n])
                if cur != getattr(run, 'last', None):
                    run.last = cur
                    print(f'{t / 1e6:8.3f}s', cur.decode('latin1'))
            while SHOTS and t >= SHOTS[0][0]:
                import oricpng
                oricpng.render(m.ram, SHOTS.pop(0)[1])
            held = set()
            for (a, b, ks) in KEYS:
                if a <= t <= b:
                    held |= set(KEYMAP[k] for k in ks)
            if not seq:
                m.pressed = held
        cyc0 = mpu.processorCycles
        mpu.step()
        dc = mpu.processorCycles - cyc0; m.cycles += dc; steps += 1
        if m.t1_next is not None and m.cycles >= m.t1_next:
            m.ifr |= 0x40
            m.t1_next = m.t1_next + m.t1_latch + 2 if (m.acr & 0x40) else None
        if (m.ifr & m.ier & 0x7F) and not (mpu.p & 0x04):
            mpu.stPush(mpu.pc >> 8); mpu.stPush(mpu.pc & 0xFF); mpu.stPush((mpu.p & ~0x10) | 0x20)
            mpu.p |= 0x04; mpu.pc = m.ram[0xFFFE] | m.ram[0xFFFF] << 8
    st = dict(pc=mpu.pc, a=mpu.a, x=mpu.x, y=mpu.y, sp=mpu.sp, p=mpu.p, cycles=m.cycles,
              via=dict(ier=m.ier, acr=m.acr, pcr=m.pcr, t1_latch=m.t1_latch, orb=m.orb, ora=m.ora, ddra=m.ddra, ddrb=m.ddrb),
              ay=m.ay, blocks=blocks_done, steps=steps)
    open(out + '.ram', 'wb').write(bytes(m.ram))
    json.dump(st, open(out + '.json', 'w'), indent=1)
    print(json.dumps(st))

AFTER = 0
WATCH = None
SHOTS = []
KEYS = []

if __name__ == '__main__':
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument('tap'); ap.add_argument('out'); ap.add_argument('rom', nargs='?', default='11')
    ap.add_argument('--after', type=float, default=0, help='seconds to keep running after the load')
    ap.add_argument('--shot', action='append', default=[], help='SECONDS:PATH')
    ap.add_argument('--key', action='append', default=[], help='S1-S2:K+K (seconds)')
    ap.add_argument('--watch', help='ADDR:LEN (hex addr) printed when it changes')
    a = ap.parse_args()
    AFTER = int(a.after * 1_000_000)
    if a.watch:
        w1, w2 = a.watch.split(':')
        WATCH = (int(w1, 16), int(w2))
    SHOTS = sorted((int(float(x.split(':')[0]) * 1_000_000), x.split(':', 1)[1]) for x in a.shot)
    for k in a.key:
        r, ks = k.split(':')
        s1, s2 = r.split('-')
        KEYS.append((int(float(s1) * 1e6), int(float(s2) * 1e6), ks.split('+')))
    run(a.tap, a.out, a.rom, stopmax=10**10 if AFTER else 200_000_000)
