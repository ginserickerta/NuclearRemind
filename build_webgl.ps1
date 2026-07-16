# ═══════════════════════════════════════════════════════════════════════════
# build_webgl.ps1 — build Unity WebGL + push ขึ้น itch.io ด้วย butler ในคำสั่งเดียว
#
# ใช้:  cd C:\Users\UsEr\NSC2026
#       .\build_webgl.ps1 -ItchTarget "ชื่อuser/ชื่อเกม:html"
#
# ตัวเลือก:
#   -ItchTarget   ปลายทาง itch.io (แก้ default ด้านล่างครั้งเดียวก็ได้ จะได้ไม่ต้องพิมพ์ทุกรอบ)
#   -SkipBuild    ข้ามขั้น build (push โฟลเดอร์ Builds\WebGL เดิมที่มีอยู่)
#   -SkipPush     build อย่างเดียว ไม่อัปขึ้น itch (เอาไว้เทสต์ local)
#
# เงื่อนไข: ปิด Unity Editor ก่อน (batch build ต้องการ project lock)
#           ติดตั้ง WebGL Build Support ใน Unity Hub แล้ว
#           butler login แล้ว (ครั้งแรกครั้งเดียว)
# ═══════════════════════════════════════════════════════════════════════════
param(
    [string]$ItchTarget = "rutxkrps/nuclear-remind-2026:html",   # https://rutxkrps.itch.io/nuclear-remind-2026
    [switch]$SkipBuild,
    [switch]$SkipPush
)

$Unity    = "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe"
$Project  = $PSScriptRoot
$BuildDir = Join-Path $Project "Builds\WebGL"
$Log      = Join-Path $Project "build_webgl.log"

# ── หา butler: จาก PATH ก่อน ไม่เจอค่อยควานในโฟลเดอร์แอป itch ──
function Find-Butler {
    $cmd = Get-Command butler -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $manual = "C:\Users\UsEr\Tools\butler\butler.exe" # ที่ติดตั้งไว้ด้วยมือ (PATH อาจยังไม่ refresh ใน shell เดิม)
    if (Test-Path $manual) { return $manual }
    $broth = Join-Path $env:LOCALAPPDATA "itch\broth\butler\versions"
    if (Test-Path $broth) {
        $exe = Get-ChildItem $broth -Recurse -Filter butler.exe -ErrorAction SilentlyContinue |
               Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($exe) { return $exe.FullName }
    }
    return $null
}

# ── 1) BUILD ──
if (-not $SkipBuild) {
    if (Test-Path (Join-Path $Project "Temp\UnityLockfile")) {
        Write-Host "⚠ Unity Editor ยังเปิดโปรเจกต์นี้อยู่ — ปิด Editor ก่อนแล้วรันใหม่" -ForegroundColor Yellow
        Write-Host "  (ถ้าปิดแล้วแต่ยังขึ้นข้อความนี้ = lockfile ค้างจาก crash — ลบ Temp\UnityLockfile ทิ้งได้)"
        exit 1
    }

    Write-Host "▶ กำลัง build WebGL... (ครั้งแรกอาจนาน 10-30 นาที — IL2CPP→wasm · log: $Log)" -ForegroundColor Cyan
    & $Unity -batchmode -projectPath $Project `
        -executeMethod NuclearReMind.EditorTools.BuildWebGL.Build `
        -logFile $Log | Out-Null

    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build ล้มเหลว — บรรทัด error ท้าย log:" -ForegroundColor Red
        Select-String -Path $Log -Pattern "error|Error CS|Exception" | Select-Object -Last 10 |
            ForEach-Object { Write-Host "  $($_.Line)" }
        exit 1
    }
    Write-Host "✅ Build สำเร็จ → $BuildDir" -ForegroundColor Green
}

# ── 2) PUSH ──
if ($SkipPush) { Write-Host "ข้าม push ตามสั่ง (-SkipPush) — เทสต์ local: กดเล่นจากเมนู NuclearReMind/Build WebGL แล้วเปิดผ่าน local server"; exit 0 }

if (-not (Test-Path (Join-Path $BuildDir "index.html"))) {
    Write-Host "❌ ไม่พบ $BuildDir\index.html — ยังไม่เคย build? (รันโดยไม่ใส่ -SkipBuild)" -ForegroundColor Red
    exit 1
}
if ($ItchTarget -eq "USER/GAME:html") {
    Write-Host "❌ ยังไม่ได้ตั้งปลายทาง itch.io — ใส่ -ItchTarget `"ชื่อuser/ชื่อเกม:html`" หรือแก้ default ในไฟล์นี้" -ForegroundColor Red
    exit 1
}

$Butler = Find-Butler
if (-not $Butler) {
    Write-Host "❌ ไม่พบ butler — โหลดจาก https://itchio.itch.io/butler แตก zip แล้วเพิ่มลง PATH · จากนั้น 'butler login' ครั้งแรก" -ForegroundColor Red
    exit 1
}

$Version = Get-Date -Format "yyyy.MM.dd-HHmm"
Write-Host "▶ push → $ItchTarget (version $Version)" -ForegroundColor Cyan
& $Butler push $BuildDir $ItchTarget --userversion $Version
if ($LASTEXITCODE -ne 0) { Write-Host "❌ butler push ล้มเหลว (ยังไม่ login? รัน 'butler login')" -ForegroundColor Red; exit 1 }

Write-Host "✅ เสร็จ — เช็คสถานะ: butler status $ItchTarget" -ForegroundColor Green
Write-Host "   ครั้งแรกอย่าลืมตั้งหน้าเกมบน itch.io: Kind = HTML + ติ๊ก 'This file will be played in the browser'"
