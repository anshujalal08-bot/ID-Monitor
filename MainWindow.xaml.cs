using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AutodeskIDMonitor
{
    public partial class MainWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly DispatcherTimer _uiUpdateTimer;
        private string _lastStatus = "online";

        public MainWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();

            // Setup UI update timer
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;
            _uiUpdateTimer.Start();

            // Initial UI update
            UpdateUI();
            LoadUserInfo();
        }

        private void LoadUserInfo()
        {
            UserNameText.Text = AppSettings.Instance.Username;
            UserInitials.Text = GetInitials(AppSettings.Instance.Username);
            MachineNameText.Text = Environment.MachineName;
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "??";

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            if (parts.Length == 1 && parts[0].Length >= 2)
                return parts[0][..2].ToUpper();
            return name[..Math.Min(2, name.Length)].ToUpper();
        }

        private void UiUpdateTimer_Tick(object? sender, EventArgs e)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            // Update status
            var status = ApiService.GetCurrentStatus();
            UpdateStatusDisplay(status);

            // Update project
            var project = ApiService.GetActiveRevitProject();
            ProjectNameText.Text = string.IsNullOrEmpty(project) ? "No active project" : project;

            // Update server time
            var dxbTime = ApiService.GetDxbTime();
            ServerTimeText.Text = dxbTime.ToString("hh:mm:ss tt");
        }

        private void UpdateStatusDisplay(string status)
        {
            _lastStatus = status;

            switch (status.ToLowerInvariant())
            {
                case "online":
                    StatusText.Text = "Online";
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
                    break;
                case "idle":
                    StatusText.Text = "Idle";
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Yellow
                    break;
                case "locked":
                    StatusText.Text = "Away (Locked)";
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                    break;
                case "break":
                    StatusText.Text = "On Break";
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0, 168, 232)); // Cyan
                    break;
                default:
                    StatusText.Text = "Offline";
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(132, 146, 166)); // Gray
                    break;
            }

            // Update tray tooltip
            TrayIcon.ToolTipText = $"ID Monitor - {StatusText.Text}";
        }

        #region Window Events

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void TrayIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        #endregion

        #region Context Menu Events

        private void OpenDashboard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = $"{AppSettings.Instance.ServerUrl}/dashboard";
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open dashboard: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowStatus_Click(object sender, RoutedEventArgs e)
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"Server: {AppSettings.Instance.ServerUrl}\n" +
                          $"User: {AppSettings.Instance.Username}\n" +
                          $"Status: {_lastStatus}",
                "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ViewLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logsPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AutodeskIDMonitor",
                    "logs"
                );
                
                if (!System.IO.Directory.Exists(logsPath))
                {
                    System.IO.Directory.CreateDirectory(logsPath);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = logsPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open logs: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SignOut_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to sign out?",
                "Sign Out",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await _apiService.LogoutAsync();
                App.RestartApp();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to exit ID Monitor?\n\nYour activity will no longer be tracked.",
                "Exit Application",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _uiUpdateTimer.Stop();
                TrayIcon.Dispose();
                Application.Current.Shutdown();
            }
        }

        #endregion

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Minimize to tray instead of closing
            e.Cancel = true;
            Hide();
        }

        protected override void OnClosed(EventArgs e)
        {
            _uiUpdateTimer.Stop();
            _apiService.Dispose();
            TrayIcon.Dispose();
            base.OnClosed(e);
        }
    }
}
