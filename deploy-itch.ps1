# deploy-itch.ps1 - upload the Windows build folder to itch.io via butler (auto-installs butler)
#
# Usage:
#   Right-click this file -> Run with PowerShell
#   or:  powershell -ExecutionPolicy Bypass -File deploy-itch.ps1
#
# Target = your itch page. https://rutxkrps.itch.io/nuclear-remind-2026 -> rutxkrps/nuclear-remind-2026

param(
  [string]$Target   = "rutxkrps/nuclear-remind-2026",
  [string]$Channel  = "windows",
  [string]$BuildDir = "$PSScriptRoot\Builds\Windows"
)

$ErrorActionPreference = "Stop"

# --- check the build exists ---
if (-not (Test-Path (Join-Path $BuildDir 'NSC2026.exe'))) {
  Write-Host "ERROR: NSC2026.exe not found in $BuildDir" -ForegroundColor Red
  Write-Host "Build first in Unity: NuclearReMind -> Build -> Build Windows (x64)"
  exit 1
}

# --- 1) install butler if missing (kept in tools\butler) ---
$butlerDir = "$PSScriptRoot\tools\butler"
$butler    = "$butlerDir\butler.exe"
if (-not (Test-Path $butler)) {
  Write-Host "Installing butler (itch.io CLI)..." -ForegroundColor Cyan
  New-Item -ItemType Directory -Force $butlerDir | Out-Null
  $zip = "$butlerDir\butler.zip"
  Invoke-WebRequest -Uri "https://broth.itch.ovh/butler/windows-amd64/LATEST/archive/default" -OutFile $zip
  Expand-Archive -Path $zip -DestinationPath $butlerDir -Force
  Remove-Item $zip -Force
}
& $butler version

# --- 2) login (first run opens a browser to Authorize; remembered afterwards) ---
& $butler login

# --- 3) drop debug symbols we should not ship ---
$noShip = Join-Path $BuildDir "NSC2026_BurstDebugInformation_DoNotShip"
if (Test-Path $noShip) { Remove-Item -Recurse -Force $noShip; Write-Host "Removed debug symbols (_DoNotShip)" }

# --- 4) push to itch.io ---
$dst = "${Target}:${Channel}"
Write-Host "Pushing $BuildDir -> $dst" -ForegroundColor Cyan
& $butler push $BuildDir $dst

Write-Host ""
Write-Host "Done. itch.io is processing the upload." -ForegroundColor Green
Write-Host "Check status: itch.io/dashboard  or run:  $butler status $dst"
