#!/usr/bin/env python3
"""Contact sheet of PNGs: sheet.py out.png cols scale in1.png in2.png ..."""
import sys
from PIL import Image
out, cols, scale = sys.argv[1], int(sys.argv[2]), float(sys.argv[3])
ims = [Image.open(p).convert('RGB') for p in sys.argv[4:]]
w, h = int(ims[0].width * scale), int(ims[0].height * scale)
rows = (len(ims) + cols - 1) // cols
sheet = Image.new('RGB', (cols * (w + 4), rows * (h + 4)), (40, 40, 40))
for i, im in enumerate(ims):
    sheet.paste(im.resize((w, h), Image.NEAREST), ((i % cols) * (w + 4), (i // cols) * (h + 4)))
sheet.save(out)
