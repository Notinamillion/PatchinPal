using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PatchinPal.Server
{
    /// <summary>
    /// Provides minimize-to-tray functionality for console application
    /// </summary>
    public class ConsoleTrayHelper
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        private NotifyIcon _trayIcon;
        private IntPtr _consoleWindow;
        private bool _isMinimizedToTray = false;

        public ConsoleTrayHelper()
        {
            _consoleWindow = GetConsoleWindow();
            CreateTrayIcon();
        }

        private void CreateTrayIcon()
        {
            // Create simple context menu
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Restore Window", null, OnRestoreWindow);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add("Exit", null, OnExit);

            // Create tray icon (hidden by default)
            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = contextMenu,
                Visible = false,
                Text = "PatchinPal Server"
            };

            _trayIcon.DoubleClick += OnRestoreWindow;
        }

        /// <summary>
        /// Check if console should minimize to tray
        /// Call this periodically from your main loop
        /// </summary>
        public void CheckMinimized()
        {
            if (_consoleWindow == IntPtr.Zero)
                return;

            bool isVisible = IsWindowVisible(_consoleWindow);

            // If window was minimized (not visible) and not already in tray
            if (!isVisible && !_isMinimizedToTray)
            {
                MinimizeToTray();
            }
        }

        /// <summary>
        /// Minimize console to system tray
        /// </summary>
        public void MinimizeToTray()
        {
            if (_consoleWindow != IntPtr.Zero)
            {
                ShowWindow(_consoleWindow, SW_HIDE);
                _trayIcon.Visible = true;
                _trayIcon.ShowBalloonTip(2000, "PatchinPal Server",
                    "Minimized to tray. Double-click icon to restore.",
                    ToolTipIcon.Info);
                _isMinimizedToTray = true;
            }
        }

        /// <summary>
        /// Restore console window from tray
        /// </summary>
        public void RestoreFromTray()
        {
            if (_consoleWindow != IntPtr.Zero)
            {
                ShowWindow(_consoleWindow, SW_SHOW);
                _trayIcon.Visible = false;
                _isMinimizedToTray = false;
            }
        }

        private void OnRestoreWindow(object sender, EventArgs e)
        {
            RestoreFromTray();
        }

        private void OnExit(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Exit PatchinPal Server?\n\nClients will not be able to report status while the server is stopped.",
                "Confirm Exit",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                Cleanup();
                Environment.Exit(0);
            }
        }

        public void Cleanup()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }
    }
}
