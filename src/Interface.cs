// Pénombre - night mode for Dofus 3
// Copyright (C) 2026 Sokenzane
//
// This program is free software: you can redistribute it and/or modify it under the terms of
// the GNU General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version. It is distributed WITHOUT ANY WARRANTY;
// see the GNU General Public License for more details: <https://www.gnu.org/licenses/>.
//
// The look of the settings window and of the menu: slate panels in relief, green actions and
// glowing gems, in the spirit of the Dofus 3 interface. Every control here is drawn by hand.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Penombre
{
    // ================================================================ Colors, fonts, helpers

    static class Theme
    {
        public static readonly Color Edge = Rgb(0x16151b), Frame = Rgb(0x6a6878), Panel = Rgb(0x2c2b34),
            Rail = Rgb(0x26252d), Line = Rgb(0x3c3a46), Raised = Rgb(0x45444f), RaisedLow = Rgb(0x2a2931),
            Field = Rgb(0x1f1e25), Text = Rgb(0xecebf2), Muted = Rgb(0xa9a6b5), Green = Rgb(0x8fbf3a),
            GreenHi = Rgb(0xc6ea6a), GreenMid = Rgb(0x6a9a2a), GreenDark = Rgb(0x4a7a1a), GreenSelected = Rgb(0x34402a),
            Violet = Rgb(0x6a5aa8), Red = Rgb(0x8a3a3a), RedDark = Rgb(0x5a1f22);

        static Color Rgb(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        // Display scale of the settings window (1 = 96 dpi).
        public static float Scale = 1;
        public static int Px(float v) { return (int)Math.Round(v * Scale); }

        // ---- embedded fonts (Nunito, SIL Open Font License 1.1)

        [DllImport("gdi32.dll")] static extern IntPtr AddFontMemResourceEx(IntPtr data, uint length, IntPtr reserved, ref uint fonts);

        static readonly PrivateFontCollection collection = new PrivateFontCollection();
        static FontFamily bold, extraBold, black;
        public static bool FontsLoaded;

        static Theme()
        {
            try
            {
                foreach (string name in new[] { "Nunito-Bold.ttf", "Nunito-ExtraBold.ttf", "Nunito-Black.ttf" })
                {
                    byte[] data;
                    using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                    {
                        if (s == null) throw new FileNotFoundException(name);
                        data = new byte[s.Length];
                        int read = 0;
                        while (read < data.Length) read += s.Read(data, read, data.Length - read);
                    }
                    // The memory stays allocated: the font collection and GDI use it until the app quits.
                    IntPtr mem = Marshal.AllocCoTaskMem(data.Length);
                    Marshal.Copy(data, 0, mem, data.Length);
                    collection.AddMemoryFont(mem, data.Length);
                    uint count = 0;
                    AddFontMemResourceEx(mem, (uint)data.Length, IntPtr.Zero, ref count); // for GDI (labels)
                }
                foreach (FontFamily f in collection.Families)
                {
                    if (f.Name == "Nunito") bold = f;
                    else if (f.Name == "Nunito ExtraBold") extraBold = f;
                    else if (f.Name == "Nunito Black") black = f;
                }
                FontsLoaded = bold != null && extraBold != null && black != null;
                if (!FontsLoaded) Log.Info("Embedded fonts: missing families, Segoe UI used");
            }
            catch (Exception ex)
            {
                Log.Error("Embedded fonts", ex);
            }
        }

        // Regular text (Nunito Bold), labels and buttons (ExtraBold), titles (Black).
        public static Font Body(float size) { return bold != null ? new Font(bold, size, FontStyle.Bold) : new Font("Segoe UI", size); }
        public static Font Strong(float size) { return extraBold != null ? new Font(extraBold, size) : new Font("Segoe UI Semibold", size); }
        public static Font Title(float size) { return black != null ? new Font(black, size) : new Font("Segoe UI", size, FontStyle.Bold); }
        public static FontFamily TitleFamily { get { return black ?? new FontFamily("Segoe UI"); } }

        // ---- drawing helpers

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillVertical(Graphics g, GraphicsPath path, RectangleF r, Color top, Color bottom)
        {
            using (var b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), top, bottom, LinearGradientMode.Vertical))
                g.FillPath(b, path);
        }

        public static void FillVertical(Graphics g, GraphicsPath path, RectangleF r, Color top, Color middle, Color bottom)
        {
            using (var b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), top, bottom, LinearGradientMode.Vertical))
            {
                b.InterpolationColors = new ColorBlend { Colors = new[] { top, middle, bottom }, Positions = new[] { 0f, 0.5f, 1f } };
                g.FillPath(b, path);
            }
        }

        // A soft glow around a shape: a few widening transparent outlines.
        public static void Glow(Graphics g, GraphicsPath path, Color color, float size)
        {
            for (int i = 3; i >= 1; i--)
                using (var pen = new Pen(Color.FromArgb(28 + 18 * (3 - i), color), size * i / 1.5f) { LineJoin = LineJoin.Round })
                    g.DrawPath(pen, path);
        }

        public static void Diamond(Graphics g, float cx, float cy, float r, Color color, bool glow)
        {
            using (var p = new GraphicsPath())
            {
                p.AddPolygon(new[] { new PointF(cx, cy - r), new PointF(cx + r, cy), new PointF(cx, cy + r), new PointF(cx - r, cy) });
                if (glow) Glow(g, p, color, r * 0.8f);
                using (var b = new SolidBrush(color)) g.FillPath(b, p);
            }
        }

        // Upper case text with extra spacing between letters, as the game's small headings.
        static readonly Dictionary<string, int> advances = new Dictionary<string, int>();

        // Width a letter takes in a line (without the side bearings MeasureText adds to a lone letter).
        static int Advance(char c, Font font)
        {
            string key = font.Name + font.SizeInPoints + c;
            int a;
            if (advances.TryGetValue(key, out a)) return a;
            string one = c.ToString(), two = one + one;
            a = TextRenderer.MeasureText(two, font, Size.Empty, TextFormatFlags.NoPadding).Width
              - TextRenderer.MeasureText(one, font, Size.Empty, TextFormatFlags.NoPadding).Width;
            advances[key] = a;
            return a;
        }

        public static Size MeasureSpaced(string text, Font font, float spacing)
        {
            int w = 0;
            foreach (char c in text) w += Advance(c, font) + (int)Math.Round(spacing);
            int h = TextRenderer.MeasureText("Ag", font, Size.Empty, TextFormatFlags.NoPadding).Height;
            return new Size(Math.Max(0, w - (int)Math.Round(spacing)), h);
        }

        public static void DrawSpaced(Graphics g, string text, Font font, Color color, int x, int y, float spacing, bool shadow)
        {
            foreach (char c in text)
            {
                string s = c.ToString();
                if (shadow) TextRenderer.DrawText(g, s, font, new Point(x, y + 1), Color.Black, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, s, font, new Point(x, y), color, TextFormatFlags.NoPadding);
                x += Advance(c, font) + (int)Math.Round(spacing);
            }
        }

        // Applies the colors to the standard controls of a window (the custom ones draw themselves).
        public static void Style(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is TextBox)
                {
                    c.BackColor = Field; c.ForeColor = Text;
                    ((TextBox)c).BorderStyle = BorderStyle.FixedSingle;
                }
                else if (c is NumericUpDown)
                {
                    c.BackColor = Field; c.ForeColor = Text;
                    ((NumericUpDown)c).BorderStyle = BorderStyle.FixedSingle;
                }
                Style(c);
            }
        }

        public static void Hand(Control c)
        {
            c.Cursor = Cursors.Hand;
        }
    }

    // ================================================================ Notification area icons

    // The icons come from one picture: 3 styles x 4 states (rows), drawn at 6 sizes (blocks).
    static class TrayIcons
    {
        public const int StyleCount = 3, StateCount = 4;
        public const int Night = 0, Twilight = 1, Day = 2, Off = 3;
        static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48 };
        static Bitmap sheet;
        static bool loaded;
        static readonly Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();

        static Bitmap Sheet
        {
            get
            {
                if (!loaded)
                {
                    loaded = true;
                    try
                    {
                        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("tray-icons.png"))
                            if (s != null) using (var img = Image.FromStream(s)) sheet = new Bitmap(img);
                    }
                    catch (Exception ex) { Log.Error("Tray icons", ex); }
                }
                return sheet;
            }
        }

        public static bool Ready { get { return Sheet != null; } }

        // The picture of one icon at the given size (null when the picture is missing).
        public static Bitmap Picture(int style, int state, int size)
        {
            style = Math.Max(0, Math.Min(StyleCount - 1, style));
            state = Math.Max(0, Math.Min(StateCount - 1, state));
            string key = style + "," + state + "," + size;
            Bitmap cached;
            if (cache.TryGetValue(key, out cached)) return cached;
            if (Sheet == null) return null;

            int block = Sizes.Length - 1;
            for (int i = 0; i < Sizes.Length; i++) if (Sizes[i] >= size) { block = i; break; }
            int x = 0;
            for (int i = 0; i < block; i++) x += Sizes[i];
            int src = Sizes[block], row = (style * StateCount + state) * Sizes[Sizes.Length - 1];

            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(Sheet, new Rectangle(0, 0, size, size), new Rectangle(x, row, src, src), GraphicsUnit.Pixel);
            }
            cache[key] = bmp;
            return bmp;
        }

        public static Icon Make(int style, int state, int size)
        {
            Bitmap bmp = Picture(style, state, size);
            if (bmp == null) return null;
            IntPtr h = bmp.GetHicon();
            Icon icon = (Icon)Icon.FromHandle(h).Clone();
            Native.DestroyIcon(h);
            return icon;
        }
    }

    // ================================================================ Controls

    // Button in relief: green for the main action, red for closing, slate otherwise.
    class GameButton : Button
    {
        public enum Kind { Normal, Primary, Close }
        public Kind Variant;
        bool hover, pressed;

        public GameButton(string text, Kind look)
        {
            Text = text;
            Variant = look;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            Font = Theme.Strong(look == Kind.Primary ? 10f : 8.5f);
            Theme.Hand(this);
            AutoSize = look != Kind.Close;
            if (look == Kind.Close) Size = new Size(Theme.Px(32), Theme.Px(32));
            Margin = new Padding(Theme.Px(3), Theme.Px(3), Theme.Px(6), Theme.Px(3));
            AccessibleName = look == Kind.Close ? T.S("close") : text;
        }

        float Spacing { get { return Variant == Kind.Primary ? Theme.Px(2) : Theme.Px(1.5f); } }

        public override Size GetPreferredSize(Size proposed)
        {
            if (Variant == Kind.Close) return Size;
            Size t = Theme.MeasureSpaced(Text.ToUpperInvariant(), Font, Spacing);
            int pad = Variant == Kind.Primary ? Theme.Px(28) : Theme.Px(16);
            return new Size(t.Width + pad * 2, Theme.Px(Variant == Kind.Primary ? 40 : 34));
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float inset = Variant == Kind.Primary ? Theme.Px(4) : Theme.Px(1);
            var r = new RectangleF(inset, inset, Width - inset * 2 - 1, Height - inset * 2 - 1 - Theme.Px(2));
            if (pressed) r.Offset(0, Theme.Px(1));
            float radius = Theme.Px(6);
            Color top, mid, bottom, border, text = Color.White;
            switch (Variant)
            {
                case Kind.Primary:
                    top = hover ? Color.FromArgb(0xb8, 0xe8, 0x5a) : Color.FromArgb(0xa8, 0xd8, 0x4a);
                    mid = Theme.GreenMid; bottom = Theme.GreenDark; border = Color.FromArgb(0xd8, 0xf5, 0x90);
                    break;
                case Kind.Close:
                    top = hover ? Color.FromArgb(0xaa, 0x4a, 0x4a) : Theme.Red; mid = top; bottom = Theme.RedDark; border = Color.FromArgb(0x5a, 0x2a, 0x2a);
                    text = Color.FromArgb(0xff, 0xe6, 0xe6);
                    break;
                default:
                    top = hover ? Color.FromArgb(0x55, 0x54, 0x60) : Theme.Raised; mid = Color.FromArgb(0x38, 0x37, 0x40); bottom = Theme.RaisedLow;
                    border = hover ? Theme.GreenHi : Color.FromArgb(0x5a, 0x58, 0x66); text = Theme.Text;
                    break;
            }
            if (!Enabled) { top = mid = bottom = Theme.RaisedLow; border = Theme.Line; text = Theme.Muted; }

            using (GraphicsPath shadow = Theme.Round(new RectangleF(r.X, r.Y + Theme.Px(2), r.Width, r.Height), radius))
            using (var b = new SolidBrush(Variant == Kind.Primary ? Color.FromArgb(0x1a, 0x2a, 0x08) : Theme.Edge))
                if (!pressed) g.FillPath(b, shadow);
            using (GraphicsPath body = Theme.Round(r, radius))
            {
                if (Variant == Kind.Primary && Enabled) Theme.Glow(g, body, Color.FromArgb(0xa8, 0xd8, 0x4a), Theme.Px(3));
                Theme.FillVertical(g, body, r, top, mid, bottom);
                using (var pen = new Pen(border)) g.DrawPath(pen, body);
                using (var pen = new Pen(Color.FromArgb(Variant == Kind.Primary ? 110 : 40, Color.White)))
                    g.DrawLine(pen, r.X + radius, r.Y + 1, r.Right - radius, r.Y + 1);
            }

            if (Variant == Kind.Close)
            {
                float c = Theme.Px(5);
                float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
                using (var pen = new Pen(text, Theme.Px(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(pen, cx - c, cy - c, cx + c, cy + c);
                    g.DrawLine(pen, cx - c, cy + c, cx + c, cy - c);
                }
            }
            else
            {
                string s = Text.ToUpperInvariant();
                Size t = Theme.MeasureSpaced(s, Font, Spacing);
                Theme.DrawSpaced(g, s, Font, text, (int)(r.X + (r.Width - t.Width) / 2), (int)(r.Y + (r.Height - t.Height) / 2), Spacing, Variant == Kind.Primary);
            }
            if (Focused && ShowFocusCues)
                using (GraphicsPath f = Theme.Round(RectangleF.Inflate(r, -Theme.Px(3), -Theme.Px(3)), radius / 2))
                using (var pen = new Pen(Theme.GreenHi) { DashStyle = DashStyle.Dot })
                    g.DrawPath(pen, f);
        }
    }

    // Slider: a dug groove, a glowing green fill and a gem as the handle.
    class GameSlider : Control
    {
        int min, max = 100, value;
        public int SmallChange = 1, LargeChange = 5;
        public bool Spectrum;               // blue (cold) to orange (warm) groove, no fill
        public event EventHandler ValueChanged;
        bool dragging, hover;

        public GameSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Size = new Size(Theme.Px(200), Theme.Px(26));
            Margin = new Padding(Theme.Px(3), Theme.Px(4), Theme.Px(3), Theme.Px(4));
            AccessibleRole = AccessibleRole.Slider;
            Theme.Hand(this);
        }

        public int Minimum { get { return min; } set { min = value; if (max < min) max = min; Value = this.value; Invalidate(); } }
        public int Maximum { get { return max; } set { max = value; if (min > max) min = max; Value = this.value; Invalidate(); } }

        public int Value
        {
            get { return value; }
            set
            {
                int v = Math.Max(min, Math.Min(max, value));
                if (v == this.value) return;
                this.value = v;
                AccessibleDescription = v.ToString();
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        RectangleF Groove
        {
            get
            {
                float knob = Theme.Px(16), h = Theme.Px(6);
                return new RectangleF(knob / 2 + 1, (Height - h) / 2f, Width - knob - 2, h);
            }
        }

        float KnobX { get { RectangleF gr = Groove; return gr.X + (max == min ? 0 : (value - min) * gr.Width / (max - min)); } }

        void SetFromMouse(int x)
        {
            RectangleF gr = Groove;
            float t = Math.Max(0, Math.Min(1, (x - gr.X) / Math.Max(1, gr.Width)));
            int v = min + (int)Math.Round(t * (max - min));
            if (SmallChange > 1) v = min + (int)Math.Round((v - min) / (double)SmallChange) * SmallChange;
            Value = v;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            Focus();
            dragging = true;
            Capture = true;
            SetFromMouse(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) SetFromMouse(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; Capture = false; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (Enabled && Focused) Value += Math.Sign(e.Delta) * SmallChange;
        }

        protected override bool IsInputKey(Keys k)
        {
            switch (k & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: return true;
            }
            return base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Down: Value -= SmallChange; break;
                case Keys.Right: case Keys.Up: Value += SmallChange; break;
                case Keys.PageDown: Value -= LargeChange; break;
                case Keys.PageUp: Value += LargeChange; break;
                case Keys.Home: Value = min; break;
                case Keys.End: Value = max; break;
                default: return;
            }
            e.Handled = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF gr = Groove;
            float kx = KnobX;

            using (GraphicsPath groove = Theme.Round(gr, gr.Height / 2))
            {
                if (Spectrum)
                {
                    using (var b = new LinearGradientBrush(gr, Color.FromArgb(0x52, 0xa0, 0xe0), Color.FromArgb(0xe0, 0x88, 0x4a), LinearGradientMode.Horizontal))
                    {
                        b.InterpolationColors = new ColorBlend
                        {
                            Colors = new[] { Color.FromArgb(0x52, 0xa0, 0xe0), Theme.Text, Color.FromArgb(0xe0, 0x88, 0x4a) },
                            Positions = new[] { 0f, 0.5f, 1f },
                        };
                        g.FillPath(b, groove);
                    }
                    if (!Enabled) using (var b = new SolidBrush(Color.FromArgb(150, Theme.Panel))) g.FillPath(b, groove);
                }
                else
                {
                    using (var b = new SolidBrush(Theme.Edge)) g.FillPath(b, groove);
                    using (var pen = new Pen(Color.FromArgb(20, Color.White))) g.DrawLine(pen, gr.X + gr.Height / 2, gr.Bottom + 1, gr.Right - gr.Height / 2, gr.Bottom + 1);
                    var fill = new RectangleF(gr.X, gr.Y, Math.Max(gr.Height, kx - gr.X), gr.Height);
                    using (GraphicsPath f = Theme.Round(fill, gr.Height / 2))
                    {
                        if (Enabled)
                        {
                            Theme.Glow(g, f, Theme.GreenHi, Theme.Px(2));
                            using (var b = new LinearGradientBrush(new RectangleF(fill.X - 1, fill.Y, fill.Width + 2, fill.Height), Theme.GreenDark, Theme.GreenHi, LinearGradientMode.Horizontal))
                                g.FillPath(b, f);
                        }
                        else using (var b = new SolidBrush(Theme.Line)) g.FillPath(b, f);
                    }
                }
            }

            float d = Theme.Px(16) + (hover || dragging ? Theme.Px(2) : 0);
            var knob = new RectangleF(kx - d / 2, Height / 2f - d / 2, d, d);
            using (var p = new GraphicsPath())
            {
                p.AddEllipse(knob);
                if (Enabled && (Focused || hover || dragging)) Theme.Glow(g, p, Theme.GreenHi, Theme.Px(3));
                using (var b = new PathGradientBrush(p))
                {
                    b.CenterPoint = new PointF(knob.X + knob.Width * 0.35f, knob.Y + knob.Height * 0.3f);
                    b.CenterColor = Color.White;
                    b.SurroundColors = new[] { Enabled ? Theme.GreenMid : Theme.Muted };
                    g.FillPath(b, p);
                }
                using (var pen = new Pen(Theme.Edge, Theme.Px(2))) g.DrawEllipse(pen, knob);
            }
        }
    }

    // Check box and radio button: a dark box that lights up green.
    class GameCheck : CheckBox
    {
        public GameCheck(string text)
        {
            Text = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.Body(9.5f);
            AutoSize = true;
            Margin = new Padding(Theme.Px(3), Theme.Px(5), Theme.Px(3), Theme.Px(4));
            Theme.Hand(this);
        }

        public override Size GetPreferredSize(Size proposed) { return Toggle.Preferred(this); }
        protected override void OnPaint(PaintEventArgs e) { Toggle.Paint(this, e.Graphics, Checked, false); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    }

    class GameRadio : RadioButton
    {
        public GameRadio(string text)
        {
            Text = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.Body(9.5f);
            AutoSize = true;
            Margin = new Padding(Theme.Px(3), Theme.Px(8), Theme.Px(3), Theme.Px(2));
            Theme.Hand(this);
        }

        public override Size GetPreferredSize(Size proposed) { return Toggle.Preferred(this); }
        protected override void OnPaint(PaintEventArgs e) { Toggle.Paint(this, e.Graphics, Checked, true); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
    }

    static class Toggle
    {
        public static Size Preferred(ButtonBase c)
        {
            Size t = TextRenderer.MeasureText(c.Text, c.Font, Size.Empty, TextFormatFlags.NoPadding);
            return new Size(Theme.Px(18) + Theme.Px(10) + t.Width + 2, Math.Max(Theme.Px(22), t.Height + 4));
        }

        public static void Paint(ButtonBase c, Graphics g, bool on, bool round)
        {
            g.Clear(c.Parent != null ? c.Parent.BackColor : Theme.Panel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Theme.Px(18);
            var box = new RectangleF(1, (c.Height - s) / 2f, s - 2, s - 2);
            using (var p = new GraphicsPath())
            {
                if (round) p.AddEllipse(box);
                else using (GraphicsPath rounded = Theme.Round(box, Theme.Px(4))) p.AddPath(rounded, false);
                if (on && c.Enabled)
                {
                    Theme.Glow(g, p, Theme.GreenHi, Theme.Px(2));
                    if (round) using (var b = new SolidBrush(Theme.Field)) g.FillPath(b, p);
                    else Theme.FillVertical(g, p, box, Color.FromArgb(0xa8, 0xd8, 0x4a), Theme.GreenDark);
                }
                else using (var b = new SolidBrush(Theme.Field)) g.FillPath(b, p);
                using (var pen = new Pen(on && c.Enabled ? Theme.GreenHi : Theme.Frame)) g.DrawPath(pen, p);
            }
            if (on)
            {
                Color mark = c.Enabled ? (round ? Theme.GreenHi : Color.White) : Theme.Muted;
                if (round)
                {
                    float d = s * 0.45f;
                    using (var b = new SolidBrush(mark)) g.FillEllipse(b, box.X + (box.Width - d) / 2, box.Y + (box.Height - d) / 2, d, d);
                }
                else
                    using (var pen = new Pen(mark, Theme.Px(2.2f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        g.DrawLines(pen, new[]
                        {
                            new PointF(box.X + box.Width * 0.24f, box.Y + box.Height * 0.52f),
                            new PointF(box.X + box.Width * 0.43f, box.Y + box.Height * 0.72f),
                            new PointF(box.X + box.Width * 0.78f, box.Y + box.Height * 0.30f),
                        });
            }
            Size t = TextRenderer.MeasureText(c.Text, c.Font, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, c.Text, c.Font, new Point((int)s + Theme.Px(10), (c.Height - t.Height) / 2),
                                  c.Enabled ? Theme.Text : Theme.Muted, TextFormatFlags.NoPadding);
            if (c.Focused)
                using (var pen = new Pen(Theme.GreenHi) { DashStyle = DashStyle.Dot })
                    g.DrawRectangle(pen, (int)s + Theme.Px(7), 0, c.Width - (int)s - Theme.Px(8), c.Height - 1);
        }
    }

    // Drop-down list on a dark field with a green selection.
    class GameCombo : ComboBox
    {
        const int WM_PAINT = 0x000F;

        public GameCombo(int width)
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            BackColor = Theme.Field;
            ForeColor = Theme.Text;
            Font = Theme.Body(9.5f);
            ItemHeight = Theme.Px(22);
            Width = Theme.Px(width);
            Margin = new Padding(Theme.Px(3), Theme.Px(4), Theme.Px(3), Theme.Px(4));
            Theme.Hand(this);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool edit = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool selected = (e.State & DrawItemState.Selected) != 0 && !edit;
            using (var b = new SolidBrush(selected ? Theme.GreenSelected : Theme.Field)) e.Graphics.FillRectangle(b, e.Bounds);
            Color text = !Enabled ? Theme.Muted : selected ? Theme.GreenHi : Theme.Text;
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font,
                                  new Rectangle(e.Bounds.X + Theme.Px(6), e.Bounds.Y, e.Bounds.Width - Theme.Px(6), e.Bounds.Height),
                                  text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != WM_PAINT) return;
            // Redraw the frame and the arrow over the system ones.
            using (Graphics g = Graphics.FromHwnd(Handle))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int w = Theme.Px(22);
                var arrow = new Rectangle(Width - w - 1, 1, w, Height - 2);
                using (var b = new SolidBrush(Theme.Field)) g.FillRectangle(b, arrow);
                float cx = arrow.X + arrow.Width / 2f, cy = Height / 2f, a = Theme.Px(4);
                using (var pen = new Pen(Enabled ? Theme.GreenHi : Theme.Muted, Theme.Px(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLines(pen, new[] { new PointF(cx - a, cy - a / 2), new PointF(cx, cy + a / 2), new PointF(cx + a, cy - a / 2) });
                g.SmoothingMode = SmoothingMode.None;
                using (var pen = new Pen(Focused ? Theme.GreenHi : Color.FromArgb(0x5a, 0x58, 0x66))) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }

    // Hour and minutes, typed or changed with the arrows and the mouse wheel.
    class TimeField : Control
    {
        int minutes;
        bool onMinutes;
        string typed = "";
        public event EventHandler ValueChanged;

        public TimeField()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            TabStop = true;
            Font = Theme.Strong(10f);
            Size = new Size(Theme.Px(78), Theme.Px(28));
            Margin = new Padding(Theme.Px(3), Theme.Px(3), Theme.Px(3), Theme.Px(3));
            AccessibleRole = AccessibleRole.SpinButton;
        }

        public DateTime Value
        {
            get { return DateTime.Today.AddMinutes(minutes); }
            set { SetMinutes(value.Hour * 60 + value.Minute); }
        }

        void SetMinutes(int m)
        {
            m = ((m % 1440) + 1440) % 1440;
            if (m == minutes) { Invalidate(); return; }
            minutes = m;
            AccessibleDescription = (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        void Step(int sign) { typed = ""; SetMinutes(minutes + sign * (onMinutes ? 5 : 60)); }

        protected override bool IsInputKey(Keys k)
        {
            switch (k & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: return true;
            }
            return base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Up: Step(1); break;
                case Keys.Down: Step(-1); break;
                case Keys.Left: onMinutes = false; typed = ""; Invalidate(); break;
                case Keys.Right: onMinutes = true; typed = ""; Invalidate(); break;
                default: return;
            }
            e.Handled = true;
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (e.KeyChar == ':' ) { onMinutes = true; typed = ""; Invalidate(); return; }
            if (e.KeyChar < '0' || e.KeyChar > '9') return;
            typed += e.KeyChar;
            int n = int.Parse(typed);
            if (onMinutes) SetMinutes(minutes / 60 * 60 + Math.Min(59, n));
            else SetMinutes(Math.Min(23, n) * 60 + minutes % 60);
            if (typed.Length >= 2) { typed = ""; if (!onMinutes) onMinutes = true; }
            Invalidate();
            e.Handled = true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            onMinutes = e.X > Width / 2;
            typed = "";
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); if (Enabled) Step(Math.Sign(e.Delta)); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { typed = ""; Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.Round(r, Theme.Px(5)))
            {
                using (var b = new SolidBrush(Theme.Field)) g.FillPath(b, p);
                using (var pen = new Pen(Focused ? Theme.GreenHi : Color.FromArgb(0x5a, 0x58, 0x66))) g.DrawPath(pen, p);
            }
            string h = (minutes / 60).ToString("00"), m = (minutes % 60).ToString("00");
            Color on = Enabled ? Theme.GreenHi : Theme.Muted, off = Enabled ? Theme.Text : Theme.Muted;
            Size hs = TextRenderer.MeasureText(h, Font, Size.Empty, TextFormatFlags.NoPadding);
            Size cs = TextRenderer.MeasureText(":", Font, Size.Empty, TextFormatFlags.NoPadding);
            int total = hs.Width * 2 + cs.Width + Theme.Px(4);
            int x = (Width - total) / 2, y = (Height - hs.Height) / 2;
            TextRenderer.DrawText(g, h, Font, new Point(x, y), Focused && !onMinutes ? on : off, TextFormatFlags.NoPadding);
            x += hs.Width + Theme.Px(2);
            TextRenderer.DrawText(g, ":", Font, new Point(x, y), off, TextFormatFlags.NoPadding);
            x += cs.Width + Theme.Px(2);
            TextRenderer.DrawText(g, m, Font, new Point(x, y), Focused && onMinutes ? on : off, TextFormatFlags.NoPadding);
        }
    }

    // Section heading: a green diamond, the title in spaced capitals and a fading line.
    class SectionLabel : Control
    {
        public SectionLabel(string text)
        {
            Text = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Font = Theme.Strong(8.5f);
            Height = Theme.Px(28);
            MinimumSize = new Size(Theme.Px(240), Height);
            AccessibleRole = AccessibleRole.StaticText;
        }

        public override Size GetPreferredSize(Size proposed)
        {
            Size t = Theme.MeasureSpaced(Text.ToUpperInvariant(), Font, Theme.Px(2));
            return new Size(Math.Max(MinimumSize.Width, t.Width + Theme.Px(40)), Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            string s = Text.ToUpperInvariant();
            Size t = Theme.MeasureSpaced(s, Font, Theme.Px(2));
            int y = Height - t.Height - Theme.Px(4);
            Theme.Diamond(g, Theme.Px(4), y + t.Height / 2f, Theme.Px(3.5f), Theme.GreenHi, false);
            Theme.DrawSpaced(g, s, Font, Color.FromArgb(0xe2, 0xdf, 0xf0), Theme.Px(14), y, Theme.Px(2), true);
            int lx = Theme.Px(14) + t.Width + Theme.Px(10);
            if (lx < Width - 4)
                using (var b = new LinearGradientBrush(new Rectangle(lx, 0, Width - lx, 1), Color.FromArgb(0x5a, 0x58, 0x66), Color.FromArgb(0, 0x5a, 0x58, 0x66), LinearGradientMode.Horizontal))
                    g.FillRectangle(b, lx, y + t.Height / 2, Width - lx, 1);
        }
    }

    // Square button of the page rail, with a line icon.
    class RailButton : Control
    {
        public readonly int Index;
        bool active, hover;
        public event EventHandler Selected;

        public RailButton(int index, string title)
        {
            Index = index;
            Text = title;
            AccessibleName = title;
            AccessibleRole = AccessibleRole.PageTab;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
            TabStop = true;
            Size = new Size(Theme.Px(44), Theme.Px(44));
            Margin = new Padding(Theme.Px(10), Theme.Px(4), Theme.Px(10), Theme.Px(4));
            Theme.Hand(this);
        }

        public bool Active { get { return active; } set { active = value; Invalidate(); } }

        void Fire() { if (Selected != null) Selected(this, EventArgs.Empty); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Focus(); Fire(); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { Fire(); e.Handled = true; }
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Rail);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(Theme.Px(3), Theme.Px(3), Width - Theme.Px(6) - 1, Height - Theme.Px(6) - 1);
            using (GraphicsPath p = Theme.Round(r, Theme.Px(6)))
            {
                if (active) Theme.Glow(g, p, Theme.GreenHi, Theme.Px(3));
                if (active) Theme.FillVertical(g, p, r, Color.FromArgb(0x5a, 0x7a, 0x2a), Color.FromArgb(0x34, 0x46, 0x1c));
                else Theme.FillVertical(g, p, r, hover ? Color.FromArgb(0x48, 0x47, 0x54) : Color.FromArgb(0x3a, 0x39, 0x44), Theme.RaisedLow);
                using (var pen = new Pen(active || Focused ? Theme.GreenHi : Color.FromArgb(0x4a, 0x48, 0x55))) g.DrawPath(pen, p);
            }
            float u = Theme.Px(22) / 24f;  // icons are drawn in a 24 x 24 box
            float ox = (Width - 24 * u) / 2, oy = (Height - 24 * u) / 2;
            using (var pen = new Pen(active ? Color.FromArgb(0xf0, 0xff, 0xc8) : Color.FromArgb(0xcf, 0xcc, 0xe0), 2 * u) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.TranslateTransform(ox, oy);
                g.ScaleTransform(u, u);
                pen.Width = 2;
                DrawIcon(g, pen, Index);
                g.ResetTransform();
            }
        }

        static void DrawIcon(Graphics g, Pen pen, int index)
        {
            switch (index)
            {
                case 0: // mood: crescent
                    using (var p = new GraphicsPath())
                    {
                        p.AddArc(4, 4, 16, 16, 300, 300);
                        p.AddArc(9, 1, 13, 13, 155, -250);
                        p.CloseFigure();
                        g.DrawPath(pen, p);
                    }
                    break;
                case 1: // schedule: clock
                    g.DrawEllipse(pen, 3, 3, 18, 18);
                    g.DrawLines(pen, new[] { new PointF(12, 7), new PointF(12, 12), new PointF(15, 14) });
                    break;
                case 2: // accounts: two people
                    g.DrawEllipse(pen, 5.5f, 4.5f, 7, 7);
                    g.DrawArc(pen, 2.5f, 14, 13, 12, 180, 180);
                    g.DrawEllipse(pen, 14.5f, 6.5f, 5, 5);
                    g.DrawArc(pen, 15, 14, 8, 9, 230, 120);
                    break;
                case 3: // display: monitor
                    g.DrawRectangle(pen, 3, 4, 18, 13);
                    g.DrawLine(pen, 8, 21, 16, 21);
                    g.DrawLine(pen, 12, 17, 12, 21);
                    break;
                case 4: // shortcuts: keyboard
                    g.DrawRectangle(pen, 2, 6, 20, 12);
                    g.DrawLine(pen, 7, 14, 17, 14);
                    for (int i = 0; i < 4; i++) g.DrawLine(pen, 6 + i * 4, 10, 6.5f + i * 4, 10);
                    break;
                default: // general: cog
                    g.DrawEllipse(pen, 8.5f, 8.5f, 7, 7);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        g.DrawLine(pen, 12 + (float)Math.Cos(a) * 7, 12 + (float)Math.Sin(a) * 7, 12 + (float)Math.Cos(a) * 9.5f, 12 + (float)Math.Sin(a) * 9.5f);
                    }
                    break;
            }
        }
    }

    // The moods as swatches: a little sky over grass, seen through each mood.
    class PresetPicker : Control
    {
        const int Columns = 3;
        int selected = -1, hover = -1;
        public event EventHandler SelectedIndexChanged;
        readonly ToolTip tip = new ToolTip();

        public PresetPicker()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            int rows = (Preset.Count + Columns - 1) / Columns;
            Size = new Size(Columns * CellW + (Columns - 1) * Gap + 4, rows * CellH + (rows - 1) * Gap + 4);
            Margin = new Padding(Theme.Px(3), Theme.Px(8), Theme.Px(3), Theme.Px(4));
            AccessibleRole = AccessibleRole.List;
            Theme.Hand(this);
        }

        int CellW { get { return Theme.Px(78); } }
        int CellH { get { return Theme.Px(50); } }
        int Gap { get { return Theme.Px(7); } }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                int v = Math.Max(-1, Math.Min(Preset.Count - 1, value));
                if (v == selected) return;
                selected = v;
                AccessibleDescription = v >= 0 ? Preset.NameOf(v) : "";
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        Rectangle Cell(int i)
        {
            return new Rectangle(2 + (i % Columns) * (CellW + Gap), 2 + (i / Columns) * (CellH + Gap), CellW, CellH);
        }

        int HitTest(Point p)
        {
            for (int i = 0; i < Preset.Count; i++) if (Cell(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = HitTest(e.Location);
            if (h != hover)
            {
                hover = h;
                Invalidate();
                tip.SetToolTip(this, h >= 0 ? Preset.NameOf(h) : "");
            }
        }

        protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int h = HitTest(e.Location);
            if (h >= 0 && Enabled) SelectedIndex = h;
        }

        protected override bool IsInputKey(Keys k)
        {
            switch (k & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: return true;
            }
            return base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            int s = Math.Max(0, selected);
            switch (e.KeyCode)
            {
                case Keys.Left: s--; break;
                case Keys.Right: s++; break;
                case Keys.Up: s -= Columns; break;
                case Keys.Down: s += Columns; break;
                default: return;
            }
            if (s >= 0 && s < Preset.Count) SelectedIndex = s;
            e.Handled = true;
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        public static Color Through(float[] m, Color c)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float[] o = new float[3];
            for (int j = 0; j < 3; j++) o[j] = r * m[j] + g * m[5 + j] + b * m[10 + j] + m[20 + j];
            return Color.FromArgb(Clamp(o[0]), Clamp(o[1]), Clamp(o[2]));
        }

        static int Clamp(float v) { return Math.Max(0, Math.Min(255, (int)Math.Round(v * 255))); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color sky = Color.FromArgb(0x86, 0xb4, 0xe8), grass = Color.FromArgb(0x8c, 0xb0, 0x5a), sand = Color.FromArgb(0xe2, 0xcc, 0x96);
            for (int i = 0; i < Preset.Count; i++)
            {
                Rectangle c = Cell(i);
                float[] m = Look.From(Mood.From(new AccountParams { Preset = i, Intensity = 80, Vignette = 0, Filter = true }), 1).Matrix;
                using (GraphicsPath p = Theme.Round(c, Theme.Px(6)))
                {
                    if (i == selected) Theme.Glow(g, p, Theme.GreenHi, Theme.Px(3));
                    Region old = g.Clip;
                    g.SetClip(p);
                    using (var b = new LinearGradientBrush(c, Through(m, sky), Through(m, Color.FromArgb(0xc8, 0xdc, 0xf0)), LinearGradientMode.Vertical))
                        g.FillRectangle(b, c);
                    var ground = new Rectangle(c.X, c.Y + c.Height * 55 / 100, c.Width, c.Height);
                    using (var b = new SolidBrush(Through(m, grass))) g.FillRectangle(b, ground);
                    using (var b = new SolidBrush(Through(m, sand))) g.FillEllipse(b, c.X + c.Width / 4, c.Y + c.Height * 62 / 100, c.Width / 2, c.Height / 4);
                    g.Clip = old;
                    using (var pen = new Pen(i == selected ? Theme.GreenHi : i == hover ? Theme.Text : Color.FromArgb(0x4a, 0x48, 0x55), i == selected ? 2 : 1))
                        g.DrawPath(pen, p);
                }
                if (i == Preset.CustomIndex)
                {
                    // custom mood: a small pen
                    float x = c.Right - Theme.Px(14), y = c.Y + Theme.Px(5);
                    using (var b = new SolidBrush(Color.FromArgb(200, Theme.Edge))) g.FillEllipse(b, x - Theme.Px(3), y - Theme.Px(1), Theme.Px(14), Theme.Px(14));
                    using (var pen = new Pen(Theme.Text, Theme.Px(1.6f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(pen, x, y + Theme.Px(9), x + Theme.Px(7), y + Theme.Px(2));
                }
            }
            if (Focused && selected >= 0)
                using (var pen = new Pen(Theme.Text) { DashStyle = DashStyle.Dot })
                    g.DrawRectangle(pen, Rectangle.Inflate(Cell(selected), -Theme.Px(4), -Theme.Px(4)));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tip.Dispose();
            base.Dispose(disposing);
        }
    }

    // Live preview: a part of the game as it is (left) and through the mood (right).
    class PreviewBox : Control
    {
        Bitmap shot;
        public Func<IntPtr> GameWindow;        // the Dofus window, IntPtr.Zero when there is none
        public bool LastShotDirect;            // true when the last picture came from the game window itself

        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        const uint PW_RENDERFULLCONTENT = 2;
        public Func<float[]> Matrix;           // color matrix of the mood
        public string NoGameText = "", DayText = "", NightText = "";
        readonly Font textFont = Theme.Body(9f), chipFont = Theme.Strong(7.5f);

        public PreviewBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(Theme.Px(250), Theme.Px(176));
            Margin = new Padding(Theme.Px(3), Theme.Px(6), Theme.Px(3), Theme.Px(4));
            AccessibleRole = AccessibleRole.Graphic;
        }

        // Takes a new picture of the game, without the filter. The game window draws itself into
        // the picture (PrintWindow), so neither the filters nor the windows over it are in it.
        // If the game cannot do that (black picture), the screen is copied instead, avoiding the
        // settings window.
        public void TakeShot()
        {
            IntPtr game = GameWindow != null ? GameWindow() : IntPtr.Zero;
            Rectangle area;
            if (game == IntPtr.Zero || !Native.TryGetClientOnScreen(game, out area))
            {
                if (shot != null) { shot.Dispose(); shot = null; }
                Invalidate();
                return;
            }
            Bitmap direct = FromWindow(game, area);
            if (direct != null)
            {
                if (shot != null) shot.Dispose();
                shot = direct;
                LastShotDirect = true;
                Invalidate();
                return;
            }
            LastShotDirect = false;
            Form form = FindForm();
            Rectangle avoid = form != null && form.Visible ? form.Bounds : Rectangle.Empty;

            int w = Math.Min(area.Width, Width * 2), h = Math.Min(area.Height, Height * 2);
            if (w < 16 || h < 16) return;
            var candidates = new List<Point>
            {
                new Point(area.X + (area.Width - w) / 2, area.Y + (area.Height - h) / 2),
                new Point(avoid.Left - w - 8, area.Y + (area.Height - h) / 2),
                new Point(avoid.Right + 8, area.Y + (area.Height - h) / 2),
                new Point(area.X + (area.Width - w) / 2, avoid.Top - h - 8),
                new Point(area.X + (area.Width - w) / 2, avoid.Bottom + 8),
                new Point(area.X + 8, area.Y + 8),
                new Point(area.Right - w - 8, area.Bottom - h - 8),
            };
            Rectangle pick = Rectangle.Empty;
            foreach (Point p in candidates)
            {
                var r = new Rectangle(p, new Size(w, h));
                if (area.Contains(r) && !r.IntersectsWith(avoid)) { pick = r; break; }
            }
            if (pick.IsEmpty) { Invalidate(); return; }   // covered: keep the last picture

            try
            {
                var raw = new Bitmap(w, h, PixelFormat.Format32bppRgb);
                using (Graphics g = Graphics.FromImage(raw))
                    g.CopyFromScreen(pick.Location, Point.Empty, pick.Size, CopyPixelOperation.SourceCopy);
                var small = new Bitmap(Width, Height, PixelFormat.Format32bppRgb);
                using (Graphics g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(raw, new Rectangle(0, 0, Width, Height));
                }
                raw.Dispose();
                if (shot != null) shot.Dispose();
                shot = small;
            }
            catch (Exception ex) { Log.Error("Preview", ex); }
            Invalidate();
        }

        // The center of the game client area, drawn by the game itself; null when it comes out black.
        Bitmap FromWindow(IntPtr game, Rectangle client)
        {
            RECT wr;
            if (!GetWindowRect(game, out wr)) return null;
            int ww = wr.Right - wr.Left, wh = wr.Bottom - wr.Top;
            if (ww < 16 || wh < 16 || ww > 10000 || wh > 10000) return null;
            try
            {
                using (var full = new Bitmap(ww, wh, PixelFormat.Format32bppRgb))
                {
                    bool ok;
                    using (Graphics g = Graphics.FromImage(full))
                    {
                        IntPtr hdc = g.GetHdc();
                        try { ok = PrintWindow(game, hdc, PW_RENDERFULLCONTENT); }
                        finally { g.ReleaseHdc(hdc); }
                    }
                    if (!ok) return null;
                    int w = Math.Min(client.Width, Width * 2), h = Math.Min(client.Height, Height * 2);
                    var src = new Rectangle(client.X - wr.Left + (client.Width - w) / 2, client.Y - wr.Top + (client.Height - h) / 2, w, h);
                    src.Intersect(new Rectangle(0, 0, ww, wh));
                    if (src.Width < 16 || src.Height < 16) return null;

                    int bright = 0;
                    for (int i = 1; i <= 6; i++)
                        for (int j = 1; j <= 6; j++)
                        {
                            Color c = full.GetPixel(src.X + src.Width * i / 7, src.Y + src.Height * j / 7);
                            bright = Math.Max(bright, Math.Max(c.R, Math.Max(c.G, c.B)));
                        }
                    if (bright < 12) return null;

                    var small = new Bitmap(Width, Height, PixelFormat.Format32bppRgb);
                    using (Graphics g = Graphics.FromImage(small))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.DrawImage(full, new Rectangle(0, 0, Width, Height), src, GraphicsUnit.Pixel);
                    }
                    return small;
                }
            }
            catch (Exception ex) { Log.Error("Preview (window)", ex); return null; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath frame = Theme.Round(r, Theme.Px(8)))
            {
                Region old = g.Clip;
                g.SetClip(frame);
                if (shot == null)
                {
                    using (var p = new GraphicsPath())
                    {
                        p.AddEllipse(-Width / 2, -Height / 3, Width * 2, Height * 2);
                        using (var b = new PathGradientBrush(p) { CenterColor = Theme.Violet, SurroundColors = new[] { Color.FromArgb(0x24, 0x20, 0x3f) } })
                            g.FillRectangle(b, r);
                    }
                    TextRenderer.DrawText(g, NoGameText, textFont, r, Theme.Text,
                                          TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                }
                else
                {
                    int half = Width / 2;
                    g.DrawImage(shot, new Rectangle(0, 0, half, Height), new Rectangle(0, 0, half, Height), GraphicsUnit.Pixel);
                    float[] m = Matrix != null ? Matrix() : null;
                    if (m != null)
                    {
                        var cm = new ColorMatrix(new[]
                        {
                            new[] { m[0], m[1], m[2], m[3], m[4] },
                            new[] { m[5], m[6], m[7], m[8], m[9] },
                            new[] { m[10], m[11], m[12], m[13], m[14] },
                            new[] { m[15], m[16], m[17], m[18], m[19] },
                            new[] { m[20], m[21], m[22], m[23], m[24] },
                        });
                        using (var attr = new ImageAttributes())
                        {
                            attr.SetColorMatrix(cm);
                            g.DrawImage(shot, new Rectangle(half, 0, Width - half, Height), half, 0, Width - half, Height, GraphicsUnit.Pixel, attr);
                        }
                    }
                    // the split: a glowing line and a gem
                    using (var p = new GraphicsPath())
                    {
                        p.AddRectangle(new RectangleF(half - Theme.Px(1.5f), 0, Theme.Px(3), Height));
                        Theme.Glow(g, p, Theme.GreenHi, Theme.Px(3));
                        using (var b = new SolidBrush(Theme.GreenHi)) g.FillPath(b, p);
                    }
                    float d = Theme.Px(20);
                    var gem = new RectangleF(half - d / 2, Height / 2f - d / 2, d, d);
                    using (var p = new GraphicsPath())
                    {
                        p.AddEllipse(gem);
                        using (var b = new PathGradientBrush(p) { CenterPoint = new PointF(gem.X + d * 0.35f, gem.Y + d * 0.3f), CenterColor = Color.White, SurroundColors = new[] { Theme.GreenMid } })
                            g.FillPath(b, p);
                        using (var pen = new Pen(Theme.Edge, Theme.Px(2))) g.DrawPath(pen, p);
                    }
                    Chip(g, DayText, Theme.Px(8), Color.FromArgb(230, 0xff, 0xf6, 0xd8), Color.FromArgb(0x3a, 0x2a, 0x10), false);
                    Chip(g, NightText, Width - Theme.Px(8), Color.FromArgb(220, 0x14, 0x13, 0x28), Color.FromArgb(0xd8, 0xd0, 0xff), true);
                }
                g.Clip = old;
                using (var pen = new Pen(Color.FromArgb(0x6a, 0x68, 0x78))) g.DrawPath(pen, frame);
            }
        }

        void Chip(Graphics g, string text, int x, Color back, Color fore, bool right)
        {
            if (string.IsNullOrEmpty(text)) return;
            Font f = chipFont;
            {
                string s = text.ToUpperInvariant();
                Size t = Theme.MeasureSpaced(s, f, 1);
                var r = new Rectangle(right ? x - t.Width - Theme.Px(14) : x, Theme.Px(8), t.Width + Theme.Px(14), t.Height + Theme.Px(6));
                using (GraphicsPath p = Theme.Round(r, Theme.Px(4)))
                using (var b = new SolidBrush(back))
                    g.FillPath(b, p);
                Theme.DrawSpaced(g, s, f, fore, r.X + Theme.Px(7), r.Y + Theme.Px(3), 1, false);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (shot != null) shot.Dispose(); textFont.Dispose(); chipFont.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // One of the notification area icon styles, chosen like a radio button.
    class IconChoice : Control
    {
        public readonly int Style;
        readonly string title, description;
        readonly Font titleFont, descFont;
        bool selected, hover;
        public event EventHandler Picked;

        public IconChoice(int style, string title, string description)
        {
            Style = style;
            this.title = title;
            this.description = description;
            Text = title;
            AccessibleName = title;
            AccessibleDescription = description;
            AccessibleRole = AccessibleRole.RadioButton;
            titleFont = Theme.Strong(10.5f);
            descFont = Theme.Body(8.5f);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Size = new Size(Theme.Px(470), Theme.Px(60));
            Margin = new Padding(Theme.Px(3), Theme.Px(4), Theme.Px(3), Theme.Px(4));
            Theme.Hand(this);
        }

        public bool Selected
        {
            get { return selected; }
            set { selected = value; AccessibleDescription = description + (value ? " ✓" : ""); Invalidate(); }
        }

        void Pick() { if (Picked != null) Picked(this, EventArgs.Empty); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Focus(); Pick(); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Pick(); e.Handled = true; }
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(Theme.Px(3), Theme.Px(3), Width - Theme.Px(6) - 1, Height - Theme.Px(6) - 1);
            using (GraphicsPath p = Theme.Round(r, Theme.Px(8)))
            {
                if (selected)
                {
                    Theme.Glow(g, p, Theme.GreenHi, Theme.Px(3));
                    Theme.FillVertical(g, p, r, Color.FromArgb(0x4a, 0x6a, 0x24), Color.FromArgb(0x2e, 0x42, 0x16));
                }
                else Theme.FillVertical(g, p, r, hover ? Color.FromArgb(0x3e, 0x3d, 0x48) : Color.FromArgb(0x34, 0x33, 0x3d), Color.FromArgb(0x26, 0x25, 0x2d));
                using (var pen = new Pen(selected || Focused ? Theme.GreenHi : Color.FromArgb(0x4a, 0x48, 0x55))) g.DrawPath(pen, p);
            }

            // radio dot
            float d = Theme.Px(16), cx = r.X + Theme.Px(12);
            var dot = new RectangleF(cx, r.Y + (r.Height - d) / 2, d, d);
            using (var b = new SolidBrush(Theme.Field)) g.FillEllipse(b, dot);
            using (var pen = new Pen(selected ? Theme.GreenHi : Theme.Frame)) g.DrawEllipse(pen, dot);
            if (selected) using (var b = new SolidBrush(Theme.GreenHi)) g.FillEllipse(b, RectangleF.Inflate(dot, -d * 0.28f, -d * 0.28f));

            int tx = (int)(dot.Right + Theme.Px(12));
            TextRenderer.DrawText(g, title, titleFont, new Point(tx, (int)r.Y + Theme.Px(8)), selected ? Theme.GreenHi : Theme.Text, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, description, descFont, new Point(tx, (int)r.Y + Theme.Px(30)), selected ? Color.FromArgb(0xd8, 0xe8, 0xc0) : Theme.Muted, TextFormatFlags.NoPadding);

            // the four states, in slots like the game's inventory
            int slot = Theme.Px(38), gap = Theme.Px(6), icon = Theme.Px(28);
            int x = (int)r.Right - Theme.Px(10) - TrayIcons.StateCount * slot - (TrayIcons.StateCount - 1) * gap;
            int y = (int)(r.Y + (r.Height - slot) / 2);
            for (int s = 0; s < TrayIcons.StateCount; s++)
            {
                var cell = new Rectangle(x + s * (slot + gap), y, slot, slot);
                using (GraphicsPath p = Theme.Round(cell, Theme.Px(6)))
                {
                    using (var b = new PathGradientBrush(p) { CenterColor = Color.FromArgb(0x3e, 0x3a, 0x5a), SurroundColors = new[] { Color.FromArgb(0x1e, 0x1c, 0x2a) } })
                        g.FillPath(b, p);
                    using (var pen = new Pen(Color.FromArgb(0x7a, 0x74, 0xa0))) g.DrawPath(pen, p);
                }
                Bitmap bmp = TrayIcons.Picture(Style, s, icon);
                if (bmp != null) g.DrawImage(bmp, cell.X + (slot - icon) / 2, cell.Y + (slot - icon) / 2, icon, icon);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { titleFont.Dispose(); descFont.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // Header of the settings window: the egg, the glowing title, and a drag area.
    class HeaderBar : Panel
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 2;
        readonly string title;

        public HeaderBar(string title)
        {
            this.title = title;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = Theme.Px(50);
            Dock = DockStyle.Top;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Form f = FindForm();
            if (f == null) return;
            ReleaseCapture();
            SendMessage(f.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            var r = new Rectangle(0, 0, Width, Height);
            using (var b = new LinearGradientBrush(r, Theme.Raised, Color.FromArgb(0x22, 0x21, 0x28), LinearGradientMode.Vertical))
            {
                b.InterpolationColors = new ColorBlend { Colors = new[] { Theme.Raised, Color.FromArgb(0x2c, 0x2b, 0x33), Color.FromArgb(0x22, 0x21, 0x28) }, Positions = new[] { 0f, 0.55f, 1f } };
                g.FillRectangle(b, r);
            }
            using (var pen = new Pen(Color.FromArgb(30, Color.White))) g.DrawLine(pen, 0, 0, Width, 0);
            using (var b = new SolidBrush(Theme.Edge)) g.FillRectangle(b, 0, Height - Theme.Px(2), Width, Theme.Px(2));

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            float em = Theme.Px(21);
            int icon = Theme.Px(26);
            using (var path = new GraphicsPath())
            {
                path.AddString(title, Theme.TitleFamily, (int)FontStyle.Regular, em, Point.Empty, StringFormat.GenericTypographic);
                RectangleF tb = path.GetBounds();
                float total = icon + Theme.Px(10) + tb.Width;
                float x0 = (Width - total) / 2, cy = (Height - Theme.Px(2)) / 2f;
                Bitmap egg = TrayIcons.Picture(0, TrayIcons.Night, icon);
                if (egg != null) g.DrawImage(egg, x0, cy - icon / 2f, icon, icon);
                using (var m = new Matrix())
                {
                    m.Translate(x0 + icon + Theme.Px(10) - tb.X, cy - tb.Height / 2 - tb.Y);
                    path.Transform(m);
                }
                using (var shadow = (GraphicsPath)path.Clone())
                {
                    using (var m = new Matrix()) { m.Translate(0, Theme.Px(2)); shadow.Transform(m); }
                    using (var b = new SolidBrush(Color.Black)) g.FillPath(b, shadow);
                }
                Theme.Glow(g, path, Theme.Green, Theme.Px(3));
                using (var b = new SolidBrush(Color.White)) g.FillPath(b, path);

                RectangleF placed = path.GetBounds();
                float lx = x0 - Theme.Px(14), rx = placed.Right + Theme.Px(14);
                Theme.Diamond(g, lx, cy, Theme.Px(4), Theme.GreenHi, true);
                Theme.Diamond(g, rx, cy, Theme.Px(4), Theme.GreenHi, true);
                using (var b = new LinearGradientBrush(new RectangleF(lx - Theme.Px(52), cy - 1, Theme.Px(46), 2), Color.FromArgb(0, Theme.GreenHi), Theme.GreenHi, LinearGradientMode.Horizontal))
                    g.FillRectangle(b, lx - Theme.Px(52), cy - 1, Theme.Px(44), 2);
                using (var b = new LinearGradientBrush(new RectangleF(rx + Theme.Px(6), cy - 1, Theme.Px(46), 2), Theme.GreenHi, Color.FromArgb(0, Theme.GreenHi), LinearGradientMode.Horizontal))
                    g.FillRectangle(b, rx + Theme.Px(8), cy - 1, Theme.Px(44), 2);
            }
        }
    }

    // Bottom bar: the filter state and the main button.
    class FooterBar : Panel
    {
        string status = "";
        bool on;

        public FooterBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = Theme.Px(62);
            Dock = DockStyle.Bottom;
            BackColor = Color.FromArgb(0x24, 0x23, 0x2b);
            Font = Theme.Strong(8.5f);
        }

        public void SetStatus(string text, bool active)
        {
            if (text == status && active == on) return;
            status = text; on = active;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            using (var b = new SolidBrush(Theme.Line)) g.FillRectangle(b, 0, 0, Width, 1);
            using (var pen = new Pen(Color.FromArgb(14, Color.White))) g.DrawLine(pen, 0, 1, Width, 1);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float d = Theme.Px(9), cy = Height / 2f;
            using (var p = new GraphicsPath())
            {
                p.AddEllipse(Theme.Px(18), cy - d / 2, d, d);
                if (on) Theme.Glow(g, p, Theme.GreenHi, Theme.Px(2));
                using (var b = new SolidBrush(on ? Theme.Green : Theme.Muted)) g.FillPath(b, p);
            }
            string s = status.ToUpperInvariant();
            Size t = Theme.MeasureSpaced(s, Font, Theme.Px(1.5f));
            Theme.DrawSpaced(g, s, Font, Theme.Muted, Theme.Px(36), (Height - t.Height) / 2, Theme.Px(1.5f), false);
        }
    }

    // Menu of the notification area icon, in the same colors.
    class GameMenuRenderer : ToolStripProfessionalRenderer
    {
        class Colors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Theme.Panel; } }
            public override Color ImageMarginGradientBegin { get { return Theme.Rail; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.Rail; } }
            public override Color ImageMarginGradientEnd { get { return Theme.Rail; } }
            public override Color MenuBorder { get { return Theme.Frame; } }
            public override Color MenuItemBorder { get { return Theme.GreenHi; } }
            public override Color MenuItemSelected { get { return Theme.GreenSelected; } }
            public override Color MenuItemSelectedGradientBegin { get { return Theme.GreenSelected; } }
            public override Color MenuItemSelectedGradientEnd { get { return Theme.GreenSelected; } }
            public override Color MenuItemPressedGradientBegin { get { return Theme.GreenSelected; } }
            public override Color MenuItemPressedGradientEnd { get { return Theme.GreenSelected; } }
            public override Color SeparatorDark { get { return Theme.Line; } }
            public override Color SeparatorLight { get { return Theme.Panel; } }
            public override Color CheckBackground { get { return Theme.GreenDark; } }
            public override Color CheckSelectedBackground { get { return Theme.GreenMid; } }
            public override Color CheckPressedBackground { get { return Theme.GreenMid; } }
        }

        public GameMenuRenderer() : base(new Colors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = !e.Item.Enabled ? Theme.Muted : e.Item.Selected ? Theme.GreenHi : Theme.Text;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item != null && e.Item.Selected ? Theme.GreenHi : Theme.Text;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = e.ImageRectangle;
            int s = Math.Min(r.Width, r.Height);
            var box = new RectangleF(r.X + (r.Width - s) / 2f + 1, r.Y + (r.Height - s) / 2f + 1, s - 2, s - 2);
            using (GraphicsPath p = Theme.Round(box, 3))
            {
                Theme.FillVertical(g, p, box, Color.FromArgb(0xa8, 0xd8, 0x4a), Theme.GreenDark);
                using (var pen = new Pen(Theme.GreenHi)) g.DrawPath(pen, p);
            }
            using (var pen = new Pen(Color.White, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[]
                {
                    new PointF(box.X + box.Width * 0.24f, box.Y + box.Height * 0.52f),
                    new PointF(box.X + box.Width * 0.43f, box.Y + box.Height * 0.72f),
                    new PointF(box.X + box.Width * 0.78f, box.Y + box.Height * 0.30f),
                });
        }
    }
}
