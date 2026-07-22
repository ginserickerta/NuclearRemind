# Detached watcher — armed once, runs independently of any tool session.
# Waits (up to 12 hours) for the Unity Editor to close, then runs the itch.io release.
# Requires Unity gone for 4 consecutive polls (60s) so a quick restart does not trigger a build.
# Progress + build output go to release_watch.log next to this file.
$log = Join-Path $PSScriptRoot "release_watch.log"
"[{0}] watcher armed - waiting for Unity to close" -f (Get-Date -Format s) | Out-File $log -Encoding utf8

$maxPolls = 2880          # 12 hours at 15s per poll
$goneCount = 0
for ($poll = 0; $poll -lt $maxPolls; $poll++) {
    $running = @(Get-Process Unity -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) { $goneCount = 0; Start-Sleep -Seconds 15; continue }

    $goneCount++
    if ($goneCount -ge 4) {
        "[{0}] Unity closed - starting release" -f (Get-Date -Format s) | Out-File $log -Append -Encoding utf8
        & pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "release_itch.ps1") *>> $log
        "[{0}] release finished (exit {1})" -f (Get-Date -Format s), $LASTEXITCODE | Out-File $log -Append -Encoding utf8
        exit $LASTEXITCODE
    }
    Start-Sleep -Seconds 15
}
"[{0}] TIMEOUT after 12h - Unity never closed, nothing built" -f (Get-Date -Format s) | Out-File $log -Append -Encoding utf8
exit 2
