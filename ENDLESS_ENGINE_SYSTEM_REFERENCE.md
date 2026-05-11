# Endless Engine — Tam Sistem Referans Dokümanı

> Versiyon 1.3.4 | Unity 6.3 LTS | UPM Paketi: `com.endlessengine.idle`

---

## İÇİNDEKİLER

1. [Genel Bakış](#1-genel-bakış)
2. [Editör Araçları](#2-editör-araçları)
3. [Çekirdek Sistemler](#3-çekirdek-sistemler)
4. [Aktif Döngü Sistemleri](#4-aktif-döngü-sistemleri)
5. [Ekonomi ve Para Birimleri](#5-ekonomi-ve-para-birimleri)
6. [İlerleme Sistemleri](#6-ilerleme-sistemleri)
7. [Dalgalar ve Savaş](#7-dalgalar-ve-savaş)
8. [Kayıt / Yükleme Sistemi](#8-kayıt--yükleme-sistemi)
9. [Yapılandırma (Config) Sistemi](#9-yapılandırma-config-sistemi)
10. [Destek Sistemleri](#10-destek-sistemleri)
11. [Platform Entegrasyonu (Steam)](#11-platform-entegrasyonu-steam)
12. [Bootstrap ve Sahne Kurulumu](#12-bootstrap-ve-sahne-kurulumu)
13. [Test Altyapısı](#13-test-altyapısı)

---

## 1. GENEL BAKIŞ

Endless Engine, Unity 6.3 LTS için yazılmış tam özellikli bir idle/incremental oyun motoru paketidir. 312 C# dosyası, 45+ namespace, ~43.000 satır kod içerir.

### Mimarinin Temelleri

| İlke | Uygulama |
|------|----------|
| Sıfır konfigürasyon | `AutoSetupBootstrap` — tek component, otomatik servis keşfi |
| Çekme tabanlı kayıt | `ISaveStateProvider` — her sistem kendi verisini yazar/okur |
| Statik event bus | `EconomyService.OnResourcesChanged`, `WaveSpawnManager.OnWaveStarted` vb. |
| Config ayrımı | ScriptableObject tabanlı — kod değişikliği gerekmez |
| Editör öncelikli | Tüm içerik editör araçlarıyla oluşturulur, kod yazmak gerekmez |

### Klasör Yapısı

```
Packages/com.endlessengine.idle/
├── Editor/          — 14 editör penceresi ve araç
├── Runtime/         — 278 runtime script (45+ alt klasör)
├── Tests/           — 12 test dosyası
└── Samples~/        — 7 örnek proje
```

---

## 2. EDITÖR ARAÇLARI

Tüm araçlar `Tools → Endless Engine` menüsünden açılır.

---

### 2.1 New Game Wizard
**Dosya:** `Editor/NewGameWizard.cs`
**Menü:** `Tools → Endless Engine → New Game Wizard`

12 oyun tipinden birini seçin, bir isim girin, "Generate" tuşuna basın. Araç:
- Config klasörü ve tüm ScriptableObject asset'lerini oluşturur
- Sahneyi kurar (Bootstrap, HUD, kamera, world objeleri)
- Bootstrap component'larını otomatik bağlar
- Sahneyi Build Settings'e ekler

**Desteklenen Oyun Tipleri:**

| Tip | Açıklama | Öne Çıkan Sistemler |
|-----|----------|----------------------|
| Pure Idle | Klasik pasif gelir + prestige | Generator, Economy, Prestige |
| Clicker Idle | Aktif tıklama + pasif gelir | ClickTargetHandler, Generator |
| Click Loop | HP'li hedefler, combo, crit, auto-click | ClickLoopService, ClickTarget |
| Harvest Idle | Cursor sürükleyerek kaynak toplama | HarvestLoopService, HarvestCursor |
| Idle-vs / RPG | Otomatik savaş dalgaları | WaveSpawnManager, AutoBattle |
| Tower Defense | Düşman patikası + kule slotları | WaveSpawnManager, BuildingService |
| Merge Idle | Eşleşen itemları birleştir | MergeService, InventoryService |
| Farm Idle | Tarla ekip biçme | BuildingService, GeneratorSystem |
| Research Idle | Araştırma kuyruğu ile kilit açma | ResearchService, TickEngine |
| Building Idle | Şehir ızgarası + gelir bölgeleri | BuildingService, ZoneSystem |
| Prestige-Heavy | Çok katmanlı prestige + skill tree | PrestigeStateManager, AscensionStateManager |
| Custom | Tüm toggle'lar kapalı, kendin ayarla | — |

---

### 2.2 Generator Editor
**Dosya:** `Editor/GeneratorEditorWindow.cs`
**Menü:** `Tools → Endless Engine → Generator Editor`

Görsel generator veritabanı editörü. Sol panel: generator listesi (sıra değiştirme, silme). Sağ panel: inspector.

**Inspector Alanları:**
- Generator ID, Display Name, Description
- Yield/Second (saniyede üretilen altın)
- Base Cost, Cost Scale Factor (her kopyada maliyet çarpanı)
- Max Count (maksimum kaç tane alınabilir, -1 = sınırsız)
- Unlock Requirement, Prerequisite Generator
- **Cost Curve grafiği** — ilk 10 kopyasının maliyetini bar chart olarak gösterir

**Temel API (GeneratorConfigSO):**
```csharp
gen.GeneratorId        // "gold_mine"
gen.DisplayName        // "Gold Mine"
gen.BaseYieldPerSecond // 1.0f
gen.BaseCost           // 50
gen.CostScalingFactor  // 1.15f  (her kopyada ×1.15)
gen.MaxCount           // -1 (sınırsız) veya pozitif int
gen.CostForCopy(n)     // n. kopyanın maliyetini hesaplar
```

---

### 2.3 Upgrade Tree Editor
**Dosya:** `Editor/UpgradeTreeEditorWindow.cs`
**Menü:** `Tools → Endless Engine → Upgrade Tree Editor`

Drag-and-drop node graph editörü. Zoom, pan, çoklu seçim destekler.

**Node Inspector Alanları:**
- Node ID, Display Name, Description
- Category: Production / Combat / Survival / Economy / Prestige (renk kodlu)
- Affected Stat (StatType enum — 30+ stat)
- Effect Type: PercentBonus / FlatBonus
- Effect Per Rank, Max Rank
- Base Cost, Cost Scale Factor
- Selection Weight (upgrade kartı olarak seçilme ağırlığı)
- Prestige Gate (hangi prestige sayısında kilidini açar)
- Max Outgoing Edges (kaç alt node bağlanabilir)
- Icon Picker (48 Font Awesome ikonu)
- Prerequisites (bağlantı çizerek kurulur)

**Temel API (UpgradeNodeDefinition):**
```csharp
node.NodeId                // "dmg_01"
node.AffectedStat          // StatType.Damage
node.EffectPerRank         // 0.10f (+10% per rank)
node.MaxRank               // 5
node.BaseCost              // 100f
node.CostScalingFactor     // 1.5f
node.PrerequisiteNodeIDs   // string[] {"dmg_00"}
```

---

### 2.4 Economy Simulator
**Dosya:** `Editor/EconomySimulatorWindow.cs`
**Menü:** `Tools → Endless Engine → Economy Simulator`

Matematiksel ilerleme simülatörü — sahne açmadan çalışır.

**Girdi Parametreleri:**
- Economy Config, Prestige Config, Generator DB, Wave Config, Run Config (asset referansları)
- Sessions to simulate (1-100)
- Session length (dakika)
- Offline between sessions (saat)
- Gen copies purchased/session
- Auto-prestige when eligible (toggle)

**Çıktı (her oturum için):**
- Session numarası
- Prestige sayısı
- O oturumda kazanılan altın
- Toplam yaşam boyu altın
- Kalıcı çarpan (×N)
- O oturumda prestige oldu mu

**Hesaplama kaynakları:** idle gelir + wave düşüşleri + offline gelir + generator geliri + prestige çarpanı büyümesi.

---

### 2.5 Economy Tuning Window
**Dosya:** `Editor/EconomyTuningWindow.cs`
**Menü:** `Tools → Endless Engine → Economy Tuning`

Gerçek zamanlı ekonomi parametre ayarlama. EconomyConfigSO'daki değerleri slider ile değiştirip anında simülasyon sonuçlarını görebilirsiniz.

---

### 2.6 Skill Tree Editor
**Dosya:** `Editor/SkillTreeEditorWindow.cs`
**Menü:** `Tools → Endless Engine → Skill Tree Editor`

UpgradeTreeEditorWindow ile benzer arayüz, SkillTreeConfigSO için kullanılır.

---

### 2.7 Config Validator
**Dosya:** `Editor/ConfigValidatorWindow.cs`
**Menü:** `Tools → Endless Engine → Config Validator`

Tüm config asset'lerini tarar, hataları listeler:
- Eksik referanslar (null GeneratorDatabaseSO.Generators elemanı vb.)
- Duplicate ID'ler
- Mantıksız değerler (negatif maliyet, sıfır HP vb.)

---

### 2.8 Schema Bump Utility
**Dosya:** `Editor/SchemaBumpUtility.cs`
**Menü:** `Tools → Endless Engine → Bump Schema Version`

SchemaVersionSO'daki versiyon numarasını artırır ve boş migration dosyası iskeletini oluşturur (`SaveMigration_VN_VN+1.cs`).

---

### 2.9 ID Registry Window
**Dosya:** `Editor/IdRegistryWindow.cs`
**Menü:** `Tools → Endless Engine → ID Registry`

Projedeki tüm benzersiz ID'leri (GeneratorId, NodeId, BuildingId vb.) listeler ve çakışmaları gösterir.

---

### 2.10 Generator Asset Creator
**Dosya:** `Editor/GeneratorAssetCreator.cs`

Hızlı generator asset oluşturma yardımcısı. Generator Editor'daki "Add" butonu bunu kullanır.

---

### 2.11 Upgrade Tree Asset Creator
**Dosya:** `Editor/UpgradeTreeAssetCreator.cs`

Hızlı upgrade tree asset oluşturma yardımcısı.

---

### 2.12 Content Pack Wizard
**Dosya:** `Editor/ContentPackWizard.cs`

Realm/content pack oluşturma sihirbazı. Birden fazla oyun varyantı için kullanılır.

---

## 3. ÇEKIRDEK SİSTEMLER

---

### 3.1 AutoSetupBootstrap
**Dosya:** `Runtime/Bootstrap/AutoSetupBootstrap.cs`
**Execution Order:** -500 (en önce çalışır)

Tüm core servisleri otomatik oluşturur ve başlatır.

**Inspector Alanları:**
- `Economy Config` — EconomyConfigSO
- `Generator Database` — GeneratorDatabaseSO
- `Schema Version` — SchemaVersionSO
- `Prestige Config` — PrestigeConfigSO (opsiyonel)
- `Realm Config` — RealmIdentityConfigSO (opsiyonel)
- `Enable Save` — otomatik kayıt (varsayılan: açık)

**Başlatma Sırası:**
1. EconomyService, GeneratorSystem, UpgradeTreeService, TickEngine, SaveService oluşturulur
2. `ConfigRegistry.InjectForTesting()` çağrılır (Editor/Dev)
3. `UpgradeTree.HandleConfigsLoaded()` çağrılır
4. Economy ve Generator başlatılır
5. `SaveService.LoadAsync()` beklenir
6. `IsReady = true`

**Erişilebilir Servisler (`IsReady` sonrası):**
```csharp
bootstrap.Economy     // EconomyService
bootstrap.Generators  // GeneratorSystem
bootstrap.UpgradeTree // UpgradeTreeService
bootstrap.Save        // SaveService
bootstrap.Tick        // TickEngine
bootstrap.IsReady     // bool
```

---

### 3.2 ConfigRegistry
**Dosya:** `Runtime/Config/ConfigRegistry.cs`

Tüm config asset'lerine merkezi erişim noktası.

```csharp
ConfigRegistry.Economy   // EconomyConfigSO
ConfigRegistry.Prestige  // PrestigeConfigSO
ConfigRegistry.Wave      // WaveConfigSO
ConfigRegistry.Enemy     // EnemyStatConfigSO
ConfigRegistry.Schema    // SchemaVersionSO
ConfigRegistry.GetArray() // UpgradeNodeConfigSO[]

// Editor/Dev: asset bağlamadan test için
ConfigRegistry.InjectForTesting(economy, schema, prestige, wave, enemy, ...);
```

---

### 3.3 TickEngine
**Dosya:** `Runtime/Flow/TickEngine.cs`

Pasif gelir ve araştırma için oyun saati. Varsayılan: saniyede 1 tick.

```csharp
TickEngine.OnTick += myMethod;  // float dt parametresi
TickEngine.OnTick -= myMethod;  // temizlik için OnDestroy'da

// Pause/Resume
tickEngine.Pause();
tickEngine.Resume();
tickEngine.IsPaused               // bool

// Hız çarpanı — PUBLIC FIELD (metod değil):
tickEngine.TimeScale = 2f;        // 2x hız — doğrudan field'a yaz
tickEngine.TickIntervalSeconds    // float — tick aralığı (varsayılan 1.0)
tickEngine.TotalEffectiveTime     // float — toplam geçen efektif süre
```

---

### 3.4 GameFlowStateMachine
**Dosya:** `Runtime/Flow/GameFlowStateMachine.cs`

Oyun durumları arası geçişleri yönetir.

**Durumlar:** `Menu → Run → PostRun`

```csharp
// Durum okuma
flow.CurrentState    // GameFlowState (Menu / InRun / PostRun)
flow.IsInMenu        // bool
flow.IsInRun         // bool
flow.IsPostRun       // bool

// Geçişler (TransitionTo() yok — her geçişin kendi metodu var):
flow.StartRun()      // Menu → InRun
flow.EndRun()        // InRun → PostRun
flow.ReturnToMenu()  // PostRun → Menu

// Statik eventler
GameFlowStateMachine.OnStateChanged  += (from, to) => { };  // Action<GameFlowState, GameFlowState>
GameFlowStateMachine.OnEnteredMenu   += () => { };
GameFlowStateMachine.OnEnteredRun    += () => { };
GameFlowStateMachine.OnEnteredPostRun += () => { };
```

---

## 4. AKTİF DÖNGÜ SİSTEMLERİ

---

### 4.1 Click Loop (ClickLoopService)
**Dosya:** `Runtime/ClickLoop/ClickLoopService.cs`
**Bootstrap:** `ClickLoopBootstrap`

HP'li hedeflere tıklama, combo ve crit sistemi.

> **Tasarım notu:** `ClickLoopBootstrap` inspector'ında bireysel `ClickTargetConfigSO` array'i yoktur. Click target'lar sahneye ayrı GameObject olarak yerleştirilir (SceneSetupUtility bunu otomatik yapar); her target'ın config'i kendi component'ında tutulur. Bu, `HarvestLoopBootstrap._nodeConfigs[]`'dan farklı bir yaklaşımdır.

**Pipeline (her tıklamada):**
1. Input algıla → `IInputProvider.GetPointerClickedThisFrame()`
2. `Physics2D.OverlapPoint` ile ClickTarget bul
3. Hasar hesapla (base × ClickDamage stat upgrade)
4. Crit roll (chance + CritMultiplier stat upgrade)
5. Combo güncelle
6. Altın ver → `EconomyService.AddResources()`
7. Event'leri fırlat

**Temel Eventler:**
```csharp
clickService.OnYieldAwarded  += amount => { };  // altın kazanıldı
clickService.OnTargetDestroyed += target => { }; // hedef yok edildi
clickService.OnComboChanged  += combo => { };   // combo değişti
clickService.OnCrit          += mult => { };    // crit oldu
```

**Config (ClickLoopConfigSO):**
```csharp
config.BaseAutoClickRate        // saniyede otomatik tıklama (0 = kapalı)
config.BaseCritChance           // 0.0-1.0
config.BaseCritMultiplier       // örn. 2.0f
config.ComboDecayDelay          // tıklama durduğunda combo azalmaya başlamadan önce bekleme süresi (saniye)
config.ComboDecayRate           // saniyede azalan combo puanı
config.ComboPointsPerStep       // her tıkta kazanılan combo puanı
config.MaxComboMultiplier       // combo tavanı çarpanı
config.OfflineCapHours          // çevrimdışı auto-click kazancı tavanı
config.OfflineEfficiency        // çevrimdışı verimlilik (0-1)
```

**ClickTarget (ClickTargetConfigSO):**
```csharp
target.TargetId             // "target_0"
target.MaxHP                // 10f
target.DamagePerClick       // 1f
target.BaseYield            // 3f
target.RespawnSeconds       // 3f
target.AwardYieldPerClick   // true = her tıkta, false = yok edilince
target.ComboContribution    // combo'ya katkısı
```

---

### 4.2 Harvest Loop (HarvestLoopService)
**Dosya:** `Runtime/Harvest/HarvestLoopService.cs`
**Bootstrap:** `HarvestLoopBootstrap`

Mouse/touch cursor'ı düşman node'larının üzerinden geçirilince hasar ve altın verilir.

**Pipeline (her tick'te):**
1. `HarvestCursor.OverlappingNodes` oku
2. Her canlı node için hasar uygula
3. Combo biriktir
4. Altın ver
5. Depleted node'u respawn timer'a al

**Temel Eventler:**
```csharp
harvestService.OnYieldAwarded += amount => { };
harvestService.OnComboChanged += combo => { };
harvestService.OnNodeDepleted += node => { };
```

**Config (HarvestAreaConfigSO):**
```csharp
area.BaseTickInterval           // saniyede kaç tick (0.1 = 10/s)
area.ComboDecayDelay            // tıklama durduğunda combo azalmadan önce bekleme (saniye)
area.ComboDecayRate             // saniyede azalan combo
area.ComboPointsPerMultiplierStep // bir sonraki çarpan seviyesi için gereken puan
area.MaxComboMultiplier         // combo tavanı çarpanı
area.OfflineCapHours            // çevrimdışı kazanç tavanı
area.OfflineEfficiency          // çevrimdışı verimlilik
```

**Node Config (HarvestNodeConfigSO):**
```csharp
node.NodeId
node.MaxHP              // 10f
node.DamagePerTick      // 1f
node.BaseYield          // 5f
node.RespawnSeconds     // 4f
node.AwardYieldPerTick  // true
```

---

## 5. EKONOMİ VE PARA BİRİMLERİ

---

### 5.1 EconomyService
**Dosya:** `Runtime/Economy/EconomyService.cs`

Birincil altın otoritesi. Tüm kaynak değişiklikleri buradan geçer.

```csharp
economy.CurrentResources          // double (mevcut altın)
economy.AddResources(long amount)
economy.DeductResources(long amount)
economy.TryPurchase(long cost)    // bool — yeterli altın varsa düşer

// Statik event
EconomyService.OnResourcesChanged += (current, delta) => { };
```

**Config (EconomyConfigSO):**
```csharp
econ.IdleYieldRateBase          // pasif gelir hızı (upgrade multiplier olmadan)
econ.ResourceHardCap            // maksimum altın (10_000_000_000 vb.)
econ.OfflineCapHours            // offline gelirin sınırı (saat)
econ.BaseGoldDropPerEnemy       // düşman başına altın (wave oyunları)
econ.GoldDropScalingExponent    // wave'e göre altın büyümesi
econ.StartingGold               // yeni oyunda başlangıç altını
econ.NumberBackend              // Double veya BigDouble (aşırı büyük sayılar için)
```

---

### 5.2 GeneratorSystem
**Dosya:** `Runtime/Generator/GeneratorSystem.cs`

Generator satın alma ve sayım sistemi. Altın ÜRETMİYOR — o `PassiveIncomeService`'in işi.

```csharp
generators.TryPurchase(string generatorId) // bool
generators.GetCount(string generatorId)    // int
generators.GetNextCost(string generatorId)    // long (backend-bağımsız)
generators.GetNextCostBig(string generatorId) // IBigNumber (BigDouble backend için)
generators.TotalGeneratorsOwned()          // int
generators.Configs                         // List<GeneratorConfigSO>

// Event
GeneratorSystem.OnGeneratorPurchased += generatorId => { };
```

**Toplu Alım:**
```csharp
BulkPurchase.GetAffordableCount(economy, gen, n); // n tane alma maliyeti
```

---

### 5.3 PassiveIncomeService
**Dosya:** `Runtime/Modules/PassiveIncomeService.cs`

Her tick'te generator gelirini hesaplar ve economy'ye ekler. AutoSetupBootstrap tarafından otomatik başlatılır.

---

### 5.4 CurrencyService (İkincil Para Birimleri)
**Dosya:** `Runtime/Economy/CurrencyService.cs`

Gem, token, prestige coin gibi ikincil para birimlerini yönetir.

```csharp
currencyService.GetBalance(string currencyId) // long
currencyService.Add(string currencyId, long amount)
currencyService.TrySpend(string currencyId, long amount) // bool

// Config (CurrencyConfigSO)
currency.CurrencyId           // "gems"
currency.DisplayName          // "Gems"
currency.Symbol               // "💎" (kısa gösterim)
currency.StartingAmount       // 0
currency.HardCap              // 0 = sınırsız, >0 = tavan
currency.ResetsOnPrestige     // bool (ResetOnPrestige DEĞİL)
currency.UnlockAtPrestigeCount // int (0 = her zaman görünür)
```

---

### 5.5 ConversionService
**Dosya:** `Runtime/Economy/ConversionService.cs`

Kaynak dönüştürme tarifleri (örn. 100 altın → 1 gem).

```csharp
conversionService.TryExecute(string recipeId) // bool
conversionService.CanExecute(string recipeId) // bool
conversionService.GetCooldownRemaining(string recipeId) // float

// Config (ConversionRecipeSO)
recipe.RecipeId
recipe.InputCurrencyId   // "gold"
recipe.InputAmount       // 100
recipe.OutputCurrencyId  // "gems"
recipe.OutputAmount      // 1
recipe.CooldownSeconds   // 0 = cooldown yok
```

---

## 6. İLERLEME SİSTEMLERİ

---

### 6.1 UpgradeTreeService
**Dosya:** `Runtime/Upgrade/UpgradeTreeService.cs`

Upgrade tree yönetimi, satın alma, stat hesaplama.

```csharp
upgradeTree.GetNode(string nodeId)            // UpgradeNode (null = bulunamadı)
upgradeTree.GetNodeCost(string nodeId)        // long — sonraki rank maliyeti
upgradeTree.IsNodeAvailable(string nodeId)    // bool — prerequisite + prestige gate + max rank kontrolü
upgradeTree.GetAvailableNodes()               // List<UpgradeNode> — satın alınabilir tüm node'lar
upgradeTree.IsReady                           // bool — config + save yüklendi mi
upgradeTree.RebuildForPrestige()              // prestige sonrası tüm rank'ları sıfırlar

// UpgradeNode (runtime wrap):
node.Config          // UpgradeNodeDefinition
node.CurrentRank     // int

// Satın alma EconomyService.TryPurchase(nodeId) üzerinden yapılır:
economyService.TryPurchase(string nodeId)     // void — altın yeterliyse satın alır, event fırlatır
```

**UpgradeApplicationSystem (statik):**
```csharp
// Tüm aktif upgrade'lerin bir stat üzerindeki net etkisi
UpgradeApplicationSystem.GetEffectiveStat(StatType.Damage)   // float
UpgradeApplicationSystem.GetEffectiveStat(StatType.IdleYieldRate)

// Prestige sonrası kalıcı çarpan
UpgradeApplicationSystem.SetPermanentMultiplier(float mult)
```

**StatType Enum (seçili örnekler):**
```
Damage, Health, Armor, CritChance, CritMultiplier,
ClickDamage, ClickAutoRate, ClickCritChance, ClickCritMultiplier,
HarvestTickRate, IdleYieldRate, PrestigeMultiplier,
GeneratorYield, ResearchSpeed, BuildingIncome
```

---

### 6.2 PrestigeStateManager
**Dosya:** `Runtime/Prestige/PrestigeStateManager.cs`
**Bootstrap:** `PrestigeBootstrap`

Prestige yaşam döngüsü yönetimi. Çift kayıt kaza güvenliği (crash-safety).

**Prestige Sırası:**
1. `PrestigeInProgress=true` → Kayıt 1 (kaza tespiti için)
2. `OnPrestigeStarted` → Economy, UAS, UpgradeTree, Wave, Health sıfırlanır
3. `PrestigeCount++`, `PrestigeInProgress=false`
4. Kayıt 2 (tamamlandı)
5. `OnPrestigeComplete(count, multiplier)`

```csharp
prestige.CanPrestige         // bool (gate kontrolü)
prestige.PrestigeCount       // int
prestige.TryPrestige()       // bool — başlatır
prestige.GetPermanentMultiplier() // float

// Statik eventler
PrestigeStateManager.OnPrestigeStarted  += () => { };
PrestigeStateManager.OnPrestigeComplete += (count, mult) => { };
PrestigeStateManager.OnRealmUnlocked    += realmId => { };
```

**Config (PrestigeConfigSO):**
```csharp
prestige.BaseMultiplierPerPrestige  // 1.5f (her prestige'de ×1.5)
prestige.MaxPermanentMultiplier     // 1000f (tavan)
prestige.MinWaveForPrestige         // 10 (wave olmayan oyunlarda 0)
prestige.MinGoldToPrestige          // 1000 (altın kapısı, 0 = kapalı)
prestige.MaxPrestigeCount           // 0 = sınırsız
```

---

### 6.3 AscensionStateManager (Çok Katmanlı Prestige)
**Dosya:** `Runtime/Prestige/AscensionStateManager.cs`

Prestige-Heavy oyunlar için birden fazla prestige katmanı yönetir.

```csharp
// Katman tetikleme:
ascension.CanTrigger(int layerIndex, int currentWaveNumber) // bool
ascension.TryTrigger(int layerIndex, int currentWaveNumber) // bool — hemen döner, async reset fire-and-forget

// Sayım ve çarpan sorguları:
ascension.GetCount(int layerIndex)    // int — bu katman kaç kez tetiklendi
ascension.GetLayer0Count()            // int — layer 0 = PrestigeStateManager.PrestigeCount
ascension.GetCascadeMultiplier()      // float — tüm katmanların birleşik çarpanı

// Statik eventler
AscensionStateManager.OnAscensionStarted  += layerIndex => { };
AscensionStateManager.OnAscensionComplete += (layerIndex, newCount, cascadeMult) => { };
AscensionStateManager.OnAscensionResetRequested += () => { };
```

---

### 6.4 ResearchService
**Dosya:** `Runtime/Research/ResearchService.cs`
**Bootstrap:** `ResearchBootstrap`

FIFO araştırma kuyruğu. Her TickEngine tick'inde bir adım ilerler.

```csharp
research.TryEnqueue(string treeId, string nodeId) // bool
research.IsCompleted(string treeId, string nodeId) // bool
research.IsQueued(string treeId, string nodeId)    // bool
research.QueueCount                               // int

// Statik eventler
ResearchService.OnNodeQueued     += (treeId, nodeId) => { };
ResearchService.OnResearchProgress += (treeId, nodeId, tick, total) => { };
ResearchService.OnNodeCompleted  += (treeId, nodeId) => { };
ResearchService.OnEnqueueFailed  += (treeId, nodeId, reason) => { };
```

**Config (ResearchTreeConfigSO → ResearchNodeConfigSO):**
```csharp
tree.TreeId
tree.GetNode(nodeId)         // ResearchNodeConfigSO

node.NodeId
node.DisplayName
node.ResearchTicks           // kaç tick sürer
node.GoldCost                // araştırmayı başlatma maliyeti
node.PrerequisiteIds         // List<string>
node.Effects                 // List<SkillEffect> (tamamlandığında uygulanan etkiler)
```

---

### 6.5 BuildingService
**Dosya:** `Runtime/Building/BuildingService.cs`
**Bootstrap:** `BuildingBootstrap`

Izgara bazlı bina yerleştirme ve üretim.

```csharp
building.TryPlace(string buildingId, int x, int y) // PlaceResult
building.TryUpgrade(string instanceId)             // bool
building.Remove(string instanceId)                 // bool  (TryRemove değil — Remove)
building.GetInstance(string instanceId)            // BuildingInstance (varsa)

// Statik eventler
BuildingService.OnBuildingPlaced    += instance => { };
BuildingService.OnBuildingUpgraded  += instance => { };
BuildingService.OnBuildingRemoved   += instanceId => { };
BuildingService.OnBuildingProduced  += (instanceId, amount) => { };
BuildingService.OnPlaceFailed       += (buildingId, reason) => { };

// PlaceResult
result.Success      // bool
result.FailReason   // string
result.Instance     // BuildingInstance

// BuildingInstance
inst.InstanceId               // GUID string (runtime ID)
inst.BuildingId               // BuildingConfigSO.BuildingId ile eşleşir
inst.GridX, inst.GridY
inst.UpgradeTier              // 0 = base tier
inst.GetProductionPerTick(BuildingConfigSO config) // long
```

---

### 6.6 SkillTreeService
**Dosya:** `Runtime/Upgrade/SkillTreeService.cs`

Skill tree yönetimi. UpgradeTreeService ile aynı API'yi paylaşır.

```csharp
skillTree.TryPurchase(string nodeId)  // bool
skillTree.GetRank(string nodeId)      // int
skillTree.CanPurchase(string nodeId)  // bool
```

---

### 6.7 MilestoneTracker
**Dosya:** `Runtime/Milestone/MilestoneTracker.cs`

Prestige sayısı, wave sayısı, toplam kaynak gibi koşullara göre milestone kilidini açar.

```csharp
// Event — Action<MilestoneConfigSO>
MilestoneTracker.OnMilestoneCompleted += milestone => { };
```

**Config (MilestoneConfigSO):**
```csharp
milestone.MilestoneId
milestone.Condition          // PrestigeCount, WaveReached, ResourceGathered
milestone.RequiredValue      // int/long
milestone.RewardDescription  // string
```

---

### 6.8 QuestService
**Dosya:** `Runtime/Quest/QuestService.cs`

Koşullu görev sistemi.

```csharp
questService.IsCompleted(string questId)  // bool
QuestService.OnQuestCompleted += questId => { };
```

**Config (QuestConfigSO):**
```csharp
quest.QuestId
quest.DisplayName
quest.Conditions   // IQuestCondition[]  (ResourceThreshold, WaveReached)
quest.Reward       // string (açıklama)
```

---

### 6.9 MergeService
**Dosya:** `Runtime/Economy/MergeService.cs`
**Bootstrap:** `MergeBootstrap`

Aynı grup ve seviyedeki iki item'ı bir üst seviye item'a dönüştürür.

```csharp
merge.TryMerge(ItemConfigSO item) // MergeResult

// MergeResult
result.Success     // bool
result.ResultItem  // ItemConfigSO
result.GoldBonus   // long
result.FailReason  // string

// InventoryService
inventory.TryAdd(ItemConfigSO item)    // bool
inventory.TryRemove(string itemId)     // bool
inventory.GetItem(string itemId)       // ItemConfigSO
inventory.IsFull                       // bool
```

**Config (ItemConfigSO):**
```csharp
item.ItemId          // benzersiz ID (yayın sonrası değiştirme)
item.DisplayName
item.Description
item.Rarity          // ItemRarity: Common / Uncommon / Rare / Epic / Legendary
item.MaxStackSize    // 1 = stack'lenemez, 0 = sınırsız
item.MergeGroupId    // aynı grup = birleştirilebilir (boş = birleşmez)
item.MergeTier       // 0-based tier (0 = en düşük)
// NOT: BaseYield alanı ItemConfigSO'da YOKTUR — satış geliri
//      MergeRule.GoldBonus veya DropResolver üzerinden tanımlanır
```

---

## 7. DALGALAR VE SAVAŞ

---

### 7.1 WaveSpawnManager
**Dosya:** `Runtime/Wave/WaveSpawnManager.cs`
**Bootstrap:** `WaveCombatBootstrap`

Düşman dalga yaşam döngüsü. Her dalga: spawn → savaş → temizlik → upgrade selection → sonraki dalga.

```csharp
waveSpawn.StartFirstWave()          // ilk dalgayı başlatır
waveSpawn.StopWaves()               // durdurur

// Statik eventler
WaveSpawnManager.OnWaveStarted      += waveNumber => { };
WaveSpawnManager.OnWaveCleared      += waveNumber => { };
WaveSpawnManager.OnUpgradeSelection += () => { };
```

**Config (WaveConfigSO):**
```csharp
wave.BaseEnemyCountPerWave      // 3
wave.EnemyCountScalingFactor    // 1.2f (her dalgada ×1.2)
wave.HardCapEnemiesOnScreen     // 20
wave.WaveDurationSeconds        // 8f
wave.WaveTransitionDelaySeconds // 2f
wave.UpgradeSelectionWaveInterval // 5 (her 5 dalgada upgrade seçimi)
```

---

### 7.2 EnemyManager
**Dosya:** `Runtime/Enemy/EnemyManager.cs`

Aktif düşman instance'larını yönetir. EnemyAgent (düz C# class, MonoBehaviour değil) nesneleriyle çalışır.

```csharp
EnemyManager.OnEnemyKilled += (agent, waveNumber) => { };
```

---

### 7.3 AutoBattleController
**Dosya:** `Runtime/Combat/AutoBattleController.cs`

Player ve düşman arasında otomatik savaş döngüsü. Her frame'de hasar uygular, ölümleri yönetir.

---

### 7.4 DamageSystem
**Dosya:** `Runtime/Damage/DamageSystem.cs`

Merkezi hasar çözümü. Modifier'ları uygular, overkill'i işler.

```csharp
DamageSystem.OnDamageResolved += hit => { };
// DamageHit: attacker, target, amount, isCrit, worldPosition
```

---

### 7.5 HealthSystem / PlayerHealthComponent
**Dosya:** `Runtime/Health/`

```csharp
// PlayerHealthComponent eventleri
PlayerHealthComponent.OnPlayerHPChanged += (current, max) => { };
PlayerHealthComponent.OnPlayerDied      += () => { };
```

---

## 8. KAYIT / YÜKLEME SİSTEMİ

---

### 8.1 SaveService
**Dosya:** `Runtime/SaveAndLoad/SaveService.cs`

Tüm kayıt/yükleme orkestratörü. Otomatik kayıt (60s), debounce (5s), yedek dosya, imza doğrulama.

**Kayıt Akışı:**
1. Tüm `ISaveStateProvider`'lardan `OnBeforeSave(saveData)` çağrılır
2. JSON serializasyonu (ana thread)
3. Atomik yazma (arka plan thread): `.tmp` → `.bak` → `.json`
4. İmza dosyası (`.json.sig`) yazılır

**Yükleme Akışı:**
1. `.json` okunur, imza doğrulanır
2. Yedek (`backup.json`) varsa kullanılır
3. Migration pipeline çalıştırılır
4. Prestige crash-safety rollback kontrolü
5. Tüm provider'lara `OnAfterLoad(saveData)` çağrılır

```csharp
saveService.SaveAsync()           // Task — manuel kaydet
saveService.LoadAsync()           // Task — önyükleme sırası
saveService.RegisterStateProvider(provider) // ISaveStateProvider ekle
saveService.GetCurrentSaveData()  // SaveData (null ise henüz yüklenmedi)

// Eventler
saveService.OnSaveLoaded         += (data, isNewGame) => { };
saveService.OnSaveCompleted      += success => { };
saveService.OnPersistentWriteFailure += () => { }; // 3 ardışık hata
```

---

### 8.2 ISaveStateProvider

Her sistem bu interface'i implemente ederek kendi verisini yönetir.

```csharp
public interface ISaveStateProvider
{
    int ProviderOrder { get; }                    // yükleme sırası
    void OnBeforeSave(SaveData saveData);         // kayıt öncesi yaz
    void OnAfterLoad(SaveData saveData);          // yükleme sonrası oku
}
```

**Provider Sıraları (SaveConstants):**
```
Economy=10, Generators=20, UpgradeTree=25, Prestige=30,
ClickLoop=40, Harvest=50, Research=90, Building=70, Wave=60
```

---

### 8.3 SaveData

Tüm oyun durumunun kök yapısı. Ana alanlar:

```csharp
saveData.CurrentResources           // long (altın)
saveData.PrestigeCount              // int
saveData.PrestigeInProgress         // bool (crash-safety flag)
saveData.WaveNumber                 // int
saveData.GeneratorStates            // Dictionary<string, GeneratorState>
saveData.UpgradeNodeStates          // Dictionary<string, int> (nodeId → rank)
saveData.BuildingInstances          // List<BuildingSaveEntry>
saveData.ClickLoopState             // ClickLoopSaveState
saveData.HarvestState               // HarvestSaveState
saveData.LastSessionTimestamp       // DateTime (offline hesabı için)
saveData.SchemaVersion              // int (migration için)
```

---

### 8.4 Save Migrasyonları

Schema versiyonu değiştiğinde otomatik veri migrasyonu:

```
SaveMigration_V1_V2.cs
SaveMigration_V2_V3.cs
SaveMigration_V3_V4.cs
```

Yeni migrasyon eklemek için: `Tools → Endless Engine → Bump Schema Version`

---

## 9. YAPILANDIRMA (CONFIG) SİSTEMİ

Tüm oyun değerleri ScriptableObject (`.asset`) dosyalarında saklanır. Kod değişikliği gerekmez.

### Temel Config Asset'leri

| Asset Türü | Açıklama |
|------------|----------|
| `EconomyConfigSO` | Başlangıç altını, hard cap, offline gelir, prestige çarpanı |
| `GeneratorConfigSO` | Generator verimi, maliyet, ölçekleme |
| `GeneratorDatabaseSO` | Tüm generator'ların koleksiyonu |
| `UpgradeNodeConfigSO` | Upgrade node tanımı, etki, maliyet |
| `UpgradeTreeConfigSO` | Node'ların tree yapısında düzeni |
| `PrestigeConfigSO` | Prestige kapısı, çarpan, max sayısı |
| `WaveConfigSO` | Dalga formülü, düşman sayısı, süre |
| `EnemyStatConfigSO` | Düşman HP, hasar, loot formülleri |
| `ResearchTreeConfigSO` | Araştırma ağacı yapısı |
| `ResearchNodeConfigSO` | Tek araştırma node'u |
| `BuildingConfigSO` | Bina maliyeti, üretimi, tier'ları |
| `ItemConfigSO` | Merge item tanımı (grup, tier) |
| `MergeConfigSO` | Merge kuralları ve sonuçları |
| `CurrencyConfigSO` | İkincil para birimi tanımı |
| `HarvestAreaConfigSO` | Harvest alan ayarları |
| `HarvestNodeConfigSO` | Harvest node ayarları |
| `ClickLoopConfigSO` | Click loop combo/crit ayarları |
| `ClickTargetConfigSO` | Click hedef HP/yield/respawn |
| `SchemaVersionSO` | Kayıt şema versiyonu |
| `RunConfigSO` | Savaş oturumu süresi ve ayarları |
| `SkillTreeConfigSO` | Skill tree yapısı |
| `SkillNodeConfigSO` | Tek skill node'u |
| `PetConfigSO` | Pet pasif efekti |
| `ChallengeConfigSO` | Challenge/modifier ayarları |
| `MilestoneConfigSO` | Milestone koşulu ve ödülü |
| `QuestConfigSO` | Görev koşulları |

---

## 10. DESTEK SİSTEMLERİ

---

### 10.1 Telemetri
**Dosya:** `Runtime/Telemetry/TelemetryService.cs`

```csharp
TelemetryService.Track(TelemetryEvents.PrestigeTriggered, new Dictionary<string,object>{
    {"prestige_count", 3},
    {"multiplier", 3.375f}
});
TelemetryService.SetPlayerProperty("prestige_count", 3);
```

Sağlayıcılar: `ITelemetryProvider` interface'i (`NullTelemetryProvider` varsayılan).

---

### 10.2 Localization
**Dosya:** `Runtime/Localization/LocalizationService.cs`

```csharp
LocalizationService.GetString("ui.gold_label")     // "Gold"
LocalizationService.SetLanguage("tr")
LocalizationService.OnLanguageChanged += () => { };
```

Backends: `DictionaryLocalizationBackend`, `UnityLocalizationBackend`

---

### 10.3 AudioService
**Dosya:** `Runtime/Audio/AudioService.cs`

Frame bazında ses tekilleştirme (200 eş zamanlı ölüm → 1 ses).

```csharp
AudioService.PlaySFX("enemy_death");
AudioService.SetVolume(0.8f);
```

---

### 10.4 OfflineTimeCalculator
**Dosya:** `Runtime/Offline/OfflineTimeCalculator.cs`

Oyuncu geri döndüğünde kaçırılan geliri hesaplar (max `OfflineCapHours` kadar).

---

### 10.5 TimeBoostService
**Dosya:** `Runtime/Flow/TimeBoostService.cs`

Geçici oyun hızı artışı (gem harcayarak 2x, 3x vb.).

```csharp
timeBoost.TryActivate(multiplier: 2f, durationSeconds: 60f) // bool
timeBoost.RemainingSeconds                                   // float
timeBoost.IsActive                                           // bool
TimeBoostService.OnBoostExpired += () => { };
```

---

### 10.6 PetService
**Dosya:** `Runtime/Pet/PetService.cs`

Aktif pet pasif efektlerini uygular.

```csharp
petService.TryActivate(string petId)    // bool
petService.GetActivePet()               // PetConfigSO

// Config (PetConfigSO)
pet.PetId
pet.AffectedStat    // StatType
pet.BonusAmount     // float
pet.UnlockCost      // long
```

---

### 10.7 StatisticsService
**Dosya:** `Runtime/Statistics/StatisticsService.cs`

Yaşam boyu istatistikler (toplam altın, tamamlanan run'lar vb.).

```csharp
stats.Add(string key, long amount)
stats.SetIfHigher(string key, float value)
stats.Get(string key)                   // long
```

---

### 10.8 ExportService
**Dosya:** `Runtime/Export/ExportService.cs`

Kayıt verisini paylaşılabilir forma dönüştürür.

```csharp
exportService.ExportToCode()            // string
exportService.TryImportFromCode(string) // bool
```

---

### 10.9 TutorialService
**Dosya:** `Runtime/Tutorial/TutorialService.cs`

```csharp
TutorialService.OnStepAdvanced += stepId => { };
TutorialService.CompleteStep(string stepId)
TutorialService.IsStepComplete(string stepId) // bool
```

---

### 10.10 NotificationService
**Dosya:** `Runtime/Notification/NotificationService.cs`

Mobil/masaüstü push bildirimleri (Firebase, OneSignal vb. ile entegre).

```csharp
notificationService.ScheduleOfflineReturn(hours: 4f, message: "Kaynakların hazır!");
notificationService.CancelAll();
```

---

### 10.11 LeaderboardService
**Dosya:** `Runtime/Leaderboard/LeaderboardService.cs`

Skor gönderme ve sorgulama.

```csharp
// Local LeaderboardService
leaderboard.SubmitScore(string boardId, string playerName, long score) // bool
leaderboard.GetBoard(string boardId)  // IReadOnlyList<LeaderboardEntry>

// Event — Action<string boardId, LeaderboardEntry entry>
LeaderboardService.OnScoreSubmitted += (boardId, entry) => { };
```

---

## 11. PLATFORM ENTEGRASYONU (STEAM)

**Dosyalar:** `Runtime/Steam/`

### SteamAchievementBridge
**Dosya:** `Runtime/Steam/SteamAchievementBridge.cs`

Achievement sistemi **MilestoneTracker** üzerine kuruludur. Doğrudan çağrı yoktur.

```csharp
// Bootstrap:
bridge.Initialize(steamService);  // null → NullSteamService

// Çalışma prensibi:
// MilestoneTracker.OnMilestoneCompleted tetiklenince
// bridge, MilestoneConfigSO.MilestoneId'yi Steam API adına çevirip
// _steam.UnlockAchievement(apiName) çağırır.

// Inspector'da ID'leri override etmek için _mappings listesini doldur:
// AchievementMapping { MilestoneId = "first_prestige", SteamApiName = "ACH_FIRST_PRESTIGE" }

// Doğrudan ISteamService üzerinden:
_steam.UnlockAchievement("ACH_PRESTIGE_10");
```

### SteamLeaderboardService
**Dosya:** `Runtime/Steam/SteamLeaderboardService.cs`

```csharp
// Bootstrap:
steamLeaderboard.Initialize(leaderboardService, steamService, boardMappings);

// Skor gönder (local + Steam aynı anda):
steamLeaderboard.SubmitScore(
    boardId:    "main_leaderboard",
    playerName: "Player",
    score:      1234L);             // long

// Liderlik tablosunu al:
steamLeaderboard.FetchGlobalLeaderboard(
    boardId:    "main_leaderboard",
    entryCount: 10,
    onComplete: entries => { /* List<SteamLeaderboardEntry> */ });
```

### SteamCloudSaveSync
**Dosya:** `Runtime/Steam/SteamCloudSaveSync.cs`

```csharp
// Bootstrap (SaveService.LoadAsync()'ten ÖNCE):
cloudSync.Initialize(saveService, steamService);

// Cloud save kontrolü (başlangıçta):
cloudSync.CheckForNewerCloudSave();

// Cloud kayıt daha yeniyse bu event tetiklenir:
SteamCloudSaveSync.OnCloudSaveNewerThanLocal += () =>
{
    // Oyuncuya sor, onaylarsa:
    cloudSync.RestoreFromCloud(success => { /* bool */ });
};

// Upload: OTOMATIK — SaveService.OnSaveCompleted her tetiklendiğinde
// SteamCloudSaveSync arka planda dosyayı Steam Cloud'a yükler.
// Manuel Upload() veya Download() metodu YOKTUR.
```

**Null-safe fallback:** `NullSteamService.Instance` — Steam olmadan da çalışır.
`Initialize(steamService: null)` → otomatik olarak `NullSteamService` devreye girer.

---

## 12. BOOTSTRAP VE SAHNE KURULUMU

### Tüm Bootstrap Component'ları

| Component | Execution Order | Görev |
|-----------|----------------|-------|
| `AutoSetupBootstrap` | -500 | Core: Economy, Generator, UpgradeTree, Save, Tick |
| `WaveCombatBootstrap` | -490 | Wave + Combat + Enemy sistemleri |
| `ClickLoopBootstrap` | -490 | Click loop servisi |
| `HarvestLoopBootstrap` | -490 | Harvest loop servisi |
| `BuildingBootstrap` | -489 | BuildingService başlatma |
| `ResearchBootstrap` | -489 | ResearchService + TickEngine bağlama |
| `MergeBootstrap` | -489 | MergeService + InventoryService başlatma |
| `PrestigeBootstrap` | -489 | PrestigeStateManager SaveService bağlama |
| `GeneratedGameHUD` | varsayılan | HUD event aboneliği |
| `ClickTargetHandler` | varsayılan | ClickerIdle basit tıklama |
| `MergeCellHandler` | varsayılan | MergeIdle hücre tıklama |
| `BuildingSlotHandler` | varsayılan | BuildingIdle slot tıklama |

### SceneSetupUtility

`Editor/SceneSetupUtility.cs` — Programatik sahne kurucusu. NewGameWizard tarafından kullanılır.

```csharp
// Elle kullanım
var opts = new SceneSetupUtility.SetupOptions
{
    GameName    = "MyGame",
    ScenesPath  = "Assets/MyGame/Scenes",
    ConfigsPath = "Assets/MyGame/Configs",
    Type        = SceneSetupUtility.GameType.PureIdle,
    HasGenerator = true,
    HasPrestige  = true,
};
SceneSetupUtility.BuildScene(opts);
```

---

## 13. TEST ALTYAPISI

**12 test dosyası**, `Tests/` klasöründe:

| Test | Kapsam |
|------|--------|
| `EconomyServiceTests` | Altın ekleme, satın alma, prestige reset, hard cap |
| `PrestigeStateManagerTests` | Prestige uygunluğu, crash-safety, para sıfırlama |
| `SaveServiceTests` | Kayıt/yükleme döngüsü, hata kurtarma |
| `SaveMigrationTests` | V1→V2→V3→V4 veri migrasyonu |
| `BigDoubleTests` | Büyük sayı aritmetiği doğruluğu |
| `MilestoneTrackerTests` | Milestone koşulları ve eventler |
| `QuestServiceTests` | Görev koşulları ve tamamlama |
| `RecipeServiceTests` | Tarifler ve cooldown |
| `TraitServiceTests` | Trait aktivasyonu ve modifier'lar |
| `TutorialServiceTests` | Tutorial adımları ve otomatik ilerleme |
| `ConditionalUnlockServiceTests` | Unlock koşul değerlendirmesi |
| `PerformanceBenchmarks` | Kritik yolların (AddResources, TickEngine) hız ölçümü |

**Test çalıştırma:**
`Window → General → Test Runner → Run All`

---

*Endless Engine v1.3.4 — Sistem Referans Dokümanı (kaynak koddan doğrulanmış)*
