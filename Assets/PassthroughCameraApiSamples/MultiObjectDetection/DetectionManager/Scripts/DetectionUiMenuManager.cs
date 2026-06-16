// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using Meta.XR.Samples;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    public class DetectionUiMenuManager : MonoBehaviour
    {
        [Header("Ui elements ref.")]
        [SerializeField] private GameObject m_loadingPanel;
        [SerializeField] private GameObject m_initialPanel;
        [SerializeField] private GameObject m_noPermissionPanel;
        [SerializeField] private Text m_labelInformation;

        public bool IsInputActive { get; set; } = false;

        public UnityEvent<bool> OnPause;

        private bool m_initialMenu;

        // start menu
        private int m_objectsDetected = 0;
        private int m_objectsIdentified = 0;

        // pause menu
        public bool IsPaused { get; private set; } = true;
        private bool m_externalSelectionActive;
        private bool m_permissionsReady;

        #region Unity Functions
        private IEnumerator Start()
        {
            m_initialPanel.SetActive(false);
            m_noPermissionPanel.SetActive(false);
            m_loadingPanel.SetActive(false);

            // Wait for permissions
            OnNoPermissionMenu();
            while (!OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.Scene) || !OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.PassthroughCameraAccess))
            {
                yield return null;
            }
            m_permissionsReady = true;
            OnInitialMenu();
        }

        private void Update()
        {
            if (!IsInputActive)
                return;

            if (m_initialMenu)
            {
                InitialMenuUpdate();
            }
        }
        #endregion

        #region Ui state: No permissions Menu
        private void OnNoPermissionMenu()
        {
            if (m_externalSelectionActive)
            {
                HideAllPanels();
                return;
            }

            m_initialMenu = false;
            IsPaused = true;
            m_initialPanel.SetActive(false);
            m_noPermissionPanel.SetActive(true);
        }
        #endregion

        #region Ui state: Initial Menu

        private void OnInitialMenu()
        {
            if (m_externalSelectionActive)
            {
                HideAllPanels();
                return;
            }

            m_initialMenu = true;
            IsPaused = true;
            m_initialPanel.SetActive(true);
            m_noPermissionPanel.SetActive(false);
        }

        private void InitialMenuUpdate()
        {
            if (InputManager.IsButtonADownOrPinchStarted())
            {
                OnPauseMenu(false);
            }
        }

        public void StartDetectionFromExternalUi()
        {
            if (!m_permissionsReady)
            {
                Debug.LogWarning("DetectionUiMenuManager cannot start detection yet because permissions are not ready.");
                return;
            }

            if (!IsPaused)
            {
                return;
            }

            if (!m_initialMenu && !m_externalSelectionActive)
            {
                Debug.LogWarning("DetectionUiMenuManager cannot start detection yet because the initial menu is not ready.");
                return;
            }

            SetExternalSelectionActive(false);
            OnPauseMenu(false);
        }

        public void SetExternalSelectionActive(bool active)
        {
            m_externalSelectionActive = active;
            IsPaused = true;
            m_initialMenu = false;

            if (active)
            {
                HideAllPanels();
                if (m_labelInformation != null)
                {
                    m_labelInformation.gameObject.SetActive(false);
                }
                Debug.Log("Normal detection UI hidden while Developer Comparison model selection is active.");
            }
            else
            {
                if (m_labelInformation != null)
                {
                    m_labelInformation.gameObject.SetActive(true);
                }
                Debug.Log("Normal detection UI shown/resumed after Developer Comparison model selection.");
            }
        }

        private void HideAllPanels()
        {
            m_initialPanel.SetActive(false);
            m_noPermissionPanel.SetActive(false);
            m_loadingPanel.SetActive(false);
        }

        private void OnPauseMenu(bool visible)
        {
            m_initialMenu = false;
            IsPaused = visible;

            m_initialPanel.SetActive(false);
            m_noPermissionPanel.SetActive(false);

            OnPause?.Invoke(visible);
        }
        #endregion

        #region Ui state: detection information
        private void UpdateLabelInformation()
        {
            m_labelInformation.text = $"Unity Sentis version: 2.1.3\nAI model: Yolo\nDetecting objects: {m_objectsDetected}\nObjects identified: {m_objectsIdentified}";
        }

        public void OnObjectsDetected(int objects)
        {
            m_objectsDetected = objects;
            UpdateLabelInformation();
        }

        public void OnObjectsIndentified(int objects)
        {
            if (objects < 0)
            {
                // reset the counter
                m_objectsIdentified = 0;
            }
            else
            {
                m_objectsIdentified += objects;
            }
            UpdateLabelInformation();
        }
        #endregion
    }
}
