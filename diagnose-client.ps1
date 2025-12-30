# PatchinPal Client Diagnostic Script
# This script checks if PatchinPal is working and keeping your system updated

Write-Host "=== PatchinPal Client Diagnostics ===" -ForegroundColor Cyan
Write-Host ""

# Check 1: Is the executable present?
Write-Host "[1/8] Checking if PatchinPal Client exists..." -ForegroundColor Yellow
$exePath = "C:\Users\s.bateman\Programs\PatchinPal2\PatchinPal.Client\bin\Debug\PatchinPal.Client.exe"
if (Test-Path $exePath) {
    Write-Host "  ✓ Client executable found" -ForegroundColor Green
} else {
    Write-Host "  ✗ Client executable NOT found at: $exePath" -ForegroundColor Red
    Write-Host "  → You need to build the project first" -ForegroundColor Yellow
}

# Check 2: Is it currently running?
Write-Host "`n[2/8] Checking if PatchinPal Client is running..." -ForegroundColor Yellow
$process = Get-Process -Name "PatchinPal.Client" -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "  ✓ Client is running (PID: $($process.Id))" -ForegroundColor Green
    Write-Host "  → Started at: $($process.StartTime)" -ForegroundColor Gray
} else {
    Write-Host "  ✗ Client is NOT running" -ForegroundColor Red
    Write-Host "  → Start it from the system tray or run the executable" -ForegroundColor Yellow
}

# Check 3: Is it set to run at startup?
Write-Host "`n[3/8] Checking startup configuration..." -ForegroundColor Yellow
$startupKey = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$startupValue = Get-ItemProperty -Path $startupKey -Name "PatchinPalClient" -ErrorAction SilentlyContinue
if ($startupValue) {
    Write-Host "  ✓ Auto-start is ENABLED" -ForegroundColor Green
    Write-Host "  → Command: $($startupValue.PatchinPalClient)" -ForegroundColor Gray
} else {
    Write-Host "  ✗ Auto-start is DISABLED" -ForegroundColor Red
    Write-Host "  → Right-click tray icon → 'Run at Startup'" -ForegroundColor Yellow
}

# Check 4: Is the scheduled task set up?
Write-Host "`n[4/8] Checking scheduled task..." -ForegroundColor Yellow
$task = Get-ScheduledTask -TaskName "PatchinPal2-DailyUpdates" -ErrorAction SilentlyContinue
if ($task) {
    Write-Host "  ✓ Scheduled task exists" -ForegroundColor Green
    Write-Host "  → Status: $($task.State)" -ForegroundColor Gray
    Write-Host "  → Next run: $(Get-ScheduledTaskInfo -TaskName 'PatchinPal2-DailyUpdates' | Select-Object -ExpandProperty NextRunTime)" -ForegroundColor Gray
    Write-Host "  → Last run: $(Get-ScheduledTaskInfo -TaskName 'PatchinPal2-DailyUpdates' | Select-Object -ExpandProperty LastRunTime)" -ForegroundColor Gray
    Write-Host "  → Last result: $(Get-ScheduledTaskInfo -TaskName 'PatchinPal2-DailyUpdates' | Select-Object -ExpandProperty LastTaskResult)" -ForegroundColor Gray
} else {
    Write-Host "  ✗ Scheduled task NOT found" -ForegroundColor Red
    Write-Host "  → Run setup-scheduled-task.ps1 as Administrator" -ForegroundColor Yellow
}

# Check 5: Check Windows Update status
Write-Host "`n[5/8] Checking Windows Update status..." -ForegroundColor Yellow
try {
    $updateSession = New-Object -ComObject Microsoft.Update.Session
    $updateSearcher = $updateSession.CreateUpdateSearcher()
    $searchResult = $updateSearcher.Search("IsInstalled=0 and Type='Software'")

    if ($searchResult.Updates.Count -eq 0) {
        Write-Host "  ✓ System is UP TO DATE (no pending updates)" -ForegroundColor Green
    } else {
        Write-Host "  ! $($searchResult.Updates.Count) update(s) available" -ForegroundColor Yellow
        foreach ($update in $searchResult.Updates | Select-Object -First 5) {
            Write-Host "    - $($update.Title)" -ForegroundColor Gray
        }
        if ($searchResult.Updates.Count -gt 5) {
            Write-Host "    ... and $($searchResult.Updates.Count - 5) more" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "  ✗ Could not check for updates: $($_.Exception.Message)" -ForegroundColor Red
}

# Check 6: Check if HTTP server is listening (port 8090)
Write-Host "`n[6/8] Checking if HTTP API is accessible..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:8090/api/ping" -TimeoutSec 3 -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Host "  ✓ HTTP API is responding on port 8090" -ForegroundColor Green
    }
} catch {
    Write-Host "  ✗ HTTP API not accessible" -ForegroundColor Red
    Write-Host "  → Client may not be running in service mode" -ForegroundColor Yellow
}

# Check 7: Look for log files
Write-Host "`n[7/8] Checking for log files..." -ForegroundColor Yellow
$logPaths = @(
    "C:\ProgramData\PatchinPal\Client\logs",
    "$env:TEMP\PatchinPal"
)
$foundLogs = $false
foreach ($logPath in $logPaths) {
    if (Test-Path $logPath) {
        $logs = Get-ChildItem -Path $logPath -Filter "*.log" -ErrorAction SilentlyContinue
        if ($logs) {
            Write-Host "  ✓ Found $($logs.Count) log file(s) at: $logPath" -ForegroundColor Green
            $foundLogs = $true
        }
    }
}
if (-not $foundLogs) {
    Write-Host "  - No log files found" -ForegroundColor Yellow
    Write-Host "    Logs may not be enabled or client has not run yet" -ForegroundColor Gray
}

# Check 8: Last Windows Update installation
Write-Host "`n[8/8] Checking last Windows Update activity..." -ForegroundColor Yellow
try {
    $session = New-Object -ComObject Microsoft.Update.Session
    $searcher = $session.CreateUpdateSearcher()
    $historyCount = $searcher.GetTotalHistoryCount()

    if ($historyCount -gt 0) {
        $history = $searcher.QueryHistory(0, 5)
        $lastUpdate = $history | Sort-Object Date -Descending | Select-Object -First 1

        Write-Host "  ℹ Last update activity:" -ForegroundColor Cyan
        Write-Host "    - Date: $($lastUpdate.Date)" -ForegroundColor Gray
        Write-Host "    - Title: $($lastUpdate.Title)" -ForegroundColor Gray
        Write-Host "    - Result: $($lastUpdate.ResultCode)" -ForegroundColor Gray

        $daysSince = (Get-Date) - $lastUpdate.Date
        if ($daysSince.Days -gt 30) {
            Write-Host "  ! Last update was $([math]::Round($daysSince.TotalDays)) days ago!" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "  ! Could not retrieve update history" -ForegroundColor Yellow
}

# Summary and recommendations
Write-Host "`n=== Summary & Recommendations ===" -ForegroundColor Cyan

$issues = @()
if (-not (Test-Path $exePath)) { $issues += "Build the project" }
if (-not $process) { $issues += "Start the client application" }
if (-not $startupValue) { $issues += "Enable auto-start" }
if (-not $task) { $issues += "Set up scheduled task" }

if ($issues.Count -eq 0) {
    Write-Host "✓ Everything looks good! PatchinPal should be keeping your system updated." -ForegroundColor Green
} else {
    Write-Host "! Issues found. To fix:" -ForegroundColor Yellow
    foreach ($issue in $issues) {
        Write-Host "  - $issue" -ForegroundColor Yellow
    }
}

Write-Host "`nTo manually check for updates now:" -ForegroundColor Cyan
Write-Host "  $exePath check" -ForegroundColor White
Write-Host "`nTo manually install updates now:" -ForegroundColor Cyan
Write-Host "  $exePath install" -ForegroundColor White
Write-Host ""
