# 配布物を作成するスクリプト
#   powershell -ExecutionPolicy Bypass -File tools\publish.ps1
# 出力: dist\TestDataMaker_v1.0.0\ と dist\TestDataMaker_v1.0.0.zip
param(
    [string]$Version = "1.0.0",
    [switch]$SkipTests
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

# dotnet の場所（PATH になければユーザー領域のインストール先を使う）
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
$userDotnet = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
if (Test-Path $userDotnet) {
    $dotnet = $userDotnet
    $env:DOTNET_ROOT = Split-Path $userDotnet
}
if (-not $dotnet) { throw ".NET 8 SDK が見つかりません。" }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_NOLOGO = "1"

Push-Location $root
try {
    if (-not $SkipTests) {
        Write-Host "== テスト ==" -ForegroundColor Cyan
        & $dotnet test TestDataMaker.sln -c Release
        if ($LASTEXITCODE -ne 0) { throw "テストに失敗しました。" }
    }

    Write-Host "== 発行（self-contained 単一 exe）==" -ForegroundColor Cyan
    $publishDir = Join-Path $root "publish"
    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    & $dotnet publish src\TestDataMaker\TestDataMaker.csproj -c Release -r win-x64 -o $publishDir -p:Version=$Version
    if ($LASTEXITCODE -ne 0) { throw "発行に失敗しました。" }

    Write-Host "== 配布フォルダ作成 ==" -ForegroundColor Cyan
    $name = "TestDataMaker_v$Version"
    $distRoot = Join-Path $root "dist"
    $dist = Join-Path $distRoot $name
    if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
    New-Item -ItemType Directory -Force (Join-Path $dist "sample") | Out-Null

    Copy-Item (Join-Path $publishDir "TestDataMaker.exe") $dist
    Copy-Item (Join-Path $root "sample\sample.csv") (Join-Path $dist "sample")
    Copy-Item (Join-Path $root "sample\sample.json") (Join-Path $dist "sample")

    # README.txt / LICENSE.txt は メモ帳で文字化けしないよう BOM 付き UTF-8 で出力する
    $utf8Bom = New-Object System.Text.UTF8Encoding $true
    $readme = [System.IO.File]::ReadAllText((Join-Path $root "README.md"), [System.Text.Encoding]::UTF8)
    $devIndex = $readme.IndexOf("## 開発者向け情報")
    if ($devIndex -gt 0) { $readme = $readme.Substring(0, $devIndex).TrimEnd("`r", "`n", "-", " ") + "`r`n" }
    [System.IO.File]::WriteAllText((Join-Path $dist "README.txt"), $readme.Replace("`r`n", "`n").Replace("`n", "`r`n"), $utf8Bom)
    $license = [System.IO.File]::ReadAllText((Join-Path $root "LICENSE.txt"), [System.Text.Encoding]::UTF8)
    [System.IO.File]::WriteAllText((Join-Path $dist "LICENSE.txt"), $license.Replace("`r`n", "`n").Replace("`n", "`r`n"), $utf8Bom)

    $zip = Join-Path $distRoot "$name.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path $dist -DestinationPath $zip

    Write-Host ""
    Write-Host "完了:" -ForegroundColor Green
    Get-ChildItem $dist -Recurse -File | ForEach-Object { "  {0,-40} {1,12:N0} bytes" -f $_.FullName.Substring($dist.Length + 1), $_.Length }
    "  ZIP: $zip ({0:N0} bytes)" -f (Get-Item $zip).Length
}
finally {
    Pop-Location
}
