#!/usr/bin/env python3
"""Writes the store listing copy: ONE file per platform (stores/copy/en/{ios,macos,android,windows}.txt),
each holding every field that store's submission form asks for, in console order, with character
counts checked against the store's limits. Edit the text here and re-run:

    python3 tools/store_copy.py
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import local_settings  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "stores", "copy", "en")
VERSION = re.search(r"<DurellVersion>(.*?)</DurellVersion>", open(os.path.join(ROOT, "Directory.Build.props")).read()).group(1)
SUPPORT = "https://<your web site>/DurellCollection-Support.html"
CONTACT = local_settings.get("CONTACT_EMAIL", "<your email>")
WIN_IDENTITY = local_settings.get("WINDOWS_IDENTITY", "<package identity>")
WIN_PUBLISHER = local_settings.get("WINDOWS_PUBLISHER", "<publisher>")
WIN_PUBLISHER_NAME = local_settings.get("WINDOWS_PUBLISHER_NAME", "<publisher display name>")
NAME = "The Durell Collection"
errors = []

# ---------------------------------------------------------------- shared text

INTRO = ("Six classic 8-bit games from Durell Software, the British publisher behind Harrier Attack, Scuba Dive "
         "and Turbo Esprit - their original Oric programs, playing again on your phone, tablet or computer - plus "
         "Harrier Attack 3D, the same mission flown from a chase camera.")

ABOUT = ("Every game runs its own original program, translated instruction by instruction into native code: no "
         "emulator, nothing remade by guesswork, so each plays exactly as it did in 1983. Choose ENHANCED and every "
         "game is redrawn in high-resolution artwork - the sea, the caves and space scroll smoothly in every direction "
         "instead of flipping screens, and Turbo Esprit's city becomes 3D - or ORIGINAL to see and hear them precisely "
         "as the Oric did. Switch at any time, even mid-game.")

GAMES = [
    "HARRIER ATTACK (1983): take off from the carrier and strike at ships, guns and jets with rockets and bombs",
    "HARRIER ATTACK 3D: the same game in 3D, from a chase camera, with all-new sound effects",
    "SCUBA DIVE (1983): dive for pearls among sharks, jellyfish and the octopus guarding the caves",
    "STAR FIGHTER (1983): hunt enemy ships across the galaxy from your cockpit",
    "GALAXY (1983): blast the swooping alien fleet, from Durell's Galaxy 5 tape",
    "LUNAR LANDER (1983): set the motors and bring the module down gently on the pad",
    "TURBO ESPRIT (1986): chase the drug smugglers' cars through the city in a Lotus Esprit Turbo",
]

FEATURES = [
    "The original games, exactly as they played - translated to native code, not emulated",
    "ENHANCED look: high-resolution artwork, smooth scrolling in every direction, a 3D city for Turbo Esprit",
    "Harrier Attack 3D: the original mission flown in 3D, with new engine, weapon and warning sounds",
    "ORIGINAL look and sound: the Oric's own pixels, colours and sound chip",
    "Your high scores are kept between sessions for every game",
    "How to play each game, built in",
]

CONTROLS = {
    "mobile": "Tilt to move and touch the screen to fire: tip your iPhone or iPad to fly, swim, steer and set the motors; any other keys a game needs are buttons on the screen, with a full Oric keyboard (KEYS) when wanted. Prefer buttons? Switch TILT off for an on-screen pad. Game controllers and hardware keyboards work too",
    "mac": "Your keyboard is the Oric's - every key works as it did, and you can redefine any game's keys - or use a game controller; resizable window or full screen",
    "android": "Tilt to move and touch the screen to fire; other keys are buttons on the screen, plus a full Oric keyboard (KEYS). Switch TILT off for an on-screen pad. Game controllers and keyboards work too",
    "windows": "Your keyboard is the Oric's - every key works as it did, and you can redefine any game's keys - or use an Xbox controller; resizable window or full screen (F11)",
}

CREDIT = ("Harrier Attack and Scuba Dive by Ronald Jeffs, Star Fighter by Mike Highfield, Galaxy by Philip Dierks, "
          "Lunar Lander by Robert White, Turbo Esprit by Mike Richardson (Oric conversion). The games remain the "
          "property of their copyright holders. Collection by Paul F. Johnson. No ads, no tracking, no internet "
          "connection needed.")

WHATS_NEW = ("First release: six Durell Software classics for the Oric plus Harrier Attack 3D, in an enhanced look or exactly as "
             "they were, with high scores saved for every game.")


def description(platform, bullet="•"):
    lines = [INTRO, "", ABOUT, "", "THE GAMES", ""]
    lines += [f"{bullet} {g}" for g in GAMES]
    lines += ["", "FEATURES", ""]
    lines += [f"{bullet} {f}" for f in FEATURES + [CONTROLS[platform]]]
    lines += ["", CREDIT]
    return "\n".join(lines)


REVIEW_NOTES = """The Durell Collection is an offline, single-player collection of seven arcade games (six classics plus a 3D version of one). No account, sign-in, network connection or purchase is needed; everything is available immediately. The app collects no data.

The games are the original 1980s Oric programs, translated ahead of time into native code (C#) and compiled into the app. The app contains no emulator, no interpreter and downloads no code.

HOW TO USE
The menu shows the seven games: choose one ({choose}) and press PLAY. LOOK switches between ENHANCED (new scenery and sound, the default) and ORIGINAL (exactly as on the Oric). SOUND turns the sound off and on, and the - and + beside it set the volume. HELP shows each game's keys.
{controls}

QUICK TEST
- Harrier Attack: press 1 (skill), then UP to take off; arrows fly, SPACE / ROCKETS fires.
- Galaxy: press 2, then SPACE; LEFT/RIGHT move, UP (or FIRE) shoots, SPACE (SHIELD) raises a shield.
- Lunar Lander: press 1, then SPACE; the number keys set the motors (try 4, then 2).{tilt}"""

REVIEW_CONTROLS = {
    "ios": "On iPhone and iPad the games are steered by TILTING the device (the accelerometer; no permission is needed): left/right and tipping the top edge away from or towards you act as the arrow keys, and in Lunar Lander tipping the top towards you sets the motor power. The way the device is held when a game starts or resumes is the neutral position; tapping the TILT bubble (bottom left) re-centres it. TILT on the menu or in the pause menu switches tilt off and brings back on-screen direction buttons. Touching the game picture fires; any other keys (bombs, shield, map) are buttons on the right, small keys at the top left give the menu keys (skill levels etc.), KEYS opens a full Oric keyboard, and II pauses (pause menu: resume, restart, look, sound, tilt, how to play, menu).",
    "macos": "The Mac keyboard is the Oric's keyboard (arrows, letters, digits, SPACE, RETURN); KEYS on the menu (or K, or KEYS in the pause menu) redefines a game's keys. Esc pauses, F2 switches the look, F11 toggles full screen. A game controller also works.",
}

APPLE_AGE = [
    ("Parental controls / age assurance", "No / No"),
    ("Unrestricted web access", "No"),
    ("User-generated content", "No"),
    ("Messaging and chat", "No"),
    ("Advertising", "No"),
    ("Profanity or crude humour", "None"),
    ("Horror or fear themes", "None"),
    ("Alcohol, tobacco or drug use or references", "Infrequent/Mild (Turbo Esprit: stop the drug smugglers' cars; no drugs shown)"),
    ("Medical or treatment information", "None"),
    ("Health or wellness topics", "No"),
    ("Sexual content or nudity", "None"),
    ("Cartoon or fantasy violence", "Infrequent/Mild (8-bit shooting of aircraft, ships and aliens)"),
    ("Realistic violence", "None"),
    ("Prolonged graphic or sadistic violence", "None"),
    ("Guns or other weapons", "None"),
    ("Simulated gambling / contests / loot boxes", "None / No / No"),
    ("Result", "9+ (12+ if the drug reference is rated higher)"),
]

IARC = [
    ("Category", "Game"),
    ("Violence", "Fantasy violence: 8-bit pixel planes, ships, aliens and cars are shot or crash and explode; no blood or gore"),
    ("Fear", "No"),
    ("Sexuality / nudity / crude language", "No / No / No"),
    ("Controlled substances", "References only: in Turbo Esprit the player stops drug smugglers' cars (no drugs shown or used)"),
    ("Gambling", "No"),
    ("Users interact or share content", "No"),
    ("Shares location / digital purchases", "No / No"),
    ("Unrestricted internet", "No"),
]

# ---------------------------------------------------------------- writing helpers


class Doc:
    def __init__(self, title):
        self.parts = [title, "=" * len(title)]

    def field(self, name, text, limit=None):
        n = len(text)
        head = f"{name}  ({n}/{limit} characters)" if limit else name
        if limit and n > limit:
            errors.append(f"{self.parts[0]}: {name} is {n} > {limit}")
        self.parts += ["", head, "-" * len(head), text]

    def lines(self, name, items, limit_each=None, max_items=None, note=""):
        longest = max(len(i) for i in items)
        head = name + (f"  (longest line {longest}/{limit_each})" if limit_each else "") + note
        if limit_each and longest > limit_each:
            errors.append(f"{self.parts[0]}: {name} line {longest} > {limit_each}")
        if max_items and len(items) > max_items:
            errors.append(f"{self.parts[0]}: {name} has {len(items)} > {max_items}")
        self.parts += ["", head, "-" * len(head)] + items

    def table(self, name, rows):
        w = max(len(k) for k, _ in rows)
        self.parts += ["", name, "-" * len(name)] + [f"{k.ljust(w)}  {v}" for k, v in rows]

    def save(self, filename):
        os.makedirs(OUT, exist_ok=True)
        with open(os.path.join(OUT, filename), "w", encoding="utf-8") as f:
            f.write("\n".join(self.parts) + "\n")


KEYWORDS = "durell,oric,retro,8-bit,harrier,scuba,turbo esprit,arcade,classic,80s,collection,galaxy"

STILLS = ["01-collection", "09-harrier3d", "02-harrier", "10-scuba-caves", "11-starfighter-combat", "05-galaxy", "07-turbo", "08-original"]

# The screenshot order and captions (store_assets.py uses the same list).
CAPTIONS = [
    "01  Six Durell classics from the Oric, plus Harrier Attack 3D",
    "02  Harrier Attack 3D: the original mission, flown in 3D",
    "03  Harrier Attack: strike from the carrier",
    "04  Scuba Dive: explore the caves in one flowing world",
    "05  Star Fighter: face the enemy from your cockpit",
    "06  Galaxy: the alien fleet swoops in",
    "07  Turbo Esprit: a whole 3D city to chase through",
    "08  Or play them exactly as they were on the Oric",
]

# ---------------------------------------------------------------- the four files


def apple(platform):
    ios = platform == "ios"
    d = Doc(f"THE DURELL COLLECTION - {'APP STORE (IPHONE AND IPAD)' if ios else 'MAC APP STORE'} - ENGLISH")
    d.parts.append(f"App Store Connect > {NAME} > {'iOS' if ios else 'macOS'} App > {VERSION} > English (U.K.)")
    d.field("NAME", NAME, 30)
    d.field("SUBTITLE", "Classic Oric games, reborn", 30)
    d.field("PROMOTIONAL TEXT", "Harrier Attack, Scuba Dive, Turbo Esprit and more: Durell's 8-bit classics, the original "
            "programs, enhanced - or exactly as they were.", 170)
    d.field("KEYWORDS", KEYWORDS, 100)
    d.field("DESCRIPTION", description("mobile" if ios else "mac"), 4000)
    d.field("WHAT'S NEW", WHATS_NEW, 4000)
    if ios:
        files = [
            "- iPhone 6.9\" Dynamic Island: stores/ios/iphone-dynamic-island-large/ (8 x 2868x1320)",
            "- iPhone 6.3\" Dynamic Island (REQUIRED): stores/ios/iphone-dynamic-island-medium/ (8 x 2622x1206)",
            "- iPhone Face ID, large: stores/ios/iphone-face-id-large/ (8 x 2778x1284)",
            "- iPad 13\" (REQUIRED): stores/ios/ipad-13in/ (8 x 2752x2064)",
            f"- Build: releases/DurellCollection-{VERSION}-ios.ipa (Transporter)",
        ]
    else:
        files = [
            "- Screenshots: stores/macos/ (8 x 2880x1800)",
            f"- Build: releases/DurellCollection-{VERSION}-macos.pkg (Transporter)",
        ]
    d.lines("FILES TO UPLOAD", files)
    d.table("ANSWERS IN THE CONSOLE", [
        ("Bundle ID", "uk.co.allthejohnsons.durellcollection"),
        ("SKU", "durellcollection"),
        ("Primary language", "English (U.K.)"),
        ("Category", "Games > Arcade (secondary: Games > Action)"),
        ("Price", "Free (or your choice); no in-app purchases"),
        ("Support URL", f"{SUPPORT}"),
        ("Privacy Policy URL", f"{SUPPORT}#privacy"),
        ("Copyright", "© 2026 Paul F. Johnson"),
        ("App Privacy", "Data Not Collected; no tracking"),
        ("Encryption", "None (ITSAppUsesNonExemptEncryption is already false in Info.plist)"),
        ("Sign-in required", "No (App Review Information: untick \"Sign-in required\")"),
        ("Devices", "iPhone and iPad, landscape, iOS 15+" if ios else "Apple silicon Macs, macOS 12+"),
        ("Contact", f"Paul F. Johnson, {CONTACT}"),
    ])
    d.table("AGE RATING", APPLE_AGE)
    notes = REVIEW_NOTES.format(controls=REVIEW_CONTROLS[platform], choose="tap a card" if ios else "arrow keys or click",
                                tilt="\n- On iPhone/iPad, UP is tipping the top edge away from you and the arrows are tilt; with TILT off the on-screen pad appears." if ios else "")
    d.field("APP REVIEW NOTES", notes, 4000)
    d.save(f"{platform}.txt")


def android():
    d = Doc("THE DURELL COLLECTION - GOOGLE PLAY - ENGLISH")
    d.parts.append(f"Play Console > {NAME} > Grow > Store presence > Main store listing > English (United Kingdom)")
    d.field("APP NAME", NAME, 30)
    d.field("SHORT DESCRIPTION", "Durell 8-bit classics, reborn in HD - and Harrier Attack in 3D.", 80)
    d.field("FULL DESCRIPTION", description("android"), 4000)
    d.field("RELEASE NOTES (en-GB)", WHATS_NEW, 500)
    d.lines("FILES TO UPLOAD", [
        "- App icon: stores/android/icon-512.png",
        "- Feature graphic: stores/android/feature-graphic-1024x500.png",
        "- Phone screenshots: stores/android/phone/ (8 x 1920x1080)",
        "- 7-inch tablet: stores/android/tablet-7in/ (1920x1200); 10-inch tablet: stores/android/tablet-10in/ (2560x1600)",
        f"- App bundle: releases/DurellCollection-{VERSION}-android.aab (Production or a testing track)",
    ])
    d.table("ANSWERS IN THE CONSOLE", [
        ("Package name", "uk.co.allthejohnsons.durellcollection"),
        ("Devices", "Android 12 (API 31) and later, phones and tablets, landscape; targets API 37"),
        ("App or game", "Game"),
        ("Category", "Arcade"),
        ("Tags", "Arcade, Retro, Classic, Shooter, Racing, Single player, Offline"),
        ("Free or paid", "Free; no in-app purchases"),
        ("Contains ads", "No"),
        ("Email", CONTACT),
        ("Website", f"{SUPPORT}"),
        ("Privacy policy", f"{SUPPORT}#privacy"),
        ("App access", "All functionality available without special access"),
        ("Target audience", "13 and over (retro arcade games; not designed for children)"),
        ("Data safety", "No data collected; no data shared; nothing to encrypt in transit"),
        ("Government app / financial / health", "No / No / No"),
        ("Play App Signing", "Enrol; upload key signing/durell-upload.keystore (alias durell)"),
    ])
    d.table("CONTENT RATING (IARC)", IARC + [("Expected result", "PEGI 7 / ESRB Everyone 10+ (or similar)")])
    d.save("android.txt")


def windows():
    d = Doc("THE DURELL COLLECTION - MICROSOFT STORE - ENGLISH")
    d.parts.append(f"Partner Center > {NAME} > Submission > Store listings > en-gb")
    d.field("PRODUCT NAME", NAME, 256)
    d.field("SHORT DESCRIPTION", "Six classic 8-bit games from Durell Software - Harrier Attack, Scuba Dive, Star Fighter, "
            "Galaxy, Lunar Lander and Turbo Esprit - running their original Oric programs, translated to native code - plus "
            "Harrier Attack 3D. Play them redrawn in high-resolution artwork with smooth scrolling and a 3D city, or "
            "exactly as they looked and sounded on the Oric.", 1000)
    d.field("DESCRIPTION", description("windows"), 10000)
    d.lines("PRODUCT FEATURES (ONE PER BOX)", [
        "Six Durell Software classics from the Oric, plus Harrier Attack 3D",
        "The original programs, translated to native code - not emulated",
        "ENHANCED look: painted scenery, smoothing, lighting and glow",
        "ORIGINAL look and sound, exactly as on the Oric",
        "Stereo sound stage for the enhanced look",
        "High scores saved for every game",
        "Keyboard (as on the Oric) and Xbox controller support",
        "Resizable window or full screen; runs natively on x64 and Arm PCs",
    ], 200, 20)
    d.lines("SEARCH TERMS (ONE PER BOX)", ["durell", "oric", "retro", "harrier attack", "turbo esprit", "arcade", "8-bit"], 30, 7)
    d.field("WHAT'S NEW IN THIS VERSION", WHATS_NEW, 1500)
    d.field("SHORT TITLE", "Durell Collection", 50)
    d.field("NOTES FOR CERTIFICATION",
            "Offline single-player collection of seven arcade games; no account, sign-in, network or purchase needed. On the "
            "menu choose a game with the arrow keys and press Enter (or click PLAY). The keyboard is the Oric's: each game "
            "uses its own keys (HELP lists them). Esc pauses (pause menu: resume, restart, look, sound, how to play, menu), "
            "F2 switches between the ENHANCED and ORIGINAL looks, F11 toggles full screen. An Xbox controller works "
            "throughout. High scores are saved in the app's own data folder. The games are the original 1980s programs "
            "translated to native code; no emulator or downloaded code. The package is a full-trust desktop app (see "
            "restricted capabilities).", 2000)
    d.lines("FILES TO UPLOAD", [
        "- Screenshots (8, no text on them): stores/windows/ (1920x1080); captions below",
        "- 1:1 App tile icon: stores/windows/art/app-tile-icon-300x300.png",
        "- 2:3 Poster art (title in top two-thirds): stores/windows/art/poster-art-1440x2160.png (also 720x1080)",
        "- 1:1 Box art (title in top two-thirds): stores/windows/art/box-art-2160x2160.png (also 1080x1080)",
        "- 16:9 Super hero art (no text): stores/windows/art/super-hero-art-3840x2160.png (also 1920x1080)",
        f"- Packages (upload both): releases/DurellCollection-{VERSION}-windows-x64.msix and -windows-arm64.msix",
    ])
    d.lines("SCREENSHOT CAPTIONS (IN ORDER)", CAPTIONS, 200)
    d.field("RESTRICTED CAPABILITIES",
            "Submission options > \"Why does your app need these capabilities?\" (runFullTrust is the only one declared):\n\n"
            "runFullTrust: The Durell Collection is a packaged desktop (Win32) game, built with .NET 10 and MonoGame (SDL2 "
            "with OpenGL for graphics, OpenAL for sound). Every packaged Win32 desktop app needs runFullTrust to start its "
            "executable (Windows.FullTrustApplication entry point). It is used only to run the game's own process. It does "
            "not use the internet, other apps, the user's documents or system settings, and needs no elevation. It reads "
            "the keyboard, mouse and game controllers, draws with OpenGL, plays sound, and saves settings and high scores "
            "in its own app data folder.", 1000)
    d.table("SYSTEM REQUIREMENTS", [
        ("OS", "Windows 10 version 1809 (build 17763) or later; Windows 11"),
        ("Architecture", "x64 and Arm64 (native package for each)"),
        ("Graphics (minimum)", "OpenGL 3.0 capable GPU and driver"),
        ("Memory (minimum / recommended)", "2 GB / 4 GB"),
        ("Storage", "About 150 MB"),
        ("Input", "Keyboard (required); mouse and Xbox controller optional"),
        ("Network", "Not required"),
    ])
    d.table("PRODUCT DECLARATIONS", [
        ("Accesses, collects or transmits personal information", "No"),
        ("Tested to meet accessibility guidelines", "No (leave unticked)"),
        ("Customers can install to alternate drives / removable storage", "Yes"),
        ("Allow Windows to back up app data", "Yes"),
        ("Depends on non-Microsoft drivers or NT services", "No"),
        ("Requires a Windows Mixed Reality headset", "No"),
        ("Uses the Microsoft Store in-app purchase system", "No"),
    ])
    d.table("ANSWERS IN THE CONSOLE", [
        ("Package identity", WIN_IDENTITY),
        ("Publisher", f"{WIN_PUBLISHER} (check Partner Center > Product identity)"),
        ("Publisher display name", WIN_PUBLISHER_NAME),
        ("Category", "Games > Classics (or Action & adventure)"),
        ("Pricing", "Free; no in-app purchases"),
        ("Age ratings", "IARC questionnaire: same answers as below"),
        ("Privacy policy URL", f"{SUPPORT}#privacy"),
        ("Website", f"{SUPPORT}"),
        ("Support contact", CONTACT),
        ("Copyright", "© 2026 Paul F. Johnson"),
        ("Game options", "Single player; Xbox controller supported; no online play"),
        ("Display", "Windowed and full screen; landscape"),
        ("Languages", "en-gb, en-us (as declared in the package manifest)"),
    ])
    d.table("CONTENT RATING (IARC)", IARC)
    d.save("windows.txt")


def main():
    for f in os.listdir(OUT) if os.path.isdir(OUT) else []:
        os.remove(os.path.join(OUT, f))  # this script owns stores/copy/en
    apple("ios")
    apple("macos")
    android()
    windows()
    if errors:
        print("OVER LIMIT:\n  " + "\n  ".join(errors))
        sys.exit(1)
    print("wrote", ", ".join(sorted(os.listdir(OUT))))


if __name__ == "__main__":
    main()
