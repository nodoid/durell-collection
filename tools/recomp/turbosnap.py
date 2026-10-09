#!/usr/bin/env python3
"""Build the start-up snapshot and the disk-tracks resource for Turbo Esprit
from the cc65 build of the Oric port (turbo/build).

The program file is loaded at $0600 and the overlay into the RAM under the
ROM, as the boot stub leaves them; the Oric font at $9900 is the one the
HIRES command copies from the ROM.  The raw-sector blobs the game reads with
DREAD ($0530) go to turbo.disk: tracks 40.. x 17 sectors x 256 bytes."""
import json
import os
import struct
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
B = os.path.join(ROOT, 'turbo', 'build')
OUT = os.path.join(ROOT, 'src', 'Durell.Core', 'Games', 'Turbo')

def rd(p):
    return open(os.path.join(B, p), 'rb').read()

def layout():
    vals = {}
    for line in open(os.path.join(ROOT, 'turbo', 'src', 'disk.inc')):
        p = line.split(';')[0].split('=')
        if len(p) == 2 and p[0].strip().endswith('_TRACK'):
            vals[p[0].strip()] = int(p[1].strip(), 0)
    return vals

mem = bytearray(0x10000)
game = rd('turbo.bin')
mem[0x0600:0x0600 + len(game)] = game
rom = open(os.path.join(ROOT, 'originals', 'roms', 'basic11b.rom'), 'rb').read()
mem[0x9900:0x9900 + 768] = rom[0x3C78:0x3C78 + 768]
ovl = rd('turbo.bin.ovl')
mem[0xC000:0xC000 + len(ovl)] = ovl
os.makedirs(os.path.join(ROOT, 'originals', 'snap'), exist_ok=True)
open(os.path.join(ROOT, 'originals', 'snap', 'turbo.ram'), 'wb').write(mem)
json.dump(dict(pc=0x0600, a=0, x=0, y=0, sp=0xFF, p=0x24, cycles=0,
               via=dict(ier=0, acr=0, pcr=0xDD, t1_latch=0xFFFF, orb=0, ora=0, ddra=0, ddrb=0),
               ay=[0] * 7 + [0x7F] + [0] * 8, blocks=0, steps=0),
          open(os.path.join(ROOT, 'originals', 'snap', 'turbo.json'), 'w'), indent=1)

lay = layout()
zxram = open(os.path.join(ROOT, 'turbo', 'data', 'zxram.bin'), 'rb').read()
cities = zxram[0x686B - 0x5B00:0x6DB7 - 0x5B00]
blobs = [(lay['OVL_TRACK'], ovl), (lay['MENU_TRACK'], rd('turbo.bin.menu')), (lay['DASH_TRACK'], rd('turbo.bin.dash')),
         (lay['CITY_TRACK'], cities), (lay['HID_TRACK'], rd('turbo.bin.hid'))]
first = min(t for t, _ in blobs)
last = max(t + ((len(d) + 255) // 256 + 16) // 17 for t, d in blobs)
disk = bytearray((last - first) * 17 * 256)
for t, d in blobs:
    o = (t - first) * 17 * 256
    disk[o:o + len(d)] = d
os.makedirs(OUT, exist_ok=True)
open(os.path.join(OUT, 'turbo.disk'), 'wb').write(zlib.compress(struct.pack('<H', first) + bytes(disk), 9))
print('turbo: snapshot + disk tracks %d-%d' % (first, last - 1))
