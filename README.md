![Build Status](https://github.com/elissonsilva85/8-ball-pool-guideline-for-windows/actions/workflows/build.yml/badge.svg)

# 8 Ball Pool Guideline (for Windows)

### Technical Summary & Architecture for AI Prompts
If you are using AI tools (such as Claude, GPT-4, or Antigravity) to further expand or improve this project, see:
- [PROJECT_OVERVIEW.md](file:///c:/others/project/8ballpoolguideline/PROJECT_OVERVIEW.md) — Complete technical architecture, math equations, Win32 API hooks, and roadmap for future AI enhancements.
- [README_FEATURES.txt](file:///c:/others/project/8ballpoolguideline/README_FEATURES.txt) — Full user guide, R-G-B color sequence, 2-ball system, and hotkey cheat sheet.

### Download

[Download the latest release](https://github.com/elissonsilva85/8-ball-pool-guideline-for-windows/releases/)

### Objective

The objective is to help you aim cue ball and object ball accurately into pockets with full 2-ball collision physics and 1 to 3 cushion bank/kick trickshots.

- [8 Ball Pool Official Website](https://8ballpool.com/en/game)
- [8 Ball Pool Android App](https://play.google.com/store/apps/details?id=com.miniclip.eightballpool&hl=en)
- [8 Ball Pool iOS](https://apps.apple.com/br/app/8-ball-pool/id543186831)

### How to Use It

1. Open the program (or launch with `LibreWolf.exe`).
2. Adjust the window position over the pool table.
3. Middle-click to position the **Cue Ball** and Right-click to position the **Target Ball**.
4. Follow the 🔴 **RED** aim line to sink target balls!
5. Win the game! 👍

### Tech Stack

| Component | Technology |
|---|---|
| Language | C# 7.3 / 8.0 |
| Framework | .NET Framework 4.8 |
| UI | Windows Forms (WinForms) |
| Graphics | GDI+ (System.Drawing) Double-Buffered 60 FPS |
| Platform APIs | Win32 P/Invoke (`SetWindowsHookEx`, `SetWindowLong`, `GetForegroundWindow`) |
| Target Process | `LibreWolf.exe` (Focused Process Gating) |
| CI/CD | GitHub Actions |
| Platform | Windows (x86/x64) |

### Screenshots

![](http://elissonsilva.com.br/8ball/print1.jpg)

![](http://elissonsilva.com.br/8ball/print2.jpg)

![](http://elissonsilva.com.br/8ball/print3.jpg)
