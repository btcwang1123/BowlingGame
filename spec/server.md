# 伺服器（Server/）

## 協定總覽
- REST：`https://api.<網域>/v1/...`，`Authorization: Bearer <accessToken>`（JWT，15 分鐘），`refreshToken` 30 天、一次性輪替
- WebSocket：`wss://api.<網域>/v1/ws?token=<accessToken>`；訊息 `{ t, id?, d }`；客戶端請求帶 `id`，伺服器回 `{ t: "ack", id, ok, d|err }`
- 伺服器推播類型：`presence.update`、`friend.request`、`friend.accepted`、`chat.message`、`match.found`、`match.state`、`match.roll`、`match.emote`、`match.end`、`invite.received`、`mission.progress`、`wallet.changed`、`server.notice`
- 所有型別定義在 `Server/src/protocol.ts`，客戶端 `Assets/Bowling/Net/Protocol/` 同名對應
- 時區：每日重置 00:00 Asia/Taipei（UTC+8），設定可改

### T18: 伺服器骨架
- Status: todo
- Depends on: —
- Goal: 建立可在本機一鍵啟動、有測試與資料庫遷移的 TypeScript 伺服器專案。
- Files: `Server/package.json`、`tsconfig.json`、`src/index.ts`、`src/app.ts`、`src/config.ts`、`src/db.ts`、`src/redis.ts`、`src/errors.ts`、`src/protocol.ts`、`prisma/schema.prisma`、`docker-compose.yml`、`.env.example`、`test/health.test.ts`、`Server/README.md`
- Interfaces: `GET /v1/health → { ok, version }`；`GET /v1/config → { minClientVersion, missions, catalog, tiers, banners }`；腳本 `npm run dev`、`npm test`、`npm run migrate`、`npm run lint`
- Acceptance:
  - `docker compose up -d && npm run migrate && npm test` 在乾淨的 Mac 上通過
  - 設定全部從環境變數讀，`.env.example` 列出每一個
  - 結構化 JSON log（pino），每個請求有 requestId
- Notes: 測試用獨立資料庫（`DATABASE_URL_TEST`），每個測試檔開始時清空。

### T19: 帳號與登入（訪客、Apple）
- Status: todo
- Depends on: T18
- Goal: 玩家不用註冊就能玩，之後可綁定 Apple 帳號保住進度，並能在 App 內刪除帳號。
- Files: `src/modules/auth/{routes,service}.ts`、`src/modules/auth/apple.ts`、`src/auth/jwt.ts`、`test/auth.test.ts`
- Interfaces:
  - `POST /v1/auth/guest { deviceId } → { accessToken, refreshToken, userId, isNew }`
  - `POST /v1/auth/apple { identityToken, nonce } → 同上`（用 Apple 公鑰驗證 JWT、檢查 aud／nonce）
  - `POST /v1/auth/link/apple`（目前訪客帳號綁 Apple；若該 Apple 已有帳號，回 `409 { existingUserId }` 讓客戶端問要用哪個）
  - `POST /v1/auth/refresh`、`POST /v1/auth/logout`、`DELETE /v1/account`（刪除帳號：個資立即刪，比賽紀錄匿名化；呼叫 Apple revoke token）
- Acceptance:
  - 測試：訪客登入兩次同 deviceId 拿到同一個 userId
  - 測試：refresh token 用過一次就失效；重複使用時整組 token 撤銷
  - 測試：刪除帳號後用舊 token 呼叫任何 API 回 401，暱稱可被別人使用
  - 登入 API 每 IP 每分鐘最多 20 次
- Manual: Apple Developer 開 Sign in with Apple、建立 Key（.p8），放到伺服器環境變數
- Notes: deviceId 用 iOS `identifierForVendor` 加上 Keychain 存的隨機 UUID（重裝 App 後仍能找回訪客帳號）。

### T20: WebSocket 閘道與在線狀態
- Status: todo
- Depends on: T19
- Goal: 建立長連線，讓對戰、聊天、好友上線狀態都能即時推播。
- Files: `src/ws/gateway.ts`、`src/ws/session.ts`、`src/ws/router.ts`、`src/modules/presence/service.ts`、`test/ws.test.ts`
- Interfaces: `router.on(type, handler(session, d) → Promise<result>)`；`gateway.sendTo(userId, msg)`（跨多台伺服器用 Redis pub/sub）；在線狀態 `online|in_match|offline` 存 Redis，TTL 60 秒，心跳每 20 秒
- Acceptance:
  - 同一帳號第二次連線會踢掉第一個（推 `server.notice{kicked}`）
  - 斷線 30 秒內重連帶 `resume=<sessionId>`，補送期間錯過的推播
  - 每連線每秒最多 10 則訊息，超過回錯誤，持續超過斷線
  - 測試：好友上線時收到 `presence.update`
- Notes: 先單台伺服器，但寫法要能水平擴充（不在記憶體裡存跨使用者狀態）。

### T21: 玩家資料與雲端存檔
- Status: todo
- Depends on: T19
- Goal: 玩家資料、統計、挑戰進度存在伺服器，換手機也找得回來。
- Files: `src/modules/profile/{routes,service}.ts`、`src/modules/progress/{routes,service}.ts`、`test/profile.test.ts`
- Interfaces:
  - `GET /v1/me → PlayerProfile`；`PATCH /v1/me { nickname?, avatarId? }`
  - `GET /v1/progress`、`PUT /v1/progress { version, stats, challengeStars, settings }`：`version` 不符回 409 與伺服器版本，客戶端合併（星星取大、統計取大）後重送
  - `POST /v1/offline-results [{ requestId, mode, score, stars, playedAt }]`：補發離線單機場的 XP／金幣（每日上限）
  - XP 曲線：`xpForLevel(n) = 100 + 50*(n-1)`，升級獎勵在 config
- Acceptance:
  - 測試：暱稱不分大小寫唯一、過濾不當字、30 天內只能改一次（第一次免費）
  - 測試：同一 `requestId` 重送不會重複發獎
  - 離線獎勵每日上限生效
- Notes: 金幣／寶石不在 progress 裡，一律走 `wallet_ledger`（T27）。

### T22: 對戰房間
- Status: todo
- Depends on: T20, T21
- Goal: 兩位玩家即時輪流投球，伺服器負責回合、計分、計時與斷線處理。
- Files: `src/modules/match/room.ts`、`src/modules/match/scoring.ts`（移植 `BowlingScore`）、`src/modules/match/validate.ts`、`src/modules/match/ws.ts`、`src/modules/match/service.ts`、`test/match.test.ts`、`test/scoring.test.ts`
- Interfaces:
  - 狀態：`waiting → countdown → playing → finished|aborted`；回合：每局換人（P1 第 1 局、P2 第 1 局…），每回合 30 秒內要出手
  - 客戶端 → `match.ready`、`match.roll { matchId, frame, roll, knockedMask, speed, angle, hook, foul, durationMs, replay(base64) }`、`match.emote { emoteId }`、`match.leave`
  - 伺服器 → `match.state`（完整狀態，重連時也送）、`match.roll`（轉發給對手，含 replay）、`match.emote`、`match.end { scores, winner, reason, rewards, rankDelta }`
  - 模式：`ranked`（T23 配對）、`friend`（T24 邀請，不影響排位分）
- Acceptance:
  - 測試：伺服器計分與 Unity `BowlingScore` 用同一組 50 個案例比對，結果完全相同（案例存 `Server/test/fixtures/score-cases.json`，Unity 測試也讀）
  - 驗證拒絕：knockedMask 有不在 standingBefore 的瓶、不是自己的回合、`durationMs < 1500`、速度超出 3.5～11 m/s；被拒時回錯誤並記錄 `suspicious` 計數
  - 回合逾時：自動記 0 分（洗溝）；連續 2 次逾時或斷線 60 秒＝判負
  - 對手中途離開：留下的人勝，獲得完整獎勵
  - 比賽與每一球存 `matches`、`match_rolls`（replay 保留 7 天）
- Notes: 物理在投球者的手機上算，伺服器只做合理性檢查（見 SPEC 的 Out of scope）。某玩家 `suspicious` 過高時排位賽只配同類玩家（T23）。

### T23: 配對、排位分、段位、排行榜
- Status: todo
- Depends on: T22
- Goal: 排位賽依實力配對，分出段位並有賽季與排行榜。
- Files: `src/modules/matchmaking/{queue,service,ws}.ts`、`src/modules/rating/{elo,tiers,season}.ts`、`src/modules/leaderboard/{routes,service}.ts`、`config/tiers.json`、`test/matchmaking.test.ts`、`test/rating.test.ts`
- Interfaces:
  - `mm.join { mode: "ranked" }`、`mm.cancel`；伺服器推 `match.found { matchId, opponent }`
  - 配對：Redis 佇列，±100 分起，每 5 秒放寬 50，最多 ±400；45 秒找不到時推 `mm.timeout`，客戶端可選「和 AI 練習」（明確標示是 AI，不算排位）
  - 分數：Elo，K=32（前 10 場 K=48），初始 1000；段位：青銅 / 白銀 / 黃金 / 白金 / 鑽石 / 霓虹大師，各 3 小段（大師除外），門檻在 `tiers.json`
  - 段位保護：剛升段後 3 場不會降段
  - 賽季：預設 8 週；結束時依最高段位發獎、分數軟重置 `new = 1000 + (old-1000)*0.5`
  - `GET /v1/leaderboard?scope=global|friends&season=current` → 前 100 名＋自己名次（Redis sorted set）
- Acceptance:
  - 測試：兩人分差 >400 永遠不會被配到一起
  - 測試：Elo 勝敗分數計算、段位換算、升段保護、賽季重置
  - 同一對玩家 10 分鐘內不會連續配對超過 2 次
- Notes: 玩家少的時候配對會慢；上線初期可把起始範圍放寬到 ±200（config）。

### T24: 好友系統
- Status: todo
- Depends on: T20, T21
- Goal: 玩家能加好友、看上線狀態、邀請好友對戰、封鎖。
- Files: `src/modules/friends/{routes,service,ws}.ts`、`src/modules/invites/{service,ws}.ts`、`test/friends.test.ts`
- Interfaces:
  - 每位玩家有 8 碼好友碼（不含易混淆字元）
  - `GET /v1/friends` → 好友清單＋在線狀態＋段位；`GET /v1/users/search?q=`（暱稱前綴，最多 20 筆）
  - `POST /v1/friends/requests { toUserId | friendCode }`、`POST /v1/friends/requests/:id/accept|decline`、`DELETE /v1/friends/:userId`
  - `POST /v1/blocks/:userId`、`DELETE /v1/blocks/:userId`：封鎖後雙方互相看不到請求、訊息、邀請，並自動解除好友
  - `invite.send { toUserId }` → 對方收到 `invite.received`（60 秒過期）→ `invite.accept` 建立 `friend` 模式房間
  - 最近對手：`GET /v1/recent-opponents`（最近 20 場），可直接加好友
- Acceptance:
  - 好友上限 200；待處理請求上限 50
  - 測試：封鎖後對方的好友請求、聊天、邀請全部被拒且不洩漏被封鎖的事實（回一般錯誤）
  - 測試：互相送請求時自動成為好友
- Notes: 搜尋結果不顯示已封鎖你的人。

### T25: 私訊聊天與過濾
- Status: todo
- Depends on: T24
- Goal: 好友之間一對一聊天，符合 App Store 對使用者內容的要求（過濾、檢舉、封鎖）。
- Files: `src/modules/chat/{routes,service,ws}.ts`、`src/moderation/filter.ts`、`config/badwords/{zh,en}.txt`、`test/chat.test.ts`、`test/filter.test.ts`
- Interfaces:
  - `chat.send { toUserId, text, clientMsgId }` → ack `{ msgId, sentAt, filteredText }`；對方收 `chat.message`
  - `GET /v1/chat/conversations` → 每個對話最後一則與未讀數；`GET /v1/chat/:userId?before=<msgId>&limit=30`；`POST /v1/chat/:userId/read { upToMsgId }`
  - 過濾：不當字換成 `＊`，含網址／電話／LINE ID 樣式的訊息遮蔽；`filter.clean(text) → { text, flagged }`，暱稱共用
- Acceptance:
  - 只有好友能互傳；每則 ≤ 200 字；每人每 10 秒最多 5 則
  - 訊息保留 90 天；刪除帳號時一起刪
  - 測試：繁中、英文、全形、插入空白／符號規避的不當字都會被擋
- Notes: 對戰中不提供自由文字，只有表情與快捷語（T34），降低審核風險。

### T26: 每日任務與簽到
- Status: todo
- Depends on: T21, T22
- Goal: 每天給 3 個任務和簽到獎勵，讓玩家有理由每天回來。
- Files: `src/modules/missions/{routes,service,progress}.ts`、`config/missions.json`、`src/modules/checkin/{routes,service}.ts`、`config/checkin.json`、`test/missions.test.ts`
- Interfaces:
  - 任務模板：`{ id, type, target, reward, weight, minLevel }`；type：`play_matches`、`win_ranked`、`strikes`、`spares`、`score_at_least`、`challenge_stars`、`commend_given`、`play_with_friend`、`hook_strike`
  - 每日重置時從模板依權重抽 3 個（同類型不重複）；可免費換 1 次、看廣告再換 1 次（T42）
  - 進度來源：伺服器自己知道的（線上比賽結果、讚美）直接算；單機項目來自 `POST /v1/offline-results`，有每日上限
  - `GET /v1/missions`、`POST /v1/missions/:id/claim`、`POST /v1/missions/:id/reroll`
  - 簽到：7 天循環，斷簽從第 1 天重來；`GET /v1/checkin`、`POST /v1/checkin`
  - 每日 3 個全完成額外獎勵寶箱
- Acceptance:
  - 測試：跨過 00:00（UTC+8）重置、同類型不重複、領過不能再領、進度不超過目標
  - 測試：用假時間跑 8 天簽到循環與斷簽
- Notes: 時間一律以伺服器時間為準，客戶端只顯示倒數。

### T27: 經濟、商店、收據驗證
- Status: todo
- Depends on: T21
- Goal: 金幣與寶石只由伺服器增減，並驗證 App Store 內購。
- Files: `src/modules/wallet/{service,ledger}.ts`、`src/modules/shop/{routes,service}.ts`、`src/modules/iap/{apple,routes}.ts`、`config/catalog.json`、`test/wallet.test.ts`、`test/iap.test.ts`
- Interfaces:
  - 貨幣：`coins`（遊戲中賺）、`gems`（主要靠內購，少量任務給）；每次變動寫一筆 `wallet_ledger { userId, currency, delta, reason, refId }`，餘額＝加總（加快取欄位）
  - `GET /v1/shop` → 商品（外觀、寶石包、新手禮包、每日特價）；`POST /v1/shop/buy { itemId, requestId }`（用金幣／寶石買）
  - `POST /v1/iap/apple { signedTransaction }`：用 App Store Server API 驗證 StoreKit 2 JWS、檢查 bundleId 和環境、`originalTransactionId` 不重複後發貨；`POST /v1/iap/apple/notifications`（App Store Server Notifications v2：退款時扣回）
  - `POST /v1/iap/restore`：非消耗型商品（例如永久外觀）恢復
- Acceptance:
  - 測試：餘額不會變負數（交易內檢查＋row lock）；同一 requestId／transactionId 不會發兩次
  - 測試：沙盒收據與正式收據都能驗證，錯誤 bundleId 被拒
  - 退款通知會扣回寶石（扣到 0 為止並標記帳號）
- Manual: App Store Connect 建立商品（id 與 `catalog.json` 一致）、產生 In-App Purchase Key、設定 Server Notifications URL
- Notes: 不賣任何影響比賽結果的東西（球重、球速加成都不賣），只賣外觀，排位才公平。

### T28: 讚美對手與檢舉
- Status: todo
- Depends on: T22
- Goal: 比賽後可以讚美對手（累積在玩家名片上），也可以檢舉不當行為。
- Files: `src/modules/commend/{routes,service}.ts`、`src/modules/reports/{routes,service}.ts`、`src/admin/cli.ts`、`test/commend.test.ts`
- Interfaces:
  - `POST /v1/matches/:id/commend { toUserId, kind }`；kind：`nice_game`（好球友）、`strike_master`（全倒高手）、`great_hook`（曲球漂亮）、`good_sport`（風度好）；每場對每位對手 1 次，比賽結束 2 分鐘內
  - 名片：`GET /v1/users/:id/card` → 暱稱、頭像、等級、段位、勝率、最高分、各種讚美數、最愛的球
  - `POST /v1/reports { targetUserId, context: match|chat|nickname, refId, reason, note? }`
  - 管理 CLI：`npm run admin -- reports:list`、`ban <userId> <days>`、`rename <userId>`、`mute <userId> <hours>`
- Acceptance:
  - 測試：不能讚美自己、不能讚美沒一起比賽的人、不能重複讚美
  - 被讚美者收到 `mission.progress`（如果有相關任務）與推播通知（T39）
  - 同一人 24 小時內被 3 個不同玩家檢舉暱稱 → 暱稱自動重設為「玩家xxxx」並通知管理員
- Notes: App Store 審核需要提供聯絡方式並在 24 小時內處理檢舉內容，管理 CLI 是最低需求。

### T29: 部署、維運、強制更新
- Status: todo
- Depends on: T18
- Goal: 伺服器可以安全部署到雲端，並能要求舊版客戶端更新或進入維護模式。
- Files: `Server/Dockerfile`、`Server/fly.toml`、`Server/scripts/backup.sh`、`.github/workflows/server.yml`、`src/modules/system/{routes,service}.ts`
- Interfaces:
  - `GET /v1/config` 的 `minClientVersion`、`latestClientVersion`、`maintenance { active, message, until }`
  - CI：push 到 master 且 `Server/` 有變動 → lint、test、build image、部署
- Acceptance:
  - HTTPS／WSS（TLS 由平台處理），HTTP 自動轉 HTTPS
  - 資料庫每日備份，保留 14 天，有還原步驟文件（`Server/README.md`）
  - 部署不中斷：新版健康後才切流量；WebSocket 連線收到 `server.notice{reconnect}` 後自動重連
  - 錯誤與延遲監控（例如 Sentry 或平台內建）有告警
- Manual: 申請網域、雲端帳號、設定 secrets（JWT 金鑰、Apple Key、DB URL）
- Notes: 預設 Fly.io（東京區，台灣延遲低）＋ 託管 Postgres ＋ Upstash Redis；可換。
