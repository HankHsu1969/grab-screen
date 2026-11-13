# GrabScreen 上架 Microsoft Store 教學（個人帳號）

整個流程分成「你要做的」和「已經準備好的」兩部分。帳號註冊、身分驗證、同意合約這幾步，微軟規定**一定要本人操作**，別人（包括 AI）不能代辦。

| 已經準備好的 | 檔案 |
|---|---|
| Store 用的程式套件（MSIX） | 執行 `.\build-store.ps1` 產生 `store\out\GrabScreen_版本_x64.msix` |
| 各種尺寸的圖示 | 打包時自動產生（星方向 LOGO） |
| 商店頁截圖 5 張 | `store\screenshots\` |
| 商店頁介紹文字、分級問卷答案、審核說明 | `store\listing-zh-TW.md` |
| 隱私權政策 | `PRIVACY.md`（push 到 GitHub 後，網址才能用） |

**打包工具**：`build-store.ps1` 需要微軟官方的 Microsoft.Windows.SDK.BuildTools（約 21 MB，裡面有 MakeAppx、MakePri），放在 `tools\sdk-buildtools`（不放進 git）。換一台電腦時可以這樣重新下載：

```powershell
$v = '10.0.28000.2957'
New-Item -ItemType Directory -Force tools\sdk-buildtools | Out-Null
Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/$v/microsoft.windows.sdk.buildtools.$v.nupkg" -OutFile tools\sdk-buildtools\buildtools.zip
Expand-Archive tools\sdk-buildtools\buildtools.zip tools\sdk-buildtools\pkg
Remove-Item tools\sdk-buildtools\buildtools.zip
```

---

## 第 1 步：準備

個人帳號只要準備兩樣，**免費**，也不用公司文件或 D-U-N-S 編號：

1. **你自己的 Microsoft 帳號**：Outlook、Hotmail，或用 Gmail 註冊的 Microsoft 帳號都可以。建議用你會長期使用的帳號，因為之後 App 都會掛在這個帳號底下。
2. **你的身分證件**（身分證、護照或駕照）：驗證時要拍證件，再拍一張自拍。

> 注意：帳號類型**選了就不能改**，個人帳號之後不能轉成公司帳號。如果將來想改用「星方向公司」名義上架，要另外註冊公司帳號，App 也要重新上架一次。

## 第 2 步：註冊開發者帳號

1. 打開 <https://storedeveloper.microsoft.com>，按「開始使用」（Get started）。
2. 帳號類型選 **個人（Individual）**。
3. 用你的 Microsoft 帳號登入。
4. 填基本資料。「發行者顯示名稱」（Publisher display name）是 Store 上顯示的開發者名稱，可以填 **星方向** 或你的名字。這個名稱必須沒有人用過，而且要是你有權使用的名稱。
5. 依畫面用證件加自拍完成身分驗證，同意開發者合約。
6. 等驗證通過，通常幾天內就會好。

> 「發行者顯示名稱」最好跟商店頁的版權資訊一致：`store\listing-zh-TW.md` 裡現在寫的是「© 2026 星方向」。如果你填的是自己的名字，送審時把版權那一欄也改成你的名字。

## 第 3 步：預留 App 名稱

1. 登入 Partner Center：<https://partner.microsoft.com/dashboard>
2. 進入「應用程式和遊戲」（Apps and games）→「新產品」（New product）→ 選「**MSIX 或 PWA 應用程式**」。
3. 名稱輸入 **GrabScreen 螢幕擷取** → 檢查是否可用 → 預留（Reserve product name）。

## 第 4 步：把「套件身分」交給我，重新打包

1. 在這個 App 的頁面，打開「產品管理」→「產品身分識別」（Product management → Product identity）。
2. 抄下三個值：
   - `Package/Identity/Name`
   - `Package/Identity/Publisher`（`CN=` 開頭的一串）
   - `Package/Properties/PublisherDisplayName`
3. 把這三個值貼給 Claude，或自己填進 `store\identity.ini`，然後執行：

```powershell
.\build-store.ps1
```

4. 得到 `store\out\GrabScreen_1.0.1.0_x64.msix`，這就是要上傳的檔案。（套件不用自己簽章，上傳後由微軟簽章。）

## 第 5 步：建立送審

在 App 頁面按「開始提交」（Start your submission），依序填寫。各欄位的內容都在 `store\listing-zh-TW.md`，直接複製貼上：

1. **定價與可用性**：免費、所有市場、公開。
2. **屬性**：類別選「公用程式與工具」，填隱私權政策網址、網站、支援連絡資訊。
3. **年齡分級**：照檔案裡的答案填問卷。
4. **套件**（Packages）：上傳 `.msix`，裝置系列勾選 Windows 10/11 桌上型（Desktop）。
5. **商店清單**（Store listings）：語言選「中文（台灣）」，貼上描述、新功能、產品功能、搜尋字詞，上傳 `store\screenshots\` 的 5 張截圖並填說明文字。
6. **送審選項**：在「受限功能說明」和「給審核人員的說明」貼上檔案裡的文字。
7. 按「提交至 Store」（Submit to the Store）。審核通常 **1～3 個工作天**，第一次可能比較久。審核結果會寄到你的 Email。

## 之後要出新版本

1. 改 `src\AssemblyInfo.cs` 的版本號（例如 1.0.2）。
2. 執行 `.\build-store.ps1`。
3. 在 Partner Center 建立新的提交，上傳新的 `.msix`，填「這個版本的新功能」，送審。

---

## 常見問題

**Store 版和 GitHub 上的安裝檔版可以同時裝嗎？**
不建議。兩個版本會搶同樣的快捷鍵，而且程式一次只會執行一個。改用 Store 版之前，請先到 Windows「設定 → 應用程式 → 已安裝的應用程式」移除舊的「GrabScreen 螢幕擷取」。

**Store 版的「開機自動啟動」在哪裡關？**
Windows「設定 → 應用程式 → 啟動」，找到「GrabScreen 螢幕擷取」切換開關。（Store 版程式裡的設定視窗也會提示這一點。）

**送審被退件怎麼辦？**
把退件通知的內容貼給 Claude，我們再一起修改後重新送審。

**資料來源**
- [Company account verification requirements（微軟）](https://learn.microsoft.com/en-us/windows/apps/publish/store-business-verification-reqs)
- [Steps to open a Microsoft Store developer account（微軟）](https://learn.microsoft.com/en-us/windows/apps/publish/partner-center/open-a-developer-account)
- [Set up your Microsoft Store developer account early（微軟）](https://learn.microsoft.com/en-us/windows/apps/publish/prepare-developer-account)
- [Get started with Microsoft Store - FAQ（微軟）](https://learn.microsoft.com/en-us/windows/apps/publish/faq/get-started-with-the-microsoft-store)
