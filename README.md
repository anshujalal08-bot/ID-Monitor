# Autodesk ID Monitor v2.0.0 - Complete Package

## Overview

Complete WPF client and server package for monitoring Autodesk license usage across Tangent Landscape Architecture offices.

## Package Contents

```
├── WPF Client (Windows)
│   ├── Models/           - Data models
│   ├── Services/         - Business logic
│   ├── ViewModels/       - MVVM ViewModels  
│   ├── Views/            - XAML windows
│   ├── Controls/         - Custom controls
│   └── *.csproj          - Project file
│
└── Server (Linux/Oracle Cloud)
    ├── app.py                    - SQLite version (lightweight)
    ├── app_postgresql.py         - PostgreSQL version (production)
    ├── deploy_flask.sh           - SQLite deployment
    ├── deploy_postgresql.sh      - PostgreSQL deployment
    ├── requirements.txt          - SQLite dependencies
    └── requirements_postgresql.txt - PostgreSQL dependencies
```

## Server Options

### Option 1: SQLite (Lightweight)
- Single file database
- Zero configuration
- Good for < 50 users
- Use: `app.py`

### Option 2: PostgreSQL (Production) ⭐ Recommended
- Full ACID compliance
- Session-based tracking
- Better for 50+ users
- Supports time-range queries
- Use: `app_postgresql.py`

## Deployment

### Oracle Cloud Free Tier ARM

```bash
# PostgreSQL (Recommended)
chmod +x deploy_postgresql.sh
sudo ./deploy_postgresql.sh

# OR SQLite (Simple)
chmod +x deploy_flask.sh
sudo ./deploy_flask.sh
```

### Configuration

Edit deployment script before running:
```bash
DB_PASS="your-secure-password"  # Change this!
API_KEY="your-api-key"          # Change this!
```

## WPF Client Build

### Requirements
- Windows 10/11
- .NET 8.0 SDK
- Visual Studio 2022 (recommended)

### Build
```bash
dotnet restore
dotnet build -c Release
```

### Publish (Self-Contained)
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Or use: `Publish_SelfContained.bat`

## Client Configuration

After first run, configure in Settings tab:

| Setting | Example |
|---------|---------|
| Cloud API URL | http://your-server.com |
| API Key | Tangent@2026 |
| Country | UAE |
| Office | Dubai |

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/health` | GET | Health check |
| `/api/status` | POST | Update user status |
| `/api/sessions` | GET | Get all active sessions |
| `/api/activity/all` | GET | Get all users' activity today |
| `/api/activity/date/{date}` | GET | Get activity for specific date |
| `/api/activity/range` | GET | Get sessions in time range |
| `/api/projects` | GET | Get active projects |
| `/api/admin/email-usage` | GET | Detect shared licenses |
| `/api/admin/cleanup` | POST | Close stale sessions |
| `/api/export/csv` | GET | Export data as CSV |

## Features

### Client Features
- ✅ Real-time Autodesk login monitoring
- ✅ Revit project tracking
- ✅ Meeting detection (Teams, Zoom, Webex)
- ✅ Idle time tracking
- ✅ Activity breakdown charts
- ✅ Admin dashboard
- ✅ Excel export
- ✅ Auto-update support

### Server Features
- ✅ Session-based tracking (PostgreSQL)
- ✅ Time-range queries
- ✅ Daily summaries
- ✅ Project analytics
- ✅ Budget tracking
- ✅ License sharing detection
- ✅ CSV export

## Troubleshooting

### Client won't connect
1. Verify server URL (include http://)
2. Check API key matches server
3. Test: `curl http://YOUR_SERVER/api/health`

### Server crashes
1. Check logs: `sudo journalctl -u idmonitor -f`
2. Verify PostgreSQL: `sudo systemctl status postgresql`
3. Check memory: `free -h`

### Revit not detected
- Ensure Revit is running with a project open
- Check Debug tab for detection logs

## Support

Contact: anshu.jalaludeen@tangentlandscape.com
