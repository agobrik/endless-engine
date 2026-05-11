# REHBER 04 — Merge Idle
## Merge Dragons / Merge Mansion Tarzı
### Endless Engine ile Sıfırdan Steam'e

> Bu rehberde eşyaları sürükleyip birleştiren, tier zinciri boyunca ilerleyen, ekonomiye bağlı, save/load sistemi olan bir merge idle oyunu yapacaksın.

---

## BU OYUNDA NE VAR?

| Sistem | Ne Yapar |
|--------|----------|
| Merge Board | Grid üzerinde eşyaları sürükle, aynı tier olanları birleştir |
| Merge Zinciri | Tier 1 + Tier 1 → Tier 2, Tier 2 + Tier 2 → Tier 3 vb. |
| Altın Bonusu | Her merge işleminde altın kazanılır |
| Inventory | Eşya sayımı ve stack yönetimi |
| Ekonomi | Altın birikiyor, eşya satın alınabilir |
| Generator'lar | Zaman geçtikçe board'a otomatik eşya düşer |
| Kayıt | Tüm board state kaydedilir |

---

## ADIM 1 — PROJEYİ HAZIRLA

### 1.1 Unity Projesi

1. Unity Hub → **New Project** → **2D (URP)**
2. Proje adı: `MergeIdle`
3. Package Manager → Endless Engine kur

### 1.2 Wizard

`Tools → Endless Engine → New Game Wizard`
1. **Merge Idle** seç
2. Game Name = `MergeIdle`
3. **Generate**

Oluşturulur:
```
Assets/MergeIdle/
    Configs/
        EconomyConfig.asset
        GeneratorDatabase.asset
        SchemaVersion.asset
        PrestigeConfig.asset
        RealmIdentityConfig.asset
    Items/           ← Burası boş, sen dolduracaksın
    Scenes/
        MergeIdle.unity
    Scripts/
        MergeIdleBootstrap.cs
        MergeIdleUI.cs
```

---

## ADIM 2 — SİSTEMLER VE PARA AKIŞI

Merge oyunlarında para akışı şöyle çalışır:

```
KAYNAK 1 — Merge İşlemi:
Oyuncu aynı tier'daki 2 eşyayı sürükleyip birleştirir →
    MergeService.TryMerge(item) çağrılır →
        MergeRule.GoldBonus → EconomyService.AddResources(goldBonus)
        Yeni item (bir üst tier) oluşturulur

KAYNAK 2 — Eşya Satma (İsteğe Bağlı):
Oyuncu bir eşyayı "sat" butonuna sürükler →
    EconomyService.AddResources(item.SellValue)
    InventoryService'den kaldırılır

KAYNAK 3 — Pasif Generator:
TickEngine → PassiveIncomeService → EconomyService
(Ayrıca generator'lar board'a eşya da düşürebilir — özel script gerekir)
```

### Merge Sistemi Nasıl Çalışır?

```
MergeConfigSO:
    MergeGroupId = "gems"
    Rules:
        Rule(InputTier=1, ResultItem=GemT2, GoldBonus=5)
        Rule(InputTier=2, ResultItem=GemT3, GoldBonus=20)
        Rule(InputTier=3, ResultItem=GemT4, GoldBonus=100)

Oyuncu GemT1 + GemT1 sürükler →
    MergeService.TryMerge(GemT1) →
        GetRule(tier=1) → Rule bulundu →
        ResultItem = GemT2 oluşturulur →
        GoldBonus = 5 → EconomyService.AddResources(5) →
        MergeService.OnMergeCompleted tetiklenir →
        UI: eski iki eşyayı kaldır, yeni eşyayı koy
```

---

## ADIM 3 — MERGE ZİNCİRİ TASARLA

### 3.1 Tema Seç

Merge oyunları için tema önerileri:
- **Mücevherler:** Kömür → Taş → Kristal → Yakut → Elmas → Pırlanta
- **Bitkiler:** Tohum → Fide → Çiçek → Meyve → Ağaç → Orman
- **Hayvanlar:** Yavru → Büyük → Dev → Efsanevi
- **Silahlar:** Ahşap → Demir → Çelik → Altın → Kristal → Efsanevi

**Bu rehber için "Mücevher" teması kullanıyoruz.**

### 3.2 Item Config'leri Oluştur

Her tier için ayrı bir `ItemConfigSO` asset oluştur.

`Assets → Create → Endless Engine → Loot → Item Config`

`Assets/MergeIdle/Items/` klasörüne kaydet:

**GemT1.asset (Tier 1 — Kömür)**

| Field | Değer |
|-------|-------|
| `ItemId` | `"gem_t1"` |
| `DisplayName` | `"Kömür"` |
| `Description` | `"En ham haliyle değerli mineral."` |
| `Rarity` | `Common` |
| `MaxStackSize` | 99 |
| `MergeGroupId` | `"gems"` ← Hangi merge grubunda |
| `MergeTier` | 1 ← Bu eşyanın tier'ı |

**GemT2.asset (Tier 2 — Taş)**

| Field | Değer |
|-------|-------|
| `ItemId` | `"gem_t2"` |
| `DisplayName` | `"Ham Taş"` |
| `Rarity` | `Common` |
| `MergeGroupId` | `"gems"` |
| `MergeTier` | 2 |

**GemT3.asset (Tier 3 — Kristal)**

| Field | Değer |
|-------|-------|
| `ItemId` | `"gem_t3"` |
| `DisplayName` | `"Kristal"` |
| `Rarity` | `Uncommon` |
| `MergeGroupId` | `"gems"` |
| `MergeTier` | 3 |

**GemT4.asset (Tier 4 — Yakut)**

| Field | Değer |
|-------|-------|
| `ItemId` | `"gem_t4"` |
| `DisplayName` | `"Yakut"` |
| `Rarity` | `Rare` |
| `MergeGroupId` | `"gems"` |
| `MergeTier` | 4 |

**GemT5.asset (Tier 5 — Elmas)**

| Field | Değer |
|-------|-------|
| `ItemId` | `"gem_t5"` |
| `DisplayName` | `"Elmas"` |
| `Rarity` | `Epic` |
| `MergeGroupId` | `"gems"` |
| `MergeTier` | 5 |

**GemT6.asset (Tier 6 — Pırlanta)**

| Field | Değer |
|-------|-------|
| `ItemId` | `"gem_t6"` |
| `DisplayName` | `"Pırlanta"` |
| `Rarity` | `Legendary` |
| `MergeGroupId` | `"gems"` |
| `MergeTier` | 6 |

> **Kaç tier olmalı?** 6-8 tier ideal. Çok az (3-4) oyun hızlı biter. Çok fazla (12+) kullanıcı kaybolur.

### 3.3 İkinci Merge Grubu (Çeşitlilik)

Oyunu zenginleştirmek için ikinci bir zincir ekle: **Savaş Eşyaları**

`Assets/MergeIdle/Items/` → Weapon_T1.asset'ten Weapon_T5.asset'e kadar oluştur:
- `MergeGroupId` = `"weapons"` (gems'ten farklı!)
- Tier 1: "Tahta Kılıç", Tier 2: "Demir Kılıç" ... Tier 5: "Efsanevi Kılıç"

**Not:** Farklı gruplar birbirleriyle birleştirilemez — MergeGroupId eşleşmesi şart.

---

## ADIM 4 — MERGE CONFIG OLUŞTUR

Her merge grubu için bir `MergeConfigSO` asset.

`Assets → Create → Endless Engine → Merge → Merge Config`

### GemsMergeConfig.asset

| Field | Değer |
|-------|-------|
| `ConfigId` | `"gems_merge"` |
| `MergeGroupId` | `"gems"` ← ItemConfigSO.MergeGroupId ile AYNI olmalı |

`Rules` listesine sırayla ekle (Inspector'da + butonuyla):

| InputTier | ResultItem | GoldBonus |
|-----------|-----------|-----------|
| 1 | GemT2.asset (sürükle) | 0 |
| 2 | GemT3.asset | 5 |
| 3 | GemT4.asset | 20 |
| 4 | GemT5.asset | 100 |
| 5 | GemT6.asset | 500 |

> **GoldBonus Stratejisi:** Erken tier'larda 0 (altın çok az), geç tier'larda büyük bonus. Oyuncuyu yüksek tier hedeflemeye motive eder.

### WeaponsMergeConfig.asset

Benzer şekilde, `MergeGroupId = "weapons"` olan kuralları ekle.

---

## ADIM 5 — INVENTORY SERVICE ANLAMA

`InventoryService`, tüm eşyaların miktarını takip eder. `MergeService`'e bağımlıdır.

Kullanım:
```csharp
// Envantere eşya ekle
inventoryService.Add("gem_t1", 5);  // 5 kömür ekle

// Eşya çıkar
inventoryService.Remove("gem_t1", 2);  // 2 kömür çıkar

// Sayım
int count = inventoryService.GetCount("gem_t1");

// Tümünü listele
var items = inventoryService.GetAll();  // IReadOnlyDictionary<string, int>
```

`MergeService.TryMerge()` çağrıldığında `InventoryService`'den 2 adet çıkarır ve 1 adet yeni tier ekler — bunu sen yapmazsın, otomatik.

---

## ADIM 6 — BOOTSTRAP KURULUMU

### 6.1 Bootstrap Script

`Assets/MergeIdle/Scripts/MergeIdleBootstrap.cs`:

```csharp
using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.Generator;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Upgrade;
using EndlessEngine.Merge;
using EndlessEngine.Inventory;

[DefaultExecutionOrder(-500)]
public class MergeIdleBootstrap : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private SaveService          _saveService;
    [SerializeField] private EconomyService       _economyService;
    [SerializeField] private UpgradeTreeService   _upgradeTreeService;
    [SerializeField] private GeneratorSystem      _generatorSystem;
    [SerializeField] private PassiveIncomeService _passiveIncomeService;

    [Header("Merge Sistemi")]
    [SerializeField] private InventoryService _inventoryService;
    [SerializeField] private MergeService     _mergeService;
    [SerializeField] private int              _inventorySlots = 30;  // Board boyutu

    [Header("Configs")]
    [SerializeField] private EconomyConfigSO    _economyConfig;
    [SerializeField] private GeneratorDatabaseSO _generatorDatabase;
    [SerializeField] private SchemaVersionSO    _schemaVersion;
    [SerializeField] private RealmIdentityConfigSO _realmConfig;

    [Header("Tüm Item Config'leri")]
    [SerializeField] private ItemConfigSO[] _allItemConfigs;  // 12 adet (6+6 tier)

    [Header("Tüm Merge Config'leri")]
    [SerializeField] private MergeConfigSO[] _mergeConfigs;  // 2 adet (gems + weapons)

    private IEnumerator Start()
    {
        // 1. Sayı motoru
        BigNumberFactory.Configure(_economyConfig.NumberBackend);

        // 2. Config registry
        ConfigRegistry.InjectForTesting(
            economy: _economyConfig,
            schema:  _schemaVersion,
            realm:   _realmConfig);

        // 3. Upgrade tree
        _upgradeTreeService?.HandleConfigsLoaded();

        // 4. Economy
        _economyService.Initialize(_upgradeTreeService, _saveService);

        // 5. Generators (pasif gelir)
        _generatorSystem.Initialize(
            _generatorDatabase.Generators,
            _economyService,
            _saveService);

        _passiveIncomeService.Initialize(
            _generatorSystem,
            _economyService,
            gameFlow: null);

        // 6. Inventory — kaç slot var, hangi item'lar var
        _inventoryService.Initialize(_allItemConfigs, _inventorySlots);

        // 7. MergeService — INVENTORY'den sonra başlatılmalı
        _mergeService.Initialize(
            configs:          _mergeConfigs,
            inventoryService: _inventoryService,
            economyService:   _economyService);

        // 8. Merge event'ini dinle — altın ekle ve UI güncelle
        MergeService.OnMergeCompleted += OnMergeCompleted;

        // 9. Kayıt sağlayıcıları
        _saveService.RegisterStateProvider(_economyService);
        _saveService.RegisterStateProvider(_upgradeTreeService);
        _saveService.RegisterStateProvider(_generatorSystem);
        _saveService.RegisterStateProvider(_inventoryService);  // ← Board state

        // 10. Kayıt yükle
        bool done = false;
        _ = _saveService.LoadAsync().ContinueWith(_ => done = true,
            System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        yield return new WaitUntil(() => done);

        // 11. Board'u kayıttan yükle ve göster
        RefreshBoard();

        Debug.Log("[MergeIdle] Hazır!");
    }

    private void OnDestroy()
    {
        MergeService.OnMergeCompleted -= OnMergeCompleted;
    }

    private void OnMergeCompleted(string itemId, int tier, MergeResult result)
    {
        // Gold bonus zaten MergeService tarafından EconomyService'e eklendi
        Debug.Log($"Merge tamamlandı: {itemId} T{tier} → {result.ResultItem?.ItemId}");
        RefreshBoard();
    }

    private void RefreshBoard()
    {
        // BoardView script'ine envanter bilgisini gönder
        FindFirstObjectByType<MergeBoardView>()?.Refresh(_inventoryService.GetAll());
    }
}
```

### 6.2 Sahne Hiyerarşisi

```
Bootstrap (GameObject)
    ├── MergeIdleBootstrap   (component)
    ├── SaveService          (component)
    ├── EconomyService       (component)
    ├── UpgradeTreeService   (component)
    ├── GeneratorSystem      (component)
    ├── PassiveIncomeService (component)
    ├── TickEngine           (component)
    ├── InventoryService     (component)
    └── MergeService         (component) — NOT: MonoBehaviour DEĞİL, ScriptableObject veya POCO

Canvas
    └── MergeBoard (MergeBoardView component)
        ├── Grid (GridLayoutGroup)
        │       └── Slot_0 ... Slot_29 (30 adet ItemSlot prefab)
        └── GoldLabel
```

**ÖNEMLİ:** `MergeService` bir MonoBehaviour değil — `new MergeService()` ile oluşturulup `Initialize()` çağrılır. Sahnede component olarak yok; Bootstrap'ta `[SerializeField]` yerine:

```csharp
private MergeService _mergeService = new MergeService();
```

Bunu Bootstrap'ın `private` alanına ekle, `[SerializeField]`'a gerek yok.

---

## ADIM 7 — MERGE BOARD UI

### 7.1 Grid Layout

Canvas içinde bir Grid oluştur:

```
MergeBoard (GameObject, 600×600 px)
    └── Grid (GridLayoutGroup)
            Cell Size: 80×80 px
            Spacing: 5×5 px
            Constraint: Fixed Column Count = 6
            → 6×5 = 30 slot (veya 5×6, 7×4 vb.)
```

### 7.2 Item Slot Prefab

```
ItemSlot (Prefab)
    ├── Background (Image — slot arka planı)
    ├── ItemIcon (Image — eşya ikonu)
    ├── TierLabel (TMP_Text — "T1" gibi)
    └── ItemSlot (component)
```

```csharp
public class ItemSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Image    _itemIcon;
    [SerializeField] private TMP_Text _tierLabel;

    public string  ItemId   { get; private set; }
    public int     Tier     { get; private set; }
    public bool    IsEmpty  => string.IsNullOrEmpty(ItemId);

    private static ItemSlot _draggingSlot;

    public void SetItem(ItemConfigSO config)
    {
        if (config == null) { Clear(); return; }
        ItemId         = config.ItemId;
        Tier           = config.MergeTier;
        _itemIcon.sprite = config.Icon;
        _tierLabel.text  = $"T{config.MergeTier}";
        _itemIcon.enabled   = true;
        _tierLabel.enabled  = true;
    }

    public void Clear()
    {
        ItemId = null;
        _itemIcon.enabled  = false;
        _tierLabel.enabled = false;
    }

    // Drag & Drop
    public void OnBeginDrag(PointerEventData e) => _draggingSlot = this;
    public void OnDrag(PointerEventData e)       { /* sürükleme görsel */ }

    public void OnEndDrag(PointerEventData e)
    {
        _draggingSlot = null;
    }

    public void OnDrop(PointerEventData e)
    {
        if (_draggingSlot == null || _draggingSlot == this) return;

        // Aynı tier mi?
        if (_draggingSlot.Tier == Tier && !IsEmpty && !_draggingSlot.IsEmpty)
        {
            // Merge dene
            MergeBoard.Instance?.TryMerge(_draggingSlot, this);
        }
        else if (IsEmpty)
        {
            // Boş slota taşı
            MergeBoard.Instance?.MoveItem(_draggingSlot, this);
        }
    }
}
```

### 7.3 Merge Board Controller

```csharp
public class MergeBoardView : MonoBehaviour
{
    public static MergeBoardView Instance { get; private set; }

    [SerializeField] private ItemSlot[]     _slots;    // 30 slot
    [SerializeField] private ItemConfigSO[] _allItems; // Tüm item config'leri

    private MergeService     _mergeService;
    private InventoryService _inventory;

    private void Awake() => Instance = this;

    public void Inject(MergeService merge, InventoryService inventory)
    {
        _mergeService = merge;
        _inventory    = inventory;
    }

    public void Refresh(IReadOnlyDictionary<string, int> inventoryState)
    {
        // Tüm slotları temizle
        foreach (var slot in _slots) slot.Clear();

        // Envanterdeki eşyaları slotlara yerleştir
        int slotIndex = 0;
        foreach (var (itemId, count) in inventoryState)
        {
            var config = GetConfig(itemId);
            for (int i = 0; i < count && slotIndex < _slots.Length; i++, slotIndex++)
                _slots[slotIndex].SetItem(config);
        }
    }

    public void TryMerge(ItemSlot source, ItemSlot target)
    {
        if (source.ItemId != target.ItemId) return;

        // MergeService.TryMerge item ile çağrılır
        var config = GetConfig(source.ItemId);
        var result = _mergeService.TryMerge(config);

        if (result.Success)
        {
            // Inventory güncellendi (MergeService halletti)
            // Sadece UI'ı yenile
            source.Clear();
            target.SetItem(result.ResultItem);

            // Altın bonusunu göster
            if (result.GoldBonus > 0)
                ShowFloatingGold(target.transform.position, result.GoldBonus);
        }
        else
        {
            Debug.LogWarning($"Merge başarısız: {result.FailReason}");
        }
    }

    public void MoveItem(ItemSlot source, ItemSlot target)
    {
        target.SetItem(GetConfig(source.ItemId));
        source.Clear();
    }

    private ItemConfigSO GetConfig(string itemId)
        => System.Array.Find(_allItems, c => c.ItemId == itemId);

    private void ShowFloatingGold(Vector3 pos, long amount)
    {
        // FloatingText prefab instance'ı → "+500 altın"
    }
}
```

---

## ADIM 8 — EŞYA SATIN ALMA (Mağaza)

Oyuncunun altınla T1 eşya satın alabilmesi için:

```csharp
public class MergeShop : MonoBehaviour
{
    [SerializeField] private Button      _buyGemButton;
    [SerializeField] private TMP_Text    _buyGemCostLabel;
    [SerializeField] private long        _gemT1Cost = 50;

    private EconomyService   _economy;
    private InventoryService _inventory;

    public void Inject(EconomyService economy, InventoryService inventory)
    {
        _economy   = economy;
        _inventory = inventory;

        EconomyService.OnResourcesChanged += OnGoldChanged;
        OnGoldChanged(_economy.CurrentResources, 0);
    }

    private void OnGoldChanged(double current, double delta)
    {
        _buyGemButton.interactable = current >= _gemT1Cost;
        _buyGemCostLabel.text = $"{_gemT1Cost} altın";
    }

    public void OnBuyGemClick()
    {
        if (_economy.CurrentResources < _gemT1Cost) return;

        _economy.DeductResources(_gemT1Cost);
        _inventory.Add("gem_t1", 1);

        FindFirstObjectByType<MergeBoardView>()?.Refresh(_inventory.GetAll());
    }
}
```

---

## ADIM 9 — GENERATOR: OTOMATIK EŞYA DÜŞÜRME

Generator'lar normal pasif gelir verir. Ama tematik olarak "jeneratörler board'a eşya düşürüyor" da yapabilirsin:

```csharp
public class ItemDropGenerator : MonoBehaviour
{
    [SerializeField] private float     _dropIntervalSeconds = 30f;
    [SerializeField] private string    _droppedItemId = "gem_t1";
    [SerializeField] private InventoryService _inventory;

    private float _timer;

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= _dropIntervalSeconds)
        {
            _timer = 0;
            _inventory.Add(_droppedItemId, 1);
            FindFirstObjectByType<MergeBoardView>()
                ?.Refresh(_inventory.GetAll());
            Debug.Log($"Otomatik eşya düştü: {_droppedItemId}");
        }
    }
}
```

Bu component'ı Bootstrap'a veya ayrı bir GameObject'e ekle.

---

## ADIM 10 — EŞYA SPRITE'LARI

Her ItemConfigSO'nun bir `Icon` (Sprite) alanı var. Sprite olmadan eşyalar görünmez olur.

1. Her tier için bir sprite hazırla (veya placeholder kullan)
2. `Assets/MergeIdle/Art/Gems/` klasörüne koy
3. Her ItemConfigSO'nun `Icon` alanına sürükle

**Hızlı yol — renkli placeholder:**
```
Sprite oluştur (Unity): Create → 2D → Sprite
Her tier için farklı renk ver (kömür: siyah, kristal: mavi, yakut: kırmızı, elmas: beyaz...)
```

---

## ADIM 11 — KAYIT SİSTEMİ

Merge board state'i `InventoryService` üzerinden kaydedilir. Bootstrap'ta:

```csharp
_saveService.RegisterStateProvider(_inventoryService);
```

Bu satır varsa tüm eşya sayımları otomatik kaydedilir.

**Test:**
1. T1 eşya satın al
2. 2 adet birleştir → T2 yap
3. Play durdur → tekrar aç
4. T2 eşyalar hâlâ orada mı?

---

## ADIM 12 — İÇERİK GENİŞLETME

### Yeni Merge Zinciri Ekleme

1. Yeni tema için 5-8 ItemConfigSO oluştur (MergeGroupId = "fruits" gibi)
2. Yeni MergeConfigSO oluştur
3. Bootstrap'taki `_mergeConfigs` array'ine ekle
4. Bootstrap'taki `_allItemConfigs` array'ine yeni item'ları ekle

### Özel Merge Kuralı (3'lü Merge)

Standart sistem 2'li merge yapar. 3'lü birleştirme için özel script:

```csharp
// Özel kural: 3 aynı T3 → T5 (tier atlama!)
// MergeRule: InputTier=3, ResultItem=GemT5, GoldBonus=500
// Ama bu sadece 2'li merge kural listesine eklenebilir
// 3'lü için özel mantık yazman gerekir
```

---

## ADIM 13 — EKONOMİ DENGESİ

Merge oyunlarında ekonomi dengesi:

| Metrik | Hedef |
|--------|-------|
| T1 satın alma maliyeti | Pasif gelirin 5-10 saniyesi |
| T5 oluşturma süresi | 15-30 dakika aktif oyun |
| T1 + T1 merge gold bonus | T1 maliyetinin %10'u |
| T5 + T5 merge gold bonus | T1 maliyetinin 50x'i |

### Dengeleme Örneği

- Pasif gelir = 100 altın/s
- T1 maliyeti = 500 altın (5 saniye)
- T1 altın bonusu = 0
- T2 altın bonusu = 5
- T3 altın bonusu = 20
- T4 altın bonusu = 100
- T5 altın bonusu = 500

---

## ADIM 14 — TEST VE YAYINA HAZIRLIK

### Test Listesi

- [ ] T1 satın alınıyor
- [ ] T1 + T1 sürükle → T2 oluşuyor
- [ ] Gold bonus ekleniyor
- [ ] Boş slota taşıma çalışıyor
- [ ] Play Stop Play → board state korunuyor
- [ ] 30 slot dolu → "Yer yok" mesajı
- [ ] Tüm tier zincirleri çalışıyor
- [ ] İki farklı grup birbirleriyle merge EDEMİYOR

---

*Bu rehber Endless Engine v1.3.4 için yazılmıştır.*
