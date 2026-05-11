# REHBER 06 — Building Idle / Farm Idle
## Hay Day / Forge of Empires Idle Tarzı
### Endless Engine ile Sıfırdan Steam'e

> Bu rehberde grid üzerine binalar yerleştiren, her bina tick başına üretim yapan, bina upgrade tipleri olan, araştırma ile yeni binalar kilidini açan bir building idle oyunu yapacaksın.

---

## BU OYUNDA NE VAR?

| Sistem | Ne Yapar |
|--------|----------|
| Grid Board | 8×8 grid üzerinde bina yerleştirme |
| BuildingService | Her tick üretim yapar, altın ekler |
| Bina Upgrade | Her binanın 3 upgrade tier'ı var |
| Generator'lar | Genel pasif gelir (binaların yanında) |
| Araştırma | Yeni bina tipleri unlock eder |
| Prestige | Bina sayımı sıfırlanır, kalıcı üretim çarpanı |
| Kayıt | Grid layout ve tüm bina state'i kaydedilir |

---

## ADIM 1 — PROJEYİ HAZIRLA

### 1.1 Wizard

`Tools → Endless Engine → New Game Wizard`
1. **Building Idle** seç
2. Game Name = `BuildIdle`
3. **Generate**

Oluşturulur:
```
Assets/BuildIdle/
    Configs/
        EconomyConfig.asset
        GeneratorDatabase.asset
        UpgradeTreeConfig.asset
        SchemaVersion.asset
        PrestigeConfig.asset
        RealmIdentityConfig.asset
    Buildings/    ← Boş, sen dolduracaksın
    Scenes/
        BuildIdle.unity
    Scripts/
        BuildIdleBootstrap.cs
        BuildIdleUI.cs
```

---

## ADIM 2 — SİSTEMLER VE PARA AKIŞI

Building oyunlarında para akışı:

```
KAYNAK 1 — Bina Üretimi:
TickEngine.OnTick →
    BuildingService.OnTick(dt) →
        Her aktif bina için: ProductionPerTick × dt →
            EconomyService.AddResources(amount)
            BuildingService.OnBuildingProduced tetiklenir (instanceId, amount)

KAYNAK 2 — Pasif Generator:
TickEngine.OnTick →
    PassiveIncomeService.HandleTick() →
        EconomyService.AddResources()
```

**Fark:** `BuildingService.OnTick()` senin tarafından TickEngine.OnTick'e bağlanmalı — bu otomatik olmaz!

```csharp
// Bootstrap'ta:
TickEngine.OnTick += dt => _buildingService?.OnTick(dt);
```

---

## ADIM 3 — BİNA CONFIG'LERİ OLUŞTUR

### 3.1 Bina Temaları Seç

Önerilen tema: **Şehir Kurma**
- Çardak (tek katlı ev) → Ev → Villa → Saray
- Kulübe (çiftlik) → Ahır → Çiftlik → Mega Çiftlik
- Dükkân → Market → Alışveriş Merkezi → Mall

Bu rehberde 4 farklı bina tipi kullanıyoruz.

### 3.2 BuildingConfigSO Oluştur

`Assets → Create → Endless Engine → Building Config`

**Bina 1: Çardak**

`Assets/BuildIdle/Buildings/Hut.asset`:

| Field | Değer |
|-------|-------|
| `BuildingId` | `"hut"` |
| `DisplayName` | `"Çardak"` |
| `Description` | `"Basit bir barınak. Az ama düzenli üretir."` |
| `GridWidth` | 1 |
| `GridHeight` | 1 |
| `PlacementCost` | 100 |
| `PlacementCurrencyId` | `"gold"` |
| `ProductionCurrencyId` | `"gold"` |
| `ProductionPerTick` | 2 |
| `MaxInstances` | 0 (sınırsız) |

**UpgradeTiers** (Inspector'da + ile ekle):

Tier 1 (Level 2):
- `DisplayLabel` = "Level 2"
- `UpgradeCost` = 500
- `UpgradeCurrencyId` = `"gold"`
- `ProductionBonusPerTick` = 3 (base 2 + bonus 3 = 5)
- `ProductionMultiplier` = 1.0

Tier 2 (Level 3):
- `DisplayLabel` = "Level 3"
- `UpgradeCost` = 2,000
- `ProductionBonusPerTick` = 8 (toplam 10)
- `ProductionMultiplier` = 1.5

Tier 3 (Level 4 — Ev'e dönüşür):
- `DisplayLabel` = "Ev'e Dönüştür"
- `UpgradeCost` = 10,000
- `ProductionBonusPerTick` = 40 (toplam 50)
- `ProductionMultiplier` = 2.0

**Bina 2: Kulübe**

`Assets/BuildIdle/Buildings/Cottage.asset`:

| Field | Değer |
|-------|-------|
| `BuildingId` | `"cottage"` |
| `DisplayName` | `"Kulübe"` |
| `GridWidth` | 1 |
| `GridHeight` | 2 ← 2 satır kaplar |
| `PlacementCost` | 500 |
| `ProductionPerTick` | 8 |

Tiers:
- Level 2: UpgradeCost=2,000, Bonus=12
- Level 3: UpgradeCost=8,000, Bonus=40 + Multiplier=1.5
- Max Level: UpgradeCost=30,000, Bonus=150 + Multiplier=2.5

**Bina 3: Dükkân**

`Assets/BuildIdle/Buildings/Shop.asset`:

| Field | Değer |
|-------|-------|
| `BuildingId` | `"shop"` |
| `DisplayName` | `"Dükkân"` |
| `GridWidth` | 2 |
| `GridHeight` | 1 |
| `PlacementCost` | 2,000 |
| `ProductionPerTick` | 30 |
| `MaxInstances` | 5 ← Aynı anda max 5 dükkân |

**Bina 4: Kule (Özel — 2×2)**

`Assets/BuildIdle/Buildings/Tower.asset`:

| Field | Değer |
|-------|-------|
| `BuildingId` | `"tower"` |
| `DisplayName` | `"İzleme Kulesi"` |
| `GridWidth` | 2 |
| `GridHeight` | 2 |
| `PlacementCost` | 10,000 |
| `ProductionPerTick` | 200 |
| `MaxInstances` | 3 |

---

## ADIM 4 — BOOTSTRAP KURULUMU

### 4.1 Bootstrap Script

`Assets/BuildIdle/Scripts/BuildIdleBootstrap.cs`:

```csharp
using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.Generator;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Upgrade;
using EndlessEngine.Building;
using EndlessEngine.Research;

[DefaultExecutionOrder(-500)]
public class BuildIdleBootstrap : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private SaveService          _saveService;
    [SerializeField] private EconomyService       _economyService;
    [SerializeField] private UpgradeTreeService   _upgradeTreeService;
    [SerializeField] private GeneratorSystem      _generatorSystem;
    [SerializeField] private PassiveIncomeService _passiveIncomeService;
    [SerializeField] private TickEngine           _tickEngine;

    [Header("Building")]
    [SerializeField] private BuildingService _buildingService;

    [Header("Araştırma (İsteğe Bağlı)")]
    [SerializeField] private ResearchService       _researchService;
    [SerializeField] private ResearchTreeConfigSO[] _researchTrees;

    [Header("Configs")]
    [SerializeField] private EconomyConfigSO      _economyConfig;
    [SerializeField] private GeneratorDatabaseSO  _generatorDatabase;
    [SerializeField] private SchemaVersionSO      _schemaVersion;
    [SerializeField] private PrestigeConfigSO     _prestigeConfig;
    [SerializeField] private RealmIdentityConfigSO _realmConfig;

    [Header("Tüm Bina Configs")]
    [SerializeField] private BuildingConfigSO[] _buildingConfigs;  // 4 bina

    private IEnumerator Start()
    {
        // 1. Sayı motoru
        BigNumberFactory.Configure(_economyConfig.NumberBackend);

        // 2. Config registry
        ConfigRegistry.InjectForTesting(
            economy:  _economyConfig,
            prestige: _prestigeConfig,
            schema:   _schemaVersion,
            realm:    _realmConfig);

        // 3. Upgrade tree
        _upgradeTreeService?.HandleConfigsLoaded();

        // 4. Economy
        _economyService.Initialize(_upgradeTreeService, _saveService);

        // 5. Generators
        _generatorSystem.Initialize(
            _generatorDatabase.Generators,
            _economyService,
            _saveService);

        _passiveIncomeService.Initialize(
            _generatorSystem,
            _economyService,
            gameFlow: null);

        // 6. BuildingService başlat
        // Binalar ekonomiyle bağlı — PlacementCost + ProductionPerTick
        _buildingService.Initialize(_buildingConfigs, _economyService);

        // 7. BuildingService'i TickEngine'e bağla — BU OLMADAN BİNALAR ÜRETİM YAPMAZ
        TickEngine.OnTick += dt => _buildingService.OnTick(dt);

        // 8. Araştırma (varsa)
        if (_researchService != null && _researchTrees.Length > 0)
        {
            _researchService.Initialize(_researchTrees, _economyService);
            TickEngine.OnTick += dt => _researchService.OnTick(dt);
        }

        // 9. Building event'lerini dinle
        BuildingService.OnBuildingPlaced   += OnBuildingPlaced;
        BuildingService.OnBuildingUpgraded += OnBuildingUpgraded;
        BuildingService.OnBuildingProduced += OnBuildingProduced;
        BuildingService.OnPlaceFailed      += OnPlaceFailed;

        // 10. Kayıt sağlayıcıları
        _saveService.RegisterStateProvider(_economyService);
        _saveService.RegisterStateProvider(_upgradeTreeService);
        _saveService.RegisterStateProvider(_generatorSystem);
        _saveService.RegisterStateProvider(_buildingService);  // ← Grid layout kaydeder
        if (_researchService != null)
            _saveService.RegisterStateProvider(_researchService);

        // 11. Kayıt yükle
        bool done = false;
        _ = _saveService.LoadAsync().ContinueWith(_ => done = true,
            System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        yield return new WaitUntil(() => done);

        // 12. Kayıttan gelen binaları grid'e çiz
        RefreshGrid();

        Debug.Log("[BuildIdle] Hazır!");
    }

    private void OnDestroy()
    {
        TickEngine.OnTick -= dt => _buildingService?.OnTick(dt);
        if (_researchService != null)
            TickEngine.OnTick -= dt => _researchService.OnTick(dt);

        BuildingService.OnBuildingPlaced   -= OnBuildingPlaced;
        BuildingService.OnBuildingUpgraded -= OnBuildingUpgraded;
        BuildingService.OnBuildingProduced -= OnBuildingProduced;
        BuildingService.OnPlaceFailed      -= OnPlaceFailed;
    }

    private void OnBuildingPlaced(BuildingInstance instance)
    {
        Debug.Log($"Bina yerleşti: {instance.BuildingId} [{instance.GridX},{instance.GridY}]");
        FindFirstObjectByType<BuildGridView>()?.PlaceBuilding(instance);
    }

    private void OnBuildingUpgraded(BuildingInstance instance)
    {
        FindFirstObjectByType<BuildGridView>()?.UpdateBuilding(instance);
    }

    private void OnBuildingProduced(string instanceId, long amount)
    {
        // Her üretimde floating text göster (isteğe bağlı)
    }

    private void OnPlaceFailed(string buildingId, string reason)
    {
        Debug.LogWarning($"Yerleştirme başarısız: {buildingId} — {reason}");
    }

    private void RefreshGrid()
    {
        FindFirstObjectByType<BuildGridView>()?.RefreshFromSave(_buildingService);
    }
}
```

### 4.2 Sahne Hiyerarşisi

```
Bootstrap (GameObject)
    ├── BuildIdleBootstrap     (component)
    ├── SaveService            (component)
    ├── EconomyService         (component)
    ├── UpgradeTreeService     (component)
    ├── GeneratorSystem        (component)
    ├── PassiveIncomeService   (component)
    ├── TickEngine             (component)
    ├── BuildingService        (component)
    └── ResearchService        (component, isteğe bağlı)

Canvas
    ├── BuildGridView (component) ← Grid UI yönetici
    ├── GoldLabel
    ├── IncomeLabel
    ├── BuildMenuPanel ← Bina seçme menüsü
    └── UpgradePanelPopup ← Bina tıklandığında açılır
```

---

## ADIM 5 — GRID UI YAPISI

### 5.1 Grid Layout

Canvas içinde `8×8` grid:

```
BuildGridView (GameObject, 640×640 px)
    └── Grid (GridLayoutGroup)
            Cell Size: 76×76 px
            Spacing: 2×2 px
            Constraint: Fixed Column Count = 8
            → 8×8 = 64 cell
```

### 5.2 Grid Cell Prefab

```
GridCell (Prefab)
    ├── Background (Image — boş/dolu rengi)
    ├── BuildingIcon (Image — bina sprite'ı)
    ├── LevelLabel (TMP_Text — "Lv2")
    ├── ProductionLabel (TMP_Text — "+2/s", küçük)
    └── GridCell (component)
```

```csharp
public class GridCell : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image    _background;
    [SerializeField] private Image    _buildingIcon;
    [SerializeField] private TMP_Text _levelLabel;
    [SerializeField] private TMP_Text _productionLabel;

    public int   GridX     { get; private set; }
    public int   GridY     { get; private set; }
    public bool  HasBuilding { get; private set; }

    private string         _instanceId;
    private BuildGridView  _gridView;

    public void Setup(int x, int y, BuildGridView gridView)
    {
        GridX     = x;
        GridY     = y;
        _gridView = gridView;
        SetEmpty();
    }

    public void SetBuilding(BuildingInstance instance, Sprite icon)
    {
        HasBuilding            = true;
        _instanceId            = instance.InstanceId;
        _buildingIcon.sprite   = icon;
        _buildingIcon.enabled  = true;
        _levelLabel.text       = $"Lv{instance.CurrentTier + 1}";
        _productionLabel.text  = $"+{instance.CurrentProductionPerTick:F0}/s";
        _background.color      = Color.white;
    }

    public void SetEmpty()
    {
        HasBuilding           = false;
        _instanceId           = null;
        _buildingIcon.enabled = false;
        _levelLabel.text      = "";
        _productionLabel.text = "";
        _background.color     = new Color(0.9f, 0.95f, 0.9f);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (HasBuilding)
            _gridView.ShowBuildingMenu(_instanceId, GridX, GridY);
        else
            _gridView.ShowPlacementMenu(GridX, GridY);
    }
}
```

### 5.3 Build Grid View Controller

```csharp
public class BuildGridView : MonoBehaviour
{
    public const int GRID_WIDTH  = 8;
    public const int GRID_HEIGHT = 8;

    [SerializeField] private GridCell       _cellPrefab;
    [SerializeField] private Transform      _gridParent;
    [SerializeField] private GameObject     _placementPanel;
    [SerializeField] private GameObject     _buildingMenuPanel;
    [SerializeField] private BuildingConfigSO[] _allBuildingConfigs;

    private GridCell[,]   _cells;
    private BuildingService _buildingService;

    private int    _selectedX, _selectedY;
    private string _selectedInstanceId;

    public void Inject(BuildingService buildingService)
    {
        _buildingService = buildingService;
        CreateGrid();
    }

    private void CreateGrid()
    {
        _cells = new GridCell[GRID_WIDTH, GRID_HEIGHT];

        for (int y = 0; y < GRID_HEIGHT; y++)
        {
            for (int x = 0; x < GRID_WIDTH; x++)
            {
                var cell = Instantiate(_cellPrefab, _gridParent);
                cell.Setup(x, y, this);
                _cells[x, y] = cell;
            }
        }
    }

    // Kayıttan gelen binaları çiz
    public void RefreshFromSave(BuildingService service)
    {
        // BuildingService'teki tüm aktif binaları iterate et
        // (BuildingService.GetAllInstances() veya benzer API)
        foreach (var instance in service.GetAllInstances())
            PlaceBuilding(instance);
    }

    public void PlaceBuilding(BuildingInstance instance)
    {
        var config = GetConfig(instance.BuildingId);
        if (config == null) return;

        // Çok hücreli bina için tüm hücreleri işaretle
        for (int y = instance.GridY; y < instance.GridY + config.GridHeight; y++)
            for (int x = instance.GridX; x < instance.GridX + config.GridWidth; x++)
                _cells[x, y]?.SetBuilding(instance, config.Icon);
    }

    public void UpdateBuilding(BuildingInstance instance)
    {
        PlaceBuilding(instance);  // Üzerine güncelle
    }

    // Boş hücreye tıklandı — bina seçme paneli göster
    public void ShowPlacementMenu(int x, int y)
    {
        _selectedX = x;
        _selectedY = y;
        _placementPanel.SetActive(true);
        // Panel içindeki butonları BuildingConfigs'e göre doldur
        FindFirstObjectByType<PlacementMenu>()?.Refresh(x, y, _allBuildingConfigs);
    }

    // Mevcut binaya tıklandı — yükseltme/kaldır menüsü
    public void ShowBuildingMenu(string instanceId, int x, int y)
    {
        _selectedInstanceId = instanceId;
        _buildingMenuPanel.SetActive(true);
        FindFirstObjectByType<BuildingMenu>()?.Refresh(instanceId, _buildingService);
    }

    private BuildingConfigSO GetConfig(string id)
        => System.Array.Find(_allBuildingConfigs, c => c.BuildingId == id);
}
```

---

## ADIM 6 — BİNA YERLEŞTIRME VE UPGRADE UI

### 6.1 Placement Menu

```csharp
public class PlacementMenu : MonoBehaviour
{
    [SerializeField] private BuildingOptionButton[] _buttons;

    private int            _targetX, _targetY;
    private BuildingService _buildingService;
    private EconomyService  _economy;

    public void Inject(BuildingService bs, EconomyService eco)
    {
        _buildingService = bs;
        _economy         = eco;
    }

    public void Refresh(int x, int y, BuildingConfigSO[] configs)
    {
        _targetX = x;
        _targetY = y;

        for (int i = 0; i < _buttons.Length && i < configs.Length; i++)
        {
            var config = configs[i];
            bool canAfford = _economy.CurrentResources >= config.PlacementCost;

            _buttons[i].Setup(
                config,
                canAfford,
                () => OnBuildingSelected(config.BuildingId));
        }
    }

    private void OnBuildingSelected(string buildingId)
    {
        var result = _buildingService.TryPlace(buildingId, _targetX, _targetY);

        if (result.Success)
        {
            gameObject.SetActive(false);
            Debug.Log($"Bina yerleşti: {buildingId}");
        }
        else
        {
            Debug.LogWarning($"Yerleştirme başarısız: {result.FailReason}");
        }
    }
}
```

### 6.2 Bina Bilgi ve Upgrade Menüsü

```csharp
public class BuildingMenu : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameLabel;
    [SerializeField] private TMP_Text _levelLabel;
    [SerializeField] private TMP_Text _productionLabel;
    [SerializeField] private Button   _upgradeButton;
    [SerializeField] private TMP_Text _upgradeCostLabel;
    [SerializeField] private Button   _removeButton;

    private string          _instanceId;
    private BuildingService _buildingService;
    private EconomyService  _economy;

    public void Inject(BuildingService bs, EconomyService eco)
    {
        _buildingService = bs;
        _economy         = eco;
    }

    public void Refresh(string instanceId, BuildingService bs)
    {
        _instanceId = instanceId;
        var instance = bs.GetInstance(instanceId);
        if (instance == null) { gameObject.SetActive(false); return; }

        var config = bs.GetConfig(instance.BuildingId);

        _nameLabel.text       = config.DisplayName;
        _levelLabel.text      = $"Seviye {instance.CurrentTier + 1}";
        _productionLabel.text = $"Üretim: +{instance.CurrentProductionPerTick:F0}/s";

        bool hasNextTier = instance.CurrentTier < config.UpgradeTiers.Length;
        _upgradeButton.gameObject.SetActive(hasNextTier);

        if (hasNextTier)
        {
            var nextTier = config.UpgradeTiers[instance.CurrentTier];
            long cost    = nextTier.UpgradeCost;
            _upgradeCostLabel.text          = $"{nextTier.DisplayLabel}: {cost} altın";
            _upgradeButton.interactable     = _economy.CurrentResources >= cost;
        }
    }

    public void OnUpgradeClick()
    {
        bool success = _buildingService.TryUpgrade(_instanceId);
        if (success) Refresh(_instanceId, _buildingService);
        else Debug.LogWarning("Yükseltme başarısız — yeterli altın yok.");
    }

    public void OnRemoveClick()
    {
        _buildingService.Remove(_instanceId);
        gameObject.SetActive(false);
    }
}
```

---

## ADIM 7 — ARAŞTIRMA İLE BİNA UNLOCK

Araştırma sistemi ile yeni binaları kilit açık yapabilirsin:

### 7.1 Research Node Config Oluştur

`Assets/BuildIdle/Configs/Research/Unlock_Shop.asset`:

| Field | Değer |
|-------|-------|
| `NodeId` | `"unlock_shop"` |
| `DisplayName` | `"Ticaret Bilimi"` |
| `Tier` | 1 |
| `GoldCost` | 10,000 |
| `ResearchTicks` | 300 |

`Assets/BuildIdle/Configs/Research/Unlock_Tower.asset`:
- `NodeId` = `"unlock_tower"`, Tier=2, GoldCost=100,000, ResearchTicks=1,800

### 7.2 Bootstrap'ta Araştırma Tamamlanınca Unlock

```csharp
ResearchService.OnNodeCompleted += (treeId, nodeId) =>
{
    if (nodeId == "unlock_shop")
        UnlockBuilding("shop");
    else if (nodeId == "unlock_tower")
        UnlockBuilding("tower");
};

private void UnlockBuilding(string buildingId)
{
    // PlacementMenu'deki bina listesini güncelle
    FindFirstObjectByType<PlacementMenu>()?.UnlockBuilding(buildingId);
    Debug.Log($"Yeni bina kilidi açıldı: {buildingId}");
}
```

### 7.3 Başlangıçta Hangi Binalar Görünür?

İlk 2 bina (Çardak, Kulübe) baştan açık; Dükkân ve Kule araştırma ile açılır.

Bootstrap'ta başlangıç durumunu ayarla:
```csharp
// PlacementMenu başlangıçta sadece şunları gösterir:
_availableBuildingIds = new List<string> { "hut", "cottage" };
// Araştırma tamamlandıkça: _availableBuildingIds.Add("shop");
```

---

## ADIM 8 — ÜRETİM VE EKONOMİ DENGESİ

### 8.1 Bina Üretimi Hesaplama

| Bina | Level 1 | Level 2 | Level 3 | Level Max |
|------|---------|---------|---------|-----------|
| Çardak | 2/s | 5/s | 10/s | 50/s |
| Kulübe | 8/s | 20/s | 60/s | 200/s |
| Dükkân | 30/s | 75/s | 200/s | 750/s |
| Kule | 200/s | 500/s | 1,500/s | 5,000/s |

Tüm grid doluysa (64 hücre):
- 16 Çardak (1×1): 16 × 50 = 800/s
- 8 Kulübe (1×2): 8 × 200 = 1,600/s
- 4 Dükkân (2×1): 4 × 750 = 3,000/s
- 4 Kule (2×2): 4 × 5,000 = 20,000/s

**Toplam max: ~25,000/s** — buna göre prestige eşiklerini ayarla.

### 8.2 Placement Cost Dengesi

Oyuncu bir binanın maliyetini kaç saniyede kazanmalı?

- Çardak (100 altın): 100 / (pasif gelir 10/s) = 10 saniye → uygun
- Kulübe (500 altın): 500 / 50/s = 10 saniye (2-3 Çardak sahibiyken)
- Dükkân (2,000 altın): 2,000 / 200/s = 10 saniye (araştırmadan sonra)

**Kural:** Her bina için maliyet, mevcut gelirin 10-30 saniyelik kazancı olmalı.

---

## ADIM 9 — UPGRADE TREE (Bina Oyunu için)

`Tools → Endless Engine → Upgrade Tree Editor`

Building oyunlarında upgrade tree bina üretimine etki eder:

```
NodeId: construction_mastery
DisplayName: "İnşaat Ustalığı"
AffectedStat: GeneratorYield    ← Bina servisi bu stat'ı okuyabilir (özel bağlantı gerekir)
EffectType: PercentBonus
EffectPerRank: 0.10
MaxRank: 10
BaseCost: 5,000
```

**Not:** `BuildingService`, `IUpgradeStatProvider` interface'ini doğrudan kullanmaz. Eğer upgrade'lerin bina üretimine etkimesini istiyorsan, `BuildingService.OnTick(dt)` içinde `statProvider.GetMultiplier(StatType.GeneratorYield)` çağırman gerekir. Bu özel bir entegrasyon:

```csharp
// BuildingService'i extend et veya dışarıdan çarpan uygula:
BuildingService.OnBuildingProduced += (instanceId, amount) =>
{
    float upgradeMultiplier = _upgradeTree.GetNode("construction_mastery")?.GetMultiplier() ?? 1f;
    _economy.AddResources(amount * (upgradeMultiplier - 1f));  // Bonus kısım
};
```

---

## ADIM 10 — KAYIT SİSTEMİ

`BuildingService`, `ISaveStateProvider` implemente eder. Bootstrap'ta:

```csharp
_saveService.RegisterStateProvider(_buildingService);
```

Kaydedilen veriler:
- `SaveData.BuildingInstances` — tüm binaların pozisyonu, tipi, tier'ı

**Test:**
1. 3 bina yerleştir, 1 tanesini upgrade et
2. Play durdur
3. Tekrar Play bas
4. Aynı binalar aynı konumda, aynı level'da mı?

---

## ADIM 11 — PRESTİGE

Building oyunlarında prestige tüm binaları sıfırlar ama kalıcı üretim çarpanı kazanır.

`PrestigeConfig.asset`:
- `MinGoldToPrestige` = 1,000,000
- `BaseMultiplierPerPrestige` = 1.5
- `StatsAmplifiedByPrestige` = [GeneratorYield]

Prestige sonrası:
- Tüm binalar yıkılır
- Altın sıfırlanır
- Oyuncu daha hızlı yeniden inşa edebilir (kalıcı çarpan sayesinde)

---

## ADIM 12 — GÖRSEL KURULUM

### 12.1 Bina Sprite'ları

Her `BuildingConfigSO`'nun `Icon` (Sprite) alanı var. Her bina ve her tier için farklı sprite:

```
Assets/BuildIdle/Art/Buildings/
    Hut_L1.png
    Hut_L2.png
    Hut_L3.png
    Hut_L4.png
    Cottage_L1.png
    ...
```

### 12.2 Grid Görünümü

Grid hücrelerinin boyutu bina boyutlarına uygun:
- 1×1 bina: 1 hücre
- 1×2 bina: 2 hücre dikey → `GridCell.Setup()` bu hücreleri işaretle

Çok hücreli binalar için `GridCell.SetBuilding()` çağrılırken tüm kaplanan hücreler güncellenmeli.

---

## ADIM 13 — TEST LİSTESİ

- [ ] Boş hücreye tıklanınca bina seçme menüsü açılıyor
- [ ] Bina satın alınıyor, grid'de görünüyor
- [ ] Bina tick başına üretim yapıyor (altın artıyor)
- [ ] Binaya tıklanınca upgrade menüsü açılıyor
- [ ] Upgrade yapılıyor, üretim artıyor
- [ ] Bina kaldırılıyor, hücre boşalıyor
- [ ] 2×2 bina 4 hücre kaplıyor
- [ ] MaxInstances = 5 olan binadan 6. alınmıyor
- [ ] Play Stop Play → grid layout korunuyor
- [ ] Araştırma tamamlandıkça yeni bina menüde görünüyor
- [ ] Prestige sonrası tüm binalar yıkılıyor
- [ ] `Config Validator` → sıfır hata

---

*Bu rehber Endless Engine v1.3.4 için yazılmıştır.*
