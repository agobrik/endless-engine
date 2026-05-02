# ClickerIdle — Endless Engine Sample

Aktif clicker loop örneği. Sahnede yerleştirilen `ClickTarget` nesnelerine tıkla, yok et, altın kazan.

## Ne Gösterir

| Sistem | Sınıf | Görev |
|--------|-------|-------|
| Aktif Tıklama | `ClickLoopService` | Tıklama tespiti, hasar, altın |
| Hedef | `ClickTarget` | HP, respawn, event |
| Combo | `ClickComboTracker` | Combo çarpanı ve decay |
| Kritik | `ClickYieldResolver` | Crit şans ve çarpan |
| Offline | `ClickLoopOfflineCalculator` | Çevrimdışı oto-tıklama geliri |
| Kayıt | `ClickLoopService.OnBeforeSave` | Respawn timer'ları saklar |

## Kurulum

1. **Package Manager → Endless Engine → Samples → ClickerIdle → Import**
2. `Assets/Samples/ClickerIdle/Scenes/ClickerIdle.unity` sahnesini açın
3. Bootstrap GameObject'ini seçin, Inspector'da tüm alanları doldurun
4. **Play** — hedeflere tıklayın!

## Gerekli Config Asset'leri

```
Create → Endless Engine → Click Loop → Click Loop Config
  ComboDecayDelay = 1.5
  MaxComboMultiplier = 8
  BaseCritChance = 0.05
  BaseCritMultiplier = 3

Create → Endless Engine → Click Loop → Click Target Config
  TargetId = "coin-bag"
  MaxHP = 5
  DamagePerClick = 1
  BaseYield = 30
  RespawnSeconds = 3
```

## Inspector Alanları

| Alan | Ne Atanır |
|------|-----------|
| Click Loop Config | `ClickLoopConfig.asset` |
| Input Provider | Player GO üzerindeki `InputProviderUnity` |
| Click Target Layer | `ClickableTargets` layer |
| Stat Definitions | StatDefinitionSO array |

## Sahnede ClickTarget Kurulumu

1. Sahnede bir GameObject oluşturun.
2. `ClickTarget` component'i ekleyin (BoxCollider2D otomatik gelir).
3. `_config` alanına `ClickTargetConfigSO` asset'ini atayın.
4. Layer'ı `ClickableTargets` yapın.

Ya da `Assets/Prefabs/ClickLoop/ClickTarget_Default.prefab`'ı sürükleyin, config'i atayın.

## Upgrade Bağlantısı

Upgrade node'larında şu StatType değerleri ClickLoopService tarafından okunur:
`ClickDamage` · `ClickYieldMultiplier` · `ClickCritChance` · `ClickCritMultiplier` · `ClickAutoRate`
