# 基礎整理（上架前必修）

### T01: 組件定義（asmdef）與 EditMode 測試
- Status: done
- Depends on: —
- Goal: 把腳本分成多個 assembly，並把現有的 Editor 檢查改成可以在命令列跑的 EditMode 測試。
- Files: `Assets/Bowling/Core/Bowling.Core.asmdef`、`Assets/Bowling/Gameplay/Bowling.Gameplay.asmdef`（`Scripts/` 改名為 `Gameplay/`）、`Assets/Bowling/Editor/Bowling.Editor.asmdef`、`Assets/Bowling/Tests/Bowling.Tests.asmdef`、`Assets/Bowling/Tests/ScoreTests.cs`、`SwingDetectorTests.cs`、`ChallengeLevelTests.cs`；把 `BowlingScore.cs`、`SwingDetector.cs`、`ChallengeLevels.cs` 搬到 `Core/`
- Interfaces: `Bowling.Core` 不參考 `UnityEngine` 以外的東西；`Bowling.Tests` 參考 Core、Gameplay、`UnityEngine.TestRunner`、`nunit.framework`，`includePlatforms: [Editor]`
- Acceptance:
  - 命令列 `-runTests -testPlatform EditMode` 全部通過，結果寫到 `Logs/editmode.xml`
  - `BowlingScoreCheck`、`BowlingSwingCheck` 的每個案例都有對應的 `[Test]`
  - 原本 Bowling 選單的工具仍可用
- Manual: 在 Unity 開專案確認沒有編譯錯誤；Test Runner 視窗看得到測試
- Notes: 搬檔時連 `.meta` 一起用 `git mv`，GUID 才不會變。`Assets/TutorialInfo/` 與 `Readme.asset` 是範本殘留，一併刪除。
- Deviation: `ChallengeLevels.cs` 原本用到的 `BallType`（原在 `BowlingBall.cs`）和 `BowlingLane`（原在 `Scripts/`，純資料無依賴）也一起搬進 `Core/`，否則 Core 會反過來參考 Gameplay。`ChallengeLevel.Name`/`Description`（讀 `GameSettings.Language`）改成 Gameplay 端的 extension method `LocalizedName()`/`LocalizedDescription()`，`BowlingUI.cs` 的 5 個呼叫點已同步更新。

### T02: 拆分 BowlingGame、產出 RollReport
- Status: done
- Depends on: T01
- Goal: 把 729 行的 `BowlingGame` 拆小，並讓每一球結束時發出可以送到伺服器的 `RollReport`。
- Files: `Gameplay/BowlingGame.cs`（只留模式與流程）、`Gameplay/RackController.cs`（排瓶、清瓶、倒瓶判定）、`Gameplay/CameraRig.cs`、`Gameplay/ReplayRecorder.cs`、`Core/RollReport.cs`、`Core/ReplayData.cs`、`Tests/RollReportTests.cs`
- Interfaces:
  - `public event Action<RollReport> RollFinished;`
  - `public interface IMatchDriver { bool IsLocalTurn { get; } void OnRollFinished(RollReport r); }`：單機、本機多人、線上對戰各一個實作
  - `ReplayData`：每 1/30 秒一格的球與 10 支瓶的位置＋旋轉，`byte[] Serialize()`／`static ReplayData Deserialize(byte[])`，量化成 16-bit
  - `public void PlayReplay(ReplayData data, bool[] standingBefore)`：重現任何人（包含對手）的一球
- Acceptance:
  - 標準局、練習、挑戰、本機多人行為和拆分前一樣（`-autoplay` 跑完一輪沒有錯誤）
  - 一球的 `ReplayData` 序列化後 ≤ 24 KB
  - `RollReport.knockedMask` 與計分用的倒瓶數一致（測試）
- Notes: 原本的「重播」功能改用 `ReplayRecorder`，不要保留兩套。
- Deviation: `IMatchDriver`介面放在 `Core/IMatchDriver.cs`（只依賴 `RollReport`），三個實作（`LocalMatchDriver`、`LocalMultiplayerMatchDriver`、`OnlineMatchDriver` 佔位）放在 `Gameplay/MatchDrivers.cs`；`CanThrow` 多了 `&& driver.IsLocalTurn`（目前永遠真，先接好線）。重播只錄「球＋10瓶」，不含關卡障礙物（障礙物在重播時單純暫停，符合 `ReplayData` 的 11-物件範圍）。Review 抓到一個真的 bug：`HasReplay` 原本讀的是錄製中的 buffer，但 `Finish()` 會把它清空，導致重播鍵永遠不會出現；已改成基於已完成的 `ReplayData.FrameCount` 判斷，並把 `ReplayRecorder.MaxFrames` 從 360 降到 180，確保最差情況（慢速球+長時間沉澱）也在 24 KB 預算內（原本只有「典型」情況過測試）。

### T03: JSON 存檔系統與 PlayerPrefs 遷移
- Status: done
- Depends on: T01
- Goal: 把設定、統計、挑戰星星改存成一個有版本號的 JSON 檔，之後可以和伺服器同步。
- Files: `Meta/SaveData.cs`、`Meta/SaveSystem.cs`、`Meta/ISaveStore.cs`、`Meta/FileSaveStore.cs`、`Meta/Bowling.Meta.asmdef`；修改 `GameSettings.cs`、`PlayerStats.cs`、`Bowling.Gameplay.asmdef`、`Bowling.Tests.asmdef`；`Tests/SaveSystemTests.cs`
- Interfaces: `SaveSystem.Data`、`SaveSystem.Save()`（延遲 0.5 秒合併寫入）、`event Action Changed`；`ISaveStore { string Load(); void Write(string json); }`
- Acceptance:
  - 第一次啟動時把舊 PlayerPrefs 的值搬進 `save.json`，之後不再讀 PlayerPrefs
  - 寫檔先寫 `save.tmp` 再改名，中途關掉 App 不會壞檔；壞檔時讀 `save.bak`
  - `version` 欄位存在，遷移函式有測試
- Notes: `GameSettings`、`PlayerStats` 對外的屬性名稱不變，呼叫端不用改。
- Deviation: `SaveData` 裡 `settings`/`stats` 做成 `SaveData` 的 nested class（不是獨立檔案），維持「一個檔案一個頂層類別」。`cachedProfile` 先存成 `cachedProfileJson`（原始字串），等 T19+ 真的有 `PlayerProfile` 型別再解。`SaveSystem.Save()` 在 `Application.isPlaying` 時才用隱藏的 `Runner` MonoBehaviour 跑 0.5 秒合併寫入，Editor／EditMode 測試裡（沒有 Update 迴圈）直接同步寫入，測試才不用等。Review 抓到兩個 bug：(1) 合併寫入期間被 OS 中止（切到背景／被殺）會漏存，已在 `Runner` 補上 `OnApplicationPause`／`OnApplicationQuit` 強制 flush；(2) `save.json` 格式像 JSON（有大括號）但內容解不開時，原本會讓 `JsonUtility.FromJson` 直接丟例外，使 `save.bak` 的備援失效，已讓 `FileSaveStore`／`SaveSystem` 都包 try/catch 並視為「讀取失敗」退回備援，新增對應測試。

### T04: 多語系改成資料檔
- Status: todo
- Depends on: T01
- Goal: 字串從 `Loc.cs` 的程式碼搬到資料檔，方便新增大量線上功能的文字和之後找翻譯。
- Files: `Resources/Loc/zh-TW.json`、`Resources/Loc/en.json`、修改 `Loc.cs`、`Tests/LocTests.cs`
- Interfaces: `Loc.T(key)`、`Loc.F(key, args)` 不變；新增 `Loc.Language`、`event Action LanguageChanged`
- Acceptance:
  - 兩個語言檔的鍵完全一致（測試會比對）
  - 缺鍵時回傳 `"#key"` 並在 Editor 印警告
  - 切換語言後畫面上的文字立刻更新（`LocText` 組件聽 `LanguageChanged`）
- Notes: 新增 `UI/Widgets/LocText.cs`：掛在 TMP 文字上，填鍵名就好。

### T05: iOS 建置設定與效能
- Status: todo
- Depends on: T01
- Goal: 可以從命令列輸出 iOS Xcode 專案，並達到 iPhone 上穩定 60 fps。
- Files: `Editor/BowlingBuild.cs`（新增 `iOS()`、讀 `-buildNumber`）、`Settings/URP-Mobile.asset`、`Settings/URP-Mobile_Renderer.asset`、`Gameplay/PerformanceGovernor.cs`
- Interfaces: 選單 Bowling → Build iOS，輸出到 `Builds/iOS/`；`PerformanceGovernor` 偵測連續掉幀時降低 Bloom 品質與陰影
- Acceptance:
  - `-executeMethod BowlingBuild.iOS` 成功產生 Xcode 專案
  - Player Settings：bundle id `com.<你的名字>.neonbowling`（不能再是 Unity 範本的 id）、產品名稱、公司名、iOS 15.0、只支援 iPhone、只允許直向
  - iPhone 12 上標準局平均 ≥ 58 fps、10 分鐘後不明顯發燙（Xcode 的 Energy 報告）
  - `Application.targetFrameRate = 60`
- Manual: Xcode 設定 Signing Team；App Store Connect 建立 App 記錄
- Notes: 目前 Player Settings 允許四個方向旋轉，體感揮臂時畫面會轉，必須鎖直向。

### T06: 體感實機紀錄與重放工具
- Status: todo
- Depends on: T01, T05
- Goal: 在真的 iPhone 上錄下揮臂的感測器資料，帶回 Editor 重放，用來調整揮臂判定。
- Files: `Gameplay/MotionRecorder.cs`、`Editor/BowlingSwingCheck.cs`（可讀錄製檔）、`Tests/SwingRecordingTests.cs`、`Tests/Recordings/*.json`
- Interfaces: 開發者選單（設定頁連點版本號 5 次）→「錄製揮臂」；檔案存在 `persistentDataPath/swings/`，用 iOS 分享面板匯出
- Acceptance:
  - 至少 30 筆真實揮臂（正常、輕甩手腕、曲球、犯規）放進 `Tests/Recordings/`，測試確認判定結果符合標註
  - 只甩手腕的錄製檔不會被判定為有效投球
- Manual: 找 3 個人、各用兩種握法實際揮臂錄製
- Notes: 正式版建置時開發者選單用 `#if DEVELOPMENT_BUILD` 關掉。

### T07: 標準局難度調整
- Status: todo
- Depends on: T02
- Goal: 修正「偏 15 cm 的直球也常常全倒」，讓全倒需要打進 pocket。
- Files: `Gameplay/BowlingBall.cs`、`Gameplay/Pin.cs`、`Editor/BowlingPhysicsCheck.cs`、`Tests/PhysicsSweepTests.cs`
- Acceptance（Physics Sweep，每點 50 球、速度 7～9 m/s）:
  - 正中央直球全倒率 20～40%
  - pocket（偏右 6 cm、帶曲球）全倒率 ≥ 55%
  - 偏離 15 cm 直球全倒率 ≤ 10%
  - 挑戰關卡 Challenge Check 每關仍可過關
- Notes: 先調球瓶間摩擦與球的角速度，不要改球道尺寸。排位賽的公平性靠這個。
