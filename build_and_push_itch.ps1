# build_and_push_itch.ps1 — Build WebGL แล้ว push ขึ้น itch.io ทีเดียวจบ
# วิธีใช้:  .\build_and_push_itch.ps1 -Target "user/game"
#   -Target = itch username/game-slug ของคุณ (ไม่ต้องใส่ :html5 — สคริปต์เติมให้)
# ต้องทำก่อน: 1) ปิด Unity Editor   2) login itch ครั้งเดียว:
#   & "C:\Users\UsEr\bin\butler\butler.exe" login

param(
    [Parameter(Mandatory=$true)][string]$Target,   # เช่น "auren/nuclear-remind"
    [string]$Channel = "html5"
)

$ErrorActionPreference = "Stop"
$Project = "C:\Users\UsEr\NSC2026"
$Unity   = "C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe"
$Butler  = "C:\Users\UsEr\bin\butler\butler.exe"
$BuildDir = Join-Path $Project "Build\WebGL"
$Log      = Join-Path $Project "build.log"

# 0) กัน batch build ชนกับ Unity ที่เปิดอยู่ (project lock)
if (Get-Process Unity -ErrorAction SilentlyContinue) {
    Write-Error "Unity ยังเปิดอยู่ — ปิด Unity Editor ก่อน (batch build ต้องการ project lock)"
    exit 1
}

# 1) Build WebGL (executeMethod เดียวกับเมนู NuclearReMind/Build WebGL)
Write-Host "==> Building WebGL (ใช้เวลาสักครู่)..." -ForegroundColor Cyan
& $Unity -quit -batchmode -nographics -projectPath $Project `
    -executeMethod NuclearReMind.EditorTools.WebGLBuilder.Build -logFile $Log
if ($LASTEXITCODE -ne 0) { Write-Error "Unity build ล้มเหลว (exit $LASTEXITCODE) — ดู $Log"; exit 1 }

# ยืนยันว่ามี output จริง
if (-not (Test-Path (Join-Path $BuildDir "index.html"))) {
    Write-Error "ไม่พบ $BuildDir\index.html — build อาจไม่สำเร็จ ดู $Log"; exit 1
}
Write-Host "==> Build สำเร็จ → $BuildDir" -ForegroundColor Green

# 2) Push ขึ้น itch ด้วย butler
$dest = "${Target}:${Channel}"
Write-Host "==> Pushing → $dest" -ForegroundColor Cyan
& $Butler push $BuildDir $dest
if ($LASTEXITCODE -ne 0) { Write-Error "butler push ล้มเหลว — login แล้วหรือยัง? (butler login)"; exit 1 }

Write-Host "==> เสร็จ! เปิดหน้า itch แล้วตั้ง 'This file will be played in the browser' + Mobile-friendly ON" -ForegroundColor Green
