# 體感保齡球（BowlingGame）Spec

> v1.0 上架規格　最後更新：2026-10-17　舊版功能紀錄（v0.1～v0.7）：[spec/design-v0.7.md](spec/design-v0.7.md)

## Overview
握著手機做出真正的保齡球揮臂動作來投球的霓虹夜光風格手遊（也支援滑動操作）。單機已有標準 10 局、練習、19 關挑戰、本機 1～4 人。v1.0 要做三件事：介面改成精緻的手遊風格（大廳、底部導覽、貨幣列、動畫）、接上自己寫的後端（帳號、即時輪流對戰、排位賽、每日任務、好友、聊天、對戰表情、讚美對手）、免費＋內購＋獎勵廣告，先上架 iOS。
核心循環：大廳 → 選模式（單機／排位／好友對戰）→ 投球 → 結算拿 XP／金幣／排位分 → 完成任務 → 換外觀 → 再玩。

## Tech
- Unity 6000.6.5f1，URP 17.6、Input System 1.20、uGUI 2.6（含 TextMeshPro）
- 首發 iOS 15+、僅 iPhone、直向鎖定；之後 Android 8+（API 26）。WebGL 版（`docs/`）只當試玩頁，不接線上功能
- 客戶端要加的套件：`com.unity.nuget.newtonsoft-json`、`com.unity.purchasing`（StoreKit 2）、`com.unity.mobile.notifications`、Sign in with Apple 外掛、廣告 SDK（AdMob）、Firebase Crashlytics／Analytics
- 伺服器：TypeScript + Node.js 22、Fastify（REST）、`ws`（WebSocket）、PostgreSQL 16（Prisma）、Redis 7；本機用 docker compose
- 測試：Unity Test Framework（EditMode）＋ 伺服器 vitest
  - Unity：`/Applications/Unity/Hub/Editor/6000.6.5f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml`
  - 伺服器：`cd Server && npm test`

## Architecture
- `Bowling.Core`（純 C#）：`BowlingScore`、`SwingDetector`、`ChallengeLevels`、資料模型、網路 DTO；不依賴 MonoBehaviour，可測試
- `Bowling.Gameplay`：`BowlingGame`（流程）、`BowlingBall`、`Pin`、`Bowler`、`Obstacle`、`BowlingFX`、`BowlingAudio`、`MotionInput`。每球結束發出 `RollReport`
- `Bowling.UI`：uGUI 畫面堆疊 `UIRouter`；畫面只讀狀態、呼叫服務，不直接改遊戲物件
- `Bowling.Net`：`ApiClient`（REST + JWT）、`RealtimeClient`（WebSocket）、各功能服務（`AuthService`、`MatchService`、`FriendService`、`ChatService`、`MissionService`、`ShopService`）
- `Bowling.Meta`：存檔、經濟、外觀、任務的客戶端狀態（`SaveSystem`、`Wallet`、`Inventory`）
- 溝通方式：靜態 `Services` 註冊表（啟動時建立）＋ C# event；伺服器推播事件經 `RealtimeClient` 分派到主執行緒
- `Server/`：`src/modules/<auth|profile|match|matchmaking|friends|chat|missions|shop|moderation>`，每模組 `routes.ts`、`service.ts`、`ws.ts`；Postgres 存永久資料，Redis 存在線狀態／配對佇列／排行榜
- 資料夾：`Assets/Bowling/{Core,Gameplay,UI,Net,Meta,Editor,Tests}/`、`Assets/Bowling/UI/{Screens,Widgets,Theme}`、`Assets/Bowling/Resources/{Loc,Config}`、`Server/`、`spec/`

## Conventions
- 程式碼識別字用英文，玩家看到的文字一律走 `Loc.T("key")`，鍵名 `area.thing`（例：`lobby.play`）
- 一個類別一個檔案；MonoBehaviour 檔名＝類別名；組件用 `[SerializeField] private`，不用 public 欄位
- 純邏輯放 `Bowling.Core` 並寫 EditMode 測試；Editor 選單工具保留，但檢查邏輯要能被測試呼叫
- UI 一律 uGUI + TMP，字型用 `UITheme` 指定的 Noto Sans TC SDF；不再新增 IMGUI
- 動畫用自己的 `UITween`（不引入 DOTween）；按鈕統一用 `NeonButton`（縮放＋音效＋震動）
- 伺服器說了算：貨幣、背包、任務進度、排位分、比賽分數都以伺服器為準，客戶端只顯示
- 網路訊息：`{ "t": "match.roll", "id": 12, "d": {...} }`；型別名 `<模組>.<動作>`，客戶端 DTO 與 `Server/src/protocol.ts` 同名同欄位
- 伺服器 REST 前綴 `/v1`；錯誤回 `{ code, message }`；所有寫入要冪等（帶 `requestId`）
- 新增 Unity 場景／Prefab 接線寫在任務的 Manual，不靠程式在執行時偷偷建立

## Data
- 客戶端存檔 `SaveData`（JSON，`Application.persistentDataPath/save.json`）：`version`、`settings`、`stats`、`challengeStars[]`、`tutorialDone`、`cachedProfile`；從 PlayerPrefs 一次性遷移
- `PlayerProfile`：`id`、`nickname`、`avatarId`、`level`、`xp`、`coins`、`gems`、`mmr`、`tier`、`commends`、`equipped{ball,trail,outfit}`
- `RollReport`：`frame`、`roll`、`standingBefore`(10-bit)、`knockedMask`(10-bit)、`speed`、`angle`、`hook`、`foul`、`durationMs`、`replay`(壓縮關鍵影格)
- `Match`：`id`、`mode`(ranked|friend)、`players[]`、`turn`、`frames[][]`、`status`、`startedAt`、`result`
- 伺服器表：`users`、`auth_identities`、`profiles`、`matches`、`match_rolls`、`ratings`、`seasons`、`friendships`、`blocks`、`messages`、`mission_instances`、`inventory`、`wallet_ledger`、`purchases`、`commends`、`reports`
- 設定資料（任務模板、商品、外觀、排位段位）放 `Server/config/*.json`，客戶端啟動時抓 `/v1/config`

## Out of scope
- 伺服器端重跑物理（防作弊只做合理性檢查＋保存重播）
- 即時同步球的物理（對手看到的是「重播」，不是同一顆球）
- 3 人以上線上房間、觀戰、公會、群組聊天、自由文字的對戰中聊天
- 賽季通行證、轉蛋／抽卡、Android 首發、Apple Watch、甲蟲主題外觀

## Tasks
任務細節放在 `spec/` 底下，依相依順序排列。

| ID | 標題 | 狀態 | 檔案 |
|---|---|---|---|
| T01 | 組件定義（asmdef）與 EditMode 測試 | done | [foundation](spec/foundation.md) |
| T02 | 拆分 BowlingGame、產出 RollReport | done | [foundation](spec/foundation.md) |
| T03 | JSON 存檔系統與 PlayerPrefs 遷移 | done | [foundation](spec/foundation.md) |
| T04 | 多語系改成資料檔 | done | [foundation](spec/foundation.md) |
| T05 | iOS 建置設定與效能 | todo | [foundation](spec/foundation.md) |
| T06 | 體感實機紀錄與重放工具 | todo | [foundation](spec/foundation.md) |
| T07 | 標準局難度調整 | todo | [foundation](spec/foundation.md) |
| T08 | UI 框架：UIRouter、SafeArea、UITween | todo | [ui](spec/ui.md) |
| T09 | 視覺主題與中文字型 | todo | [ui](spec/ui.md) |
| T10 | 共用 UI 元件 | todo | [ui](spec/ui.md) |
| T11 | 大廳畫面與底部導覽 | todo | [ui](spec/ui.md) |
| T12 | 遊戲中 HUD 重做 | todo | [ui](spec/ui.md) |
| T13 | 結算畫面重做 | todo | [ui](spec/ui.md) |
| T14 | 挑戰地圖、設定、統計畫面 | todo | [ui](spec/ui.md) |
| T15 | 新手教學與首次啟動流程 | todo | [ui](spec/ui.md) |
| T16 | 啟動、載入、轉場、背景音樂 | todo | [ui](spec/ui.md) |
| T17 | 移除 IMGUI | todo | [ui](spec/ui.md) |
| T18 | 伺服器骨架 | todo | [server](spec/server.md) |
| T19 | 帳號與登入（訪客、Apple） | todo | [server](spec/server.md) |
| T20 | WebSocket 閘道與在線狀態 | todo | [server](spec/server.md) |
| T21 | 玩家資料與雲端存檔 | todo | [server](spec/server.md) |
| T22 | 對戰房間 | todo | [server](spec/server.md) |
| T23 | 配對、排位分、段位、排行榜 | todo | [server](spec/server.md) |
| T24 | 好友系統 | todo | [server](spec/server.md) |
| T25 | 私訊聊天與過濾 | todo | [server](spec/server.md) |
| T26 | 每日任務與簽到 | todo | [server](spec/server.md) |
| T27 | 經濟、商店、收據驗證 | todo | [server](spec/server.md) |
| T28 | 讚美對手與檢舉 | todo | [server](spec/server.md) |
| T29 | 部署、維運、強制更新 | todo | [server](spec/server.md) |
| T30 | 客戶端網路層 | todo | [online](spec/online.md) |
| T31 | 登入流程與帳號畫面 | todo | [online](spec/online.md) |
| T32 | 線上對戰客戶端 | todo | [online](spec/online.md) |
| T33 | 配對與排位畫面、排行榜 | todo | [online](spec/online.md) |
| T34 | 對戰中表情與快捷語 | todo | [online](spec/online.md) |
| T35 | 讚美對手與玩家名片 | todo | [online](spec/online.md) |
| T36 | 好友畫面 | todo | [online](spec/online.md) |
| T37 | 聊天畫面 | todo | [online](spec/online.md) |
| T38 | 每日任務畫面 | todo | [online](spec/online.md) |
| T39 | 推播通知 | todo | [online](spec/online.md) |
| T40 | 外觀系統與背包 | todo | [monetization](spec/monetization.md) |
| T41 | 商城與內購 | todo | [monetization](spec/monetization.md) |
| T42 | 獎勵廣告與 ATT | todo | [monetization](spec/monetization.md) |
| T43 | 隱私、合規、法律頁面 | todo | [release](spec/release.md) |
| T44 | 當機回報與分析事件 | todo | [release](spec/release.md) |
| T45 | 正式音效 | todo | [release](spec/release.md) |
| T46 | 正式角色模型與動畫 | todo | [release](spec/release.md) |
| T47 | 商店素材 | todo | [release](spec/release.md) |
| T48 | TestFlight 與實機 QA | todo | [release](spec/release.md) |

### 待決定
1. 伺服器主機：Fly.io／Render／自己的 VPS（T29 預設 Fly.io）
2. 商品價格與首發外觀數量（T27、T40 先用假資料）
3. 排位段位名稱與賽季長度（T23 預設 6 段、每季 8 週）
