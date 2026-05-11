# REHBER 03 — Click Loop / Aktif Clicker
## Clicker Heroes / Tap Titans Tarzı
### Endless Engine ile Sıfırdan Steam'e

> Bu rehberde HP'li hedeflere tıklayan, combo ve crit sistemi olan, auto-click upgrade'leriyle pasifleşen, arka planda generator'larla gelir sağlayan bir aktif clicker oyunu yapacaksın.

---

## BU OYUNDA NE VAR?

| Sistem | Ne Yapar |
|--------|----------|
| Aktif Tıklama | Hedeflere tıkla, hasar ver, altın kazan |
| HP'li Hedefler | Her hedef canı bitince yok olur, yeniden doğar |
| Combo | Hızlı tıklama combo çarpanını artırır |
| Crit | Şansa bağlı kritik vuruş, çok altın verir |
| Auto-click | Upgrade ile otomatik tıklama açılır |
| Generator'lar | Arka planda pasif gelir |
| Upgrade Tree | Tıklama gücü, crit, auto-click, pasif gelir |
| Çevrimdışı | Auto-click kapanıkken de çalışır |

---

## ADIM 1 — PROJEYİ HAZIRLA

### 1.1 Unity Projesi

1. Unity Hub → **New Project** → **2D (URP)**
2. Proje adı: `ClickIdle`
3. `Window → Package Manager → + → Add package from disk` → `package.json`

### 1.2 Wizard

`Tools → Endless Engine → New Game Wizard`
1. **Click Loop** seç
2. Game Name = `ClickIdle`
3. **Generate**

Oluşturulan dosyalar:
```
Assets/ClickIdle/
    Configs/
        EconomyConfig.asset
        ClickLoopConfig.asset
        GeneratorDatabase.asset
        UpgradeTreeConfig.asset
        SchemaVersion.asset
        PrestigeConfig.asset
        RealmIdentityConfig.asset
    Scenes/
        ClickIdle.unity
    Scripts/
        ClickIdleBootstrap.cs
        ClickIdleUI.cs
```

---

## ADIM 2 — SİSTEMLER VE PARA AKIŞI

Click Loop oyunlarında para iki kanaldan gelir:

```
KAYNAK 1 — Tıklama Geliri:
Oyuncu tıklar →
    ClickLoopService, ClickTarget'a hasar verir →
        Hasar = DamagePerClick × (ClickDamage stat çarpanı) →
        HP ≤ 0 olunca: BaseYield × (ClickYieldMultiplier stat) altın →
            EconomyService.AddResources()

KAYNAK 2 — Pasif Gelir (Generator):
TickEngine →
    PassiveIncomeService →
        EconomyService.AddResources()
```

### Combo Sistemi Nasıl Çalışır?

```
Tıklama yapıldı →
    ClickComboTracker: ComboPoints += ComboContribution (target'dan okunur) →
        ComboPoints / ComboPointsPerStep = mevcut çarpan seviyesi →
            ComboMultiplier = min(Seviye + 1, MaxComboMultiplier) →
                Tıklama geliri × ComboMultiplier uygulanır

Tıklama durdu →
    ComboDecayDelay saniye sonra →
        ComboDecayRate puan/saniye azalır →
            Sıfıra indiğinde Combo = 1x
```

### Crit Nasıl Çalışır?

```
Her tıklamada:
    Random.value < BaseCritChance + (ClickCritChance stat bonusu) →
        Evet → Gelir × (BaseCritMultiplier + ClickCritMultiplier stat bonusu)
        Hayır → Normal gelir
```

---

## ADIM 3 — CLICK LOOP CONFIG AYARLA

`Assets/ClickIdle/Configs/ClickLoopConfig.asset`:

| Field | Değer | Açıklama |
|-------|-------|----------|
| `ComboDecayDelay` | 1.5 | 1.5 saniye tıklamasan combo azalmaya başlar |
| `ComboDecayRate` | 8.0 | Saniyede 8 combo puanı azalır |
| `ComboPointsPerStep` | 5.0 | Her 5 puan = 1 çarpan seviyesi |
| `MaxComboMultiplier` | 8.0 | Max 8x çarpan |
| `BaseCritChance` | 0.05 | %5 crit şansı |
| `BaseCritMultiplier` | 3.0 | Crit = 3x altın |
| `BaseAutoClickRate` | 0.0 | Başta auto-click yok, upgrade ile açılır |
| `OfflineCapHours` | 8.0 | 8 saate kadar çevrimdışı auto-click |
| `OfflineEfficiency` | 0.25 | Çevrimdışı aktif hızın %25'i |

### Combo Hissi Ayarlaması

| İstenen His | Değişiklik |
|-------------|-----------|
| Combo çok çabuk bitiyor | `ComboDecayDelay` artır (2.0→3.0) |
| Combo çok kolay max oluyor | `ComboPointsPerStep` artır (5→10) |
| Max combo çok güçlü | `MaxComboMultiplier` düşür (8→5) |
| Crit çok sık geliyor | `BaseCritChance` düşür |
| Crit çok az geliyor | `BaseCritChance` artır |

---

## ADIM 4 — CLICK TARGET CONFIG OLUŞTUR

Her hedef tipi için ayrı bir `ClickTargetConfigSO` asset oluştur.

`Assets → Create → Endless Engine → Click Loop → Target Config`

**3 farklı hedef öner — oyunun erken, orta, geç oyununu temsil eder:**

### Hedef 1: Basit Taş (Erken Oyun)

`Assets/ClickIdle/Configs/Targets/Target_Rock.asset`:

| Field | Değer |
|-------|-------|
| `TargetId` | `"rock"` |
| `DisplayName` | `"Kaya"` |
| `MaxHP` | 5 |
| `DamagePerClick` | 1.0 |
| `BaseYield` | 2 |
| `AwardYieldPerClick` | false ← Yok edilince altın ver |
| `RespawnSeconds` | 2.0 |
| `ComboContribution` | 1.0 |
| `YieldCurrencyId` | `""` ← Boş = altın |

### Hedef 2: Kristal (Orta Oyun)

`Assets/ClickIdle/Configs/Targets/Target_Crystal.asset`:

| Field | Değer |
|-------|-------|
| `TargetId` | `"crystal"` |
| `DisplayName` | `"Kristal"` |
| `MaxHP` | 20 |
| `DamagePerClick` | 1.0 |
| `BaseYield` | 15 |
| `AwardYieldPerClick` | false |
| `RespawnSeconds` | 4.0 |
| `ComboContribution` | 2.0 ← Combo'ya daha fazla katkı |

### Hedef 3: Ejderha Yumurtası (Geç Oyun)

`Assets/ClickIdle/Configs/Targets/Target_DragonEgg.asset`:

| Field | Değer |
|-------|-------|
| `TargetId` | `"dragon_egg"` |
| `DisplayName` | `"Ejderha Yumurtası"` |
| `MaxHP` | 100 |
| `DamagePerClick` | 1.0 |
| `BaseYield` | 150 |
| `AwardYieldPerClick` | true ← Her tıkta kısmi altın ver |
| `RespawnSeconds` | 10.0 |
| `ComboContribution` | 5.0 |

### AwardYieldPerClick Farkı

- `false` (Taş, Kristal): Hedef tamamen yok edilince tek seferde `BaseYield` altın
- `true` (Yumurta): Her tıkta `BaseYield / MaxHP × DamagePerClick` altın → sürekli küçük miktarlar

---

## ADIM 5 — SAHNEDEKİ CLICK TARGET KURULUMU

Bu adım Wave oyunlarından farklı: her hedef için sahneye özel bir GameObject eklemen gerekiyor.

### 5.1 Her Hedef İçin Prefab Oluştur

`Assets/ClickIdle/Prefabs/` klasöründe:

**ClickTarget_Rock.prefab:**
```
ClickTarget_Rock (GameObject)
    ├── SpriteRenderer (kaya sprite'ı)
    ├── BoxCollider2D (Is Trigger = true) ← Tıklama algılama
    ├── ClickTarget (component)          ← Bu şart
    └── HPBar (GameObject, isteğe bağlı)
        └── Slider (HP bar)
```

`ClickTarget` component Inspector'ında:
- `_config` → `Target_Rock.asset` sürükle

**ÖNEMLİ:** `ClickTarget` component, `ClickLoopService`'i sahneyi taradığında otomatik bulur. Bootstrap'a ayrıca referans vermeye gerek yok.

### 5.2 Hedefleri Sahnede Yerleştir

1. Prefab'ı sahnede istediğin konuma sürükle
2. Her prefab için `_config` alanına doğru ClickTargetConfigSO'yu ata
3. Layer = `ClickableTargets` yap (Bootstrap'ta bu layer mask kullanılıyor)

**Sahne düzeni örneği:**
```
Oyun alanı ortası: Target_Rock (x3)
Sağ taraf: Target_Crystal (x2)
Özel alan: Target_DragonEgg (x1)
```

### 5.3 Layer Ayarı

Unity'de:
1. `Edit → Project Settings → Tags and Layers`
2. User Layer 8'e `ClickableTargets` ekle
3. Her ClickTarget GameObject'ini bu layer'a ata

---

## ADIM 6 — GENERATOR'LAR (Pasif Gelir)

Click idle'da generator'lar arka plan geliri sağlar. Tıklamak yerine pasif beklemek isteyen oyuncu için önemli.

`Tools → Endless Engine → Generator Editor`

5 generator öner — tıklama temasıyla uyumlu:

| # | GeneratorId | DisplayName | Yield/s | BaseCost | CostScale |
|---|------------|-------------|---------|---------|-----------|
| 1 | `miner` | Madenci | 0.2 | 30 | 1.12 |
| 2 | `digger` | Kazıcı | 1.5 | 250 | 1.12 |
| 3 | `drill` | Matkap | 10 | 2,500 | 1.12 |
| 4 | `excavator` | Ekskavatör | 80 | 25,000 | 1.12 |
| 5 | `auto_clicker` | Otomatik Tıklayıcı | 600 | 250,000 | 1.12 |

Son generator `auto_clicker` tematik: pasif gelir sağlayan bir "makineli tıklayıcı". Oyuncunun auto-click upgrade aldığında bunu sağlayan ClickLoopService'den farklı ama tematik olarak uyumlu.

---

## ADIM 7 — UPGRADE TREE (Click Odaklı)

`Tools → Endless Engine → Upgrade Tree Editor`

Click idle upgrade'leri 4 gruba ayrılır:

### Grup A — Tıklama Gücü

```
NodeId: click_dmg_01
DisplayName: "Güçlü Darbeler"
AffectedStat: ClickDamage
EffectType: PercentBonus
EffectPerRank: 0.15    ← Her rankta %15 hasar
MaxRank: 10
BaseCost: 100
CostScalingFactor: 1.6
SelectionWeight: 70
```

```
NodeId: click_yield_01
DisplayName: "Altın Dokunuş"
AffectedStat: ClickYieldMultiplier
EffectType: PercentBonus
EffectPerRank: 0.20
MaxRank: 8
BaseCost: 300
CostScalingFactor: 1.8
SelectionWeight: 60
```

### Grup B — Crit Sistemi

```
NodeId: crit_chance_01
DisplayName: "Şanslı El"
AffectedStat: ClickCritChance
EffectType: FlatBonus
EffectPerRank: 0.03    ← +%3 crit şansı / rank
MaxRank: 10
BaseCost: 500
CostScalingFactor: 2.0
SelectionWeight: 40
```

```
NodeId: crit_mult_01
DisplayName: "Ölümcül Darbe"
AffectedStat: ClickCritMultiplier
EffectType: FlatBonus
EffectPerRank: 0.50    ← +0.5x crit çarpanı / rank
MaxRank: 10
BaseCost: 1,000
CostScalingFactor: 2.2
SelectionWeight: 30
PrerequisiteNodeIDs: ["crit_chance_01"]
```

### Grup C — Auto-click

```
NodeId: auto_click_01
DisplayName: "Sihirli Parmak"
AffectedStat: ClickAutoRate
EffectType: FlatBonus
EffectPerRank: 0.5     ← +0.5 click/saniye / rank
MaxRank: 10
BaseCost: 2,000
CostScalingFactor: 2.5
SelectionWeight: 35
```

> **Auto-click nasıl çalışır?** `ClickLoopService`, `ClickAutoRate` stat değeri > 0 olduğunda her saniye o kadar otomatik tıklama üretir. Gerçek tıklama gibi işlenir — combo ve crit uygular. Çevrimdışıyken `OfflineEfficiency` oranında çalışır.

### Grup D — Pasif Gelir (Generator)

```
NodeId: gen_boost_01
DisplayName: "Endüstri Yönetimi"
AffectedStat: GeneratorYield
EffectType: PercentBonus
EffectPerRank: 0.15
MaxRank: 5
BaseCost: 3,000
CostScalingFactor: 2.0
SelectionWeight: 20
```

---

## ADIM 8 — BOOTSTRAP KURULUMU

### 8.1 Bootstrap Script

`Assets/ClickIdle/Scripts/ClickIdleBootstrap.cs`:

```csharp
using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Economy;
using EndlessEngine.Economy.Math;
using EndlessEngine.Generator;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Upgrade;
using EndlessEngine.ClickLoop;

[DefaultExecutionOrder(-500)]
public class ClickIdleBootstrap : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private SaveService          _saveService;
    [SerializeField] private EconomyService       _economyService;
    [SerializeField] private UpgradeTreeService   _upgradeTreeService;
    [SerializeField] private GeneratorSystem      _generatorSystem;
    [SerializeField] private PassiveIncomeService _passiveIncomeService;

    [Header("Click Loop")]
    [SerializeField] private ClickLoopService          _clickLoopService;
    [SerializeField] private ClickLoopOfflineCalculator _offlineCalc;

    [Header("Input")]
    [SerializeField] private InputProviderUnity _inputProvider;  // Scene'deki Player GO'da
    [SerializeField] private LayerMask          _clickableLayer; // "ClickableTargets" layer

    [Header("Configs")]
    [SerializeField] private EconomyConfigSO       _economyConfig;
    [SerializeField] private ClickLoopConfigSO     _clickLoopConfig;
    [SerializeField] private ClickTargetConfigSO[] _targetConfigs;   // 3 hedef config
    [SerializeField] private GeneratorDatabaseSO   _generatorDatabase;
    [SerializeField] private SchemaVersionSO       _schemaVersion;
    [SerializeField] private PrestigeConfigSO      _prestigeConfig;
    [SerializeField] private RealmIdentityConfigSO _realmConfig;

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

        // 6. Passive income
        _passiveIncomeService.Initialize(
            _generatorSystem,
            _economyService,
            gameFlow: null);

        // 7. Click Loop Service
        // inputProvider: mouse/touch girdisi
        // clickableLayer: hangi layer'daki collider'lara tıklanır
        _clickLoopService.Initialize(
            config:      _clickLoopConfig,
            economy:     _economyService,
            input:       _inputProvider,
            targetLayer: _clickableLayer,
            statistics:  null,  // İsteğe bağlı
            vfx:         null); // İsteğe bağlı

        // 8. Offline calculator — çevrimdışı auto-click geliri
        _offlineCalc?.Initialize(
            _clickLoopConfig,
            _economyService,
            _targetConfigs);

        // 9. Kayıt sağlayıcıları
        _saveService.RegisterStateProvider(_economyService);
        _saveService.RegisterStateProvider(_upgradeTreeService);
        _saveService.RegisterStateProvider(_generatorSystem);
        _saveService.RegisterStateProvider(_clickLoopService);  // ← Respawn timer'ları kaydeder

        // Offline gelir: kayıt yüklenince hesapla
        _saveService.OnSaveLoaded += (data, isNew) =>
        {
            if (!isNew)
                _offlineCalc?.HandleSaveLoaded(data);
        };

        // 10. Kayıt yükle
        bool done = false;
        _ = _saveService.LoadAsync().ContinueWith(_ => done = true,
            System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        yield return new WaitUntil(() => done);

        Debug.Log("[ClickIdle] Hazır! Tıklamaya başla.");
    }
}
```

### 8.2 Sahne Hiyerarşisi

```
Bootstrap (GameObject)
    ├── ClickIdleBootstrap    (component)
    ├── SaveService           (component)
    ├── EconomyService        (component)
    ├── UpgradeTreeService    (component)
    ├── GeneratorSystem       (component)
    ├── PassiveIncomeService  (component)
    ├── TickEngine            (component)
    ├── ClickLoopService      (component)
    └── ClickLoopOfflineCalculator (component)

Player (GameObject)
    └── InputProviderUnity    (component) ← Mouse/touch girdi

ClickTargets (GameObject) ← Boş parent
    ├── ClickTarget_Rock_1   (ClickTarget component, config=Target_Rock)
    ├── ClickTarget_Rock_2   (ClickTarget component, config=Target_Rock)
    ├── ClickTarget_Crystal_1 (ClickTarget component, config=Target_Crystal)
    └── ClickTarget_DragonEgg (ClickTarget component, config=Target_DragonEgg)

Canvas
    └── ClickIdleUI (component)
```

### 8.3 Inspector Doldur

Bootstrap seç → Inspector:

- `_inputProvider` → Player GameObject'indeki `InputProviderUnity` sürükle
- `_clickableLayer` → "ClickableTargets" layer seç (dropdown'da görünür)
- `_targetConfigs` → Array size=3, her slota Target_Rock, Target_Crystal, Target_DragonEgg sürükle
- Diğerleri: sahne component'larını ve config asset'lerini sürükle

---

## ADIM 9 — UI YAPISI

### 9.1 Ana HUD

```
Canvas
    ├── TopBar
    │       ├── GoldLabel    (TMP_Text: "Altın: 0")
    │       └── IncomeLabel  (TMP_Text: "Toplam: 0/s")
    ├── ComboBar
    │       ├── ComboLabel   (TMP_Text: "Combo: ×1.0")
    │       └── ComboSlider  (Slider: dolu=max combo)
    ├── ClickFeedback
    │       └── CritLabel    (TMP_Text: "CRIT! ×3", geçici)
    ├── GeneratorPanel
    │       └── (Scroll View)
    └── UpgradePanel
            └── (Scroll View)
```

### 9.2 Click UI Script

```csharp
public class ClickIdleUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _goldLabel;
    [SerializeField] private TMP_Text _incomeLabel;
    [SerializeField] private TMP_Text _comboLabel;
    [SerializeField] private Slider   _comboSlider;
    [SerializeField] private TMP_Text _critLabel;
    [SerializeField] private float    _critLabelDuration = 0.8f;

    private ClickLoopService _clickService;

    public void Inject(ClickLoopService clickService)
    {
        _clickService = clickService;

        EconomyService.OnResourcesChanged += OnGoldChanged;
        _clickService.OnComboChanged      += OnComboChanged;
        _clickService.OnCrit              += OnCrit;
    }

    private void OnDestroy()
    {
        EconomyService.OnResourcesChanged -= OnGoldChanged;
        if (_clickService != null)
        {
            _clickService.OnComboChanged -= OnComboChanged;
            _clickService.OnCrit         -= OnCrit;
        }
    }

    private void OnGoldChanged(double current, double delta)
    {
        _goldLabel.text   = $"Altın: {FormatGold(current)}";
        _incomeLabel.text = delta > 0 ? $"+{FormatGold(delta)}" : "";
    }

    private void OnComboChanged(float combo)
    {
        _comboLabel.text      = $"Combo: ×{combo:F1}";
        _comboSlider.value    = combo / 8f;  // MaxComboMultiplier = 8
    }

    private void OnCrit(float critMultiplier)
    {
        StopAllCoroutines();
        StartCoroutine(ShowCritLabel(critMultiplier));
    }

    private IEnumerator ShowCritLabel(float mult)
    {
        _critLabel.text    = $"KRİTİK! ×{mult:F1}";
        _critLabel.enabled = true;
        yield return new WaitForSeconds(_critLabelDuration);
        _critLabel.enabled = false;
    }

    // Tıklama geri bildirimi — hedef tıklandığında floating text
    public void SpawnFloatingText(Vector2 worldPos, double amount, bool isCrit)
    {
        // FloatingTextPrefab'ı instantiate et, "+X" yaz, yukarı uç
        // (FloatingText.cs prefab'ı kendini yok eder)
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

### 9.3 ClickTarget HP Bar

Her `ClickTarget` GameObject'e HP bar ekle:

```csharp
public class ClickTargetHPBar : MonoBehaviour
{
    [SerializeField] private Slider _slider;
    [SerializeField] private ClickTarget _target;

    private void Start()
    {
        _target.OnDamaged  += UpdateBar;
        _target.OnRespawned += () => _slider.value = 1f;
    }

    private void UpdateBar(float currentHP, float maxHP)
    {
        _slider.value = currentHP / maxHP;
    }
}
```

---

## ADIM 10 — FLOATING TEXT VE VFX

### 10.1 Floating Text Prefab

Tıklama geri bildirimi için:

```
FloatingText (Prefab)
    ├── TMP_Text (değer: "+15")
    └── FloatingTextAnim (script)
```

```csharp
public class FloatingTextAnim : MonoBehaviour
{
    [SerializeField] private TMP_Text _label;
    [SerializeField] private float    _duration = 0.8f;
    [SerializeField] private float    _riseSpeed = 2f;

    public void Setup(string text, Color color)
    {
        _label.text  = text;
        _label.color = color;
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float t = 0;
        var startPos = transform.position;
        while (t < _duration)
        {
            t += Time.deltaTime;
            transform.position = startPos + Vector3.up * (_riseSpeed * t);
            float alpha = Mathf.Lerp(1f, 0f, t / _duration);
            _label.alpha = alpha;
            yield return null;
        }
        Destroy(gameObject);
    }
}
```

Bootstrap'ta veya UI script'te:
```csharp
_clickService.OnTargetClicked += (target, damage, yield, isCrit) =>
{
    var txt = Instantiate(floatingTextPrefab, target.WorldPosition, Quaternion.identity);
    txt.Setup(
        $"+{FormatGold(yield)}{(isCrit ? " KRİTİK!" : "")}",
        isCrit ? Color.yellow : Color.white);
};
```

---

## ADIM 11 — ÇEVRIMDIŞI KAZANÇ

`ClickLoopOfflineCalculator`, oyun kapalıyken auto-click gelirini hesaplar.

`SaveService.OnSaveLoaded` event'inde çağrılır (Bootstrap'ta zaten ayarladın).

Kullanıcıya popup göster:
```csharp
_saveService.OnSaveLoaded += (data, isNew) =>
{
    if (!isNew && data.ClickLoopState?.OfflineGoldEarned > 0)
    {
        ShowOfflinePopup(
            data.ClickLoopState.OfflineGoldEarned,
            data.ClickLoopState.OfflineTimeSeconds);
    }
};
```

Popup metni: `"4 saatte otomatik tıklayıcılar 123.456 altın kazandı!"`

---

## ADIM 12 — PRESTİGE (İsteğe Bağlı)

Click oyunlarında prestige: tüm tıklama upgrade'lerini sıfırla, kalıcı tıklama çarpanı kazan.

`PrestigeConfig.asset`:
- `MinWaveForPrestige` = 0 (wave yok)
- `MinGoldToPrestige` = 1,000,000 (1M altın eşiği)
- `BaseMultiplierPerPrestige` = 1.5
- `StatsAmplifiedByPrestige` = [ClickDamage, ClickYieldMultiplier]

Bootstrap'a prestige manager ekle:
```csharp
_saveService.RegisterStateProvider(_prestigeManager);
```

---

## ADIM 13 — EKONOMİ DENGESİ

`Tools → Endless Engine → Economy Simulator`

Click oyunlarında denge çok kritik — tıklama ve pasif gelirin dengesi:

| Metrik | Hedef |
|--------|-------|
| Tıklama geliri / saniye (max combo) | Pasif gelirin 3-5x'i |
| İlk hedef yok etme süresi | 5-10 tıklama |
| Auto-click açıldığında | 2x hız artışı hissettirmeli |

### Tıklama vs Pasif Denge Örneği

Oyun saati 10 dakika:
- Pasif gelir: 1,000 altın/s
- Aktif tıklama (combo max): 3,000-5,000 altın/tıklama
- Auto-click (10 rank): +5 click/s = 15,000-25,000 altın/s

Bu dengede aktif oyun pasifin 15-25x'i → aktif oynamaya değer.

---

## ADIM 14 — TEST VE YAYINA HAZIRLIK

### Test Listesi

- [ ] Tıklama HP düşürüyor
- [ ] HP 0'a gelince hedef yok oluyor ve para geliyor
- [ ] Respawn çalışıyor (ayarlanan sürede yeniden doğuyor)
- [ ] Combo artıyor ve zamanla azalıyor
- [ ] Crit tetikleniyor (konsol log veya UI ile doğrula)
- [ ] Auto-click upgrade alınca oto tıklama başlıyor
- [ ] Generator satın alınca pasif gelir artıyor
- [ ] Play Stop Play → combo sıfırlandı, altın ve upgrade kaydedildi
- [ ] Çevrimdışı gelir geliyor (saati ilerlet, test et)
- [ ] `Config Validator` → sıfır hata

### Yaygın Sorunlar

| Sorun | Neden | Çözüm |
|-------|-------|-------|
| Tıklama çalışmıyor | `InputProviderUnity` atanmadı | Inspector'a ata |
| Hedefler tıklanmıyor | Layer yanlış | `ClickableTargets` layer ata |
| Combo çalışmıyor | `_clickLoopConfig` atanmadı | Inspector'a ata |
| Çevrimdışı gelir yok | `_offlineCalc.HandleSaveLoaded` çağrılmadı | Bootstrap'a ekle |
| Auto-click çalışmıyor | `ClickAutoRate` stat 0 | Upgrade satın al veya BaseAutoClickRate artır |

---

*Bu rehber Endless Engine v1.3.4 için yazılmıştır.*
