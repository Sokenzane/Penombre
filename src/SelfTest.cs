// Pénombre - self-test, compiled only in the test build (build.bat test -> dist\Penombre-test.exe).
// Copyright (C) 2026 Sokenzane - GPL-3.0 (see Penombre.cs)
//
// Usage: Penombre-test.exe --selftest --data=<empty folder>
// Runs unit checks, then drives the real app over a running Dofus 3 window, measures the
// darkening on screen and writes selftest-progress.txt / selftest-report.txt in the data folder.
// Some phases need a human (or a remote tester) to press shortcuts or switch windows: the
// progress file says what to do and when.
//
// With --shots, it only takes the full-size pictures used by the README instead (game without
// filter, each mood, each settings page), in the data folder.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Penombre
{
    static partial class Program
    {
        static partial void RunSelfTest(TrayApp app)
        {
            if (!SelfTestMode) return;
            new SelfTest(app).Start();
        }
    }

    class SelfTest
    {
        [DllImport("user32.dll")] static extern void SwitchToThisWindow(IntPtr h, bool altTab);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
        [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();

        // Brings a game window to the front (the usual foreground-lock workaround).
        static void Front(IntPtr h)
        {
            uint pid;
            uint fgThread = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out pid), me = GetCurrentThreadId();
            if (fgThread != me) AttachThreadInput(me, fgThread, true);
            BringWindowToTop(h);
            SetForegroundWindow(h);
            if (fgThread != me) AttachThreadInput(me, fgThread, false);
            if (Native.GetForegroundWindow() != h) SwitchToThisWindow(h, true);
        }
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);

        class Step
        {
            public string Name;
            public Action Run;
            public int WaitMs;
            public Func<bool> Until;
            public int TimeoutMs;
        }

        class Sample
        {
            public long T;
            public bool GameFg, Shown, Peeking, OnlyFg, FgOverGame;
            public float Amount;
            public string Region, Fg;
        }

        readonly TrayApp app;
        readonly OverlayManager m;
        readonly Queue<Step> steps = new Queue<Step>();
        readonly List<Sample> samples = new List<Sample>();
        readonly List<int> commands = new List<int>();
        readonly Timer timer = new Timer { Interval = 50 };
        readonly Timer sampler = new Timer { Interval = 100 };
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly StringBuilder report = new StringBuilder();
        readonly string dir;
        int pass, fail, skip;
        Step current;
        long stepStart;
        bool waiting, busy;

        Overlay o;
        Rectangle area;
        double bOn, bOff, bOffZone, bOffCorner;

        public SelfTest(TrayApp app)
        {
            this.app = app;
            m = app.Manager;
            dir = Settings.Folder;
        }

        public void Start()
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "selftest-progress.txt"), "");
            Progress("Pénombre " + Program.Version + " self-test - " + Program.WindowsVersion());
            app.CommandReceived += cmd => commands.Add(cmd);
            sampler.Tick += (s, e) => TakeSample();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--shots") >= 0) PlanShots(); else Plan();
            timer.Tick += (s, e) => Pump();
            timer.Start();
            sampler.Start();
        }

        // ------------------------------------------------------------ engine

        void Add(string name, Action run, int waitMs) { steps.Enqueue(new Step { Name = name, Run = run, WaitMs = waitMs }); }
        void AddUntil(string name, Action run, Func<bool> until, int timeoutMs) { steps.Enqueue(new Step { Name = name, Run = run, Until = until, TimeoutMs = timeoutMs }); }

        void Pump()
        {
            if (busy) return; // a step pumping messages (DoEvents) must not start the next one
            busy = true;
            try { PumpCore(); }
            finally { busy = false; }
        }

        void PumpCore()
        {
            long now = clock.ElapsedMilliseconds;
            try
            {
                if (waiting)
                {
                    bool done = current.Until != null ? (current.Until() || now - stepStart > current.TimeoutMs) : now - stepStart >= current.WaitMs;
                    if (!done) return;
                    if (current.Until != null && now - stepStart > current.TimeoutMs) Progress("  (timeout: " + current.Name + ")");
                    waiting = false;
                }
                if (steps.Count == 0) { Finish(); return; }
                current = steps.Dequeue();
                Progress("[" + (now / 1000.0).ToString("0.0") + "s] " + current.Name);
                if (current.Run != null) current.Run();
                stepStart = clock.ElapsedMilliseconds;
                waiting = true;
            }
            catch (Exception ex)
            {
                Check("no exception in step '" + (current != null ? current.Name : "?") + "'", false, ex.ToString());
                waiting = false;
            }
        }

        void Check(string name, bool ok, string detail)
        {
            if (ok) pass++; else fail++;
            string line = (ok ? "PASS " : "FAIL ") + name + (string.IsNullOrEmpty(detail) ? "" : "  -- " + detail);
            report.AppendLine(line);
            Progress("  " + line);
        }

        void Skip(string name, string why)
        {
            skip++;
            report.AppendLine("SKIP " + name + "  -- " + why);
            Progress("  SKIP " + name + "  -- " + why);
        }

        void Progress(string line)
        {
            try { File.AppendAllText(Path.Combine(dir, "selftest-progress.txt"), line + Environment.NewLine, Encoding.UTF8); } catch { }
        }

        void Finish()
        {
            timer.Stop();
            sampler.Stop();
            string head = "Pénombre " + Program.Version + " self-test - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine
                + Program.WindowsVersion() + Environment.NewLine
                + "PASS " + pass + " / FAIL " + fail + " / SKIP " + skip + Environment.NewLine + Environment.NewLine;
            File.WriteAllText(Path.Combine(dir, "selftest-report.txt"), head + report + Environment.NewLine + m.Diagnostics(), Encoding.UTF8);
            Progress("DONE: PASS " + pass + " / FAIL " + fail + " / SKIP " + skip);
            var quit = new Timer { Interval = 1000 };
            quit.Tick += (s, e) => { quit.Stop(); Application.Exit(); };
            quit.Start();
        }

        void TakeSample()
        {
            Overlay ov = FindOverlay();
            IntPtr fg = Native.GetForegroundWindow();
            samples.Add(new Sample
            {
                T = clock.ElapsedMilliseconds,
                GameFg = ov != null && fg == ov.Target,
                Shown = ov != null && ov.IsShown,
                Peeking = m.Peeking,
                OnlyFg = Settings.OnlyForeground,
                Amount = m.Shown,
                Region = ov != null ? ov.RegionInfo : "-",
                Fg = Native.ClassOf(fg),
                FgOverGame = ov != null && fg != ov.Target && CoversGame(fg, ov.Target),
            });
        }

        // A window in front that hides part of the game.
        static bool CoversGame(IntPtr window, IntPtr game)
        {
            Rectangle r, g;
            return OverlayManager.IsOccluder(window, out r) && Native.TryGetBounds(game, out g) && r.IntersectsWith(g)
                   && !Native.ClassOf(window).StartsWith("WindowsForms10");
        }

        // The filter of the Dofus in front (or the first one).
        Overlay FindOverlay()
        {
            IntPtr fg = Native.GetForegroundWindow();
            Overlay first = null;
            foreach (Overlay x in m.All)
            {
                if (x.Target == fg) return x;
                if (first == null) first = x;
            }
            return first;
        }

        // ------------------------------------------------------------ screen measures

        // Average luminance of a screen rectangle, layered windows included (CAPTUREBLT).
        static double Luma(Rectangle r)
        {
            using (var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    IntPtr dst = g.GetHdc();
                    IntPtr src = Native.GetDC(IntPtr.Zero);
                    BitBlt(dst, 0, 0, r.Width, r.Height, src, r.X, r.Y, 0x00CC0020 | 0x40000000);
                    Native.ReleaseDC(IntPtr.Zero, src);
                    g.ReleaseHdc(dst);
                }
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, r.Width, r.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var px = new int[r.Width * r.Height];
                Marshal.Copy(data.Scan0, px, 0, px.Length);
                bmp.UnlockBits(data);
                double sum = 0; int n = 0;
                for (int i = 0; i < px.Length; i += 7)
                {
                    int c = px[i];
                    sum += 0.2126 * ((c >> 16) & 255) + 0.7152 * ((c >> 8) & 255) + 0.0722 * (c & 255);
                    n++;
                }
                return n > 0 ? sum / n : 0;
            }
        }

        Rectangle Part(float x, float y, float w, float h)
        {
            return new Rectangle(area.X + (int)(area.Width * x), area.Y + (int)(area.Height * y), (int)(area.Width * w), (int)(area.Height * h));
        }

        // Bands of the game area away from the center and from the bottom UI, skipping the ones
        // under another window (that window may not show in a screen capture).
        // Average red, green and blue of the same bands as Scene().
        double[] SceneRgb()
        {
            List<Rectangle> above = OccluderRects(o.Target);
            var sum = new double[3]; int n = 0;
            foreach (Rectangle band in new[] { Part(0.04f, 0.12f, 0.24f, 0.45f), Part(0.72f, 0.12f, 0.24f, 0.45f), Part(0.30f, 0.62f, 0.18f, 0.18f) })
            {
                bool free = true;
                foreach (Rectangle r in above) if (r.IntersectsWith(band)) free = false;
                if (!free) continue;
                using (var bmp = new Bitmap(band.Width, band.Height, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        IntPtr dst = g.GetHdc();
                        IntPtr src = Native.GetDC(IntPtr.Zero);
                        BitBlt(dst, 0, 0, band.Width, band.Height, src, band.X, band.Y, 0x00CC0020 | 0x40000000);
                        Native.ReleaseDC(IntPtr.Zero, src);
                        g.ReleaseHdc(dst);
                    }
                    BitmapData data = bmp.LockBits(new Rectangle(0, 0, band.Width, band.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    var px = new int[band.Width * band.Height];
                    Marshal.Copy(data.Scan0, px, 0, px.Length);
                    bmp.UnlockBits(data);
                    for (int i = 0; i < px.Length; i += 7)
                    {
                        sum[0] += (px[i] >> 16) & 255; sum[1] += (px[i] >> 8) & 255; sum[2] += px[i] & 255;
                        n++;
                    }
                }
            }
            if (n > 0) for (int c = 0; c < 3; c++) sum[c] /= n;
            return sum;
        }

        static string Rgb(double[] v) { return v[0].ToString("0.00") + "/" + v[1].ToString("0.00") + "/" + v[2].ToString("0.00"); }

        double Scene()
        {
            List<Rectangle> above = OccluderRects(o.Target);
            double sum = 0; int n = 0;
            foreach (Rectangle band in new[] { Part(0.04f, 0.12f, 0.24f, 0.45f), Part(0.72f, 0.12f, 0.24f, 0.45f), Part(0.30f, 0.62f, 0.18f, 0.18f) })
            {
                bool free = true;
                foreach (Rectangle r in above) if (r.IntersectsWith(band)) free = false;
                if (free) { sum += Luma(band); n++; }
            }
            return n > 0 ? sum / n : Luma(Part(0.04f, 0.12f, 0.24f, 0.45f));
        }

        List<Rectangle> OccluderRects(IntPtr target)
        {
            var list = new List<Rectangle>();
            Native.EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (h == target) return false;
                Rectangle r;
                if (!Native.ClassOf(h).StartsWith("WindowsForms10") && OverlayManager.IsOccluder(h, out r)) list.Add(r);
                return true;
            }, IntPtr.Zero);
            return list;
        }
        Rectangle ZoneRect() { return Part(0.30f, 0.86f, 0.40f, 0.12f); }
        Rectangle Corner() { return Part(0.0f, 0.0f, 0.07f, 0.10f); }

        static string F(double v) { return v.ToString("0.0"); }

        // Saves what is on screen over the game (a third of the size) for a visual check.
        void Snap(string name)
        {
            try
            {
                using (var full = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(full))
                    {
                        IntPtr dst = g.GetHdc();
                        IntPtr src = Native.GetDC(IntPtr.Zero);
                        BitBlt(dst, 0, 0, area.Width, area.Height, src, area.X, area.Y, 0x00CC0020 | 0x40000000);
                        Native.ReleaseDC(IntPtr.Zero, src);
                        g.ReleaseHdc(dst);
                    }
                    using (var small = new Bitmap(full, area.Width / 3, area.Height / 3))
                        small.Save(Path.Combine(dir, "snap-" + name + ".png"), ImageFormat.Png);
                }
            }
            catch (Exception ex) { Progress("  (snapshot " + name + " failed: " + ex.Message + ")"); }
        }

        // Windows above the game that hide part of it (the filter must be cut around them).
        string OccludersAbove(IntPtr target)
        {
            var names = new List<string>();
            Native.EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (h == target) return false;
                Rectangle r, game;
                if (Native.ClassOf(h).StartsWith("WindowsForms10")) return true; // ours
                if (OverlayManager.IsOccluder(h, out r) && Native.TryGetBounds(target, out game) && r.IntersectsWith(game))
                    names.Add(Native.ClassOf(h) + " \"" + Native.TitleOf(h) + "\" " + r);
                return true;
            }, IntPtr.Zero);
            return string.Join("; ", names.ToArray());
        }

        void CheckRegion(string name)
        {
            string above = OccludersAbove(o.Target);
            if (above.Length == 0) Check(name, o.RegionInfo == "full", "region=" + o.RegionInfo);
            else Check(name + " (cut around the windows above Dofus)", o.IsShown && o.RegionInfo.Contains("rect"), "region=" + o.RegionInfo + ", above: " + above);
        }

        void Defaults()
        {
            Settings.Enabled = true; Settings.Shader = true; Settings.PresetIndex = 0; Settings.Intensity = 80;
            Settings.Vignette = 0; Settings.Grain = 0; Settings.Auto = false; Settings.SunMode = false;
            Settings.ZonesEnabled = false; Settings.Zones = new List<Zone>(); Settings.OnlyForeground = false;
            Settings.FadeMs = 300; Settings.Accounts.Clear(); Settings.Hotkeys = Shortcuts.Defaults();
            Settings.VignetteColor = Settings.DefaultVignetteColor;
            app.NotifyChanged();
        }

        // ------------------------------------------------------------ README pictures

        // Saves a screen rectangle at full size, layered windows included.
        void Shot(Rectangle r, string name)
        {
            try
            {
                using (var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        IntPtr dst = g.GetHdc();
                        IntPtr src = Native.GetDC(IntPtr.Zero);
                        BitBlt(dst, 0, 0, r.Width, r.Height, src, r.X, r.Y, 0x00CC0020 | 0x40000000);
                        Native.ReleaseDC(IntPtr.Zero, src);
                        g.ReleaseHdc(dst);
                    }
                    bmp.Save(Path.Combine(dir, name + ".png"), ImageFormat.Png);
                }
                Progress("  saved " + name + ".png");
            }
            catch (Exception ex) { Progress("  (" + name + " failed: " + ex.Message + ")"); }
        }

        void PlanShots()
        {
            Add("defaults", () =>
            {
                Defaults();
                Settings.FadeMs = 0; Settings.Vignette = 1;
                Settings.Enabled = false; app.NotifyChanged();
            }, 200);
            AddUntil("waiting for a Dofus 3 window", null, () => FindOverlay() != null, 20000);
            Add("game window", () => { o = FindOverlay(); Front(o.Target); }, 1500);
            Add("game without filter", () =>
            {
                Front(o.Target);
                Native.TryGetClientOnScreen(o.Target, out area);
                Shot(area, "game-off");
                Settings.Enabled = true; app.NotifyChanged();
            }, 1000);
            for (int i = 0; i < Preset.All.Length; i++)
            {
                int idx = i;
                Add("mood " + idx, () => { Settings.PresetIndex = idx; app.NotifyChanged(); }, 1200);
                Add("mood " + idx + ": picture", () => Shot(area, "game-mood" + idx), 100);
            }
            SettingsForm form = null;
            Add("settings window", () =>
            {
                Settings.PresetIndex = 0; app.NotifyChanged();
                form = new SettingsForm(m, app.NotifyChanged, () => app.FailedHotkeys);
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(area.X + (area.Width - form.Width) / 2, area.Y + (area.Height - form.Height) / 2);
                form.TopMost = true; // above the game, even when Windows refuses to bring it to the front
                form.Show();
            }, 1500);
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                Add("settings page " + (idx + 1), () =>
                {
                    if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
                    form.Activate();
                    form.SelectPage(idx);
                    if (idx == 0) form.RefreshPreview();
                }, 1200);
                Add("settings page " + (idx + 1) + ": picture", () => Shot(form.Bounds, "settings" + (idx + 1)), 100);
            }
            Add("close", () => { form.Close(); form.Dispose(); }, 300);
        }

        // ------------------------------------------------------------ the plan

        void Plan()
        {
            Add("unit checks", UnitChecks, 100);
            Add("defaults", Defaults, 200);

            AddUntil("waiting for a Dofus 3 window", null, () => FindOverlay() != null, 20000);
            Add("game window detected", () =>
            {
                o = FindOverlay();
                Check("Dofus window detected", o != null, o != null ? "account \"" + o.Account + "\"" : "no UnityWndClass window in a Dofus process");
                if (o == null) { steps.Clear(); return; }
                Check("character recognized from the title", o.Account.Length > 0 && o.Account != "Dofus", "\"" + Native.TitleOf(o.Target) + "\" -> \"" + o.Account + "\"");
            }, 100);
            AddUntil("ACTION: bring Dofus to the foreground", () => { if (o != null) Front(o.Target); }, () => o != null && Native.GetForegroundWindow() == o.Target, 60000);
            Add("settle", () => { o = FindOverlay() ?? o; }, 1500);

            Add("filter on: placement and darkening", () =>
            {
                Native.TryGetClientOnScreen(o.Target, out area);
                Check("filter fully shown", m.Shown >= 0.999f && o.IsShown && Native.IsWindowVisible(o.Handle), "shown=" + m.Shown + " visible=" + Native.IsWindowVisible(o.Handle));
                Check("filter fits the game area", o.Placed == area, "filter " + o.Placed + " / game " + area);
                Check("filter is topmost", Native.IsTopmost(o.Handle), "");
                CheckRegion("filter covers the whole visible game");
                Check("shader renderer in use", o.UsesShader, "");
                Check("filter not cloaked", !Native.IsCloaked(o.Handle), "");
                bOn = Scene();
                Snap("1-blue-night");
            }, 100);
            Add("filter off", () => app.RunCommand(Shortcuts.Toggle), 900);
            Add("filter off: checks", () =>
            {
                Check("filter hidden after fade-out", m.Shown == 0 && !o.IsShown, "shown=" + m.Shown);
                bOff = Scene();
                bOffZone = Luma(ZoneRect());
                bOffCorner = Luma(Corner());
                Snap("0-no-filter");
                Check("shader darkens the game (Blue night 80 %)", bOn < bOff * 0.85, "on " + F(bOn) + " / off " + F(bOff) + " = " + F(bOn / bOff * 100) + " %");
                Check("tray icon shows the off state", app.StateName == "Off", app.StateName);
                app.RunCommand(Shortcuts.Toggle);
            }, 900);
            Add("filter back on", () =>
            {
                o = FindOverlay() ?? o;
                Check("filter shown again", m.Shown >= 0.999f && o.IsShown, "shown=" + m.Shown);
                Check("tray icon shows the night state (no auto)", app.StateName == "Night", app.StateName);
            }, 100);

            // Veil renderer
            Add("veil renderer", () => { Settings.Shader = false; m.Rebuild(); app.NotifyChanged(); }, 1500);
            Add("veil checks", () =>
            {
                o = FindOverlay();
                Check("veil renderer in use after rebuild", o != null && !o.UsesShader && o.IsShown, "");
                double b = Scene();
                Snap("2-veil");
                Check("veil darkens the game", b < bOff * 0.9, "veil " + F(b) + " / off " + F(bOff));
                Settings.Shader = true; m.Rebuild(); app.NotifyChanged();
            }, 1500);
            Add("shader back", () =>
            {
                o = FindOverlay();
                Check("shader renderer back after rebuild", o != null && o.UsesShader && o.IsShown, "");
                bOn = Scene();
                Check("shader darkens the game after a rebuild too", bOn < bOff * 0.85, "on " + F(bOn) + " / off " + F(bOff) + " = " + F(bOn / bOff * 100) + " %");
            }, 100);

            // Mood transition
            Add("mood transition", () => { Settings.FadeMs = 1000; Settings.PresetIndex = 3; app.NotifyChanged(); }, 400);
            Add("mood transition: midway", () =>
            {
                Check("mood is between the two moods midway", o.Current != null && !o.Current.Same(o.From) && !o.Current.Same(o.Goal), "");
            }, 1000);
            Add("mood transition: end", () =>
            {
                Check("mood reaches the new mood", o.Current.Same(o.Goal), "");
                double b = Scene();
                Check("Pitch black is darker than Blue night", b < bOn * 0.95, F(b) + " vs " + F(bOn));
                Snap("3-pitch-black");
                Settings.PresetIndex = 0; Settings.FadeMs = 300; app.NotifyChanged();
            }, 800);

            // Per-account settings
            Add("per-account settings", () =>
            {
                AccountProfile p = Settings.Profile(o.Account, true);
                p.Own = true; p.Preset = 1; p.Intensity = 40; p.Vignette = 0;
                app.NotifyChanged();
            }, 1000);
            Add("per-account checks", () =>
            {
                Mood expected = Mood.From(new AccountParams { Preset = 1, Intensity = 40, Vignette = 0, Filter = true });
                Check("account uses its own mood", o.Goal.Same(expected) && o.Current.Same(expected), "");
                double b = Scene();
                Check("lighter own settings (Moonlight 40 %) are lighter than the default", b > bOn * 1.05, F(b) + " vs " + F(bOn));
                Settings.Profile(o.Account, false).Filter = false;
                app.NotifyChanged();
            }, 600);
            Add("account filter off", () =>
            {
                Check("unfiltered account has no filter", !o.IsShown && o.RegionInfo == "account off", "region=" + o.RegionInfo);
                Settings.Accounts.Clear(); app.NotifyChanged();
            }, 800);
            Add("account back", () => Check("filter back after forgetting the account", o.IsShown, ""), 100);

            // Bright zones
            Add("bright zone", () =>
            {
                Rectangle z = ZoneRect();
                z.Offset(-area.X, -area.Y);
                Settings.Zones = new List<Zone> { Zone.FromLocal(z, area.Size) };
                Settings.ZonesEnabled = true;
                app.NotifyChanged();
            }, 800);
            Add("bright zone checks", () =>
            {
                Check("filter is cut around the zone", o.IsShown && o.RegionInfo.Contains("rect"), "region=" + o.RegionInfo);
                double inside = Luma(ZoneRect()), outside = Scene();
                Snap("4-bright-zone");
                Check("zone stays bright", inside > bOffZone * 0.9, "zone " + F(inside) + " / unfiltered " + F(bOffZone));
                Check("outside the zone stays dark", outside < bOff * 0.85, F(outside) + " / " + F(bOff) + " (K=" + F(o.Current.K) + " shown=" + F(m.Shown) + " frames=" + o.Frames + " fg=" + (Native.GetForegroundWindow() == o.Target) + ")");
                Settings.ZonesEnabled = false; app.NotifyChanged();
            }, 600);
            Add("zone off", () => CheckRegion("filter whole again without zones"), 100);

            // Vignette
            Add("vignette strong", () => { Settings.Vignette = 3; app.NotifyChanged(); }, 1000);
            Add("vignette checks", () =>
            {
                double corner = Luma(Corner());
                Check("vignette layer shown", o.Vignette.IsShown, "");
                Check("vignette darkens the corners", corner < bOffCorner * 0.6, "corner " + F(corner) + " / unfiltered " + F(bOffCorner));
                Snap("5-vignette");
                Settings.VignetteColor = Color.FromArgb(80, 0, 0); app.NotifyChanged();
            }, 600);
            Add("vignette tint", () =>
            {
                Check("vignette still shown after a tint change", o.Vignette.IsShown, "");
                Settings.Vignette = 0; Settings.VignetteColor = Settings.DefaultVignetteColor; app.NotifyChanged();
            }, 600);
            Add("vignette off", () => Check("vignette hidden at None", !o.Vignette.IsShown, ""), 100);

            // Grain
            int framesBefore = 0;
            Add("film grain", () => { Settings.Grain = 2; app.NotifyChanged(); }, 600);
            Add("film grain frames", () => { framesBefore = o.Grain.FramesPushed; }, 1000);
            Add("film grain checks", () =>
            {
                Check("grain layer shown", o.Grain.IsShown, "");
                Snap("6-grain");
                int frames = o.Grain.FramesPushed - framesBefore;
                Check("grain is animated (~12 frames/s)", frames >= 8 && frames <= 16, frames + " frames in 1 s");
                Check("grain preparation under 1.5 s", o.Grain.LastBuildMs < 1500, o.Grain.LastBuildMs + " ms for " + area.Size);
                Settings.Grain = 0; app.NotifyChanged();
            }, 500);
            Add("grain off", () => Check("grain hidden at None", !o.Grain.IsShown, ""), 100);

            // Automatic mode
            Add("auto: day", () =>
            {
                int now = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
                Settings.Dusk = (now + 120) % 1440; Settings.Night = (now + 180) % 1440;
                Settings.Dawn = (now + 240) % 1440; Settings.Day = (now + 300) % 1440;
                Settings.Auto = true; app.NotifyChanged();
            }, 900);
            Add("auto: day checks", () =>
            {
                Check("no filter by day", m.AutoFactor == 0 && m.Shown == 0 && !o.IsShown, "factor=" + m.AutoFactor + " shown=" + m.Shown);
                Check("tray icon shows the day state", app.StateName == "Day", app.StateName);
                int now = DateTime.Now.Hour * 60 + DateTime.Now.Minute;
                Settings.Dusk = (now + 1440 - 30) % 1440; Settings.Night = (now + 30) % 1440;
                Settings.Dawn = (now + 240) % 1440; Settings.Day = (now + 300) % 1440;
                app.NotifyChanged();
            }, 900);
            Add("auto: dusk checks", () =>
            {
                Check("half filter at mid-dusk", m.AutoFactor > 0.35f && m.AutoFactor < 0.65f && Math.Abs(m.Shown - m.AutoFactor) < 0.05f && o.IsShown,
                    "factor=" + m.AutoFactor + " shown=" + m.Shown);
                Check("tray icon shows the dusk state", app.StateName == "Twilight", app.StateName);
                Settings.SunMode = true; Settings.SunCity = "Paris"; Settings.SunLat = 48.857; Settings.SunLon = 2.352;
                app.NotifyChanged();
            }, 600);
            Add("auto: sun checks", () =>
            {
                float expected = DayCycle.Factor(DateTime.Now);
                Check("sun mode follows the computed sunset / sunrise", Math.Abs(m.AutoFactor - expected) < 0.01f, "factor=" + m.AutoFactor + " expected=" + expected + " (" + DayCycle.Describe(DateTime.Now) + ")");
                Settings.Auto = false; Settings.SunMode = false; app.NotifyChanged();
            }, 600);
            Add("auto off", () => Check("full filter without auto", m.Shown >= 0.999f && o.IsShown, "shown=" + m.Shown), 100);

            // Shortcuts registration
            Add("custom shortcut", () =>
            {
                Check("default shortcuts all registered", app.FailedHotkeys.Count == 0, app.FailedHotkeys.Count + " unavailable");
                Settings.Hotkeys[Shortcuts.Toggle] = Keys.Control | Keys.Alt | Keys.F12;
                app.NotifyChanged();
                Check("custom shortcut Ctrl+Alt+F12 registered", !app.FailedHotkeys.Contains(Shortcuts.Toggle), "");
                Settings.Hotkeys = Shortcuts.Defaults(); app.NotifyChanged();
                Check("defaults registered again", app.FailedHotkeys.Count == 0, "");
            }, 100);

            Add("files: export / import / portable / startup", FileChecks, 100);
            AddSettingsWindowSteps();

            // Manual phase 1: shortcuts and peek
            Add("defaults before manual phases", Defaults, 800);
            long hkStart = 0;
            long lastCommand = 0;
            app.CommandReceived += cmd => lastCommand = clock.ElapsedMilliseconds;
            AddUntil("MANUAL 1 (ends 6 s after the last shortcut, max 90 s): with Dofus in front press Ctrl+Alt+N, Ctrl+Alt+N, Ctrl+Alt+P, Ctrl+Alt+PgDn, Ctrl+Alt+V, Ctrl+Alt+A, Ctrl+Alt+A, then HOLD Ctrl+Alt+C for 2 s",
                () => { commands.Clear(); hkStart = clock.ElapsedMilliseconds; lastCommand = 0; },
                () => lastCommand > 0 && clock.ElapsedMilliseconds - lastCommand > 6000 && !m.Peeking, 90000);
            Add("manual 1 checks", () =>
            {
                string[] names = { "toggle", "auto", "preset", "up", "down", "vignette", "peek" };
                foreach (int id in new[] { Shortcuts.Toggle, Shortcuts.Preset, Shortcuts.Down, Shortcuts.Vignette, Shortcuts.Auto, Shortcuts.Peek })
                {
                    if (commands.Contains(id)) Check("shortcut received: " + names[id], true, "");
                    else Skip("shortcut received: " + names[id], "not pressed during the phase");
                }
                bool peekSeen = false, darkDuringPeek = false, backAfter = false;
                long peekEnd = 0;
                foreach (Sample s in samples)
                {
                    if (s.T < hkStart) continue;
                    if (s.Peeking) { peekSeen = true; peekEnd = s.T; if (s.Amount > 0.05f && s.T > FirstPeek(hkStart) + 300) darkDuringPeek = true; }
                }
                foreach (Sample s in samples) if (peekEnd > 0 && s.T > peekEnd + 500 && s.Amount > 0.99f) backAfter = true;
                if (!peekSeen) Skip("peek", "Ctrl+Alt+C was not held");
                else
                {
                    Check("peek removes the filter while held", !darkDuringPeek, "");
                    Check("peek brings the filter back on release", backAfter || !Settings.Enabled, "");
                }
                Defaults();
            }, 800);

            // Manual phase 2: covered game and "only in the foreground"
            long p2 = 0, p3 = 0;
            Add("MANUAL 2 (15 s): put a window (Notepad) partly over Dofus, then click Dofus again",
                () => { p2 = clock.ElapsedMilliseconds; }, 15000);
            Add("MANUAL 3 (15 s, only-in-foreground ON): put Notepad in front, then click Dofus again",
                () => { p3 = clock.ElapsedMilliseconds; Settings.OnlyForeground = true; app.NotifyChanged(); }, 15000);
            Add("manual 2/3 checks", () =>
            {
                bool coveredSeen = false, coveredCut = true;
                foreach (Sample s in samples)
                    if (s.T >= p2 + 300 && s.T < p3 && s.FgOverGame)
                    {
                        coveredSeen = true;
                        if (!(s.Shown && s.Region != "full")) coveredCut = false;
                    }
                if (!coveredSeen) Skip("filter cut around a window over Dofus", "no window went over Dofus");
                else Check("filter cut around a window over Dofus (not hidden, not over it)", coveredCut, "");

                bool bgSeen = false, fgSeen = false, ok = true;
                Sample prev = null;
                foreach (Sample s in samples)
                {
                    if (s.T < p3 + 500) { prev = s; continue; }
                    if (prev != null && prev.GameFg == s.GameFg)  // stable state for 2 samples
                    {
                        if (s.GameFg) { fgSeen = true; if (!s.Shown) ok = false; }
                        else { bgSeen = true; if (s.Shown) ok = false; }
                    }
                    prev = s;
                }
                if (!bgSeen || !fgSeen) Skip("only-in-foreground", "Dofus did not go to the background and back");
                else Check("only-in-foreground hides the filter when Dofus is not in front", ok, "");
                Defaults();
            }, 500);

            AddMultiAccountSteps();

            // Performance
            double cpuStart = 0; long wallStart = 0; int perfFrames = 0;
            Add("performance (10 s, shader + light vignette)", () =>
            {
                Settings.Vignette = 1; app.NotifyChanged();
                m.MaxTickMs = 0; m.TotalTickMs = 0; m.TickCount = 0; m.RenderMs = 0; m.RenderCount = 0;
                Overlay front = FindOverlay();
                perfFrames = front != null ? front.Frames : 0;
                cpuStart = Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;
                wallStart = clock.ElapsedMilliseconds;
            }, 10000);
            Add("performance checks", () =>
            {
                var proc = Process.GetCurrentProcess();
                double cpu = (proc.TotalProcessorTime.TotalMilliseconds - cpuStart) / (clock.ElapsedMilliseconds - wallStart) * 100;
                double avg = m.TickCount > 0 ? m.TotalTickMs / m.TickCount : 0;
                Check("no frozen tick (max under 100 ms)", m.MaxTickMs < 100, "average " + avg.ToString("0.00") + " ms over " + m.TickCount + " ticks (includes waiting for the screen refresh), max " + m.MaxTickMs.ToString("0.0") + " ms");
                Check("CPU under 15 % of one core", cpu < 15, cpu.ToString("0.0") + " % of one core");
                Overlay front = FindOverlay();
                double fps = front != null ? (front.Frames - perfFrames) / ((clock.ElapsedMilliseconds - wallStart) / 1000.0) : 0;
                Check("shader refreshed smoothly (>= 25 frames/s, pace of the Magnification API)", fps >= 25, fps.ToString("0") + " frames/s");
                double render = m.RenderCount > 0 ? m.RenderMs / m.RenderCount : 0;
                Check("shader frame cost measured", m.RenderCount > 0, render.ToString("0.00") + " ms per frame over " + m.RenderCount + " frames");
                Check("memory under 150 MB", proc.WorkingSet64 < 150L * 1024 * 1024, (proc.WorkingSet64 / 1024 / 1024) + " MB");
                Check("diagnostics readable", m.Diagnostics().Contains("Game windows: ") && !m.Diagnostics().Contains("6.2.9200"), "");
                Defaults();
            }, 300);
        }

        // ------------------------------------------------------------ several accounts

        void AddMultiAccountSteps()
        {
            bool multi = false;
            Overlay a = null, b = null;
            var lumA = new List<double>();
            var lumB = new List<double>();
            var saved = new Dictionary<IntPtr, Rectangle>();
            var zoomed = new Dictionary<IntPtr, bool>();
            double cpuStart = 0; long wallStart = 0; int framesA = 0, framesB = 0;

            Add("multi-account: detect", () =>
            {
                Defaults();
                var all = new List<Overlay>(m.All);
                if (all.Count < 2) { Skip("multi-account", "only one Dofus window open"); return; }
                multi = true;
                a = all[0]; b = all[1];
                Check("two Dofus windows detected", true, "\"" + a.Account + "\" and \"" + b.Account + "\"");
                Check("two different characters", a.Account != b.Account, "");
            }, 500);

            // Alternate the foreground: the darkening of each account must stay the same.
            for (int k = 0; k < 6; k++)
            {
                int kk = k;
                Add("multi-account: switch " + (k + 1) + "/6", () => { if (multi) Front((kk % 2 == 0 ? a : b).Target); }, 1200);
                Add("multi-account: switch " + (k + 1) + " checks", () =>
                {
                    if (!multi) return;
                    Overlay front = kk % 2 == 0 ? a : b, back = kk % 2 == 0 ? b : a;
                    if (Native.GetForegroundWindow() != front.Target) { Skip("switch " + (kk + 1), "Windows refused to change the foreground"); return; }
                    o = front;
                    Native.TryGetClientOnScreen(front.Target, out area);
                    Check("switch " + (kk + 1) + ": front account filtered", front.IsShown, front.Account + " region=" + front.RegionInfo);
                    Rectangle frontPlaced = front.Placed, backPlaced = back.Placed;
                    Check("switch " + (kk + 1) + ": filters never overlap", !back.IsShown || !Native.TryGetClientOnScreen(back.Target, out area) || back.RegionInfo != "full" || !frontPlaced.IntersectsWith(backPlaced),
                        back.Account + " region=" + back.RegionInfo);
                    Native.TryGetClientOnScreen(front.Target, out area);
                    (kk % 2 == 0 ? lumA : lumB).Add(Scene());
                }, 100);
            }
            Add("multi-account: stability", () =>
            {
                if (!multi) return;
                CheckStable(a.Account, lumA);
                CheckStable(b.Account, lumB);
            }, 100);

            // Same settings must give the same tint: the filter changes red, green and blue of each
            // window by the same ratios (measured filter on / filter off, each window in front).
            double[] onA = null, offA = null, onB = null, offB = null;
            int savedFade = 0;
            Add("multi-account: tint, first account", () =>
            {
                if (!multi) return;
                savedFade = Settings.FadeMs; Settings.FadeMs = 0; app.NotifyChanged();
                o = a; Front(a.Target);
            }, 1000);
            Add("multi-account: tint, first account off", () => { if (!multi) return; onA = SceneRgb(); Settings.Enabled = false; app.NotifyChanged(); }, 700);
            Add("multi-account: tint, second account", () => { if (!multi) return; offA = SceneRgb(); Settings.Enabled = true; app.NotifyChanged(); o = b; Front(b.Target); }, 1000);
            Add("multi-account: tint, second account off", () => { if (!multi) return; onB = SceneRgb(); Settings.Enabled = false; app.NotifyChanged(); }, 700);
            Add("multi-account: tint checks", () =>
            {
                if (!multi) return;
                offB = SceneRgb();
                Settings.Enabled = true; Settings.FadeMs = savedFade; app.NotifyChanged();
                double worst = 0;
                var ra = new double[3]; var rb = new double[3];
                for (int c = 0; c < 3; c++)
                {
                    ra[c] = onA[c] / Math.Max(1, offA[c]); rb[c] = onB[c] / Math.Max(1, offB[c]);
                    worst = Math.Max(worst, Math.Abs(ra[c] - rb[c]));
                }
                Check("same settings give the same tint on both accounts", worst < 0.06,
                      a.Account + " " + Rgb(ra) + " vs " + b.Account + " " + Rgb(rb) + " (filtered / unfiltered, red/green/blue)");
                o = a; Front(a.Target);
            }, 1000);

            // Exception on one account
            Add("multi-account: exception", () =>
            {
                if (!multi) return;
                Settings.Profile(b.Account, true).Filter = false;
                app.NotifyChanged();
                Front(b.Target);
            }, 1200);
            Add("multi-account: exception checks", () =>
            {
                if (!multi) return;
                Check("excluded account has no filter", !b.IsShown && b.RegionInfo == "account off", "region=" + b.RegionInfo);
                Front(a.Target);
            }, 1200);
            Add("multi-account: other account still filtered", () =>
            {
                if (!multi) return;
                Check("the other account keeps its filter", a.IsShown, "region=" + a.RegionInfo);
                Settings.Accounts.Clear(); app.NotifyChanged();
            }, 300);

            // Side by side: both visible, both rendered
            Add("multi-account: side by side", () =>
            {
                if (!multi) return;
                Rectangle work = Screen.FromHandle(a.Target).WorkingArea;
                int i = 0;
                foreach (Overlay x in new[] { a, b })
                {
                    Rectangle r;
                    Native.TryGetBounds(x.Target, out r);
                    saved[x.Target] = r;
                    zoomed[x.Target] = IsZoomed(x.Target);
                    Native.ShowWindow(x.Target, 9 /* SW_RESTORE */);
                    Native.SetWindowPos(x.Target, IntPtr.Zero, work.X + i * work.Width / 2, work.Y, work.Width / 2, work.Height, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                    i++;
                }
                Front(a.Target);
            }, 3000);
            Add("multi-account: side by side, 5 s", () =>
            {
                if (!multi) return;
                framesA = a.Frames; framesB = b.Frames;
                m.MaxTickMs = 0; m.TotalTickMs = 0; m.TickCount = 0; m.RenderMs = 0; m.RenderCount = 0;
                cpuStart = Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;
                wallStart = clock.ElapsedMilliseconds;
            }, 5000);
            Add("multi-account: side by side checks", () =>
            {
                if (!multi) return;
                double secs = (clock.ElapsedMilliseconds - wallStart) / 1000.0;
                double fpsA = (a.Frames - framesA) / secs, fpsB = (b.Frames - framesB) / secs;
                double cpu = (Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds - cpuStart) / (secs * 1000) * 100;
                bool fgIsA = Native.GetForegroundWindow() == a.Target;
                Check("both accounts filtered side by side", a.IsShown && b.IsShown, a.RegionInfo + " / " + b.RegionInfo);
                double fast = Math.Max(fpsA, fpsB), slow = Math.Min(fpsA, fpsB);
                Check("active account refreshed smoothly (>= 15 frames/s)", fast >= 15, fpsA.ToString("0") + " / " + fpsB.ToString("0") + " fps" + (fgIsA || Native.GetForegroundWindow() == b.Target ? "" : " (no account in front)"));
                Check("other account still refreshed (>= 4 frames/s)", slow >= 4, slow.ToString("0") + " fps");
                Check("CPU with two accounts under 20 % of one core", cpu < 20, cpu.ToString("0.0") + " %, shader frame " + (m.RenderCount > 0 ? (m.RenderMs / m.RenderCount).ToString("0.0") : "-") + " ms");
                foreach (Overlay x in new[] { a, b })
                {
                    if (zoomed[x.Target]) Native.ShowWindow(x.Target, 3 /* SW_MAXIMIZE */);
                    else { Rectangle r = saved[x.Target]; Native.SetWindowPos(x.Target, IntPtr.Zero, r.X, r.Y, r.Width, r.Height, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE); }
                }
                Front(a.Target);
            }, 2500);
        }

        void CheckStable(string account, List<double> values)
        {
            if (values.Count < 2) { Skip("darkening stable for " + account, "not enough switches measured"); return; }
            double min = double.MaxValue, max = 0;
            foreach (double v in values) { min = Math.Min(min, v); max = Math.Max(max, v); }
            var list = new List<string>();
            foreach (double v in values) list.Add(F(v));
            Check("darkening stable for " + account + " across switches (no build-up)", max / Math.Max(1, min) < 1.15, string.Join(" / ", list.ToArray()));
        }

        long FirstPeek(long from)
        {
            foreach (Sample s in samples) if (s.T >= from && s.Peeking) return s.T;
            return long.MaxValue;
        }

        // ------------------------------------------------------------ unit checks

        void UnitChecks()
        {
            string defaults = Path.Combine(dir, "defaults.ini");
            Settings.SaveTo(defaults);

            // Sun: Paris, reference times from published ephemerides (local time, ±5 min).
            CheckSun(new DateTime(2026, 6, 21), 5 * 60 + 47, 21 * 60 + 58);
            CheckSun(new DateTime(2026, 12, 21), 8 * 60 + 42, 16 * 60 + 56);
            CheckSun(new DateTime(2026, 10, 29), 7 * 60 + 33, 17 * 60 + 31);
            double r, s;
            Check("no sunset in polar night (Tromsø, December)", !Sun.Compute(new DateTime(2026, 12, 21), 69.65, 18.96, out r, out s), "");

            // Day cycle with fixed times
            Settings.SunMode = false;
            Settings.Dusk = 19 * 60 + 30; Settings.Night = 21 * 60 + 30; Settings.Dawn = 6 * 60 + 30; Settings.Day = 8 * 60 + 30;
            Check("day cycle: noon is day", DayCycle.Factor(new DateTime(2026, 1, 1, 12, 0, 0)) == 0, "");
            Check("day cycle: 20:30 is half dusk", Math.Abs(DayCycle.Factor(new DateTime(2026, 1, 1, 20, 30, 0)) - 0.5f) < 0.01f, "");
            Check("day cycle: 03:00 is night", DayCycle.Factor(new DateTime(2026, 1, 1, 3, 0, 0)) == 1, "");
            Check("day cycle: 07:30 is half dawn", Math.Abs(DayCycle.Factor(new DateTime(2026, 1, 1, 7, 30, 0)) - 0.5f) < 0.01f, "");

            // Zones
            Zone z = Zone.FromLocal(new Rectangle(10, 900, 200, 100), new Size(1920, 1017));
            Check("zone anchors: bottom-left", z.Ax == 0 && z.Ay == 2, "Ax=" + z.Ax + " Ay=" + z.Ay);
            Settings.ZonesAnchored = true; Settings.ZonesScale = true;
            Check("anchored zone on the same size", z.Resolve(new Rectangle(0, 0, 1920, 1017)) == new Rectangle(10, 900, 200, 100), z.Resolve(new Rectangle(0, 0, 1920, 1017)).ToString());
            Rectangle half = z.Resolve(new Rectangle(100, 50, 960, 508));
            Check("anchored zone on a half-size window", Near(half, new Rectangle(105, 50 + 450, 100, 50)), half.ToString());
            Settings.ZonesScale = false;
            Rectangle fixedSize = z.Resolve(new Rectangle(0, 0, 1280, 720));
            Check("anchored zone without scaling keeps its size and bottom margin", Near(fixedSize, new Rectangle(10, 720 - 17 - 100, 200, 100)), fixedSize.ToString());
            Settings.ZonesAnchored = false; Settings.ZonesScale = true;
            Rectangle prop = z.Resolve(new Rectangle(0, 0, 960, 508));
            Check("proportional zone", Near(prop, new Rectangle(5, 450, 100, 50)), prop.ToString());
            Settings.ZonesAnchored = true;
            Check("zone serialization round-trip", Zone.Parse(z.Serialize()) != null && Zone.Parse(z.Serialize()).Serialize() == z.Serialize(), z.Serialize());

            // Look / mood
            Mood blue = Mood.From(new AccountParams { Preset = 0, Intensity = 80, Vignette = 2 });
            Look none = Look.From(blue, 0);
            bool identity = true;
            for (int i = 0; i < 25; i++) if (Math.Abs(none.Matrix[i] - (i % 6 == 0 ? 1 : 0)) > 1e-5) identity = false;
            Check("no effect at amount 0 (identity matrix)", identity && none.VignetteOpacity == 0 && none.VeilAlpha == 0, "");
            Look full = Look.From(blue, 1);
            Check("darkening at amount 1", full.Matrix[0] + full.Matrix[5] + full.Matrix[10] < 2.4f, "");
            Mood black = Mood.From(new AccountParams { Preset = 3, Intensity = 80, Vignette = 0 });
            Mood mid = Mood.Lerp(blue, black, 0.5f);
            Check("mood blend midway", Math.Abs(mid.B - (blue.B + black.B) / 2) < 1e-4 && !mid.Same(blue) && !mid.Same(black), "");

            // Shortcuts
            Check("shortcut text", Shortcuts.Format(Keys.Control | Keys.Alt | Keys.N) == "Ctrl+Alt+N", Shortcuts.Format(Keys.Control | Keys.Alt | Keys.N));
            Check("a key alone is refused, F keys accepted", !Shortcuts.IsValid(Keys.N) && Shortcuts.IsValid(Keys.F5) && Shortcuts.IsValid(Keys.Shift | Keys.N), "");

            // Settings round-trip
            Settings.PresetIndex = Preset.CustomIndex; Settings.Intensity = 65; Settings.Vignette = 2; Settings.Grain = 1;
            Settings.VignetteColor = Color.FromArgb(10, 20, 30); Settings.Auto = true; Settings.SunMode = true; Settings.SunCity = "Lyon";
            Settings.SunLat = 45.764; Settings.SunLon = 4.836; Settings.FadeMs = 1200; Settings.OnlyForeground = true;
            Settings.CustomBrightness = 42; Settings.CustomSaturation = 33; Settings.CustomTemperature = -60; Settings.CustomLift = 20;
            Settings.Zones = new List<Zone> { z }; Settings.ZonesEnabled = true; Settings.ZonesAnchored = false; Settings.ZonesScale = false;
            Settings.Accounts.Clear();
            Settings.Accounts["Édéïyna"] = new AccountProfile { Filter = false, Own = true, Preset = 4, Intensity = 55, Vignette = 3 };
            Settings.Hotkeys[Shortcuts.Peek] = Keys.Control | Keys.Shift | Keys.F9;
            string a = Path.Combine(dir, "roundtrip-a.ini"), b = Path.Combine(dir, "roundtrip-b.ini");
            Settings.SaveTo(a);
            Settings.LoadFrom(defaults);
            bool loaded = Settings.LoadFrom(a);
            Settings.SaveTo(b);
            Check("settings round-trip (every field, accents included)", loaded && File.ReadAllText(a) == File.ReadAllText(b)
                && Settings.Accounts.ContainsKey("Édéïyna") && Settings.Hotkeys[Shortcuts.Peek] == (Keys.Control | Keys.Shift | Keys.F9), "");

            string junk = Path.Combine(dir, "junk.ini");
            File.WriteAllText(junk, "hello\nworld");
            Check("a file that is not Pénombre settings is refused", !Settings.LoadFrom(junk), "");

            Settings.LoadFrom(defaults);
            Settings.Accounts.Clear();
        }

        void CheckSun(DateTime day, int rise, int set)
        {
            double r, s;
            bool ok = Sun.Compute(day, 48.857, 2.352, out r, out s);
            Check("sun times Paris " + day.ToString("yyyy-MM-dd"), ok && Math.Abs(r - rise) <= 5 && Math.Abs(s - set) <= 5,
                "rise " + Settings.FormatTime((int)r) + " (ref " + Settings.FormatTime(rise) + "), set " + Settings.FormatTime((int)s) + " (ref " + Settings.FormatTime(set) + ")");
        }

        static bool Near(Rectangle a, Rectangle b)
        {
            return Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1 && Math.Abs(a.Width - b.Width) <= 1 && Math.Abs(a.Height - b.Height) <= 1;
        }

        // ------------------------------------------------------------ files

        void FileChecks()
        {
            // Export / import
            string export = Path.Combine(dir, "export.ini");
            Settings.Intensity = 70; Settings.SaveTo(export);
            Settings.Intensity = 20;
            Check("import restores exported settings", Settings.LoadFrom(export) && Settings.Intensity == 70, "intensity=" + Settings.Intensity);
            Settings.Intensity = 80;

            // Reset to defaults (then the test settings come back)
            string kept = Path.Combine(dir, "before-reset.ini");
            Settings.Intensity = 33; Settings.PresetIndex = 2; Settings.TrayIconAuto = false;
            Settings.Profile("ResetTest", true).Own = true;
            Settings.Zones = new List<Zone> { Zone.FromLocal(new Rectangle(1, 1, 50, 20), new Size(800, 600)) };
            Settings.Hotkeys[Shortcuts.Toggle] = Keys.Control | Keys.Alt | Keys.F12;
            Settings.SaveTo(kept);
            Settings.ResetToDefaults();
            Keys[] firstKeys = Shortcuts.Defaults();
            bool sameKeys = true;
            for (int i = 0; i < Shortcuts.Count; i++) if (Settings.Hotkeys[i] != firstKeys[i]) sameKeys = false;
            Check("reset puts the settings of a first start back",
                  Settings.Intensity == 80 && Settings.PresetIndex == 0 && Settings.TrayIconAuto && Settings.Accounts.Count == 0
                  && Settings.Zones.Count == 0 && sameKeys && Settings.FadeMs == 600,
                  "intensity=" + Settings.Intensity + " preset=" + Settings.PresetIndex + " accounts=" + Settings.Accounts.Count
                  + " zones=" + Settings.Zones.Count + " keys=" + sameKeys + " fade=" + Settings.FadeMs);
            Settings.LoadFrom(kept);
            Settings.Accounts.Remove("ResetTest"); Settings.Zones = new List<Zone>(); Settings.Hotkeys = Shortcuts.Defaults();
            Settings.Intensity = 80; Settings.PresetIndex = 0; Settings.TrayIconAuto = true;
            app.NotifyChanged();

            // Portable mode
            string portable = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), Settings.PortableFolderName);
            if (Directory.Exists(portable)) Skip("portable mode", "a Penombre-data folder already exists next to the exe");
            else
            {
                string before = Settings.Folder;
                bool on = Settings.SetPortable(true);
                Check("portable mode on: settings next to the exe", on && Settings.IsPortable && File.Exists(Path.Combine(portable, "settings.ini")), Settings.Folder);
                bool off = Settings.SetPortable(false);
                Check("portable mode off: back to the previous folder, portable folder removed", off && Settings.Folder == before && !Directory.Exists(portable), Settings.Folder);
            }

            // Start with Windows (the previous value is restored as it was)
            const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
            object original = null;
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(runKey)) if (k != null) original = k.GetValue("Penombre");
            Startup.Set(true);
            object now = null;
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(runKey)) if (k != null) now = k.GetValue("Penombre");
            Check("start with Windows writes the exe path", Startup.IsEnabled() && now is string && ((string)now).Contains(Path.GetFileName(Application.ExecutablePath)), now as string);
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(runKey))
            {
                if (original != null) k.SetValue("Penombre", original);
                else k.DeleteValue("Penombre", false);
            }
            Check("start with Windows restored as it was", Startup.IsEnabled() == (original != null), original == null ? "(was off)" : "(was on)");
        }

        // ------------------------------------------------------------ settings window

        // No DoEvents here: the shader keeps the message queue busy, so each step lets the
        // normal message loop run between actions.
        void AddSettingsWindowSteps()
        {
            SettingsForm form = null;
            var watch = new Stopwatch();
            long shownMs = -1;
            bool fits = true;
            var detail = new StringBuilder();

            Add("settings window: open", () =>
            {
                form = new SettingsForm(m, app.NotifyChanged, () => app.FailedHotkeys);
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(40, 40);
                form.Shown += (s, e) => shownMs = watch.ElapsedMilliseconds;
                watch.Start();
                form.Show();
            }, 1500);
            Add("settings window: size", () =>
            {
                // The remote-control tool used for testing minimizes windows it cannot drive: restore.
                if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
                Check("settings window opens quickly", shownMs >= 0 && shownMs < 1000, shownMs + " ms");
                Rectangle work = Screen.FromControl(form).WorkingArea;
                Check("settings window fits the screen", form.Width <= work.Width && form.Height <= work.Height, form.Size + " in " + work.Size);
                Check("settings window has a real size", form.Width >= 450 && form.Height >= 350, form.Size.ToString());
                Check("settings window has its 6 pages", form.PageCount == 6, form.PageCount.ToString());
                Check("embedded fonts loaded (Nunito)", Theme.FontsLoaded, "");
                Check("mood preview shows the game itself, without the filter", form.RefreshPreview(), "PrintWindow");
            }, 300);
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                Add("settings tab " + (i + 1) + ": select", () =>
                {
                    if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
                    if (idx < form.PageCount) form.SelectPage(idx);
                }, 400);
                Add("settings tab " + (i + 1) + ": check", () =>
                {
                    if (idx >= form.PageCount) { fits = false; return; }
                    Control host = form.PageHost, content = form.Page(idx);
                    if (!content.Visible || content.Right > host.ClientSize.Width || content.Bottom > host.ClientSize.Height || host.ClientSize.Width < 200)
                    {
                        fits = false;
                        detail.Append(form.PageTitle(idx) + " " + content.Bounds + " > " + host.ClientSize + "; ");
                    }
                    var draw = Stopwatch.StartNew();
                    using (var bmp = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                        bmp.Save(Path.Combine(dir, "settings-tab" + (idx + 1) + ".png"), ImageFormat.Png);
                    }
                    if (draw.ElapsedMilliseconds > 1000) { fits = false; detail.Append(form.PageTitle(idx) + " drawn in " + draw.ElapsedMilliseconds + " ms; "); }
                }, 100);
            }
            Add("settings window: close", () =>
            {
                Check("every settings page fits in the window (and draws in under 1 s)", fits, detail.ToString());
                form.LoadAll();
                Check("settings window reloads without error", true, "");
                form.Close();
                form.Dispose();
                using (var editor = new ZoneEditor(area, new List<Zone> { Zone.FromLocal(new Rectangle(10, 10, 100, 50), area.Size) }))
                    Check("zone editor keeps existing zones", editor.Result().Count == 1, "");

                int pictures = 0;
                for (int st = 0; st < TrayIcons.StyleCount; st++)
                    for (int state = 0; state < TrayIcons.StateCount; state++)
                        foreach (int size in new[] { 16, 24, 32 })
                            if (TrayIcons.Picture(st, state, size) != null) pictures++;
                Check("notification icons: 3 styles x 4 states at every size", pictures == 36, pictures + " / 36");
                Settings.TrayIcon = 1; app.NotifyChanged();
                int watcher = app.IconKey;
                Settings.TrayIcon = 2; app.NotifyChanged();
                int moon = app.IconKey;
                Settings.TrayIcon = 0; app.NotifyChanged();
                Check("notification icon follows the chosen style", watcher / TrayIcons.StateCount == 1 && moon / TrayIcons.StateCount == 2 && app.IconKey / TrayIcons.StateCount == 0,
                      watcher + " / " + moon + " / " + app.IconKey);

                bool wasEnabled = Settings.Enabled;
                Settings.Enabled = true; Settings.TrayIconAuto = false; Settings.TrayIconColor = TrayIcons.Twilight; app.NotifyChanged();
                int kept = app.IconKey;
                Settings.Enabled = false; app.NotifyChanged();
                int off = app.IconKey;
                Settings.Enabled = wasEnabled; Settings.TrayIconAuto = true; Settings.TrayIconColor = TrayIcons.Night; app.NotifyChanged();
                Check("notification icon keeps the chosen picture, grey when the filter is off",
                      kept % TrayIcons.StateCount == TrayIcons.Twilight && off % TrayIcons.StateCount == TrayIcons.Off, kept + " / " + off);
            }, 300);
        }

        static T Find<T>(Control root) where T : Control
        {
            foreach (Control c in root.Controls)
            {
                if (c is T) return (T)c;
                T inner = Find<T>(c);
                if (inner != null) return inner;
            }
            return null;
        }
    }
}
