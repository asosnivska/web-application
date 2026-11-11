$file = Join-Path $PSScriptRoot '.pids'
if (-not (Test-Path $file)) { Write-Host 'Nothing to stop'; exit }
foreach ($id in Get-Content $file) {
	taskkill /PID $id /T /F 2>$null | Out-Null
}
Remove-Item $file
Write-Host 'Everything stopped'