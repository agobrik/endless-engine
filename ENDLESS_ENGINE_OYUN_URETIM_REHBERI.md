# Endless Engine — Oyun Üretim Rehberi
## "Sıfırdan Steam Oyununa" Adım Adım Kılavuz

> Bu doküman, Endless Engine toolset'i kullanarak gerçek bir idle/incremental oyun üretmek isteyen herkes için yazılmıştır.
> **Tüm field isimleri, metod imzaları ve yapılar kaynak koddan doğrulanmıştır.**

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
2. `Window → Package Manager → + → Add package from disk`
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

**Şehir inşa sistemi istiyorum:**
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

### AutoSetupBootstrap Nedir?

Wizard'ın oluşturduğu sahnedeki `Bootstrap` GameObject'inde **AutoSetupBootstrap** component'ı bulunur. Inspector'da şu alanlar görünür:

| Alan | Tipi | Açıklama |
|------|------|----------|
| `_economyConfig` | EconomyConfigSO | Ekonomi ayarları (hard cap, starting gold vb.) |
| `_generatorDatabase` | GeneratorDatabaseSO | Tüm generator'ların koleksiyonu |
| `_schemaVersion` | SchemaVersionSO | Kayıt şeması versiyon asset'i |
| `_prestigeConfig` | PrestigeConfigSO | Prestige ayarları (null = prestige kapalı) |
| `_realmConfig` | RealmIdentityConfigSO | Realm ayarları (null = varsayılan) |
| `_enableSave` | bool | Otomatik kayıt açık/kapalı |

> **Önemli:** AutoSetupBootstrap yalnızca temel sistemleri (Economy, Generator, UpgradeTree, Save, Tick) başlatır. Wave, ClickLoop, Harvest, Research, Building gibi sistemler kendi Bootstrap script'leri ile başlatılır — Wizard bunları otomatik ekler.

---

## 3. ADIM 2 — GENERATOR'LARI TASARLA

### Generator Editor'ü Aç

`Tools → Endless Engine → Generator Editor`

Editor, Inspector'da seçili `GeneratorDatabaseSO` asset'ini yükler. Wizard'ın oluşturduğu asset: `Assets/[OyunAdın]/Configs/GeneratorDatabase.asset`

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
1. `+ Add` butonuna bas → yeni GeneratorConfigSO asset oluşur
2. Sol panelden seç → sağda Inspector açılır
3. Değerleri gir
4. Maliyet eğrisi grafiği anlık güncellenir
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

İlk kez açtığında boş gelir. `Load Asset` ile wizard'ın oluşturduğu `UpgradeTreeConfig.asset` dosyasını veya `New Tree` ile yenisini yükle.

### UpgradeTreeConfigSO Yapısı

Tüm node'lar tek bir `UpgradeTreeConfigSO` asset içinde `Nodes` (List\<UpgradeNodeDefinition\>) listesi olarak saklanır — her node için ayrı asset dosyası oluşturulmaz.

### Node Ekleme

1. `+ Add Node` butonuna bas veya graph'ta sağ tıkla → "Add Node"
2. Sağdaki Inspector'da dolduracakların:

**Identity:**
| Field | Açıklama |
|-------|----------|
| `NodeId` | Benzersiz, küçük harf, alt çizgi (örn. `dmg_01`) — yayın sonrası asla değiştirme |
| `DisplayName` | Oyuncunun gördüğü isim ("Güçlü Darbeler") |
| `Description` | Kısa açıklama |

**Stats:**
| Field | Açıklama |
|-------|----------|
| `Category` | `Production` / `Combat` / `Survival` / `Economy` / `Prestige` |
| `AffectedStat` | StatType enum — hangi istatistiği etkiliyor |
| `EffectType` | `PercentBonus` (%X ekler) veya `FlatBonus` (X ekler) |
| `EffectPerRank` | Her rankta etki değeri (0.10 = %10) |
| `MaxRank` | Kaç kez alınabilir |

**Economy:**
| Field | Açıklama |
|-------|----------|
| `BaseCost` | İlk rank maliyeti |
| `CostScalingFactor` | Her rankta çarpan (1.5 = ikinci rank 1.5x pahalı) |
| `SelectionWeight` | Upgrade kartı olarak seçilme şansı (yüksek = sık çıkar) |
| `PrestigeGateRequirement` | Kaçıncı prestige'den sonra görünür (0 = hep görünür) |

**Tree Behaviour:**
| Field | Açıklama |
|-------|----------|
| `PrerequisiteNodeIDs` | Bu node için önce alınması gereken node ID'leri |
| `MaxOutgoingEdges` | Bu node'dan kaç bağlantı çıkabilir (0 = sınırsız) |
| `HideUntilUnlockable` | true = önkoşullar sağlanana kadar UI'da gizle |

**Layout (tree canvas konumu):**
| Field | Açıklama |
|-------|----------|
| `GridX` | Canvas'taki sütun index'i (0-based) |
| `GridY` | Canvas'taki satır index'i (0-based) |

### Node'ları Bağlama

Bir node'un çıkış portunu (sağ nokta) başka bir node'un giriş portuna (sol nokta) sürükle.
Bu, hedef node'un `PrerequisiteNodeIDs` listesine otomatik eklenir.

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
PrestigeGateRequirement=0: Temel node'lar (herkese görünür)
PrestigeGateRequirement=1: Güçlendirilmiş node'lar (1. prestige'den sonra)
PrestigeGateRequirement=3: Efsanevi node'lar (3. prestige'den sonra)
```

### Kaydetme

`Save` butonuna bas. Kaydedilmeden pencereyi kapatmak istersen "Kaydet mi?" diye sorar.

### Çoklu Upgrade Tree

Farklı içerik alanları için farklı UpgradeTreeConfigSO asset'leri oluşturabilirsin:
- `UpgradeTreeConfig_Production.asset`
- `UpgradeTreeConfig_Combat.asset`
- `UpgradeTreeConfig_Prestige.asset`

Her tree'yi sahnedeki `UpgradeTreeService` component'ının Inspector'ına ekle. (AutoSetupBootstrap kullananlar için UpgradeTreeService ilk bulduğu tree'yi otomatik kullanır — birden fazla tree için UpgradeTreeService'e doğrudan referans vererek `LoadTree(treeConfig)` çağırmalısın.)

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

### EconomyConfigSO Alanları

`Assets/[OyunAdın]/Configs/EconomyConfig.asset`

| Field | Varsayılan | Açıklama |
|-------|-----------|----------|
| `IdleYieldRateBase` | 10 | Prestige 0'da saniye başına gelir |
| `BaseMultiplierPerPrestige` | 1.5 | Her prestige'de gelir çarpanı |
| `IdleYieldMultiplierCap` | 100 | Çarpan tavanı |
| `StartingGold` | 0 | Yeni oyunda/prestige sonrası başlangıç altını |
| `ResourceHardCap` | 1_000_000_000 | Maximum altın miktarı |
| `NumberBackend` | DoubleNumber | `DoubleNumber` (≤1e15) veya `BigDouble` (çok büyük sayılar) |
| `OfflineCapHours` | 8 | Çevrimdışı kazancı kaç saate kadar hesaplanır |
| `BaseGoldDropPerEnemy` | 1 | Wave başı düşman altın dropu (base) |
| `GoldDropScalingExponent` | 1.2 | Altın dropu dalga ölçekleme katsayısı |

### Yaygın Sorunlar ve Düzeltmeler

**Sorun: Oyuncu hiç prestige yapamıyor**
→ `PrestigeConfig.MinGoldToPrestige` değerini düşür
→ `PrestigeConfig.MinWaveForPrestige` değerini düşür (wave oyunları)

**Sorun: İlk prestige çok erken (1-2. oturumda)**
→ `PrestigeConfig.MinGoldToPrestige` değerini artır

**Sorun: Geç oyunda altın büyümesi durdu**
→ Generator yield değerlerini artır
→ `PrestigeConfig.MaxPermanentMultiplier` değerini artır

**Sorun: Ekonomi çok hızlı patladı, hard cap'e çarptı**
→ `EconomyConfig.ResourceHardCap` değerini artır
→ `EconomyConfig.NumberBackend = BigDouble` yap

---

## 6. ADIM 5 — PRESTİGE SİSTEMİ KUR

### PrestigeConfigSO Alanları

`Assets/[OyunAdın]/Configs/PrestigeConfig.asset`

| Field | Önerilen Değer | Açıklama |
|-------|---------------|----------|
| `MinWaveForPrestige` | 0 (wave yok) / 10 (wave var) | Wave kapısı |
| `MinGoldToPrestige` | Oyununa göre | Altın kapısı (0 = kapalı) |
| `MaxPrestigeCount` | 0 = sınırsız | Prestige sınırı |
| `BaseMultiplierPerPrestige` | 1.5 - 2.0 | Her prestige'de kalıcı gelir çarpanı |
| `MaxPermanentMultiplier` | 1000 - 10000 | Çarpan tavanı |
| `StatsAmplifiedByPrestige` | StatType[] | Prestige çarpanının etkileyeceği stat'lar |

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
- `PrestigeGateRequirement > 0` olan upgrade node'lar (bir sonraki prestige'de görünür)

### Prestige Butonunu Aktif/Pasif Yapma

```csharp
void Update()
{
    var pm = FindFirstObjectByType<PrestigeStateManager>();
    prestigeButton.interactable = pm != null && pm.CanPrestige;
}
```

### Çok Katmanlı Prestige (Prestige-Heavy)

`AscensionStateManager` + `AscensionDatabaseSO` ile:

**AscensionDatabaseSO** — tüm katmanların koleksiyonu
- `Layers` → `PrestigeLayerConfigSO[]`

**PrestigeLayerConfigSO** — tek katman ayarları

| Field | Açıklama |
|-------|----------|
| `LayerIndex` | Katman numarası (0-based) |
| `DisplayName` | Katman adı ("Prestige", "Ascension" vb.) |
| `ActionVerb` | Buton metni ("Prestige", "Ascend" vb.) |
| `MinWaveRequired` | Bu katman için min dalga |
| `RequiredPreviousLayerCount` | Bir önceki katmanın kaç kez tetiklenmesi gerekir |
| `MaxCount` | Bu katman kaç kez tetiklenebilir (0 = sınırsız) |
| `ResetScope` | Neyin sıfırlanacağı |
| `ResetGenerators` | true = generator'lar sıfırlanır |
| `ResetSecondaryCurrencies` | true = ikincil para birimleri sıfırlanır |
| `BaseMultiplierPerTrigger` | Her tetiklemede çarpan artışı |
| `MaxPermanentMultiplier` | Bu katman için çarpan tavanı |
| `RewardCurrencyId` | Tetiklemede verilen para birimi ID'si |
| `BaseCurrencyReward` | Tetiklemedeki temel ödül miktarı |

---

## 7. ADIM 6 — OYUN TİPİNE ÖZGÜ SİSTEMLER

---

### 7.1 Click Loop (ClickLoop Idle, Clicker Idle)

**ClickTargetConfigSO** — `Assets/[OyunAdın]/Configs/ClickTarget_0.asset` vb.

| Field | Örnek Değer | Açıklama |
|-------|------------|----------|
| `TargetId` | `"target_0"` | Benzersiz ID |
| `DisplayName` | `"Taş"` | Gösterilen isim |
| `MaxHP` | 10.0 | Kaç tıklama gerekir |
| `DamagePerClick` | 1.0 | Tıklama başına hasar |
| `BaseYield` | 3.0 | Yok edilince verilen altın |
| `AwardYieldPerClick` | false | true = her tıkta altın, false = yok edilince |
| `RespawnSeconds` | 3.0 | Kaç saniyede geri döner |
| `ComboContribution` | 1.0 | Combo sayacına katkı |
| `YieldCurrencyId` | `"gold"` | Hangi para birimine ödül verir |

**ClickLoopConfigSO** — `Assets/[OyunAdın]/Configs/ClickLoopConfig.asset`

| Field | Örnek Değer | Açıklama |
|-------|------------|----------|
| `ComboDecayDelay` | 2.0 | Tıklama durduğunda combo azalmaya başlamadan önceki süre (saniye) |
| `ComboDecayRate` | 1.0 | Saniyede azalan combo puanı |
| `ComboPointsPerStep` | 1.0 | Her tıkta kazanılan combo puanı |
| `MaxComboMultiplier` | 5.0 | Combo tavanı |
| `BaseCritChance` | 0.05 | %5 crit şansı |
| `BaseCritMultiplier` | 2.0 | Crit 2x altın |
| `BaseAutoClickRate` | 0.0 | 0 = auto-click yok, 2.0 = saniyede 2 auto |
| `OfflineCapHours` | 4.0 | Çevrimdışı auto-click kazancı tavanı |
| `OfflineEfficiency` | 0.5 | Çevrimdışı kazanç verimliliği |

**Upgrade Tree'de Click istatistikleri:**
- `StatType.ClickDamage` — hasar artışı
- `StatType.ClickCritChance` — crit şansı
- `StatType.ClickCritMultiplier` — crit çarpanı
- `StatType.ClickAutoRate` — auto-click hızı

---

### 7.2 Harvest Idle

**HarvestAreaConfigSO** — `Assets/[OyunAdın]/Configs/HarvestAreaConfig.asset`

| Field | Örnek Değer | Açıklama |
|-------|------------|----------|
| `BaseTickInterval` | 0.1 | Saniyede kaç tick (0.1 = 10/s) |
| `ComboDecayDelay` | 1.5 | Combo azalmadan önce bekleme süresi (saniye) |
| `ComboDecayRate` | 2.0 | Saniyede azalan combo |
| `ComboPointsPerMultiplierStep` | 5.0 | Bir sonraki çarpan seviyesi için gereken puan |
| `MaxComboMultiplier` | 8.0 | Combo tavanı |
| `OfflineCapHours` | 4.0 | Çevrimdışı kazanç tavanı |
| `OfflineEfficiency` | 0.5 | Çevrimdışı verimlilik |

**HarvestNodeConfigSO** — `Assets/[OyunAdın]/Configs/HarvestNode_Rock.asset` vb.

| Field | Örnek Değer | Açıklama |
|-------|------------|----------|
| `NodeId` | `"rock_node"` | Benzersiz ID |
| `DisplayName` | `"Kaya"` | Gösterilen isim |
| `MaxHP` | 10.0 | Kaç tick gerekir |
| `DamagePerTick` | 1.0 | Tick başına hasar |
| `BaseYield` | 5.0 | Yok edilince verilen altın |
| `AwardYieldPerTick` | true | true = her tick'te altın |
| `RespawnSeconds` | 4.0 | Kaç saniyede geri döner |
| `ComboContribution` | 1.0 | Combo katkısı |
| `YieldCurrencyId` | `"gold"` | Ödül para birimi |

Birden fazla node tipi için birden fazla HarvestNodeConfigSO oluştur. `HarvestLoopBootstrap` component'ının Inspector'ındaki `_nodeConfigs` array'ine ekle.

---

### 7.3 Idle-vs / RPG ve Tower Defense (Wave Oyunları)

**WaveConfigSO** — `Assets/[OyunAdın]/Configs/WaveConfig.asset`

| Field | Örnek Değer | Açıklama |
|-------|------------|----------|
| `BaseEnemyCountPerWave` | 3 | Dalga başı düşman sayısı (base) |
| `EnemyCountScalingFactor` | 1.2 | Her dalgada %20 daha fazla düşman |
| `HardCapEnemiesOnScreen` | 20 | Aynı anda maksimum düşman |
| `SpawnIntervalSeconds` | 0.5 | Düşmanlar arası spawn aralığı |
| `WaveDurationSeconds` | 8.0 | Dalga süresi |
| `WaveTransitionDelaySeconds` | 2.0 | Dalgalar arası geçiş süresi |
| `UpgradeSelectionWaveInterval` | 5 | Her 5 dalgada upgrade seçimi |
| `TotalWavesPerRun` | 50 | Run başına toplam dalga sayısı |
| `WaveSaveMilestoneInterval` | 10 | Her 10 dalgada otomatik kayıt |
| `EliteWaveInterval` | 10 | Her 10 dalgada bir elite wave |
| `EliteStatMultiplier` | 2.0 | Elite wave'de düşman stat çarpanı |
| `BossWaveInterval` | 25 | Her 25 dalgada bir boss wave |

**EnemyStatConfigSO** — `Assets/[OyunAdın]/Configs/EnemyStatConfig.asset`

| Field | Örnek Değer | Açıklama |
|-------|------------|----------|
| `BaseMaxHP` | 20.0 | Temel can (1. dalga) |
| `BaseAttackDamage` | 5.0 | Temel saldırı hasarı |
| `BaseContactDamage` | 2.0 | Temas hasarı (tick başına) |
| `MoveSpeed` | 3.0 | Hareket hızı |
| `AttackRange` | 2.0 | Saldırı menzili (world unit) |
| `AttackInterval` | 1.5 | Saldırılar arası süre (saniye) |
| `WaveScalingExponent` | 1.5 | HP ve hasar için dalga ölçekleme katsayısı |
| `HardCapEnemiesOnScreen` | 200 | Maksimum eş zamanlı düşman |

> **Not:** `WaveScalingExponent` hem HP hem hasara uygulanır. Dalga N'de HP = `BaseMaxHP × N^WaveScalingExponent`

**Wave Upgrade'leri için istatistikler:**
- `StatType.Damage` — saldırı gücü
- `StatType.Health` — can
- `StatType.Armor` — savunma
- `StatType.CritChance` — crit şansı

---

### 7.4 Merge Idle

**ItemConfigSO** — `Assets/[OyunAdın]/Configs/Items/CoinT1.asset` vb.

Yeni asset: `Assets → Create → Endless Engine → Loot → Item Config`

| Field | Örnek Değer | Açıklama |
|-------|------------|----------|
| `ItemId` | `"coin_t1"` | Benzersiz ID (yayın sonrası değiştirme) |
| `DisplayName` | `"Bronz Para"` | Gösterilen isim |
| `Description` | `"..."` | Açıklama metni |
| `Rarity` | `Common` | `Common` / `Uncommon` / `Rare` / `Epic` / `Legendary` |
| `MaxStackSize` | 99 | Stack sınırı (1 = stack'lenemez) |
| `MergeGroupId` | `"coins"` | Aynı grup birbirleriyle birleşir |
| `MergeTier` | 1 | Tier 1 + Tier 1 = Tier 2 |

**MergeConfigSO** — `Assets/[OyunAdın]/Configs/MergeConfig_Coins.asset`

Yeni asset: `Assets → Create → Endless Engine → Merge → Merge Config`

| Field | Açıklama |
|-------|----------|
| `ConfigId` | Benzersiz config ID |
| `MergeGroupId` | Hangi item grubu için (ItemConfigSO.MergeGroupId ile eşleşmeli) |
| `Rules` | `List<MergeRule>` — her tier için birleştirme kuralları |

**MergeRule** (Rules listesindeki her eleman):

| Field | Açıklama |
|-------|----------|
| `InputTier` | Birleştirilecek item'ların tier'ı (0-based) |
| `ResultItem` | Üretilecek ItemConfigSO asset referansı |
| `GoldBonus` | Birleştirme başına altın bonusu |

**Örnek merge hiyerarşisi:**
```
Rules listesi:
  Rule 0: InputTier=1, ResultItem=CoinT2.asset, GoldBonus=0
  Rule 1: InputTier=2, ResultItem=CoinT3.asset, GoldBonus=10
  Rule 2: InputTier=3, ResultItem=CoinT4.asset, GoldBonus=50
```

Yani: Bronz Para (T1) + Bronz Para (T1) → Gümüş Para (T2)

---

### 7.5 Research Idle

**ResearchTreeConfigSO** — `Assets/[OyunAdın]/Configs/ResearchTree.asset`

Yeni asset: `Assets → Create → Endless Engine → Research → Research Tree Config`

| Field | Açıklama |
|-------|----------|
| `TreeId` | Benzersiz tree ID |
| `DisplayName` | Görünen isim |
| `Nodes` | `ResearchNodeConfigSO[]` — tree'deki tüm node'lar |

**ResearchNodeConfigSO** — `Assets/[OyunAdın]/Configs/Research/BasicIncome.asset`

Yeni asset: `Assets → Create → Endless Engine → Research → Research Node Config`

| Field | Örnek Değer | Açıklama |
|-------|------------|----------|
| `NodeId` | `"basic_income"` | Benzersiz ID (yayın sonrası değiştirme) |
| `DisplayName` | `"Temel Gelir"` | Oyuncunun gördüğü isim |
| `Description` | `"Geliri %10 artırır"` | Kısa açıklama |
| `Tier` | 0 | Tier 0 = root. Önceki tier tamamlanmadan bu tier kuyruğa alınamaz |
| `PrerequisiteIds` | `["basic_income"]` | Önce tamamlanması gereken node ID'leri |
| `GoldCost` | 500 | Kuyruğa alma maliyeti |
| `SecondaryCurrencyId` | `""` | İkincil para birimi ID'si (boş = yok) |
| `SecondaryCurrencyCost` | 0 | İkincil maliyet |
| `ResearchTicks` | 60 | Tamamlanma süresi (saniye — TickEngine 1 Hz'dir) |
| `Effects` | `List<SkillEffect>` | Tamamlandığında uygulanan etkiler |

**Araştırmayı başlatmak:**
```csharp
researchService.TryEnqueue("tech_tree", "basic_income");
```

**ResearchTreeConfigSO'yu ResearchService'e bağlamak:**
Sahnedeki `ResearchService` component'ının Inspector'ında `_treeConfig` alanına drag et.

---

### 7.6 Building Idle

**BuildingConfigSO** — `Assets/[OyunAdın]/Configs/Buildings/House.asset`

Yeni asset: `Assets → Create → Endless Engine → Building Config`

| Field | Örnek Değer | Açıklama |
|-------|------------|----------|
| `BuildingId` | `"house"` | Benzersiz ID |
| `DisplayName` | `"Ev"` | Görünen isim |
| `Description` | `"..."` | Açıklama |
| `GridWidth` | 1 | Grid genişliği (hücre sayısı) |
| `GridHeight` | 1 | Grid yüksekliği (hücre sayısı) |
| `PlacementCost` | 200 | Yerleştirme maliyeti |
| `PlacementCurrencyId` | `"gold"` | Yerleştirme para birimi |
| `ProductionCurrencyId` | `"gold"` | Üretilen para birimi |
| `ProductionPerTick` | 2 | Tick başına üretim (1 tick = 1 saniye) |
| `MaxInstances` | 0 | Maksimum kopya sayısı (0 = sınırsız) |
| `UpgradeTiers` | `BuildingUpgradeTier[]` | Upgrade seviyeleri |

**BuildingUpgradeTier** (UpgradeTiers listesindeki her eleman):

| Field | Açıklama |
|-------|----------|
| `DisplayLabel` | "Level 2", "Tier II" vb. |
| `UpgradeCost` | Bu tier'a upgrade maliyeti |
| `UpgradeCurrencyId` | Upgrade para birimi |
| `ProductionBonusPerTick` | Base production'a eklenen bonus (flat) |
| `ProductionMultiplier` | Bonus üzerine çarpan |

**Bina yerleştirme:**
```csharp
buildingService.TryPlace("house", gridX: 0, gridY: 0);
```

---

## 8. ADIM 7 — İÇERİK GENİŞLETME

### Yeni Generator Ekleme

1. `Tools → Endless Engine → Generator Editor`
2. `+ Add` → yeni GeneratorConfigSO asset oluştur
3. İsim, yield, maliyet, scale gir
4. `Save`
5. Simülatörde yeniden test et

### Yeni Upgrade Node Ekleme

1. `Tools → Endless Engine → Upgrade Tree Editor`
2. Mevcut tree'yi yükle
3. `+ Add Node` → node oluştur
4. Inspector'da `AffectedStat`, `EffectPerRank`, `BaseCost`, `CostScalingFactor` gir
5. `PrerequisiteNodeIDs` ile bağlantıları kur
6. `Save`

### Yeni Research Node Ekleme

1. Yeni `ResearchNodeConfigSO` asset oluştur: `Assets → Create → Endless Engine → Research → Research Node Config`
2. `NodeId`, `DisplayName`, `Tier`, `GoldCost`, `ResearchTicks`, `Effects` doldur
3. `ResearchTreeConfigSO.Nodes` array'ine sürükle

### Prestige Katmanı Ekleme (Prestige-Heavy)

1. `Assets/[OyunAdın]/Configs/AscensionDatabase.asset` aç
2. `Layers` listesine yeni `PrestigeLayerConfigSO` asset oluştur ve ekle
3. `LayerIndex`, `RequiredPreviousLayerCount`, `BaseMultiplierPerTrigger` doldur

### İkincil Para Birimi Ekleme

1. Yeni asset: `Assets → Create → Endless Engine → Config → Currency`
2. Alanları doldur:

| Field | Açıklama |
|-------|----------|
| `CurrencyId` | Benzersiz ID |
| `DisplayName` | Görünen isim |
| `HardCap` | Maksimum miktar |
| `ResetsOnPrestige` | true = prestige'de sıfırlanır |

3. `CurrencyDatabase.asset` dosyasına bu asset'i ekle
4. HUD'da göstermek için:

```csharp
CurrencyService.OnBalanceChanged += (currencyId, newBalance) =>
{
    if (currencyId == "gems") gemsText.text = newBalance.ToString();
};
```

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

Kendi özel sisteminizi kaydetmek istiyorsanız `ISaveStateProvider` implemente edin:

```csharp
public class MyCustomService : MonoBehaviour, ISaveStateProvider
{
    public int ProviderOrder => 95; // daha yüksek = daha sonra çalışır

    public void OnBeforeSave(SaveData saveData)
    {
        saveData.CustomField = myData;
    }

    public void OnAfterLoad(SaveData saveData)
    {
        myData = saveData.CustomField;
    }
}

// Bootstrap'ta (Start veya Awake'te):
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

Bunlar sahnedeki GameObject isimlerine göre otomatik bulunur. GameObject ismini değiştirme.

### Özel UI — Event İmzaları

Aşağıdaki event imzaları kaynak koddan doğrulanmıştır:

```csharp
// Altın değişti — Action<double current, double delta>
EconomyService.OnResourcesChanged += (current, delta) =>
{
    goldText.text = FormatGold(current);
};

// Generator satın alındı — Action<string generatorId>
GeneratorSystem.OnGeneratorPurchased += generatorId =>
{
    RefreshGeneratorPanel();
};

// Prestige tamamlandı — Action<int count, float multiplier>
PrestigeStateManager.OnPrestigeComplete += (count, multiplier) =>
{
    prestigeCountText.text = $"Prestige: {count}";
    multiplierText.text = $"×{multiplier:F1}";
};

// Wave başladı — Action<int waveNumber>
WaveSpawnManager.OnWaveStarted += waveNumber =>
{
    waveText.text = $"Dalga {waveNumber}";
};

// Research ilerledi — Action<string treeId, string nodeId, int ticksDone, int ticksTotal>
ResearchService.OnResearchProgress += (treeId, nodeId, ticksDone, ticksTotal) =>
{
    researchBar.fillAmount = (float)ticksDone / ticksTotal;
};

// İkincil para birimi değişti — Action<string currencyId, double newBalance>
CurrencyService.OnBalanceChanged += (currencyId, newBalance) =>
{
    if (currencyId == "gems") gemsText.text = newBalance.ToString("N0");
};
```

### Generator Kartı Güncelleme

```csharp
void RefreshCard(string generatorId)
{
    // GetNextCost → long (long döner, BigDouble backend için GetNextCostBig() kullan)
    long cost  = generators.GetNextCost(generatorId);
    int  count = generators.GetCount(generatorId);

    costLabel.text  = $"Maliyet: {FormatGold(cost)}";
    countLabel.text = $"Sahip: {count}";
    buyButton.interactable = economy.CurrentResources >= cost;
}
```

### Prestige Butonunu Aktif/Pasif Yapma

```csharp
void Update()
{
    var pm = FindFirstObjectByType<PrestigeStateManager>();
    prestigeButton.interactable = pm != null && pm.CanPrestige;
}
```

---

## 11. ADIM 10 — STEAM ENTEGRASYONU

### Kurulum

1. `Steamworks.NET` paketini projeye ekle (`com.rlabrecque.steamworks.net`)
2. `steam_appid.txt` dosyasını proje kökünde oluştur → App ID'ni yaz
3. `SteamService.Initialize()` çağrısını oyun başlangıcına ekle

### Achievement Sistemi — Nasıl Çalışır

Achievement sistemi **MilestoneTracker** üzerine kuruludur:
1. `MilestoneConfigSO` oluştur → `MilestoneId`'yi Steam achievement API adıyla eşleştir
2. `SteamAchievementBridge` component'ını sahnedeki Bootstrap GameObject'ine ekle
3. `bridge.Initialize(steamService)` çağır
4. `MilestoneTracker.OnMilestoneCompleted` tetiklendiğinde bridge otomatik olarak `_steam.UnlockAchievement(apiName)` çağırır

```csharp
// Achievement ID'lerini override etmek istiyorsan (Steam API adı farklıysa):
// SteamAchievementBridge Inspector'ında _mappings listesine ekle:
// MilestoneId: "first_prestige"  →  SteamApiName: "ACH_FIRST_PRESTIGE"

// Doğrudan ISteamService ile:
_steam.UnlockAchievement("ACH_FIRST_PRESTIGE");
```

### Leaderboard

```csharp
// Skor gönder — boardId, playerName, score (long)
steamLeaderboardService.SubmitScore(
    boardId:    "main_leaderboard",
    playerName: "Player",
    score:      (long)prestigeStateManager.PrestigeCount);

// Liderlik tablosunu al — boardId, entryCount, callback
steamLeaderboardService.FetchGlobalLeaderboard(
    boardId:    "main_leaderboard",
    entryCount: 10,
    onComplete: entries =>
    {
        foreach (var e in entries)
            Debug.Log($"{e.Rank}. {e.PlayerName}: {e.Score}");
    });
```

### Cloud Save

Cloud save **otomatik** çalışır — `SaveService.OnSaveCompleted` her tetiklendiğinde `SteamCloudSaveSync` dosyayı Steam Cloud'a yükler.

```csharp
// Başlangıçta cloud save kontrolü (SaveService.LoadAsync()'ten ÖNCE çağır):
cloudSync.Initialize(saveService, steamService);
cloudSync.CheckForNewerCloudSave();

// CheckForNewerCloudSave, cloud kayıt daha yeniyse bu event'i tetikler:
SteamCloudSaveSync.OnCloudSaveNewerThanLocal += () =>
{
    // Oyuncuya "Cloud kayıtı daha yeni, yüklensin mi?" diye sor
    // Onaylarsa:
    cloudSync.RestoreFromCloud(success =>
    {
        if (success) Debug.Log("Cloud kayıtı yüklendi.");
    });
};
```

### Steam Olmadan Test

`NullSteamService.Instance` tüm Steam çağrılarını no-op olarak karşılar.
`Initialize(steamService: null)` çağırırsan otomatik olarak `NullSteamService` devreye girer.

---

## 12. SENARYOLAR: HER OYUN TİPİ İÇİN TAM YOL HARİTASI

---

### SENARYO A — "Klasik Idle" (Pure Idle)

**Hedef:** Cookie Clicker / Adventure Capitalist tarzı

**Adımlar:**
1. New Game Wizard → Pure Idle → Generate
2. Play'e bas, çalıştığını doğrula
3. **Generator Editor** — 8-12 generator oluştur:
   - Her biri öncekinin ~10x maliyeti, ~5x daha yüksek yield
   - `CostScalingFactor = 1.15`
4. **Upgrade Tree Editor** — 30-50 node ekle:
   - `Category=Production`, `AffectedStat=GeneratorYield`, `EffectType=PercentBonus`, `EffectPerRank=0.10`, `MaxRank=5`
   - `Category=Economy`, `AffectedStat=IdleYieldRate`
   - `Category=Prestige`, `PrestigeGateRequirement=1` (prestige çarpanı bonusları)
5. **Economy Simulator** — 30 oturum simüle et:
   - İlk prestige 5-8. oturumda gelmeli
   - `PrestigeConfig.BaseMultiplierPerPrestige = 1.5`
   - `PrestigeConfig.MinGoldToPrestige` = prestige anındaki gold'un %80-90'ı
6. Simülatörde dengeli görünene kadar generator yield ve prestige değerlerini ayarla

**İçerik hacmi (yayın için minimum):**
- 10+ generator
- 50+ upgrade node
- 20+ prestige simüle edilmiş (simülatörde test et)

---

### SENARYO B — "Click Loop" (Aktif Idle)

**Hedef:** Clicker Heroes / Tap Titans tarzı

**Adımlar:**
1. New Game Wizard → Click Loop → Generate
2. **3 ClickTargetConfigSO oluştur** (farklı HP/yield/renk):
   - `Target_Rock`: MaxHP=5, BaseYield=1, RespawnSeconds=2
   - `Target_Tree`: MaxHP=15, BaseYield=5, RespawnSeconds=4
   - `Target_Crystal`: MaxHP=30, BaseYield=15, RespawnSeconds=8
3. **ClickLoopConfigSO düzenle:**
   - `BaseCritChance = 0.05`
   - `BaseAutoClickRate = 0` (başta 0, upgrade ile açılır)
   - `MaxComboMultiplier = 5.0`
4. **Generator Database** — 5-8 pasif generator ekle (aktif tıklama yanında pasif gelir)
5. **Upgrade Tree** — hem Click hem Production node'ları:
   - `AffectedStat=ClickDamage`, `EffectPerRank=0.10` (her rankta %10 hasar)
   - `AffectedStat=ClickCritChance`, `EffectPerRank=0.02`
   - `AffectedStat=ClickAutoRate`, `EffectPerRank=0.5`
   - `AffectedStat=GeneratorYield`, `EffectPerRank=0.15`
6. **Economy Simulator** ile test et

**İçerik hacmi:**
- 3-5 ClickTarget (farklı HP/renk/yield)
- 8+ generator
- 40+ upgrade node (click + pasif karışık)

---

### SENARYO C — "Wave RPG" (Idle-vs / RPG)

**Hedef:** Idle Heroes / AFK Arena tarzı

**Adımlar:**
1. New Game Wizard → Idle-vs / RPG → Generate
2. **WaveConfigSO düzenle:**
   - `BaseEnemyCountPerWave = 3`
   - `EnemyCountScalingFactor = 1.2`
   - `UpgradeSelectionWaveInterval = 5`
3. **EnemyStatConfigSO düzenle:**
   - `BaseMaxHP = 20`
   - `BaseAttackDamage = 5`
   - `WaveScalingExponent = 1.5`
4. **PrestigeConfigSO:**
   - `MinWaveForPrestige = 10`
   - `BaseMultiplierPerPrestige = 2.0`
5. **Upgrade Tree** — savaş odaklı:
   - `AffectedStat=Damage`, `EffectPerRank=0.10`
   - `AffectedStat=Health`, `EffectPerRank=0.15`
   - `AffectedStat=CritChance`, `EffectPerRank=0.02`
   - `AffectedStat=Armor`, `EffectPerRank=0.05`
6. **Economy Simulator** — wave parametrelerini de bağla

**İçerik hacmi:**
- 5+ generator (pasif gelir, dalga arasında satın alma için)
- 40+ upgrade node (savaş ağırlıklı)
- 50+ wave hedefi (simülatörle test et)

---

### SENARYO D — "Merge Idle"

**Hedef:** Merge Dragons / Merge Mansion tarzı

**Adımlar:**
1. New Game Wizard → Merge Idle → Generate
2. **Merge grubu tasarla** — örnek: "coins" grubu için 5 tier:
   - `ItemConfigSO`: ItemId=`coin_t1`, MergeGroupId=`coins`, MergeTier=1, DisplayName="Bronz Para"
   - `ItemConfigSO`: ItemId=`coin_t2`, MergeGroupId=`coins`, MergeTier=2, DisplayName="Gümüş Para"
   - `ItemConfigSO`: ItemId=`coin_t3`, MergeGroupId=`coins`, MergeTier=3, DisplayName="Altın Para"
   - `ItemConfigSO`: ItemId=`coin_t4`, MergeGroupId=`coins`, MergeTier=4, DisplayName="Platin Para"
   - `ItemConfigSO`: ItemId=`coin_t5`, MergeGroupId=`coins`, MergeTier=5, DisplayName="Elmas"
3. **MergeConfigSO oluştur** (`MergeConfig_Coins.asset`):
   - `MergeGroupId = "coins"`
   - `Rules` listesi:
     - Rule: InputTier=1, ResultItem=coin_t2, GoldBonus=0
     - Rule: InputTier=2, ResultItem=coin_t3, GoldBonus=5
     - Rule: InputTier=3, ResultItem=coin_t4, GoldBonus=20
     - Rule: InputTier=4, ResultItem=coin_t5, GoldBonus=100
4. Bu MergeConfigSO'yu `MergeService` component'ının Inspector'ına ekle
5. İstersen 2-3 farklı merge grubu (coins, gems, items) ile çeşitlendirme yap

**İçerik hacmi:**
- 3-5 merge grubu
- Her grup için 5-8 tier
- Her tier için ayrı ItemConfigSO

---

### SENARYO E — "Research Idle"

**Hedef:** Universal Paperclips / Kittens Game tarzı

**Adımlar:**
1. New Game Wizard → Research Idle → Generate
2. **ResearchTreeConfigSO** oluştur veya wizard'ınkini düzenle
3. **20-30 ResearchNodeConfigSO** oluştur:
   - Tier 0 node'lar (root): ResearchTicks=60, GoldCost=500
   - Tier 1 node'lar: ResearchTicks=300, GoldCost=5000
   - Tier 2 node'lar: ResearchTicks=1800, GoldCost=50000 (30 dakika)
   - Tier 3 node'lar: ResearchTicks=7200, GoldCost=500000 (2 saat)
4. Her node'un `Effects` listesine bir `SkillEffect` ekle (stat bonusu veya unlock)
5. Gerekli `PrerequisiteIds`'leri tanımla (zincir oluştur)
6. `ResearchTreeConfigSO.Nodes` array'ine hepsini ekle
7. Generator gelirini araştırma unlock'larına bağlamak için:
   ```csharp
   ResearchService.OnResearchCompleted += (treeId, nodeId) =>
   {
       if (nodeId == "unlock_generator_2")
           generatorService.UnlockGenerator("factory");
   };
   ```

---

### SENARYO F — "Prestige-Heavy"

**Hedef:** Antimatter Dimensions / Realm Grinder tarzı

**Adımlar:**
1. New Game Wizard → Prestige-Heavy → Generate
2. **AscensionDatabaseSO** düzenle — 3 katman oluştur:
   - **Katman 0** (`PrestigeLayerConfigSO`): DisplayName="Prestige", LayerIndex=0, BaseMultiplierPerTrigger=1.5, RequiredPreviousLayerCount=0
   - **Katman 1** (`PrestigeLayerConfigSO`): DisplayName="Ascension", LayerIndex=1, BaseMultiplierPerTrigger=3.0, RequiredPreviousLayerCount=10 (10 prestige gerekir)
   - **Katman 2** (`PrestigeLayerConfigSO`): DisplayName="Transcension", LayerIndex=2, BaseMultiplierPerTrigger=10.0, RequiredPreviousLayerCount=5 (5 ascension gerekir)
3. Her katman için farklı `ResetScope` tanımla
4. Her katman için ayrı **SkillTree** veya **UpgradeTree** oluştur (katmana özgü kalıcı bonuslar)
5. **Economy Simulator'de 100 oturum simüle et** — çok katmanlı prestige'de denge çok önemli

---

## GENEL İPUÇLARI

### "Oyun çok hızlı bitiyor" sorunu
→ `GeneratorConfigSO.CostScalingFactor` artır (1.15 → 1.20)
→ `PrestigeConfigSO.MinGoldToPrestige` yükselt
→ Daha fazla upgrade node ekle (ilerlemeyi yavaşlatır)

### "Oyun çok sıkıcı / çok yavaş" sorunu
→ İlk generator maliyetini düşür
→ `PrestigeConfigSO.MinGoldToPrestige` değerini düşür
→ `EconomyConfigSO.OfflineCapHours` değerini artır (daha fazla geri dönüş ödülü)

### "Ekonomi patlıyor, hard cap'e çarpıyor" sorunu
→ `EconomyConfigSO.ResourceHardCap` değerini 10x artır
→ `EconomyConfigSO.NumberBackend = BigDouble` yap (aşırı büyük sayılar için)

### "Config değiştirdim ama oyunda görünmüyor" sorunu
→ Play modundayken yapılan config değişiklikleri Play durduğunda kaybolur
→ Config'i Play modundan ÖNCE değiştir

### "ID çakışması uyarısı alıyorum"
→ `Tools → Endless Engine → ID Registry` aç → çakışan ID'yi bul → düzelt

### "Yeni sahne/içerik ekledim, kayıt çalışmıyor"
→ `Tools → Endless Engine → Schema Bump Utility` çalıştır
→ Migration dosyasını doldur
→ `ISaveStateProvider` implemente et ve `RegisterStateProvider` çağır

### "BigDouble ne zaman kullanmalıyım?"
→ Oyununda altın değerleri 1 katrilyon (1e15) üzerini geçecekse
→ `EconomyConfigSO.NumberBackend = BigDouble` yap
→ `GeneratorSystem.GetNextCostBig(generatorId)` metodunu kullan (long yerine IBigNumber döner)

---

## STEAM YAYINI ÖNCESİ KONTROL LİSTESİ

- [ ] Economy Simulator'de 50+ oturum simüle edildi, denge sağlandı
- [ ] Config Validator'da sıfır hata (`Tools → Endless Engine → Config Validator`)
- [ ] ID Registry'de sıfır çakışma (`Tools → Endless Engine → ID Registry`)
- [ ] Tüm oyun tipleri Play'e basınca çalışıyor
- [ ] Kayıt/yükleme test edildi (Play → Stop → Play → veri kaldı mı?)
- [ ] Offline gelir test edildi (zaman at, geri dön, altın geldi mi?)
- [ ] Prestige crash-safety test edildi (prestige sırasında Play durdur, geri dönünce rollback oldu mu?)
- [ ] `SteamAchievementBridge._mappings` listesi dolduruldu (milestone ID → Steam API adı)
- [ ] `SteamLeaderboardService.SubmitScore(boardId, playerName, score)` test edildi
- [ ] `SteamCloudSaveSync.CheckForNewerCloudSave()` oyun başlangıcında çağrılıyor
- [ ] Build Settings'te doğru sahne var
- [ ] Tüm gereksiz debug log'ları kapatıldı (Development Build only)

---

*Endless Engine v1.3.4 — Oyun Üretim Rehberi (kaynak koddan doğrulanmış)*
