import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import recomp
MATRIX = [['7','N','5','V','RCTL','1','X','3'],['J','T','R','F',None,'ESC','Q','D'],['M','6','B','4','CTRL','Z','2','C'],['K','9',';','-',None,None,'\\',"'"],['SPACE',',','.','UP','LSHIFT','LEFT','DOWN','RIGHT'],['U','I','O','P','FUNCT','DEL',']','['],['Y','H','G','E',None,'A','S','W'],['8','L','0','/','RSHIFT','RETURN',None,'=']]
def key(v):
    if not v & 0x80: return '-'
    return MATRIX[v & 7][(v >> 3) & 7]
g = recomp.Game(sys.argv[1]); g.discover()
pcs = sorted(g.ins)
for i, pc in enumerate(pcs):
    m, mode, arg, n, c = g.ins[pc]
    if m in ('LDA','LDX','LDY') and mode == 'abs' and arg in (0x0208, 0x0209, 0x02DF):
        seq = []
        q = pc + n
        for _ in range(12):
            if q not in g.ins: break
            m2, mo2, a2, n2, c2 = g.ins[q]
            if m2 in ('CMP','CPX','CPY') and mo2 == 'imm':
                seq.append(f"{a2:02X}={key(a2) if arg != 0x02DF else repr(chr(a2 & 0x7f))}")
            if m2 in ('RTS','JMP','RTI'): break
            q += n2
        print(f"{pc:04X} {m} ${arg:04X}: {' '.join(seq)}")
