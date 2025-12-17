# PatchinPal2 - Quick Start Guide

Get up and running with PatchinPal2 in minutes!

## Step 1: Build the Solution

```cmd
cd C:\Users\s.bateman\Programs\PatchinPal2
msbuild PatchinPal2.sln /p:Configuration=Release
```

Or open `PatchinPal2.sln` in Visual Studio and press **F5** to build.

## Step 2: Set Up the Client

### On Each Machine You Want to Manage:

1. **Copy the client files** to the target machine:
   ```
   PatchinPal.Client\bin\Release\
   ├── PatchinPal.Client.exe
   ├── PatchinPal.Common.dll
   └── PatchinPal.Client.exe.config
   ```

2. **Configure firewall** to allow inbound connections on port 8090:
   ```cmd
   netsh advfirewall firewall add rule name="PatchinPal Client" dir=in action=allow protocol=TCP localport=8090
   ```

3. **Run the client as Administrator**:
   ```cmd
   # Right-click → Run as Administrator
   PatchinPal.Client.exe service
   ```

4. You should see:
   ```
   ====================================
       PatchinPal Client v1.0
       Windows Update Management
   ====================================

   Starting PatchinPal Client on port 8090...
   HTTP Server listening on port 8090
   Client is running.
   ```

## Step 3: Set Up the Server

### On Your Management Machine:

1. **Copy the server files**:
   ```
   PatchinPal.Server\bin\Release\
   ├── PatchinPal.Server.exe
   ├── PatchinPal.Common.dll
   └── PatchinPal.Server.exe.config
   ```

2. **Run the server as Administrator**:
   ```cmd
   # Right-click → Run as Administrator
   PatchinPal.Server.exe
   ```

3. You should see:
   ```
   ========================================
       PatchinPal Server v1.0
       Central Update Management
   ========================================

   PatchinPal>
   ```

## Step 4: Discover Machines

Scan your network to find Windows machines:

```cmd
PatchinPal> scan 192.168.1.0/24
```

Replace `192.168.1.0/24` with your network subnet.

Wait for the scan to complete. You'll see:
```
Scanning network: 192.168.1.0/24
Scanning 254 IP addresses with 50 threads...

[+] Found: 192.168.1.100 (DESKTOP-ABC123)
[+] Found: 192.168.1.105 (LAPTOP-XYZ789)

Scan complete! Found 2 machine(s)
```

## Step 5: List Machines

```cmd
PatchinPal> list
```

Output:
```
IP Address      Hostname             OS Version                     Status          Updates
-----------------------------------------------------------------------------------------------
192.168.1.100   DESKTOP-ABC123       Microsoft Windows 11 Pro       Online          0
192.168.1.105   LAPTOP-XYZ789        Microsoft Windows 10 Pro       Online          0
```

## Step 6: Check for Updates

Check a single machine:
```cmd
PatchinPal> check 192.168.1.100
```

Or check all online machines:
```cmd
PatchinPal> check all
```

## Step 7: Install Updates

Install updates on a specific machine:
```cmd
PatchinPal> update 192.168.1.100
```

Or use aggressive mode for faster installation:
```cmd
PatchinPal> update 192.168.1.100 -aggressive
```

## Step 8: Schedule Updates

Schedule an update for tonight at 11 PM:
```cmd
PatchinPal> schedule 192.168.1.100 23:00
```

With aggressive mode:
```cmd
PatchinPal> schedule 192.168.1.100 23:00 -aggressive
```

## Common Workflows

### Workflow 1: Daily Update Check

```cmd
# Check all machines for updates
PatchinPal> check all

# List machines with updates available
PatchinPal> list -online

# Update specific machines
PatchinPal> update 192.168.1.100
PatchinPal> update 192.168.1.105
```

### Workflow 2: Scheduled Maintenance Window

```cmd
# Schedule updates for all machines at 2 AM with aggressive mode
PatchinPal> schedule 192.168.1.100 2025-11-10 02:00 -aggressive
PatchinPal> schedule 192.168.1.105 2025-11-10 02:00 -aggressive
```

### Workflow 3: Emergency Patch Deployment

```cmd
# Scan network
PatchinPal> scan 192.168.1.0/24

# Check all for updates
PatchinPal> check all

# Update all with aggressive mode (one by one)
PatchinPal> update 192.168.1.100 -aggressive
PatchinPal> update 192.168.1.105 -aggressive
```

## Testing Locally

You can test on a single machine:

1. **Start the client** in one Command Prompt (as Administrator):
   ```cmd
   cd PatchinPal.Client\bin\Release
   PatchinPal.Client.exe service
   ```

2. **Start the server** in another Command Prompt (as Administrator):
   ```cmd
   cd PatchinPal.Server\bin\Release
   PatchinPal.Server.exe
   ```

3. **Add localhost** manually:
   ```cmd
   PatchinPal> scan 127.0.0.1/32
   ```

4. **Test commands**:
   ```cmd
   PatchinPal> check 127.0.0.1
   PatchinPal> status 127.0.0.1
   ```

## Tips & Tricks

### Tip 1: Keyboard Shortcuts
- Press **Up Arrow** to recall previous commands (server CLI)
- Use **Tab** for auto-complete (if supported by your terminal)

### Tip 2: Running as Windows Service
To run client as a Windows service:
```cmd
sc create PatchinPalClient binPath= "C:\Path\To\PatchinPal.Client.exe service" start= auto
sc start PatchinPalClient
```

### Tip 3: Firewall Quick Setup
```cmd
# On client machines
netsh advfirewall firewall add rule name="PatchinPal Client" dir=in action=allow protocol=TCP localport=8090
```

### Tip 4: Check Logs
- Client logs: `C:\ProgramData\PatchinPal\Client\logs`
- Server data: `C:\ProgramData\PatchinPal\Server\machines.json`

## Troubleshooting Quick Fixes

### "Access Denied" Error
→ **Solution**: Run as Administrator

### "Port already in use"
→ **Solution**: Change port in `App.config` or stop conflicting service

### "No machines found"
→ **Solution**:
1. Verify client is running on target machines
2. Check firewall allows port 8090
3. Try pinging the machine first

### Updates Won't Install
→ **Solution**:
1. Run client as Administrator
2. Check Windows Update service: `sc query wuauserv`
3. Verify internet connection

## Next Steps

- Read the full [README.md](README.md) for detailed documentation
- Customize `App.config` files for your environment
- Set up scheduled tasks to run scans automatically
- Consider implementing HTTPS for production use

---

**Need Help?** Check the main README.md or review the source code comments.
