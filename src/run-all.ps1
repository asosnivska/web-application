Set-Location $PSScriptRoot
if (Test-Path (Join-Path $PSScriptRoot '.pids')) { & (Join-Path $PSScriptRoot 'stop-all.ps1') }
$logs = Join-Path $PSScriptRoot 'logs'
New-Item -ItemType Directory -Force $logs | Out-Null
Write-Host 'Building the project...'
dotnet build --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host 'Build failed'; exit 1 }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$script:ids = @()

function Start-Svc($name, $dotnetArgs) {
  $p = Start-Process dotnet -ArgumentList $dotnetArgs -WorkingDirectory $PSScriptRoot `
       -WindowStyle Hidden -PassThru `
       -RedirectStandardOutput "$logs\$name.log" -RedirectStandardError "$logs\$name.err.log"
Write-Host "$name started (PID $($p.Id))"
$script:ids += $p.Id
}

Start-Svc 'api-1' 'run --no-build --project Slae.WebApi --no-launch-profile --urls http://localhost:5001 -- --ServerId=api-1'
Start-Svc 'api-2' 'run --no-build --project Slae.WebApi --no-launch-profile --urls http://localhost:5002 -- --ServerId=api-2'
Start-Svc 'worker-1' 'run --no-build --project Slae.Worker -- --ServerId=worker-1'
Start-Svc 'worker-2' 'run --no-build --project Slae.Worker -- --ServerId=worker-2'
foreach ($port in 5001, 5002) {
  for ($i = 0; $i -lt 30; $i++) {
    try {
      Invoke-WebRequest "http://localhost:$port/health" -UseBasicParsing -TimeoutSec 2 | Out-Null
      break
    } catch { Start-Sleep -Seconds 1 }
  }
}
Start-Svc 'loadbalancer' 'run --no-build --project Slae.LoadBalancer --no-launch-profile'
$script:ids | Set-Content (Join-Path $PSScriptRoot '.pids')
Start-Sleep -Seconds 3
Start-Process 'https://localhost:5443/'