# PatchinPal Client Installation Script
# Run this as Administrator to set up PatchinPal on this machine

Write-Host "=== PatchinPal Client Installation ===" -ForegroundColor Cyan
Write-Host ""

# Check if running as admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[ERROR] This script requires Administrator privileges" -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    pause
    exit 1
}

$projectPath = "C:\Users\s.bateman\Programs\PatchinPal2"
$clientExe = "$projectPath\PatchinPal.Client\bin\Debug\PatchinPal.Client.exe"

# Step 1: Check if executable exists
Write-Host "[1/4] Checking if client executable exists..." -ForegroundColor Yellow
if (Test-Path $clientExe) {
    Write-Host "  ✓ Found: $clientExe" -ForegroundColor Green
} else {
    Write-Host "  ✗ Client executable not found" -ForegroundColor Red
    Write-Host "  You need to build the project first in Visual Studio" -ForegroundColor Yellow
    Write-Host "  Or run: msbuild $projectPath\PatchinPal2.sln /p:Configuration=Debug" -ForegroundColor Gray
    pause
    exit 1
}

# Step 2: Start the client in tray mode
Write-Host "`n[2/4] Starting PatchinPal Client..." -ForegroundColor Yellow
$existingProcess = Get-Process -Name "PatchinPal.Client" -ErrorAction SilentlyContinue
if ($existingProcess) {
    Write-Host "  ✓ Client is already running" -ForegroundColor Green
} else {
    Start-Process -FilePath $clientExe -ArgumentList "/background" -WindowStyle Hidden
    Start-Sleep -Seconds 2

    $process = Get-Process -Name "PatchinPal.Client" -ErrorAction SilentlyContinue
    if ($process) {
        Write-Host "  ✓ Client started successfully (PID: $($process.Id))" -ForegroundColor Green
        Write-Host "  Check your system tray for the PatchinPal icon" -ForegroundColor Gray
    } else {
        Write-Host "  ✗ Failed to start client" -ForegroundColor Red
        pause
        exit 1
    }
}

# Step 3: Set up scheduled task for daily updates
Write-Host "`n[3/4] Creating scheduled task for daily updates..." -ForegroundColor Yellow
$taskName = "PatchinPal2-DailyUpdates"
$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

if ($existingTask) {
    Write-Host "  ! Task already exists, removing old one..." -ForegroundColor Yellow
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

$action = New-ScheduledTaskAction -Execute $clientExe -Argument "install"
$trigger = New-ScheduledTaskTrigger -Daily -At "3:00AM"
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2)

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description "Daily Windows Updates via PatchinPal2" -Force | Out-Null

Write-Host "  ✓ Scheduled task created successfully" -ForegroundColor Green
Write-Host "  Will run daily at 3:00 AM" -ForegroundColor Gray

# Step 4: Enable auto-start
Write-Host "`n[4/4] Enabling auto-start on Windows login..." -ForegroundColor Yellow
$startupKey = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$appName = "PatchinPalClient"

try {
    Set-ItemProperty -Path $startupKey -Name $appName -Value "`"$clientExe`" /background" -ErrorAction Stop
    Write-Host "  ✓ Auto-start enabled" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Failed to enable auto-start: $_" -ForegroundColor Red
}

# Summary
Write-Host "`n=== Installation Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "PatchinPal Client is now configured:" -ForegroundColor Cyan
Write-Host "  ✓ Running in system tray" -ForegroundColor Green
Write-Host "  ✓ Daily updates scheduled for 3:00 AM" -ForegroundColor Green
Write-Host "  ✓ Auto-start on Windows login enabled" -ForegroundColor Green
Write-Host ""
Write-Host "To manually check for updates:" -ForegroundColor Yellow
Write-Host "  Right-click the tray icon → Check for Updates" -ForegroundColor White
Write-Host ""
Write-Host "To test the scheduled task now:" -ForegroundColor Yellow
Write-Host "  schtasks /Run /TN `"PatchinPal2-DailyUpdates`"" -ForegroundColor White
Write-Host ""

pause
