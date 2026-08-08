using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace _8BallPool
{
    public partial class FormMain : Form
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;

        private const int VK_RBUTTON = 0x02;
        private const int VK_MBUTTON = 0x04;
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // Alt key
        private const int VK_SPACE = 0x20;
        private const int VK_F1 = 0x70;
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;
        private const int VK_LEFT = 0x25;
        private const int VK_RIGHT = 0x27;
        private const int VK_T = 0x54;
        private const int VK_B = 0x42;
        private const int VK_P = 0x50; // P key to cycle target pocket
        private const int VK_O = 0x4F; // O key to cycle opacity
        private const int VK_OEM_4 = 0xDB; // '[' key for lower opacity (-5%)
        private const int VK_OEM_6 = 0xDD; // ']' key for higher opacity (+5%)
        private const int VK_0 = 0x30;
        private const int VK_1 = 0x31;
        private const int VK_OEM_PLUS = 0xBB; // '+' / '=' key to increase ball size
        private const int VK_OEM_MINUS = 0xBD; // '-' / '_' key to decrease ball size
        private const int VK_ADD = 0x6B; // Numpad '+'
        private const int VK_SUBTRACT = 0x6D; // Numpad '-'

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc _mouseProc;
        private IntPtr _hookID = IntPtr.Zero;
        private bool isRightHookDown = false;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private int referenceBallSize = 23;
        private const int BallCenterDotSize = 2;
        private const int CornerLineLength = 40;
        private const int CornerLineThickness = 4;
        private const int PocketIndicatorSize = 6;
        private const int GuideLineThickness = 2;
        private const int HighlightLineThickness = 4;

        private Point cueBallPosition;
        private Point targetBallPosition;
        private int activeDragBall = 0; // 0=None, 1=CueBall, 2=TargetBall
        private bool isClickThrough;
        private bool isFirstRun = false;
        private int cushionMode = 1; // 0=Off, 1=1-Cushion, 2=2-Cushion, 3=3-Cushion
        private int targetPocketSelection = -1; // -1=Auto (Closest), 0=TopLeft, 1=TopMiddle, 2=TopRight, 3=BottomLeft, 4=BottomMiddle, 5=BottomRight

        private static readonly Color[] ThemeColors = new Color[]
        {
            Color.Cyan,
            Color.Lime,
            Color.Gold,
            Color.DeepSkyBlue,
            Color.Magenta,
            Color.OrangeRed,
            Color.White
        };
        private static readonly string[] ThemeNames = new string[]
        {
            "Laser Cyan",
            "Neon Green",
            "Golden Glow",
            "Deep Sky Blue",
            "Electric Purple",
            "Crimson Red",
            "Classic White"
        };
        private int currentThemeIndex = 0;

        private bool wasSpaceDown;
        private bool wasF1Down;
        private bool wasUpDown;
        private bool wasDownDown;
        private bool wasLeftDown;
        private bool wasRightKey;
        private bool wasTDown;
        private bool wasBDown;
        private bool wasPDown;
        private bool wasODown;
        private bool wasLBracketDown;
        private bool wasRBracketDown;
        private bool wasPlusDown;
        private bool wasMinusDown;
        private bool[] wasNumDown = new bool[7];

        private DateTime hudMessageTime = DateTime.MinValue;
        private string hudMessageText = "";

        private bool IsTargetWindowFocused()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return false;
                if (hwnd == this.Handle) return true;

                uint processId;
                GetWindowThreadProcessId(hwnd, out processId);
                if (processId == 0) return false;

                using (Process proc = Process.GetProcessById((int)processId))
                {
                    string procName = proc.ProcessName;
                    if (procName.Equals("librewolf", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private void ShowHUD(string message)
        {
            hudMessageText = message;
            hudMessageTime = DateTime.Now.AddSeconds(2.5);
        }

        private Timer updateTimer;
        private string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "guideline_config.txt");

        public FormMain()
        {
            InitializeComponent();

            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | 
                           ControlStyles.UserPaint | 
                           ControlStyles.OptimizedDoubleBuffer | 
                           ControlStyles.ResizeRedraw, true);
            this.UpdateStyles();

            Pocket.Initialize();
            cueBallPosition = new Point(this.Width / 3, this.Height / 2);
            targetBallPosition = new Point(this.Width * 2 / 3, this.Height / 2);

            this.Opacity = 0.65D; // Default opacity

            LoadConfig();

            _mouseProc = HookCallback;
            _hookID = SetHook(_mouseProc);

            updateTimer = new Timer();
            updateTimer.Interval = 16; // ~60 FPS update loop
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if (!IsTargetWindowFocused())
                {
                    isRightHookDown = false;
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                int msg = wParam.ToInt32();
                if (msg == WM_RBUTTONDOWN || msg == WM_RBUTTONUP || msg == WM_MOUSEMOVE)
                {
                    MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    Point clientPos = this.PointToClient(new Point(hookStruct.pt.x, hookStruct.pt.y));

                    if (msg == WM_RBUTTONDOWN)
                    {
                        if (this.ClientRectangle.Contains(clientPos))
                        {
                            isRightHookDown = true;
                            bool isShiftDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
                            double distCue = Math.Sqrt(Math.Pow(clientPos.X - cueBallPosition.X, 2) + Math.Pow(clientPos.Y - cueBallPosition.Y, 2));
                            double distTarget = Math.Sqrt(Math.Pow(clientPos.X - targetBallPosition.X, 2) + Math.Pow(clientPos.Y - targetBallPosition.Y, 2));

                            if (isShiftDown || (distCue < distTarget && distCue < 40))
                            {
                                activeDragBall = 1;
                                cueBallPosition = clientPos;
                            }
                            else
                            {
                                activeDragBall = 2;
                                targetBallPosition = clientPos;
                            }

                            ClampBallPosition();
                            this.Invalidate();
                            return (IntPtr)1; // Block right-click press from reaching game
                        }
                    }
                    else if (msg == WM_RBUTTONUP)
                    {
                        if (isRightHookDown)
                        {
                            isRightHookDown = false;
                            activeDragBall = 0;
                            return (IntPtr)1; // Block right-click release from reaching game
                        }
                    }
                    else if (msg == WM_MOUSEMOVE)
                    {
                        if (isRightHookDown && this.ClientRectangle.Contains(clientPos))
                        {
                            if (activeDragBall == 1)
                            {
                                cueBallPosition = clientPos;
                            }
                            else
                            {
                                targetBallPosition = clientPos;
                            }
                            ClampBallPosition();
                            this.Invalidate();
                        }
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetClickThrough(!isFirstRun);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveConfig();
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            base.OnFormClosing(e);
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            SaveConfig();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            SaveConfig();
        }

        private void SaveConfig()
        {
            try
            {
                string[] lines = new string[]
                {
                    "X=" + this.Location.X,
                    "Y=" + this.Location.Y,
                    "Width=" + this.Width,
                    "Height=" + this.Height,
                    "Opacity=" + this.Opacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    "Theme=" + currentThemeIndex,
                    "CushionMode=" + cushionMode,
                    "TargetPocket=" + targetPocketSelection,
                    "BallSize=" + referenceBallSize,
                    "CueBallX=" + cueBallPosition.X,
                    "CueBallY=" + cueBallPosition.Y,
                    "TargetBallX=" + targetBallPosition.X,
                    "TargetBallY=" + targetBallPosition.Y
                };
                File.WriteAllLines(configPath, lines);
            }
            catch { }
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    isFirstRun = true;
                    return;
                }
                string[] lines = File.ReadAllLines(configPath);
                int posX = this.Left, posY = this.Top, w = this.Width, h = this.Height;
                bool hasPos = false;

                foreach (string line in lines)
                {
                    string[] parts = line.Split('=');
                    if (parts.Length != 2) continue;
                    string key = parts[0].Trim();
                    string val = parts[1].Trim();
                    int intVal;
                    double dblVal;

                    if (key == "X" && int.TryParse(val, out intVal)) { posX = intVal; hasPos = true; }
                    else if (key == "Y" && int.TryParse(val, out intVal)) { posY = intVal; hasPos = true; }
                    else if (key == "Width" && int.TryParse(val, out intVal)) { w = Math.Max(200, intVal); }
                    else if (key == "Height" && int.TryParse(val, out intVal)) { h = Math.Max(150, intVal); }
                    else if (key == "Opacity" && double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out dblVal)) { this.Opacity = Math.Max(0.10D, Math.Min(1.0D, dblVal)); }
                    else if (key == "Theme" && int.TryParse(val, out intVal)) { currentThemeIndex = Math.Max(0, Math.Min(ThemeColors.Length - 1, intVal)); }
                    else if (key == "CushionMode" && int.TryParse(val, out intVal)) { cushionMode = Math.Max(0, Math.Min(3, intVal)); }
                    else if (key == "TargetPocket" && int.TryParse(val, out intVal)) { targetPocketSelection = Math.Max(-1, Math.Min(5, intVal)); }
                    else if ((key == "BallSize" || key == "ReferenceBallSize") && int.TryParse(val, out intVal)) { referenceBallSize = Math.Max(8, Math.Min(60, intVal)); }
                    else if (key == "CueBallX" && int.TryParse(val, out intVal)) { cueBallPosition.X = intVal; }
                    else if (key == "CueBallY" && int.TryParse(val, out intVal)) { cueBallPosition.Y = intVal; }
                    else if (key == "TargetBallX" && int.TryParse(val, out intVal)) { targetBallPosition.X = intVal; }
                    else if (key == "TargetBallY" && int.TryParse(val, out intVal)) { targetBallPosition.Y = intVal; }
                }

                this.Size = new Size(w, h);
                if (hasPos)
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = new Point(posX, posY);
                }
            }
            catch { }
        }

        private void SetClickThrough(bool enable)
        {
            isClickThrough = enable;
            int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            if (enable)
            {
                exStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
            }
            else
            {
                exStyle &= ~WS_EX_TRANSPARENT;
            }
            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle);
            this.Invalidate();
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            if (!IsTargetWindowFocused())
            {
                wasSpaceDown = false;
                wasF1Down = false;
                wasUpDown = false;
                wasDownDown = false;
                wasLeftDown = false;
                wasRightKey = false;
                wasTDown = false;
                wasBDown = false;
                wasPDown = false;
                wasODown = false;
                wasLBracketDown = false;
                wasRBracketDown = false;
                wasPlusDown = false;
                wasMinusDown = false;
                for (int i = 0; i < wasNumDown.Length; i++) wasNumDown[i] = false;
                return;
            }

            bool isCtrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool isShiftDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
            bool isAltDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;

            // Space / F1 to toggle Click-Through Mode (Lock/Unlock Setup Mode)
            bool isSpaceDown = (GetAsyncKeyState(VK_SPACE) & 0x8000) != 0;
            bool isF1Down = (GetAsyncKeyState(VK_F1) & 0x8000) != 0;
            if ((isSpaceDown && !wasSpaceDown) || (isF1Down && !wasF1Down))
            {
                SetClickThrough(!isClickThrough);
            }
            wasSpaceDown = isSpaceDown;
            wasF1Down = isF1Down;

            // Arrow keys handling
            bool isUpDown = (GetAsyncKeyState(VK_UP) & 0x8000) != 0;
            bool isDownDown = (GetAsyncKeyState(VK_DOWN) & 0x8000) != 0;
            bool isLeftDown = (GetAsyncKeyState(VK_LEFT) & 0x8000) != 0;
            bool isRightKey = (GetAsyncKeyState(VK_RIGHT) & 0x8000) != 0;

            if (isCtrlDown)
            {
                // Resize window width/height with Ctrl + Arrow keys
                if (isRightKey && !wasRightKey) { this.Width += 10; SaveConfig(); }
                if (isLeftDown && !wasLeftDown) { this.Width = Math.Max(200, this.Width - 10); SaveConfig(); }
                if (isUpDown && !wasUpDown) { this.Height = Math.Max(150, this.Height - 10); SaveConfig(); }
                if (isDownDown && !wasDownDown) { this.Height += 10; SaveConfig(); }
            }
            else if (!isClickThrough || isAltDown)
            {
                // Move window position with Arrow keys when Click-Through is OFF or Alt is held
                if (isRightKey && !wasRightKey) { this.Left += 5; SaveConfig(); }
                if (isLeftDown && !wasLeftDown) { this.Left -= 5; SaveConfig(); }
                if (isUpDown && !wasUpDown) { this.Top -= 5; SaveConfig(); }
                if (isDownDown && !wasDownDown) { this.Top += 5; SaveConfig(); }

                // Opacity control with Shift+Up/Down when Click-Through OFF
                if (isShiftDown && isUpDown && !wasUpDown)
                {
                    if (this.Opacity < 1.0D) { this.Opacity = Math.Min(1.0D, Math.Round(this.Opacity + 0.05D, 2)); SaveConfig(); }
                }
                if (isShiftDown && isDownDown && !wasDownDown)
                {
                    if (this.Opacity > 0.10D) { this.Opacity = Math.Max(0.10D, Math.Round(this.Opacity - 0.05D, 2)); SaveConfig(); }
                }
            }
            else
            {
                // Opacity control with Up/Down when Click-Through is ON
                if (isUpDown && !wasUpDown)
                {
                    if (this.Opacity < 1.0D)
                    {
                        this.Opacity = Math.Min(1.0D, Math.Round(this.Opacity + 0.05D, 2));
                        SaveConfig();
                    }
                }
                if (isDownDown && !wasDownDown)
                {
                    if (this.Opacity > 0.10D)
                    {
                        this.Opacity = Math.Max(0.10D, Math.Round(this.Opacity - 0.05D, 2));
                        SaveConfig();
                    }
                }
            }

            wasUpDown = isUpDown;
            wasDownDown = isDownDown;
            wasLeftDown = isLeftDown;
            wasRightKey = isRightKey;

            // '+' / '-' or Numpad '+' / '-' to adjust Reference Ball Size
            bool isPlusDown = ((GetAsyncKeyState(VK_OEM_PLUS) & 0x8000) != 0) || ((GetAsyncKeyState(VK_ADD) & 0x8000) != 0);
            bool isMinusDown = ((GetAsyncKeyState(VK_OEM_MINUS) & 0x8000) != 0) || ((GetAsyncKeyState(VK_SUBTRACT) & 0x8000) != 0);

            if (isPlusDown && !wasPlusDown)
            {
                referenceBallSize = Math.Min(60, referenceBallSize + 1);
                ShowHUD("Ball Size: " + referenceBallSize + "px");
                SaveConfig();
                this.Invalidate();
            }
            wasPlusDown = isPlusDown;

            if (isMinusDown && !wasMinusDown)
            {
                referenceBallSize = Math.Max(8, referenceBallSize - 1);
                ShowHUD("Ball Size: " + referenceBallSize + "px");
                SaveConfig();
                this.Invalidate();
            }
            wasMinusDown = isMinusDown;

            // '[' Key: Lower Opacity by 5%
            bool isLBracketDown = (GetAsyncKeyState(VK_OEM_4) & 0x8000) != 0;
            if (isLBracketDown && !wasLBracketDown)
            {
                if (this.Opacity > 0.10D)
                {
                    this.Opacity = Math.Max(0.10D, Math.Round(this.Opacity - 0.05D, 2));
                    SaveConfig();
                    this.Invalidate();
                }
            }
            wasLBracketDown = isLBracketDown;

            // ']' Key: Higher Opacity by 5%
            bool isRBracketDown = (GetAsyncKeyState(VK_OEM_6) & 0x8000) != 0;
            if (isRBracketDown && !wasRBracketDown)
            {
                if (this.Opacity < 1.0D)
                {
                    this.Opacity = Math.Min(1.0D, Math.Round(this.Opacity + 0.05D, 2));
                    SaveConfig();
                    this.Invalidate();
                }
            }
            wasRBracketDown = isRBracketDown;

            // O Key: Cycle Opacity Presets (40% -> 60% -> 80% -> 100%)
            bool isODown = (GetAsyncKeyState(VK_O) & 0x8000) != 0;
            if (isODown && !wasODown)
            {
                double op = Math.Round(this.Opacity + 0.20D, 2);
                if (op > 1.0D) op = 0.40D;
                this.Opacity = op;
                ShowHUD("Opacity: " + (int)(this.Opacity * 100) + "%");
                SaveConfig();
                this.Invalidate();
            }
            wasODown = isODown;

            // T Key: Cycle Color Themes
            bool isTDown = (GetAsyncKeyState(VK_T) & 0x8000) != 0;
            if (isTDown && !wasTDown)
            {
                currentThemeIndex = (currentThemeIndex + 1) % ThemeColors.Length;
                ShowHUD("Theme: " + ThemeNames[currentThemeIndex]);
                SaveConfig();
                this.Invalidate();
            }
            wasTDown = isTDown;

            // B Key: Cycle Trick Shot / Cushion Bounce Modes (0=Off, 1=1-Cushion, 2=2-Cushion, 3=3-Cushion)
            bool isBDown = (GetAsyncKeyState(VK_B) & 0x8000) != 0;
            if (isBDown && !wasBDown)
            {
                cushionMode = (cushionMode + 1) % 4;
                string modeTxt = cushionMode == 0 ? "Off" : (cushionMode + "-Cushion");
                ShowHUD("Bounce Mode: " + modeTxt);
                SaveConfig();
                this.Invalidate();
            }
            wasBDown = isBDown;

            // P Key: Cycle Target Pocket (Auto -> TopLeft -> TopMiddle -> TopRight -> BottomLeft -> BottomMiddle -> BottomRight)
            bool isPDown = (GetAsyncKeyState(VK_P) & 0x8000) != 0;
            if (isPDown && !wasPDown)
            {
                targetPocketSelection++;
                if (targetPocketSelection > 5) targetPocketSelection = -1;
                SaveConfig();
                this.Invalidate();
            }
            wasPDown = isPDown;

            // Number Keys 0..6 for direct target pocket selection
            for (int i = 0; i <= 6; i++)
            {
                int vk = (i == 0) ? VK_0 : (VK_1 + i - 1);
                bool isNumDown = (GetAsyncKeyState(vk) & 0x8000) != 0;
                if (isNumDown && !wasNumDown[i])
                {
                    targetPocketSelection = (i == 0) ? -1 : (i - 1);
                    SaveConfig();
                    this.Invalidate();
                }
                wasNumDown[i] = isNumDown;
            }
        }

        private PocketPosition GetClosestPocket()
        {
            PocketPosition closest = PocketPosition.TopLeft;
            double minDistance = double.MaxValue;
            foreach (PocketPosition pos in Enum.GetValues(typeof(PocketPosition)))
            {
                Point p = Pocket.GetPoint(pos);
                double dist = Math.Sqrt(Math.Pow(p.X - targetBallPosition.X, 2) + Math.Pow(p.Y - targetBallPosition.Y, 2));
                if (dist < minDistance)
                {
                    minDistance = dist;
                    closest = pos;
                }
            }
            return closest;
        }

        private PocketPosition GetTargetPocket()
        {
            if (targetPocketSelection >= 0 && targetPocketSelection <= 5)
            {
                return (PocketPosition)targetPocketSelection;
            }
            return GetClosestPocket();
        }

        private void FormMain_Paint(object sender, PaintEventArgs e)
        {
            Pocket.UpdatePoints(this.Width, this.Height);
            Color themeColor = ThemeColors[currentThemeIndex];
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            PocketPosition activeTargetPocket = GetTargetPocket();

            // Draw Setup Mode Border & Instructions if Click-Through is OFF
            if (!isClickThrough)
            {
                using (Pen borderPen = new Pen(Color.Gold, 3))
                {
                    g.DrawRectangle(borderPen, 1, 1, this.Width - 2, this.Height - 2);
                }
                using (Brush handleBrush = new SolidBrush(Color.Gold))
                {
                    g.FillRectangle(handleBrush, 0, 0, 10, 10);
                    g.FillRectangle(handleBrush, this.Width - 10, 0, 10, 10);
                    g.FillRectangle(handleBrush, 0, this.Height - 10, 10, 10);
                    g.FillRectangle(handleBrush, this.Width - 10, this.Height - 10, 10, 10);
                }
                using (Brush bannerBg = new SolidBrush(Color.FromArgb(230, 20, 20, 20)))
                {
                    g.FillRectangle(bannerBg, 10, 10, Math.Max(100, this.Width - 20), 32);
                }
                using (Pen bannerBorder = new Pen(Color.Gold, 1))
                {
                    g.DrawRectangle(bannerBorder, 10, 10, Math.Max(100, this.Width - 20), 32);
                }
                using (Font font = new Font("Segoe UI", 9, FontStyle.Bold))
                using (Brush textBrush = new SolidBrush(Color.Gold))
                {
                    string txt = "SETUP MODE: Left-Click Drag to Move | Ctrl+Arrows to Resize | SPACE to Lock";
                    g.DrawString(txt, font, textBrush, 15, 17);
                }

                // Interactive Opacity Buttons UI on Setup Banner
                int opacityPercent = (int)Math.Round(this.Opacity * 100);
                int btnY = 14;
                int btnHeight = 24;
                
                Rectangle minusRect = new Rectangle(this.Width - 230, btnY, 60, btnHeight);
                Rectangle plusRect = new Rectangle(this.Width - 70, btnY, 60, btnHeight);

                using (Brush btnBg = new SolidBrush(Color.FromArgb(200, 50, 50, 50)))
                using (Pen btnPen = new Pen(Color.Gold, 1))
                using (Font btnFont = new Font("Segoe UI", 9, FontStyle.Bold))
                using (Brush btnTextBrush = new SolidBrush(Color.White))
                {
                    // Draw [ Lower (-5%) ] button
                    g.FillRectangle(btnBg, minusRect);
                    g.DrawRectangle(btnPen, minusRect);
                    g.DrawString("Lower [", btnFont, btnTextBrush, minusRect.X + 6, minusRect.Y + 3);

                    // Draw Opacity percentage text
                    string opTxt = opacityPercent + "%";
                    g.DrawString(opTxt, btnFont, btnTextBrush, minusRect.X + 68, minusRect.Y + 3);

                    // Draw [ Higher (+5%) ] button
                    g.FillRectangle(btnBg, plusRect);
                    g.DrawRectangle(btnPen, plusRect);
                    g.DrawString("Higher ]", btnFont, btnTextBrush, plusRect.X + 5, plusRect.Y + 3);
                }
            }

            DrawCorners(g, themeColor);
            DrawPockets(g, themeColor);
            DrawGuideLinesAndGhost(g, themeColor, activeTargetPocket);
            
            if (cushionMode > 0)
            {
                DrawTrickShots(g, themeColor, activeTargetPocket);
            }

            DrawBalls(g, themeColor);

            // Draw HUD overlay notification if active
            if (DateTime.Now < hudMessageTime && !string.IsNullOrEmpty(hudMessageText))
            {
                using (Font font = new Font("Segoe UI", 10, FontStyle.Bold))
                using (Brush bgBrush = new SolidBrush(Color.FromArgb(220, 20, 20, 20)))
                using (Pen borderPen = new Pen(themeColor, 1.5f))
                using (Brush textBrush = new SolidBrush(Color.White))
                {
                    SizeF size = g.MeasureString(hudMessageText, font);
                    int hudW = (int)size.Width + 24;
                    int hudH = (int)size.Height + 12;
                    int hudX = (this.Width - hudW) / 2;
                    int hudY = this.Height - hudH - 20;

                    Rectangle hudRect = new Rectangle(hudX, hudY, hudW, hudH);
                    g.FillRectangle(bgBrush, hudRect);
                    g.DrawRectangle(borderPen, hudRect);
                    g.DrawString(hudMessageText, font, textBrush, hudX + 12, hudY + 6);
                }
            }
        }

        private void DrawCorners(Graphics g, Color themeColor)
        {
            using (Pen pen = new Pen(Color.FromArgb(180, themeColor.R, themeColor.G, themeColor.B), CornerLineThickness))
            {
                DrawCorner(g, pen, PocketPosition.TopLeft, +1, +1);
                DrawCorner(g, pen, PocketPosition.BottomLeft, +1, -1);
                DrawCorner(g, pen, PocketPosition.TopRight, -1, +1);
                DrawCorner(g, pen, PocketPosition.BottomRight, -1, -1);
            }
        }

        private void DrawCorner(Graphics g, Pen pen, PocketPosition position, int dirX, int dirY)
        {
            Point reference = Pocket.GetPoint(position);
            Point coordHor = new Point(reference.X + dirX * CornerLineLength, reference.Y);
            Point coordVer = new Point(reference.X, reference.Y + dirY * CornerLineLength);
            g.DrawLines(pen, new[] { coordHor, reference, coordVer });
        }

        private void DrawPockets(Graphics g, Color themeColor)
        {
            foreach (PocketPosition position in Enum.GetValues(typeof(PocketPosition)))
            {
                Point pt = Pocket.GetPoint(position);
                int offsetX = GetPocketOffsetX(position);
                int offsetY = GetPocketOffsetY(position);

                int size = PocketIndicatorSize;
                using (Pen pen = new Pen(themeColor, 2))
                {
                    g.DrawEllipse(pen, pt.X + offsetX - size / 2, pt.Y + offsetY - size / 2, size, size);
                }
            }
        }

        private int GetPocketOffsetX(PocketPosition position)
        {
            switch (position)
            {
                case PocketPosition.TopMiddle:
                case PocketPosition.BottomMiddle:
                    return -2;
                case PocketPosition.TopRight:
                case PocketPosition.BottomRight:
                    return -4;
                default:
                    return 0;
            }
        }

        private int GetPocketOffsetY(PocketPosition position)
        {
            switch (position)
            {
                case PocketPosition.BottomLeft:
                case PocketPosition.BottomMiddle:
                case PocketPosition.BottomRight:
                    return -4;
                default:
                    return 0;
            }
        }

        private void DrawBalls(Graphics g, Color themeColor)
        {
            int halfBall = referenceBallSize / 2;
            int halfDot = BallCenterDotSize / 2;

            // 1. Draw Cue Ball (White Ball)
            Rectangle rectCue = new Rectangle(cueBallPosition.X - halfBall, cueBallPosition.Y - halfBall, referenceBallSize, referenceBallSize);
            Rectangle rectCueCenter = new Rectangle(cueBallPosition.X - halfDot, cueBallPosition.Y - halfDot, BallCenterDotSize, BallCenterDotSize);

            using (Pen cuePen = new Pen(Color.White, 2.5f))
            using (Brush cueFill = new SolidBrush(Color.White))
            {
                g.DrawEllipse(cuePen, rectCue);
                g.FillEllipse(cueFill, rectCueCenter);
            }

            // Draw 'C' label above Cue Ball
            using (Font font = new Font("Segoe UI", 7, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
            {
                g.DrawString("CUE", font, textBrush, cueBallPosition.X - 10, cueBallPosition.Y - halfBall - 13);
            }

            // 2. Draw Target Ball (Object Ball - Theme Color)
            Rectangle rectTarget = new Rectangle(targetBallPosition.X - halfBall, targetBallPosition.Y - halfBall, referenceBallSize, referenceBallSize);
            Rectangle rectTargetCenter = new Rectangle(targetBallPosition.X - halfDot, targetBallPosition.Y - halfDot, BallCenterDotSize, BallCenterDotSize);

            using (Pen targetPen = new Pen(themeColor, 2.5f))
            using (Pen insidePen = new Pen(Color.White, 2))
            {
                g.DrawEllipse(targetPen, rectTarget);
                g.DrawEllipse(insidePen, rectTargetCenter);
            }
        }

        private void DrawGuideLinesAndGhost(Graphics g, Color themeColor, PocketPosition targetPocket)
        {
            Point targetPocketPt = Pocket.GetPoint(targetPocket);
            double dx = targetPocketPt.X - targetBallPosition.X;
            double dy = targetPocketPt.Y - targetBallPosition.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist < 1) return;

            double ux = dx / dist;
            double uy = dy / dist;

            // Ghost ball collision center where Cue Ball collides with Target Ball
            Point ghostPos = new Point(
                (int)(targetBallPosition.X - ux * referenceBallSize),
                (int)(targetBallPosition.Y - uy * referenceBallSize));

            // 1. FIRST DIRECTION (Aim Line: Cue Ball -> Ghost Ball) -> RED
            DrawDirectionalLine(g, cueBallPosition, ghostPos, Color.Red, 3);

            // 2. SECOND DIRECTION (Target Ball Path -> Pocket) -> GREEN
            using (Pen targetPathPen = new Pen(Color.Lime, GuideLineThickness))
            {
                targetPathPen.DashStyle = DashStyle.Custom;
                targetPathPen.DashPattern = new float[] { 4, 4 };
                g.DrawLine(targetPathPen, targetBallPosition, targetPocketPt);
            }

            // 3. Ghost Ball Circle
            int halfBall = referenceBallSize / 2;
            Rectangle rectGhost = new Rectangle(ghostPos.X - halfBall, ghostPos.Y - halfBall, referenceBallSize, referenceBallSize);
            using (Pen ghostPen = new Pen(Color.FromArgb(200, themeColor), 2))
            {
                ghostPen.DashStyle = DashStyle.Dash;
                g.DrawEllipse(ghostPen, rectGhost);
            }

            // 4. Cue Ball Tangent Deflection Line (post-collision) -> BLUE
            double cdx = ghostPos.X - cueBallPosition.X;
            double cdy = ghostPos.Y - cueBallPosition.Y;
            double cdist = Math.Sqrt(cdx * cdx + cdy * cdy);
            if (cdist > 1)
            {
                double tx = -uy;
                double ty = ux;
                double dot = (cueBallPosition.X - ghostPos.X) * tx + (cueBallPosition.Y - ghostPos.Y) * ty;
                if (dot < 0) { tx = -tx; ty = -ty; }

                Point tangentEnd = new Point((int)(ghostPos.X + tx * 35), (int)(ghostPos.Y + ty * 35));
                using (Pen tanPen = new Pen(Color.Cyan, 2))
                {
                    tanPen.DashStyle = DashStyle.Dot;
                    g.DrawLine(tanPen, ghostPos, tangentEnd);
                }
            }
        }

        private void DrawDirectionalLine(Graphics g, Point p1, Point p2, Color col, int thickness)
        {
            using (Pen pen = new Pen(col, thickness))
            {
                g.DrawLine(pen, p1, p2);
            }

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist > 25)
            {
                double midX = p1.X + dx * 0.5;
                double midY = p1.Y + dy * 0.5;
                double angle = Math.Atan2(dy, dx);

                int arrowSize = 7;
                PointF arrow1 = new PointF(
                    (float)(midX - arrowSize * Math.Cos(angle - Math.PI / 6)),
                    (float)(midY - arrowSize * Math.Sin(angle - Math.PI / 6)));
                PointF arrow2 = new PointF(
                    (float)(midX - arrowSize * Math.Cos(angle + Math.PI / 6)),
                    (float)(midY - arrowSize * Math.Sin(angle + Math.PI / 6)));

                using (Pen arrowPen = new Pen(Color.White, 2))
                {
                    g.DrawLine(arrowPen, (float)midX, (float)midY, arrow1.X, arrow1.Y);
                    g.DrawLine(arrowPen, (float)midX, (float)midY, arrow2.X, arrow2.Y);
                }
            }
        }

        private void DrawBounceTarget(Graphics g, Point pt, string label, Color col)
        {
            int size = 18;
            Rectangle rect = new Rectangle(pt.X - size / 2, pt.Y - size / 2, size, size);

            using (Brush bgBrush = new SolidBrush(Color.FromArgb(230, 20, 20, 20)))
            {
                g.FillEllipse(bgBrush, rect);
            }
            using (Pen borderPen = new Pen(col, 2))
            {
                g.DrawEllipse(borderPen, rect);
            }
            using (Font font = new Font("Segoe UI", 8, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
            {
                SizeF textSize = g.MeasureString(label, font);
                g.DrawString(label, font, textBrush, pt.X - textSize.Width / 2 + 0.5f, pt.Y - textSize.Height / 2 + 0.5f);
            }
        }

        private void DrawTrickShots(Graphics g, Color themeColor, PocketPosition targetPos)
        {
            Point target = Pocket.GetPoint(targetPos);

            int ballRadius = referenceBallSize / 2;
            int topY = ballRadius;
            int botY = this.Height - ballRadius;
            int leftX = ballRadius;
            int rightX = this.Width - ballRadius;

            // Bank Shots (Target Ball bounces off cushion to target pocket)
            if (cushionMode == 1)
            {
                Draw1CushionBounce(g, cueBallPosition, targetBallPosition, target, topY, true, Color.Gold);
                Draw1CushionBounce(g, cueBallPosition, targetBallPosition, target, botY, true, Color.Orange);
            }
            else if (cushionMode == 2)
            {
                Draw2CushionBounce(g, cueBallPosition, targetBallPosition, target, topY, rightX, Color.Lime);
                Draw2CushionBounce(g, cueBallPosition, targetBallPosition, target, topY, leftX, Color.Cyan);
            }
            else if (cushionMode == 3)
            {
                Draw3CushionBounce(g, cueBallPosition, targetBallPosition, target, topY, rightX, botY, Color.Magenta);
                Draw3CushionBounce(g, cueBallPosition, targetBallPosition, target, topY, leftX, botY, Color.DeepSkyBlue);
            }
        }

        private void Draw1CushionBounce(Graphics g, Point C, Point T, Point P, int cushionY, bool isHorizontal, Color col)
        {
            if (isHorizontal)
            {
                double mirPy = 2.0 * cushionY - P.Y;
                double dy = mirPy - T.Y;
                if (Math.Abs(dy) < 0.001) return;
                double bounceX = T.X + (P.X - T.X) * (cushionY - T.Y) / dy;
                if (bounceX >= 5 && bounceX <= this.Width - 5)
                {
                    Point C1 = new Point((int)bounceX, cushionY);
                    
                    // Ghost Ball G at Target Ball along vector to Rail Bounce C1
                    double bdx = C1.X - T.X;
                    double bdy = C1.Y - T.Y;
                    double bdist = Math.Sqrt(bdx * bdx + bdy * bdy);
                    if (bdist > 1)
                    {
                        Point ghostG = new Point(
                            (int)(T.X - (bdx / bdist) * referenceBallSize),
                            (int)(T.Y - (bdy / bdist) * referenceBallSize));
                        
                        // 1. FIRST AIM DIRECTION (Cue Ball -> Ghost Ball) -> RED
                        DrawDirectionalLine(g, C, ghostG, Color.Red, 3);
                    }

                    // 2. SECOND DIRECTION (Target Ball -> Rail 1) -> GREEN
                    DrawDirectionalLine(g, T, C1, Color.Lime, 3);
                    // 3. THIRD DIRECTION (Rail 1 -> Target Hole) -> BLUE
                    DrawDirectionalLine(g, C1, P, Color.Cyan, 3);

                    // Bounce Marker Target
                    DrawBounceTarget(g, C1, "1", Color.Lime);
                }
            }
        }

        private void Draw2CushionBounce(Graphics g, Point C, Point T, Point P, int cushion1Y, int cushion2X, Color col)
        {
            double mirP1x = 2.0 * cushion2X - P.X;
            Point P1 = new Point((int)mirP1x, P.Y);
            double mirP2y = 2.0 * cushion1Y - P1.Y;
            Point P2 = new Point(P1.X, (int)mirP2y);

            double dy = P2.Y - T.Y;
            if (Math.Abs(dy) < 0.001) return;
            double c1X = T.X + (P2.X - T.X) * (cushion1Y - T.Y) / dy;
            if (c1X < 5 || c1X > this.Width - 5) return;
            Point C1 = new Point((int)c1X, cushion1Y);

            double dx = P1.X - C1.X;
            if (Math.Abs(dx) < 0.001) return;
            double c2Y = C1.Y + (P1.Y - C1.Y) * (cushion2X - C1.X) / dx;
            if (c2Y < 5 || c2Y > this.Height - 5) return;
            Point C2 = new Point(cushion2X, (int)c2Y);

            // Ghost Ball G at Target Ball along vector to Rail 1
            double bdx = C1.X - T.X;
            double bdy = C1.Y - T.Y;
            double bdist = Math.Sqrt(bdx * bdx + bdy * bdy);
            if (bdist > 1)
            {
                Point ghostG = new Point(
                    (int)(T.X - (bdx / bdist) * referenceBallSize),
                    (int)(T.Y - (bdy / bdist) * referenceBallSize));

                // 1. FIRST AIM DIRECTION (Cue Ball -> Ghost Ball) -> RED
                DrawDirectionalLine(g, C, ghostG, Color.Red, 3);
            }

            // 2. SECOND DIRECTION (Target Ball -> Rail 1) -> GREEN
            DrawDirectionalLine(g, T, C1, Color.Lime, 3);
            // 3. THIRD DIRECTION (Rail 1 -> Rail 2) -> BLUE
            DrawDirectionalLine(g, C1, C2, Color.Cyan, 3);
            // 4. FOURTH DIRECTION (Rail 2 -> Target Hole) -> YELLOW
            DrawDirectionalLine(g, C2, P, Color.Gold, 3);

            // Rail Bounce Markers
            DrawBounceTarget(g, C1, "1", Color.Lime);
            DrawBounceTarget(g, C2, "2", Color.Cyan);
        }

        private void Draw3CushionBounce(Graphics g, Point C, Point T, Point P, int cushion1Y, int cushion2X, int cushion3Y, Color col)
        {
            Point P1 = new Point(P.X, (int)(2.0 * cushion3Y - P.Y));
            Point P2 = new Point((int)(2.0 * cushion2X - P1.X), P1.Y);
            Point P3 = new Point(P2.X, (int)(2.0 * cushion1Y - P2.Y));

            double dy1 = P3.Y - T.Y;
            if (Math.Abs(dy1) < 0.001) return;
            double c1X = T.X + (P3.X - T.X) * (cushion1Y - T.Y) / dy1;
            if (c1X < 5 || c1X > this.Width - 5) return;
            Point C1 = new Point((int)c1X, cushion1Y);

            double dx2 = P2.X - C1.X;
            if (Math.Abs(dx2) < 0.001) return;
            double c2Y = C1.Y + (P2.Y - C1.Y) * (cushion2X - C1.X) / dx2;
            if (c2Y < 5 || c2Y > this.Height - 5) return;
            Point C2 = new Point(cushion2X, (int)c2Y);

            double dy3 = P1.Y - C2.Y;
            if (Math.Abs(dy3) < 0.001) return;
            double c3X = C2.X + (P1.X - C2.X) * (cushion3Y - C2.Y) / dy3;
            if (c3X < 5 || c3X > this.Width - 5) return;
            Point C3 = new Point((int)c3X, cushion3Y);

            // Ghost Ball G at Target Ball along vector to Rail 1
            double bdx = C1.X - T.X;
            double bdy = C1.Y - T.Y;
            double bdist = Math.Sqrt(bdx * bdx + bdy * bdy);
            if (bdist > 1)
            {
                Point ghostG = new Point(
                    (int)(T.X - (bdx / bdist) * referenceBallSize),
                    (int)(T.Y - (bdy / bdist) * referenceBallSize));

                // 1. FIRST AIM DIRECTION (Cue Ball -> Ghost Ball) -> RED
                DrawDirectionalLine(g, C, ghostG, Color.Red, 3);
            }

            // 2. SECOND DIRECTION (Target Ball -> Rail 1) -> GREEN
            DrawDirectionalLine(g, T, C1, Color.Lime, 3);
            // 3. THIRD DIRECTION (Rail 1 -> Rail 2) -> BLUE
            DrawDirectionalLine(g, C1, C2, Color.Cyan, 3);
            // 4. FOURTH DIRECTION (Rail 2 -> Rail 3) -> MAGENTA
            DrawDirectionalLine(g, C2, C3, Color.Magenta, 3);
            // 5. FIFTH DIRECTION (Rail 3 -> Target Hole) -> YELLOW
            DrawDirectionalLine(g, C3, P, Color.Gold, 3);

            // Rail Bounce Markers
            DrawBounceTarget(g, C1, "1", Color.Lime);
            DrawBounceTarget(g, C2, "2", Color.Cyan);
            DrawBounceTarget(g, C3, "3", Color.Magenta);
        }

        private void FormMain_MouseUp(object sender, MouseEventArgs e)
        {
            activeDragBall = 0;
        }

        private void FormMain_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                cueBallPosition = new Point(e.X, e.Y);
                ClampBallPosition();
                this.Invalidate();
                return;
            }
            if (e.Button == MouseButtons.Right)
            {
                double distCue = Math.Sqrt(Math.Pow(e.X - cueBallPosition.X, 2) + Math.Pow(e.Y - cueBallPosition.Y, 2));
                if (distCue < 30)
                {
                    cueBallPosition = new Point(e.X, e.Y);
                }
                else
                {
                    targetBallPosition = new Point(e.X, e.Y);
                }
                ClampBallPosition();
                this.Invalidate();
                return;
            }

            if (!isClickThrough && e.Button == MouseButtons.Left)
            {
                // Check if user clicked [ Lower ] or [ Higher ] Opacity buttons on Setup Banner
                Rectangle minusRect = new Rectangle(this.Width - 230, 14, 60, 24);
                Rectangle plusRect = new Rectangle(this.Width - 70, 14, 60, 24);

                if (minusRect.Contains(e.X, e.Y))
                {
                    this.Opacity = Math.Max(0.10D, Math.Round(this.Opacity - 0.05D, 2));
                    SaveConfig();
                    this.Invalidate();
                    return;
                }
                if (plusRect.Contains(e.X, e.Y))
                {
                    this.Opacity = Math.Min(1.0D, Math.Round(this.Opacity + 0.05D, 2));
                    SaveConfig();
                    this.Invalidate();
                    return;
                }

                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void FormMain_MouseMove(object sender, MouseEventArgs e)
        {
            if (isClickThrough) return;
            Cursor.Current = Cursors.Default;
        }

        private void FormMain_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.F1)
            {
                SetClickThrough(!isClickThrough);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ClampBallPosition();
            this.Invalidate();
        }

        private void ClampBallPosition()
        {
            int cx = Math.Max(0, Math.Min(cueBallPosition.X, this.ClientSize.Width));
            int cy = Math.Max(0, Math.Min(cueBallPosition.Y, this.ClientSize.Height));
            cueBallPosition = new Point(cx, cy);

            int tx = Math.Max(0, Math.Min(targetBallPosition.X, this.ClientSize.Width));
            int ty = Math.Max(0, Math.Min(targetBallPosition.Y, this.ClientSize.Height));
            targetBallPosition = new Point(tx, ty);
        }
    }
}
