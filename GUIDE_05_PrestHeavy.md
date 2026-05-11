# REHBER 05 — Prestige-Heavy / Çok Katmanlı Prestige
## Antimatter Dimensions / Realm Grinder Tarzı
### Endless Engine ile Sıfırdan Steam'e

> Bu rehberde birden fazla prestige katmanı olan (Prestige → Ascension → Transcension), araştırma kuyruklu, skill tree'li, çok para birimli bir prestige-heavy idle oyunu yapacaksın.

---

## BU OYUNDA NE VAR?

| Sistem | Ne Yapar |
|--------|----------|
| Prestige (Katman 0) | Sıfırla, kalıcı gelir çarpanı kazan |
| Ascension (Katman 1) | 10 prestige sonrası büyük sıfırlama, cascade çarpan |
| Transcension (Katman 2) | 5 ascension sonrası mutlak sıfırlama, devasa bonus |
| Araştırma Kuyruğu | Zaman içinde araştırmalar tamamlanır |
| Skill Tree | Prestige noktalarıyla kalıcı güçlendirmeler |
| İkincil Para Birimi | "Kristal" sadece prestige'de kazanılır |
| Generator'lar | Her prestige daha güçlü hale gelir |

---

## ADIM 1 — PROJEYİ HAZIRLA

### 1.1 Wizard

`Tools → Endless Engine → New Game Wizard`
1. **Prestige-Heavy** seç
2. Game Name = `PrestHeavy`
3. **Generate**

Oluşturulur:
```
Assets/PrestHeavy/
    Configs/
        EconomyConfig.asset
        PrestigeConfig.asset
        AscensionDatabase.asset
        GeneratorDatabase.asset
        UpgradeTreeConfig.asset
        ResearchTree.asset
        SkillTreeConfig.asset
        CurrencyDatabase.asset
        SchemaVersion.asset
        RealmIdentityConfig.asset
    Scenes/
        PrestHeavy.unity
    Scripts/
        PrestHeavyBootstrap.cs
        PrestHeavyUI.cs
```

---

## ADIM 2 — MİMARİ VE AKIŞ

Prestige-Heavy oyunlarda para akışı ve sıfırlama hiyerarşisi çok önemli:

```
PARA AKIŞI (katmandan bağımsız):
TickEngine → PassiveIncomeService → EconomyService

PRESİGE AKIŞI:
Oyuncu TryPrestige() çağırır →
    SAVE-1 (crash koruma) →
    OnPrestigeStarted tetiklenir:
        EconomyService sıfırlanır
        GeneratorSystem sıfırlanır
        UpgradeTreeService sıfırlanır
    PrestigeCount++ →
    PermanentMultiplier = pow(1.5, PrestigeCount) →
    SAVE-2 (tamamlandı)

ASCENSION AKIŞI (10 prestige sonra):
AscensionStateManager.TryTrigger(layerIndex=1, currentWave) →
    PrestigeStateManager.TryPrestige() çağrılır (layer 0 prestige içerir) →
    Layer 1'e özel reset uygulanır →
    CascadeMultiplier güncellenir →
    OnAscensionComplete tetiklenir

TRANSCENSION AKIŞI (5 ascension sonra):
TryTrigger(layerIndex=2, ...) →
    Daha derin sıfırlama + devasa çarpan
```

---

## ADIM 3 — ASCENSION DATABASE AYARLA

`Assets/PrestHeavy/Configs/AscensionDatabase.asset` → Inspector

### Katman 0: Prestige

Bu katman `PrestigeStateManager` tarafından yönetilir. `AscensionDatabase.Layers[0]` = Layer 0.

`PrestigeLayerConfigSO` oluştur:
`Assets → Create → Endless Engine → Config → Prestige Layer Config`

`Assets/PrestHeavy/Configs/Layers/Layer0_Prestige.asset`:

| Field | Değer | Açıklama |
|-------|-------|----------|
| `LayerIndex` | 0 | Katman 0 = Prestige |
| `DisplayName` | `"Prestige"` | UI'da görünecek |
| `ActionVerb` | `"Prestige Yap"` | Buton metni |
| `MinWaveRequired` | 0 | Wave şartı yok |
| `RequiredPreviousLayerCount` | 0 | Bu ilk katman, öncesi yok |
| `MaxCount` | 0 | Sınırsız |
| `ResetGenerators` | true | Generator'lar sıfırlanır |
| `ResetSecondaryCurrencies` | false | Kristaller sıfırlanmaz |
| `BaseMultiplierPerTrigger` | 1.5 | Her prestige'de ×1.5 |
| `MaxPermanentMultiplier` | 1,000 | Bu katman tavanı |

### Katman 1: Ascension

`Assets/PrestHeavy/Configs/Layers/Layer1_Ascension.asset`:

| Field | Değer | Açıklama |
|-------|-------|----------|
| `LayerIndex` | 1 | Katman 1 = Ascension |
| `DisplayName` | `"Ascension"` | |
| `ActionVerb` | `"Ascend"` | |
| `RequiredPreviousLayerCount` | 10 | 10 prestige sonrası |
| `MaxCount` | 0 | Sınırsız |
| `ResetGenerators` | true | |
| `ResetSecondaryCurrencies` | true | Kristaller sıfırlanır |
| `BaseMultiplierPerTrigger` | 3.0 | Her ascension'da ×3 |
| `MaxPermanentMultiplier` | 1,000,000 | |
| `RewardCurrencyId` | `"ascension_shards"` | Ascension başına ödül |
| `BaseCurrencyReward` | 1 | 1 Ascension Shard |

### Katman 2: Transcension

`Assets/PrestHeavy/Configs/Layers/Layer2_Transcension.asset`:

| Field | Değer | Açıklama |
|-------|-------|----------|
| `LayerIndex` | 2 | Katman 2 = Transcension |
| `DisplayName` | `"Transcension"` | |
| `ActionVerb` | `"Transcend"` | |
| `RequiredPreviousLayerCount` | 5 | 5 ascension sonrası |
| `MaxCount` | 0 | Sınırsız |
| `ResetGenerators` | true | |
| `ResetSecondaryCurrencies` | true | |
| `BaseMultiplierPerTrigger` | 10.0 | |
| `MaxPermanentMultiplier` | 1,000,000,000 | 1 milyar tavan |

### AscensionDatabase'e Ekle

`AscensionDatabase.asset` → `Layers` array → 3 asset'i sırayla sürükle (Layer0, Layer1, Layer2).

---

## ADIM 4 — PRESTIGE CONFIG

`Assets/PrestHeavy/Configs/PrestigeConfig.asset`:

| Field | Değer |
|-------|-------|
| `MinWaveForPrestige` | 0 |
| `MinGoldToPrestige` | 100,000 |
| `MaxPrestigeCount` | 0 |
| `BaseMultiplierPerPrestige` | 1.5 |
| `MaxPermanentMultiplier` | 1,000 |

---

## ADIM 5 — İKİNCİL PARA BİRİMLERİ

Prestige-heavy oyunlarda birden fazla para birimi oyunun derinliğini artırır.

### 5.1 Currency Config Oluştur

`Assets → Create → Endless Engine → Config → Currency`

**Altın (Ana Para):**
`Assets/PrestHeavy/Configs/Currencies/Gold.asset`:
- `CurrencyId` = `"gold"` ← EconomyService'in kullandığı para birimi bu
- `DisplayName` = `"Altın"`
- `HardCap` = 0 (sınırsız)
- `ResetsOnPrestige` = true

**Kristal (Prestige Para):**
`Assets/PrestHeavy/Configs/Currencies/Crystal.asset`:
- `CurrencyId` = `"crystal"`
- `DisplayName` = `"Kristal"`
- `Symbol` = `"◆"`
- `HardCap` = 0
- `ResetsOnPrestige` = false ← Prestige'de SIFIRLANMAZ
- `UnlockAtPrestigeCount` = 1 ← İlk prestige'den sonra görünür

**Ascension Shard (Ascension Para):**
`Assets/PrestHeavy/Configs/Currencies/AscensionShard.asset`:
- `CurrencyId` = `"ascension_shards"`
- `DisplayName` = `"Ascension Shard"`
- `Symbol` = `"✦"`
- `ResetsOnPrestige` = false
- `UnlockAtPrestigeCount` = 10 ← Ascension'dan sonra görünür

### 5.2 CurrencyDatabase'e Ekle

`Assets/PrestHeavy/Configs/CurrencyDatabase.asset` → `Currencies` array → üç currency'i sürükle.

### 5.3 Prestige'de Kristal Nasıl Kazanılır?

Otomatik değil — `OnPrestigeComplete` event'ini dinleyip kendin eklersin:

```csharp
PrestigeStateManager.OnPrestigeComplete += (count, multiplier) =>
{
    // Her prestige'de 1 kristal kazan
    int crystalsToAdd = 1 + (count / 5);  // Her 5 prestige'de 1 bonus
    _currencyService.Add("crystal", crystalsToAdd);
};
```

---

## ADIM 6 — ARAŞTIRMA SİSTEMİ

Araştırma sistemi: oyuncu altın harcayarak bir araştırmayı kuyruğa alır, TickEngine'in her tick'inde ilerler.

### 6.1 Research Node Config Oluştur

`Assets → Create → Endless Engine → Research → Research Node Config`

**5 katmanlı araştırma ağacı:**

**Tier 0 — Temel Araştırmalar:**

`Assets/PrestHeavy/Configs/Research/Research_BasicIncome.asset`:

| Field | Değer |
|-------|-------|
| `NodeId` | `"basic_income"` |
| `DisplayName` | `"Temel Gelir Teorisi"` |
| `Tier` | 0 |
| `PrerequisiteIds` | `[]` (boş = root) |
| `GoldCost` | 500 |
| `ResearchTicks` | 60 ← 60 saniye |
| `Effects` | (bak: 6.2) |

`Research_BasicMining.asset`:
- `NodeId` = `"basic_mining"`
- `Tier` = 0, `GoldCost` = 500, `ResearchTicks` = 60

**Tier 1 — Orta Araştırmalar:**

`Research_AdvancedIncome.asset`:
- `NodeId` = `"advanced_income"`
- `Tier` = 1
- `PrerequisiteIds` = `["basic_income"]`
- `GoldCost` = 5,000
- `ResearchTicks` = 300 (5 dakika)

`Research_EfficientMining.asset`:
- `NodeId` = `"efficient_mining"`
- `Tier` = 1
- `PrerequisiteIds` = `["basic_mining"]`
- `GoldCost` = 5,000
- `ResearchTicks` = 300

**Tier 2 — Geç Araştırmalar:**

- `NodeId` = `"quantum_economics"`, Tier 2, GoldCost = 50,000, ResearchTicks = 1800 (30 dk)
- `PrerequisiteIds` = `["advanced_income", "efficient_mining"]` (iki şart!)

**Tier 3 — Prestige Araştırmaları:**

- `NodeId` = `"prestige_mastery"`, Tier 3, GoldCost = 500,000, ResearchTicks = 7200 (2 saat)
- `PrerequisiteIds` = `["quantum_economics"]`
- Bunlar sadece araştırma kuyruğuna alınabilir, tamamlanması uzun sürer

### 6.2 Research Effect Nedir?

Her araştırma tamamlandığında bir etki uygular. `Effects` listesine `SkillEffect` ekle:

Inspector'da:
- `SkillEffect.StatType` = `GeneratorYield`
- `SkillEffect.Modifier` = 0.25 (=%25 artış)
- `SkillEffect.IsPercentage` = true

Veya kod ile:
```csharp
ResearchService.OnNodeCompleted += (treeId, nodeId) =>
{
    switch (nodeId)
    {
        case "basic_income":
            // %10 pasif gelir bonusu — UpgradeTree'de bir node activate et
            // veya EconomyService multiplier'ı güncelle
            break;
        case "unlock_crystal_mining":
            // Kristal üretimi aktifleştir
            _currencyService.UnlockCurrency("crystal");
            break;
    }
};
```

### 6.3 Research Tree Config

`Assets/PrestHeavy/Configs/ResearchTree.asset`:
- `TreeId` = `"main_tree"`
- `Nodes` → Tüm ResearchNodeConfigSO asset'lerini array'e ekle

---

## ADIM 7 — SKILL TREE

Skill Tree = prestige noktalarıyla kalıcı bonuslar. Prestige'de sıfırlanmaz.

### 7.1 Skill Node Config

`Assets → Create → Endless Engine → Config → Skill Node Config`

`Assets/PrestHeavy/Configs/Skills/Skill_BetterStart.asset`:

| Field | Değer |
|-------|-------|
| `NodeId` | `"better_start"` |
| `DisplayName` | `"İyi Başlangıç"` |
| `Description` | `"Her prestige başlangıcında +100 altın"` |
| `SkillPointCost` | 1 |
| `MaxRank` | 5 |
| `PrerequisiteIds` | `[]` |
| `Effects` → SkillEffect | StatType=StartingGoldBonus, Modifier=100 (flat) |

`Skill_FasterResearch.asset`:

| Field | Değer |
|-------|-------|
| `NodeId` | `"faster_research"` |
| `DisplayName` | `"Araştırmacı Zihin"` |
| `SkillPointCost` | 2 |
| `MaxRank` | 3 |
| `PrerequisiteIds` | `["better_start"]` |
| `Effects` → | Araştırma hızı +20%/rank |

### 7.2 Skill Tree Config

`Assets/PrestHeavy/Configs/SkillTreeConfig.asset`:
- `TreeId` = `"prestige_skills"`
- `Nodes` → Tüm Skill Node Config'leri ekle

### 7.3 Skill Puanı Kazanma

Her prestige'de 1 skill puanı kazan:
```csharp
PrestigeStateManager.OnPrestigeComplete += (count, multiplier) =>
{
    _saveData.SkillPoints += 1;  // SaveData'da SkillPoints alanı var
};
```

Skill satın alma:
```csharp
_skillTreeService.TryUnlock("prestige_skills", "better_start");
// Eğer yeterli SkillPoints varsa unlock olur, SaveData.UnlockedSkillNodes'a eklenir
```

---

## ADIM 8 — BOOTSTRAP KURULUMU

Bu en karmaşık bootstrap — tüm sistemleri bir arada başlatır.

```csharp
using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.Generator;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Upgrade;
using EndlessEngine.Prestige;
using EndlessEngine.Ascension;
using EndlessEngine.Research;
using EndlessEngine.Skill;
using EndlessEngine.Currency;

[DefaultExecutionOrder(-500)]
public class PrestHeavyBootstrap : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private SaveService          _saveService;
    [SerializeField] private EconomyService       _economyService;
    [SerializeField] private UpgradeTreeService   _upgradeTreeService;
    [SerializeField] private GeneratorSystem      _generatorSystem;
    [SerializeField] private PassiveIncomeService _passiveIncomeService;

    [Header("Prestige & Ascension")]
    [SerializeField] private PrestigeStateManager  _prestigeManager;
    [SerializeField] private AscensionStateManager _ascensionManager;

    [Header("Araştırma & Skill")]
    [SerializeField] private ResearchService  _researchService;
    [SerializeField] private SkillTreeService _skillTreeService;

    [Header("Para Birimleri")]
    [SerializeField] private CurrencyService _currencyService;

    [Header("Configs")]
    [SerializeField] private EconomyConfigSO         _economyConfig;
    [SerializeField] private PrestigeConfigSO         _prestigeConfig;
    [SerializeField] private AscensionDatabaseSO      _ascensionDatabase;
    [SerializeField] private GeneratorDatabaseSO      _generatorDatabase;
    [SerializeField] private SchemaVersionSO          _schemaVersion;
    [SerializeField] private RealmIdentityConfigSO    _realmConfig;
    [SerializeField] private CurrencyDatabaseSO       _currencyDatabase;
    [SerializeField] private ResearchTreeConfigSO[]   _researchTrees;
    [SerializeField] private SkillTreeConfigSO[]      _skillTrees;

    private IEnumerator Start()
    {
        // ── 1. Sayı motoru ──────────────────────────────────
        BigNumberFactory.Configure(_economyConfig.NumberBackend);

        // ── 2. Config registry ──────────────────────────────
        ConfigRegistry.InjectForTesting(
            economy:  _economyConfig,
            prestige: _prestigeConfig,
            schema:   _schemaVersion,
            realm:    _realmConfig);

        // ── 3. Upgrade tree ─────────────────────────────────
        _upgradeTreeService?.HandleConfigsLoaded();

        // ── 4. Economy ──────────────────────────────────────
        _economyService.Initialize(_upgradeTreeService, _saveService);

        // ── 5. Generators ───────────────────────────────────
        _generatorSystem.Initialize(
            _generatorDatabase.Generators,
            _economyService,
            _saveService);

        // ── 6. Pasif gelir ──────────────────────────────────
        _passiveIncomeService.Initialize(
            _generatorSystem,
            _economyService,
            gameFlow: null);

        // ── 7. İkincil para birimleri ────────────────────────
        _currencyService?.Initialize(_currencyDatabase);

        // ── 8. Ascension (PrestigeManager + Generator gerektirir) ─
        _ascensionManager?.Initialize(
            database:        _ascensionDatabase,
            prestigeManager: _prestigeManager,
            saveService:     _saveService,
            economyService:  _economyService,
            generatorSystem: _generatorSystem,
            currencyService: _currencyService);

        // ── 9. Araştırma servisi ─────────────────────────────
        _researchService?.Initialize(
            trees:         _researchTrees,
            economyService: _economyService,
            currencyService: _currencyService);

        // Araştırma her tick ilerler
        TickEngine.OnTick += dt => _researchService?.OnTick(dt);

        // ── 10. Skill tree ───────────────────────────────────
        _skillTreeService?.Initialize(_skillTrees);

        // ── 11. Event bağlantıları ───────────────────────────
        PrestigeStateManager.OnPrestigeComplete += OnPrestigeComplete;
        AscensionStateManager.OnAscensionComplete += OnAscensionComplete;
        ResearchService.OnNodeCompleted += OnResearchCompleted;

        // ── 12. Kayıt sağlayıcıları ──────────────────────────
        _saveService.RegisterStateProvider(_economyService);
        _saveService.RegisterStateProvider(_upgradeTreeService);
        _saveService.RegisterStateProvider(_generatorSystem);
        _saveService.RegisterStateProvider(_prestigeManager);
        _saveService.RegisterStateProvider(_ascensionManager);
        _saveService.RegisterStateProvider(_researchService);
        _saveService.RegisterStateProvider(_skillTreeService);
        _saveService.RegisterStateProvider(_currencyService);

        // ── 13. Kayıt yükle ──────────────────────────────────
        bool done = false;
        _ = _saveService.LoadAsync().ContinueWith(_ => done = true,
            System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        yield return new WaitUntil(() => done);

        Debug.Log("[PrestHeavy] Hazır!");
    }

    private void OnDestroy()
    {
        PrestigeStateManager.OnPrestigeComplete      -= OnPrestigeComplete;
        AscensionStateManager.OnAscensionComplete    -= OnAscensionComplete;
        ResearchService.OnNodeCompleted              -= OnResearchCompleted;
        TickEngine.OnTick                            -= dt => _researchService?.OnTick(dt);
    }

    private void OnPrestigeComplete(int count, float multiplier)
    {
        // Her prestige'de 1 kristal + 1 skill puanı
        _currencyService?.Add("crystal", 1 + count / 5);
        Debug.Log($"Prestige {count}! Çarpan: ×{multiplier:F2}");
    }

    private void OnAscensionComplete(int layerIndex, int newCount, float cascadeMult)
    {
        Debug.Log($"Ascension {newCount}! Cascade: ×{cascadeMult:F2}");
    }

    private void OnResearchCompleted(string treeId, string nodeId)
    {
        Debug.Log($"Araştırma tamamlandı: {nodeId}");
        // Özel efektler burada uygula
    }
}
```

---

## ADIM 9 — UI YAPISI

### 9.1 Ana HUD

```
Canvas
    ├── TopBar
    │       ├── GoldLabel         "Altın: X"
    │       ├── IncomeLabel       "X/s"
    │       ├── CrystalLabel      "◆ X"
    │       └── AscShardsLabel    "✦ X"
    ├── PrestigePanel
    │       ├── PrestigeButton
    │       ├── PrestigeMultLabel "×X çarpan"
    │       └── PrestigeCountLabel "Prestige: X"
    ├── AscensionPanel (10 prestige sonrası görünür)
    │       ├── AscensionButton
    │       ├── CascadeMultLabel  "Cascade: ×X"
    │       └── AscCountLabel     "Ascension: X"
    ├── TranscensionPanel (5 ascension sonrası)
    │       └── (benzer)
    ├── ResearchPanel
    │       ├── ActiveResearchBar (Slider)
    │       ├── ActiveResearchLabel "Araştırılıyor: X"
    │       └── QueueList
    ├── SkillTreeButton → Skill Tree panelini açar
    └── GeneratorPanel + UpgradePanel
```

### 9.2 Prestige & Ascension UI

```csharp
public class PrestigeUI : MonoBehaviour
{
    [SerializeField] private Button   _prestigeButton;
    [SerializeField] private TMP_Text _prestigeMultLabel;
    [SerializeField] private TMP_Text _prestigeCountLabel;

    [Header("Ascension")]
    [SerializeField] private GameObject _ascensionPanel;
    [SerializeField] private Button     _ascensionButton;
    [SerializeField] private TMP_Text   _cascadeLabel;
    [SerializeField] private TMP_Text   _ascCountLabel;

    private PrestigeStateManager  _prestige;
    private AscensionStateManager _ascension;
    private WaveSpawnManager      _waves;  // CurrentWaveNumber için

    public void Inject(PrestigeStateManager p, AscensionStateManager a)
    {
        _prestige  = p;
        _ascension = a;

        PrestigeStateManager.OnPrestigeComplete  += OnPrestigeComplete;
        AscensionStateManager.OnAscensionComplete += OnAscensionComplete;
        Refresh();
    }

    private void OnDestroy()
    {
        PrestigeStateManager.OnPrestigeComplete  -= OnPrestigeComplete;
        AscensionStateManager.OnAscensionComplete -= OnAscensionComplete;
    }

    private void Update()
    {
        _prestigeButton.interactable = _prestige.CanPrestige;

        bool canAscend = _ascension.CanTrigger(1, 0);  // 0 = wave yok
        _ascensionButton.interactable = canAscend;
        _ascensionPanel.SetActive(_prestige.PrestigeCount >= 10);
    }

    private void OnPrestigeComplete(int count, float mult) => Refresh();
    private void OnAscensionComplete(int layer, int count, float cascade) => Refresh();

    private void Refresh()
    {
        _prestigeMultLabel.text  = $"×{_prestige.GetPermanentMultiplier():F2} kalıcı çarpan";
        _prestigeCountLabel.text = $"Prestige: {_prestige.PrestigeCount}";
        _cascadeLabel.text       = $"Cascade: ×{_ascension.GetCascadeMultiplier():F2}";
        _ascCountLabel.text      = $"Ascension: {_ascension.GetCount(1)}";
    }

    public void OnPrestigeClick() => _prestige.TryPrestige();

    public void OnAscensionClick() => _ascension.TryTrigger(1, 0);
}
```

### 9.3 Araştırma UI

```csharp
public class ResearchUI : MonoBehaviour
{
    [SerializeField] private Slider   _progressBar;
    [SerializeField] private TMP_Text _activeLabel;
    [SerializeField] private TMP_Text _queueCountLabel;
    [SerializeField] private Transform _nodeContainer; // Araştırma butonları parent

    private ResearchService _research;

    public void Inject(ResearchService research)
    {
        _research = research;
        ResearchService.OnResearchProgress += OnProgress;
        ResearchService.OnNodeCompleted    += OnCompleted;
        ResearchService.OnNodeQueued       += OnQueued;
    }

    private void OnDestroy()
    {
        ResearchService.OnResearchProgress -= OnProgress;
        ResearchService.OnNodeCompleted    -= OnCompleted;
        ResearchService.OnNodeQueued       -= OnQueued;
    }

    private void OnProgress(string treeId, string nodeId, int done, int total)
    {
        _progressBar.value = (float)done / total;
        _activeLabel.text  = $"Araştırılıyor: {nodeId} ({done}/{total}s)";
    }

    private void OnCompleted(string treeId, string nodeId)
    {
        _progressBar.value = 0;
        _activeLabel.text  = "Araştırma tamamlandı!";
        RefreshQueueCount();
    }

    private void OnQueued(string treeId, string nodeId)
        => RefreshQueueCount();

    private void RefreshQueueCount()
        => _queueCountLabel.text = $"Kuyrukta: {_research.QueueCount}";

    // Araştırmayı kuyruğa ekle (UI butonundan çağrılır)
    public void EnqueueResearch(string treeId, string nodeId)
    {
        bool success = _research.TryEnqueue(treeId, nodeId);
        if (!success)
            Debug.Log($"Kuyruğa eklenemedi: {nodeId}");
    }
}
```

### 9.4 Skill Tree UI

```csharp
public class SkillTreeUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _skillPointsLabel;
    [SerializeField] private SkillNodeButton[] _nodeButtons;

    private SkillTreeService _skillTree;

    public void Inject(SkillTreeService skillTree)
    {
        _skillTree = skillTree;
        Refresh();
    }

    private void Refresh()
    {
        // SaveData.SkillPoints doğrudan SaveService'ten oku
        // veya SkillTreeService üzerinden
    }

    public void OnNodeClick(string treeId, string nodeId)
    {
        bool success = _skillTree.TryUnlock(treeId, nodeId);
        if (success) Refresh();
    }
}
```

---

## ADIM 10 — ARAŞTIRMA ZAMANLAMA STRATEJİSİ

Araştırma süreleri oyunun temposunu belirler:

| Tier | Süre | GoldCost | Açıklama |
|------|------|---------|----------|
| 0 | 60s | 500 | İlk oturumda tamamlanır |
| 1 | 5 dk | 5,000 | 3-5. oturumda erişilir |
| 2 | 30 dk | 50,000 | Uzun oturum veya offline |
| 3 | 2 saat | 500,000 | Birkaç gün gerekir |
| 4 | 8 saat | 5,000,000 | Haftalık hedef |

**Çevrimdışı araştırma:** TickEngine çevrimdışıyken çalışmaz. Araştırma ilerlemesi yalnızca oyun açıkken ilerler. Bu bir tasarım kararı — offline araştırma istiyorsan özel bir `OfflineResearchCalculator` yazman gerekir (engine'de hazır değil).

---

## ADIM 11 — ÇOKLU KATMAN DENGESİ

### Prestige Dengesi (Katman 0)

| Prestige | Kalıcı Çarpan | Birikim Süresi |
|---------|--------------|---------------|
| 1 | ×1.5 | 20 dakika |
| 5 | ×7.6 | 10 dakika |
| 10 | ×57.7 | 5 dakika |
| 20 | ×3,325 | 2 dakika |

### Ascension Dengesi (Katman 1)

Ascension için 10 prestige gerekir. 10 prestige ≈ 1-2 saatlik oyun. Ascension cascade çarpanı:
- Ascension 1: ×3 (tek başına)
- Ascension 2: ×3 × ×3 = ×9 (kümülatif)
- Ascension 5: ×3^5 = ×243

Bu çarpanlar çok hızlı büyür — `MaxPermanentMultiplier` ile dengele.

### Economy Simulator'de 100 Oturum Test Et

`Tools → Endless Engine → Economy Simulator` → Sessions = 100

Kontrol et:
- 10. oturumda prestige ×15 civarında mı?
- 50. oturumda ascension gerçekleşiyor mu?
- 100. oturumda oyun hâlâ ilerleme sunuyor mu?

---

## ADIM 12 — GENERATOR'LAR (Prestige-Heavy için)

Prestige-heavy oyunlarda generator'lar her prestige döngüsünde hızla yeniden satın alınabilmeli:

| Generator | Yield/s | BaseCost | Açıklama |
|-----------|---------|---------|----------|
| `spark` | 0.01 | 5 | Çok ucuz, ilk başta hızlıca al |
| `ember` | 0.1 | 50 | |
| `flame` | 1.0 | 500 | |
| `blaze` | 10 | 5,000 | |
| `inferno` | 100 | 50,000 | |
| `singularity` | 1,000 | 500,000 | Prestige'in sonuna kadar ulaşılmaz |

`CostScalingFactor` = 1.15 (her kopyada %15 artış).

**Önemli:** Her prestige'de generator'lar sıfırlanır. Oyuncunun çabuk tekrar satın alabilmesi için ilk generator'ın çok ucuz olması gerekir.

---

## ADIM 13 — TEST LİSTESİ

- [ ] Prestige yapılıyor, altın + generator sıfırlanıyor
- [ ] Prestige sayısı ve kalıcı çarpan kalıcı
- [ ] 10 prestige sonrası Ascension butonu görünüyor
- [ ] Ascension cascade çarpanı doğru hesaplanıyor
- [ ] Araştırma kuyruğa alınıyor ve ilerliyor
- [ ] Araştırma tamamlandığında efekt uygulanıyor
- [ ] Kristal prestige'de sıfırlanmıyor
- [ ] Skill puanı kazanılıyor ve skill unlock edilebiliyor
- [ ] Play Stop Play → tüm sayaçlar geri geliyor
- [ ] `Config Validator` → sıfır hata

---

*Bu rehber Endless Engine v1.3.4 için yazılmıştır.*
