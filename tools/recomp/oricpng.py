"""Decode an Oric screen (TEXT/HIRES, serial attributes) from 64K of memory to a PNG (dev tool)."""
from PIL import Image
PAL = [(0,0,0),(255,0,0),(0,255,0),(255,255,0),(0,0,255),(255,0,255),(0,255,255),(255,255,255)]
def render(m, path, hires=None):
    if hires is None:
        hires = (m[0xBFDF] & 0x1C) == 0x1C
    im = Image.new('RGB', (240, 224))
    px = im.load()
    for y in range(224):
        ink, paper, alt, dbl = 7, 0, False, False
        hl = hires and y < 200
        base = 0xA000 + y * 40 if hl else 0xBB80 + (y >> 3) * 40
        for col in range(40):
            b = m[base + col]
            inv = 7 if b & 0x80 else 0
            if (b & 0x60) == 0:
                v = b & 0x1F
                if v < 8: ink = v
                elif v < 16: alt = bool(v & 1); dbl = bool(v & 2)
                elif v < 24: paper = v & 7
                bits = 0
            elif hl:
                bits = b & 0x3F
            else:
                line = y & 7
                if dbl: line = (line >> 1) | (((y >> 3) & 1) << 2)
                cs = (0x9C00 if alt else 0x9800) if hires else (0xB800 if alt else 0xB400)
                bits = m[cs + (b & 0x7F) * 8 + line] & 0x3F
            for i in range(6):
                c = (ink ^ inv) if bits & (0x20 >> i) else (paper ^ inv)
                px[col * 6 + i, y] = PAL[c]
    im.save(path)
