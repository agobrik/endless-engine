# Endless Engine — Oyun Üretim Rehberi
## "Sıfırdan Steam Oyununa" Adım Adım Kılavuz

> Bu doküman, Endless Engine toolset'i kullanarak gerçek bir idle/incremental oyun üretmek isteyen herkes için yazılmıştır.

---

## İÇİNDEKİLER

1. [Başlamadan Önce](#1-başlamadan-önce)
2. [Adım 1 — Oyun Tipini Seç ve Prototype Üret](#2-adım-1--oyun-tipini-seç-ve-prototype-üret)
3. [Adım 2 — Generator'ları Tasarla](#3-adım-2--generatorları-tasarla)
4. [Adım 3 — Upgrade Tree Oluştur](#4-adım-3--upgrade-tree-oluştur)
5. [Adım 4 — Ekonomiyi Simüle Et ve Dengele](#5-adım-4--ekonomiyi-simüle-et-ve-dengele)
6. [Adım 5 — Prestige Sistemi Kur](#6-adım-5--prestige-sistemi-kur)
7. [Adım 6 — Oyun Tipine Özgü Sistemler](#7-adım-6--oyun-tipine-özgü-sistemler)
8. [Adım 7 — İçerik Genişletme](#8-adım-7--içerik-genişletme)
9. [Adım 8 — Kayıt Sistemi ve Veri Güvenliği](#9-adım-8--kayıt-sistemi-ve-veri-güvenliği)
10. [Adım 9 — HUD ve UI Bağlama](#10-adım-9--hud-ve-ui-bağlama)
11. [Adım 10 — Steam Entegrasyonu](#11-adım-10--steam-entegrasyonu)
12. [Senaryolar: Her Oyun Tipi İçin Tam Yol Haritası](#12-senaryolar-her-oyun-tipi-için-tam-yol-haritası)

---

## 1. BAŞLAMADAN ÖNCE

### Kurulum

1. Unity 6.3 LTS'i aç
2. `Window → Package Manager → Add package from disk`
3. `Packages/com.endlessengine.idle/package.json` dosyasını seç
4. Paketin yüklendiğini menüde `Tools → Endless Engine` görerek doğrula

### Ne Zaman Hangi Araç

| Ne yapmak istiyorsun | Hangi araç |
|---------------------|-----------|
| Yeni oyun oluştur | New Game Wizard |
| Generator ekle/düzenle | Generator Editor |
| Upgrade node'ları tasarla | Upgrade Tree Editor |
| Ekonomi dengesini test et | Economy Simulator |
| Config'lerde hata var mı bak | Config Validator |
| Duplicate ID var mı kontrol et | ID Registry |
| Save şemasını güncelle | Schema Bump Utility |

---

## 2. ADIM 1 — OYUN TİPİNİ SEÇ VE PROTOTYPE ÜRET

### New Game Wizard'ı Aç

`Tools → Endless Engine → New Game Wizard`

### Hangi Tipi Seçmeli?

**Pasif gelir odaklı, savaş yok:**
→ **Pure Idle** (en basit, ilk oyun için ideal)

**Aktif tıklama mekanikleri istiyorum:**
→ **Clicker Idle** (basit) veya **Click Loop** (HP'li hedefler, combo, crit)

**Düşman ve dalga sistemi istiyorum:**
→ **Idle-vs / RPG** (otomatik savaş) veya **Tower Defense**

**Eşya birleştirme istiyorum (Merge Dragon tarzı):**
→ **Merge Idle**

**Tarım/çiftçilik istiyorum:**
→ **Farm Idle**

**Uzun araştırma kuyrukları istiyorum:**
→ **Research Idle**

**Şehir inşaa sistemi istiyorum:**
→ **Building Idle**

**Çok güçlü prestige döngüsü istiyorum:**
→ **Prestige-Heavy**

**Cursor ile kaynak toplama istiyorum:**
→ **Harvest Idle**

**Her şeyi kendin ayarla:**
→ **Custom**

### Generate'e Bastıktan Sonra

Wizard otomatik olarak şunu yapar:
1. `Assets/[OyunAdın]/Configs/` — tüm config asset'leri
2. `Assets/[OyunAdın]/Scenes/[OyunAdın].unity` — hazır sahne
3. Sahneyi Build Settings'e ekler

**Şimdi şunu yap:**
- `Assets/[OyunAdın]/Scenes/[OyunAdın].unity` dosyasını aç
- Play'e bas
- Oyunun çalışıyor olması lazım

> Bu prototipte her şey varsayılan değerlerle gelir. Sonraki adımlar bunları oyununa göre özelleştirmek içindir.

---

## 3. ADIM 2 — GENERATOR'LARI TASARLA

### Generator Editor'ü Aç

`Tools → Endless Engine → Generator Editor`

Editor otomatik olarak `Assets/[OyunAdın]/Configs/GeneratorDatabase.asset` dosyasını yükler.

### Wizard'ın Oluşturduğu Varsayılan

Wizard "GoldMine" adında 1 generator oluşturur:
- Yield: 1/s
- Base Cost: 50
- Cost Scale: 1.15

Bu bir başlangıç noktası. Gerçek bir idle oyununda 8-15 generator olur.

### Generator Hiyerarşisi Nasıl Kurulur

**Klasik idle formülü:** Her generator bir öncekinden ~10x daha pahalı, ~5-8x daha güçlü olmalı.

**Örnek hiyerarşi (Pure Idle için):**

| # | İsim | Yield/s | Başlangıç Maliyet | Scale |
|---|------|---------|------------------|-------|
| 1 | Maden | 0.1 | 10 | 1.15 |
| 2 | Çiftlik | 0.5 | 100 | 1.15 |
| 3 | Fabrika | 3.0 | 1.100 | 1.15 |
| 4 | Güç Santrali | 20 | 12.000 | 1.15 |
| 5 | Araştırma Lab | 150 | 130.000 | 1.15 |
| 6 | Uzay İstasyonu | 1.200 | 1.400.000 | 1.15 |

**Generator Editörde:**
1. `+ Add` butonuna bas → yeni generator asset oluşur
2. Sol panelden seç → sağda Inspector açılır
3. Değerleri gir
4. Maliyet eğrisi grafiği anlık güncellenir — ilk 10 kopya için gösterir
5. `Save` bas

### Unlock Sistemi

Bir generator başka bir generator'a sahip olmadan kilitli kalabilir:
```
UnlockPrerequisite = GoldMine  (GoldMine'dan en az 1 tane alınmadan görünmez)
UnlockRequirement = 10         (10 tane alınmadan bir sonrakini gösterme)
```

### Cost Scale Seçimi

| Scale | Etki |
|-------|------|
| 1.07 | Çok uygun — casual oyuncular için |
| 1.15 | Klasik idle dengesi |
| 1.20 | Zorlu — her kopya belirgin şekilde daha pahalı |
| 1.30 | Sert — geç oyun için kasıtlı yavaşlatma |

---

## 4. ADIM 3 — UPGRADE TREE OLUŞTUR

### Upgrade Tree Editor'ü Aç

`Tools → Endless Engine → Upgrade Tree Editor`

İlk kez açtığında boş gelir. `Load Asset` ile wizard'ın oluşturduğu tree'yi veya `New Tree` ile yenisini yükle.

### Node Ekleme

1. `+ Add Node` butonuna bas veya graph'ta sağ tıkla → "Add Node"
2. Sağdaki Inspector'da dolduracakların:

**Identity:**
- **Node ID:** benzersiz, küçük harf, alt çizgi (örn. `dmg_01`) — ID Registry'de çakışma kontrol et
- **Display Name:** oyuncunun gördüğü isim ("Güçlü Darbeler")
- **Description:** kısa açıklama

**Stats:**
- **Category:** Production (sarı), Combat (kırmızı), Survival (yeşil), Economy (mavi), Prestige (mor)
- **Stat:** hangi istatistiği etkiliyor (StatType enum'dan)
- **Effect Type:** PercentBonus (%X ekler) veya FlatBonus (X ekler)
- **Per Rank:** her rankta etki değeri (0.10 = %10)
- **Max Rank:** kaç kez alınabilir

**Economy:**
- **Base Cost:** ilk rank maliyeti
- **Cost Scale:** her rankta çarpan (1.5 = ikinci rank 1.5x daha pahalı)
- **Card Weight:** upgrade kartı olarak seçilme şansı (daha yüksek = daha sık çıkar)
- **Prestige Gate:** kaçıncı prestige'den sonra görünür (0 = hep görünür)

**Tree Behaviour:**
- **Max Out Edges:** bu node'dan kaç bağlantı çıkabilir (0 = sınırsız)

### Node'ları Bağlama

Bir node'un çıkış portunu (sağ nokta) başka bir node'un giriş portuna (sol nokta) sürükle. Bu, hedef node için prerequisite oluşturur.

### Örnek Tree Yapıları

**Basit doğrusal ağaç:**
```
[Üretim Artışı 1] → [Üretim Artışı 2] → [Üretim Artışı 3]
```

**Paralel ağaç:**
```
[Temel] → [Saldırı Dalı]
         → [Savunma Dalı]
         → [Ekonomi Dalı]
```

**Prestige-gated ağaç:**
```
Prestige 0: Temel node'lar
Prestige 1: Güçlendirilmiş node'lar (PrestigeGate=1)
Prestige 3: Efsanevi node'lar (PrestigeGate=3)
```

### Kaydetme

`Save` butonuna bas. Kaydedilmeden pencereyi kapatmak istersen "Kaydet mi?" diye sorar.

---

## 5. ADIM 4 — EKONOMİYİ SİMÜLE ET VE DENGELE

### Economy Simulator'ü Aç

`Tools → Endless Engine → Economy Simulator`

Bu araç, herhangi bir sahne açmadan oyununun 30 oturumluk seyrini simüle eder.

### Config'leri Bağla

Sol panelde:
- Economy Config → `Assets/[OyunAdın]/Configs/EconomyConfig.asset`
- Prestige Config → `Assets/[OyunAdın]/Configs/PrestigeConfig.asset`
- Generator DB → `Assets/[OyunAdın]/Configs/GeneratorDatabase.asset`
- Wave Config → (wave oyunları için)
- Run Config → (wave oyunları için)

### Parametreleri Ayarla

- **Sessions:** 30 oturum simüle et
- **Session length:** kaç dakikalık oturum (ortalama 20-30 dk)
- **Offline hours:** oturumlar arası kaç saat çevrimdışı (4-8 saat)
- **Gen copies/session:** oyuncunun oturum başına kaç generator aldığını tahmin et
- **Auto-prestige:** prestige uygun olunca otomatik prestige et

### Sonuçları Oku

Her satır bir oturum:
- **Gold Earned:** o oturumda kazanılan altın — oturum oturum büyümeli
- **Perm Mult:** kalıcı çarpan — her prestige'den sonra sıçramalı
- **Prestiged?** — prestige olan oturumlarda ✓

### Hedef Dengeler

| Metrik | İyi Değer |
|--------|-----------|
| İlk prestige | 5-10. oturumda |
| Her prestige'de Perm Mult artışı | ×1.5 - ×3.0 |
| 30. oturumda toplam Perm Mult | ×10 - ×100 |
| Oturum başına altın büyümesi | önceki oturumun 2-5 katı |

### Yaygın Sorunlar ve Düzeltmeler

**Sorun: Oyuncu hiç prestige yapamıyor**
→ `PrestigeConfig.MinGoldToPrestige` değerini düşür
→ `PrestigeConfig.MinWaveForPrestige` değerini düşür (wave oyunları)

**Sorun: İlk prestige çok erken (1-2. oturumda)**
→ `PrestigeConfig.MinGoldToPrestige` değerini artır

**Sorun: Geç oyunda altın büyümesi durdu**
→ Generator yield değerlerini artır
→ Prestige çarpanı sınırını artır (`MaxPermanentMultiplier`)

**Sorun: Ekonomi çok hızlı patladı, hard cap'e çarptı**
→ `EconomyConfig.ResourceHardCap` değerini artır
→ `EconomyConfig.BaseGoldDropScalingExponent` değerini azalt

---

## 6. ADIM 5 — PRESTİGE SİSTEMİ KUR

### Config Değerleri (PrestigeConfigSO)

```
Assets/[OyunAdın]/Configs/PrestigeConfig.asset
```

| Alan | Önerilen Değer | Açıklama |
|------|---------------|----------|
| BaseMultiplierPerPrestige | 1.5 - 2.0 | Her prestige'de kalıcı gelir çarpanı |
| MaxPermanentMultiplier | 1000 - 10000 | Çarpan tavanı |
| MinGoldToPrestige | Oyununa göre | Altın kapısı (0 = kapalı) |
| MinWaveForPrestige | 0 (wave yok) / 10 (wave var) | Wave kapısı |
| MaxPrestigeCount | 0 = sınırsız | Prestige sınırı |

### Prestige Sıfırlaması Neyi Etkiler

`PrestigeStateManager.OnPrestigeStarted` event'ini dinleyen tüm sistemler sıfırlanır:
- `EconomyService` → altın sıfırlanır
- `GeneratorSystem` → tüm generator'lar sıfırlanır
- `UpgradeTreeService` → tüm upgrade'ler sıfırlanır (kalıcı değil)
- `WaveSpawnManager` → dalga sıfırlanır
- `HealthSystem` → HP sıfırlanır

**Sıfırlanmayan şeyler:**
- Kalıcı çarpan (`GetPermanentMultiplier()`)
- Prestige sayısı
- Prestige-gated upgrade node'lar (bir sonraki prestige'de görünür)

### Çok Katmanlı Prestige (Prestige-Heavy)

`AscensionStateManager` ile katman sistemi:
- Her katman kendi çarpanını ve reset kurallarını tanımlar
- `AscensionDatabaseSO` — tüm katmanların koleksiyonu
- `AscensionLayerConfigSO` — tek katman ayarları

---

## 7. ADIM 6 — OYUN TİPİNE ÖZGÜ SİSTEMLER

---

### 7.1 Click Loop (ClickLoop Idle, Clicker Idle)

**Click Target'ları Ayarla** (`Assets/[OyunAdın]/Configs/ClickTarget_0.asset` vb.):

```
TargetId:          "target_0"
MaxHP:             10.0          (ne kadar tıklama gerekir)
DamagePerClick:    1.0
BaseYield:         3.0           (yok edilince verilen altın)
RespawnSeconds:    3.0           (kaç saniyede geri döner)
AwardYieldPerClick: false        (true = her tıkta altın, false = yok edilince)
ComboContribution:  1.0          (combo'ya katkı)
```

**Click Loop Config** (`Assets/[OyunAdın]/Configs/ClickLoopConfig.asset`):
```
BaseAutoClickRate:    0.0        (0 = auto-click yok, 2.0 = saniyede 2 auto)
BaseCritChance:       0.05       (%5 crit şansı)
BaseCritMultiplier:   2.0        (crit 2x altın)
ComboDecaySeconds:    2.0        (2 saniye tıklama olmazsa combo sıfırlanır)
MaxComboMultiplier:   5.0        (combo tavanı)
```

**Upgrade Tree'de Click istatistikleri:**
- `StatType.ClickDamage` — hasar artışı
- `StatType.ClickCritChance` — crit şansı
- `StatType.ClickCritMultiplier` — crit çarpanı
- `StatType.ClickAutoRate` — auto-click hızı

---

### 7.2 Harvest Idle

**Harvest Area Config** (`Assets/[OyunAdın]/Configs/HarvestAreaConfig.asset`):
```
BaseTickInterval:    0.1         (saniyede kaç tick, 0.1 = 10/s)
MaxComboMultiplier:  8.0
ComboDecaySeconds:   1.5
```

**Harvest Node Config** (`Assets/[OyunAdın]/Configs/HarvestNode.asset`):
```
NodeId:             "rock_node"
MaxHP:              10.0
DamagePerTick:      1.0
BaseYield:          5.0
RespawnSeconds:     4.0
AwardYieldPerTick:  true         (her tick'te altın)
ComboContribution:  1.0
```

Birden fazla node tipi için birden fazla `HarvestNodeConfigSO` oluştur. HarvestLoopBootstrap'ta `_nodeConfigs` array'ine ekle.

---

### 7.3 Idle-vs / RPG ve Tower Defense (Wave Oyunları)

**Wave Config** (`Assets/[OyunAdın]/Configs/WaveConfig.asset`):
```
BaseEnemyCountPerWave:      3
EnemyCountScalingFactor:    1.2       (her dalgada %20 daha fazla düşman)
HardCapEnemiesOnScreen:     20
WaveDurationSeconds:        8.0
WaveTransitionDelaySeconds: 2.0
UpgradeSelectionWaveInterval: 5       (her 5 dalgada upgrade seç)
```

**Enemy Stat Config** (`Assets/[OyunAdın]/Configs/EnemyStatConfig.asset`):
```
BaseHealth:              20.0
HealthScalingFactor:     1.15          (her dalgada HP ×1.15)
BaseDamage:              5.0
DamageScalingFactor:     1.10
BaseGoldDrop:            (EconomyConfig'den gelir)
```

**Run Config** (`Assets/[OyunAdın]/Configs/RunConfig.asset`):
```
RunDurationSeconds:      120           (2 dakikalık run)
```

**Wave Upgrade'leri için istatistikler:**
- `StatType.Damage` — saldırı gücü
- `StatType.Health` — can
- `StatType.Armor` — savunma
- `StatType.CritChance` — crit şansı

---

### 7.4 Merge Idle

**İtem tanımları (ItemConfigSO):**
```
ItemId:          "coin_t1"
MergeGroupId:    "coins"         (aynı grup birbiriyle birleşir)
MergeTier:       1               (tier 1 + tier 1 = tier 2)
BaseYield:       5               (satışta verilen altın)
DisplayName:     "Bronz Madeni"
```

**Birleştirme kuralları (MergeConfigSO):**
```
MergeGroupId:    "coins"
TierProgressions: [
  {InputTier:1, OutputItemId:"coin_t2"},
  {InputTier:2, OutputItemId:"coin_t3"},
  {InputTier:3, OutputItemId:"coin_t4"},
]
GoldBonusPerMerge: 10
```

**Starter Merge Config:** Wizard `StarterMergeConfig.asset` oluşturur, bunu düzenle.

---

### 7.5 Research Idle

**Research Tree Config** (`Assets/[OyunAdın]/Configs/ResearchDatabase.asset`):

```
TreeId: "tech_tree"
Nodes: [
  {
    NodeId: "basic_income",
    DisplayName: "Temel Gelir",
    ResearchTicks: 60,         (60 saniye = 1 dakika araştırma)
    GoldCost: 500,
    StatBonus: {Stat: IdleYieldRate, Value: 0.1}
  },
  {
    NodeId: "advanced_income",
    DisplayName: "Gelişmiş Gelir",
    ResearchTicks: 300,        (5 dakika)
    GoldCost: 5000,
    PrerequisiteNodeIds: ["basic_income"],
    StatBonus: {Stat: IdleYieldRate, Value: 0.25}
  }
]
```

**Araştırmayı başlatmak:**
```csharp
researchService.TryEnqueue("tech_tree", "basic_income");
```

---

### 7.6 Building Idle

**Building Config** (`Assets/[OyunAdın]/Configs/StarterBuilding.asset`):
```
BuildingId:           "house"
DisplayName:          "Ev"
BaseCost:             200
ProductionPerSecond:  2.0
MaxUpgradeTier:       5
UpgradeCostMultiplier: 2.0    (her tier 2x daha pahalı)
UpgradeProductionMultiplier: 1.5  (her tier %50 daha fazla üretim)
GridWidth:            1
GridHeight:           1
```

**Bina yerleştirme:**
```csharp
buildingService.TryPlace("house", gridX: 0, gridY: 0);
```

---

## 8. ADIM 7 — İÇERİK GENİŞLETME

### Yeni Generator Ekleme

1. `Tools → Endless Engine → Generator Editor`
2. `+ Add` → yeni generator oluştur
3. İsim, yield, maliyet, scale gir
4. `Save`
5. Simülatörde yeniden test et

### Yeni Upgrade Node Ekleme

1. `Tools → Endless Engine → Upgrade Tree Editor`
2. Mevcut tree'yi yükle veya yenisini oluştur
3. `+ Add Node` → node oluştur
4. Inspector'da stat, etki, maliyet gir
5. Bağlantıları çiz
6. `Save`

### Çoklu Upgrade Tree

Farklı içerik alanları için farklı tree'ler oluşturabilirsin:
- `UpgradeTreeConfig_Production.asset` — üretim upgrade'leri
- `UpgradeTreeConfig_Combat.asset` — savaş upgrade'leri
- `UpgradeTreeConfig_Prestige.asset` — prestige bonusları

Her tree'yi AutoSetupBootstrap'ın `_upgradeConfigs` array'ine ekle.

### Yeni Research Node Ekleme

`ResearchDatabase.asset` dosyasını Inspector'da aç → `Nodes` listesine yeni eleman ekle.

### Prestige Katmanı Ekleme (Prestige-Heavy)

1. `AscensionDatabase.asset` aç
2. Yeni `AscensionLayerConfig` ekle
3. Katmanın çarpanını, reset kuralını ve kilit koşulunu tanımla

### İkincil Para Birimi Ekleme

1. Yeni `CurrencyConfigSO` oluştur: `Assets → Create → Endless Engine → Config → Currency`
2. CurrencyId, DisplayName, HardCap, ResetOnPrestige ayarla
3. `CurrencyDatabase.asset` dosyasına ekle
4. HUD'da göstermek için `CurrencyService.OnBalanceChanged` event'ine abone ol

---

## 9. ADIM 8 — KAYIT SİSTEMİ VE VERİ GÜVENLİĞİ

### Otomatik Kayıt

SaveService her 60 saniyede bir otomatik kaydeder. Özellikle belirtmeye gerek yok.

### Manuel Kayıt Tetikleme

```csharp
// Herhangi bir önemli aksiyondan sonra (generator satın alma vb.)
saveService.NotifyUpgradePurchased(); // 5 saniye debounce ile kaydeder
```

### Yeni Sistem için Kayıt Ekleme

Eğer özel bir sistem yazıyorsan ve kayıt etmek istiyorsan:

```csharp
public class MyCustomService : MonoBehaviour, ISaveStateProvider
{
    public int ProviderOrder => 95; // diğer sistemlerden sonra

    public void OnBeforeSave(SaveData saveData)
    {
        saveData.CustomField = myData; // SaveData'ya alan ekle
    }

    public void OnAfterLoad(SaveData saveData)
    {
        myData = saveData.CustomField;
    }
}

// Bootstrap'ta:
saveService.RegisterStateProvider(myService);
```

### Save Schema Güncellemesi

SaveData'ya yeni alan eklediğinde:
1. `Tools → Endless Engine → Bump Schema Version` — versiyonu artırır
2. Oluşan `SaveMigration_VN_VN+1.cs` dosyasını doldur:

```csharp
public class SaveMigration_V4_V5 : IMigration
{
    public int FromVersion => 4;
    public int ToVersion   => 5;

    public void Apply(SaveData data)
    {
        // Eski kayıtlarda olmayan yeni alanı varsayılanla doldur
        data.MyNewField = 0;
    }
}
```

---

## 10. ADIM 9 — HUD VE UI BAĞLAMA

### GeneratedGameHUD (Otomatik)

Wizard'ın oluşturduğu sahnede `GeneratedGameHUD` component'ı zaten şu olayları dinler:
- Altın değişimi → `GoldLabel`
- Gelir oranı → `IncomeLabel`
- Wave sayısı → `WaveLabel`
- Harvest geliri → `HarvestLabel`
- Combo → `ComboLabel`
- Research ilerlemesi → `ResearchLabel`

Bunlar, sahnedeki GameObject isimlerine göre otomatik bulunur. GameObject ismini değiştirme.

### Özel UI Eklemek

Kendi UI'ını eklemek istiyorsan, event'lere abone ol:

```csharp
// Altın değişti
EconomyService.OnResourcesChanged += (current, delta) =>
{
    goldText.text = FormatGold(current);
};

// Generator satın alındı
GeneratorSystem.OnGeneratorPurchased += generatorId =>
{
    RefreshGeneratorPanel();
};

// Prestige tamamlandı
PrestigeStateManager.OnPrestigeComplete += (count, multiplier) =>
{
    prestigeCountText.text = $"Prestige: {count}";
    multiplierText.text = $"×{multiplier:F1}";
};

// Wave başladı
WaveSpawnManager.OnWaveStarted += wave =>
{
    waveText.text = $"Dalga {wave}";
};

// Research ilerledi
ResearchService.OnResearchProgress += (treeId, nodeId, tick, total) =>
{
    researchBar.fillAmount = (float)tick / total;
};
```

### Prestige Butonunu Aktif/Pasif Yapma

```csharp
void Update()
{
    var pm = FindFirstObjectByType<PrestigeStateManager>();
    prestigeButton.interactable = pm != null && pm.CanPrestige;
}
```

### Generator Kartı Güncelleme

```csharp
void RefreshCard(string generatorId)
{
    double cost = generators.GetNextCost(generatorId);
    int count   = generators.GetCount(generatorId);

    costLabel.text  = $"Maliyet: {FormatGold(cost)}";
    countLabel.text = $"Sahip: {count}";
    buyButton.interactable = economy.CurrentResources >= cost;
}
```

---

## 11. ADIM 10 — STEAM ENTEGRASYONU

### Kurulum

1. `Steamworks.NET` paketini yükle (veya `com.rlabrecque.steamworks.net`)
2. `steam_appid.txt` dosyasını proje kökünde oluştur (App ID'ni yaz)
3. `SteamService.Initialize()` çağrısını oyun başlangıcına ekle

### Achievement

```csharp
// Achievement kilidini aç
SteamAchievementBridge.UnlockAchievement("ACH_FIRST_PRESTIGE");

// Prestige olduğunda otomatik achievement
PrestigeStateManager.OnPrestigeComplete += (count, mult) =>
{
    if (count == 1) SteamAchievementBridge.UnlockAchievement("ACH_FIRST_PRESTIGE");
    if (count == 10) SteamAchievementBridge.UnlockAchievement("ACH_PRESTIGE_10");
};
```

### Leaderboard

```csharp
// Skor gönder (örn: toplam prestige sayısı)
SteamLeaderboardService.SubmitScore(prestigeStateManager.PrestigeCount);

// Skor tablosunu al
var entries = await SteamLeaderboardService.GetTopEntries(count: 10);
```

### Cloud Save

```csharp
// Kayıt dosyasını Steam Cloud'a yükle
SteamCloudSaveSync.Upload(
    Path.Combine(Application.persistentDataPath, "save.json")
);

// İndir (oyun başlangıcında, yerel kayıtla birleştir)
await SteamCloudSaveSync.Download();
```

### Steam Olmadan Test

`NullSteamService` sınıfı tüm Steam çağrılarını no-op olarak karşılar. Geliştirme sırasında Steam SDK olmadan çalışmaya devam eder.

---

## 12. SENARYOLAR: HER OYUN TİPİ İÇİN TAM YOL HARİTASI

---

### SENARYO A — "Klasik Idle" (Pure Idle)

**Hedef:** Cookie Clicker / Adventure Capitalist tarzı

**Adımlar:**
1. New Game Wizard → Pure Idle
2. Generator Editor'de 8-12 generator oluştur (her biri öncekinin 10x maliyeti, 5x güçlü)
3. Upgrade Tree Editor'de 30-50 node ekle:
   - Production category: GeneratorYield stat (+10%, MaxRank=5)
   - Economy category: IdleYieldRate stat
   - Prestige category: prestige çarpanı artışı (PrestigeGate=1)
4. Economy Simulator: 30 oturum simüle et, ilk prestige 5-8. oturumda gelmeli
5. PrestigeConfig: BaseMultiplier=1.5, MinGold=prestige anındaki gold miktarının %90'ı
6. Tekrar simüle et, dengeli mi kontrol et

**İçerik hacmi (yayın için minimum):**
- 10+ generator
- 50+ upgrade node
- 5+ araştırma node'u (opsiyonel)
- 20+ prestige (simülatörde test et)

---

### SENARYO B — "Click Loop" (Aktif Idle)

**Hedef:** Clicker Heroes / Tap Titans tarzı

**Adımlar:**
1. New Game Wizard → Click Loop
2. 3 ClickTarget config oluştur (farklı HP/yield/renk)
3. ClickLoopConfig: BaseCritChance=0.05, BaseAutoClickRate=0 (başta 0)
4. Generator Database: 5-8 pasif generator ekle
5. Upgrade Tree: hem Click hem Production stat'ları için node'lar
   - ClickDamage +10% (Production category, orange)
   - ClickCritChance +2% (Combat category, red)
   - ClickAutoRate +0.5 (Economy category, blue — her rank'ta saniyede 0.5 auto-click)
   - GeneratorYield +15% (Production category)
6. Economy Simulator'de test et

**İçerik hacmi:**
- 3-5 click target (farklı HP/renkler/yield)
- 8+ generator
- 40+ upgrade node (click odaklı + pasif gelir)

---

### SENARYO C — "Wave RPG" (Idle-vs / RPG)

**Hedef:** Idle Heroes / AFK Arena tarzı

**Adımlar:**
1. New Game Wizard → Idle-vs / RPG
2. WaveConfig: BaseEnemyCount=3, ScalingFactor=1.2, WaveInterval=5
3. EnemyStatConfig: BaseHealth=20, HealthScale=1.15
4. Upgrade Tree: savaş odaklı (Damage, Health, CritChance, CritMultiplier)
5. PrestigeConfig: MinWaveForPrestige=10
6. Wave upgrade selection card'larını ayarla (UpgradeSelectionConfigSO)
7. Düşman loot tablosu (DropTableConfigSO) oluştur

**İçerik hacmi:**
- 5+ generator
- 40+ upgrade node (savaş ağırlıklı)
- 3 farklı düşman tipi (EnemyStatConfig çoğalt)
- 50+ wave hedefi (simülatörle test et)

---

### SENARYO D — "Merge Idle"

**Hedef:** Merge Dragons / Merge Mansion tarzı

**Adımlar:**
1. New Game Wizard → Merge Idle
2. 3-5 merge grubu oluştur (coins, gems, items vb.)
3. Her grup için 5-8 tier ItemConfigSO oluştur
4. Her grup için MergeConfigSO oluştur (tier progressions)
5. StarterMergeConfig.asset'i düzenle
6. Board grid boyutunu artır (SceneSetupUtility'de MergeBoard boyutu)

**Örnek merge hiyerarşisi:**
```
Bronz Para (T1) + Bronz Para (T1) → Gümüş Para (T2)
Gümüş Para (T2) + Gümüş Para (T2) → Altın Para (T3)
Altın Para (T3) + Altın Para (T3) → Elmas (T4)
```

---

### SENARYO E — "Research Idle"

**Hedef:** Universal Paperclips / Kittens Game tarzı

**Adımlar:**
1. New Game Wizard → Research Idle
2. ResearchDatabase.asset: 20-30 araştırma node'u oluştur
3. Node süreleri: başta 30-60 saniye, sonra 5-30 dakika
4. Her node bir stat bonusu veya içerik kilidi açsın
5. Generator geliri araştırma unlock'larına bağlı: `UnlockPrerequisite = research_node_X`

---

### SENARYO F — "Prestige-Heavy"

**Hedef:** Antimatter Dimensions / Realm Grinder tarzı

**Adımlar:**
1. New Game Wizard → Prestige-Heavy
2. AscensionDatabase: 3 katman oluştur
   - Katman 1 (Normal Prestige): BaseMultiplier=1.5, unlock=0
   - Katman 2 (Ascension): BaseMultiplier=3.0, unlock=prestige10
   - Katman 3 (Transcension): BaseMultiplier=10.0, unlock=ascension5
3. Her katman için ayrı skill tree oluştur
4. Katman geçişleri farklı şeyleri sıfırlar (AscensionLayerConfig'de tanımla)
5. Economy Simulator'de 100 oturum simüle et

---

## GENEL İPUÇLARI

### "Oyun çok hızlı bitiyor" sorunu
→ Generator cost scale'i artır (1.15 → 1.20)
→ Prestige kapısını yükselt
→ Daha fazla upgrade node ekle (ilerlemeyi yavaşlatır)

### "Oyun çok sıkıcı / çok yavaş" sorunu
→ İlk generator maliyetini düşür
→ İlk prestige için gereken altını düşür
→ Offline gelir cap'ini artır (daha çok geri dönüş ödülü)

### "Ekonomi patlıyor, hard cap'e çarpıyor" sorunu
→ `EconomyConfig.ResourceHardCap` değerini 10x artır
→ `NumberBackend = BigDouble` yap (aşırı büyük sayılar için)

### "Config değiştirdim ama oyunda görünmüyor" sorunu
→ Play modundayken yapılan config değişiklikleri Play durduğunda kaybolur
→ Config'i Play modundan ÖNCE değiştir

### ID çakışması uyarısı alıyorum
→ `Tools → Endless Engine → ID Registry` aç → çakışan ID'yi bul → düzelt

### Yeni sahne/içerik ekledim, kayıt çalışmıyor
→ `Tools → Endless Engine → Schema Bump Utility` çalıştır
→ Migration dosyasını doldur
→ `ISaveStateProvider` implemente et ve `RegisterStateProvider` çağır

---

## STEAM YAYINI ÖNCESİ KONTROL LİSTESİ

- [ ] Economy Simulator'de 50+ oturum simüle edildi, denge sağlandı
- [ ] Config Validator'da sıfır hata
- [ ] ID Registry'de sıfır çakışma
- [ ] Tüm oyun tipleri Play'e basınca çalışıyor
- [ ] Kayıt/yükleme test edildi (Play → Stop → Play → veri kaldı mı?)
- [ ] Offline gelir test edildi (zaman at, geri dön, altın geldi mi?)
- [ ] Prestige crash-safety test edildi (prestige sırasında Play durdur, geri dönünce rollback oldu mu?)
- [ ] Steam achievement'ları tanımlandı
- [ ] Steam leaderboard entegrasyonu test edildi
- [ ] Build Settings'te doğru sahne var
- [ ] Tüm gereksiz debug log'ları kapatıldı (`Debug.Log` → Development Build only)

---

*Endless Engine v1.3.4 — Oyun Üretim Rehberi*
