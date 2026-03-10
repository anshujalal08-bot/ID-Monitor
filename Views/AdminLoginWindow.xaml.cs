using System.Windows;
using System.Windows.Input;
using AutodeskIDMonitor.Services;

namespace AutodeskIDMonitor.Views;

public partial class AdminLoginWindow : Window
{
    private int _attemptCount = 0;
    private const int MaxAttempts = 3;

    public AdminLoginWindow()
    {
        InitializeComponent();
        UsernameBox.Focus();
    }

    private void UsernameBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PasswordBox.Focus();
        }
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AttemptLogin();
        }
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        AttemptLogin();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void AttemptLogin()
    {
        var username = UsernameBox.Text?.Trim() ?? "";
        var password = PasswordBox.Password;

        if (string.IsNullOrEmpty(username))
        {
            ShowError("Please enter your username or email");
            UsernameBox.Focus();
            return;
        }

        if (string.IsNullOrEmpty(password))
        {
            ShowError("Please enter a password");
            PasswordBox.Focus();
            return;
        }

        _attemptCount++;

        if (AdminService.Instance.Login(username, password))
        {
            DialogResult = true;
            Close();
        }
        else
        {
            ShowError("Invalid username or password");
            AttemptText.Text = $"Attempt {_attemptCount} of {MaxAttempts}";
            AttemptText.Visibility = Visibility.Visible;
            
            PasswordBox.SelectAll();
            PasswordBox.Focus();

            if (_attemptCount >= MaxAttempts)
            {
                MessageBox.Show("Too many failed attempts. Please try again later.", 
                    "Login Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                DialogResult = false;
                Close();
            }
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
