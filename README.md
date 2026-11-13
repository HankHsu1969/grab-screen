# GrabScreen 螢幕擷取 + 標示編輯器

按一個快捷鍵就把螢幕畫面抓進剪貼簿，抓完可以馬上開編輯器加上框框、箭頭、文字、編號、馬賽克。

- 不用安裝任何東西：只用 Windows 內建的 .NET Framework 4.8，`dist\GrabScreen.exe` 直接點兩下就能用
- 支援 125% / 150% 螢幕縮放、多螢幕

---

## 開始使用

1. 點兩下 `dist\GrabScreen.exe`
2. 右下角系統匣（時鐘旁邊）會出現星方向的星星圖示，代表它在背景待命
3. 按快捷鍵擷取：

| 快捷鍵 | 模式 | 說明 |
|---|---|---|
| `Ctrl + Alt + 1` | 全螢幕 | 立刻抓下滑鼠所在的那一台螢幕（設定裡可改成「所有螢幕」） |
| `Ctrl + Alt + 2` | 視窗 / 物件 | 滑鼠移到哪裡就自動框出那個視窗、按鈕、面板，點一下就擷取 |
| `Ctrl + Alt + 3` | 自行框選 | 按住左鍵拖曳框出範圍，放開就擷取 |

> 左鍵點一下系統匣圖示 ＝ 自行框選擷取。右鍵點圖示可以打開選單、改設定、結束程式。

擷取完成後，圖片**已經在剪貼簿裡**（可以直接 `Ctrl + V` 貼到 LINE、Word、簡報），右下角會跳出小視窗問你要不要「開啟編輯」。

### 視窗 / 物件模式的小技巧

- **滾輪往上**：範圍變大（例如從「按鈕」變成「整個工具列」再到「整個視窗」）
- **滾輪往下**：範圍變小
- **直接拖曳**：改成自由框選
- `Enter` 擷取目前框住的範圍，`Esc` 或右鍵取消

---

## 編輯器

| 工具 | 快捷鍵 | 用途 |
|---|---|---|
| 選取 | `V` | 點選標示後可移動、拖曳白色控制點調整大小、`Delete` 刪除、雙擊文字修改、方向鍵微調 |
| 方框 | `R` | 框出重點（按住 `Shift` 畫正方形） |
| 圓圈 | `E` | 圈出重點（按住 `Shift` 畫正圓） |
| 箭頭 | `A` | 從起點拖到要指的地方（`Shift` 鎖定 45° 角） |
| 直線 | `L` | 畫直線（`Shift` 鎖定 45° 角） |
| 畫筆 | `P` | 自由手繪 |
| 螢光筆 | `H` | 半透明，畫過要強調的文字 |
| 文字 | `T` | 點一下開始打字，`Enter` 完成、`Shift + Enter` 換行；字會自動加白色描邊，放在哪裡都看得清楚 |
| 編號 | `N` | 點一下放上 ① ② ③，數字自動往上加（工具列可以重設回 1） |
| 馬賽克 | `M` | 遮住帳號、電話、個資 |
| 裁切 | `C` | 拖曳出要保留的範圍 |
| 尺寸 | `Ctrl + R` | 調整整張圖的大小：用百分比、寬高像素，或按快速鍵（25%～200%、常用寬度 640～1920）。預設維持長寬比例；畫好的標示會跟著一起縮放，之後還能繼續編輯 |

其他快捷鍵：

- 顏色：工具列點色塊，或 `Shift + 1~8`
- 粗細 / 大小：工具列下拉選單，或數字鍵 `1~5`（線條、文字、編號、馬賽克格子一起變）
- `Ctrl + Z` 復原、`Ctrl + Y` 重做
- `Ctrl + C` 複製結果到剪貼簿、`Ctrl + S` 另存新檔（PNG / JPG / BMP）
- `Ctrl + 滾輪` 縮放畫面、`Ctrl + 0` 符合視窗、`Ctrl + 1` 實際大小、按住滑鼠中鍵拖曳可平移
- 「完成」按鈕：複製到剪貼簿並關閉

> 選取工具選中標示後，再點顏色或粗細，會直接改那個標示。

---

## 快捷鍵設定

右鍵點系統匣的星星圖示 →「快捷鍵設定…」：

1. 點一下輸入框（會變成淡黃色）
2. 直接按下想要的組合鍵，例如 `Ctrl + Shift + A`、`F9`
3. 旁邊會馬上顯示「✔ 可以使用」或「⚠ 已被其他程式使用」；有衝突或重複時不能儲存
4. 按「儲存」立刻生效，不用重開程式。按「恢復預設值」可以回到 `Ctrl + Alt + 1/2/3`

- 按 `Delete` 或 `Backspace` 可以清除，該模式就只能從選單使用
- 一般按鍵要搭配 `Ctrl` / `Alt` / `Shift`；`F1～F12`、`PrtSc` 可以單獨使用
- 想用 `PrtSc`：先到 Windows「設定 → 協助工具 → 鍵盤」，關閉「使用 Print Screen 鍵開啟螢幕擷取」

## 其他設定

右鍵點系統匣圖示 →「其他設定…」可以：

- 全螢幕要抓「滑鼠所在螢幕」還是「所有螢幕」
- 擷取後要不要跳出詢問視窗、幾秒後自動關閉
- 擷取後同時自動存檔到指定資料夾
- 開機自動啟動

設定檔存在 `%AppData%\GrabScreen\settings.ini`。

---

## 分享給其他人（安裝檔）

直接給對方下載頁連結：<https://github.com/HankHsu1969/grab-screen/releases/latest>

或把檔案傳給對方，二選一：

- `release\GrabScreen-Setup-1.0.0.exe`（約 150 KB）：點兩下就能安裝
- `release\GrabScreen-Setup-1.0.0.zip`（約 70 KB）：**.exe 會被擋的時候用這個**，裡面是同一個安裝檔加上〈安裝說明.txt〉，對方要先「全部解壓縮」再執行

安裝檔的特色：

- **不用系統管理員權限**，只安裝給對方自己的帳號（裝在 `%LocalAppData%\Programs\GrabScreen`）
- 會建立開始功能表捷徑，可選擇桌面捷徑、開機自動啟動、裝完立即執行
- 會出現在 Windows「設定 → 應用程式 → 已安裝的應用程式」，可以正常移除
- 以後出新版本，直接再執行新的安裝檔就會更新（會先自動關閉舊版，設定會保留）
- 需要 Windows 10 或 11（內建的 .NET Framework 4.8 就夠，不用另外裝東西）

傳檔時要注意：

- **Gmail 會擋 .exe 附件**（壓縮成 zip 也一樣），請改用 Google 雲端硬碟、LINE、隨身碟傳送
- 這個安裝檔沒有付費的「程式碼簽章」，對方第一次執行時 Windows 可能跳出藍色的「Windows 已保護您的電腦」：
  點「其他資訊」→「仍要執行」即可。這是所有沒簽章的小工具都會遇到的正常情況

可以直接複製給對方的說明：

> 這是我用的截圖小工具 GrabScreen。點兩下安裝檔就能安裝，不用管理員權限。
> 如果出現「Windows 已保護您的電腦」，按「其他資訊」→「仍要執行」。
> 裝好後：Ctrl+Alt+1 全螢幕、Ctrl+Alt+2 視窗、Ctrl+Alt+3 框選，截完直接 Ctrl+V 貼上；
> 跳出的小視窗按「開啟編輯」可以加框框、箭頭、文字、馬賽克。

要發新版本：改 `src\AssemblyInfo.cs` 裡的版本號，再執行 `.\build.ps1`，`release\` 裡就會出現新版號的安裝檔和 ZIP，然後：

```powershell
gh release create v1.1.0 release\GrabScreen-Setup-1.1.0.exe release\GrabScreen-Setup-1.1.0.zip --title "GrabScreen 1.1.0"
```

只想重做 ZIP（不重新編譯）：`.\build.ps1 -ZipOnly`。

## 自己重新編譯

在這個資料夾用 PowerShell 執行：

```powershell
.\build.ps1
```

會輸出 `dist\GrabScreen.exe` 和安裝檔 `release\GrabScreen-Setup-版本.exe`。原始碼在 `src\`，編輯器在 `src\Editor\`，安裝程式在 `installer\Setup.cs`。

安裝程式的自動測試（在指定資料夾裡安裝、更新、移除一次，不會動到真的系統設定）：

```powershell
Start-Process .\release\GrabScreen-Setup-1.0.0.exe -ArgumentList '/selftest', "$env:TEMP\gs-setup-test" -Wait
Get-Content "$env:TEMP\gs-setup-test\selftest.log"
```

| 檔案 | 內容 |
|---|---|
| `src\TrayApp.cs` | 系統匣、快捷鍵、擷取流程 |
| `src\CaptureOverlay.cs` | 擷取時蓋住螢幕的畫面（視窗偵測、框選、放大鏡） |
| `src\WindowFinder.cs` | 找出畫面上的視窗與視窗裡的物件（UI 自動化） |
| `src\ToastForm.cs` | 擷取後右下角的詢問視窗 |
| `src\HotkeyForm.cs` | 快捷鍵設定視窗 |
| `src\Editor\ResizeDialog.cs` | 調整圖片尺寸視窗 |
| `src\Editor\Shapes.cs` | 各種標示（方框、箭頭、文字…）怎麼畫 |
| `src\Editor\EditorCanvas.cs` | 編輯器畫布：滑鼠操作、復原重做、縮放、裁切 |
| `src\Editor\EditorForm.cs` | 編輯器視窗與工具列 |
| `tools\SelfTest.cs` | 自動測試（不會動到真的滑鼠鍵盤） |
