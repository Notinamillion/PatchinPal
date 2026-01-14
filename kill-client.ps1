$processes = Get-Process -Name "PatchinPal.Client" -ErrorAction SilentlyContinue
if ($processes) {
    $processes | Stop-Process -Force
    Write-Host "Killed $($processes.Count) PatchinPal.Client process(es)"
} else {
    Write-Host "No PatchinPal.Client processes found"
}
