import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from disasm import dis
mem = bytearray(open(sys.argv[1], 'rb').read())
for r in sys.argv[2:]:
    a, b = r.split('-'); print('---', r); dis(mem, int(a, 16), int(b, 16))
