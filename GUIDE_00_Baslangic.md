# GUIDE_00 — Endless Engine ile Oyun Yapımına Başlangıç Rehberi

> Bu rehber GUIDE_01–06'daki oyun rehberlerinden **önce** okunmalıdır.  
> Unity'nin temel kullanımını (sahne oluşturma, GameObject, Component, Inspector)  
> bildiğinizi varsayar. Endless Engine paketine özgü kurulum, evrensel sistemler  
> (menü, settings, build, Steam) ve kapsamlı troubleshooting burada ele alınır.

---

## İçindekiler

1. [Package Kurulumu — Scoped Registry](#1-package-kurulumu)
2. [İlk Proje Kurulumu](#2-ilk-proje-kurulumu)
3. [Prefab Instantiate Kullanımı](#3-prefab-instantiate-kullanımı)
4. [Ana Menü Sistemi](#4-ana-menü-sistemi)
5. [Settings Screen](#5-settings-screen)
6. [Pause Screen](#6-pause-screen)
7. [Gerçek Oyun Döngüsü Tasarımı](#7-gerçek-oyun-döngüsü-tasarımı)
8. [Build Alma — Windows ve Mac](#8-build-alma)
9. [Steam SDK Entegrasyonu](#9-steam-sdk-entegrasyonu)
10. [Kapsamlı Troubleshooting](#10-kapsamlı-troubleshooting)

---

## 1. Package Kurulumu

### 1.1 Scoped Registry Ekleme

Endless Engine, Unity'nin varsayılan Package Registry'sinde değildir. Kendi registry'sinden kurulur.

**Adımlar:**

1. Unity'yi açın, üst menüden **Edit → Project Settings** açın
2. Sol panelden **Package Manager** seçin
3. **Scoped Registries** bölümünde **+** butonuna tıklayın
4. Şu bilgileri girin:

```
Name:    Endless Engine
URL:     https://registry.endlessengine.com
Scopes:  com.endlessengine
```

5. **Save** butonuna tıklayın
6. Project Settings penceresini kapatın

### 1.2 Paketi Yükleme

1. Üst menüden **Window → Package Manager** açın
2. Sol üstteki dropdown'dan **My Registries** seçin
3. Listede **Endless Engine Idle** paketini bulun
4. Sağ alttaki **Install** butonuna tıklayın
5. Kurulum tamamlandığında `Packages/com.endlessengine.idle/` klasörü oluşur

### 1.3 Bağımlılıklar

Endless Engine şu paketlere ihtiyaç duyar — genellikle otomatik kurulur, kurulmadıysa Package Manager'dan manuel ekleyin:

| Paket | Minimum Versiyon |
|---|---|
| TextMeshPro | 3.0.6 |
| Unity UI | 1.0.0 |
| Newtonsoft Json | 3.2.1 |

**TextMeshPro Essential Resources kurulumu (ilk kullanımda sorulur):**  
Window → TextMeshPro → Import TMP Essential Resources → Import

---

## 2. İlk Proje Kurulumu

### 2.1 Önerilen Klasör Yapısı

```
Assets/
├── _Game/
│   ├── Configs/          ← ScriptableObject dosyaları
│   │   ├── Economy/
│   │   ├── Generators/
│   │   ├── Upgrades/
│   │   └── Prestige/
│   ├── Scripts/
│   │   ├── Bootstrap/
│   │   ├── UI/
│   │   └── Gameplay/
│   ├── Prefabs/
│   │   ├── UI/
│   │   └── Gameplay/
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   └── Game.unity
│   └── Art/
│       ├── Sprites/
│       └── Fonts/
```

Assets klasöründe sağ tık → **Create → Folder** ile bu yapıyı oluşturun.

### 2.2 Build Settings'e Sahneleri Ekleme

1. **File → Build Settings** açın
2. Her sahneyi **Add Open Scenes** ile veya sürükleyerek ekleyin
3. Sıralama şöyle olmalı:
   - Index 0: `MainMenu`
   - Index 1: `Game`

Bu sıra `SceneManager.LoadScene(1)` gibi çağrılar için kritiktir.

### 2.3 ScriptableObject Oluşturma (Genel Yöntem)

Her rehberde "şu SO'yu oluştur" denildiğinde bu yöntemi kullanın:

1. Project panelinde hedef klasöre gidin (örn. `Assets/_Game/Configs/Economy/`)
2. Sağ tık → **Create** → Endless Engine menüsünden ilgili tipi seçin
3. Oluşan dosyaya anlamlı bir isim verin (örn. `EconomyConfig_MyGame`)
4. Inspector'da field'ları doldurun

> **Not:** Bazı SO tipleri Create menüsünde görünmeyebilir. Bu durumda Project panelinde  
> sağ tık → **Create → ScriptableObject** yerine script'i seçmeniz gerekebilir.  
> Troubleshooting bölümü 10.1'e bakın.

---

## 3. Prefab Instantiate Kullanımı

GUIDE_01–06'daki rehberlerde sıkça `Instantiate` ile dinamik olarak UI kartları veya  
gameplay nesneleri oluşturulur. Temel pattern şudur:

### 3.1 Prefab Hazırlama

1. Sahneye bir GameObject yerleştirin ve istediğiniz Component'leri ekleyin
2. Bu GameObject'i Project panelindeki `Prefabs/` klasörüne **sürükleyin**
3. Sahmedeki kopya artık bir prefab instance'ıdır — silebilirsiniz (sahnede kalmasına gerek yok)

### 3.2 Script'ten Instantiate

```csharp
[SerializeField] private GeneratorCard _generatorCardPrefab; // Inspector'dan bağlanır
[SerializeField] private Transform _cardContainer;           // ScrollView > Content gibi

private void SpawnGeneratorCards()
{
    foreach (var generatorConfig in _generators)
    {
        GeneratorCard card = Instantiate(_generatorCardPrefab, _cardContainer);
        card.Initialize(generatorConfig);
    }
}
```

**Inspector'da bağlama:**
1. Script'in bulunduğu GameObject'i seçin
2. Inspector'da `_generatorCardPrefab` alanına Project panelinden prefab'ı **sürükleyin**
3. `_cardContainer` alanına sahnedeki ScrollView → Viewport → Content nesnesini sürükleyin

### 3.3 Instantiate Sonrası Temizlik

Listeyi yeniden oluşturmadan önce eski kartları silmek için:

```csharp
private void ClearCards()
{
    // Container içindeki tüm child nesneleri sil
    foreach (Transform child in _cardContainer)
    {
        Destroy(child.gameObject);
    }
}
```

### 3.4 Object Pooling (Performans için)

Çok fazla nesne oluşturup siliyorsanız (örn. düşman spawn, floating text) her Instantiate/Destroy  
bir GC yükü oluşturur. Basit bir pool:

```csharp
public class SimplePool<T> where T : Component
{
    private readonly T _prefab;
    private readonly Transform _parent;
    private readonly Queue<T> _pool = new();

    public SimplePool(T prefab, Transform parent, int preload = 10)
    {
        _prefab = prefab;
        _parent = parent;
        for (int i = 0; i < preload; i++)
        {
            var obj = Object.Instantiate(prefab, parent);
            obj.gameObject.SetActive(false);
            _pool.Enqueue(obj);
        }
    }

    public T Get()
    {
        if (_pool.Count > 0)
        {
            var obj = _pool.Dequeue();
            obj.gameObject.SetActive(true);
            return obj;
        }
        return Object.Instantiate(_prefab, _parent);
    }

    public void Return(T obj)
    {
        obj.gameObject.SetActive(false);
        _pool.Enqueue(obj);
    }
}
```

---

## 4. Ana Menü Sistemi

### 4.1 Sahne Yapısı

`MainMenu` sahnesini açın ve şu hiyerarşiyi oluşturun:

```
MainMenu (Scene)
└── Canvas (Screen Space - Overlay, Sort Order: 0)
    ├── Background
    │   └── Image (oyun logosu veya arkaplan görseli)
    ├── TitleText (TextMeshPro)
    ├── ButtonPanel
    │   ├── PlayButton (Button + TextMeshPro)
    │   ├── SettingsButton (Button + TextMeshPro)
    │   └── QuitButton (Button + TextMeshPro)
    └── VersionText (TextMeshPro, sağ alt köşe)
```

### 4.2 MainMenuController.cs

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private TMP_Text _versionText;
    [SerializeField] private GameObject _settingsPanel;

    private void Start()
    {
        _versionText.text = $"v{Application.version}";
        _settingsPanel.SetActive(false);

        // Kayıt var mı kontrol et — varsa "Devam Et" butonu göster
        // SaveService burada çağrılmaz, sadece dosya varlığı kontrol edilir
        bool hasSave = System.IO.File.Exists(
            System.IO.Path.Combine(Application.persistentDataPath, "save.json"));
        // hasSave değerine göre ContinueButton'ı aktif/pasif yapabilirsiniz
    }

    public void OnPlayClicked()
    {
        SceneManager.LoadScene("Game");
    }

    public void OnSettingsClicked()
    {
        _settingsPanel.SetActive(true);
    }

    public void OnQuitClicked()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}
```

**Inspector bağlantıları:**
1. `Canvas` → sağ tık → Add Component → `MainMenuController`
2. `_versionText` → `VersionText` nesnesini sürükle
3. `_settingsPanel` → Settings panelini sürükle (Section 5'te yapılacak)
4. `PlayButton` OnClick → `MainMenuController.OnPlayClicked`
5. `SettingsButton` OnClick → `MainMenuController.OnSettingsClicked`
6. `QuitButton` OnClick → `MainMenuController.OnQuitClicked`

### 4.3 Sahne Geçiş Animasyonu (Opsiyonel ama önerilir)

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }
    
    [SerializeField] private Image _fadeImage;
    [SerializeField] private float _fadeDuration = 0.5f;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        // Karart
        float t = 0;
        Color c = Color.black;
        while (t < _fadeDuration)
        {
            t += Time.deltaTime;
            c.a = t / _fadeDuration;
            _fadeImage.color = c;
            yield return null;
        }

        yield return SceneManager.LoadSceneAsync(sceneName);

        // Aç
        t = 0;
        while (t < _fadeDuration)
        {
            t += Time.deltaTime;
            c.a = 1f - (t / _fadeDuration);
            _fadeImage.color = c;
            yield return null;
        }
    }
}
```

`OnPlayClicked` içinde `SceneManager.LoadScene("Game")` yerine  
`SceneTransition.Instance.LoadScene("Game")` kullanın.

---

## 5. Settings Screen

### 5.1 UI Yapısı

Settings panelini `MainMenu` sahnesi Canvas'ına ve `Game` sahnesi Canvas'ına ayrı ayrı ekleyin:

```
SettingsPanel (Panel, başlangıçta SetActive(false))
├── Title (TextMeshPro "Ayarlar")
├── MusicSlider (Slider, 0–1)
├── SfxSlider (Slider, 0–1)
├── MusicLabel (TextMeshPro)
├── SfxLabel (TextMeshPro)
├── LanguageDropdown (TMP_Dropdown) ← opsiyonel
└── CloseButton (Button)
```

### 5.2 SettingsManager.cs

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private TMP_Text _musicLabel;
    [SerializeField] private TMP_Text _sfxLabel;

    private const string MusicKey = "MusicVolume";
    private const string SfxKey = "SfxVolume";

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        float music = PlayerPrefs.GetFloat(MusicKey, 0.8f);
        float sfx = PlayerPrefs.GetFloat(SfxKey, 1f);

        _musicSlider.value = music;
        _sfxSlider.value = sfx;

        ApplyMusic(music);
        ApplySfx(sfx);

        _musicSlider.onValueChanged.AddListener(OnMusicChanged);
        _sfxSlider.onValueChanged.AddListener(OnSfxChanged);

        _panel.SetActive(false);
    }

    private void OnMusicChanged(float value)
    {
        PlayerPrefs.SetFloat(MusicKey, value);
        ApplyMusic(value);
        _musicLabel.text = $"Müzik: {Mathf.RoundToInt(value * 100)}%";
    }

    private void OnSfxChanged(float value)
    {
        PlayerPrefs.SetFloat(SfxKey, value);
        ApplySfx(value);
        _sfxLabel.text = $"Ses: {Mathf.RoundToInt(value * 100)}%";
    }

    private void ApplyMusic(float value)
    {
        // AudioMixer kullanıyorsanız:
        // _audioMixer.SetFloat("MusicVolume", Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f);
        // Basit AudioSource için:
        AudioListener.volume = value;
    }

    private void ApplySfx(float value)
    {
        // SFX AudioSource referanslarına volume set edin
    }

    public void Open() => _panel.SetActive(true);
    public void Close() => _panel.SetActive(false);
}
```

**Inspector bağlantıları:**
- `SettingsManager` script'ini DontDestroyOnLoad için ayrı bir `_Managers` GameObject'e ekleyin
- `CloseButton` OnClick → `SettingsManager.Close`
- Oyun sahnesindeki settings butonuna → `SettingsManager.Instance.Open()` çağrısı yapın

---

## 6. Pause Screen

### 6.1 UI Yapısı

`Game` sahnesinin Canvas'ına ekleyin:

```
PausePanel (Panel, başlangıçta SetActive(false))
├── Title (TextMeshPro "Duraklatıldı")
├── ResumeButton
├── SettingsButton
├── MainMenuButton
└── QuitButton
```

### 6.2 PauseController.cs

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseController : MonoBehaviour
{
    public static PauseController Instance { get; private set; }

    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private GameObject _gameUI; // Ana oyun UI'ı

    public bool IsPaused { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Toggle();
    }

    public void Toggle()
    {
        if (IsPaused) Resume(); else Pause();
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        _pausePanel.SetActive(true);
        _gameUI.SetActive(false);
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        _pausePanel.SetActive(false);
        _gameUI.SetActive(true);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        // Önce kaydet
        // SaveService.Instance?.SaveAsync();
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        // Önce kaydet
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}
```

> **Önemli:** `Time.timeScale = 0f` tüm animasyonları ve fizik hesaplamalarını durdurur.  
> Endless Engine'in `TickEngine` sistemi `Time.timeScale`'e duyarlıdır — pause açıkken  
> generator geliri otomatik durur. Bunu istemiyorsanız (idle game'lerde nadiren istenmez)  
> TickEngine'i manuel durdurun: `TickEngine.SetPaused(true)`.

**Inspector bağlantıları:**
- `ResumeButton` OnClick → `PauseController.Resume`
- `SettingsButton` OnClick → `SettingsManager.Instance.Open`
- `MainMenuButton` OnClick → `PauseController.GoToMainMenu`
- `QuitButton` OnClick → `PauseController.QuitGame`

---

## 7. Gerçek Oyun Döngüsü Tasarımı

Bu bölüm en kritik bölümdür. Mekanik çalışan ama sıkıcı bir oyun ile insanların  
100 saat oynadığı bir oyun arasındaki fark tamamen burada.

### 7.1 Progression Eğrisi — Temel İlke

Bir idle oyunda oyuncunun her üretim katlaması için harcadığı zaman **sabit veya artmalı** olmalı:

```
İlk 100 gelir:     30 saniye
100→1.000:          2 dakika
1.000→10.000:       10 dakika
10.000→100.000:     45 dakika
100K→1M:            3 saat
1M→10M:             8 saat  ← prestige/ascension eşiği buraya yakın olmalı
```

Bu eğriyi kontrol eden şey **upgrade maliyetlerinin çarpanı**:

```
Maliyet(n) = BaseCost * GrowthRate^n
```

| GrowthRate | Sonuç |
|---|---|
| 1.05 | Çok yavaş büyüme, oyun durur |
| 1.07 | İdeal başlangıç noktası |
| 1.10 | Agresif, prestij daha erken gelir |
| 1.15 | Çok agresif, erken game çöker |

`GeneratorConfigSO.CostGrowthRate` bu değeri kontrol eder. **1.07 ile başlayın.**

### 7.2 Early / Mid / Late Game Sınırları

**Early Game (İlk 30 dakika):**
- Oyuncu her 30 saniyede bir yeni bir şey açmalı
- Generator 1–3 erişilebilir, upgrade'ler ucuz
- "Ah bu kadar mı kolaymış" hissi kasıtlıdır — bağımlılık buradan başlar
- Prestige henüz görünmez veya kilitli

**Mid Game (30dk–5 saat):**
- Generator 4–7 açılır
- İlk prestige burada tetiklenir
- Prestige sonrası oyun daha hızlı başlar — bu "güçlenme hissi" critical
- Research tree veya skill tree açılır

**Late Game (5 saat+):**
- Generator 8–10
- Ascension / üst katman prestige
- Çarpanlar artık sayısal değil, konseptsel (notasyon değişir: K → M → B → T → Qa)
- Oyuncu "bir sonraki hedefi" her zaman görmeli ama asla kolayca ulaşamamalı

### 7.3 "One More Click" Hissini Yaratmak

İdeal idle oyunda oyuncu şu döngüyü hisseder:

```
Bir şey satın al → Anında fark edilir güçlenme → Bir sonraki hedef görünür → Satın al
```

Bunu sağlamak için:

1. **Milestone ödülleri**: Her 10, 25, 50, 100, 200, ... alımda görsel efekt + %50 bonus
   ```csharp
   // GeneratorConfigSO'da MilestoneThresholds = [10, 25, 50, 100, 200, 400]
   // MilestoneMultiplier = 1.5f
   ```

2. **"Neredeyse aldım" göstergesi**: Bir sonraki upgrade'in maliyetini ve mevcut bakiyeyi  
   progress bar ile gösterin. Oyuncu %80'e geldiğinde durduramaz kendini.

3. **Prestige öncesi uyarı**: "X daha biriktirirsen prestige çarpanın 2.5x olacak"  
   mesajı oyuncuyu birkaç dakika daha bekletir.

### 7.4 Sayı Notasyonu Geçişleri

`BigNumber` formatı `EconomyConfigSO.NumberBackend` ile kontrol edilir.  
Oyuncunun "büyük sayı gördüm" sürprizi her 3–4 saatte bir gelmeli:

```
0 – 999.999      → Normal sayı (999,999)
1.000.000+       → K/M notasyonu (1.00M)
1.000.000.000+   → B (1.00B)
1e12+            → T (1.00T)
1e15+            → Qa
1e18+            → Qi
1e21+            → Sx
```

Her notasyon geçişi oyuncuya küçük bir "ilerliyorum" hissi verir.

### 7.5 Offline Gelir Dengesi

Offline gelir çok yüksek olursa oyuncu aktif oynamayı bırakır.  
Çok düşük olursa geri dönme motivasyonu kalmaz.

**Altın oran:**
```
Offline gelir = Aktif gelirin %25–40'ı (saatte)
Maksimum offline süre = 8–12 saat
```

```csharp
// OfflineCalculatorSO ayarları:
OfflineYieldRate    = 0.30f   // Aktif gelirin %30'u
MaxOfflineHours     = 10f     // 10 saat sonrası birikmiyor
ShowOfflineReport   = true    // Oyuna döndüğünde "8 saatte X kazandın" popup'ı
```

"8 saatte 2.4M altın kazandın!" popup'ı oyuncuyu oyuna geri çeken en güçlü mekanizmadır.

### 7.6 Prestige Zamanlaması

Oyuncu ilk prestige'i **ne zaman yapmalı?**

- Çok erken: Güçlenme hissi yok, oyuncu ne yaptığını anlamaz
- Çok geç: Oyuncu sıkılır, bırakır

**İdeal:** İlk prestige, oyuncu "tamam artık çok yavaşladı" hissine geldiği anda  
mevcut olmalı ve `BaseMultiplierPerTrigger` ile gelen güçlenme **açıkça hissedilmeli**.

```
İlk prestige anı:         Yaklaşık 45dk–2 saat aktif oynama
İlk prestige sonrası:     Aynı noktaya 10–15 dakikada ulaşılmalı
```

Bunu ayarlamak için `PrestigeLayerConfigSO.BaseMultiplierPerTrigger = 1.5` ile başlayın.  
Playtesting sonrası ayarlayın.

---

## 8. Build Alma

### 8.1 Windows Build

1. **File → Build Settings** açın
2. Platform: **PC, Mac & Linux Standalone** seçin (zaten seçiliyse geç)
3. Target Platform: **Windows**
4. Architecture: **x86_64**
5. **Player Settings** açın:
   - Company Name: şirket adınız
   - Product Name: oyun adı (Steam'deki isimle aynı olmalı)
   - Version: `1.0.0`
   - Scripting Backend: **IL2CPP** (daha iyi performans, Steam için önerilir)
   - Api Compatibility Level: **.NET Standard 2.1**
6. Geri dönüp **Build** butonuna tıklayın
7. Çıktı klasörünü seçin (örn. `Builds/Windows/`)

> **IL2CPP için ek gereksinim:** Visual Studio ile "Game Development with Unity"  
> bileşeni kurulu olmalı. Kurulu değilse Unity Hub → Installs → modülü ekleyin.

### 8.2 Mac Build

Mac build için Mac üzerinde çalışmanız gerekir (veya Unity Cloud Build kullanın).  
Windows'tan Mac build alamazsınız.

### 8.3 Build Boyutunu Küçültme

| Ayar | Nerede | Değer |
|---|---|---|
| Texture Compression | Player Settings → Android | ETC2 (platform bağımlı) |
| Managed Stripping Level | Player Settings → Other | Medium |
| Script Debugging | Build Settings | Kapalı (release build'de) |
| Development Build | Build Settings | Kapalı (release build'de) |

Tipik idle oyun build boyutu: **50–150 MB** (grafikler olmadan daha az)

### 8.4 Build Test Checklist

Build aldıktan sonra kontrol edin:
- [ ] Oyun masaüstünden çalışıyor (Unity Editor'siz)
- [ ] Kayıt dosyası oluşuyor ve yükleniyor (`%AppData%\LocalLow\[CompanyName]\[ProductName]\`)
- [ ] Prestige sonrası veri doğru sıfırlanıyor
- [ ] Settings değerleri kayıt sonrası koruyor (PlayerPrefs)
- [ ] Quit tuşu çalışıyor
- [ ] Pencere boyutu ayarları çalışıyor

---

## 9. Steam SDK Entegrasyonu

### 9.1 Gereksinimler

- Steam Yetkili Geliştirici hesabı (100$ ödeme)
- Steamworks.NET paketi: `https://github.com/rlabrecque/Steamworks.NET`
- AppID (Steam store sayfası oluşturulunca verilir)
- Test için AppID yoksa `480` (Spacewar) kullanılır

### 9.2 Steamworks.NET Kurulumu

1. GitHub'dan en son `.unitypackage` dosyasını indirin
2. Unity'de **Assets → Import Package → Custom Package** ile içe aktarın
3. `steam_appid.txt` dosyasını proje kökünde oluşturun (Editor klasörü değil)  
   İçine sadece AppID yazın: `480`
4. Unity'yi yeniden başlatın

### 9.3 SteamManager.cs (Otomatik Gelir)

Steamworks.NET paketi ile birlikte gelen `SteamManager.cs` scripti otomatik olarak  
projeye eklenir. Bu script DontDestroyOnLoad mantığıyla Steam API'yi başlatır.

Sahnenizde bir boş GameObject oluşturun, adını `SteamManager` yapın ve bu scripti ekleyin.

### 9.4 Temel Steam Entegrasyonu

```csharp
using Steamworks;
using UnityEngine;

public class SteamIntegration : MonoBehaviour
{
    private void Start()
    {
        if (!SteamManager.Initialized) return;
        
        string playerName = SteamFriends.GetPersonaName();
        Debug.Log($"Steam'e bağlandı: {playerName}");
    }

    // Oyun tamamlandığında achievement aç
    public static void UnlockAchievement(string achievementId)
    {
        if (!SteamManager.Initialized) return;
        SteamUserStats.SetAchievement(achievementId);
        SteamUserStats.StoreStats();
    }

    // İstatistik kaydet (örn. toplam tıklama sayısı)
    public static void SetStat(string statName, int value)
    {
        if (!SteamManager.Initialized) return;
        SteamUserStats.SetStat(statName, value);
        SteamUserStats.StoreStats();
    }
}
```

### 9.5 Önerilen Achievement Listesi (Idle Oyun için)

| Achievement ID | Tetikleyici |
|---|---|
| `FIRST_PRESTIGE` | İlk prestige yapıldığında |
| `PRESTIGE_10` | 10. prestijde |
| `TOTAL_CLICKS_1000` | 1000 toplam tıklamada |
| `ALL_GENERATORS` | Tüm generatorlar satın alındığında |
| `OFFLINE_HOUR` | 1 saat offline gelir toplandığında |
| `REACH_1B` | 1 milyar birikimde |

Achievement'ları Steamworks dashboard'unda bu ID'lerle tanımlayın.

### 9.6 Build için Hazırlık

1. `steam_appid.txt` içindeki ID'yi gerçek AppID ile değiştirin
2. Build aldıktan sonra `steam_appid.txt` dosyasını `.exe` ile aynı klasöre kopyalayın
3. Steam'den oyunu başlatmayı test edin (direkt .exe değil)

---

## 10. Kapsamlı Troubleshooting

### 10.1 Derleme Hataları

---

#### HATA: `The type or namespace 'EndlessEngine' could not be found`

```
Assets/_Game/Scripts/Bootstrap/MyBootstrap.cs(3,7): error CS0246:
The type or namespace name 'EndlessEngine' could not be found
```

**Neden:** Endless Engine paketi kurulu değil veya asmdef dosyası eksik.  
**Çözüm:**
1. Package Manager'ı açın → `com.endlessengine.idle` kurulu mu kontrol edin
2. Projenizin `.asmdef` dosyası varsa, içine `"com.endlessengine.idle"` referansı ekleyin:
   ```json
   {
     "name": "MyGame",
     "references": ["com.endlessengine.idle"]
   }
   ```
3. `.asmdef` yoksa sorun yok — Unity tüm assembly'leri görür.

---

#### HATA: `Assets does not exist in the namespace`

```
error CS0234: The type or namespace name 'Assets' does not exist
```

**Neden:** `using` satırı yanlış yazılmış.  
**Çözüm:** Dosyanın en üstündeki using satırlarını kontrol edin:
```csharp
// YANLIŞ:
using EndlessEngine.Assets;

// DOĞRU:
using EndlessEngine.Core;
using EndlessEngine.Economy;
```

---

#### HATA: `Cannot implicitly convert type 'X' to 'Y'`

**Neden:** Yanlış tip atanmış (örn. `int` bekleyen yere `float` verilmiş).  
**Çözüm:** Explicit cast veya uygun conversion kullanın:
```csharp
int count = (int)floatValue;      // float → int
float f = (float)intValue;        // int → float
string s = value.ToString();      // herhangi → string
```

---

#### HATA: `NullReferenceException: Object reference not set`

```
NullReferenceException: Object reference not set to an instance of an object
MyBootstrap.Start () (at Assets/_Game/Scripts/Bootstrap/MyBootstrap.cs:42)
```

**Neden:** Inspector'da bir alan boş bırakılmış (bağlanmamış).  
**Çözüm:**
1. Hata satır numarasına gidin (örn. 42)
2. O satırdaki nesnenin hangisi olduğuna bakın
3. Inspector'da o GameObject'i seçin, boş kalan alanı bulun ve bağlayın

En sık karşılaşılan boş alanlar:
```
_economyService   → EconomyService component'i olan GameObject sürükleyin
_generatorSystem  → GeneratorSystem component'i olan GameObject sürükleyin
_saveService      → SaveService component'i olan GameObject sürükleyin
```

---

#### HATA: `SerializedField is not showing in Inspector`

**Neden:** Script derleme hatası var veya `[SerializeField]` üstte `private` olmayan bir alana uygulanmış.  
**Çözüm:**
```csharp
// YANLIŞ (public, [SerializeField] gerekmez ama çalışır):
public GeneratorSystem generatorSystem;

// DOĞRU:
[SerializeField] private GeneratorSystem _generatorSystem;

// YANLIŞ (protected ve SerializeField birlikte sorun çıkarabilir):
[SerializeField] protected GeneratorSystem _gs;
```

Console'da başka derleme hatası varsa tümünü temizlemeden Inspector güncellenmez.

---

#### HATA: `Ambiguous reference between 'X' and 'Y'`

```
error CS0104: 'Random' is an ambiguous reference between 'UnityEngine.Random' and 'System.Random'
```

**Neden:** İki namespace'de aynı isimde tip var.  
**Çözüm:** Tam nitelikli isim kullanın:
```csharp
float r = UnityEngine.Random.value;   // Unity Random
System.Random sysRand = new System.Random(); // System Random
```

---

#### HATA: `The name 'X' does not exist in the current context`

**Neden:** Değişken yanlış scope'ta tanımlanmış veya yazım hatası var.  
**Çözüm:**
```csharp
// YANLIŞ: if bloğu içinde tanımlanıp dışında kullanılmış
if (condition)
{
    GeneratorSystem sys = GetComponent<GeneratorSystem>();
}
sys.DoSomething(); // HATA: sys burada yok

// DOĞRU:
GeneratorSystem sys = null;
if (condition)
{
    sys = GetComponent<GeneratorSystem>();
}
sys?.DoSomething();
```

---

#### HATA: `Assets/Scripts/X.cs(1,1): error CS1056: Unexpected character`

**Neden:** Dosyada BOM (Byte Order Mark) veya görünmez karakter var.  
**Çözüm:** Dosyayı Notepad++ veya VS Code'da açın → Encoding → "UTF-8 without BOM" olarak kaydedin.

---

### 10.2 Runtime Hataları

---

#### SORUN: Generator geliri hiç gelmiyor

**Kontrol listesi (sırayla):**
1. `PassiveIncomeService.Initialize()` çağrıldı mı?
2. `TickEngine` sahnede var mı? (Component olarak değil, serviste yönetiliyor)
3. `GeneratorSystem.Initialize()` çağrıldı mı ve generator database boş değil mi?
4. `_saveService.LoadAsync()` await edildi mi? (load olmadan count 0 kalır)

```csharp
// Hızlı debug — gelir gerçekten 0 mı?
Debug.Log($"PassiveIncome/s: {_passiveIncomeService.GetTotalPerSecond()}");
```

---

#### SORUN: Wave oyununda düşmanlar altın düşürmüyor

**Neden:** Event subscription eksik.  
**Çözüm:**
```csharp
// Bu satır mutlaka olmalı:
_enemyManager.OnEnemyKilled += OnEnemyKilledAddGold;

private void OnEnemyKilledAddGold(EnemyAgent agent)
    => _economyService?.AddResources(agent.GoldDropAmount);
```

---

#### SORUN: Wave asla bitmiyor, yeni wave gelmiyor

**Neden:** `HandleEnemyKilledByManager` subscription eksik.  
**Çözüm:**
```csharp
// Bu satır mutlaka olmalı:
_enemyManager.OnEnemyKilled += _autoBattle.HandleEnemyKilledByManager;
```

---

#### SORUN: Prestige sonrası gold sıfırlanmıyor (veya her şey sıfırlanıyor)

**Gold sıfırlanmıyorsa:**
```csharp
// EconomyConfigSO → PrimaryCurrencyId'nin CurrencyConfigSO'sunda:
ResetsOnPrestige = true; // bu false ise sıfırlanmaz
```

**İkincil currency sıfırlanıyorsa:**
```csharp
// İlgili CurrencyConfigSO'da:
ResetsOnPrestige = false; // Crystal, Gem gibi kalıcı para birimleri
```

---

#### SORUN: Upgrade satın alındı ama stat değişmedi

**Neden:** `UpgradeTreeService.IsReady` false veya yanlış StatType kullanılmış.  
**Kontrol:**
```csharp
Debug.Log($"UpgradeTree ready: {_upgradeTreeService.IsReady}");
// false ise save load tamamlanmamış
```

StatType'ın tam listesi `ENDLESS_ENGINE_OYUN_URETIM_REHBERI.md` Section 6'da.

---

#### SORUN: Kayıt yüklenmiyor veya bozuluyor

**Neden 1:** `RegisterStateProvider` çağrılmadı.  
```csharp
// Her servis için:
_saveService.RegisterStateProvider(_economyService);
_saveService.RegisterStateProvider(_generatorSystem);
_saveService.RegisterStateProvider(_upgradeTreeService);
```

**Neden 2:** `LoadAsync()` tamamlanmadan sistemler başlatıldı.  
```csharp
// YANLIŞ:
_ = _saveService.LoadAsync();
_generatorSystem.DoSomethingWithSaveData(); // load henüz bitmedi!

// DOĞRU:
bool done = false;
_ = _saveService.LoadAsync().ContinueWith(_ => done = true,
    TaskScheduler.FromCurrentSynchronizationContext());
yield return new WaitUntil(() => done);
// Buradan sonra save data hazır
```

---

#### SORUN: BuildingService tick çalışmıyor, binalar üretim yapmıyor

**Neden:** BuildingService otomatik tick almaz.  
**Çözüm:**
```csharp
// Bootstrap'ta mutlaka:
TickEngine.OnTick += dt => _buildingService.OnTick(dt);
```

---

#### SORUN: ResearchService araştırmalar ilerlenmiyor

**Neden:** ResearchService de otomatik tick almaz.  
**Çözüm:**
```csharp
TickEngine.OnTick += dt => _researchService.OnTick(dt);
```

---

#### SORUN: MergeService component olarak bulunamıyor

**Neden:** MergeService bir MonoBehaviour değil, normal C# sınıfıdır.  
**Çözüm:**
```csharp
// YANLIŞ:
[SerializeField] private MergeService _mergeService; // Inspector'da görünmez!

// DOĞRU:
private MergeService _mergeService;

private void Awake()
{
    _mergeService = new MergeService();
}
```

---

#### SORUN: `[DefaultExecutionOrder(-500)]` çalışmıyor gibi görünüyor

Bootstrap scripti diğerlerinden önce çalışmıyor mu?  
**Kontrol:**
1. Script sınıfının başına `[DefaultExecutionOrder(-500)]` attribute yazılmış mı?
2. Project Settings → Script Execution Order'da override var mı? (varsa orası önce gelir)
3. Coroutine içindeki `yield` noktasından sonra diğer scriptler çalışmaya başlar — bu normaldir.

---

#### SORUN: DontDestroyOnLoad ile sahne geçişinde nesne duplicate oluyor

**Neden:** DontDestroyOnLoad uygulanan script her sahne yüklendiğinde tekrar Awake çalışıyor.  
**Çözüm:** Singleton pattern'da duplicate kontrolü yapın:
```csharp
private void Awake()
{
    if (Instance != null && Instance != this)
    {
        Destroy(gameObject); // Duplicate'i yok et
        return;
    }
    Instance = this;
    DontDestroyOnLoad(gameObject);
}
```

---

#### SORUN: Steamworks.NET `SteamAPI.Init()` false dönüyor

**Kontrol listesi:**
1. `steam_appid.txt` dosyası `.exe` ile aynı klasörde mi?
2. Steam istemcisi açık mı? (Steam kapalıyken init başarısız olur)
3. Dosya içinde sadece AppID var mı? (boşluk veya satır sonu olmamalı)
4. AppID geçerli mi? (test için `480` kullanın)

---

### 10.3 Inspector / Editor Sorunları

---

#### SORUN: ScriptableObject Create menüde görünmüyor

**Çözüm:** Unity'yi yeniden başlatın. Bazı SO tipleri ilk derleme sonrası menüde çıkmaz.  
Hâlâ görünmüyorsa Assets/Create'ten değil, doğrudan Inspector'dan oluşturun:
```
Assets klasöründe sağ tık → Create → ScriptableObject → ilgili tipi arayın
```

---

#### SORUN: Inspector'da field'a nesne sürüklediğimde bağlanmıyor

**Neden:** Tip uyuşmuyor.  
Örn. `EconomyService` bekleyen alana `GeneratorSystem` sürükleyemezsiniz.  
**Çözüm:** Sürüklediğiniz nesnenin Inspector'da hangi Component'lere sahip olduğunu kontrol edin.  
Doğru component'i içeren GameObject'i sürükleyin.

---

#### SORUN: Prefab'daki değişiklikler sahnede yansımıyor

**Çözüm:** Prefab'ı düzenledikten sonra **Overrides → Apply All** butonuna tıklayın,  
veya Inspector'daki **Apply** butonunu kullanın.

---

### 10.4 Performans Sorunları

---

#### SORUN: FPS düşüyor, oyun kasıyor

**Olası nedenler ve çözümler:**

| Neden | Kontrol | Çözüm |
|---|---|---|
| Her frame Update() çok fazla iş yapıyor | Profiler → CPU | Hesaplamaları TickEngine'e taşıyın |
| Çok fazla Instantiate/Destroy | Profiler → GC | Object pooling kullanın (Section 3.4) |
| UI Canvas her frame rebuild oluyor | Profiler → UI | Canvas'ı statik ve dinamik olarak ayırın |
| String concatenation loop içinde | Kod incelemesi | StringBuilder kullanın |

**Profiler açmak için:** Window → Analysis → Profiler

---

## Sonraki Adım

Bu rehberi tamamladıktan sonra oyun türünüze göre ilgili rehbere geçin:

| Oyun Türü | Rehber |
|---|---|
| Cookie Clicker / AdCap tarzı | GUIDE_01_PureIdle.md |
| Wave RPG / Tower Defence | GUIDE_02_WaveRPG.md |
| Clicker Heroes / Tap Titans | GUIDE_03_ClickLoop.md |
| Merge Dragons tarzı | GUIDE_04_MergeIdle.md |
| Antimatter Dimensions tarzı | GUIDE_05_PrestHeavy.md |
| Hay Day / Building Idle | GUIDE_06_BuildingIdle.md |
