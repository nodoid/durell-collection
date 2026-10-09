import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from op6502 import decode, fmt
from tap import parse
def load_rom_syms(p):
    d={}
    for l in open(p):
        x=l.split()
        if len(x)>=2:
            try: d[int(x[0],16)]=x[1]
            except: pass
    return d
SYM=load_rom_syms(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../originals/roms/basic11b.sym'))
def dis(mem, start, end):
    pc=start
    while pc<end:
        d=decode(mem,pc)
        if d is None:
            print(f"{pc:04X}  {mem[pc]:02X}        .byte"); pc+=1; continue
        m,mode,arg,n,c=d
        b=' '.join(f"{mem[pc+i]:02X}" for i in range(n))
        print(f"{pc:04X}  {b:9} {fmt(m,mode,arg, lambda a,w: SYM.get(a) if a>=0xC000 else None)}")
        pc+=n
if __name__=='__main__':
    f=sys.argv[1]; bi=int(sys.argv[2]); s=int(sys.argv[3],16) if len(sys.argv)>3 else None; e=int(sys.argv[4],16) if len(sys.argv)>4 else None
    blocks=parse(open(f,'rb').read()); b=blocks[bi]
    mem=bytearray(65536); mem[b['start']:b['start']+len(b['data'])]=b['data']
    dis(mem, s or b['start'], e or b['end']+1)
