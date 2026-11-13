# 用 Windows 內建的 .NET Framework 編譯器把程式編成 dist\GrabScreen.exe（不需要另外安裝任何東西）
# 會依序產生：dist\GrabScreen.exe → release\GrabScreen-Setup-版本.exe → release\GrabScreen-Setup-版本.zip
# 加上 -ZipOnly：不重新編譯，只用現有的安裝檔重做 ZIP
param([switch]$ZipOnly)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path (Join-Path $fw 'csc.exe'))) { $fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319' }
$csc = Join-Path $fw 'csc.exe'
$wpf = Join-Path $fw 'WPF'
$out = Join-Path $root 'dist'
$rel = Join-Path $root 'release'

# 版本號從 src\AssemblyInfo.cs 讀
$info = Get-Content (Join-Path $root 'src\AssemblyInfo.cs') -Raw -Encoding UTF8
$m = [regex]::Match($info, 'AssemblyVersion\("(\d+)\.(\d+)\.(\d+)')
if (-not $m.Success) { throw '找不到版本號（src\AssemblyInfo.cs）' }
$ver = "$($m.Groups[1].Value).$($m.Groups[2].Value).$($m.Groups[3].Value)"
$setup = Join-Path $rel "GrabScreen-Setup-$ver.exe"

if (-not $ZipOnly) {
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

    # 2. 編譯主程式
    $src = Get-ChildItem (Join-Path $root 'src') -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
    & $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /utf8output `
        "/out:$(Join-Path $out 'GrabScreen.exe')" "/win32icon:$ico" `
        /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
        "/r:$(Join-Path $wpf 'UIAutomationClient.dll')" "/r:$(Join-Path $wpf 'UIAutomationTypes.dll')" "/r:$(Join-Path $wpf 'WindowsBase.dll')" `
        $src
    if ($LASTEXITCODE -ne 0) { throw '編譯失敗' }
    Write-Host "完成：$(Join-Path $out 'GrabScreen.exe')"

    # 3. 安裝檔：把剛編好的 GrabScreen.exe 包進 Setup
    $obj = Join-Path $root 'installer\obj'
    New-Item -ItemType Directory -Force $obj | Out-Null
    $verFile = Join-Path $obj 'Version.cs'
    Set-Content $verFile "[assembly: System.Reflection.AssemblyVersion(""$ver.0"")]`r`n[assembly: System.Reflection.AssemblyFileVersion(""$ver.0"")]" -Encoding UTF8
    New-Item -ItemType Directory -Force $rel | Out-Null
    & $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /utf8output `
        "/out:$setup" "/win32icon:$ico" "/resource:$(Join-Path $out 'GrabScreen.exe'),GrabScreen.exe" `
        /r:System.dll /r:System.Core.dll /r:Microsoft.CSharp.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
        (Join-Path $root 'installer\Setup.cs') (Join-Path $root 'src\Icons.cs') (Join-Path $root 'src\Native.cs') $verFile
    if ($LASTEXITCODE -ne 0) { throw '安裝檔編譯失敗' }
    Write-Host "完成：$setup"
}

# 4. ZIP：安裝檔＋中文安裝說明（有些地方會擋 .exe，ZIP 比較好傳）
if (-not (Test-Path $setup)) { throw "找不到安裝檔：$setup（請先不加 -ZipOnly 執行一次）" }
$stage = Join-Path $root "installer\obj\zip"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item $setup $stage
$readme = (Get-Content (Join-Path $root 'installer\zip-readme.txt') -Raw -Encoding UTF8).Replace('{VERSION}', $ver)
[IO.File]::WriteAllText((Join-Path $stage '安裝說明.txt'), $readme.Replace("`r`n", "`n").Replace("`n", "`r`n"), (New-Object Text.UTF8Encoding($true)))
$zip = Join-Path $rel "GrabScreen-Setup-$ver.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [IO.Compression.CompressionLevel]::Optimal, $false, [Text.Encoding]::UTF8)
Remove-Item $stage -Recurse -Force
Write-Host "完成：$zip"
