# Architecture

## No emulator: translated programs

Each Oric game is translated, before the app is built, from its 6502 machine code into C#:

1. **Snapshot** - `tools/recomp/orictrace.py` boots the Oric ROM on a dev-time 6502 simulator (py65),
   types `CLOAD""`, feeds the tape image to the ROM's tape routines and saves the machine when the
   program has loaded (`originals/snap/GAME.ram/.json`). Galaxy needs the Oric-1 ROM (1.0); the
   others the Atmos ROM (1.1). Turbo Esprit's image comes from its cc65 build (`turbosnap.py`).
2. **Discovery** - `tools/recomp/recomp.py` finds the code by recursive descent from the entry
   points (plus, for Turbo, every instruction span in `turbo/build/turbo.dbg`). Flags known along
   straight-line code (CLC/BCC, BEQ/BNE) stop it running into data.
3. **Emission** - one C# method per 256-byte page; each basic block is a label, branches within the
   page are `goto`s, everything else returns the next address to `Cpu6502.Run`, the dispatcher, which
   also takes the VIA timer interrupt. Cycles are counted per instruction (with page-crossing
   penalties) so delay loops, timers and the sound run at the original speed.
4. **Self-modifying code** - instructions whose operands the program rewrites read them from memory
   at run time; instructions whose opcodes are rewritten become a `switch` on the live opcode over
   every candidate. Writes seen at run time (`originals/cov/*.codewrites/codevalues`) feed back in.
5. **Coverage** - `tools/recomp/explore.sh GAME FRAMES [runner args]` runs the game headless with
   random or scripted keys; any address reached that wasn't translated is logged
   (`originals/cov/GAME.entries`) and the loop retranslates until none remain. The tests run every
   game for ten minutes of random play.

The generated `*.g.cs` files must not be edited by hand: change `recomp.py` / `games.json` and
regenerate. Game-specific settings live in `tools/recomp/games.json` (hooks such as Turbo's disk
reader at `$0530`, probes, leader ranges for unrolled code entered part-way, discovery overlays).

## The machine around the program

`Machine/OricBus.cs` - the 6522 VIA (timers, IFR/IER, keyboard row select and sense, AY bus control,
mirrored across `$0300-$03FF`) and the AY-3-8912 registers, logging writes with their cycle.
`Machine/OricVideo.cs` - the ULA: TEXT and HIRES modes, serial attributes, double height, blink,
inverse video; 240 x 224 colour indices plus an "ink" flag. `Machine/AyChip.cs` - the sound chip at
1 MHz: tones, noise, envelopes, log volume; ORIGINAL mono or ENHANCED stereo with warmth and room.

## Looks

`Graphics/FrameRenderer.cs`: ORIGINAL = the indices through the Oric palette, 3x nearest then linear
(crisp, even pixels); ENHANCED = per-game `Look` (Games/Looks.cs) decides which flat background
pixels become painted scenery, the rest goes through the look's palette, Scale3x, a bevel, a drop
shadow and a glow map.

### Enhanced: scenes

On play screens every look redraws the game from its state instead of its pixels (menus and title
screens fall back to the upscaled picture above):

- `Look.AfterFrame` calls the look's `ReadScene` after every game frame; `Look.Attach` lets a look hook
  translator probes (`games.json` `probes`) to sample at a consistent point: Harrier `$22F3` (the
  instant each scroll ends: a tear-free world snapshot timed in CPU cycles), Turbo `$C11B` (the start
  of its sprite scan). `Games/Scene.cs` has `SceneTrack` (blend between the last two frames) and
  `Tweener` (for things the game moves in whole steps at their own rates: glide over the measured step).
- `DrawScene` draws the playfield; `HudRows` are kept from the original (score lines), or `SceneArea`
  limits the redraw to one part (Lunar's window, Turbo's road view) and the rest stays the original.
- Art is drawn in code at first use: `Graphics/Art` (`Canvas` = anti-aliased vector paths, gradients,
  noise, blur; `ArtCatalog` lists every piece; `ArtCache` makes textures; `tools/Durell.ArtLab OUT`
  renders them to PNG without a GPU).
- Worlds bigger than a screen are rebuilt from the game's own data: Harrier's columns as the game
  generates them (`Scenes/HarrierWorld.cs`, painted from its glyphs: bilinear-sampled and thresholded
  into smooth outlines, then textured), Scuba's sea plus the random cavern maze at `$1FA0` through the
  game's cell tables (`ScubaWorld`), Star Fighter's 256 x 256 sector from its object tables, Turbo's
  city from its map block at `$019D` (`TurboCity`: layout, seeded building types, the position formula).
- 3D (`Graphics/ThreeD`: `MeshBuilder`, models; `BasicEffect` with vertex colours, lighting and fog) is
  used by Harrier Attack 3D (`Scenes/Harrier3D.cs`, a `HarrierLook` subclass) and Turbo's city. Render
  targets carry a depth buffer for this.
- Harrier Attack 3D replaces the AY sound with `Audio/HarrierSound.cs` (`Look.MixAudio`), a synthesiser
  (`Audio/Synth.cs`) driven by state changes: engine by speed and take-off, rockets, bombs, explosions,
  flak, jet fly-bys, missile and low-fuel warnings, terrain warning, touchdown and fanfare.

The research behind the RAM maps (screen layouts, object tables, timings) was done per game against the
translated programs and the `turbo/` source; the addresses used are commented in each scene file.

## Input

`Input/InputState.cs` maps the host keyboard straight onto the Oric's keys. `Screens/PlayScreen.cs`
adds each game's `Controls` (`Games/Catalog.cs`): gamepad buttons, on-screen buttons, and on phones and
tablets **tilt**. `Input/Tilt.cs` takes gravity in screen axes from the head (`DurellGame.TiltProvider`:
CoreMotion on iOS, SensorManager on Android), low-pass filters it, measures roll and pitch from a neutral
taken when play starts/resumes (or when the TILT bubble is tapped), and engages a direction at 9 degrees
(released below 5). Per game, `TiltX`/`TiltY` say which arrow pairs tilt replaces (their on-screen pad
buttons are then hidden behind a spirit-level bubble) and `TiltPower` turns pitch into Lunar Lander's 0-9
motor keys while `LunarProgram.Flying`. `SaveData.Tilt` (default on) is toggled by TILT on the menu and
the pause menu, which appear only when an accelerometer reading arrives.

## High scores

`Games/Scores.cs`: where each game keeps its table (found by reading the translated code), restored
once the game has set its table up (after a frame count, or at a probe address), then watched and
saved to `durell-save.json` whenever it changes.
