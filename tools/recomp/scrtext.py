import sys
r=open(sys.argv[1],'rb').read()
for y in range(28):
    print(''.join(chr(c&0x7f) if 32<=(c&0x7f)<127 else '.' for c in r[0xBB80+y*40:0xBB80+y*40+40]))
