// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    public enum ModelComparisonLaunchMode
    {
        DefaultSampleMode,
        DeveloperComparisonMode
    }

    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    [DisallowMultipleComponent]
    public class ModelComparisonModeController : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private ModelComparisonLaunchMode m_mode = ModelComparisonLaunchMode.DefaultSampleMode;

        [Header("Developer Comparison")]
        [SerializeField] private ModelComparisonRegistry m_registry;
        [SerializeField] private ModelSelectionUiController m_selectionUi;
        [SerializeField] private bool m_autoCreateSelectionUi = true;

        private SentisInferenceRunManager m_runManager;
        private ModelComparisonProfile m_selectedProfile;
        private bool m_hasStartedSelection;
        private bool m_loggedMode;

        public ModelComparisonLaunchMode Mode => m_mode;
        public bool IsDeveloperComparisonMode => m_mode == ModelComparisonLaunchMode.DeveloperComparisonMode;
        public bool HasSelectedProfile => !IsDeveloperComparisonMode || m_selectedProfile != null;
        public ModelComparisonProfile SelectedProfile => m_selectedProfile;

        private void Awake()
        {
            LogStartupStateOnce();
        }

        public void BeginSelection(SentisInferenceRunManager runManager)
        {
            m_runManager = runManager;
            LogStartupStateOnce();

            if (!IsDeveloperComparisonMode)
            {
                Debug.Log("Default Sample Mode: model chooser disabled");
                return;
            }

            if (m_hasStartedSelection)
            {
                return;
            }

            m_hasStartedSelection = true;
            m_selectedProfile = null;
            Debug.Log("DeveloperComparisonMode enabled. Waiting for runtime model selection.");

            if (m_registry == null)
            {
                Debug.LogError("DeveloperComparisonMode is enabled, but no ModelComparisonRegistry is assigned.");
                return;
            }

            if (m_registry.Count == 0)
            {
                Debug.LogError("DeveloperComparisonMode is enabled, but the assigned ModelComparisonRegistry has no valid profiles.");
                return;
            }

            var selectionUi = GetSelectionUi();
            if (selectionUi == null)
            {
                Debug.LogError("DeveloperComparisonMode is enabled, but no ModelSelectionUiController is assigned or could be created.");
                return;
            }

            Debug.Log("Developer Comparison Mode: showing model chooser");
            m_runManager.SetComparisonSelectionActive(true);
            selectionUi.Show(m_registry, ConfirmSelectedProfile);
        }

        public string GetHierarchyPath()
        {
            return GetHierarchyPath(transform);
        }

        private ModelSelectionUiController GetSelectionUi()
        {
            if (m_selectionUi != null)
            {
                return m_selectionUi;
            }

            m_selectionUi = GetComponent<ModelSelectionUiController>();
            if (m_selectionUi == null && m_autoCreateSelectionUi)
            {
                m_selectionUi = gameObject.AddComponent<ModelSelectionUiController>();
            }

            return m_selectionUi;
        }

        private void ConfirmSelectedProfile(ModelComparisonProfile profile)
        {
            if (profile == null)
            {
                Debug.LogError("Cannot confirm model selection because profile is null.");
                return;
            }

            if (m_runManager == null)
            {
                Debug.LogError("Cannot apply selected model profile because SentisInferenceRunManager is not assigned.");
                return;
            }

            if (!m_runManager.ApplyModelProfile(profile))
            {
                return;
            }

            m_selectedProfile = profile;
            Debug.Log("Selected model profile: " + profile.BenchmarkName);
            Debug.Log("Runtime model selection complete; starting detection");
            m_runManager.StartDetectionFromComparisonMode();
        }

        private void LogStartupStateOnce()
        {
            if (m_loggedMode)
            {
                return;
            }

            Debug.Log("ModelComparisonModeController mode: " + m_mode);
            Debug.Log(
                "ModelComparisonModeController startup | object: " + gameObject.name +
                " | scene: " + gameObject.scene.name +
                " | hierarchy: " + GetHierarchyPath() +
                " | current mode value: " + m_mode +
                " | registry assigned: " + (m_registry != null) +
                " | registry profile count: " + (m_registry != null ? m_registry.Count : 0) +
                " | selection UI assigned: " + (m_selectionUi != null) +
                " | auto create selection UI: " + m_autoCreateSelectionUi);
            if (!IsDeveloperComparisonMode)
            {
                Debug.Log("Default Sample Mode: model chooser disabled");
            }

            m_loggedMode = true;
        }

        private static string GetHierarchyPath(Transform current)
        {
            if (current == null)
            {
                return "<null>";
            }

            var path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }

            return path;
        }
    }
}
