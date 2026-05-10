# Endless Engine — Hızlı Başlangıç

5 dakikada çalışan bir idle oyun.

---

## Yol 1: Sıfırdan Yeni Oyun (En Kolay)

### Adım 1 — Sihirbazı Aç

Unity'de: **Tools → Endless Engine → New Game Wizard**

### Adım 2 — Oyun Adı ve Türünü Seç

- **Game Name**: örn. `MyIdleGame`
- **Game Type**: `Pure Idle` (sadece generator + prestige — en basit)
- Diğer türler için açıklama sihirbazda görünür

### Adım 3 — Generate

**"Generate Skeleton"** butonuna tıkla.

Sihirbaz şunları oluşturur:
- `Assets/MyIdleGame/Configs/` → tüm ayar dosyaları (EconomyConfig, GeneratorDatabase, vb.)
- `Assets/MyIdleGame/Scenes/MyIdleGame.unity` → tam kurulmuş, oynanabilir sahne

### Adım 4 — Oyna

`Assets/MyIdleGame/Scenes/MyIdleGame.unity` dosyasını aç.  
**Play** bas.  
Altın birikiyor.

---

## Yol 2: Hazır Sample (Hemen Dene)

### Adım 1 — Sample'ı İçe Aktar

**Window → Package Manager → Endless Engine → Samples → MinimalIdle → Import**

### Adım 2 — Sahneyi Aç

`Assets/Samples/EndlessEngine/[versiyon]/MinimalIdle/Scenes/MinimalIdle.unity`

### Adım 3 — Oyna

**Play** bas. Hepsi bu.

Altın otomatik birikiyor. Mine satın al, oran artar. Upgrade al, oran 2 katına çıkar. Oyunu kapatıp açtığında kayıt yüklenir.

---

## Sistemi Genişletme

### Generator Ekle (Pasif Gelir)

1. **Project** penceresinde sağ tıkla → **Create → Endless Engine → Config → Generator Config**
2. Ayarla: `GeneratorId`, `DisplayName`, `BaseYieldPerSecond`, `BaseCost`
3. `GeneratorDatabase.asset` içine sürükle

### Upgrade Ekle (Gelişme)

1. **Create → Endless Engine → Config → Upgrade Node**
2. Ayarla: `NodeId`, `DisplayName`, `BaseCost`, `EffectPerRank`, `AffectedStat`
3. Bootstrap'ta `UpgradeNodes[]` dizisine ekle

### Prestige Ekle (Derin İlerleme)

1. **Create → Endless Engine → Config → Prestige**  
2. Ayarla: `MinGoldToPrestige`, `BaseMultiplierPerPrestige`
3. Bootstrap objesine `PrestigeStateManager` component'i ekle
4. UI'da `PrestigeStateManager.TryPrestige()` çağır

---

## Bootstrap Nasıl Çalışır?

`AutoSetupBootstrap` component'i:

1. Aynı GameObject'e tüm servisleri otomatik ekler (EconomyService, GeneratorSystem, vb.)
2. Config SO'larını bağlar
3. Save/Load başlatır

**Tek yapman gereken**: Config SO'larını Inspector'dan atamak.

```
Bootstrap (GameObject)
  └── AutoSetupBootstrap (Component)
        ├── EconomyConfig → EconomyConfig.asset
        ├── GeneratorDatabase → GeneratorDatabase.asset
        └── SchemaVersion → SchemaVersion.asset
```

---

## Sistemler Arası İletişim

Tüm sistemler static event'ler üzerinden haberleşir:

```csharp
// Altın değişince UI güncelle
EconomyService.OnResourcesChanged += (current, delta) => {
    goldLabel.text = current.ToString("N0");
};

// Generator satın alınınca ses çal
GeneratorSystem.OnGeneratorPurchased += id => {
    AudioSource.PlayClipAtPoint(purchaseClip, Vector3.zero);
};

// Dalga başlayınca kamera sars
WaveSpawnManager.OnWaveStarted += wave => {
    StartCoroutine(ShakeCamera());
};
```

**Tüm event'ler statik** — herhangi bir MonoBehaviour'dan subscribe edebilirsin.

---

## Sorun Giderme

| Sorun | Çözüm |
|-------|-------|
| "Gold: 0" ve artmıyor | `GeneratorDatabase`'te en az 1 generator var mı? |
| `ConfigNotLoadedException` | `ConfigRegistry.InjectForTesting()` bootstrap'ta çağrıldı mı? |
| Save yüklenmiyor | `RegisterStateProvider()` çağrıları `LoadAsync()`'den önce mi? |
| Upgrade butonu çalışmıyor | `UpgradeTreeService.HandleConfigsLoaded()` çağrıldı mı? |
| Scene boş görünüyor | `AutoSetupBootstrap` GameObject'e eklendi mi? |

---

## Detaylı Belgeleme

- **`kullanim-kilavuzu-tr.md`** — tüm sistemlerin tam API referansı (58 bölüm)
- **`api-reference.md`** — İngilizce API özeti
- **`cookbook.md`** — 30+ hazır kod tarifi

Her sample'ın kendi `README.md` dosyası da sistemi gösterir.

---

*Endless Engine v1.1.0 — MIT License*
