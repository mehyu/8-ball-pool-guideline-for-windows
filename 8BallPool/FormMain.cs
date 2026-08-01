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
        private const int VK_0 = 0x30;
        private const int VK_1 = 0x31;

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

        private const int ReferenceBallSize = 25;
        private const int BallCenterDotSize = 2;
        private const int BallHitAreaRadius = 15;
        private const int CornerLineLength = 40;
        private const int CornerLineThickness = 4;
        private const int PocketIndicatorSize = 6;
        private const int GuideLineThickness = 2;
        private const int HighlightLineThickness = 4;

        private Point lastBallPosition;
        private bool isDragging;
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
        private bool[] wasNumDown = new bool[7];

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
            lastBallPosition = new Point(this.Width / 2, this.Height / 2);
            isDragging = false;

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
                            lastBallPosition = clientPos;
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
                            return (IntPtr)1; // Block right-click release from reaching game
                        }
                    }
                    else if (msg == WM_MOUSEMOVE)
                    {
                        if (isRightHookDown && this.ClientRectangle.Contains(clientPos))
                        {
                            if (lastBallPosition != clientPos)
                            {
                                lastBallPosition = clientPos;
                                ClampBallPosition();
                                this.Invalidate();
                            }
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
                    "TargetPocket=" + targetPocketSelection
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

            // T Key: Cycle Color Themes
            bool isTDown = (GetAsyncKeyState(VK_T) & 0x8000) != 0;
            if (isTDown && !wasTDown)
            {
                currentThemeIndex = (currentThemeIndex + 1) % ThemeColors.Length;
                SaveConfig();
                this.Invalidate();
            }
            wasTDown = isTDown;

            // B Key: Cycle Trick Shot / Cushion Bounce Modes (0=Off, 1=1-Cushion, 2=2-Cushion, 3=3-Cushion)
            bool isBDown = (GetAsyncKeyState(VK_B) & 0x8000) != 0;
            if (isBDown && !wasBDown)
            {
                cushionMode = (cushionMode + 1) % 4;
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
                double dist = Math.Sqrt(Math.Pow(p.X - lastBallPosition.X, 2) + Math.Pow(p.Y - lastBallPosition.Y, 2));
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
                    g.FillRectangle(bannerBg, 10, 10, Math.Max(100, this.Width - 20), 28);
                }
                using (Pen bannerBorder = new Pen(Color.Gold, 1))
                {
                    g.DrawRectangle(bannerBorder, 10, 10, Math.Max(100, this.Width - 20), 28);
                }
                using (Font font = new Font("Segoe UI", 9, FontStyle.Bold))
                using (Brush textBrush = new SolidBrush(Color.Gold))
                {
                    string txt = "SETUP MODE: Left-Click Drag to Move | Ctrl+Arrows to Resize | SPACE to Lock";
                    g.DrawString(txt, font, textBrush, 15, 15);
                }
            }

            DrawCorners(g, themeColor);
            DrawPockets(g, themeColor, activeTargetPocket);
            DrawGuideLines(g, themeColor, activeTargetPocket);
            
            if (cushionMode > 0)
            {
                DrawTrickShots(g, themeColor, activeTargetPocket);
            }

            DrawGhostBall(g, themeColor, activeTargetPocket);
            DrawBall(g, themeColor);
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

        private void DrawPockets(Graphics g, Color themeColor, PocketPosition closestPocket)
        {
            foreach (PocketPosition position in Enum.GetValues(typeof(PocketPosition)))
            {
                Point pt = Pocket.GetPoint(position);
                int offsetX = GetPocketOffsetX(position);
                int offsetY = GetPocketOffsetY(position);

                bool isTarget = (position == closestPocket);
                int size = isTarget ? PocketIndicatorSize * 2 : PocketIndicatorSize;
                Color col = isTarget ? themeColor : Color.FromArgb(160, 255, 0, 0);

                using (Pen pen = new Pen(col, isTarget ? 3 : 2))
                {
                    g.DrawEllipse(pen, pt.X + offsetX - size / 2, pt.Y + offsetY - size / 2, size, size);
                }

                if (isTarget)
                {
                    using (Brush brush = new SolidBrush(Color.FromArgb(100, themeColor)))
                    {
                        g.FillEllipse(brush, pt.X + offsetX - size / 2, pt.Y + offsetY - size / 2, size, size);
                    }
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

        private void DrawBall(Graphics g, Color themeColor)
        {
            Point pt = lastBallPosition;
            int halfBall = ReferenceBallSize / 2;
            int halfDot = BallCenterDotSize / 2;

            Rectangle rectOutside = new Rectangle(pt.X - halfBall, pt.Y - halfBall, ReferenceBallSize, ReferenceBallSize);
            Rectangle rectInside = new Rectangle(pt.X - halfDot, pt.Y - halfDot, BallCenterDotSize, BallCenterDotSize);

            using (Pen penOutside = new Pen(themeColor, 2))
            using (Pen penInside = new Pen(Color.White, 2))
            {
                g.DrawEllipse(penOutside, rectOutside);
                g.DrawEllipse(penInside, rectInside);
            }
        }

        private void DrawGuideLines(Graphics g, Color themeColor, PocketPosition closestPocket)
        {
            foreach (PocketPosition position in Enum.GetValues(typeof(PocketPosition)))
            {
                bool isTarget = (position == closestPocket);
                Color lineCol = isTarget ? themeColor : Color.FromArgb(100, 180, 180, 180);
                int thickness = isTarget ? HighlightLineThickness : GuideLineThickness;

                using (Pen pen = new Pen(lineCol, thickness))
                {
                    if (isTarget)
                    {
                        pen.DashStyle = DashStyle.Solid;
                    }
                    else
                    {
                        pen.DashStyle = DashStyle.Custom;
                        pen.DashPattern = new float[] { 4, 4 };
                    }
                    g.DrawLine(pen, lastBallPosition, Pocket.GetPoint(position));
                }
            }
        }

        private void DrawGhostBall(Graphics g, Color themeColor, PocketPosition closestPocket)
        {
            Point targetPocketPt = Pocket.GetPoint(closestPocket);
            double dx = targetPocketPt.X - lastBallPosition.X;
            double dy = targetPocketPt.Y - lastBallPosition.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist < 1) return;

            double ghostDist = Math.Min(dist * 0.5, 60.0);
            int ghostX = (int)(lastBallPosition.X + (dx / dist) * ghostDist);
            int ghostY = (int)(lastBallPosition.Y + (dy / dist) * ghostDist);

            int halfBall = ReferenceBallSize / 2;
            Rectangle rectGhost = new Rectangle(ghostX - halfBall, ghostY - halfBall, ReferenceBallSize, ReferenceBallSize);

            using (Pen ghostPen = new Pen(Color.FromArgb(180, themeColor), 2))
            {
                ghostPen.DashStyle = DashStyle.Dash;
                g.DrawEllipse(ghostPen, rectGhost);
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
            Point ball = lastBallPosition;
            Point target = Pocket.GetPoint(targetPos);

            int topY = 10;
            int botY = this.Height - 10;
            int leftX = 10;
            int rightX = this.Width - 10;

            // Mode 1: 1-Cushion Trick Shots
            if (cushionMode == 1)
            {
                Draw1CushionBounce(g, ball, target, topY, true, Color.Gold);
                Draw1CushionBounce(g, ball, target, botY, true, Color.Orange);
            }
            // Mode 2: 2-Cushion Trick Shots
            else if (cushionMode == 2)
            {
                Draw2CushionBounce(g, ball, target, topY, rightX, Color.Lime);
                Draw2CushionBounce(g, ball, target, topY, leftX, Color.Cyan);
            }
            // Mode 3: 3-Cushion Trick Shots
            else if (cushionMode == 3)
            {
                Draw3CushionBounce(g, ball, target, topY, rightX, botY, Color.Magenta);
                Draw3CushionBounce(g, ball, target, topY, leftX, botY, Color.DeepSkyBlue);
            }
        }

        private void Draw1CushionBounce(Graphics g, Point B, Point P, int cushionY, bool isHorizontal, Color col)
        {
            if (isHorizontal)
            {
                double mirPy = 2.0 * cushionY - P.Y;
                double dy = mirPy - B.Y;
                if (Math.Abs(dy) < 0.001) return;
                double bounceX = B.X + (P.X - B.X) * (cushionY - B.Y) / dy;
                if (bounceX >= 5 && bounceX <= this.Width - 5)
                {
                    Point C1 = new Point((int)bounceX, cushionY);
                    
                    // Path 1: Cue Ball -> Rail 1
                    DrawDirectionalLine(g, B, C1, Color.Yellow, 3);
                    // Path 2: Rail 1 -> Target Hole
                    DrawDirectionalLine(g, C1, P, col, 3);

                    // Bounce Marker Target
                    DrawBounceTarget(g, C1, "1", col);
                }
            }
        }

        private void Draw2CushionBounce(Graphics g, Point B, Point P, int cushion1Y, int cushion2X, Color col)
        {
            double mirP1x = 2.0 * cushion2X - P.X;
            Point P1 = new Point((int)mirP1x, P.Y);
            double mirP2y = 2.0 * cushion1Y - P1.Y;
            Point P2 = new Point(P1.X, (int)mirP2y);

            double dy = P2.Y - B.Y;
            if (Math.Abs(dy) < 0.001) return;
            double c1X = B.X + (P2.X - B.X) * (cushion1Y - B.Y) / dy;
            if (c1X < 5 || c1X > this.Width - 5) return;
            Point C1 = new Point((int)c1X, cushion1Y);

            double dx = P1.X - C1.X;
            if (Math.Abs(dx) < 0.001) return;
            double c2Y = C1.Y + (P1.Y - C1.Y) * (cushion2X - C1.X) / dx;
            if (c2Y < 5 || c2Y > this.Height - 5) return;
            Point C2 = new Point(cushion2X, (int)c2Y);

            // Path 1: Cue Ball -> Rail 1
            DrawDirectionalLine(g, B, C1, Color.Yellow, 3);
            // Path 2: Rail 1 -> Rail 2
            DrawDirectionalLine(g, C1, C2, col, 3);
            // Path 3: Rail 2 -> Target Hole
            DrawDirectionalLine(g, C2, P, Color.Cyan, 3);

            // Rail Bounce Markers
            DrawBounceTarget(g, C1, "1", col);
            DrawBounceTarget(g, C2, "2", col);
        }

        private void Draw3CushionBounce(Graphics g, Point B, Point P, int cushion1Y, int cushion2X, int cushion3Y, Color col)
        {
            Point P1 = new Point(P.X, (int)(2.0 * cushion3Y - P.Y));
            Point P2 = new Point((int)(2.0 * cushion2X - P1.X), P1.Y);
            Point P3 = new Point(P2.X, (int)(2.0 * cushion1Y - P2.Y));

            double dy1 = P3.Y - B.Y;
            if (Math.Abs(dy1) < 0.001) return;
            double c1X = B.X + (P3.X - B.X) * (cushion1Y - B.Y) / dy1;
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

            // Path 1: Cue Ball -> Rail 1
            DrawDirectionalLine(g, B, C1, Color.Yellow, 3);
            // Path 2: Rail 1 -> Rail 2
            DrawDirectionalLine(g, C1, C2, col, 3);
            // Path 3: Rail 2 -> Rail 3
            DrawDirectionalLine(g, C2, C3, Color.Magenta, 3);
            // Path 4: Rail 3 -> Target Hole
            DrawDirectionalLine(g, C3, P, Color.Lime, 3);

            // Rail Bounce Markers
            DrawBounceTarget(g, C1, "1", col);
            DrawBounceTarget(g, C2, "2", col);
            DrawBounceTarget(g, C3, "3", col);
        }

        private void FormMain_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }

        private void FormMain_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle)
            {
                lastBallPosition = new Point(e.X, e.Y);
                ClampBallPosition();
                this.Invalidate();
                return;
            }

            Rectangle hitArea = new Rectangle(
                lastBallPosition.X - BallHitAreaRadius,
                lastBallPosition.Y - BallHitAreaRadius,
                BallHitAreaRadius * 2,
                BallHitAreaRadius * 2);

            if (hitArea.Contains(e.X, e.Y))
            {
                isDragging = true;
                lastBallPosition = new Point(e.X, e.Y);
                this.Invalidate();
            }
            else if (!isClickThrough && e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void FormMain_MouseMove(object sender, MouseEventArgs e)
        {
            if (isClickThrough) return;

            Rectangle hitArea = new Rectangle(
                lastBallPosition.X - BallHitAreaRadius,
                lastBallPosition.Y - BallHitAreaRadius,
                BallHitAreaRadius * 2,
                BallHitAreaRadius * 2);

            if (hitArea.Contains(e.X, e.Y) || isDragging)
            {
                Cursor.Current = Cursors.Hand;
                if (isDragging)
                {
                    lastBallPosition = new Point(e.X, e.Y);
                    this.Invalidate();
                }
            }
            else
            {
                Cursor.Current = Cursors.Default;
            }
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
            int x = Math.Max(0, Math.Min(lastBallPosition.X, this.ClientSize.Width));
            int y = Math.Max(0, Math.Min(lastBallPosition.Y, this.ClientSize.Height));
            lastBallPosition = new Point(x, y);
        }
    }
}
