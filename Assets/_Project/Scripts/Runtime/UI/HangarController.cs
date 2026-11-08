using System.Collections.Generic;
using TowerGlide.App;
using TowerGlide.Core.Aircraft;
using TowerGlide.Core.Gameplay;
using TowerGlide.Core.Save;
using TowerGlide.Flight;
using UnityEngine;
using UnityEngine.UI;

namespace TowerGlide.UI
{
    /// <summary>
    /// 격납고(애드온 명세 14.5): 기체 카드 목록(카탈로그에서 자동 생성) → 상세(이름·분류·설명·상대 성능 막대·해금 조건) → SELECT / FLY.
    /// 성능 막대는 "게임 내 상대 성능"이며 실제 항공기 제원이 아님을 화면에 표시한다. 잠긴·개발 예정 기체는 보이되 선택 불가, 결제 유도 없음.
    /// </summary>
    public sealed class HangarController : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [Tooltip("격납고가 열려 있는 동안 숨길 메인 메뉴 요소 묶음.")]
        [SerializeField] private GameObject menuGroup;
        [SerializeField] private RectTransform cardContainer;
        [Tooltip("비활성 카드 템플릿(Button + 자식 Text 'Name', 'Status').")]
        [SerializeField] private Button cardTemplate;
        [SerializeField] private Text nameText;
        [SerializeField] private Text detailText;
        [SerializeField] private Text unlockText;
        [Tooltip("Speed, Acceleration, Agility, Stability, Size 순서의 Filled 이미지.")]
        [SerializeField] private Image[] statBars = new Image[5];
        [SerializeField] private Button selectButton;
        [SerializeField] private Text selectButtonLabel;
        [SerializeField] private Button flyFreeButton;
        [SerializeField] private Button flySkylineButton;
        [SerializeField] private Text noticeText;
        [SerializeField] private AircraftPreviewController preview;
        [SerializeField] private MainMenuController menu;

        private readonly List<(AircraftDefinition def, Button button, Image bg)> _cards = new List<(AircraftDefinition, Button, Image)>();
        private Dictionary<string, AircraftStatBars> _bars = new Dictionary<string, AircraftStatBars>();
        private AircraftDefinition _viewing;

        private static readonly Color CardNormal = new Color(0.08f, 0.12f, 0.2f, 0.85f);
        private static readonly Color CardActive = new Color(0.95f, 0.45f, 0.1f, 0.95f);
        private static readonly Color CardLocked = new Color(0.15f, 0.15f, 0.17f, 0.7f);

        private AircraftSelectionService Selection => GameApp.Instance != null ? GameApp.Instance.Aircraft : null;
        private RecordsData Records => GameApp.Instance != null ? GameApp.Instance.Records : new RecordsData();

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (cardTemplate != null) cardTemplate.gameObject.SetActive(false);
        }

        public void Show()
        {
            AircraftSelectionService sel = Selection;
            if (sel == null || sel.Catalog == null)
            {
                menu?.ShowNotice("기체 목록이 없습니다 — 에디터에서 씬 생성을 실행하세요.");
                return;
            }
            if (panel != null) panel.SetActive(true);
            if (menuGroup != null) menuGroup.SetActive(false);
            BuildCards(sel.Catalog);
            View(sel.Selected);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
            if (menuGroup != null) menuGroup.SetActive(true);
            preview?.Show(null);
            menu?.Refresh();
        }

        private void BuildCards(AircraftCatalog catalog)
        {
            foreach ((AircraftDefinition _, Button b, Image _) in _cards)
                if (b != null) Destroy(b.gameObject);
            _cards.Clear();
            _bars = catalog.ComputeStatBars();

            foreach (AircraftDefinition def in catalog.All)
            {
                if (def == null) continue;
                Button card = Instantiate(cardTemplate, cardContainer);
                card.gameObject.SetActive(true);
                card.name = $"Card_{def.id}";
                SetChildText(card.transform, "Name", def.displayName);
                bool selectable = catalog.IsSelectable(def, Records);
                SetChildText(card.transform, "Status", def.IsImplemented ? (selectable ? CategoryLabel(def.category) : "잠김") : "개발 예정");
                AircraftDefinition captured = def;
                card.onClick.AddListener(() => View(captured));
                _cards.Add((def, card, card.GetComponent<Image>()));
            }
        }

        private void View(AircraftDefinition def)
        {
            _viewing = def;
            if (def == null) return;
            AircraftSelectionService sel = Selection;
            bool selectable = sel.Catalog.IsSelectable(def, Records);
            bool isCurrent = sel.Selected == def;

            if (nameText != null) nameText.text = $"{def.displayName}  ·  {CategoryLabel(def.category)}";
            if (detailText != null) detailText.text = $"{def.description}\n길이 {def.length:0.#}m · 날개폭 {def.wingspan:0.#}m";
            if (unlockText != null)
                unlockText.text = !def.IsImplemented ? "개발 예정 기체입니다."
                    : selectable ? "성능 막대는 게임 내 상대 성능입니다(실제 항공기 제원 아님)."
                    : $"해금 조건: {AircraftRules.Describe(def.unlock)}";

            bool hasBars = _bars.TryGetValue(def.id, out AircraftStatBars b);
            float[] values = hasBars ? new[] { b.Speed, b.Acceleration, b.Agility, b.Stability, b.Size } : new float[5];
            for (int i = 0; i < statBars.Length && i < values.Length; i++)
                if (statBars[i] != null) statBars[i].fillAmount = values[i];

            if (selectButton != null) selectButton.interactable = selectable && !isCurrent;
            if (selectButtonLabel != null) selectButtonLabel.text = isCurrent ? "선택됨" : "SELECT";
            if (flyFreeButton != null) flyFreeButton.interactable = selectable;
            if (flySkylineButton != null) flySkylineButton.interactable = selectable && AircraftRules.CanPlay(FlightMode.SkylineRun, def.tags, out _);
            if (noticeText != null)
                noticeText.text = selectable && !AircraftRules.CanPlay(FlightMode.SkylineRun, def.tags, out string reason) ? reason : string.Empty;

            foreach ((AircraftDefinition d, Button _, Image bg) in _cards)
                if (bg != null) bg.color = d == def ? CardActive : sel.Catalog.IsSelectable(d, Records) ? CardNormal : CardLocked;

            preview?.Show(def);
        }

        /// <summary>SELECT 버튼.</summary>
        public void SelectViewing()
        {
            if (_viewing != null && Selection != null && Selection.Select(_viewing)) View(_viewing);
        }

        /// <summary>FLY(FREE FLIGHT) — 보고 있는 기체를 선택하고 바로 출발.</summary>
        public void FlyFree() => Fly(FlightMode.FreeFlight);

        public void FlySkyline() => Fly(FlightMode.SkylineRun);

        private void Fly(FlightMode mode)
        {
            if (_viewing == null || Selection == null || !Selection.Select(_viewing) && Selection.Selected != _viewing) return;
            if (!AircraftRules.CanPlay(mode, _viewing.tags, out string reason))
            {
                if (noticeText != null) noticeText.text = reason;
                return;
            }
            GameApp.Instance.PendingMode = mode;
            SceneFlow.LoadFlight();
        }

        private static void SetChildText(Transform root, string child, string value)
        {
            Transform t = root.Find(child);
            if (t != null && t.TryGetComponent(out Text text)) text.text = value;
        }

        public static string CategoryLabel(AircraftCategory c)
        {
            switch (c)
            {
                case AircraftCategory.LightProp: return "소형 프로펠러기";
                case AircraftCategory.Aerobatic: return "곡예기";
                case AircraftCategory.FighterJet: return "전투기형 제트";
                case AircraftCategory.RegionalAirliner: return "지역 여객기";
                case AircraftCategory.Airliner: return "중형 여객기";
                case AircraftCategory.WidebodyAirliner: return "대형 여객기";
                default: return "화물기";
            }
        }
    }
}
