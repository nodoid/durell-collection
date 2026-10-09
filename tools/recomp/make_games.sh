#!/bin/zsh
# Rebuilds everything taken from the original games, which is NOT in git (copyright):
# the post-load memory snapshots (originals/snap), and from them the translated programs
# (src/Durell.Core/Games/*/*.g.cs) and the snapshots the app embeds (*.snap, Turbo's turbo.disk).
#
# Needs, in originals/ (git-ignored): the tapes from oric.org and the Oric ROM images -
#   originals/tapes/harrier/harriera.tap   originals/tapes/scuba/Scuba.tap
#   originals/tapes/starfighter/starfighter_atmos.tap (+ starfighter_oric1.tap, see games.json "patches")
#   originals/tapes/galaxy/galaxy.tap      originals/tapes/lunar-lander/lunar-lander.tap
#   originals/roms/basic11b.rom (Atmos)    originals/roms/basic10.rom (Oric-1, for Galaxy)
# plus the Turbo Esprit port (the turbo/ submodule, built with cc65) and Python 3 with py65.
# Lunar Lander's game itself is a hand port of the BASIC listing (src/Durell.Core/Games/Lunar/
# LunarProgram.cs), which is not in git either.
set -e
cd "$(dirname "$0")/../.."
mkdir -p originals/snap
snap() { python3 tools/recomp/orictrace.py "originals/tapes/$1" "originals/snap/$2" "${@:3}" >/dev/null; echo "snapshot $2"; }
snap harrier/harriera.tap harrier
snap scuba/Scuba.tap scuba
snap starfighter/starfighter_atmos.tap starfighter
snap galaxy/galaxy.tap galaxy 10
snap lunar-lander/lunar-lander.tap lunar --after 0.02
make -C turbo build/turbo.bin LD="ld65 --dbgfile build/turbo.dbg" >/dev/null
python3 tools/recomp/turbosnap.py >/dev/null && echo "snapshot turbo"
for g in harrier scuba starfighter galaxy lunar turbo; do
  python3 tools/recomp/recomp.py $g | head -1
done
