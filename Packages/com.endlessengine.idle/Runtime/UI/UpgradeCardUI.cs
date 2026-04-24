using UnityEngine;
using UnityEngine.UIElements;
using EndlessEngine.Upgrade;

namespace EndlessEngine.UI
{
    /// <summary>
    /// Upgrade card selection overlay.
    ///
    /// Shows when <see cref="UpgradeSelectionService.OnCardsReady"/> fires;
    /// hides when <see cref="UpgradeSelectionService.OnUpgradeSelected"/> fires.
    /// Each card button routes its click to <see cref="UpgradeSelectionService.NotifyCardChosen"/>.
    ///
    /// UIDocument layer 8 (above HUD 0 and Pause 5).
    ///
    /// GDD: design/gdd/upgrade-selection.md (UI Requirements section)
    /// ADR: ADR-0012 (upgrade card selection), ADR-0013 (UI Toolkit)
    /// Sprint: S4-04
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class UpgradeCardUI : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Tooltip("The UpgradeSelectionService instance in this scene. Required for NotifyCardChosen routing.")]
        [SerializeField] private UpgradeSelectionService _upgradeSelectionService;

        // ── UI References ─────────────────────────────────────────────────────────

        private VisualElement _overlayRoot;
        private VisualElement _cardContainer;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            var doc  = GetComponent<UIDocument>();
            var root = doc.rootVisualElement;

            _overlayRoot   = root.Q<VisualElement>("overlay-root");
            _cardContainer = root.Q<VisualElement>("card-container");

            // Hidden by default
            SetVisible(false);
        }

        private void OnEnable()
        {
            UpgradeSelectionService.OnCardsReady     += OnCardsReady;
            UpgradeSelectionService.OnUpgradeSelected += OnUpgradeSelected;
        }

        private void OnDisable()
        {
            UpgradeSelectionService.OnCardsReady     -= OnCardsReady;
            UpgradeSelectionService.OnUpgradeSelected -= OnUpgradeSelected;
        }

        // ── Event Handlers ────────────────────────────────────────────────────────

        private void OnCardsReady(UpgradeCardData[] cards)
        {
            BuildCards(cards);
            SetVisible(true);
        }

        private void OnUpgradeSelected(string nodeId)
        {
            SetVisible(false);
        }

        // ── Card Building ─────────────────────────────────────────────────────────

        private void BuildCards(UpgradeCardData[] cards)
        {
            _cardContainer.Clear();

            for (int i = 0; i < cards.Length; i++)
            {
                int capturedIndex = i; // capture for lambda
                UpgradeCardData card = cards[i];

                var cardEl = new Button(() => _upgradeSelectionService?.NotifyCardChosen(capturedIndex))
                {
                    name = "card-" + i
                };
                cardEl.AddToClassList("upgrade-card");

                if (!card.IsAffordable)
                    cardEl.SetEnabled(false);

                var nameLabel = new Label(card.DisplayName);
                nameLabel.AddToClassList("card-name");

                var descLabel = new Label(card.Description);
                descLabel.AddToClassList("card-description");

                var costLabel = new Label("◆ " + GoldFormatter.Format(card.Cost));
                costLabel.AddToClassList("card-cost");

                cardEl.Add(nameLabel);
                cardEl.Add(descLabel);
                cardEl.Add(costLabel);

                _cardContainer.Add(cardEl);
            }
        }

        // ── Visibility ────────────────────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            if (_overlayRoot == null) return;

            if (visible)
                _overlayRoot.AddToClassList("visible");
            else
                _overlayRoot.RemoveFromClassList("visible");
        }
    }
}
