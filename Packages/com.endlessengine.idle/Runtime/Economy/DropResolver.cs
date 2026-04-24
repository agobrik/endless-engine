using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;

namespace EndlessEngine.Economy
{
    /// <summary>
    /// Result of a single drop roll.
    /// </summary>
    [Serializable]
    public class DropResult
    {
        public ItemConfigSO Item;
        public int          Count;
        public ItemRarity   Rarity;
        public bool         WasPityGuaranteed;
    }

    /// <summary>
    /// Stateless weighted-random drop resolver with integrated pity counter.
    ///
    /// Usage:
    ///   var resolver = new DropResolver();
    ///   var drops = resolver.Roll(table, pityCounters);
    ///   // pityCounters is updated in-place after each roll.
    ///
    /// Zero-allocation for common path (no LINQ).
    /// </summary>
    public class DropResolver
    {
        // ── Pity counters ─────────────────────────────────────────────────────────
        // tableId → number of non-pity-level rolls since last rare+
        private readonly Dictionary<string, int> _pityCounters = new Dictionary<string, int>();

        // ── Roll ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Rolls the drop table and returns a list of drops (one per RollsPerUse).
        /// Updates internal pity counters.
        /// Returns empty list if table is null, has no entries, or total weight is 0.
        /// </summary>
        public List<DropResult> Roll(DropTableConfigSO table)
        {
            var results = new List<DropResult>();
            if (table == null || table.Entries == null || table.Entries.Count == 0) return results;

            float totalWeight = table.TotalWeight();
            if (totalWeight <= 0f) return results;

            for (int roll = 0; roll < table.RollsPerUse; roll++)
            {
                bool isPityActive = false;

                if (table.EnablePity && !string.IsNullOrEmpty(table.TableId))
                {
                    _pityCounters.TryGetValue(table.TableId, out int pityCurrent);
                    if (pityCurrent >= table.PityThreshold)
                        isPityActive = true;
                }

                DropEntry entry;
                if (isPityActive)
                {
                    entry = RollWithMinRarity(table, table.PityMinRarity);
                    if (entry == null) entry = RollWeighted(table, totalWeight);
                }
                else
                {
                    entry = RollWeighted(table, totalWeight);
                }

                if (entry == null || entry.Item == null) continue;

                // Update pity counter
                if (table.EnablePity && !string.IsNullOrEmpty(table.TableId))
                {
                    bool meetsRarity = entry.Rarity >= table.PityMinRarity;
                    if (meetsRarity || isPityActive)
                        _pityCounters[table.TableId] = 0; // reset
                    else
                    {
                        _pityCounters.TryGetValue(table.TableId, out int c);
                        _pityCounters[table.TableId] = c + 1;
                    }
                }

                int count = entry.MinCount >= entry.MaxCount
                    ? entry.MinCount
                    : UnityEngine.Random.Range(entry.MinCount, entry.MaxCount + 1);

                results.Add(new DropResult
                {
                    Item              = entry.Item,
                    Count             = Mathf.Max(1, count),
                    Rarity            = entry.Rarity,
                    WasPityGuaranteed = isPityActive
                });
            }

            return results;
        }

        // ── Roll helpers ──────────────────────────────────────────────────────────

        private DropEntry RollWeighted(DropTableConfigSO table, float totalWeight)
        {
            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float accumulated = 0f;

            foreach (var entry in table.Entries)
            {
                if (entry?.Item == null) continue;
                float w = Mathf.Max(0f, entry.Weight);
                accumulated += w;
                if (roll <= accumulated) return entry;
            }

            return table.Entries[table.Entries.Count - 1]; // fallback: last entry
        }

        private DropEntry RollWithMinRarity(DropTableConfigSO table, ItemRarity minRarity)
        {
            // Build a temporary sub-table of entries meeting the min rarity
            float total = 0f;
            var eligible = new List<DropEntry>();
            foreach (var entry in table.Entries)
            {
                if (entry?.Item == null) continue;
                if (entry.Rarity >= minRarity)
                {
                    eligible.Add(entry);
                    total += Mathf.Max(0f, entry.Weight);
                }
            }

            if (eligible.Count == 0 || total <= 0f) return null;

            float roll = UnityEngine.Random.Range(0f, total);
            float accumulated = 0f;
            foreach (var entry in eligible)
            {
                accumulated += Mathf.Max(0f, entry.Weight);
                if (roll <= accumulated) return entry;
            }
            return eligible[eligible.Count - 1];
        }

        // ── Pity accessors ────────────────────────────────────────────────────────

        /// <summary>Returns the current pity counter for a table (rolls since last rare+).</summary>
        public int GetPityCounter(string tableId) =>
            _pityCounters.TryGetValue(tableId, out int c) ? c : 0;

        /// <summary>Resets the pity counter for a table (e.g. on prestige).</summary>
        public void ResetPityCounter(string tableId) => _pityCounters.Remove(tableId);

        /// <summary>Resets all pity counters.</summary>
        public void ResetAllPityCounters() => _pityCounters.Clear();

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void InjectPityCounterForTesting(string tableId, int count) =>
            _pityCounters[tableId] = count;
#endif
    }
}
