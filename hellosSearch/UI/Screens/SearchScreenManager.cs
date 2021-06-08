using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HMUI;
using VRUIControls;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using HUI.UI.Screens;
using HUI.UI.Components;
using HUI.Utilities;
using Object = UnityEngine.Object;

namespace HUISearch.UI.Screens
{
    public class SearchScreenManager : ModifiableScreenManagerBase
    {
        public event Action SearchButtonPressed;
        public event Action CancelButtonPressed;

        public override string ScreenName => "Search Widget";
        protected override string AssociatedBSMLResource => "HUISearch.UI.Views.SearchScreenView.bsml";

        public string SearchText
        {
            get => _searchButtonText.textComponent.text;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _searchButtonText.textComponent.text = "Search";
                    _searchButtonText.textComponent.color = DefaultTextColour;
                }
                else
                {
                    _searchButtonText.textComponent.text = value;
                    _searchButtonText.textComponent.color = SearchTextColour;
                }
            }
        }

        private ScrollingText _searchButtonText;

#pragma warning disable CS0649
        [UIObject("search-button")]
        private GameObject _searchButton;
        [UIComponent("cancel-search-button")]
        private Button _cancelButton;
#pragma warning restore CS0649

        private static readonly Color DefaultTextColour = new Color(0.9f, 0.9f, 0.9f, 0.75f);
        private static readonly Color SearchTextColour = new Color(1f, 1f, 1f, 0.75f);

        public SearchScreenManager(
            MainMenuViewController mainMenuVC,
            SoloFreePlayFlowCoordinator soloFC,
            PartyFreePlayFlowCoordinator partyFC,
            LevelCollectionNavigationController levelCollectionNC,
            PhysicsRaycasterWithCache physicsRaycaster)
            : base(mainMenuVC, soloFC, partyFC, levelCollectionNC, physicsRaycaster, new Vector2(44f, 10f), new Vector3(0f, 0.15f, 3.1f), Quaternion.Euler(65f, 0f, 0f))
        {
            this._screen.name = "HUISearchScreen";
            this._animationHandler.UsePointerAnimations = false;

            Object.Destroy(_searchButton.GetComponent<ContentSizeFitter>());
            Object.Destroy(_searchButton.GetComponent<ButtonStaticAnimations>());

            Object.Destroy(_cancelButton.GetComponent<ContentSizeFitter>());
            Object.Destroy(_cancelButton.GetComponent<ButtonStaticAnimations>());

            // remove skew
            _searchButton.transform.Find("BG").GetComponent<ImageView>().SetSkew(0f);
            _searchButton.transform.Find("Underline").GetComponent<ImageView>().SetSkew(0f);
            _cancelButton.transform.Find("BG").GetComponent<ImageView>().SetSkew(0f);
            _cancelButton.transform.Find("Underline").GetComponent<ImageView>().SetSkew(0f);
            _cancelButton.transform.Find("Underline").GetComponentsInChildren<ImageView>();

            var container = _searchButton.transform.Find("Content");
            Object.DestroyImmediate(container.GetComponent<StackLayoutGroup>());

            var hlg = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.spacing = 2f;
            hlg.padding = new RectOffset(2, 1, 1, 1);

            // search icon
            var icon = new GameObject("Icon").AddComponent<Image>();
            icon.sprite = Resources.FindObjectsOfTypeAll<Sprite>().First(x => x.name == "SearchIcon");
            icon.preserveAspect = true;

            var layoutElement = icon.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = 4f;
            layoutElement.preferredWidth = 4f;

            icon.transform.SetParent(container, false);

            // search text
            Object.Destroy(container.transform.Find("Text").gameObject);

            _searchButtonText = new GameObject("Text").AddComponent<ScrollingText>();
            _searchButtonText.textComponent.text = "Search";
            _searchButtonText.textComponent.fontSize = 4f;
            _searchButtonText.textComponent.alignment = TextAlignmentOptions.Midline;
            _searchButtonText.textComponent.fontStyle = FontStyles.UpperCase | FontStyles.Italic;
            _searchButtonText.textComponent.color = DefaultTextColour;

            layoutElement = _searchButtonText.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 40f;

            _searchButtonText.transform.SetParent(container, false);

            // button animations
            Color searchButtonColour = new Color(0.145f, 0.443f, 1f);
            var iconBtnAnims = _searchButton.gameObject.AddComponent<CustomIconButtonAnimations>();
            iconBtnAnims.HighlightedBGColour = searchButtonColour;
            iconBtnAnims.PressedBGColour = searchButtonColour;

            iconBtnAnims = _cancelButton.gameObject.AddComponent<CustomIconButtonAnimations>();
            iconBtnAnims.NormalIconColour = Color.white;
            iconBtnAnims.HighlightedBGColour = Color.red;
            iconBtnAnims.PressedBGColour = Color.red;
            iconBtnAnims.HighlightedLocalScale = new Vector3(1.1f, 1.1f, 1.1f);
        }

        [UIAction("search-button-clicked")]
        private void OnSearchButtonPressed() => this.CallAndHandleAction(SearchButtonPressed, nameof(SearchButtonPressed));

        [UIAction("cancel-search-button-clicked")]
        private void OnCancelButtonPressed() => this.CallAndHandleAction(CancelButtonPressed, nameof(CancelButtonPressed));
    }
}
