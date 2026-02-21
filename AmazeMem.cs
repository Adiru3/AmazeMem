using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Management;
using System.Security.Principal;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Timer = System.Windows.Forms.Timer;

namespace AmazingMemoryFixer
{
    public partial class Form1 : Form
    {
        private const string CurrentVersion = "3.0";
        private static bool startMinimized = false;
        private static string targetFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AmazeMem");
        private static string targetPath = Path.Combine(targetFolder, "AmazeMem.exe");
        private static string logFilePath = Path.Combine(targetFolder, "cleaner.log");
        private bool isSettingsLoading = false;

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out long lpLuid);
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES { public int PrivilegeCount; public long Luid; public int Attributes; }

        [DllImport("ntdll.dll")]
        static extern uint NtSetSystemInformation(int InfoClass, IntPtr Info, int Length);
        [DllImport("psapi.dll")]
        static extern bool EmptyWorkingSet(IntPtr hProcess);

        const int SystemMemoryListInformation = 80;
        const int SystemFileCacheInformation = 21;
        const int MemoryPurgeStandbyList = 4;
        const int MemoryEmptyWorkingSets = 2;
        const int MemoryFlushModifiedList = 3;

        [StructLayout(LayoutKind.Sequential)]
        struct SYSTEM_FILECACHE_INFORMATION
        {
            public IntPtr MinimumWorkingSet;
            public IntPtr MaximumWorkingSet;
        }

        private NotifyIcon trayIcon;
        private Timer autoTimer;
        private Timer logUpdateTimer;
        private DateTime lastCleanTime = DateTime.MinValue;
        private PerformanceCounter ramCounter;

        private TextBox txtLog = new TextBox();
        private CheckBox chkWS = new CheckBox() { Checked = true, Location = new Point(15, 50), AutoSize = true };
        private CheckBox chkSL = new CheckBox() { Checked = true, Location = new Point(15, 75), AutoSize = true };
        private CheckBox chkML = new CheckBox() { Checked = true, Location = new Point(15, 100), AutoSize = true };
        private CheckBox chkP0 = new CheckBox() { Checked = true, Location = new Point(15, 125), AutoSize = true };
        private CheckBox chkAutoRam = new CheckBox() { Checked = true, AutoSize = true };
        private CheckBox chkAutoTime = new CheckBox() { Checked = true, AutoSize = true };
        private TrackBar trkPercent = new TrackBar() { Minimum = 0, Maximum = 100, Value = 90, Location = new Point(160, 75), Size = new Size(150, 45) };
        private Label lblPercent = new Label() { Location = new Point(165, 55), AutoSize = true };
        private TextBox txtInterval = new TextBox() { Text = "60", Location = new Point(340, 75), Size = new Size(40, 20) };
        private Label lblInterval = new Label() { Location = new Point(340, 55), AutoSize = true };
        private ComboBox comboTheme = new ComboBox() { Size = new Size(120, 25), DropDownStyle = ComboBoxStyle.DropDownList };
        private ComboBox comboLang = new ComboBox() { Location = new Point(310, 12), Size = new Size(110, 25), DropDownStyle = ComboBoxStyle.DropDownList };
        private Button btnManual = new Button() { Location = new Point(15, 10), Size = new Size(130, 30), BackColor = Color.FromArgb(60, 60, 60) };
        private Button btnGithub = new Button() { Text = "GitHub", Location = new Point(220, 10), Size = new Size(80, 30), BackColor = Color.FromArgb(45, 45, 45), FlatStyle = FlatStyle.Flat };

        private Panel titleBar;
        private Panel sidebar;
        private Panel content;
        private Label lblTitle;
        private Button btnClose;
        private Button btnDonate;

        private Panel loadingOverlay;
        private Timer spinTimer;
        private float spinAngle = 0;

        // --- WINAMP THEME VARIABLES ---
        private Panel winampPanel;
        private Timer winampTimer;
        private int winampMarqueeOffset = 0;
        private int[] winampEqBars = new int[19];
        private Random rand = new Random();
        private bool winampPlaying = false;
        private int winampSeconds = 0;
        private string winampCurrentSong = "1. Amaze Mem - System Cleaning (No Track Loaded) ***";
        private List<string> winampPlaylist = new List<string>();
        private List<string> winampFilePaths = new List<string>();
        private int winampSelectedIndex = 0;
        private int winampScrollY = 0;

        // --- ICQ THEME VARIABLES ---
        private Panel icqPanel;
        private Timer icqTimer;
        private bool icqPlaying = false;
        private Button btnGithubIcq;
        private Button btnDonateIcq;

        [DllImport("winmm.dll")]
        private static extern long mciSendString(string strCommand, System.Text.StringBuilder strReturn, int iReturnLength, IntPtr hwndCallback);

        public Form1()
        {
            try { ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use"); } catch { }
            SetupUI();
            InitializeWinampUI();
            InitializeIcqUI();
            SetupTray();
            LoadSettings(); 
            InstallAndCreateTask();

            autoTimer = new Timer();
            autoTimer.Interval = 10000;
            autoTimer.Tick += new EventHandler(CheckLogUpdates);
            autoTimer.Start();

            logUpdateTimer = new Timer();
            logUpdateTimer.Interval = 2000;
            logUpdateTimer.Tick += new EventHandler(LoadLogFile);
            logUpdateTimer.Start();

            if (startMinimized)
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
                this.Load += (s, e) => this.Hide();
            }
        }

        // --- Window Dragging Imports ---
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void SetupUI()
        {
            this.Text = "AmazeMem | Windows XP Edition";
            this.Size = new Size(550, 380);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;

            // Colors based on modern XP mockup
            Color xpBlue = Color.FromArgb(41, 100, 246);
            Color xpSidebar = Color.FromArgb(101, 114, 219);
            Color textWhite = Color.White;

            // Sidebar
            sidebar = new Panel { Location = new Point(0, 0), Size = new Size(140, this.Height), BackColor = xpSidebar };
            this.Controls.Add(sidebar);

            // Title Bar
            titleBar = new Panel { Location = new Point(140, 0), Size = new Size(this.Width - 140, 30), BackColor = xpBlue };
            titleBar.MouseDown += (s, e) => { if(e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };
            this.Controls.Add(titleBar);

            lblTitle = new Label { Text = "AmazeMem", ForeColor = textWhite, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = true, Location = new Point(10, 5) };
            lblTitle.MouseDown += (s, e) => { if(e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };
            titleBar.Controls.Add(lblTitle);

            btnClose = new Button { Text = "X", Size = new Size(40, 30), Location = new Point(titleBar.Width - 40, 0), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(224, 67, 67), ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();
            titleBar.Controls.Add(btnClose);

            // Buttons in sidebar
            btnManual.Text = "FORCE CLEAN";
            btnManual.Size = new Size(120, 35);
            btnManual.Location = new Point(10, 20);
            btnManual.FlatStyle = FlatStyle.Flat;
            btnManual.FlatAppearance.BorderSize = 0;
            btnManual.BackColor = Color.White;
            btnManual.ForeColor = xpSidebar;
            btnManual.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnManual.Cursor = Cursors.Hand;
            sidebar.Controls.Add(btnManual);

            btnGithub.Text = "GitHub";
            btnGithub.Size = new Size(120, 35);
            btnGithub.Location = new Point(10, 65);
            btnGithub.FlatStyle = FlatStyle.Flat;
            btnGithub.FlatAppearance.BorderSize = 0;
            btnGithub.BackColor = Color.FromArgb(80, 95, 200);
            btnGithub.ForeColor = Color.White;
            btnGithub.Cursor = Cursors.Hand;
            sidebar.Controls.Add(btnGithub);

            btnDonate = new Button { Text = "Donate", Size = new Size(120, 35), Location = new Point(10, 110), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(255, 170, 0), ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnDonate.FlatAppearance.BorderSize = 0;
            btnDonate.Click += (s, e) => { try { Process.Start("https://adiru3.github.io/Donate/"); } catch { } };
            sidebar.Controls.Add(btnDonate);

            comboTheme.Location = new Point(10, sidebar.Height - 75);
            comboTheme.Size = new Size(120, 25);
            comboTheme.FlatStyle = FlatStyle.Flat;
            comboTheme.Items.Add("Windows XP Theme");
            comboTheme.Items.Add("Winamp Theme");
            comboTheme.Items.Add("ICQ Theme");
            sidebar.Controls.Add(comboTheme);

            comboLang.Location = new Point(10, sidebar.Height - 40);
            comboLang.Size = new Size(120, 25);
            comboLang.FlatStyle = FlatStyle.Flat;
            sidebar.Controls.Add(comboLang);

            // Main Content Area
            content = new Panel { Location = new Point(140, 30), Size = new Size(this.Width - 140, this.Height - 30), BackColor = Color.FromArgb(245, 246, 247) };
            this.Controls.Add(content);

            // Options
            chkWS.Location = new Point(20, 20); chkWS.Font = new Font("Segoe UI", 9); chkWS.ForeColor = Color.Black; chkWS.AutoSize = true;
            chkSL.Location = new Point(20, 50); chkSL.Font = new Font("Segoe UI", 9); chkSL.ForeColor = Color.Black; chkSL.AutoSize = true;
            chkML.Location = new Point(20, 80); chkML.Font = new Font("Segoe UI", 9); chkML.ForeColor = Color.Black; chkML.AutoSize = true;
            chkP0.Location = new Point(20, 110); chkP0.Font = new Font("Segoe UI", 9); chkP0.ForeColor = Color.Black; chkP0.AutoSize = true;
            
            chkAutoRam.Location = new Point(170, 22); chkAutoRam.Size = new Size(18, 18); chkAutoRam.AutoSize = false; chkAutoRam.Text = "";
            lblPercent.Location = new Point(190, 23); lblPercent.Font = new Font("Segoe UI", 9); lblPercent.ForeColor = Color.Black; lblPercent.AutoSize = true;
            trkPercent.Location = new Point(170, 45); trkPercent.Size = new Size(140, 45);
            
            chkAutoTime.Location = new Point(170, 102); chkAutoTime.Size = new Size(18, 18); chkAutoTime.AutoSize = false; chkAutoTime.Text = "";
            lblInterval.Location = new Point(190, 103); lblInterval.Font = new Font("Segoe UI", 9); lblInterval.ForeColor = Color.Black; lblInterval.AutoSize = true;
            txtInterval.Location = new Point(190, 125); txtInterval.Size = new Size(50, 23); txtInterval.BorderStyle = BorderStyle.FixedSingle;
            
            content.Controls.AddRange(new Control[] { chkWS, chkSL, chkML, chkP0, chkAutoRam, lblPercent, trkPercent, chkAutoTime, lblInterval, txtInterval });

            txtLog.Location = new Point(20, 160);
            txtLog.Size = new Size(370, 170);
            txtLog.BackColor = Color.White;
            txtLog.ForeColor = Color.FromArgb(41, 100, 246);
            txtLog.BorderStyle = BorderStyle.FixedSingle;
            txtLog.Font = new Font("Consolas", 8);
            txtLog.Multiline = true;
            txtLog.ReadOnly = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            content.Controls.Add(txtLog);

            comboLang.Items.AddRange(new string[] { "English", "Русский", "Українська", "Türkçe" });
            string sysLang = System.Globalization.CultureInfo.CurrentCulture.Name.ToLower();
            if (sysLang.Contains("ru")) comboLang.SelectedIndex = 1;
            else if (sysLang.Contains("uk")) comboLang.SelectedIndex = 2;
            else if (sysLang.Contains("tr")) comboLang.SelectedIndex = 3;
            else comboLang.SelectedIndex = 0;

            comboTheme.SelectedIndexChanged += (s, e) => { ApplyTheme(); SaveSettings(); };
            comboLang.SelectedIndexChanged += (s, e) => { ApplyLanguage(); SaveSettings(); };
            trkPercent.ValueChanged += (s, e) => { ApplyLanguage(); SaveSettings(); };
            txtInterval.TextChanged += (s, e) => { SaveSettings(); };
            chkWS.CheckedChanged += (s, e) => { SaveSettings(); };
            chkSL.CheckedChanged += (s, e) => { SaveSettings(); };
            chkML.CheckedChanged += (s, e) => { SaveSettings(); };
            chkP0.CheckedChanged += (s, e) => { SaveSettings(); };
            chkAutoRam.CheckedChanged += (s, e) => { SaveSettings(); };
            chkAutoTime.CheckedChanged += (s, e) => { SaveSettings(); };
            btnGithub.Click += new EventHandler(OpenGithub);
            btnManual.Click += new EventHandler(ManualCleanClick);

            loadingOverlay = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 246, 247), Visible = false };
            typeof(Panel).InvokeMember("DoubleBuffered", BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic, null, loadingOverlay, new object[] { true });

            loadingOverlay.Paint += (s, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float cx = loadingOverlay.Width / 2f;
                // Сдвигаем центр круга чуть выше, чтобы вместе с текстом общая композиция была ровно по центру
                float cy = (loadingOverlay.Height / 2f) - 15f; 
                float radius = 30;
                
                for (int i = 0; i < 8; i++) {
                    float angle = spinAngle - (i * 45);
                    double rad = angle * Math.PI / 180.0;
                    float x = cx + (float)(Math.Cos(rad) * radius);
                    float y = cy + (float)(Math.Sin(rad) * radius);
                    
                    int alpha = 255 - (i * 28);
                    if (alpha < 0) alpha = 0;
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 41, 100, 246))) {
                        e.Graphics.FillEllipse(b, x - 6, y - 6, 12, 12);
                    }
                }
                
                string t = "CLEANING...";
                using (Font f = new Font("Segoe UI", 11, FontStyle.Bold)) {
                    SizeF sz = e.Graphics.MeasureString(t, f);
                    using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(41, 100, 246))) {
                        e.Graphics.DrawString(t, f, textBrush, cx - sz.Width / 2, cy + radius + 15);
                    }
                }
            };
            content.Controls.Add(loadingOverlay);

            spinTimer = new Timer { Interval = 50 };
            spinTimer.Tick += (s, e) => { spinAngle += 30; if (spinAngle >= 360) spinAngle -= 360; loadingOverlay.Invalidate(); };

            ApplyLanguage();
        }

        private void InitializeWinampUI()
        {
            winampPlaylist.Add("1. Amaze Mem - System Cleaning (Original Mix)");
            winampFilePaths.Add(Path.Combine(Path.GetTempPath(), "amaze_theme.mp3"));
            winampPlaylist.Add("2. Return To Classic XP");
            winampFilePaths.Add("ACTION_RETURN");

            winampPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
            typeof(Panel).InvokeMember("DoubleBuffered", BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic, null, winampPanel, new object[] { true });
            
            winampTimer = new Timer { Interval = 100 };
            winampTimer.Tick += (s, e) => {
                if (winampPlaying) {
                    winampMarqueeOffset -= 5;
                    if (winampMarqueeOffset < -200) winampMarqueeOffset = 150;
                    
                    for (int i = 0; i < winampEqBars.Length; i++) {
                        winampEqBars[i] = rand.Next(0, 16);
                    }
                } else {
                    for (int i = 0; i < winampEqBars.Length; i++) winampEqBars[i] = 0;
                }
                winampPanel.Invalidate();
            };

            Timer secondTimer = new Timer { Interval = 1000 };
            secondTimer.Tick += (s, e) => {
                if (winampPlaying) {
                    winampSeconds++;
                    winampPanel.Invalidate();
                }
            };
            secondTimer.Start();

            winampPanel.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.Clear(Color.FromArgb(17, 17, 24)); // bg
                
                // Top Player Area
                Pen borderPen = new Pen(Color.FromArgb(70, 70, 80), 2);
                g.DrawRectangle(borderPen, 2, 2, 271, 112);
                g.FillRectangle(new SolidBrush(Color.FromArgb(30, 30, 40)), 3, 3, 270, 14);
                g.DrawString("WINAMP", new Font("Arial", 8, FontStyle.Bold), Brushes.LightGray, 110, 3);
                
                // Close button (fake)
                g.FillRectangle(Brushes.DarkRed, 260, 4, 10, 10);
                
                // Screen Box
                g.FillRectangle(Brushes.Black, 10, 25, 253, 40);
                
                // Time (LED style)
                string timeStr = string.Format("{0:D2}:{1:D2}", winampSeconds / 60, winampSeconds % 60);
                using (Font timeFont = new Font("Consolas", 20, FontStyle.Bold)) {
                    g.DrawString(timeStr, timeFont, new SolidBrush(Color.FromArgb(0, 255, 0)), 15, 28);
                }
                
                // Marquee Text
                g.Clip = new Region(new Rectangle(110, 25, 145, 15));
                using (Font mFont = new Font("Consolas", 8)) {
                    g.DrawString(winampCurrentSong + "   ***   ", mFont, new SolidBrush(Color.FromArgb(0, 255, 0)), 110 + winampMarqueeOffset, 27);
                }
                g.ResetClip();
                
                // Bitrate etc
                using (Font sFont = new Font("Arial", 7)) {
                    g.DrawString("128 kbps  44 kHz", sFont, new SolidBrush(Color.FromArgb(0, 180, 0)), 110, 42);
                }

                // Spectrum Visualizer
                for (int i = 0; i < winampEqBars.Length; i++) {
                    int h = winampEqBars[i];
                    Rectangle bar = new Rectangle(110 + (i * 4), 62 - h, 3, h + 1);
                    g.FillRectangle(new SolidBrush(Color.FromArgb(0, 255, 0)), bar);
                }
                
                // Buttons
                g.FillRectangle(new SolidBrush(Color.FromArgb(80, 80, 90)), 15, 75, 40, 30); // Play
                g.FillRectangle(new SolidBrush(Color.FromArgb(60, 60, 70)), 60, 75, 40, 30); // Stop
                g.DrawString("▶ PLAY", new Font("Arial", 7, FontStyle.Bold), Brushes.White, 18, 85);
                g.DrawString("■ STOP", new Font("Arial", 7, FontStyle.Bold), Brushes.White, 63, 85);
                
                // Playlist Area
                g.DrawRectangle(borderPen, 2, 116, 271, 330);
                g.FillRectangle(new SolidBrush(Color.FromArgb(30, 30, 40)), 3, 117, 270, 14);
                g.DrawString("WINAMP PLAYLIST", new Font("Arial", 8, FontStyle.Bold), Brushes.LightGray, 75, 117);
                
                // Tracklist UI
                g.FillRectangle(Brushes.Black, 10, 135, 253, 130);
                g.SetClip(new Rectangle(10, 135, 253, 130));
                using (Font listFont = new Font("Arial", 8)) {
                    SolidBrush greenBrush = new SolidBrush(Color.FromArgb(0, 200, 0));
                    SolidBrush whiteBrush = new SolidBrush(Color.White);
                    SolidBrush selectBrush = new SolidBrush(Color.FromArgb(0, 0, 150));
                    
                    int yPos = 140 - winampScrollY;
                    for (int i = 0; i < winampPlaylist.Count; i++) {
                        if (i == winampSelectedIndex) {
                            g.FillRectangle(selectBrush, 12, yPos - 2, 249, 14);
                            g.DrawString(winampPlaylist[i], listFont, whiteBrush, 15, yPos);
                        } else {
                            g.DrawString(winampPlaylist[i], listFont, greenBrush, 15, yPos);
                        }
                        yPos += 15;
                    }
                }
                g.ResetClip();

                // Scrollbar Simulation
                g.FillRectangle(new SolidBrush(Color.FromArgb(40,40,50)), 255, 135, 8, 130);
                
                // Bottom Bar Tools
                g.FillRectangle(new SolidBrush(Color.FromArgb(50, 50, 60)), 10, 270, 253, 20);
                g.DrawString("ADD  REM GITHUB  DONATE CLEAN", new Font("Arial", 7, FontStyle.Bold), Brushes.LightGray, 15, 274);

                // Settings Area (Embedded in playlist)
                g.FillRectangle(new SolidBrush(Color.FromArgb(17, 17, 24)), 4, 295, 267, 148);
                g.DrawString("- MEMORY CLEANER SETTINGS -", new Font("Consolas", 8, FontStyle.Bold), Brushes.DarkGray, 40, 300);
            };

            winampPanel.MouseDown += (s, e) => {
                // Minimize (Close Button)
                if (e.X >= 260 && e.X <= 270 && e.Y >= 4 && e.Y <= 14) {
                    this.WindowState = FormWindowState.Minimized;
                    this.ShowInTaskbar = false;
                    this.Hide();
                }
                // Play Button
                if (e.X >= 15 && e.X <= 55 && e.Y >= 75 && e.Y <= 105) {
                    if (!winampPlaying) {
                        mciSendString("play MediaFile repeat", null, 0, IntPtr.Zero);
                    }
                    winampPlaying = true;
                    if (winampSeconds == 0) winampSeconds = 1;
                    // Trigger Actual Clean
                    ThreadPool.QueueUserWorkItem(state => {
                        try {
                            ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", "/Run /TN \"AmazingMemCleaner\"") 
                            { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true };
                            Process.Start(psi);
                        } catch { }
                    });
                }
                // Stop Button
                if (e.X >= 60 && e.X <= 100 && e.Y >= 75 && e.Y <= 105) {
                    winampPlaying = false;
                    winampSeconds = 0;
                    winampMarqueeOffset = 0;
                    mciSendString("stop MediaFile", null, 0, IntPtr.Zero);
                    winampPanel.Invalidate();
                }
                // Playlist Click Logic
                if (e.X >= 10 && e.X <= 253 && e.Y >= 135 && e.Y <= 265) {
                    int clickedIndex = (e.Y - 135 + winampScrollY) / 15;
                    if (clickedIndex >= 0 && clickedIndex < winampPlaylist.Count) {
                        winampSelectedIndex = clickedIndex;
                        
                        if (winampFilePaths[clickedIndex] == "ACTION_RETURN") {
                            mciSendString("stop MediaFile", null, 0, IntPtr.Zero);
                            comboTheme.SelectedIndex = 0;
                        } else {
                            winampCurrentSong = winampPlaylist[clickedIndex] + " *** ";
                            mciSendString("close MediaFile", null, 0, IntPtr.Zero);
                            mciSendString("open \"" + winampFilePaths[clickedIndex] + "\" type mpegvideo alias MediaFile", null, 0, IntPtr.Zero);
                            if (winampPlaying) {
                                winampSeconds = 0;
                                mciSendString("play MediaFile repeat", null, 0, IntPtr.Zero);
                            }
                        }
                        winampPanel.Invalidate();
                    }
                }
                
                // Playlist Scroll Logic (Clicking top/bottom of scrollbar)
                if (e.X >= 255 && e.X <= 263 && e.Y >= 135 && e.Y <= 265) {
                    if (e.Y < 200) {
                        winampScrollY = Math.Max(0, winampScrollY - 30);
                    } else {
                        int maxScroll = Math.Max(0, (winampPlaylist.Count * 15) - 130);
                        winampScrollY = Math.Min(maxScroll, winampScrollY + 30);
                    }
                    winampPanel.Invalidate();
                }

                // Click "ADD"
                if (e.X >= 10 && e.X <= 30 && e.Y >= 270 && e.Y <= 290) {
                    using (OpenFileDialog ofd = new OpenFileDialog()) {
                        ofd.Filter = "Audio Files|*.mp3;*.wav";
                        if (ofd.ShowDialog() == DialogResult.OK) {
                            mciSendString("close MediaFile", null, 0, IntPtr.Zero);
                            mciSendString("open \"" + ofd.FileName + "\" type mpegvideo alias MediaFile", null, 0, IntPtr.Zero);
                            winampPlaylist.Add((winampPlaylist.Count + 1) + ". " + Path.GetFileName(ofd.FileName));
                            winampFilePaths.Add(ofd.FileName);
                            
                            winampSelectedIndex = winampPlaylist.Count - 1; // Select the newly added track
                            winampCurrentSong = winampPlaylist[winampSelectedIndex] + " *** ";
                            
                            winampSeconds = 0;
                            winampPlaying = true;
                            mciSendString("play MediaFile repeat", null, 0, IntPtr.Zero);
                            winampPanel.Invalidate();
                            
                            // Trigger cleaning alongside new track
                            ThreadPool.QueueUserWorkItem(state => {
                                try {
                                    ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", "/Run /TN \"AmazingMemCleaner\"") 
                                    { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true };
                                    Process.Start(psi);
                                } catch { }
                            });
                        }
                    }
                }
                // Click "REM"
                if (e.X >= 35 && e.X <= 55 && e.Y >= 270 && e.Y <= 290) {
                    if (winampSelectedIndex > 1 && winampSelectedIndex < winampPlaylist.Count) {
                        winampPlaylist.RemoveAt(winampSelectedIndex);
                        winampFilePaths.RemoveAt(winampSelectedIndex);
                        // Re-number subsequent tracks
                        for (int i = 2; i < winampPlaylist.Count; i++) {
                            string[] parts = winampPlaylist[i].Split(new char[] { '.' }, 2);
                            if (parts.Length == 2) winampPlaylist[i] = (i + 1) + "." + parts[1];
                        }
                        winampSelectedIndex = 0; // fallback to song 1
                        mciSendString("stop MediaFile", null, 0, IntPtr.Zero);
                        winampPanel.Invalidate();
                    }
                }
                // Click "GITHUB"
                if (e.X >= 60 && e.X <= 95 && e.Y >= 270 && e.Y <= 290) {
                    try { Process.Start("https://github.com/Adirulian/AmazeMem"); } catch {}
                }
                // Click "DONATE"
                if (e.X >= 100 && e.X <= 140 && e.Y >= 270 && e.Y <= 290) {
                    try { Process.Start("https://app.crypto2p.com/"); } catch {}
                }
                // Click "CLEAN" (FORCE CLEAN)
                if (e.X >= 145 && e.X <= 250 && e.Y >= 270 && e.Y <= 290) {
                    ManualCleanClick(this, EventArgs.Empty);
                }
            };
            
            // Allow dragging the UI
            winampPanel.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left && e.Y < 20) {
                    ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };

            this.Controls.Add(winampPanel);
        }

        private void InitializeIcqUI()
        {
            icqPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
            typeof(Panel).InvokeMember("DoubleBuffered", BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic, null, icqPanel, new object[] { true });

            btnGithubIcq = new Button { Text = "GitHub", Size = new Size(85, 24), Location = new Point(30, 360), BackColor = Color.FromArgb(230, 230, 230), FlatStyle = FlatStyle.Standard, Font = new Font("Arial", 8) };
            btnGithubIcq.Click += (s, e) => { try { Process.Start("https://github.com/Adiru3/AmazeMem"); } catch { } };

            btnDonateIcq = new Button { Text = "Donate", Size = new Size(85, 24), Location = new Point(125, 360), BackColor = Color.FromArgb(230, 230, 230), FlatStyle = FlatStyle.Standard, Font = new Font("Arial", 8) };
            btnDonateIcq.Click += (s, e) => { try { Process.Start("https://adiru3.github.io/Donate/"); } catch { } };

            icqPanel.Controls.Add(btnGithubIcq);
            icqPanel.Controls.Add(btnDonateIcq);

            icqTimer = new Timer { Interval = 100 };
            icqTimer.Tick += (s, e) => {
                // Future animation frame tick
                icqPanel.Invalidate();
            };

            icqPanel.Paint += (s, e) => {
                Graphics g = e.Graphics;
                g.Clear(Color.FromArgb(245, 250, 245)); // Off-white ICQ list background
                
                // Classic Green Header Gradient (approximation)
                using (System.Drawing.Drawing2D.LinearGradientBrush brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                    new Rectangle(0, 0, icqPanel.Width, 60), 
                    Color.FromArgb(170, 225, 120), 
                    Color.FromArgb(80, 180, 50), 
                    90f)) 
                {
                    g.FillRectangle(brush, 0, 0, icqPanel.Width, 60);
                }
                
                // Top control bar
                g.FillRectangle(new SolidBrush(Color.FromArgb(220, 240, 210)), 0, 0, icqPanel.Width, 20);
                g.DrawString("icq", new Font("Arial", 10, FontStyle.Bold), new SolidBrush(Color.FromArgb(30, 100, 20)), 25, 2);
                
                // Flower logo (fake dots)
                SolidBrush red = new SolidBrush(Color.FromArgb(220, 50, 50));
                SolidBrush green = new SolidBrush(Color.FromArgb(50, 200, 50));
                g.FillEllipse(red, 10, 5, 5, 5); g.FillEllipse(green, 6, 8, 5, 5);
                g.FillEllipse(green, 14, 8, 5, 5); g.FillEllipse(green, 10, 11, 5, 5);

                // Close Button
                g.DrawString("X", new Font("Arial", 8, FontStyle.Bold), Brushes.Gray, icqPanel.Width - 15, 3);
                
                // User Profile Area
                g.DrawRectangle(Pens.White, 10, 25, 30, 30);
                g.FillRectangle(Brushes.WhiteSmoke, 11, 26, 28, 28);
                g.DrawString("AmazeMem", new Font("Arial", 10, FontStyle.Bold), Brushes.White, 45, 25);
                g.DrawString("Memory Cleaner", new Font("Arial", 8), new SolidBrush(Color.FromArgb(220,255,220)), 45, 42);

                // Contacts Tab Bar
                g.FillRectangle(new SolidBrush(Color.FromArgb(235, 245, 235)), 0, 60, icqPanel.Width, 25);
                g.DrawString("Settings Contact List", new Font("Arial", 8, FontStyle.Bold), Brushes.DimGray, 10, 65);
                
                // Bottom Toolbar
                g.FillRectangle(new SolidBrush(Color.FromArgb(235, 245, 235)), 0, icqPanel.Height - 35, icqPanel.Width, 35);
                g.DrawString("RETURN TO XP", new Font("Arial", 7, FontStyle.Bold), Brushes.DimGray, 10, icqPanel.Height - 25);
                g.DrawString("FORCE CLEAN (PLAY SOUND)", new Font("Arial", 7, FontStyle.Bold), Brushes.DimGray, 90, icqPanel.Height - 25);
            };

            icqPanel.MouseDown += (s, e) => {
                // Minimize
                if (e.X >= icqPanel.Width - 20 && e.Y <= 20) {
                    this.WindowState = FormWindowState.Minimized;
                    this.ShowInTaskbar = false;
                    this.Hide();
                }
                // Return to XP
                if (e.Y >= icqPanel.Height - 35 && e.X <= 80) {
                    comboTheme.SelectedIndex = 0;
                }
                // Force Clean
                if (e.Y >= icqPanel.Height - 35 && e.X > 80) {
                    ManualCleanClick(this, EventArgs.Empty);
                    PlayIcqSound();
                }

                // Drag
                if (e.Button == MouseButtons.Left && e.Y < 20) {
                    ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
            };

            this.Controls.Add(icqPanel);
        }

        private void PlayIcqSound()
        {
            try {
                string icqAudioPath = Path.Combine(Path.GetTempPath(), "amaze_icq.mp3");
                // Only extract if it doesn't exist to save IO
                if (!File.Exists(icqAudioPath)) {
                    string resName = Assembly.GetExecutingAssembly().GetManifestResourceNames().FirstOrDefault(r => r.EndsWith("icq.mp3", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(resName)) {
                        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName)) {
                            if (stream != null) {
                                using (FileStream fileStream = new FileStream(icqAudioPath, FileMode.Create)) {
                                    stream.CopyTo(fileStream);
                                }
                            }
                        }
                    }
                }
                mciSendString("close IcqFile", null, 0, IntPtr.Zero);
                mciSendString("open \"" + icqAudioPath + "\" type mpegvideo alias IcqFile", null, 0, IntPtr.Zero);
                mciSendString("play IcqFile", null, 0, IntPtr.Zero);
            } catch { }
        }

        private void ManualCleanClick(object sender, EventArgs e) 
        { 
            try {
                ThreadPool.QueueUserWorkItem(state => {
                    try {
                        ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", "/Run /TN \"AmazingMemCleaner\"") 
                        { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true };
                        Process.Start(psi);
                    } catch { }
                });
                Log("Requesting SYSTEM clean via Task Scheduler (Background)...");

                spinAngle = 0;
                loadingOverlay.Visible = true;
                loadingOverlay.BringToFront();
                spinTimer.Start();
                btnManual.Enabled = false;

                Timer stopTimer = new Timer { Interval = 2500 };
                stopTimer.Tick += (s2, e2) => {
                    stopTimer.Stop();
                    stopTimer.Dispose();
                    spinTimer.Stop();
                    loadingOverlay.Visible = false;
                    btnManual.Enabled = true;
                };
                stopTimer.Start();

                // Play ICQ sound on clean if ICQ Theme is active
                if (comboTheme.SelectedIndex == 2) {
                    PlayIcqSound();
                }

            } catch (Exception ex) {
                Log("Error triggering task: " + ex.Message);
            }
        }

        private void CheckLogUpdates(object sender, EventArgs e)
        {
            // 1. Проверка интервала времени
            int min;
            if (!int.TryParse(txtInterval.Text, out min)) min = 60;
            
            // 2. Проверка порога ОЗУ (из вашего TrackBar)
            float ramUsage = GetRamUsagePercentage();
            bool overThreshold = ramUsage >= trkPercent.Value;

            // Защита от спама (cooldown как минимум 1 минута, чтобы не вызывать очистку каждые 10 секунд при забитой ОЗУ)
            bool cooldownPassed = (DateTime.Now - lastCleanTime).TotalMinutes >= 1.0;

            bool timeTrigger = chkAutoTime.Checked && ((DateTime.Now - lastCleanTime).TotalMinutes >= min);
            bool ramTrigger = chkAutoRam.Checked && overThreshold && cooldownPassed;

            if (timeTrigger || ramTrigger) {
                lastCleanTime = DateTime.Now;
                try {
                    ThreadPool.QueueUserWorkItem(state => {
                        try {
                            ProcessStartInfo psi = new ProcessStartInfo("schtasks.exe", "/Run /TN \"AmazingMemCleaner\"") 
                            { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true };
                            Process.Start(psi);
                        } catch { }
                    });
                    
                    if (ramTrigger) Log(string.Format("RAM Threshold hit ({0:F1}%)! Triggering SYSTEM clean...", ramUsage));
                    else if (timeTrigger) Log("Time interval reached! Triggering SYSTEM clean...");
                } catch { }
            }
        }

        // Вспомогательный метод для получения % загрузки ОЗУ
        private float GetRamUsagePercentage()
        {
            if (ramCounter == null) return 0;
            try { return ramCounter.NextValue(); } catch { return 0; }
        }

        private void LoadLogFile(object sender, EventArgs e)
        {
            try {
                if (File.Exists(logFilePath)) {
                    string[] lines = File.ReadAllLines(logFilePath);
                    int startIndex = Math.Max(0, lines.Length - 15);
                    txtLog.Text = string.Join(Environment.NewLine, lines.Skip(startIndex));
                    txtLog.SelectionStart = txtLog.Text.Length;
                    txtLog.ScrollToCaret();
                }
            } catch { }
        }

        private void InstallAndCreateTask()
        {
            try {
                if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);
                if (!string.Equals(Application.ExecutablePath, targetPath, StringComparison.OrdinalIgnoreCase)) File.Copy(Application.ExecutablePath, targetPath, true);

                // Создаем задачу для GUI (запуск при логине)
                string guiArgs = string.Format("/Create /F /TN \"AmazingMem\" /TR \"\\\"{0}\\\" /min\" /SC ONLOGON /RL HIGHEST", targetPath);
                Process.Start(new ProcessStartInfo("schtasks.exe", guiArgs) { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true });

                // Создаем системную задачу для очистки (права SYSTEM)
                // /clean argument triggers the cleaning logic including the ICQ sound if ICQ theme is active
                string cleanerArgs = string.Format("/Create /F /TN \"AmazingMemCleaner\" /TR \"\\\"{0}\\\" /clean\" /SC MINUTE /MO 60 /RU \"System\" /RL HIGHEST", targetPath);
                Process.Start(new ProcessStartInfo("schtasks.exe", cleanerArgs) { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true });

                Log("System Scheduler Tasks synchronized.");
            } catch (Exception ex) { 
                Log("Sync Error: " + ex.Message); 
            }
        }

        private void ApplyLanguage()
        {
            string lang = comboLang.Text;
            if (lang == "Русский") {
                btnManual.Text = "ОЧИСТИТЬ";
                lblPercent.Text = string.Format("Порог ОЗУ: {0}%", trkPercent.Value);
                lblInterval.Text = "Мин:";
                chkWS.Text = "Рабочие наборы"; chkSL.Text = "Список ожидания";
                chkML.Text = "Модифицированный"; chkP0.Text = "Приоритет 0";
            } else if (lang == "Українська") {
                btnManual.Text = "ОЧИСТИТИ";
                lblPercent.Text = string.Format("Поріг ОЗП: {0}%", trkPercent.Value);
                lblInterval.Text = "Хв:";
                chkWS.Text = "Робочі набори"; chkSL.Text = "Список очікування";
                chkML.Text = "Модифікований"; chkP0.Text = "Пріоритет 0";
            } else if (lang == "Türkçe") {
                btnManual.Text = "TEMIZLE";
                lblPercent.Text = string.Format("RAM Eşiği: {0}%", trkPercent.Value);
                lblInterval.Text = "Dak:";
                chkWS.Text = "Çalışma Setleri"; chkSL.Text = "Bekleme Listesi";
                chkML.Text = "Değiştirilmiş"; chkP0.Text = "Öncelik 0";
            } else {
                btnManual.Text = "FORCE CLEAN";
                lblPercent.Text = string.Format("RAM Threshold: {0}%", trkPercent.Value);
                lblInterval.Text = "Min:";
                chkWS.Text = "Working Sets"; chkSL.Text = "Standby List";
                chkML.Text = "Modified List"; chkP0.Text = "Priority 0";
            }
        }

        private void ApplyTheme()
        {
            if (comboTheme.SelectedIndex == 1) // Winamp Theme
            {
                this.Size = new Size(275, 450); // Classic Winamp stacked size + settings
                titleBar.Visible = false;
                sidebar.Visible = false;
                content.Visible = false;
                if (icqPanel != null) icqPanel.Visible = false;
                if (winampPanel != null) {
                    winampPanel.Visible = true;
                    winampTimer.Start();
                }

                // Ensure the built-in MP3 is pre-loaded for playback when entering the theme
                string tempAudioPath = Path.Combine(Path.GetTempPath(), "amaze_theme.mp3");
                try {
                    string resName = Assembly.GetExecutingAssembly().GetManifestResourceNames().FirstOrDefault(r => r.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(resName)) {
                        using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName)) {
                            if (stream != null) {
                                using (FileStream fileStream = new FileStream(tempAudioPath, FileMode.Create)) {
                                    stream.CopyTo(fileStream);
                                }
                                if (winampCurrentSong.Contains("No Track") || winampCurrentSong.Contains("Original Mix")) {
                                    winampCurrentSong = "1. Amaze Mem - System Cleaning (Original Mix) *** ";
                                    mciSendString("close MediaFile", null, 0, IntPtr.Zero);
                                    mciSendString("open \"" + tempAudioPath + "\" type mpegvideo alias MediaFile", null, 0, IntPtr.Zero);
                                }
                            }
                        }
                    }
                } catch { }

                // Move settings to WinampPanel and skin them
                winampPanel.Controls.Add(chkWS); chkWS.Location = new Point(15, 320); chkWS.ForeColor = Color.FromArgb(0,255,0); chkWS.BackColor = Color.Transparent;
                winampPanel.Controls.Add(chkSL); chkSL.Location = new Point(15, 340); chkSL.ForeColor = Color.FromArgb(0,255,0); chkSL.BackColor = Color.Transparent;
                winampPanel.Controls.Add(chkML); chkML.Location = new Point(15, 360); chkML.ForeColor = Color.FromArgb(0,255,0); chkML.BackColor = Color.Transparent;
                winampPanel.Controls.Add(chkP0); chkP0.Location = new Point(15, 380); chkP0.ForeColor = Color.FromArgb(0,255,0); chkP0.BackColor = Color.Transparent;
                
                winampPanel.Controls.Add(chkAutoRam); chkAutoRam.Location = new Point(140, 320); chkAutoRam.ForeColor = Color.FromArgb(0,255,0); chkAutoRam.BackColor = Color.Transparent;
                winampPanel.Controls.Add(lblPercent); lblPercent.Location = new Point(160, 321); lblPercent.ForeColor = Color.FromArgb(0,255,0); lblPercent.BackColor = Color.Transparent;
                winampPanel.Controls.Add(trkPercent); trkPercent.Location = new Point(140, 340); trkPercent.Width = 100; trkPercent.BackColor = Color.FromArgb(17,17,24);
                
                winampPanel.Controls.Add(chkAutoTime); chkAutoTime.Location = new Point(140, 375); chkAutoTime.ForeColor = Color.FromArgb(0,255,0); chkAutoTime.BackColor = Color.Transparent;
                winampPanel.Controls.Add(txtInterval); txtInterval.Location = new Point(140, 400); txtInterval.Width = 30; txtInterval.BackColor = Color.Black; txtInterval.ForeColor = Color.FromArgb(0,255,0);
                winampPanel.Controls.Add(lblInterval); lblInterval.Location = new Point(175, 403); lblInterval.ForeColor = Color.FromArgb(0,255,0); lblInterval.BackColor = Color.Transparent;
            }
            else if (comboTheme.SelectedIndex == 2) // ICQ Theme
            {
                this.Size = new Size(250, 500); // Tall narrow ICQ messenger size
                titleBar.Visible = false;
                sidebar.Visible = false;
                content.Visible = false;
                if (winampPanel != null) {
                    winampPanel.Visible = false;
                    winampTimer.Stop();
                }
                if (icqPanel != null) {
                    icqPanel.Visible = true;
                    icqTimer.Start();
                }

                // Place options into the ICQ "Contacts" space
                icqPanel.Controls.Add(chkWS); chkWS.Location = new Point(30, 100); chkWS.ForeColor = Color.Black; chkWS.BackColor = Color.Transparent; chkWS.Font = new Font("Arial", 8, FontStyle.Bold);
                icqPanel.Controls.Add(chkSL); chkSL.Location = new Point(30, 130); chkSL.ForeColor = Color.Black; chkSL.BackColor = Color.Transparent; chkSL.Font = new Font("Arial", 8, FontStyle.Bold);
                icqPanel.Controls.Add(chkML); chkML.Location = new Point(30, 160); chkML.ForeColor = Color.Black; chkML.BackColor = Color.Transparent; chkML.Font = new Font("Arial", 8, FontStyle.Bold);
                icqPanel.Controls.Add(chkP0); chkP0.Location = new Point(30, 190); chkP0.ForeColor = Color.Black; chkP0.BackColor = Color.Transparent; chkP0.Font = new Font("Arial", 8, FontStyle.Bold);
                
                icqPanel.Controls.Add(chkAutoRam); chkAutoRam.Location = new Point(30, 230); chkAutoRam.ForeColor = Color.Black; chkAutoRam.BackColor = Color.Transparent;
                icqPanel.Controls.Add(lblPercent); lblPercent.Location = new Point(50, 231); lblPercent.ForeColor = Color.Black; lblPercent.BackColor = Color.Transparent; lblPercent.Font = new Font("Arial", 8, FontStyle.Bold);
                icqPanel.Controls.Add(trkPercent); trkPercent.Location = new Point(30, 250); trkPercent.Width = 150; trkPercent.BackColor = Color.FromArgb(245, 250, 245);
                
                icqPanel.Controls.Add(chkAutoTime); chkAutoTime.Location = new Point(30, 300); chkAutoTime.ForeColor = Color.Black; chkAutoTime.BackColor = Color.Transparent;
                icqPanel.Controls.Add(txtInterval); txtInterval.Location = new Point(30, 320); txtInterval.Width = 35; txtInterval.BackColor = Color.White; txtInterval.ForeColor = Color.Black;
                icqPanel.Controls.Add(lblInterval); lblInterval.Location = new Point(70, 323); lblInterval.ForeColor = Color.Black; lblInterval.BackColor = Color.Transparent; lblInterval.Font = new Font("Arial", 8, FontStyle.Bold);

                PlayIcqSound();
            }
            else // Windows XP Theme (Default)
            {
                this.Size = new Size(550, 380);
                titleBar.Visible = true;
                sidebar.Visible = true;
                content.Visible = true;
                if (icqPanel != null) {
                    icqPanel.Visible = false;
                    icqTimer.Stop();
                }
                if (winampPanel != null) {
                    winampPanel.Visible = false;
                    winampTimer.Stop();
                }

                // Move settings back to Content and restore original XP UI
                content.Controls.Add(chkWS); chkWS.Location = new Point(20, 20); chkWS.ForeColor = Color.Black;
                content.Controls.Add(chkSL); chkSL.Location = new Point(20, 50); chkSL.ForeColor = Color.Black;
                content.Controls.Add(chkML); chkML.Location = new Point(20, 80); chkML.ForeColor = Color.Black;
                content.Controls.Add(chkP0); chkP0.Location = new Point(20, 110); chkP0.ForeColor = Color.Black;
                
                content.Controls.Add(chkAutoRam); chkAutoRam.Location = new Point(160, 22); chkAutoRam.ForeColor = Color.Black;
                content.Controls.Add(lblPercent); lblPercent.Location = new Point(180, 23); lblPercent.ForeColor = Color.Black;
                content.Controls.Add(trkPercent); trkPercent.Location = new Point(160, 45); trkPercent.Width = 140; trkPercent.BackColor = Color.FromArgb(245, 246, 247);
                
                content.Controls.Add(chkAutoTime); chkAutoTime.Location = new Point(160, 100); chkAutoTime.ForeColor = Color.Black;
                content.Controls.Add(txtInterval); txtInterval.Location = new Point(160, 125); txtInterval.Width = 35; txtInterval.BackColor = Color.White; txtInterval.ForeColor = Color.Black;
                content.Controls.Add(lblInterval); lblInterval.Location = new Point(200, 128); lblInterval.ForeColor = Color.Black;

                this.Text = "AmazeMem | Windows XP Edition";
                titleBar.BackColor = Color.FromArgb(41, 100, 246);
                sidebar.BackColor = Color.FromArgb(101, 114, 219);
                content.BackColor = Color.FromArgb(245, 246, 247);
                
                btnManual.BackColor = Color.White;
                btnManual.ForeColor = Color.FromArgb(101, 114, 219);
                btnGithub.BackColor = Color.FromArgb(80, 95, 200);
                btnGithub.ForeColor = Color.White;
                btnDonate.BackColor = Color.FromArgb(255, 170, 0);
                btnDonate.ForeColor = Color.White;
                txtLog.BackColor = Color.White; txtLog.ForeColor = Color.FromArgb(41, 100, 246);
            }
        }

        private void SaveSettings()
        {
            if (isSettingsLoading) return;
            try {
                RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\AmazeMem");
                key.SetValue("Threshold", trkPercent.Value);
                key.SetValue("Interval", txtInterval.Text);
                key.SetValue("Lang", comboLang.SelectedIndex);
                key.SetValue("chkWS", chkWS.Checked ? 1 : 0);
                key.SetValue("chkSL", chkSL.Checked ? 1 : 0);
                key.SetValue("chkML", chkML.Checked ? 1 : 0);
                key.SetValue("chkP0", chkP0.Checked ? 1 : 0);
                key.SetValue("chkAutoRam", chkAutoRam.Checked ? 1 : 0);
                key.SetValue("chkAutoTime", chkAutoTime.Checked ? 1 : 0);
                key.SetValue("Theme", comboTheme.SelectedIndex);
                key.Close();
            } catch { }
        }

        private void LoadSettings()
        {
            try {
                isSettingsLoading = true;
                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\AmazeMem");
                if (key != null) {
                    trkPercent.Value = (int)key.GetValue("Threshold", 90);
                    txtInterval.Text = (string)key.GetValue("Interval", "60");
                    comboLang.SelectedIndex = (int)key.GetValue("Lang", 0);
                    chkWS.Checked = (int)key.GetValue("chkWS", 1) == 1;
                    chkSL.Checked = (int)key.GetValue("chkSL", 1) == 1;
                    chkML.Checked = (int)key.GetValue("chkML", 1) == 1;
                    chkP0.Checked = (int)key.GetValue("chkP0", 1) == 1;
                    chkAutoRam.Checked = (int)key.GetValue("chkAutoRam", 1) == 1;
                    chkAutoTime.Checked = (int)key.GetValue("chkAutoTime", 1) == 1;
                    
                    int savedTheme = (int)key.GetValue("Theme", 0);
                    if (savedTheme >= 0 && savedTheme < comboTheme.Items.Count) comboTheme.SelectedIndex = savedTheme;
                    else comboTheme.SelectedIndex = 0;
                    
                    key.Close();
                }
            } catch { } 
            finally {
                isSettingsLoading = false;
                ApplyTheme();
                ApplyLanguage();
            }
        }

        private void Log(string m) { txtLog.AppendText(string.Format("[{0}] {1}\r\n", DateTime.Now.ToString("HH:mm:ss"), m)); }

        private void SetupTray()
        {
            trayIcon = new NotifyIcon() { Icon = SystemIcons.Shield, Visible = true, Text = "AmazeMem by amazingb01" };
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; this.ShowInTaskbar = true; this.Activate(); };
            this.FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; this.Hide(); this.ShowInTaskbar = false; } };
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open", null, (s, e) => { this.Show(); this.WindowState = FormWindowState.Normal; this.ShowInTaskbar = true; this.Activate(); });
            menu.Items.Add("Exit", null, (s, e) => Application.Exit());
            trayIcon.ContextMenuStrip = menu;
        }

        private void OpenGithub(object sender, EventArgs e) { try { Process.Start("https://github.com/Adiru3"); } catch { } }

        private static bool IsAdmin()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(id);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static string GetFileVersion(string path)
        {
            try { return FileVersionInfo.GetVersionInfo(path).FileVersion ?? "0.0"; } catch { return "0.0"; }
        }

        // ========== CLEANER MODE (SYSTEM) ==========
        private static void RunCleanerMode()
        {
            try {
                // Список для хранения текущих строк лога
                List<string> logLines = new List<string>();

                // Если файл уже существует, читаем его
                if (File.Exists(logFilePath)) {
                    logLines = File.ReadAllLines(logFilePath).ToList();
                }

                // Подготавливаем новые записи
                string timestamp = DateTime.Now.ToString("dd.MM.yyyy (dddd) HH:mm:ss");
                logLines.Add(string.Format("[{0}] === SYSTEM CLEANING STARTED ===", timestamp));

                EnablePrivilegesStatic();

                // ... (код получения настроек из реестра остается прежним) ...
                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\AmazeMem");
                bool chkWS = true, chkSL = true, chkML = true, chkP0 = true;
                if (key != null) {
                    chkWS = (int)key.GetValue("chkWS", 1) == 1;
                    chkSL = (int)key.GetValue("chkSL", 1) == 1;
                    chkML = (int)key.GetValue("chkML", 1) == 1;
                    chkP0 = (int)key.GetValue("chkP0", 1) == 1;
                    key.Close();
                }

                // Очистка Working Sets
                if (chkWS) {
                    int total = 0, success = 0;
                    foreach (Process p in Process.GetProcesses()) {
                        total++;
                        try { if (EmptyWorkingSet(p.Handle)) success++; } catch { }
                    }
                    logLines.Add(string.Format("[{0}] -> Working Sets: {1}/{2} OK", DateTime.Now.ToString("HH:mm:ss"), success, total));
                }

                // Очистка Standby List
                if (chkSL) {
                    IntPtr pEnum = Marshal.AllocHGlobal(sizeof(int));
                    try {
                        Marshal.WriteInt32(pEnum, MemoryPurgeStandbyList);
                        uint res = NtSetSystemInformation(SystemMemoryListInformation, pEnum, sizeof(int));
                        logLines.Add(string.Format("[{0}] -> Standby List: {1}", DateTime.Now.ToString("HH:mm:ss"), res == 0 ? "OK" : "BLOCKED"));
                    } finally { Marshal.FreeHGlobal(pEnum); }
                }

                // Очистка Modified List
                if (chkML) {
                    IntPtr pEnum = Marshal.AllocHGlobal(sizeof(int));
                    try {
                        Marshal.WriteInt32(pEnum, MemoryFlushModifiedList);
                        uint res = NtSetSystemInformation(SystemMemoryListInformation, pEnum, sizeof(int));
                        logLines.Add(string.Format("[{0}] -> Modified List: {1}", DateTime.Now.ToString("HH:mm:ss"), res == 0 ? "OK" : "ERROR"));
                    } finally { Marshal.FreeHGlobal(pEnum); }
                }

                // Очистка Priority 0
                if (chkP0) {
                    IntPtr pEnum = Marshal.AllocHGlobal(sizeof(int));
                    try {
                        Marshal.WriteInt32(pEnum, 5); // 5 = MemoryPurgeLowPriorityStandbyList
                        uint res = NtSetSystemInformation(SystemMemoryListInformation, pEnum, sizeof(int));
                        logLines.Add(string.Format("[{0}] -> Priority 0: {1}", DateTime.Now.ToString("HH:mm:ss"), res == 0 ? "OK" : "ERROR"));
                    } finally { Marshal.FreeHGlobal(pEnum); }
                }

                logLines.Add(string.Format("[{0}] === CLEANING FINISHED ===", DateTime.Now.ToString("dd.MM.yyyy (dddd) HH:mm:ss")));

                // КЛЮЧЕВОЙ МОМЕНТ: Обрезаем список до последних 50 строк и сохраняем
                var finalLogs = logLines.Skip(Math.Max(0, logLines.Count - 50)).ToList();
                File.WriteAllLines(logFilePath, finalLogs);

            } catch (Exception ex) {
                try { File.AppendAllText(logFilePath, "CRITICAL ERROR: " + ex.Message + Environment.NewLine); } catch { }
            }
        }

        private static void EnablePrivilegesStatic()
        {
            string[] privileges = { 
                "SeProfileSingleProcessPrivilege", "SeIncreaseQuotaPrivilege", 
                "SeSystemProfilePrivilege", "SeDebugPrivilege", "SeShutdownPrivilege",
                "SeIncreaseBasePriorityPrivilege", "SeLockMemoryPrivilege",
                "SeSystemEnvironmentPrivilege", "SeManageVolumePrivilege"
            };
            try {
                IntPtr token;
                if (OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0020 | 0x0008, out token)) {
                    foreach (string priv in privileges) {
                        TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                        tp.PrivilegeCount = 1;
                        tp.Attributes = 0x00000002;
                        if (LookupPrivilegeValue(null, priv, out tp.Luid)) {
                            AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                }
            } catch { }
        }

        [STAThread]
        static void Main(string[] args) 
        { 
            Application.EnableVisualStyles();
            
            // Режим очищення (викликається Task Scheduler як SYSTEM)
            if (args.Contains("/clean")) {
                if (!IsAdmin()) {
                    ProcessStartInfo psi = new ProcessStartInfo() { Verb = "runas", FileName = Application.ExecutablePath, Arguments = "/clean" };
                    try { Process.Start(psi); } catch {}
                    return;
                }
                RunCleanerMode();
                return;
            }
            
            if (args.Contains("/min")) startMinimized = true;

            // Перевірка на адміна
            if (!IsAdmin()) {
                ProcessStartInfo psi = new ProcessStartInfo() { Verb = "runas", FileName = Application.ExecutablePath };
                if (startMinimized) psi.Arguments = "/min";
                try { Process.Start(psi); } catch {}
                return;
            }

            // Копіювання в Program Files
            if (!string.Equals(Application.ExecutablePath, targetPath, StringComparison.OrdinalIgnoreCase)) {
                if (File.Exists(targetPath)) {
                    Version currentVer = new Version(CurrentVersion);
                    Version installedVer = new Version(GetFileVersion(targetPath));
                    if (currentVer > installedVer) {
                        try { File.Copy(Application.ExecutablePath, targetPath, true); } catch { }
                    }
                } else {
                    try {
                        if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);
                        File.Copy(Application.ExecutablePath, targetPath, true);
                    } catch { }
                }

                ProcessStartInfo psi = new ProcessStartInfo(targetPath);
                if (startMinimized) psi.Arguments = "/min";
                Process.Start(psi);
                return; 
            }

            // Запуск GUI
            Application.Run(new Form1()); 
        }
    }
}