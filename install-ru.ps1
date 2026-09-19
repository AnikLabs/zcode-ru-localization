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
$asar = Join-Path $InstallDir "resources\app.asar"
if (-not (Test-Path $asar)) {
  if (Test-Path (Join-Path $InstallDir "resources\app")) {
    Write-Host "Русская локализация уже установлена в $InstallDir" -ForegroundColor Yellow; exit 0
  }
  Write-Host "app.asar не найден в $InstallDir\resources" -ForegroundColor Red; exit 1
}
$version = (Get-Item (Join-Path $InstallDir "ZCode.exe")).VersionInfo.ProductVersion
Write-Host "ZCode $version в $InstallDir" -ForegroundColor Cyan

# --- проверить, что ZCode закрыт ---
$running = Get-Process -Name ZCode -ErrorAction SilentlyContinue
if ($running) {
  Write-Host "ZCode запущен. Закройте его полностью (включая трей) и запустите снова." -ForegroundColor Red
  exit 1
}

# --- патчим во временную папку ---
$temp = Join-Path $env:TEMP "zcode-ru-patch"
if (-not (Test-Path (Join-Path $ScriptDir "node_modules\@electron\asar"))) {
  Write-Host "Устанавливаю @electron/asar (один раз)..."
  npm install --prefix $ScriptDir @electron/asar --no-audit --no-fund
  if ($LASTEXITCODE -ne 0) { Write-Host "Не удалось установить @electron/asar. Нужен Node.js и доступ к npm." -ForegroundColor Red; exit 1 }
}
Write-Host "Патчу файлы (это займёт минуту-две)..."
node (Join-Path $ScriptDir "patch.mjs") $asar $temp
if ($LASTEXITCODE -ne 0) { Write-Host "Патч не удался — установка отменена, ничего не изменено." -ForegroundColor Red; exit 1 }

# --- бэкап ---
$backup = Join-Path $InstallDir "resources\app.asar.bak"
if (-not (Test-Path $backup)) {
  Copy-Item $asar $backup
  Write-Host "Бэкап: $backup"
}

# --- установка ---
Write-Host "Копирую файлы..."
robocopy $temp (Join-Path $InstallDir "resources\app") /E /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { Write-Host "Ошибка копирования." -ForegroundColor Red; exit 1 }
Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
Rename-Item $asar "app.asar.orig" -ErrorAction SilentlyContinue

# --- включить русский язык ---
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
Write-Host "Готово! Запустите ZCode — интерфейс будет на русском." -ForegroundColor Green
Write-Host ""
Write-Host "Откат: удалите папку $InstallDir\resources\app и переименуйте app.asar.orig обратно в app.asar."
