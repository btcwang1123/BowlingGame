# UI 手遊化

設計方向：霓虹夜光延續到介面。深藍紫底、半透明玻璃面板、青／粉紅／黃三色霓虹描邊、粗體圓角中文字。每個按鈕按下都有回饋，數字變化都有滾動動畫，重要時刻（全倒、升段、領獎）要有全螢幕演出。參考：Clash Royale、8 Ball Pool、Bowling Crew 的大廳結構。
畫面基準 1080×1920 直向，CanvasScaler Match = 0.5，所有可點區域 ≥ 44 pt，內容避開瀏海和 Home 指示條。

### T08: UI 框架：UIRouter、SafeArea、UITween
- Status: todo
- Depends on: T01, T04
- Goal: 建立 uGUI 畫面堆疊、安全區域、補間動畫這三個所有畫面都會用的基礎。
- Files: `UI/UIRouter.cs`、`UI/UIScreen.cs`、`UI/UIPopup.cs`、`UI/SafeArea.cs`、`UI/UITween.cs`、`UI/Bowling.UI.asmdef`、`Tests/UITweenTests.cs`、`Tests/UIRouterTests.cs`
- Interfaces:
  - `UIRouter.Push<T>(object args = null) where T : UIScreen`、`Pop()`、`Replace<T>()`、`ShowPopup<T>()`、`Back()`（Android 返回鍵／iOS 左滑也走這裡）
  - `UIScreen`：`virtual void OnEnter(object args)`、`OnExit()`、`OnBack() → bool`；進出場預設動畫（淡入＋上滑 40 px，0.25 秒，EaseOutCubic）
  - `UITween.Move/Scale/Fade/Value(float from, float to, float dur, Ease ease, Action<float> set)`，回傳可 `Kill()` 的 handle；用 unscaled time（慢動作時 UI 不變慢）
  - `SafeArea`：掛在畫面根節點，依 `Screen.safeArea` 調 anchor
- Acceptance:
  - 測試：Push 兩個畫面後 Back 回到第一個，且 `OnExit`／`OnEnter` 呼叫順序正確
  - 測試：各種 Ease 在 t=0、0.5、1 的值正確
  - iPhone 15 Pro 模擬器上瀏海和底部條不會蓋到按鈕
- Manual: 場景建立 `UIRoot`（Canvas Screen Space - Overlay、CanvasScaler、EventSystem 用 Input System UI Input Module），各畫面做成 Prefab 放 `Resources/UI/Screens/`
- Notes: `Time.timeScale` 在慢動作與暫停時會變，UI 一律用 unscaled。

### T09: 視覺主題與中文字型
- Status: todo
- Depends on: T08
- Goal: 統一顏色、字型、面板圖，所有畫面從同一個地方取用。
- Files: `UI/Theme/UITheme.cs`（ScriptableObject）、`UI/Theme/DefaultTheme.asset`、`UI/Theme/NeonPanel.shader`（圓角＋描邊＋外光暈，可調顏色）、`Editor/FontAssetBuilder.cs`
- Interfaces: `UITheme.Current`；欄位：`primary`(#2DE2E6)、`accent`(#FF3CAC)、`gold`(#FFD23F)、`bg`(#120B2E)、`panel`、`textMain`、`textDim`、`danger`；`TMP_FontAsset fontBold, fontRegular`；`Sprite panel9, buttonPrimary9, buttonSecondary9, iconSet[]`
- Acceptance:
  - 字型：Noto Sans TC（Bold、Regular）產生 TMP SDF，靜態字集＝常用 4808 字＋英數符號，`Loc` 檔裡出現的每個字都在字集內（Editor 檢查）；動態 fallback 給玩家暱稱和聊天
  - App 大小增加 ≤ 15 MB
  - `NeonPanel` shader 在 iPhone 上一個畫面 30 個面板仍 60 fps
- Manual: 從 Google Fonts 下載 Noto Sans TC 放進 `Assets/Bowling/UI/Fonts/`；圖示用 Material Symbols（Apache 2.0）或購買圖示包
- Notes: 霓虹光暈用 shader 做，不要每個按鈕一張發光圖。

### T10: 共用 UI 元件
- Status: todo
- Depends on: T09
- Goal: 做好所有畫面重複使用的元件。
- Files: `UI/Widgets/NeonButton.cs`、`CurrencyBar.cs`、`Toast.cs`、`ConfirmPopup.cs`、`LoadingOverlay.cs`、`RedDot.cs`、`TabBar.cs`、`AvatarFrame.cs`、`CountUpText.cs`、`ProgressBar.cs`、`RewardFlyer.cs`
- Interfaces:
  - `NeonButton`：按下縮到 0.92、放開回彈；點擊音效＋輕震動；`interactable=false` 時變灰；防連點 0.3 秒
  - `CurrencyBar`：金幣／寶石數字，變化時 `CountUpText` 滾動並跳一下；點 + 開商城
  - `Toast.Show(string, float sec = 2)`；`ConfirmPopup.Show(title, body, okKey, cancelKey) → Task<bool>`
  - `RedDot.Bind(string path)`：以字串路徑（`social.requests`）綁定未讀數，子節點有就亮
  - `RewardFlyer.Fly(icon, count, from, toCurrencyBar)`：獎勵圖示沿曲線飛進貨幣列
- Acceptance:
  - 每個元件有一個測試場景 `Scenes/WidgetGallery.unity` 可以一次看全部
  - `RedDot` 路徑聚合有測試（子路徑加總到父路徑）
- Manual: 每個元件做成 Prefab 放 `Resources/UI/Widgets/`

### T11: 大廳畫面與底部導覽
- Status: todo
- Depends on: T10, T03
- Goal: 取代目前的主選單，做成手遊式大廳。
- Files: `UI/Screens/LobbyScreen.cs`、`ModeSelectPopup.cs`、`UI/Screens/MainShell.cs`（頂列＋底部導覽的外框）、`Gameplay/LobbyShowcase.cs`
- Interfaces:
  - 頂列：頭像框、暱稱、等級＋XP 條、金幣、寶石、設定齒輪
  - 中間：3D 霓虹角色拿著目前裝備的球，會轉身／揮手（`LobbyShowcase`，用遊戲場景的另一台攝影機算圖到 RenderTexture）
  - 大按鈕「開始」：預設上次玩的模式；旁邊小按鈕切換模式（排位、好友對戰、單機 10 局、練習、挑戰）
  - 左側浮動入口：每日任務（紅點）、簽到；右側：活動 banner（伺服器設定）
  - 底部 5 個分頁：商城、背包、大廳（中間、凸起）、社交、排行
- Acceptance:
  - 冷啟動到大廳可操作 ≤ 3 秒（iPhone 12）
  - 未登入／離線時，線上入口顯示鎖頭與「需要連線」提示，單機模式照常可玩
  - 分頁切換有左右滑動動畫，切換時不重建畫面
- Manual: 大廳背景用目前的霓虹球館，鏡頭位置在 Inspector 設定

### T12: 遊戲中 HUD 重做
- Status: todo
- Depends on: T10, T02
- Goal: 投球畫面的資訊清楚、不擋球道，結果演出更有衝擊感。
- Files: `UI/Screens/GameHud.cs`、`UI/Widgets/ScoreStrip.cs`、`PlayerCard.cs`、`ThrowStatsCard.cs`、`ResultBurst.cs`、`TurnTimer.cs`
- Interfaces:
  - `ScoreStrip`：頂部可橫捲的 10 局記分格，目前局發光；點一下展開完整記分板
  - `PlayerCard`：頭像、暱稱、目前總分；線上對戰時左右各一張（VS 版面），輪到誰誰發光
  - `ResultBurst.Play(mark)`：STRIKE／SPARE／GUTTER／連續 STRIKE（DOUBLE、TURKEY）大字從畫面中央放大＋震動＋光環
  - `ThrowStatsCard`：球速、角度、曲球，0.3 秒滑入、3 秒後縮小到角落
  - `TurnTimer`：線上對戰的環狀倒數，剩 5 秒變紅並震動
- Acceptance:
  - 球道中央 60% 寬度在投球中沒有 UI 遮擋
  - 連續 3 次全倒顯示 TURKEY（測試 mark 序列 → 顯示字）
  - 暫停鍵在右上角安全區內
- Notes: 這個畫面要同時支援單機、本機多人（最多 4 張 PlayerCard 縮小排列）、線上對戰。

### T13: 結算畫面重做
- Status: todo
- Depends on: T12
- Goal: 結算有節奏地依序揭曉，讓玩家有「領獎」的感覺。
- Files: `UI/Screens/ResultScreen.cs`、`UI/Widgets/RankChangePanel.cs`、`UI/Widgets/RewardRow.cs`
- Interfaces: `ResultScreen.OnEnter(MatchResultView v)`；`MatchResultView`：分數、名次、統計、星星、`rewards[]`、`rankDelta?`、`opponents[]`、`canCommend`
- Acceptance:
  - 演出順序：總分滾動 → 新紀錄印章 → 星星一顆顆亮 → XP 條增加（升級時全螢幕演出）→ 金幣飛進貨幣列 → 排位分變化（升段時段位徽章演出）→ 按鈕出現
  - 點螢幕任意處可以跳過演出直接看結果
  - 按鈕：再來一場、回大廳、分享截圖（iOS 分享面板）；線上對戰多「讚美對手」（T35）
- Notes: 單機的獎勵也要經伺服器發（離線時暫存，連線後補發，見 T21）。

### T14: 挑戰地圖、設定、統計畫面
- Status: todo
- Depends on: T10
- Goal: 把剩下的舊畫面搬到新 UI，挑戰關卡改成手遊式的關卡地圖。
- Files: `UI/Screens/ChallengeMapScreen.cs`、`SettingsScreen.cs`、`StatsScreen.cs`、`CalibrationScreen.cs`、`PracticeScreen.cs`、`PauseMenu.cs`
- Interfaces:
  - 關卡地圖：直向捲動的霓虹路徑，每關一個節點（星星、鎖頭），目前關卡有呼吸光；點節點跳出關卡說明＋開始
  - 設定分頁：遊戲（操作、慣用手、瞄準線、球重）、音效（音樂、音效、震動）、帳號（T31）、其他（語言、隱私權政策、服務條款、客服信箱、版本號）
- Acceptance:
  - 舊 `BowlingUI` 的每個功能在新畫面都找得到（對照清單寫在 PR 說明）
  - 揮臂校正有動畫示範與 3 次進度指示
- Notes: 關卡數會增加，地圖要能從 `ChallengeLevels` 自動產生節點。

### T15: 新手教學與首次啟動流程
- Status: todo
- Depends on: T11, T12
- Goal: 第一次打開的人 60 秒內學會投球，並自然地完成第一場。
- Files: `UI/Screens/OnboardingFlow.cs`、`Gameplay/TutorialDirector.cs`、`UI/Widgets/CoachMark.cs`
- Interfaces: 流程：語言（跟系統，可改）→ 安全提醒 → 選操作（體感／滑動，附動畫）→ 互動教學：投 3 球（站位、出手、曲球），每步有手指／手臂動畫提示 → 取暱稱 → 進大廳並亮起「開始」；`SaveData.tutorialDone`
- Acceptance:
  - 教學中第 1 球保證打到球瓶（暫時加大瞄準容錯）
  - 可以跳過；設定裡可以重看
  - 暱稱 2～12 字，過濾不當字（伺服器 T25 的過濾器；離線時先存，連線後檢查）
- Notes: 分析事件記錄每一步完成率（T44）。

### T16: 啟動、載入、轉場、背景音樂
- Status: todo
- Depends on: T10
- Goal: 補上手遊必備的開場、載入和聲音氛圍。
- Files: `UI/Screens/SplashScreen.cs`、`LoadingScreen.cs`、`UI/ScreenFader.cs`、`Gameplay/MusicPlayer.cs`、`Gameplay/UISfx.cs`
- Interfaces: 啟動流程：iOS Launch Screen（靜態 logo）→ Splash（logo 霓虹點亮動畫 1.5 秒，同時登入與抓設定）→ 大廳；`MusicPlayer.Play(track, fade)`：大廳、對戰、結算三首，切換交叉淡入淡出
- Acceptance:
  - 網路慢時 Splash 最多等 5 秒就進大廳，線上功能背景重試
  - 背景音樂和音效音量分開設定；接電話／切到背景時暫停，回來恢復
  - iPhone 靜音鍵開著時遵守系統設定（AVAudioSession Ambient）
- Manual: 背景音樂需購買或委託（授權要能商用）

### T17: 移除 IMGUI
- Status: todo
- Depends on: T11, T12, T13, T14, T15
- Goal: 刪掉舊的 `BowlingUI` 和所有 `OnGUI`。
- Files: 刪除 `Gameplay/BowlingUI.cs`；修改 `BowlingAutoplay.cs`（改透過 `UIRouter`／遊戲 API 操作）
- Acceptance:
  - 專案中搜尋 `OnGUI`、`GUI.` 沒有結果（Editor 資料夾除外）
  - `-autoplay` 仍能跑完整輪
