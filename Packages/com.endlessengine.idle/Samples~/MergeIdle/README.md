# MergeIdle — Endless Engine Sample

Merge board + pasif gelir + ekonomi örneği.

## Ne Gösterir

| Sistem | Sınıf | Görev |
|--------|-------|-------|
| Birleştirme | `MergeService` | 2 aynı tier → 1 üst tier |
| Ekonomi | `EconomyService` | Merge başına anlık altın |
| Yükseltme | `UpgradeTreeService` | Merge bonusu upgrade'leri |
| Kayıt | SaveService | Board state persist |

## Kurulum

1. **Package Manager → Endless Engine → Samples → MergeIdle → Import**
2. `Assets/Samples/MergeIdle/Scenes/MergeIdle.unity` sahnesini açın
3. Inspector'da tüm alanları doldurun
4. **Play** — nesneleri sürükleyip birleştirin!

## Gerekli Config Asset'leri

```
Create → Endless Engine → Merge Config
  // Tier zinciri tanımlayın:
  // Tier1 + Tier1 → Tier2
  // Tier2 + Tier2 → Tier3  ...
```

## Merge + Altın Bağlantısı

```csharp
MergeService.OnMergeCompleted += (result) =>
{
    double goldReward = result.ResultItem.Tier * 10.0;
    _economyService.AddResources(goldReward);
    SpawnMergeVFX(result.ResultItem.Position);
};
```

## Board Mantığı

```csharp
public void OnItemDropped(MergeItem a, MergeItem b, Vector2Int targetPos)
{
    if (a.Tier != b.Tier) return;
    var result = _mergeService.TryMerge(a, b);
    if (!result.Success) return;
    gridView.Remove(a.Position);
    gridView.Remove(b.Position);
    gridView.Place(result.ResultItem, targetPos);
}
```
