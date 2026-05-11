# PrestHeavy — Endless Engine Sample

Prestige + ascension + skill tree + research queue + wave/combat tam zinciri örneği.

## Ne Gösterir

| Sistem | Sınıf | Görev |
|--------|-------|-------|
| Otomatik Savaş | `AutoBattleController` | En yakın düşmana otomatik saldırı |
| Dalga | `WaveSpawnManager` | Düşman spawn, dalga geçişi |
| Düşman | `EnemyManager` | Düşman yaşam döngüsü |
| Prestige | `PrestigeStateManager` | Sıfırla, kalıcı çarpan kazan |
| Ascension | `AscensionStateManager` | Prestige sayısı eşiğinde büyük sıfırlama |
| Araştırma | `ResearchService` | Tick-bazlı araştırma kuyruğu |
| Jeneratör | `GeneratorSystem` + `PassiveIncomeService` | Pasif gelir |
| İstatistik | `StatisticsService` | Ömür boyu ve oturum istatistikleri |
| Kayıt | `SaveService` | Tüm state persist |

## Kurulum

1. **Package Manager → Endless Engine → Samples → PrestHeavy → Import**
2. `Assets/Samples/PrestHeavy/Scenes/PrestHeavy.unity` sahnesini açın
3. Inspector'da tüm alanları doldurun
4. **Play** — dalga 10'da prestige, prestige 10'da ascension!

## Gerekli Config Asset'leri

```
Create → Endless Engine → Config → Wave Config
  TotalWavesPerRun = 50
  WaveTransitionDelaySeconds = 2
  UpgradeSelectionWaveInterval = 5

Create → Endless Engine → Config → Enemy Stats
  BaseMaxHP = 20
  BaseAttackDamage = 5
  WaveScalingExponent = 1.5

Create → Endless Engine → Config → Prestige Config
  MinWaveForPrestige = 10
  BaseMultiplierPerPrestige = 1.5
  MaxPermanentMultiplier = 1000

Create → Endless Engine/Ascension Database SO
  // PrestigeLayerConfigSO'ları Layers listesine ekleyin
  // LayerIndex=1 → RequiredPreviousLayerCount=10 (10 prestige gerekir)

Create → Endless Engine → Research → Research Tree Config
  // ResearchNodeConfigSO'ları Nodes array'ine ekleyin
```

## Prestige + Ascension Farkı

| Özellik | Prestige (Layer 0) | Ascension (Layer 1+) |
|---------|-------------------|----------------------|
| Tetikleyici | `MinWaveForPrestige` eşiği | `RequiredPreviousLayerCount` prestige sonrası |
| Sıfırlanan | Dalga, upgrade, jeneratörler | `ResetScope`'a göre (daha kapsamlı) |
| Kalıcı bonus | `BaseMultiplierPerPrestige` çarpanı | `GetCascadeMultiplier()` kümülatif çarpan |
| API | `PrestigeStateManager.TryPrestige()` | `AscensionStateManager.TryTrigger(layerIndex, wave)` |

## Araştırma Kuyruğu

```csharp
// Araştırma kuyruğa alma
_researchService.TryEnqueue("tech_tree", "advanced_damage");

// Her tick ilerler (Bootstrap'te bağlı):
// TickEngine.OnTick += dt => _researchService.OnTick(dt);

// Tamamlandığında:
ResearchService.OnNodeCompleted += (treeId, nodeId) =>
{
    Debug.Log($"Araştırma tamamlandı: {nodeId}");
};

// İlerleme takibi:
ResearchService.OnResearchProgress += (treeId, nodeId, ticksDone, ticksTotal) =>
{
    researchBar.fillAmount = (float)ticksDone / ticksTotal;
};
```

## Prestige Akışı

```csharp
void Update()
{
    prestigeBtn.interactable = _prestigeManager.CanPrestige;
    multiplierText.text = $"x{_prestigeManager.GetPermanentMultiplier():F2}";
}

public void OnPrestigeClick() => _prestigeManager.TryPrestige();
```

## Ascension Akışı

```csharp
void Update()
{
    int currentWave = _waveManager.CurrentWaveNumber;
    ascensionBtn.interactable = _ascensionManager.CanTrigger(layerIndex: 1, currentWave);
    cascadeText.text = $"Cascade: x{_ascensionManager.GetCascadeMultiplier():F2}";
    ascensionCountText.text = $"Ascension: {_ascensionManager.GetCount(1)}";
}

public void OnAscensionClick()
{
    int currentWave = _waveManager.CurrentWaveNumber;
    _ascensionManager.TryTrigger(layerIndex: 1, currentWave);
}

// Tamamlanınca event:
AscensionStateManager.OnAscensionComplete += (layerIndex, newCount, cascadeMult) =>
{
    Debug.Log($"Ascension {newCount} tamamlandı! Cascade: x{cascadeMult:F2}");
};
```

## StatisticsService Bağlantısı

```csharp
// İstatistik okuma
long totalPrestige = _statisticsService.Get("total_prestige");
long totalKills    = _statisticsService.Get("total_enemies_killed");

statsText.text = $"Prestige: {totalPrestige}  Kills: {totalKills}";
```
