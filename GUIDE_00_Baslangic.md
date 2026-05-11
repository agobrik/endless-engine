# Endless Engine — Oyun Yapım Rehberi

> **Bu tek rehber yeterlidir.** Kurulumdan Steam'e kadar her şey burada.  
> Unity temellerini bildiğinizi (sahne, GameObject, Component, Inspector) varsayar.  
> Kod yazmadan oyun yapabilirsiniz — tüm sistemler pakette hazır.

---

## İçindekiler

1. [Paket Kurulumu](#1-paket-kurulumu)
2. [New Game Wizard ile Oyun Oluştur](#2-new-game-wizard-ile-oyun-oluştur)
3. [Sahneyi Anlamak](#3-sahneyi-anlamak)
4. [Oyun Türü Başlatma Rehberleri](#4-oyun-türü-başlatma-rehberleri)
5. [Config Scriptable Object'lerini Düzenle](#5-config-scriptable-objectlerini-düzenle)
6. [Upgrade Tree Düzenle](#6-upgrade-tree-düzenle)
7. [UI Ekranlarını Özelleştir](#7-ui-ekranlarını-özelleştir)
8. [Prefab Setlerini Kullan](#8-prefab-setlerini-kullan)
9. [Prestige Sistemi](#9-prestige-sistemi)
10. [Kayıt / Yükleme Sistemi](#10-kayıt--yükleme-sistemi)
11. [Build Alma](#11-build-alma)
12. [Steam SDK Entegrasyonu](#12-steam-sdk-entegrasyonu)
13. [Troubleshooting](#13-troubleshooting)

---

## 1. Paket Kurulumu

Bu repo, Endless Engine paketini zaten yerel olarak içerir (`Packages/com.endlessengine.idle/`).  
Mevcut projeyi kullanıyorsanız bu bölümü atlayın.

### Yeni bir Unity projesine kurmak için

**Yöntem A — Git URL (Tavsiye Edilen)**

1. **Window → Package Manager** açın
2. Sol üstteki **+** → **Add package from git URL...**
3. Şu URL'yi yapıştırın:
   ```
   https://github.com/agobrik/endless-engine.git?path=Packages/com.endlessengine.idle
   ```
4. **Add** tıklayın

**Yöntem B — Diskten Yerel Klasör**

1. Repo'yu zip olarak indirin ve çıkarın
2. **Window → Package Manager → + → Add package from disk...**
3. `com.endlessengine.idle/package.json` seçin

### Bağımlılıklar

Paket kurulunca bunlar otomatik yüklenir:

| Paket | Sürüm |
|---|---|
| Unity Input System | 1.14.2 |
| Addressables | 2.7.6 |
| Unity UI (uGUI) | 2.0.0 |
| Newtonsoft JSON | 3.2.2 |

TextMeshPro sorulursa: **Window → TextMeshPro → Import TMP Essential Resources → Import**

---

## 2. New Game Wizard ile Oyun Oluştur

**Tools → Endless Engine → New Game Wizard**

Bu tek adımda sahne + tüm config asset'leri oluşur.

### Adımlar

| Adım | Ne Yapıyorsunuz |
|---|---|
| **1. Game Name** | Oyun adını yazın (örn. `GoldMineEmpire`) |
| **2. Game Type** | Aşağıdaki tablodan türü seçin |
| **3. Modules** | Otomatik seçilir — gerekirse değiştirin |
| **4. Generate Skeleton** | Butona basın |
| **5. Sahneyi aç** | `Assets/GoldMineEmpire/Scenes/GoldMineEmpire.unity` |
| **6. Play** | Oyunu çalıştırın — anında çalışır |

### Game Type Seçim Tablosu

| Tür | Oyun Örneği | Temel Mekanik |
|---|---|---|
| **Pure Idle** | AdVenture Capitalist, Cookie Clicker | Generator → Gold → Upgrade |
| **Clicker Idle** | Clicker Heroes (erken) | Tıklama + Generator |
| **Click Loop** | Tap Titans | HP'li target'lara tıkla, combo yap |
| **Harvest Idle** | — | Cursor'ı node'ların üzerinde gez |
| **Idle-vs / RPG** | Swipe Brick Breaker | Otomatik savaş + Wave |
| **Tower Defense** | Kingdom Rush Idle | Path + Tower Slot + Wave |
| **Merge Idle** | Merge Dragons | 2×N birleştir → 1×(N+1) |
| **Farm Idle** | Hay Day Idle | Grid + Ürün + Building |
| **Research Idle** | — | Generator + Zaman-kilitli tech tree |
| **Building Idle** | Idle City Builder | Grid building, her bina gelir üretir |
| **Prestige-Heavy** | NGU Idle | Tüm prestige katmanları aktif |
| **Custom** | — | Tüm modüller kapalı, elle kur |

### Wizard ne oluşturur?

```
Assets/GoldMineEmpire/
  Configs/
    EconomyConfig.asset          ← Ekonomi ayarları (preset uygulandı)
    SchemaVersion.asset          ← Save şema versiyonu
    GoldMine.asset               ← İlk generator config
    GeneratorDatabase.asset      ← Generator listesi
    PrestigeConfig.asset         ← Prestige ayarları
    UpgradeTreeConfig.asset      ← UI upgrade tree (hazır node'larla dolu)
    Upgrades/                    ← UpgradeNodeConfigSO asset'leri (4 adet)
  Scenes/
    GoldMineEmpire.unity         ← Açıp Play'e bas, çalışır
```

---

## 3. Sahneyi Anlamak

Generate ettiğiniz sahneyi açıp Hierarchy'e bakın:

```
Main Camera
EventSystem
Bootstrap                        ← Tüm sistemler buraya bağlı
  AutoSetupBootstrap             ← Inspector'da config'leri görebilirsiniz
  [Modüle göre: WaveCombatBootstrap, HarvestLoopBootstrap, vs.]
Canvas                           ← UGUI HUD (her zaman görünür)
  HUDPanel
    GoldLabel
    IncomeLabel
    GeneratorTitle
    BuyGeneratorButton
    PrestigeButton
Screen_HUD                       ← UIToolkit HUD (HUD.uxml bağlı)
Screen_Generator                 ← Generator ekranı (GeneratorScreen.uxml bağlı)
Screen_Upgrades                  ← Upgrade tree ekranı (UpgradeScreen.uxml bağlı)
Screen_Prestige                  ← Prestige overlay (PrestigeOverlay.uxml bağlı)
[Oyun türüne göre world nesneleri]
```

### Bootstrap Inspector'ı

`Bootstrap` GameObject'ini seçin → Inspector:

| Alan | Ne İşe Yarar |
|---|---|
| **Economy Config** | Wizard otomatik bağladı |
| **Generator Database** | Wizard otomatik bağladı |
| **Schema Version** | Wizard otomatik bağladı |
| **Prestige Config** | Wizard otomatik bağladı |
| **Upgrade Node Configs** | Wizard otomatik doldurdu (Upgrades/ klasöründen) |
| **Enable Save** | ✓ — kayıt açık |

---

## 4. Oyun Türü Başlatma Rehberleri

### Pure Idle / Farm Idle / Building Idle

Sahne açılır açılmaz çalışır. Özelleştirmek için:

1. `Configs/EconomyConfig.asset` seçin → Inspector'da `Idle Yield Rate Base` değerini artırın
2. `Configs/GoldMine.asset` seçin → `Base Yield Per Second`, `Base Cost`, `Cost Scaling Factor` ayarlayın
3. Birden fazla generator eklemek: [Bölüm 5'e bakın](#5-config-scriptable-objectlerini-düzenle)

### Click Loop

Play'e basınca 3 renkli target görünür. Tıkladıkça HP azalır, gold düşer.

Özellestirmek için:
1. `Configs/ClickLoopConfig.asset` → kombo eşiği, auto-click hızı
2. `Configs/ClickTarget_0/1/2.asset` → her target'ın HP, yield, respawn süresi
3. Ek target eklemek: [Bölüm 8'e bakın](#8-prefab-setlerini-kullan)

### Harvest Idle

Play'e basınca yeşil node'lar görünür. Mouse'u üzerine götürün, gold düşer.

Özelleştirmek için:
1. `Configs/HarvestAreaConfig.asset` → cursor yarıçapı, combo çarpanı
2. `Configs/HarvestNode.asset` → node HP, yield, respawn süresi

### Idle-vs / RPG ve Tower Defense

Play'e basınca düşmanlar spawn olup otomatik savaş başlar.

Özelleştirmek için:
1. `Configs/WaveConfig.asset` → wave sayısı, enemy sayısı per wave
2. `Configs/EnemyStatConfig.asset` → düşman HP, hasar, hız
3. Tower Defense'de: Sahnedeki `TowerSlot` nesnelerini dilediğiniz konuma taşıyın

### Merge Idle

3×3 grid başlar, 2 adet T1 item var. İki aynı item'ı merge edince bir üst tier oluşur.

Özelleştirmek için:
1. `Configs/StarterMergeConfig.asset` → başlangıç item sayısı, tier sınırı
2. Merge item prefab'larını değiştirmek: [Bölüm 8'e bakın](#8-prefab-setlerini-kullan)

### Research Idle

Generator geliriyle araştırma yapılır. Araştırmalar zaman-kilitlidir.

Özelleştirmek için:
1. `Configs/ResearchDatabase.asset` → araştırma ağacını Inspector'da düzenleyin

---

## 5. Config Scriptable Object'lerini Düzenle

Tüm oyun parametreleri `Assets/[OyunAdı]/Configs/` klasöründeki `.asset` dosyalarındadır.

### Yeni Generator Eklemek

1. `Assets/[OyunAdı]/Configs/` klasörüne sağ tıklayın
2. **Create → Endless Engine → Config → Generator Config**
3. `GeneratorId`, `Display Name`, `Base Yield Per Second`, `Base Cost` doldurun
4. `Configs/GeneratorDatabase.asset` seçin → Inspector'da **Generators** listesine sürükleyip bırakın

### EconomyConfig Parametreleri

`Configs/EconomyConfig.asset` seçin:

| Parametre | Açıklama |
|---|---|
| **Idle Yield Rate Base** | Pasif gelir çarpanı (0 = saf aktif oyun) |
| **Base Multiplier Per Prestige** | Her prestige'de gelir çarpanı |
| **Resource Hard Cap** | Maximum altın miktarı |
| **Offline Cap Hours** | Offline kazanç kaç saat sayılır |

### PrestigeConfig Parametreleri

`Configs/PrestigeConfig.asset` seçin:

| Parametre | Açıklama |
|---|---|
| **Base Multiplier Per Prestige** | Prestige başına kalıcı çarpan |
| **Max Permanent Multiplier** | Maksimum çarpan sınırı |
| **Min Gold To Prestige** | Prestige için gereken minimum altın |
| **Min Wave For Prestige** | Wave tabanlı oyunlarda minimum wave |

---

## 6. Upgrade Tree Düzenle

### UpgradeTreeConfig.asset (UI Görsel Ağaç)

1. `Configs/UpgradeTreeConfig.asset` seçin
2. Inspector'da **Nodes** listesi görünür — her node bir upgrade'dir
3. Node eklemek: **+** butonuna basın, alanları doldurun:

| Alan | Açıklama |
|---|---|
| **Node Id** | Benzersiz sabit ID (değiştirmeyin, save key!) |
| **Display Name** | Ekranda gösterilecek isim |
| **Description** | Tooltip açıklaması |
| **Affected Stat** | Hangi istatistiği etkiler |
| **Effect Per Rank** | Her rank'ta ne kadar etki (0.20 = %20) |
| **Max Rank** | Maksimum yükseltme sayısı |
| **Base Cost** | İlk rank maliyeti (altın) |
| **Cost Scaling Factor** | Her rank'ta maliyet artış çarpanı (1.5 = %50 artış) |
| **Prerequisite Node IDs** | Açılması için önce hangi node alınmalı |
| **Grid X / Grid Y** | Upgrade screen'deki kolon/satır konumu |

### Upgrades/ Klasöründeki NodeConfigSO'lar (Oyun Mantığı)

`Configs/Upgrades/` klasöründe her node için ayrı bir `.asset` dosyası var.  
Bu dosyalar `UpgradeTreeService`'in kullandığı veridir (kayıt/yükleme dahil).  
`UpgradeTreeConfig.asset` ile senkronize tutun.

> **İpucu:** Yeni bir node ekleyince hem `UpgradeTreeConfig.asset`'e hem de  
> `Configs/Upgrades/` klasörüne eklemelisiniz.

---

## 7. UI Ekranlarını Özelleştir

Sahnede `Screen_*` adlı GameObject'ler UIToolkit ekranlarıdır.

### HUD Ekranı (Screen_HUD)

`Screen_HUD` seçin:
- **UIDocument** component → **Visual Tree Asset**: `Assets/UI/HUD/HUD.uxml`
- **HUDController** component — hiçbir şeye dokunmanız gerekmez, static event'lere abone olur

HUD görünümünü değiştirmek için `Assets/UI/HUD/HUD.uxml` dosyasını UI Builder'da açın.

### Generator Ekranı (Screen_Generator)

`Screen_Generator` seçin:
- **UIDocument** → `Assets/UI/Generator/GeneratorScreen.uxml`
- **GeneratorScreenController** → **Generator System** alanı doldurulumuş olmalı

`GeneratorScreen.uxml` dosyasını UI Builder'da özelleştirin.

### Upgrade Ekranı (Screen_Upgrades)

`Screen_Upgrades` seçin:
- **UIDocument** → `Assets/UI/Upgrade/UpgradeScreen.uxml`
- **UpgradeScreenController** → **Upgrade Tree** alanı `Configs/UpgradeTreeConfig.asset`'e bağlı

### Prestige Overlay (Screen_Prestige)

`Screen_Prestige` seçin:
- **UIDocument** → `Assets/UI/Prestige/PrestigeOverlay.uxml`
- **PrestigeScreenUI** component

### UGUI Canvas HUD (Canvas/HUDPanel)

Wizard ayrıca bir UGUI canvas da oluşturur. UIToolkit'e geçiş yapmak istemiyorsanız bunu kullanın.  
`GeneratedGameHUD` component'i Bootstrap'a bakarak label'ları otomatik günceller.

---

## 8. Prefab Setlerini Kullan

### Package Prefab'larını Oluştur

Paketteki hazır prefab'ları oluşturmak için:

**Tools → Endless Engine → Create Package Prefabs**

Bu komut şu klasörü doldurur:
```
Packages/com.endlessengine.idle/Runtime/Prefabs/
  Bootstrap/
    AutoSetupBootstrap.prefab
  ClickLoop/
    ClickTarget_Red.prefab
    ClickTarget_Blue.prefab
    ClickTarget_Green.prefab
  Harvest/
    HarvestNode_Green.prefab
    HarvestNode_Stone.prefab
    HarvestNode_Golden.prefab
  Combat/
    Enemy_Default.prefab
  TowerDefense/
    TowerSlot_Default.prefab
  Merge/
    MergeItem_T1.prefab
    MergeItem_T2.prefab
    MergeItem_T3.prefab
  Farm/
    FarmPlot_Default.prefab
  Building/
    BuildingSlot_Default.prefab
```

Prefab'ları oluşturduktan sonra **Generate Skeleton** yeniden çalıştırılırsa bu prefab'lar sahneye yerleştirilir.

### Kendi Prefab'ınızı Kullanmak

1. `Packages/.../Runtime/Prefabs/` içindeki prefab'ı Project penceresinde seçin
2. **Ctrl+D** ile kopyalayın → kendi `Assets/` klasörünüze taşıyın
3. Görseli, component ayarlarını dilediğiniz gibi değiştirin
4. Sahnede eski prefab instance'larını yenisiyle değiştirin (sürükleyip bırakın)

---

## 9. Prestige Sistemi

### Prestige Nasıl Çalışır

Oyuncu prestige yaptığında:
1. Gold sıfırlanır
2. Generator'lar sıfırlanır
3. `PrestigeConfig.BaseMultiplierPerPrestige` kadar kalıcı çarpan kazanılır
4. `PrestigeGateRequirement` olan upgrade node'ları açılır

### Prestige'i Aktif Etmek

Wizard **Prestige** modülü seçiliyse otomatik aktiftir.  
Doğrulayın: `Bootstrap` → Inspector → **PrestigeBootstrap** component var mı?

### Prestige Butonu

`Screen_Prestige` → **PrestigeScreenUI** component:
- `_prestigeSystem` alanı, `Bootstrap`'taki `PrestigeSystem`'e bağlı olmalı

Eğer bağlı değilse: `Bootstrap` GO'yu sürükleyip `_prestigeSystem` alanına bırakın.

---

## 10. Kayıt / Yükleme Sistemi

Kayıt otomatiktir — `Bootstrap` → **Enable Save = ✓**

Kayıt dosyası: `Application.persistentDataPath/save.json`

### Kayıt Başarılı mı?

Play modunda `Console`'da şunu görmelisiniz:
```
[SaveService] Saved to: C:/Users/.../save.json
```

### Kayıt Sorunları

| Sorun | Çözüm |
|---|---|
| `[SaveService] Load failed: file not found` | İlk çalışmada normaldir |
| Save sonrası değerler sıfırlanıyor | `Schema Version` asset eksik — Wizard yeniden çalıştırın |
| Upgrade tree sıfırlanıyor | `Upgrades/` klasöründeki NodeID'leri değiştirmeyin |

---

## 11. Build Alma

### Windows x64 Build

1. **File → Build Settings**
2. **Platform: Windows, Mac, Linux** — Windows x86_64 seçin
3. Sahnenizi **Scenes In Build** listesinde görün (Wizard otomatik ekler)
4. **Build** → çıktı klasörü seçin

### Build Kontrol Listesi

- [ ] `EconomyConfig` asset sahnede Bootstrap'a bağlı
- [ ] `GeneratorDatabase` asset sahnede Bootstrap'a bağlı
- [ ] `UpgradeTreeConfig` asset `Screen_Upgrades`'e bağlı
- [ ] Tüm UXML asset'leri `Assets/UI/` klasöründe var
- [ ] **Enable Save** açık
- [ ] Build platform: **IL2CPP** (daha hızlı çalışır)

---

## 12. Steam SDK Entegrasyonu

### Steamworks.NET Kurulumu

1. [Steamworks.NET](https://steamworks.github.io/) GitHub sayfasından `.unitypackage` indirin
2. Unity'de **Assets → Import Package → Custom Package**
3. `steam_appid.txt` dosyasına AppID'nizi yazın (proje kökünde olmalı)

### Temel Steam Init

Sahneye boş bir GameObject ekleyin, adını `SteamManager` yapın.  
Steamworks.NET paketindeki `SteamManager.cs` component'ini bu GameObject'e ekleyin.

Achievements için:
- `SteamUserStats.SetAchievement("ACHIEVEMENT_ID")` — ilgili eventi tetikleyin
- Endless Engine'deki `MilestoneSystem` eventi dinleyip bunu çağırabilir

### Steam Cloud Save

`SaveService.GetSaveData()` metodunu çağırıp JSON'ı `SteamRemoteStorage.FileWrite()` ile kaydedin.  
Load sırasında ise `SteamRemoteStorage.FileRead()` ile okuyup `SaveService.LoadFromData()` çağırın.

---

## 13. Troubleshooting

### "ConfigNotLoadedException: Economy was accessed before configs were loaded"

**Neden:** Bir sistem `ConfigRegistry.Economy`'e Bootstrap'tan önce erişmeye çalışıyor.  
**Çözüm:** `Bootstrap` GO'nun `DefaultExecutionOrder(-500)` ile ilk başlamasını sağlayın.  
Wizard'ın oluşturduğu Bootstrap bunu otomatik yapıyor — sadece kopyalayıp başka sahneye aldıysanız sorun çıkar.

### "NullReferenceException: _economyConfig is null" (AutoSetupBootstrap)

**Neden:** Config asset Bootstrap'a bağlı değil.  
**Çözüm:** `Bootstrap` → **AutoSetupBootstrap** Inspector'da `Economy Config` alanı boş. `Configs/EconomyConfig.asset`'i sürükleyip bırakın.

### Upgrade Tree Boş / Upgrade Ekranında Hiç Node Yok

**Neden:** Bootstrap'taki `_upgradeNodeConfigs` boş kalmış.  
**Çözüm:**
1. `Bootstrap` seçin → Inspector → **AutoSetupBootstrap** → **Upgrade Node Configs** listesini kontrol edin
2. Boşsa: `Configs/Upgrades/` klasöründeki tüm `.asset` dosyalarını listeye sürükleyin
3. Veya Wizard'ı yeniden çalıştırın (Asset mevcutsa üzerine yazmaz)

### Generator Geliri Sıfır

**Neden:** Generator Database yüklenmemiş veya generator count 0.  
**Çözüm:** Play modunda `Bootstrap` Inspector'ında **GeneratorSystem** component'ini kontrol edin. `Configs` property'de generator listesi gözükmeliydi.  
Yoksa: `Configs/GeneratorDatabase.asset` seçin → **Generators** listesinde generator asset'i var mı kontrol edin.

### Prestige Butonu Görünmüyor / Çalışmıyor

**Neden:** PrestigeStateManager/PrestigeBootstrap eksik veya `_prestigeSystem` bağlı değil.  
**Çözüm:**
1. `Bootstrap` → Inspector → **PrestigeBootstrap** component var mı?
2. `Screen_Prestige` → **PrestigeScreenUI** → `_prestigeSystem` alanı dolu mu?
3. Boşsa: `Bootstrap` GO'yu sürükleyip `_prestigeSystem` alanına bırakın

### Save Yükleniyor Ama Değerler Tutarsız

**Neden:** Schema versiyonu değişmiş, eski save ile uyuşmuyor.  
**Çözüm:** `Configs/SchemaVersion.asset` → **Version** değerini artırın. Eski save'i siler, temiz başlar.

### UIDocument / Screen Görünmüyor

**Neden:** `PanelSettings` atanmamış veya UXML asset eksik.  
**Çözüm:**
1. `Screen_HUD` → **UIDocument** component → **Panel Settings** boşsa bir `PanelSettings` asset'i oluşturun (Create → UI Toolkit → Panel Settings) ve atayın
2. **Visual Tree Asset** boşsa `Assets/UI/HUD/HUD.uxml`'i sürükleyip bırakın

### ClickTarget / HarvestNode Spawn Olmadı

**Neden:** Bootstrap içinde ilgili Bootstrap component'i eksik.  
**Çözüm:**
- Click Loop için: `Bootstrap` → **ClickLoopBootstrap** component var mı? `_clickConfig` bağlı mı?
- Harvest için: `Bootstrap` → **HarvestLoopBootstrap** component var mı? `_areaConfig` bağlı mı?

Eksikse Wizard'ı yeniden çalıştırın veya elle Component ekleyin ve config'leri bağlayın.

### Wave'ler Başlamıyor

**Neden:** WaveCombatBootstrap bağlantıları eksik.  
**Çözüm:** `Bootstrap` → **WaveCombatBootstrap** Inspector:
- `_waveSpawnManager` → sahnedeki **WaveSpawnManager** component'i
- `_waveConfig` → `Configs/WaveConfig.asset`
- `_enemyConfig` → `Configs/EnemyStatConfig.asset`
- `_enemyPrefab` → Sahnedeki `EnemyPrefabHolder/Enemy` GO

### "The name 'GeneratedGameHUD' does not exist in the current context"

**Neden:** Eski versiyondan bir sahne açtınız.  
**Çözüm:** `Canvas` → **GeneratedGameHUD** component kaldırın, yerine **AutoSetupBootstrap** Bootstrap referansıyla yeniden ekleyin. Veya Wizard ile yeni sahne oluşturun.

### Performans: Sahne Ağır Açılıyor

- `TickEngine` tick interval'ını artırın: Bootstrap'ta `TickInterval = 0.5f` (varsayılan 0.1f)
- Offline gelir hesabı yavaşsa `OfflineCapHours`'u düşürün

---

## Editor Araçları Özeti

| Menü | Ne İşe Yarar |
|---|---|
| **Tools → Endless Engine → New Game Wizard** | Sahne + config oluştur |
| **Tools → Endless Engine → Create Package Prefabs** | Hazır prefab setleri oluştur |
| **Tools → Endless Engine → Economy Simulator** | Gelir dengesini simüle et |
| **Tools → Endless Engine → Economy Tuning** | Canlı tuning penceresi |
| **Tools → Endless Engine → Config Validator** | Config eksikliklerini bul |
| **Tools → Endless Engine → Upgrade Tree Editor** | Görsel upgrade tree düzenleyici |
| **Tools → Endless Engine → Generator Window** | Generator listesi ve testleri |
| **Tools → Endless Engine → Schema Bump** | Save şemasını yükselt |

---

*Endless Engine v1.3.4 — Bu rehber tek kaynak noktasıdır.*
