# 上架準備（iOS 首發）

### T43: 隱私、合規、法律頁面
- Status: todo
- Depends on: T31, T25, T42
- Goal: 補齊 App Store 審核要求的隱私與使用者內容規範。
- Files: `Assets/Plugins/iOS/PrivacyInfo.xcprivacy`、`Editor/IosPostProcess.cs`（寫入 Info.plist 字串與 capability）、`Server/public/privacy.html`、`Server/public/terms.html`、`Server/public/support.html`、`spec/app-privacy.md`（App Store「App 隱私權」問卷的填答依據）
- Interfaces: 設定頁與首次啟動都能開隱私權政策、服務條款；客服信箱
- Acceptance:
  - Privacy manifest 列出使用的 Required Reason API（UserDefaults、檔案時間戳記、系統開機時間等）與蒐集的資料類型；第三方 SDK 都附帶自己的 manifest
  - 使用者內容（暱稱、聊天）具備：過濾、檢舉、封鎖、24 小時內處理的聯絡方式（Guideline 1.2）
  - App 內可刪除帳號（T31，Guideline 5.1.1(v)）
  - 年齡分級問卷填答一致（有使用者聊天與內購）；預設不開放給 13 歲以下（不做兒童類別）
  - Info.plist 只放實際用到的權限說明字串
- Manual: 隱私權政策、服務條款文字建議請法律專業看過；App Store Connect 填 App 隱私權與年齡分級
- Notes: 若之後上 Google Play，同一份資料用來填 Data safety 表單。

### T44: 當機回報與分析事件
- Status: todo
- Depends on: T30
- Goal: 上架後知道玩家在哪裡卡住、哪裡當機。
- Files: `Meta/Analytics.cs`、`Meta/CrashReporting.cs`、`spec/analytics-events.md`
- Interfaces: `Analytics.Log(string evt, params (string, object)[] props)`；事件：`tutorial_step`、`match_start`、`match_end`、`roll`（取樣 10%）、`swing_rejected`（含原因）、`mm_wait`、`purchase_*`、`ad_*`、`mission_claim`、`screen_view`
- Acceptance:
  - 強制當機測試能在 Crashlytics 看到含符號的 stack trace
  - 未同意 ATT 時不送 IDFA；設定裡可以關閉分析
  - 事件名稱與參數全部列在 `analytics-events.md`
- Manual: Firebase 專案、`GoogleService-Info.plist`、Xcode 上傳 dSYM 的 build phase

### T45: 正式音效
- Status: todo
- Depends on: T16
- Goal: 把程式合成的音效換成真實錄音，加上 UI 音效。
- Files: `Gameplay/BowlingAudio.cs`（改讀 AudioClip，保留合成版當備用）、`Resources/Audio/*`、`Gameplay/AudioMixer.mixer`
- Acceptance:
  - 滾球、撞瓶、球瓶互撞、溝槽、球坑、全倒歡呼、UI 點擊／領獎／升級全部有錄音版
  - 音效總大小 ≤ 8 MB（壓縮 Vorbis／ADPCM）
  - 同時大量撞瓶時不爆音（限制同時發聲數）
- Manual: 購買可商用音效包或錄音

### T46: 正式角色模型與動畫
- Status: todo
- Depends on: T40
- Goal: 換掉幾何形狀拼成的角色與僵硬的投球動作，修正球從手上滑到球道的問題。
- Files: `Gameplay/Bowler.cs`（改用 Animator）、`Gameplay/BowlerAnimator.controller`、`Gameplay/BallHandIK.cs`
- Interfaces: Animator 參數：`Idle`、`Approach`、`Release`（觸發點用 Animation Event 呼叫 `onRelease`）、`Celebrate`、`Disappointed`；左撇子用鏡像
- Acceptance:
  - 出手瞬間球在手上，且球的起點與 `StartThrow` 的位置一致（誤差 < 3 cm）
  - 全倒時角色慶祝、洗溝時失望
  - 服裝外觀（T40）可套在新模型上
- Manual: 從 Mixamo 下載角色與動作（需 Adobe 帳號），在 Unity 設成 Humanoid；霓虹材質套用

### T47: 商店素材
- Status: todo
- Depends on: T11, T12, T13
- Goal: 準備 App Store 頁面需要的所有圖片與文字。
- Files: `Editor/StoreScreenshots.cs`（自動擺鏡頭、隱藏除錯 UI、輸出各尺寸）、`Store/zh-TW/*`、`Store/en/*`、`Store/icon-1024.png`
- Acceptance:
  - App 圖示 1024×1024，無透明
  - 6.9 吋（1320×2868）與 6.5 吋（1284×2778）截圖各 6 張，繁中、英文各一套：體感投球、全倒演出、線上對戰 VS、排位段位、挑戰地圖、外觀
  - 15～30 秒預覽影片
  - 名稱（30 字內）、副標題、關鍵字（100 字元）、描述、更新說明，繁中＋英文
- Manual: 預覽影片用實機錄影；App Store Connect 上傳

### T48: TestFlight 與實機 QA
- Status: todo
- Depends on: T05, T06, T32, T41, T43
- Goal: 正式送審前在多台 iPhone 與外部測試者身上驗證。
- Files: `spec/qa-checklist.md`
- Acceptance:
  - 機型：iPhone SE（第 2 代）、iPhone 12、iPhone 15、最新機型，各跑一次完整 QA 清單
  - 效能：最低機型 ≥ 50 fps、記憶體 < 800 MB、20 分鐘遊玩無當機
  - 網路：4G、弱網（Network Link Conditioner 3G）、飛航模式切換、對戰中切到背景再回來
  - 外部 TestFlight 至少 20 人玩一週：當機率 < 0.5%、教學完成率 > 80%、揮臂被拒絕率 < 15%
  - 內購沙盒、Apple 登入、刪除帳號、推播都走過
  - 送審備註寫好審核用測試帳號與體感操作說明（附影片連結）
- Manual: App Store Connect 建立 TestFlight 群組；招募測試者
