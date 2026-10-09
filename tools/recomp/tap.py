import sys
def parse(data):
    i=0; out=[]
    while i < len(data):
        while i < len(data) and data[i]==0x16: i+=1
        if i>=len(data): break
        if data[i]!=0x24: i+=1; continue
        i+=1
        h=data[i:i+9]; i+=9
        name=b''
        while data[i]!=0: name+=bytes([data[i]]); i+=1
        i+=1
        typ=h[2]; auto=h[3]; end=(h[4]<<8)|h[5]; start=(h[6]<<8)|h[7]
        n=end-start+1
        body=data[i:i+n]; i+=n
        out.append(dict(name=name.decode('latin1'),type=typ,auto=auto,start=start,end=end,data=body))
    return out
if __name__=='__main__':
    for f in sys.argv[1:]:
        print('##',f)
        for b in parse(open(f,'rb').read()):
            print(f"  {b['name']!r:14} type={b['type']:#04x} auto={b['auto']:#04x} {b['start']:04X}-{b['end']:04X} len={len(b['data'])}")
