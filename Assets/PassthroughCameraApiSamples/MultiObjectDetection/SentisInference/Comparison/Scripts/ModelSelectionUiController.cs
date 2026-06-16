// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using PassthroughCameraSamples.StartScene;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [DisallowMultipleComponent]
    public class ModelSelectionUiController : MonoBehaviour
    {
        private const float BaseWorldCanvasScale = 0.0016f;

        [Header("Placement")]
        [SerializeField] private float distanceFromCamera = 1.4f;
        [SerializeField] private float verticalOffset = -0.15f;
        [SerializeField] private Vector3 panelScale = new(0.55f, 0.55f, 0.55f);

        private readonly List<Button> m_profileButtons = new();
        private readonly List<ModelComparisonProfile> m_profiles = new();

        private Action<ModelComparisonProfile> m_onConfirmed;
        private GameObject m_root;
        private RectTransform m_buttonsRoot;
        private Text m_titleText;
        private Text m_detailsText;
        private Button m_startButton;
        private ModelComparisonProfile m_selectedProfile;
        private OVRInputModule m_activeInputModule;
        private OVRRaycaster m_activeRaycaster;
        private readonly List<OVRInputModule.InputSource> m_temporarilyUntrackedInputSources = new();

        public void Show(ModelComparisonRegistry registry, Action<ModelComparisonProfile> onConfirmed)
        {
            m_onConfirmed = onConfirmed;
            m_profiles.Clear();

            if (registry != null)
            {
                var profiles = registry.GetValidProfiles();
                for (var i = 0; i < profiles.Count; i++)
                {
                    m_profiles.Add(profiles[i]);
                }
            }

            EnsureUi();
            PositionInFrontOfCamera();
            ConfigureOvrInput();
            RebuildProfileButtons();

            m_root.SetActive(true);
            SetSelectedProfile(m_profiles.Count > 0 ? m_profiles[0] : null);
            Debug.Log("Model selection UI shown with " + m_profiles.Count + " model profiles.");
        }

        public void Hide()
        {
            if (m_root != null)
            {
                m_root.SetActive(false);
            }

            RestoreTrackedInputSources();
            if (m_activeInputModule != null && m_activeInputModule.activeGraphicRaycaster == m_activeRaycaster)
            {
                m_activeInputModule.activeGraphicRaycaster = null;
            }
        }

        private void OnDisable()
        {
            RestoreTrackedInputSources();
        }

        private void OnDestroy()
        {
            RestoreTrackedInputSources();
        }

        private void LateUpdate()
        {
            if (m_root == null || !m_root.activeInHierarchy)
            {
                return;
            }

            PositionInFrontOfCamera(false);
        }

        private void EnsureUi()
        {
            if (m_root != null)
            {
                return;
            }

            var font = ResolveFont();
            m_root = new GameObject("DeveloperComparisonModelSelection");
            m_root.transform.SetParent(transform, false);

            var canvas = m_root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = ResolveEventCamera();
            canvas.sortingOrder = 50;
            var raycaster = m_root.AddComponent<OVRRaycaster>();

            var scaler = m_root.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            var rect = m_root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(860f, 600f);
            rect.localScale = Vector3.one * BaseWorldCanvasScale;

            var panel = CreateRect("Panel", rect);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.03f, 0.04f, 0.05f, 0.92f);

            m_titleText = CreateText("Title", panel, font, 30, FontStyle.Bold);
            m_titleText.alignment = TextAnchor.MiddleCenter;
            m_titleText.text = "Select Detection Model";
            SetTopLeftRect(m_titleText.rectTransform, 28f, 20f, 804f, 46f);

            m_buttonsRoot = CreateRect("ModelButtons", panel);
            SetTopLeftRect(m_buttonsRoot, 34f, 92f, 420f, 400f);

            m_detailsText = CreateText("Details", panel, font, 18, FontStyle.Normal);
            m_detailsText.alignment = TextAnchor.UpperLeft;
            SetTopLeftRect(m_detailsText.rectTransform, 490f, 92f, 330f, 300f);

            m_startButton = CreateButton("Start Detection", panel, font, () =>
            {
                if (m_selectedProfile == null)
                {
                    Debug.LogWarning("Start Detection clicked, but no model profile is selected.");
                    return;
                }

                Debug.Log("Start Detection clicked for model profile: " + m_selectedProfile.BenchmarkName);
                Hide();
                m_onConfirmed?.Invoke(m_selectedProfile);
            }, 14);
            SetTopLeftRect(m_startButton.GetComponent<RectTransform>(), 620f, 450f, 170f, 42f);

            var laserPointer = FindLaserPointer();
            if (laserPointer != null)
            {
                raycaster.pointer = laserPointer.gameObject;
            }
        }

        private void RebuildProfileButtons()
        {
            for (var i = 0; i < m_profileButtons.Count; i++)
            {
                Destroy(m_profileButtons[i].gameObject);
            }
            m_profileButtons.Clear();

            for (var i = 0; i < m_profiles.Count; i++)
            {
                var profile = m_profiles[i];
                var button = CreateButton(profile.BenchmarkName, m_buttonsRoot, ResolveFont(), () =>
                {
                    Debug.Log("Model selection button clicked: " + profile.BenchmarkName);
                    SetSelectedProfile(profile);
                });
                var buttonRect = button.GetComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(0f, 1f);
                buttonRect.anchorMax = new Vector2(1f, 1f);
                buttonRect.pivot = new Vector2(0.5f, 1f);
                buttonRect.offsetMin = new Vector2(0f, -52f - i * 64f);
                buttonRect.offsetMax = new Vector2(0f, -i * 64f);
                m_profileButtons.Add(button);
            }
        }

        private void SetSelectedProfile(ModelComparisonProfile profile)
        {
            m_selectedProfile = profile;
            if (m_startButton != null)
            {
                m_startButton.interactable = profile != null;
            }

            if (m_detailsText == null)
            {
                return;
            }

            if (profile == null)
            {
                m_detailsText.text = "No valid model profiles are assigned in the registry.";
                return;
            }

            m_detailsText.text =
                "Selected model\n" +
                profile.BenchmarkName + "\n\n" +
                profile.Summary + "\n" +
                "Input size: " + profile.InputSize.x + " x " + profile.InputSize.y + "\n" +
                "IoU threshold: " + profile.IouThreshold.ToString("0.00") + "\n" +
                "Score threshold: " + profile.ScoreThreshold.ToString("0.00") + "\n\n" +
                profile.Notes;
        }

        private void ConfigureOvrInput()
        {
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            }

            if (EventSystem.current != eventSystem)
            {
                EventSystem.current = eventSystem;
            }

            var inputModule = eventSystem.GetComponent<OVRInputModule>();
            if (inputModule == null)
            {
                inputModule = eventSystem.gameObject.AddComponent<OVRInputModule>();
            }

            inputModule.allowActivationOnMobileDevice = true;

            var laserPointer = FindLaserPointer();
            if (laserPointer == null)
            {
                laserPointer = CreateLaserPointer();
            }

            laserPointer.gameObject.SetActive(true);
            laserPointer.enabled = true;
            if (laserPointer.CursorVisual != null)
            {
                laserPointer.CursorVisual.SetActive(true);
            }

            PreferRightHandInputSources();

            var rayTransform = ResolveRightHandRayTransform();
            inputModule.rayTransform = rayTransform;
            inputModule.m_Cursor = laserPointer;
            m_activeInputModule = inputModule;

            var raycaster = m_root != null ? m_root.GetComponent<OVRRaycaster>() : null;
            if (raycaster != null && laserPointer != null)
            {
                raycaster.pointer = laserPointer.gameObject;
                inputModule.activeGraphicRaycaster = raycaster;
                m_activeRaycaster = raycaster;
            }

            Debug.Log("Developer Comparison pointer source selected: " + rayTransform.name);
            Debug.Log("Developer Comparison active ray/interactor name: " + rayTransform.name);
        }

        private void PreferRightHandInputSources()
        {
            RestoreTrackedInputSources();

            var selectedSourceName = string.Empty;
            var hands = FindObjectsByType<OVRHand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < hands.Length; i++)
            {
                var hand = hands[i];
                if (hand == null)
                {
                    continue;
                }

                if (hand.GetHand() == OVRPlugin.Hand.HandRight)
                {
                    OVRInputModule.TrackInputSource(hand);
                    selectedSourceName = string.IsNullOrEmpty(selectedSourceName) ? hand.name : selectedSourceName;
                    continue;
                }

                if (hand.GetHand() == OVRPlugin.Hand.HandLeft)
                {
                    OVRInputModule.UntrackInputSource(hand);
                    m_temporarilyUntrackedInputSources.Add(hand);
                }
            }

            var controllers = FindObjectsByType<OVRControllerHelper>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                if (controller.GetHand() == OVRPlugin.Hand.HandRight)
                {
                    OVRInputModule.TrackInputSource(controller);
                    selectedSourceName = string.IsNullOrEmpty(selectedSourceName) ? controller.name : selectedSourceName;
                    continue;
                }

                if (controller.GetHand() == OVRPlugin.Hand.HandLeft)
                {
                    OVRInputModule.UntrackInputSource(controller);
                    m_temporarilyUntrackedInputSources.Add(controller);
                }
            }

            if (!string.IsNullOrEmpty(selectedSourceName))
            {
                Debug.Log("Developer Comparison right-hand UI input source selected: " + selectedSourceName);
            }
        }

        private void RestoreTrackedInputSources()
        {
            for (var i = 0; i < m_temporarilyUntrackedInputSources.Count; i++)
            {
                var inputSource = m_temporarilyUntrackedInputSources[i];
                if (inputSource != null && inputSource.IsValid())
                {
                    OVRInputModule.TrackInputSource(inputSource);
                }
            }

            m_temporarilyUntrackedInputSources.Clear();
        }

        private static Transform ResolveRightHandRayTransform()
        {
            var rightHandPointer = ResolveRightHandPointerPose();
            if (rightHandPointer != null)
            {
                return rightHandPointer;
            }

            var rightControllerPointer = ResolveRightControllerPointer();
            if (rightControllerPointer != null)
            {
                return rightControllerPointer;
            }

            var cameraRig = FindFirstObjectByType<OVRCameraRig>();
            if (cameraRig != null)
            {
                if (cameraRig.rightHandAnchor != null)
                {
                    return cameraRig.rightHandAnchor;
                }

                if (cameraRig.rightControllerAnchor != null)
                {
                    return cameraRig.rightControllerAnchor;
                }

                if (cameraRig.leftHandAnchor != null)
                {
                    Debug.LogWarning("Right hand anchor missing. Developer Comparison UI is falling back to left hand anchor.");
                    return cameraRig.leftHandAnchor;
                }

                if (cameraRig.centerEyeAnchor != null)
                {
                    Debug.LogWarning("Hand anchors missing. Developer Comparison UI is falling back to center eye anchor.");
                    return cameraRig.centerEyeAnchor;
                }
            }

            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform;
            }

            var fallbackRay = new GameObject("DeveloperComparisonFallbackRay").transform;
            fallbackRay.position = Vector3.zero;
            fallbackRay.rotation = Quaternion.identity;
            return fallbackRay;
        }

        private static Transform ResolveRightHandPointerPose()
        {
            var hands = FindObjectsByType<OVRHand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < hands.Length; i++)
            {
                var hand = hands[i];
                if (hand != null
                    && hand.GetHand() == OVRPlugin.Hand.HandRight
                    && hand.IsActive()
                    && hand.IsPointerPoseValid)
                {
                    return hand.GetPointerRayTransform();
                }
            }

            return null;
        }

        private static Transform ResolveRightControllerPointer()
        {
            var controllers = FindObjectsByType<OVRControllerHelper>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller != null
                    && controller.GetHand() == OVRPlugin.Hand.HandRight
                    && controller.IsActive())
                {
                    return controller.GetPointerRayTransform();
                }
            }

            return null;
        }

        private static LaserPointer FindLaserPointer()
        {
            var laserPointers = FindObjectsByType<LaserPointer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < laserPointers.Length; i++)
            {
                if (laserPointers[i] != null && laserPointers[i].name == "LaserPointer")
                {
                    return laserPointers[i];
                }
            }

            return laserPointers.Length > 0 ? laserPointers[0] : null;
        }

        private static LaserPointer CreateLaserPointer()
        {
            var pointerObject = new GameObject("DeveloperComparisonLaserPointer");
            var lineRenderer = pointerObject.AddComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.widthMultiplier = 0.01f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));

            var cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cursor.name = "DeveloperComparisonCursor";
            cursor.transform.SetParent(pointerObject.transform, false);
            cursor.transform.localScale = Vector3.one * 0.025f;
            var collider = cursor.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var laserPointer = pointerObject.AddComponent<LaserPointer>();
            laserPointer.CursorVisual = cursor;
            return laserPointer;
        }

        private void PositionInFrontOfCamera(bool logPlacement = true)
        {
            if (m_root == null)
            {
                return;
            }

            var target = m_root.transform;
            var canvas = m_root.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.worldCamera = ResolveEventCamera();
            }

            var cameraTransform = ResolveCameraTransform();
            target.localScale = Vector3.Scale(Vector3.one * BaseWorldCanvasScale, panelScale);

            if (cameraTransform == null)
            {
                target.localPosition = new Vector3(0f, 1.35f + verticalOffset, distanceFromCamera);
                target.localRotation = Quaternion.identity;
            }
            else
            {
                target.position = cameraTransform.position
                    + cameraTransform.forward * distanceFromCamera
                    + cameraTransform.up * verticalOffset;
                target.rotation = Quaternion.LookRotation(target.position - cameraTransform.position, cameraTransform.up);
            }

            if (logPlacement)
            {
                Debug.Log("Model selection UI positioned in front of camera");
                Debug.Log("Model selection UI world position: " + target.position);
                Debug.Log("Model selection UI world scale: " + target.lossyScale);
            }
        }

        private static Transform ResolveCameraTransform()
        {
            var cameraRig = FindFirstObjectByType<OVRCameraRig>();
            if (cameraRig != null && cameraRig.centerEyeAnchor != null)
            {
                return cameraRig.centerEyeAnchor;
            }

            return Camera.main != null ? Camera.main.transform : null;
        }

        private static Camera ResolveEventCamera()
        {
            var cameraTransform = ResolveCameraTransform();
            if (cameraTransform != null && cameraTransform.TryGetComponent<Camera>(out var camera))
            {
                return camera;
            }

            return Camera.main;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Text CreateText(string name, Transform parent, Font font, int fontSize, FontStyle style)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(string label, Transform parent, Font font, UnityEngine.Events.UnityAction onClick, int fontSize = 20)
        {
            var rect = CreateRect(label + " Button", parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.20f, 0.28f, 0.95f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var text = CreateText("Text", rect, font, fontSize, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            SetRect(text.rectTransform, 12f, -8f, -12f, 8f);
            return button;
        }

        private static void SetRect(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private static void SetTopLeftRect(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static Font ResolveFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }
    }
}
