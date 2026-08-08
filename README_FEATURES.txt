================================================================================
                    8 BALL POOL GUIDELINES - USER GUIDE & FEATURES
================================================================================

Welcome to the 8 Ball Pool Guideline Overlay!
This program is an ultra-fast, smooth 60 FPS, click-through guideline helper for 8 Ball Pool featuring Mode switching (NORMAL MODE vs TRICKSHOT MODE).

--------------------------------------------------------------------------------
1. MODE SWITCHING: NORMAL MODE vs TRICKSHOT MODE ('M' or 'B' KEY)
--------------------------------------------------------------------------------
Easily switch between aiming modes anytime with the 'M' or 'B' key:

* 🎯 NORMAL MODE (All 6 Pocket Guidelines) : Renders direct aim lines to ALL 6 POCKETS simultaneously from the Target Ball, plus the Red Cue Ball aim line & ghost ball to your target pocket!
* 🎱 TRICKSHOT MODE (1-Cushion)           : Calculates 1-cushion bank shots (Red -> Green -> Blue).
* 🎱 TRICKSHOT MODE (2-Cushion)           : Calculates 2-cushion double bank shots (Red -> Green -> Blue -> Yellow).
* 🎱 TRICKSHOT MODE (3-Cushion)           : Calculates 3-cushion triple bank shots.

Pressing 'M' or 'B' displays a real-time HUD notification pop-up confirming your active mode!

--------------------------------------------------------------------------------
2. R-G-B COLOR-CODED SHOT SEQUENCES (NO CONFUSION!)
--------------------------------------------------------------------------------
Every shot path uses clear color coding so you instantly know where to aim:

* 🔴 RED LINE   (1st Direction) : CUE BALL AIM LINE (Where you MUST aim your cue stick!).
* 🟢 GREEN LINE (2nd Direction) : TARGET BALL PATH (Target Ball -> Rail 1 or Pocket).
* 🔵 BLUE LINE  (3rd Direction) : RAIL BOUNCE PATH (Rail 1 -> Rail 2 or Pocket).
* 🟡 YELLOW LINE(4th Direction) : MULTI-RAIL PATH (Rail 2 / Rail 3 -> Pocket).

--------------------------------------------------------------------------------
3. 2-BALL SYSTEM & CONTROLS (CUE BALL & TARGET BALL)
--------------------------------------------------------------------------------
The overlay renders TWO distinct ball circles for 100% pool physics precision:

* CUE BALL (White Circle with "CUE" label) : Represents your White Cue Ball.
* TARGET BALL (Theme Color Circle)         : Represents the Target / Object Ball.

CONTROLS:
* MIDDLE MOUSE CLICK (Scroll Wheel)        : Moves/Drags the White Cue Ball specifically.
* RIGHT-CLICK / RIGHT-CLICK DRAG           : Moves/Drags the Target Ball (or closest ball).
* SHIFT + RIGHT-CLICK                      : Moves/Drags the White Cue Ball anywhere on table.

--------------------------------------------------------------------------------
4. LIBREWOLF FOCUS GATING (SMART SAFETY FEATURE)
--------------------------------------------------------------------------------
All global mouse hooks, right-click ball dragging, and hotkeys are automatically
GATED to LibreWolf.exe:

* ACTIVE WHEN FOCUSED: Hotkeys and right-click dragging only operate when
  `LibreWolf.exe` (or the setup window) is the active focused window.
* INACTIVE WHEN UNFOCUSED: Moving to another program (e.g., Discord, Notepad, Browser tab)
  instantly disables all hotkeys and mouse hooks so they never interfere with your typing!

--------------------------------------------------------------------------------
5. DYNAMIC BALL CIRCLE RESIZING (+ / - KEYS)
--------------------------------------------------------------------------------
You can adjust the ball circle diameter live on screen to fit any resolution or pool ball size:

* '+' KEY / NUMPAD '+' : Increase reference ball diameter (+1px)
* '-' KEY / NUMPAD '-' : Decrease reference ball diameter (-1px)

Real-time HUD notification pop-ups display current ball size in pixels (e.g. `Ball Size: 23px`),
and the preferred size is saved automatically!

--------------------------------------------------------------------------------
6. OPACITY BUTTONS & SETTINGS
--------------------------------------------------------------------------------
You can adjust the overlay transparency dynamically by 5% steps anytime:

* '[' KEY (Left Bracket)  : Lower opacity by 5% (down to 10%)
* ']' KEY (Right Bracket) : Higher opacity by 5% (up to 100%)

* INTERACTIVE BUTTONS    : In Setup Mode (Click-Through OFF), click the `[ Lower ]` 
  and `[ Higher ]` buttons on the top gold banner to adjust opacity.

* 'O' KEY SHORTCUT        : Press 'O' anytime to cycle preset opacity levels (40% -> 60% -> 80% -> 100%).

--------------------------------------------------------------------------------
7. TARGET POCKET SELECTION & GHOST BALL COLLISION
--------------------------------------------------------------------------------
You can change the target pocket (hole) to ANY pocket you want at any time!
The app calculates the exact collision point (Ghost Ball) where the Cue Ball must hit the Target Ball:

* PRESS 'P' KEY : Cycle target pockets (Auto -> Top-Left -> Top-Middle -> Top-Right -> Bottom-Left -> Bottom-Middle -> Bottom-Right)
* NUMBER KEYS 1-6 : Directly select a target hole:
    - 1 : Top-Left Pocket
    - 2 : Top-Middle Pocket
    - 3 : Top-Right Pocket
    - 4 : Bottom-Left Pocket
    - 5 : Bottom-Middle Pocket
    - 6 : Bottom-Right Pocket
    - 0 : Auto-Detect (Selects the closest pocket automatically)

--------------------------------------------------------------------------------
8. SETUP MODE & WINDOW ALIGNMENT (FIRST LAUNCH)
--------------------------------------------------------------------------------
When you open the program in Setup Mode, a GOLD OUTLINE FRAME appears:

* LEFT-CLICK DRAG: Click and drag anywhere inside the gold frame to move the window over your 8 Ball Pool table.
* ARROW KEYS: Nudge the window position by 5 pixels.
* CTRL + ARROW KEYS: Resize the window width and height to fit your pool table:
    - Ctrl + Right Arrow : Increase Width
    - Ctrl + Left Arrow  : Decrease Width
    - Ctrl + Down Arrow  : Increase Height
    - Ctrl + Up Arrow    : Decrease Height
* PRESS SPACE or F1: Locks the window in place, hides the setup frame, and enables Click-Through Mode so you can play!

--------------------------------------------------------------------------------
9. LASER COLOR THEMES (T KEY)
--------------------------------------------------------------------------------
Press the 'T' key to cycle between 7 high-contrast neon theme colors:

1. Laser Cyan
2. Neon Green
3. Golden Glow
4. Deep Sky Blue
5. Electric Purple
6. Crimson Red
7. Classic White

--------------------------------------------------------------------------------
10. AUTOMATIC CONFIGURATION SAVING
--------------------------------------------------------------------------------
Your window position, size, opacity, color theme, trick shot mode, target pocket, reference ball size, Cue Ball position, and Target Ball position are automatically saved to `guideline_config.txt`. Every time you launch the app, it restores your exact setup automatically!

================================================================================
                       HOTKEY & CONTROL SUMMARY CHEAT-SHEET
================================================================================
| ACTION                          | CONTROL / HOTKEY                           |
|---------------------------------|--------------------------------------------|
| Target Process Focus            | Active ONLY when LibreWolf.exe is focused  |
| Mode Switch (Normal / Trickshot)| M Key or B Key                             |
| Normal Mode Display             | Direct guidelines to ALL 6 POCKETS at once |
| First Aim Line Color            | 🔴 RED (Cue Ball Aim Line)                  |
| Second Path Color               | 🟢 GREEN (Target Ball Path)                 |
| Third Path Color                | 🔵 BLUE (Rail Bounce Path)                  |
| Move Cue Ball (White Ball)      | Middle-Click or Shift + Right-Click Drag   |
| Move Target Ball (Color Ball)   | Right-Click or Right-Click Drag            |
| Increase / Decrease Ball Size   | '+' / '-' Keys or Numpad '+' / '-'         |
| Lower Opacity (-5%)             | [ Key (Left Bracket) or [ Lower ] Button   |
| Higher Opacity (+5%)            | ] Key (Right Bracket) or [ Higher ] Button |
| Cycle Opacity Presets           | O Key (40% -> 60% -> 80% -> 100%)          |
| Select Target Hole              | P key (Cycle) OR Number Keys 1-6 (0=Auto)  |
| Move Window Position            | Left-Click Drag (Setup Mode) or Arrow Keys |
| Resize Window Size              | Ctrl + Arrow Keys                          |
| Lock / Toggle Click-Through     | Space Bar or F1                            |
| Cycle Color Themes              | T Key                                      |
================================================================================
