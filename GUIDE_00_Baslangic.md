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

---

---

# BÖLÜM II — TAM SİSTEM ENTEGRASYON REHBERİ

> Bu bölüm Endless Engine'deki **her sistemi** kapsar.  
> Wizard'ın ürettiği iskelet üzerine istediğiniz sistemi nasıl ekleyeceğinizi,  
> sistemlerin birbirine nasıl bağlandığını, hangi SO'ların ne işe yaradığını  
> ve hangi kombinasyonların birlikte nasıl çalıştığını öğrenirsiniz.  
> Oyun türünden bağımsızdır — Tower Defense'e Harvest ekleyin, Pure Idle'a  
> Wave Combat ekleyin, her kombinasyon mümkündür.

---

## İçindekiler (Sistem Rehberi)

- [S1. Sisteme Genel Bakış — Ne Var, Ne İşe Yarar](#s1-sisteme-genel-bakış)
- [S2. Zorunlu Temel — Her Oyunda Olması Gerekenler](#s2-zorunlu-temel)
- [S3. Upgrade Tree Sistemi](#s3-upgrade-tree-sistemi)
- [S4. Skill Tree Sistemi](#s4-skill-tree-sistemi)
- [S5. İkincil Para Birimi (CurrencyService)](#s5-ikincil-para-birimi)
- [S6. Envanter Sistemi (InventoryService)](#s6-envanter-sistemi)
- [S7. Dönüşüm Sistemi (ConversionService)](#s7-dönüşüm-sistemi)
- [S8. Harvest Loop Sistemi](#s8-harvest-loop-sistemi)
- [S9. Click Loop Sistemi](#s9-click-loop-sistemi)
- [S10. Wave ve Combat Sistemi](#s10-wave-ve-combat-sistemi)
- [S11. Building Sistemi](#s11-building-sistemi)
- [S12. Research Sistemi](#s12-research-sistemi)
- [S13. Prestige Sistemi](#s13-prestige-sistemi)
- [S14. Realm Sistemi](#s14-realm-sistemi)
- [S15. İstatistik Takibi (StatisticsService)](#s15-istatistik-takibi)
- [S16. Sistem Kombinasyonları — Gerçek Oyun Örnekleri](#s16-sistem-kombinasyonları)
- [S17. Tam Bootstrap Şablonu — Tüm Sistemler](#s17-tam-bootstrap-şablonu)

---

## S1. Sisteme Genel Bakış

Endless Engine'de 21 servis vardır. Her servis bağımsız olarak eklenebilir veya çıkarılabilir.  
Zorunlu olan sadece 3 tanesidir; geri kalanı isteğe bağlıdır.

### Servis Haritası

```
ZORUNLU ÇEKİRDEK
├── EconomyService          — Altın bakiyesi, satın alma
├── GeneratorSystem         — Pasif gelir kaynakları
└── SaveService             — Kayıt/yükleme

GELİR SİSTEMLERİ (birini veya birkaçını seçin)
├── PassiveIncomeService    — Generatorlardan otomatik gelir (tick tabanlı)
├── ClickLoopService        — Tıklama ile gelir (aktif)
└── HarvestLoopService      — İmleç/alan hasat ile gelir (aktif)

AKTİF DÖNGÜ SİSTEMLERİ (isteğe bağlı, birden fazlası olabilir)
├── WaveSpawnManager        — Düşman dalgası yönetimi
├── AutoBattleController    — Otomatik savaş döngüsü
├── BuildingService         — Bina yerleştirme ve üretim
└── MergeService            — Merge mekaniği (harici, new ile oluşturulur)

PROGRESSION SİSTEMLERİ (isteğe bağlı)
├── UpgradeTreeService      — Satın alınan upgrade node'ları
├── SkillTreeService        — Puan harcanan kalıcı skill'ler
├── ResearchService         — Zaman bazlı araştırma ağacı
└── PrestigeStateManager    — Soft reset + kalıcı çarpan

PARA BİRİMİ / ENVANTER (isteğe bağlı)
├── CurrencyService         — İkincil para birimleri (gem, shard, vb.)
├── InventoryService        — Eşya envanteri
└── ConversionService       — Para/eşya dönüşüm tarifleri

YARDIMCI SİSTEMLER
├── StatisticsService       — İstatistik takibi
├── RealmConfigSystem       — Çok alemli konfigürasyon
└── UpgradeSelectionService — Wave arası upgrade kart seçimi
```

### Zorunlu Bağımlılıklar

```
EconomyService
    ↑ beslenir: PassiveIncomeService, ClickLoopService, HarvestLoopService, Wave (enemy drop)
    ↑ sorgular: UpgradeTreeService (upgrade maliyetleri)
    ↑ bildirir: SaveService

GeneratorSystem
    → EconomyService (satın alma için)
    → SaveService

PassiveIncomeService
    → GeneratorSystem (yield hesabı için)
    → EconomyService (geliri ekler)
    → TickEngine (otomatik tick)

UpgradeTreeService
    → ConfigRegistry (upgrade config'lerini alır)
    → SaveService (node rank'larını yükler)
    ↑ sorgulanır: EconomyService, UpgradeApplicationSystem

SaveService
    ← RegisterStateProvider() ile tüm servisler kayıt olur
```

---

## S2. Zorunlu Temel

Her oyunda bulunması gereken minimum kurulum. Bunu yapmadan hiçbir sistem çalışmaz.

### S2.1 Gerekli GameObjects

Sahnenize şu GameObject'leri ekleyin:

```
Bootstrap (boş GameObject, [DefaultExecutionOrder(-500)] script'i burada)
├── EconomyService component
├── GeneratorSystem component
├── UpgradeTreeService component
├── PassiveIncomeService component
├── SaveService component
└── TickEngine component
```

### S2.2 Gerekli ScriptableObject'ler

**1. EconomyConfigSO** oluşturun (`Assets/_Game/Configs/Economy/`):
```
Sağ tık → Create → Endless Engine → Economy → EconomyConfig
```
Önemli field'lar:
| Field | Açıklama | Başlangıç Değeri |
|---|---|---|
| IdleYieldRateBase | Pasif gelir baz çarpanı | 1.0 |
| ResourceHardCap | Maksimum altın | 1e100 |
| StartingGold | Yeni oyun başlangıç altını | 0 |
| NumberBackend | DoubleNumber veya BigDouble | DoubleNumber |
| OfflineCapHours | Maksimum offline birikim süresi | 8 |

**2. SchemaVersionSO** oluşturun:
```
Sağ tık → Create → Endless Engine → Core → SchemaVersion
```
CurrentSchemaVersion = 1 (başlangıçta)

**3. GeneratorConfigSO** (en az bir tane):
```
Sağ tık → Create → Endless Engine → Generator → GeneratorConfig
```
| Field | Açıklama | Örnek Değer |
|---|---|---|
| GeneratorId | Benzersiz string ID | "gold_mine" |
| DisplayName | Gösterilecek isim | "Altın Madeni" |
| BaseYieldPerSecond | Saniye başı baz gelir | 1.0 |
| BaseCost | İlk satın alma maliyeti | 10 |
| CostScalingFactor | Her alımda maliyet çarpanı | 1.07 |
| MaxCount | Maksimum adet (0 = sınırsız) | 0 |

**4. GeneratorDatabaseSO** oluşturun:
```
Sağ tık → Create → Endless Engine → Generator → GeneratorDatabase
```
Oluşturduğunuz tüm GeneratorConfigSO'ları bu listeye ekleyin.

### S2.3 Minimum Bootstrap Script

```csharp
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using EndlessEngine.Core;
using EndlessEngine.Economy;
using EndlessEngine.Generator;
using EndlessEngine.Upgrade;
using EndlessEngine.SaveAndLoad;

[DefaultExecutionOrder(-500)]
public class GameBootstrap : MonoBehaviour
{
    [Header("Configs")]
    [SerializeField] private EconomyConfigSO _economyConfig;
    [SerializeField] private SchemaVersionSO _schemaVersion;

    [Header("Services")]
    [SerializeField] private EconomyService _economyService;
    [SerializeField] private GeneratorSystem _generatorSystem;
    [SerializeField] private UpgradeTreeService _upgradeTreeService;
    [SerializeField] private PassiveIncomeService _passiveIncomeService;
    [SerializeField] private SaveService _saveService;

    [Header("Generator Database")]
    [SerializeField] private GeneratorDatabaseSO _generatorDatabase;

    private IEnumerator Start()
    {
        // 1. BigNumber backend'i yapılandır
        BigNumberFactory.Configure(_economyConfig.NumberBackend);

        // 2. Config registry'yi doldur (upgrade config'leri S3'te eklenir)
        ConfigRegistry.InjectForTesting(
            economy: _economyConfig,
            schema: _schemaVersion,
            upgrades: null   // ← upgrade eklenince buraya gelecek
        );

        // 3. UpgradeTree config'leri oku
        _upgradeTreeService?.HandleConfigsLoaded();

        // 4. EconomyService başlat
        _economyService.Initialize(_upgradeTreeService, _saveService);

        // 5. GeneratorSystem başlat
        _generatorSystem.Initialize(_generatorDatabase.Generators, _economyService, _saveService);

        // 6. PassiveIncomeService başlat
        _passiveIncomeService.Initialize(_generatorSystem, _economyService, gameFlow: null);

        // 7. Save provider'ları kaydet
        _saveService.RegisterStateProvider(_economyService);
        _saveService.RegisterStateProvider(_upgradeTreeService);
        _saveService.RegisterStateProvider(_generatorSystem);

        // 8. Kayıt yükle (tamamlanana kadar bekle)
        bool done = false;
        _ = _saveService.LoadAsync().ContinueWith(
            _ => done = true,
            TaskScheduler.FromCurrentSynchronizationContext());
        yield return new WaitUntil(() => done);

        // 9. Buradan sonra tüm sistemler hazır
        OnGameReady();
    }

    private void OnGameReady()
    {
        Debug.Log("Oyun hazır!");
        // UI'ı burada aktif edin
    }
}
```

**Inspector bağlantıları:**
1. Bootstrap GameObject'i seçin
2. Script'teki her `[SerializeField]` alanına ilgili component veya asset'i sürükleyin
3. `_economyService` → Bootstrap GameObject'indeki EconomyService component'i
4. `_generatorDatabase` → `Assets/_Game/Configs/Generators/` klasöründeki GeneratorDatabase asset'i

---

## S3. Upgrade Tree Sistemi

Upgrade tree, oyuncunun altın harcayarak kalıcı stat bonusları aldığı sistemdir.  
Tower Defense'e de, Harvest oyununa da, Pure Idle'a da eklenebilir.

### S3.1 UpgradeNodeConfigSO Oluşturma

Her upgrade için ayrı bir SO oluşturun:
```
Sağ tık → Create → Endless Engine → Upgrade → UpgradeNode
```

| Field | Açıklama | Örnek |
|---|---|---|
| NodeId | Benzersiz string ID | "yield_boost_1" |
| DisplayName | Gösterilecek isim | "Verim Artışı I" |
| Description | Açıklama metni | "Tüm generatorların verimini %20 artırır" |
| MaxRank | Kaç kez satın alınabilir | 5 |
| BaseCost | İlk rank maliyeti | 100 |
| CostScalingFactor | Her rank'ta maliyet çarpanı | 1.5 |
| AffectedStat | Hangi stat etkilenir | GeneratorYield |
| EffectPerRank | Her rank başına etki miktarı | 0.20 |
| EffectType | AdditivePercent / AdditiveFlat / Multiplicative | AdditivePercent |
| PrerequisiteNodeIDs | Bu upgrade'den önce alınması gerekenler | [] |
| MinWaveRequirement | Kaçıncı wave'den sonra satın alınabilir | 0 |
| PrestigeGateRequirement | Kaç prestige sonrası açılır | 0 |
| SelectionWeight | Wave arası kart seçiminde görünme ağırlığı | 1.0 |

**StatType değerleri (AffectedStat için):**

| StatType | Etki Alanı |
|---|---|
| GeneratorYield | Tüm generator verimleri |
| ClickDamage | Tıklama hasarı |
| ClickYieldMultiplier | Tıklama başı altın çarpanı |
| ClickCritChance | Tıklama kritik şansı |
| ClickCritMultiplier | Kritik hasar çarpanı |
| ClickAutoRate | Otomatik tıklama hızı |
| HarvestYieldMultiplier | Hasat geliri çarpanı |
| IdleYieldRate | Pasif gelir hız çarpanı |
| OfflineYieldRate | Offline gelir verimliliği |
| PrestigeMultiplier | Prestige çarpan bonusu |
| Damage | Savaş hasarı |
| MaxHP | Maksimum can |
| CritChance | Savaş kritik şansı |
| CritMultiplier | Savaş kritik çarpanı |
| StartingGoldBonus | Prestige sonrası başlangıç altın bonusu |

### S3.2 Upgrade'leri Bootstrap'a Bağlama

Birden fazla upgrade node'u için önce bir dizi oluşturun.  
Bootstrap script'ine ekleyin:

```csharp
[Header("Upgrade Configs")]
[SerializeField] private UpgradeNodeConfigSO[] _upgradeNodes;
```

`ConfigRegistry.InjectForTesting` çağrısını güncelleyin:

```csharp
ConfigRegistry.InjectForTesting(
    economy: _economyConfig,
    schema: _schemaVersion,
    upgrades: _upgradeNodes   // ← artık dolu
);
```

Inspector'da `_upgradeNodes` dizisine oluşturduğunuz tüm UpgradeNodeConfigSO'ları sürükleyin.

### S3.3 Upgrade UI Oluşturma

**UpgradePanel** sahne yapısı:
```
Canvas
└── UpgradePanel
    └── ScrollView
        └── Viewport
            └── Content (Vertical Layout Group)
                └── UpgradeCard prefab (Instantiate ile doldurulur)
```

**UpgradeCard.cs** (prefab'a eklenecek script):

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EndlessEngine.Upgrade;
using EndlessEngine.Economy;

public class UpgradeCard : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descText;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private TMP_Text _rankText;
    [SerializeField] private Button _buyButton;

    private UpgradeTreeService _upgradeTree;
    private EconomyService _economy;
    private string _nodeId;

    public void Initialize(string nodeId, UpgradeTreeService upgradeTree, EconomyService economy)
    {
        _nodeId = nodeId;
        _upgradeTree = upgradeTree;
        _economy = economy;

        var node = _upgradeTree.GetNode(nodeId);
        _nameText.text = node.Config.DisplayName;
        _descText.text = node.Config.Description;

        _buyButton.onClick.AddListener(OnBuyClicked);
        Refresh();
    }

    public void Refresh()
    {
        if (_upgradeTree == null || !_upgradeTree.IsReady) return;
        var node = _upgradeTree.GetNode(_nodeId);
        bool available = _upgradeTree.IsNodeAvailable(_nodeId);
        bool canAfford = _economy.CurrentResources >= _upgradeTree.GetNodeCost(_nodeId);

        _rankText.text = $"{node.CurrentRank}/{node.Config.MaxRank}";
        _costText.text = node.CurrentRank >= node.Config.MaxRank
            ? "MAKS"
            : $"{_upgradeTree.GetNodeCost(_nodeId):N0} altın";
        _buyButton.interactable = available && canAfford && node.CurrentRank < node.Config.MaxRank;
    }

    private void OnBuyClicked()
    {
        _economy.TryPurchase(_nodeId);
        Refresh();
    }
}
```

**UpgradePanel controller** (UpgradePanel GameObject'e ekleyin):

```csharp
using UnityEngine;
using EndlessEngine.Upgrade;
using EndlessEngine.Economy;

public class UpgradePanelController : MonoBehaviour
{
    [SerializeField] private UpgradeCard _cardPrefab;
    [SerializeField] private Transform _content;
    [SerializeField] private UpgradeTreeService _upgradeTree;
    [SerializeField] private EconomyService _economy;

    private readonly System.Collections.Generic.List<UpgradeCard> _cards = new();

    private void Start()
    {
        // UpgradeTree hazır olana kadar bekle
        if (_upgradeTree.IsReady) BuildCards();
        else StartCoroutine(WaitForTree());

        _economy.OnResourcesChanged += OnResourcesChanged;
    }

    private System.Collections.IEnumerator WaitForTree()
    {
        yield return new WaitUntil(() => _upgradeTree.IsReady);
        BuildCards();
    }

    private void BuildCards()
    {
        foreach (var node in _upgradeTree.GetAvailableNodes())
        {
            var card = Instantiate(_cardPrefab, _content);
            card.Initialize(node.Config.NodeId, _upgradeTree, _economy);
            _cards.Add(card);
        }
    }

    private void OnResourcesChanged(double _)
    {
        foreach (var card in _cards)
            card.Refresh();
    }

    private void OnDestroy()
    {
        if (_economy != null) _economy.OnResourcesChanged -= OnResourcesChanged;
    }
}
```

### S3.4 Upgrade Ağacı Tasarımı — Önerilen Yapı

```
[yield_boost_1] GeneratorYield +20%  (maliyet: 100)
      ↓ önkoşul
[yield_boost_2] GeneratorYield +40%  (maliyet: 500)
      ↓ önkoşul
[yield_boost_3] GeneratorYield +80%  (maliyet: 2500)

[click_boost_1] ClickDamage +25%    (maliyet: 200)   ← ClickLoop kullananlar için
      ↓ önkoşul
[click_boost_2] ClickCritChance +5% (maliyet: 800)

[prestige_prep_1] PrestigeMultiplier +10%  (PrestigeGate: 1)  ← 1 prestijden sonra açılır
```

`PrerequisiteNodeIDs` dizisine önceki node'un ID'sini yazarak zincir oluşturursunuz.

---

## S4. Skill Tree Sistemi

Upgrade tree satın alınan stat bonuslarıysa, Skill Tree **puan harcanan kalıcı yeteneklerdir**.  
Prestige veya zaman geçtikçe puan kazanılır ve bu puanlar kalıcı skilllere harcanır.

### S4.1 SkillTreeConfigSO Oluşturma

```
Sağ tık → Create → Endless Engine → Upgrade → SkillTree
```

SkillTreeConfigSO içinde node'ları tanımlarsınız. Her node:

| Field | Açıklama |
|---|---|
| NodeId | Benzersiz ID |
| DisplayName | Görünen isim |
| PointCost | Kaç skill puanı gerekli |
| PrerequisiteIds | Önce alınması gerekenler |
| Refundable | Geri alınabilir mi (puanlar iade edilir) |
| Effects[] | StatType + magnitude çiftleri |

### S4.2 Bootstrap'a Ekleme

Bootstrap script'ine ekleyin:

```csharp
[Header("Skill Tree")]
[SerializeField] private SkillTreeService _skillTreeService;
[SerializeField] private SkillTreeConfigSO[] _skillTrees;
```

`Start()` içinde (SaveService.RegisterStateProvider'lardan önce):

```csharp
_skillTreeService.Initialize(_skillTrees, startingPoints: 0);
_saveService.RegisterStateProvider(_skillTreeService);
```

### S4.3 Skill Puanı Kazandırma

Puanları istediğiniz zaman ekleyebilirsiniz — prestige'de, level'da, achievement'da:

```csharp
// Prestige sonrası 1 puan ver
_prestigeStateManager.OnPrestigeComplete += (count, mult) =>
{
    _skillTreeService.AddPoints(1);
};

// Belirli bir wave'e ulaşınca puan ver
_waveSpawnManager.OnWaveStarted += waveNumber =>
{
    if (waveNumber % 10 == 0) // Her 10 wave'de 1 puan
        _skillTreeService.AddPoints(1);
};
```

### S4.4 Skill UI

```csharp
public class SkillNodeButton : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private Button _button;

    private SkillTreeService _skillTree;
    private string _treeId;
    private string _nodeId;

    public void Initialize(string treeId, string nodeId, SkillTreeService skillTree)
    {
        _treeId = treeId;
        _nodeId = nodeId;
        _skillTree = skillTree;
        _skillTree.OnSkillPointsChanged += _ => Refresh();
        _skillTree.OnNodeUnlocked += (t, n) => { if (t == treeId) Refresh(); };
        Refresh();
    }

    private void Refresh()
    {
        bool unlocked = _skillTree.IsUnlocked(_treeId, _nodeId);
        _button.interactable = !unlocked && _skillTree.SkillPoints > 0;
        _nameText.text = unlocked ? $"[✓] {_nodeId}" : _nodeId;
    }

    private void OnButtonClicked() => _skillTree.TryUnlock(_treeId, _nodeId);
}
```

---

## S5. İkincil Para Birimi

Oyununuzda altın dışında gem, kristal, rune gibi para birimleri istiyorsanız CurrencyService kullanın.

### S5.1 CurrencyConfigSO Oluşturma

Her para birimi için bir SO:
```
Sağ tık → Create → Endless Engine → Economy → CurrencyConfig
```

| Field | Açıklama | Örnek |
|---|---|---|
| CurrencyId | Benzersiz ID | "gem" |
| DisplayName | Görünen isim | "Mücevher" |
| StartingAmount | Başlangıç miktarı | 0 |
| HardCap | Maksimum birikim | 10000 |
| ResetsOnPrestige | Prestige'de sıfırlanır mı | false |

**Prestige'de sıfırlanacak para birimleri** (run currency'leri):
- Örn: `souls`, `shards` — `ResetsOnPrestige = true`

**Kalıcı para birimleri** (meta currency):
- Örn: `gems`, `crystals` — `ResetsOnPrestige = false`

### S5.2 CurrencyDatabaseSO Oluşturma

```
Sağ tık → Create → Endless Engine → Economy → CurrencyDatabase
```
Tüm CurrencyConfigSO'ları bu listeye ekleyin.

### S5.3 Bootstrap'a Ekleme

```csharp
[Header("Currency")]
[SerializeField] private CurrencyService _currencyService;
[SerializeField] private CurrencyDatabaseSO _currencyDatabase;
```

`Start()` içinde:
```csharp
_currencyService.Initialize(_currencyDatabase);
_saveService.RegisterStateProvider(_currencyService);
```

### S5.4 Kullanım

```csharp
// Para birimi ekle
_currencyService.Add("gem", 5);

// Harcamayı dene
if (_currencyService.TrySpend("gem", 10))
{
    // Başarılı
}

// Bakiye kontrol
double gemBalance = _currencyService.GetBalance("gem");

// Formatlı gösterim (UI için)
string display = _currencyService.GetFormatted("gem"); // "5 Mücevher"
```

### S5.5 Prestige'den Kazanılan Currency

```csharp
// PrestigeStateManager'ın OnPrestigeComplete event'ına bağlanın
_prestigeStateManager.OnPrestigeComplete += (count, mult) =>
{
    double reward = Mathf.Pow(count, 1.5f); // Prestige sayısına göre artan ödül
    _currencyService.Add("crystal", reward);
};
```

---

## S6. Envanter Sistemi

Oyuncunun eşya taşıdığı, item biriktirdiği oyunlar için (Merge, Building tarzı).

### S6.1 ItemConfigSO Oluşturma

```
Sağ tık → Create → Endless Engine → Economy → ItemConfig
```

| Field | Açıklama |
|---|---|
| ItemId | "wood", "stone", "gem_t1" |
| DisplayName | "Odun" |
| MaxStackSize | Stack başına maksimum adet |

### S6.2 Bootstrap'a Ekleme

```csharp
[Header("Inventory")]
[SerializeField] private InventoryService _inventoryService;
[SerializeField] private ItemConfigSO[] _allItems;
```

```csharp
_inventoryService.Initialize(_allItems, maxSlots: 20);
_saveService.RegisterStateProvider(_inventoryService);
```

### S6.3 Kullanım

```csharp
// Eşya ekle (returns actual amount added — slot doluysa eksik ekler)
int added = _inventoryService.Add("wood", 5);

// Eşya çıkar
bool removed = _inventoryService.Remove("wood", 3);

// Kontrol
bool hasEnough = _inventoryService.Has("stone", 10);
int woodCount = _inventoryService.GetCount("wood");

// Tüm eşyalar
foreach (var kv in _inventoryService.Stacks)
    Debug.Log($"{kv.Key}: {kv.Value}");
```

---

## S7. Dönüşüm Sistemi

"10 odun → 1 tahta", "1000 altın → 1 gem" gibi tarif tabanlı dönüşümler için.

### S7.1 ConversionRecipeSO Oluşturma

```
Sağ tık → Create → Endless Engine → Economy → ConversionRecipe
```

| Field | Açıklama | Örnek |
|---|---|---|
| RecipeId | "wood_to_plank" | |
| InputCurrencyId | null veya currency ID | "gold" |
| InputAmount | Girdi miktarı | 1000 |
| OutputCurrencyId | null veya currency ID | "gem" |
| OutputAmount | Çıktı miktarı | 1 |
| InputItemId | null veya item ID | null |
| OutputItemId | null veya item ID | null |
| CooldownSeconds | İki kullanım arası bekleme | 0 |

### S7.2 Bootstrap'a Ekleme

```csharp
[Header("Conversion")]
[SerializeField] private ConversionService _conversionService;
[SerializeField] private ConversionDatabaseSO _conversionDatabase;
```

```csharp
_conversionService.Initialize(_conversionDatabase, _economyService, _currencyService);
```

### S7.3 Kullanım

```csharp
// Tarifi çalıştır
if (_conversionService.TryConvert("wood_to_plank", count: 1))
{
    Debug.Log("Dönüşüm başarılı");
}

// Soğuma kontrolü
float cd = _conversionService.GetCooldownRemaining("daily_trade");
```

---

## S8. Harvest Loop Sistemi

İmleç veya parmak ile hasat alanı üzerine gelince otomatik hasar + gelir.  
Tower Defense sahnesine eklenebilir, Pure Idle'a eklenebilir — her oyun türüyle çalışır.

### S8.1 Sahneye Eklenecekler

```
HarvestArea (boş GameObject)
├── HarvestLoopService component
├── HarvestCursor component
└── CursorRadiusIndicator (opsiyonel, Circle sprite)

HarvestNodes
├── Node_1 (HarvestNode component)
├── Node_2
└── Node_3
```

**HarvestCursor component:**
- Mouse/touch pozisyonunu takip eder
- Radius içindeki HarvestNode'ları tespit eder

**HarvestNode component:**
- CircleCollider2D gerektirir (isTrigger = true)
- Config referansı alır

### S8.2 HarvestNodeConfigSO Oluşturma

```
Sağ tık → Create → Endless Engine → Harvest → HarvestNodeConfig
```

| Field | Açıklama | Örnek |
|---|---|---|
| NodeId | "ore_node_1" | |
| DamagePerTick | Her tick'te alınan hasar | 10 |
| MaxHP | Toplam can | 100 |
| RespawnTime | Saniye cinsinden yeniden doğma | 5 |
| AwardYieldPerTick | Her tick gelir mi? | true |
| YieldPerTick | Tick başına altın | 2 |
| YieldOnDepletion | Tükenenince bonus altın | 50 |

### S8.3 HarvestAreaConfigSO Oluşturma

```
Sağ tık → Create → Endless Engine → Harvest → HarvestAreaConfig
```

| Field | Açıklama | Örnek |
|---|---|---|
| BaseRadius | İmleç etkili alanı | 2.0 |
| BaseTickInterval | Tick arası süre (saniye) | 0.5 |
| ComboDecayDelay | Kombo düşmeye başlama süresi (sn) | 2.0 |
| ComboDecayRate | Saniyede kombo düşüşü | 0.5 |
| MaxComboMultiplier | Maksimum kombo çarpanı | 3.0 |
| OfflineCapHours | Maksimum offline birikim | 8 |
| OfflineEfficiency | Offline verim oranı (0–1) | 0.3 |

### S8.4 Bootstrap'a Ekleme

```csharp
[Header("Harvest")]
[SerializeField] private HarvestLoopService _harvestLoopService;
[SerializeField] private HarvestCursor _harvestCursor;
[SerializeField] private HarvestAreaConfigSO _harvestAreaConfig;
[SerializeField] private InputProviderUnity _inputProvider;
```

```csharp
// Start() içinde, SaveService.LoadAsync'ten önce:
_harvestLoopService.Initialize(
    cursor: _harvestCursor,
    config: _harvestAreaConfig,
    economy: _economyService,
    statistics: _statisticsService  // null olabilir
);
_saveService.RegisterStateProvider(_harvestLoopService);

// InputProvider'ı kur
_inputProvider = gameObject.AddComponent<InputProviderUnity>();
_harvestCursor.Inject(_inputProvider);
```

### S8.5 Harvest + Upgrade Tree Kombinasyonu

Upgrade tree'deki `HarvestYieldMultiplier` stat'ı doğrudan harvest gelirine etki eder:

```csharp
// UpgradeNodeConfigSO ayarları:
AffectedStat = HarvestYieldMultiplier
EffectType   = AdditivePercent
EffectPerRank = 0.25  // Her rank %25 daha fazla hasat geliri
```

`UpgradeApplicationSystem.GetEffectiveStat(StatType.HarvestYieldMultiplier)` otomatik hesaplar.  
HarvestLoopService bu stat'ı her yield hesaplamasında otomatik uygular.

---

## S9. Click Loop Sistemi

Tıklama ile hasar verme ve gelir kazanma. Clicker veya aktif battle oyunları için.

### S9.1 ClickTargetConfigSO Oluşturma

```
Sağ tık → Create → Endless Engine → ClickLoop → ClickTargetConfig
```

| Field | Açıklama | Örnek |
|---|---|---|
| TargetId | "ore_rock" | |
| DamagePerClick | Her tıklamada hasar | 10 |
| MaxHP | Toplam can | 100 |
| RespawnTime | Yeniden doğma süresi (sn) | 3 |
| AwardYieldPerClick | Her tıklamada mı gelir verir | true |
| YieldPerClick | Tıklama başı altın | 1 |
| YieldOnDestruction | Yok edilince bonus | 20 |

### S9.2 ClickLoopConfigSO Oluşturma

```
Sağ tık → Create → Endless Engine → ClickLoop → ClickLoopConfig
```

| Field | Açıklama | Örnek |
|---|---|---|
| ComboDecayDelay | Kombo bozulma gecikmesi (sn) | 1.5 |
| ComboDecayRate | Saniyede kombo azalışı | 1.0 |
| MaxComboMultiplier | Maksimum kombo | 5.0 |
| BaseCritChance | Baz kritik şansı (0–1) | 0.05 |
| BaseCritMultiplier | Kritik çarpanı | 2.0 |
| BaseAutoClickRate | Otomatik tıklama/saniye (0 = yok) | 0 |
| OfflineEfficiency | Offline verim (0–1) | 0.0 |

### S9.3 Sahne Kurulumu

```
ClickTarget (GameObject, Collider2D veya Collider gerekli)
└── ClickTarget component (TargetId referansı)

Canvas → ClickDetector (Physics raycast için kamera önünde)
```

ClickableTarget'ların `TargetLayer` isimli bir Layer'da olması gerekir.  
Unity → Edit → Project Settings → Tags and Layers → Layers → boş slota "ClickableTargets" ekleyin.  
ClickTarget GameObject'lerini bu layer'a atayın.

### S9.4 Bootstrap'a Ekleme

```csharp
[Header("Click Loop")]
[SerializeField] private ClickLoopService _clickLoopService;
[SerializeField] private ClickLoopConfigSO _clickLoopConfig;
[SerializeField] private LayerMask _clickTargetLayer;
[SerializeField] private InputProviderUnity _inputProvider;
```

```csharp
_inputProvider = gameObject.AddComponent<InputProviderUnity>();

_clickLoopService.Initialize(
    config: _clickLoopConfig,
    economy: _economyService,
    input: _inputProvider,
    targetLayer: _clickTargetLayer,
    statistics: _statisticsService
);
_saveService.RegisterStateProvider(_clickLoopService);
```

---

## S10. Wave ve Combat Sistemi

Otomatik düşman dalgaları ve savaş. Tower Defense, Wave RPG, roguelike idle için.

### S10.1 Gerekli Config SO'lar

**WaveConfigSO:**
```
Sağ tık → Create → Endless Engine → Wave → WaveConfig
```

| Field | Açıklama | Örnek |
|---|---|---|
| TotalWavesPerRun | Run başına toplam wave | 30 |
| BaseEnemyCountPerWave | Wave 1'deki düşman sayısı | 3 |
| EnemyCountScalingFactor | Her wave'de düşman artış çarpanı | 1.1 |
| HardCapEnemiesOnScreen | Aynı anda max düşman | 20 |
| SpawnIntervalSeconds | Spawn arası süre | 0.5 |
| WaveTransitionDelaySeconds | Wave geçiş bekleme süresi | 3.0 |
| UpgradeSelectionWaveInterval | Kaç wave'de bir upgrade seçimi | 5 |
| EliteWaveInterval | Kaç wave'de bir elite wave | 10 |
| EliteStatMultiplier | Elite düşman stat çarpanı | 2.0 |
| BossWaveInterval | Kaç wave'de bir boss | 0 (kapalı) |

**EnemyStatConfigSO:**
```
Sağ tık → Create → Endless Engine → Wave → EnemyStatConfig
```

| Field | Örnek |
|---|---|
| BaseMaxHP | 50 |
| BaseAttackDamage | 5 |
| BaseContactDamage | 3 |
| MoveSpeed | 2.0 |
| WaveScalingExponent | 1.15 (her wave'de HP bu üstle büyür) |

**PlayerBaseStatConfigSO:**
```
Sağ tık → Create → Endless Engine → Combat → PlayerBaseStatConfig
```

| Field | Örnek |
|---|---|
| BaseAttackDamage | 10 |
| BaseMaxHP | 100 |
| BaseCritChance | 0.05 |
| BaseCritMultiplier | 2.0 |
| BaseAttackInterval | 1.0 (saniye) |

### S10.2 Sahne Kurulumu

```
Bootstrap
├── WaveSpawnManager component
├── EnemyManager component
├── AutoBattleController component
└── PrestigeStateManager component (wave sistemi PrestigeStateManager'ı bildirir)
```

Düşman prefabı hazırlayın:
```
EnemyPrefab (GameObject)
├── SpriteRenderer (düşman görseli)
├── Rigidbody2D (gravityScale: 0, collisionDetection: Continuous)
├── CircleCollider2D
├── EnemyAgent component
└── HPBar (opsiyonel UI)
```

### S10.3 Bootstrap'a Ekleme

```csharp
[Header("Wave & Combat")]
[SerializeField] private WaveSpawnManager _waveSpawnManager;
[SerializeField] private EnemyManager _enemyManager;
[SerializeField] private AutoBattleController _autoBattle;
[SerializeField] private PrestigeStateManager _prestigeManager;
[SerializeField] private WaveConfigSO _waveConfig;
[SerializeField] private EnemyStatConfigSO _enemyStatConfig;
[SerializeField] private PlayerBaseStatConfigSO _playerStatConfig;
[SerializeField] private GameObject _enemyPrefab;
```

Start() içinde (LoadAsync tamamlandıktan SONRA):
```csharp
// Wave sistemi başlat
_waveSpawnManager.Initialize(_enemyManager, _saveService);
_saveService.RegisterStateProvider(_waveSpawnManager);

// AutoBattle başlat
var statProvider = new BaseStatUpgradeProvider(_upgradeTreeService);
_autoBattle.Initialize(
    enemyManager: _enemyManager,
    waveSpawnManager: _waveSpawnManager,
    statProvider: statProvider,
    playerConfig: _playerStatConfig,
    waveConfig: _waveConfig,
    playerId: 0
);

// KRİTİK: Bu iki satır olmadan altın gelmez ve wave bitmez
_enemyManager.OnEnemyKilled += OnEnemyKilledAddGold;
_enemyManager.OnEnemyKilled += _autoBattle.HandleEnemyKilledByManager;

// EnemyManager'a prefab ve config ver
_enemyManager.SetEnemyPrefab(_enemyPrefab);
_enemyManager.SetEnemyStatConfig(_enemyStatConfig);

// PrestigeStateManager wave'i takip etsin
_waveSpawnManager.OnWaveStarted += _prestigeManager.SetCurrentWave;

// Savaşı başlat
_autoBattle.StartCombat();
_waveSpawnManager.StartFirstWave();
```

```csharp
private void OnEnemyKilledAddGold(EnemyAgent agent)
    => _economyService?.AddResources(agent.GoldDropAmount);

private void OnDestroy()
{
    if (_enemyManager != null)
    {
        _enemyManager.OnEnemyKilled -= OnEnemyKilledAddGold;
        _enemyManager.OnEnemyKilled -= _autoBattle.HandleEnemyKilledByManager;
    }
}
```

### S10.4 Wave Arası Upgrade Seçimi

```csharp
[Header("Upgrade Selection")]
[SerializeField] private UpgradeSelectionService _upgradeSelection;
[SerializeField] private UpgradeSelectionConfigSO _upgradeSelectionConfig;
```

```csharp
_upgradeSelection.Initialize(_upgradeTreeService, _upgradeTreeService, _economyService);

// Wave arası kart göster
_waveSpawnManager.OnUpgradeSelectionTriggered += () =>
{
    _upgradeSelection.SetCurrentWave(_waveSpawnManager.CurrentWaveNumber);
    ShowUpgradeCards(_upgradeSelection);
};
```

---

## S11. Building Sistemi

Izgara bazlı bina yerleştirme ve üretim. Hay Day, Forge tarzı oyunlar için.

### S11.1 BuildingConfigSO Oluşturma

```
Sağ tık → Create → Endless Engine → Building → BuildingConfig
```

| Field | Açıklama | Örnek |
|---|---|---|
| BuildingId | "lumber_mill" | |
| DisplayName | "Kereste Fabrikası" | |
| GridWidth | Izgara genişliği (hücre) | 2 |
| GridHeight | Izgara yüksekliği (hücre) | 1 |
| PlacementCost | Yerleştirme maliyeti | 500 |
| PlacementCurrencyId | "gold" veya başka currency | "gold" |
| ProductionCurrencyId | Üretilen şey | "wood" |
| ProductionPerTick | Tick başına üretim | 5 |
| MaxInstances | Aynı anda max kaç tane | 3 |
| UpgradeTiers[] | Yükseltme seviyeleri (maliyet + çarpan) | — |

### S11.2 Bootstrap'a Ekleme

```csharp
[Header("Building")]
[SerializeField] private BuildingService _buildingService;
[SerializeField] private BuildingConfigSO[] _buildingConfigs;
```

```csharp
_buildingService.Initialize(_buildingConfigs, _economyService);
_saveService.RegisterStateProvider(_buildingService);

// KRİTİK: Manuel tick bağlantısı olmadan binalar üretim yapmaz
TickEngine.OnTick += dt => _buildingService.OnTick(dt);
```

### S11.3 Kullanım

```csharp
// Bina yerleştir (grid koordinatları ile)
var result = _buildingService.TryPlace("lumber_mill", gridX: 2, gridY: 3);
if (result.Success)
    Debug.Log($"Bina yerleştirildi: {result.InstanceId}");

// Bina yükselt
_buildingService.TryUpgrade(instanceId);

// Bina kaldır
_buildingService.Remove(instanceId);

// Tüm binalar
foreach (var kv in _buildingService.GetAllInstances())
    Debug.Log($"{kv.Value.Config.DisplayName} at ({kv.Value.GridX},{kv.Value.GridY})");
```

---

## S12. Research Sistemi

Zaman bazlı araştırma ağacı. "30 dakika araştır → yeni özellik açılır."

### S12.1 ResearchNodeConfigSO Oluşturma

ResearchTreeConfigSO içinde inline tanımlanır:
```
Sağ tık → Create → Endless Engine → Research → ResearchTree
```

Her node için:
| Field | Açıklama | Örnek |
|---|---|---|
| NodeId | "advanced_mining" | |
| DisplayName | "Gelişmiş Madencilik" | |
| ResearchTicks | Kaç tick sürer | 3600 (1 saat, tick=1sn ise) |
| GoldCost | Araştırmaya başlama maliyeti | 5000 |
| PrerequisiteIds | Önce tamamlanması gerekenler | ["basic_mining"] |
| Effects[] | Tamamlanınca aktif olan stat etkileri | GeneratorYield +30% |

### S12.2 Bootstrap'a Ekleme

```csharp
[Header("Research")]
[SerializeField] private ResearchService _researchService;
[SerializeField] private ResearchTreeConfigSO[] _researchTrees;
```

```csharp
_researchService.Initialize(_researchTrees, _economyService, _currencyService);
_saveService.RegisterStateProvider(_researchService);

// KRİTİK: Manuel tick bağlantısı
TickEngine.OnTick += dt => _researchService.OnTick(dt);
```

### S12.3 Kullanım

```csharp
// Araştırma sırasına ekle
if (_researchService.TryEnqueue("tech_tree", "advanced_mining"))
    Debug.Log("Araştırma kuyruğa eklendi");

// İptal et (altını geri alır)
_researchService.TryDequeue("tech_tree", "advanced_mining");

// Durum kontrolü
bool done = _researchService.IsCompleted("tech_tree", "advanced_mining");
var (progress, total) = _researchService.GetActiveProgress();
```

### S12.4 Research + Building Kombinasyonu

Research tamamlanınca yeni bina tipi açılsın:
```csharp
_researchService.OnNodeCompleted += (treeId, nodeId) =>
{
    if (nodeId == "unlock_sawmill")
        _buildingService.UnlockBuilding("sawmill"); // Kilit kaldır
};
```

---

## S13. Prestige Sistemi

Soft reset ile kalıcı çarpan kazanma. Tüm oyun türlerine eklenebilir.

### S13.1 PrestigeConfigSO Oluşturma

```
Sağ tık → Create → Endless Engine → Prestige → PrestigeConfig
```

| Field | Açıklama | Örnek |
|---|---|---|
| MinWaveForPrestige | Prestige için gereken wave | 0 (wave yoksa) |
| MinGoldToPrestige | Prestige için gereken altın | 1000000 |
| MaxPrestigeCount | Maksimum prestige sayısı | 999 |
| BaseMultiplierPerPrestige | Prestige başına çarpan | 1.5 |
| MaxPermanentMultiplier | Maksimum kalıcı çarpan | 1000 |
| StatsAmplifiedByPrestige | Hangi stat'lar çarpandan etkilenir | [GeneratorYield, ClickDamage] |

**Wave kullanan oyunlarda:** `MinWaveForPrestige = 10` (en az 10 wave gerekli)  
**Wave kullanmayan oyunlarda:** `MinWaveForPrestige = 0`, `MinGoldToPrestige = 1000000`

### S13.2 Bootstrap'a Ekleme

```csharp
[Header("Prestige")]
[SerializeField] private PrestigeStateManager _prestigeManager;
[SerializeField] private PrestigeConfigSO _prestigeConfig;
```

```csharp
_prestigeManager.InjectEconomy(_economyService);
_saveService.RegisterStateProvider(_prestigeManager);
```

### S13.3 Prestige Butonu

```csharp
public class PrestigeButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private PrestigeStateManager _prestigeManager;

    private void Update()
    {
        bool can = _prestigeManager.CanPrestige;
        _button.interactable = can;
        float mult = _prestigeManager.GetPermanentMultiplier();
        _label.text = can
            ? $"Prestige ({mult:F1}x çarpan)"
            : "Prestige (henüz hazır değil)";
    }

    public void OnPrestigeClicked()
    {
        if (_prestigeManager.CanPrestige)
            _prestigeManager.TryPrestige();
    }
}
```

### S13.4 Prestige Sırasında Sıfırlanmayacak Sistemler

| Sistem | Prestige'de ne olur |
|---|---|
| EconomyService (gold) | Sıfırlanır (StartingGold'a döner) |
| GeneratorSystem | Sıfırlanır (count = 0) |
| UpgradeTreeService | Sıfırlanır (rank = 0) |
| CurrencyService | ResetsOnPrestige = true olanlar sıfırlanır |
| SkillTreeService | Korunur |
| BuildingService | Korunur |
| StatisticsService | Korunur |
| ResearchService | Tamamlananlar korunur, kuyruk temizlenir |

---

## S14. Realm Sistemi

Farklı "alemler" — her alemin kendi config seti var. Prestige ile yeni alemler açılır.

### S14.1 Realm Config Oluşturma

**RealmIdentityConfigSO:**
```
Sağ tık → Create → Endless Engine → Realm → RealmIdentity
```

| Field | Açıklama |
|---|---|
| Slug | "forest_realm" |
| DisplayName | "Orman Alemi" |
| UnlockPrestigeThreshold | Kaç prestige sonrası açılır |

**RealmPackSO** (her alem için ayrı config seti):
```
Sağ tık → Create → Endless Engine → Realm → RealmPack
```
Linked configs: alem'e özgü EconomyConfigSO, GeneratorDatabaseSO vb.

**RealmRegistrySO:**
```
Sağ tık → Create → Endless Engine → Realm → RealmRegistry
```
Tüm RealmIdentityConfigSO'ları ekleyin.

### S14.2 Bootstrap'a Ekleme

```csharp
[Header("Realm")]
[SerializeField] private RealmConfigSystem _realmSystem;
[SerializeField] private RealmRegistrySO _realmRegistry;
```

RealmConfigSystem event'lara kendi kendine bağlanır — Initialize çağrısı gerekmez.  
PrestigeStateManager'ın `OnRealmUnlocked` event'ını otomatik dinler.

---

## S15. İstatistik Takibi

Oynanış verilerini kaydetmek, Steam achievement'ları tetiklemek veya oyun içi profil göstermek için.

### S15.1 StatDefinitionSO Oluşturma

```
Sağ tık → Create → Endless Engine → Statistics → StatDefinition
```

| Field | Açıklama |
|---|---|
| StatId | "total_gold_earned" |
| DisplayName | "Toplam Kazanılan Altın" |
| IsPeakValue | false = biriktirici, true = en yüksek değer |

### S15.2 Bootstrap'a Ekleme

```csharp
[Header("Statistics")]
[SerializeField] private StatisticsService _statisticsService;
[SerializeField] private StatDefinitionSO[] _statDefinitions;
```

```csharp
_statisticsService.Initialize(_statDefinitions);
_saveService.RegisterStateProvider(_statisticsService);
```

### S15.3 İstatistik Kaydetme

```csharp
// Altın kazanıldığında
_economyService.OnResourcesChanged += newBalance =>
    _statisticsService.SetIfHigher("peak_gold", newBalance);

// Düşman öldürüldüğünde
_enemyManager.OnEnemyKilled += _ =>
    _statisticsService.Add("total_kills", 1);

// Prestige'de
_prestigeManager.OnPrestigeComplete += (count, _) =>
    _statisticsService.Add("total_prestiges", 1);
```

### S15.4 Steam Achievement Tetikleme

```csharp
_statisticsService.OnStatChanged += (statId, value) =>
{
    if (statId == "total_kills" && value >= 1000)
        SteamIntegration.UnlockAchievement("KILLS_1000");
    if (statId == "total_prestiges" && value >= 1)
        SteamIntegration.UnlockAchievement("FIRST_PRESTIGE");
};
```

---

## S16. Sistem Kombinasyonları — Gerçek Oyun Örnekleri

### Kombinasyon A: Tower Defense + Harvest + Upgrade Tree

```
Senaryo: Düşmanlara hasat imleci ile hasar ver, öldürünce altın düşür,
altınla hem generator hem upgrade al. Upgrade tree hasatı güçlendirir.

Sistemler:
✓ EconomyService + GeneratorSystem + PassiveIncomeService
✓ HarvestLoopService (imleç hasarı)
✓ WaveSpawnManager + EnemyManager + AutoBattleController
✓ UpgradeTreeService (HarvestYieldMultiplier + GeneratorYield node'ları)
✓ SaveService + TickEngine

Bootstrap sırası:
1. BigNumberFactory.Configure
2. ConfigRegistry.InjectForTesting(economy, schema, upgrades: harvestAndGeneratorNodes)
3. _upgradeTreeService.HandleConfigsLoaded()
4. _economyService.Initialize(_upgradeTreeService, _saveService)
5. _generatorSystem.Initialize(...)
6. _passiveIncomeService.Initialize(...)
7. _harvestLoopService.Initialize(_harvestCursor, _harvestConfig, _economyService)
8. _waveSpawnManager.Initialize(_enemyManager, _saveService)
9. _autoBattle.Initialize(...)
10. RegisterStateProvider: economy, upgradeTree, generatorSystem, harvestLoop, waveSpawnManager
11. LoadAsync() — bekle
12. OnEnemyKilled event'larını bağla
13. StartCombat() + StartFirstWave()
```

### Kombinasyon B: Pure Idle + Research + Building

```
Senaryo: Generator idle geliri, araştırma ile yeni binalar aç,
binalar ekstra currency üretsin.

Sistemler:
✓ EconomyService + GeneratorSystem + PassiveIncomeService
✓ CurrencyService (wood, stone gibi ikincil currency)
✓ BuildingService (binalar wood üretir)
✓ ResearchService (yeni bina tiplerini açar)
✓ UpgradeTreeService
✓ SaveService + TickEngine

Önemli notlar:
- BuildingService → TickEngine.OnTick manuel bağlantı ŞART
- ResearchService → TickEngine.OnTick manuel bağlantı ŞART
- CurrencyService'i SaveService'e registerla
- Building production currency'si CurrencyService'te tanımlı olmalı
```

### Kombinasyon C: Clicker + Prestige + Skill Tree

```
Senaryo: Aktif tıklama oyunu, prestij ile gem kazan,
gem ile kalıcı skill ağacı node'larını aç.

Sistemler:
✓ EconomyService + GeneratorSystem
✓ ClickLoopService (aktif gelir)
✓ CurrencyService ("gem" kalıcı para)
✓ PrestigeStateManager (prestijde gem ver)
✓ SkillTreeService (gem harcayarak skill aç)
✓ UpgradeTreeService

Gem kazanma kodu:
_prestigeManager.OnPrestigeComplete += (count, mult) =>
{
    double gems = Mathf.Pow(count + 1, 1.5f);
    _currencyService.Add("gem", gems);
};

Skill puan kazanma:
_currencyService.OnCurrencyChanged += (id, amount) =>
{
    // Alternatif: her gem harcaması yerine ayrı puan sistemi
};
// Ya da direkt:
_skillTreeService.AddPoints(pointAmount);
```

### Kombinasyon D: Merge + Building + Research

```
Senaryo: Merge ile eşya üret, eşyaları binaya ver,
research ile daha iyi merge tarifler aç.

Sistemler:
✓ EconomyService
✓ InventoryService (merge eşyaları)
✓ MergeService (new MergeService() — MonoBehaviour DEĞİL)
✓ BuildingService (eşya tüketip currency üretir)
✓ ResearchService (yeni merge zinciri açar)
✓ ConversionService (eşya → currency tarifler)

Önemli not:
MergeService = new MergeService()  // GetComponent ile ARAMA
```

---

## S17. Tam Bootstrap Şablonu — Tüm Sistemler

Aşağıdaki bootstrap tüm sistemleri içerir. İhtiyaç duymadıklarınızı silin.

```csharp
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using EndlessEngine.Core;
using EndlessEngine.Economy;
using EndlessEngine.Generator;
using EndlessEngine.Upgrade;
using EndlessEngine.Prestige;
using EndlessEngine.Wave;
using EndlessEngine.Combat;
using EndlessEngine.Harvest;
using EndlessEngine.ClickLoop;
using EndlessEngine.Building;
using EndlessEngine.Research;
using EndlessEngine.Statistics;
using EndlessEngine.SaveAndLoad;

[DefaultExecutionOrder(-500)]
public class FullGameBootstrap : MonoBehaviour
{
    // ── CORE CONFIGS ──────────────────────────────────────────
    [Header("Core Configs")]
    [SerializeField] private EconomyConfigSO _economyConfig;
    [SerializeField] private SchemaVersionSO _schemaVersion;
    [SerializeField] private UpgradeNodeConfigSO[] _upgradeNodes;

    // ── CORE SERVICES ─────────────────────────────────────────
    [Header("Core Services")]
    [SerializeField] private EconomyService _economyService;
    [SerializeField] private GeneratorSystem _generatorSystem;
    [SerializeField] private GeneratorDatabaseSO _generatorDatabase;
    [SerializeField] private UpgradeTreeService _upgradeTreeService;
    [SerializeField] private PassiveIncomeService _passiveIncomeService;
    [SerializeField] private SaveService _saveService;

    // ── CURRENCY & INVENTORY ──────────────────────────────────
    [Header("Currency & Inventory (opsiyonel)")]
    [SerializeField] private CurrencyService _currencyService;
    [SerializeField] private CurrencyDatabaseSO _currencyDatabase;
    [SerializeField] private InventoryService _inventoryService;
    [SerializeField] private ItemConfigSO[] _itemConfigs;
    [SerializeField] private ConversionService _conversionService;
    [SerializeField] private ConversionDatabaseSO _conversionDatabase;

    // ── PRESTIGE ──────────────────────────────────────────────
    [Header("Prestige (opsiyonel)")]
    [SerializeField] private PrestigeStateManager _prestigeManager;
    [SerializeField] private PrestigeConfigSO _prestigeConfig;

    // ── HARVEST ───────────────────────────────────────────────
    [Header("Harvest (opsiyonel)")]
    [SerializeField] private HarvestLoopService _harvestLoopService;
    [SerializeField] private HarvestCursor _harvestCursor;
    [SerializeField] private HarvestAreaConfigSO _harvestAreaConfig;

    // ── CLICK LOOP ────────────────────────────────────────────
    [Header("Click Loop (opsiyonel)")]
    [SerializeField] private ClickLoopService _clickLoopService;
    [SerializeField] private ClickLoopConfigSO _clickLoopConfig;
    [SerializeField] private LayerMask _clickTargetLayer;

    // ── WAVE & COMBAT ─────────────────────────────────────────
    [Header("Wave & Combat (opsiyonel)")]
    [SerializeField] private WaveSpawnManager _waveSpawnManager;
    [SerializeField] private EnemyManager _enemyManager;
    [SerializeField] private AutoBattleController _autoBattle;
    [SerializeField] private WaveConfigSO _waveConfig;
    [SerializeField] private EnemyStatConfigSO _enemyStatConfig;
    [SerializeField] private PlayerBaseStatConfigSO _playerStatConfig;
    [SerializeField] private GameObject _enemyPrefab;

    // ── BUILDING ──────────────────────────────────────────────
    [Header("Building (opsiyonel)")]
    [SerializeField] private BuildingService _buildingService;
    [SerializeField] private BuildingConfigSO[] _buildingConfigs;

    // ── RESEARCH ──────────────────────────────────────────────
    [Header("Research (opsiyonel)")]
    [SerializeField] private ResearchService _researchService;
    [SerializeField] private ResearchTreeConfigSO[] _researchTrees;

    // ── SKILL TREE ────────────────────────────────────────────
    [Header("Skill Tree (opsiyonel)")]
    [SerializeField] private SkillTreeService _skillTreeService;
    [SerializeField] private SkillTreeConfigSO[] _skillTrees;

    // ── STATISTICS ────────────────────────────────────────────
    [Header("Statistics (opsiyonel)")]
    [SerializeField] private StatisticsService _statisticsService;
    [SerializeField] private StatDefinitionSO[] _statDefinitions;

    private InputProviderUnity _inputProvider;

    private IEnumerator Start()
    {
        // ── 1. BigNumber backend ───────────────────────────────
        BigNumberFactory.Configure(_economyConfig.NumberBackend);

        // ── 2. Config Registry ────────────────────────────────
        ConfigRegistry.InjectForTesting(
            economy: _economyConfig,
            schema: _schemaVersion,
            upgrades: _upgradeNodes  // null ise upgrade tree boş kalır
        );

        // ── 3. Upgrade Tree config'leri yükle ─────────────────
        _upgradeTreeService?.HandleConfigsLoaded();

        // ── 4. Core servisler başlat ──────────────────────────
        _economyService.Initialize(_upgradeTreeService, _saveService);
        _generatorSystem.Initialize(_generatorDatabase.Generators, _economyService, _saveService);
        _passiveIncomeService.Initialize(_generatorSystem, _economyService, gameFlow: null);

        // ── 5. İkincil currency & envanter (varsa) ────────────
        if (_currencyService != null && _currencyDatabase != null)
            _currencyService.Initialize(_currencyDatabase);

        if (_inventoryService != null)
            _inventoryService.Initialize(_itemConfigs, maxSlots: 30);

        if (_conversionService != null && _conversionDatabase != null)
            _conversionService.Initialize(_conversionDatabase, _economyService, _currencyService);

        // ── 6. Prestige (varsa) ───────────────────────────────
        if (_prestigeManager != null)
            _prestigeManager.InjectEconomy(_economyService);

        // ── 7. Input provider (Harvest veya Click kullanıyorsa) ─
        if (_harvestLoopService != null || _clickLoopService != null)
        {
            _inputProvider = gameObject.AddComponent<InputProviderUnity>();
        }

        // ── 8. Harvest (varsa) ────────────────────────────────
        if (_harvestLoopService != null)
        {
            _harvestLoopService.Initialize(_harvestCursor, _harvestAreaConfig,
                _economyService, _statisticsService);
            _harvestCursor.Inject(_inputProvider);
        }

        // ── 9. Click Loop (varsa) ─────────────────────────────
        if (_clickLoopService != null)
        {
            _clickLoopService.Initialize(_clickLoopConfig, _economyService,
                _inputProvider, _clickTargetLayer, _statisticsService);
        }

        // ── 10. Wave & Combat (varsa) ─────────────────────────
        if (_waveSpawnManager != null)
        {
            _waveSpawnManager.Initialize(_enemyManager, _saveService);
            var statProvider = new BaseStatUpgradeProvider(_upgradeTreeService);
            _autoBattle.Initialize(_enemyManager, _waveSpawnManager,
                statProvider, _playerStatConfig, _waveConfig, playerId: 0);
        }

        // ── 11. Building (varsa) ──────────────────────────────
        if (_buildingService != null)
        {
            _buildingService.Initialize(_buildingConfigs, _economyService);
            TickEngine.OnTick += dt => _buildingService.OnTick(dt);
        }

        // ── 12. Research (varsa) ──────────────────────────────
        if (_researchService != null)
        {
            _researchService.Initialize(_researchTrees, _economyService, _currencyService);
            TickEngine.OnTick += dt => _researchService.OnTick(dt);
        }

        // ── 13. Skill Tree (varsa) ────────────────────────────
        if (_skillTreeService != null)
            _skillTreeService.Initialize(_skillTrees, startingPoints: 0);

        // ── 14. Statistics (varsa) ────────────────────────────
        if (_statisticsService != null)
            _statisticsService.Initialize(_statDefinitions);

        // ── 15. Save provider'ları kaydet ─────────────────────
        _saveService.RegisterStateProvider(_economyService);
        _saveService.RegisterStateProvider(_upgradeTreeService);
        _saveService.RegisterStateProvider(_generatorSystem);

        if (_currencyService != null) _saveService.RegisterStateProvider(_currencyService);
        if (_inventoryService != null) _saveService.RegisterStateProvider(_inventoryService);
        if (_prestigeManager != null)  _saveService.RegisterStateProvider(_prestigeManager);
        if (_harvestLoopService != null) _saveService.RegisterStateProvider(_harvestLoopService);
        if (_clickLoopService != null) _saveService.RegisterStateProvider(_clickLoopService);
        if (_buildingService != null)  _saveService.RegisterStateProvider(_buildingService);
        if (_researchService != null)  _saveService.RegisterStateProvider(_researchService);
        if (_skillTreeService != null) _saveService.RegisterStateProvider(_skillTreeService);
        if (_statisticsService != null) _saveService.RegisterStateProvider(_statisticsService);
        if (_waveSpawnManager != null) _saveService.RegisterStateProvider(_waveSpawnManager);

        // ── 16. Kayıt yükle ───────────────────────────────────
        bool done = false;
        _ = _saveService.LoadAsync().ContinueWith(
            _ => done = true,
            TaskScheduler.FromCurrentSynchronizationContext());
        yield return new WaitUntil(() => done);

        // ── 17. Load sonrası event bağlantıları ───────────────
        if (_waveSpawnManager != null)
        {
            // KRİTİK: Bu iki satır olmadan düşmanlar altın düşürmez ve wave bitmez
            _enemyManager.OnEnemyKilled += OnEnemyKilledAddGold;
            _enemyManager.OnEnemyKilled += _autoBattle.HandleEnemyKilledByManager;
            _waveSpawnManager.OnWaveStarted += _prestigeManager.SetCurrentWave;

            _autoBattle.StartCombat();
            _waveSpawnManager.StartFirstWave();
        }

        // ── 18. Prestige event bağlantıları ───────────────────
        if (_prestigeManager != null && _currencyService != null)
        {
            _prestigeManager.OnPrestigeComplete += (count, mult) =>
            {
                // Prestige'de gem ver (opsiyonel)
                _currencyService.Add("gem", Mathf.Pow(count, 1.5f));
            };
        }

        if (_prestigeManager != null && _skillTreeService != null)
        {
            _prestigeManager.OnPrestigeComplete += (count, _) =>
            {
                _skillTreeService.AddPoints(1); // Her prestige 1 skill puanı
            };
        }

        Debug.Log("[Bootstrap] Oyun hazır.");
    }

    private void OnEnemyKilledAddGold(EnemyAgent agent)
        => _economyService?.AddResources(agent.GoldDropAmount);

    private void OnDestroy()
    {
        if (_enemyManager != null)
        {
            _enemyManager.OnEnemyKilled -= OnEnemyKilledAddGold;
            if (_autoBattle != null)
                _enemyManager.OnEnemyKilled -= _autoBattle.HandleEnemyKilledByManager;
        }
        if (_buildingService != null)
            TickEngine.OnTick -= dt => _buildingService.OnTick(dt);
        if (_researchService != null)
            TickEngine.OnTick -= dt => _researchService.OnTick(dt);
    }
}
```

---

## Hızlı Başvuru: Hangi Sistem Ne Zaman Kullanılır

| İstediğin Şey | Kullanılacak Sistem |
|---|---|
| Pasif gelir (generator satın al, bekle) | GeneratorSystem + PassiveIncomeService |
| Aktif tıklama geliri | ClickLoopService |
| İmleç/alan hasat geliri | HarvestLoopService |
| Stat bonusu satın al (altınla) | UpgradeTreeService + UpgradeNodeConfigSO |
| Kalıcı yetenek ağacı (puan harca) | SkillTreeService + SkillTreeConfigSO |
| Zaman bazlı araştırma | ResearchService + ResearchTreeConfigSO |
| Izgara bazlı bina + üretim | BuildingService + BuildingConfigSO |
| Düşman dalgaları + savaş | WaveSpawnManager + AutoBattleController |
| Yumuşak sıfırlama + çarpan | PrestigeStateManager + PrestigeConfigSO |
| Altın dışı para birimi | CurrencyService + CurrencyDatabaseSO |
| Eşya/item yönetimi | InventoryService + ItemConfigSO |
| Para/eşya dönüşüm tarifleri | ConversionService + ConversionDatabaseSO |
| Farklı dünya konfigürasyonları | RealmConfigSystem + RealmRegistrySO |
| Oyun içi istatistik/Steam achievement | StatisticsService + StatDefinitionSO |
