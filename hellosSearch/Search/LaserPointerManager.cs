using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;
using VRUIControls;
using HMUI;
using Libraries.HM.HMLib.VR;
using IPA.Utilities;
using HUISearch.HarmonyPatches;

namespace HUISearch.Search
{
    public class LaserPointerManager : IDisposable, ILateTickable
    {
        private bool _enabled = false;
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (!_initialized)
                    return;

                _enabled = value;

                VRInputModuleProcessPatch.ProcessHook -= Process;

                if (_laserPointerTransform != null)
                {
                    GameObject.Destroy(_laserPointerTransform.gameObject);
                    _laserPointerTransform = null;
                }

                if (value)
                {
                    VRInputModuleProcessPatch.ProcessHook += Process;

                    if (_offHandController == null)
                        _offHandController = VRControllerAccessor(ref _originalPointer) == _rightController ? _leftController : _rightController;

                    _laserPointerTransform = GameObject.Instantiate(_pointerPrefab, _offHandController.transform, false);
                    SetLaserPointerPositionAndScale(_defaultPointerLength);
                }
            }
        }

        private PointerEventData _pointerEventData;
        private List<RaycastResult> _raycastResults = new List<RaycastResult>();
        private List<Component> _componentsList = new List<Component>();

        private VRPointer _originalPointer;
        private VRController _leftController;
        private VRController _rightController;
        private VRController _offHandController;

        private bool _initialized = false;

        private Transform _laserPointerTransform;

        private static Transform _pointerPrefab;
        private static float _defaultPointerLength;
        private static float _pointerWidth;

        private static FieldAccessor<BaseInputModule, EventSystem>.Accessor EventSystemAccessor = FieldAccessor<BaseInputModule, EventSystem>.GetAccessor("m_EventSystem");
        private static FieldAccessor<VRInputModule, HapticFeedbackController>.Accessor HapticFeedbackControllerAccessor = FieldAccessor<VRInputModule, HapticFeedbackController>.GetAccessor("_hapticFeedbackController");
        private static FieldAccessor<VRInputModule, HapticPresetSO>.Accessor HapticPresetSOAccessor = FieldAccessor<VRInputModule, HapticPresetSO>.GetAccessor("_rumblePreset");
        private static FieldAccessor<VRPointer, VRController>.Accessor VRControllerAccessor = FieldAccessor<VRPointer, VRController>.GetAccessor("_vrController");

        private const int OffHandPointerId = 2;

        [Inject]
        public LaserPointerManager(VRInputModule vrInputModule)
        {
            _originalPointer = FieldAccessor<VRInputModule, VRPointer>.Get(ref vrInputModule, "_vrPointer");
            if (_originalPointer == null)
                return;

            _leftController = FieldAccessor<VRPointer, VRController>.Get(ref _originalPointer, "_leftVRController");
            _rightController = FieldAccessor<VRPointer, VRController>.Get(ref _originalPointer, "_rightVRController");

            _pointerPrefab = FieldAccessor<VRPointer, Transform>.Get(ref _originalPointer, "_cursorPrefab");
            _defaultPointerLength = FieldAccessor<VRPointer, float>.Get(ref _originalPointer, "_defaultLaserPointerLength");
            _pointerWidth = FieldAccessor<VRPointer, float>.Get(ref _originalPointer, "_laserPointerWidth");

            _initialized = _rightController != null && _leftController != null && _originalPointer != null && _pointerPrefab != null;

            if (!_initialized)
                Plugin.Log.Warn("Unable to initialize LaserPointerManager for two handed typing");
        }

        public void Dispose()
        {
            _enabled = false;

            VRInputModuleProcessPatch.ProcessHook -= Process;
            
            if (_laserPointerTransform != null)
            {
                GameObject.Destroy(_laserPointerTransform.gameObject);
                _laserPointerTransform = null;
            }
        }

        public void LateTick()
        {
            if (!_initialized || !_enabled)
                return;

            VRController lastOffHandController = _offHandController;
            VRController currentMainController = VRControllerAccessor(ref _originalPointer);
            _offHandController = currentMainController == _rightController ? _leftController : _rightController;

            if (lastOffHandController != _offHandController)
            {
                if (_laserPointerTransform == null)
                    _laserPointerTransform = GameObject.Instantiate(_pointerPrefab, _offHandController.transform, false);
                else
                    _laserPointerTransform.SetParent(_offHandController.transform, false);
                SetLaserPointerPositionAndScale(_defaultPointerLength);
            }
        }

        private void SetLaserPointerPositionAndScale(float pointerLength)
        {
            _laserPointerTransform.localPosition = new Vector3(0f, 0f, pointerLength / 2f);
            _laserPointerTransform.localScale = new Vector3(_pointerWidth * 0.3f, _pointerWidth * 0.3f, pointerLength);
        }

        public void Process(VRInputModule vrInputModule)
        {
            if (!_initialized || !_enabled || _offHandController == null)
                return;

            BaseInputModule baseInputModule = vrInputModule;
            EventSystem eventSystem = EventSystemAccessor(ref baseInputModule);
            if (_pointerEventData == null)
                _pointerEventData = new PointerEventData(eventSystem) { pointerId = OffHandPointerId };

            // perform raycast
            _pointerEventData.Reset();
            _pointerEventData.pointerCurrentRaycast = new RaycastResult
            {
                worldPosition = _offHandController.position,
                worldNormal = _offHandController.forward
            };
            _pointerEventData.scrollDelta = Vector2.zero;

            eventSystem.RaycastAll(_pointerEventData, _raycastResults);

            // reimplementation of FindFirstRaycast
            _pointerEventData.pointerCurrentRaycast = default;
            for (int i = 0; i < _raycastResults.Count; ++i)
            {
                if (_raycastResults[i].gameObject != null)
                {
                    _pointerEventData.pointerCurrentRaycast = _raycastResults[i];
                    break;
                }
            }

            _pointerEventData.delta = _pointerEventData.pointerCurrentRaycast.screenPosition - _pointerEventData.position;
            _pointerEventData.position = _pointerEventData.pointerCurrentRaycast.screenPosition;

            // handle pointer enter/exit on gameobjects
            HandlePointerExitAndEnter(vrInputModule, _pointerEventData, _pointerEventData.pointerCurrentRaycast.gameObject);

            // send raycast results to laser pointer
            if (float.IsNaN(_pointerEventData.pointerCurrentRaycast.worldPosition.x) || _laserPointerTransform == null)
                return;

            if (_pointerEventData.pointerCurrentRaycast.gameObject != null)
                SetLaserPointerPositionAndScale(_pointerEventData.pointerCurrentRaycast.distance);
            else
                SetLaserPointerPositionAndScale(_defaultPointerLength);
        }

        // reimplementation of VRInputModule's HandlePointerExitAndEnter
        // only functional change should be that we target the off-hand controller for haptics
        private void HandlePointerExitAndEnter(VRInputModule vrInputModule, PointerEventData eventData, GameObject newEnterTarget)
        {
            if (newEnterTarget == null || eventData.pointerEnter == null)
            {
                foreach (var hovered in eventData.hovered)
                    ExecuteEvents.Execute(hovered, eventData, ExecuteEvents.pointerExitHandler);
                eventData.hovered.Clear();

                if (newEnterTarget == null)
                {
                    eventData.pointerEnter = null;
                    return;
                }
            }

            // at this point, newEnterTarget cannot be null
            if (eventData.pointerEnter == newEnterTarget)
                return;

            GameObject commonRoot = null;
            Transform t = null;
            if (eventData.pointerEnter != null)
            {
                // reimplementation of BaseInputModule.FindCommonRoot
                Transform t1 = eventData.pointerEnter.transform;
                bool found = false;
                while (t1 != null)
                {
                    Transform t2 = newEnterTarget.transform;
                    while (t2 != null)
                    {
                        if (t1 == t2)
                        {
                            commonRoot = t1.gameObject;
                            found = true;
                            break;
                        }

                        t2 = t2.parent;
                    }

                    if (found)
                        break;
                    else
                        t1 = t1.parent;
                }

                t = eventData.pointerEnter.transform;
                while (t != null && commonRoot?.transform != t)
                {
                    ExecuteEvents.Execute(t.gameObject, eventData, ExecuteEvents.pointerExitHandler);
                    eventData.hovered.Remove(t.gameObject);
                    t = t.parent;
                }
            }

            if (!vrInputModule.enabled)
                return;

            bool hasTriggeredHapticPulse = false;

            eventData.pointerEnter = newEnterTarget;
            t = newEnterTarget.transform;

            while (t != null && t.gameObject != commonRoot)
            {
                _componentsList.Clear();
                t.gameObject.GetComponents(_componentsList);

                if (!hasTriggeredHapticPulse)
                {
                    foreach (var component in _componentsList)
                    {
                        Selectable selectable = component as Selectable;
                        Interactable interactable = component as Interactable;
                        CanvasGroup canvasGroup = component as CanvasGroup;

                        bool isSelectableInteractable = selectable != null && selectable.isActiveAndEnabled && selectable.interactable;
                        bool isInteractableInteractable = interactable != null && interactable.isActiveAndEnabled && interactable.interactable;
                        bool isCanvasGroupInteractable = canvasGroup?.interactable ?? true;
                        if ((isSelectableInteractable || isInteractableInteractable) && isCanvasGroupInteractable)
                        {
                            HapticFeedbackControllerAccessor(ref vrInputModule).PlayHapticFeedback(_offHandController.node, HapticPresetSOAccessor(ref vrInputModule));
                            hasTriggeredHapticPulse = true;
                            break;
                        }
                    }
                }

                ExecuteEvents.Execute(t.gameObject, eventData, ExecuteEvents.pointerEnterHandler);
                eventData.hovered.Add(t.gameObject);
                t = t.parent;
            }
        }
    }
}
