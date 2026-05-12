# Endless Engine — Tam Oyun Yapım Rehberi

> **Bu tek rehber yeterlidir.**  
> Kurulumdan Steam'e, basit Pure Idle'dan karmaşık Prestige + Research + Wave kombinasyonlarına kadar her şey burada.  
> Unity temellerini biliyorsunuz (sahne, GameObject, Component, Inspector, Prefab).  
> **Kod yazmadan her türlü idle/incremental oyun yapabilirsiniz** — tüm sistemler pakette hazır.

---

## İçindekiler

### Bölüm A — Kurulum ve İlk Adımlar
1. [Paket Kurulumu](#1-paket-kurulumu)
2. [New Game Wizard ile Proje Oluştur](#2-new-game-wizard-ile-proje-oluştur)
3. [Oluşturulan Sahneyi Anlamak](#3-oluşturulan-sahneyi-anlamak)
4. [Hızlı Test — İlk 5 Dakika](#4-hızlı-test--ilk-5-dakika)

### Bölüm B — Config (Oyun Parametreleri)
5. [EconomyConfig — Temel Ekonomi](#5-economyconfig--temel-ekonomi)
6. [Generator Sistemi](#6-generator-sistemi)
7. [ClickSourceConfig — Tıklama Mekaniği](#7-clicksourceconfig--tıklama-mekaniği)
8. [ClickLoopConfig — HP'li Target Sistemi](#8-clickloopconfig--hpli-target-sistemi)
9. [HarvestAreaConfig — Cursor Hasat Sistemi](#9-harvestareconfig--cursor-hasat-sistemi)
10. [WaveConfig — Dalga / Savaş Sistemi](#10-waveconfig--dalga--savaş-sistemi)
11. [PrestigeConfig — Prestige Sistemi](#11-prestigeconfig--prestige-sistemi)
12. [ResearchTreeConfig — Araştırma Ağacı](#12-researchtreeconfig--araştırma-ağacı)
13. [BuildingConfig — Bina Sistemi](#13-buildingconfig--bina-sistemi)
14. [MergeConfig — Birleştirme Mekaniği](#14-mergeconfig--birleştirme-mekaniği)

### Bölüm C — Upgrade Tree
15. [Upgrade Tree — Tam Rehber](#15-upgrade-tree--tam-rehber)

### Bölüm D — UI Ekranları
16. [Sahne UI Yapısı](#16-sahne-ui-yapısı)
17. [HUD Ekranı](#17-hud-ekranı)
18. [Generator Ekranı](#18-generator-ekranı)
19. [Upgrade Ekranı](#19-upgrade-ekranı)
20. [Prestige Overlay](#20-prestige-overlay)
21. [Research Ekranı](#21-research-ekranı)
22. [Building Ekranı](#22-building-ekranı)
23. [Ana Menü (MainMenuController)](#23-ana-menü-mainmenucontroller)
24. [UI Ekranları Arası Geçiş Nasıl Çalışır?](#24-ui-ekranları-arası-geçiş-nasıl-çalışır)

### Bölüm E — Oyun Türü Rehberleri
25. [Pure Idle — Adım Adım Bitirme Rehberi](#25-pure-idle--adım-adım-bitirme-rehberi)
26. [Clicker Idle](#26-clicker-idle)
27. [Click Loop (HP'li Hedef)](#27-click-loop-hpli-hedef)
28. [Harvest Idle](#28-harvest-idle)
29. [Idle-vs / RPG (Wave Savaş)](#29-idle-vs--rpg-wave-savaş)
30. [Tower Defense Idle](#30-tower-defense-idle)
31. [Merge Idle](#31-merge-idle)
32. [Farm Idle](#32-farm-idle)
33. [Research Idle](#33-research-idle)
34. [Building Idle](#34-building-idle)
35. [Prestige-Heavy (Çok Katmanlı)](#35-prestige-heavy-çok-katmanlı)
36. [Sistemleri Birleştirmek (Custom / Hibrit Oyunlar)](#36-sistemleri-birleştirmek-custom--hibrit-oyunlar)

### Bölüm F — İleri Seviye
37. [Ses Sistemi (AudioService)](#37-ses-sistemi-audioservice)
38. [Kayıt / Yükleme Sistemi](#38-kayıt--yükleme-sistemi)
39. [Görsel Özelleştirme — Sprite ve Prefab](#39-görsel-özelleştirme--sprite-ve-prefab)
40. [Ana Menü Sahnesi Oluşturma](#40-ana-menü-sahnesi-oluşturma)
41. [Build Alma](#41-build-alma)
42. [Steam SDK Entegrasyonu](#42-steam-sdk-entegrasyonu)

### Bölüm G — Troubleshooting
43. [Troubleshooting — Tüm Hatalar ve Çözümleri](#43-troubleshooting--tüm-hatalar-ve-çözümleri)

---

# BÖLÜM A — KURULUM VE İLK ADIMLAR

---

## 1. Paket Kurulumu

Bu repo, paketi zaten yerel olarak içerir. Mevcut projeyi kullanıyorsanız bu bölümü atlayın.

### Yeni Unity projesine kurmak

**Yöntem A — Git URL (Tavsiye)**

1. Unity: **Window → Package Manager**
2. Sol üst **+** → **Add package from git URL...**
3. Yapıştırın:
   ```
   https://github.com/agobrik/endless-engine.git?path=Packages/com.endlessengine.idle
   ```
4. **Add**

**Yöntem B — Diskten**

1. Repo'yu indirin/çıkarın
2. **Window → Package Manager → + → Add package from disk...**
3. `com.endlessengine.idle/package.json` seçin

### Bağımlılıklar (otomatik yüklenir)

| Paket | Sürüm |
|---|---|
| Unity Input System | 1.14.2 |
| Addressables | 2.7.6 |
| Unity UI (uGUI) | 2.0.0 |
| Newtonsoft JSON | 3.2.2 |

**TextMeshPro:** İlk kurulumda `Window → TextMeshPro → Import TMP Essential Resources → Import`

---

## 2. New Game Wizard ile Proje Oluştur

**Tools → Endless Engine → New Game Wizard**

Bu tek adımda tüm iskelet oluşur: sahne, config asset'leri, UI ekranları, upgrade tree.

### Adımlar

| Adım | Açıklama |
|---|---|
| **Game Name** | Proje adı — klasör ve sahne adı olur (örn. `GoldRushEmpire`) |
| **Game Type** | Aşağıdan seçin |
| **Modules** | Otomatik seçilir, gerekirse değiştirin |
| **Generate Skeleton** | Butona basın |
| Sahneyi açın | `Assets/GoldRushEmpire/Scenes/GoldRushEmpire.unity` |
| **Play** | Oyun çalışır |

### Game Type Tablosu

| Tür | Ne Üretir | Referans Oyunlar |
|---|---|---|
| **Pure Idle** | Generator + Prestige | AdCap, Cookie Clicker |
| **Clicker Idle** | Tıklama hedefi + Generator | Clicker Heroes erken evhre |
| **Click Loop** | HP'li targetlar, combo, crit, respawn | Tap Titans |
| **Harvest Idle** | Cursor drag, node'lar, combo | — |
| **Idle-vs / RPG** | Otomatik savaş + Wave + Generator | Swipe Brick Breaker |
| **Tower Defense** | Path + Tower slotlar + Wave | Kingdom Rush Idle |
| **Merge Idle** | 3×3 board, tier birleştirme | Merge Dragons |
| **Farm Idle** | Grid farm plotlar + Building | Hay Day Idle |
| **Research Idle** | Generator + Zaman-kilitli tech tree | — |
| **Building Idle** | Grid bina sistemi, her bina gelir üretir | Idle City Builder |
| **Prestige-Heavy** | Tüm prestige katmanları + multi-currency | NGU Idle |
| **Custom** | Her şey kapalı, siz açın | — |

### Wizard Ne Oluşturur?

```
Assets/GoldRushEmpire/
  Configs/
    EconomyConfig.asset          ← Temel ekonomi (oyun türüne göre preset)
    SchemaVersion.asset          ← Save şeması
    GeneratorDatabase.asset      ← Generator listesi
    GoldMine.asset               ← İlk generator
    PrestigeConfig.asset         ← Prestige ayarları
    UpgradeTreeConfig.asset      ← Upgrade tree (4 starter node)
    Upgrades/                    ← UpgradeNodeConfigSO dosyaları
    [Modüle göre ek config'ler]
  Scenes/
    GoldRushEmpire.unity
```

---

## 3. Oluşturulan Sahneyi Anlamak

Sahneyi açın, Hierarchy'e bakın:

```
Main Camera
EventSystem
Bootstrap
  ├─ AutoSetupBootstrap        ← Core: Economy, Generator, UpgradeTree, Save, Tick
  ├─ [Modül Bootstrap'ları]    ← WaveCombatBootstrap, ClickLoopBootstrap, vs.
  └─ [Servis GO'ları]          ← BuildingService, ResearchService, vs.
Canvas                         ← UGUI HUD (her zaman görünür, kod gerektirmez)
  └─ HUDPanel
       ├─ GoldLabel
       ├─ IncomeLabel
       ├─ BuyGeneratorButton
       └─ PrestigeButton
Screen_HUD                     ← UIToolkit HUD (HUD.uxml)
Screen_Generator               ← Generator ekranı (GeneratorScreen.uxml)
Screen_Upgrades                ← Upgrade tree (UpgradeScreen.uxml)
Screen_Prestige                ← Prestige overlay (PrestigeOverlay.uxml)
[Screen_Research]              ← Research türünde eklenir
[Screen_Building]              ← Building/Farm türünde eklenir
[Oyun dünyası nesneleri]       ← ClickTarget'lar, HarvestNode'lar, vs.
```

### Bootstrap Inspector'ı — Ne Kontrol Edin

`Bootstrap` GO'yu seçin → **AutoSetupBootstrap**:

| Alan | Beklenen Değer |
|---|---|
| Economy Config | `Configs/EconomyConfig.asset` ✓ |
| Generator Database | `Configs/GeneratorDatabase.asset` ✓ |
| Schema Version | `Configs/SchemaVersion.asset` ✓ |
| Prestige Config | `Configs/PrestigeConfig.asset` ✓ |
| Upgrade Node Configs | `Upgrades/` klasöründeki asset'ler (4 adet) ✓ |
| Enable Save | ✓ işaretli |

Herhangi biri boşsa → o asset'i sürükleyip bırakın.

---

## 4. Hızlı Test — İlk 5 Dakika

Play'e basın. Şunları görmelisiniz:

| Beklenen | Görmüyorsanız |
|---|---|
| Gold sayacı artıyor | Generator bağlı değil → [Bkz. §43](#43-troubleshooting--tüm-hatalar-ve-çözümleri) |
| "Buy Gold Mine" butonu çalışıyor | EconomyConfig yok → [Bkz. §43](#43-troubleshooting--tüm-hatalar-ve-çözümleri) |
| Konsolda `[AutoSetupBootstrap] Ready.` | Config hatası var → Konsola bakın |
| Prestige butonu görünüyor | PrestigeConfig atanmamış |

---

# BÖLÜM B — CONFIG (OYUN PARAMETRELERİ)

---

## 5. EconomyConfig — Temel Ekonomi

`Assets/[OyunAdı]/Configs/EconomyConfig.asset` seçin:

| Parametre | Açıklama | Tavsiye Değer |
|---|---|---|
| **Idle Yield Rate Base** | Pasif gelir temel çarpanı | Pure Idle: 0.5 / Wave: 0.2 / Harvest: 0 |
| **Base Multiplier Per Prestige** | Her prestige'de gelir çarpanı | 1.5 (standart) / 3.0 (Prestige-Heavy) |
| **Idle Yield Multiplier Cap** | Prestige çarpan tavanı | 100–1000 |
| **Starting Gold** | Oyun başındaki altın | 0 (çoğu oyun) / 50 (tutorial için) |
| **Resource Hard Cap** | Maximum altın | 1B (basit) / 1T (orta) / 1Q (deep idle) |
| **Number Backend** | Sayı tipi | DoubleNumber (hız) / BigDouble (∞ değerler) |
| **Offline Cap Hours** | Offline kazanç kaç saat sayılır | 8–24 |
| **Active Run State Offline Modifier** | Offline sırasında aktif run modifiyeri | 0.5 |
| **Base Gold Drop Per Enemy** | Wave oyunlarında düşman başı gold | 1–5 |
| **Gold Drop Scaling Exponent** | Wave arttıkça gold artış eğrisi | 1.1–1.3 |

---

## 6. Generator Sistemi

### Mevcut Generator'u Düzenle

`Configs/GoldMine.asset` seçin:

| Parametre | Açıklama |
|---|---|
| **Generator Id** | Benzersiz sabit ID — değiştirmeyin (save key!) |
| **Display Name** | UI'da görünen isim |
| **Description** | Kısa açıklama |
| **Base Yield Per Second** | 1 copy'nin saniyede ürettiği altın |
| **Base Cost** | İlk copy maliyeti |
| **Cost Scaling Factor** | Her copy'de maliyet kaç katı artar (1.15 = %15 artış) |
| **Max Count** | Maksimum satın alınabilir kopya (-1 = sınırsız) |
| **Unlock Prerequisite** | Açılmak için hangi generator olmalı |

### Yeni Generator Ekle

1. `Assets/[OyunAdı]/Configs/` klasörüne **sağ tıklayın**
2. **Create → Endless Engine → Config → Generator Config**
3. Tüm alanları doldurun (özellikle `Generator Id` benzersiz olsun)
4. `Configs/GeneratorDatabase.asset` seçin
5. Inspector'da **Generators** listesi görünür → yeni asset'i listeye **sürükleyip bırakın**

### Çok Generator — Dengeli Maliyet Örneği

| Generator | Base Yield/s | Base Cost | Cost Scale |
|---|---|---|---|
| Gold Mine | 0.5 | 10 | 1.15 |
| Silver Mill | 3 | 120 | 1.15 |
| Crystal Cave | 20 | 1,300 | 1.15 |
| Dragon Nest | 150 | 15,000 | 1.15 |
| Void Rift | 1,200 | 200,000 | 1.15 |

Kural: Her generator öncekinin ~8–10×'i kadar verimli, ~10–15×'i kadar pahalı.

---

## 7. ClickSourceConfig — Tıklama Mekaniği

> Basit clicker için (`_modClick = true`). HP'li target sistemi için §8'e bakın.

`Configs/ClickSourceConfig.asset` seçin:

| Parametre | Açıklama | Tavsiye |
|---|---|---|
| **Gold Per Click** | Tıklama başına temel gold | 10 |
| **Yield Rate Click Fraction** | Click yield = pasif gelirin bu oranı | 0 (bağımsız) |
| **Enable Combo** | Hızlı tıklamada combo çarpanı | ✓ |
| **Combo Window Seconds** | Combo zinciri kopmaması için süre | 0.8 |
| **Max Combo Multiplier** | Combo tavanı | 5× |
| **Combo Multiplier Step** | Her tıklamada combo artışı | 0.1 |
| **Base Auto Clicks Per Second** | Otomatik tıklama hızı (0 = kapalı) | 0 |
| **Crit Chance** | Kritik isabet olasılığı (0–1) | 0.05 |
| **Crit Multiplier** | Kritik hasarda çarpan | 3× |
| **Max Clicks Per Second Cap** | Anti-hile tıklama sınırı | 20 |

---

## 8. ClickLoopConfig — HP'li Target Sistemi

> Click Loop modülü: hedeflerin HP'si var, yok edilince gold düşer, respawn eder.

`Configs/ClickLoopConfig.asset` seçin:

| Parametre | Açıklama |
|---|---|
| **Auto Click Rate** | Otomatik tıklama/saniye (0 = sadece elle) |
| **Combo Decay Delay** | Tıklama durduğunda combo kaç saniyede sıfırlanır |
| **Max Combo Multiplier** | Combo tavanı |
| **Offline Cap Hours** | Offline otomatik tıklama kaç saat sayılır |

`Configs/ClickTarget_0/1/2.asset` her target için:

| Parametre | Açıklama |
|---|---|
| **Target Id** | Benzersiz ID |
| **Display Name** | Ekran adı |
| **Max HP** | Başlangıç HP |
| **Base Yield** | Yok edilince düşen gold |
| **Respawn Seconds** | Yok edildikten kaç saniye sonra geri gelir |

### Yeni ClickTarget Asset Ekle

1. `Configs/` → **Create → Endless Engine → Config → Click Target Config**
2. Doldurun
3. Sahneye `ClickTarget` prefab'ı sürükleyin (`Packages/.../Runtime/Prefabs/ClickLoop/`)
4. O GameObject'i seçin → **ClickTarget** component → `_config` alanına yeni asset'i sürükleyin

---

## 9. HarvestAreaConfig — Cursor Hasat Sistemi

`Configs/HarvestAreaConfig.asset` seçin:

| Parametre | Açıklama | Tavsiye |
|---|---|---|
| **Base Radius** | Cursor'ın dünyada kaç birim etki alanı | 1.5 |
| **Base Tick Interval** | Hasat tick'i kaç saniyede bir | 0.25 |
| **Combo Decay Delay** | Hasat durduğunda combo düşmeden önce bekleme | 2.0 |
| **Combo Decay Rate** | Combo düşüş hızı (puan/saniye) | 5.0 |
| **Max Combo Multiplier** | Combo tavanı | 5× |
| **Combo Points Per Multiplier Step** | Her 1× artış için gereken combo puanı | 10 |
| **Offline Cap Hours** | Offline hasat kaç saat | 8 |
| **Offline Efficiency** | Offline hasat verimliliği (0–1) | 0.3 |

`Configs/HarvestNode.asset`:

| Parametre | Açıklama |
|---|---|
| **Node Id** | Benzersiz ID |
| **Max HP** | Node'un başlangıç HP'si |
| **Damage Per Tick** | Her tick'te azalan HP |
| **Base Yield** | Yok edilince kazanılan gold |
| **Respawn Seconds** | Respawn süresi |
| **Award Yield Per Tick** | ✓ = her tick gold düşer, ✗ = sadece yok edilince |

---

## 10. WaveConfig — Dalga / Savaş Sistemi

`Configs/WaveConfig.asset`:

| Parametre | Açıklama | Tavsiye |
|---|---|---|
| **Total Waves Per Run** | Toplam wave sayısı (-1 = sonsuz) | 30–50 |
| **Base Enemy Count Per Wave** | Wave 1'deki düşman sayısı | 3–8 |
| **Enemy Count Scaling Factor** | Her wave'de düşman artış çarpanı | 1.10–1.15 |
| **Hard Cap Enemies On Screen** | Aynı anda maksimum aktif düşman | 30–50 |
| **Spawn Interval Seconds** | Düşmanlar arası spawn gecikmesi | 0.5 |
| **Wave Transition Delay Seconds** | Wave'ler arası bekleme süresi | 1.5 |
| **Wave Duration Seconds** | Wave zorla kapatma timeout'u | 120 |
| **Upgrade Selection Wave Interval** | Kaç wave'de bir upgrade seçim ekranı | 3–5 |
| **Wave Save Milestone Interval** | Kaç wave'de bir kayıt | 10 |
| **Elite Wave Interval** | Kaç wave'de bir elite düşman | 5 |
| **Elite Stat Multiplier** | Elite düşmanın stat çarpanı | 3× |
| **Boss Wave Interval** | Kaç wave'de bir boss | 20 |

`Configs/EnemyStatConfig.asset`:

| Parametre | Açıklama |
|---|---|
| **Base HP** | Wave 1 düşman HP |
| **HP Scaling Exponent** | Wave başına HP artış eğrisi |
| **Base Damage** | Düşman hasarı |
| **Move Speed** | Hareket hızı |
| **Gold Drop Base** | Temel gold drop (EconomyConfig ile çarpılır) |

---

## 11. PrestigeConfig — Prestige Sistemi

`Configs/PrestigeConfig.asset`:

| Parametre | Açıklama | Tavsiye |
|---|---|---|
| **Base Multiplier Per Prestige** | Her prestige'de kalıcı gelir çarpanı | 1.5 (hafif) / 3.0 (ağır) |
| **Max Permanent Multiplier** | Tüm prestige'lerdeki maksimum çarpan | 200–10,000 |
| **Min Gold To Prestige** | Wave olmayan oyunlarda gereken minimum altın | Geç oyun hedefi |
| **Min Wave For Prestige** | Wave oyunlarında gereken minimum wave | 10–20 |
| **Reset Generators On Prestige** | Prestige'de generator'lar sıfırlansın mı? | ✓ (standart) |
| **Reset Upgrades On Prestige** | Upgradeler sıfırlansın mı? | Oyun tasarımına göre |

---

## 12. ResearchTreeConfig — Araştırma Ağacı

`Configs/ResearchDatabase.asset`:

| Parametre | Açıklama |
|---|---|
| **Tree Id** | Benzersiz ID |
| **Display Name** | Ekranda görünen isim |
| **Nodes** | ResearchNodeConfigSO array — her araştırma bir node |

### ResearchNodeConfigSO Alanları

1. `Configs/` → **Create → Endless Engine → Config → Research Node Config**

| Alan | Açıklama |
|---|---|
| **Node Id** | Benzersiz ID (save key — değiştirmeyin) |
| **Display Name** | Araştırma adı |
| **Description** | Kısa açıklama |
| **Research Duration Seconds** | Kaç saniyede tamamlanır |
| **Gold Cost** | Araştırma başlatma maliyeti |
| **Prerequisite Node IDs** | Önce hangi araştırmalar bitmeli |
| **Stat Bonuses** | Bu araştırmanın verdiği stat bonusları |

2. Oluşturduğunuz node'u `ResearchDatabase.asset` → **Nodes** listesine ekleyin

---

## 13. BuildingConfig — Bina Sistemi

`Configs/StarterBuilding.asset`:

| Parametre | Açıklama |
|---|---|
| **Building Id** | Benzersiz ID |
| **Display Name** | Bina adı |
| **Base Income Per Tick** | Tick başına gelir |
| **Base Cost** | İlk yerleştirme maliyeti |
| **Max Level** | Kaç kez yükseltilebilir |
| **Upgrade Cost Multiplier** | Her seviyede maliyet çarpanı |

### Yeni Bina Türü Ekle

1. `Configs/` → **Create → Endless Engine → Config → Building Config**
2. Doldurun
3. `Screen_Building` → **BuildingScreenController** → `_allBuildings` listesine ekleyin

---

## 14. MergeConfig — Birleştirme Mekaniği

`Configs/StarterMergeConfig.asset`:

| Parametre | Açıklama |
|---|---|
| **Max Tier** | Birleştirilebilecek maksimum tier |
| **Base Sell Value** | T1 item'ın satış değeri |
| **Sell Value Multiplier Per Tier** | Her tier'da satış değeri artışı |
| **Board Width / Height** | Merge board boyutları |
| **Starting Items** | Oyun başında board'da olan item'lar |

---

# BÖLÜM C — UPGRADE TREE

---

## 15. Upgrade Tree — Tam Rehber

Endless Engine'de iki paralel upgrade sistemi vardır:

| Sistem | Dosya | Kullanım Yeri |
|---|---|---|
| **UpgradeTreeConfigSO** | `Configs/UpgradeTreeConfig.asset` | UpgradeScreenController (UI görselleştirme) |
| **UpgradeNodeConfigSO[]** | `Configs/Upgrades/*.asset` | UpgradeTreeService (oyun mantığı + kayıt) |

Her ikisini de senkronize tutmanız gerekir.

### UpgradeTreeConfig — Node Eklemek

`Configs/UpgradeTreeConfig.asset` seçin → Inspector'da **Nodes** listesi görünür.

**+** butonuna basın, yeni node alanları:

| Alan | Açıklama | Örnek |
|---|---|---|
| **Node Id** | Benzersiz sabit ID. **Hiç değiştirmeyin** — save key | `yield_boost_3` |
| **Display Name** | Ekranda görünen isim | `Yield Boost III` |
| **Description** | Tooltip açıklaması | `Increases generator output by 25% per rank.` |
| **Category** | Upgrade ekranındaki tab | Production / Combat / Economy / Prestige |
| **Affected Stat** | Hangi stat etkilenir | `GeneratorSpeed` |
| **Effect Per Rank** | Rank başına etki (0.25 = %25) | `0.25` |
| **Effect Type** | Etki biçimi | `PercentBonus` (çoğunluk) / `FlatBonus` |
| **Max Rank** | Kaç kez alınabilir | 5 |
| **Base Cost** | Rank 0 maliyeti | 500 |
| **Cost Scaling Factor** | Her rank'ta maliyet çarpanı | 1.5 |
| **Prerequisite Node IDs** | Önce alınması gereken node ID'leri | `yield_boost_2` |
| **Prestige Gate Requirement** | Kaç prestige'den sonra açılır | 0 (her zaman açık) |
| **Grid X / Grid Y** | Upgrade ekranındaki kolon/satır | 0, 0 |
| **Selection Weight** | Upgrade kart seçim havuzundaki ağırlık | 10 |

### UpgradeNodeConfigSO — Oyun Mantığı İçin

Yukarıdaki her node için eşleşen bir `.asset` dosyası da olmalı:

1. `Configs/Upgrades/` → **Create → Endless Engine → Config → Upgrade Node**
2. `Node Id`'yi UpgradeTreeConfig'dekiyle **birebir aynı** yazın
3. Diğer alanları aynı değerlerle doldurun
4. `Bootstrap` → **AutoSetupBootstrap** → `_upgradeNodeConfigs` listesine ekleyin

### Mevcut Stat Tipleri (AffectedStat)

**Üretim:**
`IdleYieldRate` · `GeneratorSpeed` · `OfflineYieldRate` · `ActiveRunPassiveBonus`

**Savaş:**
`Damage` · `AttackInterval` · `AttackRange` · `CritChance` · `CritMultiplier` · `AreaDamage`

**Hayatta Kalma:**
`MaxHP` · `MoveSpeed` · `DamageReduction` · `HPRegen`

**Ekonomi:**
`GoldDropMultiplier` · `GoldPickupRange` · `BonusRunReward` · `ComboMultiplier`

**Prestige:**
`PrestigeMultiplier` · `StartingGoldBonus` · `RunDurationBonus` · `DoubleGeneratorChance`

**Hasat:**
`HarvestRadius` · `HarvestTickRate` · `HarvestYieldMultiplier` · `HarvestNodeMaxHP`
`HarvestNodeRespawnRate` · `HarvestComboMultiplier` · `HarvestComboDecayRate` · `HarvestMultiNodeBonus`

**Click Loop:**
`ClickDamage` · `ClickTargetMaxHP` · `ClickTargetRespawnRate` · `ClickYieldMultiplier`
`ClickComboMultiplier` · `ClickComboDecayRate` · `ClickCritChance` · `ClickCritMultiplier` · `ClickAutoRate`

### Upgrade Tree Örnek Yapısı (Pure Idle)

```
[Production Tab]
Sütun 0: yield_1 (Yield Boost I) → yield_2 (II) → yield_3 (III)
Sütun 1: offline_1 (Offline Bonus I) → offline_2 (II)

[Prestige Tab]
Sütun 0: prestige_multi_1 (Prestige Edge I) → prestige_multi_2 (II)
  [prestige_multi_1 prereq: yield_3, prestige gate: 1]
```

---

# BÖLÜM D — UI EKRANLARI

---

## 16. Sahne UI Yapısı

Wizard iki paralel UI sistemi oluşturur:

| Sistem | Nerede | Avantaj |
|---|---|---|
| **UGUI Canvas** | `Canvas/HUDPanel` | Her Unity projesiyle uyumlu |
| **UIToolkit** | `Screen_*` GameObject'leri | Daha modern, CSS-like stil |

İkisi aynı anda çalışır. Birini kaldırıp diğerini kullanabilirsiniz.

---

## 17. HUD Ekranı

### UGUI HUD (Canvas/HUDPanel)

**GeneratedGameHUD** component'i **otomatik** çalışır — özel bir şey yapmanıza gerek yok.  
Bootstrap'tan servisleri bulur ve label'ları günceller.

Özelleştirmek için: Hierarchy'de `HUDPanel` altındaki GameObject'leri seçin, `Text` (veya TMP) component'ini düzenleyin.

### UIToolkit HUD (Screen_HUD)

`Screen_HUD` seçin → **UIDocument** component:
- **Visual Tree Asset**: `Assets/UI/HUD/HUD.uxml` ✓ (wizard bağladı)
- **Panel Settings**: Boşsa → **Create → UI Toolkit → Panel Settings** oluşturun ve atayın

HUD görselini değiştirmek: `Assets/UI/HUD/HUD.uxml` → çift tıklayın → **UI Builder** açılır.

---

## 18. Generator Ekranı

`Screen_Generator` seçin:
- **UIDocument** → `Assets/UI/Generator/GeneratorScreen.uxml`
- **GeneratorScreenController** → `_generatorSystem` alanı dolu olmalı

**Bu ekran nasıl açılır?** [Bkz. §24](#24-ui-ekranları-arası-geçiş-nasıl-çalışır)

---

## 19. Upgrade Ekranı

`Screen_Upgrades` seçin:
- **UIDocument** → `Assets/UI/Upgrade/UpgradeScreen.uxml`
- **UpgradeScreenController** alanları:

| Alan | Değer |
|---|---|
| `_upgradeTree` | `Configs/UpgradeTreeConfig.asset` ✓ (wizard bağladı) |
| `_economy` | Boşsa → `Bootstrap` GO'yu sürükleyin |
| `_saveService` | Boşsa → `Bootstrap` GO'yu sürükleyin |
| `_prestigeManager` | Boşsa → `Bootstrap` GO'yu sürükleyin |

Upgrade ekranı **drag-to-pan** ve **mouse wheel zoom** destekler — özel bir şey eklemenize gerek yok.

---

## 20. Prestige Overlay

`Screen_Prestige` seçin:
- **UIDocument** → `Assets/UI/Prestige/PrestigeOverlay.uxml`
- **PrestigeScreenUI** → `_prestigeSystem` alanı

Boşsa: `Bootstrap` GO → Inspector → **PrestigeSystem** component'ini bulun, sürükleyin.

---

## 21. Research Ekranı

`Screen_Research` seçin (Research Idle türünde otomatik oluşur):
- **UIDocument** → `Assets/UI/Research/ResearchScreen.uxml`
- **ResearchScreenController** alanları:

| Alan | Değer |
|---|---|
| `_researchService` | `Bootstrap` GO'dan ResearchService |
| `_researchTree` | `Configs/ResearchDatabase.asset` |

---

## 22. Building Ekranı

`Screen_Building` seçin (Building/Farm türünde):
- **UIDocument** → `Assets/UI/Building/BuildingGridScreen.uxml`
- **BuildingScreenController** alanları:

| Alan | Değer |
|---|---|
| `_buildingService` | `Bootstrap` GO'dan BuildingService |
| `_allBuildings` | `Configs/StarterBuilding.asset` + diğerleri |

---

## 23. Ana Menü (MainMenuController)

Ana oyun menüsü için `Screen_MainMenu` adlı bir GameObject oluşturun:

1. Boş GameObject → adını `Screen_MainMenu` yapın
2. **UIDocument** component ekleyin → `Assets/UI/MainMenu/MainMenu.uxml`
3. **MainMenuController** component ekleyin

**MainMenuController** alanlarını doldurun:

| Alan | Nereden Alınır |
|---|---|
| `_gameFlow` | Bootstrap → **GameFlowStateMachine** (varsa) |
| `_generatorSystem` | Bootstrap → **GeneratorSystem** |
| `_prestigeManager` | Bootstrap → **PrestigeStateManager** |
| `_prestigeSystem` | Bootstrap → **PrestigeSystem** |
| `_generatorScreen` | `Screen_Generator` GO |
| `_upgradeScreen` | `Screen_Upgrades` GO |

---

## 24. UI Ekranları Arası Geçiş Nasıl Çalışır?

**MainMenuController** düğmeleri direkt olarak diğer ekranları açar:

```
"Generators" butonu  →  _generatorScreen.Show()
"Upgrades" butonu    →  _upgradeScreen.Show()
"Prestige" butonu    →  _prestigeSystem.TryInitiatePrestige()
```

Her ekranın kendi **Kapat** butonu vardır (`close-button` → `Hide()` çağırır).

### Manuel Buton Bağlantısı (MainMenu olmadan)

Canvas/HUDPanel'deki UGUI butonlarını kullanıyorsanız:

1. `BuyGeneratorButton` → **Button** component → **On Click** eventi
2. `+` → `Screen_Generator` GO'yu sürükleyin → `GeneratorScreenController.Show` seçin

Upgrade için de aynı: `Screen_Upgrades` → `UpgradeScreenController.Show`

### Screen Stack Mantığı

Oyununuzda birden fazla ekran açılabiliyorsa `Screen_*` GameObject'lerine bir `ScreenManager` component yazabilirsiniz. Ancak basit oyunlar için `Show()`/`Hide()` direkt çağrıları yeterlidir.

---

# BÖLÜM E — OYUN TÜRÜ REHBERLERİ

---

## 25. Pure Idle — Adım Adım Bitirme Rehberi

Pure Idle = Generator + Upgrade + Prestige. Tıklama yok, tamamen pasif.

### Adım 1: Ekonomiyi Dengele

`EconomyConfig.asset`:
- `Idle Yield Rate Base`: 0.5
- `Resource Hard Cap`: 10,000,000,000 (10B)
- `Offline Cap Hours`: 12

### Adım 2: Generator Zinciri Oluştur

[Bkz. §6 — Çok Generator örneği](#6-generator-sistemi)  
5–7 generator iyi bir oyun süresi verir.

### Adım 3: Upgrade Tree Tasarla

[Bkz. §15 — Upgrade Tree Tam Rehber](#15-upgrade-tree--tam-rehber)  
Minimum 8–10 node önerilir:
- 3× Yield Boost (Production)
- 2× Offline Bonus (Production)
- 2× Generator Speed (Production)
- 2× Prestige Edge (Prestige, gate: prestige_count >= 1)

### Adım 4: Prestige'i Ayarla

`PrestigeConfig.asset`:
- `Base Multiplier Per Prestige`: 1.5
- `Min Gold To Prestige`: son generator'ın ~100× maliyeti

### Adım 5: UI'ı Bağla

- Generator Ekranı: `Screen_Generator` → `_generatorSystem` dolu mu?
- Upgrade Ekranı: `Screen_Upgrades` → `_upgradeTree` dolu mu?
- HUD butonları: `BuyGeneratorButton` → `GeneratorScreenController.Show`

### Adım 6: Dengeleme — Oyun Süresi Hesabı

| Hedef | Formül |
|---|---|
| İlk generator maliyeti | 30 saniye idle geliri |
| 5. generator maliyeti | 10 dakika idle geliri |
| İlk prestige noktası | 30–60 dakika |
| Hard cap | 10–15 prestige sonrası ulaşılmalı |

### Adım 7: Test Listesi

- [ ] Oyun başında hiç generator yok → yavaş ama gold geliyor (temel rate)
- [ ] 1. generator alınınca gelir belirgin artıyor
- [ ] Upgrade tree açılıyor, node'lar satın alınabiliyor
- [ ] Prestige butonu görünüyor (min gold koşulu sağlanınca)
- [ ] Prestige sonrası çarpan artıyor, hız artıyor
- [ ] Oyunu kapatıp açınca offline kazanç hesaplanıyor

---

## 26. Clicker Idle

Pure Idle üzerine aktif tıklama eklenir.

### Pure Idle adımlarının üzerine:

1. Wizard'da **Click (simple)** modülünü açın
2. `Configs/ClickSourceConfig.asset` → [Bkz. §7](#7-clicksourceconfig--tıklama-mekaniği)
3. Sahnede `ClickTarget` GO var → `ClickTargetHandler` component kontrol edin
4. Combo sistemi için: `Enable Combo: ✓`, `Combo Window: 0.8`, `Max Combo: 5×`

**Dengeleme:** Click yield ≈ 2–5 saniye idle geliri oranında tutun. Tıklama hissettirmeli ama zorunlu olmamalı.

---

## 27. Click Loop (HP'li Hedef)

### Kurulum Kontrol Listesi

- [ ] Sahnede `Bootstrap` → **ClickLoopBootstrap** var
- [ ] `_clickConfig`: `Configs/ClickLoopConfig.asset` bağlı
- [ ] `_clickTargetLayer`: -1 (Everything)
- [ ] Sahnede 3 `ClickTarget` GO var, her birinde `ClickTarget` component + config bağlı

### Target Config Dengesi

| Target | Max HP | Base Yield | Respawn |
|---|---|---|---|
| Kolay (T1) | 10 | 3 gold | 3s |
| Orta (T2) | 25 | 8 gold | 4s |
| Zor (T3) | 50 | 20 gold | 6s |

### Upgrade Önerileri

| Node | Stat | Açıklama |
|---|---|---|
| Click Power I–III | ClickDamage | Her tıklama daha fazla hasar |
| Quick Respawn | ClickTargetRespawnRate | Target'lar daha hızlı geri gelir |
| Combo King | ClickComboMultiplier | Combo çarpanı artar |
| Auto Click | ClickAutoRate | Otomatik tıklama hızı |

---

## 28. Harvest Idle

### Kurulum Kontrol Listesi

- [ ] Bootstrap → **HarvestLoopBootstrap** var
- [ ] `_areaConfig`: `Configs/HarvestAreaConfig.asset` bağlı
- [ ] `_harvestLayer`: -1 (Everything)
- [ ] Sahnede 5 `HarvestNode_*` prefab var, her birinde `HarvestNode` component + config bağlı

### Node Çeşitlendirme

Farklı node türleri için ayrı `HarvestNodeConfigSO` oluşturun:

| Node | Max HP | Yield | Respawn |
|---|---|---|---|
| Çalı | 5 | 2 | 2s |
| Ağaç | 15 | 7 | 5s |
| Maden | 30 | 18 | 10s |

### Upgrade Önerileri

| Node | Stat | Açıklama |
|---|---|---|
| Wide Harvest | HarvestRadius | Cursor daha geniş alan etkiler |
| Quick Tick | HarvestTickRate | Daha sık hasat tick |
| Harvest Combo | HarvestComboMultiplier | Combo çarpanı artar |
| Node Respawn | HarvestNodeRespawnRate | Node'lar daha hızlı respawn |

---

## 29. Idle-vs / RPG (Wave Savaş)

### Kurulum Kontrol Listesi

- [ ] Bootstrap → **WaveCombatBootstrap** var
- [ ] `_waveConfig`, `_enemyConfig` bağlı
- [ ] `_enemyPrefab`: Sahnedeki Enemy GO bağlı
- [ ] `_waveSpawnManager`, `_enemyManager`, `_autoBattle` bağlı

### Wave Dengesi

- Wave 1: 3 düşman, 5 HP, kolay
- Her 10 wave: Boss wave (Elite Stat Multiplier × 3)
- Wave 20+: Oyuncu prestij yapmadan ilerleyemez → prestige kapısı

### Upgrade Önerileri

| Node | Stat | Açıklama |
|---|---|---|
| Attack Boost I–III | Damage | Hasar artışı |
| Critical Hit | CritChance | Kritik şans |
| Gold Rush | GoldDropMultiplier | Düşman gold drop artar |
| Tank | MaxHP | Oyuncu HP artışı |

---

## 30. Tower Defense Idle

Wave savaşın üzerine path + tower slotlar eklenir.

### Wave sisteminin aynısı geçerli ([Bkz. §29](#29-idle-vs--rpg-wave-savaş))

### Ek Adımlar

1. Sahnede `TowerSlot_*` prefab'larını istediğiniz konumlara taşıyın
2. Her `TowerSlot` → **BuildingSlotHandler** component → `_buildingId`, `_gridX`, `_gridY` doldurun
3. Tower yerleştirme geliri için `BuildingService`'i aktif edin (Building modülü ekleyin)

---

## 31. Merge Idle

### Kurulum Kontrol Listesi

- [ ] Bootstrap → **MergeBootstrap** var
- [ ] `Configs/StarterMergeConfig.asset` bağlı
- [ ] Sahnede `MergeBoard` → 3×3 grid, her cell'de `MergeCellHandler` var
- [ ] T1 ve T2 item prefab'ları başlangıçta board'da yerleştirilmiş

### Oyun Akışı

1. Board'da iki aynı tier item → birleşince bir üst tier
2. Satış → gold kazanır
3. Yeterli gold birikince yeni item spawn

### Dengeleme

- T1 satış değeri: 5 gold
- Her tier: ×3 satış değeri
- T1 spawn maliyeti: 10 gold
- Temel pasif gold rate: 0.5 gold/s (öğrenciler biriksin)

---

## 32. Farm Idle

### Generator + Building kombinasyonu

Farm = plot (generator gibi çalışır) + bina yerleştirme.

1. Wizard **Generator** + **Building** modüllerini açtı
2. `Configs/GeneratorDatabase.asset` → farm plot'larınızı generator olarak tanımlayın
3. `Configs/StarterBuilding.asset` → farklı bina türleri (ahır, değirmen, vs.)
4. [Generator Zinciri §6](#6-generator-sistemi) + [Bina Sistemi §13](#13-buildingconfig--bina-sistemi)

---

## 33. Research Idle

### Kurulum Kontrol Listesi

- [ ] Bootstrap → **ResearchBootstrap** var
- [ ] `_researchTree`: `Configs/ResearchDatabase.asset` bağlı
- [ ] `Screen_Research` → **ResearchScreenController** → `_researchService` + `_researchTree` bağlı

### Research Tree Tasarımı

Research ağacı üç katmanlı düşünün:

| Katman | Süre | Maliyet | Etki |
|---|---|---|---|
| Temel | 30–60s | Düşük | +%20 generator hızı |
| Orta | 5–15dk | Orta | Yeni generator açılır |
| İleri | 1–4 saat | Yüksek | Prestige bonusu, offline hız |

---

## 34. Building Idle

### Kurulum Kontrol Listesi

- [ ] Bootstrap → **BuildingBootstrap** var
- [ ] `_starterConfig`: `Configs/StarterBuilding.asset` bağlı
- [ ] `Screen_Building` → **BuildingScreenController** dolu
- [ ] Sahnede `Building_*` GO'lar var, `BuildingSlotHandler` bağlı

### Bina Türü Örnekleri

| Bina | Income/Tick | Base Cost | Max Level |
|---|---|---|---|
| Küçük Ev | 1 | 100 | 5 |
| Dükkan | 5 | 500 | 5 |
| Fabrika | 25 | 3,000 | 5 |
| Gökdelen | 150 | 20,000 | 3 |

---

## 35. Prestige-Heavy (Çok Katmanlı)

### Katmanlar

| Katman | Bootstrap | Ne Sıfırlar | Ne Korur |
|---|---|---|---|
| **Prestige** | PrestigeBootstrap | Gold, Generator | Prestige çarpanı |
| **Ascension** | AscensionStateManager | Prestige sayacı | Ascension bonusu |

### Kurulum

1. Wizard **Prestige-Heavy** türü seçin
2. Bootstrap → **PrestigeBootstrap** + **PrestigeStateManager** kontrol edin
3. `PrestigeConfig.asset` → `Max Permanent Multiplier`: 10,000+
4. Ascension için `AscensionDatabaseSO` oluşturun: **Create → Endless Engine → Config → Ascension Database**

### Çok Para Birimi (Multi-Currency)

**CurrencyService** aktifse:
1. `Configs/CurrencyDatabase.asset` → para birimi tanımları ekleyin (Gems, Crystals, vs.)
2. Her para birimi ayrı `CurrencyConfigSO` asset'i
3. HUD'da `GemLabel` var → otomatik güncellenir

---

## 36. Sistemleri Birleştirmek (Custom / Hibrit Oyunlar)

Endless Engine'in gerçek gücü sistem kombinasyonlarındadır.

### Örnek 1: Tower Defense + Research

Tower Defense oyununa araştırma sistemi eklemek:

1. Tower Defense sahnesi Generate edin
2. Sahnede `Bootstrap` GO seçin → **Component Ekle** → **ResearchBootstrap**
3. `ResearchDatabase.asset` oluşturun, tower upgrade'lerini buraya tanımlayın
4. `Screen_Research` GO oluşturun → UIDocument + ResearchScreenController
5. Bir "Research" butonu ekleyin → `ResearchScreenController.Show()`

### Örnek 2: Harvest Idle + Prestige + Building

1. Harvest Idle Generate edin
2. Bootstrap → **PrestigeBootstrap** + **BuildingBootstrap** ekleyin (wizard zaten bunları ekledi)
3. `PrestigeConfig.asset` → min hasat miktarı koşulu koyun
4. Bina geliri prestige'den sonra açılsın → node'ları `Prestige Gate: 1` yapın

### Örnek 3: Wave RPG + Merge Board (Loot sistemi)

1. IdleVsRPG Generate edin
2. Bootstrap → **MergeBootstrap** ekleyin
3. Düşmanlar yok edilince item drop etsin → `EnemyManager.OnEnemyKilled` eventi → MergeService.SpawnItem()
4. Ayrı bir sahne ekranı olarak Merge Board ekleyin

### Hangi Bootstrap Hangi Modülle Çalışır?

| Modül | Bootstrap Bileşeni | Ön Koşul |
|---|---|---|
| Generator + Economy | AutoSetupBootstrap | — |
| Click (basit) | ClickTargetHandler | AutoSetupBootstrap |
| Click Loop | ClickLoopBootstrap | AutoSetupBootstrap |
| Harvest | HarvestLoopBootstrap | AutoSetupBootstrap |
| Wave / Savaş | WaveCombatBootstrap | AutoSetupBootstrap |
| Prestige | PrestigeBootstrap + PrestigeStateManager | AutoSetupBootstrap |
| Building | BuildingBootstrap + BuildingService | AutoSetupBootstrap |
| Research | ResearchBootstrap + ResearchService | AutoSetupBootstrap + TickEngine |
| Merge | MergeBootstrap + MergeService + InventoryService | AutoSetupBootstrap |

---

# BÖLÜM F — İLERİ SEVİYE

---

## 37. Ses Sistemi (AudioService)

### Kurulum

1. Sahneye boş GO ekleyin → adı `AudioManager`
2. **AudioService** component ekleyin
3. `Configs/` → **Create → Endless Engine → Config → Audio Config** → `AudioConfig.asset`
4. **AudioService** → `_config` alanına `AudioConfig.asset` sürükleyin

### AudioConfig Alanları

`AudioConfig.asset` seçin:

| Alan | Açıklama |
|---|---|
| **Hit Normal Clip** | Normal saldırı sesi |
| **Hit Crit Clip** | Kritik saldırı sesi |
| **Enemy Death Clip** | Düşman ölüm sesi |
| **Wave Complete Clip** | Wave tamamlama sesi |
| **Upgrade Purchased Clip** | Upgrade satın alma sesi |
| **SFX Pool Size** | Eş zamanlı ses kanalı (32 önerilir) |
| **Music Crossfade Duration** | Müzik geçiş süresi (saniye) |

### Ses Olayları — Otomatik

**AudioService** şu olaylara otomatik abone olur ve ses çalar:
- Herhangi bir hasar → `HitNormalClip` / `HitCritClip`
- Düşman ölümü → `EnemyDeathClip`
- Wave tamamlandı → `WaveCompleteClip`
- Upgrade alındı → `UpgradePurchasedClip`

Wave oyunu değilseniz: siz özel olaylarda `AudioService.PlaySFX(clip, volume)` çağırabilirsiniz.

### Müzik Entegrasyonu

1. AudioConfig → `Music Mixer Group` doldurun
2. Kendi MusicManager script'inizde: `AudioService.Duck()` (dialog sırasında), `AudioService.Unduck()` (devam)
3. Müzik geçişi: `AudioService.TransitionToSnapshot(snapshot, duration)`

---

## 38. Kayıt / Yükleme Sistemi

Kayıt tamamen otomatiktir. `Bootstrap` → **Enable Save = ✓**

### Kayıt Dosyası Konumu

```
Windows:  C:\Users\[kullanıcı]\AppData\LocalLow\[Company]\[Game]\save.json
Mac:      ~/Library/Application Support/[Company]/[Game]/save.json
Android:  /data/data/[package]/files/save.json
```

### Ne Kaydedilir?

| Sistem | Kaydedilen Veri |
|---|---|
| EconomyService | Gold miktarı |
| GeneratorSystem | Her generator sayısı |
| UpgradeTreeService | Her node'un rank'ı |
| PrestigeStateManager | Prestige sayısı, kalıcı çarpan |

### Yeni Sistem Kaydetmek

Kendi sisteminizin verisini kaydetmek için `ISaveStateProvider` interface'ini implement edin:

- `OnBeforeSave(SaveData data)` — Kayıt öncesi verinizi SaveData'ya yazın
- `OnAfterLoad(SaveData data)` — Yükleme sonrası SaveData'dan okuyun

Sonra: `SaveService.RegisterStateProvider(this)`

### Schema Versiyonu Yükseltme

Save formatı değiştiyse:  
**Tools → Endless Engine → Schema Bump** → version artar, eski save'ler temiz sıfırlanır.

---

## 39. Görsel Özelleştirme — Sprite ve Prefab

### Package Prefab'larını Oluştur

**Tools → Endless Engine → Create Package Prefabs**

Bu komut, tüm oyun türleri için hazır prefab setlerini oluşturur:
```
Packages/.../Runtime/Prefabs/
  ClickLoop/   ClickTarget_Red, Blue, Green
  Harvest/     HarvestNode_Green, Stone, Golden
  Combat/      Enemy_Default
  TowerDefense/TowerSlot_Default
  Merge/       MergeItem_T1, T2, T3
  Farm/        FarmPlot_Default
  Building/    BuildingSlot_Default
```

### Prefab'ı Özelleştir

1. Prefab'ı Project penceresinde seçin → **Ctrl+D** (kopyala)
2. Kopyanızı `Assets/[OyunAdı]/Prefabs/` klasörüne taşıyın
3. Çift tıklayın → Prefab Mode açılır
4. Sprite'ı değiştirin: `SpriteRenderer` → `Sprite` alanı
5. Sahnede eski instance'ları yenisiyle değiştirin

### Sprite Import Ayarları

Pixel Art için:
- **Filter Mode**: Point (no filter)
- **Compression**: None
- **Pixels Per Unit**: 16 veya 32

2D oyun için:
- **Sprite Mode**: Single
- **Pixels Per Unit**: 100
- **Generate Physics Shape**: ✓

---

## 40. Ana Menü Sahnesi Oluşturma

### Ayrı Sahne Yöntemi (Tavsiye Edilen)

1. **File → New Scene** → `Assets/[OyunAdı]/Scenes/MainMenu.unity` kaydedin
2. Sahneye Canvas ekleyin → "Start", "Settings", "Quit" butonları
3. Start butonu → `SceneManager.LoadScene("GoldRushEmpire")`
4. **File → Build Settings** → MainMenu sahnesini **ilk sıraya** koyun

### Oyun Sahnesi içinde Ana Menü

`Screen_MainMenu` GO'yu kullanın ([Bkz. §23](#23-ana-menü-mainmenucontroller)):
- Oyun başlangıcında otomatik görünür
- "Start" → `GameFlowStateMachine.StartRun()` → menü kapanır, oyun başlar

### Sahne Geçişi

```
MainMenu sahnesi (Build Index 0)
  ↓ Start butonu
Oyun sahnesi (Build Index 1)
  ↓ Prestige → oyun devam eder (sahne geçişi yok)
  ↓ "Ana Menü" butonu → Build Index 0'a dön
```

---

## 41. Build Alma

### Windows x64

1. **File → Build Settings**
2. Platform: **PC, Mac & Linux Standalone** → **Windows x86_64**
3. Sahneler listesinde oyun sahneniz var mı? (Wizard otomatik ekler)
4. **Player Settings**:
   - Company Name, Product Name doldurun
   - **Scripting Backend**: IL2CPP (daha hızlı)
   - **Api Compatibility Level**: .NET Standard 2.1
5. **Build**

### Android

1. **File → Build Settings** → **Android**
2. **Switch Platform**
3. **Player Settings**:
   - **Package Name**: `com.yourcompany.gamename`
   - **Minimum API Level**: 26 (Android 8.0)
   - **Target API Level**: Highest installed
   - **Scripting Backend**: IL2CPP
   - **Target Architectures**: ARMv7 + ARM64
4. **Build And Run**

### Build Kontrol Listesi

- [ ] Tüm sahne asset'leri mevcut (kırmızı ünlem işareti yok)
- [ ] `EconomyConfig`, `GeneratorDatabase`, `SchemaVersion` bağlı
- [ ] `UpgradeTreeConfig` bağlı
- [ ] Tüm UXML dosyaları `Assets/UI/` altında
- [ ] **Enable Save** açık
- [ ] IL2CPP seçili (mono değil)

---

## 42. Steam SDK Entegrasyonu

### Steamworks.NET Kurulumu

1. [https://steamworks.github.io/](https://steamworks.github.io/) → Son sürüm `.unitypackage`
2. Unity: **Assets → Import Package → Custom Package**
3. Proje kökünde `steam_appid.txt` → AppID'nizi yazın (test: `480`)
4. Sahneye `SteamManager` prefab'ını ekleyin (Steamworks paketinde gelir)

### Achievement Sistemi

```
Endless Engine'in MilestoneTracker'ı bir milestone'a ulaştığında
SteamUserStats.SetAchievement("ACH_WIN_100_WAVES") çağırın
SteamUserStats.StoreStats()
```

### Steam Cloud Save

`SaveService`'in verisini Steam buluta senkronize etmek için:

1. `SaveService` → `OnBeforeSave` eventi dinleyin
2. JSON'ı `SteamRemoteStorage.FileWrite("save.json", data)` ile kaydedin
3. `OnAfterLoad` öncesi `SteamRemoteStorage.FileRead("save.json")` ile yükleyin

---

# BÖLÜM G — TROUBLESHOOTING

---

## 43. Troubleshooting — Tüm Hatalar ve Çözümleri

### Derleme Hataları

| Hata | Neden | Çözüm |
|---|---|---|
| `ConfigNotLoadedException: Economy was accessed before configs loaded` | Bir sistem Bootstrap'tan önce çalıştı | Bootstrap GO'nun `DefaultExecutionOrder(-500)` var mı? Silinmemişse `AutoSetupBootstrap.cs` bu attribute'u taşır. |
| `NullReferenceException: _economyConfig is null` | Bootstrap'taki alan boş | `Bootstrap` → `AutoSetupBootstrap` → `Economy Config` alanına `EconomyConfig.asset` sürükleyin |
| `The type 'GeneratedGameHUD' could not be found` | Eski versiyon sahnesi | `Canvas` → `GeneratedGameHUD` component kaldırın, yeniden ekleyin |
| `Ambiguous reference: HUDController` | Bootstrap.HUDController ve UI.HUDController çakışıyor | Tam namespace kullanın: `EndlessEngine.UI.HUDController` |

### Oyun Başlamıyor / Ekran Boş

| Belirtiler | Çözüm |
|---|---|
| Console'da hiçbir log yok | `Bootstrap` GO sahnede var mı? |
| `[AutoSetupBootstrap] Ready.` yok | Config null hatası var — Console'a bakın |
| Gold artmıyor | `GeneratorDatabase` bağlı ama generator count 0 |
| Hiç generator yok Inspector'da | `GeneratorDatabase.asset` → Generators listesi boş |

### Upgrade Tree

| Belirtiler | Çözüm |
|---|---|
| Upgrade ekranı boş | `_upgradeTree` alanı boş → `UpgradeTreeConfig.asset` sürükleyin |
| Node'lar görünüyor ama satın alınamıyor | `_economy` alanı boş → Bootstrap GO sürükleyin |
| Play sonrası upgrade rank'ları sıfırlanıyor | `_upgradeNodeConfigs` boş → Upgrades/ klasöründeki asset'leri Bootstrap'a sürükleyin |
| Node ID uyuşmazlığı uyarısı | `UpgradeTreeConfig`'teki NodeId ile `UpgradeNodeConfigSO`'daki farklı → birebir aynı yapın |

### Generator

| Belirtiler | Çözüm |
|---|---|
| Buy butonu çalışmıyor | `GeneratorScreenController._generatorSystem` boş |
| Generator alındı ama gelir artmadı | `PassiveIncomeService` Bootstrap'ta mevcut mu? AutoSetupBootstrap otomatik ekler |
| Generator sayısı kayıt sonrası sıfırlanıyor | GeneratorId değiştirilmiş — eski save ile uyuşmuyor, `SchemaVersion`'ı yükseltin |

### Wave / Savaş

| Belirtiler | Çözüm |
|---|---|
| Wave başlamıyor | `WaveCombatBootstrap` → `_waveSpawnManager` boş |
| Düşmanlar spawn olmuyor | `_enemyPrefab` atanmamış |
| Düşman HP'si görünmüyor | Prefab'da HPBar child GO yok — `Enemy_Default.prefab` kullanın |
| Wave 1'den sonra duruyor | `WaveConfig` → `Total Waves Per Run` değeri kontrol edin |

### Prestige

| Belirtiler | Çözüm |
|---|---|
| Prestige butonu görünmüyor | `PrestigeBootstrap` eksik veya min koşul sağlanmamış |
| Butona basılıyor ama hiçbir şey olmuyor | `_prestigeSystem` alanı boş |
| Prestige sonrası çarpan artmıyor | `PrestigeConfig.Base Multiplier Per Prestige` 0 olabilir |
| Prestige overlay açılmıyor | `Screen_Prestige` → UIDocument PanelSettings atanmamış |

### UIToolkit / Ekranlar

| Belirtiler | Çözüm |
|---|---|
| `Screen_*` GO'lar görünmüyor | **Panel Settings** eksik → Create → UI Toolkit → Panel Settings → atayın |
| UXML yükleniyor ama içerik boş | Panel Settings'teki Sort Order değerini 100 yapın |
| Ekran açılıyor kapanmıyor | `close-button` element `Hide()` metoduna bağlı mı? UXML'de `name="close-button"` var mı? |
| Upgrade node'lar grid'de görünmüyor | `Grid X` ve `Grid Y` tüm node'larda 0 — farklı değerler verin |

### Kayıt / Yükleme

| Belirtiler | Çözüm |
|---|---|
| Save dosyası oluşmuyor | `Enable Save` kapalı veya path izin sorunu |
| Yüklemede herşey sıfırlanıyor | SchemaVersion değişmiş veya NodeID'ler değiştirilmiş |
| `Load failed: invalid JSON` | Save dosyası bozulmuş — `Application.persistentDataPath`'te `save.json` silin |

### Ses

| Belirtiler | Çözüm |
|---|---|
| Ses çalmıyor | AudioService sahnede yok veya `_config` boş |
| Ses pool tükeniyor uyarısı | `SFX Pool Size` değerini artırın (64'e kadar çıkarın) |
| Müzik crossfade çalışmıyor | `Music Crossfade Duration` 0 olabilir |

---

## Editor Araçları Özeti

| Menü | Ne İşe Yarar |
|---|---|
| **Tools → Endless Engine → New Game Wizard** | Sahne + config oluştur |
| **Tools → Endless Engine → Create Package Prefabs** | Tüm oyun türleri için prefab setleri |
| **Tools → Endless Engine → Economy Simulator** | Gelir dengesi simülasyonu (Play mode gerektirmez) |
| **Tools → Endless Engine → Economy Tuning** | Canlı parametre sliders |
| **Tools → Endless Engine → Config Validator** | Eksik/hatalı config referansları |
| **Tools → Endless Engine → Upgrade Tree Editor** | Görsel upgrade tree düzenleyici |
| **Tools → Endless Engine → Generator Window** | Generator listesi ve test |
| **Tools → Endless Engine → Schema Bump** | Save şema versiyonu artır |

---

*Endless Engine v1.3.4 — Bu dosya tek kaynak noktasıdır.*
