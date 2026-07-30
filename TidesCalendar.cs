using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TidesCalendar
{
    public class TaskItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Date { get; set; }
        public bool Done { get; set; }
        public int ColorArgb { get; set; }
    }

    public static class TaskPalette
    {
        static readonly Color[] colors = new Color[]
        {
            Color.FromArgb(0, 153, 255),
            Color.FromArgb(22, 198, 12),
            Color.FromArgb(255, 140, 0),
            Color.FromArgb(227, 0, 140),
            Color.FromArgb(0, 199, 215),
            Color.FromArgb(255, 67, 67),
            Color.FromArgb(255, 201, 20),
            Color.FromArgb(168, 85, 247)
        };

        static readonly Color[] legacyColors = new Color[]
        {
            Color.FromArgb(0, 120, 212),
            Color.FromArgb(16, 124, 16),
            Color.FromArgb(202, 80, 16),
            Color.FromArgb(136, 23, 152),
            Color.FromArgb(0, 153, 188),
            Color.FromArgb(196, 43, 28),
            Color.FromArgb(108, 117, 43),
            Color.FromArgb(118, 96, 138)
        };

        public static Color Get(TaskItem task, int fallbackIndex)
        {
            if (task.ColorArgb != 0)
            {
                for (int i = 0; i < colors.Length; i++)
                    if (task.ColorArgb == colors[i].ToArgb()) return colors[i];
                for (int i = 0; i < legacyColors.Length; i++)
                {
                    if (task.ColorArgb == legacyColors[i].ToArgb())
                    {
                        task.ColorArgb = colors[i].ToArgb();
                        return colors[i];
                    }
                }
                Color saved = Color.FromArgb(task.ColorArgb);
                if (saved.GetBrightness() > .5F && saved.GetSaturation() > .5F)
                    return saved;
            }
            int hash = fallbackIndex;
            if (!string.IsNullOrEmpty(task.Id))
                foreach (char c in task.Id) hash = (hash * 31 + c) & 0x7fffffff;
            Color color = colors[Math.Abs(hash) % colors.Length];
            task.ColorArgb = color.ToArgb();
            return color;
        }

        public static Color Next(int index)
        {
            return colors[Math.Abs(index) % colors.Length];
        }
    }

    public class AppData
    {
        public List<TaskItem> Tasks { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Opacity { get; set; }
        public bool Locked { get; set; }
        public int Theme { get; set; }
        public int BackgroundArgb { get; set; }
        public int ForegroundArgb { get; set; }
        public double FontScale { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool AutoTextColor { get; set; }
        public int StyleVersion { get; set; }
    }

    public static class AppFonts
    {
        static readonly PrivateFontCollection privateFonts = new PrivateFontCollection();
        static FontFamily family;
        static IntPtr fontMemory = IntPtr.Zero;

        public static void Initialize()
        {
            if (family != null) return;
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("LXGWWenKai.ttf"))
                {
                    if (stream == null) throw new InvalidOperationException();
                    byte[] bytes = new byte[(int)stream.Length];
                    stream.Read(bytes, 0, bytes.Length);
                    fontMemory = Marshal.AllocCoTaskMem(bytes.Length);
                    Marshal.Copy(bytes, 0, fontMemory, bytes.Length);
                    privateFonts.AddMemoryFont(fontMemory, bytes.Length);
                    family = privateFonts.Families[0];
                }
            }
            catch
            {
                family = new FontFamily("Microsoft YaHei UI");
            }
        }

        public static Font Create(float size)
        {
            return Create(size, FontStyle.Regular);
        }

        public static Font Create(float size, FontStyle style)
        {
            Initialize();
            try { return new Font(family, size, style, GraphicsUnit.Point); }
            catch
            {
                FontStyle safeStyle = style & (FontStyle.Strikeout | FontStyle.Underline);
                return new Font(family, size, safeStyle, GraphicsUnit.Point);
            }
        }
    }

    public class SmoothLabel : Label
    {
        public SmoothLabel()
        {
            SetStyle(ControlStyles.UserPaint
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            e.Graphics.TextContrast = 0;

            StringFormat format = new StringFormat();
            format.Trimming = AutoEllipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None;
            if (!Text.Contains("\n")) format.FormatFlags |= StringFormatFlags.NoWrap;

            if (TextAlign == ContentAlignment.TopLeft || TextAlign == ContentAlignment.MiddleLeft || TextAlign == ContentAlignment.BottomLeft)
                format.Alignment = StringAlignment.Near;
            else if (TextAlign == ContentAlignment.TopCenter || TextAlign == ContentAlignment.MiddleCenter || TextAlign == ContentAlignment.BottomCenter)
                format.Alignment = StringAlignment.Center;
            else
                format.Alignment = StringAlignment.Far;

            if (TextAlign == ContentAlignment.TopLeft || TextAlign == ContentAlignment.TopCenter || TextAlign == ContentAlignment.TopRight)
                format.LineAlignment = StringAlignment.Near;
            else if (TextAlign == ContentAlignment.MiddleLeft || TextAlign == ContentAlignment.MiddleCenter || TextAlign == ContentAlignment.MiddleRight)
                format.LineAlignment = StringAlignment.Center;
            else
                format.LineAlignment = StringAlignment.Far;

            using (Brush brush = new SolidBrush(ForeColor))
                e.Graphics.DrawString(Text, Font, brush, ClientRectangle, format);
            format.Dispose();
        }
    }

    public class FluentSurface : Panel
    {
        public Color FillColor = Color.White;
        public Color BorderColor = Color.FromArgb(225, 225, 225);
        public Color AccentLineColor = Color.Transparent;
        public int Radius = 10;

        public FluentSurface()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Resize += delegate { UpdateShape(); };
        }

        void UpdateShape()
        {
            if (Width < 2 || Height < 2) return;
            using (GraphicsPath path = MakePath(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = MakePath(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
            using (Brush fill = new SolidBrush(FillColor))
            using (Pen border = new Pen(BorderColor, 1))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }
            if (AccentLineColor.A > 0)
            {
                using (Pen accent = new Pen(AccentLineColor, 3F))
                {
                    accent.StartCap = LineCap.Round;
                    accent.EndCap = LineCap.Round;
                    e.Graphics.DrawLine(accent, Radius + 5, Height - 4, Width - Radius - 5, Height - 4);
                }
            }
            base.OnPaint(e);
        }

        static GraphicsPath MakePath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class ModernColorSlider : Control
    {
        int currentValue;
        bool hovered;
        bool dragging;

        public event EventHandler ValueChanged;
        public Color AccentColor { get; set; }

        public int Value
        {
            get { return currentValue; }
            set
            {
                int next = Math.Max(0, Math.Min(255, value));
                if (next == currentValue) return;
                currentValue = next;
                Invalidate();
                EventHandler changed = ValueChanged;
                if (changed != null) changed(this, EventArgs.Empty);
            }
        }

        public ModernColorSlider()
        {
            AccentColor = Color.FromArgb(0, 120, 212);
            TabStop = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            const int pad = 10;
            int cy = Height / 2;
            int usable = Math.Max(1, Width - pad * 2);
            int knobX = pad + (int)Math.Round(usable * currentValue / 255D);
            Rectangle track = new Rectangle(pad, cy - 3, usable, 6);
            Rectangle filled = new Rectangle(pad, cy - 3, Math.Max(1, knobX - pad), 6);
            using (GraphicsPath trackPath = RoundRect(track, 3))
            using (GraphicsPath fillPath = RoundRect(filled, 3))
            using (Brush trackBrush = new SolidBrush(Color.FromArgb(215, 215, 215)))
            using (Brush fillBrush = new SolidBrush(AccentColor))
            {
                g.FillPath(trackBrush, trackPath);
                g.FillPath(fillBrush, fillPath);
            }

            int knobSize = hovered || dragging || Focused ? 17 : 15;
            Rectangle knob = new Rectangle(knobX - knobSize / 2, cy - knobSize / 2, knobSize, knobSize);
            using (Brush shadow = new SolidBrush(Color.FromArgb(35, 0, 0, 0)))
                g.FillEllipse(shadow, knob.X + 1, knob.Y + 2, knob.Width, knob.Height);
            using (Brush white = new SolidBrush(Color.White))
            using (Pen border = new Pen(AccentColor, 2F))
            {
                g.FillEllipse(white, knob);
                g.DrawEllipse(border, knob);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                Focus();
                SetFromMouse(e.X);
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (dragging) SetFromMouse(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            dragging = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down)
            {
                Value--;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up)
            {
                Value++;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        void SetFromMouse(int x)
        {
            const int pad = 10;
            int usable = Math.Max(1, Width - pad * 2);
            Value = (int)Math.Round(Math.Max(0, Math.Min(usable, x - pad)) * 255D / usable);
        }

        static GraphicsPath RoundRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (rect.Width <= 2 || rect.Height <= 2)
            {
                path.AddRectangle(rect);
                return path;
            }
            int safeRadius = Math.Max(1, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
            int d = safeRadius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class ModernColorSwatch : Control
    {
        bool hovered;
        bool selected;

        public Color SwatchColor { get; private set; }
        public bool Selected
        {
            get { return selected; }
            set { selected = value; Invalidate(); }
        }

        public ModernColorSwatch(Color color)
        {
            SwatchColor = color;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle outer = new Rectangle(1, 1, Width - 3, Height - 3);
            Rectangle inner = Selected ? new Rectangle(5, 5, Width - 11, Height - 11) : outer;
            using (GraphicsPath outerPath = RoundRect(outer, 8))
            using (Brush fill = new SolidBrush(Selected ? Color.White : SwatchColor))
            {
                g.FillPath(fill, outerPath);
                using (Pen border = new Pen(Selected ? Color.FromArgb(0, 120, 212) :
                    Color.FromArgb(hovered ? 125 : 45, 60, 60, 60), Selected ? 2F : 1F))
                    g.DrawPath(border, outerPath);
            }
            if (Selected)
            {
                using (GraphicsPath innerPath = RoundRect(inner, 5))
                using (Brush color = new SolidBrush(SwatchColor))
                    g.FillPath(color, innerPath);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        static GraphicsPath RoundRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (rect.Width <= 2 || rect.Height <= 2)
            {
                path.AddRectangle(rect);
                return path;
            }
            int safeRadius = Math.Max(1, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
            int d = safeRadius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class ModernColorPicker : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        public Color SelectedColor { get; private set; }
        readonly FluentSurface preview = new FluentSurface();
        readonly ModernColorSlider red = new ModernColorSlider();
        readonly ModernColorSlider green = new ModernColorSlider();
        readonly ModernColorSlider blue = new ModernColorSlider();
        readonly SmoothLabel redValue = new SmoothLabel();
        readonly SmoothLabel greenValue = new SmoothLabel();
        readonly SmoothLabel blueValue = new SmoothLabel();
        readonly TextBox hexBox = new TextBox();
        readonly List<ModernColorSwatch> swatches = new List<ModernColorSwatch>();
        bool updating;

        static readonly Color[] palette = new Color[]
        {
            Color.FromArgb(0, 120, 212), Color.FromArgb(0, 153, 255),
            Color.FromArgb(0, 183, 195), Color.FromArgb(16, 124, 16),
            Color.FromArgb(22, 198, 12), Color.FromArgb(255, 185, 0),
            Color.FromArgb(255, 140, 0), Color.FromArgb(202, 80, 16),
            Color.FromArgb(232, 17, 35), Color.FromArgb(255, 67, 67),
            Color.FromArgb(227, 0, 140), Color.FromArgb(168, 85, 247),
            Color.FromArgb(136, 23, 152), Color.FromArgb(91, 95, 199),
            Color.FromArgb(32, 32, 32), Color.FromArgb(80, 80, 80),
            Color.FromArgb(128, 128, 128), Color.FromArgb(180, 180, 180),
            Color.FromArgb(230, 230, 230), Color.FromArgb(250, 250, 250)
        };

        public ModernColorPicker(Color initial, string titleText)
        {
            SelectedColor = Color.FromArgb(initial.R, initial.G, initial.B);
            Text = titleText;
            ClientSize = new Size(460, 500);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(243, 243, 243);
            Font = AppFonts.Create(9F);
            AutoScaleMode = AutoScaleMode.Dpi;

            Shown += delegate
            {
                int corner = 2;
                try { DwmSetWindowAttribute(Handle, 33, ref corner, 4); } catch { }
                ApplyRoundedRegion();
            };
            Resize += delegate { ApplyRoundedRegion(); };

            FluentSurface header = new FluentSurface
            {
                FillColor = Color.FromArgb(251, 251, 251),
                BorderColor = Color.FromArgb(225, 225, 225),
                Radius = 12
            };
            header.SetBounds(10, 10, 440, 62);
            header.MouseDown += DragHeader;
            Controls.Add(header);

            SmoothLabel title = new SmoothLabel
            {
                Text = titleText,
                Font = AppFonts.Create(13F, FontStyle.Bold),
                ForeColor = Color.FromArgb(32, 32, 32),
                TextAlign = ContentAlignment.MiddleLeft
            };
            title.SetBounds(20, 10, 340, 40);
            title.MouseDown += DragHeader;
            header.Controls.Add(title);

            Button close = ModernButton("×", Color.FromArgb(251, 251, 251), Color.FromArgb(80, 80, 80));
            close.Font = AppFonts.Create(12F);
            close.SetBounds(392, 14, 34, 32);
            close.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
            close.DialogResult = DialogResult.Cancel;
            header.Controls.Add(close);

            SmoothLabel presetTitle = SectionLabel("推荐颜色");
            presetTitle.SetBounds(22, 83, 120, 25);
            Controls.Add(presetTitle);

            for (int i = 0; i < palette.Length; i++)
            {
                Color swatchColor = palette[i];
                ModernColorSwatch swatch = new ModernColorSwatch(swatchColor);
                swatch.SetBounds(22 + (i % 10) * 41, 112 + (i / 10) * 41, 30, 30);
                swatch.Click += delegate { SetColor(swatchColor, true); };
                swatches.Add(swatch);
                Controls.Add(swatch);
            }

            SmoothLabel customTitle = SectionLabel("自定义颜色");
            customTitle.SetBounds(22, 202, 120, 25);
            Controls.Add(customTitle);

            SetupSlider(red, 238, "R", redValue);
            SetupSlider(green, 283, "G", greenValue);
            SetupSlider(blue, 328, "B", blueValue);

            FluentSurface hexSurface = new FluentSurface
            {
                FillColor = Color.White,
                BorderColor = Color.FromArgb(205, 205, 205),
                Radius = 8
            };
            hexSurface.SetBounds(22, 378, 210, 42);
            Controls.Add(hexSurface);
            SmoothLabel hash = new SmoothLabel
            {
                Text = "#", Font = AppFonts.Create(10F),
                ForeColor = Color.FromArgb(100, 100, 100),
                TextAlign = ContentAlignment.MiddleCenter
            };
            hash.SetBounds(9, 7, 22, 28);
            hexSurface.Controls.Add(hash);
            hexBox.BorderStyle = BorderStyle.None;
            hexBox.BackColor = Color.White;
            hexBox.ForeColor = Color.FromArgb(35, 35, 35);
            hexBox.Font = AppFonts.Create(10F);
            hexBox.CharacterCasing = CharacterCasing.Upper;
            hexBox.MaxLength = 6;
            hexBox.SetBounds(34, 11, 160, 24);
            hexBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                Color parsed;
                if (TryParseHex(hexBox.Text, out parsed)) SetColor(parsed, true);
                e.SuppressKeyPress = true;
            };
            hexSurface.Controls.Add(hexBox);

            preview.FillColor = SelectedColor;
            preview.BorderColor = Color.FromArgb(205, 205, 205);
            preview.Radius = 8;
            preview.SetBounds(244, 378, 194, 42);
            Controls.Add(preview);

            SmoothLabel hint = new SmoothLabel
            {
                Text = "输入十六进制颜色后按回车即可预览",
                Font = AppFonts.Create(8F),
                ForeColor = Color.FromArgb(105, 105, 105),
                TextAlign = ContentAlignment.MiddleLeft
            };
            hint.SetBounds(23, 425, 300, 24);
            Controls.Add(hint);

            Button cancel = ModernButton("取消", Color.FromArgb(232, 232, 232), Color.FromArgb(55, 55, 55));
            cancel.SetBounds(282, 452, 76, 36);
            cancel.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 220, 220);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            Button apply = ModernButton("应用", Color.FromArgb(0, 120, 212), Color.White);
            apply.SetBounds(368, 452, 70, 36);
            apply.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 102, 180);
            apply.DialogResult = DialogResult.OK;
            Controls.Add(apply);
            AcceptButton = apply;
            CancelButton = cancel;

            red.ValueChanged += SliderChanged;
            green.ValueChanged += SliderChanged;
            blue.ValueChanged += SliderChanged;
            red.AccentColor = Color.FromArgb(232, 17, 35);
            green.AccentColor = Color.FromArgb(16, 124, 16);
            blue.AccentColor = Color.FromArgb(0, 120, 212);
            SetColor(SelectedColor, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int CS_DROPSHADOW = 0x00020000;
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        void SetupSlider(ModernColorSlider slider, int y, string labelText, SmoothLabel valueLabel)
        {
            SmoothLabel label = new SmoothLabel
            {
                Text = labelText, Font = AppFonts.Create(9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 55, 55),
                TextAlign = ContentAlignment.MiddleCenter
            };
            label.SetBounds(22, y, 28, 32);
            Controls.Add(label);
            slider.SetBounds(54, y, 330, 32);
            Controls.Add(slider);
            valueLabel.Font = AppFonts.Create(9F);
            valueLabel.ForeColor = Color.FromArgb(70, 70, 70);
            valueLabel.TextAlign = ContentAlignment.MiddleCenter;
            valueLabel.SetBounds(392, y, 46, 32);
            Controls.Add(valueLabel);
        }

        void SliderChanged(object sender, EventArgs e)
        {
            if (updating) return;
            SetColor(Color.FromArgb(red.Value, green.Value, blue.Value), false);
        }

        void SetColor(Color color, bool updateSliders)
        {
            SelectedColor = Color.FromArgb(color.R, color.G, color.B);
            if (updateSliders)
            {
                updating = true;
                red.Value = SelectedColor.R;
                green.Value = SelectedColor.G;
                blue.Value = SelectedColor.B;
                updating = false;
            }
            redValue.Text = SelectedColor.R.ToString();
            greenValue.Text = SelectedColor.G.ToString();
            blueValue.Text = SelectedColor.B.ToString();
            hexBox.Text = SelectedColor.R.ToString("X2") + SelectedColor.G.ToString("X2") + SelectedColor.B.ToString("X2");
            preview.FillColor = SelectedColor;
            preview.Invalidate();
            foreach (ModernColorSwatch swatch in swatches)
            {
                swatch.Selected = swatch.SwatchColor.R == SelectedColor.R &&
                    swatch.SwatchColor.G == SelectedColor.G &&
                    swatch.SwatchColor.B == SelectedColor.B;
            }
        }

        bool TryParseHex(string text, out Color color)
        {
            color = Color.Black;
            string value = text.Trim().TrimStart('#');
            int parsed;
            if (value.Length != 6 || !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed))
                return false;
            color = Color.FromArgb((parsed >> 16) & 255, (parsed >> 8) & 255, parsed & 255);
            return true;
        }

        SmoothLabel SectionLabel(string text)
        {
            return new SmoothLabel
            {
                Text = text, Font = AppFonts.Create(9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(45, 45, 45),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        Button ModernButton(string text, Color back, Color fore)
        {
            Button button = new Button
            {
                Text = text, BackColor = back, ForeColor = fore,
                Font = AppFonts.Create(9F), FlatStyle = FlatStyle.Flat,
                TabStop = false,
                UseCompatibleTextRendering = true
            };
            button.FlatAppearance.BorderSize = 0;
            button.Resize += delegate { RoundButton(button, 7); };
            return button;
        }

        void RoundButton(Button button, int radius)
        {
            if (button.Width < 2 || button.Height < 2) return;
            using (GraphicsPath path = new GraphicsPath())
            {
                int d = radius * 2;
                Rectangle rect = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                Region old = button.Region;
                button.Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        void ApplyRoundedRegion()
        {
            if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
            using (GraphicsPath path = new GraphicsPath())
            {
                int r = 13;
                int d = r * 2;
                Rectangle rect = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        void DragHeader(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
        }
    }

    public class ScheduleEditor : Form
    {
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        public List<TaskItem> ResultTasks { get; private set; }
        readonly FlowLayoutPanel taskList = new FlowLayoutPanel();
        readonly TextBox addBox = new TextBox();
        readonly SmoothLabel progressLabel = new SmoothLabel();
        readonly ToolTip colorTip = new ToolTip();
        readonly Color accent;
        readonly string dateKey;

        public ScheduleEditor(DateTime date, IEnumerable<TaskItem> items, Color theme)
        {
            accent = theme;
            dateKey = date.ToString("yyyy-MM-dd");
            ResultTasks = items.Select(t => new TaskItem
            {
                Id = t.Id, Title = t.Title, Date = t.Date, Done = t.Done,
                ColorArgb = t.ColorArgb
            }).ToList();

            Text = date.ToString("yyyy年M月d日") + " 待办";
            ClientSize = new Size(520, 600);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(243, 243, 243);
            ShowInTaskbar = false;
            Font = AppFonts.Create(9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            MouseDown += DragHeader;

            Shown += delegate
            {
                int corner = 2;
                try { DwmSetWindowAttribute(Handle, 33, ref corner, 4); } catch { }
                ApplyRoundedRegion();
                addBox.Focus();
            };
            Resize += delegate { ApplyRoundedRegion(); };

            FluentSurface header = new FluentSurface
            {
                FillColor = Color.FromArgb(251, 251, 251),
                BorderColor = Color.FromArgb(229, 229, 229),
                Radius = 12
            };
            header.SetBounds(10, 10, 500, 96);
            header.MouseDown += DragHeader;
            Controls.Add(header);

            Panel accentBar = new Panel { BackColor = accent };
            accentBar.SetBounds(14, 20, 4, 56);
            accentBar.MouseDown += DragHeader;
            header.Controls.Add(accentBar);

            FluentSurface dragGrip = new FluentSurface
            {
                FillColor = Color.FromArgb(188, 188, 188),
                BorderColor = Color.FromArgb(188, 188, 188),
                Radius = 2
            };
            dragGrip.SetBounds(230, 6, 40, 4);
            dragGrip.MouseDown += DragHeader;
            header.Controls.Add(dragGrip);

            SmoothLabel dateLabel = new SmoothLabel
            {
                Text = date.ToString("M月d日 dddd", new CultureInfo("zh-CN")),
                ForeColor = Color.FromArgb(32, 32, 32),
                Font = AppFonts.Create(16F, FontStyle.Bold),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = true
            };
            dateLabel.SetBounds(30, 13, 370, 38);
            dateLabel.MouseDown += DragHeader;
            header.Controls.Add(dateLabel);

            progressLabel.ForeColor = Color.FromArgb(100, 100, 100);
            progressLabel.Font = AppFonts.Create(9F);
            progressLabel.AutoSize = false;
            progressLabel.TextAlign = ContentAlignment.MiddleLeft;
            progressLabel.UseCompatibleTextRendering = true;
            progressLabel.SetBounds(31, 55, 365, 25);
            progressLabel.MouseDown += DragHeader;
            header.Controls.Add(progressLabel);

            Button close = ActionButton("×", Color.FromArgb(251, 251, 251), Color.FromArgb(90, 90, 90));
            close.Font = AppFonts.Create(12F);
            close.SetBounds(452, 14, 34, 32);
            close.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
            close.DialogResult = DialogResult.Cancel;
            header.Controls.Add(close);

            SmoothLabel section = new SmoothLabel
            {
                Text = "待办事项",
                Font = AppFonts.Create(10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(45, 45, 45),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                UseCompatibleTextRendering = true
            };
            section.SetBounds(22, 118, 120, 27);
            section.MouseDown += DragHeader;
            Controls.Add(section);

            Button clearDone = ActionButton("清除已完成", Color.FromArgb(243, 243, 243), Color.FromArgb(105, 105, 105));
            clearDone.Font = AppFonts.Create(9F);
            clearDone.SetBounds(400, 116, 96, 29);
            clearDone.FlatAppearance.MouseOverBackColor = Color.FromArgb(231, 231, 231);
            clearDone.Click += delegate
            {
                ResultTasks.RemoveAll(t => t.Done);
                RefreshTasks();
            };
            Controls.Add(clearDone);

            taskList.SetBounds(16, 150, 488, 320);
            taskList.Padding = new Padding(4, 2, 4, 4);
            taskList.FlowDirection = FlowDirection.TopDown;
            taskList.WrapContents = false;
            taskList.AutoScroll = true;
            taskList.BackColor = Color.FromArgb(243, 243, 243);
            taskList.MouseDown += DragHeader;
            Controls.Add(taskList);

            FluentSurface inputSurface = new FluentSurface
            {
                FillColor = Color.White,
                BorderColor = Color.FromArgb(205, 205, 205),
                Radius = 9
            };
            inputSurface.SetBounds(20, 480, 480, 50);
            Controls.Add(inputSurface);

            addBox.BorderStyle = BorderStyle.None;
            addBox.BackColor = Color.White;
            addBox.ForeColor = Color.FromArgb(38, 38, 38);
            addBox.Font = AppFonts.Create(11F);
            addBox.SetBounds(14, 14, 365, 24);
            addBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { AddTask(); e.SuppressKeyPress = true; }
            };
            inputSurface.Controls.Add(addBox);

            Button add = ActionButton("＋ 添加", accent, Color.White);
            add.SetBounds(392, 8, 80, 34);
            add.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent);
            add.Click += delegate { AddTask(); };
            inputSurface.Controls.Add(add);

            Button cancel = ActionButton("取消", Color.FromArgb(232, 232, 232), Color.FromArgb(55, 55, 55));
            cancel.SetBounds(338, 550, 76, 36);
            cancel.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 220, 220);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            Button save = ActionButton("保存", accent, Color.White);
            save.SetBounds(424, 550, 76, 36);
            save.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent);
            save.DialogResult = DialogResult.OK;
            Controls.Add(save);

            AcceptButton = save;
            CancelButton = cancel;
            RefreshTasks();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int CS_DROPSHADOW = 0x00020000;
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        void ApplyRoundedRegion()
        {
            if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
            using (GraphicsPath path = new GraphicsPath())
            {
                int r = 13;
                int d = r * 2;
                Rectangle rect = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        void DragHeader(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
        }

        Button ActionButton(string text, Color back, Color fore)
        {
            Button b = new Button
            {
                Text = text, BackColor = back, ForeColor = fore,
                FlatStyle = FlatStyle.Flat,
                Font = AppFonts.Create(9F), TabStop = false,
                UseCompatibleTextRendering = true
            };
            b.FlatAppearance.BorderSize = 0;
            b.Resize += delegate { RoundButton(b, 7); };
            return b;
        }

        void RoundButton(Button button, int radius)
        {
            if (button.Width < 2 || button.Height < 2) return;
            using (GraphicsPath path = new GraphicsPath())
            {
                int d = radius * 2;
                Rectangle rect = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                Region old = button.Region;
                button.Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        void AddTask()
        {
            string text = addBox.Text.Trim();
            if (text.Length == 0) return;
            ResultTasks.Add(new TaskItem
            {
                Id = Guid.NewGuid().ToString(), Title = text, Date = dateKey, Done = false,
                ColorArgb = TaskPalette.Next(ResultTasks.Count).ToArgb()
            });
            addBox.Clear();
            RefreshTasks();
            addBox.Focus();
        }

        void RefreshTasks()
        {
            int completed = ResultTasks.Count(t => t.Done);
            progressLabel.Text = ResultTasks.Count == 0
                ? "今天还没有安排"
                : completed + " / " + ResultTasks.Count + " 已完成";

            taskList.SuspendLayout();
            taskList.Controls.Clear();
            if (ResultTasks.Count == 0)
            {
                SmoothLabel empty = new SmoothLabel
                {
                    Text = "✓\n这一天还没有待办\n在下方输入内容并按回车添加",
                    Font = AppFonts.Create(10F),
                    ForeColor = Color.FromArgb(125, 125, 125),
                    TextAlign = ContentAlignment.MiddleCenter,
                    UseCompatibleTextRendering = true,
                    Size = new Size(458, 260),
                    Margin = new Padding(0)
                };
                taskList.Controls.Add(empty);
            }

            foreach (TaskItem task in ResultTasks.OrderBy(t => t.Done).ToList())
            {
                FluentSurface row = new FluentSurface
                {
                    Width = 458, Height = 58, Margin = new Padding(5, 4, 5, 4),
                    FillColor = task.Done ? Color.FromArgb(248, 248, 248) : Color.White,
                    BorderColor = Color.FromArgb(224, 224, 224),
                    AccentLineColor = TaskPalette.Get(task, ResultTasks.IndexOf(task)),
                    Radius = 9
                };

                CheckBox done = new CheckBox { Checked = task.Done, AutoSize = false };
                done.SetBounds(14, 17, 24, 24);
                row.Controls.Add(done);

                Color taskColor = TaskPalette.Get(task, ResultTasks.IndexOf(task));
                Button colorButton = ActionButton("", taskColor, Color.White);
                colorButton.SetBounds(44, 19, 20, 20);
                RoundButton(colorButton, 10);
                colorButton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(taskColor);
                colorTip.SetToolTip(colorButton, "选择这项待办的高亮颜色");
                colorButton.Click += delegate
                {
                    using (ModernColorPicker picker = new ModernColorPicker(
                        Color.FromArgb(task.ColorArgb), "待办高亮颜色"))
                    {
                        if (picker.ShowDialog(this) != DialogResult.OK) return;
                        task.ColorArgb = picker.SelectedColor.ToArgb();
                        colorButton.BackColor = picker.SelectedColor;
                        colorButton.FlatAppearance.MouseOverBackColor = ControlPaint.Light(picker.SelectedColor);
                        row.AccentLineColor = picker.SelectedColor;
                        row.Invalidate();
                    }
                };
                row.Controls.Add(colorButton);

                SmoothLabel text = new SmoothLabel
                {
                    Text = task.Title,
                    Font = AppFonts.Create(10F, task.Done ? FontStyle.Strikeout : FontStyle.Regular),
                    ForeColor = task.Done ? Color.FromArgb(145, 145, 145) : Color.FromArgb(35, 35, 35),
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    UseCompatibleTextRendering = true
                };
                text.SetBounds(72, 9, 328, 40);
                row.Controls.Add(text);

                done.CheckedChanged += delegate
                {
                    task.Done = done.Checked;
                    text.Font = AppFonts.Create(10F, done.Checked ? FontStyle.Strikeout : FontStyle.Regular);
                    text.ForeColor = done.Checked ? Color.FromArgb(145, 145, 145) : Color.FromArgb(35, 35, 35);
                    row.FillColor = done.Checked ? Color.FromArgb(248, 248, 248) : Color.White;
                    row.Invalidate();
                    progressLabel.Text = ResultTasks.Count(t => t.Done) + " / " + ResultTasks.Count + " 已完成";
                };

                Button remove = ActionButton("×", row.FillColor, Color.FromArgb(150, 70, 70));
                remove.Font = AppFonts.Create(11F);
                remove.SetBounds(412, 12, 34, 34);
                remove.FlatAppearance.MouseOverBackColor = Color.FromArgb(252, 230, 230);
                remove.Click += delegate { ResultTasks.Remove(task); RefreshTasks(); };
                row.Controls.Add(remove);
                taskList.Controls.Add(row);
            }
            taskList.ResumeLayout();
        }
    }

    public class CalendarOverlay : Form
    {
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x80;
        const int WS_EX_TRANSPARENT = 0x20;
        const int WM_NCHITTEST = 0x0084;
        const int HTLEFT = 10;
        const int HTRIGHT = 11;
        const int HTTOP = 12;
        const int HTTOPLEFT = 13;
        const int HTTOPRIGHT = 14;
        const int HTBOTTOM = 15;
        const int HTBOTTOMLEFT = 16;
        const int HTBOTTOMRIGHT = 17;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOACTIVATE = 0x0010;
        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        readonly string dataFolder;
        readonly string dataFile;
        readonly JavaScriptSerializer json = new JavaScriptSerializer();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Timer desktopTimer = new Timer();
        readonly ChineseLunisolarCalendar lunar = new ChineseLunisolarCalendar();

        List<TaskItem> tasks = new List<TaskItem>();
        DateTime viewMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        Rectangle[] cells = new Rectangle[42];
        Rectangle prevRect, nextRect, closeRect, todayRect;
        int hoverCell = -1;
        int hoverAction;
        bool dragging;
        Point dragOrigin;
        Point formOrigin;
        bool locked;
        int theme;
        int customBackgroundArgb = Color.FromArgb(29, 142, 184).ToArgb();
        int foregroundArgb = Color.White.ToArgb();
        double fontScale = 1.0;
        bool alwaysOnTop;
        bool autoTextColor = true;
        bool exiting;

        Color OverlayColor
        {
            get
            {
                if (theme == 1) return Color.FromArgb(32, 32, 32);
                if (theme == 2) return Color.FromArgb(45, 123, 162);
                if (theme == 3) return Color.FromArgb(customBackgroundArgb);
                return Color.FromArgb(243, 246, 244);
            }
        }

        Color AccentColor
        {
            get
            {
                if (theme == 1) return Color.FromArgb(96, 205, 255);
                if (theme == 2) return Color.FromArgb(96, 205, 255);
                if (theme == 3) return OverlayColor;
                return Color.FromArgb(0, 120, 212);
            }
        }

        Color TextColor
        {
            get
            {
                if (!autoTextColor) return Color.FromArgb(foregroundArgb);
                Color c = OverlayColor;
                double luminance = .299 * c.R + .587 * c.G + .114 * c.B;
                return luminance > 155 ? Color.FromArgb(31, 31, 31) : Color.FromArgb(248, 248, 248);
            }
        }

        float FontSize(float size)
        {
            float responsive = Math.Min(ClientSize.Width / 900F, ClientSize.Height / 590F);
            responsive = Math.Max(.84F, Math.Min(1.28F, responsive));
            return Math.Max(7.5F, Math.Min(28F, size * (float)fontScale * responsive));
        }

        public CalendarOverlay()
        {
            dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TidesCalendar");
            dataFile = Path.Combine(dataFolder, "data.json");

            Text = "潮汐日历";
            Width = 900;
            Height = 590;
            MinimumSize = new Size(650, 430);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            KeyPreview = true;
            Font = AppFonts.Create(9F);
            AutoScaleMode = AutoScaleMode.Dpi;

            LoadData();
            EnsureVisibleBounds();
            ApplyTheme();
            BuildTray();
            ContextMenuStrip = BuildMenu();

            Shown += delegate
            {
                int ex = GetWindowLong(Handle, GWL_EXSTYLE);
                SetWindowLong(Handle, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW);
                int corner = 2;
                try { DwmSetWindowAttribute(Handle, 33, ref corner, 4); } catch { }
                ApplyRoundedRegion();
                ApplyLockMode();
                BeginInvoke((MethodInvoker)delegate { RestoreFromExternalLaunch(); });
                // Keep the overlay directly above the desktop. As soon as the
                // user activates another app, normal Windows z-order naturally
                // places that app above the calendar.
            };
            FormClosing += OnClosing;
            Resize += delegate { ApplyRoundedRegion(); Invalidate(); SaveData(); };
            LocationChanged += delegate { if (!dragging) SaveData(); };
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseDoubleClick += OnDoubleClick;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Left) ChangeMonth(-1);
                if (e.KeyCode == Keys.Right) ChangeMonth(1);
                if (e.KeyCode == Keys.Home) GoToday();
            };

            desktopTimer.Interval = 1600;
            desktopTimer.Tick += delegate { if (!ContainsFocus) Invalidate(); };
            desktopTimer.Start();
        }

        void ApplyTheme()
        {
            BackColor = OverlayColor;
            Invalidate();
        }

        void ApplyRoundedRegion()
        {
            if (ClientSize.Width < 2 || ClientSize.Height < 2) return;
            using (GraphicsPath path = RoundedRectangle(
                new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), 14))
            {
                Region oldRegion = Region;
                Region = new Region(path);
                if (oldRegion != null) oldRegion.Dispose();
            }
        }

        void LoadData()
        {
            try
            {
                Directory.CreateDirectory(dataFolder);
                if (File.Exists(dataFile))
                {
                    AppData data = json.Deserialize<AppData>(File.ReadAllText(dataFile, Encoding.UTF8));
                    tasks = data.Tasks ?? new List<TaskItem>();
                    locked = data.Locked;
                    theme = data.Theme;
                    if (data.BackgroundArgb != 0)
                        customBackgroundArgb = data.BackgroundArgb;
                    if (data.ForegroundArgb != 0)
                        foregroundArgb = data.ForegroundArgb;
                    fontScale = data.FontScale >= .8 && data.FontScale <= 1.5 ? data.FontScale : 1.0;
                    alwaysOnTop = data.AlwaysOnTop;
                    TopMost = alwaysOnTop;
                    autoTextColor = data.StyleVersion >= 2 ? data.AutoTextColor : true;
                    if (data.StyleVersion < 2)
                    {
                        theme = 0;
                        Opacity = .94;
                    }
                    Width = data.Width >= 650 ? data.Width : 900;
                    Height = data.Height >= 430 ? data.Height : 590;
                    Opacity = data.StyleVersion < 2
                        ? .94
                        : (data.Opacity >= 0 && data.Opacity <= 1 ? data.Opacity : .94);
                    Location = new Point(data.X, data.Y);
                    if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(new Rectangle(Location, Size))))
                        Location = DefaultLocation();
                    return;
                }
            }
            catch { }
            Location = DefaultLocation();
            Opacity = .94;
        }

        Point DefaultLocation()
        {
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            return new Point(area.Left + 32, area.Top + 38);
        }

        void EnsureVisibleBounds()
        {
            Rectangle requested = new Rectangle(Location, Size);
            Screen target = Screen.AllScreens
                .OrderByDescending(screen =>
                {
                    Rectangle overlap = Rectangle.Intersect(screen.WorkingArea, requested);
                    return Math.Max(0, overlap.Width) * Math.Max(0, overlap.Height);
                })
                .FirstOrDefault() ?? Screen.PrimaryScreen;
            Rectangle area = target.WorkingArea;
            int margin = 18;
            int width = Math.Min(Width, Math.Max(MinimumSize.Width, area.Width - margin * 2));
            int height = Math.Min(Height, Math.Max(MinimumSize.Height, area.Height - margin * 2));
            int maxX = Math.Max(area.Left + margin, area.Right - width - margin);
            int maxY = Math.Max(area.Top + margin, area.Bottom - height - margin);
            int x = Math.Max(area.Left + margin, Math.Min(Left, maxX));
            int y = Math.Max(area.Top + margin, Math.Min(Top, maxY));
            SetBounds(x, y, width, height);
        }

        void SaveData()
        {
            if (exiting) return;
            try
            {
                Directory.CreateDirectory(dataFolder);
                AppData data = new AppData
                {
                    Tasks = tasks, X = Left, Y = Top, Width = Width, Height = Height,
                    Opacity = Opacity, Locked = locked, Theme = theme,
                    BackgroundArgb = customBackgroundArgb,
                    ForegroundArgb = foregroundArgb,
                    FontScale = fontScale,
                    AlwaysOnTop = alwaysOnTop,
                    AutoTextColor = autoTextColor,
                    StyleVersion = 2
                };
                File.WriteAllText(dataFile, json.Serialize(data), Encoding.UTF8);
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.TextContrast = 0;
            Brush textBrush = new SolidBrush(TextColor);

            int headerH = Math.Max(56, Height / 11);
            int weekH = 30;
            int pad = 18;
            int footerH = 36;
            int gridBottom = Height - footerH;
            Rectangle whole = new Rectangle(0, 0, Width - 1, Height - 1);
            using (Pen outer = new Pen(Color.FromArgb(80, TextColor), 1))
            using (GraphicsPath outline = RoundedRectangle(whole, 13))
                g.DrawPath(outer, outline);

            string title = viewMonth.ToString("yyyy年 M月");
            using (Font monthFont = AppFonts.Create(FontSize(13F), FontStyle.Bold))
            using (Brush white = new SolidBrush(TextColor))
            {
                SizeF size = g.MeasureString(title, monthFont);
                g.DrawString(title, monthFont, white, (Width - size.Width) / 2, 17);
            }

            prevRect = new Rectangle(Width - 122, 12, 36, 34);
            nextRect = new Rectangle(Width - 82, 12, 36, 34);
            closeRect = new Rectangle(Width - 40, 12, 28, 34);
            DrawToolbarButton(g, prevRect, "‹");
            DrawToolbarButton(g, nextRect, "›");
            DrawToolbarButton(g, closeRect, "⌄");

            Font todayFont = AppFonts.Create(FontSize(8.5F));
            SizeF todayTextSize = g.MeasureString("回到今天", todayFont);
            int todayButtonWidth = Math.Max(92, (int)Math.Ceiling(todayTextSize.Width) + 28);
            int todayButtonHeight = Math.Max(32, (int)Math.Ceiling(todayTextSize.Height) + 10);
            todayRect = new Rectangle(16, 12, todayButtonWidth, todayButtonHeight);
            using (Brush soft = new SolidBrush(Color.FromArgb(hoverAction == 1 ? 70 : 34, AccentColor)))
            using (GraphicsPath todayPath = RoundedRectangle(todayRect, 7))
                g.FillPath(soft, todayPath);
            DrawCentered(g, "回到今天", todayFont, textBrush, todayRect);

            string[] week = { "星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期日" };
            int gridTop = headerH + weekH;
            float cellW = (Width - pad * 2) / 7f;
            float cellH = (gridBottom - gridTop) / 6f;
            using (Pen line = new Pen(Color.FromArgb(42, TextColor), 1))
            {
                for (int i = 0; i <= 7; i++)
                {
                    int x = pad + (int)Math.Round(i * cellW);
                    g.DrawLine(line, x, headerH, x, gridBottom);
                }
                g.DrawLine(line, pad, headerH, Width - pad, headerH);
                g.DrawLine(line, pad, gridTop, Width - pad, gridTop);
                for (int row = 1; row <= 6; row++)
                {
                    int y = gridTop + (int)Math.Round(row * cellH);
                    g.DrawLine(line, pad, y, Width - pad, y);
                }
            }
            for (int i = 0; i < 7; i++)
            {
                Rectangle wr = new Rectangle(pad + (int)(i * cellW), headerH, (int)cellW, weekH);
                Color weekColor = i > 4 ? MixColor(TextColor, AccentColor, .55F) : TextColor;
                using (Brush weekBrush = new SolidBrush(weekColor))
                    DrawCentered(g, week[i], AppFonts.Create(FontSize(8F)), weekBrush, wr);
            }

            DateTime first = new DateTime(viewMonth.Year, viewMonth.Month, 1);
            int offset = ((int)first.DayOfWeek + 6) % 7;
            DateTime start = first.AddDays(-offset);
            for (int i = 0; i < 42; i++)
            {
                int col = i % 7;
                int row = i / 7;
                Rectangle cell = new Rectangle(
                    pad + (int)Math.Round(col * cellW),
                    gridTop + (int)Math.Round(row * cellH),
                    (int)Math.Round(cellW),
                    (int)Math.Round(cellH));
                cells[i] = cell;
                DateTime d = start.AddDays(i);

                if (i == hoverCell && !locked)
                {
                    using (Brush hover = new SolidBrush(Color.FromArgb(22, TextColor)))
                    using (GraphicsPath hoverPath = RoundedRectangle(
                        new Rectangle(cell.X + 3, cell.Y + 3, cell.Width - 6, cell.Height - 6), 8))
                        g.FillPath(hover, hoverPath);
                }
                if (d.Date == DateTime.Today)
                {
                    using (Brush todayFill = new SolidBrush(Color.FromArgb(34, AccentColor)))
                    using (GraphicsPath todayPath = RoundedRectangle(
                        new Rectangle(cell.X + 2, cell.Y + 2, cell.Width - 4, cell.Height - 4), 8))
                        g.FillPath(todayFill, todayPath);
                }

                Color dayColor = d.Month == viewMonth.Month
                    ? (col > 4 ? MixColor(TextColor, AccentColor, .42F) : TextColor)
                    : Color.FromArgb(110, TextColor);
                using (Brush dayBrush = new SolidBrush(dayColor))
                using (Font dayFont = AppFonts.Create(FontSize(10.2F), FontStyle.Regular))
                {
                    string solarText = d.Day.ToString();
                    float solarX = cell.X + 7;
                    float solarY = cell.Y + 6;
                    g.DrawString(solarText, dayFont, dayBrush, solarX, solarY);

                    string lunarText = LunarText(d);
                    float lunarSize = Math.Max(6F, FontSize(7F) * .86F);
                    using (Brush lunarBrush = new SolidBrush(Color.FromArgb(d.Month == viewMonth.Month ? 178 : 88, TextColor)))
                    using (Font lunarFont = AppFonts.Create(lunarSize, FontStyle.Regular))
                    {
                        float solarWidth = g.MeasureString(solarText, dayFont).Width;
                        g.DrawString(lunarText, lunarFont, lunarBrush, solarX + solarWidth + 3, solarY + 2);
                    }
                }

                int allCount = tasks.Count(t => t.Date == Key(d));
                float taskSize = Math.Max(6.8F, Math.Min(FontSize(8F), cellH / 8.2F));
                using (Font measuringFont = AppFonts.Create(taskSize))
                {
                    float taskLineHeight = Math.Max(14F, measuringFont.GetHeight(g) + 3F);
                    float taskTop = cell.Y + Math.Max(31F, FontSize(10.2F) + 19F);
                    float usableHeight = Math.Max(0F, cell.Bottom - 5F - taskTop);
                    int lineCapacity = Math.Max(0, Math.Min(5, (int)Math.Floor(usableHeight / taskLineHeight)));
                    bool needsMoreLine = allCount > lineCapacity;
                    int visibleTaskCount = needsMoreLine ? Math.Max(0, lineCapacity - 1) : Math.Min(allCount, lineCapacity);
                    List<TaskItem> dayTasks = tasks.Where(t => t.Date == Key(d)).Take(visibleTaskCount).ToList();

                    GraphicsState cellState = g.Save();
                    g.SetClip(new Rectangle(cell.X + 2, cell.Y + 2, cell.Width - 4, cell.Height - 4));
                    StringFormat taskFormat = new StringFormat
                    {
                        Trimming = StringTrimming.EllipsisCharacter,
                        FormatFlags = StringFormatFlags.NoWrap,
                        LineAlignment = StringAlignment.Center
                    };

                    for (int n = 0; n < dayTasks.Count; n++)
                    {
                        TaskItem item = dayTasks[n];
                        string text = (item.Done ? "✓ " : (n + 1) + ". ") + item.Title;
                        using (Brush taskBrush = new SolidBrush(item.Done ? Color.FromArgb(145, TextColor) : TextColor))
                        using (Font taskFont = AppFonts.Create(taskSize, item.Done ? FontStyle.Strikeout : FontStyle.Regular))
                        {
                            RectangleF taskRect = new RectangleF(
                                cell.X + 7, taskTop + n * taskLineHeight,
                                cell.Width - 14, taskLineHeight);
                            g.DrawString(text, taskFont, taskBrush, taskRect, taskFormat);
                            Color markerColor = TaskPalette.Get(item, n);
                            float markerWidth = Math.Min(
                                taskRect.Width,
                                Math.Max(20F, g.MeasureString(text, taskFont).Width));
                            using (Pen glow = new Pen(Color.FromArgb(72, markerColor), 5F))
                            using (Pen marker = new Pen(markerColor, 2.7F))
                            {
                                glow.StartCap = LineCap.Round;
                                glow.EndCap = LineCap.Round;
                                marker.StartCap = LineCap.Round;
                                marker.EndCap = LineCap.Round;
                                float markerY = taskRect.Bottom - 1.5F;
                                g.DrawLine(glow, taskRect.X, markerY, taskRect.X + markerWidth, markerY);
                                g.DrawLine(marker, taskRect.X, markerY, taskRect.X + markerWidth, markerY);
                            }
                        }
                    }

                    if (allCount > visibleTaskCount && lineCapacity > 0)
                    {
                        int hiddenCount = allCount - visibleTaskCount;
                        using (Font moreFont = AppFonts.Create(Math.Max(6.5F, taskSize - .6F)))
                        using (Brush moreBrush = new SolidBrush(Color.FromArgb(175, TextColor)))
                        {
                            RectangleF moreRect = new RectangleF(
                                cell.X + 7, taskTop + visibleTaskCount * taskLineHeight,
                                cell.Width - 14, taskLineHeight);
                            g.DrawString("+" + hiddenCount + " 项待办", moreFont, moreBrush, moreRect, taskFormat);
                        }
                    }

                    taskFormat.Dispose();
                    g.Restore(cellState);
                }
            }

            string hintText = locked
                ? "已锁定 · 从托盘解锁后可编辑"
                : "双击日期添加待办 · 拖动顶部移动 · 右键打开设置";
            float hintSize = FontSize(7.6F);
            float hintWidth = Width - 72;
            Font hintFont = AppFonts.Create(hintSize, FontStyle.Regular);
            while (g.MeasureString(hintText, hintFont).Width > hintWidth && hintSize > 6F)
            {
                hintFont.Dispose();
                hintSize -= .25F;
                hintFont = AppFonts.Create(hintSize, FontStyle.Regular);
            }
            using (hintFont)
            using (Brush hintBrush = new SolidBrush(Color.FromArgb(175, TextColor)))
            {
                RectangleF hintRect = new RectangleF(20, Height - 29, hintWidth, 20);
                StringFormat hintFormat = new StringFormat
                {
                    FormatFlags = StringFormatFlags.NoWrap,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(hintText, hintFont, hintBrush, hintRect, hintFormat);
                hintFormat.Dispose();
            }

            if (!locked)
            {
                using (Pen grip = new Pen(Color.FromArgb(120, TextColor), 1))
                {
                    g.DrawLine(grip, Width - 16, Height - 4, Width - 4, Height - 16);
                    g.DrawLine(grip, Width - 11, Height - 4, Width - 4, Height - 11);
                    g.DrawLine(grip, Width - 6, Height - 4, Width - 4, Height - 6);
                }
            }
            textBrush.Dispose();
        }

        void DrawToolbarButton(Graphics g, Rectangle rect, string text)
        {
            bool hot = (rect == prevRect && hoverAction == 2)
                || (rect == nextRect && hoverAction == 3)
                || (rect == closeRect && hoverAction == 4);
            using (Brush soft = new SolidBrush(Color.FromArgb(hot ? 62 : 24, TextColor)))
            using (GraphicsPath path = RoundedRectangle(rect, 7))
                g.FillPath(soft, path);
            using (Brush brush = new SolidBrush(TextColor))
                DrawCentered(g, text, AppFonts.Create(FontSize(12F)), brush, rect);
        }

        GraphicsPath RoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        Color MixColor(Color first, Color second, float amount)
        {
            amount = Math.Max(0F, Math.Min(1F, amount));
            return Color.FromArgb(
                (int)(first.R + (second.R - first.R) * amount),
                (int)(first.G + (second.G - first.G) * amount),
                (int)(first.B + (second.B - first.B) * amount));
        }

        string TrimText(Graphics graphics, string text, Font font, float maxWidth)
        {
            if (graphics.MeasureString(text, font).Width <= maxWidth) return text;
            string suffix = "…";
            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (graphics.MeasureString(text.Substring(0, mid) + suffix, font).Width <= maxWidth)
                    low = mid;
                else
                    high = mid - 1;
            }
            return text.Substring(0, low) + suffix;
        }

        void DrawCentered(Graphics g, string text, Font font, Brush brush, Rectangle rect)
        {
            StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, brush, rect, sf);
            sf.Dispose();
            font.Dispose();
        }

        string LunarText(DateTime date)
        {
            try
            {
                int day = lunar.GetDayOfMonth(date);
                int month = lunar.GetMonth(date);
                string[] nums = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
                if (day == 1) return nums[Math.Min(month, 10)] + "月";
                if (day <= 10) return "初" + nums[day];
                if (day < 20) return "十" + nums[day - 10];
                if (day == 20) return "二十";
                if (day < 30) return "廿" + nums[day - 20];
                return "三十";
            }
            catch { return ""; }
        }

        void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || locked) return;
            int headerH = Math.Max(56, Height / 11);
            if (e.Y < headerH && !prevRect.Contains(e.Location) && !nextRect.Contains(e.Location) && !closeRect.Contains(e.Location))
            {
                if (todayRect.Contains(e.Location)) { GoToday(); return; }
                dragging = true;
                dragOrigin = Cursor.Position;
                formOrigin = Location;
            }
        }

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point delta = new Point(Cursor.Position.X - dragOrigin.X, Cursor.Position.Y - dragOrigin.Y);
                Location = new Point(formOrigin.X + delta.X, formOrigin.Y + delta.Y);
                return;
            }
            int nextHover = -1;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].Contains(e.Location)) { nextHover = i; break; }
            int nextAction = 0;
            if (todayRect.Contains(e.Location)) nextAction = 1;
            else if (prevRect.Contains(e.Location)) nextAction = 2;
            else if (nextRect.Contains(e.Location)) nextAction = 3;
            else if (closeRect.Contains(e.Location)) nextAction = 4;
            if (nextHover != hoverCell || nextAction != hoverAction)
            {
                hoverCell = nextHover;
                hoverAction = nextAction;
                Invalidate();
            }
        }

        void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                dragging = false;
                SnapToEdges();
                SaveData();
            }
            if (e.Button != MouseButtons.Left || locked) return;
            if (prevRect.Contains(e.Location)) ChangeMonth(-1);
            else if (nextRect.Contains(e.Location)) ChangeMonth(1);
            else if (closeRect.Contains(e.Location)) ContextMenuStrip.Show(this, closeRect.Left, closeRect.Bottom);
        }

        void OnDoubleClick(object sender, MouseEventArgs e)
        {
            if (locked || e.Button != MouseButtons.Left) return;
            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i].Contains(e.Location)) continue;
                DateTime first = new DateTime(viewMonth.Year, viewMonth.Month, 1);
                int offset = ((int)first.DayOfWeek + 6) % 7;
                DateTime date = first.AddDays(-offset + i);
                EditDate(date);
                break;
            }
        }

        void EditDate(DateTime date)
        {
            List<TaskItem> old = tasks.Where(t => t.Date == Key(date)).ToList();
            using (ScheduleEditor dialog = new ScheduleEditor(date, old, AccentColor))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                tasks.RemoveAll(t => t.Date == Key(date));
                tasks.AddRange(dialog.ResultTasks);
                viewMonth = new DateTime(date.Year, date.Month, 1);
                SaveData();
                Invalidate();
            }
            Invalidate();
        }

        void ChangeMonth(int amount)
        {
            viewMonth = viewMonth.AddMonths(amount);
            Invalidate();
        }

        void GoToday()
        {
            viewMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            Invalidate();
        }

        void SnapToEdges()
        {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            int threshold = 24;
            int x = Left, y = Top;
            if (Math.Abs(Left - area.Left) < threshold) x = area.Left + 4;
            if (Math.Abs(Right - area.Right) < threshold) x = area.Right - Width - 4;
            if (Math.Abs(Top - area.Top) < threshold) y = area.Top + 4;
            if (Math.Abs(Bottom - area.Bottom) < threshold) y = area.Bottom - Height - 4;
            Location = new Point(x, y);
        }

        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            ToolStripMenuItem today = new ToolStripMenuItem("回到今天");
            today.Click += delegate { GoToday(); };
            ToolStripMenuItem edit = new ToolStripMenuItem("编辑今天的日程");
            edit.Click += delegate { EditDate(DateTime.Today); };
            ToolStripMenuItem lockItem = new ToolStripMenuItem("锁定并允许穿透点击");
            lockItem.Checked = locked;
            lockItem.CheckOnClick = true;
            lockItem.CheckedChanged += delegate { locked = lockItem.Checked; ApplyLockMode(); SaveData(); Invalidate(); };
            ToolStripMenuItem startup = new ToolStripMenuItem("开机自动启动");
            startup.Checked = IsStartupEnabled();
            startup.CheckOnClick = true;
            startup.CheckedChanged += delegate { SetStartup(startup.Checked); };

            ToolStripMenuItem opacityMenu = new ToolStripMenuItem("透明度");
            foreach (int value in new int[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 })
            {
                int val = value;
                ToolStripMenuItem item = new ToolStripMenuItem(value + "%");
                item.Checked = Math.Abs(Opacity * 100 - value) < 1;
                item.Click += delegate { Opacity = val / 100.0; SaveData(); };
                opacityMenu.DropDownItems.Add(item);
            }

            ToolStripMenuItem themeMenu = new ToolStripMenuItem("底色");
            string[] names = { "Win11 浅色", "Win11 深色", "海蓝" };
            for (int i = 0; i < names.Length; i++)
            {
                int value = i;
                ToolStripMenuItem item = new ToolStripMenuItem(names[i]);
                item.Click += delegate { theme = value; autoTextColor = true; ApplyTheme(); SaveData(); };
                themeMenu.DropDownItems.Add(item);
            }
            themeMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem customColor = new ToolStripMenuItem("自定义底色…");
            customColor.Click += delegate { ChooseBackgroundColor(); };
            themeMenu.DropDownItems.Add(customColor);

            ToolStripMenuItem textMenu = new ToolStripMenuItem("文字设置");
            ToolStripMenuItem autoText = new ToolStripMenuItem("自动适配背景");
            autoText.Checked = autoTextColor;
            autoText.CheckOnClick = true;
            autoText.CheckedChanged += delegate
            {
                autoTextColor = autoText.Checked;
                Invalidate();
                SaveData();
            };
            textMenu.DropDownItems.Add(autoText);
            textMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem textColor = new ToolStripMenuItem("文字颜色…");
            textColor.Click += delegate { ChooseTextColor(); };
            textMenu.DropDownItems.Add(textColor);
            ToolStripMenuItem fontSizeMenu = new ToolStripMenuItem("字号比例");
            foreach (int value in new int[] { 85, 100, 115, 130 })
            {
                int val = value;
                ToolStripMenuItem item = new ToolStripMenuItem(value + "%");
                item.Checked = Math.Abs(fontScale * 100 - value) < 1;
                item.Click += delegate { fontScale = val / 100.0; Invalidate(); SaveData(); };
                fontSizeMenu.DropDownItems.Add(item);
            }
            textMenu.DropDownItems.Add(fontSizeMenu);

            ToolStripMenuItem topItem = new ToolStripMenuItem("置顶显示");
            topItem.Checked = alwaysOnTop;
            topItem.CheckOnClick = true;
            topItem.CheckedChanged += delegate
            {
                alwaysOnTop = topItem.Checked;
                TopMost = alwaysOnTop;
                SaveData();
            };
            ToolStripMenuItem hideItem = new ToolStripMenuItem("隐藏日历（双击托盘恢复）");
            hideItem.Click += delegate { Hide(); };

            ToolStripMenuItem sizes = new ToolStripMenuItem("尺寸");
            AddSizeItem(sizes, "紧凑 720 × 470", 720, 470);
            AddSizeItem(sizes, "标准 900 × 590", 900, 590);
            AddSizeItem(sizes, "宽屏 1120 × 680", 1120, 680);

            ToolStripMenuItem exit = new ToolStripMenuItem("退出潮汐日历");
            exit.Click += delegate { exiting = true; Close(); };
            menu.Items.Add(today);
            menu.Items.Add(edit);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(lockItem);
            menu.Items.Add(startup);
            menu.Items.Add(opacityMenu);
            menu.Items.Add(themeMenu);
            menu.Items.Add(textMenu);
            menu.Items.Add(sizes);
            menu.Items.Add(topItem);
            menu.Items.Add(hideItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);
            return menu;
        }

        void AddSizeItem(ToolStripMenuItem parent, string text, int width, int height)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += delegate { Size = new Size(width, height); SnapToEdges(); SaveData(); };
            parent.DropDownItems.Add(item);
        }

        void ChooseBackgroundColor()
        {
            using (ModernColorPicker picker = new ModernColorPicker(
                OverlayColor, "日历背景颜色"))
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                customBackgroundArgb = picker.SelectedColor.ToArgb();
                theme = 3;
                autoTextColor = true;
                ApplyTheme();
                SaveData();
            }
        }

        void ChooseTextColor()
        {
            using (ModernColorPicker picker = new ModernColorPicker(
                TextColor, "日历文字颜色"))
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                foregroundArgb = picker.SelectedColor.ToArgb();
                autoTextColor = false;
                Invalidate();
                SaveData();
            }
        }

        void BuildTray()
        {
            tray.Text = "潮汐日历";
            tray.Icon = SystemIcons.Information;
            tray.Visible = true;
            tray.ContextMenuStrip = BuildMenu();
            tray.DoubleClick += delegate
            {
                locked = false;
                ApplyLockMode();
                RestoreFromExternalLaunch();
            };
        }

        public void RestoreFromExternalLaunch()
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)delegate { RestoreFromExternalLaunch(); });
                return;
            }
            if (Opacity < .1)
            {
                Opacity = .94;
                SaveData();
            }
            Show();
            WindowState = FormWindowState.Normal;
            bool pulseTopMost = !alwaysOnTop;
            if (pulseTopMost) TopMost = true;
            BringToFront();
            Activate();
            SetForegroundWindow(Handle);
            if (pulseTopMost)
                BeginInvoke((MethodInvoker)delegate { TopMost = false; });
        }

        void ApplyLockMode()
        {
            if (!IsHandleCreated) return;
            int ex = GetWindowLong(Handle, GWL_EXSTYLE);
            if (locked) ex |= WS_EX_TRANSPARENT;
            else ex &= ~WS_EX_TRANSPARENT;
            SetWindowLong(Handle, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW);
        }

        void SendBehindWindows()
        {
            // Intentionally left at normal non-topmost z-order. HWND_BOTTOM can
            // place a window behind Progman/WorkerW on some Windows versions,
            // making a desktop overlay invisible behind the wallpaper.
        }

        bool IsStartupEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                return key != null && key.GetValue("TidesCalendar") != null;
        }

        void SetStartup(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (enabled) key.SetValue("TidesCalendar", "\"" + Application.ExecutablePath + "\"");
                    else key.DeleteValue("TidesCalendar", false);
                }
            }
            catch { MessageBox.Show("无法修改开机启动设置。", "潮汐日历"); }
        }

        void OnClosing(object sender, FormClosingEventArgs e)
        {
            SaveData();
            tray.Visible = false;
        }

        static string Key(DateTime date) { return date.ToString("yyyy-MM-dd"); }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != WM_NCHITTEST || locked) return;
            Point point = PointToClient(new Point((short)((long)m.LParam & 0xFFFF), (short)(((long)m.LParam >> 16) & 0xFFFF)));
            int edge = 9;
            bool left = point.X <= edge;
            bool right = point.X >= ClientSize.Width - edge;
            bool top = point.Y <= edge;
            bool bottom = point.Y >= ClientSize.Height - edge;
            if (left && top) m.Result = (IntPtr)HTTOPLEFT;
            else if (right && top) m.Result = (IntPtr)HTTOPRIGHT;
            else if (left && bottom) m.Result = (IntPtr)HTBOTTOMLEFT;
            else if (right && bottom) m.Result = (IntPtr)HTBOTTOMRIGHT;
            else if (left) m.Result = (IntPtr)HTLEFT;
            else if (right) m.Result = (IntPtr)HTRIGHT;
            else if (top) m.Result = (IntPtr)HTTOP;
            else if (bottom) m.Result = (IntPtr)HTBOTTOM;
        }
    }

    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int processId);
        [DllImport("user32.dll")] static extern IntPtr GetProcessWindowStation();
        [DllImport("user32.dll")] static extern IntPtr GetThreadDesktop(uint threadId);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetUserObjectInformation(
            IntPtr handle, int index, StringBuilder information, int length, out int needed);

        static string DesktopScope()
        {
            string station = UserObjectName(GetProcessWindowStation());
            string desktop = UserObjectName(GetThreadDesktop(GetCurrentThreadId()));
            string raw = station + "_" + desktop;
            StringBuilder safe = new StringBuilder();
            foreach (char c in raw)
                safe.Append(char.IsLetterOrDigit(c) ? c : '_');
            return safe.Length > 0 ? safe.ToString() : "DefaultDesktop";
        }

        static string UserObjectName(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return "";
            StringBuilder name = new StringBuilder(256);
            int needed;
            return GetUserObjectInformation(handle, 2, name, name.Capacity * 2, out needed)
                ? name.ToString()
                : "";
        }

        [STAThread]
        static void Main()
        {
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(true);
            AppFonts.Initialize();
            string desktopScope = DesktopScope();
            bool created;
            using (System.Threading.Mutex mutex = new System.Threading.Mutex(
                true, "TidesCalendarSingleton_" + desktopScope, out created))
            using (System.Threading.EventWaitHandle activationSignal = new System.Threading.EventWaitHandle(
                false, System.Threading.EventResetMode.AutoReset,
                "TidesCalendarActivationSignal_" + desktopScope))
            {
                if (!created)
                {
                    try
                    {
                        System.Diagnostics.Process current = System.Diagnostics.Process.GetCurrentProcess();
                        foreach (System.Diagnostics.Process process in
                            System.Diagnostics.Process.GetProcessesByName(current.ProcessName))
                        {
                            if (process.Id != current.Id)
                            {
                                AllowSetForegroundWindow(process.Id);
                                break;
                            }
                        }
                    }
                    catch { }
                    activationSignal.Set();
                    return;
                }
                CalendarOverlay overlay = new CalendarOverlay();
                System.Threading.RegisteredWaitHandle activationWait =
                    System.Threading.ThreadPool.RegisterWaitForSingleObject(
                        activationSignal,
                        delegate(object state, bool timedOut)
                        {
                            try
                            {
                                if (!overlay.IsDisposed && overlay.IsHandleCreated)
                                    overlay.BeginInvoke((MethodInvoker)delegate
                                    {
                                        overlay.RestoreFromExternalLaunch();
                                    });
                            }
                            catch { }
                        },
                        null, System.Threading.Timeout.Infinite, false);
                try
                {
                    overlay.Show();
                    overlay.WindowState = FormWindowState.Normal;
                    overlay.BringToFront();
                    Application.Run(overlay);
                }
                finally
                {
                    activationWait.Unregister(null);
                }
            }
        }
    }
}
