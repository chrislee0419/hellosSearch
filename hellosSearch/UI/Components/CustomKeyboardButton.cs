using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HMUI;
using HUI.UI.Components;
using HUI.Utilities;

namespace HUISearch.UI.Components
{
    [RequireComponent(typeof(RectTransform), typeof(CustomButtonAnimations))]
    internal abstract class CustomKeyboardButton : MonoBehaviour
    {
        public RectTransform rectTransform { get; private set; }

        public Color SelectedColour
        {
            get => _btnAnims.HighlightedBGColour;
            set
            {
                _btnAnims.HighlightedBGColour = value;
                _btnAnims.PressedBGColour = value;
            }
        }

        protected Button _button;
        protected TextMeshProUGUI _text;
        protected CustomButtonAnimations _btnAnims;

        protected virtual void Awake()
        {
            rectTransform = this.GetComponent<RectTransform>();

            _button = this.GetComponent<NoTransitionsButton>();
            _button.onClick.RemoveAllListeners();

            _text = this.GetComponentInChildren<TextMeshProUGUI>();

            Color bgColour = new Color(0.116f, 0.354f, 0.8f);
            _btnAnims = this.gameObject.GetComponent<CustomButtonAnimations>();
            _btnAnims.NormalBGColour = Color.clear;
            _btnAnims.DisabledBGColour = Color.clear;
            _btnAnims.HighlightedBGColour = bgColour;
            _btnAnims.PressedBGColour = bgColour;

            RectTransform bg = rectTransform.Find("BG") as RectTransform;
            bg.sizeDelta = new Vector2(-0.4f, -0.4f);

            RectTransform stroke = rectTransform.Find("Stroke") as RectTransform;
            stroke.sizeDelta = Vector2.zero;
        }
    }

    internal class CustomKeyboardKeyButton : CustomKeyboardButton
    {
        public event Action<char> KeyPressed;

        private char _key;
        public char Key
        {
            get => _key;
            set
            {
                _key = value;

                if (!_showAltKey)
                    _text.text = _key.ToString();
            }
        }

        private char _altKey;
        public char AltKey
        {
            get => _altKey;
            set
            {
                _altKey = value;

                if (_showAltKey)
                {
                    if (_altKey == 0)
                    {
                        _button.interactable = false;
                        _text.text = " ";
                    }
                    else
                    {
                        _button.interactable = true;
                        _text.text = _altKey.ToString();
                    }
                }
            }
        }

        private bool _showAltKey = false;
        public bool ShowAltKey
        {
            get => _showAltKey;
            set
            {
                if (_showAltKey == value)
                    return;

                _showAltKey = value;

                if (_altKey == 0)
                {
                    _button.interactable = !_showAltKey;
                    _text.text = _showAltKey ? " " : _key.ToString();
                }
                else
                {
                    _button.interactable = true;
                    _text.text = _showAltKey ? _altKey.ToString() : _key.ToString();
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();

            _button.onClick.AddListener(() => this.CallAndHandleAction(KeyPressed, nameof(KeyPressed), _showAltKey ? AltKey : Key));
        }
    }

    internal class CustomKeyboardActionButton : CustomKeyboardButton
    {
        public event Action ButtonPressed;

        public string Text
        {
            get => _text.text;
            set => _text.text = value;
        }

        protected override void Awake()
        {
            base.Awake();

            _button.onClick.AddListener(() => this.CallAndHandleAction(ButtonPressed, nameof(ButtonPressed)));
        }
    }
}
