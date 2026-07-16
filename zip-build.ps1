# zip-build.ps1 - zip the Windows build for manual upload to itch.io (no butler needed)
# Run after building:  powershell -ExecutionPolicy Bypass -File zip-build.ps1

$src = "$PSScriptRoot\Builds\Windows"
$out = "$PSScriptRoot\Builds\nuclear-remind-2026-windows.zip"

if (-not (Test-Path (Join-Path $src 'NSC2026.exe'))) {
  Write-Host "ERROR: NSC2026.exe not found. Build first: Unity -> NuclearReMind -> Build -> Build Windows (x64)" -ForegroundColor Red
  exit 1
}
if (Test-Path $out) { Remove-Item $out -Force }

# zip everything except the debug symbols folder
$items = Get-ChildItem $src | Where-Object { $_.Name -ne 'NSC2026_BurstDebugInformation_DoNotShip' }
Compress-Archive -Path $items.FullName -DestinationPath $out -CompressionLevel Optimal

$mb = (Get-Item $out).Length / 1MB
Write-Host ("Created: $out  ({0:N1} MB)" -f $mb) -ForegroundColor Green
Write-Host "Upload it at: https://rutxkrps.itch.io/nuclear-remind-2026 -> Edit game -> Uploads -> Upload files -> drag this zip"
