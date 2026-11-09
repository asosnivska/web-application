$file = Join-Path $PSScriptRoot '.pids'
if (-not (Test-Path $file)) { Write-Host 'Нічого зупиняти'; exit }
foreach ($id in Get-Content $file) {
  taskkill /PID $id /T /F | Out-Null
}
Remove-Item $file
Write-Host 'Усе зупинено'