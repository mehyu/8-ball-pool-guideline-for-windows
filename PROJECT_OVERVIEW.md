# 8 Ball Pool Guideline Overlay - Project Overview & Technical Architecture

## 1. Project Purpose & Overview
**8 Ball Pool Guideline Overlay** is a high-performance Windows desktop overlay application written in C# (.NET Framework / WinForms). It renders a transparent, click-through 60 FPS visual guideline grid over 8 Ball Pool games (such as browser-based pool in **LibreWolf.exe**). 

The goal of the project is to provide mathematically precise, real-time aiming assistance, ghost-ball collision projection, tangent line deflection, and 1-to-3 cushion trickshot trajectory calculations.

---

## 2. Architecture & File Structure

```
8ballpoolguideline/
├── 8BallPool/
│   ├── FormMain.cs               # Main application logic, rendering pipeline, hooks & Win32 P/Invoke
│   ├── FormMain.Designer.cs      # WinForms UI layout designer
│   ├── FormMain.resx             # Form resources & icons
│   ├── Pocket.cs                 # Pocket coordinate registry & aspect-ratio point updater
│   └── Program.cs                # Application entry point
├── 8BallPool.sln                 # Visual Studio Solution File
├── README_FEATURES.txt           # Comprehensive user feature guide & hotkey manual
├── PROJECT_OVERVIEW.md           # Technical architecture summary (This File)
└── guideline_config.txt          # Persistent runtime configuration file (auto-generated)
```

---

## 3. Key Technical Implementations

### A. Window Transparency & Click-Through Mode (`FormMain.cs`)
- Uses Win32 P/Invoke (`GetWindowLong`, `SetWindowLong`) with extended window styles:
  - `WS_EX_TRANSPARENT` (`0x20`): Allows mouse clicks to pass straight through the overlay window to the game underlying it.
  - `WS_EX_LAYERED` (`0x80000`): Enables alpha blending and hardware-accelerated transparency.
- Toggle between **Setup Mode** (interactive border, drag-to-resize, opacity buttons) and **Click-Through Mode** via `SPACE` or `F1`.

### B. Low-Level Win32 Mouse Hooking (`HookCallback`)
- Installs a low-level global mouse hook via `SetWindowsHookEx(WH_MOUSE_LL, ...)` to capture Right-Click press and drag events across the screen.
- Intercepts `WM_RBUTTONDOWN` and `WM_RBUTTONUP` when over the overlay canvas, blocking right-clicks from reaching the game window (preventing accidental cue stick fires).
- Left-clicks are strictly gated to setup window dragging and opacity buttons, keeping ball circle movement strictly on Right-Click or Middle-Click.

### C. Process Focus Gating (`LibreWolf.exe`)
- Queries Win32 `GetForegroundWindow()` and `GetWindowThreadProcessId()` on a 60 FPS timer loop.
- **Smart Safety Gating**: Intercepted hotkeys and right-click dragging are active **ONLY** when `LibreWolf.exe` (or the overlay window) is the foreground window. Moving to any other app immediately disables hooks to prevent input interference.

### D. 2-Ball System & Ghost Ball Collision Physics
- Tracks two independent ball positions:
  - `cueBallPosition` (White Cue Ball, labeled "CUE")
  - `targetBallPosition` (Color Object Ball)
- **Ghost Ball Math**: Calculates collision coordinate $G$ touching Target Ball $T$ along the vector $V_{TP}$ pointing to Target Pocket $P$:
  $$G = T - U_{TP} \times \text{referenceBallSize}$$
  where $U_{TP} = \frac{P - T}{|P - T|}$.
- **Tangent Line Deflection**: Renders 90-degree post-collision tangent deflection angle vector for the Cue Ball.

### E. Multi-Cushion Trickshot Engine (`Draw1CushionBounce`, `Draw2CushionBounce`, `Draw3CushionBounce`)
- Uses geometric cushion reflection ray casting:
  - Calculates virtual mirror images of target pockets across cushion rails.
  - Renders 1-Cushion, 2-Cushion, and 3-Cushion bounce trajectory lines with numbered target markers (①, ②, ③) and directional arrows.
- Collision offsets use dynamic ball radius (`referenceBallSize / 2`) for exact physical accuracy.

### F. R-G-B Color-Coded Shot Sequence
- Shot path segments use intuitive sequential color coding:
  1. 🔴 **RED**: 1st Direction — **Cue Ball Aim Line** (Where the player must aim).
  2. 🟢 **GREEN**: 2nd Direction — **Target Ball Path** (Target Ball to Rail 1 or Pocket).
  3. 🔵 **BLUE**: 3rd Direction — **First Rail Bounce** (Rail 1 to Rail 2 or Pocket).
  4. 🟡 **YELLOW / MAGENTA**: 4th/5th Directions — Multi-rail cushions.

### G. Mode Switching System (`DrawAllPocketGuideLines` vs `DrawTrickShots`)
- Switched via **`M`** / **`B`** key (cycle modes) or **`N`** key (direct Normal Mode shortcut) with real-time HUD visual notifications:
  - **`cushionMode == 0` (NORMAL MODE)**: Renders direct aim guidelines to **ALL 6 POCKETS** simultaneously on the table from the Target Ball, plus the 🔴 **RED** Cue Ball aim line and ghost ball for the active/closest pocket.
  - **`cushionMode == 1` (TRICKSHOT MODE 1-CUSHION)**: Focuses on single-target 1-Cushion bank shot reflection trajectories.
  - **`cushionMode == 2` (TRICKSHOT MODE 2-CUSHION)**: Focuses on 2-Cushion double rail bank shot trajectories.
  - **`cushionMode == 3` (TRICKSHOT MODE 3-CUSHION)**: Focuses on 3-Cushion triple rail bank shot trajectories.

---

## 4. Current Controls & Hotkeys Summary

| Feature | Shortcut / Action |
|---|---|
| **Process Focus** | Active ONLY when `LibreWolf.exe` is focused |
| **Cycle Modes** | `M` Key or `B` Key (Normal Mode -> 1-Cushion -> 2-Cushion -> 3-Cushion) |
| **Normal Mode Shortcut** | `N` Key (Direct shortcut to Normal Mode) |
| **Normal Mode Display** | Direct dashed guidelines to **ALL 6 POCKETS** at once |
| **Shot Color Sequence** | 🔴 RED (Aim Line) -> 🟢 GREEN (Target Path) -> 🔵 BLUE (Bounce) |
| **Move Cue Ball (White)** | Middle Mouse Click (Scroll Wheel) OR `Shift` + Right-Click |
| **Move Target Ball (Color)** | Right-Click / Right-Click Drag |
| **Resize Ball Circle** | `+` / `-` Keys OR Numpad `+` / `-` |
| **Adjust Opacity** | `[` / `]` Keys OR `O` Key (Preset cycle: 40% -> 60% -> 80% -> 100%) |
| **Select Target Hole** | `P` Key (Cycle) OR Number Keys `1-6` (`0` = Auto-closest) |
| **Color Themes** | `T` Key (Cycle 7 neon themes) |
| **Lock / Setup Mode** | `SPACE` or `F1` |
| **Move / Resize Overlay** | Setup Mode Left-Click Drag / `Ctrl` + Arrow Keys |

---

## 5. Potential Future Improvements & Roadmap for AI Development

If you are looking to enhance this project further using Claude or other advanced AI models, here are prime features to implement:

1. **Auto Ball Detection via Computer Vision (OpenCV / ONNX)**:
   - Capture game screen frame via Win32 `BitBlt` / Desktop Duplication API.
   - Run color segmentation or YOLOv8 ball detection model to auto-position `cueBallPosition` and `targetBallPosition` in real-time.
2. **Cue Spin & English Spin Deflection Curve**:
   - Model top-spin (follow), back-spin (draw), and side-spin (english) curved trajectories after impact.
3. **Power Bar & Force Calculation**:
   - Estimate shot speed and rail cushion deformation compression.
4. **Auto-Pocket Selector**:
   - Automatically calculate shot difficulty score for all 6 pockets and highlight the highest percentage shot.
5. **Modern WPF / DirectX / WebAssembly Overlay**:
   - Port rendering pipeline to Direct2D / DirectX 11 overlay or Electron/Tauri for ultra-smooth anti-aliased vector rendering.
