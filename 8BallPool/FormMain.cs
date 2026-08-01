using System;
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

        private const int VK_RBUTTON = 0x02;
        private const int VK_MBUTTON = 0x04;
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_SPACE = 0x20;
        private const int VK_F1 = 0x70;
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;
        private const int VK_LEFT = 0x25;
        private const int VK_RIGHT = 0x27;
        private const int VK_T = 0x54;
        private const int VK_B = 0x42;

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
        private int cushionMode = 1; // 0=Off, 1=1-Cushion, 2=2-Cushion, 3=3-Cushion

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

        private bool wasRightDown;
        private bool wasSpaceDown;
        private bool wasF1Down;
        private bool wasUpDown;
        private bool wasDownDown;
        private bool wasLeftDown;
        private bool wasRightKey;
        private bool wasTDown;
        private bool wasBDown;

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

            updateTimer = new Timer();
            updateTimer.Interval = 16; // ~60 FPS update loop
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetClickThrough(true); // Enable click-through overlay by default
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveConfig();
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
                    "CushionMode=" + cushionMode
                };
                File.WriteAllLines(configPath, lines);
            }
            catch { }
        }

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(configPath)) return;
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
            // Middle Mouse Wheel Click/Hold OR Ctrl+Right-Click to move/drag ball without triggering in-game right clicks
            bool isMiddleDown = (GetAsyncKeyState(VK_MBUTTON) & 0x8000) != 0;
            bool isCtrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool isShiftDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
            bool isRightDown = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;

            bool isMoveTriggered = isMiddleDown || ((isCtrlDown || isShiftDown) && isRightDown);

            if (isMoveTriggered)
            {
                Point cursorPos = Cursor.Position;
                Point clientPos = this.PointToClient(cursorPos);
                if (this.ClientRectangle.Contains(clientPos))
                {
                    if (lastBallPosition != clientPos)
                    {
                        lastBallPosition = clientPos;
                        ClampBallPosition();
                        this.Invalidate();
                    }
                }
            }

            // Space / F1 to toggle Click-Through Mode
            bool isSpaceDown = (GetAsyncKeyState(VK_SPACE) & 0x8000) != 0;
            bool isF1Down = (GetAsyncKeyState(VK_F1) & 0x8000) != 0;
            if ((isSpaceDown && !wasSpaceDown) || (isF1Down && !wasF1Down))
            {
                SetClickThrough(!isClickThrough);
            }
            wasSpaceDown = isSpaceDown;
            wasF1Down = isF1Down;

            // Up / Down arrow keys for live opacity control (or Ctrl+Arrow keys for window resizing)
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
            else
            {
                // Opacity control
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

        private void FormMain_Paint(object sender, PaintEventArgs e)
        {
            Pocket.UpdatePoints(this.Width, this.Height);
            Color themeColor = ThemeColors[currentThemeIndex];
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            PocketPosition closestPocket = GetClosestPocket();

            DrawCorners(g, themeColor);
            DrawPockets(g, themeColor, closestPocket);
            DrawGuideLines(g, themeColor, closestPocket);
            
            if (cushionMode > 0)
            {
                DrawTrickShots(g, themeColor, closestPocket);
            }

            DrawGhostBall(g, themeColor, closestPocket);
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

        private void DrawTrickShots(Graphics g, Color themeColor, PocketPosition targetPos)
        {
            Point ball = lastBallPosition;
            Point target = Pocket.GetPoint(targetPos);

            int topY = 10;
            int botY = this.Height - 10;
            int leftX = 10;
            int rightX = this.Width - 10;

            // 1-Cushion Bounces
            if (cushionMode >= 1)
            {
                Draw1CushionBounce(g, ball, target, topY, true, Color.FromArgb(220, 255, 140, 0));
                Draw1CushionBounce(g, ball, target, botY, true, Color.FromArgb(220, 255, 215, 0));
            }

            // 2-Cushion Bounces
            if (cushionMode >= 2)
            {
                Draw2CushionBounce(g, ball, target, topY, rightX, Color.FromArgb(220, 50, 205, 50));
                Draw2CushionBounce(g, ball, target, topY, leftX, Color.FromArgb(220, 0, 255, 255));
                Draw2CushionBounce(g, ball, target, botY, rightX, Color.FromArgb(220, 238, 130, 238));
                Draw2CushionBounce(g, ball, target, botY, leftX, Color.FromArgb(220, 255, 105, 180));
            }

            // 3-Cushion Bounces
            if (cushionMode >= 3)
            {
                Draw3CushionBounce(g, ball, target, topY, rightX, botY, Color.FromArgb(220, 255, 0, 255));
                Draw3CushionBounce(g, ball, target, topY, leftX, botY, Color.FromArgb(220, 0, 191, 255));
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
                    using (Pen pen = new Pen(col, 2))
                    {
                        pen.DashStyle = DashStyle.Dash;
                        g.DrawLine(pen, B, C1);
                        g.DrawLine(pen, C1, P);
                    }
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

            using (Pen pen = new Pen(col, 2))
            {
                pen.DashStyle = DashStyle.Dash;
                g.DrawLine(pen, B, C1);
                g.DrawLine(pen, C1, C2);
                g.DrawLine(pen, C2, P);
            }
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

            using (Pen pen = new Pen(col, 2))
            {
                pen.DashStyle = DashStyle.Dash;
                g.DrawLine(pen, B, C1);
                g.DrawLine(pen, C1, C2);
                g.DrawLine(pen, C2, C3);
                g.DrawLine(pen, C3, P);
            }
        }

        private void FormMain_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }

        private void FormMain_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Right && (Control.ModifierKeys == Keys.Control || Control.ModifierKeys == Keys.Shift)))
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
                // Drag entire frameless overlay window when click-through is OFF
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
