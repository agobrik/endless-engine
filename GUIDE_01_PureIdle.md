# REHBER 01 — Klasik Idle Oyunu
## Cookie Clicker / Adventure Capitalist Tarzı
### Endless Engine ile Sıfırdan Steam'e

> Bu rehberi takip ederek, hiçbir şey bilmesen bile baştan sona çalışan, Steam'de satılabilir bir klasik idle oyunu yapabilirsin. Her adım sırayla yap, atlamadan ilerle.

---

## BU OYUNDA NE VAR?

| Sistem | Ne Yapar |
|--------|----------|
| Generator'lar | Her saniye otomatik altın üretir |
| Upgrade Tree | Generator'ları ve geliri güçlendirir |
| Prestige | Her şeyi sıfırla, kalıcı çarpan kazan |
| Kayıt | Tüm ilerleme otomatik kaydedilir |
| Çevrimdışı Gelir | Oyun kapalıyken de altın birikir |

---

## ADIM 1 — PROJEYİ HAZIRLA

### 1.1 Unity Projesi Aç
1. Unity Hub → **New Project** → **2D (URP)** şablonu seç
2. Proje adını yaz (örn. `IdleEmpire`)
3. **Create Project**

### 1.2 Endless Engine'i Kur
`Window → Package Manager → + (sol üst) → Add package from disk`
→ `Packages/com.endlessengine.idle/package.json` seç → **Open**

Kurulum tamamlandığında menüde `Tools → Endless Engine` görünür.

### 1.3 Örnek Sahneleri İncele (İsteğe Bağlı)
`Window → Package Manager → Endless Engine → Samples → MinimalIdle → Import`
→ `Assets/Samples/MinimalIdle/Scenes/MinimalIdle.unity` aç → Play bas → Çalışıyor mu gör

Bu örnek senin yapacağın oyunun temel iskeletidir.

---

## ADIM 2 — WIZARD İLE İSKELETİ OLUŞTUR

`Tools → Endless Engine → New Game Wizard`

1. Sol panelden **Pure Idle** seç
2. **Game Name** = `IdleEmpire` (ya da kendi oyun adın)
3. **Generate** bas

Wizard şunları oluşturur:
```
Assets/IdleEmpire/
    Configs/
        EconomyConfig.asset
        PrestigeConfig.asset
        GeneratorDatabase.asset
        UpgradeTreeConfig.asset
        SchemaVersion.asset
        RealmIdentityConfig.asset
    Scenes/
        IdleEmpire.unity
    Scripts/
        IdleEmpireBootstrap.cs
        IdleEmpireUI.cs
```

**Hemen test et:**
`Assets/IdleEmpire/Scenes/IdleEmpire.unity` → çift tıkla → Play bas
→ Sahne çalışmalı, altın birikiyor olmalı

---

## ADIM 3 — GENERATOR'LARI TASARLA

Generator = pasif gelir kaynağı. TickEngine her saniye bir tick atar, PassiveIncomeService bu tick'te tüm generator'ların yield'larını toplayıp EconomyService'e altın ekler.

### 3.1 Generator Editor'ü Aç

`Tools → Endless Engine → Generator Editor`

Sol panelde `Assets/IdleEmpire/Configs/GeneratorDatabase.asset` otomatik yüklenir.

### 3.2 Kaç Generator Olmalı?

Steam'de satılabilir bir klasik idle için **10-15 generator** idealdir. Oyuncu her birini satın almak için yeterince zaman geçirmeli ama son generator çok uzak hissettirmemeli.

### 3.3 Generator Hiyerarşisi Kur

**Altın Kural:** Her generator bir öncekinin ~10x maliyeti, ~6-8x daha güçlü yield'ı olmalı.

`+ Add` butonuna bas ve şu 10 generator'ı oluştur:

| # | GeneratorId | DisplayName | BaseYieldPerSecond | BaseCost | CostScalingFactor |
|---|------------|-------------|-------------------|---------|------------------|
| 1 | `mine` | Altın Madeni | 0.1 | 15 | 1.15 |
| 2 | `farm` | Çiftlik | 0.6 | 100 | 1.15 |
| 3 | `factory` | Fabrika | 4.0 | 1,100 | 1.15 |
| 4 | `power_plant` | Enerji Santrali | 25 | 12,000 | 1.15 |
| 5 | `lab` | Araştırma Laboratuvarı | 180 | 130,000 | 1.15 |
| 6 | `oil_rig` | Petrol Kuyusu | 1,400 | 1,400,000 | 1.15 |
| 7 | `space_station` | Uzay İstasyonu | 12,000 | 20,000,000 | 1.15 |
| 8 | `time_machine` | Zaman Makinesi | 100,000 | 300,000,000 | 1.15 |
| 9 | `singularity` | Singularite | 1,000,000 | 5,000,000,000 | 1.15 |
| 10 | `multiverse` | Çoklu Evren | 10,000,000 | 100,000,000,000 | 1.15 |

**Her generator için ayarla:**
- `GeneratorId` — benzersiz, küçük harf, alt çizgi. Yayın sonrası asla değiştirme.
- `DisplayName` — oyuncunun göreceği isim
- `BaseYieldPerSecond` — tablodan
- `BaseCost` — tablodan
- `CostScalingFactor` = 1.15 (her yeni kopya öncekinin 1.15x'i kadar pahalı)
- `MaxCount` = -1 (sınırsız)

### 3.4 Unlock Sistemi Kur

Generator'lar baştan hepsi görünürse oyuncu bunalır. 3. generator'dan itibaren öncekini almadan görünmesin:

Her generator için:
- `UnlockPrerequisite` = bir önceki GeneratorConfigSO asset'i (sürükle)
- `UnlockRequirement` = 1 (1 tane alınınca bir sonraki açılır)

İstenirse daha sıkı yapabilirsin: `UnlockRequirement = 10` → öncekinden 10 tane almadan görünmez.

### 3.5 GeneratorDatabase'e Ekle

Tüm generator asset'lerini `GeneratorDatabase.asset`'in `Generators` array'ine sürükle. Sıra önemli — UI'da bu sırayla görünür.

### 3.6 CostScalingFactor Rehberi

| Değer | Etki | Ne Zaman Kullan |
|-------|------|----------------|
| 1.07 | Çok ucuz — hızlı ilerleme | Casual oyunlar |
| 1.12 | Hafif zorluk | Yeni başlayanlar için |
| **1.15** | **Klasik idle dengesi** | **Çoğu oyun için** |
| 1.20 | Zorlu | Her kopya belirgin şekilde pahalı |
| 1.30 | Sert | Kasıtlı yavaşlatma istenen geç oyun |

---

## ADIM 4 — UPGRADE TREE TASARLA

Upgrade Tree = oyuncunun altınla satın aldığı kalıcı güçlendirmeler. Generator'ların yield'ını, gelir hızını, prestige bonuslarını artırır.

### 4.1 Upgrade Tree Editor

`Tools → Endless Engine → Upgrade Tree Editor`

`Assets/IdleEmpire/Configs/UpgradeTreeConfig.asset` dosyasını aç.

### 4.2 Node Ekleme

Sol üstten `+ Add Node` → sağ panelden ayarla:

**Her node için doldurulması zorunlu alanlar:**

| Alan | Açıklama | Örnek |
|------|----------|-------|
| `NodeId` | Benzersiz ID, asla değiştirme | `"prod_01"` |
| `DisplayName` | Oyuncunun gördüğü isim | `"Verimli Maden"` |
| `AffectedStat` | Hangi stat etkilenir | `GeneratorYield` |
| `EffectType` | `PercentBonus` veya `FlatBonus` | `PercentBonus` |
| `EffectPerRank` | Her rankta etki miktarı | `0.10` (= %10) |
| `MaxRank` | Kaç kez alınabilir | `5` |
| `BaseCost` | İlk rank maliyeti | `200` |
| `CostScalingFactor` | Her rankta çarpan | `1.5` |

### 4.3 Klasik Idle için Upgrade Planı

Steam'de satılabilir bir oyun için minimum **40-60 upgrade node** olmalı. Şu yapıyı uygula:

**BLOK 1 — Üretim Artışı (15 node)**
Her generator için 1 özel node + genel node'lar:

```
NodeId: mine_boost_01
DisplayName: "Maden Verimliliği I"
AffectedStat: GeneratorYield
EffectType: PercentBonus
EffectPerRank: 0.15   ← %15 artış
MaxRank: 5
BaseCost: 100
CostScalingFactor: 1.5
```

```
NodeId: mine_boost_02
DisplayName: "Maden Verimliliği II"
AffectedStat: GeneratorYield
EffectType: PercentBonus
EffectPerRank: 0.25
MaxRank: 5
BaseCost: 5,000
CostScalingFactor: 2.0
PrerequisiteNodeIDs: ["mine_boost_01"]   ← Önce bunu al
PrestigeGateRequirement: 0
```

Tüm generator'lar için bu şemayı uygula.

**BLOK 2 — Global Gelir Artışı (10 node)**

```
NodeId: global_yield_01
DisplayName: "Endüstri Devrimi"
AffectedStat: IdleYieldRate
EffectType: PercentBonus
EffectPerRank: 0.20
MaxRank: 5
BaseCost: 50,000
CostScalingFactor: 3.0
```

**BLOK 3 — Offline Gelir (5 node)**

```
NodeId: offline_01
DisplayName: "Otomatik Yönetim"
AffectedStat: OfflineYieldRate
EffectType: PercentBonus
EffectPerRank: 0.10
MaxRank: 5
BaseCost: 10,000
CostScalingFactor: 2.0
```

**BLOK 4 — Prestige Bonusları (10 node, PrestigeGateRequirement=1)**

```
NodeId: prestige_power_01
DisplayName: "Prestige Ustalığı"
AffectedStat: PrestigeMultiplier
EffectType: PercentBonus
EffectPerRank: 0.05
MaxRank: 10
BaseCost: 1,000
CostScalingFactor: 2.0
PrestigeGateRequirement: 1   ← İlk prestige'den sonra görünür
```

**BLOK 5 — Başlangıç Bonusu (5 node, PrestigeGateRequirement=2)**

```
NodeId: start_bonus_01
DisplayName: "Hızlı Başlangıç"
AffectedStat: StartingGoldBonus
EffectType: FlatBonus
EffectPerRank: 500
MaxRank: 5
BaseCost: 5,000
CostScalingFactor: 2.5
PrestigeGateRequirement: 2
```

### 4.4 Node'ları Bağla

Graph'ta bir node'un sağ tarafındaki noktayı başka bir node'un sol tarafına sürükle. Bağlantı kurulunca hedef node'un `PrerequisiteNodeIDs` listesi otomatik güncellenir.

### 4.5 SelectionWeight Ayarla

`SelectionWeight` — bu node'un upgrade kart olarak seçilme şansı. 1-100 arası.
- Temel, sık çıkması gereken node'lar: 50-80
- Nadir, çok güçlü node'lar: 5-15

### 4.6 Kaydet

`Save` bas. Her değişiklikten sonra kaydetmeyi unutma.

---

## ADIM 5 — EKONOMİ CONFIG AYARLA

`Assets/IdleEmpire/Configs/EconomyConfig.asset` dosyasını Proje panelinden seç ve Inspector'dan ayarla:

| Field | Değer | Neden |
|-------|-------|-------|
| `StartingGold` | 0 | Her prestige sıfırdan başlar |
| `ResourceHardCap` | 1_000_000_000_000 | 1 trilyon tavan |
| `NumberBackend` | DoubleNumber | 1e15'e kadar yeterli |
| `OfflineCapHours` | 8 | 8 saate kadar çevrimdışı gelir |
| `ActiveRunStateOfflineModifier` | 1.0 | Pure idle'da run state yok |

> **1 trilyon yetmeyecek mi?** — Eğer oyuncu 10+ prestige yapacaksa BigDouble gerekebilir. Bunu daha sonra test ederken anlayacaksın. Şimdilik DoubleNumber ile başla.

---

## ADIM 6 — PRESTİGE CONFIG AYARLA

`Assets/IdleEmpire/Configs/PrestigeConfig.asset`:

| Field | Değer | Açıklama |
|-------|-------|----------|
| `MinWaveForPrestige` | 0 | Wave oyunu değil, wave şartı yok |
| `MinGoldToPrestige` | 100,000 | İlk oyunda ~5-10 dakikada ulaşılacak değer |
| `MaxPrestigeCount` | 0 | Sınırsız prestige |
| `BaseMultiplierPerPrestige` | 1.5 | Her prestige'de kalıcı çarpan ×1.5 |
| `MaxPermanentMultiplier` | 10,000 | 10.000x tavan |
| `StatsAmplifiedByPrestige` | [GeneratorYield, IdleYieldRate] | Bu stat'lar çarpandan etkilenir |

### MinGoldToPrestige Nasıl Belirlenir?

İlk prestige ~20-30 dakikada gelmeli. Bunu belirlemek için:
1. Play bas, üretim hızına bak
2. `Economy Simulator`'ü aç (`Tools → Endless Engine → Economy Simulator`)
3. Session = 30, Session length = 25 dakika
4. "İlk prestige kaçıncı oturumda geliyor?" sorusunun cevabı 1. oturumda olmamalı, 2-3. oturumda olmalı

---

## ADIM 7 — BOOTSTRAP'I ANLA VE KONTROL ET

Wizard'ın ürettiği `IdleEmpireBootstrap.cs` dosyasını `Assets/IdleEmpire/Scripts/` altında bul ve aç.

Şunların HEPSİ bulunmalı:

```csharp
[DefaultExecutionOrder(-500)]
public class IdleEmpireBootstrap : MonoBehaviour
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
        // 1. Sayı motorunu ayarla — HER ŞEYDEN ÖNCE
        BigNumberFactory.Configure(_economyConfig.NumberBackend);

        // 2. Config'leri yükle
        ConfigRegistry.InjectForTesting(
            economy:  _economyConfig,
            schema:   _schemaVersion,
            prestige: _prestigeConfig,
            realm:    _realmConfig);

        // 3. UpgradeTree hazırla (config yüklendikten hemen sonra)
        _upgradeTreeService?.HandleConfigsLoaded();

        // 4. EconomyService başlat
        _economyService.Initialize(_upgradeTreeService, _saveService);

        // 5. GeneratorSystem başlat
        _generatorSystem.Initialize(
            _generatorDatabase.Generators,
            _economyService,
            _saveService);

        // 6. PassiveIncomeService başlat
        _passiveIncomeService.Initialize(
            _generatorSystem,
            _economyService,
            gameFlow: null);

        // 7. Kayıt sağlayıcıları kaydet
        _saveService.RegisterStateProvider(_economyService);
        _saveService.RegisterStateProvider(_upgradeTreeService);
        _saveService.RegisterStateProvider(_generatorSystem);

        // PrestigeStateManager yoksa kaydet
        // _saveService.RegisterStateProvider(_prestigeManager);

        // 8. Kayıtları yükle (async — bitene kadar bekle)
        bool done = false;
        _ = _saveService.LoadAsync().ContinueWith(_ => done = true,
            System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        yield return new WaitUntil(() => done);

        // 9. Hazır!
        Debug.Log("[IdleEmpire] Oyun başladı.");
    }
}
```

**Eğer wizard bunların herhangi birini üretmediyse**, eksik satırı manuel ekle.

### 7.1 Sahneyi Düzenle

`IdleEmpire.unity` sahnesini aç. Hiyerarşide şunlar olmalı:

```
Bootstrap (GameObject)
    ├── IdleEmpireBootstrap  (component)
    ├── SaveService          (component)
    ├── EconomyService       (component)
    ├── UpgradeTreeService   (component)
    ├── GeneratorSystem      (component)
    ├── PassiveIncomeService (component)
    └── TickEngine           (component)

Canvas
    └── IdleEmpireUI (component)
```

Bootstrap GameObject'i seç. Inspector'da tüm `[SerializeField]` alanlarını doldur:
- Sahne component'larını sürükle (SaveService, EconomyService vb.)
- Config asset'lerini `Assets/IdleEmpire/Configs/` klasöründen sürükle

---

## ADIM 8 — UI YAPI

### 8.1 Otomatik HUD

Wizard'ın oluşturduğu `IdleEmpireUI.cs` şu olayları otomatik dinler:

| Event | UI Elemanı | Ne Gösterir |
|-------|-----------|-------------|
| `EconomyService.OnResourcesChanged` | `GoldLabel` (Text/TMP) | `"Altın: 1.234"` |
| `EconomyService.OnResourcesChanged` | `IncomeLabel` | `"Gelir: 45/s"` |
| `GeneratorSystem.OnGeneratorPurchased` | Generator kartları | Sayım günceller |
| `PrestigeStateManager.OnPrestigeComplete` | `PrestigeLabel` | Prestige sayısı |

UI elemanları isimle bulunur — **şu GameObject isimlerini kullan:**
- `GoldLabel` — altın miktarı
- `IncomeLabel` — saniyedeki gelir
- `PrestigeLabel` — prestige sayısı
- `BuyGeneratorButton` — ilk generator satın alma butonu
- `PrestigeButton` — prestige butonu

### 8.2 Generator Kartı Yaz

Her generator için bir kart UI oluştur. Prefab'a `GeneratorCard.cs` ekle:

```csharp
public class GeneratorCard : MonoBehaviour
{
    [SerializeField] private string _generatorId;
    [SerializeField] private TMP_Text _nameLabel;
    [SerializeField] private TMP_Text _costLabel;
    [SerializeField] private TMP_Text _countLabel;
    [SerializeField] private TMP_Text _yieldLabel;
    [SerializeField] private Button   _buyButton;
    [SerializeField] private Button   _buyX10Button;

    private GeneratorSystem  _generators;
    private EconomyService   _economy;

    public void Inject(GeneratorSystem generators, EconomyService economy)
    {
        _generators = generators;
        _economy    = economy;

        EconomyService.OnResourcesChanged   += OnResourcesChanged;
        GeneratorSystem.OnGeneratorPurchased += OnGeneratorPurchased;

        Refresh();
    }

    private void OnDestroy()
    {
        EconomyService.OnResourcesChanged   -= OnResourcesChanged;
        GeneratorSystem.OnGeneratorPurchased -= OnGeneratorPurchased;
    }

    private void OnResourcesChanged(double current, double delta) => Refresh();
    private void OnGeneratorPurchased(string id) { if (id == _generatorId) Refresh(); }

    private void Refresh()
    {
        int    count = _generators.GetCount(_generatorId);
        long   cost  = _generators.GetNextCost(_generatorId);
        double yield = _generators.GetBulkCostDisplay(_generatorId, BulkPurchaseMode.Single);

        _nameLabel.text  = _generatorId;  // DisplayName'e erişmek için config'den oku
        _countLabel.text = $"Sahip: {count}";
        _costLabel.text  = $"Maliyet: {FormatNumber(cost)}";
        _buyButton.interactable  = _economy.CurrentResources >= cost;
    }

    public void OnBuyClick()
    {
        _generators.TryPurchase(_generatorId);
    }

    public void OnBuyX10Click()
    {
        _generators.TryPurchaseBulk(_generatorId, BulkPurchaseMode.Count10);
    }

    private string FormatNumber(double n)
    {
        if (n >= 1e9)  return $"{n/1e9:F1}M";
        if (n >= 1e6)  return $"{n/1e6:F1}M";
        if (n >= 1e3)  return $"{n/1e3:F1}K";
        return $"{n:F0}";
    }
}
```

### 8.3 Prestige Butonu Yaz

```csharp
public class PrestigeButton : MonoBehaviour
{
    [SerializeField] private Button   _button;
    [SerializeField] private TMP_Text _multiplierText;
    [SerializeField] private TMP_Text _countText;

    private PrestigeStateManager _prestige;

    public void Inject(PrestigeStateManager prestige)
    {
        _prestige = prestige;
        PrestigeStateManager.OnPrestigeComplete += OnPrestigeComplete;
        UpdateUI();
    }

    private void OnDestroy()
    {
        PrestigeStateManager.OnPrestigeComplete -= OnPrestigeComplete;
    }

    private void Update()
    {
        _button.interactable = _prestige != null && _prestige.CanPrestige;
    }

    private void OnPrestigeComplete(int count, float multiplier) => UpdateUI();

    private void UpdateUI()
    {
        if (_prestige == null) return;
        _multiplierText.text = $"×{_prestige.GetPermanentMultiplier():F2} kalıcı çarpan";
        _countText.text      = $"Prestige: {_prestige.PrestigeCount}";
    }

    public void OnPrestigeClick()
    {
        _prestige?.TryPrestige();
    }
}
```

### 8.4 Upgrade Kart Yaz

```csharp
public class UpgradeCard : MonoBehaviour
{
    [SerializeField] private string   _nodeId;
    [SerializeField] private TMP_Text _nameLabel;
    [SerializeField] private TMP_Text _costLabel;
    [SerializeField] private TMP_Text _rankLabel;
    [SerializeField] private TMP_Text _effectLabel;
    [SerializeField] private Button   _buyButton;

    private UpgradeTreeService _upgrades;
    private EconomyService     _economy;

    public void Inject(UpgradeTreeService upgrades, EconomyService economy)
    {
        _upgrades = upgrades;
        _economy  = economy;

        EconomyService.OnResourcesChanged += OnResourcesChanged;
        EconomyService.OnUpgradePurchased += OnUpgradePurchased;
        Refresh();
    }

    private void OnDestroy()
    {
        EconomyService.OnResourcesChanged -= OnResourcesChanged;
        EconomyService.OnUpgradePurchased -= OnUpgradePurchased;
    }

    private void OnResourcesChanged(double c, double d) => Refresh();
    private void OnUpgradePurchased(string id, long cost) { if (id == _nodeId) Refresh(); }

    private void Refresh()
    {
        if (!_upgrades.IsReady) return;

        var  node    = _upgrades.GetNode(_nodeId);
        long cost    = _upgrades.GetNodeCost(_nodeId);
        bool canBuy  = _upgrades.IsNodeAvailable(_nodeId);

        if (node == null) { gameObject.SetActive(false); return; }

        _nameLabel.text   = node.DisplayName;
        _costLabel.text   = $"Maliyet: {FormatNumber(cost)}";
        _rankLabel.text   = $"Rank {node.CurrentRank}/{node.MaxRank}";
        _effectLabel.text = $"+{node.Definition.EffectPerRank * 100:F0}% / rank";
        _buyButton.interactable = canBuy && _economy.CurrentResources >= cost;

        // MaxRank'a ulaştıysa gizle
        if (node.CurrentRank >= node.MaxRank)
            gameObject.SetActive(false);
    }

    public void OnBuyClick()
    {
        _economy.TryPurchase(_nodeId);
    }

    private string FormatNumber(double n) { /* ... */ return n.ToString("N0"); }
}
```

---

## ADIM 9 — EKONOMİ SİMÜLASYONU VE DENGE

`Tools → Endless Engine → Economy Simulator`

### 9.1 Config'leri Bağla

Sol panelde:
- Economy Config → `EconomyConfig.asset`
- Prestige Config → `PrestigeConfig.asset`
- Generator DB → `GeneratorDatabase.asset`

### 9.2 Parametreler

| Parametre | Değer | Açıklama |
|-----------|-------|----------|
| Sessions | 30 | 30 oturum simüle et |
| Session length | 25 dakika | Oturum başına ortalama süre |
| Offline hours | 6 | Oturumlar arası çevrimdışı |
| Auto-prestige | ✓ | Uygun olunca prestige et |

### 9.3 Hedef Değerler

| Metrik | İyi | Kötü |
|--------|-----|------|
| İlk prestige | 2-4. oturum | 1. oturum veya 8+ oturum |
| Prestige başı çarpan artışı | ×1.5 - ×3.0 | ×1.1 (çok az) veya ×10+ (fazla) |
| 30. oturumda toplam çarpan | ×100 - ×10,000 | — |
| Oturum başına büyüme | Öncekinin 2-5x'i | Sabit veya düşüyor |

### 9.4 Sorun Giderme

| Sorun | Çözüm |
|-------|-------|
| İlk prestige 1. oturumda geliyor | `MinGoldToPrestige` ×10 artır |
| İlk prestige hiç gelmiyor | `MinGoldToPrestige` ÷5 düşür |
| Geç oyunda büyüme durdu | Son 2 generator yield'ını ×3 artır |
| Oyun çok hızlı bitiyor | `CostScalingFactor` 1.15 → 1.20 yap |
| Hard cap'e çarpıyor | `ResourceHardCap` ×1000 artır veya `BigDouble` geç |

---

## ADIM 10 — KAYIT SİSTEMİ TEST ET

### 10.1 Temel Test

1. Play bas → biraz altın birikt → generator al → upgrade al
2. Play'i DURDUR
3. Tekrar Play bas
4. Altın, generator sayısı, upgrade rank'ları geri geldi mi?

**Gelmiyorsa:** Bootstrap'ta şu satırlar var mı?
```csharp
_saveService.RegisterStateProvider(_economyService);
_saveService.RegisterStateProvider(_upgradeTreeService);
_saveService.RegisterStateProvider(_generatorSystem);
```

### 10.2 Prestige Kayıt Testi

1. Prestige yap
2. Play durdur
3. Tekrar Play bas
4. Prestige sayısı ve kalıcı çarpan geri geldi mi?

**Gelmiyorsa:** `_saveService.RegisterStateProvider(_prestigeManager)` satırı eksik.

### 10.3 Çevrimdışı Gelir Testi

1. Play bas, biraz generator al
2. Play durdur
3. Bilgisayar saatini 2 saat ilerlet (ya da EconomyConfig.OfflineCapHours değerini geçici olarak 0.01'e düşür)
4. Tekrar Play bas
5. "Çevrimdışıyken X altın kazandın!" mesajı geldi mi?

---

## ADIM 11 — GÖRSEL VE SES

### 11.1 Arka Plan ve Tema

Pure Idle için önerilen görsel stil: **soyut/minimalist** veya **tematik ikon setleri**

- Generator ikonları: Maden için balta, Fabrika için dişli, Uzay için roket
- İkonları `ClickTarget` prefab'larına veya kartlardaki `Image` component'larına ata
- Arka plan: gradient veya basit bir texture yeterli

### 11.2 Ses

`AudioConfigSO` oluştur: `Assets → Create → Endless Engine → Config → Audio Config`

| Alan | Açıklama |
|------|----------|
| `PurchaseClip` | Generator satın alma sesi |
| `UpgradeClip` | Upgrade alma sesi |
| `PrestigeClip` | Prestige sesi |
| `AmbientClip` | Arka plan müziği |

`AudioService` component'ını Bootstrap'a ekle ve config'i ata.

### 11.3 Sayı Formatı

Oyuncunun gördüğü büyük sayıları formatla. `IdleEmpireUI.cs`'e ekle:

```csharp
public static string FormatGold(double n)
{
    if (n >= 1e12) return $"{n/1e12:F2}T";  // Trilyon
    if (n >= 1e9)  return $"{n/1e9:F2}B";   // Milyar
    if (n >= 1e6)  return $"{n/1e6:F2}M";   // Milyon
    if (n >= 1e3)  return $"{n/1e3:F2}K";   // Bin
    return $"{n:F0}";
}
```

---

## ADIM 12 — ÇEVRIMDIŞI GELİR VE BILDIRIM

Oyuncu uygulamayı kapatıp açtığında geçen süredeki altın otomatik eklenir (`SaveService.LoadAsync()` içinde). Bunu UI'da göster:

```csharp
// Bootstrap'ta SaveService.OnSaveLoaded event'ini dinle
_saveService.OnSaveLoaded += (saveData, isNewGame) =>
{
    if (!isNewGame && saveData.OfflineGoldEarned > 0)
        ShowOfflineEarningsPopup(saveData.OfflineGoldEarned, saveData.OfflineTimeSeconds);
};
```

Popup içinde:
```
"Çevrimdışıyken 4 saat 23 dakika geçti.
1.234.567 altın kazandın!"
```

---

## ADIM 13 — STEAM ENTEGRASYONU (Yayın için)

### 13.1 Steamworks.NET Kur

`Window → Package Manager → + → Add package from git URL`
→ `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net`

### 13.2 App ID

Proje kök klasörüne `steam_appid.txt` oluştur → içine Steam App ID'nizi yaz (test için `480`)

### 13.3 Achievement'ları Bağla

`MilestoneConfigSO` oluştur (her achievement için bir tane):

```
Assets → Create → Endless Engine → Config → Milestone Config

MilestoneId: "first_prestige"
DisplayName: "İlk Prestij"
```

Sahnedeki Bootstrap'a `SteamAchievementBridge` component'ı ekle:
- `_mappings` listesine ekle: MilestoneId=`"first_prestige"` → SteamApiName=`"ACH_FIRST_PRESTIGE"`

`MilestoneTracker.OnMilestoneCompleted` tetiklendiğinde bridge otomatik Steam achievement açar.

### 13.4 Leaderboard

```csharp
// En yüksek prestige sayısını gönder
steamLeaderboardService.SubmitScore(
    "most_prestiges",
    playerName,
    (long)_prestigeManager.PrestigeCount);
```

---

## ADIM 14 — FINAL TEST LİSTESİ

Yayından önce her maddeyi işaretle:

- [ ] Play → Stop → Play: altın, generator, upgrade geri geliyor
- [ ] Prestige yapılıyor, kalıcı çarpan artıyor
- [ ] Prestige sonrası altın ve generator'lar sıfırlanıyor
- [ ] Prestige sırasında Play durdur → tekrar aç → crash recovery çalışıyor
- [ ] Çevrimdışı gelir geliyor (saat ilerlet, test et)
- [ ] `Tools → Endless Engine → Config Validator` → sıfır hata
- [ ] `Tools → Endless Engine → ID Registry` → sıfır çakışma
- [ ] Economy Simulator → 30+ oturum → dengeli görünüyor
- [ ] İlk 5 dakika oynanabilir (çok yavaş değil)
- [ ] 30. prestige'de hâlâ ilerleme var (çok hızlı bitmiyor)
- [ ] Tüm sayılar okunabilir formatta görünüyor (1.23M gibi)
- [ ] Ses efektleri çalışıyor
- [ ] Steam achievement'ları test edildi (App ID 480 ile)
- [ ] Build al → exe çalışıyor

---

## HIZLI BAŞVURU

### Servis Başlatma Sırası (Zorunlu)
```
1. BigNumberFactory.Configure()
2. ConfigRegistry.InjectForTesting()
3. upgradeTreeService.HandleConfigsLoaded()
4. economyService.Initialize(upgradeTree, save)
5. generatorSystem.Initialize(configs, economy, save)
6. passiveIncomeService.Initialize(generators, economy, null)
7. saveService.RegisterStateProvider() × N
8. await saveService.LoadAsync()
```

### Para Nasıl Akar
```
TickEngine (1 Hz)
  → PassiveIncomeService.HandleTick()
    → generatorSystem.CalculateTotalYieldBig()
      → economyService.AddResources(miktar)
        → EconomyService.OnResourcesChanged tetiklenir
          → UI güncellenir
```

### Upgrade Nasıl Çalışır
```
Oyuncu butona tıklar
  → economyService.TryPurchase(nodeId)
    → maliyet kesilir
    → upgradeTreeService.IncrementNodeRank(nodeId)
    → EconomyService.OnUpgradePurchased tetiklenir
      → UI güncellenir
    → Bir sonraki tick'te passiveIncomeService yeni stat'ı okur
```

### Kritik StatType'lar
| StatType | Etkisi |
|----------|--------|
| `GeneratorYield` | Pasif gelir çarpanı |
| `IdleYieldRate` | Global gelir çarpanı |
| `OfflineYieldRate` | Çevrimdışı gelir |
| `PrestigeMultiplier` | Prestige bonus çarpanı |
| `StartingGoldBonus` | Prestige sonrası başlangıç altını |

---

*Bu rehber Endless Engine v1.3.4 için yazılmıştır. Tüm API'ler kaynak koddan doğrulanmıştır.*
