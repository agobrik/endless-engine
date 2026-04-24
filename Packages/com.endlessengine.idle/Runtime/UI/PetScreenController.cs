using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Pet;
using EndlessEngine.Config;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Controls the pet/companion screen: pet list, equip/unequip, level up, evolve,
    /// and active effects display.
    ///
    /// Attach to a UIDocument whose Source Asset is PetScreen.uxml.
    /// Wire PetService and PetConfigs in Inspector.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PetScreenController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private PetService    _petService;
        [SerializeField] private PetConfigSO[] _allPets;

        // ── UI Elements ──────────────────────────────────────────────────────────

        private VisualElement _root;
        private VisualElement _list;
        private Button        _closeButton;
        private Label         _activePetName;
        private Label         _activeEffectsLabel;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            var doc     = GetComponent<UIDocument>();
            var docRoot = doc.rootVisualElement;

            _root               = docRoot.Q<VisualElement>("pet-root");
            _list               = docRoot.Q<VisualElement>("pet-list");
            _closeButton        = docRoot.Q<Button>("close-button");
            _activePetName      = docRoot.Q<Label>("active-pet-name");
            _activeEffectsLabel = docRoot.Q<Label>("active-effects-label");

            _closeButton?.RegisterCallback<ClickEvent>(_ => Hide());

            PetService.OnPetEquipped   += OnPetEquipped;
            PetService.OnPetUnequipped += OnPetUnequipped;
            PetService.OnPetLeveledUp  += OnPetLevelChanged;
            PetService.OnPetEvolved    += OnPetEvolved;
        }

        private void OnDisable()
        {
            PetService.OnPetEquipped   -= OnPetEquipped;
            PetService.OnPetUnequipped -= OnPetUnequipped;
            PetService.OnPetLeveledUp  -= OnPetLevelChanged;
            PetService.OnPetEvolved    -= OnPetEvolved;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        public void Show()
        {
            RefreshAll();
            _root.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            _root.style.display = DisplayStyle.None;
        }

        // ── Event handlers ───────────────────────────────────────────────────────

        private void OnPetEquipped(PetConfigSO _)
        {
            if (_root.style.display == DisplayStyle.Flex) RefreshAll();
        }

        private void OnPetUnequipped()
        {
            if (_root.style.display == DisplayStyle.Flex) RefreshAll();
        }

        private void OnPetLevelChanged(PetConfigSO _, int __)
        {
            if (_root.style.display == DisplayStyle.Flex) RefreshAll();
        }

        private void OnPetEvolved(PetConfigSO _from, PetConfigSO _to)
        {
            if (_root.style.display == DisplayStyle.Flex) RefreshAll();
        }

        // ── Refresh ──────────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            RefreshActiveEffectsBar();
            RefreshList();
        }

        private void RefreshActiveEffectsBar()
        {
            if (_petService == null) return;

            var equippedCfg = _petService.GetEquippedConfig();
            if (equippedCfg == null)
            {
                if (_activePetName      != null) _activePetName.text      = "No companion equipped";
                if (_activeEffectsLabel != null) _activeEffectsLabel.text = "";
                return;
            }

            if (_activePetName != null)
                _activePetName.text = equippedCfg.DisplayName + $"  (Lv {_petService.GetLevel(equippedCfg.PetId)})";

            var effects = _petService.GetActiveEffects();
            if (_activeEffectsLabel != null)
            {
                var parts = new List<string>();
                foreach (var fx in effects)
                    parts.Add($"{fx.TargetId} +{fx.Value:F2}");
                _activeEffectsLabel.text = parts.Count > 0
                    ? string.Join("  |  ", parts)
                    : "No active bonuses";
            }
        }

        private void RefreshList()
        {
            if (_list == null || _allPets == null || _petService == null) return;
            _list.Clear();

            foreach (var cfg in _allPets)
            {
                if (cfg == null) continue;
                _list.Add(BuildRow(cfg));
            }
        }

        private VisualElement BuildRow(PetConfigSO cfg)
        {
            bool isEquipped = _petService.IsEquipped(cfg.PetId);
            int  level      = _petService.GetLevel(cfg.PetId);
            bool canEvolve  = cfg.EvolveAtLevel > 0 && level >= cfg.EvolveAtLevel;

            var row = new VisualElement();
            row.AddToClassList("pet-row");
            if (isEquipped) row.AddToClassList("equipped");

            // Top row: name + equipped badge
            var topRow = new VisualElement();
            topRow.AddToClassList("pet-row-top");

            var nameLabel = new Label(cfg.DisplayName);
            nameLabel.AddToClassList("pet-row-name");
            topRow.Add(nameLabel);

            var badge = new Label("EQUIPPED");
            badge.AddToClassList("pet-equipped-badge");
            badge.style.display = isEquipped ? DisplayStyle.Flex : DisplayStyle.None;
            topRow.Add(badge);

            row.Add(topRow);

            // Level bar
            var levelRow = new VisualElement();
            levelRow.AddToClassList("pet-level-row");

            var levelLabel = new Label($"Lv {level} / {cfg.MaxLevel}");
            levelLabel.AddToClassList("pet-level-label");
            levelRow.Add(levelLabel);

            var barBg = new VisualElement();
            barBg.AddToClassList("pet-level-bar-bg");

            var barFill = new VisualElement();
            barFill.AddToClassList("pet-level-bar-fill");
            float fillPct = cfg.MaxLevel > 0 ? (float)level / cfg.MaxLevel : 0f;
            barFill.style.width = Length.Percent(fillPct * 100f);
            barBg.Add(barFill);
            levelRow.Add(barBg);

            row.Add(levelRow);

            // Action buttons
            var actionRow = new VisualElement();
            actionRow.AddToClassList("pet-action-row");

            if (isEquipped)
            {
                var unequipBtn = new Button(() => _petService?.Unequip());
                unequipBtn.text = "Unequip";
                unequipBtn.AddToClassList("pet-action-btn");
                unequipBtn.AddToClassList("pet-unequip-btn");
                actionRow.Add(unequipBtn);
            }
            else
            {
                var equipBtn = new Button(() => _petService?.TryEquip(cfg.PetId));
                equipBtn.text = "Equip";
                equipBtn.AddToClassList("pet-action-btn");
                equipBtn.AddToClassList("pet-equip-btn");
                actionRow.Add(equipBtn);
            }

            bool atMaxLevel   = level >= cfg.MaxLevel;
            bool hasLevelCost = cfg.LevelUpCosts != null && level - 1 < cfg.LevelUpCosts.Length;
            long levelCost    = hasLevelCost ? cfg.LevelUpCosts[level - 1] : 0L;
            var levelUpBtn    = new Button(() => _petService?.TryLevelUp(cfg.PetId));
            levelUpBtn.text   = atMaxLevel ? "Max Level" : $"Level Up ({levelCost:N0})";
            levelUpBtn.AddToClassList("pet-action-btn");
            levelUpBtn.AddToClassList("pet-levelup-btn");
            levelUpBtn.SetEnabled(!atMaxLevel);
            actionRow.Add(levelUpBtn);

            if (canEvolve)
            {
                long evoCost     = cfg.EvolutionCost;
                var evolveBtn    = new Button(() => _petService?.TryEvolve(cfg.PetId));
                evolveBtn.text   = $"Evolve ({evoCost:N0})";
                evolveBtn.AddToClassList("pet-action-btn");
                evolveBtn.AddToClassList("pet-evolve-btn");
                actionRow.Add(evolveBtn);
            }

            row.Add(actionRow);
            return row;
        }
    }
}
