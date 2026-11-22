using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SkillPeek
{
    internal static class Native
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr icon);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (Mutex mtx = new Mutex(true, "SkillPeek.SingleInstance.7f3a", out created))
            {
                if (!created) { return; }
                Native.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                // 不把 Form 交给 Run/ApplicationContext，否则面板会在启动时就显示出来
                using (PeekForm form = new PeekForm())
                {
                    Application.Run(new ApplicationContext());
                }
            }
        }
    }

    internal sealed class PeekForm : Form
    {
        private const int HOTKEY_ID = 0x5EEC;
        private const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8;
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_NOREPEAT = 0x4000;

        private SkillPanel panel;
        private readonly NotifyIcon tray;
        private readonly System.Windows.Forms.Timer holdTimer;
        private readonly System.Windows.Forms.Timer pasteTimer;
        private ToolStripMenuItem[] hkPresets = new ToolStripMenuItem[0];

        private string settingsPath;
        private string mode = "once";
        private uint hkMods;
        private uint hkVk;
        private IntPtr previousWindow = IntPtr.Zero;
        private IntPtr pendingPasteWindow;
        private int pasteStarted;
        private bool pasteEnabled = true;
        private string[] skillsRoots = new string[0];

        public PeekForm()
        {
            Text = "Skill Peek";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.White;
            KeyPreview = true;
            AutoScaleMode = AutoScaleMode.None;

            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SkillPeek");
            Directory.CreateDirectory(appData);
            settingsPath = Path.Combine(appData, "settings.ini");

            LoadSettings();

            tray = new NotifyIcon();
            tray.Icon = BuildTrayIcon();
            tray.Text = "Skill Peek";
            tray.Visible = true;
            tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) { TogglePanel(); }
            };

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("打开面板", null, delegate { ShowPanel(); });

            ToolStripMenuItem hkRoot = new ToolStripMenuItem("快捷键");
            hkPresets = new ToolStripMenuItem[] {
                MakeHotkeyItem("Alt+`", MOD_ALT, 0xC0),
                MakeHotkeyItem("Ctrl+Shift+Space", MOD_CONTROL | MOD_SHIFT, 0x20),
                MakeHotkeyItem("Ctrl+Alt+Space", MOD_CONTROL | MOD_ALT, 0x20),
                MakeHotkeyItem("Alt+Shift+K", MOD_ALT | MOD_SHIFT, 0x4B),
                MakeHotkeyItem("F8", 0, 0x77),
                MakeHotkeyItem("F9", 0, 0x78)
            };
            for (int i = 0; i < hkPresets.Length; i++) { hkRoot.DropDownItems.Add(hkPresets[i]); }
            menu.Items.Add(hkRoot);
            RefreshHotkeyChecks();

            menu.Items.Add("切换 按住 / 一次", null, delegate { ToggleMode(); });

            menu.Items.Add("重新扫描 skill", null, delegate {
                RefreshSkills();
                tray.ShowBalloonTip(1500, "Skill Peek", "已重新扫描本地 skill", ToolTipIcon.Info);
            });

            ToolStripMenuItem pasteItem = new ToolStripMenuItem("回车直接粘贴到原窗口");
            pasteItem.CheckOnClick = true;
            pasteItem.Checked = pasteEnabled;
            pasteItem.CheckedChanged += delegate { pasteEnabled = pasteItem.Checked; SaveSettings(); };
            menu.Items.Add(pasteItem);
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem auto = new ToolStripMenuItem("开机自启");
            auto.CheckOnClick = true;
            auto.Checked = IsAutoStart();
            auto.CheckedChanged += delegate { SetAutoStart(auto.Checked); };
            menu.Items.Add(auto);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Application.Exit(); });
            tray.ContextMenuStrip = menu;

            holdTimer = new System.Windows.Forms.Timer();
            holdTimer.Interval = 40;
            holdTimer.Tick += delegate { CheckHold(); };
            pasteTimer = new System.Windows.Forms.Timer { Interval = 15 };
            pasteTimer.Tick += delegate { PasteWhenFocused(); };
            // 面板启动时隐藏，仍需创建窗口句柄以注册全局热键。
            IntPtr forceHandle = Handle;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int preference = 2; // DWMWCP_ROUND
            try { Native.DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int)); }
            catch (DllNotFoundException) { }
            RegisterHotkey();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                if (IsHandleCreated) { Native.UnregisterHotKey(Handle, HOTKEY_ID); }
                if (holdTimer != null) { holdTimer.Dispose(); }
                if (pasteTimer != null) { pasteTimer.Dispose(); }
                if (tray != null)
                {
                    tray.Visible = false;
                    Icon icon = tray.Icon;
                    if (tray.ContextMenuStrip != null) { tray.ContextMenuStrip.Dispose(); }
                    tray.Dispose();
                    if (icon != null) { icon.Dispose(); }
                }
            }
            base.Dispose(disposing);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (panel != null && panel.HandleKey(keyData)) { return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                if (mode == "hold") { ShowPanel(); }
                else { TogglePanel(); }
                return;
            }
            base.WndProc(ref m);
        }

        /* ── 显示 / 隐藏 ──────────────────────────────────────── */

        private void TogglePanel()
        {
            if (Visible) { HidePanel(); } else { ShowPanel(); }
        }

        private void ShowPanel()
        {
            if (Visible) { return; }
            pasteTimer.Stop();
            pendingPasteWindow = IntPtr.Zero;
            previousWindow = Native.GetForegroundWindow();
            Rectangle work = Screen.FromHandle(previousWindow).WorkingArea;
            if (panel == null)
            {
                float scale;
                using (Graphics graphics = CreateGraphics()) { scale = graphics.DpiX / 96f; }
                panel = new SkillPanel(scale, Pick, RefreshSkills, SetMode, HidePanel);
                panel.Dock = DockStyle.Fill;
                Controls.Add(panel);
            }

            panel.SetMode(mode);
            int width = Math.Min(panel.PreferredPanelWidth, (int)(work.Width * 0.94));
            ClientSize = new Size(width, panel.PreferredPanelHeight(width, (int)(work.Height * 0.92)));
            Location = new Point(work.Left + (work.Width - Width) / 2, work.Top + (work.Height - Height) / 2);
            panel.Reset(ScanSkills());
            Show();
            if (Native.GetForegroundWindow() != Handle) { Native.SetForegroundWindow(Handle); }
            panel.FocusSelection();
            UpdateHoldTimer();
        }

        private void HidePanel()
        {
            if (!Visible) { return; }
            holdTimer.Stop();
            Hide();
            if (previousWindow != IntPtr.Zero)
            {
                Native.SetForegroundWindow(previousWindow);
                previousWindow = IntPtr.Zero;
            }
        }

        private void Pick(string name)
        {
            try { Clipboard.SetText("@" + name); }
            catch (ExternalException) { panel.ShowNotice("剪贴板正忙，请再试一次"); return; }
            IntPtr target = previousWindow;
            HidePanel();
            if (pasteEnabled && target != IntPtr.Zero)
            {
                pendingPasteWindow = target;
                pasteStarted = Environment.TickCount;
                BeginInvoke(new Action(PasteWhenFocused));
            }
        }

        private void RefreshSkills()
        {
            if (panel != null) { panel.SetInstalled(ScanSkills()); }
        }

        /* 把焦点还给原窗口，再发一次 Ctrl+V */
        private void PasteWhenFocused()
        {
            if (pendingPasteWindow != IntPtr.Zero)
            {
                if (Native.GetForegroundWindow() == pendingPasteWindow)
                {
                    Native.keybd_event(0x11, 0, 0, UIntPtr.Zero);   // Ctrl down
                    Native.keybd_event(0x56, 0, 0, UIntPtr.Zero);   // V down
                    Native.keybd_event(0x56, 0, 2, UIntPtr.Zero);   // V up
                    Native.keybd_event(0x11, 0, 2, UIntPtr.Zero);   // Ctrl up
                }
                else if (unchecked(Environment.TickCount - pasteStarted) < 100)
                {
                    pasteTimer.Start();
                    return;
                }
                pendingPasteWindow = IntPtr.Zero;
            }
            pasteTimer.Stop();
        }

        private void CheckHold()
        {
            if (mode != "hold" || !Visible) { return; }
            if ((Native.GetAsyncKeyState((int)hkVk) & 0x8000) == 0) { HidePanel(); }
        }
        private void UpdateHoldTimer()
        {
            holdTimer.Enabled = mode == "hold" && Visible;
        }

        /* ── 热键 ─────────────────────────────────────────────── */

        private void RegisterHotkey()
        {
            if (!Native.RegisterHotKey(Handle, HOTKEY_ID, hkMods | MOD_NOREPEAT, hkVk))
            {
                tray.ShowBalloonTip(4000, "Skill Peek",
                    "热键 " + HotkeyName() + " 注册失败，可能被别的程序占用。\n" +
                    "可以改 %APPDATA%\\SkillPeek\\settings.ini 里的 hotkey 后重启。",
                    ToolTipIcon.Warning);
            }
        }

        private ToolStripMenuItem MakeHotkeyItem(string label, uint mods, uint vk)
        {
            ToolStripMenuItem it = new ToolStripMenuItem(label);
            it.Tag = mods + "|" + vk;
            it.Click += delegate { TrySetHotkey(mods, vk, label); };
            return it;
        }

        private void RefreshHotkeyChecks()
        {
            for (int i = 0; i < hkPresets.Length; i++)
            {
                ToolStripMenuItem it = hkPresets[i];
                string[] p = ((string)it.Tag).Split('|');
                it.Checked = (uint.Parse(p[0]) == hkMods && uint.Parse(p[1]) == hkVk);
            }
        }

        /* 换热键：先退掉旧的，注册失败就退回原来的 */
        private void TrySetHotkey(uint mods, uint vk, string label)
        {
            if (mods == hkMods && vk == hkVk) { return; }
            Native.UnregisterHotKey(Handle, HOTKEY_ID);
            if (!Native.RegisterHotKey(Handle, HOTKEY_ID, mods | MOD_NOREPEAT, vk))
            {
                Native.RegisterHotKey(Handle, HOTKEY_ID, hkMods | MOD_NOREPEAT, hkVk);
                tray.ShowBalloonTip(3000, "Skill Peek",
                    label + " 被别的程序占用了，还是用 " + HotkeyName(), ToolTipIcon.Warning);
                return;
            }
            hkMods = mods;
            hkVk = vk;
            SaveSettings();
            RefreshHotkeyChecks();
            tray.ShowBalloonTip(1800, "Skill Peek", "快捷键已改成 " + label, ToolTipIcon.Info);
        }

        private static uint KeyToVk(string p)
        {
            if (p.Length == 1)
            {
                char c = char.ToUpperInvariant(p[0]);
                if (c >= 'A' && c <= 'Z') { return (uint)c; }
                if (c >= '0' && c <= '9') { return (uint)c; }
                if (c == '`') { return 0xC0; }
            }
            if (p == "space") { return 0x20; }
            if (p == "tab") { return 0x09; }
            if (p == "escape" || p == "esc") { return 0x1B; }
            if (p.Length >= 2 && p[0] == 'f')
            {
                int n;
                if (int.TryParse(p.Substring(1), out n) && n >= 1 && n <= 12)
                {
                    return (uint)(0x70 + n - 1);
                }
            }
            return 0;
        }

        private static void ParseHotkey(string s, out uint mods, out uint vk)
        {
            mods = 0;
            vk = 0;
            string[] parts = s.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim().ToLowerInvariant();
                if (p == "ctrl" || p == "control") { mods |= MOD_CONTROL; }
                else if (p == "alt") { mods |= MOD_ALT; }
                else if (p == "shift") { mods |= MOD_SHIFT; }
                else if (p == "win") { mods |= MOD_WIN; }
                else if (p.Length > 0) { vk = KeyToVk(p); }
            }
            if (vk == 0) { vk = 0x20; mods = MOD_CONTROL | MOD_SHIFT; }
        }

        /* ── 配置 / 托盘 ──────────────────────────────────────── */

        private void LoadSettings()
        {
            string hotkey = "Ctrl+Shift+Space";
            string roots = "%USERPROFILE%\\.dsh\\skills;%USERPROFILE%\\.claude\\skills";
            if (File.Exists(settingsPath))
            {
                string[] lines = File.ReadAllLines(settingsPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string t = lines[i].Trim();
                    if (t.StartsWith("mode=")) { mode = t.Substring(5).Trim(); }
                    else if (t.StartsWith("hotkey=")) { hotkey = t.Substring(7).Trim(); }
                    else if (t.StartsWith("paste=")) { pasteEnabled = t.Substring(6).Trim() != "0"; }
                    else if (t.StartsWith("skills=")) { roots = t.Substring(7).Trim(); }
                }
            }
            if (mode != "hold" && mode != "once") { mode = "once"; }
            ParseHotkey(hotkey, out hkMods, out hkVk);
            LoadSkillsRoots(roots);
        }

        private void LoadSkillsRoots(string raw)
        {
            System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
            string[] parts = raw.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) { continue; }
                p = Environment.ExpandEnvironmentVariables(p);
                list.Add(p);
            }
            skillsRoots = list.ToArray();
        }

        /* 扫本地 skill 目录：只看一级子目录名 */
        private HashSet<string> ScanSkills()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in skillsRoots)
            {
                try
                {
                    foreach (string dir in Directory.EnumerateDirectories(root))
                    {
                        names.Add(Path.GetFileName(dir));
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (ArgumentException) { }
                catch (System.Security.SecurityException) { }
            }
            return names;
        }

        private void SaveSettings()
        {
            try
            {
                var lines = new List<string>();
                if (File.Exists(settingsPath)) { lines.AddRange(File.ReadAllLines(settingsPath)); }
                lines.RemoveAll(delegate(string line)
                {
                    string key = line.TrimStart();
                    return key.StartsWith("mode=") || key.StartsWith("hotkey=") || key.StartsWith("paste=");
                });
                lines.Add("mode=" + mode);
                lines.Add("hotkey=" + HotkeyName());
                lines.Add("paste=" + (pasteEnabled ? "1" : "0"));
                File.WriteAllLines(settingsPath, lines);
            }
            catch { }
        }

        private string HotkeyName()
        {
            string s = "";
            if ((hkMods & MOD_CONTROL) != 0) { s += "Ctrl+"; }
            if ((hkMods & MOD_ALT) != 0) { s += "Alt+"; }
            if ((hkMods & MOD_SHIFT) != 0) { s += "Shift+"; }
            if ((hkMods & MOD_WIN) != 0) { s += "Win+"; }
            if (hkVk == 0x20) { s += "Space"; }
            else if (hkVk == 0xC0) { s += "`"; }
            else if (hkVk >= 0x70 && hkVk <= 0x7B) { s += "F" + (hkVk - 0x70 + 1); }
            else { s += ((char)hkVk).ToString(); }
            return s;
        }

        private void ToggleMode()
        {
            SetMode(mode == "hold" ? "once" : "hold");
        }

        private void SetMode(string value)
        {
            mode = value;
            SaveSettings();
            if (panel != null) { panel.SetMode(value); }
            UpdateHoldTimer();
        }

        private static Icon BuildTrayIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(22, 163, 74)))
                    {
                        g.FillEllipse(bg, 0, 0, 31, 31);
                    }
                    using (SolidBrush fg = new SolidBrush(Color.White))
                    {
                        Point[] bolt = new Point[] {
                            new Point(18, 5), new Point(9, 18), new Point(15, 18),
                            new Point(13, 27), new Point(23, 14), new Point(17, 14)
                        };
                        g.FillPolygon(fg, bolt);
                    }
                }
                IntPtr handle = bmp.GetHicon();
                try
                {
                    using (Icon icon = Icon.FromHandle(handle)) { return (Icon)icon.Clone(); }
                }
                finally { Native.DestroyIcon(handle); }
            }
        }

        private const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private static bool IsAutoStart()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RUN_KEY))
                {
                    return k != null && k.GetValue("SkillPeek") != null;
                }
            }
            catch (System.Security.SecurityException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static void SetAutoStart(bool on)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RUN_KEY))
                {
                    if (on) { k.SetValue("SkillPeek", "\"" + Application.ExecutablePath + "\""); }
                    else { k.DeleteValue("SkillPeek", false); }
                }
            }
            catch { }
        }
    }
}
