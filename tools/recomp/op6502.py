# 6502 opcode table: opcode -> (mnemonic, mode, cycles)
# modes: imp acc imm zp zpx zpy abs abx aby ind izx izy rel
_T = """
00 BRK imp 7|01 ORA izx 6|05 ORA zp 3|06 ASL zp 5|08 PHP imp 3|09 ORA imm 2|0A ASL acc 2|0D ORA abs 4|0E ASL abs 6
10 BPL rel 2|11 ORA izy 5|15 ORA zpx 4|16 ASL zpx 6|18 CLC imp 2|19 ORA aby 4|1D ORA abx 4|1E ASL abx 7
20 JSR abs 6|21 AND izx 6|24 BIT zp 3|25 AND zp 3|26 ROL zp 5|28 PLP imp 4|29 AND imm 2|2A ROL acc 2|2C BIT abs 4|2D AND abs 4|2E ROL abs 6
30 BMI rel 2|31 AND izy 5|35 AND zpx 4|36 ROL zpx 6|38 SEC imp 2|39 AND aby 4|3D AND abx 4|3E ROL abx 7
40 RTI imp 6|41 EOR izx 6|45 EOR zp 3|46 LSR zp 5|48 PHA imp 3|49 EOR imm 2|4A LSR acc 2|4C JMP abs 3|4D EOR abs 4|4E LSR abs 6
50 BVC rel 2|51 EOR izy 5|55 EOR zpx 4|56 LSR zpx 6|58 CLI imp 2|59 EOR aby 4|5D EOR abx 4|5E LSR abx 7
60 RTS imp 6|61 ADC izx 6|65 ADC zp 3|66 ROR zp 5|68 PLA imp 4|69 ADC imm 2|6A ROR acc 2|6C JMP ind 5|6D ADC abs 4|6E ROR abs 6
70 BVS rel 2|71 ADC izy 5|75 ADC zpx 4|76 ROR zpx 6|78 SEI imp 2|79 ADC aby 4|7D ADC abx 4|7E ROR abx 7
81 STA izx 6|84 STY zp 3|85 STA zp 3|86 STX zp 3|88 DEY imp 2|8A TXA imp 2|8C STY abs 4|8D STA abs 4|8E STX abs 4
90 BCC rel 2|91 STA izy 6|94 STY zpx 4|95 STA zpx 4|96 STX zpy 4|98 TYA imp 2|99 STA aby 5|9A TXS imp 2|9D STA abx 5
A0 LDY imm 2|A1 LDA izx 6|A2 LDX imm 2|A4 LDY zp 3|A5 LDA zp 3|A6 LDX zp 3|A8 TAY imp 2|A9 LDA imm 2|AA TAX imp 2|AC LDY abs 4|AD LDA abs 4|AE LDX abs 4
B0 BCS rel 2|B1 LDA izy 5|B4 LDY zpx 4|B5 LDA zpx 4|B6 LDX zpy 4|B8 CLV imp 2|B9 LDA aby 4|BA TSX imp 2|BC LDY abx 4|BD LDA abx 4|BE LDX aby 4
C0 CPY imm 2|C1 CMP izx 6|C4 CPY zp 3|C5 CMP zp 3|C6 DEC zp 5|C8 INY imp 2|C9 CMP imm 2|CA DEX imp 2|CC CPY abs 4|CD CMP abs 4|CE DEC abs 6
D0 BNE rel 2|D1 CMP izy 5|D5 CMP zpx 4|D6 DEC zpx 6|D8 CLD imp 2|D9 CMP aby 4|DD CMP abx 4|DE DEC abx 7
E0 CPX imm 2|E1 SBC izx 6|E4 CPX zp 3|E5 SBC zp 3|E6 INC zp 5|E8 INX imp 2|E9 SBC imm 2|EA NOP imp 2|EC CPX abs 4|ED SBC abs 4|EE INC abs 6
F0 BEQ rel 2|F1 SBC izy 5|F5 SBC zpx 4|F6 INC zpx 6|F8 SED imp 2|F9 SBC aby 4|FD SBC abx 4|FE INC abx 7
"""
# undocumented NMOS opcodes that behave reliably (programs do execute some of
# them, e.g. filler bytes run through as code)
_U = """
1A NOP imp 2|3A NOP imp 2|5A NOP imp 2|7A NOP imp 2|DA NOP imp 2|FA NOP imp 2
80 NOP imm 2|82 NOP imm 2|89 NOP imm 2|C2 NOP imm 2|E2 NOP imm 2
04 NOP zp 3|44 NOP zp 3|64 NOP zp 3|14 NOP zpx 4|34 NOP zpx 4|54 NOP zpx 4|74 NOP zpx 4|D4 NOP zpx 4|F4 NOP zpx 4
0C NOP abs 4|1C NOP abx 4|3C NOP abx 4|5C NOP abx 4|7C NOP abx 4|DC NOP abx 4|FC NOP abx 4
07 SLO zp 5|17 SLO zpx 6|0F SLO abs 6|1F SLO abx 7|1B SLO aby 7|03 SLO izx 8|13 SLO izy 8
27 RLA zp 5|37 RLA zpx 6|2F RLA abs 6|3F RLA abx 7|3B RLA aby 7|23 RLA izx 8|33 RLA izy 8
47 SRE zp 5|57 SRE zpx 6|4F SRE abs 6|5F SRE abx 7|5B SRE aby 7|43 SRE izx 8|53 SRE izy 8
67 RRA zp 5|77 RRA zpx 6|6F RRA abs 6|7F RRA abx 7|7B RRA aby 7|63 RRA izx 8|73 RRA izy 8
87 SAX zp 3|97 SAX zpy 4|8F SAX abs 4|83 SAX izx 6
A7 LAX zp 3|B7 LAX zpy 4|AF LAX abs 4|BF LAX aby 4|A3 LAX izx 6|B3 LAX izy 5
C7 DCP zp 5|D7 DCP zpx 6|CF DCP abs 6|DF DCP abx 7|DB DCP aby 7|C3 DCP izx 8|D3 DCP izy 8
E7 ISC zp 5|F7 ISC zpx 6|EF ISC abs 6|FF ISC abx 7|FB ISC aby 7|E3 ISC izx 8|F3 ISC izy 8
0B ANC imm 2|2B ANC imm 2|4B ALR imm 2|6B ARR imm 2|CB SBX imm 2|EB SBC imm 2
"""
OPS = {}
for part in _T.replace('\n', '|').split('|'):
    p = part.split()
    if p:
        OPS[int(p[0], 16)] = (p[1], p[2], int(p[3]))
for part in _U.replace('\n', '|').split('|'):
    p = part.split()
    if p:
        OPS[int(p[0], 16)] = (p[1], p[2], int(p[3]))
SIZE = {'imp': 1, 'acc': 1, 'imm': 2, 'zp': 2, 'zpx': 2, 'zpy': 2, 'izx': 2, 'izy': 2, 'rel': 2,
        'abs': 3, 'abx': 3, 'aby': 3, 'ind': 3}

def decode(mem, pc):
    op = mem[pc]
    if op not in OPS:
        return None
    m, mode, cyc = OPS[op]
    n = SIZE[mode]
    if n == 2:
        arg = mem[(pc + 1) & 0xFFFF]
        if mode == 'rel':
            arg = (pc + 2 + (arg - 256 if arg & 0x80 else arg)) & 0xFFFF
    elif n == 3:
        arg = mem[(pc + 1) & 0xFFFF] | mem[(pc + 2) & 0xFFFF] << 8
    else:
        arg = None
    return m, mode, arg, n, cyc

def fmt(m, mode, arg, sym=lambda a, w: None):
    def s(a, w):
        n = sym(a, w)
        return n if n else (f"${a:02X}" if w == 2 else f"${a:04X}")
    return m + {'imp': '', 'acc': ' A', 'imm': f" #${arg if arg is not None else 0:02X}",
                'zp': f" {s(arg,2) if arg is not None else ''}", 'zpx': f" {s(arg,2) if arg is not None else ''},X", 'zpy': f" {s(arg,2) if arg is not None else ''},Y",
                'abs': f" {s(arg,4) if arg is not None else ''}", 'abx': f" {s(arg,4) if arg is not None else ''},X", 'aby': f" {s(arg,4) if arg is not None else ''},Y",
                'ind': f" ({s(arg,4) if arg is not None else ''})", 'izx': f" ({s(arg,2) if arg is not None else ''},X)", 'izy': f" ({s(arg,2) if arg is not None else ''}),Y",
                'rel': f" {s(arg,4) if arg is not None else ''}"}[mode]
