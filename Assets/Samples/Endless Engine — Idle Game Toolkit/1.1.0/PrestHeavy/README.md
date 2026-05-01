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
4. **Play** — dalga 10'da prestige, 50'de ascension!

## Gerekli Config Asset'leri

```
Create → Wave Config
  TotalWavesPerRun = 50
  WaveTransitionDelaySeconds = 2
  UpgradeSelectionWaveInterval = 5

Create → Player Base Stat Config
  BaseAttackDamage = 10
  BaseMaxHP = 100
  BaseAttackInterval = 1
  BaseCritChance = 0.05

Create → Prestige Config
  MinWaveToPrestige = 10
  BaseMultiplierPerPrestige = 0.1

Create → Ascension Database SO
  // AscensionNodeSO'ları buraya ekleyin
  // Her node bir kalıcı bonus verir (ör. +%20 tüm hasar)

Create → Research Tree Config SO
  // ResearchNodeSO'ları zincir halinde tanımlayın
```

## Prestige + Ascension Farkı

| Özellik | Prestige | Ascension |
|---------|----------|-----------|
| Tetikleyici | MinWaveToPrestige eşiği | N prestige sonrası |
| Sıfırlanan | Dalga, upgrade, jeneratörler | Prestige sayısı dahil çoğu şey |
| Kalıcı bonus | Altın çarpanı | Ascension node bonusları |
| Kaç kez | Sınırsız | Sınırlı (AscensionDatabase kapasitesi) |

## Araştırma Kuyruğu

```csharp
// Araştırma başlatma
_researchService.EnqueueResearch("node-id-advanced-damage");

// Her tick ilerler (Bootstrap'te bağlı):
// _tickEngine.OnTick += _researchService.OnTick;

// Tamamlandığında:
ResearchService.OnResearchCompleted += (nodeId) =>
{
    Debug.Log($"Research complete: {nodeId}");
    // Upgrade node'u otomatik aktif olur
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
    ascensionBtn.interactable = _ascensionManager.CanAscend;
    ascensionCountText.text = $"Ascension: {_ascensionManager.AscensionCount}";
}

public void OnAscensionClick()
{
    if (_ascensionManager.TryAscend())
        ShowAscensionRewardScreen(_ascensionManager.LastUnlockedNodes);
}
```

## StatisticsService Bağlantısı

```csharp
// Ömür boyu istatistik okuma
long totalMerges = _statisticsService.GetLifetime(StatKey.TotalMerges);
long totalPrestige = _statisticsService.GetLifetime(StatKey.TotalPrestige);
long totalKills = _statisticsService.GetLifetime(StatKey.TotalEnemiesKilled);

// HUD'da göster
statsText.text =
    $"Prestige: {totalPrestige}  Kills: {totalKills}";
```
