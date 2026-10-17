# 客戶端線上功能

### T30: 客戶端網路層
- Status: todo
- Depends on: T01, T18
- Goal: 建立 REST、WebSocket、主執行緒分派、token 自動更新，所有線上功能共用。
- Files: `Net/Bowling.Net.asmdef`、`Net/ApiClient.cs`、`Net/RealtimeClient.cs`、`Net/ITransport.cs`、`Net/TokenStore.cs`、`Net/MainThread.cs`、`Net/Protocol/*.cs`、`Net/Services.cs`、`Net/NetConfig.cs`、`Tests/RealtimeClientTests.cs`、`Tests/ApiClientTests.cs`
- Interfaces:
  - `ApiClient.Get<T>(path)`、`Post<T>(path, body)`、`Delete(path)` → `Task<ApiResult<T>>`；401 時自動 refresh 一次再重試
  - `RealtimeClient.Connect()`、`Request<T>(type, data) → Task<T>`（等 ack，逾時 10 秒）、`On<T>(type, Action<T>)`、`State`（Disconnected / Connecting / Connected / Reconnecting）、自動重連（1、2、4、8…最多 30 秒）、帶 resume
  - `TokenStore`：refresh token 存 iOS Keychain（不存 PlayerPrefs）
  - `Services.Api`、`Services.Realtime`、`Services.Auth`…（啟動時註冊）
  - `NetConfig`（ScriptableObject）：Dev／Staging／Prod 伺服器網址
- Acceptance:
  - 測試用假 `ITransport`：斷線重連、ack 逾時、401 → refresh → 重試成功
  - 所有回呼都在主執行緒執行（測試）
  - App 切背景 > 30 秒斷線，回前景自動重連
- Notes: WebSocket 用 .NET `ClientWebSocket`（iOS IL2CPP 可用），JSON 用 Newtonsoft；加 `link.xml` 防止 DTO 被剝除。

### T31: 登入流程與帳號畫面
- Status: todo
- Depends on: T30, T19, T14
- Goal: 啟動時自動以訪客登入，可在設定中綁定 Apple 帳號、登出、刪除帳號。
- Files: `Net/AuthService.cs`、`Net/AppleSignIn.cs`、`UI/Screens/AccountScreen.cs`、`UI/Screens/AccountConflictPopup.cs`
- Interfaces: `AuthService.EnsureLoggedIn() → Task<bool>`、`LinkApple()`、`SignInApple()`、`Logout()`、`DeleteAccount()`、`event Action<PlayerProfile> ProfileChanged`
- Acceptance:
  - 第一次啟動不需任何操作就有帳號；沒網路時可玩單機，連上後自動登入
  - 帳號頁：目前狀態（訪客／Apple）、綁定 Apple、切換帳號、登出、刪除帳號（兩次確認，說明會刪掉什麼）
  - 綁定衝突時顯示兩個帳號的等級／段位讓玩家選
  - 訪客帳號在大廳顯示一次性提示「綁定帳號以免遺失進度」
- Manual: Xcode 加 Sign in with Apple capability；安裝 Sign in with Apple Unity 外掛（例如 lupidan/apple-signin-unity）
- Notes: Apple 審核規定有帳號就必須能在 App 內刪除帳號。

### T32: 線上對戰客戶端
- Status: todo
- Depends on: T30, T22, T02, T12
- Goal: 在現有遊戲流程上接上線上對戰：輪到自己就投，輪到對手就看對手那球的重播。
- Files: `Gameplay/OnlineMatchDriver.cs`（實作 `IMatchDriver`）、`Net/MatchService.cs`、`UI/Screens/MatchIntroScreen.cs`、`UI/Widgets/ConnectionBanner.cs`
- Interfaces:
  - `MatchService.Join(matchId)`、`SendRoll(RollReport)`、`event Action<MatchState> StateChanged`、`event Action<RollReport, ReplayData> OpponentRoll`、`event Action<MatchEnd> Ended`
  - 開場：VS 畫面（雙方頭像、段位、勝率）3 秒倒數
  - 對手回合：鏡頭切到球道，用 `PlayReplay` 播對手的球；球瓶最後狀態以伺服器的 knockedMask 為準
- Acceptance:
  - 兩台裝置（或 Editor ＋ 模擬器）能打完一場，雙方記分板一致
  - 斷線時顯示「重新連線中…」，60 秒內回來能接續（含輪到自己的回合）
  - 自己回合倒數結束沒出手：顯示逾時，記 0 分
  - 對手離開：彈出「對手已離開，你獲勝」
  - 線上對戰中不能暫停（暫停鍵改成「投降」，需確認）
- Notes: 每局清瓶重排都由本機做，重播開始前先擺成對手那球的 standingBefore。

### T33: 配對與排位畫面、排行榜
- Status: todo
- Depends on: T32, T23, T11
- Goal: 從大廳進入排位配對，看自己的段位與排行榜。
- Files: `UI/Screens/MatchmakingScreen.cs`、`UI/Screens/RankScreen.cs`、`UI/Screens/LeaderboardScreen.cs`、`UI/Widgets/TierBadge.cs`、`Net/MatchmakingService.cs`、`Net/LeaderboardService.cs`
- Interfaces:
  - 配對畫面：雷達掃描動畫、已等待秒數、取消；45 秒逾時跳出「和 AI 練習／繼續等」
  - 段位頁：目前段位徽章、分數條、本季最高、賽季剩餘天數、段位獎勵預覽
  - 排行榜：全球／好友分頁，前 100 名，自己的名次固定在底部；點玩家開名片（T35）
- Acceptance:
  - 找到對手時有「配對成功」演出並震動
  - 段位徽章 6 種＋小段都有圖（`TierBadge.Set(tier, division)`）
  - 排行榜捲動 100 筆維持 60 fps（物件重複利用）
- Manual: 段位徽章美術

### T34: 對戰中表情與快捷語
- Status: todo
- Depends on: T32
- Goal: 對戰中可以傳表情和預設短句給對手，增加互動但不讓人被騷擾。
- Files: `UI/Widgets/EmoteWheel.cs`、`UI/Widgets/EmoteBubble.cs`、`Meta/EmoteCatalog.cs`、`Resources/Config/emotes.json`
- Interfaces:
  - 左下角表情按鈕 → 展開 8 格輪盤：4 個表情（讚、哈哈、驚訝、哭哭）＋4 句快捷語（「好球！」「可惜！」「加油！」「好險」）；擁有的表情包可在背包換（T40）
  - 對手的表情從對方 PlayerCard 冒泡泡＋音效，2.5 秒消失
  - 每 3 秒最多 1 次；可點對手頭像「靜音對手表情」
- Acceptance:
  - 伺服器只轉發 emoteId（不轉發文字），客戶端依語言顯示快捷語
  - 靜音後收到表情不顯示也不出聲，整場有效
  - 不會擋住投球手勢的區域（輪盤展開時暫停握球判定）

### T35: 讚美對手與玩家名片
- Status: todo
- Depends on: T13, T28, T32
- Goal: 結算時可以讚美對手，讚美會顯示在玩家名片上。
- Files: `UI/Widgets/CommendPanel.cs`、`UI/Screens/PlayerCardPopup.cs`、`Net/CommendService.cs`
- Interfaces:
  - 結算畫面對手頭像下方 4 個讚美按鈕（好球友、全倒高手、曲球漂亮、風度好），按一個後其他變灰，送出時愛心飛向對手
  - 收到讚美時結算畫面跳出「XXX 稱讚你：曲球漂亮！」
  - 名片：頭像、暱稱、等級、段位、勝率、最高分、讚美數（四種各自計數），按鈕：加好友、邀請對戰、封鎖、檢舉
- Acceptance:
  - 2 分鐘內沒按就關閉讚美面板
  - 名片從排行榜、好友清單、最近對手、對戰中頭像都能打開
  - 檢舉有原因選單（暱稱不當、騷擾、作弊、其他）

### T36: 好友畫面
- Status: todo
- Depends on: T30, T24, T11
- Goal: 社交分頁裡管理好友、加好友、邀請對戰。
- Files: `UI/Screens/SocialScreen.cs`（分頁：好友、聊天、請求）、`UI/Widgets/FriendRow.cs`、`UI/Screens/AddFriendPopup.cs`、`UI/Screens/InvitePopup.cs`、`Net/FriendService.cs`
- Interfaces:
  - 好友清單：在線排前面、綠點／對戰中橘點／離線灰；每列按鈕：邀請對戰、聊天
  - 加好友：顯示自己的好友碼（可複製、分享）、輸入好友碼、搜尋暱稱、最近對手
  - 請求：收到的（接受／拒絕）、送出的（取消）
  - 收到邀請：在任何畫面（對戰中除外）頂部滑下通知，60 秒倒數，接受直接進房
- Acceptance:
  - 好友上下線即時更新，不需重新整理
  - 底部「社交」分頁紅點＝未讀訊息＋待處理請求
  - 沒有好友時顯示引導（分享好友碼）

### T37: 聊天畫面
- Status: todo
- Depends on: T36, T25
- Goal: 和好友一對一聊天。
- Files: `UI/Screens/ChatListScreen.cs`、`UI/Screens/ChatScreen.cs`、`UI/Widgets/ChatBubble.cs`、`Net/ChatService.cs`
- Interfaces: 對話清單（最後一則、時間、未讀數）；聊天室：泡泡（自己右側青色、對方左側紫色）、時間分隔線、往上捲載入更早訊息、輸入框跟著鍵盤上移、送出中／失敗重送；右上角選單：查看名片、封鎖、檢舉
- Acceptance:
  - iOS 中文輸入法（注音、拼音）組字中不會被送出或截斷
  - 訊息依 `clientMsgId` 去重，重送不會出現兩則
  - 超過頻率限制時輸入框顯示冷卻秒數
  - 被過濾的字顯示為 ＊
- Notes: TMP_InputField 在 iOS 用原生鍵盤；要測試 emoji 與注音。

### T38: 每日任務畫面
- Status: todo
- Depends on: T30, T26, T11
- Goal: 顯示今天的任務、進度、簽到，並有領獎演出。
- Files: `UI/Screens/MissionsPopup.cs`、`UI/Widgets/MissionRow.cs`、`UI/Screens/CheckInPopup.cs`、`Net/MissionService.cs`
- Interfaces: 任務列：圖示、說明、進度條、獎勵、按鈕（前往／領取／已領）；頂部重置倒數；3 個全完成的寶箱；換任務按鈕（免費／看廣告）；簽到：7 格，今天的格子發光，領取時翻牌
- Acceptance:
  - 每天第一次進大廳自動跳簽到（領過就不跳）
  - 比賽結束後任務有進度時，結算畫面出現小提示「任務進度 2/3」
  - 「前往」帶玩家到對應模式
  - 可領取時大廳入口有紅點

### T39: 推播通知
- Status: todo
- Depends on: T31, T24
- Goal: 好友請求、對戰邀請、被讚美、簽到提醒時發推播。
- Files: `Net/PushService.cs`、`Server/src/modules/push/{apns,service}.ts`、`Server/test/push.test.ts`
- Interfaces: `POST /v1/push/token { platform: "ios", token }`；伺服器用 APNs token-based auth 發送；類型：`friend_request`、`invite`（玩家離線或在背景時）、`commend`、`checkin_reminder`（每天 20:00，當天還沒簽到）；設定頁可分別關閉
- Acceptance:
  - 第一次完成對戰後才請求通知權限（先顯示自訂說明頁，不在啟動時要）
  - 點推播打開 App 並跳到對應畫面（邀請 → 進房，請求 → 社交頁）
  - 失效的 device token 會從資料庫移除
- Manual: Xcode 加 Push Notifications capability；Apple Developer 產生 APNs Key
