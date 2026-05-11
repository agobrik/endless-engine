# Endless Engine — Oyun Üretim Rehberi
## Sıfırdan İstediğin Idle Oyununu Yap

> Tüm field isimleri, metod imzaları ve kod örnekleri kaynak koddan doğrulanmıştır.

---

## İÇİNDEKİLER

1. [Sistemler Nasıl Çalışır — Genel Mimari](#1-sistemler-nasıl-çalışır--genel-mimari)
2. [Adım 1 — Yeni Oyun Oluştur](#2-adım-1--yeni-oyun-oluştur)
3. [Adım 2 — Generator'ları Tasarla](#3-adım-2--generatorları-tasarla)
4. [Adım 3 — Upgrade Tree Kur](#4-adım-3--upgrade-tree-kur)
5. [Adım 4 — Ekonomiyi Simüle Et ve Dengele](#5-adım-4--ekonomiyi-simüle-et-ve-dengele)
6. [Adım 5 — Prestige Sistemi](#6-adım-5--prestige-sistemi)
7. [Adım 6 — Oyun Tipine Özgü Sistemler](#7-adım-6--oyun-tipine-özgü-sistemler)
8. [Adım 7 — Kayıt Sistemi](#8-adım-7--kayıt-sistemi)
9. [Adım 8 — UI Bağlama](#9-adım-8--ui-bağlama)
10. [Adım 9 — Steam Entegrasyonu](#10-adım-9--steam-entegrasyonu)
11. [Tam Senaryolar: Her Oyun Tipi İçin Başlangıçtan Bitişe](#11-tam-senaryolar-her-oyun-tipi-i̇çin-başlangıçtan-bitişe)

---

## 1. SİSTEMLER NASIL ÇALIŞIR — GENEL MİMARİ

**Tüm sistemleri anlamak için önce bu bölümü oku. Geri kalanı bunun üzerine inşa edilir.**

### Para Nasıl Akar?

Endless Engine'de altın (veya herhangi bir para birimi) yalnızca iki yerden gelir:

```
KAYNAK 1: Pasif Gelir
TickEngine (1 Hz)
    → PassiveIncomeService.HandleTick()
        → GeneratorSystem.CalculateTotalYieldBig()   ← Generator'ların toplam yield'ı
            → EconomyService.AddResources(miktar)    ← Altın eklenir

KAYNAK 2: Düşman Öldürme (Wave/RPG oyunları)
EnemyManager.Update()
    → Düşman HP = 0
        → EnemyManager.OnEnemyKilled event tetiklenir
            → Bootstrap'ta kayıtlı: EnemyManager.OnEnemyKilled += OnEnemyKilledAddGold
                → EconomyService.AddResources(agent.GoldDropAmount)   ← Altın eklenir
```

Para bir yerden gelip `EconomyService`'e düşer. Hepsi bu. Başka yol yok.

### Upgrade Nasıl Devreye Girer?

```
Oyuncu "Upgrade Al" butonuna tıklar
    → UI: EconomyService.TryPurchase(nodeId)
        → EconomyService: maliyet kesiyorum, OnUpgradePurchased event tetikleniyor
            → UpgradeTreeService: node rank'ı artırıyorum
                → AutoBattleController veya PassiveIncomeService:
                   bir sonraki hesaplamada yeni stat'ı okuyor (IUpgradeStatProvider üzerinden)
```

Upgrade değeri **anında** uygulanmaz. Bir sonraki tick veya saldırıda otomatik olarak okunur.

### Sistem Bağımlılık Sırası

Bootstrap'ta sistemleri **her zaman bu sırayla** başlatmak zorundasın:

```
1. BigNumberFactory.Configure()           ← Her şeyden önce
2. ConfigRegistry.InjectForTesting()      ← Config'ler yüklendi
3. EconomyService.Initialize()            ← Para servisi hazır
4. GeneratorSystem.Initialize()           ← Generator'lar hazır
5. PassiveIncomeService.Initialize()      ← Pasif gelir tiki başladı
6. UpgradeTreeService.HandleConfigsLoaded() ← Upgrade tree yüklendi
7. (Wave oyunları) WaveSpawnManager.Initialize()
8. (Wave oyunları) AutoBattleController.Initialize()
9. SaveService: RegisterStateProvider() x N  ← Kayıt sistemi bağlandı
10. SaveService.LoadAsync()               ← Kayıt yüklendi, oyun başlıyor
11. (Wave oyunları) autoBattle.StartCombat()
```

**Neden bu sıra önemli?** Örneğin `EconomyService`, `UpgradeTreeService`'e bağımlı. `UpgradeTreeService` initialize edilmeden önce `EconomyService` başlatılırsa upgrade maliyetleri yanlış hesaplanır. `SaveService.LoadAsync()` bitmeden önce `StartCombat()` çağrılırsa dalga 1 değil son kaydedilen dalgadan başlar.

### Bootstrap Nedir?

Bootstrap, sahnedeki tüm servisleri birbirine bağlayan MonoBehaviour'dur. Sen de kendi bootstrap'ını yazarsın. Temel şablon:

```csharp
[DefaultExecutionOrder(-500)]   // En erken çalış
public class MyGameBootstrap : MonoBehaviour
{
    // Inspector'dan atanır
    [SerializeField] private SaveService          _saveService;
    [SerializeField] private EconomyService       _economyService;
    [SerializeField] private UpgradeTreeService   _upgradeTreeService;
    [SerializeField] private GeneratorSystem      _generatorSystem;
    [SerializeField] private PassiveIncomeService _passiveIncomeService;

    [SerializeField] private EconomyConfigSO      _economyConfig;
    [SerializeField] private GeneratorDatabaseSO  _generatorDatabase;
    [SerializeField] private SchemaVersionSO      _schemaVersion;
    [SerializeField] private PrestigeConfigSO     _prestigeConfig;
    [SerializeField] private RealmIdentityConfigSO _realmConfig;

    private IEnumerator Start()
    {
        // 1. Sayı motoru
        BigNumberFactory.Configure(_economyConfig.NumberBackend);

        // 2. Config registry
        ConfigRegistry.InjectForTesting(
            economy:  _economyConfig,
            schema:   _schemaVersion,
            prestige: _prestigeConfig,
            realm:    _realmConfig);

        // 3. Ekonomi
        _economyService.Initialize(_upgradeTreeService, _saveService);

        // 4. Generator'lar
        _generatorSystem.Initialize(_generatorDatabase.Generators, _economyService, _saveService);

        // 5. Pasif gelir
        _passiveIncomeService.Initialize(_generatorSystem, _economyService, null);

        // 6. Kayıt sağlayıcıları
        _saveService.RegisterStateProvider(_economyService);
        _saveService.RegisterStateProvider(_upgradeTreeService);
        _saveService.RegisterStateProvider(_generatorSystem);

        // 7. Kayıt yükle (async — bitene kadar bekle)
        bool done = false;
        _ = _saveService.LoadAsync().ContinueWith(_ => done = true,
            System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        yield return new WaitUntil(() => done);

        // 8. Oyun başlıyor — artık servisler kullanılabilir
        Debug.Log("Hazır!");
    }
}
```

Bu **Pure Idle** için tam bootstrap. Wave sistemi eklemek istiyorsan aşağıda Senaryo C'ye bak.

### Sahnede Ne Olması Lazım?

```
Hiyerarşi:
Bootstrap (GameObject)
    ├── MyGameBootstrap  (component — sen yazarsın)
    ├── SaveService      (component)
    ├── EconomyService   (component)
    ├── UpgradeTreeService (component)
    ├── GeneratorSystem  (component)
    ├── PassiveIncomeService (component)
    └── TickEngine       (component)

Canvas (GameObject)
    └── UI script'lerin (EconomyService.OnResourcesChanged'i dinler)
```

Bootstrap GameObject'i seç → Inspector'da tüm alanları doldur (her component'ı sürükle, her config asset'ini sürükle) → Play.

---

## 2. ADIM 1 — YENİ OYUN OLUŞTUR

### New Game Wizard

`Tools → Endless Engine → New Game Wizard`

| Tip | Ne Zaman Seç |
|-----|-------------|
| **Pure Idle** | Sadece pasif gelir, tıklama yok, savaş yok |
| **Clicker Idle** | Basit tıklama mekanikleri |
| **Click Loop** | HP'li hedefler, combo, crit (daha derin clicker) |
| **Idle-vs / RPG** | Otomatik savaş + dalga sistemi |
| **Tower Defense** | Dalga sistemi + kule yerleşimi |
| **Merge Idle** | Eşya birleştirme (Merge Dragons tarzı) |
| **Farm Idle** | Hasat/çiftçilik mekanikleri |
| **Research Idle** | Uzun araştırma kuyrukları |
| **Building Idle** | Grid üzerine bina yerleşimi |
| **Prestige-Heavy** | Çok katmanlı prestige döngüsü |
| **Harvest Idle** | Cursor ile kaynak toplama |
| **Custom** | Sadece core sistemler, seni bekliyor |

**Generate'e bastıktan sonra:**
- `Assets/[OyunAdın]/Configs/` — tüm config asset'leri (buradan düzenlersin)
- `Assets/[OyunAdın]/Scenes/[OyunAdın].unity` — hazır sahne
- Sahneyi aç → Play → çalışıyor olmalı

---

## 3. ADIM 2 — GENERATOR'LARI TASARLA

Generator = Pasif gelir kaynağı. Her biri `TickEngine`'in her tick'inde `PassiveIncomeService` üzerinden altın üretir.

### Generator Editor

`Tools → Endless Engine → Generator Editor`

Editor, wizard'ın oluşturduğu `GeneratorDatabase.asset` ile açılır.

### GeneratorConfigSO Alanları

| Field | Açıklama | Örnek |
|-------|----------|-------|
| `GeneratorId` | Benzersiz ID — yayın sonrası asla değiştirme | `"gold_mine"` |
| `DisplayName` | UI'da görünen isim | `"Altın Madeni"` |
| `BaseYieldPerSecond` | Saniyedeki temel üretim | `0.5` |
| `BaseCost` | İlk kopya maliyeti | `50` |
| `CostScalingFactor` | Her kopyada maliyet çarpanı | `1.15` |
| `UnlockPrerequisiteId` | Bu generator açılmadan görünmez | `"gold_mine"` |
| `UnlockRequiredCount` | Kaç kopya alınırsa sonraki açılır | `10` |

### İyi Bir Generator Hiyerarşisi

Her generator öncekinden ~10x pahalı, ~5-8x daha güçlü olmalı:

| # | İsim | Yield/s | Başlangıç Maliyeti | CostScale |
|---|------|---------|-------------------|-----------|
| 1 | Maden | 0.1 | 10 | 1.15 |
| 2 | Çiftlik | 0.6 | 100 | 1.15 |
| 3 | Fabrika | 4.0 | 1,100 | 1.15 |
| 4 | Güç Santrali | 25 | 12,000 | 1.15 |
| 5 | Araştırma Lab | 200 | 130,000 | 1.15 |
| 6 | Uzay İstasyonu | 1,500 | 1,400,000 | 1.15 |

### Generator'ı Upgrade Tree ile Bağlamak

Generator yield'ı upgrade'den etkilenmez mi? Etkilenir — ama dolaylı yoldan. `UpgradeNodeDefinition.AffectedStat = StatType.GeneratorYield` olan bir node ekle. `PassiveIncomeService`, her tick hesaplarken `IUpgradeStatProvider.GetMultiplier(StatType.GeneratorYield)` çağırır ve toplam yield'a uygular.

---

## 4. ADIM 3 — UPGRADE TREE KUR

### Upgrade Tree Editor

`Tools → Endless Engine → Upgrade Tree Editor`

Wizard'ın `UpgradeTreeConfig.asset` dosyasını aç veya `New Tree` ile yenisini oluştur.

### Bir Node'un Tüm Alanları

**Identity:**
| Field | Açıklama |
|-------|----------|
| `NodeId` | Benzersiz, küçük harf+alt çizgi (`"dmg_01"`) — yayın sonrası değiştirme |
| `DisplayName` | Oyuncunun gördüğü isim |
| `Description` | Kısa açıklama |

**Etki:**
| Field | Açıklama |
|-------|----------|
| `AffectedStat` | StatType enum (bak: hangi StatType ne işe yarar — aşağıda) |
| `EffectType` | `PercentBonus` (%X ekler) veya `FlatBonus` (X ekler) |
| `EffectPerRank` | Her rankta etki (0.10 = %10) |
| `MaxRank` | Kaç kez alınabilir |

**Ekonomi:**
| Field | Açıklama |
|-------|----------|
| `BaseCost` | İlk rank maliyeti |
| `CostScalingFactor` | Her rankta çarpan (1.5 = 2. rank 1.5x pahalı) |
| `SelectionWeight` | Dalga sonu kart olarak çıkma şansı (yüksek = sık) |
| `PrestigeGateRequirement` | Kaçıncı prestige'den sonra görünür (0 = hep) |

**Tree Yapısı:**
| Field | Açıklama |
|-------|----------|
| `PrerequisiteNodeIDs` | Önce alınması gereken node ID'leri |
| `MaxOutgoingEdges` | Kaç bağlantı çıkabilir (0 = sınırsız) |
| `HideUntilUnlockable` | true = koşul sağlanana kadar gizle |

### StatType Referansı — Ne Hangi Sistemi Etkiler

| StatType | Etkisi | Hangi Sistem Okur |
|----------|--------|------------------|
| `GeneratorYield` | Pasif gelir çarpanı | PassiveIncomeService |
| `Damage` | Saldırı hasarı | AutoBattleController |
| `MaxHP` | Oyuncu can | PlayerHealthComponent |
| `Armor` | Savunma azaltma | DamageSystem |
| `CritChance` | Crit şansı | AutoBattleController |
| `CritMultiplier` | Crit hasarı çarpanı | AutoBattleController |
| `AttackSpeed` | Saldırı hızı | AutoBattleController |
| `ClickDamage` | Tıklama hasarı | ClickLoopService |
| `ClickYieldMultiplier` | Tıklama altın çarpanı | ClickLoopService |
| `ClickCritChance` | Tıklama crit şansı | ClickYieldResolver |
| `ClickCritMultiplier` | Tıklama crit çarpanı | ClickYieldResolver |
| `ClickAutoRate` | Otomatik tıklama hızı | ClickLoopService |
| `HarvestRadius` | Harvest alan yarıçapı | HarvestLoopService |
| `HarvestYieldMultiplier` | Harvest altın çarpanı | HarvestYieldResolver |
| `HarvestTickRate` | Harvest tick hızı | HarvestLoopService |
| `IdleYieldRate` | Genel pasif gelir | PassiveIncomeService |

### Örnek Tree Yapıları

**Doğrusal (başlangıç):**
```
[Üretim +10%] → [Üretim +10%] → [Üretim +10%]
```

**Dallanan (orta oyun):**
```
                 → [Saldırı Dalı: Damage +10%]
[Temel Üretim] →
                 → [Savunma Dalı: MaxHP +15%]
                 → [Ekonomi Dalı: GeneratorYield +20%]
```

**Prestige-gated (geç oyun):**
```
PrestigeGateRequirement=0: Temel node'lar (herkese görünür)
PrestigeGateRequirement=1: Güçlendirilmiş node'lar
PrestigeGateRequirement=3: Efsanevi node'lar
```

### Upgrade Satın Alma (Kod)

```csharp
// Upgrade mevcut mu ve maliyeti ne?
bool canBuy = upgradeTreeService.IsNodeAvailable(nodeId);
long cost   = upgradeTreeService.GetNodeCost(nodeId);

// Satın al — EconomyService üzerinden geçer, maliyet kesilir
economyService.TryPurchase(nodeId);

// Event ile sonucu dinle
EconomyService.OnUpgradePurchased += (nodeId, cost) => RefreshUI();
EconomyService.OnPurchaseFailed   += (nodeId, cost, balance) => ShowError();
```

---

## 5. ADIM 4 — EKONOMİYİ SİMÜLE ET VE DENGELE

### Economy Simulator

`Tools → Endless Engine → Economy Simulator`

Sol panelde config'leri bağla → parametreleri ayarla → **Simulate** bas.

| Parametre | Açıklama |
|-----------|----------|
| Sessions | Kaç oturum simüle edilsin (30-50 önerilir) |
| Session length | Oturum başına dakika |
| Offline hours | Oturumlar arası çevrimdışı saat |
| Auto-prestige | Uygun olunca otomatik prestige et |

### Hedef Dengeler

| Metrik | İyi Değer |
|--------|-----------|
| İlk prestige | 5-10. oturumda |
| Prestige başı Perm Mult artışı | ×1.5 - ×3.0 |
| 30. oturumda toplam Perm Mult | ×10 - ×100 |
| Oturum başına altın büyümesi | Önceki oturumun 2-5 katı |

### EconomyConfigSO Alanları

`Assets/[OyunAdın]/Configs/EconomyConfig.asset`

| Field | Varsayılan | Açıklama |
|-------|-----------|----------|
| `StartingGold` | 0 | Yeni oyunda/prestige sonrası başlangıç altını |
| `ResourceHardCap` | 1_000_000_000 | Maksimum altın |
| `NumberBackend` | DoubleNumber | `DoubleNumber` (≤1e15) veya `BigDouble` (çok büyük sayılar) |
| `OfflineCapHours` | 8 | Çevrimdışı kazanç sınırı (saat) |
| `BaseGoldDropPerEnemy` | 1 | Wave oyunlarında düşman başına temel altın |
| `GoldDropScalingExponent` | 1.2 | Düşman altın dropu dalga ölçekleme katsayısı |

### Yaygın Sorunlar

| Sorun | Çözüm |
|-------|-------|
| Hiç prestige yapılamıyor | `PrestigeConfig.MinGoldToPrestige` değerini düşür |
| İlk prestige çok erken geliyor | `PrestigeConfig.MinGoldToPrestige` değerini artır |
| Geç oyunda büyüme durdu | Generator yield artır veya `MaxPermanentMultiplier` büyüt |
| Hard cap'e çarpıyor | `ResourceHardCap` artır veya `NumberBackend = BigDouble` yap |

---

## 6. ADIM 5 — PRESTİGE SİSTEMİ

### PrestigeConfigSO Alanları

`Assets/[OyunAdın]/Configs/PrestigeConfig.asset`

| Field | Açıklama | Önerilen |
|-------|----------|---------|
| `MinWaveForPrestige` | Minimum dalga şartı (0 = kapalı) | Wave oyunları: 10 |
| `MinGoldToPrestige` | Minimum altın şartı (0 = kapalı) | Oyununa göre ayarla |
| `MaxPrestigeCount` | Prestige sınırı (0 = sınırsız) | 0 |
| `BaseMultiplierPerPrestige` | Her prestige'de kalıcı çarpan | 1.5 |
| `MaxPermanentMultiplier` | Çarpan tavanı | 1000 |
| `StatsAmplifiedByPrestige` | Hangi stat'lar etkilenir | `[Damage, MaxHP]` |

### Prestige Neyi Sıfırlar?

`PrestigeStateManager.OnPrestigeStarted` event'ini dinleyen tüm sistemler sıfırlanır:
- Altın → `StartingGold`'a döner
- Generator sayıları → 0'a döner
- Upgrade rank'ları → 0'a döner
- Dalga sayısı → 1'e döner

**Sıfırlanmayan:**
- Kalıcı çarpan (`GetPermanentMultiplier()`)
- Prestige sayısı
- `PrestigeGateRequirement > 0` olan node'lar bir sonraki prestige'de açılır

### Prestige Butonu (Kod)

```csharp
void Update()
{
    prestigeButton.interactable = _prestigeManager.CanPrestige;
    multiplierText.text = $"×{_prestigeManager.GetPermanentMultiplier():F2}";
}

public void OnPrestigeClick() => _prestigeManager.TryPrestige();

PrestigeStateManager.OnPrestigeComplete += (count, multiplier) =>
{
    Debug.Log($"Prestige {count}! Çarpan: ×{multiplier:F2}");
};
```

### Çok Katmanlı Prestige (Ascension)

`AscensionStateManager` + `AscensionDatabaseSO`:

| Field (PrestigeLayerConfigSO) | Açıklama |
|-------------------------------|----------|
| `LayerIndex` | Katman numarası (0-based) |
| `DisplayName` | Katman adı ("Prestige", "Ascension" vb.) |
| `RequiredPreviousLayerCount` | Bir önceki katmanın kaç kez tetiklenmesi gerekir |
| `MinWaveRequired` | Bu katman için min dalga |
| `MaxCount` | Bu katman kaç kez tetiklenebilir (0 = sınırsız) |
| `BaseMultiplierPerTrigger` | Her tetiklemede çarpan artışı |
| `MaxPermanentMultiplier` | Bu katman için çarpan tavanı |
| `ResetGenerators` | true = generator'lar bu katmanda da sıfırlanır |

```csharp
// Ascension butonu
bool canAscend = _ascensionManager.CanTrigger(layerIndex: 1, _waveManager.CurrentWaveNumber);
ascensionButton.interactable = canAscend;

public void OnAscensionClick()
{
    _ascensionManager.TryTrigger(layerIndex: 1, _waveManager.CurrentWaveNumber);
}

AscensionStateManager.OnAscensionComplete += (layerIndex, newCount, cascadeMult) =>
{
    Debug.Log($"Ascension {newCount}! Cascade: ×{cascadeMult:F2}");
};
```

---

## 7. ADIM 6 — OYUN TİPİNE ÖZGÜ SİSTEMLER

---

### 7.1 Wave / RPG / Tower Defence

Wave sisteminde para akışı farklı çalışır — generator'ların yanı sıra düşman öldürmeden de altın gelir.

#### Tam Para Akışı (Wave Oyunları)

```
TickEngine (1 Hz)
    → PassiveIncomeService → EconomyService.AddResources()      [pasif gelir]

EnemyManager.Update() → düşman HP=0
    → EnemyManager.OnEnemyKilled event
        → Bootstrap'ta kayıtlı: EnemyManager.OnEnemyKilled += OnEnemyKilledAddGold
            → EconomyService.AddResources(agent.GoldDropAmount)  [düşman dropu]
```

`agent.GoldDropAmount` değeri `EnemyStatConfigSO.BaseGoldDropPerEnemy × wave^GoldDropScalingExponent` formülüyle hesaplanır. Bunu değiştirmek istersen `EconomyConfigSO.BaseGoldDropPerEnemy` ve `GoldDropScalingExponent` alanlarını düzenle.

#### Bootstrap — Wave Oyunu için Tam Wiring

Wizard'ın ürettiği sahnede bu zaten hazır. Kendisi yazıyorsan:

```csharp
[DefaultExecutionOrder(-500)]
public class TowerDefenceBootstrap : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private SaveService          _saveService;
    [SerializeField] private EconomyService       _economyService;
    [SerializeField] private UpgradeTreeService   _upgradeTreeService;
    [SerializeField] private GeneratorSystem      _generatorSystem;
    [SerializeField] private PassiveIncomeService _passiveIncomeService;

    [Header("Wave & Combat")]
    [SerializeField] private AutoBattleController _autoBattle;
    [SerializeField] private WaveSpawnManager     _waveSpawnManager;
    [SerializeField] private EnemyManager         _enemyManager;
    [SerializeField] private PrestigeStateManager _prestigeManager;

    [Header("Configs")]
    [SerializeField] private EconomyConfigSO        _economyConfig;
    [SerializeField] private WaveConfigSO           _waveConfig;
    [SerializeField] private PlayerBaseStatConfigSO _playerConfig;
    [SerializeField] private PrestigeConfigSO       _prestigeConfig;
    [SerializeField] private GeneratorDatabaseSO    _generatorDatabase;
    [SerializeField] private SchemaVersionSO        _schemaVersion;
    [SerializeField] private RealmIdentityConfigSO  _realmConfig;

    private IEnumerator Start()
    {
        // 1. Sayı motoru
        BigNumberFactory.Configure(_economyConfig.NumberBackend);

        // 2. Config registry — wave ve player config'leri dahil
        ConfigRegistry.InjectForTesting(
            economy:  _economyConfig,
            wave:     _waveConfig,
            player:   _playerConfig,
            prestige: _prestigeConfig,
            schema:   _schemaVersion,
            realm:    _realmConfig);

        // 3. Upgrade tree
        _upgradeTreeService?.HandleConfigsLoaded();

        // 4. Ekonomi
        _economyService.Initialize(_upgradeTreeService, _saveService);

        // 5. Generator'lar
        _generatorSystem.Initialize(_generatorDatabase.Generators, _economyService, _saveService);

        // 6. Pasif gelir
        _passiveIncomeService.Initialize(_generatorSystem, _economyService, null);

        // 7. Wave spawner — EnemyManager'ı alır
        _waveSpawnManager.Initialize(_enemyManager, saveNotifier: null);

        // 8. AutoBattle — stat provider ile combat istatistikleri okur
        var statProvider = new BaseStatUpgradeProvider(_playerConfig);
        _autoBattle.Initialize(
            _enemyManager, _waveSpawnManager, statProvider,
            _playerConfig, _waveConfig, playerId: 1);

        // 9. Düşman öldüğünde altın ekle — BU SATIR OLMADAN DÜŞMANLAR PARA DÜŞÜRMEZ
        EnemyManager.OnEnemyKilled += OnEnemyKilledAddGold;
        // AutoBattle'ın ölümleri işlemesi için de gerekli
        EnemyManager.OnEnemyKilled += _autoBattle.HandleEnemyKilledByManager;

        // 10. Kayıt sağlayıcıları
        _saveService.RegisterStateProvider(_economyService);
        _saveService.RegisterStateProvider(_upgradeTreeService);
        _saveService.RegisterStateProvider(_generatorSystem);
        _saveService.RegisterStateProvider(_waveSpawnManager);
        _saveService.RegisterStateProvider(_prestigeManager);

        // 11. Kayıt yükle
        bool done = false;
        _ = _saveService.LoadAsync().ContinueWith(_ => done = true,
            System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        yield return new WaitUntil(() => done);

        // 12. Savaş başlasın — LoadAsync'ten SONRA çağrılmalı
        _autoBattle.StartCombat();
    }

    private void OnDestroy()
    {
        EnemyManager.OnEnemyKilled -= OnEnemyKilledAddGold;
        EnemyManager.OnEnemyKilled -= _autoBattle.HandleEnemyKilledByManager;
    }

    private void OnEnemyKilledAddGold(EnemyAgent agent)
        => _economyService?.AddResources(agent.GoldDropAmount);
}
```

**Not:** `WaveSpawnManager.StartFirstWave()` ayrıca çağrılmaz — `AutoBattleController.StartCombat()` içinde otomatik tetiklenir.

#### WaveConfigSO Alanları

`Assets/[OyunAdın]/Configs/WaveConfig.asset`

| Field | Örnek | Açıklama |
|-------|-------|----------|
| `TotalWavesPerRun` | 50 | Run başına toplam dalga (-1 = sonsuz) |
| `BaseEnemyCountPerWave` | 3 | Dalga 1'deki düşman sayısı |
| `EnemyCountScalingFactor` | 1.12 | Her dalgada düşman sayısı çarpanı |
| `HardCapEnemiesOnScreen` | 20 | Aynı anda maksimum düşman |
| `SpawnIntervalSeconds` | 0.5 | Düşmanlar arası spawn aralığı |
| `WaveTransitionDelaySeconds` | 2.0 | Dalgalar arası bekleme süresi |
| `WaveDurationSeconds` | 120 | Dalga zaman aşımı (güvenlik) |
| `UpgradeSelectionWaveInterval` | 5 | Her N dalgada upgrade kartı seçimi |
| `WaveSaveMilestoneInterval` | 10 | Her N dalgada otomatik kayıt |
| `EliteWaveInterval` | 10 | Her N dalgada elite düşman |
| `EliteStatMultiplier` | 3.0 | Elite düşman stat çarpanı |
| `BossWaveInterval` | 25 | Her N dalgada boss |

#### EnemyStatConfigSO Alanları

`Assets/[OyunAdın]/Configs/EnemyStatConfig.asset`

| Field | Örnek | Açıklama |
|-------|-------|----------|
| `BaseMaxHP` | 20 | Dalga 1 düşman canı |
| `BaseAttackDamage` | 5 | Dalga 1 saldırı hasarı |
| `BaseContactDamage` | 2 | Temas hasarı (tick başına) |
| `MoveSpeed` | 3.0 | Hareket hızı (world unit/s) |
| `AttackRange` | 2.0 | Saldırı menzili (world unit) |
| `AttackInterval` | 1.5 | Saldırılar arası süre (saniye) |
| `WaveScalingExponent` | 1.5 | Dalga N'de HP = BaseMaxHP × N^WaveScalingExponent |
| `HardCapEnemiesOnScreen` | 200 | Maksimum eş zamanlı düşman |

#### PlayerBaseStatConfigSO Alanları

| Field | Örnek | Açıklama |
|-------|-------|----------|
| `BaseMaxHP` | 200 | Oyuncu başlangıç canı |
| `BaseAttackDamage` | 10 | Temel saldırı hasarı |
| `BaseAttackInterval` | 1.0 | Saldırılar arası süre (saniye) |
| `BaseAttackRange` | 5.0 | Saldırı menzili |
| `BaseCritChance` | 0.1 | %10 crit şansı |
| `BaseCritMultiplier` | 2.0 | Crit 2x hasar |
| `BaseMoveSpeed` | 5.0 | Hareket hızı |

#### Upgrade'ler Savaşı Nasıl Etkiler?

Upgrade satın alındığında `AutoBattleController`, bir sonraki dalga geçişinde veya upgrade seçiminin ardından `CacheStats()` çağırır. Bu çağrı `IUpgradeStatProvider.GetMultiplier(StatType.Damage)` vb. okur ve `_effectiveAttackDamage` gibi cache'lenmiş alanları günceller. Değişiklik o andan itibaren geçerlidir.

#### Wave Sonu Upgrade Seçimi

```csharp
// WaveSpawnManager bu event'i her UpgradeSelectionWaveInterval dalgada bir tetikler
WaveSpawnManager.OnUpgradeSelectionTriggered += () =>
{
    _autoBattle.StopCombat();   // Savaşı durdur

    var nodes = _upgradeTree.GetAvailableNodes();
    upgradeScreen.Show(nodes.Take(3).ToArray(), (chosen) =>
    {
        _economyService.TryPurchase(chosen.NodeId);
        upgradeScreen.Hide();
        _autoBattle.NotifyUpgradeSelected();   // Savaşı yeniden başlat
    });
};
```

---

### 7.2 Click Loop (Aktif Clicker)

**ClickLoopConfigSO** — `Assets/[OyunAdın]/Configs/ClickLoopConfig.asset`

| Field | Örnek | Açıklama |
|-------|-------|----------|
| `ComboDecayDelay` | 1.5 | Tıklama durduğunda combo azalmaya başlayana kadar bekleme (saniye) |
| `ComboDecayRate` | 8.0 | Saniyede azalan combo puanı |
| `ComboPointsPerStep` | 5.0 | Her bir ×1 çarpan için gereken combo puanı |
| `MaxComboMultiplier` | 8.0 | Combo tavanı |
| `BaseCritChance` | 0.05 | %5 crit şansı |
| `BaseCritMultiplier` | 3.0 | Crit 3x altın |
| `BaseAutoClickRate` | 0.0 | 0 = auto-click yok; upgrade ile açılır |
| `OfflineCapHours` | 4.0 | Çevrimdışı auto-click kazanç sınırı |
| `OfflineEfficiency` | 0.25 | Çevrimdışı verimlilik (aktifin %25'i) |

**ClickTargetConfigSO** — her hedef tipi için ayrı asset

| Field | Örnek | Açıklama |
|-------|-------|----------|
| `TargetId` | `"rock"` | Benzersiz ID |
| `MaxHP` | 5 | Kaç tıklama dayanır |
| `DamagePerClick` | 1.0 | Tıklama başına hasar |
| `BaseYield` | 3.0 | Yok edilince verilen altın |
| `AwardYieldPerClick` | false | true = her tıkta altın, false = yok edilince |
| `RespawnSeconds` | 3.0 | Kaç saniyede geri gelir |
| `ComboContribution` | 1.0 | Combo sayacına katkı |

**Sahnede ClickTarget kurulumu:**
1. GameObject oluştur → `ClickTarget` component ekle (BoxCollider2D otomatik gelir)
2. `_config` alanına ClickTargetConfigSO asset'ini ata
3. Layer'ı `ClickableTargets` yap
4. Bootstrap'ın Inspector'ında bu target'ı referans vermene gerek yok — ClickLoopService tüm sahnedeki ClickTarget'ları otomatik bulur

---

### 7.3 Harvest Idle

**HarvestAreaConfigSO**

| Field | Örnek | Açıklama |
|-------|-------|----------|
| `BaseTickInterval` | 0.25 | Saniyede kaç tick (0.25 = 4/s) |
| `ComboDecayDelay` | 1.5 | Combo azalmaya başlamadan önce bekleme |
| `ComboDecayRate` | 2.0 | Saniyede azalan combo |
| `ComboPointsPerMultiplierStep` | 5.0 | Bir sonraki çarpan için gereken puan |
| `MaxComboMultiplier` | 8.0 | Combo tavanı |
| `OfflineCapHours` | 4.0 | Çevrimdışı kazanç sınırı |
| `OfflineEfficiency` | 0.5 | Çevrimdışı verimlilik |

**HarvestNodeConfigSO** — her node tipi için ayrı asset

| Field | Örnek | Açıklama |
|-------|-------|----------|
| `NodeId` | `"gold_ore"` | Benzersiz ID |
| `MaxHP` | 20 | Kaç tick dayanır |
| `DamagePerTick` | 2.0 | Tick başına hasar |
| `BaseYield` | 100 | Yok edilince verilen altın |
| `AwardYieldPerTick` | true | Her tick'te kısmi altın ver |
| `RespawnSeconds` | 10 | Kaç saniyede geri gelir |
| `ComboContribution` | 1.0 | Combo katkısı |

---

### 7.4 Merge Idle

**ItemConfigSO** — her tier için ayrı asset

| Field | Örnek | Açıklama |
|-------|-------|----------|
| `ItemId` | `"coin_t1"` | Benzersiz ID |
| `DisplayName` | `"Bronz Para"` | Görünen isim |
| `MergeGroupId` | `"coins"` | Aynı grup birbirleriyle birleşir |
| `MergeTier` | 1 | Tier 1 + Tier 1 = Tier 2 |
| `Rarity` | `Common` | Common/Uncommon/Rare/Epic/Legendary |
| `MaxStackSize` | 99 | Stack sınırı |

**MergeConfigSO** — grup başına bir asset

| Field | Açıklama |
|-------|----------|
| `ConfigId` | Benzersiz config ID |
| `MergeGroupId` | Hangi item grubu (ItemConfigSO.MergeGroupId ile eşleşmeli) |
| `Rules` | `List<MergeRule>` — tier bazında kurallar |

**MergeRule:**
| Field | Açıklama |
|-------|----------|
| `InputTier` | Birleştirilecek item tier'ı |
| `ResultItem` | Üretilecek ItemConfigSO |
| `GoldBonus` | Birleştirme bonusu altın |

**Örnek kural zinciri:**
```
Rule: InputTier=1, ResultItem=CoinT2, GoldBonus=0
Rule: InputTier=2, ResultItem=CoinT3, GoldBonus=5
Rule: InputTier=3, ResultItem=CoinT4, GoldBonus=20
```
→ Bronz + Bronz = Gümüş, Gümüş + Gümüş = Altın, vb.

```csharp
// Merge event dinleme
MergeService.OnMergeCompleted += (result) =>
{
    _economyService.AddResources(result.GoldBonus);
    gridView.Remove(result.InputA.Position);
    gridView.Remove(result.InputB.Position);
    gridView.Place(result.ResultItem, targetPos);
};
```

---

### 7.5 Research Idle

**ResearchNodeConfigSO** — her araştırma için ayrı asset

| Field | Örnek | Açıklama |
|-------|-------|----------|
| `NodeId` | `"adv_damage"` | Benzersiz ID |
| `DisplayName` | `"Gelişmiş Hasar"` | Görünen isim |
| `Tier` | 1 | Tier 0 = root; önceki tier bitmeden kuyruğa alınamaz |
| `PrerequisiteIds` | `["basic_income"]` | Önce tamamlanması gereken node'lar |
| `GoldCost` | 5000 | Kuyruğa alma maliyeti |
| `ResearchTicks` | 300 | Tamamlanma süresi (saniye, TickEngine 1 Hz) |
| `Effects` | `List<SkillEffect>` | Tamamlandığında uygulanan etkiler |

**ResearchTreeConfigSO:**
- `TreeId` — benzersiz tree ID
- `Nodes` — `ResearchNodeConfigSO[]` array'i, tüm node'ları buraya ekle

**Wiring (kodda):**

```csharp
// Araştırmayı kuyruğa al
_researchService.TryEnqueue("tech_tree", "adv_damage");

// İlerleme takibi
ResearchService.OnResearchProgress += (treeId, nodeId, ticksDone, ticksTotal) =>
{
    researchBar.fillAmount = (float)ticksDone / ticksTotal;
};

// Tamamlandığında
ResearchService.OnNodeCompleted += (treeId, nodeId) =>
{
    Debug.Log($"Araştırma tamamlandı: {nodeId}");
    // Örn: generator unlock, yeni sistem aç, vb.
};
```

**ResearchService, TickEngine.OnTick'e otomatik subscribe olur.** Bootstrap'ta ayrıca bağlaman gerekmez, sadece `Initialize()` çağır.

---

### 7.6 Building Idle

**BuildingConfigSO** — her bina tipi için ayrı asset

| Field | Örnek | Açıklama |
|-------|-------|----------|
| `BuildingId` | `"house"` | Benzersiz ID |
| `GridWidth` | 1 | Grid genişliği (hücre) |
| `GridHeight` | 1 | Grid yüksekliği (hücre) |
| `PlacementCost` | 200 | Yerleştirme maliyeti |
| `ProductionCurrencyId` | `"gold"` | Üretilen para birimi |
| `ProductionPerTick` | 2 | Tick başına üretim |
| `MaxInstances` | 0 | Maks kopya (0 = sınırsız) |
| `UpgradeTiers` | `BuildingUpgradeTier[]` | Upgrade seviyeleri |

**BuildingUpgradeTier** (UpgradeTiers array'indeki her eleman):
| Field | Açıklama |
|-------|----------|
| `DisplayLabel` | "Level 2", "Tier II" vb. |
| `UpgradeCost` | Bu tier'a geçiş maliyeti |
| `ProductionBonusPerTick` | Flat üretim bonusu |
| `ProductionMultiplier` | Çarpan bonusu |

```csharp
buildingService.TryPlace("house", gridX: 2, gridY: 3);
```

---

## 8. ADIM 7 — KAYIT SİSTEMİ

### Otomatik Kayıt

`SaveService` her 60 saniyede bir otomatik kaydeder. Ek kod gerekmez.

### Manuel Kayıt Tetikleme

```csharp
saveService.NotifyUpgradePurchased();  // 5 saniye debounce ile kaydeder
```

### Kendi Sistemini Kayıt Sistemine Eklemek

```csharp
public class MyCustomService : MonoBehaviour, ISaveStateProvider
{
    public int ProviderOrder => 95;  // Yüksek = daha geç çalışır

    public void OnBeforeSave(SaveData saveData)
    {
        saveData.CustomField = myData;  // Kaydet
    }

    public void OnAfterLoad(SaveData saveData)
    {
        myData = saveData.CustomField;  // Yükle
    }
}

// Bootstrap'ta:
saveService.RegisterStateProvider(myCustomService);
```

### Save Şeması Güncellemesi

SaveData'ya yeni alan eklediğinde:
1. `Tools → Endless Engine → Bump Schema Version`
2. Üretilen `SaveMigration_VN_VN+1.cs` dosyasını doldur:

```csharp
public class SaveMigration_V4_V5 : IMigration
{
    public int FromVersion => 4;
    public int ToVersion   => 5;

    public void Apply(SaveData data)
    {
        data.MyNewField = 0;  // Eski kayıtlarda yoktu, varsayılan ver
    }
}
```

### ISaveStateProvider Kayıt Sırası

Kayıt ve yükleme sırasını belirler:

| Sistem | ProviderOrder |
|--------|--------------|
| EconomyService | 10 |
| GeneratorSystem | 15 |
| UpgradeTreeService | 20 |
| PrestigeStateManager | 30 |
| WaveSpawnManager | 40 |
| Özel sistemlerin | 90+ |

---

## 9. ADIM 8 — UI BAĞLAMA

### Event İmzaları (Kaynak Koddan Doğrulanmış)

```csharp
// Altın değişti — double current, double delta
EconomyService.OnResourcesChanged += (current, delta) =>
{
    goldText.text = FormatGold(current);
    incomeLabel.text = $"{delta:F1}/s";
};

// Upgrade satın alındı — string nodeId, long cost
EconomyService.OnUpgradePurchased += (nodeId, cost) => RefreshUpgradePanel();

// Upgrade başarısız — string nodeId, long cost, double balance
EconomyService.OnPurchaseFailed += (nodeId, cost, balance) => ShakeButton();

// Generator satın alındı — string generatorId
GeneratorSystem.OnGeneratorPurchased += generatorId => RefreshGeneratorCard(generatorId);

// Wave başladı — int waveNumber
WaveSpawnManager.OnWaveStarted += waveNumber => waveText.text = $"Dalga {waveNumber}";

// Wave bitti — int waveNumber
WaveSpawnManager.OnWaveComplete += waveNumber => ShowWaveCompletePopup();

// Prestige tamamlandı — int count, float multiplier
PrestigeStateManager.OnPrestigeComplete += (count, multiplier) =>
{
    prestigeCountText.text = $"Prestige: {count}";
    multiplierText.text = $"×{multiplier:F1}";
};

// Research ilerledi — string treeId, string nodeId, int ticksDone, int ticksTotal
ResearchService.OnResearchProgress += (treeId, nodeId, ticksDone, ticksTotal) =>
{
    researchBar.fillAmount = (float)ticksDone / ticksTotal;
};

// Research tamamlandı — string treeId, string nodeId
ResearchService.OnNodeCompleted += (treeId, nodeId) => ShowResearchComplete(nodeId);

// İkincil para birimi değişti — string currencyId, double newBalance
CurrencyService.OnBalanceChanged += (currencyId, newBalance) =>
{
    if (currencyId == "gems") gemsText.text = newBalance.ToString("N0");
};
```

### Generator Kartı Güncelleme

```csharp
void RefreshCard(string generatorId)
{
    long cost  = _generators.GetNextCost(generatorId);   // GetNextCost → long döner
    int  count = _generators.GetCount(generatorId);
    double yield = _generators.GetYieldPerSecond(generatorId);

    costLabel.text  = $"Maliyet: {FormatGold(cost)}";
    countLabel.text = $"Sahip: {count}";
    yieldLabel.text = $"{yield:F1}/s";
    buyButton.interactable = _economy.CurrentResources >= cost;
}
```

### Upgrade Kartı Güncelleme

```csharp
void RefreshUpgradeCard(string nodeId)
{
    var node    = _upgradeTree.GetNode(nodeId);
    long cost   = _upgradeTree.GetNodeCost(nodeId);
    bool canBuy = _upgradeTree.IsNodeAvailable(nodeId);

    nameLabel.text  = node.DisplayName;
    costLabel.text  = $"Maliyet: {FormatGold(cost)}";
    buyButton.interactable = canBuy && _economy.CurrentResources >= cost;
}
```

---

## 10. ADIM 9 — STEAM ENTEGRASYONU

### Kurulum

1. `Steamworks.NET` paketini ekle (`com.rlabrecque.steamworks.net`)
2. `steam_appid.txt` — proje kökünde, içinde App ID
3. `SteamService.Initialize()` — oyun başlangıcında

### Achievement

```csharp
// MilestoneConfigSO oluştur → MilestoneId'yi Steam API adıyla eşleştir
// SteamAchievementBridge.Initialize(steamService) çağır
// MilestoneTracker.OnMilestoneCompleted tetiklenince bridge otomatik açar:
MilestoneTracker.OnMilestoneCompleted += (milestoneSO) =>
{
    // Bridge bunu otomatik halleder, ayrıca yazman gerekmez
};

// Doğrudan açmak istersen:
_steam.UnlockAchievement("ACH_FIRST_PRESTIGE");
```

### Leaderboard

```csharp
// Skor gönder
steamLeaderboardService.SubmitScore("main_leaderboard", "PlayerName", (long)score);

// Liderlik tablosunu çek
steamLeaderboardService.FetchGlobalLeaderboard("main_leaderboard", 10, entries =>
{
    foreach (var e in entries)
        Debug.Log($"{e.Rank}. {e.PlayerName}: {e.Score}");
});
```

### Cloud Save

```csharp
// Başlangıçta (SaveService.LoadAsync'ten ÖNCE)
cloudSync.Initialize(saveService, steamService);
cloudSync.CheckForNewerCloudSave();

SteamCloudSaveSync.OnCloudSaveNewerThanLocal += () =>
{
    // Oyuncuya sor, onaylarsa:
    cloudSync.RestoreFromCloud(success => Debug.Log("Cloud yüklendi: " + success));
};
// Cloud'a yükleme OTOMATİK — SaveService.OnSaveCompleted tetiklendiğinde kendi yükler
```

---

## 11. TAM SENARYOLAR: HER OYUN TİPİ İÇİN BAŞLANGICTAN BİTİŞE

---

### SENARYO A — "Klasik Idle" (Pure Idle)
**Hedef:** Cookie Clicker / Adventure Capitalist tarzı

**Adım 1: Wizard**
`Tools → Endless Engine → New Game Wizard → Pure Idle → Generate`

**Adım 2: Bootstrap kontrolü**
Wizard'ın oluşturduğu `Bootstrap.cs`'i aç. Şunları içermeli:
- `BigNumberFactory.Configure()`
- `ConfigRegistry.InjectForTesting(economy, schema, prestige, realm)`
- `EconomyService.Initialize(upgradeTree, saveService)`
- `GeneratorSystem.Initialize(generators, economy, save)`
- `PassiveIncomeService.Initialize(generators, economy, null)`
- `saveService.RegisterStateProvider(...)` × 3
- `saveService.LoadAsync()`

Eksik varsa yukardaki Bölüm 1'deki şablonu kullan.

**Adım 3: Generator Database**
`Generator Editor` → 8-12 generator ekle. Her biri öncekinin ~10x maliyeti, ~5-8x yield'ı.

**Adım 4: Upgrade Tree**
`Upgrade Tree Editor` → 30-50 node ekle:
- `AffectedStat=GeneratorYield`, `EffectPerRank=0.10`, `MaxRank=5` (pasif gelir artışı)
- `AffectedStat=IdleYieldRate`, `EffectPerRank=0.15` (global gelir çarpanı)
- `PrestigeGateRequirement=1` olan daha güçlü node'lar (prestige sonrası açılır)

**Adım 5: Ekonomi dengeleme**
`Economy Simulator` → 30 oturum simüle et → ilk prestige 5-8. oturumda geliyorsa dengelidir.

**Adım 6: Prestige**
`PrestigeConfig.MinGoldToPrestige` = prestige anındaki gold'un ~80-90'ı
`PrestigeConfig.BaseMultiplierPerPrestige = 1.5`

**İçerik minimum (yayın için):**
10+ generator, 50+ upgrade node, 20+ prestige simüle edilmiş

---

### SENARYO B — "Wave RPG / Tower Defence"
**Hedef:** Idle Heroes / AFK Arena tarzı veya tower defence

**Adım 1: Wizard**
`Tools → Endless Engine → New Game Wizard → Idle-vs/RPG veya Tower Defense → Generate`

**Adım 2: Bootstrap kontrolü**
Wizard'ın bootstrap'ı Senaryo A'ya ek olarak şunları içermeli:
- `ConfigRegistry.InjectForTesting(...)` içinde `wave:` ve `player:` parametreleri de var mı?
- `WaveSpawnManager.Initialize(_enemyManager, null)` çağrısı var mı?
- `AutoBattleController.Initialize(...)` çağrısı var mı?
- `EnemyManager.OnEnemyKilled += OnEnemyKilledAddGold` **var mı?** → Bu yoksa düşmanlar para düşürmez!
- `EnemyManager.OnEnemyKilled += _autoBattle.HandleEnemyKilledByManager` var mı? → Bu yoksa wave'ler bitmez!
- `_saveService.RegisterStateProvider(_waveSpawnManager)` var mı?
- `_autoBattle.StartCombat()` `LoadAsync()`'ten sonra mı çağrılıyor?

**Adım 3: Wave Config**
```
WaveConfig:
  TotalWavesPerRun = 50
  BaseEnemyCountPerWave = 3
  EnemyCountScalingFactor = 1.12
  SpawnIntervalSeconds = 0.5
  WaveTransitionDelaySeconds = 2
  UpgradeSelectionWaveInterval = 5
```

**Adım 4: Enemy Stats**
```
EnemyStatConfig:
  BaseMaxHP = 20
  BaseAttackDamage = 5
  WaveScalingExponent = 1.5
```

**Adım 5: Player Stats**
```
PlayerBaseStatConfig:
  BaseAttackDamage = 10
  BaseAttackInterval = 1.0
  BaseMaxHP = 200
  BaseCritChance = 0.05
```

**Adım 6: Generator'lar**
5-8 generator ekle. Wave oyunlarında generator pasif gelir sağlar — dalga aralarında upgrade satın almak için kullanılır.

**Adım 7: Upgrade Tree — Savaş Odaklı**
- `AffectedStat=Damage`, `EffectPerRank=0.10` — hasar artışı
- `AffectedStat=MaxHP`, `EffectPerRank=0.15` — can artışı
- `AffectedStat=CritChance`, `EffectPerRank=0.02` — crit şansı
- `AffectedStat=GeneratorYield`, `EffectPerRank=0.10` — pasif gelir (dalga araları için)

**Adım 8: Wave Sonu Upgrade Kartı (isteğe bağlı)**
```csharp
WaveSpawnManager.OnUpgradeSelectionTriggered += () =>
{
    _autoBattle.StopCombat();
    var nodes = _upgradeTree.GetAvailableNodes().Take(3).ToArray();
    upgradeScreen.Show(nodes, chosen =>
    {
        _economyService.TryPurchase(chosen.NodeId);
        upgradeScreen.Hide();
        _autoBattle.NotifyUpgradeSelected();
    });
};
```

**Adım 9: Prestige**
```
PrestigeConfig:
  MinWaveForPrestige = 10
  BaseMultiplierPerPrestige = 2.0
```

**İçerik minimum:**
5+ generator, 40+ upgrade node (savaş ağırlıklı), 50+ wave simüle edilmiş

---

### SENARYO C — "Click Loop" (Aktif Clicker)
**Hedef:** Clicker Heroes / Tap Titans tarzı

**Adım 1:** `New Game Wizard → Click Loop → Generate`

**Adım 2:** 3 farklı ClickTargetConfigSO oluştur (farklı HP/yield/respawn):
- `Target_Rock`: MaxHP=5, BaseYield=1, RespawnSeconds=2
- `Target_Tree`: MaxHP=15, BaseYield=5, RespawnSeconds=4
- `Target_Crystal`: MaxHP=30, BaseYield=15, RespawnSeconds=8

**Adım 3:** ClickLoopConfig ayarla:
- `BaseCritChance = 0.05`, `MaxComboMultiplier = 5.0`, `BaseAutoClickRate = 0`

**Adım 4:** Sahnede her hedef için bir GameObject oluştur → `ClickTarget` component → config ata.

**Adım 5:** 5-8 pasif generator ekle (aktif tıklama yanında arka plan geliri).

**Adım 6:** Upgrade Tree — hem click hem pasif:
- `AffectedStat=ClickDamage`, `EffectPerRank=0.10`
- `AffectedStat=ClickCritChance`, `EffectPerRank=0.02`
- `AffectedStat=ClickAutoRate`, `EffectPerRank=0.5` (auto-click açılır)
- `AffectedStat=GeneratorYield`, `EffectPerRank=0.15`

---

### SENARYO D — "Merge Idle"
**Hedef:** Merge Dragons tarzı

**Adım 1:** `New Game Wizard → Merge Idle → Generate`

**Adım 2:** Merge zinciri tasarla — örnek: 5 tier'lı "coins" grubu:
- 5 adet ItemConfigSO oluştur: coin_t1 (T1) → coin_t5 (T5), MergeGroupId="coins" hepsinde
- 1 adet MergeConfigSO: MergeGroupId="coins", Rules listesine 4 kural ekle

**Adım 3:** MergeConfigSO'yu `MergeService` component'ının Inspector'ındaki alana sürükle.

**Adım 4:** Merge event'ini ekonomiye bağla:
```csharp
MergeService.OnMergeCompleted += (result) =>
{
    _economyService.AddResources(result.GoldBonus);
};
```

**Adım 5:** Pasif gelir için generator'lar ekle (merge olmadığı zamanlarda da para kazanılsın).

---

### SENARYO E — "Research Idle"
**Hedef:** Universal Paperclips / Kittens Game tarzı

**Adım 1:** `New Game Wizard → Research Idle → Generate`

**Adım 2:** 20-30 adet ResearchNodeConfigSO oluştur:
- Tier 0 (root): ResearchTicks=60, GoldCost=500
- Tier 1: ResearchTicks=300, GoldCost=5,000
- Tier 2: ResearchTicks=1,800, GoldCost=50,000 (30 dakika)
- Tier 3: ResearchTicks=7,200, GoldCost=500,000 (2 saat)

**Adım 3:** Her node'un `PrerequisiteIds` listesini doldur (araştırma ağacı oluştur).

**Adım 4:** Her node'un `Effects` listesine SkillEffect ekle.

**Adım 5:** Tüm node'ları ResearchTreeConfigSO.Nodes array'ine ekle.

**Adım 6:** Araştırma tamamlandığında sistem aç:
```csharp
ResearchService.OnNodeCompleted += (treeId, nodeId) =>
{
    if (nodeId == "unlock_factory")
        generatorSystem.UnlockGenerator("factory");
};
```

---

### SENARYO F — "Prestige-Heavy"
**Hedef:** Antimatter Dimensions / Realm Grinder tarzı

**Adım 1:** `New Game Wizard → Prestige-Heavy → Generate`

**Adım 2:** AscensionDatabaseSO — 3 katman:
- Katman 0: DisplayName="Prestige", LayerIndex=0, BaseMultiplierPerTrigger=1.5, RequiredPreviousLayerCount=0
- Katman 1: DisplayName="Ascension", LayerIndex=1, BaseMultiplierPerTrigger=3.0, RequiredPreviousLayerCount=10
- Katman 2: DisplayName="Transcension", LayerIndex=2, BaseMultiplierPerTrigger=10.0, RequiredPreviousLayerCount=5

**Adım 3:** Her katman için ayrı UpgradeTree oluştur (katmana özgü kalıcı bonuslar).

**Adım 4:** Economy Simulator'de 100 oturum simüle et. Çok katmanlı sistemlerde denge kritik.

**Adım 5:** Ascension butonu:
```csharp
void Update()
{
    bool canAscend = _ascensionManager.CanTrigger(1, _waveManager.CurrentWaveNumber);
    ascensionButton.interactable = canAscend;
    cascadeText.text = $"Cascade: ×{_ascensionManager.GetCascadeMultiplier():F2}";
}
public void OnAscensionClick()
    => _ascensionManager.TryTrigger(1, _waveManager.CurrentWaveNumber);
```

---

## GENEL SORUN GİDERME

| Sorun | Nereye Bak |
|-------|-----------|
| Düşmanlar para düşürmüyor | Bootstrap'ta `EnemyManager.OnEnemyKilled += OnEnemyKilledAddGold` var mı? |
| Wave bir türlü bitmiyor | `EnemyManager.OnEnemyKilled += _autoBattle.HandleEnemyKilledByManager` var mı? |
| Upgrade satın aldım ama combat değişmedi | `_autoBattle.NotifyUpgradeSelected()` çağrıldı mı? Upgrade tree `HandleConfigsLoaded()` çağrıldı mı? |
| Kayıt yüklenmiyor | `saveService.LoadAsync()` `yield return` ile bekleniyor mu? `RegisterStateProvider` çağrıldı mı? |
| Config değiştirdim görünmüyor | Play modundan ÇIKIP config değiştirip yeniden Play bas |
| Oyun çok hızlı bitiyor | `CostScalingFactor` artır (1.15 → 1.20), daha fazla upgrade node ekle |
| Oyun çok yavaş / sıkıcı | İlk generator maliyetini düşür, `MinGoldToPrestige` düşür |
| Ekonomi patlıyor | `ResourceHardCap` artır, `NumberBackend = BigDouble` yap |
| ID çakışması uyarısı | `Tools → Endless Engine → ID Registry` |
| Yeni alan ekledim kayıt bozuldu | `Tools → Endless Engine → Bump Schema Version` + migration dosyasını doldur |
| BigDouble ne zaman lazım | Altın 1 katrilyon (1e15) üzerini geçecekse |

---

## STEAM YAYINI ÖNCESİ KONTROL LİSTESİ

- [ ] Economy Simulator'de 50+ oturum simüle edildi, denge sağlandı
- [ ] Config Validator'da sıfır hata (`Tools → Endless Engine → Config Validator`)
- [ ] ID Registry'de sıfır çakışma (`Tools → Endless Engine → ID Registry`)
- [ ] Play → Stop → Play → veri kaldı mı? (kayıt testi)
- [ ] Zaman atla, geri dön → çevrimdışı gelir geldi mi?
- [ ] Prestige sırasında Play durdur → geri dönünce crash recovery çalıştı mı?
- [ ] Wave oyunları: düşman öldürünce para düşüyor mu?
- [ ] Wave oyunları: wave tamamlandığında bir sonraki başlıyor mu?
- [ ] Upgrade satın alındıktan sonra combat/gelir değişti mi?
- [ ] `SteamAchievementBridge._mappings` dolduruldu (milestone ID → Steam API adı)
- [ ] `cloudSync.CheckForNewerCloudSave()` oyun başlangıcında çağrılıyor
- [ ] Build Settings'te doğru sahne var
- [ ] Debug log'ları kapatıldı (Development Build only)

---

*Endless Engine v1.3.4 — Kaynak koddan doğrulanmış (WaveIdleBootstrap.cs, MinimalIdleBootstrap.cs, VerticalSliceBootstrap.cs, AutoBattleController.cs, WaveSpawnManager.cs, EnemyManager.cs)*
