using System;
using System.Threading.Tasks;
using System.Windows;

namespace AutodeskIDMonitor
{
    public partial class App : Application
    {
        private MainWindow? _mainWindow;
        private ApiService? _apiService;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Initialize settings
            AppSettings.Load();

            // Check if first run or no auth token
            if (AppSettings.Instance.IsFirstRun || string.IsNullOrEmpty(AppSettings.Instance.AuthToken))
            {
                ShowLoginWindow();
            }
            else
            {
                // Validate existing token
                _ = ValidateAndStartAsync();
            }
        }

        private async Task ValidateAndStartAsync()
        {
            try
            {
                _apiService = new ApiService();
                var isValid = await _apiService.ValidateTokenAsync();

                if (isValid)
                {
                    ShowMainWindow();
                }
                else
                {
                    AppSettings.Instance.AuthToken = string.Empty;
                    AppSettings.Save();
                    ShowLoginWindow();
                }
            }
            catch
            {
                ShowLoginWindow();
            }
        }

        public void ShowLoginWindow()
        {
            Current.Dispatcher.Invoke(() =>
            {
                var loginWindow = new LoginWindow();
                loginWindow.LoginSuccessful += OnLoginSuccessful;
                loginWindow.Show();
            });
        }

        private void OnLoginSuccessful(object? sender, EventArgs e)
        {
            if (sender is Window loginWindow)
            {
                loginWindow.Close();
            }
            ShowMainWindow();
        }

        public void ShowMainWindow()
        {
            Current.Dispatcher.Invoke(() =>
            {
                _mainWindow = new MainWindow();
                _mainWindow.Show();
            });
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            _apiService?.Dispose();
            AppSettings.Save();
        }

        public static void RestartApp()
        {
            var appPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(appPath))
            {
                System.Diagnostics.Process.Start(appPath);
            }
            Current.Shutdown();
        }
    }
}
