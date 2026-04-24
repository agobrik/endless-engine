# Idle Game Toolset — Editor Reference

**Menü yolu:** `Tools → Endless Engine → ...`

Bu doküman, toolset'in her editor penceresini, ne yaptığını, nasıl açılacağını,
hangi asset'lara ihtiyaç duyduğunu ve adım adım nasıl kullanılacağını açıklar.

---

## İçindekiler

1. [New Game Wizard](#1-new-game-wizard)
2. [Generator Editor](#2-generator-editor)
3. [Upgrade Tree Editor](#3-upgrade-tree-editor)
4. [Economy Tuning](#4-economy-tuning)
5. [Asset Creator'lar (Bu Oyuna Özgü)](#5-asset-creatorlar-bu-oyuna-özgü)

---

## 1. New Game Wizard

**Menü:** `Tools → Endless Engine → New Game Wizard`

### Ne yapar

Yeni bir idle oyunu için boş bir proje iskeleti oluşturur. Seçtiğiniz modüllere
göre `Assets/<OyunAdı>/` altında config asset'leri ve bir bootstrap script'i üretir.
Her zaman dahil olan **Core** modülünün yanı sıra şu modülleri seçebilirsiniz:

| Modül | İçerik |
|-------|--------|
| **Core** (zorunlu) | TickEngine, EconomyService, SaveService, GameFlowStateMachine |
| **Generator** | PassiveIncomeService + GeneratorSystem — pasif tıklama geliri |
| **Cursor** | CursorYieldService — mouse hareketi ile gelir (Speed/Hover/Distance) |
| **Click** | ClickYieldService — tıklama geliri, combo, crit, auto-click |
| **Zone** | ZoneSystem — world-space bölgeler, hover veya pasif gelir |
| **Wave / Combat** | WaveSpawnManager + EnemyManager + HealthSystem — wave tabanlı koşu |
| **Prestige** | PrestigeStateManager — prestige döngüsü |

### Gerekli asset'lar

Hiçbir asset gerekmez — wizard hepsini oluşturur.

### Adım adım kullanım

1. `Tools → Endless Engine → New Game Wizard` aç
2. **Game Name** alanına oyun adını gir (boşluk/tire PascalCase'e çevrilir)
3. İstediğin modülleri seç — sağdaki açıklama ne gerektirdiğini anlatır
4. **Files to Create** önizlemesi oluşacak dosya listesini gösterir
5. **Generate Skeleton** butonuna tıkla

### Oluşturulan dosyalar

```
Assets/<OyunAdı>/
├── Configs/
│   ├── EconomyConfig.asset           ← her zaman
│   ├── GeneratorDatabase.asset       ← Generator modülü seçiliyse
│   ├── CursorActivityConfig.asset    ← Cursor modülü seçiliyse
│   ├── ClickSourceConfig.asset       ← Click modülü seçiliyse
│   ├── ZoneDatabase.asset            ← Zone modülü seçiliyse
│   ├── WaveConfig.asset              ← Wave modülü seçiliyse
│   ├── EnemyStatConfig.asset         ← Wave modülü seçiliyse
│   ├── RunConfig.asset               ← Wave modülü seçiliyse
│   └── PrestigeConfig.asset          ← Prestige modülü seçiliyse
├── Scripts/
│   └── <OyunAdı>Bootstrap.cs         ← sadece seçilen modülleri wire eden bootstrap
└── Scenes/
    └── (boş klasör — sahneni manuel oluştur)
```

### Bootstrap script

Üretilen `<OyunAdı>Bootstrap.cs` sadece seçilen modüllerin SerializeField'larını
içerir. Inspector'da ilgili GameObject'leri sürükleyip Play'e basabilirsin.
Wave ve Zone modülleri için `IInputProvider` atama satırları yorum olarak bırakılmıştır
— kendi input setup'ına göre uncommment et.

### Dikkat noktaları

- Aynı isimle ikinci kez çalıştırırsan mevcut asset'ları/script'i **üzerine yazmaz**
  (idempotent — güvenle tekrar çalıştırabilirsin)
- Sahne dosyası oluşturulmaz — Unity'de `File → New Scene` ile kendin oluştur,
  ardından gerekli GameObject'leri ve bootstrap'ı ekle
- Wizard ürettikten sonra `Assets → Refresh` (Ctrl+R) ile Unity'nin script'i görmesini sağla

---

## 2. Generator Editor

**Menü:** `Tools → Endless Engine → Generator Editor`

### Ne yapar

Oyunun pasif gelir kaynaklarını (generator'ları) görsel olarak düzenler.
Sol tarafta generator listesi, sağ tarafta seçilen generator'ın tüm alanları
ve maliyet eğrisi grafiği gösterilir.

### Gerekli asset'lar

| Asset | Tür | Nasıl oluşturulur |
|---|---|---|
| `GeneratorDatabase.asset` | `GeneratorDatabaseSO` | Project penceresi → sağ tık → Create → Endless Engine → Config → Generator Database |
| Her generator için `.asset` | `GeneratorConfigSO` | Generator Editor'dan "+" butonu ile otomatik oluşturulur |

> **Not:** Projede tek bir `GeneratorDatabaseSO` varsa editor açılışta otomatik yükler.
> Birden fazla varsa toolbar'daki **Load Database** ile seçin.

### Arayüz

```
┌─────────────────────────────────────────────────────────────────┐
│ [Load Database] [New Database] [+ Add]   filename.asset *  [Save]│
├──────────────┬──────────────────────────────────────────────────┤
│  GENERATORS  │  Generator Inspector                             │
│              │                                                  │
│  #1 ██ Name  │  IDENTITY                                        │
│     yield•cost│    Generator ID   [gen_basic        ]          │
│              │    Display Name   [Basic Producer    ]          │
│  #2 ██ Name  │    Description    [Generates gold... ]          │
│     yield•cost│                                                 │
│              │  ECONOMY                                         │
│  ...         │    Yield / Second [1.0  ]                        │
│              │    Base Cost      [100  ]                        │
│              │    Cost Scale     [1.15 ]                        │
│              │    Max Count      [-1   ]  (-1 = sınırsız)       │
│  ─────────── │                                                  │
│  [↑] [↓] [Del]│  UNLOCK                                        │
│              │    Unlock Req.    [0    ]                        │
│              │    Prerequisite   [None ↓]                       │
│              │                                                  │
│              │  COST CURVE (first 10 copies)                    │
│              │  ████▓▓▒▒░░  (mavi→turuncu bar chart)           │
└──────────────┴──────────────────────────────────────────────────┘
```

### Adım adım kullanım

**Yeni proje başlatırken:**

1. `Tools → Endless Engine → Generator Editor` ile pencereyi aç
2. **New Database** → kayıt yeri seç (örn. `Assets/Configs/Generators/GeneratorDatabase.asset`)
3. **+ Add** butonu ile generator ekle
4. Sağ tarafta açılan inspector'da şu alanları doldur:
   - **Generator ID:** Kayıt anahtarı. Bir kez ayarlandıktan sonra **asla değiştirme** (save verisi bozulur)
   - **Display Name:** Oyuncuya gösterilen isim
   - **Yield / Second:** Bu generator'ın her kopyasının saniyede ürettiği gold
   - **Base Cost:** İlk kopyanın maliyeti
   - **Cost Scale Factor:** Her ek kopyanın maliyeti bu oran kadar artar (1.15 = %15 artış)
   - **Max Count:** Maksimum satın alınabilir kopya (-1 = sınırsız)
   - **Unlock Req.:** Önceki generator'dan kaç kopya olması gerekiyor
   - **Prerequisite:** Hangi generator önce gelmeli (referans)
5. **Save** ile kaydet (sol üstte `*` işareti unsaved değişiklik anlamına gelir)

**Sıralama:**
- Liste sırasına dikkat et — `GeneratorSystem` bunu `Generators[]` array sırası olarak kullanır
- ↑ ↓ butonları ile sırayı değiştir, ardından Save

**Cost Curve grafiği:**
- Mavi (1. kopya) → turuncu (10. kopya) gradient
- Sol üst köşedeki değer maksimum maliyeti gösterir
- Scale Factor yüksekse eğim dik olur — oyunun sonunda ulaşılmaz maliyet demek

**Dikkat edilmesi gerekenler:**
- Generator ID'yi ship ettikten sonra değiştirme — save dosyası bu ID ile saklar
- `Max Count = -1` sınırsız demek, 0 satın alınamaz demek
- Prerequisite generator silinirse referans kırılır — önce bağımlılığı kaldır

---

## 3. Upgrade Tree Editor

**Menü:** `Tools → Endless Engine → Upgrade Tree Editor`

### Ne yapar

`UpgradeTreeConfigSO` asset'ının node'larını görsel bir canvas üzerinde
GraphView ile düzenler. Node'lar arasında port'ları sürükleyerek önkoşul
bağlantıları (edge) kurulur. Sağ inspector panel ile her node'un tüm
özellikleri düzenlenir.

### Gerekli asset'lar

| Asset | Tür | Nasıl oluşturulur |
|---|---|---|
| `UpgradeTreeConfig.asset` | `UpgradeTreeConfigSO` | Project → Create → Endless Engine → Config → Upgrade Tree Config |

> **Not:** Projede tek `UpgradeTreeConfigSO` varsa otomatik yüklenir.

### Arayüz

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ [Load Asset] [New Tree] [+ Add Node] [Frame All]  filename * [PR] [Save]     │
├────────────────────────────────────────────────┬─────────────────────────────┤
│                                                │  Node Inspector             │
│   GraphView Canvas                             │                             │
│                                                │  IDENTITY                   │
│   ┌──────────┐      ┌──────────┐               │    Node ID  [awakening    ] │
│   │ Node A   ├──────┤ Node B   │               │    Name     [Awakening    ] │
│   │ [out] ●──┤  ┌───┤ ● [in]  │               │    Desc     [The spark... ] │
│   └──────────┘  │  └──────────┘               │    Category [Production ↓ ] │
│                 │                              │                             │
│   ┌──────────┐  │                              │  EFFECT                     │
│   │ Node C   ◄──┘                              │    Stat     [Damage      ↓ ]│
│   └──────────┘                                 │    Per Rank [0.10        ] │
│                                                │    Type     [PercentBonus↓]│
│   (port'u sürükle → başka node'a bırak         │    Max Rank [5           ] │
│    = edge/bağlantı oluşur)                     │                             │
│                                                │  COST                       │
│                                                │    Base Cost [100        ] │
│                                                │    Scale     [1.5        ] │
│                                                │                             │
│                                                │  UNLOCK                     │
│                                                │    Prestige Gate [0      ] │
│                                                │    Selection W.  [50     ] │
│                                                │                             │
│                                                │  LAYOUT                     │
│                                                │    Grid X [10] Grid Y [7 ] │
│                                                │                             │
│                                                │  ICON                       │
│                                                │  [  ♥  ★  ⚡  ⚔  🛡  ...  ] │
│                                                │  Unicode: [               ] │
└────────────────────────────────────────────────┴─────────────────────────────┘
```

### Adım adım kullanım

**Yeni upgrade tree oluştururken:**

1. **New Tree** → kayıt yeri seç
2. **+ Add Node** ile boş bir node ekle (canvas ortasına yerleşir)
3. Node'u tıkla → sağ inspector açılır

**Her node için doldurulması gereken alanlar:**

| Alan | Açıklama | Önemli not |
|---|---|---|
| **Node ID** | Kayıt anahtarı — benzersiz, değişmez | `prod_gen_speed_1` gibi prefix kullan |
| **Display Name** | UI'da gösterilen isim | Kısa tut (<20 karakter) |
| **Description** | Tooltip metni | Efekti sayısal olarak yaz |
| **Category** | Tab gruplandırması | Production / Combat / Survival / Economy / Prestige |
| **Affected Stat** | Hangi stat'ı etkiliyor | Enum değeri |
| **Effect Per Rank** | Her rank'ta eklenen değer | Flat ise +5, Percent ise 0.05 (=%5) |
| **Effect Type** | FlatBonus veya PercentBonus | — |
| **Max Rank** | Kaç kez satın alınabilir | 1–10 arası önerilir |
| **Base Cost** | İlk rank maliyeti | — |
| **Cost Scaling Factor** | Her rank'ta maliyet çarpanı | 1.5 = %50 artış |
| **Prestige Gate** | Kaç prestige gerekiyor | 0 = her zaman açık |
| **Selection Weight** | Kart çekme havuzunda ağırlık | 1–100 arası |
| **Grid X / Grid Y** | Canvas'taki kolon/satır konumu | 0-based, çakışma olmasın |
| **Icon Unicode** | Font Awesome 6 Solid kodu | Boş bırak → stat'a göre varsayılan |

**Bağlantı (edge) kurmak:**

1. Source node'un sağ alt köşesindeki **output port** (●) üzerine git
2. Sol tuşu basılı tut, hedef node'un **input port** üzerine sürükle
3. Bırak → gri çizgi oluşur
4. Bu bağlantı "hedef node için önkoşul" anlamına gelir
5. Inspector'daki **Prerequisite Node IDs** alanı otomatik güncellenir

**Bağlantıyı silmek:** Edge'e tıkla → Delete tuşu

**Bağlantı kısıtı (MaxOutgoingEdges):**
- Inspector'da `Max Outgoing Edges = 0` ise sınırsız çıkış
- `Max Outgoing Edges = 2` ise o node'dan en fazla 2 bağlantı çıkabilir
- Bu kısıt `GetCompatiblePorts` içinde uygulanır

**Progressive Reveal:**
- Toolbar'daki toggle ile tüm tree genelinde açılır
- Açıkken: `HideUntilUnlockable = true` olan node'lar, tüm önkoşulları satın alınana kadar UI'da görünmez
- Kapalıken: tüm node'lar her zaman görünür (kilitli de olsa)

**Frame All:** Tüm node'ları viewport'a sığdırır. İlk açılışta veya kayboldu sanıyorsan kullan.

**Dikkat edilmesi gerekenler:**
- Node ID'yi ship ettikten sonra **asla değiştirme** — save dosyası bu ID ile saklar
- Grid X/Y çakışması olursa node'lar üst üste biner — her node için unique konum kullan
- Döngüsel bağlantı oluşturmayın (A→B→A) — UpgradeTreeService'i kilitler
- Canvas çok dolunca Frame All ile hepsini gör, sonra zoom in/out

---

## 4. Economy Tuning

**Menü:** `Tools → Endless Engine → Economy Tuning`

### Ne yapar

Oyunun tüm ekonomi parametrelerini görsel grafikler, tablolar ve
cross-system simülasyon ile analiz eder. 6 sekmesi vardır.

### Gerekli asset'lar

Tüm asset'lar opsiyonel — eksik olanlar o sekmenin ilgili kısmını devre dışı bırakır.
Projede tekse otomatik yüklenir, birden fazlaysa toolbar'dan seç.

| Asset | İlgili sekme(ler) |
|---|---|
| `EconomyConfigSO` | Gold Curve, Wave Economy, Simulation, Config Editor |
| `PrestigeConfigSO` | Gold Curve, Prestige, Simulation, Config Editor |
| `GeneratorDatabaseSO` | Gold Curve, Gen Costs, Simulation |
| `WaveConfigSO` | Wave Economy, Simulation, Config Editor |
| `RunConfigSO` | Gold Curve (run chip'leri), Simulation, Config Editor |
| `PlayerBaseStatConfigSO` | Config Editor |

### Sekme: Gold Curve

Generator kopyaları arttıkça toplam idle yield/s eğrisini gösterir.
**4 çizgi** = Prestige 0, 1, 3, 5 seviyelerindeki gelir.

- X ekseni: her generator tipinden kaç kopya var
- Y ekseni: toplam gold/s
- Doldurulmuş alan her prestige seviyesinin katkısını gösterir
- Altta **yield katkı tablosu**: her generator için ×1/×10/×50 kopya çıktısı

**"Max copies per gen" alanını** değiştirerek grafiğin x aralığını ayarla.

**Nasıl kullanılır:**
- Eğri düz gidiyorsa generator yield değerleri çok düşük veya çok yüksek
- P0 ve P5 çizgileri arasındaki fark prestige'in ne kadar etkili olduğunu gösterir
- Eğri erken plateau yapıyorsa generator eğimi çok sığ

---

### Sekme: Generator Costs

Her generator için ayrı bir maliyet bar chart'ı ve karşılaştırma tablosu.

- Bar chart: kopya #1'den #N'e kadar maliyet (mavi = ucuz, turuncu = pahalı)
- Alt tablo: her generator'ın #1, #5, #10, #20 kopyasının maliyeti ve toplam ×10 maliyet

**"Copies to show" alanını** değiştirerek bar sayısını ayarla (2–50).

**Nasıl kullanılır:**
- Bar grafiği neredeyse düz ise scale factor çok düşük → çok hızlı satın alınır
- Dik eğim ise çok pahalılaşıyor → oyuncular belirli bir noktada durur
- `×10 total` sütunu: oyuncunun ilk 10 kopya için harcadığı toplam gold — bütçe dengesi için kritik

---

### Sekme: Prestige

Prestige çarpan eğrisi ve threshold tablosu.

- Sarı çizgi: `Min(Cap, BaseMultiplier^N)` formülü
- Kırmızı yatay çizgi: cap değeri (bu noktada eğri yataylaşır)
- Üstte mavi chip'ler: base, cap, hard limit, min wave değerleri
- Kaçıncı prestijde cap'e çarpıldığı otomatik hesaplanır ve uyarı olarak gösterilir

**Tablo sütunları:**
- `#` — prestige sayısı
- `Multiplier` — o andaki çarpan (sarı: normal, kırmızı: cap'e geldi)
- `+delta` — bir önceki prestijden fark (yeşil)
- `Min wave` — o prestige'e ulaşmak için gereken minimum wave
- `Idle/s` — bu çarpanla base idle yield/s değeri
- `Enemy drop` — bu çarpanla base enemy gold drop

**PrestigeConfigSO yoksa:**
Sekme uyarı + **"Create PrestigeConfigSO"** butonu gösterir. Butona tıklayınca
`Assets/Configs/PrestigeConfig.asset` varsayılan değerlerle oluşturulur.

---

### Sekme: Wave Economy

Wave ilerledikçe düşman sayısı ve gold drop'u gösteren tablo.

| Sütun | Açıklama |
|---|---|
| Wave | Wave numarası (seçili örnekler) |
| Enemies | O wave'deki düşman sayısı (cap uygulanmış) |
| Type | Normal / ELITE (sarı) / BOSS (kırmızı) |
| Drop/enemy | Düşman başına gold (GoldDropScalingExponent^wave) |
| Total drop | O wave'in toplam gold'u |
| ×run bonus | Run sırasında ActiveRunEnemyGoldMultiplier uygulanmış değer |
| Upgrade? | Bu wave'de upgrade seçimi tetikleniyor mu |
| Save? | Bu wave'de otosave tetikleniyor mu |

**Nasıl kullanılır:**
- Wave 1 gold drop'u ile wave 20 arasındaki fark makul mu?
- Elite ve boss wave'lerindeki gold spike dengeli mi?
- Upgrade interval'i (her 3 wave) çok sık mı, çok seyrek mi?

---

### Sekme: Simulation

**En önemli sekme.** Player durumunu simüle ederek tüm sistemlerin birlikte çıktısını gösterir.

**Input alanları:**

| Alan | Ne anlama gelir |
|---|---|
| Prestige count | Oyuncunun toplam prestige sayısı |
| Wave reached | Oyuncunun ulaştığı wave (drop hesabı için) |
| Gen copies (each) | Her generator tipinden kaç kopya var |
| Offline hours | Kaç saat offline kaldı |
| Active run active | Run modifier uygulansın mı |

**Çıktı kartları:**

| Kart | Hesaplama |
|---|---|
| Idle yield/s | `BaseRate × PrestigeMult + Σ(gen.Yield × copies × PrestigeMult)` |
| Run passive yield/s | `Idle × RunConfig.ActiveRunPassiveModifier` |
| Offline credit | `Idle × min(offlineHrs, capHrs) × 3600` |
| Gold/wave (this wave) | `EnemyDrop(wave) × enemies × runBonus × PrestigeMult` |
| Total run gold (est.) | `RunPassive × runSecs + Σ wave drops` |

**Prestige readiness panel:**
- Yeşil dot: min wave şartı karşılandı / prestige limiti dolmadı
- Kırmızı dot: şart karşılanmadı
- Bir sonraki prestijdeki multiplier değeri gösterilir

**Nasıl kullanılır:**
1. "Wave 20, Prestige 2, 10 kopya her gen, 4 saat offline" doldur
2. Offline credit makul mu? Çok düşüksa `OfflineCapHours` artır
3. Total run gold, idle'ı domine ediyor mu? Bunlar yakın olmalı veya kasıtlı
4. Prestige 10 ile Prestige 1 arasındaki fark ne kadar?

---

### Sekme: Config Editor

Yüklü tüm SO'ları inline olarak düzenle ve kaydet.

Her SO ayrı bir bölümde gösterilir:
- Üstte: dosya yolu (hangi asset düzenleniyor)
- Ortada: tüm serialized alanlar (Unity'nin standart PropertyField'ları)
- Sağda: **Save** butonu → `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`

**Dikkat:** Değişiklik yapıp Save'e basmadan başka sekmeye geçersen değişiklikler
memory'de kalır ama diske yazılmaz. Her değişiklikten sonra Save'e bas.

**Neden bu sekme var:**
- Grafikleri izlerken değeri değiştirmek için Unity Inspector'ı aramak zaman kaybı
- Buradan değiştir → Gold Curve sekmesine geç → etkiyi hemen gör

---

## 5. Asset Creator'lar (Bu Oyuna Özgü)

> ⚠️ **Bu menü öğeleri toolset değil, endless-engine referans implementasyonuna özgüdür.**
> Başka bir proje için **kullanma** — içerikleri hardcode bu oyunun değerlerini içerir.
> Kendi projen için Generator Editor ve Upgrade Tree Editor kullanarak kendin oluştur.

### Create Starter Generators

**Menü:** `Tools → Endless Engine → Create Starter Generators`

`Assets/Configs/Generators/` klasöründe 5 hazır generator asset'ı oluşturur:

| ID | İsim | Yield/s | Base Cost | Scale |
|---|---|---|---|---|
| `gen_basic` | Basic Producer | 1 | 100 | 1.15 |
| `gen_mine` | Gold Mine | 5 | 500 | 1.15 |
| `gen_factory` | Gold Factory | 25 | 2.500 | 1.15 |
| `gen_vault` | Vault Network | 100 | 10.000 | 1.15 |
| `gen_black_hole` | Black Hole | 500 | 50.000 | 1.15 |

Ayrıca `GeneratorDatabase.asset` oluşturur ve 5 generator'ı sırayla ekler.
Zaten varsa mevcut asset'lar atlanır (üzerine yazmaz).

**Çalıştırmadan önce:**
- `Assets/Configs/` klasörünün var olduğundan emin ol
- Bootstrap Inspector'da **GeneratorDatabase** referansını bu asset'a ata

### Create Upgrade Tree

**Menü:** `Tools → Endless Engine → Create Upgrade Tree`

`Assets/Configs/UpgradeTreeConfig.asset` oluşturur.
85 node, 5 dal (Production / Combat / Economy / Survival / Prestige) içerir.
Asset zaten varsa işlem yapmaz — Rebuild kullan.

### Rebuild Upgrade Tree

**Menü:** `Tools → Endless Engine → Rebuild Upgrade Tree`

Mevcut `UpgradeTreeConfig.asset`'i bu oyunun 85 node'lu ağacıyla **sıfırdan yazar**.
El ile yapılan değişiklikler kaybolur.

**Ne zaman kullan:** Upgrade Tree Editor'da yaptığın değişiklikler bozulduysa
veya toolset kaynak kodunda node tanımları güncellendiyse.

---

## Hızlı Başlangıç Rehberi

### Yeni bir idle oyunu kuruyorum

```
1. GeneratorDatabaseSO oluştur
   → Generator Editor → New Database

2. Generator'ları ekle
   → Generator Editor → + Add (her generator için)
   → ID, Yield, Cost, Scale doldur
   → Save

3. UpgradeTreeConfigSO oluştur
   → Upgrade Tree Editor → New Tree

4. Node'ları ekle
   → + Add Node → inspector'da doldur
   → Port'ları sürükle bağlantı kur
   → Save

5. EconomyConfigSO, PrestigeConfigSO oluştur
   → Project sağ tık → Create → Endless Engine → Config → Economy / Prestige

6. Economy Tuning'i aç
   → Asset'lar otomatik yüklenir
   → Gold Curve / Simulation sekmelerini kontrol et
   → Config Editor'da değerleri ayarla → Gold Curve'e geri dön → tekrarla
```

### Generator balance nasıl ayarlanır

1. Economy Tuning → **Gen Costs** sekmesini aç
2. İlk generator'ın ×10 total maliyet değerine bak
3. Bu değer oyuncunun 2–5 dakika idle kazanımına eşit olmalı
4. Economy Tuning → **Simulation** → 5 dakika idle kazanımı ne?
5. Gen Costs → total maliyet bu değerin 2–5 katı arası ideal

### Prestige dengesi nasıl ayarlanır

1. Economy Tuning → **Prestige** sekmesi
2. Cap'e kaç prestijde ulaşılıyor? 10–20 arası ideal
3. Her prestijin delta'sı (yeşil sütun) yeterince büyük mü? Küçülüyorsa BaseMultiplier artır
4. **Simulation** → Prestige 1 vs Prestige 5 arasındaki idle/s farkı ne kadar?

---

## Dosya Yapısı

```
Assets/
├── Editor/
│   ├── GeneratorEditorWindow.cs      ← Generator Editor (toolset)
│   ├── UpgradeTreeEditorWindow.cs    ← Upgrade Tree Editor (toolset)
│   ├── UpgradeTreeEditor.uss         ← Upgrade Tree Editor stili
│   ├── EconomyTuningWindow.cs        ← Economy Tuning (toolset)
│   ├── GeneratorAssetCreator.cs      ← Bu oyuna özgü — starter gen'ler
│   ├── UpgradeTreeAssetCreator.cs    ← Bu oyuna özgü — 85 node ağaç
│   └── EndlessEngine.Editor.asmdef   ← Editor assembly tanımı
└── Scripts/Runtime/
    ├── Config/ScriptableObjects/
    │   ├── GeneratorConfigSO.cs       ← Tekli generator tanımı
    │   ├── GeneratorDatabaseSO.cs     ← Generator koleksiyonu
    │   ├── UpgradeTreeConfigSO.cs     ← Upgrade ağacı + node tanımları
    │   ├── EconomyConfigSO.cs         ← Ekonomi parametreleri
    │   ├── PrestigeConfigSO.cs        ← Prestige parametreleri
    │   ├── WaveConfigSO.cs            ← Wave parametreleri
    │   └── RunConfigSO.cs             ← Run parametreleri
    └── Modules/
        ├── CursorYieldService.cs      ← Cursor activity modülü
        ├── ClickYieldService.cs       ← Click-to-produce modülü
        └── ZoneSystem.cs              ← Zone/region modülü
```

---

## Sık Sorulan Sorular

**S: Generator ID'yi değiştirdim, save bozuldu.**
C: Save dosyası eski ID ile sakladığı için yeni ID'yi bulamıyor. Migration yaz:
`SaveData` şemasını güncelle, `IMigration` zinciriyle eski ID → yeni ID map'le.

**S: Upgrade Tree Editor'da edge'ler görünmüyor.**
C: `EndlessEngine.Editor.asmdef` içinde `Unity.GraphTools.Foundation` referansı varsa kaldır.
Bu paket GraphView'un internal edge renderer'ını override ediyor.

**S: Economy Tuning Prestige sekmesi "No PrestigeConfigSO loaded" diyor.**
C: `Assets/Configs/PrestigeConfig.asset` yoktur. Sekmedeki **Create** butonuna bas.
Veya toolbar'da **Prestige** butonuna tıklayıp dosyayı seç.

**S: Generator Editor'da "+" ekledikten sonra asset oluşmuyor.**
C: GeneratorDatabase yüklü değil. Önce **Load Database** veya **New Database**.

**S: Simulation'daki "Offline credit" çok yüksek görünüyor.**
C: `EconomyConfigSO.OfflineCapHours` değerini düşür. Şu an kaç saat cap'te?
Economy Tuning → Config Editor → Economy Config → Offline Cap Hours.
