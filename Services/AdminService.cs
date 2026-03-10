using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AutodeskIDMonitor.Services;

public class AdminService
{
    private const string DEFAULT_PASSWORD = "admin";
    private const string DEFAULT_USERNAME = "admin";
    private const string CONFIG_FILENAME = "admin_config.json";
    
    private static AdminService? _instance;
    private static readonly object _lock = new();
    
    private string _passwordHash = "";
    private string _adminUsername = DEFAULT_USERNAME;
    private string _adminEmail = "";
    private bool _isAdminLoggedIn = false;
    private readonly string _configPath;

    public static AdminService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new AdminService();
                }
            }
            return _instance;
        }
    }

    public event EventHandler<bool>? AdminStatusChanged;
    
    public bool IsAdminLoggedIn => _isAdminLoggedIn;

    private AdminService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutodeskIDMonitor");
        Directory.CreateDirectory(appDataPath);
        _configPath = Path.Combine(appDataPath, CONFIG_FILENAME);
    }

    public void Initialize()
    {
        LoadConfig();
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<Models.AdminConfig>(json);
                if (config != null && !string.IsNullOrEmpty(config.PasswordHash))
                {
                    _passwordHash = config.PasswordHash;
                    _adminUsername = config.AdminUsername ?? DEFAULT_USERNAME;
                    _adminEmail = config.AdminEmail ?? "";
                    return;
                }
            }
        }
        catch { }

        // Set default password hash
        _passwordHash = ComputeHash(DEFAULT_PASSWORD);
        _adminUsername = DEFAULT_USERNAME;
        SaveConfig();
    }

    private void SaveConfig()
    {
        try
        {
            var config = new Models.AdminConfig 
            { 
                PasswordHash = _passwordHash,
                AdminUsername = _adminUsername,
                AdminEmail = _adminEmail
            };
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch { }
    }

    /// <summary>
    /// Login with username (or email) and password.
    /// Accepts: "admin" username, configured admin username, or configured admin email.
    /// </summary>
    public bool Login(string usernameOrEmail, string password)
    {
        if (string.IsNullOrEmpty(usernameOrEmail) || string.IsNullOrEmpty(password))
            return false;

        // Check username match (case-insensitive)
        bool usernameMatch = 
            usernameOrEmail.Equals(DEFAULT_USERNAME, StringComparison.OrdinalIgnoreCase) ||
            usernameOrEmail.Equals(_adminUsername, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(_adminEmail) && usernameOrEmail.Equals(_adminEmail, StringComparison.OrdinalIgnoreCase));

        if (!usernameMatch)
            return false;

        var inputHash = ComputeHash(password);
        
        if (inputHash.Equals(_passwordHash, StringComparison.OrdinalIgnoreCase))
        {
            _isAdminLoggedIn = true;
            AdminStatusChanged?.Invoke(this, true);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Legacy password-only login (kept for backward compatibility).
    /// </summary>
    public bool Login(string password)
    {
        return Login(DEFAULT_USERNAME, password);
    }

    public void Logout()
    {
        _isAdminLoggedIn = false;
        AdminStatusChanged?.Invoke(this, false);
    }

    public bool ChangePassword(string currentPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 4)
            return false;

        var currentHash = ComputeHash(currentPassword);
        if (!currentHash.Equals(_passwordHash, StringComparison.OrdinalIgnoreCase))
            return false;

        _passwordHash = ComputeHash(newPassword);
        SaveConfig();
        return true;
    }

    public bool SetAdminCredentials(string username, string email, string currentPassword, string newPassword)
    {
        var currentHash = ComputeHash(currentPassword);
        if (!currentHash.Equals(_passwordHash, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(username))
            _adminUsername = username;
        if (!string.IsNullOrEmpty(email))
            _adminEmail = email;
        if (!string.IsNullOrEmpty(newPassword) && newPassword.Length >= 4)
            _passwordHash = ComputeHash(newPassword);

        SaveConfig();
        return true;
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
