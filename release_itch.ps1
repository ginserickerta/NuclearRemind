<#
  release_itch.ps1 — build NUCLEAR Re:Mind แล้วอัปขึ้น itch.io ทั้ง 2 แบบในคำสั่งเดียว

    WebGL (เล่นบนเว็บ)  Build\WebGL      → rutxkrps/nuclear-remind-2026:html
    Windows (โหลดไปเล่น) Builds\Windows  → rutxkrps/nuclear-remind-2026:windows

  ⚠ ต้องปิด Unity Editor ก่อน — Unity ล็อกโปรเจกต์ไว้ (สคริปต์เช็คให้และหยุดเองถ้ายังเปิดอยู่)

  ตัวอย่าง:
    .\release_itch.ps1                    # build ทั้งคู่ แล้ว push ทั้งคู่
    .\release_itch.ps1 -SkipWindows       # เอาแค่ WebGL
    .\release_itch.ps1 -SkipBuild         # ไม่ build ใหม่ อัปของที่มีอยู่
    .\release_itch.ps1 -SkipPush          # build อย่างเดียว ไม่อัป
#>
param(
  [string]$Target      = "rutxkrps/nuclear-remind-2026",
  [string]$WebChannel  = "html",
  [string]$WinChannel  = "windows",
  [switch]$SkipWebGL,
  [switch]$SkipWindows,
  [switch]$SkipBuild,
  [switch]$SkipPush
)

$ErrorActionPreference = "Stop"
$root   = "C:\Users\UsEr\NSC2026"
$unity  = "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe"
$webDir = Join-Path $root "Build\WebGL"
$winDir = Join-Path $root "Builds\Windows"
# InvariantCulture so the year is Gregorian (2026), not the Thai Buddhist year (2569)
$stamp  = (Get-Date).ToString("yyyy.MM.dd-HHmm", [System.Globalization.CultureInfo]::InvariantCulture)

function Say($msg) { Write-Host "`n=== $msg" -ForegroundColor Cyan }
function Die($msg) { Write-Host "`nหยุด: $msg" -ForegroundColor Red; exit 1 }

# ── butler ─────────────────────────────────────────────────────────────
$butler = $null
foreach ($p in @(
    (Get-Command butler -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "C:\Users\UsEr\Tools\butler\butler.exe",
    "C:\Users\UsEr\bin\butler\butler.exe")) {
  if ($p -and (Test-Path $p)) { $butler = $p; break }
}
if (-not $SkipPush -and -not $butler) { Die "หา butler.exe ไม่เจอ — ติดตั้งจาก https://itch.io/docs/butler/" }

# ── Unity ต้องปิด (project lock) ───────────────────────────────────────
if (-not $SkipBuild) {
  if (-not (Test-Path $unity)) { Die "ไม่พบ Unity ที่ $unity" }
  $running = Get-Process Unity -ErrorAction SilentlyContinue
  if ($running) {
    Die "Unity Editor ยังเปิดอยู่ (PID $($running.Id -join ', ')) — เซฟงาน (Ctrl+S) แล้วปิด Unity ก่อน จากนั้นรันสคริปต์นี้ใหม่"
  }
}

function Invoke-UnityBuild($method, $logName, $label) {
  Say "build $label ... (นานหลายนาที · log: $logName)"
  $log = Join-Path $root $logName
  if (Test-Path $log) { Remove-Item $log -Force }

  # Unity batch relaunches itself once: the process we start returns an exit code EARLY while the
  # real worker keeps building. Trusting that exit code reports a false failure mid-build. So we
  # launch, then WAIT for every Unity process to be gone, and judge success from the log instead.
  Start-Process -FilePath $unity -ArgumentList @(
    '-quit','-batchmode','-nographics','-projectPath', $root, '-executeMethod', $method, '-logFile', $log
  )
  Start-Sleep -Seconds 5
  $waited = 0
  while (@(Get-Process Unity -ErrorAction SilentlyContinue).Count -gt 0) {
    Start-Sleep -Seconds 3; $waited += 3
    if ($waited -ge 2700) { Die "build $label เกิน 45 นาที — ดู $log" } # 45-min guard
  }
  Start-Sleep -Seconds 2 # let the log flush

  $ok   = Select-String -Path $log -Pattern 'Build succeeded|✅ build สำเร็จ' -ErrorAction SilentlyContinue
  $errs = Select-String -Path $log -Pattern 'error CS|BuildFailedException|Build .*(Failed|Cancelled)' -ErrorAction SilentlyContinue |
          Select-Object -First 15
  if (-not $ok) {
    if ($errs) { $errs | ForEach-Object { Write-Host $_.Line -ForegroundColor Yellow } }
    Die "build $label ไม่สำเร็จ (ไม่พบ 'Build succeeded' ใน log) — ดู $log"
  }
  Write-Host "build $label สำเร็จ" -ForegroundColor Green
}

function Push-Itch($dir, $channel, $label) {
  if (-not (Test-Path $dir)) { Die "ไม่มีโฟลเดอร์ $dir — build ก่อน" }
  $mb = [math]::Round(((Get-ChildItem $dir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
  Say "push $label ($mb MB) → ${Target}:${channel}  version $stamp"
  & $butler push $dir "${Target}:${channel}" --userversion $stamp
  if ($LASTEXITCODE -ne 0) { Die "butler push $label ไม่สำเร็จ" }
}

# ── WebGL ──────────────────────────────────────────────────────────────
if (-not $SkipWebGL) {
  if (-not $SkipBuild) { Invoke-UnityBuild "NuclearReMind.EditorTools.WebGLBuilder.Build" "build_webgl.log" "WebGL" }
  if (-not $SkipPush)  { Push-Itch $webDir $WebChannel "WebGL" }
}

# ── Windows standalone ─────────────────────────────────────────────────
if (-not $SkipWindows) {
  if (-not $SkipBuild) { Invoke-UnityBuild "NuclearReMind.EditorTools.GameBuilder.BuildWindowsBatch" "build_windows.log" "Windows x64" }

  # Burst debug symbols ห้ามแจก และกินพื้นที่เปล่า ๆ
  $burst = Join-Path $winDir "NSC2026_BurstDebugInformation_DoNotShip"
  if (Test-Path $burst) { Remove-Item $burst -Recurse -Force; Write-Host "ลบ BurstDebugInformation_DoNotShip แล้ว" }

  if (-not $SkipPush) { Push-Itch $winDir $WinChannel "Windows" }
}

Say "เสร็จแล้ว — https://rutxkrps.itch.io/nuclear-remind-2026   (version $stamp)"
if (-not $SkipPush -and $butler) { & $butler status $Target }
