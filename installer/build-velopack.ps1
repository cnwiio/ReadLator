param(
    [string]$Version = "",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $RootDir "src\NovelTranslator.App\NovelTranslator.App.csproj"
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$csproj = Get-Content $ProjectPath
    $Version = $csproj.Project.PropertyGroup.Version | Select-Object -First 1
}
$PublishDir = Join-Path $RootDir "publish\$Runtime"
$ReleasesDir = Join-Path $PSScriptRoot "Releases\$Runtime"
$IconPath = Join-Path $RootDir "src\NovelTranslator.App\Assets\avalonia-logo.ico"

Write-Host "=== 1. Publish แบบ Self-Contained ($Runtime) ===" -ForegroundColor Cyan
dotnet publish $ProjectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -o $PublishDir

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    Write-Warning "ไม่พบคำสั่ง 'vpk' (Velopack CLI) ในระบบ"
    Write-Host "กรุณาติดตั้งด้วยคำสั่ง: dotnet tool install -g vpk" -ForegroundColor Yellow
    exit 1
}

Write-Host "=== 2. สร้าง Installer ด้วย Velopack (v$Version) ===" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $ReleasesDir | Out-Null

$vpkArgs = @(
    "pack",
    "--packId", "NovelTranslator",
    "--packTitle", "NovelTranslator",
    "--packVersion", $Version,
    "--packDir", $PublishDir,
    "--mainExe", "NovelTranslator.App.exe",
    "--outputDir", $ReleasesDir
)

if (Test-Path $IconPath) {
    $vpkArgs += @("--icon", $IconPath)
}

& vpk @vpkArgs
Write-Host "สร้าง Installer สำเร็จ! ไฟล์อยู่ที่: $ReleasesDir" -ForegroundColor Green