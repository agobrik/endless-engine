# REHBER 02 — Wave RPG / Tower Defence Idle
## Idle Heroes / AFK Arena Tarzı
### Endless Engine ile Sıfırdan Steam'e

> Bu rehberi takip ederek, hiçbir şey bilmesen bile otomatik savaşan, dalga dalga ilerleyen, upgrade ve prestige içeren tam bir idle RPG / tower defence oyunu yapabilirsin.

---

## BU OYUNDA NE VAR?

| Sistem | Ne Yapar |
|--------|----------|
| Otomatik Savaş | Oyuncu savaşmaz, kahraman otomatik saldırır |
| Dalga Sistemi | Her dalga daha fazla ve güçlü düşman |
| Düşman Ölünce Para | Savaştan altın gelir |
| Generator'lar | Dalga aralarında pasif gelir sağlar |
| Upgrade Seçimi | Her N. dalgada kartlardan seçim |
| Prestige | Minimum dalga şartıyla prestige |
| Kayıt | Dalga numarası da kaydedilir |

---

## ADIM 1 — PROJEYİ HAZIRLA

### 1.1 Unity Projesi

1. Unity Hub → **New Project** → **2D (URP)**
2. Proje adı: `WaveIdle`
3. **Create Project**
4. `Window → Package Manager → + → Add package from disk` → `package.json` seç

### 1.2 Wizard ile İskelet Oluştur

`Tools → Endless Engine → New Game Wizard`
1. **Idle-vs / RPG** seç (Tower Defence için de aynı şablon)
2. Game Name = `WaveIdle`
3. **Generate** bas

Wizard oluşturur:
```
Assets/WaveIdle/
    Configs/
        EconomyConfig.asset
        WaveConfig.asset
        EnemyStatConfig.asset
        PlayerBaseStatConfig.asset
        PrestigeConfig.asset
        GeneratorDatabase.asset
        UpgradeTreeConfig.asset
        SchemaVersion.asset
        RealmIdentityConfig.asset
    Scenes/
        WaveIdle.unity
    Scripts/
        WaveIdleBootstrap.cs
        WaveIdleUI.cs
```

Sahneyi aç → Play → çalışıyor mu kontrol et.

---

## ADIM 2 — SİSTEMLER VE PARA AKIŞI

Wave oyunlarında iki farklı para kaynağı vardır. Bu ikisini anlamak kritik:

```
KAYNAK 1 — Pasif Gelir (Generator'lar):
TickEngine (saniyede 1 kez) →
    PassiveIncomeService.HandleTick() →
        GeneratorSystem.CalculateTotalYieldBig() →
            EconomyService.AddResources(miktar)

KAYNAK 2 — Aktif Gelir (Düşman Öldürme):
EnemyManager.Update() → Düşman HP ≤ 0 →
    EnemyManager.OnEnemyKilled event tetiklenir →
        Bootstrap'taki handler: EnemyManager.OnEnemyKilled += OnEnemyKilledAddGold →
            EconomyService.AddResources(agent.GoldDropAmount)
```

**ÖNEMLİ:** `EnemyManager.OnEnemyKilled += OnEnemyKilledAddGold` satırı olmadan düşmanlar HİÇ para düşürmez. Bootstrap'ta mutlaka bu bağlantıyı kontrol et.

### Upgrade Combat'ı Nasıl Etkiler?

```
Upgrade satın alındı →
    EconomyService.OnUpgradePurchased tetiklendi →
        UpgradeTreeService rank artırdı →
            AutoBattleController, dalga bitişinde CacheStats() çağırır →
                _effectiveAttackDamage yeni stat'tan hesaplanır →
                    Bir sonraki saldırıda yeni hasar uygulanır
```

Upgrade anında etki etmez — bir sonraki dalga geçişinde veya upgrade seçiminin ardından devreye girer.

---

## ADIM 3 — WAVE CONFIG AYARLA

`Assets/WaveIdle/Configs/WaveConfig.asset` → Inspector:

| Field | Başlangıç Değeri | Açıklama |
|-------|-----------------|----------|
| `TotalWavesPerRun` | 50 | Run başına toplam dalga |
| `BaseEnemyCountPerWave` | 3 | Dalga 1'de 3 düşman |
| `EnemyCountScalingFactor` | 1.12 | Her dalgada %12 daha fazla düşman |
| `HardCapEnemiesOnScreen` | 20 | Aynı anda max 20 düşman |
| `SpawnIntervalSeconds` | 0.5 | Düşmanlar arası 0.5s aralık |
| `WaveTransitionDelaySeconds` | 2.0 | Dalgalar arası 2 saniye mola |
| `WaveDurationSeconds` | 120 | Dalga 2 dakikada bitmezse zorla geç |
| `UpgradeSelectionWaveInterval` | 5 | Her 5. dalgada upgrade kart seçimi |
| `WaveSaveMilestoneInterval` | 10 | Her 10. dalgada otomatik kayıt |
| `EliteWaveInterval` | 10 | Her 10. dalgada elite düşman |
| `EliteStatMultiplier` | 3.0 | Elite'in 3x canı ve hasarı var |
| `BossWaveInterval` | 25 | Her 25. dalgada boss |

### Dalga Güçlük Eğrisi

Oyun boyunca düşman sayısı şöyle artar:
- Dalga 1: 3 düşman
- Dalga 10: 3 × 1.12^9 ≈ 8 düşman
- Dalga 25: 3 × 1.12^24 ≈ 29 düşman → HardCap = 20'ye takılır
- Dalga 50: teorik 90+ ama HardCap devrede

**HardCap neden önemli?** Çok fazla düşman sahneyi yavaşlatır. 20-30 yeterli.

---

## ADIM 4 — DÜŞMAN STAT CONFIG

`Assets/WaveIdle/Configs/EnemyStatConfig.asset`:

| Field | Başlangıç Değeri | Açıklama |
|-------|-----------------|----------|
| `BaseMaxHP` | 20 | Dalga 1 düşman canı |
| `BaseAttackDamage` | 5 | Dalga 1 saldırı hasarı |
| `BaseContactDamage` | 2 | Oyuncuya yakın gelirse temas hasarı |
| `MoveSpeed` | 3.0 | Saniyede 3 world unit |
| `AttackRange` | 2.0 | 2 world unit menzili |
| `AttackInterval` | 1.5 | 1.5 saniyede bir saldırı |
| `WaveScalingExponent` | 1.5 | Dalga N'de HP = BaseMaxHP × N^1.5 |
| `HardCapEnemiesOnScreen` | 200 | Motordaki mutlak sınır |

### Dalga Başına Düşman HP Örneği (WaveScalingExponent=1.5)

| Dalga | HP |
|-------|-----|
| 1 | 20 |
| 5 | 20 × 5^1.5 ≈ 224 |
| 10 | 20 × 10^1.5 ≈ 632 |
| 25 | 20 × 25^1.5 ≈ 2,500 |
| 50 | 20 × 50^1.5 ≈ 7,071 |

Oyuncunun hasar upgrade'leri bu büyümeyle rekabet edebilmeli.

### Düşman Altın Dropu

`EconomyConfig.asset`'teki şu alanlar düşman altın dropunu belirler:
- `BaseGoldDropPerEnemy` = 1 (dalga 1'de düşman başına 1 altın)
- `GoldDropScalingExponent` = 1.2 (dalga N'de = 1 × N^1.2)

Dalga 10'da düşman başına ≈ 16 altın düşer.

---

## ADIM 5 — OYUNCU STAT CONFIG

`Assets/WaveIdle/Configs/PlayerBaseStatConfig.asset`:

| Field | Değer | Açıklama |
|-------|-------|----------|
| `BaseMaxHP` | 200 | Oyuncu başlangıç canı |
| `BaseAttackDamage` | 10 | Temel saldırı hasarı |
| `BaseAttackInterval` | 1.0 | Saniyede 1 saldırı |
| `BaseAttackRange` | 5.0 | 5 world unit menzil |
| `BaseCritChance` | 0.05 | %5 crit şansı |
| `BaseCritMultiplier` | 2.0 | Crit 2x hasar |
| `BaseMoveSpeed` | 5.0 | — (tower defence'de kullanılmaz) |
| `AttackTargetUpdateInterval` | 0.1 | Her 0.1s en yakın düşmanı güncelle |

### Hasar Dengesi Nasıl Yapılır?

Oyuncu hasar artış upgrade'leri:
- `AffectedStat = Damage`, `EffectPerRank = 0.15` (her rankta %15)
- 5 rank → toplam %75 artış → `10 × 1.75 = 17.5` efektif hasar

Dalga 10 düşmanı 632 HP. Oyuncu 17.5 hasar/sn verirse:
- 632 / 17.5 ≈ 36 saniye → 1 düşman öldürmek için çok uzun!

Bu yüzden dalga ilerledikçe oyuncunun daha fazla upgrade aldığını varsay. Simülatörde test et.

---

## ADIM 6 — GENERATOR'LAR (Dalga Oyunu için)

Wave oyunlarında generator'lar ikincil ama önemli: dalga aralarında pasif gelir sağlar, bu gelirle upgrade satın alınır.

`Tools → Endless Engine → Generator Editor`

Wave oyunu için 5-8 hafif generator öner:

| # | GeneratorId | DisplayName | BaseYieldPerSecond | BaseCost | CostScale |
|---|------------|-------------|-------------------|---------|-----------|
| 1 | `tavern` | Meyhane | 0.5 | 50 | 1.12 |
| 2 | `barracks` | Kışla | 3 | 400 | 1.12 |
| 3 | `market` | Çarşı | 20 | 4,000 | 1.12 |
| 4 | `castle` | Kale | 150 | 50,000 | 1.12 |
| 5 | `dragon_lair` | Ejderha Yuvası | 1,200 | 700,000 | 1.12 |

---

## ADIM 7 — UPGRADE TREE (Savaş Odaklı)

`Tools → Endless Engine → Upgrade Tree Editor`

Wave oyunları için upgrade tree iki gruba ayrılır:

### Grup A — Savaş Upgrade'leri (Dalga Kart Seçimlerinde Çıkar)

```
NodeId: dmg_01
DisplayName: "Keskin Kılıç"
AffectedStat: Damage
EffectType: PercentBonus
EffectPerRank: 0.10
MaxRank: 10
BaseCost: 200
CostScalingFactor: 1.8
SelectionWeight: 60    ← Sık çıkar
```

```
NodeId: hp_01
DisplayName: "Demir Zırh"
AffectedStat: MaxHP
EffectType: PercentBonus
EffectPerRank: 0.15
MaxRank: 10
BaseCost: 150
CostScalingFactor: 1.8
SelectionWeight: 50
```

```
NodeId: crit_01
DisplayName: "Göz Kararması"
AffectedStat: CritChance
EffectType: FlatBonus
EffectPerRank: 0.02    ← +%2 crit şansı / rank
MaxRank: 15
BaseCost: 500
CostScalingFactor: 2.0
SelectionWeight: 30
```

```
NodeId: crit_dmg_01
DisplayName: "Ölümcül Vuruş"
AffectedStat: CritMultiplier
EffectType: FlatBonus
EffectPerRank: 0.25    ← +0.25x crit çarpanı / rank
MaxRank: 10
BaseCost: 800
CostScalingFactor: 2.0
SelectionWeight: 20
PrerequisiteNodeIDs: ["crit_01"]
```

```
NodeId: atk_speed_01
DisplayName: "Hız Ayakkabısı"
AffectedStat: AttackInterval
EffectType: PercentBonus
EffectPerRank: -0.05   ← Negatif = interval kısalır = daha hızlı
MaxRank: 10
BaseCost: 600
CostScalingFactor: 2.0
SelectionWeight: 25
```

### Grup B — Ekonomi Upgrade'leri (Satın Alınır, Kart Seçiminde Nadiren Çıkar)

```
NodeId: gold_drop_01
DisplayName: "Ganimetçi"
AffectedStat: GoldDropMultiplier
EffectType: PercentBonus
EffectPerRank: 0.20
MaxRank: 5
BaseCost: 1,000
CostScalingFactor: 2.5
SelectionWeight: 10
```

```
NodeId: gen_yield_01
DisplayName: "Loncalar"
AffectedStat: GeneratorYield
EffectType: PercentBonus
EffectPerRank: 0.15
MaxRank: 5
BaseCost: 2,000
CostScalingFactor: 2.0
SelectionWeight: 5
```

### Grup C — Prestige Bonusları (PrestigeGateRequirement=1)

```
NodeId: pres_dmg_01
DisplayName: "Efsanevi Kahraman"
AffectedStat: Damage
EffectType: PercentBonus
EffectPerRank: 0.20
MaxRank: 10
BaseCost: 5,000
CostScalingFactor: 2.0
PrestigeGateRequirement: 1
SelectionWeight: 40
```

---

## ADIM 8 — PRESTİGE CONFIG

`Assets/WaveIdle/Configs/PrestigeConfig.asset`:

| Field | Değer | Açıklama |
|-------|-------|----------|
| `MinWaveForPrestige` | 10 | Dalga 10'a ulaşmadan prestige yok |
| `MinGoldToPrestige` | 0 | Altın şartı yok (dalga şartı yeterli) |
| `MaxPrestigeCount` | 0 | Sınırsız |
| `BaseMultiplierPerPrestige` | 2.0 | Her prestige'de ×2 kalıcı çarpan |
| `MaxPermanentMultiplier` | 50,000 | 50K tavan |
| `StatsAmplifiedByPrestige` | [Damage, MaxHP] | Bu stat'lar prestige çarpanından etkilenir |

---

## ADIM 9 — BOOTSTRAP YAPI VE WIRING

Wave oyunlarının bootstrap'ı Pure Idle'dan daha karmaşık. Her satırın ne yaptığını anlayarak kur.

### 9.1 Bootstrap Script Şablonu

`Assets/WaveIdle/Scripts/WaveIdleBootstrap.cs`:

```csharp
using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.Generator;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Upgrade;
using EndlessEngine.Wave;
using EndlessEngine.Combat;
using EndlessEngine.Enemy;
using EndlessEngine.Prestige;

[DefaultExecutionOrder(-500)]
public class WaveIdleBootstrap : MonoBehaviour
{
    [Header("Core Servisler")]
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

    [Header("Config Asset'leri")]
    [SerializeField] private EconomyConfigSO        _economyConfig;
    [SerializeField] private WaveConfigSO           _waveConfig;
    [SerializeField] private PlayerBaseStatConfigSO _playerConfig;
    [SerializeField] private PrestigeConfigSO       _prestigeConfig;
    [SerializeField] private GeneratorDatabaseSO    _generatorDatabase;
    [SerializeField] private SchemaVersionSO        _schemaVersion;
    [SerializeField] private RealmIdentityConfigSO  _realmConfig;

    private IEnumerator Start()
    {
        // ═══════════════════════════════════════════════════════
        // ADIM 1: Sayı motorunu ayarla — HER ŞEYDEN ÖNCE
        // ═══════════════════════════════════════════════════════
        BigNumberFactory.Configure(_economyConfig.NumberBackend);

        // ═══════════════════════════════════════════════════════
        // ADIM 2: Config'leri yükle
        // Wave ve Player config'leri de dahil!
        // ═══════════════════════════════════════════════════════
        ConfigRegistry.InjectForTesting(
            economy:  _economyConfig,
            wave:     _waveConfig,      // ← Wave oyununda bu şart
            player:   _playerConfig,    // ← Combat stats için şart
            prestige: _prestigeConfig,
            schema:   _schemaVersion,
            realm:    _realmConfig);

        // ═══════════════════════════════════════════════════════
        // ADIM 3: UpgradeTree hazırla
        // ═══════════════════════════════════════════════════════
        _upgradeTreeService?.HandleConfigsLoaded();

        // ═══════════════════════════════════════════════════════
        // ADIM 4: Ekonomi
        // ═══════════════════════════════════════════════════════
        _economyService.Initialize(_upgradeTreeService, _saveService);

        // ═══════════════════════════════════════════════════════
        // ADIM 5: Generator'lar
        // ═══════════════════════════════════════════════════════
        _generatorSystem.Initialize(
            _generatorDatabase.Generators,
            _economyService,
            _saveService);

        // ═══════════════════════════════════════════════════════
        // ADIM 6: Pasif gelir
        // ═══════════════════════════════════════════════════════
        _passiveIncomeService.Initialize(
            _generatorSystem,
            _economyService,
            gameFlow: null);

        // ═══════════════════════════════════════════════════════
        // ADIM 7: Wave spawner — EnemyManager'ı alır
        // WaveConfig ve EnemyConfig'i ConfigRegistry'den okur
        // ═══════════════════════════════════════════════════════
        _waveSpawnManager.Initialize(_enemyManager, saveNotifier: null);

        // ═══════════════════════════════════════════════════════
        // ADIM 8: AutoBattle — combat istatistiklerini okur
        // ═══════════════════════════════════════════════════════
        var statProvider = new BaseStatUpgradeProvider(_playerConfig);
        _autoBattle.Initialize(
            enemyManager:     _enemyManager,
            waveSpawnManager: _waveSpawnManager,
            statProvider:     statProvider,
            playerConfig:     _playerConfig,
            waveConfig:       _waveConfig,
            playerId:         1);

        // ═══════════════════════════════════════════════════════
        // ADIM 9: KRİTİK EVENT BAĞLANTILARI
        // Bu iki satır olmadan oyun çalışmaz:
        // - İlki: düşman ölünce para ekler
        // - İkincisi: wave'in bitmesini takip eder
        // ═══════════════════════════════════════════════════════
        EnemyManager.OnEnemyKilled += OnEnemyKilledAddGold;
        EnemyManager.OnEnemyKilled += _autoBattle.HandleEnemyKilledByManager;

        // ═══════════════════════════════════════════════════════
        // ADIM 10: Kayıt sağlayıcıları
        // Sıra önemli! Önce economy, sonra generator, wave en son
        // ═══════════════════════════════════════════════════════
        _saveService.RegisterStateProvider(_economyService);        // Order 10
        _saveService.RegisterStateProvider(_upgradeTreeService);    // Order 20
        _saveService.RegisterStateProvider(_generatorSystem);       // Order 15
        _saveService.RegisterStateProvider(_waveSpawnManager);      // Order 40
        _saveService.RegisterStateProvider(_prestigeManager);       // Order 30

        // ═══════════════════════════════════════════════════════
        // ADIM 11: Kayıt yükle — TÜM KAYITLAR YÜKLENİNCE oyun başlar
        // ═══════════════════════════════════════════════════════
        bool done = false;
        _ = _saveService.LoadAsync().ContinueWith(_ => done = true,
            System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        yield return new WaitUntil(() => done);

        // ═══════════════════════════════════════════════════════
        // ADIM 12: Savaşı başlat — MUTLAKA LoadAsync'ten SONRA
        // StartCombat() içinde wave de başlar
        // ═══════════════════════════════════════════════════════
        _autoBattle.StartCombat();

        Debug.Log("[WaveIdle] Hazır. Savaş başlıyor!");
    }

    private void OnDestroy()
    {
        // Event'leri temizle — sahne yeniden yüklendiğinde çift bağlantı olmasın
        EnemyManager.OnEnemyKilled -= OnEnemyKilledAddGold;
        EnemyManager.OnEnemyKilled -= _autoBattle.HandleEnemyKilledByManager;
    }

    // Bu metod olmadan düşmanlar PARA DÜŞÜRMEZ
    private void OnEnemyKilledAddGold(EnemyAgent agent)
        => _economyService?.AddResources(agent.GoldDropAmount);
}
```

### 9.2 Sahne Hiyerarşisi

```
Bootstrap (GameObject)
    ├── WaveIdleBootstrap    (component)
    ├── SaveService          (component)
    ├── EconomyService       (component)
    ├── UpgradeTreeService   (component)
    ├── GeneratorSystem      (component)
    ├── PassiveIncomeService (component)
    ├── TickEngine           (component)
    ├── AutoBattleController (component)
    ├── WaveSpawnManager     (component)
    │       └── _enemyPrefab → EnemyPrefab.prefab (sürükle)
    ├── EnemyManager         (component)
    └── PrestigeStateManager (component)

Arena (GameObject)
    ├── Player (GameObject)
    │       └── SpriteRenderer + PlayerHealthComponent
    └── SpawnPoints (GameObject)
            ├── SpawnPoint_Left
            ├── SpawnPoint_Right
            └── SpawnPoint_Top

Canvas (UI)
    └── WaveIdleUI (component)
```

### 9.3 Inspector Alanlarını Doldur

Bootstrap GameObject'ini seç → Inspector:

**Core Servisler:**
- `_saveService` → Bootstrap GameObject'indeki SaveService component'ını sürükle
- `_economyService` → EconomyService sürükle
- `_upgradeTreeService` → UpgradeTreeService sürükle
- `_generatorSystem` → GeneratorSystem sürükle
- `_passiveIncomeService` → PassiveIncomeService sürükle

**Wave & Combat:**
- `_autoBattle` → AutoBattleController sürükle
- `_waveSpawnManager` → WaveSpawnManager sürükle
- `_enemyManager` → EnemyManager sürükle
- `_prestigeManager` → PrestigeStateManager sürükle

**Config Asset'leri:**
- `_economyConfig` → `Assets/WaveIdle/Configs/EconomyConfig.asset`
- `_waveConfig` → `Assets/WaveIdle/Configs/WaveConfig.asset`
- `_playerConfig` → `Assets/WaveIdle/Configs/PlayerBaseStatConfig.asset`
- (diğerleri de aynı şekilde)

**WaveSpawnManager'ın kendi Inspector alanları:**
- `_enemyPrefab` → EnemyPrefab prefab'ını sürükle (Packages içinde default prefab var)

---

## ADIM 10 — UI YAPISI

### 10.1 Ana HUD Elemanları

Canvas'a şu UI elemanlarını ekle (TMP_Text veya standard Text):

```
Canvas
    ├── TopBar
    │       ├── GoldLabel          (Text: "Altın: 0")
    │       ├── IncomeLabel        (Text: "Gelir: 0/s")
    │       └── WaveLabel          (Text: "Dalga: 1")
    ├── PlayerPanel
    │       ├── PlayerHPBar        (Slider, max=1)
    │       └── PlayerHPLabel      (Text: "200/200")
    ├── PrestigePanel
    │       ├── PrestigeButton     (Button)
    │       ├── PrestigeMultLabel  (Text: "×1.00")
    │       └── PrestigeCountLabel (Text: "Prestige: 0")
    ├── GeneratorPanel
    │       └── (Generator kartları Scroll View içinde)
    ├── UpgradePanel
    │       └── (Upgrade kartları Scroll View içinde)
    └── UpgradeSelectionScreen (başta kapalı)
            └── (3 upgrade kart butonu)
```

### 10.2 Wave UI Script

```csharp
public class WaveIdleUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _goldLabel;
    [SerializeField] private TMP_Text _incomeLabel;
    [SerializeField] private TMP_Text _waveLabel;
    [SerializeField] private Slider   _playerHPBar;
    [SerializeField] private TMP_Text _playerHPLabel;

    private void Start()
    {
        EconomyService.OnResourcesChanged  += OnGoldChanged;
        WaveSpawnManager.OnWaveStarted     += OnWaveStarted;
        WaveSpawnManager.OnWaveComplete    += OnWaveComplete;
        PrestigeStateManager.OnPrestigeComplete += OnPrestigeComplete;
    }

    private void OnDestroy()
    {
        EconomyService.OnResourcesChanged  -= OnGoldChanged;
        WaveSpawnManager.OnWaveStarted     -= OnWaveStarted;
        WaveSpawnManager.OnWaveComplete    -= OnWaveComplete;
        PrestigeStateManager.OnPrestigeComplete -= OnPrestigeComplete;
    }

    private void OnGoldChanged(double current, double delta)
    {
        _goldLabel.text  = $"Altın: {FormatGold(current)}";
        _incomeLabel.text = delta > 0 ? $"+{FormatGold(delta)}" : "";
    }

    private void OnWaveStarted(int wave)
    {
        _waveLabel.text = $"Dalga {wave}";
    }

    private void OnWaveComplete(int wave)
    {
        _waveLabel.text = $"Dalga {wave} Tamamlandı!";
    }

    private void OnPrestigeComplete(int count, float multiplier)
    {
        Debug.Log($"Prestige {count}! Çarpan: ×{multiplier:F2}");
    }

    private string FormatGold(double n)
    {
        if (n >= 1e9) return $"{n/1e9:F2}B";
        if (n >= 1e6) return $"{n/1e6:F2}M";
        if (n >= 1e3) return $"{n/1e3:F1}K";
        return $"{n:F0}";
    }
}
```

### 10.3 Dalga Sonu Upgrade Seçimi

Her `UpgradeSelectionWaveInterval` dalgada sistem `WaveSpawnManager.OnUpgradeSelectionTriggered` tetikler. UI bu event'i dinleyip 3 kart gösterir:

```csharp
public class UpgradeSelectionScreen : MonoBehaviour
{
    [SerializeField] private GameObject _panel;
    [SerializeField] private UpgradeCardButton[] _cardSlots;  // 3 slot

    private AutoBattleController _autoBattle;
    private UpgradeTreeService   _upgradeTree;
    private EconomyService       _economy;

    public void Inject(AutoBattleController ab, UpgradeTreeService ut, EconomyService eco)
    {
        _autoBattle  = ab;
        _upgradeTree = ut;
        _economy     = eco;

        WaveSpawnManager.OnUpgradeSelectionTriggered += ShowUpgradeSelection;
    }

    private void OnDestroy()
    {
        WaveSpawnManager.OnUpgradeSelectionTriggered -= ShowUpgradeSelection;
    }

    private void ShowUpgradeSelection()
    {
        // Savaşı durdur
        _autoBattle.StopCombat();

        // 3 rastgele uygun node seç (SelectionWeight'e göre ağırlıklı)
        var nodes = _upgradeTree.GetAvailableNodes()
            .OrderBy(_ => Random.value)  // Gerçek ağırlık için WeightedRandom kullan
            .Take(3)
            .ToArray();

        // Kartları doldur
        for (int i = 0; i < _cardSlots.Length && i < nodes.Count; i++)
            _cardSlots[i].Setup(nodes[i], OnCardSelected);

        _panel.SetActive(true);
    }

    private void OnCardSelected(UpgradeNode chosen)
    {
        _economy.TryPurchase(chosen.NodeId);
        _panel.SetActive(false);
        _autoBattle.NotifyUpgradeSelected();  // Savaşı yeniden başlat
    }
}
```

---

## ADIM 11 — EKONOMİ DENGESİ TEST ET

`Tools → Endless Engine → Economy Simulator`

Wave oyunlarında denge farklı çalışır çünkü altın iki yerden gelir.

| Parametre | Değer |
|-----------|-------|
| Sessions | 30 |
| Session length | 20 dakika |
| Waves per session | 15-20 |
| Auto-prestige | ✓ |

**Hedef:**
- Dalga 10'a ulaşmak ilk oturumda mümkün olmalı
- İlk prestige 2-3. oturumda gelmeli
- Her prestige daha hızlı ilerletmeli

---

## ADIM 12 — GÖRSEL KURULUM

### 12.1 Player Sprite

1. Player GameObject → `SpriteRenderer` → Sprite ata (basit bir karakter silueti)
2. `PlayerHealthComponent` → MaxHP = 200 (PlayerBaseStatConfig'le eşleşmeli)
3. `Rigidbody2D` → Body Type = **Kinematic** (AutoBattle fizik kullanmaz)

### 12.2 Enemy Prefab

WaveSpawnManager'ın kullandığı `_enemyPrefab` Inspector alanına bir prefab ata:

```
EnemyPrefab (Prefab)
    ├── SpriteRenderer (düşman görüntüsü)
    ├── Rigidbody2D (Body Type = Kinematic)
    └── CircleCollider2D (Is Trigger = true)
```

> Default prefab: `Packages/com.endlessengine.idle/Runtime/Enemy/Prefabs/DefaultEnemy.prefab` kullanabilirsin.

### 12.3 Arena

```
Arena (GameObject)
    ├── Background (SpriteRenderer — arka plan resmi)
    ├── PlayerSpawn (Transform — oyuncu spawn pozisyonu)
    └── EnemySpawnEdge (Collider2D — düşmanlar bu kenardan gelir)
```

WaveSpawnManager spawn pozisyonlarını arena sınırından hesaplar — özel bir setup gerekmez.

---

## ADIM 13 — PRESTIGE VE DALGA SONU

### 13.1 Prestige Butonu

Oyuncu dalga 10'a ulaştığında prestige butonu aktifleşir:

```csharp
public class PrestigeButton : MonoBehaviour
{
    [SerializeField] private Button   _button;
    [SerializeField] private TMP_Text _descText;

    private PrestigeStateManager _prestige;
    private WaveSpawnManager     _waves;

    public void Inject(PrestigeStateManager prestige, WaveSpawnManager waves)
    {
        _prestige = prestige;
        _waves    = waves;
        WaveSpawnManager.OnWaveStarted += OnWaveChanged;
        UpdateUI();
    }

    private void Update()
    {
        _button.interactable = _prestige.CanPrestige;
    }

    private void OnWaveChanged(int wave) => UpdateUI();

    private void UpdateUI()
    {
        int wave        = _waves?.CurrentWaveNumber ?? 0;
        int minWave     = 10;  // PrestigeConfig.MinWaveForPrestige
        float mult      = _prestige?.GetPermanentMultiplier() ?? 1f;

        if (wave < minWave)
            _descText.text = $"Dalga {minWave}'e ulaş ({wave}/{minWave})";
        else
            _descText.text = $"×{mult:F2} çarpanı kazan";
    }

    public void OnPrestigeClick()
    {
        if (_prestige.CanPrestige)
            _prestige.TryPrestige();
    }
}
```

### 13.2 Prestige Sıfırlama

`PrestigeStateManager.OnPrestigeStarted` tetiklendiğinde otomatik sıfırlanır:
- Altın → `StartingGold`'a döner
- Generator sayıları → 0
- Upgrade rank'ları → 0
- Dalga → 1
- **Kalan:** Prestige sayısı + kalıcı çarpan + `PrestigeGateRequirement ≥ 1` olan upgrade'ler

---

## ADIM 14 — YAYINA HAZIRLIK VE TEST

### Fonksiyonel Testler

- [ ] Play → düşmanlar spawn oluyor
- [ ] Düşman öldürünce altın artıyor
- [ ] Wave bitince yeni wave başlıyor
- [ ] Her 5. dalgada upgrade ekranı çıkıyor
- [ ] Upgrade seçince combat devam ediyor
- [ ] Dalga 10'da prestige butonu aktifleşiyor
- [ ] Prestige yapınca generator, altın, upgrade sıfırlanıyor
- [ ] Prestige sonrası kalıcı çarpan kalıcı
- [ ] Play Stop Play → dalga numarası geri geliyor
- [ ] Generator satın alınca pasif gelir artıyor

### Performans

- [ ] 50 düşman sahnede → FPS ≥ 30 (mobil hedef varsa)
- [ ] `HardCapEnemiesOnScreen` değerini test et

### Yaygın Sorunlar

| Sorun | Neden | Çözüm |
|-------|-------|-------|
| Düşmanlar para düşürmüyor | `OnEnemyKilled += OnEnemyKilledAddGold` eksik | Bootstrap'a ekle |
| Wave bir türlü bitmiyor | `OnEnemyKilled += _autoBattle.HandleEnemyKilledByManager` eksik | Bootstrap'a ekle |
| Upgrade kart çıkmıyor | `OnUpgradeSelectionTriggered` dinlenmiyor | UI script'e ekle |
| Savaş LoadAsync'ten önce başlıyor | `StartCombat()` yanlış yerde | `yield return WaitUntil(done)` sonrasına taşı |
| Dalga kaydedilmiyor | `RegisterStateProvider(_waveSpawnManager)` eksik | Bootstrap'a ekle |

---

## HIZLI BAŞVURU

### Bootstrap Kontrol Listesi
```
✓ BigNumberFactory.Configure()
✓ ConfigRegistry.InjectForTesting(economy, wave, player, prestige, ...)
✓ upgradeTreeService.HandleConfigsLoaded()
✓ economyService.Initialize(upgradeTree, save)
✓ generatorSystem.Initialize(configs, economy, save)
✓ passiveIncomeService.Initialize(generators, economy, null)
✓ waveSpawnManager.Initialize(enemyManager, null)
✓ autoBattle.Initialize(enemyManager, waveSpawnManager, statProvider, playerConfig, waveConfig, 1)
✓ EnemyManager.OnEnemyKilled += OnEnemyKilledAddGold       ← ŞART
✓ EnemyManager.OnEnemyKilled += autoBattle.HandleEnemyKilledByManager  ← ŞART
✓ saveService.RegisterStateProvider(economyService)
✓ saveService.RegisterStateProvider(upgradeTreeService)
✓ saveService.RegisterStateProvider(generatorSystem)
✓ saveService.RegisterStateProvider(waveSpawnManager)
✓ saveService.RegisterStateProvider(prestigeManager)
✓ await saveService.LoadAsync()
✓ autoBattle.StartCombat()
```

### Para Akışı Özeti
```
Pasif:    TickEngine → PassiveIncomeService → EconomyService
Aktif:    EnemyManager.OnEnemyKilled → EconomyService.AddResources(agent.GoldDropAmount)
Harcama:  EconomyService.TryPurchase(nodeId) → upgrade satın alma
```

---

*Bu rehber Endless Engine v1.3.4 için yazılmıştır.*
