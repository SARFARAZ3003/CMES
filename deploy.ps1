# ============================================================================
#  deploy.ps1  —  CMES build + publish for IIS (same-origin)
# ----------------------------------------------------------------------------
#  Kya karta hai:
#    1. React frontend build  (npm install + npm run build)
#    2. Build ko backend\wwwroot me copy  (taaki ek hi site sab serve kare)
#    3. Backend ko Release me publish  (wwwroot + CMES.dll + web.config)
#
#  Usage (PowerShell, repo root se):
#    .\deploy.ps1                       # -> .\publish folder
#    .\deploy.ps1 -Output C:\CMES_publish
#
#  Iske baad: IIS site ko publish folder pe point karo (neeche guide).
# ============================================================================

param(
    [string]$Output = "$PSScriptRoot\publish"
)

$ErrorActionPreference = "Stop"
$root     = $PSScriptRoot
$frontend = Join-Path $root "frontend"
$backend  = Join-Path $root "backend"
$wwwroot  = Join-Path $backend "wwwroot"

Write-Host ""
Write-Host "==> 1/4  React frontend build..." -ForegroundColor Cyan
Push-Location $frontend
try {
    npm install
    npm run build
} finally { Pop-Location }

Write-Host ""
Write-Host "==> 2/4  Build -> backend\wwwroot copy..." -ForegroundColor Cyan
if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }
New-Item -ItemType Directory -Path $wwwroot | Out-Null
Copy-Item (Join-Path $frontend "dist\*") $wwwroot -Recurse

Write-Host ""
Write-Host "==> 3/4  Backend publish (Release) -> $Output ..." -ForegroundColor Cyan
Push-Location $backend
try {
    dotnet publish -c Release -o $Output
} finally { Pop-Location }

Write-Host ""
Write-Host "==> 4/4  DONE." -ForegroundColor Green
Write-Host "Publish output : $Output" -ForegroundColor Green
Write-Host ""
Write-Host "Aage (one-time, IIS pe):" -ForegroundColor Yellow
Write-Host "  1. '$Output' me appsettings.json kholo -> CMES_DB / AUTH_DB / ORACLE_DB asli daalo" -ForegroundColor Yellow
Write-Host "  2. IIS site is folder pe point karo, App Pool = 'No Managed Code'" -ForegroundColor Yellow
Write-Host "  3. Windows Authentication ON, Anonymous OFF" -ForegroundColor Yellow
Write-Host ""
