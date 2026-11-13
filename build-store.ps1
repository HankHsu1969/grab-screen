# 產生要上傳 Microsoft Store 的 MSIX 套件：store\out\GrabScreen_版本_x64.msix
# 需要 tools\sdk-buildtools（微軟官方 Microsoft.Windows.SDK.BuildTools，裡面有 MakeAppx / MakePri）
# 加上 -SkipBuild：不重新編譯，直接用現有的 dist\GrabScreen.exe
param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

$mk = Get-ChildItem (Join-Path $root 'tools\sdk-buildtools') -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1
if (-not $mk) { throw '找不到 MakeAppx：請先把 Microsoft.Windows.SDK.BuildTools 解壓縮到 tools\sdk-buildtools（說明見 store\STORE-GUIDE.md）' }
$makeappx = $mk.FullName
$makepri = Join-Path $mk.DirectoryName 'makepri.exe'

# 1. 主程式
if (-not $SkipBuild) { & (Join-Path $root 'build.ps1') }
$exe = Join-Path $root 'dist\GrabScreen.exe'
if (-not (Test-Path $exe)) { throw '找不到 dist\GrabScreen.exe，請先執行 build.ps1' }

# 2. 版本號（Store 規定第四碼必須是 0）與套件身分
$info = Get-Content (Join-Path $root 'src\AssemblyInfo.cs') -Raw -Encoding UTF8
$m = [regex]::Match($info, 'AssemblyVersion\("(\d+)\.(\d+)\.(\d+)')
if (-not $m.Success) { throw '找不到版本號（src\AssemblyInfo.cs）' }
$ver = "$($m.Groups[1].Value).$($m.Groups[2].Value).$($m.Groups[3].Value).0"
$id = @{}
Get-Content (Join-Path $root 'store\identity.ini') -Encoding UTF8 | ForEach-Object {
    if ($_ -match '^\s*([^#=][^=]*?)\s*=\s*(.*)$') { $id[$matches[1]] = $matches[2].Trim() }
}
foreach ($k in 'IdentityName', 'Publisher', 'PublisherDisplayName') { if (-not $id[$k]) { throw "store\identity.ini 缺少 $k" } }

# 3. 準備套件內容
$obj = Join-Path $root 'store\obj'
$layout = Join-Path $obj 'layout'
if (Test-Path $obj) { Remove-Item $obj -Recurse -Force }
New-Item -ItemType Directory -Force $layout | Out-Null
Copy-Item $exe $layout
Add-Type -ReferencedAssemblies System.Drawing -Path @(
    (Join-Path $root 'tools\StoreAssets.cs'),
    (Join-Path $root 'src\Icons.cs'),
    (Join-Path $root 'src\Native.cs'))
[StoreAssets]::Make((Join-Path $layout 'Assets'))

$esc = { param($s) [Security.SecurityElement]::Escape($s) }
$manifest = (Get-Content (Join-Path $root 'store\AppxManifest.template.xml') -Raw -Encoding UTF8).
    Replace('{IDENTITY_NAME}', (& $esc $id['IdentityName'])).
    Replace('{PUBLISHER}', (& $esc $id['Publisher'])).
    Replace('{PUBLISHER_DISPLAY_NAME}', (& $esc $id['PublisherDisplayName'])).
    Replace('{VERSION}', $ver)
[IO.File]::WriteAllText((Join-Path $layout 'AppxManifest.xml'), $manifest, (New-Object Text.UTF8Encoding($false)))

# 4. 資源索引（讓 Windows 依螢幕縮放挑選合適尺寸的圖示）
$pricfg = Join-Path $obj 'priconfig.xml'
& $makepri createconfig /cf $pricfg /dq zh-TW /pv 10.0.0 /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'MakePri createconfig 失敗' }
# 單一套件：不要把各尺寸拆成獨立的資源套件，全部放進同一個 resources.pri
[xml]$cfg = Get-Content $pricfg -Raw
$pk = $cfg.SelectSingleNode('//packaging')
if ($pk) { [void]$pk.ParentNode.RemoveChild($pk) }
$cfg.Save($pricfg)
& $makepri new /pr $layout /cf $pricfg /mn (Join-Path $layout 'AppxManifest.xml') /of (Join-Path $layout 'resources.pri') /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'MakePri new 失敗' }

# 5. 打包（不用自己簽章：上傳到 Partner Center 後由微軟簽章）
$out = Join-Path $root 'store\out'
New-Item -ItemType Directory -Force $out | Out-Null
$msix = Join-Path $out "GrabScreen_${ver}_x64.msix"
$log = & $makeappx pack /d $layout /p $msix /o 2>&1
if ($LASTEXITCODE -ne 0) { $log | Write-Host; throw 'MakeAppx 打包失敗' }
Write-Host "完成：$msix"
if ($id['IdentityName'] -like 'Placeholder*') {
    Write-Warning '目前用的是暫時的套件身分，只能檢查格式。正式上傳前，請把 Partner Center 給的值填進 store\identity.ini 再執行一次。'
}
