# 營利（免費＋內購＋可選廣告）

原則：只賣外觀與便利，不賣影響比賽結果的東西；廣告只有玩家自己按的獎勵廣告，不放插頁廣告。

### T40: 外觀系統與背包
- Status: todo
- Depends on: T21, T11
- Goal: 球、拖尾、角色服裝、頭像框、表情包可以擁有與裝備，對手也看得到。
- Files: `Meta/CosmeticDef.cs`（ScriptableObject）、`Meta/CosmeticCatalog.cs`、`Meta/Inventory.cs`、`Gameplay/CosmeticApplier.cs`、`UI/Screens/InventoryScreen.cs`、`Server/src/modules/inventory/{routes,service}.ts`、`Resources/Cosmetics/*.asset`、`Tests/InventoryTests.cs`
- Interfaces:
  - `CosmeticDef { id, slot: Ball|Trail|Outfit|Frame|EmotePack, rarity: Common|Rare|Epic|Legendary, material/mesh/colors, previewSprite }`
  - `GET /v1/inventory`、`POST /v1/inventory/equip { slot, itemId }`；`PlayerProfile.equipped` 會帶到對戰（對手的球用對手的外觀）
  - 背包畫面：分類分頁、3D 預覽可旋轉、稀有度光框、已裝備標記
- Acceptance:
  - 首發至少：球 8、拖尾 4、服裝 4、頭像框 4、表情包 2（美術可先用程式生成的配色版本）
  - 外觀只改材質／模型，不改質量與物理（測試：換球前後 Physics Sweep 結果相同）
  - 換裝後大廳角色即時更新
- Manual: 正式外觀美術

### T41: 商城與內購
- Status: todo
- Depends on: T40, T27, T30
- Goal: 商城可用金幣／寶石買外觀，用真錢買寶石與禮包。
- Files: `UI/Screens/ShopScreen.cs`、`UI/Widgets/ShopItemCard.cs`、`UI/Screens/PurchaseConfirmPopup.cs`、`Net/ShopService.cs`、`Meta/IapManager.cs`
- Interfaces:
  - 分區：每日特價（6 格，每日換，倒數）、精選、外觀、寶石、新手禮包（只顯示 72 小時、限買一次）
  - `IapManager`：Unity IAP 5（StoreKit 2），購買成功 → 把 JWS 送 `POST /v1/iap/apple` → 伺服器確認後才 `ConfirmPendingPurchase`；啟動時處理未完成的交易；「恢復購買」按鈕
  - 價格顯示用商店回傳的在地化價格字串
- Acceptance:
  - 沙盒帳號完整走過：買寶石 → 餘額增加 → 用寶石買外觀 → 裝備
  - 購買中斷（殺 App）重開後會補發
  - 伺服器拒絕時不會 confirm，並顯示「購買處理中，請稍後」與客服資訊
  - 未成年／家長控制導致購買被拒時有正確提示
- Manual: App Store Connect 建立商品、填稅務與銀行資料、沙盒測試帳號
- Notes: 寶石包的價格級距、內容在 `catalog.json`，不寫死在程式。

### T42: 獎勵廣告與 ATT
- Status: todo
- Depends on: T41, T26
- Goal: 玩家可以自願看廣告換獎勵，並符合 iOS 追蹤授權規定。
- Files: `Meta/AdsManager.cs`、`Meta/TrackingConsent.cs`、`Server/src/modules/ads/{routes,service}.ts`（AdMob SSV 驗證）、`Server/test/ads.test.ts`
- Interfaces:
  - 觸發點：結算獎勵加倍、每日任務多換 1 次、每日免費寶箱（每天上限 5 次）
  - 獎勵由 AdMob server-side verification 回呼伺服器後發放，客戶端等 `wallet.changed`
  - ATT：第一次要看廣告時才顯示自訂說明＋系統 ATT 詢問；拒絕時用非個人化廣告
- Acceptance:
  - 沒有廣告可播時按鈕變灰並顯示「目前沒有廣告」
  - 測試：SSV 簽章錯誤被拒；同一 transaction 不重複發
  - 廣告播放時遊戲音樂暫停、結束後恢復
- Manual: AdMob 帳號、建立 App 與獎勵廣告單元、`Info.plist` 的 `NSUserTrackingUsageDescription` 與 `SKAdNetworkItems`
- Notes: 若要先簡化，可以首發不放廣告，只做內購；此任務可延後。
