// Pénombre - A night mode for Dofus 3: a click-through filter laid over the game window.
// Copyright (C) 2026 Sokenzane
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.
//
// Pénombre is an independent project, not affiliated with or endorsed by Ankama.
// Dofus is a trademark of Ankama.
//
// Single-file WinForms app in C# 5, built with the csc.exe shipped with Windows (build.bat).
// Nothing is injected into, read from or sent to the Dofus client: the filter is a separate
// window. Two renderers:
//   - Shader: the Windows Magnification API redraws the game area through a 5x5 color matrix
//     (real per-pixel darkening, desaturation and tint);
//   - Veil: a plain semi-transparent colored window (fallback).
// A per-pixel alpha vignette can be added on top of either.
//
// Multi-account: every filter is clipped (SetWindowRgn) to the part of its game window that is
// actually visible, so two filters never overlap and the darkening never stacks up. Zones of
// the game UI can also be cut out of the filter so they stay bright.
//
// Options: --lang=en|fr

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Pénombre")]
[assembly: AssemblyDescription("A night mode for Dofus 3")]
[assembly: AssemblyProduct("Pénombre")]
[assembly: AssemblyCopyright("Copyright (C) 2026 Sokenzane - GPL-3.0")]
[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]

namespace Penombre
{
    static class Program
    {
        public const string Version = "0.3.0";
        static bool errorShown;

        [STAThread]
        static void Main(string[] args)
        {
            string lang = null;
            foreach (string a in args)
                if (a.StartsWith("--lang=")) lang = a.Substring(7);
            T.Init(lang);

            bool first;
            using (var mutex = new System.Threading.Mutex(true, "Sokenzane.Penombre", out first))
            {
                if (!first)
                {
                    MessageBox.Show(T.S("already"), "Pénombre", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Real pixels, even with Windows display scaling (125 %, 150 %...).
                try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); }
                catch { try { Native.SetProcessDPIAware(); } catch { } }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => OnError(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Error("Unhandled exception", e.ExceptionObject as Exception);

                Settings.Load();
                Log.Info("Pénombre " + Program.Version + " started - Windows " + Environment.OSVersion.Version
                         + (Environment.Is64BitProcess ? " x64" : " x86"));
                Mag.Init();
                Startup.RefreshPath();
                try
                {
                    using (var app = new TrayApp())
                        Application.Run();
                }
                finally
                {
                    Settings.Save();
                    Mag.Shutdown();
                    Log.Info("Stopped");
                }
            }
        }

        static void OnError(Exception ex)
        {
            Log.Error("UI thread exception", ex);
            if (errorShown) return;
            errorShown = true;
            MessageBox.Show(T.S("error") + "\n\n" + ex.Message, "Pénombre", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ================================================================ Error log

    static class Log
    {
        const long MaxSize = 512 * 1024;
        static readonly object sync = new object();

        public static string FilePath { get { return Path.Combine(Settings.Folder, "penombre.log"); } }

        public static void Info(string message) { Write("INFO ", message); }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR", ex == null ? message : message + " - " + ex);
        }

        static void Write(string level, string message)
        {
            try
            {
                lock (sync)
                {
                    Directory.CreateDirectory(Settings.Folder);
                    var info = new FileInfo(FilePath);
                    if (info.Exists && info.Length > MaxSize)
                    {
                        string old = FilePath + ".old";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(FilePath, old);
                    }
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                        + " " + level + " " + message + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }

    // ================================================================ Win32

    static class Native
    {
        public delegate bool EnumProc(IntPtr h, IntPtr l);
        public delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool repaint);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern bool InvalidateRect(IntPtr h, IntPtr rect, bool erase);
        [DllImport("user32.dll")] public static extern int SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
        [DllImport("user32.dll")] public static extern bool GetLayeredWindowAttributes(IntPtr h, out uint key, out byte alpha, out uint flags);
        [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr h, IntPtr hdcDst, ref POINT dst, ref SIZE size, IntPtr hdcSrc, ref POINT src, uint key, ref BLENDFUNCTION blend, uint flags);
        [DllImport("user32.dll", EntryPoint = "UpdateLayeredWindow")] public static extern bool UpdateLayeredWindowAlpha(IntPtr h, IntPtr hdcDst, IntPtr dst, IntPtr size, IntPtr hdcSrc, IntPtr src, uint key, ref BLENDFUNCTION blend, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] public static extern int GetWindowLong(IntPtr h, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern IntPtr CreateWindowEx(int exStyle, string cls, string name, int style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
        [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] public static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT value, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int W, H; public SIZE(int w, int h) { W = w; H = h; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte Op, Flags, Alpha, Format; }

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004,
                          SWP_NOACTIVATE = 0x0010, SWP_NOOWNERZORDER = 0x0200;
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOPMOST = 0x08, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80,
                         WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000;
        public const uint LWA_ALPHA = 0x2, ULW_ALPHA = 0x2;

        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016, EVENT_SYSTEM_MINIMIZEEND = 0x0017;
        public const uint EVENT_OBJECT_DESTROY = 0x8001, EVENT_OBJECT_REORDER = 0x8004;
        public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        public const uint EVENT_OBJECT_CLOAKED = 0x8017, EVENT_OBJECT_UNCLOAKED = 0x8018;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        public static bool IsTopmost(IntPtr h) { return (GetWindowLong(h, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0; }

        // Visible bounds, without the invisible resize borders of Windows 10/11.
        public static bool TryGetBounds(IntPtr h, out Rectangle r)
        {
            RECT rc;
            if (DwmGetWindowAttribute(h, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out rc, Marshal.SizeOf(typeof(RECT))) != 0
                && !GetWindowRect(h, out rc))
            {
                r = Rectangle.Empty;
                return false;
            }
            r = Rectangle.FromLTRB(rc.Left, rc.Top, rc.Right, rc.Bottom);
            return r.Width > 0 && r.Height > 0;
        }

        public static bool TryGetClientOnScreen(IntPtr h, out Rectangle r)
        {
            RECT rc;
            var p = new POINT(0, 0);
            r = Rectangle.Empty;
            if (!GetClientRect(h, out rc) || !ClientToScreen(h, ref p)) return false;
            r = new Rectangle(p.X, p.Y, rc.Right, rc.Bottom);
            return r.Width > 0 && r.Height > 0;
        }

        public static bool IsCloaked(IntPtr h)
        {
            int cloaked;
            return DwmGetWindowAttribute(h, 14 /* DWMWA_CLOAKED */, out cloaked, 4) == 0 && cloaked != 0;
        }

        public static string ClassOf(IntPtr h) { var sb = new StringBuilder(128); GetClassName(h, sb, sb.Capacity); return sb.ToString(); }
        public static string TitleOf(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, sb.Capacity); return sb.ToString(); }
    }

    // ================================================================ Magnification API (the "shader")

    static class Mag
    {
        public static bool Ready;

        [DllImport("Magnification.dll")] static extern bool MagInitialize();
        [DllImport("Magnification.dll")] static extern bool MagUninitialize();
        [DllImport("Magnification.dll")] static extern bool MagSetWindowSource(IntPtr h, Native.RECT source);
        [DllImport("Magnification.dll")] static extern bool MagSetWindowTransform(IntPtr h, ref Transform t);
        [DllImport("Magnification.dll")] static extern bool MagSetColorEffect(IntPtr h, ref Effect e);
        [DllImport("Magnification.dll")] static extern bool MagSetWindowFilterList(IntPtr h, int mode, int count, IntPtr[] list);

        [StructLayout(LayoutKind.Sequential)] struct Transform { [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)] public float[] V; }
        [StructLayout(LayoutKind.Sequential)] struct Effect { [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)] public float[] V; }

        public static void Init()
        {
            // The Magnification API does not work in a 32-bit process on 64-bit Windows.
            if (IntPtr.Size == 4 && Environment.Is64BitOperatingSystem) { Ready = false; }
            else
            {
                try { Ready = MagInitialize(); }
                catch (Exception ex) { Ready = false; Log.Error("MagInitialize", ex); }
            }
            Log.Info("Magnification API: " + (Ready ? "ready" : "unavailable"));
        }

        public static void Shutdown() { if (Ready) try { MagUninitialize(); } catch { } }

        // Creates a 1:1 magnifier control inside a host window. Returns IntPtr.Zero on failure.
        public static IntPtr Create(IntPtr host)
        {
            if (!Ready) return IntPtr.Zero;
            IntPtr h = Native.CreateWindowEx(0, "Magnifier", "PenombreShader", Native.WS_CHILD | Native.WS_VISIBLE,
                0, 0, 1, 1, host, IntPtr.Zero, Native.GetModuleHandle(null), IntPtr.Zero);
            if (h == IntPtr.Zero)
            {
                Log.Error("Magnifier window creation failed, Win32 error " + Marshal.GetLastWin32Error(), null);
                return h;
            }
            var t = new Transform { V = new float[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 } };
            if (!MagSetWindowTransform(h, ref t))
            {
                Log.Error("MagSetWindowTransform failed", null);
                Native.DestroyWindow(h);
                return IntPtr.Zero;
            }
            return h;
        }

        public static void SetEffect(IntPtr h, float[] matrix)
        {
            var e = new Effect { V = matrix };
            MagSetColorEffect(h, ref e);
        }

        public static void SetSource(IntPtr h, Rectangle r)
        {
            var rc = new Native.RECT { Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom };
            MagSetWindowSource(h, rc);
            Native.InvalidateRect(h, IntPtr.Zero, false);
        }

        public static void Exclude(IntPtr h, IntPtr[] windows)
        {
            if (windows.Length > 0) MagSetWindowFilterList(h, 0 /* MW_FILTERMODE_EXCLUDE */, windows.Length, windows);
        }
    }

    // ================================================================ Texts (FR / EN)

    static class T
    {
        static bool fr;
        static readonly Dictionary<string, string[]> texts = new Dictionary<string, string[]>
        {
            // key            { English, French }
            { "already",    new[] { "Pénombre is already running (moon icon in the notification area).",
                                    "Pénombre tourne déjà (icône lune dans la zone de notification)." } },
            { "error",      new[] { "Something went wrong. Details are in the log (menu > Open the log).",
                                    "Une erreur s'est produite. Le détail est dans le journal (menu > Ouvrir le journal)." } },
            { "enabled",    new[] { "Filter on", "Filtre actif" } },
            { "auto",       new[] { "Follow the real time", "Suivre l'heure réelle" } },
            { "mood",       new[] { "Mood", "Ambiance" } },
            { "custom",     new[] { "Custom", "Personnalisée" } },
            { "intensity",  new[] { "Intensity", "Intensité" } },
            { "render",     new[] { "Rendering", "Rendu" } },
            { "shader",     new[] { "Shader (reworked colors)", "Shader (couleurs retravaillées)" } },
            { "veil",       new[] { "Veil (simple darkening)", "Voile (simple assombrissement)" } },
            { "noShader",   new[] { "Shader unavailable on this PC: veil used instead.", "Shader indisponible sur ce PC : voile utilisé à la place." } },
            { "vignette",   new[] { "Vignette", "Vignettage" } },
            { "v0",         new[] { "None", "Aucun" } },
            { "v1",         new[] { "Light", "Léger" } },
            { "v2",         new[] { "Medium", "Moyen" } },
            { "v3",         new[] { "Strong", "Fort" } },
            { "accounts",   new[] { "Accounts", "Comptes" } },
            { "noAccount",  new[] { "No Dofus window", "Aucune fenêtre Dofus" } },
            { "settings",   new[] { "Settings…", "Réglages…" } },
            { "settingsT",  new[] { "Settings", "Réglages" } },
            { "diag",       new[] { "Copy diagnostics", "Copier le diagnostic" } },
            { "diagDone",   new[] { "Diagnostics copied to the clipboard.", "Diagnostic copié dans le presse-papiers." } },
            { "openLog",    new[] { "Open the log", "Ouvrir le journal" } },
            { "noLog",      new[] { "The log is empty for now.", "Le journal est vide pour l'instant." } },
            { "quit",       new[] { "Quit", "Quitter" } },
            { "off",        new[] { "off", "désactivé" } },
            { "windows",    new[] { "window(s)", "fenêtre(s)" } },
            { "noHotkeys",  new[] { "Shortcuts unavailable (used by another app): ", "Raccourcis indisponibles (pris par une autre appli) : " } },
            { "help",       new[] { "Ctrl+Alt+N: on / off · Ctrl+Alt+P: mood · Ctrl+Alt+A: auto\nCtrl+Alt+PgUp / PgDn: intensity · Ctrl+Alt+V: vignette",
                                    "Ctrl+Alt+N : activer / couper · Ctrl+Alt+P : ambiance · Ctrl+Alt+A : auto\nCtrl+Alt+PgUp / PgDn : intensité · Ctrl+Alt+V : vignettage" } },
            { "p0",         new[] { "Blue night", "Nuit bleutée" } },
            { "p1",         new[] { "Moonlight", "Clair de lune" } },
            { "p2",         new[] { "Dusk", "Crépuscule" } },
            { "p3",         new[] { "Pitch black", "Nuit noire" } },
            { "p4",         new[] { "Old-school night", "Nuit d'antan" } },
            // Settings window
            { "sMood",      new[] { "Mood", "Ambiance" } },
            { "brightness", new[] { "Brightness", "Luminosité" } },
            { "saturation", new[] { "Saturation", "Saturation" } },
            { "temperature",new[] { "Temperature", "Température" } },
            { "cold",       new[] { "cold", "froid" } },
            { "warm",       new[] { "warm", "chaud" } },
            { "neutral",    new[] { "neutral", "neutre" } },
            { "lift",       new[] { "Lifted blacks", "Noirs relevés" } },
            { "customHint", new[] { "Moving a slider switches to the Custom mood.", "Toucher un curseur passe sur l'ambiance Personnalisée." } },
            { "sAuto",      new[] { "Automatic mode", "Mode automatique" } },
            { "dusk",       new[] { "Dusk starts", "Début du crépuscule" } },
            { "night",      new[] { "Full night", "Nuit complète" } },
            { "dawn",       new[] { "Dawn starts", "Début de l'aube" } },
            { "day",        new[] { "Full day", "Jour complet" } },
            { "now",        new[] { "Now", "Maintenant" } },
            { "stDay",      new[] { "day (no filter)", "jour (pas de filtre)" } },
            { "stDusk",     new[] { "dusk", "crépuscule" } },
            { "stNight",    new[] { "night", "nuit" } },
            { "stDawn",     new[] { "dawn", "aube" } },
            { "sTrans",     new[] { "Transitions", "Transitions" } },
            { "fade",       new[] { "Fade", "Fondu" } },
            { "instant",    new[] { "instant", "instantané" } },
            { "sUi",        new[] { "Game interface", "Interface du jeu" } },
            { "zonesKeep",  new[] { "Keep some zones bright (spells, chat…)", "Garder des zones claires (sorts, chat…)" } },
            { "zonesEdit",  new[] { "Draw the zones…", "Définir les zones…" } },
            { "zonesCount", new[] { "{0} zone(s)", "{0} zone(s)" } },
            { "zonesNoGame",new[] { "Open Dofus first: zones are drawn over its window.", "Ouvre Dofus d'abord : les zones se dessinent sur sa fenêtre." } },
            { "zoneHelp",   new[] { "Drag to draw a zone that stays bright · right-click: remove · Del: clear all · Enter: save · Esc: cancel",
                                    "Glisse pour dessiner une zone qui restera claire · clic droit : supprimer · Suppr : tout effacer · Entrée : valider · Échap : annuler" } },
            { "sAccounts",  new[] { "Accounts", "Comptes" } },
            { "accHint",    new[] { "Untick a character to leave its window unfiltered.", "Décoche un personnage pour ne pas filtrer sa fenêtre." } },
            { "sWindows",   new[] { "Windows", "Windows" } },
            { "startup",    new[] { "Start with Windows", "Lancer au démarrage de Windows" } },
            { "close",      new[] { "Close", "Fermer" } },
        };

        public static void Init(string lang)
        {
            if (lang == null) lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            fr = lang == "fr";
        }

        public static string S(string key) { return texts[key][fr ? 1 : 0]; }
    }

    // ================================================================ Moods

    class Preset
    {
        public readonly string Key;
        public readonly float Brightness, Saturation;
        public readonly float[] Tint, Lift;   // R, G, B
        public readonly Color Veil;           // veil color (fallback renderer)

        public Preset(string key, float brightness, float saturation, float[] tint, float[] lift, Color veil)
        {
            Key = key; Brightness = brightness; Saturation = saturation; Tint = tint; Lift = lift; Veil = veil;
        }

        public string Name { get { return T.S(Key); } }

        public static readonly Preset[] All =
        {
            new Preset("p0", 0.55f, 0.55f, new[] { 0.80f, 0.92f, 1.18f }, new[] { 0.00f, 0.01f, 0.04f }, Color.FromArgb(12, 22, 64)),
            new Preset("p1", 0.70f, 0.30f, new[] { 0.88f, 0.97f, 1.12f }, new[] { 0.02f, 0.03f, 0.06f }, Color.FromArgb(40, 58, 96)),
            new Preset("p2", 0.72f, 0.80f, new[] { 1.08f, 0.86f, 0.96f }, new[] { 0.03f, 0.00f, 0.03f }, Color.FromArgb(70, 34, 80)),
            new Preset("p3", 0.38f, 0.50f, new[] { 0.85f, 0.90f, 1.10f }, new[] { 0.00f, 0.00f, 0.01f }, Color.FromArgb(0, 0, 8)),
            new Preset("p4", 0.62f, 0.90f, new[] { 0.70f, 0.78f, 1.00f }, new[] { 0.00f, 0.00f, 0.02f }, Color.FromArgb(20, 24, 60)),
        };

        public static int CustomIndex { get { return All.Length; } }
        public static int Count { get { return All.Length + 1; } }

        public static Preset Get(int index)
        {
            if (index >= 0 && index < All.Length) return All[index];
            return FromSliders(Settings.CustomBrightness, Settings.CustomSaturation, Settings.CustomTemperature, Settings.CustomLift);
        }

        public static string NameOf(int index) { return index < All.Length ? All[index].Name : T.S("custom"); }

        // brightness 10..100, saturation 0..100, temperature -100 (cold) .. 100 (warm), lift 0..100
        public static Preset FromSliders(int brightness, int saturation, int temperature, int lift)
        {
            float t = temperature / 100f;
            var tint = new[] { 1 + 0.25f * t, 1 + 0.08f * t, 1 - 0.25f * t };
            float[] hue = t < 0 ? new[] { 0.3f, 0.5f, 1.0f } : new[] { 1.0f, 0.4f, 0.7f };
            float amount = lift / 100f * 0.06f, c = Math.Abs(t);
            var lifts = new float[3];
            for (int i = 0; i < 3; i++) lifts[i] = amount * (0.8f + (hue[i] - 0.8f) * c);
            var veil = Color.FromArgb(Byte(30 * tint[0] - 8), Byte(30 * tint[1] - 6), Byte(56 * tint[2]));
            return new Preset("custom", brightness / 100f, saturation / 100f, tint, lifts, veil);
        }

        // Approximate slider positions of this mood (used to start a custom mood from it).
        public int[] ToSliders()
        {
            float max = Math.Max(Lift[0], Math.Max(Lift[1], Lift[2]));
            return new[]
            {
                Clamp((int)Math.Round(Brightness * 100), 10, 100),
                Clamp((int)Math.Round(Saturation * 100), 0, 100),
                Clamp((int)Math.Round((Tint[0] - Tint[2]) / 0.5f * 100), -100, 100),
                Clamp((int)Math.Round(max / 0.06f * 100), 0, 100),
            };
        }

        static int Clamp(int v, int min, int max) { return Math.Max(min, Math.Min(max, v)); }
        static int Byte(float v) { return Clamp((int)Math.Round(v), 0, 255); }
    }

    // ================================================================ Real-time day / night cycle

    static class DayCycle
    {
        // 0 = full day (no filter), 1 = full night.
        public static float Factor(DateTime now)
        {
            float m = now.Hour * 60 + now.Minute + now.Second / 60f;
            float x;
            if (Within(m, Settings.Dusk, Settings.Night, out x)) return Smooth(x);
            if (Within(m, Settings.Night, Settings.Dawn, out x)) return 1;
            if (Within(m, Settings.Dawn, Settings.Day, out x)) return 1 - Smooth(x);
            return 0;
        }

        public static string Describe(DateTime now)
        {
            float m = now.Hour * 60 + now.Minute + now.Second / 60f;
            float x;
            int pct = (int)Math.Round(Factor(now) * 100);
            if (Within(m, Settings.Dusk, Settings.Night, out x)) return T.S("stDusk") + " " + pct + " %";
            if (Within(m, Settings.Night, Settings.Dawn, out x)) return T.S("stNight");
            if (Within(m, Settings.Dawn, Settings.Day, out x)) return T.S("stDawn") + " " + pct + " %";
            return T.S("stDay");
        }

        // Is minute m in the circular range [a, b)? x = progress inside the range (0..1).
        static bool Within(float m, int a, int b, out float x)
        {
            float span = Mod(b - a);
            float d = Mod(m - a);
            x = span > 0 ? d / span : 0;
            return span > 0 && d < span;
        }

        static float Mod(float v) { return ((v % 1440) + 1440) % 1440; }
        static float Smooth(float x) { return x * x * (3 - 2 * x); }
    }

    // ================================================================ Look (what the filters show)

    class Look
    {
        public float[] Matrix;
        public Color VeilColor;
        public byte VeilAlpha;
        public int VignetteStrength;  // alpha of the vignette edges (0 = none)
        public byte VignetteOpacity;  // global opacity of the vignette (follows fade / auto)

        static readonly float[] Luma = { 0.2126f, 0.7152f, 0.0722f };
        static readonly int[] VignetteAlpha = { 0, 110, 170, 230 };

        // amount: 0..1, product of the fade and of the automatic mode.
        public static Look Current(float amount)
        {
            Preset p = Preset.Get(Settings.PresetIndex);
            float k = Settings.Intensity / 100f * amount;
            float b = Lerp(1, p.Brightness, k), s = Lerp(1, p.Saturation, k);

            // GDI+-style 5x5 color matrix, row vector: [R G B A 1] x M.
            var m = new float[25];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    m[i * 5 + j] = ((1 - s) * Luma[i] + (i == j ? s : 0)) * b * Lerp(1, p.Tint[j], k);
            m[3 * 5 + 3] = 1;
            for (int j = 0; j < 3; j++) m[4 * 5 + j] = p.Lift[j] * k;
            m[4 * 5 + 4] = 1;

            return new Look
            {
                Matrix = m,
                VeilColor = p.Veil,
                VeilAlpha = (byte)Math.Max(0, Math.Min(230, Math.Round(k * (1 - p.Brightness) * 255 * 0.9))),
                VignetteStrength = VignetteAlpha[Settings.Vignette],
                VignetteOpacity = (byte)Math.Max(0, Math.Min(255, Math.Round(amount * 255))),
            };
        }

        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
    }

    // ================================================================ Settings

    static class Settings
    {
        public const int MinIntensity = 5, MaxIntensity = 100;

        public static bool Enabled = true;
        public static bool Shader = true;
        public static int PresetIndex = 0;
        public static int Intensity = 80;
        public static int Vignette = 1;
        public static string ProcessName = "Dofus";

        public static bool Auto = false;
        public static int Dusk = 19 * 60 + 30, Night = 21 * 60 + 30, Dawn = 6 * 60 + 30, Day = 8 * 60 + 30;
        public static int FadeMs = 600;

        public static int CustomBrightness = 55, CustomSaturation = 55, CustomTemperature = -80, CustomLift = 30;

        public static bool ZonesEnabled = false;
        public static List<RectangleF> Zones = new List<RectangleF>();   // relative to the game area (0..1)
        public static HashSet<string> Excluded = new HashSet<string>();  // characters left unfiltered

        static Timer saveTimer;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Penombre"); }
        }

        static string FilePath { get { return Path.Combine(Folder, "settings.ini"); } }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                    int n;
                    switch (k)
                    {
                        case "enabled": Enabled = v != "0"; break;
                        case "render": Shader = v != "veil"; break;
                        case "preset": if (int.TryParse(v, out n) && n >= 0 && n < Preset.Count) PresetIndex = n; break;
                        case "intensity": if (int.TryParse(v, out n)) Intensity = ClampIntensity(n); break;
                        case "vignette": if (int.TryParse(v, out n) && n >= 0 && n <= 3) Vignette = n; break;
                        case "process": if (v.Length > 0) ProcessName = v; break;
                        case "auto": Auto = v == "1"; break;
                        case "dusk": ParseTime(v, ref Dusk); break;
                        case "night": ParseTime(v, ref Night); break;
                        case "dawn": ParseTime(v, ref Dawn); break;
                        case "day": ParseTime(v, ref Day); break;
                        case "fade": if (int.TryParse(v, out n)) FadeMs = Math.Max(0, Math.Min(3000, n)); break;
                        case "custom": ParseCustom(v); break;
                        case "zonesEnabled": ZonesEnabled = v == "1"; break;
                        case "zones": Zones = ParseZones(v); break;
                        case "excluded":
                            Excluded = new HashSet<string>();
                            foreach (string name in v.Split('|')) if (name.Length > 0) Excluded.Add(name);
                            break;
                    }
                }
            }
            catch (Exception ex) { Log.Error("Settings load", ex); }
        }

        // Saves a moment later, so dragging a slider does not write the file 50 times.
        public static void SaveSoon()
        {
            if (saveTimer == null)
            {
                saveTimer = new Timer { Interval = 500 };
                saveTimer.Tick += (s, e) => { saveTimer.Stop(); Save(); };
            }
            saveTimer.Stop();
            saveTimer.Start();
        }

        public static void Save()
        {
            if (saveTimer != null) saveTimer.Stop();
            try
            {
                Directory.CreateDirectory(Folder);
                var zones = new List<string>();
                foreach (RectangleF z in Zones)
                    zones.Add(string.Format(Inv, "{0:0.####},{1:0.####},{2:0.####},{3:0.####}", z.X, z.Y, z.Width, z.Height));
                File.WriteAllLines(FilePath, new[]
                {
                    "enabled=" + (Enabled ? "1" : "0"),
                    "render=" + (Shader ? "shader" : "veil"),
                    "preset=" + PresetIndex,
                    "intensity=" + Intensity,
                    "vignette=" + Vignette,
                    "process=" + ProcessName,
                    "auto=" + (Auto ? "1" : "0"),
                    "dusk=" + FormatTime(Dusk),
                    "night=" + FormatTime(Night),
                    "dawn=" + FormatTime(Dawn),
                    "day=" + FormatTime(Day),
                    "fade=" + FadeMs,
                    "custom=" + CustomBrightness + "," + CustomSaturation + "," + CustomTemperature + "," + CustomLift,
                    "zonesEnabled=" + (ZonesEnabled ? "1" : "0"),
                    "zones=" + string.Join(";", zones.ToArray()),
                    "excluded=" + string.Join("|", new List<string>(Excluded).ToArray()),
                }, Encoding.UTF8);
            }
            catch (Exception ex) { Log.Error("Settings save", ex); }
        }

        public static int ClampIntensity(int v) { return Math.Max(MinIntensity, Math.Min(MaxIntensity, v)); }

        public static string FormatTime(int minutes) { return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00"); }

        static void ParseTime(string v, ref int target)
        {
            string[] p = v.Split(':');
            int h, m;
            if (p.Length == 2 && int.TryParse(p[0], out h) && int.TryParse(p[1], out m) && h >= 0 && h < 24 && m >= 0 && m < 60)
                target = h * 60 + m;
        }

        static void ParseCustom(string v)
        {
            string[] p = v.Split(',');
            int b, s, t, l;
            if (p.Length == 4 && int.TryParse(p[0], out b) && int.TryParse(p[1], out s) && int.TryParse(p[2], out t) && int.TryParse(p[3], out l))
            {
                CustomBrightness = Math.Max(10, Math.Min(100, b));
                CustomSaturation = Math.Max(0, Math.Min(100, s));
                CustomTemperature = Math.Max(-100, Math.Min(100, t));
                CustomLift = Math.Max(0, Math.Min(100, l));
            }
        }

        static List<RectangleF> ParseZones(string v)
        {
            var list = new List<RectangleF>();
            foreach (string z in v.Split(';'))
            {
                string[] p = z.Split(',');
                float x, y, w, h;
                if (p.Length == 4
                    && float.TryParse(p[0], NumberStyles.Float, Inv, out x) && float.TryParse(p[1], NumberStyles.Float, Inv, out y)
                    && float.TryParse(p[2], NumberStyles.Float, Inv, out w) && float.TryParse(p[3], NumberStyles.Float, Inv, out h)
                    && w > 0 && h > 0)
                    list.Add(new RectangleF(x, y, w, h));
            }
            return list;
        }
    }

    // ================================================================ Start with Windows

    static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "Penombre";

        static string Command { get { return "\"" + Application.ExecutablePath + "\""; } }

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(Name) != null;
            }
            catch { return false; }
        }

        public static void Set(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue(Name, Command);
                    else k.DeleteValue(Name, false);
                }
            }
            catch (Exception ex) { Log.Error("Start with Windows", ex); }
        }

        // If the exe was moved, keep the startup entry pointing at the right place.
        public static void RefreshPath()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (k == null) return;
                    object v = k.GetValue(Name);
                    if (v == null || (string)v == Command) return;
                }
                Set(true);
            }
            catch { }
        }
    }

    // ================================================================ Filter windows

    // Base for our click-through windows: borderless, never activated, topmost,
    // absent from the taskbar and Alt+Tab.
    class ClickThroughWindow : Form
    {
        public bool IsShown;

        public ClickThroughWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Pénombre";
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW
                            | Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084, HTTRANSPARENT = -1;
            if (m.Msg == WM_NCHITTEST) { m.Result = new IntPtr(HTTRANSPARENT); return; }
            base.WndProc(ref m);
        }

        public void ShowNa() { if (!IsShown) { Native.ShowWindow(Handle, 8 /* SW_SHOWNA */); IsShown = true; } }
        public void HideNow() { if (IsShown) { Native.ShowWindow(Handle, 0 /* SW_HIDE */); IsShown = false; } }
    }

    // Per-pixel alpha vignette (dark edges, clear center), owned by the filter so it stays above it.
    class VignetteWindow : ClickThroughWindow
    {
        Size drawnSize = Size.Empty;
        int drawnStrength = -1;
        byte opacity = 255;
        Point placedAt = new Point(int.MinValue, int.MinValue);

        public VignetteWindow() { CreateHandle(); }

        public void Place(Rectangle r, int strength, byte globalOpacity)
        {
            if (strength <= 0 || globalOpacity == 0) { HideNow(); return; }
            if (r.Size != drawnSize || strength != drawnStrength)
            {
                opacity = globalOpacity;
                Draw(r, strength);
            }
            else
            {
                if (r.Location != placedAt)
                    Native.SetWindowPos(Handle, IntPtr.Zero, r.X, r.Y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                if (globalOpacity != opacity)
                {
                    // Only the global opacity changes: no need to redraw the bitmap.
                    opacity = globalOpacity;
                    var blend = Blend(opacity);
                    Native.UpdateLayeredWindowAlpha(Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref blend, Native.ULW_ALPHA);
                }
            }
            placedAt = r.Location;
            ShowNa();
        }

        static Native.BLENDFUNCTION Blend(byte alpha)
        {
            return new Native.BLENDFUNCTION { Op = 0, Flags = 0, Alpha = alpha, Format = 1 /* AC_SRC_ALPHA */ };
        }

        void Draw(Rectangle r, int strength)
        {
            using (var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                using (var path = new GraphicsPath())
                {
                    g.Clear(Color.Transparent);
                    // Ellipse through the corners, so they get the full darkness.
                    float rx = r.Width * 0.72f, ry = r.Height * 0.72f;
                    path.AddEllipse(r.Width / 2f - rx, r.Height / 2f - ry, rx * 2, ry * 2);
                    using (var brush = new PathGradientBrush(path))
                    {
                        brush.CenterColor = Color.FromArgb(0, 0, 0, 0);
                        brush.SurroundColors = new[] { Color.FromArgb(strength, 0, 2, 12) };
                        brush.Blend = new Blend
                        {
                            Positions = new[] { 0f, 0.25f, 0.6f, 1f },
                            Factors = new[] { 0f, 0.45f, 1f, 1f },
                        };
                        g.FillRectangle(brush, 0, 0, r.Width, r.Height);
                    }
                }

                IntPtr screen = Native.GetDC(IntPtr.Zero);
                IntPtr mem = Native.CreateCompatibleDC(screen);
                IntPtr hbmp = bmp.GetHbitmap(Color.FromArgb(0));
                IntPtr old = Native.SelectObject(mem, hbmp);
                var dst = new Native.POINT(r.X, r.Y);
                var size = new Native.SIZE(r.Width, r.Height);
                var src = new Native.POINT(0, 0);
                var blend = Blend(opacity);
                Native.UpdateLayeredWindow(Handle, screen, ref dst, ref size, mem, ref src, 0, ref blend, Native.ULW_ALPHA);
                Native.SelectObject(mem, old);
                Native.DeleteObject(hbmp);
                Native.DeleteDC(mem);
                Native.ReleaseDC(IntPtr.Zero, screen);
            }
            drawnSize = r.Size;
            drawnStrength = strength;
        }
    }

    // The filter over one game window: a magnifier "shader" host, or a colored veil.
    class Overlay : ClickThroughWindow
    {
        public readonly IntPtr Target;
        public readonly IntPtr MagWindow;
        public readonly VignetteWindow Vignette;
        public Rectangle Placed = Rectangle.Empty;
        public string RegionKey;
        public string RegionInfo = "-";
        public string Account = "";

        public bool UsesShader { get { return MagWindow != IntPtr.Zero; } }

        public Overlay(IntPtr target, bool wantShader)
        {
            Target = target;
            BackColor = Color.Black;
            CreateHandle();
            if (wantShader)
            {
                MagWindow = Mag.Create(Handle);
                if (MagWindow != IntPtr.Zero)
                    Native.SetLayeredWindowAttributes(Handle, 0, 255, Native.LWA_ALPHA); // the shader output must be opaque
            }
            Vignette = new VignetteWindow();
            Vignette.Owner = this; // owned window: always drawn above the filter
        }

        public void ApplyLook(Look look)
        {
            if (UsesShader) Mag.SetEffect(MagWindow, look.Matrix);
            else
            {
                if (BackColor != look.VeilColor) { BackColor = look.VeilColor; Invalidate(); }
                Native.SetLayeredWindowAttributes(Handle, 0, look.VeilAlpha, Native.LWA_ALPHA);
            }
        }

        // r: game area on screen. region: visible part, in local coordinates (null = all of it).
        public void Place(Rectangle r, Region region, string key, Graphics g, Look look)
        {
            ShowNa();
            if (r != Placed)
            {
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, r.X, r.Y, r.Width, r.Height, Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);
                if (UsesShader) Native.MoveWindow(MagWindow, 0, 0, r.Width, r.Height, false);
                Placed = r;
                RegionKey = null;
            }
            else if (!Native.IsTopmost(Handle))
            {
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_NOOWNERZORDER);
            }

            Vignette.Place(r, look.VignetteStrength, look.VignetteOpacity);

            if (key != RegionKey)
            {
                // The system owns the HRGN given to SetWindowRgn: one per window.
                Native.SetWindowRgn(Handle, region == null ? IntPtr.Zero : region.GetHrgn(g), true);
                Native.SetWindowRgn(Vignette.Handle, region == null ? IntPtr.Zero : region.GetHrgn(g), true);
                RegionKey = key;
            }
        }

        public void HideAll()
        {
            HideNow();
            Vignette.HideNow();
        }

        // Shader: redraw the game area through the color matrix (one frame).
        public void RenderFrame()
        {
            if (UsesShader && IsShown) Mag.SetSource(MagWindow, Placed);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Vignette.Dispose();
            base.Dispose(disposing);
        }
    }

    // ================================================================ Global shortcuts

    class HotkeyWindow : NativeWindow, IDisposable
    {
        readonly Action<int> callback;
        readonly List<int> ids = new List<int>();

        public HotkeyWindow(Action<int> callback)
        {
            this.callback = callback;
            var cp = new CreateParams();
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE
            CreateHandle(cp);
        }

        public bool Register(int id, uint mods, uint vk)
        {
            bool ok = Native.RegisterHotKey(Handle, id, mods | 0x4000 /* MOD_NOREPEAT */, vk);
            if (ok) ids.Add(id);
            return ok;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 /* WM_HOTKEY */) callback(m.WParam.ToInt32());
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            foreach (int id in ids) Native.UnregisterHotKey(Handle, id);
            DestroyHandle();
        }
    }

    // ================================================================ Tracking the game windows

    class OverlayManager : IDisposable
    {
        const int FastInterval = 15;    // ~60 Hz while a shader is on screen or a fade is running
        const int IdleInterval = 100;   // otherwise
        const int ScanEveryMs = 1000, SafetyLayoutMs = 500, AutoEveryMs = 5000;

        readonly Dictionary<IntPtr, Overlay> overlays = new Dictionary<IntPtr, Overlay>();
        readonly HashSet<IntPtr> ownWindows = new HashSet<IntPtr>();
        readonly List<IntPtr> hooks = new List<IntPtr>();
        readonly Native.WinEventProc eventProc; // kept in a field so the GC never collects it
        readonly Timer timer;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Bitmap scratch = new Bitmap(1, 1);
        readonly Graphics g;

        Look look;
        float shown;          // what is on screen now (0..1), moves towards the target during a fade
        float autoFactor = 1; // automatic mode (0 = day, 1 = night)
        bool layoutDirty = true;
        long lastTick, lastScan = -ScanEveryMs, lastLayout, lastAuto = -AutoEveryMs, lastErrorLog = -10000;
        int frame;
        IntPtr lastGameForeground = IntPtr.Zero;

        public event EventHandler CountChanged;
        public int Count { get { return overlays.Count; } }
        public bool ShaderFailed { get; private set; }
        public float Shown { get { return shown; } }
        public float AutoFactor { get { return autoFactor; } }

        public OverlayManager()
        {
            g = Graphics.FromImage(scratch);
            look = Look.Current(0);

            // Windows events: something moved, appeared, disappeared or changed order.
            eventProc = OnWinEvent;
            Hook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND);
            Hook(Native.EVENT_SYSTEM_MINIMIZESTART, Native.EVENT_SYSTEM_MINIMIZEEND);
            Hook(Native.EVENT_OBJECT_DESTROY, Native.EVENT_OBJECT_REORDER);
            Hook(Native.EVENT_OBJECT_LOCATIONCHANGE, Native.EVENT_OBJECT_LOCATIONCHANGE);
            Hook(Native.EVENT_OBJECT_CLOAKED, Native.EVENT_OBJECT_UNCLOAKED);

            timer = new Timer { Interval = FastInterval };
            timer.Tick += (s, e) => OnTick();
            timer.Start();
        }

        void Hook(uint min, uint max)
        {
            IntPtr h = Native.SetWinEventHook(min, max, IntPtr.Zero, eventProc, 0, 0, Native.WINEVENT_OUTOFCONTEXT);
            if (h == IntPtr.Zero) Log.Error("SetWinEventHook failed for 0x" + min.ToString("X"), null);
            else hooks.Add(h);
        }

        void OnWinEvent(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
        {
            if (idObject != 0 || idChild != 0) return;      // only whole windows (not carets, cursors...)
            if (ownWindows.Contains(hwnd)) return;          // our own filters moving: no loop
            if (ev == Native.EVENT_SYSTEM_FOREGROUND && overlays.ContainsKey(hwnd)) lastGameForeground = hwnd;
            if (overlays.Count == 0) return;
            layoutDirty = true;
            if (timer.Interval != FastInterval) timer.Interval = FastInterval;
        }

        void OnTick()
        {
            long now = clock.ElapsedMilliseconds;
            float dt = Math.Min(0.2f, (now - lastTick) / 1000f);
            lastTick = now;
            try
            {
                if (now - lastScan >= ScanEveryMs) { lastScan = now; Scan(); }

                if (now - lastAuto >= AutoEveryMs) { lastAuto = now; UpdateAutoFactor(); }

                // Fade towards the target (on / off, automatic mode).
                float target = Settings.Enabled ? autoFactor : 0;
                bool fading = shown != target;
                if (fading)
                {
                    bool wasOff = shown <= 0;
                    float step = Settings.FadeMs <= 0 ? 1 : dt * 1000f / Settings.FadeMs;
                    shown = target > shown ? Math.Min(target, shown + step) : Math.Max(target, shown - step);
                    ApplyLookNow();
                    if (wasOff != (shown <= 0)) layoutDirty = true; // filters appear or disappear
                }

                if (layoutDirty || now - lastLayout >= SafetyLayoutMs)
                {
                    layoutDirty = false;
                    lastLayout = now;
                    Layout();
                }

                // Shader frames: full rate for the game in the foreground, a third of it in the background.
                frame++;
                IntPtr fg = Native.GetForegroundWindow();
                bool rendering = false;
                foreach (var o in overlays.Values)
                {
                    if (!o.IsShown || !o.UsesShader) continue;
                    rendering = true;
                    if (o.Target == fg || frame % 3 == 0) o.RenderFrame();
                }

                int interval = rendering || fading ? FastInterval : IdleInterval;
                if (timer.Interval != interval) timer.Interval = interval;
            }
            catch (Exception ex)
            {
                if (now - lastErrorLog > 10000) { lastErrorLog = now; Log.Error("Tick", ex); }
            }
        }

        public void UpdateAutoFactor()
        {
            autoFactor = Settings.Auto ? DayCycle.Factor(DateTime.Now) : 1;
        }

        // Finds the Dofus 3 client windows (Unity) and gives each one its own filter.
        public void Scan()
        {
            var found = new List<IntPtr>();
            Native.EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (IsGameWindow(h)) found.Add(h);
                return true;
            }, IntPtr.Zero);

            bool changed = false;
            foreach (IntPtr h in found)
            {
                if (overlays.ContainsKey(h)) continue;
                var o = new Overlay(h, Settings.Shader && Mag.Ready);
                if (Settings.Shader && !o.UsesShader) ShaderFailed = true;
                o.ApplyLook(look);
                overlays[h] = o;
                changed = true;
                Log.Info("Game window found: 0x" + h.ToInt64().ToString("X") + " \"" + Native.TitleOf(h) + "\" - "
                         + (o.UsesShader ? "shader" : "veil"));
            }

            var dead = new List<IntPtr>();
            foreach (var kv in overlays) if (!Native.IsWindow(kv.Key)) dead.Add(kv.Key);
            foreach (IntPtr h in dead)
            {
                overlays[h].Dispose();
                overlays.Remove(h);
                changed = true;
                Log.Info("Game window closed: 0x" + h.ToInt64().ToString("X"));
            }

            if (changed)
            {
                RefreshOwnWindows();
                layoutDirty = true;
                if (CountChanged != null) CountChanged(this, EventArgs.Empty);
            }
        }

        bool IsGameWindow(IntPtr h)
        {
            if (!Native.IsWindowVisible(h) || Native.ClassOf(h) != "UnityWndClass") return false;
            uint pid;
            Native.GetWindowThreadProcessId(h, out pid);
            try
            {
                using (var p = Process.GetProcessById((int)pid))
                    return p.ProcessName.IndexOf(Settings.ProcessName, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        // Character name from a Dofus 3 title ("Name - Class - version - Release").
        public static string AccountOf(IntPtr h)
        {
            string title = Native.TitleOf(h).Trim();
            int i = title.IndexOf(" - ");
            string name = i > 0 ? title.Substring(0, i).Trim() : title;
            return name.Length > 0 ? name : "Dofus";
        }

        void RefreshOwnWindows()
        {
            ownWindows.Clear();
            foreach (var o in overlays.Values) { ownWindows.Add(o.Handle); ownWindows.Add(o.Vignette.Handle); }

            // A shader must never capture a filter or a vignette (its own included),
            // otherwise the darkening would be applied again on every frame.
            IntPtr[] all = new List<IntPtr>(ownWindows).ToArray();
            foreach (var o in overlays.Values)
                if (o.UsesShader) Mag.Exclude(o.MagWindow, all);
        }

        // Clips every filter to the visible part of its game window. Windows are walked from the
        // top of the Z-order down: whatever has been met so far (other apps, other game windows)
        // hides the game windows below it. Filters therefore never overlap.
        void Layout()
        {
            if (overlays.Count == 0) return;
            if (shown <= 0)
            {
                foreach (var o in overlays.Values) { o.HideAll(); o.RegionInfo = "off"; }
                return;
            }

            var order = new List<IntPtr>();
            Native.EnumWindows(delegate (IntPtr h, IntPtr l) { order.Add(h); return true; }, IntPtr.Zero);

            var seen = new HashSet<IntPtr>();
            using (var covered = new Region())
            {
                covered.MakeEmpty();
                foreach (IntPtr h in order)
                {
                    if (ownWindows.Contains(h)) continue;
                    Overlay o;
                    Rectangle r;
                    if (overlays.TryGetValue(h, out o))
                    {
                        seen.Add(h);
                        LayoutOne(o, covered);
                        if (Native.IsWindowVisible(h) && !Native.IsIconic(h) && Native.TryGetBounds(h, out r)) covered.Union(r);
                    }
                    else if (IsOccluder(h, out r))
                    {
                        covered.Union(r);
                    }
                }
            }
            foreach (var kv in overlays) if (!seen.Contains(kv.Key)) kv.Value.HideAll();
        }

        void LayoutOne(Overlay o, Region covered)
        {
            IntPtr t = o.Target;
            o.Account = AccountOf(t);
            Rectangle area;
            if (Settings.Excluded.Contains(o.Account)) { o.HideAll(); o.RegionInfo = "account off"; return; }
            if (!Native.IsWindowVisible(t) || Native.IsIconic(t) || !Native.TryGetClientOnScreen(t, out area))
            {
                o.HideAll();
                o.RegionInfo = "minimized";
                return;
            }

            using (var visible = new Region(area))
            {
                visible.Exclude(covered);
                if (Settings.ZonesEnabled)
                    foreach (RectangleF z in Settings.Zones)
                        visible.Exclude(new Rectangle(
                            area.X + (int)Math.Round(z.X * area.Width), area.Y + (int)Math.Round(z.Y * area.Height),
                            (int)Math.Round(z.Width * area.Width), (int)Math.Round(z.Height * area.Height)));

                if (visible.IsEmpty(g)) { o.HideAll(); o.RegionInfo = "covered"; return; }

                visible.Translate(-area.X, -area.Y);
                RectangleF[] scans;
                using (var identity = new Matrix()) scans = visible.GetRegionScans(identity);

                bool full = scans.Length == 1 && scans[0].X <= 0 && scans[0].Y <= 0
                            && scans[0].Width >= area.Width && scans[0].Height >= area.Height;
                string key;
                if (full) key = "full";
                else
                {
                    var sb = new StringBuilder();
                    foreach (var s in scans) sb.Append((int)s.X).Append(',').Append((int)s.Y).Append(',').Append((int)s.Width).Append(',').Append((int)s.Height).Append(';');
                    key = sb.ToString();
                }
                o.RegionInfo = full ? "full" : scans.Length + " rect(s)";
                o.Place(area, full ? null : visible, key, g, look);
            }
        }

        // A window that hides what is below it. Click-through and fully transparent windows
        // (GPU / Discord / Steam overlays...) and cloaked windows do not count.
        static bool IsOccluder(IntPtr h, out Rectangle r)
        {
            r = Rectangle.Empty;
            if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return false;
            int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            if ((ex & Native.WS_EX_TRANSPARENT) != 0) return false;
            if ((ex & Native.WS_EX_LAYERED) != 0)
            {
                uint key, flags; byte alpha;
                if (Native.GetLayeredWindowAttributes(h, out key, out alpha, out flags) && (flags & Native.LWA_ALPHA) != 0 && alpha == 0)
                    return false;
            }
            if (Native.IsCloaked(h)) return false;
            return Native.TryGetBounds(h, out r);
        }

        void ApplyLookNow()
        {
            look = Look.Current(shown);
            foreach (var o in overlays.Values) o.ApplyLook(look);
        }

        // Settings changed: new look, new layout.
        public void ApplyLook()
        {
            UpdateAutoFactor();
            ApplyLookNow();
            layoutDirty = true;
            timer.Interval = FastInterval;
        }

        // Renderer changed: rebuild every filter.
        public void Rebuild()
        {
            foreach (var o in overlays.Values) o.Dispose();
            overlays.Clear();
            ShaderFailed = false;
            lastScan = clock.ElapsedMilliseconds;
            Scan();
            ApplyLook();
            if (CountChanged != null) CountChanged(this, EventArgs.Empty);
        }

        public List<string> Accounts()
        {
            var list = new List<string>();
            foreach (var o in overlays.Values)
            {
                string a = AccountOf(o.Target);
                if (!list.Contains(a)) list.Add(a);
            }
            list.Sort(StringComparer.CurrentCultureIgnoreCase);
            return list;
        }

        // The game window to draw the zones over: the last Dofus in the foreground, or any visible one.
        public bool TryGetEditArea(out Rectangle area)
        {
            area = Rectangle.Empty;
            Overlay pick = null;
            foreach (var o in overlays.Values)
            {
                if (!Native.IsWindowVisible(o.Target) || Native.IsIconic(o.Target)) continue;
                if (o.Target == lastGameForeground) { pick = o; break; }
                if (pick == null) pick = o;
            }
            return pick != null && Native.TryGetClientOnScreen(pick.Target, out area);
        }

        public string Diagnostics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Pénombre " + Program.Version + " - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Windows " + Environment.OSVersion.Version + (Environment.Is64BitProcess ? " (x64 process)" : " (x86 process)"));
            sb.AppendLine("Magnification API: " + (Mag.Ready ? "ready" : "unavailable"));
            sb.AppendLine("Settings: enabled=" + Settings.Enabled + " render=" + (Settings.Shader ? "shader" : "veil")
                + " preset=" + Settings.PresetIndex + " intensity=" + Settings.Intensity + " vignette=" + Settings.Vignette
                + " process=" + Settings.ProcessName);
            sb.AppendLine("Auto=" + Settings.Auto + " (" + Settings.FormatTime(Settings.Dusk) + " / " + Settings.FormatTime(Settings.Night)
                + " / " + Settings.FormatTime(Settings.Dawn) + " / " + Settings.FormatTime(Settings.Day) + ") factor="
                + autoFactor.ToString("0.00", CultureInfo.InvariantCulture) + " shown=" + shown.ToString("0.00", CultureInfo.InvariantCulture)
                + " fade=" + Settings.FadeMs + "ms");
            sb.AppendLine("Zones: " + (Settings.ZonesEnabled ? "on" : "off") + " (" + Settings.Zones.Count + ")"
                + " - excluded accounts: " + string.Join(", ", new List<string>(Settings.Excluded).ToArray()));
            sb.AppendLine("Screens: " + string.Join(" | ", Array.ConvertAll(Screen.AllScreens, s => s.Bounds.ToString())));
            sb.AppendLine("Timer: " + timer.Interval + " ms");
            IntPtr fg = Native.GetForegroundWindow();
            sb.AppendLine();
            sb.AppendLine("Game windows: " + overlays.Count);
            foreach (var o in overlays.Values)
            {
                IntPtr target = o.Target;
                Rectangle area;
                Native.TryGetClientOnScreen(target, out area);
                uint pid; Native.GetWindowThreadProcessId(target, out pid);
                sb.AppendLine(string.Format("  0x{0:X} pid {1} \"{2}\" area {3}{4}", target.ToInt64(), pid, Native.TitleOf(target), area,
                    target == fg ? " [foreground]" : ""));
                sb.AppendLine(string.Format("    filter 0x{0:X} {1} shown={2} topmost={3} region={4} vignette shown={5}",
                    o.Handle.ToInt64(), o.UsesShader ? "shader" : "veil", o.IsShown, Native.IsTopmost(o.Handle), o.RegionInfo, o.Vignette.IsShown));
            }
            sb.AppendLine();
            sb.AppendLine("Z-order (top 30 visible windows):");
            int n = 0;
            Native.EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                if (!Native.IsWindowVisible(h)) return true;
                Rectangle r; Native.TryGetBounds(h, out r);
                int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
                Rectangle dummy;
                string role = ownWindows.Contains(h) ? "PENOMBRE" : overlays.ContainsKey(h) ? "GAME" : IsOccluder(h, out dummy) ? "occluder" : "ignored";
                sb.AppendLine(string.Format("  {0,-8} 0x{1:X} {2} ex=0x{3:X} {4} \"{5}\"{6}", role, h.ToInt64(), Native.ClassOf(h), ex, r,
                    Native.TitleOf(h), Native.IsCloaked(h) ? " cloaked" : ""));
                return ++n < 30;
            }, IntPtr.Zero);
            return sb.ToString();
        }

        public void Dispose()
        {
            timer.Stop();
            timer.Dispose();
            foreach (IntPtr h in hooks) Native.UnhookWinEvent(h);
            foreach (var o in overlays.Values) o.Dispose();
            overlays.Clear();
            g.Dispose();
            scratch.Dispose();
        }
    }

    // ================================================================ Zone editor

    // Drawn over the game area: drag to add a zone that stays bright, right-click to remove one.
    class ZoneEditor : Form
    {
        readonly Rectangle area;
        public readonly List<RectangleF> Zones;
        Point dragStart;
        Rectangle dragRect = Rectangle.Empty;
        bool dragging;

        public ZoneEditor(Rectangle area, List<RectangleF> zones)
        {
            this.area = area;
            Zones = new List<RectangleF>(zones);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(10, 14, 30);
            Opacity = 0.6;
            Cursor = Cursors.Cross;
            Text = "Pénombre";
            Font = new Font("Segoe UI", 11f);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Bounds = area;
            Activate();
        }

        RectangleF ToRelative(Rectangle r)
        {
            return new RectangleF((float)r.X / area.Width, (float)r.Y / area.Height, (float)r.Width / area.Width, (float)r.Height / area.Height);
        }

        Rectangle ToLocal(RectangleF z)
        {
            return new Rectangle((int)Math.Round(z.X * area.Width), (int)Math.Round(z.Y * area.Height),
                                 (int)Math.Round(z.Width * area.Width), (int)Math.Round(z.Height * area.Height));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { dragging = true; dragStart = e.Location; dragRect = Rectangle.Empty; }
            else if (e.Button == MouseButtons.Right)
            {
                for (int i = Zones.Count - 1; i >= 0; i--)
                    if (ToLocal(Zones[i]).Contains(e.Location)) { Zones.RemoveAt(i); Invalidate(); break; }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!dragging) return;
            dragRect = Rectangle.FromLTRB(Math.Min(dragStart.X, e.X), Math.Min(dragStart.Y, e.Y), Math.Max(dragStart.X, e.X), Math.Max(dragStart.Y, e.Y));
            dragRect.Intersect(ClientRectangle);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!dragging || e.Button != MouseButtons.Left) return;
            dragging = false;
            if (dragRect.Width >= 8 && dragRect.Height >= 8) Zones.Add(ToRelative(dragRect));
            dragRect = Rectangle.Empty;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { DialogResult = DialogResult.OK; Close(); }
            else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            else if (e.KeyCode == Keys.Delete) { Zones.Clear(); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var gr = e.Graphics;
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            using (var fill = new SolidBrush(Color.FromArgb(110, 160, 255)))
            using (var pen = new Pen(Color.White, 2))
            {
                foreach (RectangleF z in Zones)
                {
                    Rectangle r = ToLocal(z);
                    gr.FillRectangle(fill, r);
                    gr.DrawRectangle(pen, r);
                }
                if (dragRect.Width > 0 && dragRect.Height > 0)
                {
                    gr.FillRectangle(fill, dragRect);
                    using (var dash = new Pen(Color.White, 2) { DashStyle = DashStyle.Dash }) gr.DrawRectangle(dash, dragRect);
                }
            }

            string help = T.S("zoneHelp");
            SizeF size = gr.MeasureString(help, Font, ClientSize.Width - 40);
            var box = new RectangleF((ClientSize.Width - size.Width) / 2 - 12, 16, size.Width + 24, size.Height + 12);
            using (var bg = new SolidBrush(Color.Black)) gr.FillRectangle(bg, box);
            using (var fg = new SolidBrush(Color.White))
                gr.DrawString(help, Font, fg, new RectangleF(box.X + 12, box.Y + 6, size.Width, size.Height));
        }
    }

    // ================================================================ Settings window

    class SettingsForm : Form
    {
        readonly OverlayManager manager;
        readonly Action changed;
        bool loading;

        ComboBox presetBox, vignetteBox, renderBox;
        TrackBar bright, sat, temp, lift, intensity, fade;
        Label brightV, satV, tempV, liftV, intensityV, fadeV, autoState, zonesInfo, accountsEmpty;
        CheckBox autoBox, zonesBox, startupBox;
        DateTimePicker duskPick, nightPick, dawnPick, dayPick;
        CheckedListBox accounts;
        readonly Timer stateTimer;

        static readonly Color Accent = Color.FromArgb(63, 81, 181);

        public SettingsForm(OverlayManager manager, Action changed)
        {
            this.manager = manager;
            this.changed = changed;

            Text = "Pénombre - " + T.S("settingsT");
            Icon = TrayApp.AppIcon;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            var table = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(18, 10, 18, 14),
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(table);

            // --- Mood
            Section(table, T.S("sMood"), true);
            presetBox = Combo(240);
            for (int i = 0; i < Preset.Count; i++) presetBox.Items.Add(Preset.NameOf(i));
            Row(table, T.S("mood"), presetBox);
            bright = Slider(10, 100, 5, out brightV); Row(table, T.S("brightness"), Wrap(bright, brightV));
            sat = Slider(0, 100, 5, out satV); Row(table, T.S("saturation"), Wrap(sat, satV));
            temp = Slider(-100, 100, 10, out tempV); Row(table, T.S("temperature"), Wrap(temp, tempV));
            lift = Slider(0, 100, 5, out liftV); Row(table, T.S("lift"), Wrap(lift, liftV));
            Hint(table, T.S("customHint"));
            intensity = Slider(Settings.MinIntensity, Settings.MaxIntensity, 5, out intensityV); Row(table, T.S("intensity"), Wrap(intensity, intensityV));
            vignetteBox = Combo(240);
            for (int i = 0; i < 4; i++) vignetteBox.Items.Add(T.S("v" + i));
            Row(table, T.S("vignette"), vignetteBox);
            renderBox = Combo(240);
            renderBox.Items.Add(T.S("shader"));
            renderBox.Items.Add(T.S("veil"));
            Row(table, T.S("render"), renderBox);

            // --- Automatic mode
            Section(table, T.S("sAuto"), false);
            autoBox = Check(T.S("auto"));
            Row(table, "", autoBox);
            duskPick = TimePick(); Row(table, T.S("dusk"), duskPick);
            nightPick = TimePick(); Row(table, T.S("night"), nightPick);
            dawnPick = TimePick(); Row(table, T.S("dawn"), dawnPick);
            dayPick = TimePick(); Row(table, T.S("day"), dayPick);
            autoState = new Label { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 6, 3, 3) };
            Row(table, T.S("now"), autoState);

            // --- Transitions
            Section(table, T.S("sTrans"), false);
            fade = Slider(0, 2000, 100, out fadeV); Row(table, T.S("fade"), Wrap(fade, fadeV));

            // --- Game interface
            Section(table, T.S("sUi"), false);
            zonesBox = Check(T.S("zonesKeep"));
            Row(table, "", zonesBox);
            var zonesButton = new Button { Text = T.S("zonesEdit"), AutoSize = true, FlatStyle = FlatStyle.System };
            zonesInfo = new Label { AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(8, 8, 3, 3) };
            Row(table, "", Flow(zonesButton, zonesInfo));

            // --- Accounts
            Section(table, T.S("sAccounts"), false);
            accounts = new CheckedListBox { CheckOnClick = true, Width = 240, Height = 72, BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false };
            accountsEmpty = new Label { AutoSize = true, ForeColor = Color.DimGray, Text = T.S("noAccount"), Margin = new Padding(3, 6, 3, 3) };
            Row(table, "", Flow(accounts, accountsEmpty));
            Hint(table, T.S("accHint"));

            // --- Windows
            Section(table, T.S("sWindows"), false);
            startupBox = Check(T.S("startup"));
            Row(table, "", startupBox);
            var logButton = new Button { Text = T.S("openLog"), AutoSize = true, FlatStyle = FlatStyle.System };
            Row(table, "", logButton);

            var closeButton = new Button { Text = T.S("close"), AutoSize = true, FlatStyle = FlatStyle.System, Anchor = AnchorStyles.Right, Margin = new Padding(3, 14, 3, 3) };
            table.Controls.Add(closeButton, 1, table.RowCount++);
            CancelButton = closeButton;

            // --- Events
            presetBox.SelectedIndexChanged += (s, e) =>
            {
                if (loading) return;
                Settings.PresetIndex = presetBox.SelectedIndex;
                LoadSliders();
                Commit();
            };
            EventHandler customMoved = (s, e) =>
            {
                if (loading) return;
                Settings.CustomBrightness = bright.Value;
                Settings.CustomSaturation = sat.Value;
                Settings.CustomTemperature = temp.Value;
                Settings.CustomLift = lift.Value;
                if (Settings.PresetIndex != Preset.CustomIndex)
                {
                    Settings.PresetIndex = Preset.CustomIndex;
                    loading = true; presetBox.SelectedIndex = Preset.CustomIndex; loading = false;
                }
                Commit();
            };
            bright.ValueChanged += customMoved;
            sat.ValueChanged += customMoved;
            temp.ValueChanged += customMoved;
            lift.ValueChanged += customMoved;
            intensity.ValueChanged += (s, e) => { if (!loading) { Settings.Intensity = intensity.Value; Commit(); } };
            vignetteBox.SelectedIndexChanged += (s, e) => { if (!loading) { Settings.Vignette = vignetteBox.SelectedIndex; Commit(); } };
            renderBox.SelectedIndexChanged += (s, e) =>
            {
                if (loading) return;
                Settings.Shader = renderBox.SelectedIndex == 0;
                manager.Rebuild();
                Commit();
            };
            autoBox.CheckedChanged += (s, e) => { if (!loading) { Settings.Auto = autoBox.Checked; Commit(); } };
            EventHandler timeChanged = (s, e) =>
            {
                if (loading) return;
                Settings.Dusk = Minutes(duskPick);
                Settings.Night = Minutes(nightPick);
                Settings.Dawn = Minutes(dawnPick);
                Settings.Day = Minutes(dayPick);
                Commit();
            };
            duskPick.ValueChanged += timeChanged;
            nightPick.ValueChanged += timeChanged;
            dawnPick.ValueChanged += timeChanged;
            dayPick.ValueChanged += timeChanged;
            fade.ValueChanged += (s, e) => { if (!loading) { Settings.FadeMs = fade.Value; Commit(); } };
            zonesBox.CheckedChanged += (s, e) => { if (!loading) { Settings.ZonesEnabled = zonesBox.Checked; Commit(); } };
            zonesButton.Click += (s, e) => EditZones();
            accounts.ItemCheck += (s, e) =>
            {
                if (loading) return;
                string name = (string)accounts.Items[e.Index];
                if (e.NewValue == CheckState.Checked) Settings.Excluded.Remove(name);
                else Settings.Excluded.Add(name);
                Commit();
            };
            startupBox.CheckedChanged += (s, e) => { if (!loading) Startup.Set(startupBox.Checked); };
            logButton.Click += (s, e) => TrayApp.OpenLog();
            closeButton.Click += (s, e) => Close();

            stateTimer = new Timer { Interval = 1000 };
            stateTimer.Tick += (s, e) => UpdateLabels();
            stateTimer.Start();

            LoadAll();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) stateTimer.Dispose();
            base.Dispose(disposing);
        }

        // ---- building blocks

        static void Section(TableLayoutPanel t, string title, bool first)
        {
            var l = new Label
            {
                Text = title,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10.5f),
                ForeColor = Accent,
                Margin = new Padding(0, first ? 4 : 16, 0, 4),
            };
            t.Controls.Add(l, 0, t.RowCount);
            t.SetColumnSpan(l, 2);
            t.RowCount++;
        }

        static void Row(TableLayoutPanel t, string label, Control c)
        {
            var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 16, 3) };
            t.Controls.Add(l, 0, t.RowCount);
            t.Controls.Add(c, 1, t.RowCount);
            t.RowCount++;
        }

        static void Hint(TableLayoutPanel t, string text)
        {
            var l = new Label { Text = text, AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(3, 0, 3, 6) };
            t.Controls.Add(l, 1, t.RowCount);
            t.RowCount++;
        }

        static ComboBox Combo(int width) { return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width }; }

        static CheckBox Check(string text) { return new CheckBox { Text = text, AutoSize = true, Margin = new Padding(3, 4, 3, 3) }; }

        static TrackBar Slider(int min, int max, int step, out Label value)
        {
            value = new Label { AutoSize = true, Margin = new Padding(4, 8, 3, 3), MinimumSize = new Size(70, 0) };
            return new TrackBar
            {
                Minimum = min, Maximum = max, SmallChange = step, LargeChange = step * 2,
                TickStyle = TickStyle.None, Width = 200, AutoSize = false, Height = 30,
            };
        }

        static DateTimePicker TimePick()
        {
            return new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 80 };
        }

        static Control Wrap(Control a, Control b) { return Flow(a, b); }

        static Control Flow(params Control[] items)
        {
            var p = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0) };
            p.Controls.AddRange(items);
            return p;
        }

        static int Minutes(DateTimePicker p) { return p.Value.Hour * 60 + p.Value.Minute; }
        static DateTime At(int minutes) { return DateTime.Today.AddMinutes(minutes); }

        // ---- data

        public void LoadAll()
        {
            loading = true;
            presetBox.SelectedIndex = Settings.PresetIndex;
            LoadSlidersCore();
            intensity.Value = Settings.Intensity;
            vignetteBox.SelectedIndex = Settings.Vignette;
            renderBox.SelectedIndex = Settings.Shader ? 0 : 1;
            renderBox.Enabled = Mag.Ready;
            autoBox.Checked = Settings.Auto;
            duskPick.Value = At(Settings.Dusk);
            nightPick.Value = At(Settings.Night);
            dawnPick.Value = At(Settings.Dawn);
            dayPick.Value = At(Settings.Day);
            fade.Value = Math.Min(fade.Maximum, Settings.FadeMs);
            zonesBox.Checked = Settings.ZonesEnabled;
            startupBox.Checked = Startup.IsEnabled();
            loading = false;
            RefreshAccounts();
            UpdateLabels();
        }

        void LoadSliders()
        {
            loading = true;
            LoadSlidersCore();
            loading = false;
        }

        void LoadSlidersCore()
        {
            int[] v = Preset.Get(Settings.PresetIndex).ToSliders();
            if (Settings.PresetIndex == Preset.CustomIndex)
                v = new[] { Settings.CustomBrightness, Settings.CustomSaturation, Settings.CustomTemperature, Settings.CustomLift };
            bright.Value = v[0];
            sat.Value = v[1];
            temp.Value = v[2];
            lift.Value = v[3];
        }

        public void RefreshAccounts()
        {
            loading = true;
            accounts.Items.Clear();
            foreach (string name in manager.Accounts()) accounts.Items.Add(name, !Settings.Excluded.Contains(name));
            loading = false;
            accounts.Visible = accounts.Items.Count > 0;
            accountsEmpty.Visible = accounts.Items.Count == 0;
        }

        void UpdateLabels()
        {
            brightV.Text = bright.Value + " %";
            satV.Text = sat.Value + " %";
            tempV.Text = temp.Value == 0 ? T.S("neutral") : (temp.Value < 0 ? T.S("cold") : T.S("warm")) + " " + Math.Abs(temp.Value);
            liftV.Text = lift.Value + " %";
            intensityV.Text = intensity.Value + " %";
            fadeV.Text = fade.Value == 0 ? T.S("instant") : (fade.Value / 1000.0).ToString("0.0") + " s";
            autoState.Text = Settings.Auto ? DayCycle.Describe(DateTime.Now) : "-";
            zonesInfo.Text = string.Format(T.S("zonesCount"), Settings.Zones.Count);
            bool times = Settings.Auto;
            duskPick.Enabled = nightPick.Enabled = dawnPick.Enabled = dayPick.Enabled = times;
        }

        void Commit()
        {
            UpdateLabels();
            changed();
        }

        void EditZones()
        {
            Rectangle area;
            if (!manager.TryGetEditArea(out area))
            {
                MessageBox.Show(this, T.S("zonesNoGame"), "Pénombre", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var editor = new ZoneEditor(area, Settings.Zones))
            {
                if (editor.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Zones = editor.Zones;
                    Settings.ZonesEnabled = Settings.Zones.Count > 0;
                    loading = true; zonesBox.Checked = Settings.ZonesEnabled; loading = false;
                    Commit();
                }
            }
        }
    }

    // ================================================================ Tray icon & menu

    class TrayApp : IDisposable
    {
        const int HK_TOGGLE = 1, HK_PRESET = 2, HK_UP = 3, HK_DOWN = 4, HK_VIGNETTE = 5, HK_AUTO = 6;
        static readonly int[] IntensitySteps = { 40, 50, 60, 70, 80, 90, 100 };

        public static Icon AppIcon;

        readonly NotifyIcon tray = new NotifyIcon();
        readonly OverlayManager overlays;
        readonly HotkeyWindow hotkeys;
        readonly Timer tooltipTimer;
        ToolStripMenuItem enableItem, autoItem, presetMenu, intensityMenu, vignetteMenu, accountsMenu;
        SettingsForm settingsForm;
        bool warnedShader;

        public TrayApp()
        {
            AppIcon = LoadIcon();
            overlays = new OverlayManager();
            BuildMenu();
            tray.Icon = AppIcon;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Command(HK_TOGGLE); };
            tray.Visible = true;
            overlays.CountChanged += (s, e) =>
            {
                RefreshUi();
                if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshAccounts();
            };

            hotkeys = new HotkeyWindow(Command);
            const uint CTRL_ALT = 0x0002 | 0x0001;
            var failed = new List<string>();
            if (!hotkeys.Register(HK_TOGGLE, CTRL_ALT, 0x4E)) failed.Add("Ctrl+Alt+N");
            if (!hotkeys.Register(HK_PRESET, CTRL_ALT, 0x50)) failed.Add("Ctrl+Alt+P");
            if (!hotkeys.Register(HK_UP, CTRL_ALT, 0x21)) failed.Add("Ctrl+Alt+PgUp");
            if (!hotkeys.Register(HK_DOWN, CTRL_ALT, 0x22)) failed.Add("Ctrl+Alt+PgDn");
            if (!hotkeys.Register(HK_VIGNETTE, CTRL_ALT, 0x56)) failed.Add("Ctrl+Alt+V");
            if (!hotkeys.Register(HK_AUTO, CTRL_ALT, 0x41)) failed.Add("Ctrl+Alt+A");
            if (failed.Count > 0) Log.Info("Shortcuts unavailable: " + string.Join(", ", failed.ToArray()));

            // The tooltip shows the automatic mode progress.
            tooltipTimer = new Timer { Interval = 30000 };
            tooltipTimer.Tick += (s, e) => RefreshUi();
            tooltipTimer.Start();

            RefreshUi();
            string tip = T.S("help");
            if (failed.Count > 0) tip += "\n" + T.S("noHotkeys") + string.Join(", ", failed.ToArray());
            tray.ShowBalloonTip(5000, "Pénombre " + Program.Version, tip, ToolTipIcon.None);
        }

        void Command(int cmd)
        {
            switch (cmd)
            {
                case HK_TOGGLE: Settings.Enabled = !Settings.Enabled; break;
                case HK_AUTO: Settings.Auto = !Settings.Auto; Settings.Enabled = true; break;
                case HK_UP: Settings.Intensity = Settings.ClampIntensity(Settings.Intensity + 5); Settings.Enabled = true; break;
                case HK_DOWN: Settings.Intensity = Settings.ClampIntensity(Settings.Intensity - 5); Settings.Enabled = true; break;
                case HK_VIGNETTE: Settings.Vignette = (Settings.Vignette + 1) % 4; Settings.Enabled = true; break;
                case HK_PRESET: Settings.PresetIndex = (Settings.PresetIndex + 1) % Preset.Count; Settings.Enabled = true; break;
            }
            Changed();
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.LoadAll();
        }

        void Changed()
        {
            overlays.ApplyLook();
            Settings.SaveSoon();
            RefreshUi();
        }

        void OpenSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(overlays, Changed);
                settingsForm.Show();
            }
            if (settingsForm.WindowState == FormWindowState.Minimized) settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Activate();
        }

        public static void OpenLog()
        {
            try
            {
                if (File.Exists(Log.FilePath)) Process.Start("notepad.exe", "\"" + Log.FilePath + "\"");
                else MessageBox.Show(T.S("noLog"), "Pénombre", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { Log.Error("Open log", ex); }
        }

        void BuildMenu()
        {
            var menu = new ContextMenuStrip();

            enableItem = new ToolStripMenuItem(T.S("enabled"));
            enableItem.ShortcutKeyDisplayString = "Ctrl+Alt+N";
            enableItem.Click += (s, e) => Command(HK_TOGGLE);

            autoItem = new ToolStripMenuItem(T.S("auto"));
            autoItem.ShortcutKeyDisplayString = "Ctrl+Alt+A";
            autoItem.Click += (s, e) => Command(HK_AUTO);

            presetMenu = new ToolStripMenuItem(T.S("mood"));
            presetMenu.ShortcutKeyDisplayString = "Ctrl+Alt+P";
            for (int i = 0; i < Preset.Count; i++)
            {
                int idx = i;
                var it = new ToolStripMenuItem(Preset.NameOf(i));
                it.Click += (s, e) => { Settings.PresetIndex = idx; Settings.Enabled = true; Changed(); };
                presetMenu.DropDownItems.Add(it);
            }

            intensityMenu = new ToolStripMenuItem(T.S("intensity"));
            intensityMenu.ShortcutKeyDisplayString = "Ctrl+Alt+PgUp/PgDn";
            foreach (int v in IntensitySteps)
            {
                int val = v;
                var it = new ToolStripMenuItem(val + " %");
                it.Tag = val;
                it.Click += (s, e) => { Settings.Intensity = val; Settings.Enabled = true; Changed(); };
                intensityMenu.DropDownItems.Add(it);
            }

            vignetteMenu = new ToolStripMenuItem(T.S("vignette"));
            vignetteMenu.ShortcutKeyDisplayString = "Ctrl+Alt+V";
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var it = new ToolStripMenuItem(T.S("v" + i));
                it.Click += (s, e) => { Settings.Vignette = idx; Settings.Enabled = true; Changed(); };
                vignetteMenu.DropDownItems.Add(it);
            }

            accountsMenu = new ToolStripMenuItem(T.S("accounts"));
            accountsMenu.DropDownItems.Add(new ToolStripMenuItem(T.S("noAccount")) { Enabled = false });
            accountsMenu.DropDownOpening += (s, e) => FillAccounts();

            var settings = new ToolStripMenuItem(T.S("settings"));
            settings.Font = new Font(settings.Font, FontStyle.Bold);
            settings.Click += (s, e) => OpenSettings();

            var diag = new ToolStripMenuItem(T.S("diag"));
            diag.Click += (s, e) =>
            {
                try { Clipboard.SetText(overlays.Diagnostics()); tray.ShowBalloonTip(2000, "Pénombre", T.S("diagDone"), ToolTipIcon.None); }
                catch (Exception ex) { Log.Error("Diagnostics", ex); }
            };

            var log = new ToolStripMenuItem(T.S("openLog"));
            log.Click += (s, e) => OpenLog();

            var quit = new ToolStripMenuItem(T.S("quit"));
            quit.Click += (s, e) => Application.Exit();

            menu.Items.Add(enableItem);
            menu.Items.Add(autoItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(presetMenu);
            menu.Items.Add(intensityMenu);
            menu.Items.Add(vignetteMenu);
            menu.Items.Add(accountsMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(settings);
            menu.Items.Add(diag);
            menu.Items.Add(log);
            menu.Items.Add(quit);
            tray.ContextMenuStrip = menu;
        }

        void FillAccounts()
        {
            accountsMenu.DropDownItems.Clear();
            List<string> names = overlays.Accounts();
            if (names.Count == 0)
            {
                accountsMenu.DropDownItems.Add(new ToolStripMenuItem(T.S("noAccount")) { Enabled = false });
                return;
            }
            foreach (string name in names)
            {
                string n = name;
                var it = new ToolStripMenuItem(n) { Checked = !Settings.Excluded.Contains(n) };
                it.Click += (s, e) =>
                {
                    if (!Settings.Excluded.Remove(n)) Settings.Excluded.Add(n);
                    Changed();
                    if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshAccounts();
                };
                accountsMenu.DropDownItems.Add(it);
            }
        }

        void RefreshUi()
        {
            string state;
            if (!Settings.Enabled) state = T.S("off");
            else
            {
                state = Preset.NameOf(Settings.PresetIndex) + " " + Settings.Intensity + " %";
                if (Settings.Auto) state += " · auto " + (int)Math.Round(DayCycle.Factor(DateTime.Now) * 100) + " %";
            }
            string text = "Pénombre : " + state + " · " + overlays.Count + " " + T.S("windows");
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;

            enableItem.Checked = Settings.Enabled;
            autoItem.Checked = Settings.Auto;
            for (int i = 0; i < presetMenu.DropDownItems.Count; i++) ((ToolStripMenuItem)presetMenu.DropDownItems[i]).Checked = i == Settings.PresetIndex;
            for (int i = 0; i < vignetteMenu.DropDownItems.Count; i++) ((ToolStripMenuItem)vignetteMenu.DropDownItems[i]).Checked = i == Settings.Vignette;
            foreach (ToolStripMenuItem it in intensityMenu.DropDownItems) it.Checked = (int)it.Tag == Settings.Intensity;

            if (Settings.Shader && (overlays.ShaderFailed || !Mag.Ready) && !warnedShader)
            {
                warnedShader = true;
                tray.ShowBalloonTip(4000, "Pénombre", T.S("noShader"), ToolTipIcon.Warning);
            }
        }

        static Icon LoadIcon()
        {
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("penombre.ico"))
                    if (s != null) return new Icon(s, SystemInformation.SmallIconSize);
            }
            catch { }
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            tooltipTimer.Dispose();
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.Dispose();
            hotkeys.Dispose();
            overlays.Dispose();
            tray.Visible = false;
            tray.Dispose();
        }
    }
}
