# HarvestLoop — Endless Engine Sample

Aktif hasat loop örneği. Mouse/parmağı `HarvestNode` nesneleri üzerinde tutarak sürekli hasar ver, altın kazan.

## Ne Gösterir

| Sistem | Sınıf | Görev |
|--------|-------|-------|
| Hasat Servisi | `HarvestLoopService` | Tick bazlı hasar ve altın |
| Cursor | `HarvestCursor` | Dünya pozisyonu + Physics2D alan tespiti |
| Node | `HarvestNode` | HP, respawn, event |
| Combo | `HarvestComboTracker` | Combo çarpanı ve decay |
| Multi-node | `HarvestYieldResolver` | Aynı anda birden fazla node bonusu |
| Offline | `HarvestOfflineCalculator` | Çevrimdışı hasat geliri |
| Kayıt | `HarvestLoopService.OnBeforeSave` | Node respawn timer'larını saklar |

## Kurulum

1. **Package Manager → Endless Engine → Samples → HarvestLoop → Import**
2. `Assets/Samples/HarvestLoop/Scenes/HarvestLoop.unity` sahnesini açın
3. Inspector'da tüm alanları doldurun
4. **Play** — mouse'u node'lar üzerine sürükleyin!

## Gerekli Config Asset'leri

```
Create → Endless Engine → Harvest → Harvest Area Config
  BaseRadius = 1.5
  BaseTickInterval = 0.25
  ComboDecayDelay = 1.5
  MaxComboMultiplier = 8

Create → Endless Engine → Harvest → Harvest Node Config
  NodeId = "gold-ore"
  MaxHP = 20
  DamagePerTick = 2
  BaseYield = 100
  RespawnSeconds = 10
```

## Sahnede HarvestNode Kurulumu

1. Sahnede bir GameObject oluşturun.
2. `HarvestNode` component'i ekleyin (CircleCollider2D otomatik gelir).
3. `_config` alanına `HarvestNodeConfigSO` asset'ini atayın.
4. Layer'ı `HarvestNodes` yapın.

Ya da `Assets/Prefabs/Harvest/HarvestNode_Default.prefab`'ı sürükleyin, config'i atayın.

## HarvestCursor Kurulumu

1. Sahnede ayrı bir GameObject oluşturun.
2. `HarvestCursor` component'i ekleyin.
3. Inspector'da `_config` = HarvestAreaConfig, `_harvestLayer` = HarvestNodes layer.
4. Bootstrap'te `HarvestCursor.Inject(inputProvider)` çağrılır — otomatik.

## Upgrade Bağlantısı

`HarvestRadius` · `HarvestTickRate` · `HarvestYieldMultiplier` · `HarvestComboMultiplier` · `HarvestMultiNodeBonus`
