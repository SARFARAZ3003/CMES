# ============================================================================
#  iis-setup.ps1  —  CMES ko IIS pe ONE-TIME set karta hai
#  (App Pool + Site banao, Windows Auth sections unlock — web.config se apply).
# ----------------------------------------------------------------------------
#  ADMIN PowerShell se chalao (pehle .\deploy.ps1 chala ke 'publish' bana lo):
#     .\iis-setup.ps1
#     .\iis-setup.ps1 -Path C:\inetpub\CMES -Port 8080
#
#  Phir browser:  http://localhost:<Port>/
# ============================================================================

param(
    [string]$SiteName = "CMES",
    [string]$Path     = "$PSScriptRoot\publish",
    [int]   $Port     = 8080
)

$ErrorActionPreference = "Stop"
$appcmd = "$env:windir\system32\inetsrv\appcmd.exe"

if (-not (Test-Path $appcmd)) { throw "IIS (appcmd) nahi mila -> pehle IIS install karo." }
if (-not (Test-Path $Path))   { throw "Publish folder nahi mila: $Path -> pehle .\deploy.ps1 chalao." }

Write-Host ""
Write-Host "==> Auth sections unlock (taaki web.config ke Windows-Auth settings apply ho, 500.19 na aaye)..." -ForegroundColor Cyan
& $appcmd unlock config /section:system.webServer/security/authentication/windowsAuthentication | Out-Null
& $appcmd unlock config /section:system.webServer/security/authentication/anonymousAuthentication | Out-Null

Write-Host "==> Purana CMES site/pool (agar ho) hatao..." -ForegroundColor Cyan
& $appcmd delete site    "$SiteName" 2>$null | Out-Null
& $appcmd delete apppool "$SiteName" 2>$null | Out-Null

Write-Host "==> App Pool '$SiteName' (No Managed Code)..." -ForegroundColor Cyan
& $appcmd add apppool /name:"$SiteName" /managedRuntimeVersion:"" | Out-Null

Write-Host "==> Site '$SiteName'  (port $Port  ->  $Path)..." -ForegroundColor Cyan
& $appcmd add site /name:"$SiteName" /physicalPath:"$Path" /bindings:"http/*:$Port`:" | Out-Null
& $appcmd set app "$SiteName/" /applicationPool:"$SiteName" | Out-Null

Write-Host ""
Write-Host "DONE ✔  Browser me kholo:  http://localhost:$Port/" -ForegroundColor Green
Write-Host "(Windows Auth ON + Anonymous OFF web.config se aate hain.)" -ForegroundColor Green
Write-Host ""
Write-Host "Reminder: '$Path\appsettings.json' me asli CMES_DB / AUTH_DB / ORACLE_DB daalo." -ForegroundColor Yellow
Write-Host ""
