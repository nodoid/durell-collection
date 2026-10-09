#!/usr/bin/env python3
"""Static 6502 -> C# translator (dev tool).

Translates the machine code of an Oric program, taken from a memory snapshot
made after the program has loaded, into C# source.  Every 6502 instruction
becomes C# statements working on the program's memory array; there is no
interpreter and no CPU emulation in the generated code.  Control flow is
mapped to gotos inside one method per 256-byte page; transfers to another
page, RTS/RTI and indirect jumps return the next address to a small
dispatcher (Cpu6502.Run), which is also where the timer interrupt is taken.

usage: recomp.py GAME            (settings in games.json)

Code is found by recursive descent from the entry points.  Addresses that the
running program reaches but that were not found statically (indirect jumps,
pushed return addresses) are logged by the dev runner to
originals/cov/GAME.entries and picked up on the next run.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from op6502 import OPS, SIZE, decode  # noqa: E402

ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))

BRANCH = {'BPL': '(NR & 0x80) == 0', 'BMI': '(NR & 0x80) != 0', 'BVC': 'V == 0', 'BVS': 'V != 0',
          'BCC': 'C == 0', 'BCS': 'C != 0', 'BNE': 'ZR != 0', 'BEQ': 'ZR == 0'}
WRITES = {'STA', 'STX', 'STY', 'INC', 'DEC', 'ASL', 'LSR', 'ROL', 'ROR', 'SLO', 'RLA', 'SRE', 'RRA', 'SAX', 'DCP', 'ISC'}
RMW = {'INC', 'DEC', 'ASL', 'LSR', 'ROL', 'ROR'}


def h4(a):
    return f"0x{a:04X}"


class Game:
    def __init__(self, name):
        cfg = json.load(open(os.path.join(HERE, 'games.json')))[name]
        self.name = name
        self.cfg = cfg
        snap = os.path.join(ROOT, cfg['snap'])
        self.mem = bytearray(open(snap + '.ram', 'rb').read())
        self.state = json.load(open(snap + '.json'))
        # repairs to damaged tape images: {"ADDR": "hex bytes"} written over the snapshot before translating
        # (data is hex bytes, or {"tape", "block", "len"}: the same address read from another tape image,
        # so no game code needs to be kept in this repository)
        for addr, data in cfg.get('patches', {}).items():
            at = int(addr, 16)
            if isinstance(data, dict):
                from tap import parse
                blk = parse(open(os.path.join(ROOT, data['tape']), 'rb').read())[data.get('block', 0)]
                b = bytes(blk['data'][at - blk['start']:at - blk['start'] + data['len']])
            else:
                b = bytes.fromhex(data)
            self.mem[at:at + len(b)] = b
        self.ns = cfg['namespace']
        self.cls = cfg['class']
        self.out = os.path.join(ROOT, cfg['out'])
        self.writable_rom = cfg.get('writable_rom', False)
        self.hooks = {int(k, 16): v for k, v in cfg.get('hooks', {}).items()}
        # probes: the app is told when the program reaches these addresses
        self.probes = set(int(k, 16) for k in cfg.get('probes', []))
        self.entries_extra = sorted(self.probes)
        self.volatile = [(int(a, 16), int(b, 16)) for a, b in cfg.get('volatile', [["0200", "02FF"]])]
        self.dynamic = set(int(a, 16) for a in cfg.get('dynamic', []))
        self.notcode = [(int(a, 16), int(b, 16)) for a, b in cfg.get('data', [])]
        self.inline = {int(k, 16): v for k, v in cfg.get('inline', {}).items()}
        self.labels = {}
        if cfg.get('labels'):
            for line in open(os.path.join(ROOT, cfg['labels'])):
                t = line.split()
                if len(t) >= 3 and t[0] == 'al':
                    a, n = int(t[1], 16), t[2].lstrip('.')
                    if a not in self.labels or n.startswith('Z_') or (not n.startswith('@') and self.labels[a].startswith('@')):
                        self.labels[a] = n
        self.seeds = []
        self.dbg_data = None
        # memory used to find and decode the code: the snapshot, plus blobs a
        # program loads at run time before running them (e.g. Turbo's menus)
        self.dmem = bytearray(self.mem)
        self.overlay_ranges = []
        for path, addr in cfg.get('discovery_overlay', []):
            blob = open(os.path.join(ROOT, path), 'rb').read()
            a = int(addr, 16)
            self.dmem[a:a + len(blob)] = blob
            self.overlay_ranges.append((a, a + len(blob) - 1))
        # code the program writes at run time (e.g. "JMP label" stubs): bytes, or
        # "label:NAME" for a little-endian label address
        for addr, items in cfg.get('discovery_bytes', {}).items():
            a = int(addr, 16)
            for it in items.split():
                if it.startswith('label:'):
                    v = next(k for k, n in self.labels.items() if n == it[6:])
                    self.dmem[a] = v & 0xFF
                    self.dmem[a + 1] = v >> 8
                    a += 2
                else:
                    self.dmem[a] = int(it, 16)
                    a += 1
            self.seeds.append(int(addr, 16))
        if cfg.get('dbg'):
            self.load_dbg(os.path.join(ROOT, cfg['dbg']))
        self.entries = [self.state['pc']] if cfg.get('snapshot_pc', True) else []
        self.entries += [int(a, 16) for a in cfg.get('entries', [])]
        self.entries += self.entries_extra
        self.code_writes = set()
        cw = os.path.join(ROOT, 'originals', 'cov', name + '.codewrites')
        if os.path.exists(cw):
            for line in open(cw):
                for t in line.split():
                    self.code_writes.add(int(t, 16))
        self.code_values = {}
        cv = os.path.join(ROOT, 'originals', 'cov', name + '.codevalues')
        if os.path.exists(cv):
            for line in open(cv):
                for t in line.split():
                    a, v = t.split(':')
                    self.code_values.setdefault(int(a, 16), set()).add(int(v, 16))
        cov = os.path.join(ROOT, 'originals', 'cov', name + '.entries')
        if os.path.exists(cov):
            for line in open(cov):
                line = line.strip()
                if line:
                    self.entries.append(int(line.split()[0], 16))
        # interrupt vectors (the vector itself is in ROM or RAM: followed at run time
        # by the dispatcher; its current target is an entry point)
        for v in ((0xFFFE,) if cfg.get('snapshot_pc', True) else ()):  # (NMI is the reset button: not used)
            t = self.mem[v] | self.mem[v + 1] << 8
            if t >= 0x0100:
                self.entries.append(t)

    def load_dbg(self, path):
        """ld65 debug info: spans without a data type are instructions."""
        segs = {}
        spans = []
        for line in open(path):
            kind, _, rest = line.rstrip('\n').partition('\t')
            if kind not in ('seg', 'span'):
                continue
            f = dict(kv.split('=', 1) for kv in rest.split(','))
            if kind == 'seg':
                name = f['name'].strip('"')
                segs[f['id']] = (int(f['start'], 16), 'CODE' in name or 'STARTUP' in name, name)
            else:
                spans.append(f)
        self.dbg_data = set()
        for f in spans:
            base, is_code, sname = segs[f['seg']]
            if sname in self.cfg.get('dbg_data_segments', []):
                self.dbg_data.update(range(base + int(f['start']), base + int(f['start']) + int(f['size'])))
                continue
            a = base + int(f['start'])
            n = int(f['size'])
            if n == 0:
                continue
            if is_code and n == 1 and self.dmem[a] in (0x2C, 0x24):
                self.seeds.append(a)            # ".byte $2C": BIT used to skip the next instruction
            elif 'type' in f:
                self.dbg_data.update(range(a, a + n))
            elif n <= 3 and self.dmem[a] != 0x00:
                # an instruction (also code kept in writable segments); a zero
                # byte is reserved space (.res), not a BRK
                d = decode(self.dmem, a)
                if d and d[3] == n:
                    self.seeds.append(a)
                elif not is_code:
                    self.dbg_data.update(range(a, a + n))
            elif not is_code:
                self.dbg_data.update(range(a, a + n))
        # blobs share addresses with other segments: code wins
        for a in self.seeds:
            for i in range(decode(self.dmem, a)[3]):
                self.dbg_data.discard(a + i)

    def is_volatile(self, a):
        return any(lo <= a <= hi for lo, hi in self.volatile)

    def is_data(self, a):
        if self.dbg_data is not None and a in self.dbg_data:
            return True
        return any(lo <= a <= hi for lo, hi in self.notcode)

    # ------------------------------------------------------------------
    def discover(self):
        for _ in range(4):
            self.late_entries = []
            self._discover()
            if not self.late_entries:
                break
            self.entries += self.late_entries

    def _discover(self):
        mem = self.dmem
        self.ins = {}          # pc -> (m, mode, arg, n, cyc)
        self.leaders = set()
        self.bad = []
        work = list(self.entries) + list(self.seeds)
        self.leaders.update(self.entries)
        self.parent = {e: None for e in self.entries + self.seeds}
        FLAG_OF = {'BPL': ('N', 0), 'BMI': ('N', 1), 'BVC': ('V', 0), 'BVS': ('V', 1),
                   'BCC': ('C', 0), 'BCS': ('C', 1), 'BNE': ('Z', 0), 'BEQ': ('Z', 1)}
        SETS = {'C': {'ADC', 'SBC', 'CMP', 'CPX', 'CPY', 'ASL', 'LSR', 'ROL', 'ROR', 'PLP', 'RTI'},
                'V': {'ADC', 'SBC', 'BIT', 'PLP', 'RTI'},
                'Z': {'LDA', 'LDX', 'LDY', 'ADC', 'SBC', 'AND', 'ORA', 'EOR', 'CMP', 'CPX', 'CPY', 'BIT', 'INC', 'DEC',
                      'INX', 'INY', 'DEX', 'DEY', 'ASL', 'LSR', 'ROL', 'ROR', 'TAX', 'TAY', 'TXA', 'TYA', 'TSX', 'PLA', 'PLP', 'RTI'}}
        SETS['N'] = SETS['Z']
        self.always = set()
        while work:
            pc = work.pop()
            known = {}                      # flags known on this straight-line path
            while True:
                if pc in self.ins:
                    break
                if pc in self.hooks:
                    self.ins[pc] = ('HOOK', 'imp', None, 1, 0)
                    break
                if self.is_data(pc):
                    self.bad.append((pc, 'data'))
                    break
                d = decode(mem, pc)
                if d is None:
                    self.bad.append((pc, f'opcode {mem[pc]:02X}'))
                    self.ins[pc] = ('BAD', 'imp', mem[pc], 1, 0)
                    break
                self.ins[pc] = d
                m, mode, arg, n, cyc = d
                nxt = (pc + n) & 0xFFFF
                for t in ([arg] if m in BRANCH or (m in ('JMP', 'JSR') and mode == 'abs') else []) + [nxt]:
                    self.parent.setdefault(t, pc)
                if m in BRANCH:
                    self.leaders.add(arg)
                    work.append(arg)
                    f, want = FLAG_OF[m]
                    if known.get(f) == want:          # always taken: no fall-through
                        self.always.add(pc)
                        break
                    self.leaders.add(nxt)
                    known[f] = 1 - want
                    pc = nxt
                    continue
                for f, ms in SETS.items():
                    if m in ms:
                        known.pop(f, None)
                if m == 'CLC': known['C'] = 0
                elif m == 'SEC': known['C'] = 1
                elif m == 'CLV': known['V'] = 0
                elif m in ('LDA', 'LDX', 'LDY') and mode == 'imm':
                    known['Z'] = 1 if arg == 0 else 0
                    known['N'] = 1 if arg & 0x80 else 0
                if m == 'JMP':
                    if mode == 'abs':
                        self.leaders.add(arg)
                        work.append(arg)
                    break
                if m == 'JSR':
                    self.leaders.add(arg)
                    work.append(arg)
                    if arg in self.inline:              # inline data after the call
                        nxt = (nxt + self.inline[arg]) & 0xFFFF
                        if self.inline[arg] < 0:
                            break                       # never returns
                    self.leaders.add(nxt)
                    pc = nxt
                    continue
                if m in ('RTS', 'RTI'):
                    break
                if m == 'BRK':           # never used as a call by these programs: data
                    self.bad.append((pc, 'BRK'))
                    break
                if m in ('CLI', 'PLP'):                 # interrupts may become enabled
                    self.leaders.add(nxt)
                pc = nxt
        # unrolled routines entered part-way at computed offsets: every instruction is an entry
        for lo, hi in self.cfg.get('leader_ranges', []):
            lo, hi = int(lo, 16), int(hi, 16)
            for pc in self.ins:
                if lo <= pc <= hi:
                    self.leaders.add(pc)
        # a seed that nothing falls through into is reached some other way
        # (computed jump, return past inline data): it must be a leader
        falls = set()
        for pc, (m, mode, arg, n, c) in self.ins.items():
            if m in ('JMP', 'RTS', 'RTI', 'BRK', 'HOOK', 'BAD') or pc in self.always:
                continue
            if m == 'JSR' and arg in self.inline:
                continue
            falls.add((pc + n) & 0xFFFF)
        for sd in self.seeds:
            if sd not in falls:
                self.leaders.add(sd)
        # constant-address writes into code -> instructions with run-time operands
        code_bytes = {}
        for pc, (m, mode, arg, n, c) in self.ins.items():
            for i in range(n):
                code_bytes.setdefault((pc + i) & 0xFFFF, []).append((pc, i))
        self.code_bytes = code_bytes
        written = set()
        for pc, (m, mode, arg, n, c) in self.ins.items():
            if m in WRITES and mode in ('abs', 'zp'):
                written.add(arg)
            if m in WRITES and mode in ('abx', 'aby'):
                # an indexed store into code: assume up to 255 bytes after the base
                pass
        # values stored into code bytes by "LDr #v ; STr addr" (for rewritten opcodes)
        self.smc_vals = {}
        for pc, (m, mode, arg, n, c) in self.ins.items():
            if m in ('STA', 'STX', 'STY') and mode in ('abs', 'zp') and arg in code_bytes \
                    and not any(lo <= arg <= hi for lo, hi in self.overlay_ranges):
                prev = None
                for back in (2, 3):
                    q = (pc - back) & 0xFFFF
                    if q in self.ins and self.ins[q][3] == back:
                        prev = self.ins[q]
                        break
                v = None
                if prev and prev[0] == 'LD' + m[2] and prev[1] == 'imm':
                    v = prev[2]
                cur = self.smc_vals.setdefault(arg, set())
                if v is None:
                    cur.add(None)
                else:
                    cur.add(v)
        self.smc_ops = set()
        # (code in an overlay is loaded whole before it runs: the program's
        # other uses of that memory are data, not changes to the code)
        in_ovl = lambda a: any(lo <= a <= hi for lo, hi in self.overlay_ranges)
        for a in written | self.code_writes:
            if in_ovl(a):
                continue
            for (pc, i) in code_bytes.get(a, []):
                if i == 0:
                    self.smc_ops.add(pc)          # the opcode itself is rewritten
                else:
                    self.dynamic.add(pc)
        for pc in list(self.ins):
            if self.is_volatile(pc):
                self.dynamic.add(pc)
        # instructions whose opcode is rewritten: the possible opcodes
        self.smc_cands = {}
        for pc in self.smc_ops:
            m, mode, arg, n, c = self.ins[pc]
            vals = set(self.smc_vals.get(pc, set())) | self.code_values.get(pc, set())
            cands = {mem[pc]}
            # values seen written (any size: e.g. an RTS cutting a chain short)
            cands |= {v for v in vals if v is not None and v in OPS and OPS[v][0] not in ('BRK',)}
            for op, (m2, mode2, c2) in OPS.items():
                if op > 0xFF or m2 in ('BRK', 'RTI', 'RTS'):
                    continue
                same = mode2 == mode or (n == 1 and SIZE[mode2] == 1)
                if not same:
                    continue
                if m2 in ('JSR', 'JMP') and m not in ('JSR', 'JMP') and op not in vals:
                    continue
                if m2 in BRANCH and m not in BRANCH:
                    continue
                cands.add(op)
            self.smc_cands[pc] = sorted(cands)
            for op in cands:
                n2 = SIZE[OPS[op][1]]
                if OPS[op][0] not in ('RTS', 'RTI', 'JMP'):
                    self.leaders.add((pc + n2) & 0xFFFF)
                    if (pc + n2) & 0xFFFF not in self.ins:
                        self.late_entries.append((pc + n2) & 0xFFFF)
            self.dynamic.add(pc)            # operands of such instructions are usually patched too

    # ------------------------------------------------------------------
    def emit(self):
        ins = self.ins
        pages = {}
        for pc in self.leaders:
            if pc in ins:
                pages.setdefault(pc >> 8, []).append(pc)
        out = []
        w = out.append
        w('// <auto-generated>')
        w(f'// Translated from the original 6502 machine code by tools/recomp/recomp.py ({self.name}).')
        w('// Do not edit: regenerate instead.')
        w('// </auto-generated>')
        w('#pragma warning disable CS0162, CS0164, IDE0059')
        w(f'namespace {self.ns};')
        w('')
        w(f'internal sealed partial class {self.cls} : Durell.Machine.Cpu6502')
        w('{')
        w('    protected override int Page(int pc)')
        w('    {')
        w('        switch (pc >> 8)')
        w('        {')
        for p in sorted(pages):
            w(f'            case 0x{p:02X}: return P{p:02X}(pc);')
        w('            default: return Miss(pc);')
        w('        }')
        w('    }')
        w('')
        # code map for SMC detection
        cb = sorted(a for a in self.code_bytes if not any(lo <= a <= hi for lo, hi in self.overlay_ranges))
        ranges = []
        for a in cb:
            if ranges and ranges[-1][1] == a - 1:
                ranges[-1][1] = a
            else:
                ranges.append([a, a])
        w('    protected override void MarkCode(bool[] map)')
        w('    {')
        for a, b in ranges:
            w(f'        for (int i = 0x{a:04X}; i <= 0x{b:04X}; i++) map[i] = true;')
        w('    }')
        w('')
        self.hook_names = sorted(set(self.hooks.values()))
        for p in sorted(pages):
            self.emit_page(w, p, sorted(pages[p]))
        w('}')
        os.makedirs(os.path.dirname(self.out), exist_ok=True)
        open(self.out, 'w').write('\n'.join(out) + '\n')

    def emit_page(self, w, page, leaders):
        lset = set(leaders)
        w(f'    private int P{page:02X}(int pc)')
        w('    {')
        w('        switch (pc)')
        w('        {')
        for pc in leaders:
            w(f'            case {h4(pc)}: goto L{pc:04X};')
        w('            default: return Miss(pc);')
        w('        }')
        for pc in leaders:
            self.emit_block(w, pc, lset)
        w('    }')
        w('')

    def go(self, target, lset):
        """Transfer to a static target: goto when it is in this page method."""
        if target in lset:
            return f'if (Cy >= Ev) return {h4(target)}; goto L{target:04X};'
        return f'return {h4(target)};'

    def emit_block(self, w, start, lset):
        if start in self.labels:
            w(f'    // {self.labels[start]}')
        w(f'    L{start:04X}:')
        if start in self.probes:
            w(f'        Probe({h4(start)});')
        pc = start
        off = 0             # cycles since the block start (for I/O timing)
        first = True
        while True:
            if not first and pc in lset:
                w(f'        Cy += {off}; ' + self.go(pc, lset))
                return
            first = False
            if pc not in self.ins:
                w(f'        Cy += {off}; return {h4(pc)};')
                return
            m, mode, arg, n, cyc = self.ins[pc]
            if m == 'HOOK':
                w(f'        Cy += {off}; return {self.hooks[pc]}();')
                return
            if m == 'BAD':
                w(f'        Cy += {off}; return Bad({h4(pc)});')
                return
            if pc in self.smc_ops:
                off = self.emit_variable(w, pc, n, off, lset)
                pc = (pc + n) & 0xFFFF
                continue
            dyn = pc in self.dynamic
            nxt = (pc + n) & 0xFFFF
            comment = f'        // {h4(pc)} {m} {mode} {"" if arg is None else hex(arg)}{" (run-time operand)" if dyn else ""}'
            w(comment)
            ctl = self.emit_ins(w, pc, m, mode, arg, n, cyc, off, dyn, nxt, lset)
            off += cyc
            if ctl is not None:
                # ctl: list of lines finishing the block; they take care of Cy
                for line in ctl(off):
                    w('        ' + line)
                return
            pc = nxt

    def emit_variable(self, w, pc, n, off, lset):
        """An instruction whose opcode the program rewrites: switch on it."""
        w(f'        // {h4(pc)}: opcode rewritten by the program')
        w(f'        switch (M[{h4(pc)}])')
        w('        {')
        cmax = 0
        nxt = (pc + n) & 0xFFFF
        for op in self.smc_cands[pc]:
            m2, mode2, c2 = OPS[op]
            cmax = max(cmax, c2)
            arg = decode(bytes([op]) + bytes(self.dmem[pc + 1:pc + 3]) + b'\0\0', 0)[2]
            if mode2 == 'rel' and arg is not None:
                arg = (arg + pc) & 0xFFFF
            n2 = SIZE[mode2]
            nxt2 = (pc + n2) & 0xFFFF
            lines = []
            ctl = self.emit_ins(lines.append, pc, m2, mode2, arg, n2, c2, off, True, nxt2, lset)
            if ctl is None and n2 != n:
                # a different length: continue at the end of this instruction
                ctl = (lambda t: (lambda o: [f'Cy += {o}; return {h4(t)};']))(nxt2)
            w(f'            case 0x{op:02X}: // {m2} {mode2}')
            w('            {')
            for line in lines:
                w('    ' + line)
            if ctl is not None:
                for line in ctl(off + c2):
                    w('                ' + line)
            else:
                w('                break;')
            w('            }')
        w(f'            default: Cy += {off}; return Bad({h4(pc)});')
        w('        }')
        return off + cmax

    # operand helpers -------------------------------------------------------
    def opnd8(self, pc, arg, dyn):
        return f'M[{h4((pc + 1) & 0xFFFF)}]' if dyn else f'0x{arg:02X}'

    def opnd16(self, pc, arg, dyn):
        return f'(M[{h4((pc + 1) & 0xFFFF)}] | M[{h4((pc + 2) & 0xFFFF)}] << 8)' if dyn else h4(arg)

    def ea(self, pc, mode, arg, dyn):
        """(expression, kind) kind: 'zp' | 'abs-const' | 'any'"""
        if mode == 'zp':
            return (self.opnd8(pc, arg, dyn), 'zp' if not dyn else 'zpd')
        if mode == 'zpx':
            return (f'(({self.opnd8(pc, arg, dyn)} + X) & 0xFF)', 'zpd')
        if mode == 'zpy':
            return (f'(({self.opnd8(pc, arg, dyn)} + Y) & 0xFF)', 'zpd')
        if mode == 'abs':
            if dyn:
                return (self.opnd16(pc, arg, dyn), 'any')
            return (h4(arg), 'const')
        if mode == 'abx':
            return (f'(({self.opnd16(pc, arg, dyn)} + X) & 0xFFFF)', 'any')
        if mode == 'aby':
            return (f'(({self.opnd16(pc, arg, dyn)} + Y) & 0xFFFF)', 'any')
        if mode == 'izx':
            z = self.opnd8(pc, arg, dyn)
            return (f'(M[({z} + X) & 0xFF] | M[({z} + X + 1) & 0xFF] << 8)', 'any')
        if mode == 'izy':
            z = self.opnd8(pc, arg, dyn)
            if dyn:
                return (f'(((M[{z}] | M[({z} + 1) & 0xFF] << 8) + Y) & 0xFFFF)', 'any')
            return (f'(((M[0x{arg:02X}] | M[0x{(arg + 1) & 0xFF:02X}] << 8) + Y) & 0xFFFF)', 'any')
        raise ValueError(mode)

    def penalty(self, pc, mode, arg, dyn):
        """Extra cycle when an indexed read crosses a page (as on the 6502)."""
        if mode == 'abx':
            lo = f'M[{h4((pc + 1) & 0xFFFF)}]' if dyn else f'0x{arg & 0xFF:02X}'
            return f' Cy += ({lo} + X) >> 8;'
        if mode == 'aby':
            lo = f'M[{h4((pc + 1) & 0xFFFF)}]' if dyn else f'0x{arg & 0xFF:02X}'
            return f' Cy += ({lo} + Y) >> 8;'
        if mode == 'izy':
            z = self.opnd8(pc, arg, dyn)
            return f' Cy += (M[{z}] + Y) >> 8;'
        return ''

    def is_io(self, a):
        return 0x0300 <= a <= 0x03FF

    def rd(self, pc, mode, arg, dyn, off):
        if mode == 'imm':
            return self.opnd8(pc, arg, dyn)
        e, kind = self.ea(pc, mode, arg, dyn)
        if kind in ('zp', 'zpd'):
            return f'M[{e}]'
        if kind == 'const':
            if self.is_io(arg):
                return f'IoRd({e}, {off})'
            return f'M[{e}]'
        return f'Rd({e}, {off})'

    def wr(self, pc, mode, arg, dyn, val, off):
        e, kind = self.ea(pc, mode, arg, dyn)
        if kind in ('zp', 'zpd'):
            return f'M[{e}] = (byte)({val});'
        if kind == 'const':
            if self.is_io(arg):
                return f'IoWr({e}, {val}, {off});'
            if arg >= 0xC000 and not self.writable_rom:
                return f'/* write to ROM {h4(arg)} ignored */'
            if arg in self.code_bytes:
                return f'WrCode({e}, {val});'
            return f'M[{e}] = (byte)({val});'
        return f'Wr({e}, {val}, {off});'

    # -------------------------------------------------------------------------
    def emit_ins(self, w, pc, m, mode, arg, n, cyc, off, dyn, nxt, lset):
        def W(s):
            w('        ' + s)
        rd = lambda: self.rd(pc, mode, arg, dyn, off)
        pen = self.penalty(pc, mode, arg, dyn) if m in ('LDA', 'LDX', 'LDY', 'ADC', 'SBC', 'AND', 'ORA', 'EOR', 'CMP', 'LAX', 'NOP') else ''
        if m in ('LDA', 'LDX', 'LDY'):
            r = m[2]
            W(f'{r} = {rd()}; ZR = NR = {r};{pen}')
        elif m in ('STA', 'STX', 'STY'):
            W(self.wr(pc, mode, arg, dyn, m[2], off))
        elif m == 'ADC':
            W(f'Adc({rd()});{pen}')
        elif m == 'SBC':
            W(f'Sbc({rd()});{pen}')
        elif m in ('AND', 'ORA', 'EOR'):
            op = {'AND': '&', 'ORA': '|', 'EOR': '^'}[m]
            W(f'A {op}= {rd()}; ZR = NR = A;{pen}')
        elif m in ('CMP', 'CPX', 'CPY'):
            r = {'CMP': 'A', 'CPX': 'X', 'CPY': 'Y'}[m]
            W(f'{{ int t = {rd()}; C = {r} >= t ? 1 : 0; ZR = NR = ({r} - t) & 0xFF; }}{pen}')
        elif m == 'BIT':
            W(f'{{ int t = {rd()}; ZR = A & t; NR = t; V = (t >> 6) & 1; }}')
        elif m in RMW:
            if mode == 'acc':
                W({'ASL': 'C = A >> 7; A = (A << 1) & 0xFF; ZR = NR = A;',
                   'LSR': 'C = A & 1; A >>= 1; ZR = NR = A;',
                   'ROL': '{ int t = (A << 1) | C; C = t >> 8; A = t & 0xFF; ZR = NR = A; }',
                   'ROR': '{ int t = A | (C << 8); C = t & 1; A = t >> 1; ZR = NR = A; }'}[m])
            else:
                e, kind = self.ea(pc, mode, arg, dyn)
                if kind in ('zp', 'zpd') or (kind == 'const' and not self.is_io(arg)):
                    src = f'M[{e}]'
                    if kind == 'any':
                        src = f'Rd({e}, {off})'
                else:
                    src = f'Rd({e}, {off})' if kind == 'any' else f'IoRd({e}, {off})'
                calc = {'INC': 't = (t + 1) & 0xFF;', 'DEC': 't = (t - 1) & 0xFF;',
                        'ASL': 'C = t >> 7; t = (t << 1) & 0xFF;', 'LSR': 'C = t & 1; t >>= 1;',
                        'ROL': 't = (t << 1) | C; C = t >> 8; t &= 0xFF;',
                        'ROR': 't |= C << 8; C = t & 1; t >>= 1;'}[m]
                if kind == 'any':
                    W(f'{{ int ea = {e}; int t = Rd(ea, {off}); {calc} ZR = NR = t; Wr(ea, t, {off + cyc - 1}); }}')
                else:
                    W(f'{{ int t = {src}; {calc} ZR = NR = t; {self.wr(pc, mode, arg, dyn, "t", off + cyc - 1)} }}')
        elif m in ('INX', 'INY', 'DEX', 'DEY'):
            r = m[2]
            d = '+ 1' if m[0] == 'I' else '- 1'
            W(f'{r} = ({r} {d}) & 0xFF; ZR = NR = {r};')
        elif m in ('TAX', 'TAY', 'TXA', 'TYA', 'TSX'):
            s, d = {'TAX': ('A', 'X'), 'TAY': ('A', 'Y'), 'TXA': ('X', 'A'), 'TYA': ('Y', 'A'), 'TSX': ('S', 'X')}[m]
            W(f'{d} = {s}; ZR = NR = {d};')
        elif m == 'TXS':
            W('S = X;')
        elif m == 'PHA':
            W('Push(A);')
        elif m == 'PHP':
            W('Push(GetP() | 0x30);')
        elif m == 'PLA':
            W('A = Pop(); ZR = NR = A;')
        elif m == 'PLP':
            W('SetP(Pop());')
            return lambda o: [f'Cy += {o}; Ev = 0; ' + self.go(nxt, lset)]
        elif m in ('CLC', 'SEC'):
            W(f'C = {1 if m == "SEC" else 0};')
        elif m == 'CLI':
            W('I = 0;')
            return lambda o: [f'Cy += {o}; Ev = 0; ' + self.go(nxt, lset)]
        elif m == 'SEI':
            W('I = 1;')
        elif m == 'CLV':
            W('V = 0;')
        elif m in ('CLD', 'SED'):
            W(f'D = {1 if m == "SED" else 0};')
        elif m == 'NOP':
            if mode not in ('imp', 'imm'):
                e = self.rd(pc, mode, arg, dyn, off)      # the dummy read (matters for I/O)
                if 'IoRd' in e or 'Rd(' in e:
                    W(f'_ = {e};')
        elif m in ('SLO', 'RLA', 'SRE', 'RRA', 'DCP', 'ISC'):
            e, kind = self.ea(pc, mode, arg, dyn)
            calc = {'SLO': 'C = t >> 7; t = (t << 1) & 0xFF; A |= t; ZR = NR = A;',
                    'RLA': 't = (t << 1) | C; C = t >> 8; t &= 0xFF; A &= t; ZR = NR = A;',
                    'SRE': 'C = t & 1; t >>= 1; A ^= t; ZR = NR = A;',
                    'RRA': 't |= C << 8; C = t & 1; t >>= 1; Adc(t);',
                    'DCP': 't = (t - 1) & 0xFF; C = A >= t ? 1 : 0; ZR = NR = (A - t) & 0xFF;',
                    'ISC': 't = (t + 1) & 0xFF; Sbc(t);'}[m]
            W(f'{{ int ea = {e}; int t = Rd(ea, {off}); {calc} Wr(ea, t, {off + cyc - 1}); }}')
        elif m == 'SAX':
            W(self.wr(pc, mode, arg, dyn, 'A & X', off))
        elif m == 'LAX':
            W(f'A = X = {rd()}; ZR = NR = A;{pen}')
        elif m == 'ANC':
            W(f'A &= {rd()}; ZR = NR = A; C = A >> 7;')
        elif m == 'ALR':
            W(f'A &= {rd()}; C = A & 1; A >>= 1; ZR = NR = A;')
        elif m == 'ARR':
            W(f'A &= {rd()}; A = (A >> 1) | (C << 7); ZR = NR = A; C = (A >> 6) & 1; V = ((A >> 6) ^ (A >> 5)) & 1;')
        elif m == 'SBX':
            W(f'{{ int t = (A & X) - {rd()}; C = t >= 0 ? 1 : 0; X = t & 0xFF; ZR = NR = X; }}')
        elif m in BRANCH:
            cond = BRANCH[m]
            if dyn:
                tgt = f'((0x{(pc + 2) & 0xFFFF:04X} + (sbyte)M[{h4((pc + 1) & 0xFFFF)}]) & 0xFFFF)'
                return lambda o: [f'Cy += {o};', f'if ({cond}) {{ Cy++; return {tgt}; }}', self.go(nxt, lset)]
            extra = 2 if (arg >> 8) != (nxt >> 8) else 1     # taken (+1 more across a page)
            return lambda o: [f'Cy += {o};', f'if ({cond}) {{ Cy += {extra}; {self.go(arg, lset)} }}', self.go(nxt, lset)]
        elif m == 'JMP':
            if mode == 'abs':
                if dyn:
                    return lambda o: [f'Cy += {o}; return {self.opnd16(pc, arg, True)};']
                return lambda o: [f'Cy += {o}; ' + self.go(arg, lset)]
            # indirect, with the 6502's page-wrap bug
            ptr = self.opnd16(pc, arg, dyn)
            return lambda o: [f'Cy += {o}; {{ int p = {ptr}; return M[p] | M[(p & 0xFF00) | ((p + 1) & 0xFF)] << 8; }}']
        elif m == 'JSR':
            ret = (pc + 2) & 0xFFFF
            W(f'Push(0x{ret >> 8:02X}); Push(0x{ret & 0xFF:02X});')
            if dyn:
                return lambda o: [f'Cy += {o}; return {self.opnd16(pc, arg, True)};']
            return lambda o: [f'Cy += {o}; ' + self.go(arg, lset)]
        elif m == 'RTS':
            return lambda o: [f'Cy += {o}; {{ int lo = Pop(); return ((Pop() << 8 | lo) + 1) & 0xFFFF; }}']
        elif m == 'RTI':
            return lambda o: [f'Cy += {o}; SetP(Pop()); Ev = 0; {{ int lo = Pop(); return Pop() << 8 | lo; }}']
        elif m == 'BRK':
            ret = (pc + 2) & 0xFFFF
            return lambda o: [f'Cy += {o}; return Brk({h4(ret)});']
        else:
            raise ValueError(m)
        return None


def write_snapshot(g):
    """The machine at the moment the translated program starts: registers, VIA
    and AY state and the 64K of memory (RAM + the ROM image), zlib-compressed."""
    import struct
    import zlib
    st = g.state
    v = st['via']
    hdr = b'DSN1' + struct.pack('<HBBBBBBBBHBBBB', st['pc'], st['a'], st['x'], st['y'], st['sp'], st['p'],
                                v['ier'], v['acr'], v['pcr'], v['t1_latch'], v['orb'], v['ora'], v['ddra'], v['ddrb'])
    hdr += bytes(st['ay'][:16])
    out = os.path.join(os.path.dirname(g.out), g.name + '.snap')
    open(out, 'wb').write(zlib.compress(hdr + bytes(g.mem), 9))


def why(g, a):
    chain = []
    while a is not None and len(chain) < 60:
        chain.append(a)
        a = g.parent.get(a)
    return ' <- '.join(f'{x:04X}' for x in chain)


def main():
    g = Game(sys.argv[1])
    g.discover()
    for a in sys.argv[2:]:
        print('why', a, ':', why(g, int(a, 16)))
    g.emit()
    write_snapshot(g)
    nins = len(g.ins)
    print(f'{g.name}: {nins} instructions, {len(g.leaders)} leaders, {len(g.dynamic)} run-time operands,'
          f' {len(g.smc_ops)} rewritten opcodes -> {os.path.relpath(g.out, ROOT)}')
    for pc, reason in g.bad[:40]:
        print(f"  stop at {pc:04X}: {reason}")
    if g.smc_ops:
        print('  rewritten opcodes at', ' '.join(f'{a:04X}' for a in sorted(g.smc_ops)))


if __name__ == '__main__':
    main()
