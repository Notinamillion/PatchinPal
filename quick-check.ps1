# Quick PatchinPal Status Check
Write-Host "=== PatchinPal Quick Status Check ===" -ForegroundColor Cyan
Write-Host ""

# Check if running
$process = Get-Process -Name "PatchinPal.Client" -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "[OK] Client is running" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Client is NOT running" -ForegroundColor Red
}

# Check scheduled task
$task = Get-ScheduledTask -TaskName "PatchinPal2-DailyUpdates" -ErrorAction SilentlyContinue
if ($task) {
    Write-Host "[OK] Scheduled task exists - State: $($task.State)" -ForegroundColor Green
    $taskInfo = Get-ScheduledTaskInfo -TaskName "PatchinPal2-DailyUpdates"
    Write-Host "     Next run: $($taskInfo.NextRunTime)" -ForegroundColor Gray
} else {
    Write-Host "[ERROR] Scheduled task NOT found" -ForegroundColor Red
}

# Check for Windows Updates
Write-Host "`nChecking for available updates..." -ForegroundColor Yellow
try {
    $updateSession = New-Object -ComObject Microsoft.Update.Session
    $updateSearcher = $updateSession.CreateUpdateSearcher()
    $searchResult = $updateSearcher.Search("IsInstalled=0 and Type='Software'")

    if ($searchResult.Updates.Count -eq 0) {
        Write-Host "[OK] System is UP TO DATE" -ForegroundColor Green
    } else {
        Write-Host "[PENDING] $($searchResult.Updates.Count) updates available:" -ForegroundColor Yellow
        foreach ($update in $searchResult.Updates | Select-Object -First 5) {
            Write-Host "  - $($update.Title)" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "[ERROR] Could not check for updates" -ForegroundColor Red
}

Write-Host ""
