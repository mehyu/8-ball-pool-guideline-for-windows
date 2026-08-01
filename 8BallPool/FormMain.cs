using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace _8BallPool
{
    public partial class FormMain : Form
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        private const int VK_RBUTTON = 0x02;
        private const int VK_SPACE = 0x20;
        private const int VK_F1 = 0x70;
        private const int VK_UP = 0x26;
        private const int VK_DOWN = 0x28;
        private const int VK_T = 0x54;
        private const int VK_B = 0x42;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

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
        private bool isBankShotEnabled;

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
        private bool wasTDown;
        private bool wasBDown;

        private Timer updateTimer;

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
            isBankShotEnabled = false;

            this.Opacity = 0.65D; // 15% higher opacity than default 0.50D

            updateTimer = new Timer();
            updateTimer.Interval = 16; // ~60 FPS update loop
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetClickThrough(true); // Enable click-through by default so user mouse clicks pass straight to game
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
            // Right-Click hold or click: continuously update ball position while right mouse button is held
            bool isRightDown = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
            if (isRightDown)
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
            wasRightDown = isRightDown;

            // Space / F1 to toggle Click-Through Mode
            bool isSpaceDown = (GetAsyncKeyState(VK_SPACE) & 0x8000) != 0;
            bool isF1Down = (GetAsyncKeyState(VK_F1) & 0x8000) != 0;
            if ((isSpaceDown && !wasSpaceDown) || (isF1Down && !wasF1Down))
            {
                SetClickThrough(!isClickThrough);
            }
            wasSpaceDown = isSpaceDown;
            wasF1Down = isF1Down;

            // Up / Down arrow keys for live opacity control
            bool isUpDown = (GetAsyncKeyState(VK_UP) & 0x8000) != 0;
            if (isUpDown && !wasUpDown)
            {
                if (this.Opacity < 1.0D)
                    this.Opacity = Math.Min(1.0D, Math.Round(this.Opacity + 0.05D, 2));
            }
            wasUpDown = isUpDown;

            bool isDownDown = (GetAsyncKeyState(VK_DOWN) & 0x8000) != 0;
            if (isDownDown && !wasDownDown)
            {
                if (this.Opacity > 0.10D)
                    this.Opacity = Math.Max(0.10D, Math.Round(this.Opacity - 0.05D, 2));
            }
            wasDownDown = isDownDown;

            // T Key: Cycle Color Themes
            bool isTDown = (GetAsyncKeyState(VK_T) & 0x8000) != 0;
            if (isTDown && !wasTDown)
            {
                currentThemeIndex = (currentThemeIndex + 1) % ThemeColors.Length;
                this.Invalidate();
            }
            wasTDown = isTDown;

            // B Key: Toggle Bank Shot Reflection Lines
            bool isBDown = (GetAsyncKeyState(VK_B) & 0x8000) != 0;
            if (isBDown && !wasBDown)
            {
                isBankShotEnabled = !isBankShotEnabled;
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
            string themeName = ThemeNames[currentThemeIndex];
            string modeText = isClickThrough ? " [Click-Through ON]" : " [Click-Through OFF]";
            string bankText = isBankShotEnabled ? " [Bank Shots: ON]" : " [Bank Shots: OFF (B)]";
            this.Text = "8 Ball Pool Guidelines (" + this.Width + "x" + this.Height + ") | Theme: " + themeName + " (T)" + modeText + bankText;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            PocketPosition closestPocket = GetClosestPocket();

            DrawCorners(g, themeColor);
            DrawPockets(g, themeColor, closestPocket);
            DrawGuideLines(g, themeColor, closestPocket);
            if (isBankShotEnabled)
            {
                DrawBankShotLines(g, themeColor, closestPocket);
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

        private void DrawBankShotLines(Graphics g, Color themeColor, PocketPosition closestPocket)
        {
            Point targetPt = Pocket.GetPoint(closestPocket);

            // Calculate 1-cushion bank shot bounce point off top cushion
            int topCushionY = 10;
            int bottomCushionY = this.Height - 50;

            // Top cushion bank bounce point
            double mirroredTargetY = -targetPt.Y + 2 * topCushionY;
            double dy = mirroredTargetY - lastBallPosition.Y;
            if (Math.Abs(dy) > 0.001)
            {
                double bounceX = lastBallPosition.X + (targetPt.X - lastBallPosition.X) * (topCushionY - lastBallPosition.Y) / dy;
                if (bounceX > 10 && bounceX < this.Width - 10)
                {
                    Point bouncePt = new Point((int)bounceX, topCushionY);
                    using (Pen bankPen = new Pen(Color.FromArgb(200, Color.Orange), 2))
                    {
                        bankPen.DashStyle = DashStyle.Dot;
                        g.DrawLine(bankPen, lastBallPosition, bouncePt);
                        g.DrawLine(bankPen, bouncePt, targetPt);
                    }
                }
            }

            // Bottom cushion bank bounce point
            double mirroredBottomY = 2 * bottomCushionY - targetPt.Y;
            double dyB = mirroredBottomY - lastBallPosition.Y;
            if (Math.Abs(dyB) > 0.001)
            {
                double bounceX = lastBallPosition.X + (targetPt.X - lastBallPosition.X) * (bottomCushionY - lastBallPosition.Y) / dyB;
                if (bounceX > 10 && bounceX < this.Width - 10)
                {
                    Point bouncePt = new Point((int)bounceX, bottomCushionY);
                    using (Pen bankPen = new Pen(Color.FromArgb(200, Color.Yellow), 2))
                    {
                        bankPen.DashStyle = DashStyle.Dot;
                        g.DrawLine(bankPen, lastBallPosition, bouncePt);
                        g.DrawLine(bankPen, bouncePt, targetPt);
                    }
                }
            }
        }

        private void FormMain_MouseUp(object sender, MouseEventArgs e)
        {
            isDragging = false;
        }

        private void FormMain_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
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
