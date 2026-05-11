# WaveIdle — Endless Engine Sample

Otomatik savaş + dalga sistemi + upgrade kartı + prestige örneği.

## Ne Gösterir

| Sistem | Sınıf | Görev |
|--------|-------|-------|
| Otomatik Savaş | `AutoBattleController` | En yakın düşmana otomatik saldırı |
| Dalga | `WaveSpawnManager` | Düşman spawn, dalga geçişi |
| Düşman | `EnemyManager` | Düşman yaşam döngüsü |
| Upgrade Kartı | `UpgradeTreeService.GetAvailableNodes()` | Dalga sonu kart seçimi |
| Prestige | `PrestigeStateManager` | Sıfırla, kalıcı çarpan kazan |
| Jeneratör | `GeneratorSystem` + `PassiveIncomeService` | Pasif gelir |
| Kayıt | SaveService | Dalga, altın, prestige sayısı |

## Kurulum

1. **Package Manager → Endless Engine → Samples → WaveIdle → Import**
2. `Assets/Samples/WaveIdle/Scenes/WaveIdle.unity` sahnesini açın
3. Inspector'da tüm alanları doldurun
4. **Play** — savaş otomatik başlar!

## Gerekli Config Asset'leri

```
Create → Wave Config
  TotalWavesPerRun = 20
  WaveTransitionDelaySeconds = 2
  UpgradeSelectionWaveInterval = 5

Create → Player Base Stat Config
  BaseAttackDamage = 10
  BaseMaxHP = 100
  BaseAttackInterval = 1
  BaseCritChance = 0.05

Create → Prestige Config
  MinWaveForPrestige = 10
  BaseMultiplierPerPrestige = 1.5
```

## Dalga Sonu Upgrade Ekranı Örneği

```csharp
WaveSpawnManager.OnWaveCleared += (wave) =>
{
    if (wave % ConfigRegistry.Wave.UpgradeSelectionWaveInterval == 0)
    {
        _autoBattle.StopCombat(); // Bootstrap'te değil; UI controller'ınızda yapın
        var nodes = _upgradeTree.GetAvailableNodes();
        upgradeScreen.Show(nodes.Take(3).ToArray(), (chosen) =>
        {
            _economyService.TryPurchase(chosen.NodeId);
            upgradeScreen.Hide();
            _autoBattle.StartCombat();
        });
    }
};
```

## Prestige Butonu

```csharp
void Update()
{
    prestigeBtn.interactable = _prestigeManager.CanPrestige;
    multiplierText.text = $"x{_prestigeManager.GetPermanentMultiplier():F2}";
}
public void OnPrestigeClick() => _prestigeManager.TryPrestige();
```
