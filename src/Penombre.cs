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
// WinForms app in C# 5, built with the csc.exe shipped with Windows (build.bat): this file holds
// the app, Interface.cs its hand-drawn controls and colors, SelfTest.cs the test build.
// Nothing is injected into, read from or sent to the Dofus client: the filter is a separate
// window. Two renderers:
//   - Shader: the Windows Magnification API redraws the game area through a 5x5 color matrix
//     (real per-pixel darkening, desaturation and tint);
//   - Veil: a plain semi-transparent colored window (fallback).
// A per-pixel alpha vignette and an animated film grain can be added on top of either.
//
// Multi-account: every filter is clipped (SetWindowRgn) to the part of its game window that is
// actually visible, so two filters never overlap and the darkening never stacks up. Each
// account can use the default settings or its own. Zones of the game UI can be cut out of the
// filter so they stay bright.
//
// Options: --lang=en|fr|es, --data=<folder> (settings and log folder)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Pénombre")]
[assembly: AssemblyDescription("A night mode for Dofus 3")]
[assembly: AssemblyProduct("Pénombre")]
[assembly: AssemblyCopyright("Copyright (C) 2026 Sokenzane - GPL-3.0")]
[assembly: AssemblyVersion("0.5.2.0")]
[assembly: AssemblyFileVersion("0.5.2.0")]

namespace Penombre
{
    static partial class Program
    {
        public const string Version = "0.5.2";
        public static bool SelfTestMode;   // test build only (src/SelfTest.cs)
        public static string DataOverride; // --data=<folder>: settings and log elsewhere
        public static string InstanceName { get { return SelfTestMode ? "Sokenzane.Penombre.SelfTest" : "Sokenzane.Penombre"; } }
        static bool errorShown;

        static partial void RunSelfTest(TrayApp app);

        [STAThread]
        static void Main(string[] args)
        {
            string lang = null;
            foreach (string a in args)
            {
                if (a.StartsWith("--lang=")) lang = a.Substring(7);
                if (a.StartsWith("--data=")) DataOverride = Path.GetFullPath(a.Substring(7));
                if (a == "--selftest") SelfTestMode = true;
            }
            T.Init(lang);

            bool first;
            using (var mutex = new System.Threading.Mutex(true, InstanceName, out first))
            {
                if (!first)
                {
                    // Started again: the running Pénombre opens its settings window.
                    IntPtr running = Native.FindWindowEx(new IntPtr(-3) /* HWND_MESSAGE */, IntPtr.Zero, null, HotkeyWindow.Caption);
                    if (running != IntPtr.Zero)
                    {
                        Native.AllowSetForegroundWindow(-1 /* ASFW_ANY */);
                        Native.PostMessage(running, HotkeyWindow.WM_SHOW_SETTINGS, IntPtr.Zero, IntPtr.Zero);
                    }
                    else MessageBox.Show(T.S("already"), "Pénombre", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Real pixels, even with Windows display scaling (125 %, 150 %...).
                try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); }
                catch { try { Native.SetProcessDPIAware(); } catch { } }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => OnError(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Error("Unhandled exception", e.ExceptionObject as Exception);

                Settings.Init();
                Settings.Load();
                if (lang == null && Settings.Language.Length > 0) T.Init(Settings.Language);
                Log.Info("Pénombre " + Program.Version + " started - " + WindowsVersion() + (Environment.Is64BitProcess ? " x64" : " x86")
                         + (Settings.IsPortable ? " - portable" : ""));
                Mag.Init();
                Startup.RefreshPath();
                try
                {
                    using (var app = new TrayApp())
                    {
                        RunSelfTest(app);
                        Application.Run();
                    }
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

        // The real Windows version: Environment.OSVersion lies (6.2) without a compatibility manifest.
        public static string WindowsVersion()
        {
            try
            {
                var v = new Native.OSVERSIONINFOEX();
                v.Size = Marshal.SizeOf(typeof(Native.OSVERSIONINFOEX));
                if (Native.RtlGetVersion(ref v) == 0)
                {
                    string name = v.Major == 10 && v.Build >= 22000 ? "Windows 11" : v.Major == 10 ? "Windows 10" : "Windows";
                    string display = "", ubr = "";
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    {
                        if (k != null)
                        {
                            object d = k.GetValue("DisplayVersion"), u = k.GetValue("UBR");
                            if (d != null) display = " " + d;
                            if (u != null) ubr = "." + u;
                        }
                    }
                    return name + display + " (" + v.Major + "." + v.Minor + "." + v.Build + ubr + ")";
                }
            }
            catch { }
            return "Windows " + Environment.OSVersion.Version;
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
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
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
        [DllImport("ntdll.dll")] public static extern int RtlGetVersion(ref OSVERSIONINFOEX v);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int processId);
        [DllImport("user32.dll")] public static extern uint GetQueueStatus(uint flags);
        [DllImport("user32.dll")] public static extern bool UpdateWindow(IntPtr h);

        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int W, H; public SIZE(int w, int h) { W = w; H = h; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] public struct BLENDFUNCTION { public byte Op, Flags, Alpha, Format; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OSVERSIONINFOEX
        {
            public int Size, Major, Minor, Build, Platform;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string CsdVersion;
            public ushort SpMajor, SpMinor, SuiteMask;
            public byte ProductType, Reserved;
        }

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

        // Draws a 32-bit ARGB bitmap into a layered window (per-pixel alpha), at a global opacity.
        public static void PushBitmap(IntPtr window, IntPtr hbitmap, Rectangle r, byte opacity)
        {
            IntPtr screen = GetDC(IntPtr.Zero);
            IntPtr mem = CreateCompatibleDC(screen);
            IntPtr old = SelectObject(mem, hbitmap);
            var dst = new POINT(r.X, r.Y);
            var size = new SIZE(r.Width, r.Height);
            var src = new POINT(0, 0);
            var blend = Blend(opacity);
            UpdateLayeredWindow(window, screen, ref dst, ref size, mem, ref src, 0, ref blend, ULW_ALPHA);
            SelectObject(mem, old);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, screen);
        }

        // Changes only the global opacity of a layered window drawn with PushBitmap.
        public static void PushOpacity(IntPtr window, byte opacity)
        {
            var blend = Blend(opacity);
            UpdateLayeredWindowAlpha(window, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref blend, ULW_ALPHA);
        }

        static BLENDFUNCTION Blend(byte alpha)
        {
            return new BLENDFUNCTION { Op = 0, Flags = 0, Alpha = alpha, Format = 1 /* AC_SRC_ALPHA */ };
        }
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
        static int column; // 0 English, 1 French, 2 Spanish
        static readonly Dictionary<string, string[]> texts = new Dictionary<string, string[]>
        {
            // key            { English, French, Spanish }
            { "already",    new[] { "Pénombre is already running (icon near the clock, in the notification area).",
                                    "Pénombre tourne déjà (icône près de l'horloge, dans la zone de notification).",
                                    "Pénombre ya está en marcha (icono junto al reloj, en el área de notificación)." } },
            { "error",      new[] { "Something went wrong. Details are in the log (menu > Open the log).",
                                    "Une erreur s'est produite. Le détail est dans le journal (menu > Ouvrir le journal).",
                                    "Algo ha fallado. Los detalles están en el registro (menú > Abrir el registro)." } },
            { "enabled",    new[] { "Filter on", "Filtre actif",
                                    "Filtro activo" } },
            { "auto",       new[] { "Follow the real time", "Suivre l'heure réelle",
                                    "Seguir la hora real" } },
            { "mood",       new[] { "Mood", "Ambiance",
                                    "Ambiente" } },
            { "custom",     new[] { "Custom", "Personnalisée",
                                    "Personalizado" } },
            { "intensity",  new[] { "Intensity", "Intensité",
                                    "Intensidad" } },
            { "render",     new[] { "Rendering", "Rendu",
                                    "Renderizado" } },
            { "shader",     new[] { "Shader (reworked colors)", "Shader (couleurs retravaillées)",
                                    "Shader (colores retocados)" } },
            { "veil",       new[] { "Veil (simple darkening)", "Voile (simple assombrissement)",
                                    "Velo (oscurecimiento simple)" } },
            { "noShader",   new[] { "Shader unavailable on this PC: veil used instead.", "Shader indisponible sur ce PC : voile utilisé à la place.",
                                    "Shader no disponible en este PC: se usa el velo." } },
            { "vignette",   new[] { "Vignette", "Vignettage",
                                    "Viñeta" } },
            { "v0",         new[] { "None", "Aucun",
                                    "Ninguna" } },
            { "v1",         new[] { "Light", "Léger",
                                    "Ligera" } },
            { "v2",         new[] { "Medium", "Moyen",
                                    "Media" } },
            { "v3",         new[] { "Strong", "Fort",
                                    "Fuerte" } },
            { "vColor",     new[] { "Vignette tint", "Teinte du vignettage",
                                    "Tono de la viñeta" } },
            { "choose",     new[] { "Choose…", "Choisir…",
                                    "Elegir…" } },
            { "reset",      new[] { "Reset", "Par défaut",
                                    "Por defecto" } },
            { "grain",      new[] { "Film grain", "Grain pellicule",
                                    "Grano de película" } },
            { "g0",         new[] { "None", "Aucun",
                                    "Ninguno" } },
            { "g1",         new[] { "Light", "Léger",
                                    "Ligero" } },
            { "g2",         new[] { "Medium", "Moyen",
                                    "Medio" } },
            { "accounts",   new[] { "Accounts", "Comptes",
                                    "Cuentas" } },
            { "noAccount",  new[] { "No Dofus window", "Aucune fenêtre Dofus",
                                    "Ninguna ventana de Dofus" } },
            { "settings",   new[] { "Settings…", "Réglages…",
                                    "Ajustes…" } },
            { "settingsT",  new[] { "Settings", "Réglages",
                                    "Ajustes" } },
            { "diag",       new[] { "Copy diagnostics", "Copier le diagnostic",
                                    "Copiar el diagnóstico" } },
            { "duskTint",   new[] { "Dusk colours at sunset and sunrise", "Teinte de crépuscule au coucher et au lever du soleil",
                                    "Tono de atardecer al ponerse y salir el sol" } },
            { "duskHint",   new[] { "While night falls and while day breaks, the filter takes the warm colours of the Dusk mood, then goes back to your mood.",
                                    "Pendant que la nuit tombe et que le jour se lève, le filtre prend les couleurs chaudes de l'ambiance Crépuscule, puis revient à ton ambiance.",
                                    "Mientras cae la noche y mientras amanece, el filtro toma los colores cálidos del ambiente Atardecer y luego vuelve a tu ambiente." } },
            { "pause",      new[] { "Pause", "Pause",
                                    "Pausa" } },
            { "pauseFor",   new[] { "For {0}", "Pendant {0}",
                                    "Durante {0}" } },
            { "pauseUntil", new[] { "Paused until {0}", "En pause jusqu'à {0}",
                                    "En pausa hasta las {0}" } },
            { "resume",     new[] { "Resume now", "Reprendre maintenant",
                                    "Reanudar ahora" } },
            { "langAuto",   new[] { "Windows language", "Langue de Windows", "Idioma de Windows" } },
            { "langTip",    new[] { "Language", "Langue", "Idioma" } },
            { "updCheck",   new[] { "Check for updates (once a day, on GitHub)", "Vérifier les mises à jour (une fois par jour, sur GitHub)",
                                    "Buscar actualizaciones (una vez al día, en GitHub)" } },
            { "updNow",     new[] { "Check now", "Vérifier maintenant",
                                    "Buscar ahora" } },
            { "updSee",     new[] { "Get version {0}", "Obtenir la version {0}",
                                    "Obtener la versión {0}" } },
            { "updChecking",new[] { "Checking…", "Vérification…",
                                    "Buscando…" } },
            { "updLatest",  new[] { "You have the latest version.", "Tu as la dernière version.",
                                    "Tienes la última versión." } },
            { "updNew",     new[] { "Version {0} is available.", "La version {0} est disponible.",
                                    "La versión {0} está disponible." } },
            { "updFail",    new[] { "Cannot reach GitHub right now.", "Impossible de joindre GitHub pour le moment.",
                                    "No se puede contactar con GitHub ahora mismo." } },
            { "updMenu",    new[] { "New version {0} available…", "Nouvelle version {0} disponible…",
                                    "Nueva versión {0} disponible…" } },
            { "updBalloon", new[] { "Pénombre {0} is available. Click to open its page.", "Pénombre {0} est disponible. Clique pour ouvrir sa page.",
                                    "Pénombre {0} está disponible. Haz clic para abrir su página." } },
            { "diagDone",   new[] { "Diagnostics copied to the clipboard.", "Diagnostic copié dans le presse-papiers.",
                                    "Diagnóstico copiado al portapapeles." } },
            { "openLog",    new[] { "Open the log", "Ouvrir le journal",
                                    "Abrir el registro" } },
            { "noLog",      new[] { "The log is empty for now.", "Le journal est vide pour l'instant.",
                                    "El registro está vacío por ahora." } },
            { "quit",       new[] { "Quit", "Quitter",
                                    "Salir" } },
            { "off",        new[] { "off", "désactivé",
                                    "desactivado" } },
            { "windows",    new[] { "window(s)", "fenêtre(s)",
                                    "ventana(s)" } },
            { "noHotkeys",  new[] { "Shortcuts unavailable (used by another app): ", "Raccourcis indisponibles (pris par une autre appli) : ",
                                    "Atajos no disponibles (usados por otra aplicación): " } },
            { "help",       new[] { "Right-click the icon for the menu. {0}: on / off · {1}: peek (hold)",
                                    "Clic droit sur l'icône pour le menu. {0} : activer / couper · {1} : coup d'œil (maintenir)",
                                    "Clic derecho en el icono para el menú. {0}: activar / desactivar · {1}: vistazo (mantener)" } },
            { "p0",         new[] { "Blue night", "Nuit bleutée",
                                    "Noche azulada" } },
            { "p1",         new[] { "Moonlight", "Clair de lune",
                                    "Claro de luna" } },
            { "p2",         new[] { "Dusk", "Crépuscule",
                                    "Atardecer" } },
            { "p3",         new[] { "Pitch black", "Nuit noire",
                                    "Noche cerrada" } },
            { "p4",         new[] { "Old-school night", "Nuit d'antan",
                                    "Noche de antaño" } },
            // Settings window - pages
            { "tMood",      new[] { "Mood", "Ambiance",
                                    "Ambiente" } },
            { "tAuto",      new[] { "Schedule", "Horaires",
                                    "Horarios" } },
            { "tAccounts",  new[] { "Accounts", "Comptes",
                                    "Cuentas" } },
            { "tDisplay",   new[] { "Display", "Affichage",
                                    "Visualización" } },
            { "tKeys",      new[] { "Shortcuts", "Raccourcis",
                                    "Atajos" } },
            { "tGeneral",   new[] { "General", "Général",
                                    "General" } },
            // Mood
            { "defaultHint",new[] { "Default settings, used by every account without its own settings.",
                                    "Réglages par défaut, utilisés par tous les comptes sans réglages propres.",
                                    "Ajustes por defecto, usados por todas las cuentas sin ajustes propios." } },
            { "brightness", new[] { "Brightness", "Luminosité",
                                    "Brillo" } },
            { "saturation", new[] { "Saturation", "Saturation",
                                    "Saturación" } },
            { "temperature",new[] { "Temperature", "Température",
                                    "Temperatura" } },
            { "cold",       new[] { "cold", "froid",
                                    "frío" } },
            { "warm",       new[] { "warm", "chaud",
                                    "cálido" } },
            { "neutral",    new[] { "neutral", "neutre",
                                    "neutro" } },
            { "lift",       new[] { "Lifted blacks", "Noirs relevés",
                                    "Negros elevados" } },
            { "customHint", new[] { "Moving a slider switches to the Custom mood.", "Toucher un curseur passe sur l'ambiance Personnalisée.",
                                    "Mover un control deslizante cambia al ambiente Personalizado." } },
            // Schedule
            { "fixedTimes", new[] { "Fixed times", "Horaires fixes",
                                    "Horarios fijos" } },
            { "sunTimes",   new[] { "Real sunset and sunrise", "Vrai coucher et lever du soleil",
                                    "Puesta y salida reales del sol" } },
            { "dusk",       new[] { "Dusk starts", "Début du crépuscule",
                                    "Inicio del atardecer" } },
            { "night",      new[] { "Full night", "Nuit complète",
                                    "Noche completa" } },
            { "dawn",       new[] { "Dawn starts", "Début de l'aube",
                                    "Inicio del alba" } },
            { "day",        new[] { "Full day", "Jour complet",
                                    "Día completo" } },
            { "city",       new[] { "City", "Ville",
                                    "Ciudad" } },
            { "customPlace",new[] { "Other place (coordinates)", "Autre lieu (coordonnées)",
                                    "Otro lugar (coordenadas)" } },
            { "latitude",   new[] { "Latitude", "Latitude",
                                    "Latitud" } },
            { "longitude",  new[] { "Longitude", "Longitude",
                                    "Longitud" } },
            { "today",      new[] { "Today", "Aujourd'hui",
                                    "Hoy" } },
            { "sunInfo",    new[] { "sunset {0} · sunrise {1}", "coucher {0} · lever {1}",
                                    "puesta {0} · salida {1}" } },
            { "sunNone",    new[] { "no sunset or sunrise today: fixed times used", "pas de coucher ou de lever aujourd'hui : horaires fixes utilisés",
                                    "hoy no hay puesta ni salida del sol: se usan los horarios fijos" } },
            { "sunHint",    new[] { "Night falls 45 min before sunset and is complete 45 min after; same around sunrise.",
                                    "La nuit tombe 45 min avant le coucher et est complète 45 min après ; pareil autour du lever.",
                                    "La noche cae 45 min antes de la puesta y es completa 45 min después; igual alrededor de la salida." } },
            { "now",        new[] { "Now", "Maintenant",
                                    "Ahora" } },
            { "stDay",      new[] { "day (no filter)", "jour (pas de filtre)",
                                    "día (sin filtro)" } },
            { "stDusk",     new[] { "dusk", "crépuscule",
                                    "atardecer" } },
            { "stNight",    new[] { "night", "nuit",
                                    "noche" } },
            { "stDawn",     new[] { "dawn", "aube",
                                    "alba" } },
            { "fade",       new[] { "Transitions", "Transitions",
                                    "Transiciones" } },
            { "fadeHint",   new[] { "Fade when turning on or off and when the mood changes.", "Fondu à l'activation, à la coupure et au changement d'ambiance.",
                                    "Fundido al activar, al desactivar y al cambiar de ambiente." } },
            { "instant",    new[] { "instant", "instantané",
                                    "instantáneo" } },
            // Accounts
            { "accList",    new[] { "Character", "Personnage",
                                    "Personaje" } },
            { "offline",    new[] { "(not open)", "(pas ouvert)",
                                    "(no abierto)" } },
            { "accFilter",  new[] { "Filter this account", "Filtrer ce compte",
                                    "Filtrar esta cuenta" } },
            { "accOwn",     new[] { "Use its own settings", "Utiliser des réglages propres",
                                    "Usar ajustes propios" } },
            { "ownHint",    new[] { "Changing one of these gives this character its own settings.", "Changer l'un de ces réglages donne à ce personnage ses propres réglages.",
                                    "Cambiar uno de estos ajustes da a este personaje sus propios ajustes." } },
            { "accForget",  new[] { "Forget this account", "Oublier ce compte",
                                    "Olvidar esta cuenta" } },
            { "accHint",    new[] { "Characters are recognized from the Dofus window title. Shortcuts change the settings of the Dofus in the foreground.",
                                    "Les personnages sont reconnus au titre de la fenêtre Dofus. Les raccourcis modifient les réglages du Dofus au premier plan.",
                                    "Los personajes se reconocen por el título de la ventana de Dofus. Los atajos cambian los ajustes del Dofus en primer plano." } },
            { "ownSettings",new[] { "own settings", "réglages propres",
                                    "ajustes propios" } },
            // Display
            { "sZones",     new[] { "Bright zones", "Zones claires",
                                    "Zonas claras" } },
            { "zonesKeep",  new[] { "Keep some zones bright (spells, chat…)", "Garder des zones claires (sorts, chat…)",
                                    "Mantener zonas claras (hechizos, chat…)" } },
            { "zonesEdit",  new[] { "Draw the zones…", "Définir les zones…",
                                    "Definir las zonas…" } },
            { "zonesCount", new[] { "{0} zone(s)", "{0} zone(s)",
                                    "{0} zona(s)" } },
            { "zonesAnchor",new[] { "Anchor zones to the nearest edge", "Ancrer les zones au bord le plus proche",
                                    "Anclar las zonas al borde más cercano" } },
            { "zonesScale", new[] { "Zones grow with the window height", "Les zones grandissent avec la hauteur de la fenêtre",
                                    "Las zonas crecen con la altura de la ventana" } },
            { "zonesHint",  new[] { "Untick both to keep zones proportional to the window.", "Décoche les deux pour des zones proportionnelles à la fenêtre.",
                                    "Desmarca ambas para zonas proporcionales a la ventana." } },
            { "zonesNoGame",new[] { "Open Dofus first: zones are drawn over its window.", "Ouvre Dofus d'abord : les zones se dessinent sur sa fenêtre.",
                                    "Abre Dofus primero: las zonas se dibujan sobre su ventana." } },
            { "zoneHelp",   new[] { "Drag to draw a zone that stays bright · right-click: remove · Del: clear all · Enter: save · Esc: cancel",
                                    "Glisse pour dessiner une zone qui restera claire · clic droit : supprimer · Suppr : tout effacer · Entrée : valider · Échap : annuler",
                                    "Arrastra para dibujar una zona que seguirá clara · clic derecho: borrar · Supr: borrar todo · Intro: guardar · Esc: cancelar" } },
            { "sBehavior",  new[] { "Behavior", "Comportement",
                                    "Comportamiento" } },
            { "sIcon",      new[] { "Icon near the clock", "Icône près de l'horloge",
                                    "Icono junto al reloj" } },
            { "ico0",       new[] { "Moon egg", "Œuf de lune",
                                    "Huevo lunar" } },
            { "ico0d",      new[] { "Changes colour with the sky (default)", "Change de couleur avec le ciel (par défaut)",
                                    "Cambia de color con el cielo (por defecto)" } },
            { "iconAuto",   new[] { "Match the icon to the time of day", "Adapter l'icône à l'heure",
                                    "Adaptar el icono a la hora" } },
            { "iconHint",   new[] { "Ticked: the icon follows the sky of the times set in Schedule (night, dusk, day), even if the filter does not. Unticked: click the picture you want to keep. The grey picture always means the filter is off.",
                                    "Coché : l'icône suit le ciel selon les horaires de la rubrique Horaires (nuit, crépuscule, jour), même si le filtre ne les suit pas. Décoché : clique sur l'image de ton choix pour la garder. L'image grise indique toujours un filtre coupé.",
                                    "Marcado: el icono sigue el cielo según los horarios de la sección Horarios (noche, atardecer, día), aunque el filtro no los siga. Desmarcado: haz clic en la imagen que quieras conservar. La imagen gris siempre indica un filtro desactivado." } },
            { "ico1",       new[] { "Watcher egg", "Œuf veilleur",
                                    "Huevo vigía" } },
            { "ico1d",      new[] { "Awake at night, asleep by day", "Veille la nuit, dort le jour",
                                    "Vigila de noche, duerme de día" } },
            { "ico2",       new[] { "Round moon", "Lune bombée",
                                    "Luna redonda" } },
            { "ico2d",      new[] { "The most readable when small", "La plus lisible en petit",
                                    "La más legible en pequeño" } },
            { "sColors",    new[] { "Colors", "Couleurs",
                                    "Colores" } },
            { "sEffects",   new[] { "Effects", "Effets",
                                    "Efectos" } },
            { "sFixed",     new[] { "Fixed times", "Horaires fixes",
                                    "Horarios fijos" } },
            { "sSun",       new[] { "Real sun", "Vrai soleil",
                                    "Sol real" } },
            { "sTransition",new[] { "Transition", "Transition",
                                    "Transición" } },
            { "sOwn",       new[] { "Own settings", "Réglages propres",
                                    "Ajustes propios" } },
            { "previewNone",new[] { "Start Dofus to see the preview", "Lance Dofus pour voir l'aperçu",
                                    "Abre Dofus para ver la vista previa" } },
            { "previewDay", new[] { "Without", "Sans",
                                    "Sin" } },
            { "previewNight",new[] { "With Pénombre", "Avec Pénombre",
                                    "Con Pénombre" } },
            { "statusOn",   new[] { "Filter on · {0} window(s)", "Filtre actif · {0} fenêtre(s)",
                                    "Filtro activo · {0} ventana(s)" } },
            { "statusOff",  new[] { "Filter off", "Filtre coupé",
                                    "Filtro desactivado" } },
            { "onlyFg",     new[] { "Only when Dofus is in the foreground", "Seulement quand Dofus est au premier plan",
                                    "Solo cuando Dofus está en primer plano" } },
            // Shortcuts
            { "hkToggle",   new[] { "Filter on / off", "Activer / couper le filtre",
                                    "Activar / desactivar el filtro" } },
            { "hkAuto",     new[] { "Automatic mode on / off", "Activer / couper le mode auto",
                                    "Activar / desactivar el modo auto" } },
            { "hkPreset",   new[] { "Next mood", "Ambiance suivante",
                                    "Ambiente siguiente" } },
            { "hkUp",       new[] { "Intensity +5 %", "Intensité +5 %",
                                    "Intensidad +5 %" } },
            { "hkDown",     new[] { "Intensity -5 %", "Intensité -5 %",
                                    "Intensidad -5 %" } },
            { "hkVignette", new[] { "Next vignette", "Vignettage suivant",
                                    "Viñeta siguiente" } },
            { "hkPeek",     new[] { "Peek (hold: game without filter)", "Coup d'œil (maintenir : jeu sans filtre)",
                                    "Vistazo (mantener: juego sin filtro)" } },
            { "hkNone",     new[] { "none", "aucun",
                                    "ninguno" } },
            { "hkBusy",     new[] { "unavailable", "indisponible",
                                    "no disponible" } },
            { "hkHint",     new[] { "Click a box and press the shortcut (at least Ctrl, Alt or Shift, or an F key). Backspace: none.",
                                    "Clique dans une case et appuie sur le raccourci (au moins Ctrl, Alt ou Maj, ou une touche F). Retour arrière : aucun.",
                                    "Haz clic en una casilla y pulsa el atajo (al menos Ctrl, Alt o Mayús, o una tecla F). Retroceso: ninguno." } },
            { "hkDefaults", new[] { "Default shortcuts", "Raccourcis par défaut",
                                    "Atajos por defecto" } },
            { "shift",      new[] { "Shift", "Maj",
                                    "Mayús" } },
            { "space",      new[] { "Space", "Espace",
                                    "Espacio" } },
            // General
            { "sWindows",   new[] { "Windows", "Windows",
                                    "Windows" } },
            { "startup",    new[] { "Start with Windows", "Lancer au démarrage de Windows",
                                    "Iniciar con Windows" } },
            { "portable",   new[] { "Portable mode (settings in a Penombre-data folder next to the exe)",
                                    "Mode portable (réglages dans un dossier Penombre-data à côté de l'exe)",
                                    "Modo portátil (ajustes en una carpeta Penombre-data junto al exe)" } },
            { "portableErr",new[] { "Cannot write next to the exe. Move Penombre.exe to a folder you own (Documents, Desktop…).",
                                    "Impossible d'écrire à côté de l'exe. Déplace Penombre.exe dans un dossier à toi (Documents, Bureau…).",
                                    "No se puede escribir junto al exe. Mueve Penombre.exe a una carpeta tuya (Documentos, Escritorio…)." } },
            { "sBackup",    new[] { "Settings file", "Fichier de réglages",
                                    "Archivo de ajustes" } },
            { "export",     new[] { "Export…", "Exporter…",
                                    "Exportar…" } },
            { "import",     new[] { "Import…", "Importer…",
                                    "Importar…" } },
            { "importDone", new[] { "Settings imported.", "Réglages importés.",
                                    "Ajustes importados." } },
            { "importBad",  new[] { "This file does not look like Pénombre settings.", "Ce fichier ne ressemble pas à des réglages de Pénombre.",
                                    "Este archivo no parece contener ajustes de Pénombre." } },
            { "resetAll",   new[] { "Reset settings…", "Réinitialiser les réglages…",
                                    "Restablecer los ajustes…" } },
            { "resetHint",  new[] { "Puts everything back as on the first start. The log, the settings folder and start with Windows stay as they are.",
                                    "Remet tout comme au premier lancement. Le journal, le dossier des réglages et le lancement au démarrage restent tels quels.",
                                    "Lo deja todo como en el primer inicio. El registro, la carpeta de ajustes y el inicio con Windows no cambian." } },
            { "resetAsk",   new[] { "Put all settings back to default?\n\nMood, schedule, shortcuts, bright zones and per-account settings will be lost. Use Export… first to keep a copy.",
                                    "Remettre tous les réglages par défaut ?\n\nAmbiance, horaires, raccourcis, zones claires et réglages par compte seront perdus. Utilise d'abord Exporter… pour en garder une copie.",
                                    "¿Volver a los ajustes por defecto?\n\nSe perderán el ambiente, los horarios, los atajos, las zonas claras y los ajustes por cuenta. Usa antes Exportar… para guardar una copia." } },
            { "resetDone",  new[] { "Settings reset.", "Réglages réinitialisés.",
                                    "Ajustes restablecidos." } },
            { "folder",     new[] { "Settings folder", "Dossier des réglages",
                                    "Carpeta de ajustes" } },
            { "openFolder", new[] { "Open", "Ouvrir",
                                    "Abrir" } },
            { "close",      new[] { "Close", "Fermer",
                                    "Cerrar" } },
        };

        public static readonly string[] Codes = { "en", "fr", "es" };
        public static readonly string[] Names = { "English", "Français", "Español" };
        public static event Action LanguageChanged;

        // lang: "en", "fr", "es", or null / "" for the Windows language.
        public static void Init(string lang)
        {
            if (string.IsNullOrEmpty(lang)) lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            column = lang == "fr" ? 1 : lang == "es" ? 2 : 0;
        }

        public static string Code { get { return Codes[column]; } }

        // Chosen in the settings: saved, and the menus and windows are rebuilt.
        public static void Choose(string lang)
        {
            Settings.Language = lang ?? "";
            Settings.Save();
            Init(lang);
            if (LanguageChanged != null) LanguageChanged();
        }

        public static string S(string key) { string[] t = texts[key]; return t[column < t.Length ? column : 0]; }
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

        public static string NameOf(int index) { return index >= 0 && index < All.Length ? All[index].Name : T.S("custom"); }

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

    // A mood with its intensity and vignette, as numbers that can be blended (smooth transitions).
    class Mood
    {
        public float B, S, K, Vig;
        public float[] Tint = new float[3], Lift = new float[3], Veil = new float[3];

        static readonly float[] VignetteLevels = { 0f, 0.43f, 0.67f, 0.9f };

        public static Mood From(AccountParams p)
        {
            Preset pr = Preset.Get(p.Preset);
            var m = new Mood { B = pr.Brightness, S = pr.Saturation, K = p.Intensity / 100f, Vig = VignetteLevels[Math.Max(0, Math.Min(3, p.Vignette))] };
            for (int i = 0; i < 3; i++) { m.Tint[i] = pr.Tint[i]; m.Lift[i] = pr.Lift[i]; }
            m.Veil[0] = pr.Veil.R; m.Veil[1] = pr.Veil.G; m.Veil[2] = pr.Veil.B;
            return m;
        }

        public static Mood Lerp(Mood a, Mood b, float t)
        {
            var m = new Mood { B = L(a.B, b.B, t), S = L(a.S, b.S, t), K = L(a.K, b.K, t), Vig = L(a.Vig, b.Vig, t) };
            for (int i = 0; i < 3; i++)
            {
                m.Tint[i] = L(a.Tint[i], b.Tint[i], t);
                m.Lift[i] = L(a.Lift[i], b.Lift[i], t);
                m.Veil[i] = L(a.Veil[i], b.Veil[i], t);
            }
            return m;
        }

        public bool Same(Mood o)
        {
            if (o == null) return false;
            if (!Near(B, o.B) || !Near(S, o.S) || !Near(K, o.K) || !Near(Vig, o.Vig)) return false;
            for (int i = 0; i < 3; i++)
                if (!Near(Tint[i], o.Tint[i]) || !Near(Lift[i], o.Lift[i]) || !Near(Veil[i], o.Veil[i])) return false;
            return true;
        }

        static float L(float a, float b, float t) { return a + (b - a) * t; }
        static bool Near(float a, float b) { return Math.Abs(a - b) < 0.0005f; }
    }

    // What one filter shows: mood x amount (fade, automatic mode, peek).
    class Look
    {
        public float[] Matrix;
        public Color VeilColor;
        public byte VeilAlpha;
        public byte VignetteOpacity;
        public byte GrainOpacity;

        static readonly float[] Luma = { 0.2126f, 0.7152f, 0.0722f };

        public static Look From(Mood mood, float amount)
        {
            float k = mood.K * amount;
            float b = Lerp(1, mood.B, k), s = Lerp(1, mood.S, k);

            // GDI+-style 5x5 color matrix, row vector: [R G B A 1] x M.
            var m = new float[25];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    m[i * 5 + j] = ((1 - s) * Luma[i] + (i == j ? s : 0)) * b * Lerp(1, mood.Tint[j], k);
            m[3 * 5 + 3] = 1;
            for (int j = 0; j < 3; j++) m[4 * 5 + j] = mood.Lift[j] * k;
            m[4 * 5 + 4] = 1;

            return new Look
            {
                Matrix = m,
                VeilColor = Color.FromArgb(ToByte(mood.Veil[0]), ToByte(mood.Veil[1]), ToByte(mood.Veil[2])),
                VeilAlpha = (byte)Math.Max(0, Math.Min(230, Math.Round(k * (1 - mood.B) * 255 * 0.9))),
                VignetteOpacity = ToByte(mood.Vig * amount * 255),
                GrainOpacity = ToByte(amount * 255),
            };
        }

        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
        static byte ToByte(float v) { return (byte)Math.Max(0, Math.Min(255, Math.Round(v))); }
    }

    // ================================================================ Sun (sunset / sunrise, offline)

    static class Sun
    {
        public class City
        {
            public readonly string Name; public readonly double Lat, Lon;
            public City(string name, double lat, double lon) { Name = name; Lat = lat; Lon = lon; }
        }

        public static readonly City[] Cities =
        {
            new City("Paris", 48.857, 2.352), new City("Lyon", 45.764, 4.836), new City("Marseille", 43.296, 5.370),
            new City("Toulouse", 43.605, 1.444), new City("Bordeaux", 44.838, -0.579), new City("Lille", 50.629, 3.057),
            new City("Nantes", 47.218, -1.554), new City("Strasbourg", 48.573, 7.752), new City("Nice", 43.710, 7.262),
            new City("Rennes", 48.117, -1.678), new City("Montpellier", 43.611, 3.877), new City("Brest", 48.390, -4.486),
            new City("Ajaccio", 41.919, 8.739), new City("Bruxelles", 50.850, 4.352), new City("Genève", 46.204, 6.143),
            new City("Luxembourg", 49.611, 6.130), new City("Montréal", 45.502, -73.567), new City("Québec", 46.813, -71.208),
            new City("Saint-Denis (La Réunion)", -20.882, 55.450), new City("Fort-de-France", 14.616, -61.059),
        };

        // Sunrise and sunset in minutes of local time (NOAA algorithm, ~1 min precision).
        // False when the sun does not rise or set that day (polar day / night).
        public static bool Compute(DateTime date, double lat, double lon, out double sunrise, out double sunset)
        {
            sunrise = sunset = 0;
            double r, s;
            if (!Event(date, lat, lon, true, out r) || !Event(date, lat, lon, false, out s)) return false;
            double offset = TimeZoneInfo.Local.GetUtcOffset(date.Date.AddHours(12)).TotalMinutes;
            sunrise = Mod(r * 60 + offset);
            sunset = Mod(s * 60 + offset);
            return true;
        }

        static bool Event(DateTime date, double lat, double lon, bool rising, out double utHours)
        {
            utHours = 0;
            const double zenith = 90.833;
            int n = date.DayOfYear;
            double lngHour = lon / 15;
            double t = n + ((rising ? 6 : 18) - lngHour) / 24;
            double m = 0.9856 * t - 3.289;
            double l = Norm(m + 1.916 * Sin(m) + 0.020 * Sin(2 * m) + 282.634, 360);
            double ra = Norm(Atan(0.91764 * Tan(l)), 360);
            ra += Math.Floor(l / 90) * 90 - Math.Floor(ra / 90) * 90;
            ra /= 15;
            double sinDec = 0.39782 * Sin(l);
            double cosDec = Math.Cos(Math.Asin(sinDec));
            double cosH = (Cos(zenith) - sinDec * Sin(lat)) / (cosDec * Cos(lat));
            if (cosH > 1 || cosH < -1) return false;
            double h = (rising ? 360 - Acos(cosH) : Acos(cosH)) / 15;
            double localT = h + ra - 0.06571 * t - 6.622;
            utHours = Norm(localT - lngHour, 24);
            return true;
        }

        const double Deg = Math.PI / 180;
        static double Sin(double d) { return Math.Sin(d * Deg); }
        static double Cos(double d) { return Math.Cos(d * Deg); }
        static double Tan(double d) { return Math.Tan(d * Deg); }
        static double Atan(double v) { return Math.Atan(v) / Deg; }
        static double Acos(double v) { return Math.Acos(v) / Deg; }
        static double Norm(double v, double max) { v %= max; return v < 0 ? v + max : v; }
        static double Mod(double minutes) { return Norm(minutes, 1440); }
    }

    // ================================================================ Real-time day / night cycle

    static class DayCycle
    {
        const int Twilight = 45; // minutes on each side of sunset / sunrise
        static DateTime cacheDate = DateTime.MinValue;
        static double cacheLat = double.NaN, cacheLon = double.NaN;
        static int[] cacheTimes;
        static bool cacheSunOk;

        // dusk start, full night, dawn start, full day (minutes of local time)
        public static int[] Times(DateTime now, out bool sunOk)
        {
            sunOk = false;
            int[] fixedTimes = { Settings.Dusk, Settings.Night, Settings.Dawn, Settings.Day };
            if (!Settings.SunMode) return fixedTimes;
            if (cacheDate != now.Date || cacheLat != Settings.SunLat || cacheLon != Settings.SunLon)
            {
                cacheDate = now.Date; cacheLat = Settings.SunLat; cacheLon = Settings.SunLon;
                double rise, set;
                cacheSunOk = Sun.Compute(now.Date, Settings.SunLat, Settings.SunLon, out rise, out set);
                cacheTimes = cacheSunOk
                    ? new[] { M(set - Twilight), M(set + Twilight), M(rise - Twilight), M(rise + Twilight) }
                    : fixedTimes;
            }
            sunOk = cacheSunOk;
            return cacheSunOk ? cacheTimes : fixedTimes;
        }

        // 0 = full day (no filter), 1 = full night.
        public static float Factor(DateTime now)
        {
            bool ok;
            int[] t = Times(now, out ok);
            float m = now.Hour * 60 + now.Minute + now.Second / 60f;
            float x;
            if (Within(m, t[0], t[1], out x)) return Smooth(x);
            if (Within(m, t[1], t[2], out x)) return 1;
            if (Within(m, t[2], t[3], out x)) return 1 - Smooth(x);
            return 0;
        }

        public static string Describe(DateTime now)
        {
            bool ok;
            int[] t = Times(now, out ok);
            float m = now.Hour * 60 + now.Minute + now.Second / 60f;
            float x;
            int pct = (int)Math.Round(Factor(now) * 100);
            if (Within(m, t[0], t[1], out x)) return T.S("stDusk") + " " + pct + " %";
            if (Within(m, t[1], t[2], out x)) return T.S("stNight");
            if (Within(m, t[2], t[3], out x)) return T.S("stDawn") + " " + pct + " %";
            return T.S("stDay");
        }

        static int M(double v) { return (int)Math.Round(((v % 1440) + 1440) % 1440) % 1440; }

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

    // ================================================================ Zones that stay bright

    // A zone drawn over a game area of size RefW x RefH (pixels). It can be resolved on another
    // game area size either proportionally, or anchored to its nearest edges.
    class Zone
    {
        public int Ax, Ay;          // 0 = left / top, 1 = center, 2 = right / bottom
        public float X, Y, W, H;    // pixels, relative to the top-left of the reference area
        public float RefW, RefH;

        static int R(float v) { return (int)Math.Round(v); }

        public Rectangle Resolve(Rectangle area)
        {
            if (!Settings.ZonesAnchored)
            {
                float sx = area.Width / RefW, sy = area.Height / RefH;
                float sw = Settings.ZonesScale ? sx : 1, sh = Settings.ZonesScale ? sy : 1;
                return new Rectangle(area.X + R(X * sx), area.Y + R(Y * sy), R(W * sw), R(H * sh));
            }
            float s = Settings.ZonesScale ? area.Height / RefH : 1f;
            float w = W * s, h = H * s;
            float x = Place(Ax, X, W, RefW, area.Width, s, w);
            float y = Place(Ay, Y, H, RefH, area.Height, s, h);
            return new Rectangle(area.X + R(x), area.Y + R(y), R(w), R(h));
        }

        static float Place(int anchor, float pos, float size, float refSize, float areaSize, float s, float newSize)
        {
            if (anchor == 0) return pos * s;
            if (anchor == 2) return areaSize - (refSize - pos - size) * s - newSize;
            return areaSize / 2f + (pos + size / 2f - refSize / 2f) * s - newSize / 2f;
        }

        // r: local rectangle inside an area of the given size.
        public static Zone FromLocal(Rectangle r, Size area)
        {
            return new Zone
            {
                X = r.X, Y = r.Y, W = r.Width, H = r.Height, RefW = area.Width, RefH = area.Height,
                Ax = Anchor(r.X + r.Width / 2f, area.Width), Ay = Anchor(r.Y + r.Height / 2f, area.Height),
            };
        }

        static int Anchor(float center, float size) { return center < size / 3f ? 0 : center > size * 2f / 3f ? 2 : 1; }

        public string Serialize()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:0.#},{3:0.#},{4:0.#},{5:0.#},{6:0},{7:0}", Ax, Ay, X, Y, W, H, RefW, RefH);
        }

        public static Zone Parse(string s)
        {
            string[] p = s.Split(',');
            if (p.Length != 8) return null;
            var z = new Zone();
            float[] f = new float[6];
            if (!int.TryParse(p[0], out z.Ax) || !int.TryParse(p[1], out z.Ay)) return null;
            for (int i = 0; i < 6; i++)
                if (!float.TryParse(p[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out f[i])) return null;
            z.X = f[0]; z.Y = f[1]; z.W = f[2]; z.H = f[3]; z.RefW = f[4]; z.RefH = f[5];
            if (z.W <= 0 || z.H <= 0 || z.RefW <= 0 || z.RefH <= 0) return null;
            return z;
        }
    }

    // ================================================================ Shortcuts

    static class Shortcuts
    {
        public const int Toggle = 0, Auto = 1, Preset = 2, Up = 3, Down = 4, Vignette = 5, Peek = 6, Count = 7;
        public static readonly string[] Keys_ = { "hkToggle", "hkAuto", "hkPreset", "hkUp", "hkDown", "hkVignette", "hkPeek" };
        public static readonly string[] SettingNames = { "toggle", "auto", "preset", "up", "down", "vignette", "peek" };

        public static Keys[] Defaults()
        {
            const Keys ca = Keys.Control | Keys.Alt;
            return new[] { ca | Keys.N, ca | Keys.A, ca | Keys.P, ca | Keys.PageUp, ca | Keys.PageDown, ca | Keys.V, ca | Keys.C };
        }

        public static string Format(Keys k)
        {
            Keys code = k & Keys.KeyCode;
            var parts = new List<string>();
            if ((k & Keys.Control) != 0) parts.Add("Ctrl");
            if ((k & Keys.Alt) != 0) parts.Add("Alt");
            if ((k & Keys.Shift) != 0) parts.Add(T.S("shift"));
            if (code == Keys.None) return parts.Count == 0 ? T.S("hkNone") : string.Join("+", parts.ToArray()) + "+…";
            parts.Add(KeyName(code));
            return string.Join("+", parts.ToArray());
        }

        static string KeyName(Keys code)
        {
            if (code >= Keys.D0 && code <= Keys.D9) return ((int)(code - Keys.D0)).ToString();
            if (code >= Keys.NumPad0 && code <= Keys.NumPad9) return "Num " + (int)(code - Keys.NumPad0);
            switch (code)
            {
                case Keys.PageUp: return "PgUp";
                case Keys.PageDown: return "PgDn";
                case Keys.Space: return T.S("space");
                case Keys.Return: return "Enter";
                case Keys.Escape: return "Esc";
                case Keys.Insert: return "Ins";
                case Keys.Delete: return "Del";
                case Keys.Oemcomma: return ",";
                case Keys.OemPeriod: return ".";
                case Keys.OemMinus: return "-";
                case Keys.Oemplus: return "=";
            }
            return code.ToString();
        }

        public static uint Mods(Keys k)
        {
            uint m = 0;
            if ((k & Keys.Alt) != 0) m |= 0x1;
            if ((k & Keys.Control) != 0) m |= 0x2;
            if ((k & Keys.Shift) != 0) m |= 0x4;
            return m;
        }

        public static bool IsValid(Keys k)
        {
            Keys code = k & Keys.KeyCode;
            if (code == Keys.None) return false;
            bool fkey = code >= Keys.F1 && code <= Keys.F24;
            return fkey || (k & (Keys.Control | Keys.Alt | Keys.Shift)) != 0;
        }
    }

    // ================================================================ Settings

    class AccountProfile
    {
        public bool Filter = true;      // false: this account is never filtered
        public bool Own;                // true: uses its own mood / intensity / vignette
        public int Preset, Intensity = 80, Vignette = 1;
    }

    // The settings that apply to one account (its own, or the default ones).
    class AccountParams
    {
        public int Preset, Intensity, Vignette;
        public bool Filter;
    }

    static class Settings
    {
        public const int MinIntensity = 5, MaxIntensity = 100;
        public const string PortableFolderName = "Penombre-data";

        public static bool Enabled = true;
        public static bool Shader = true;
        public static int PresetIndex = 0;
        public static int Intensity = 80;
        public static int Vignette = 1;
        public static Color VignetteColor = DefaultVignetteColor;
        public static int Grain = 0;
        public static string ProcessName = "Dofus";

        public static bool Auto = false;
        public static bool SunMode = false;
        public static string SunCity = "Paris";
        public static double SunLat = 48.857, SunLon = 2.352;
        public static int Dusk = 19 * 60 + 30, Night = 21 * 60 + 30, Dawn = 6 * 60 + 30, Day = 8 * 60 + 30;
        public static int FadeMs = 600;

        public static int CustomBrightness = 55, CustomSaturation = 55, CustomTemperature = -80, CustomLift = 30;

        public static bool ZonesEnabled = false, ZonesAnchored = true, ZonesScale = true;
        public static List<Zone> Zones = new List<Zone>();
        public static bool OnlyForeground = false;
        public static int TrayIcon = 0;             // 0 moon egg, 1 watcher egg, 2 round moon
        public static bool TrayIconAuto = true;     // the icon follows the sky of the automatic mode
        public static int TrayIconColor = 0;        // otherwise, the picture kept while the filter is on (TrayIcons.Night / Twilight / Day)
        public static bool CheckUpdates = true;     // asks GitHub once a day whether a newer version exists
        public static bool DuskTint = true;
        public static string Language = "";         // "" = the Windows language, else "en", "fr" or "es"         // automatic mode: the Dusk colours at sunset and sunrise

        public static Dictionary<string, AccountProfile> Accounts = new Dictionary<string, AccountProfile>();
        public static Keys[] Hotkeys = Shortcuts.Defaults();

        public static Color DefaultVignetteColor { get { return Color.FromArgb(0, 2, 12); } }

        static Timer saveTimer;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static string folder;
        static string[] defaults; // the settings of a first start, taken before loading the file

        // ---- where the settings live

        static string AppDataFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Penombre"); }
        }

        static string PortableFolder
        {
            get { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), PortableFolderName); }
        }

        // Where the settings live when not portable: %APPDATA%, or the --data folder.
        static string DefaultFolder { get { return Program.DataOverride ?? AppDataFolder; } }

        public static void Init()
        {
            if (defaults == null) defaults = Lines();
            folder = Program.DataOverride != null ? Program.DataOverride
                   : Directory.Exists(PortableFolder) ? PortableFolder : AppDataFolder;
        }

        public static string Folder { get { if (folder == null) Init(); return folder; } }
        public static bool IsPortable { get { return string.Equals(Folder, PortableFolder, StringComparison.OrdinalIgnoreCase); } }
        public static string FilePath { get { return Path.Combine(Folder, "settings.ini"); } }

        // Moves the settings (and the log) next to the exe, or back to %APPDATA%.
        public static bool SetPortable(bool on)
        {
            if (on == IsPortable) return true;
            string from = Folder, to = on ? PortableFolder : DefaultFolder;
            try
            {
                Directory.CreateDirectory(to);
                string probe = Path.Combine(to, ".write-test");
                File.WriteAllText(probe, "");
                File.Delete(probe);
            }
            catch (Exception ex) { Log.Error("Portable mode: cannot write to " + to, ex); return false; }

            folder = to;
            Save();
            Log.Info("Settings moved to " + to);
            if (!on)
            {
                // The portable folder only holds Pénombre's own files: remove it so it is not picked up again.
                try { Directory.Delete(from, true); }
                catch (Exception ex) { Log.Error("Portable mode: cannot remove " + from, ex); }
            }
            return true;
        }

        // ---- per-account settings

        public static AccountProfile Profile(string account, bool create)
        {
            AccountProfile p;
            if (account != null && Accounts.TryGetValue(account, out p)) return p;
            if (!create || account == null) return null;
            p = new AccountProfile { Preset = PresetIndex, Intensity = Intensity, Vignette = Vignette };
            Accounts[account] = p;
            return p;
        }

        public static AccountParams ParamsFor(string account)
        {
            AccountProfile p = Profile(account, false);
            if (p != null && p.Own)
                return new AccountParams { Preset = p.Preset, Intensity = p.Intensity, Vignette = p.Vignette, Filter = p.Filter };
            return new AccountParams { Preset = PresetIndex, Intensity = Intensity, Vignette = Vignette, Filter = p == null || p.Filter };
        }

        // ---- load / save

        public static void Load() { LoadFrom(FilePath); }

        // Returns false when the file is not a Pénombre settings file.
        public static bool LoadFrom(string path)
        {
            try
            {
                return File.Exists(path) && Parse(File.ReadAllLines(path, Encoding.UTF8));
            }
            catch (Exception ex) { Log.Error("Settings load from " + path, ex); return false; }
        }

        // Back to the settings of a first start (the folder, portable or not, stays the same).
        public static void ResetToDefaults()
        {
            Parse(defaults);
            Save();
            Log.Info("Settings reset to defaults");
        }

        static bool Parse(string[] lines)
        {
            bool recognized = false;
            foreach (string line in lines)
                if (line.StartsWith("enabled=") || line.StartsWith("preset=") || line.StartsWith("render=")) recognized = true;
            if (!recognized) return false;

            Accounts = new Dictionary<string, AccountProfile>();
            Zones = new List<Zone>();
            Hotkeys = Shortcuts.Defaults();
            foreach (string line in lines)
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                int n;
                double d;
                if (k.StartsWith("account."))
                {
                    ParseAccount(k.Substring(8), v);
                    continue;
                }
                if (k.StartsWith("hotkey."))
                {
                    int idx = Array.IndexOf(Shortcuts.SettingNames, k.Substring(7));
                    if (idx >= 0 && int.TryParse(v, out n)) Hotkeys[idx] = (Keys)n;
                    continue;
                }
                switch (k)
                {
                    case "enabled": Enabled = v != "0"; break;
                    case "render": Shader = v != "veil"; break;
                    case "preset": if (int.TryParse(v, out n) && n >= 0 && n < Preset.Count) PresetIndex = n; break;
                    case "intensity": if (int.TryParse(v, out n)) Intensity = ClampIntensity(n); break;
                    case "vignette": if (int.TryParse(v, out n) && n >= 0 && n <= 3) Vignette = n; break;
                    case "vignetteColor": VignetteColor = ParseColor(v, DefaultVignetteColor); break;
                    case "grain": if (int.TryParse(v, out n) && n >= 0 && n <= 2) Grain = n; break;
                    case "process": if (v.Length > 0) ProcessName = v; break;
                    case "auto": Auto = v == "1"; break;
                    case "sun": SunMode = v == "1"; break;
                    case "sunCity": SunCity = v; break;
                    case "sunLat": if (double.TryParse(v, NumberStyles.Float, Inv, out d) && d >= -90 && d <= 90) SunLat = d; break;
                    case "sunLon": if (double.TryParse(v, NumberStyles.Float, Inv, out d) && d >= -180 && d <= 180) SunLon = d; break;
                    case "dusk": ParseTime(v, ref Dusk); break;
                    case "night": ParseTime(v, ref Night); break;
                    case "dawn": ParseTime(v, ref Dawn); break;
                    case "day": ParseTime(v, ref Day); break;
                    case "fade": if (int.TryParse(v, out n)) FadeMs = Math.Max(0, Math.Min(3000, n)); break;
                    case "custom": ParseCustom(v); break;
                    case "zonesEnabled": ZonesEnabled = v == "1"; break;
                    case "zonesAnchored": ZonesAnchored = v == "1"; break;
                    case "zonesScale": ZonesScale = v == "1"; break;
                    case "zones2":
                        foreach (string z in v.Split(';')) { Zone zone = Zone.Parse(z); if (zone != null) Zones.Add(zone); }
                        break;
                    case "onlyForeground": OnlyForeground = v == "1"; break;
                    case "trayIcon": if (int.TryParse(v, out n)) TrayIcon = Math.Max(0, Math.Min(2, n)); break;
                    case "trayIconAuto": TrayIconAuto = v != "0"; break;
                    case "trayIconColor": if (int.TryParse(v, out n)) TrayIconColor = Math.Max(0, Math.Min(2, n)); break;
                    case "checkUpdates": CheckUpdates = v != "0"; break;
                    case "duskTint": DuskTint = v != "0"; break;
                    case "lang": Language = Array.IndexOf(T.Codes, v) >= 0 ? v : ""; break;
                }
            }
            return true;
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
                SaveTo(FilePath);
            }
            catch (Exception ex) { Log.Error("Settings save", ex); }
        }

        public static void SaveTo(string path)
        {
            File.WriteAllLines(path, Lines(), new UTF8Encoding(false));
        }

        static string[] Lines()
        {
            var zones = new List<string>();
            foreach (Zone z in Zones) zones.Add(z.Serialize());
            var lines = new List<string>
            {
                "# Pénombre " + Program.Version + " settings",
                "enabled=" + (Enabled ? "1" : "0"),
                "render=" + (Shader ? "shader" : "veil"),
                "preset=" + PresetIndex,
                "intensity=" + Intensity,
                "vignette=" + Vignette,
                "vignetteColor=" + ColorToString(VignetteColor),
                "grain=" + Grain,
                "process=" + ProcessName,
                "auto=" + (Auto ? "1" : "0"),
                "sun=" + (SunMode ? "1" : "0"),
                "sunCity=" + SunCity,
                "sunLat=" + SunLat.ToString("0.####", Inv),
                "sunLon=" + SunLon.ToString("0.####", Inv),
                "dusk=" + FormatTime(Dusk),
                "night=" + FormatTime(Night),
                "dawn=" + FormatTime(Dawn),
                "day=" + FormatTime(Day),
                "fade=" + FadeMs,
                "custom=" + CustomBrightness + "," + CustomSaturation + "," + CustomTemperature + "," + CustomLift,
                "zonesEnabled=" + (ZonesEnabled ? "1" : "0"),
                "zonesAnchored=" + (ZonesAnchored ? "1" : "0"),
                "zonesScale=" + (ZonesScale ? "1" : "0"),
                "zones2=" + string.Join(";", zones.ToArray()),
                "onlyForeground=" + (OnlyForeground ? "1" : "0"),
                "trayIcon=" + TrayIcon,
                "trayIconAuto=" + (TrayIconAuto ? "1" : "0"),
                "trayIconColor=" + TrayIconColor,
                "checkUpdates=" + (CheckUpdates ? "1" : "0"),
                "duskTint=" + (DuskTint ? "1" : "0"),
                "lang=" + Language,
            };
            for (int i = 0; i < Shortcuts.Count; i++) lines.Add("hotkey." + Shortcuts.SettingNames[i] + "=" + (int)Hotkeys[i]);
            foreach (var kv in Accounts)
            {
                AccountProfile p = kv.Value;
                lines.Add("account." + kv.Key.Replace("=", "") + "=" + (p.Filter ? 1 : 0) + "," + (p.Own ? 1 : 0) + ","
                          + p.Preset + "," + p.Intensity + "," + p.Vignette);
            }
            return lines.ToArray();
        }

        public static int ClampIntensity(int v) { return Math.Max(MinIntensity, Math.Min(MaxIntensity, v)); }

        public static string FormatTime(int minutes) { return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00"); }

        static string ColorToString(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }

        static Color ParseColor(string v, Color fallback)
        {
            int rgb;
            if (v.Length == 7 && v[0] == '#' && int.TryParse(v.Substring(1), NumberStyles.HexNumber, Inv, out rgb))
                return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
            return fallback;
        }

        static void ParseAccount(string name, string v)
        {
            string[] p = v.Split(',');
            int f, o, pr, it, vg;
            if (name.Length == 0 || p.Length != 5) return;
            if (!int.TryParse(p[0], out f) || !int.TryParse(p[1], out o) || !int.TryParse(p[2], out pr)
                || !int.TryParse(p[3], out it) || !int.TryParse(p[4], out vg)) return;
            Accounts[name] = new AccountProfile
            {
                Filter = f != 0, Own = o != 0,
                Preset = pr >= 0 && pr < Preset.Count ? pr : 0,
                Intensity = ClampIntensity(it),
                Vignette = Math.Max(0, Math.Min(3, vg)),
            };
        }

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

    }

    // ================================================================ Updates

    // Asks GitHub for the latest release (the only time Pénombre goes online). Nothing is
    // downloaded: a newer version is only announced, with a link to its page.
    static class Updates
    {
        const string Api = "https://api.github.com/repos/Sokenzane/Penombre/releases/latest";
        public const string Page = "https://github.com/Sokenzane/Penombre/releases/latest";
        public enum State { None, Checking, UpToDate, Newer, Failed }

        public static State Status = State.None;
        public static string Latest;          // version of the latest release, once known
        public static event Action Changed;   // raised on the UI thread

        public static void Check()
        {
            if (Status == State.Checking) return;
            Status = State.Checking;
            Raise();
            var ui = System.Threading.SynchronizationContext.Current;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                string tag = null;
                try
                {
                    ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
                    var req = (HttpWebRequest)WebRequest.Create(Api);
                    req.UserAgent = "Penombre/" + Program.Version;
                    req.Accept = "application/vnd.github+json";
                    req.Timeout = req.ReadWriteTimeout = 10000;
                    using (WebResponse resp = req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        tag = ParseTag(reader.ReadToEnd());
                }
                catch (Exception ex) { Log.Info("Update check failed: " + ex.Message); }
                ui.Post(__ =>
                {
                    Latest = tag;
                    Status = tag == null ? State.Failed : IsNewer(tag, Program.Version) ? State.Newer : State.UpToDate;
                    if (Status == State.Newer) Log.Info("Newer version available: " + tag);
                    Raise();
                }, null);
            });
        }

        static void Raise() { if (Changed != null) Changed(); }

        // "tag_name": "v0.5.2" -> "0.5.2"
        public static string ParseTag(string json)
        {
            Match m = Regex.Match(json ?? "", "\"tag_name\"\\s*:\\s*\"v?([0-9][0-9.]*)");
            return m.Success ? m.Groups[1].Value.TrimEnd('.') : null;
        }

        public static bool IsNewer(string latest, string current)
        {
            Version a, b;
            return Version.TryParse(latest, out a) && Version.TryParse(current, out b) && a > b;
        }

        public static void OpenPage()
        {
            try { Process.Start(Page); }
            catch (Exception ex) { Log.Error("Open the release page", ex); }
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

        // Also re-shows the window if something else hid it behind our back.
        public void ShowNa()
        {
            if (IsShown && Native.IsWindowVisible(Handle) && !Native.IsIconic(Handle)) return;
            Native.ShowWindow(Handle, Native.IsIconic(Handle) ? 4 /* SW_SHOWNOACTIVATE: restore */ : 8 /* SW_SHOWNA */);
            IsShown = true;
        }
        public void HideNow() { if (IsShown) { Native.ShowWindow(Handle, 0 /* SW_HIDE */); IsShown = false; } }
    }

    // Per-pixel alpha vignette (dark edges, clear center). Drawn once at full strength; the
    // strength, the fade and the automatic mode only change its global opacity.
    class VignetteWindow : ClickThroughWindow
    {
        Size drawnSize = Size.Empty;
        Color drawnColor = Color.Empty;
        byte opacity;
        Point placedAt = new Point(int.MinValue, int.MinValue);

        public VignetteWindow() { CreateHandle(); }

        public void Place(Rectangle r, Color color, byte globalOpacity)
        {
            opacity = globalOpacity;
            if (globalOpacity == 0) { HideNow(); return; }
            if (r.Size != drawnSize || color != drawnColor) Draw(r, color);
            else
            {
                if (r.Location != placedAt)
                    Native.SetWindowPos(Handle, IntPtr.Zero, r.X, r.Y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
                Native.PushOpacity(Handle, opacity);
            }
            placedAt = r.Location;
            ShowNa();
        }

        public void SetOpacity(byte value)
        {
            if (value == opacity) return;
            opacity = value;
            if (value == 0) HideNow();
            else if (IsShown) Native.PushOpacity(Handle, value);
        }

        void Draw(Rectangle r, Color color)
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
                        brush.CenterColor = Color.FromArgb(0, color);
                        brush.SurroundColors = new[] { Color.FromArgb(255, color) };
                        brush.Blend = new Blend
                        {
                            Positions = new[] { 0f, 0.25f, 0.6f, 1f },
                            Factors = new[] { 0f, 0.45f, 1f, 1f },
                        };
                        g.FillRectangle(brush, 0, 0, r.Width, r.Height);
                    }
                }
                IntPtr hbmp = bmp.GetHbitmap(Color.FromArgb(0));
                Native.PushBitmap(Handle, hbmp, r, opacity);
                Native.DeleteObject(hbmp);
            }
            drawnSize = r.Size;
            drawnColor = color;
        }
    }

    // Animated film grain: a few frames of noise generated once, cycled at ~12 frames per second.
    class GrainWindow : ClickThroughWindow
    {
        static readonly int[] Levels = { 0, 16, 30 }; // max alpha of a grain dot
        IntPtr[] frames;
        Size builtSize = Size.Empty;
        int builtLevel = -1, index;
        byte opacity;
        Rectangle placed = Rectangle.Empty;

        public GrainWindow() { CreateHandle(); }

        public void Place(Rectangle r, int level, byte globalOpacity)
        {
            opacity = globalOpacity;
            if (level <= 0 || globalOpacity == 0) { HideNow(); if (level <= 0) Free(); return; }
            if (r.Size != builtSize || level != builtLevel) Build(r.Size, level);
            if (r != placed || !IsShown) { placed = r; Push(); }
            ShowNa();
        }

        public void SetOpacity(byte value)
        {
            if (value == opacity) return;
            opacity = value;
            if (value == 0) HideNow();
            else if (IsShown) Native.PushOpacity(Handle, value);
        }

        public void NextFrame()
        {
            if (!IsShown || frames == null) return;
            index = (index + 1) % frames.Length;
            Push();
        }

        public int FramesPushed;
        public long LastBuildMs;

        void Push() { if (frames != null) { Native.PushBitmap(Handle, frames[index], placed, opacity); FramesPushed++; } }

        void Build(Size size, int level)
        {
            var watch = Stopwatch.StartNew();
            Free();
            var rnd = new Random();
            int max = Levels[Math.Min(level, Levels.Length - 1)];
            frames = new IntPtr[3];
            var pixels = new int[size.Width * size.Height];
            for (int f = 0; f < frames.Length; f++)
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    int a = rnd.Next(max + 1);
                    int c = (i ^ rnd.Next()) % 2 == 0 ? 0 : 255;  // dark or light speck
                    pixels[i] = (a << 24) | (c << 16) | (c << 8) | c;
                }
                using (var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb))
                {
                    BitmapData data = bmp.LockBits(new Rectangle(Point.Empty, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                    bmp.UnlockBits(data);
                    frames[f] = bmp.GetHbitmap(Color.FromArgb(0));
                }
            }
            builtSize = size;
            builtLevel = level;
            placed = Rectangle.Empty;
            LastBuildMs = watch.ElapsedMilliseconds;
        }

        void Free()
        {
            if (frames != null) foreach (IntPtr h in frames) Native.DeleteObject(h);
            frames = null;
            builtSize = Size.Empty;
            builtLevel = -1;
        }

        protected override void Dispose(bool disposing)
        {
            Free();
            base.Dispose(disposing);
        }
    }

    // The filter over one game window: a magnifier "shader" host, or a colored veil.
    class Overlay : ClickThroughWindow
    {
        public readonly IntPtr Target;
        public readonly IntPtr MagWindow;
        public readonly VignetteWindow Vignette;
        public readonly GrainWindow Grain;
        public Rectangle Placed = Rectangle.Empty;
        public string RegionKey;
        public string RegionInfo = "-";
        public string Account = "";

        // Smooth transitions between moods.
        public Mood Current, From, Goal;
        public long TransitionStart;
        public Look LastLook;

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
            Vignette.Owner = this; // owned windows: always drawn above the filter
            Grain = new GrainWindow();
            Grain.Owner = this;
        }

        public void ApplyLook(Look look)
        {
            LastLook = look;
            if (UsesShader) Mag.SetEffect(MagWindow, look.Matrix);
            else
            {
                if (BackColor != look.VeilColor) { BackColor = look.VeilColor; Invalidate(); }
                Native.SetLayeredWindowAttributes(Handle, 0, look.VeilAlpha, Native.LWA_ALPHA);
            }
            Vignette.SetOpacity(IsShown ? look.VignetteOpacity : (byte)0);
            Grain.SetOpacity(IsShown ? look.GrainOpacity : (byte)0);
        }

        // r: game area on screen. region: visible part, in local coordinates (null = all of it).
        public void Place(Rectangle r, Region region, string key, Graphics g)
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

            if (LastLook != null)
            {
                Vignette.Place(r, Settings.VignetteColor, LastLook.VignetteOpacity);
                Grain.Place(r, Settings.Grain, LastLook.GrainOpacity);
            }

            if (key != RegionKey)
            {
                // The system owns the HRGN given to SetWindowRgn: one per window.
                Native.SetWindowRgn(Handle, region == null ? IntPtr.Zero : region.GetHrgn(g), true);
                Native.SetWindowRgn(Vignette.Handle, region == null ? IntPtr.Zero : region.GetHrgn(g), true);
                Native.SetWindowRgn(Grain.Handle, region == null ? IntPtr.Zero : region.GetHrgn(g), true);
                RegionKey = key;
            }

            // Paint now: posted render messages would otherwise keep WM_PAINT waiting,
            // and a filter just shown or cut would stay invisible.
            Native.UpdateWindow(Handle);
            if (UsesShader) Native.UpdateWindow(MagWindow);
        }

        public void HideAll()
        {
            HideNow();
            Vignette.HideNow();
            Grain.HideNow();
        }

        // Shader: redraw the game area through the color matrix (one frame).
        public int Frames;

        public void RenderFrame()
        {
            if (UsesShader && IsShown) { Mag.SetSource(MagWindow, Placed); Native.UpdateWindow(MagWindow); Frames++; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { Vignette.Dispose(); Grain.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // ================================================================ Global shortcuts

    // Receives the global shortcuts, and the request to open the settings sent when Pénombre is started again.
    class HotkeyWindow : NativeWindow, IDisposable
    {
        public const int WM_SHOW_SETTINGS = 0x8000 + 0x50; // WM_APP + 0x50
        public static string Caption { get { return Program.InstanceName + ".Messages"; } }
        readonly Action<int> callback;
        readonly Action showSettings;
        readonly List<int> ids = new List<int>();

        public HotkeyWindow(Action<int> callback, Action showSettings)
        {
            this.callback = callback;
            this.showSettings = showSettings;
            var cp = new CreateParams();
            cp.Caption = Caption;
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE
            CreateHandle(cp);
        }

        public bool Register(int id, Keys keys)
        {
            if (!Shortcuts.IsValid(keys)) return true; // "none" is not a failure
            bool ok = Native.RegisterHotKey(Handle, id, Shortcuts.Mods(keys) | 0x4000 /* MOD_NOREPEAT */, (uint)(keys & Keys.KeyCode));
            if (ok) ids.Add(id);
            return ok;
        }

        public void UnregisterAll()
        {
            foreach (int id in ids) Native.UnregisterHotKey(Handle, id);
            ids.Clear();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 /* WM_HOTKEY */) callback(m.WParam.ToInt32());
            else if (m.Msg == WM_SHOW_SETTINGS) showSettings();
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            UnregisterAll();
            DestroyHandle();
        }
    }

    // ================================================================ Shader render pump

    // Drives the shader frames at the screen rate. WinForms timers tick every ~31 ms when the work
    // takes one screen refresh (their resolution is 15.6 ms), which halves the frame rate. Here a
    // background thread asks the UI thread for the next frame each time the UI thread becomes
    // idle: input, timers and painting always go first, then one frame is drawn (it waits for
    // the screen refresh), and so on.
    class RenderPump : NativeWindow, IDisposable
    {
        const int WM_RENDER = 0x8000 + 1;   // WM_APP + 1
        const uint QS_INPUT = 0x0407;
        readonly Action render;
        readonly System.Threading.Thread thread;
        readonly System.Threading.AutoResetEvent idle = new System.Threading.AutoResetEvent(true);
        readonly EventHandler onIdle;
        volatile bool running = true;
        public volatile bool Active;

        public RenderPump(Action render)
        {
            this.render = render;
            var cp = new CreateParams();
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE
            CreateHandle(cp);
            onIdle = (s, e) => idle.Set();
            Application.Idle += onIdle;
            IntPtr h = Handle;
            thread = new System.Threading.Thread(() =>
            {
                while (running)
                {
                    if (!Active) { System.Threading.Thread.Sleep(50); continue; }
                    if (!idle.WaitOne(100) || !running || !Active) continue;
                    if (!Native.PostMessage(h, WM_RENDER, IntPtr.Zero, IntPtr.Zero)) System.Threading.Thread.Sleep(10);
                }
            });
            thread.IsBackground = true;
            thread.Name = "Penombre render pump";
            thread.Start();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_RENDER)
            {
                try
                {
                    // Input waiting: skip this frame; the next one is asked for once idle again.
                    if ((Native.GetQueueStatus(QS_INPUT) >> 16) == 0) render();
                }
                catch (Exception ex) { Log.Error("Render", ex); }
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            Application.Idle -= onIdle;
            running = false;
            Active = false;
            idle.Set();
            thread.Join(500);
            DestroyHandle();
        }
    }

    // ================================================================ Tracking the game windows

    class OverlayManager : IDisposable
    {
        const int FastInterval = 15;    // ~60 Hz while a shader is on screen or something is animating
        const int IdleInterval = 100;   // otherwise
        const int ScanEveryMs = 1000, SafetyLayoutMs = 500, AutoEveryMs = 5000, GrainEveryMs = 83, PeekFadeMs = 120;

        readonly Dictionary<IntPtr, Overlay> overlays = new Dictionary<IntPtr, Overlay>();
        readonly HashSet<IntPtr> ownWindows = new HashSet<IntPtr>();
        readonly List<IntPtr> hooks = new List<IntPtr>();
        readonly Native.WinEventProc eventProc; // kept in a field so the GC never collects it
        readonly Timer timer;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Bitmap scratch = new Bitmap(1, 1);
        readonly Graphics g;

        float shown;          // what is on screen now (0..1), moves towards the target during a fade
        float autoFactor = 1; // automatic mode (0 = day, 1 = night)
        public bool Paused;   // "pause for 15 min…" from the menu: the filter fades out, settings unchanged
        bool layoutDirty = true, forceLook = true, quickFade, peeking;
        int peekKey;
        long peekStart;
        long lastTick, lastScan = -ScanEveryMs, lastLayout, lastAuto = -AutoEveryMs, lastGrain, lastErrorLog = -10000;
        int frame;
        IntPtr lastGameForeground = IntPtr.Zero;
        readonly RenderPump pump;

        public event EventHandler CountChanged;
        public int Count { get { return overlays.Count; } }
        public bool ShaderFailed { get; private set; }
        public float AutoFactor { get { return autoFactor; } }
        public float Shown { get { return shown; } }
        public bool Peeking { get { return peeking; } }
        public IEnumerable<Overlay> All { get { return overlays.Values; } }
        public double MaxTickMs, TotalTickMs, RenderMs;
        public int TickCount, RenderCount;

        public OverlayManager()
        {
            g = Graphics.FromImage(scratch);

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
            pump = new RenderPump(RenderFrames);
        }

        // One shader frame: every time for the game in the foreground, one time in four for the
        // others. Each frame waits for the screen refresh, so this runs at the screen rate.
        void RenderFrames()
        {
            frame++;
            IntPtr fg = Native.GetForegroundWindow();
            foreach (var o in overlays.Values)
            {
                if (!o.IsShown || !o.UsesShader) continue;
                if (o.Target != fg && frame % 4 != 0) continue;
                var watch = Stopwatch.StartNew();
                o.RenderFrame();
                RenderMs += watch.Elapsed.TotalMilliseconds;
                RenderCount++;
            }
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

        // "Peek": while the shortcut is held, the game is shown without filter.
        public void StartPeek(Keys keys)
        {
            peeking = true;
            peekStart = clock.ElapsedMilliseconds;
            peekKey = (int)(keys & Keys.KeyCode);
            quickFade = true;
            timer.Interval = FastInterval;
        }

        void OnTick()
        {
            var watch = Stopwatch.StartNew();
            OnTickCore();
            double ms = watch.Elapsed.TotalMilliseconds;
            TickCount++;
            TotalTickMs += ms;
            if (ms > MaxTickMs) MaxTickMs = ms;
        }

        void OnTickCore()
        {
            long now = clock.ElapsedMilliseconds;
            float dt = Math.Min(0.2f, (now - lastTick) / 1000f);
            lastTick = now;
            try
            {
                if (now - lastScan >= ScanEveryMs) { lastScan = now; Scan(); }
                if (now - lastAuto >= AutoEveryMs) { lastAuto = now; UpdateAutoFactor(); }

                // Peek ends when the key is released (or after a minute, in case a key stays stuck).
                if (peeking && ((Native.GetAsyncKeyState(peekKey) & 0x8000) == 0 || now - peekStart > 60000)) { peeking = false; quickFade = true; }

                // Fade towards the target (on / off, automatic mode, peek).
                float target = Settings.Enabled && !peeking && !Paused ? autoFactor : 0;
                bool fading = shown != target;
                if (fading)
                {
                    bool wasOff = shown <= 0;
                    int ms = quickFade ? PeekFadeMs : Settings.FadeMs;
                    float step = ms <= 0 ? 1 : dt * 1000f / ms;
                    shown = target > shown ? Math.Min(target, shown + step) : Math.Max(target, shown - step);
                    if (wasOff != (shown <= 0)) layoutDirty = true; // filters appear or disappear
                }
                else quickFade = false;

                bool morphing = UpdateLooks(now, fading || forceLook);
                forceLook = false;

                if (layoutDirty || now - lastLayout >= SafetyLayoutMs)
                {
                    layoutDirty = false;
                    lastLayout = now;
                    Layout();
                }

                // Shader frames are drawn by the render pump (see RenderFrames).
                bool rendering = false, grain = false;
                foreach (var o in overlays.Values)
                {
                    if (o.Grain.IsShown) grain = true;
                    if (o.IsShown && o.UsesShader) rendering = true;
                }
                if (grain && now - lastGrain >= GrainEveryMs)
                {
                    lastGrain = now;
                    foreach (var o in overlays.Values) o.Grain.NextFrame();
                }

                int interval = rendering || fading || morphing || grain || peeking ? FastInterval : IdleInterval;
                if (timer.Interval != interval) timer.Interval = interval;
                pump.Active = rendering;
            }
            catch (Exception ex)
            {
                if (now - lastErrorLog > 10000) { lastErrorLog = now; Log.Error("Tick", ex); }
            }
        }

        // Moves every filter towards the mood of its account. Returns true while a transition runs.
        bool UpdateLooks(long now, bool force)
        {
            bool morphing = false;
            foreach (var o in overlays.Values)
            {
                AccountParams p = Settings.ParamsFor(o.Account);
                Mood goal = Mood.From(p);
                float dusk = DuskWeight();
                if (dusk > 0)
                {
                    // Same intensity and vignette, the colours of the Dusk mood (strongest halfway).
                    AccountParams warm = new AccountParams { Preset = DuskPreset, Intensity = p.Intensity, Vignette = p.Vignette, Filter = p.Filter };
                    goal = Mood.Lerp(goal, Mood.From(warm), dusk);
                }
                bool changed = force;
                if (o.Current == null) { o.Current = o.From = o.Goal = goal; changed = true; }
                else if (!goal.Same(o.Goal)) { o.From = o.Current; o.Goal = goal; o.TransitionStart = now; }

                if (o.Current != o.Goal)
                {
                    float t = Settings.FadeMs <= 0 ? 1 : (now - o.TransitionStart) / (float)Settings.FadeMs;
                    if (t >= 1) o.Current = o.Goal;
                    else { o.Current = Mood.Lerp(o.From, o.Goal, t * t * (3 - 2 * t)); morphing = true; }
                    changed = true;
                }
                if (changed) o.ApplyLook(Look.From(o.Current, shown));
            }
            return morphing;
        }

        const int DuskPreset = 2; // the Dusk mood

        // 0 by day and by night, up to 1 halfway through dusk and dawn (automatic mode only).
        float DuskWeight()
        {
            if (!Settings.Auto || !Settings.DuskTint || autoFactor <= 0 || autoFactor >= 1) return 0;
            return 4 * autoFactor * (1 - autoFactor);
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
                o.Account = AccountOf(h);
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
                forceLook = true;
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
            foreach (var o in overlays.Values) { ownWindows.Add(o.Handle); ownWindows.Add(o.Vignette.Handle); ownWindows.Add(o.Grain.Handle); }

            // A shader must never capture a filter, a vignette or a grain layer (its own included),
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
            IntPtr fg = Native.GetForegroundWindow();

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
                        LayoutOne(o, covered, fg);
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

        void LayoutOne(Overlay o, Region covered, IntPtr fg)
        {
            IntPtr t = o.Target;
            string account = AccountOf(t);
            if (account != o.Account) { o.Account = account; forceLook = true; }

            Rectangle area;
            if (!Settings.ParamsFor(o.Account).Filter) { o.HideAll(); o.RegionInfo = "account off"; return; }
            if (Settings.OnlyForeground && fg != t) { o.HideAll(); o.RegionInfo = "not in foreground"; return; }
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
                    foreach (Zone z in Settings.Zones) visible.Exclude(z.Resolve(area));

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
                bool wasShown = o.IsShown;
                o.Place(area, full ? null : visible, key, g);
                if (!wasShown && o.LastLook != null) o.ApplyLook(o.LastLook); // vignette / grain opacity
            }
        }

        // Shell popups drawn with per-pixel transparency: their window spans far more than what
        // they show (the taskbar previews cover the whole width of the screen), so they never cut the filter.
        static readonly HashSet<string> ShellPopups = new HashSet<string>
        {
            "TaskListThumbnailWnd", "TaskListOverlayWnd",          // taskbar previews (Windows 10)
            "XamlExplorerHostIslandWindow",                         // taskbar previews, Alt+Tab (Windows 11)
            "MultitaskingViewFrame", "ForegroundStaging"            // Alt+Tab, task switching
        };

        // A window that hides what is below it. Click-through, fully transparent, cloaked
        // and very thin windows (GPU / Discord / Steam overlays, tab handles...) do not count.
        public static bool IsOccluder(IntPtr h, out Rectangle r)
        {
            r = Rectangle.Empty;
            if (!Native.IsWindowVisible(h) || Native.IsIconic(h)) return false;
            if (ShellPopups.Contains(Native.ClassOf(h))) return false;
            int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            if ((ex & Native.WS_EX_TRANSPARENT) != 0) return false;
            if ((ex & Native.WS_EX_LAYERED) != 0)
            {
                uint key, flags; byte alpha;
                if (Native.GetLayeredWindowAttributes(h, out key, out alpha, out flags) && (flags & Native.LWA_ALPHA) != 0 && alpha == 0)
                    return false;
            }
            if (Native.IsCloaked(h)) return false;
            // thin strips (window tab handles, edge decorations) hide almost nothing
            return Native.TryGetBounds(h, out r) && r.Width >= 12 && r.Height >= 12;
        }

        // Settings changed: moods are followed on the next tick; refresh the rest now.
        public void ApplyLook()
        {
            UpdateAutoFactor();
            layoutDirty = true;
            forceLook = true;
            foreach (var o in overlays.Values) o.RegionKey = null; // zones may have changed
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

        // Accounts currently open (true) and accounts that only have saved settings (false).
        public List<KeyValuePair<string, bool>> Accounts()
        {
            var open = new List<string>();
            foreach (var o in overlays.Values)
            {
                string a = AccountOf(o.Target);
                if (!open.Contains(a)) open.Add(a);
            }
            var list = new List<KeyValuePair<string, bool>>();
            foreach (string a in open) list.Add(new KeyValuePair<string, bool>(a, true));
            foreach (string a in Settings.Accounts.Keys) if (!open.Contains(a)) list.Add(new KeyValuePair<string, bool>(a, false));
            list.Sort((x, y) => x.Value != y.Value ? (x.Value ? -1 : 1) : string.Compare(x.Key, y.Key, StringComparison.CurrentCultureIgnoreCase));
            return list;
        }

        // The account of the last Dofus in the foreground, or null.
        public string ForegroundAccount()
        {
            Overlay o;
            return lastGameForeground != IntPtr.Zero && overlays.TryGetValue(lastGameForeground, out o) ? AccountOf(o.Target) : null;
        }

        // The game window to draw the zones over: the last Dofus in the foreground, or any visible one.
        public bool TryGetEditArea(out Rectangle area)
        {
            area = Rectangle.Empty;
            IntPtr h = EditTarget();
            return h != IntPtr.Zero && Native.TryGetClientOnScreen(h, out area);
        }

        // The Dofus window to work on: the last one in the foreground, else the first visible one.
        public IntPtr EditTarget()
        {
            Overlay pick = null;
            foreach (var o in overlays.Values)
            {
                if (!Native.IsWindowVisible(o.Target) || Native.IsIconic(o.Target)) continue;
                if (o.Target == lastGameForeground) { pick = o; break; }
                if (pick == null) pick = o;
            }
            return pick != null ? pick.Target : IntPtr.Zero;
        }

        public string Diagnostics()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("Pénombre " + Program.Version + " - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine(Program.WindowsVersion() + (Environment.Is64BitProcess ? " (x64 process)" : " (x86 process)"));
            sb.AppendLine("Magnification API: " + (Mag.Ready ? "ready" : "unavailable") + " - settings: " + (Settings.IsPortable ? "portable" : "%APPDATA%"));
            sb.AppendLine("Default: enabled=" + Settings.Enabled + " render=" + (Settings.Shader ? "shader" : "veil")
                + " preset=" + Settings.PresetIndex + " intensity=" + Settings.Intensity + " vignette=" + Settings.Vignette
                + " grain=" + Settings.Grain + " process=" + Settings.ProcessName + " onlyForeground=" + Settings.OnlyForeground);
            bool sunOk;
            int[] times = DayCycle.Times(DateTime.Now, out sunOk);
            sb.AppendLine("Auto=" + Settings.Auto + (Settings.SunMode ? " sun " + Settings.SunCity + (sunOk ? "" : " (no sunset today)") : " fixed")
                + " (" + string.Join(" / ", Array.ConvertAll(times, Settings.FormatTime)) + ") factor="
                + autoFactor.ToString("0.00", inv) + " shown=" + shown.ToString("0.00", inv) + " fade=" + Settings.FadeMs + "ms peek=" + peeking);
            sb.AppendLine("Zones: " + (Settings.ZonesEnabled ? "on" : "off") + " (" + Settings.Zones.Count + ", "
                + (Settings.ZonesAnchored ? "anchored" : "proportional") + (Settings.ZonesScale ? ", scaled" : "") + ")");
            var acc = new List<string>();
            foreach (var kv in Settings.Accounts)
                acc.Add(kv.Key + (kv.Value.Filter ? "" : " off") + (kv.Value.Own ? " own(" + kv.Value.Preset + "/" + kv.Value.Intensity + "/" + kv.Value.Vignette + ")" : ""));
            sb.AppendLine("Accounts: " + (acc.Count == 0 ? "-" : string.Join(", ", acc.ToArray())));
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
                sb.AppendLine(string.Format("    filter 0x{0:X} {1} shown={2} topmost={3} region={4} vignette={5} grain={6}",
                    o.Handle.ToInt64(), o.UsesShader ? "shader" : "veil", o.IsShown, Native.IsTopmost(o.Handle), o.RegionInfo,
                    o.Vignette.IsShown, o.Grain.IsShown));
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
            pump.Dispose();
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
        readonly List<Rectangle> rects = new List<Rectangle>();   // local to the game area
        Point dragStart;
        Rectangle dragRect = Rectangle.Empty;
        bool dragging;

        public ZoneEditor(Rectangle area, List<Zone> zones)
        {
            this.area = area;
            foreach (Zone z in zones)
            {
                Rectangle r = z.Resolve(area);
                r.Offset(-area.X, -area.Y);
                rects.Add(r);
            }
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

        // The zones, based on the current game area size.
        public List<Zone> Result()
        {
            var list = new List<Zone>();
            foreach (Rectangle r in rects) list.Add(Zone.FromLocal(r, area.Size));
            return list;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Bounds = area;
            Activate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { dragging = true; dragStart = e.Location; dragRect = Rectangle.Empty; }
            else if (e.Button == MouseButtons.Right)
            {
                for (int i = rects.Count - 1; i >= 0; i--)
                    if (rects[i].Contains(e.Location)) { rects.RemoveAt(i); Invalidate(); break; }
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
            if (dragRect.Width >= 8 && dragRect.Height >= 8) rects.Add(dragRect);
            dragRect = Rectangle.Empty;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { DialogResult = DialogResult.OK; Close(); }
            else if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            else if (e.KeyCode == Keys.Delete) { rects.Clear(); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var gr = e.Graphics;
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            using (var fill = new SolidBrush(Color.FromArgb(110, 160, 255)))
            using (var pen = new Pen(Color.White, 2))
            {
                foreach (Rectangle r in rects)
                {
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

    // ================================================================ Shortcut capture box

    class HotkeyBox : TextBox
    {
        Keys value;
        public event EventHandler ValueChanged;

        public HotkeyBox()
        {
            ReadOnly = true;
            BackColor = SystemColors.Window;
            Width = 170;
            ShortcutsEnabled = false;
            Cursor = Cursors.Hand;
        }

        public Keys Value
        {
            get { return value; }
            set { this.value = value; Text = Shortcuts.Format(value); }
        }

        protected override bool IsInputKey(Keys keyData) { return true; }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Catch Alt+... and other command keys before the form does.
            HandleKey(keyData);
            return true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            Text = Shortcuts.Format(value);
            e.Handled = true;
        }

        void HandleKey(Keys keyData)
        {
            Keys code = keyData & Keys.KeyCode, mods = keyData & Keys.Modifiers;
            if ((code == Keys.Back || code == Keys.Delete) && mods == Keys.None) { Set(Keys.None); return; }
            if (code == Keys.Tab && mods == Keys.None) { Parent.SelectNextControl(this, true, true, true, true); return; }
            if (code == Keys.ControlKey || code == Keys.ShiftKey || code == Keys.Menu || code == Keys.LWin || code == Keys.RWin)
            {
                Text = Shortcuts.Format(mods);
                return;
            }
            if (Shortcuts.IsValid(code | mods)) Set(code | mods);
        }

        void Set(Keys k)
        {
            Value = k;
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }

    // ================================================================ Settings window

    class SettingsForm : Form
    {
        readonly OverlayManager manager;
        readonly Action changed;
        readonly Func<HashSet<int>> failedHotkeys;
        bool loading;

        // Mood (default settings)
        PresetPicker presetBox;
        Label presetName;
        PreviewBox preview;
        ComboBox vignetteBox, renderBox, grainBox;
        GameSlider bright, sat, temp, lift, intensity;
        Label brightV, satV, tempV, liftV, intensityV;
        Panel vColorSwatch;
        // Schedule
        CheckBox autoBox, duskBox;
        RadioButton fixedRadio, sunRadio;
        TimeField duskPick, nightPick, dawnPick, dayPick;
        ComboBox cityBox;
        NumericUpDown latBox, lonBox;
        Label sunInfo, autoState, fadeV;
        GameSlider fade;
        // Accounts
        ListBox accountList;
        CheckBox accFilter, accOwn;
        ComboBox accPreset, accVignette;
        GameSlider accIntensity;
        Label accIntensityV;
        Button accForget;
        // Display
        IconChoice[] iconChoices;
        CheckBox zonesBox, zonesAnchor, zonesScale, onlyFgBox, iconAutoBox;
        Label zonesInfo;
        // Shortcuts
        HotkeyBox[] hotkeyBoxes;
        Label[] hotkeyStatus;
        // General
        CheckBox startupBox, portableBox, updatesBox;
        Label updateLabel;
        GameButton updateGet;
        Label folderLabel;

        readonly Timer stateTimer;
        readonly Panel host;
        readonly FooterBar footer;
        readonly List<Control> pages = new List<Control>();
        readonly List<string> titles = new List<string>();
        readonly List<RailButton> rail = new List<RailButton>();
        int current = -1;

        class AccountItem
        {
            public string Name; public bool Open;
            public override string ToString()
            {
                AccountProfile p = Settings.Profile(Name, false);
                string extra = !Open ? " " + T.S("offline") : "";
                if (p != null && p.Own) extra += " · " + T.S("ownSettings");
                if (p != null && !p.Filter) extra += " · " + T.S("off");
                return Name + extra;
            }
        }

        public SettingsForm(OverlayManager manager, Action changed, Func<HashSet<int>> failedHotkeys)
        {
            this.manager = manager;
            this.changed = changed;
            this.failedHotkeys = failedHotkeys;

            using (Graphics g = CreateGraphics()) Theme.Scale = g.DpiX / 96f;
            Text = "Pénombre - " + T.S("settingsT");
            Icon = TrayApp.WindowIcon ?? TrayApp.AppIcon;
            Font = Theme.Body(9.5f);
            BackColor = Theme.Edge;
            ForeColor = Theme.Text;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Padding = new Padding(Theme.Px(2));
            KeyPreview = true;

            host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Panel, ForeColor = Theme.Text };
            var railPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Left, Width = Theme.Px(66), BackColor = Theme.Rail, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, Padding = new Padding(0, Theme.Px(10), 0, 0),
            };
            railPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Theme.Line)) e.Graphics.DrawLine(pen, railPanel.Width - 1, 0, railPanel.Width - 1, railPanel.Height);
                // the version, discreetly, at the bottom of the rail
                using (Font f = Theme.Body(7.5f))
                    TextRenderer.DrawText(e.Graphics, "v" + Program.Version, f, new Rectangle(0, railPanel.Height - Theme.Px(26), railPanel.Width - 1, Theme.Px(20)),
                                          Color.FromArgb(0x6e, 0x6b, 0x7a), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            };
            railPanel.Resize += (s, e) => railPanel.Invalidate();
            footer = new FooterBar();
            var closeButton = new GameButton(T.S("close"), GameButton.Kind.Primary);
            closeButton.Click += (s, e) => Close();
            CancelButton = closeButton;
            footer.Controls.Add(closeButton);
            footer.Resize += (s, e) => closeButton.Location = new Point(footer.Width - closeButton.Width - Theme.Px(14), (footer.Height - closeButton.Height) / 2);
            var header = new HeaderBar("Pénombre");
            var closeX = new GameButton("", GameButton.Kind.Close);
            closeX.Click += (s, e) => Close();
            header.Controls.Add(closeX);
            var lang = new LanguageChip { Text = T.Code.ToUpperInvariant(), AccessibleName = T.S("langTip") };
            new ToolTip().SetToolTip(lang, T.S("langTip"));
            lang.Click += (s, e) =>
            {
                var menu = new ContextMenuStrip { Font = Theme.Body(9.5f) };
                var auto = new ToolStripMenuItem(T.S("langAuto")) { Checked = Settings.Language.Length == 0 };
                auto.Click += (s2, e2) => T.Choose("");
                menu.Items.Add(auto);
                menu.Items.Add(new ToolStripSeparator());
                for (int i = 0; i < T.Codes.Length; i++)
                {
                    string code = T.Codes[i];
                    var it = new ToolStripMenuItem(T.Names[i]) { Checked = Settings.Language == code };
                    it.Click += (s2, e2) => T.Choose(code);
                    menu.Items.Add(it);
                }
                menu.Closed += (s2, e2) => BeginInvoke((Action)menu.Dispose);
                menu.Show(lang, new Point(0, lang.Height + Theme.Px(2)));
            };
            header.Controls.Add(lang);
            header.Resize += (s, e) => lang.Location = new Point(Theme.Px(14), (header.Height - Theme.Px(2) - lang.Height) / 2);
            header.Resize += (s, e) => closeX.Location = new Point(header.Width - closeX.Width - Theme.Px(10), (header.Height - Theme.Px(2) - closeX.Height) / 2);

            Controls.Add(host);
            Controls.Add(railPanel);
            Controls.Add(footer);
            Controls.Add(header);

            AddPage(T.S("tMood"), BuildMood);
            AddPage(T.S("tAuto"), t => BuildSchedule(t));
            AddPage(T.S("tAccounts"), t => BuildAccounts(t));
            AddPage(T.S("tDisplay"), t => BuildDisplay(t));
            AddPage(T.S("tKeys"), t => BuildShortcuts(t));
            AddPage(T.S("tGeneral"), t => BuildGeneral(t));
            for (int i = 0; i < titles.Count; i++)
            {
                var b = new RailButton(i, titles[i]);
                b.Selected += (s, e) => SelectPage(((RailButton)s).Index);
                new ToolTip().SetToolTip(b, titles[i]);
                rail.Add(b);
                railPanel.Controls.Add(b);
            }
            Theme.Style(host);
            FitPages(railPanel.Width, header.Height, footer.Height);
            SelectPage(0);

            stateTimer = new Timer { Interval = 1000 };
            stateTimer.Tick += (s, e) => { UpdateLabels(); if (current == 0 && Visible) preview.TakeShot(); };
            stateTimer.Start();

            LoadAll();
            Shown += (s, e) => preview.TakeShot();
        }

        // Drop shadow, and the system menu / minimize of a normal window despite having no border.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;          // CS_DROPSHADOW
                cp.Style |= 0x00020000 | 0x00080000; // WS_MINIMIZEBOX | WS_SYSMENU
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.Frame)) e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) stateTimer.Dispose();
            base.Dispose(disposing);
        }

        // ---- pages (also used by the self-test)

        public int PageCount { get { return pages.Count; } }
        public string PageTitle(int i) { return titles[i]; }
        public Control PageHost { get { return host; } }
        public Control Page(int i) { return pages[i]; }
        public bool RefreshPreview() { preview.TakeShot(); return preview.LastShotDirect; }

        public int CurrentPage { get { return current; } }

        public void SelectPage(int index)
        {
            if (index < 0 || index >= pages.Count) return;
            current = index;
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].Visible = i == index;
                rail[i].Active = i == index;
            }
            if (index == 0 && preview != null && Visible) preview.TakeShot();
        }

        void AddPage(string title, Func<string, Control> build)
        {
            Control content = build(title);
            content.Location = new Point(Theme.Px(20), Theme.Px(16));
            content.Visible = false;
            host.Controls.Add(content);
            pages.Add(content);
            titles.Add(title);
        }

        void AddPage(string title, Action<TableLayoutPanel> build)
        {
            AddPage(title, t => (Control)PageTable(t, build));
        }

        TableLayoutPanel PageTable(string title, Action<TableLayoutPanel> build)
        {
            TableLayoutPanel t = Table(2);
            Heading(t, title);
            build(t);
            return t;
        }

        static TableLayoutPanel Table(int columns)
        {
            var t = new TableLayoutPanel { ColumnCount = columns, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0), Padding = new Padding(0) };
            for (int i = 0; i < columns; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            return t;
        }

        // Every page shows its whole content: the window is sized on the largest one.
        void FitPages(int railWidth, int headerHeight, int footerHeight)
        {
            Size need = Size.Empty;
            foreach (Control p in pages)
            {
                Size ps = p.GetPreferredSize(Size.Empty);
                p.Size = ps;
                need = new Size(Math.Max(need.Width, ps.Width), Math.Max(need.Height, ps.Height));
            }
            int pad = Theme.Px(20);
            ClientSize = new Size(Padding.Horizontal + railWidth + need.Width + pad * 2,
                                  Padding.Vertical + headerHeight + footerHeight + need.Height + Theme.Px(16) * 2);
        }

        // ---- page contents

        Control BuildMood(string title)
        {
            var outer = Table(2);

            // left: preview and mood swatches
            var left = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, Theme.Px(22), 0) };
            var heading = Table(1);
            Heading(heading, title);
            left.Controls.Add(heading);
            left.Controls.Add(new Label { Text = T.S("defaultHint").ToUpperInvariant(), AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Strong(7.5f), MaximumSize = new Size(Theme.Px(250), 0), Margin = new Padding(Theme.Px(3), 0, Theme.Px(3), Theme.Px(6)) });
            preview = new PreviewBox
            {
                GameWindow = () => manager.EditTarget(),
                Matrix = () => Look.From(Mood.From(new AccountParams { Preset = Settings.PresetIndex, Intensity = Settings.Intensity, Vignette = 0, Filter = true }), 1).Matrix,
                NoGameText = T.S("previewNone"), DayText = T.S("previewDay"), NightText = T.S("previewNight"),
            };
            left.Controls.Add(preview);
            presetBox = new PresetPicker { AccessibleName = T.S("mood") };
            left.Controls.Add(presetBox);
            presetName = new Label { AutoSize = true, Font = Theme.Strong(10.5f), ForeColor = Theme.GreenHi, Margin = new Padding(Theme.Px(3), Theme.Px(4), Theme.Px(3), 0) };
            left.Controls.Add(presetName);

            // right: the settings
            var t = Table(2);
            Section(t, T.S("sColors"));
            bright = Slider(10, 100, 5, out brightV); Row(t, T.S("brightness"), Flow(bright, brightV));
            sat = Slider(0, 100, 5, out satV); Row(t, T.S("saturation"), Flow(sat, satV));
            temp = Slider(-100, 100, 10, out tempV); temp.Spectrum = true; Row(t, T.S("temperature"), Flow(temp, tempV));
            lift = Slider(0, 100, 5, out liftV); Row(t, T.S("lift"), Flow(lift, liftV));
            Hint(t, T.S("customHint"), 1);
            Section(t, T.S("sEffects"));
            intensity = Slider(Settings.MinIntensity, Settings.MaxIntensity, 5, out intensityV);
            Label il = Row(t, T.S("intensity"), Flow(intensity, intensityV));
            il.ForeColor = Theme.GreenHi; il.Font = Theme.Strong(9.5f);
            intensityV.ForeColor = Theme.GreenHi;
            vignetteBox = Combo(200);
            for (int i = 0; i < 4; i++) vignetteBox.Items.Add(T.S("v" + i));
            vColorSwatch = new Panel { Width = Theme.Px(30), Height = Theme.Px(26), Margin = new Padding(Theme.Px(3), Theme.Px(5), Theme.Px(3), Theme.Px(3)) };
            vColorSwatch.Paint += (s, e) => { using (var pen = new Pen(Theme.Frame)) e.Graphics.DrawRectangle(pen, 0, 0, vColorSwatch.Width - 1, vColorSwatch.Height - 1); };
            Row(t, T.S("vignette"), Flow(vignetteBox, vColorSwatch));
            var vChoose = new GameButton(T.S("choose"), GameButton.Kind.Normal);
            var vReset = new GameButton(T.S("reset"), GameButton.Kind.Normal);
            Row(t, T.S("vColor"), Flow(vChoose, vReset));
            grainBox = Combo(200);
            for (int i = 0; i < 3; i++) grainBox.Items.Add(T.S("g" + i));
            Row(t, T.S("grain"), grainBox);
            renderBox = Combo(250);
            renderBox.Items.Add(T.S("shader"));
            renderBox.Items.Add(T.S("veil"));
            Row(t, T.S("render"), renderBox);

            outer.Controls.Add(left, 0, 0);
            outer.Controls.Add(t, 1, 0);

            presetBox.SelectedIndexChanged += (s, e) =>
            {
                presetName.Text = presetBox.SelectedIndex >= 0 ? Preset.NameOf(presetBox.SelectedIndex) : "";
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
            grainBox.SelectedIndexChanged += (s, e) => { if (!loading) { Settings.Grain = grainBox.SelectedIndex; Commit(); } };
            vChoose.Click += (s, e) =>
            {
                using (var dlg = new ColorDialog { Color = Settings.VignetteColor, FullOpen = true })
                    if (dlg.ShowDialog(this) == DialogResult.OK) { Settings.VignetteColor = dlg.Color; Commit(); }
            };
            vReset.Click += (s, e) => { Settings.VignetteColor = Settings.DefaultVignetteColor; Commit(); };
            renderBox.SelectedIndexChanged += (s, e) =>
            {
                if (loading) return;
                Settings.Shader = renderBox.SelectedIndex == 0;
                manager.Rebuild();
                Commit();
            };
            return outer;
        }

        void BuildSchedule(TableLayoutPanel t)
        {
            autoBox = Check(T.S("auto"));
            Row(t, "", autoBox);
            Section(t, T.S("sFixed"));
            fixedRadio = new GameRadio(T.S("fixedTimes"));
            Row(t, "", fixedRadio);
            duskPick = TimePick(); nightPick = TimePick(); dawnPick = TimePick(); dayPick = TimePick();
            Row(t, T.S("dusk") + " / " + T.S("night"), Flow(duskPick, Arrow(), nightPick));
            Row(t, T.S("dawn") + " / " + T.S("day"), Flow(dawnPick, Arrow(), dayPick));
            Section(t, T.S("sSun"));
            sunRadio = new GameRadio(T.S("sunTimes"));
            Row(t, "", sunRadio);
            cityBox = Combo(240);
            foreach (Sun.City c in Sun.Cities) cityBox.Items.Add(c.Name);
            cityBox.Items.Add(T.S("customPlace"));
            Row(t, T.S("city"), cityBox);
            latBox = Coord(-90, 90); lonBox = Coord(-180, 180);
            Row(t, T.S("latitude") + " / " + T.S("longitude"), Flow(latBox, lonBox));
            sunInfo = Muted("");
            Row(t, T.S("today"), sunInfo);
            Hint(t, T.S("sunHint"), 1);
            Section(t, T.S("sTransition"));
            duskBox = Check(T.S("duskTint")); Row(t, "", duskBox);
            Hint(t, T.S("duskHint"), 1);
            duskBox.CheckedChanged += (s, e) => { if (!loading) { Settings.DuskTint = duskBox.Checked; Commit(); } };
            autoState = new Label { AutoSize = true, Margin = new Padding(Theme.Px(3), Theme.Px(7), Theme.Px(3), Theme.Px(3)), Font = Theme.Strong(9.5f), ForeColor = Theme.GreenHi };
            Row(t, T.S("now"), autoState);
            fade = Slider(0, 2000, 100, out fadeV);
            Row(t, T.S("fade"), Flow(fade, fadeV));
            Hint(t, T.S("fadeHint"), 1);

            autoBox.CheckedChanged += (s, e) => { if (!loading) { Settings.Auto = autoBox.Checked; Commit(); } };
            EventHandler modeChanged = (s, e) => { if (!loading) { Settings.SunMode = sunRadio.Checked; Commit(); } };
            fixedRadio.CheckedChanged += modeChanged;
            sunRadio.CheckedChanged += modeChanged;
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
            cityBox.SelectedIndexChanged += (s, e) =>
            {
                if (loading || cityBox.SelectedIndex < 0) return;
                if (cityBox.SelectedIndex < Sun.Cities.Length)
                {
                    Sun.City c = Sun.Cities[cityBox.SelectedIndex];
                    Settings.SunCity = c.Name; Settings.SunLat = c.Lat; Settings.SunLon = c.Lon;
                    loading = true; latBox.Value = (decimal)c.Lat; lonBox.Value = (decimal)c.Lon; loading = false;
                }
                else Settings.SunCity = "";
                Commit();
            };
            EventHandler coordChanged = (s, e) =>
            {
                if (loading) return;
                Settings.SunLat = (double)latBox.Value;
                Settings.SunLon = (double)lonBox.Value;
                Settings.SunCity = "";
                loading = true; cityBox.SelectedIndex = Sun.Cities.Length; loading = false;
                Commit();
            };
            latBox.ValueChanged += coordChanged;
            lonBox.ValueChanged += coordChanged;
            fade.ValueChanged += (s, e) => { if (!loading) { Settings.FadeMs = fade.Value; Commit(); } };
        }

        void BuildAccounts(TableLayoutPanel t)
        {
            accountList = new ListBox
            {
                Width = Theme.Px(300), Height = Theme.Px(150), IntegralHeight = false, BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = Theme.Px(28), BackColor = Theme.Field, ForeColor = Theme.Text,
            };
            accountList.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                bool sel = (e.State & DrawItemState.Selected) != 0;
                using (var b = new SolidBrush(sel ? Theme.GreenSelected : Theme.Field)) e.Graphics.FillRectangle(b, e.Bounds);
                if (sel) using (var b = new SolidBrush(Theme.GreenHi)) e.Graphics.FillRectangle(b, e.Bounds.X, e.Bounds.Y, Theme.Px(3), e.Bounds.Height);
                var item = accountList.Items[e.Index] as AccountItem;
                Color c = item != null && !item.Open ? Theme.Muted : sel ? Theme.GreenHi : Theme.Text;
                TextRenderer.DrawText(e.Graphics, accountList.Items[e.Index].ToString(), accountList.Font,
                                      new Rectangle(e.Bounds.X + Theme.Px(10), e.Bounds.Y, e.Bounds.Width - Theme.Px(12), e.Bounds.Height),
                                      c, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            var listFrame = new Panel { Padding = new Padding(1), BackColor = Theme.Frame, Size = new Size(accountList.Width + 2, accountList.Height + 2), Margin = new Padding(Theme.Px(3), Theme.Px(4), Theme.Px(3), Theme.Px(6)) };
            accountList.Dock = DockStyle.Fill;
            listFrame.Controls.Add(accountList);
            Row(t, T.S("accList"), listFrame);
            accFilter = Check(T.S("accFilter")); Row(t, "", accFilter);
            accOwn = Check(T.S("accOwn")); Row(t, "", accOwn);
            Section(t, T.S("sOwn"));
            Hint(t, T.S("ownHint"), 0);
            accPreset = Combo(240);
            for (int i = 0; i < Preset.Count; i++) accPreset.Items.Add(Preset.NameOf(i));
            Row(t, T.S("mood"), accPreset);
            accIntensity = Slider(Settings.MinIntensity, Settings.MaxIntensity, 5, out accIntensityV);
            Row(t, T.S("intensity"), Flow(accIntensity, accIntensityV));
            accVignette = Combo(240);
            for (int i = 0; i < 4; i++) accVignette.Items.Add(T.S("v" + i));
            Row(t, T.S("vignette"), accVignette);
            accForget = new GameButton(T.S("accForget"), GameButton.Kind.Normal);
            Row(t, "", accForget);
            Hint(t, T.S("accHint"), 0);

            accountList.SelectedIndexChanged += (s, e) => { if (!loading) LoadAccount(); };
            accFilter.CheckedChanged += (s, e) => { if (!loading) EditAccount(p => p.Filter = accFilter.Checked); };
            // Own settings start from what is shown (the default ones); touching one of them turns them on.
            Action<AccountProfile> takeShown = p =>
            {
                p.Preset = accPreset.SelectedIndex;
                p.Intensity = accIntensity.Value;
                p.Vignette = accVignette.SelectedIndex;
            };
            accOwn.CheckedChanged += (s, e) => { if (!loading) EditAccount(p => { if (accOwn.Checked) takeShown(p); p.Own = accOwn.Checked; }); };
            EventHandler ownChanged = (s, e) =>
            {
                if (loading) return;
                EditAccount(p => { takeShown(p); p.Own = true; });
                loading = true; accOwn.Checked = true; loading = false;
            };
            accPreset.SelectedIndexChanged += ownChanged;
            accIntensity.ValueChanged += ownChanged;
            accVignette.SelectedIndexChanged += ownChanged;
            accForget.Click += (s, e) =>
            {
                var item = accountList.SelectedItem as AccountItem;
                if (item == null) return;
                Settings.Accounts.Remove(item.Name);
                Commit();
                RefreshAccounts();
            };
        }

        void BuildDisplay(TableLayoutPanel t)
        {
            Section(t, T.S("sIcon"));
            iconAutoBox = Check(T.S("iconAuto"));
            Wide(t, iconAutoBox);
            iconAutoBox.CheckedChanged += (s, e) =>
            {
                if (loading) return;
                Settings.TrayIconAuto = iconAutoBox.Checked;
                ShowIconChoices();
                Commit();
            };
            iconChoices = new IconChoice[TrayIcons.StyleCount];
            for (int i = 0; i < iconChoices.Length; i++)
            {
                var c = new IconChoice(i, T.S("ico" + i), T.S("ico" + i + "d"));
                c.Picked += (s, e) =>
                {
                    var picked = (IconChoice)s;
                    Settings.TrayIcon = picked.Style;
                    if (picked.PickedShade >= 0) Settings.TrayIconColor = picked.PickedShade;
                    ShowIconChoices();
                    Commit();
                };
                iconChoices[i] = c;
                Wide(t, c);
            }
            Hint(t, T.S("iconHint"), 0);
            Section(t, T.S("sZones"));
            zonesBox = Check(T.S("zonesKeep")); Row(t, "", zonesBox);
            var zonesButton = new GameButton(T.S("zonesEdit"), GameButton.Kind.Normal);
            zonesInfo = Muted("");
            zonesInfo.Margin = new Padding(Theme.Px(8), Theme.Px(10), Theme.Px(3), Theme.Px(3));
            Row(t, "", Flow(zonesButton, zonesInfo));
            zonesAnchor = Check(T.S("zonesAnchor")); Row(t, "", zonesAnchor);
            zonesScale = Check(T.S("zonesScale")); Row(t, "", zonesScale);
            Hint(t, T.S("zonesHint"), 1);
            Section(t, T.S("sBehavior"));
            onlyFgBox = Check(T.S("onlyFg")); Row(t, "", onlyFgBox);

            zonesBox.CheckedChanged += (s, e) => { if (!loading) { Settings.ZonesEnabled = zonesBox.Checked; Commit(); } };
            zonesAnchor.CheckedChanged += (s, e) => { if (!loading) { Settings.ZonesAnchored = zonesAnchor.Checked; Commit(); } };
            zonesScale.CheckedChanged += (s, e) => { if (!loading) { Settings.ZonesScale = zonesScale.Checked; Commit(); } };
            onlyFgBox.CheckedChanged += (s, e) => { if (!loading) { Settings.OnlyForeground = onlyFgBox.Checked; Commit(); } };
            zonesButton.Click += (s, e) => EditZones();
        }

        void BuildShortcuts(TableLayoutPanel t)
        {
            hotkeyBoxes = new HotkeyBox[Shortcuts.Count];
            hotkeyStatus = new Label[Shortcuts.Count];
            for (int i = 0; i < Shortcuts.Count; i++)
            {
                int idx = i;
                hotkeyBoxes[i] = new HotkeyBox { Font = Theme.Strong(9.5f) };
                hotkeyStatus[i] = new Label { AutoSize = true, ForeColor = Color.FromArgb(0xff, 0x8a, 0x7a), Margin = new Padding(Theme.Px(6), Theme.Px(7), Theme.Px(3), Theme.Px(3)) };
                Row(t, T.S(Shortcuts.Keys_[i]), Flow(hotkeyBoxes[i], hotkeyStatus[i]));
                hotkeyBoxes[i].ValueChanged += (s, e) => { if (!loading) { Settings.Hotkeys[idx] = hotkeyBoxes[idx].Value; Commit(); } };
            }
            Hint(t, T.S("hkHint"), 0);
            var reset = new GameButton(T.S("hkDefaults"), GameButton.Kind.Normal);
            Row(t, "", reset);
            reset.Click += (s, e) => { Settings.Hotkeys = Shortcuts.Defaults(); Commit(); LoadAll(); };
        }

        void BuildGeneral(TableLayoutPanel t)
        {
            Section(t, T.S("sWindows"));
            startupBox = Check(T.S("startup")); Row(t, "", startupBox);
            portableBox = Check(T.S("portable")); Row(t, "", portableBox);
            updatesBox = Check(T.S("updCheck")); Row(t, "", updatesBox);
            var updateNow = new GameButton(T.S("updNow"), GameButton.Kind.Normal);
            updateGet = new GameButton(string.Format(T.S("updSee"), "0.0.0"), GameButton.Kind.Normal) { Visible = false };
            updateLabel = Muted("");
            updateLabel.MaximumSize = new Size(Theme.Px(360), 0);
            Row(t, "", Flow(updateNow, updateGet));
            Row(t, "", updateLabel);
            updatesBox.CheckedChanged += (s, e) => { if (!loading) { Settings.CheckUpdates = updatesBox.Checked; Commit(); } };
            updateNow.Click += (s, e) => Updates.Check();
            updateGet.Click += (s, e) => Updates.OpenPage();
            folderLabel = Muted("");
            folderLabel.MaximumSize = new Size(Theme.Px(360), 0);
            var openFolder = new GameButton(T.S("openFolder"), GameButton.Kind.Normal);
            Row(t, T.S("folder"), folderLabel);
            Row(t, "", openFolder);
            Section(t, T.S("sBackup"));
            var export = new GameButton(T.S("export"), GameButton.Kind.Normal);
            var import = new GameButton(T.S("import"), GameButton.Kind.Normal);
            var log = new GameButton(T.S("openLog"), GameButton.Kind.Normal);
            Row(t, "", Flow(export, import, log));
            var reset = new GameButton(T.S("resetAll"), GameButton.Kind.Normal);
            Row(t, "", reset);
            Hint(t, T.S("resetHint"), 1);

            startupBox.CheckedChanged += (s, e) => { if (!loading) Startup.Set(startupBox.Checked); };
            portableBox.CheckedChanged += (s, e) =>
            {
                if (loading) return;
                if (!Settings.SetPortable(portableBox.Checked))
                {
                    MessageBox.Show(this, T.S("portableErr"), "Pénombre", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    loading = true; portableBox.Checked = Settings.IsPortable; loading = false;
                }
                UpdateLabels();
            };
            openFolder.Click += (s, e) =>
            {
                try { Directory.CreateDirectory(Settings.Folder); Process.Start("explorer.exe", "\"" + Settings.Folder + "\""); }
                catch (Exception ex) { Log.Error("Open folder", ex); }
            };
            export.Click += (s, e) =>
            {
                using (var dlg = new SaveFileDialog { FileName = "Penombre-settings.ini", Filter = "Pénombre (*.ini)|*.ini", DefaultExt = "ini" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    try { Settings.SaveTo(dlg.FileName); }
                    catch (Exception ex) { Log.Error("Export", ex); MessageBox.Show(this, ex.Message, "Pénombre", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            };
            import.Click += (s, e) =>
            {
                using (var dlg = new OpenFileDialog { Filter = "Pénombre (*.ini)|*.ini|*.*|*.*" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    if (!Settings.LoadFrom(dlg.FileName))
                    {
                        MessageBox.Show(this, T.S("importBad"), "Pénombre", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    Settings.Save();
                    manager.Rebuild();
                    Commit();
                    LoadAll();
                    MessageBox.Show(this, T.S("importDone"), "Pénombre", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            log.Click += (s, e) => TrayApp.OpenLog();
            reset.Click += (s, e) =>
            {
                if (MessageBox.Show(this, T.S("resetAsk"), "Pénombre", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;
                Settings.ResetToDefaults();
                manager.Rebuild();
                Commit();
                LoadAll();
                MessageBox.Show(this, T.S("resetDone"), "Pénombre", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }

        // ---- building blocks

        static void Heading(TableLayoutPanel t, string title)
        {
            var l = new Label { Text = title, AutoSize = true, Font = Theme.Title(17f), ForeColor = Color.White, Margin = new Padding(0, 0, 0, Theme.Px(4)) };
            t.Controls.Add(l, 0, t.RowCount);
            if (t.ColumnCount > 1) t.SetColumnSpan(l, t.ColumnCount);
            t.RowCount++;
        }

        static void Section(TableLayoutPanel t, string title)
        {
            var l = new SectionLabel(title) { Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, Theme.Px(t.RowCount <= 1 ? 2 : 10), 0, Theme.Px(2)) };
            Wide(t, l);
        }

        static void Wide(TableLayoutPanel t, Control c)
        {
            t.Controls.Add(c, 0, t.RowCount);
            t.SetColumnSpan(c, 2);
            t.RowCount++;
        }

        static Label Row(TableLayoutPanel t, string label, Control c)
        {
            var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(Theme.Px(3), Theme.Px(6), Theme.Px(18), Theme.Px(3)) };
            t.Controls.Add(l, 0, t.RowCount);
            t.Controls.Add(c, 1, t.RowCount);
            t.RowCount++;
            return l;
        }

        // column 0: spans both columns; 1: under the controls
        static void Hint(TableLayoutPanel t, string text, int column)
        {
            var l = new Label { Text = text, AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Body(8.5f), Margin = new Padding(Theme.Px(3), Theme.Px(2), Theme.Px(3), Theme.Px(8)), MaximumSize = new Size(Theme.Px(400), 0) };
            t.Controls.Add(l, column, t.RowCount);
            if (column == 0) t.SetColumnSpan(l, 2);
            t.RowCount++;
        }

        static Label Muted(string text)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(Theme.Px(3), Theme.Px(6), Theme.Px(3), Theme.Px(3)) };
        }

        static Label Arrow()
        {
            return new Label { Text = "→", AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(Theme.Px(4), Theme.Px(8), Theme.Px(4), 0) };
        }

        static ComboBox Combo(int width) { return new GameCombo(width); }

        static CheckBox Check(string text) { return new GameCheck(text); }

        static GameSlider Slider(int min, int max, int step, out Label value)
        {
            value = new Label { AutoSize = true, Font = Theme.Strong(9.5f), Margin = new Padding(Theme.Px(6), Theme.Px(8), Theme.Px(3), Theme.Px(3)), MinimumSize = new Size(Theme.Px(70), 0) };
            return new GameSlider { Minimum = min, Maximum = max, SmallChange = step, LargeChange = step * 2 };
        }

        static NumericUpDown Coord(int min, int max)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = 3, Increment = 0.1m, Width = Theme.Px(90), Margin = new Padding(Theme.Px(3), Theme.Px(4), Theme.Px(6), Theme.Px(3)) };
        }

        static TimeField TimePick() { return new TimeField(); }

        static Control Flow(params Control[] items)
        {
            var p = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0) };
            p.Controls.AddRange(items);
            return p;
        }

        static int Minutes(TimeField p) { return p.Value.Hour * 60 + p.Value.Minute; }
        static DateTime At(int minutes) { return DateTime.Today.AddMinutes(minutes); }
        static decimal Clamp(double v, decimal min, decimal max) { return Math.Max(min, Math.Min(max, (decimal)v)); }

        // ---- data

        public void LoadAll()
        {
            loading = true;
            presetBox.SelectedIndex = Settings.PresetIndex;
            presetName.Text = Preset.NameOf(Settings.PresetIndex);
            LoadSlidersCore();
            intensity.Value = Settings.Intensity;
            vignetteBox.SelectedIndex = Settings.Vignette;
            grainBox.SelectedIndex = Settings.Grain;
            renderBox.SelectedIndex = Settings.Shader ? 0 : 1;
            renderBox.Enabled = Mag.Ready;

            autoBox.Checked = Settings.Auto;
            duskBox.Checked = Settings.DuskTint;
            fixedRadio.Checked = !Settings.SunMode;
            sunRadio.Checked = Settings.SunMode;
            duskPick.Value = At(Settings.Dusk);
            nightPick.Value = At(Settings.Night);
            dawnPick.Value = At(Settings.Dawn);
            dayPick.Value = At(Settings.Day);
            int city = Array.FindIndex(Sun.Cities, c => c.Name == Settings.SunCity);
            cityBox.SelectedIndex = city >= 0 ? city : Sun.Cities.Length;
            latBox.Value = Clamp(Settings.SunLat, latBox.Minimum, latBox.Maximum);
            lonBox.Value = Clamp(Settings.SunLon, lonBox.Minimum, lonBox.Maximum);
            fade.Value = Math.Min(fade.Maximum, Settings.FadeMs);

            iconAutoBox.Checked = Settings.TrayIconAuto;
            ShowIconChoices();
            zonesBox.Checked = Settings.ZonesEnabled;
            zonesAnchor.Checked = Settings.ZonesAnchored;
            zonesScale.Checked = Settings.ZonesScale;
            onlyFgBox.Checked = Settings.OnlyForeground;

            for (int i = 0; i < Shortcuts.Count; i++) hotkeyBoxes[i].Value = Settings.Hotkeys[i];

            startupBox.Checked = Startup.IsEnabled();
            portableBox.Checked = Settings.IsPortable;
            updatesBox.Checked = Settings.CheckUpdates;
            RefreshUpdate();
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
            int[] v = Settings.PresetIndex == Preset.CustomIndex
                ? new[] { Settings.CustomBrightness, Settings.CustomSaturation, Settings.CustomTemperature, Settings.CustomLift }
                : Preset.Get(Settings.PresetIndex).ToSliders();
            bright.Value = v[0];
            sat.Value = v[1];
            temp.Value = v[2];
            lift.Value = v[3];
        }

        public void RefreshAccounts()
        {
            string selected = accountList.SelectedItem is AccountItem ? ((AccountItem)accountList.SelectedItem).Name : null;
            loading = true;
            accountList.Items.Clear();
            foreach (var kv in manager.Accounts()) accountList.Items.Add(new AccountItem { Name = kv.Key, Open = kv.Value });
            int index = -1;
            for (int i = 0; i < accountList.Items.Count; i++)
                if (((AccountItem)accountList.Items[i]).Name == selected) index = i;
            if (index < 0 && accountList.Items.Count > 0) index = 0;
            accountList.SelectedIndex = index;
            loading = false;
            LoadAccount();
        }

        void LoadAccount()
        {
            var item = accountList.SelectedItem as AccountItem;
            bool has = item != null;
            AccountProfile p = has ? Settings.Profile(item.Name, false) : null;
            loading = true;
            accFilter.Checked = p == null || p.Filter;
            accOwn.Checked = p != null && p.Own;
            accPreset.SelectedIndex = p != null && p.Own ? p.Preset : Settings.PresetIndex;
            accIntensity.Value = p != null && p.Own ? p.Intensity : Settings.Intensity;
            accVignette.SelectedIndex = p != null && p.Own ? p.Vignette : Settings.Vignette;
            loading = false;
            accFilter.Enabled = accOwn.Enabled = has;
            accPreset.Enabled = accIntensity.Enabled = accVignette.Enabled = has;
            accForget.Enabled = p != null;
            UpdateLabels();
        }

        void EditAccount(Action<AccountProfile> edit)
        {
            var item = accountList.SelectedItem as AccountItem;
            if (item == null) return;
            AccountProfile p = Settings.Profile(item.Name, true);
            edit(p);
            Commit();
            int i = accountList.SelectedIndex;
            loading = true; accountList.Items[i] = accountList.Items[i]; loading = false; // refresh the label
            accForget.Enabled = true;
        }

        void UpdateLabels()
        {
            brightV.Text = bright.Value + " %";
            satV.Text = sat.Value + " %";
            tempV.Text = temp.Value == 0 ? T.S("neutral") : (temp.Value < 0 ? T.S("cold") : T.S("warm")) + " " + Math.Abs(temp.Value);
            tempV.ForeColor = temp.Value < 0 ? Color.FromArgb(0x7c, 0xc0, 0xf2) : temp.Value > 0 ? Color.FromArgb(0xf2, 0xa8, 0x6c) : Theme.Text;
            liftV.Text = lift.Value + " %";
            intensityV.Text = intensity.Value + " %";
            accIntensityV.Text = accIntensity.Value + " %";
            vColorSwatch.BackColor = Settings.VignetteColor;
            fadeV.Text = fade.Value == 0 ? T.S("instant") : (fade.Value / 1000.0).ToString("0.0") + " s";
            autoState.Text = Settings.Auto ? DayCycle.Describe(DateTime.Now) : "-";
            zonesInfo.Text = string.Format(T.S("zonesCount"), Settings.Zones.Count);

            double rise, set;
            sunInfo.Text = Sun.Compute(DateTime.Today, Settings.SunLat, Settings.SunLon, out rise, out set)
                ? string.Format(T.S("sunInfo"), Settings.FormatTime((int)Math.Round(set) % 1440), Settings.FormatTime((int)Math.Round(rise) % 1440))
                : T.S("sunNone");

            bool auto = Settings.Auto;
            fixedRadio.Enabled = sunRadio.Enabled = auto;
            duskPick.Enabled = nightPick.Enabled = dawnPick.Enabled = dayPick.Enabled = auto && !Settings.SunMode;
            cityBox.Enabled = latBox.Enabled = lonBox.Enabled = auto && Settings.SunMode;
            zonesAnchor.Enabled = zonesScale.Enabled = Settings.Zones.Count > 0;

            HashSet<int> failed = failedHotkeys();
            for (int i = 0; i < Shortcuts.Count; i++) hotkeyStatus[i].Text = failed.Contains(i) ? T.S("hkBusy") : "";
            folderLabel.Text = Settings.Folder;

            int open = manager.Count;
            footer.SetStatus(Settings.Enabled ? string.Format(T.S("statusOn"), open) : T.S("statusOff"), Settings.Enabled);
            if (preview != null) preview.Invalidate();
        }

        public void RefreshUpdate()
        {
            switch (Updates.Status)
            {
                case Updates.State.Checking: updateLabel.Text = T.S("updChecking"); break;
                case Updates.State.UpToDate: updateLabel.Text = T.S("updLatest"); break;
                case Updates.State.Newer: updateLabel.Text = string.Format(T.S("updNew"), Updates.Latest); break;
                case Updates.State.Failed: updateLabel.Text = T.S("updFail"); break;
                default: updateLabel.Text = ""; break;
            }
            updateLabel.ForeColor = Updates.Status == Updates.State.Newer ? Theme.GreenHi : Theme.Muted;
            updateGet.Visible = Updates.Status == Updates.State.Newer;
            if (updateGet.Visible) updateGet.Text = string.Format(T.S("updSee"), Updates.Latest);
        }

        void ShowIconChoices()
        {
            foreach (IconChoice c in iconChoices)
            {
                c.ChooseShade = !Settings.TrayIconAuto;
                c.Shade = Settings.TrayIconColor;
                c.Selected = c.Style == Settings.TrayIcon;
            }
        }

        void Commit()
        {
            changed();
            UpdateLabels();
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
                    Settings.Zones = editor.Result();
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
        static readonly int[] IntensitySteps = { 40, 50, 60, 70, 80, 90, 100 };

        public static Icon AppIcon, WindowIcon;

        enum TrayState { Off, Day, Twilight, Night }

        readonly NotifyIcon tray = new NotifyIcon();
        readonly OverlayManager overlays;
        readonly HotkeyWindow hotkeys;
        readonly Timer stateTimer;
        readonly Dictionary<int, Icon> trayIcons = new Dictionary<int, Icon>();
        readonly HashSet<int> failedHotkeys = new HashSet<int>();
        string registeredHotkeys = "";
        TrayState? currentState;
        int currentIconKey = -1;
        ToolStripMenuItem enableItem, autoItem, presetMenu, intensityMenu, vignetteMenu, accountsMenu, updateItem;
        readonly Timer updateTimer;
        readonly Timer pauseTimer = new Timer { Interval = 1000 };
        DateTime pauseUntil;
        ToolStripMenuItem pauseMenu, pauseInfo, resumeItem;
        string announced;   // version already announced in a balloon
        bool balloonIsUpdate;
        SettingsForm settingsForm;
        bool warnedShader;

        public OverlayManager Manager { get { return overlays; } }
        public HashSet<int> FailedHotkeys { get { return failedHotkeys; } }
        public string StateName { get { return currentState.HasValue ? currentState.Value.ToString() : "-"; } }
        public int IconKey { get { return currentIconKey; } }
        public event Action<int> CommandReceived;
        public void RunCommand(int cmd) { Command(cmd); }
        public void NotifyChanged() { Changed(); }

        public TrayApp()
        {
            AppIcon = LoadIcon(SystemInformation.SmallIconSize);
            WindowIcon = LoadIcon(SystemInformation.IconSize);
            ToolStripManager.Renderer = new GameMenuRenderer();
            overlays = new OverlayManager();
            BuildMenu();
            tray.Icon = AppIcon;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Command(Shortcuts.Toggle); };
            tray.Visible = true;
            overlays.CountChanged += (s, e) =>
            {
                RefreshUi();
                if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshAccounts();
            };

            hotkeys = new HotkeyWindow(id => Command(id - 1), OpenSettings);
            RegisterHotkeys();

            // The icon and the tooltip follow the automatic mode.
            stateTimer = new Timer { Interval = 30000 };
            stateTimer.Tick += (s, e) => RefreshUi();
            stateTimer.Start();

            pauseTimer.Tick += (s, e) => { if (DateTime.Now >= pauseUntil) Resume(); };
            T.LanguageChanged += OnLanguageChanged;

            // Update check: a moment after start, then once a day (never in the self-test).
            Updates.Changed += OnUpdates;
            tray.BalloonTipClicked += (s, e) => { if (balloonIsUpdate) Updates.OpenPage(); };
            updateTimer = new Timer { Interval = 15000 };
            updateTimer.Tick += (s, e) =>
            {
                updateTimer.Interval = 24 * 60 * 60 * 1000;
                if (Settings.CheckUpdates && !Program.SelfTestMode) Updates.Check();
            };
            updateTimer.Start();

            RefreshUi();
            string tip = string.Format(T.S("help"), Shortcuts.Format(Settings.Hotkeys[Shortcuts.Toggle]), Shortcuts.Format(Settings.Hotkeys[Shortcuts.Peek]));
            if (failedHotkeys.Count > 0) tip += "\n" + T.S("noHotkeys") + FailedNames();
            balloonIsUpdate = false;
            tray.ShowBalloonTip(5000, "Pénombre " + Program.Version, tip, ToolTipIcon.None);
        }

        // Fades the filter out for a while, without touching the settings (nothing is saved).
        public void Pause(int minutes)
        {
            pauseUntil = DateTime.Now.AddMinutes(minutes);
            overlays.Paused = true;
            pauseTimer.Start();
            Log.Info("Paused for " + minutes + " min");
            overlays.ApplyLook();
            RefreshUi();
        }

        public void Resume()
        {
            if (!overlays.Paused) return;
            overlays.Paused = false;
            pauseTimer.Stop();
            overlays.ApplyLook();
            RefreshUi();
        }

        // New language: the menu and the settings window are built again with the new texts.
        void OnLanguageChanged()
        {
            ContextMenuStrip old = tray.ContextMenuStrip;
            BuildMenu();
            if (old != null) old.Dispose();
            RefreshUi();
            OnUpdates();
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                SettingsForm form = settingsForm;
                Point where = form.Location;
                int page = form.CurrentPage;
                form.BeginInvoke((Action)(() =>
                {
                    form.Close();
                    form.Dispose();
                    OpenSettings();
                    settingsForm.StartPosition = FormStartPosition.Manual;
                    settingsForm.Location = where;
                    settingsForm.SelectPage(page);
                }));
            }
        }

        void OnUpdates()
        {
            bool newer = Updates.Status == Updates.State.Newer;
            updateItem.Visible = newer;
            if (newer)
            {
                updateItem.Text = string.Format(T.S("updMenu"), Updates.Latest);
                if (announced != Updates.Latest)
                {
                    announced = Updates.Latest;
                    Balloon(string.Format(T.S("updBalloon"), Updates.Latest), true);
                }
            }
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshUpdate();
        }

        void Balloon(string text, bool update)
        {
            balloonIsUpdate = update;
            tray.ShowBalloonTip(update ? 8000 : 4000, "Pénombre", text, ToolTipIcon.None);
        }

        void RegisterHotkeys()
        {
            string signature = string.Join(",", Array.ConvertAll(Settings.Hotkeys, k => ((int)k).ToString()));
            if (signature == registeredHotkeys) return;
            registeredHotkeys = signature;
            hotkeys.UnregisterAll();
            failedHotkeys.Clear();
            for (int i = 0; i < Shortcuts.Count; i++)
                if (!hotkeys.Register(i + 1, Settings.Hotkeys[i])) failedHotkeys.Add(i);
            if (failedHotkeys.Count > 0) Log.Info("Shortcuts unavailable: " + FailedNames());
        }

        string FailedNames()
        {
            var names = new List<string>();
            foreach (int i in failedHotkeys) names.Add(Shortcuts.Format(Settings.Hotkeys[i]));
            return string.Join(", ", names.ToArray());
        }

        // Shortcuts change the settings of the Dofus in the foreground when it has its own settings.
        void Command(int cmd)
        {
            if (CommandReceived != null) CommandReceived(cmd);
            AccountProfile own = Settings.Profile(overlays.ForegroundAccount(), false);
            if (own != null && !own.Own) own = null;
            if (overlays.Paused && cmd == Shortcuts.Toggle) { Resume(); Settings.Enabled = true; Changed(); RefreshSettings(); return; }
            switch (cmd)
            {
                case Shortcuts.Toggle: Settings.Enabled = !Settings.Enabled; break;
                case Shortcuts.Auto: Settings.Auto = !Settings.Auto; Settings.Enabled = true; break;
                case Shortcuts.Peek: overlays.StartPeek(Settings.Hotkeys[Shortcuts.Peek]); return;
                case Shortcuts.Up:
                    if (own != null) own.Intensity = Settings.ClampIntensity(own.Intensity + 5);
                    else Settings.Intensity = Settings.ClampIntensity(Settings.Intensity + 5);
                    Settings.Enabled = true;
                    break;
                case Shortcuts.Down:
                    if (own != null) own.Intensity = Settings.ClampIntensity(own.Intensity - 5);
                    else Settings.Intensity = Settings.ClampIntensity(Settings.Intensity - 5);
                    Settings.Enabled = true;
                    break;
                case Shortcuts.Vignette:
                    if (own != null) own.Vignette = (own.Vignette + 1) % 4;
                    else Settings.Vignette = (Settings.Vignette + 1) % 4;
                    Settings.Enabled = true;
                    break;
                case Shortcuts.Preset:
                    if (own != null) own.Preset = (own.Preset + 1) % Preset.Count;
                    else Settings.PresetIndex = (Settings.PresetIndex + 1) % Preset.Count;
                    Settings.Enabled = true;
                    break;
                default: return;
            }
            Changed();
            RefreshSettings();
        }

        void Changed()
        {
            if (overlays.Paused && Settings.Enabled) Resume(); // any change shows the filter again
            RegisterHotkeys();
            overlays.ApplyLook();
            Settings.SaveSoon();
            RefreshUi();
        }

        void OpenSettings()
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm(overlays, Changed, () => failedHotkeys);
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
            enableItem.Click += (s, e) => Command(Shortcuts.Toggle);

            autoItem = new ToolStripMenuItem(T.S("auto"));
            autoItem.Click += (s, e) => Command(Shortcuts.Auto);

            presetMenu = new ToolStripMenuItem(T.S("mood"));
            for (int i = 0; i < Preset.Count; i++)
            {
                int idx = i;
                var it = new ToolStripMenuItem(Preset.NameOf(i));
                it.Click += (s, e) => { Settings.PresetIndex = idx; Settings.Enabled = true; Changed(); RefreshSettings(); };
                presetMenu.DropDownItems.Add(it);
            }

            intensityMenu = new ToolStripMenuItem(T.S("intensity"));
            foreach (int v in IntensitySteps)
            {
                int val = v;
                var it = new ToolStripMenuItem(val + " %");
                it.Tag = val;
                it.Click += (s, e) => { Settings.Intensity = val; Settings.Enabled = true; Changed(); RefreshSettings(); };
                intensityMenu.DropDownItems.Add(it);
            }

            vignetteMenu = new ToolStripMenuItem(T.S("vignette"));
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var it = new ToolStripMenuItem(T.S("v" + i));
                it.Click += (s, e) => { Settings.Vignette = idx; Settings.Enabled = true; Changed(); RefreshSettings(); };
                vignetteMenu.DropDownItems.Add(it);
            }

            pauseMenu = new ToolStripMenuItem(T.S("pause"));
            pauseInfo = new ToolStripMenuItem { Enabled = false, Visible = false };
            resumeItem = new ToolStripMenuItem(T.S("resume")) { Visible = false };
            resumeItem.Click += (s, e) => Resume();
            pauseMenu.DropDownItems.Add(pauseInfo);
            pauseMenu.DropDownItems.Add(resumeItem);
            foreach (int minutes in new[] { 15, 30, 60, 120 })
            {
                int min = minutes;
                var it = new ToolStripMenuItem(string.Format(T.S("pauseFor"), min < 60 ? min + " min" : min / 60 + " h"));
                it.Click += (s, e) => Pause(min);
                pauseMenu.DropDownItems.Add(it);
            }

            accountsMenu = new ToolStripMenuItem(T.S("accounts"));
            accountsMenu.DropDownItems.Add(new ToolStripMenuItem(T.S("noAccount")) { Enabled = false });
            accountsMenu.DropDownOpening += (s, e) => FillAccounts();

            var settings = new ToolStripMenuItem(T.S("settings"));
            settings.Font = Theme.Strong(9.5f);
            settings.Click += (s, e) => OpenSettings();

            var diag = new ToolStripMenuItem(T.S("diag"));
            diag.Click += (s, e) =>
            {
                try { Clipboard.SetText(overlays.Diagnostics()); balloonIsUpdate = false; tray.ShowBalloonTip(2000, "Pénombre", T.S("diagDone"), ToolTipIcon.None); }
                catch (Exception ex) { Log.Error("Diagnostics", ex); }
            };

            var log = new ToolStripMenuItem(T.S("openLog"));
            log.Click += (s, e) => OpenLog();

            var quit = new ToolStripMenuItem(T.S("quit"));
            quit.Click += (s, e) => Application.Exit();

            updateItem = new ToolStripMenuItem { Visible = false, ForeColor = Theme.GreenHi };
            updateItem.Click += (s, e) => Updates.OpenPage();
            menu.Items.Add(updateItem);

            menu.Items.Add(enableItem);
            menu.Items.Add(autoItem);
            menu.Items.Add(pauseMenu);
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
            menu.Opening += (s, e) => RefreshUi();
            menu.Font = Theme.Body(9.5f);
            settings.Font = Theme.Strong(9.5f);
            updateItem.Font = Theme.Strong(9.5f);
            foreach (ToolStripMenuItem sub in new[] { pauseMenu, presetMenu, intensityMenu, vignetteMenu, accountsMenu }) sub.DropDown.Font = menu.Font;
            tray.ContextMenuStrip = menu;
        }

        void RefreshSettings()
        {
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.LoadAll();
        }

        void FillAccounts()
        {
            accountsMenu.DropDownItems.Clear();
            var names = overlays.Accounts().FindAll(kv => kv.Value);
            if (names.Count == 0)
            {
                accountsMenu.DropDownItems.Add(new ToolStripMenuItem(T.S("noAccount")) { Enabled = false });
                return;
            }
            foreach (var kv in names)
            {
                string n = kv.Key;
                AccountProfile p = Settings.Profile(n, false);
                var it = new ToolStripMenuItem(n + (p != null && p.Own ? " (" + T.S("ownSettings") + ")" : ""))
                {
                    Checked = p == null || p.Filter,
                };
                it.Click += (s, e) =>
                {
                    AccountProfile prof = Settings.Profile(n, true);
                    prof.Filter = !prof.Filter;
                    Changed();
                    if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.RefreshAccounts();
                };
                accountsMenu.DropDownItems.Add(it);
            }
        }

        void RefreshUi()
        {
            float f = Settings.Auto ? DayCycle.Factor(DateTime.Now) : 1;
            string state;
            if (overlays.Paused) state = string.Format(T.S("pauseUntil"), pauseUntil.ToString("HH:mm"));
            else if (!Settings.Enabled) state = T.S("off");
            else
            {
                state = Preset.NameOf(Settings.PresetIndex) + " " + Settings.Intensity + " %";
                if (Settings.Auto) state += " · auto " + (int)Math.Round(f * 100) + " %";
            }
            string text = "Pénombre : " + state + " · " + overlays.Count + " " + T.S("windows");
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;

            TrayState now = !Settings.Enabled || overlays.Paused ? TrayState.Off : f <= 0 ? TrayState.Day : f >= 1 ? TrayState.Night : TrayState.Twilight;
            currentState = now;
            // The icon shows the sky of the schedule (Horaires), even when the filter does not follow it.
            float sky = DayCycle.Factor(DateTime.Now);
            TrayState skyState = sky <= 0 ? TrayState.Day : sky >= 1 ? TrayState.Night : TrayState.Twilight;
            int picture = !Settings.Enabled || overlays.Paused ? TrayIcons.Off : !Settings.TrayIconAuto ? Settings.TrayIconColor : StateOf(skyState);
            int key = Settings.TrayIcon * TrayIcons.StateCount + picture;
            if (key != currentIconKey)
            {
                Icon icon;
                if (!trayIcons.TryGetValue(key, out icon))
                {
                    icon = TrayIcons.Make(Settings.TrayIcon, picture, SystemInformation.SmallIconSize.Width) ?? AppIcon;
                    trayIcons[key] = icon;
                }
                currentIconKey = key;
                tray.Icon = icon;
            }

            enableItem.Checked = Settings.Enabled && !overlays.Paused;
            pauseMenu.Checked = overlays.Paused;
            pauseInfo.Visible = resumeItem.Visible = overlays.Paused;
            if (overlays.Paused) pauseInfo.Text = string.Format(T.S("pauseUntil"), pauseUntil.ToString("HH:mm"));
            autoItem.Checked = Settings.Auto;
            enableItem.ShortcutKeyDisplayString = HotkeyText(Shortcuts.Toggle);
            autoItem.ShortcutKeyDisplayString = HotkeyText(Shortcuts.Auto);
            presetMenu.ShortcutKeyDisplayString = HotkeyText(Shortcuts.Preset);
            vignetteMenu.ShortcutKeyDisplayString = HotkeyText(Shortcuts.Vignette);
            for (int i = 0; i < presetMenu.DropDownItems.Count; i++) ((ToolStripMenuItem)presetMenu.DropDownItems[i]).Checked = i == Settings.PresetIndex;
            for (int i = 0; i < vignetteMenu.DropDownItems.Count; i++) ((ToolStripMenuItem)vignetteMenu.DropDownItems[i]).Checked = i == Settings.Vignette;
            foreach (ToolStripMenuItem it in intensityMenu.DropDownItems) it.Checked = (int)it.Tag == Settings.Intensity;

            if (Settings.Shader && (overlays.ShaderFailed || !Mag.Ready) && !warnedShader)
            {
                warnedShader = true;
                balloonIsUpdate = false;
                tray.ShowBalloonTip(4000, "Pénombre", T.S("noShader"), ToolTipIcon.Warning);
            }
        }

        static string HotkeyText(int i)
        {
            return Shortcuts.IsValid(Settings.Hotkeys[i]) ? Shortcuts.Format(Settings.Hotkeys[i]) : "";
        }

        static int StateOf(TrayState s)
        {
            switch (s)
            {
                case TrayState.Night: return TrayIcons.Night;
                case TrayState.Twilight: return TrayIcons.Twilight;
                case TrayState.Day: return TrayIcons.Day;
                default: return TrayIcons.Off;
            }
        }

        // size: SmallIconSize for the notification area, IconSize for the windows (taskbar, Alt+Tab):
        // each size has its own drawing in the .ico, so the icon stays sharp.
        static Icon LoadIcon(Size size)
        {
            try
            {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("penombre.ico"))
                    if (s != null) return new Icon(s, size);
            }
            catch { }
            return SystemIcons.Application;
        }

        public void Dispose()
        {
            stateTimer.Dispose();
            updateTimer.Dispose();
            pauseTimer.Dispose();
            T.LanguageChanged -= OnLanguageChanged;
            Updates.Changed -= OnUpdates;
            if (settingsForm != null && !settingsForm.IsDisposed) settingsForm.Dispose();
            hotkeys.Dispose();
            overlays.Dispose();
            tray.Visible = false;
            tray.Dispose();
            foreach (Icon i in trayIcons.Values) if (i != AppIcon) i.Dispose();
        }
    }
}
