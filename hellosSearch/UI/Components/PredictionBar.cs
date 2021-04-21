using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Polyglot;
using HMUI;
using HUI.UI.Components;
using HUI.Utilities;
using static HUISearch.Search.WordPredictionEngine;

namespace HUISearch.UI.Components
{
    [RequireComponent(typeof(RectTransform), typeof(HorizontalLayoutGroup))]
    public class PredictionBar : MonoBehaviour
    {
        public event Action<SuggestedWord> PredictionButtonPressed;

        public RectTransform RectTransform { get; private set; }

        private Stack<PredictionButton> _unusedButtons = new Stack<PredictionButton>();
        private List<PredictionButton> _predictionButtons = new List<PredictionButton>();

        private Transform _containerTransform;

        private const float ButtonSpacing = 1f;
        private static readonly Color DefaultPredictionButtonNormalColour = new Color(0.6f, 0.6f, 0.8f, 0.25f);
        private static readonly Color DefaultPredictionButtonHighlightedColour = new Color(0.6f, 0.6f, 0.8f, 0.75f);
        private static readonly Color FuzzyMatchPredictionButtonNormalColour = new Color(0.8f, 0.5f, 0.6f, 0.25f);
        private static readonly Color FuzzyMatchPredictionButtonHighlightedColour = new Color(0.8f, 0.5f, 0.6f, 0.75f);

        private void Awake()
        {
            RectTransform = this.GetComponent<RectTransform>();

            HorizontalLayoutGroup hlg = this.GetComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            hlg.childAlignment = TextAnchor.MiddleLeft;

            var go = new GameObject("Container", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();

            _containerTransform = go.transform;
            _containerTransform.SetParent(RectTransform, false);

            hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            hlg.spacing = ButtonSpacing;
        }

        public void ClearPredictionButtons()
        {
            // preserve button order, so the top of the stack always contains the next leftmost button
            foreach (var oldButton in _predictionButtons.Reverse<PredictionButton>())
            {
                oldButton.SetActive(false);
                _unusedButtons.Push(oldButton);
            }
            _predictionButtons.Clear();
        }

        public void ClearAndSetPredictionButtons(IEnumerable<SuggestedWord> predictedWords)
        {
            // NOTE: searchText should be lower-cased (keyboard sends lowercase characters)
            ClearPredictionButtons();

            // estimate total width to break early, instead of creating a button for every predicted word
            // first button will never have the spacer, so we start from -ButtonSpacing
            float estimatedWidth = -ButtonSpacing;

            // create new or re-use old buttons
            foreach (SuggestedWord suggestedWord in predictedWords)
            {
                PredictionButton button;
                if (_unusedButtons.Count > 0)
                {
                    button = _unusedButtons.Pop();
                    button.SetActive(true);
                }
                else
                {
                    button = new PredictionButton(_containerTransform);
                    button.Pressed += delegate (SuggestedWord word)
                    {
                        // exception handling done in Pressed event
                        PredictionButtonPressed?.Invoke(word);
                    };
                }

                button.SuggestedWord = suggestedWord;

                estimatedWidth += button.PreferredWidth + ButtonSpacing;
                if (estimatedWidth > RectTransform.rect.width)
                {
                    button.SetActive(false);
                    _unusedButtons.Push(button);

                    break;
                }

                _predictionButtons.Add(button);
            }

            // force rebuild needed, otherwise the layout gets messed up for some reason
            // (it acts like the preferred widths lag behind one layout pass)
            LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform);
        }

        private class PredictionButton
        {
            public event Action<SuggestedWord> Pressed;
            public float PreferredWidth { get => _text.preferredWidth + 2 * XPadding; }

            private SuggestedWord _suggestedWord;
            public SuggestedWord SuggestedWord
            {
                get => _suggestedWord;
                set
                {
                    _suggestedWord = value;

                    _text.text = _suggestedWord.Word;
                    SetBGColours(_suggestedWord.Type == SuggestionType.FuzzyMatch);
                }
            }

            private Button _button;
            private TextMeshProUGUI _text;
            private CustomButtonAnimations _buttonAnims;

            private static Button ButtonPrefab;
            private const int XPadding = 2;

            public PredictionButton(Transform parent)
            {
                if (ButtonPrefab == null)
                    ButtonPrefab = Resources.FindObjectsOfTypeAll<Button>().Last(x => x.name == "PracticeButton");

                // straight rip from bsml:
                // https://github.com/monkeymanboy/BeatSaberMarkupLanguage/blob/master/BeatSaberMarkupLanguage/Tags/ButtonTag.cs
                _button = Instantiate(ButtonPrefab, parent, false);
                _button.name = "SearchPredictionBarButton";
                _button.interactable = true;

                Destroy(_button.GetComponent<ButtonStaticAnimations>());
                _buttonAnims = _button.gameObject.AddComponent<CustomButtonAnimations>();
                SetBGColours(true);

                Transform contentTransform = _button.transform.Find("Content");
                contentTransform.GetComponent<StackLayoutGroup>().padding = new RectOffset(XPadding, XPadding, 0, 0);
                Destroy(contentTransform.GetComponent<LayoutElement>());

                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => this.CallAndHandleAction(Pressed, nameof(Pressed), _suggestedWord));

                LocalizedTextMeshProUGUI localizer = _button.GetComponentInChildren<LocalizedTextMeshProUGUI>(true);
                if (localizer != null)
                    Destroy(localizer);

                _text = _button.GetComponentInChildren<TextMeshProUGUI>();
                _text.richText = true;
                _text.enableWordWrapping = false;
                _text.fontSize = 2.8f;

                ContentSizeFitter buttonSizeFitter = _button.gameObject.AddComponent<ContentSizeFitter>();
                buttonSizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                buttonSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

                _button.GetComponent<StackLayoutGroup>();
            }

            public void SetActive(bool active) => _button.gameObject.SetActive(active);

            private void SetBGColours(bool useFuzzyMatchColours)
            {
                if (useFuzzyMatchColours)
                {
                    _buttonAnims.NormalBGColour = FuzzyMatchPredictionButtonNormalColour;
                    _buttonAnims.HighlightedBGColour = FuzzyMatchPredictionButtonHighlightedColour;
                    _buttonAnims.PressedBGColour = FuzzyMatchPredictionButtonHighlightedColour;
                }
                else
                {
                    _buttonAnims.NormalBGColour = DefaultPredictionButtonNormalColour;
                    _buttonAnims.HighlightedBGColour = DefaultPredictionButtonHighlightedColour;
                    _buttonAnims.PressedBGColour = DefaultPredictionButtonHighlightedColour;
                }
            }
        }
    }
}
