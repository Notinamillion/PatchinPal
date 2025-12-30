# PatchinPal Client - New Features

## What's New

The PatchinPal client now includes **logging**, **update history tracking**, and **configurable notifications**!

---

## 1. Logging System

### Features:
- **Automatic logging** to `C:\ProgramData\PatchinPal\Client\logs\`
- **Daily log files** (e.g., `patchinpal-2025-12-30.log`)
- **Auto-cleanup** of logs older than 30 days
- **Multiple log levels**: Debug, Info, Warning, Error
- **Console + File output** for easy debugging

### Configuration:
Logs are enabled by default. You can adjust settings in:
```
C:\ProgramData\PatchinPal\Client\settings.json
```

```json
{
  "EnableLogging": true,
  "LogLevel": "Info"
}
```

### Log Levels:
- **Debug** - Everything (very verbose)
- **Info** - Normal operations (default)
- **Warning** - Potential issues
- **Error** - Failures and exceptions

---

## 2. Update History

### Features:
- **Persistent history** of all update checks and installations
- **Track successes and failures**
- **View recent activity** from the tray menu
- **JSON-based storage** at `C:\ProgramData\PatchinPal\Client\update-history.json`

### How to View:
Right-click the tray icon → **View Update History**

### What's Tracked:
- ✅ Update checks (how many updates found)
- ✅ Update installations (what was installed, success/failure)
- ✅ Timestamps for all operations
- ✅ Reboot requirements
- ✅ Error messages

### Example History Entry:
```json
{
  "Timestamp": "2025-12-30T14:30:00",
  "Action": "Install",
  "Success": true,
  "Message": "Installation completed successfully",
  "UpdatesInstalled": 4,
  "RebootRequired": true
}
```

---

## 3. Configurable Notifications

### Features:
- **Toggle notifications on/off** from the tray menu
- **Granular control** over different notification types
- **Persistent settings** across restarts

### How to Use:
Right-click tray icon → **Show Notifications** (toggle on/off)

### Notification Types:
You can control each type separately in `settings.json`:

```json
{
  "ShowNotifications": true,
  "ShowCheckNotifications": true,
  "ShowInstallNotifications": true,
  "ShowErrorNotifications": true,
  "ShowSuccessNotifications": true
}
```

### Examples:
- Want updates to run silently? Set `ShowNotifications` to `false`
- Only see errors? Enable only `ShowErrorNotifications`
- Never miss successes? Keep `ShowSuccessNotifications` enabled

---

## 4. Enhanced Tray Menu

### New Menu Items:
- **View Update History** - See what PatchinPal has done
- **Show Notifications** - Quick toggle for all notifications
- **Settings...** - View current configuration

---

## Settings File Location

All settings are stored in:
```
C:\ProgramData\PatchinPal\Client\settings.json
```

### Default Settings:
```json
{
  "ShowNotifications": true,
  "ShowCheckNotifications": true,
  "ShowInstallNotifications": true,
  "ShowErrorNotifications": true,
  "ShowSuccessNotifications": true,
  "EnableLogging": true,
  "LogLevel": "Info",
  "AutoInstallUpdates": false,
  "CheckIntervalMinutes": 60
}
```

---

## Log File Location

Logs are stored in:
```
C:\ProgramData\PatchinPal\Client\logs\patchinpal-YYYY-MM-DD.log
```

### Example Log Output:
```
[2025-12-30 14:30:15] [INFO] PatchinPal Client starting...
[2025-12-30 14:30:16] [INFO] Client started successfully
[2025-12-30 14:35:00] [INFO] User initiated update check
[2025-12-30 14:35:02] [INFO] Found 4 available updates
[2025-12-30 14:40:00] [INFO] User initiated update installation
[2025-12-30 14:42:15] [INFO] Updates installed successfully, RebootRequired=True
```

---

## Update History File Location

History is stored in:
```
C:\ProgramData\PatchinPal\Client\update-history.json
```

### History Limits:
- Keeps last **1,000 entries**
- Automatically prunes oldest entries
- Includes full details of installed updates

---

## How to Use the New Features

### Step 1: Run the Client
Start PatchinPal Client (it will create the settings file automatically)

### Step 2: Check Logs
Navigate to `C:\ProgramData\PatchinPal\Client\logs\` to see what's happening

### Step 3: View History
Right-click tray icon → **View Update History**

### Step 4: Customize Notifications
- Quick toggle: Right-click → **Show Notifications**
- Fine-tune: Edit `settings.json` manually

### Step 5: Adjust Log Level
Edit `settings.json` and change `"LogLevel": "Debug"` for more detailed logs

---

## Troubleshooting

### Logs not appearing?
- Check `C:\ProgramData\PatchinPal\Client\logs\`
- Verify `EnableLogging: true` in settings.json
- Ensure client is running as Administrator

### History not saving?
- Check write permissions to `C:\ProgramData\PatchinPal\Client\`
- View logs for error messages

### Notifications not showing?
- Check `ShowNotifications: true` in settings.json
- Verify Windows notification settings allow PatchinPal

### Settings file missing?
- It's created automatically on first run
- Default settings will be used if file doesn't exist

---

## Benefits

✅ **Know what's happening** - Logs show all activity
✅ **Track your update history** - See when updates were installed
✅ **Control notifications** - Turn them on/off as needed
✅ **Debug issues easily** - Detailed logs help troubleshoot
✅ **Compliance tracking** - History proves updates were applied

---

## Future Enhancements

These features lay the groundwork for:
- Email notifications on failures
- Custom notification sounds
- Export history to CSV/PDF reports
- Integration with centralized logging systems
- Alert thresholds (e.g., notify if X updates fail)

---

**Version**: 2.1
**Last Updated**: December 30, 2025
