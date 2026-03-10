using System;
using System.Windows;
using System.Windows.Input;

namespace AutodeskIDMonitor
{
    public partial class LoginWindow : Window
    {
        private ApiService? _apiService;

        public event EventHandler? LoginSuccessful;

        public LoginWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();

            // Load saved username
            if (!string.IsNullOrEmpty(AppSettings.Instance.SavedUsername))
            {
                UsernameTextBox.Text = AppSettings.Instance.SavedUsername;
                RememberMeCheckBox.IsChecked = AppSettings.Instance.RememberMe;
                PasswordBox.Focus();
            }
            else
            {
                UsernameTextBox.Focus();
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async void SignIn_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(username))
            {
                ShowError("Please enter your username");
                UsernameTextBox.Focus();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Please enter your password");
                PasswordBox.Focus();
                return;
            }

            // Disable UI during login
            SetLoading(true);
            HideError();

            try
            {
                _apiService ??= new ApiService();
                var result = await _apiService.LoginAsync(username, password);

                if (result.Success)
                {
                    // Save preferences
                    if (RememberMeCheckBox.IsChecked == true)
                    {
                        AppSettings.Instance.SavedUsername = username;
                        AppSettings.Instance.RememberMe = true;
                    }
                    else
                    {
                        AppSettings.Instance.SavedUsername = string.Empty;
                        AppSettings.Instance.RememberMe = false;
                    }
                    AppSettings.Save();

                    // Notify success
                    LoginSuccessful?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ShowError(result.Error ?? "Login failed. Please try again.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Connection error: {ex.Message}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private async void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameTextBox.Text.Trim();

            if (string.IsNullOrEmpty(username))
            {
                ShowError("Please enter your username or email first");
                UsernameTextBox.Focus();
                return;
            }

            // Check if it's a valid email
            if (!IsValidEmail(username) && !username.Contains('@'))
            {
                ShowError("Please enter your email address to reset password");
                return;
            }

            SetLoading(true);
            HideError();

            try
            {
                _apiService ??= new ApiService();
                var success = await _apiService.ForgotPasswordAsync(username);

                if (success)
                {
                    MessageBox.Show(
                        "If this email is registered, you will receive a password reset link shortly.",
                        "Password Reset",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    ShowError("Failed to send reset email. Please try again.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Error: {ex.Message}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorBorder.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorBorder.Visibility = Visibility.Collapsed;
        }

        private void SetLoading(bool isLoading)
        {
            SignInButton.IsEnabled = !isLoading;
            SignInButton.Content = isLoading ? "Signing in..." : "Sign In";
            UsernameTextBox.IsEnabled = !isLoading;
            PasswordBox.IsEnabled = !isLoading;
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _apiService?.Dispose();
            base.OnClosed(e);
        }
    }
}
