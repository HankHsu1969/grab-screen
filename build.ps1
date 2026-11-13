# 用 Windows 內建的 .NET Framework 編譯器把程式編成 dist\GrabScreen.exe（不需要另外安裝任何東西）
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $fw 'csc.exe'))) { $fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$csc = Join-Path $fw 'csc.exe'
$wpf = Join-Path $fw 'WPF'
$out = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $out | Out-Null

# 1. 產生程式圖示 app.ico
$ico = Join-Path $root 'app.ico'
if (-not (Test-Path $ico)) {
    Add-Type -ReferencedAssemblies System.Drawing -Path @(
        (Join-Path $root 'tools\IconMaker.cs'),
        (Join-Path $root 'src\Icons.cs'),
        (Join-Path $root 'src\Native.cs'))
    [IconMaker]::Make($ico)
}

# 2. 編譯
$src = Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /utf8output `
    "/out:$(Join-Path $out 'GrabScreen.exe')" "/win32icon:$ico" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    "/r:$(Join-Path $wpf 'UIAutomationClient.dll')" "/r:$(Join-Path $wpf 'UIAutomationTypes.dll')" "/r:$(Join-Path $wpf 'WindowsBase.dll')" `
    $src
if ($LASTEXITCODE -ne 0) { throw '編譯失敗' }
Write-Host "完成：$(Join-Path $out 'GrabScreen.exe')"
