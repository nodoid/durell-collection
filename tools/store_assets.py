#!/usr/bin/env python3
"""Builds every store screenshot, store-art tile and app-preview video from the footage captured by
tools/Durell.Capture (see stores/README.md for the capture commands).

    python3 tools/store_assets.py            ->  stores/{ios,macos,android,windows}

Screenshots are the game's own frames, captured natively at each device's shape, with a caption band
drawn in the game's Oric font (parsed from BitmapFont.cs). Store art uses the game's own logo
(saved by the capture tool's icon script). Needs Pillow and ffmpeg.
"""
import os
import random
import re
import shutil
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CAPTURE = os.path.join(ROOT, "artifacts", "capture")
OUT = os.path.join(ROOT, "stores")
GENERATED = ["ios", "macos", "android", "windows"]  # rebuilt each run; stores/copy is written by store_copy.py

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from store_copy import CAPTIONS  # noqa: E402

# ---------------------------------------------------------------- font (from the game source)
_src = open(os.path.join(ROOT, "src", "Durell.Core", "Graphics", "BitmapFont.cs"), encoding="utf-8").read()
_block = _src[_src.index("Ascii ="):_src.index("};", _src.index("Ascii ="))]
GLYPHS = [int(h, 16) for h in re.findall(r"0x([0-9A-F]{2})", _block)]
assert len(GLYPHS) == 95 * 5


def text_image(text, scale, colour, outline=True):
    o = max(2, scale // 3) if outline else 0
    w = len(text) * 6 * scale
    img = Image.new("RGBA", (w + 2 * o, 8 * scale + 2 * o), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    layers = [((0, 0, 0, 255), (o + ox, o + oy)) for ox in (-o, 0, o) for oy in (-o, 0, o) if (ox, oy) != (0, 0)] if o else []
    layers.append((colour, (o, o)))
    for col, (dx, dy) in layers:
        for i, ch in enumerate(text):
            c = ord(ch) if 0x20 <= ord(ch) <= 0x7E else ord("?")
            for x, bits in enumerate(GLYPHS[(c - 0x20) * 5:(c - 0x20) * 5 + 5]):
                for y in range(8):
                    if bits & (1 << y):
                        x0, y0 = (i * 6 + x) * scale + dx, y * scale + dy
                        d.rectangle([x0, y0, x0 + scale - 1, y0 + scale - 1], fill=col)
    return img


def wrap(caption, max_chars):
    words, lines, line = caption.split(), [], ""
    for w in words:
        if line and len(line) + 1 + len(w) > max_chars:
            lines.append(line)
            line = w
        else:
            line = (line + " " + w).strip()
    lines.append(line)
    return lines


# ---------------------------------------------------------------- screenshots
# (still name, caption) in store order; the captions are store_copy.CAPTIONS without their numbers
from store_copy import STILLS  # noqa: E402
SHOTS = [(name, CAPTIONS[i][4:].upper()) for i, name in enumerate(STILLS)]

# folder: (capture look, width, height)
SIZES = {
    # App Store Connect categories (Apple's screenshot spec, Oct 2026; landscape).
    "ios/iphone-dynamic-island-large": ("iphone", 2868, 1320),    # 6.9"
    "ios/iphone-dynamic-island-medium": ("iphone", 2622, 1206),   # 6.3" (REQUIRED; scaled from the 6.9" capture)
    "ios/iphone-face-id-large": ("iphone", 2778, 1284),           # 6.5"
    "ios/ipad-13in": ("ipad", 2752, 2064),                        # REQUIRED for iPad apps
    "macos": ("mac", 2880, 1800),
    "android/phone": ("android-phone", 1920, 1080),
    "android/tablet-7in": ("android-tablet", 1920, 1200),
    "android/tablet-10in": ("android-tablet", 2560, 1600),
    "windows": ("windows", 1920, 1080),
}


def still(look, name):
    return Image.open(os.path.join(CAPTURE, look, "stills", name + ".png")).convert("RGB")


def cover(img, w, h):
    """Scale to fill w x h, cropping the (small) excess evenly."""
    s = max(w / img.width, h / img.height)
    img = img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)
    x, y = (img.width - w) // 2, (img.height - h) // 2
    return img.crop((x, y, x + w, y + h))


def backdrop(w, h, seed=3):
    """The game's deep-space look: a blue-violet gradient with stars."""
    img = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / h
        d.line([(0, y), (w, y)], fill=(int(10 + 34 * t), int(14 - 2 * t), int(52 + 22 * t)))
    rnd = random.Random(seed)
    for _ in range(int(w * h / 9000)):
        x, y, r = rnd.random() * w, rnd.random() * h, rnd.random() * max(1.0, w / 1400)
        v = int(150 + rnd.random() * 105)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(v, v, v))
    return img


def compose(frame, caption):
    """Caption in its own band at the top; the game frame fitted below it, framed, on the backdrop."""
    w, h = frame.size
    scale = max(3, round(h / 150))
    max_chars = max(16, int(w * 0.92 / (6 * scale)))
    lines = wrap(caption, max_chars)
    line_h = 10 * scale
    band = len(lines) * line_h + 5 * scale
    out = backdrop(w, h).convert("RGBA")
    for i, line in enumerate(lines):
        t = text_image(line, scale, (255, 214, 64, 255))
        out.alpha_composite(t, ((w - t.width) // 2, 3 * scale + i * line_h))
    margin = max(8, h // 60)
    avail_w, avail_h = w - 2 * margin, h - band - 2 * margin
    s = min(avail_w / w, avail_h / h)
    fw, fh = round(w * s), round(h * s)
    fx, fy = (w - fw) // 2, band + margin + (avail_h - fh) // 2
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ImageDraw.Draw(glow).rectangle([fx - margin // 2, fy - margin // 2, fx + fw + margin // 2, fy + fh + margin // 2], fill=(255, 190, 80, 120))
    out.alpha_composite(glow.filter(ImageFilter.GaussianBlur(margin)))
    ImageDraw.Draw(out).rectangle([fx - 2, fy - 2, fx + fw + 1, fy + fh + 1], outline=(255, 214, 120, 255), width=2)
    out.paste(frame.resize((fw, fh), Image.LANCZOS), (fx, fy))
    return out.convert("RGB")


def screenshots():
    for folder, (look, w, h) in SIZES.items():
        out_dir = os.path.join(OUT, folder)
        os.makedirs(out_dir, exist_ok=True)
        for i, (name, caption) in enumerate(SHOTS):
            img = cover(still(look, name), w, h)
            # numbered in store order (upload order), named after the shot
            name_out = f"{i + 1:02d}-{name[3:]}"
            if folder == "windows":
                # Microsoft forbids marketing text on screenshots; captions go in the listing instead.
                img.save(os.path.join(out_dir, f"{name_out}.png"), optimize=True)
                continue
            compose(img, caption).save(os.path.join(out_dir, f"{name_out}.png"), optimize=True)
        print(f"  {folder}: {len(SHOTS)} x {w}x{h}")


# ---------------------------------------------------------------- store art


def title_overlay(img, frac=0.6, top=0):
    """THE DURELL COLLECTION across the top (Microsoft: titles in the top two-thirds), with a subtitle."""
    w, h = img.size
    img = img.convert("RGBA")
    scale = max(2, int(w * frac / (21 * 6)))
    title = text_image("THE DURELL COLLECTION", scale, (255, 206, 84, 255))
    sub_scale = max(1, scale // 3)
    sub = text_image("CLASSIC ORIC GAMES", sub_scale, (220, 230, 255, 255))
    pad = max(6, h // 60)
    band = top + title.height + sub.height + 3 * pad
    shade = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(shade).rectangle([0, 0, w, band], fill=(8, 10, 34, 180))
    img.alpha_composite(shade.filter(ImageFilter.GaussianBlur(pad * 2)))
    img.alpha_composite(title, ((w - title.width) // 2, top + pad))
    img.alpha_composite(sub, ((w - sub.width) // 2, top + 2 * pad + title.height))
    return img.convert("RGB")


def art(look, name, w, h, hud=True):
    """A game frame with the status panel cropped away (Microsoft: avoid app UI in store art), filling w x h."""
    img = still(look, name)
    W, H = img.size
    pw = int(H * 4 / 3)                      # the game picture: 4:3, full height, centred
    x0 = (W - pw) // 2
    top = int(H * 0.06) if hud else 0        # drop the score line
    return cover(img.crop((x0, top, x0 + pw, H)), w, h)


def branded_key_art(img):
    """Xbox branded key art: a publisher band across the top, then the title, all in the top two-thirds."""
    w, h = img.size
    bar_h = max(28, h // 20)
    img = title_overlay(img, 0.8, top=bar_h).convert("RGBA")
    ImageDraw.Draw(img).rectangle([0, 0, w, bar_h], fill=(10, 8, 34, 255))
    t = text_image("ALL-THE-JOHNSONS", max(1, int(w * 0.8 / (16 * 6))) if bar_h > 20 else 1, (200, 220, 255, 255), outline=False)
    t = t.resize((min(t.width, int(w * 0.8)), int(t.height * min(t.width, int(w * 0.8)) / t.width)), Image.NEAREST)
    img.alpha_composite(t, ((w - t.width) // 2, (bar_h - t.height) // 2))
    return img.convert("RGB")


def store_art():
    win = os.path.join(OUT, "windows", "art")
    os.makedirs(win, exist_ok=True)
    art("mac", "02-harrier", 3840, 2160, hud=True).save(os.path.join(win, "super-hero-art-3840x2160.png"))  # no text
    art("mac", "02-harrier", 1920, 1080, hud=True).save(os.path.join(win, "super-hero-art-1920x1080.png"))
    for size in (2160, 1080):
        title_overlay(art("mac", "06-lunar", size, size, hud=False), 0.85).save(os.path.join(win, f"box-art-{size}x{size}.png"))
    for w, h in ((1440, 2160), (720, 1080)):
        title_overlay(art("mac", "03-scuba", w, h, hud=False), 0.9).save(os.path.join(win, f"poster-art-{w}x{h}.png"))
    Image.open(os.path.join(ROOT, "art", "generated", "icon-1024.png")).convert("RGB") \
        .resize((300, 300), Image.LANCZOS).save(os.path.join(win, "app-tile-icon-300x300.png"))
    print("  windows/art: hero, box, poster, tile icon")
    android = os.path.join(OUT, "android")
    os.makedirs(android, exist_ok=True)
    title_overlay(art("mac", "02-harrier", 1024, 500, hud=True), 0.7).save(os.path.join(android, "feature-graphic-1024x500.png"))
    Image.open(os.path.join(ROOT, "art", "generated", "icon-1024.png")).resize((512, 512), Image.LANCZOS) \
        .save(os.path.join(android, "icon-512.png"))
    print("  android: feature-graphic-1024x500.png, icon-512.png")


# ---------------------------------------------------------------- app-preview videos
def preview(look, out_name, w, h):
    src = os.path.join(CAPTURE, look)
    out = os.path.join(OUT, out_name)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    # Lossless master -> H.264 High at 30 fps with a stereo AAC soundtrack, as App Store Connect wants.
    vf = f"scale={w}:{h}:force_original_aspect_ratio=increase:flags=lanczos,crop={w}:{h},setsar=1,format=yuv420p"
    cmd = ["ffmpeg", "-y", "-loglevel", "error",
           "-i", os.path.join(src, "footage.mkv"), "-i", os.path.join(src, "soundtrack.wav"),
           "-vf", vf, "-r", "30", "-c:v", "libx264", "-profile:v", "high", "-level", "4.0",
           "-preset", "slow", "-b:v", "11M", "-minrate", "11M", "-maxrate", "11M", "-bufsize", "11M", "-x264-params", "nal-hrd=cbr",
           "-af", "loudnorm=I=-16:TP=-1.5:LRA=11", "-c:a", "aac", "-b:a", "256k", "-ar", "44100", "-ac", "2", "-shortest",
           "-t", "29.5", "-movflags", "+faststart", out]
    subprocess.run(cmd, check=True)
    print(f"  {out_name}")


def windows_trailer():
    """Microsoft Store trailer: 1920x1080 H.264 High, 48 kHz 384 kbps stereo AAC, plus a 1920x1080 PNG thumbnail."""
    src = os.path.join(CAPTURE, "video-mac")
    out = os.path.join(OUT, "windows", "trailer")
    os.makedirs(out, exist_ok=True)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error",
                    "-i", os.path.join(src, "footage.mkv"), "-i", os.path.join(src, "soundtrack.wav"),
                    "-vf", "scale=1920:1080:force_original_aspect_ratio=increase:flags=lanczos,crop=1920:1080,setsar=1,format=yuv420p", "-r", "30",
                    "-c:v", "libx264", "-profile:v", "high", "-preset", "slow", "-b:v", "20M", "-maxrate", "25M", "-bufsize", "40M",
                    "-bf", "2", "-g", "15", "-flags", "+cgop",
                    "-af", "loudnorm=I=-16:TP=-1.5:LRA=11", "-c:a", "aac", "-b:a", "384k", "-ar", "48000", "-ac", "2", "-shortest",
                    "-movflags", "+faststart", os.path.join(out, "trailer-1920x1080.mp4")], check=True)
    cover(still("mac", "01-collection"), 1920, 1080).save(os.path.join(out, "trailer-thumbnail-1920x1080.png"))
    print("  windows/trailer: trailer-1920x1080.mp4, trailer-thumbnail-1920x1080.png")


GAMEPLAY = [("harrier", "Harrier Attack"), ("harrier3d", "Harrier Attack 3D"), ("scuba", "Scuba Dive"), ("starfighter", "Star Fighter"),
            ("galaxy", "Galaxy"), ("lunar", "Lunar Lander"), ("turbo", "Turbo Esprit")]


def gameplay(gid, title):
    """A longer gameplay video of one game (1920x1080, H.264 + AAC), from artifacts/capture/gameplay-GAME."""
    src = os.path.join(CAPTURE, "gameplay-" + gid)
    out = os.path.join(OUT, "videos", f"gameplay-{gid}-1920x1080.mp4")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error",
                    "-i", os.path.join(src, "footage.mkv"), "-i", os.path.join(src, "soundtrack.wav"),
                    "-vf", "scale=1920:1080:flags=lanczos,setsar=1,format=yuv420p", "-r", "30",
                    "-c:v", "libx264", "-profile:v", "high", "-preset", "slow", "-crf", "16",
                    "-af", "loudnorm=I=-16:TP=-1.5:LRA=11", "-c:a", "aac", "-b:a", "256k", "-ar", "48000", "-ac", "2", "-shortest",
                    "-movflags", "+faststart", out], check=True)
    print(f"  videos/gameplay-{gid}-1920x1080.mp4")


def videos():
    """App previews (App Store: up to 30 s, these sizes), the Microsoft Store trailer, and gameplay videos.
    Copies go to ~/Movies/The Durell Collection."""
    preview("video-iphone", "ios/app-preview-iphone-1920x886.mp4", 1920, 886)
    preview("video-ipad", "ios/app-preview-ipad-1600x1200.mp4", 1600, 1200)
    preview("video-mac", "macos/app-preview-mac-1920x1080.mp4", 1920, 1080)
    windows_trailer()
    for gid, title in GAMEPLAY:
        if os.path.exists(os.path.join(CAPTURE, "gameplay-" + gid, "footage.mkv")):
            gameplay(gid, title)
    movies = os.path.expanduser("~/Movies/The Durell Collection")
    os.makedirs(movies, exist_ok=True)
    copies = [("ios/app-preview-iphone-1920x886.mp4", "The Durell Collection - iPhone app preview (1920x886).mp4"),
              ("ios/app-preview-ipad-1600x1200.mp4", "The Durell Collection - iPad app preview (1600x1200).mp4"),
              ("macos/app-preview-mac-1920x1080.mp4", "The Durell Collection - Mac app preview (1920x1080).mp4"),
              ("windows/trailer/trailer-1920x1080.mp4", "The Durell Collection - Windows trailer (1920x1080).mp4")]
    copies += [(f"videos/gameplay-{gid}-1920x1080.mp4", f"The Durell Collection - {title} gameplay (1920x1080).mp4") for gid, title in GAMEPLAY]
    for src, name in copies:
        p = os.path.join(OUT, src)
        if os.path.exists(p):
            shutil.copyfile(p, os.path.join(movies, name))
    print(f"  copied to {movies}")


README = """# Store assets

Everything here except `copy/` is generated, and the folder is git-ignored:

```bash
dotnet build tools/Durell.Capture -c Release
for l in mac iphone ipad windows android-phone android-tablet; do
  dotnet tools/Durell.Capture/bin/Release/net10.0/Durell.Capture.dll $l artifacts/capture/$l; done
python3 tools/store_copy.py
python3 tools/store_assets.py
```

The capture runs the real app and the real games at each device's own shape, so every frame is genuine.

| Folder | Contents |
|---|---|
| `ios/iphone-dynamic-island-large` (2868x1320), `ios/iphone-dynamic-island-medium` (2622x1206, **required**), `ios/iphone-face-id-large` (2778x1284), `ios/ipad-13in` (2752x2064, **required**) | 8 screenshots each |
| `macos/` (2880x1800) | 8 screenshots |
| `android/phone` (1920x1080), `tablet-7in` (1920x1200), `tablet-10in` (2560x1600), `feature-graphic-1024x500.png`, `icon-512.png` | Google Play |
| `windows/` (1920x1080, no text) + `windows/art/` | Microsoft Store |
| `copy/en/ios.txt`, `macos.txt`, `android.txt`, `windows.txt` | Every field each store form asks for, with character counts |
"""


def main():
    for g in GENERATED:
        p = os.path.join(OUT, g)
        if os.path.isdir(p):
            shutil.rmtree(p)
    print("screenshots")
    screenshots()
    print("store art")
    store_art()
    with open(os.path.join(OUT, "README.md"), "w") as f:
        f.write(README)


if __name__ == "__main__":
    if "--videos" in sys.argv:
        videos()
    else:
        main()
