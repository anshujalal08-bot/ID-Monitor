using System;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Timers;
using System.Threading.Tasks;

namespace AutodeskIDMonitor
{
    /// <summary>
    /// API service for communicating with the ID Monitor server
    /// </summary>
    public sealed class ApiService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly Timer _heartbeatTimer;
        private readonly Timer _statusTimer;
        private bool _disposed;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        #region Native Methods

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        #endregion

        public ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(AppSettings.Instance.ServerUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            // Set auth header if available
            if (!string.IsNullOrEmpty(AppSettings.Instance.AuthToken))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {AppSettings.Instance.AuthToken}");
            }

            // Setup heartbeat timer
            _heartbeatTimer = new Timer(AppSettings.Instance.HeartbeatIntervalSeconds * 1000);
            _heartbeatTimer.Elapsed += HeartbeatTimer_Elapsed;
            _heartbeatTimer.AutoReset = true;
            _heartbeatTimer.Start();

            // Setup status sync timer
            _statusTimer = new Timer(AppSettings.Instance.StatusSyncIntervalSeconds * 1000);
            _statusTimer.Elapsed += StatusTimer_Elapsed;
            _statusTimer.AutoReset = true;
            _statusTimer.Start();
        }

        #region Authentication

        public async Task<LoginResult> LoginAsync(string username, string password)
        {
            try
            {
                var payload = new { username, password };
                var json = JsonSerializer.Serialize(payload, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/auth/login", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<LoginResponse>(responseJson, JsonOptions);
                    if (result?.Success == true)
                    {
                        // Update settings
                        AppSettings.Instance.AuthToken = result.Token ?? string.Empty;
                        AppSettings.Instance.UserId = result.User?.Id ?? string.Empty;
                        AppSettings.Instance.Username = result.User?.Username ?? string.Empty;
                        AppSettings.Instance.Email = result.User?.Email ?? string.Empty;
                        AppSettings.Instance.IsAdmin = result.User?.IsAdmin ?? false;
                        AppSettings.Instance.IsFirstRun = false;
                        AppSettings.Save();

                        // Update auth header
                        _httpClient.DefaultRequestHeaders.Remove("Authorization");
                        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {result.Token}");

                        return new LoginResult { Success = true };
                    }
                }

                var errorResult = JsonSerializer.Deserialize<ErrorResponse>(responseJson, JsonOptions);
                return new LoginResult { Success = false, Error = errorResult?.Error ?? "Login failed" };
            }
            catch (Exception ex)
            {
                return new LoginResult { Success = false, Error = $"Connection error: {ex.Message}" };
            }
        }

        public async Task<bool> LogoutAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("/api/auth/logout", null);
                
                // Clear settings regardless of response
                AppSettings.Instance.AuthToken = string.Empty;
                AppSettings.Instance.UserId = string.Empty;
                AppSettings.Save();

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ValidateTokenAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/server-time");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ForgotPasswordAsync(string email)
        {
            try
            {
                var payload = new { email };
                var json = JsonSerializer.Serialize(payload, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/auth/forgot-password", content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Status Updates

        public async Task UpdateStatusAsync(string status, string? autodeskUser = null, 
            string? machineName = null, string? projectName = null)
        {
            try
            {
                var payload = new
                {
                    status,
                    autodesk_user = autodeskUser,
                    machine_name = machineName ?? Environment.MachineName,
                    project_name = projectName
                };

                var json = JsonSerializer.Serialize(payload, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _httpClient.PostAsync("/api/status/update", content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Status update failed: {ex.Message}");
            }
        }

        public async Task LogActivityAsync(string activityType, string description, object? metadata = null)
        {
            try
            {
                var payload = new
                {
                    type = activityType,
                    description,
                    metadata
                };

                var json = JsonSerializer.Serialize(payload, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                await _httpClient.PostAsync("/api/activity/log", content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Activity log failed: {ex.Message}");
            }
        }

        #endregion

        #region Detection Methods

        public static string GetCurrentStatus()
        {
            if (IsWorkstationLocked())
                return "locked";

            if (IsIdle())
                return "idle";

            return "online";
        }

        public static bool IsIdle()
        {
            var idleThreshold = TimeSpan.FromMinutes(AppSettings.Instance.IdleThresholdMinutes);
            return GetIdleTime() > idleThreshold;
        }

        public static TimeSpan GetIdleTime()
        {
            var lastInput = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (GetLastInputInfo(ref lastInput))
            {
                var idleTime = Environment.TickCount - lastInput.dwTime;
                return TimeSpan.FromMilliseconds(idleTime);
            }
            return TimeSpan.Zero;
        }

        public static bool IsWorkstationLocked()
        {
            try
            {
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                    return true;

                _ = GetWindowThreadProcessId(foregroundWindow, out uint processId);
                if (processId == 0)
                    return true;

                var process = Process.GetProcessById((int)processId);
                return process.ProcessName.Equals("LockApp", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string? GetActiveAutodeskUser()
        {
            // Check for Revit, AutoCAD, Civil 3D, etc.
            var autodeskProcesses = new[] { "Revit", "acad", "Civil3D", "Navisworks", "3dsmax" };

            foreach (var processName in autodeskProcesses)
            {
                var processes = Process.GetProcessesByName(processName);
                if (processes.Length > 0)
                {
                    // Return the Windows username as Autodesk user identifier
                    return Environment.UserName;
                }
            }

            return null;
        }

        public static string? GetActiveRevitProject()
        {
            try
            {
                var revitProcesses = Process.GetProcessesByName("Revit");
                if (revitProcesses.Length > 0)
                {
                    var mainWindow = revitProcesses[0].MainWindowTitle;
                    if (!string.IsNullOrEmpty(mainWindow))
                    {
                        // Extract project name from window title
                        // Format: "Project Name - Autodesk Revit 2024"
                        var dashIndex = mainWindow.IndexOf(" - ", StringComparison.Ordinal);
                        if (dashIndex > 0)
                        {
                            return mainWindow[..dashIndex].Trim();
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        #endregion

        #region Timer Callbacks

        private async void HeartbeatTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (_disposed) return;

            try
            {
                var status = GetCurrentStatus();
                var autodeskUser = GetActiveAutodeskUser();
                var projectName = GetActiveRevitProject();

                await UpdateStatusAsync(status, autodeskUser, null, projectName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Heartbeat failed: {ex.Message}");
            }
        }

        private async void StatusTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            if (_disposed) return;

            try
            {
                var status = GetCurrentStatus();
                var autodeskUser = GetActiveAutodeskUser();
                var projectName = GetActiveRevitProject();

                await UpdateStatusAsync(status, autodeskUser, null, projectName);

                // Log project change if applicable
                if (!string.IsNullOrEmpty(projectName))
                {
                    await LogActivityAsync("project_activity", $"Working on: {projectName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Status sync failed: {ex.Message}");
            }
        }

        #endregion

        #region DXB Time

        public static DateTime GetDxbTime()
        {
            return DateTime.UtcNow.AddHours(4);
        }

        public async Task<DateTime?> GetServerTimeAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/server-time");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ServerTimeResponse>(json, JsonOptions);
                    if (result != null && DateTime.TryParse(result.ServerTimeDxb, out var serverTime))
                    {
                        return serverTime;
                    }
                }
            }
            catch
            {
                // Fall back to calculated time
            }

            return GetDxbTime();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            
            _heartbeatTimer.Stop();
            _heartbeatTimer.Dispose();
            
            _statusTimer.Stop();
            _statusTimer.Dispose();
            
            _httpClient.Dispose();

            GC.SuppressFinalize(this);
        }

        #endregion
    }

    #region Response Models

    public class LoginResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public UserInfo? User { get; set; }
        public string? ServerTimeDxb { get; set; }
    }

    public class UserInfo
    {
        public string? Id { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public bool IsAdmin { get; set; }
        public string? CurrentStatus { get; set; }
    }

    public class ErrorResponse
    {
        public string? Error { get; set; }
    }

    public class ServerTimeResponse
    {
        public string? ServerTimeDxb { get; set; }
        public string? Timestamp { get; set; }
        public string? Timezone { get; set; }
    }

    #endregion
}
