#!/usr/bin/env python3
"""Writes the self-contained support page (images embedded) to ~/Downloads/DurellCollection-Support.html.

    python3 tools/make_support_page.py

Uses the master icon and the Mac store captures in artifacts/capture/mac/stills.
"""
import base64
import io
import os

from PIL import Image

import local_settings

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
STILLS = os.path.join(ROOT, "artifacts", "capture", "mac", "stills")
OUT = os.path.expanduser("~/Downloads/DurellCollection-Support.html")
CONTACT = local_settings.get("CONTACT_EMAIL", "you@example.com")


def data_uri(path, width, crop_hud=False, fmt="JPEG"):
    img = Image.open(path).convert("RGB")
    if crop_hud:
        img = img.crop((0, 0, img.width, int(img.height * 0.86)))
    img = img.resize((width, round(img.height * width / img.width)), Image.LANCZOS)
    buf = io.BytesIO()
    img.save(buf, fmt, quality=82) if fmt == "JPEG" else img.save(buf, fmt, optimize=True)
    return f"data:image/{fmt.lower()};base64," + base64.b64encode(buf.getvalue()).decode()


def still(name, width=640, crop_hud=False):
    return data_uri(os.path.join(STILLS, name + ".png"), width, crop_hud)


ICON = data_uri(os.path.join(ROOT, "art", "generated", "icon-1024.png"), 144, fmt="PNG")
FAVICON = data_uri(os.path.join(ROOT, "art", "generated", "icon-1024.png"), 64, fmt="PNG")

GAMES = [
    ("Harrier Attack", "1983, Ronald Jeffs", "1 - 5 skill, then UP to take off. Arrows: climb, dive, faster, slower. SPACE fires rockets; Z to / drop bombs. Land back on the carrier before the fuel runs out.", "02-harrier"),
    ("Harrier Attack 3D", "the same game, in 3D", "The original Harrier Attack program, flown from a chase camera behind your jet, with new sound effects: the engine, take-off, rockets, bombs, explosions, flak, missile and low-fuel warnings, a terrain warning and the landing. Turn back on the final approach and the jet loops over and rolls. Same keys as Harrier Attack.", "09-harrier3d"),
    ("Scuba Dive", "1983, Ronald Jeffs", "1 - 5 skill. RIGHT then DOWN to dive from the boat; the arrows swim. Gather pearls, avoid the sea creatures, and return before your air runs out.", "03-scuba"),
    ("Star Fighter", "1983, Mike Highfield", "0 - 9 skill, then Y or N for sound. Arrows steer, SPACE fires.", "04-starfighter"),
    ("Galaxy", "1983, Philip Dierks", "1 - 4 skill, SPACE to set the volume. LEFT and RIGHT move, UP fires, and SPACE raises one of your few shields.", "05-galaxy"),
    ("Lunar Lander", "1983, Robert White", "1 - 4 level, SPACE to set the volume. The number keys set the motors (0 off, 9 full). Land gently, quickly and with fuel to spare. Y or N for another go.", "06-lunar"),
    ("Turbo Esprit", "1986, Mike Richardson (Oric conversion)", "Menu 1 - 8 (8 plays, 7 practises). J and L steer, S and A faster and slower, K fires, M shows the map, T gives up the current life.", "07-turbo"),
]
games_html = "\n".join(f'<h3>{t} <span style="color:var(--muted);font-weight:400">- {c}</span></h3><p>{h}</p>' for t, c, h, _ in GAMES)

HTML = f"""<!doctype html>
<html lang="en-GB">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>The Durell Collection Support</title>
<meta name="description" content="Help and support for The Durell Collection - Harrier Attack, Scuba Dive, Star Fighter, Galaxy, Lunar Lander and Turbo Esprit - for iPhone, iPad, Android, Mac and Windows.">
<link rel="icon" href="{FAVICON}">
<style>
:root {{ --bg: #F3F2F7; --pane: #FFFFFF; --chip: #E6E4EE; --text: #1B1A24; --muted: #62607A;
  --accent: #A05A00; --accent-2: #2C4AA8; --border: #DAD7E6; }}
@media (prefers-color-scheme: dark) {{
  :root:not([data-theme="light"]) {{ --bg: #070A22; --pane: #11163A; --chip: #1E2450; --text: #ECEBF5;
    --muted: #A6A8C8; --accent: #FFC93C; --accent-2: #7FD8FF; --border: #262E66; }}
}}
:root[data-theme="dark"] {{ --bg: #070A22; --pane: #11163A; --chip: #1E2450; --text: #ECEBF5;
  --muted: #A6A8C8; --accent: #FFC93C; --accent-2: #7FD8FF; --border: #262E66; }}
* {{ box-sizing: border-box; }}
html {{ scroll-behavior: smooth; }}
body {{ margin: 0; background: var(--bg); color: var(--text);
  font: 16px/1.6 -apple-system, BlinkMacSystemFont, "Segoe UI", system-ui, Roboto, "Helvetica Neue", Arial, sans-serif; }}
a {{ color: var(--accent); }}
.wrap {{ max-width: 1000px; margin: 0 auto; padding: 0 16px; }}
header {{ background: var(--pane); border-bottom: 1px solid var(--border); }}
header .wrap {{ padding-top: 32px; padding-bottom: 26px; }}
.brand {{ display: flex; align-items: center; gap: 16px; }}
.brand img {{ width: 72px; height: 72px; border-radius: 16px; }}
.eyebrow {{ color: var(--accent-2); font-weight: 600; margin: 0; }}
h1 {{ font-size: clamp(28px, 5vw, 40px); line-height: 1.15; margin: 0; }}
.lead {{ font-size: 18px; color: var(--muted); margin: 16px 0 0; max-width: 48em; }}
.hero {{ width: 100%; border-radius: 14px; margin-top: 22px; display: block; }}
nav.toc {{ display: flex; flex-wrap: wrap; gap: 8px; margin-top: 22px; }}
nav.toc a {{ text-decoration: none; color: var(--text); background: var(--chip); border-radius: 999px; padding: 6px 14px; font-size: 14px; }}
main section {{ background: var(--pane); border: 1px solid var(--border); border-radius: 14px; padding: 8px 24px 16px; margin: 20px 0; }}
h2 {{ font-size: 24px; margin: 16px 0 8px; }}
h3 {{ font-size: 17px; margin: 18px 0 2px; }}
table {{ width: 100%; border-collapse: collapse; font-size: 15px; }}
.table {{ overflow-x: auto; }}
th, td {{ text-align: left; padding: 8px 10px; border-bottom: 1px solid var(--border); vertical-align: top; }}
th {{ color: var(--muted); font-weight: 600; }}
kbd {{ font: 13px ui-monospace, SFMono-Regular, Menlo, monospace; background: var(--chip); border: 1px solid var(--border);
  border-radius: 5px; padding: 1px 5px; }}
.gallery {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 12px; margin: 14px 0 6px; }}
.gallery img {{ width: 100%; border-radius: 10px; display: block; }}
.pixel {{ image-rendering: pixelated; }}
footer {{ color: var(--muted); font-size: 14px; padding: 8px 0 40px; }}
</style>
</head>
<body>
<header><div class="wrap">
  <div class="brand"><img src="{ICON}" alt="">
    <div><p class="eyebrow">Support</p><h1>The Durell Collection</h1></div></div>
  <p class="lead">Six classic Oric games from Durell Software, playing their original programs again - redrawn in high resolution, or exactly as they were - plus Harrier Attack in 3D. For iPhone, iPad, Android, Mac and Windows.</p>
  <img class="hero" src="{still('01-collection', 960)}" alt="The collection's menu">
  <nav class="toc"><a href="#about">The collection</a><a href="#looks">Enhanced and original</a><a href="#controls">Controls</a><a href="#games">The games</a><a href="#faq">Questions</a><a href="#privacy">Privacy</a><a href="#contact">Contact</a></nav>
</div></header>
<main class="wrap">
<section id="about"><h2>The collection</h2>
<p>Durell Software, of Taunton in Somerset, started out on the Oric. This collection brings back its Oric games - Harrier Attack, Scuba Dive, Star Fighter, and Galaxy and Lunar Lander from the Galaxy 5 tape - together with the Oric conversion of its 1986 hit Turbo Esprit.</p>
<p>Each game is its original program, translated instruction by instruction into native code for your device. There is no emulator: the games simply run, exactly as they did. The app has no ads, no in-app purchases and no tracking, and it never needs the internet.</p>
<div class="gallery"><img src="{still('02-harrier', 480)}" alt="Harrier Attack"><img src="{still('03-scuba', 480)}" alt="Scuba Dive"><img src="{still('07-turbo', 480)}" alt="Turbo Esprit"></div></section>

<section id="looks"><h2>Enhanced and original</h2>
<p>Choose with <em>LOOK</em> on the menu or in the pause menu, or press <kbd>F2</kbd> on a computer - you can switch at any time, even mid-game.</p>
<ul>
<li><strong>Enhanced</strong> (the default) redraws every game in high-resolution artwork, read live from the game as it plays: a real sky and sea with a shaded Harrier and carrier, an underwater world whose caves the camera follows smoothly in every direction (no more flip screens), a 3D city for Turbo Esprit, a lunar module coming down on its true height, and starfields and fleets in space. Everything glides smoothly between the original's steps. The sound chip's three voices are spread into stereo.</li>
<li><strong>Original</strong> shows the Oric's own 240 by 224 pixels in its eight colours, and plays its sound chip exactly as it was.</li>
</ul>
<img class="hero" src="{still('08-original', 960)}" alt="Scuba Dive in the original look"></section>

<section id="controls"><h2>Controls</h2><div class="table"><table><thead><tr><th></th><th>Mac and Windows</th><th>iPhone, iPad and Android</th></tr></thead><tbody>
<tr><td>Moving</td><td>The arrow keys (Turbo Esprit: <kbd>J</kbd> <kbd>L</kbd> steer, <kbd>S</kbd> <kbd>A</kbd> faster and slower)</td><td><strong>Tilt the device</strong>: left and right, and tip the top edge away from you (up) or towards you (down). Tap the <em>TILT</em> bubble to re-centre</td></tr>
<tr><td>Firing</td><td>The game's own key (rockets <kbd>Space</kbd>, Turbo <kbd>K</kbd>...)</td><td><strong>Touch the game picture</strong> - anywhere away from the buttons</td></tr>
<tr><td>The games' other keys</td><td>Your keyboard is the Oric's: letters, digits, arrows, <kbd>Space</kbd> and <kbd>Return</kbd> work as they did. <em>KEYS</em> on the menu (or <kbd>K</kbd>, or the pause menu) redefines any game's keys</td><td>Buttons on the screen for the other keys a game needs (bombs, shield, map); small keys at the top left for menu choices; <em>KEYS</em> opens a full Oric keyboard</td></tr>
<tr><td>Pause</td><td><kbd>Esc</kbd></td><td>The <em>II</em> button; Back on Android</td></tr>
<tr><td>Back to the menu</td><td><em>QUIT</em> at the top left (or <em>MENU</em> in the pause menu)</td><td><em>QUIT</em> at the top left</td></tr>
<tr><td>Switch the look</td><td><kbd>F2</kbd>, or <em>LOOK</em></td><td><em>LOOK</em> on the menu or the pause menu</td></tr>
<tr><td>Volume</td><td><em>-</em> and <em>+</em> beside <em>SOUND</em> on the menu, or the <kbd>-</kbd> and <kbd>+</kbd> keys (a controller's shoulder buttons)</td><td><em>-</em> and <em>+</em> beside <em>SOUND</em> on the menu</td></tr>
<tr><td>Full screen</td><td><kbd>F11</kbd></td><td>Always</td></tr>
</tbody></table></div>
<p>Game controllers work everywhere: the stick or D-pad and the A, B, X and Y buttons stand in for each game's main keys, and Start pauses.</p>
<h3>Tilt controls on iPhone, iPad and Android</h3>
<p>On phones and tablets you play by tilting the device, like a joystick. Hold it comfortably when a game starts - that's the neutral position - then tip it:</p>
<ul>
<li><strong>Left and right</strong> (like a steering wheel) for the left and right arrows.</li>
<li><strong>Top edge away from you</strong> for up, <strong>towards you</strong> for down.</li>
</ul>
<p>The bubble at the bottom left shows the tilt and lights an arrow as each direction engages. A small tilt is ignored, so a steady hand doesn't wander. Tap the bubble to make the way you're holding the device the new neutral; it is also re-centred whenever you start, restart or resume a game.</p>
<div class="table"><table><thead><tr><th>Game</th><th>Tilt</th></tr></thead><tbody>
<tr><td>Harrier Attack</td><td>Right / left: faster / slower. Away / towards you: climb / descend (away also takes off)</td></tr>
<tr><td>Scuba Dive</td><td>Swim in the direction you tip</td></tr>
<tr><td>Star Fighter</td><td>Steer in all four directions</td></tr>
<tr><td>Galaxy</td><td>Left and right move your ship (up and down aren't used)</td></tr>
<tr><td>Lunar Lander</td><td>Tip the top towards you for more motor power - level is 0, further back up to 9; the bar in the bubble shows the setting</td></tr>
<tr><td>Turbo Esprit</td><td>Left / right steer; away / towards you: faster / slower</td></tr>
</tbody></table></div>
<p>Firing, bombs, skill levels and the rest stay on the on-screen buttons. Prefer buttons? Turn <em>TILT</em> off on the menu or in the pause menu and the on-screen direction pad comes back. Tilt is only offered on devices with an accelerometer, and it needs no permission.</p></section>

<section id="games"><h2>The games</h2>
{games_html}</section>

<section id="faq"><h2>Questions</h2>
<h3>Where are my high scores kept?</h3><p>Every game's high scores are saved on your device, in the app's own storage, and are back next time you play. Uninstalling the app deletes them.</p>
<h3>Why does Star Fighter forget my high score sometimes?</h3><p>That is the original game: it keeps a high score only while you play at the same skill level. Choose a different level and it starts again, just as it did on the Oric.</p>
<h3>Why are some keys unusual?</h3><p>They are the original games' keys. The menu's <em>HELP</em> and the pause menu's <em>HOW TO PLAY</em> list them for each game.</p>
<h3>Tilt steering keeps drifting. What can I do?</h3><p>Tap the <em>TILT</em> bubble while holding the device the way you want to play - that becomes the new centre. If you are lying down or prefer buttons, turn <em>TILT</em> off on the menu.</p>
<h3>How do I change the volume or turn the sound off?</h3><p>On the menu, <em>-</em> and <em>+</em> either side of <em>SOUND</em> set the volume in steps of 10% (a beep lets you hear the new level), and the bar under it shows the level. Tapping <em>SOUND</em> itself, or <em>SOUND</em> in the pause menu, turns the sound off and back on at the same volume. The setting is kept between sessions.</p>
<h3>Can I play with a controller?</h3><p>Yes, on Mac, Windows, iPad, iPhone and Android, with any controller the system recognises.</p></section>

<section id="privacy"><h2>Privacy</h2><p><strong>The Durell Collection collects nothing.</strong> It has no accounts, advertising, analytics or tracking, and it never connects to the internet. It doesn't collect, store, share or sell any personal information, from anyone, including children.</p>
<p>The app keeps one small file in its private storage on your device with each game's high scores and your settings (look and sound). It never leaves your device and is deleted when you uninstall the app.</p>
<p>If this policy changes, the new version will be posted here with a new date. Last updated: 8 October 2026.</p></section>

<section id="contact"><h2>Contact</h2><p>Found a bug, or have a question this page doesn't answer? Email <a href="mailto:{CONTACT}?subject=Durell%20Collection%20support">{CONTACT}</a>.</p></section>
</main>
<footer class="wrap">Harrier Attack, Scuba Dive, Star Fighter, Galaxy, Lunar Lander and Turbo Esprit were published by Durell Software; the games remain the property of their copyright holders. Collection by Paul F. Johnson. © 2026 Paul F. Johnson.</footer>
</body>
</html>
"""

with open(OUT, "w", encoding="utf-8") as f:
    f.write(HTML)
print("wrote", OUT, len(HTML) // 1024, "KB")
