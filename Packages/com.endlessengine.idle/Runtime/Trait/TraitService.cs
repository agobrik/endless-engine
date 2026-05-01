using System;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Stats;
using EndlessEngine.Prestige;
using EndlessEngine.SaveAndLoad;

namespace EndlessEngine.Trait
{
    /// <summary>
    /// Manages the permanent trait build system.
    /// Traits are chosen at prestige and persist across all subsequent prestiges.
    ///
    /// One trait choice is offered per prestige. Exclusivity groups prevent
    /// contradictory trait combinations.
    ///
    /// Implements IModifierSource so ModifierRegistry can aggregate trait stat bonuses.
    ///
    /// Bootstrap wiring:
    ///   traitService.Initialize(allTraits, prestigeStateManager, saveService);
    ///
    /// On prestige: call traitService.NotifyPrestige(prestigeCount) to generate
    ///   the trait selection pool; subscribe OnTraitSelectionAvailable to show UI.
    /// </summary>
    public class TraitService : MonoBehaviour, ISaveStateProvider, IModifierSource
    {
        public int ProviderOrder => SaveConstants.SaveProviderOrder.Milestone + 20;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when a trait selection pool is ready. UI should show choices.</summary>
        public static event Action<TraitConfigSO[]> OnTraitSelectionAvailable;

        /// <summary>Fires when the player chooses a trait.</summary>
        public static event Action<TraitConfigSO> OnTraitChosen;

        // ── State ─────────────────────────────────────────────────────────────────

        private TraitConfigSO[]        _allTraits;
        private PrestigeStateManager   _prestige;

        private readonly HashSet<string>                       _chosenIds = new();
        private readonly Dictionary<string, TraitConfigSO>    _lookup    = new();

        private bool _pendingSelection;

        // ── Bootstrap ─────────────────────────────────────────────────────────────

        public void Initialize(TraitConfigSO[] allTraits, PrestigeStateManager prestige, SaveService saveService)
        {
            _allTraits = allTraits ?? Array.Empty<TraitConfigSO>();
            _prestige  = prestige;

            _lookup.Clear();
            foreach (var t in _allTraits)
                if (t != null && !string.IsNullOrEmpty(t.TraitId))
                    _lookup[t.TraitId] = t;

            saveService?.RegisterStateProvider(this);

            if (prestige != null)
                PrestigeStateManager.OnPrestigeComplete += HandlePrestige;
        }

        private void OnDestroy()
        {
            PrestigeStateManager.OnPrestigeComplete -= HandlePrestige;
            ClearSubscribersForTesting();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>True if the trait has been chosen by the player.</summary>
        public bool IsChosen(string traitId) => _chosenIds.Contains(traitId);

        /// <summary>True if a trait selection is waiting for player input.</summary>
        public bool HasPendingSelection => _pendingSelection;

        /// <summary>
        /// Chooses a trait from the current selection pool.
        /// Returns false if the trait is not in the pool, is exclusive with a chosen trait,
        /// or there is no pending selection.
        /// </summary>
        public bool ChooseTrait(string traitId)
        {
            if (!_pendingSelection) return false;
            if (!_lookup.TryGetValue(traitId, out var trait)) return false;
            if (!IsEligible(trait)) return false;

            _chosenIds.Add(traitId);
            _pendingSelection = false;
            OnTraitChosen?.Invoke(trait);
            return true;
        }

        /// <summary>Returns traits currently chosen by the player.</summary>
        public IReadOnlyCollection<string> ChosenTraitIds => _chosenIds;

        // ── IModifierSource ───────────────────────────────────────────────────────

        public string SourceId => "trait";

        public Modifier GetModifier(StatType stat)
        {
            double additive = 0.0;
            double mult     = 1.0;
            foreach (var id in _chosenIds)
            {
                if (!_lookup.TryGetValue(id, out var trait) || trait.Effects == null) continue;
                foreach (var effect in trait.Effects)
                {
                    if (!System.Enum.TryParse<StatType>(effect.TargetId, ignoreCase: true, out var targetStat)) continue;
                    if (targetStat != stat) continue;
                    if (effect.Type == SkillEffectType.StatMultiplier)   mult     *= effect.Value;
                    else if (effect.Type == SkillEffectType.StatAdditive
                          || effect.Type == SkillEffectType.IncomeBonus) additive += effect.Value;
                }
            }
            return (additive == 0.0 && mult == 1.0) ? Modifier.None : new Modifier(additive, mult);
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.CompletedMilestones ??= new HashSet<string>();
            foreach (var id in _chosenIds)
                saveData.CompletedMilestones.Add($"trait:{id}");
        }

        public void OnAfterLoad(SaveData saveData)
        {
            _chosenIds.Clear();
            if (saveData.CompletedMilestones == null) return;
            const string prefix = "trait:";
            foreach (var entry in saveData.CompletedMilestones)
                if (entry.StartsWith(prefix))
                    _chosenIds.Add(entry.Substring(prefix.Length));
        }

        // ── Internal ──────────────────────────────────────────────────────────────

        private void HandlePrestige(int prestigeCount, float _)
        {
            var pool = BuildSelectionPool(prestigeCount);
            if (pool.Length == 0) return;
            _pendingSelection = true;
            OnTraitSelectionAvailable?.Invoke(pool);
        }

        private TraitConfigSO[] BuildSelectionPool(int prestigeCount)
        {
            var eligible = new List<TraitConfigSO>();
            foreach (var t in _allTraits)
            {
                if (t == null) continue;
                if (prestigeCount < t.PrestigeRequired) continue;
                if (_chosenIds.Contains(t.TraitId)) continue;
                if (!IsEligible(t)) continue;
                eligible.Add(t);
            }
            // Return up to 3 random choices
            Shuffle(eligible);
            int count = Mathf.Min(3, eligible.Count);
            var result = new TraitConfigSO[count];
            for (int i = 0; i < count; i++) result[i] = eligible[i];
            return result;
        }

        private bool IsEligible(TraitConfigSO trait)
        {
            if (trait.ExclusiveWith == null) return true;
            foreach (var exId in trait.ExclusiveWith)
                if (_chosenIds.Contains(exId)) return false;
            return true;
        }

        private static void Shuffle<T>(List<T> list)
        {
            var rng = new System.Random();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ── Test helpers ──────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void ClearSubscribersForTesting()
        {
            OnTraitSelectionAvailable = null;
            OnTraitChosen             = null;
        }
        public void ForceChooseForTesting(string traitId) => _chosenIds.Add(traitId);
        public bool IsChosenForTesting(string traitId)    => IsChosen(traitId);
#else
        private static void ClearSubscribersForTesting() { }
#endif
    }
}
