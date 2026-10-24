# Универсальный установщик русской локализации ZCode Desktop.
# Работает с любой версией: сам извлекает app.asar, патчит и ставит папку resources\app.
# Запуск:  powershell -ExecutionPolicy Bypass -File install-ru.ps1 [-InstallDir "H:\__ZCode"]
param(
  [string]$InstallDir = ""
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# --- найти папку установки ---
if (-not $InstallDir) {
  $proc = Get-Process -Name ZCode -ErrorAction SilentlyContinue | Where-Object { $_.Path } | Select-Object -First 1
  if ($proc) { $InstallDir = Split-Path -Parent $proc.Path }
  elseif (Test-Path "$env:LOCALAPPDATA\Programs\zcode\ZCode.exe") { $InstallDir = "$env:LOCALAPPDATA\Programs\zcode" }
  else { Write-Host "Не нашёл ZCode. Укажите путь: install-ru.ps1 -InstallDir `"C:\путь\к\ZCode`"" -ForegroundColor Red; exit 1 }
}
$zcodeExe = Join-Path $InstallDir "ZCode.exe"
if (-not (Test-Path $zcodeExe)) { Write-Host "ZCode.exe не найден в $InstallDir" -ForegroundColor Red; exit 1 }
$zcodeVersion = (Get-Item $zcodeExe).VersionInfo.ProductVersion
Write-Host "ZCode $zcodeVersion в $InstallDir" -ForegroundColor Cyan
$res = Join-Path $InstallDir "resources"
$asar = Join-Path $res "app.asar"

# --- проверить, что ZCode закрыт ---
$running = Get-Process -Name ZCode -ErrorAction SilentlyContinue
if ($running) {
  Write-Host "ZCode запущен. Закройте его полностью (включая значок в трее) и запустите снова." -ForegroundColor Red
  exit 1
}

# --- убедиться, что @electron/asar установлен ---
if (-not (Test-Path (Join-Path $ScriptDir "node_modules\@electron\asar"))) {
  Write-Host "Устанавливаю @electron/asar (один раз)..."
  npm install --prefix $ScriptDir @electron/asar --no-audit --no-fund
  if ($LASTEXITCODE -ne 0) { Write-Host "Не удалось установить @electron/asar. Нужен Node.js и доступ к npm." -ForegroundColor Red; exit 1 }
}

# --- A. Определить asar-источник: app.asar, затем app.asar.orig/bak/original ---
# У модуля @electron/asar (>=4.x) нет main, только exports "./lib/asar.js" —
# require по абсолютному пути каталога ломается (MODULE_NOT_FOUND), поэтому
# require по прямому пути lib\asar.js.
function Get-VersionFromAsar {
  param([string]$AsarPath, [string]$ScriptDir)
  try {
    $tempPkg = Join-Path $env:TEMP ("zcode-pkg-" + [Guid]::NewGuid().ToString("N") + ".json")
    $script = "const fs=require('fs');fs.writeFileSync(process.argv[2],require(require('path').join(process.argv[3],'node_modules','@electron','asar','lib','asar.js')).extractFile(process.argv[1],'package.json'))"
    node -e $script $AsarPath $tempPkg $ScriptDir | Out-Null
    # $exitCode = node ... присваивает STDOUT, а не код выхода; код выхода только в $LASTEXITCODE
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $tempPkg)) { return "unknown" }
    $json = Get-Content $tempPkg -Raw -Encoding UTF8
    if ($json -match '"version"\s*:\s*"([^"]+)"') { $ver = $matches[1] }
    else { $ver = "unknown" }
    Remove-Item $tempPkg -Force -ErrorAction SilentlyContinue
    return $ver
  } catch { return "unknown" }
}

function Read-VersionFromPackageJson {
  param([string]$PkgPath)
  try {
    $json = Get-Content $PkgPath -Raw -Encoding UTF8
    if ($json -match '"version"\s*:\s*"([^"]+)"') { return $matches[1] }
    return "unknown"
  } catch { return "unknown" }
}

# Сравнение по первым 3 сегментам: ProductVersion ZCode.exe четырёхсегментный
# ("3.16.2.8769"), а version в package.json трёхсегментный ("3.16.2").
function VersionEquals {
  param([string]$a, [string]$b)
  if ([string]::IsNullOrEmpty($a) -or [string]::IsNullOrEmpty($b)) { return $false }
  if ($a -eq "unknown" -or $b -eq "unknown") { return $false }
  $aParts = $a.Split('.')
  $bParts = $b.Split('.')
  $len = [Math]::Min(3, [Math]::Min($aParts.Length, $bParts.Length))
  for ($i = 0; $i -lt $len; $i++) {
    if ($aParts[$i] -ne $bParts[$i]) { return $false }
  }
  return $true
}

$targetAsar = $null
$candidates = @(
  (Join-Path $res "app.asar"),
  (Join-Path $res "app.asar.orig"),
  (Join-Path $res "app.asar.bak"),
  (Join-Path $res "app.asar.original")
)

foreach ($candidate in $candidates) {
  if (-not (Test-Path $candidate)) { continue }
  $ver = Get-VersionFromAsar $candidate $ScriptDir
  Write-Host "Проверка $(Split-Path $candidate -Leaf): версия $ver"
  if (VersionEquals $ver $zcodeVersion) {
    if (-not [string]::Equals($candidate, $asar, [StringComparison]::OrdinalIgnoreCase)) {
      if (Test-Path $asar) {
        $existingVer = Get-VersionFromAsar $asar $ScriptDir
        Write-Host "Удалён несовпадающий app.asar (версия $existingVer)"
        Remove-Item $asar -Force
      }
      Move-Item $candidate $asar -Force
      Write-Host "Восстановлен $(Split-Path $candidate -Leaf) -> app.asar"
    }
    $targetAsar = $asar
    break
  }
}

if (-not $targetAsar) {
  Write-Host ""
  Write-Host "ОШИБКА: не найден app.asar с версией, совпадающей с ZCode.exe." -ForegroundColor Red
  Write-Host "Возможная причина: в папке resources остались файлы от старой версии ZCode."
  Write-Host "Решение: переустановите ZCode $zcodeVersion штатным апдейтером/установщиком, затем запустите русификатор снова."
  exit 1
}

# --- Патчинг во временную папку ---
$temp = Join-Path $env:TEMP "zcode-ru-patch"
Write-Host "Патчу файлы (это займёт минуту-две)..."
node (Join-Path $ScriptDir "patch.mjs") $targetAsar $temp
if ($LASTEXITCODE -ne 0) { Write-Host "Патч не удался — установка отменена, ничего не изменено." -ForegroundColor Red; exit 1 }

# --- C. Сверка версии пропатченного стейджа ---
$stagePackage = Join-Path $temp "package.json"
if (-not (Test-Path $stagePackage)) {
  Write-Host "ОШИБКА: после патча не найден package.json в стейдже." -ForegroundColor Red
  Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
  exit 1
}
$stageVersion = Read-VersionFromPackageJson $stagePackage

if (-not (VersionEquals $stageVersion $zcodeVersion)) {
  Write-Host "ОШИБКА: версия в пропатченном стейдже ($stageVersion) не совпадает с версией ZCode.exe ($zcodeVersion)." -ForegroundColor Red
  Write-Host "Установка отменена, файлы не изменены."
  Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
  exit 1
}
Write-Host "Версия стейджа подтверждена: $stageVersion"

# --- Бэкап app.asar.bak (после успешной сверки) ---
$backup = Join-Path $res "app.asar.bak"
if (-not (Test-Path $backup)) {
  Copy-Item $targetAsar $backup
  Write-Host "Бэкап: $backup"
}

# --- B. Удалить старый resources\app ТОЛЬКО после успешных проверок ---
$appDir = Join-Path $res "app"
if (Test-Path $appDir) {
  Write-Host "Удаление старой папки resources\app..."
  Remove-Item $appDir -Recurse -Force
}

# --- Копирование файлов ---
Write-Host "Копирую файлы..."
robocopy $temp $appDir /E /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { Write-Host "Ошибка копирования." -ForegroundColor Red; Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue; exit 1 }
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue

# --- D. Сохранение оригинала: ровно один .orig актуальной версии ---
$orig = Join-Path $res "app.asar.orig"
if (Test-Path $orig) {
  $origVersion = Get-VersionFromAsar $orig $ScriptDir
  if (-not (VersionEquals $origVersion $zcodeVersion)) {
    Write-Host "Удаление устаревшего app.asar.orig (версия $origVersion)"
    Remove-Item $orig -Force
  }
  else {
    Write-Host "Удаление дубликата app.asar.orig (та же версия)"
    Remove-Item $orig -Force
  }
}
if (Test-Path $targetAsar) {
  Move-Item $targetAsar $orig -Force
  Write-Host "Оригинал сохранён: app.asar.orig"
}

# --- E. Включить русский язык ---
$settings = "$env:USERPROFILE\.zcode\v2\setting.json"
if (Test-Path $settings) {
  $j = Get-Content $settings -Raw -Encoding UTF8 | ConvertFrom-Json
  if ($j.locale -ne "ru-RU") {
    $j.locale = "ru-RU"; $j.localePreference = "ru-RU"
    $j | ConvertTo-Json -Depth 20 | Set-Content $settings -Encoding UTF8
    Write-Host "Язык в настройках: Русский"
  }
}

Write-Host ""
Write-Host "Русификация ZCode $zcodeVersion установлена." -ForegroundColor Green
Write-Host ""
Write-Host "Откат: удалите папку $InstallDir\resources\app и переименуйте app.asar.orig обратно в app.asar."
