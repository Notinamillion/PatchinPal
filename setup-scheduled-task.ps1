# PatchinPal2 Scheduled Task Setup
# This creates a daily task to run Windows updates at 3:00 AM

$exePath = "C:\Users\s.bateman\Programs\PatchinPal2\PatchinPal.Client\bin\Debug\PatchinPal.Client.exe"
$taskName = "PatchinPal2-DailyUpdates"

# Create the action
$action = New-ScheduledTaskAction -Execute $exePath -Argument "install"

# Create the trigger (daily at 3:00 AM)
$trigger = New-ScheduledTaskTrigger -Daily -At "3:00AM"

# Create the principal (run as current user with highest privileges)
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERNAME" -RunLevel Highest

# Create settings
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2)

# Register the task
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description "Daily Windows Updates via PatchinPal2" -Force

Write-Host "Successfully created scheduled task: $taskName"
Write-Host "The task will run daily at 3:00 AM"
Write-Host ""
Write-Host "To test it now, run:"
Write-Host "  schtasks /Run /TN `"$taskName`""
