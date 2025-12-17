# PatchinPal2 - Windows Update Management System

A centralized Windows Update management solution with client-server architecture, written in .NET Framework 4.8.

## Overview

PatchinPal2 consists of two components:

1. **Client** - Runs on each Windows machine to manage Windows updates locally and receive commands from the server
2. **Server** - Centralized management tool to scan networks, track machines, and remotely manage updates

## Features

### Client Features
- ✅ Check for Windows updates using Windows Update API
- ✅ Install updates with normal or aggressive mode
- ✅ Schedule updates for specific times
- ✅ HTTP server for remote management (REST API)
- ✅ Command-line interface for local control
- ✅ Requires Administrator privileges

### Server Features
- ✅ Network scanning to discover Windows machines
- ✅ JSON-based storage for machine inventory
- ✅ Track IP addresses, hostnames, and OS versions
- ✅ Remote update management via REST API
- ✅ Batch operations on multiple machines
- ✅ Persistent storage of offline machines

## Architecture

```
┌─────────────────┐                    ┌─────────────────┐
│  Server (CLI)   │ ◄──── REST API ───►│  Client (CLI)   │
│  - Scanner      │                    │  - UpdateMgr    │
│  - Repository   │                    │  - HttpServer   │
│  - ClientMgr    │                    │  - Scheduler    │
└─────────────────┘                    └─────────────────┘
        │                                       │
        ▼                                       ▼
  machines.json                         Windows Update API
```

## Prerequisites

- Windows OS (Windows 7/Server 2008 R2 or later)
- .NET Framework 4.8 (pre-installed on Windows 10/11)
- Administrator privileges for both client and server
- Network connectivity between server and clients
- Firewall rules allowing:
  - Client: Inbound TCP port 8090 (configurable)
  - Server: Outbound HTTP to clients

## Installation

### Building from Source

1. Open `PatchinPal2.sln` in Visual Studio 2017 or later
2. Build the solution (Ctrl+Shift+B)
3. Output files will be in:
   - `PatchinPal.Client\bin\Debug\` or `Release\`
   - `PatchinPal.Server\bin\Debug\` or `Release\`

### Using MSBuild (Command Line)

```cmd
cd C:\Users\s.bateman\Programs\PatchinPal2
msbuild PatchinPal2.sln /p:Configuration=Release
```

## Configuration

### Client Configuration

Edit `PatchinPal.Client\App.config`:

```xml
<appSettings>
  <add key="ListenPort" value="8090"/>
  <add key="CheckInterval" value="60"/><!-- Minutes -->
  <add key="LogPath" value="C:\ProgramData\PatchinPal\Client\logs"/>
</appSettings>
```

### Server Configuration

Edit `PatchinPal.Server\App.config`:

```xml
<appSettings>
  <add key="DataPath" value="C:\ProgramData\PatchinPal\Server"/>
  <add key="DefaultClientPort" value="8090"/>
  <add key="ScanThreads" value="50"/>
  <add key="ScanTimeout" value="1000"/><!-- Milliseconds -->
</appSettings>
```

## Usage

### Client Usage

**Run as Service (Recommended):**
```cmd
# Must run as Administrator
PatchinPal.Client.exe service
```

**One-time Commands:**
```cmd
# Check for updates
PatchinPal.Client.exe check

# Install updates
PatchinPal.Client.exe install

# Install updates aggressively
PatchinPal.Client.exe install -aggressive

# Show status
PatchinPal.Client.exe status
```

**Interactive Commands (when running as service):**
```
> check              - Check for updates now
> install            - Install available updates
> install-aggressive - Install with aggressive mode
> status             - Show current status
> exit               - Stop the client
```

### Server Usage

**Start the Server:**
```cmd
# Must run as Administrator
PatchinPal.Server.exe
```

**Available Commands:**

| Command | Description |
|---------|-------------|
| `scan [subnet]` | Scan network for machines (e.g., `scan 192.168.1.0/24`) |
| `list [-online]` | List all discovered machines |
| `status <ip>` | Show detailed status of a specific machine |
| `check <ip\|all>` | Check machine(s) for available updates |
| `update <ip> [-aggressive]` | Install updates on a machine |
| `schedule <ip> <time> [-aggressive]` | Schedule an update |
| `help` | Show help |
| `exit` | Exit the program |

**Examples:**

```cmd
# Scan local subnet
PatchinPal> scan 192.168.1.0/24

# List all online machines
PatchinPal> list -online

# Check a specific machine for updates
PatchinPal> check 192.168.1.100

# Check all online machines
PatchinPal> check all

# Install updates on a machine
PatchinPal> update 192.168.1.100

# Install updates aggressively
PatchinPal> update 192.168.1.100 -aggressive

# Schedule update for tonight at 11 PM
PatchinPal> schedule 192.168.1.100 23:00

# Schedule update for specific date
PatchinPal> schedule 192.168.1.100 2025-11-10 02:00 -aggressive

# Get detailed status
PatchinPal> status 192.168.1.100
```

## Aggressive Mode

Aggressive mode makes updates install faster and with fewer prompts:

- ✅ Accepts all EULAs automatically
- ✅ Suppresses user prompts
- ✅ Forces quiet installation
- ✅ Installs all available updates without waiting
- ⚠️ May require automatic reboot (configurable)

**Use aggressive mode for:**
- Maintenance windows
- Automated deployments
- Critical security updates
- Machines that are behind on patches

## Network Scanning

The server can scan network ranges to discover Windows machines:

**Supported CIDR notations:**
- `/24` - Standard subnet (e.g., `192.168.1.0/24` = 254 addresses)
- `/16` - Large network (e.g., `10.0.0.0/16` = 65,534 addresses)

**What gets detected:**
- ✅ IP address
- ✅ Hostname (via DNS)
- ✅ OS version (if WMI accessible)
- ✅ OS build number
- ✅ PatchinPal client status
- ✅ Online/offline status

**Performance:**
- Multi-threaded scanning (default: 50 threads)
- Configurable timeout per host
- Progress indicator during scan

## Data Storage

### Server Data

Stored in `C:\ProgramData\PatchinPal\Server\machines.json`:

```json
[
  {
    "IpAddress": "192.168.1.100",
    "HostName": "DESKTOP-ABC123",
    "OsVersion": "Microsoft Windows 11 Pro",
    "OsBuild": "22631",
    "IsOnline": true,
    "LastSeen": "2025-11-09T14:30:00",
    "LastUpdateCheck": "2025-11-09T14:25:00",
    "PendingUpdates": 3,
    "ClientPort": 8090,
    "Status": "UpdatesAvailable"
  }
]
```

## Security Considerations

⚠️ **Important Security Notes:**

1. **Administrator Privileges Required** - Both client and server must run as Administrator
2. **Network Authentication** - Currently no authentication between client/server (add if needed)
3. **Firewall Rules** - Ensure proper firewall configuration
4. **WMI Access** - Network scanning requires WMI access (may need credentials)
5. **HTTP (not HTTPS)** - Communication is unencrypted (consider HTTPS for production)

## Troubleshooting

### Client Won't Start
- Ensure running as Administrator
- Check if port 8090 is already in use
- Verify .NET Framework 4.8 is installed

### Server Can't Find Machines
- Check network connectivity (ping machines first)
- Verify firewall allows ICMP and port 8090
- Some machines may block WMI queries
- Ensure client is running on target machines

### Updates Won't Install
- Client must run as Administrator
- Check Windows Update service is running
- Verify internet connectivity on client
- Check disk space availability

### WMI Access Denied
- Requires administrator credentials
- Windows Firewall must allow WMI
- Remote Registry service must be running

## API Reference

### Client REST API

**Endpoints:**

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/ping` | Check if client is online |
| GET | `/api/status` | Get current update status |
| POST | `/api/command` | Execute update command |

**Command Types:**
- `CheckForUpdates` - Search for available updates
- `InstallUpdates` - Install updates
- `ScheduleUpdate` - Schedule future update
- `GetStatus` - Get detailed status
- `CancelScheduledUpdate` - Cancel scheduled update

## Roadmap

Potential future enhancements:
- [ ] HTTPS support for secure communication
- [ ] Authentication between server and clients
- [ ] Web-based dashboard
- [ ] Email notifications
- [ ] Update reports and logs
- [ ] Support for WSUS integration
- [ ] Automatic retry for failed updates
- [ ] Remote reboot capability
- [ ] Update approval workflows

## License

Copyright © 2025. All rights reserved.

## Author

Created by S. Bateman

---

**Version:** 1.0.0
**Last Updated:** November 9, 2025
