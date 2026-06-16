// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Unity.InferenceEngine;
using UnityEngine;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    public enum ModelComparisonOutputMode
    {
        Auto,
        MetaSampleRawOutput,
        YoloNmsSingleOutput
    }

    [Serializable]
    public class ModelComparisonProfile
    {
        [SerializeField] private string m_displayName;
        [SerializeField] private string m_modelTypeName;
        [SerializeField] private ModelAsset m_sentisModel;
        [SerializeField] private ModelAsset m_onnxModel;
        [SerializeField] private string m_runtimeOnnxPath;
        [SerializeField] private string m_expectedOnnxPath;
        [SerializeField] private string m_expectedSentisPath;
        [SerializeField] private string m_expectedLabelsPath;
        [SerializeField] private TextAsset m_labelsAsset;
        [SerializeField] private ModelComparisonOutputMode m_outputMode = ModelComparisonOutputMode.Auto;
        [SerializeField] private Vector2Int m_inputSize = new Vector2Int(416, 416);
        [SerializeField] private BackendType m_backend = BackendType.CPU;
        [SerializeField, Range(0f, 1f)] private float m_iouThreshold = 0.6f;
        [SerializeField, Range(0f, 1f)] private float m_scoreThreshold = 0.23f;
        [SerializeField, TextArea] private string m_notes;

        [NonSerialized] private bool m_hasLoggedMissingReferenceWarnings;

        public string DisplayName => m_displayName;
        public string ModelTypeName => m_modelTypeName;
        public ModelAsset SentisModel => m_sentisModel;
        public ModelAsset OnnxModel => m_onnxModel;
        public string RuntimeOnnxPath => m_runtimeOnnxPath;
        public string ExpectedOnnxPath => m_expectedOnnxPath;
        public string ExpectedSentisPath => m_expectedSentisPath;
        public string ExpectedLabelsPath => m_expectedLabelsPath;
        public TextAsset LabelsAsset => m_labelsAsset;
        public ModelComparisonOutputMode OutputMode => m_outputMode;
        public Vector2Int InputSize => m_inputSize;
        public BackendType Backend => m_backend;
        public float IouThreshold => m_iouThreshold;
        public float ScoreThreshold => m_scoreThreshold;
        public string Notes => m_notes;

        public string BenchmarkName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(m_displayName))
                {
                    return m_displayName;
                }

                if (m_sentisModel != null)
                {
                    return m_sentisModel.name;
                }

                return string.IsNullOrWhiteSpace(m_modelTypeName) ? "UnknownModel" : m_modelTypeName;
            }
        }

        public string Summary
        {
            get
            {
                var modelType = string.IsNullOrWhiteSpace(m_modelTypeName) ? "model type unknown" : m_modelTypeName;
                return modelType + " | " + m_backend + " | " + m_outputMode + " | score " + m_scoreThreshold.ToString("0.00");
            }
        }

        public bool HasRuntimeModel => m_sentisModel != null && m_labelsAsset != null;

        public void LogMissingReferenceWarnings()
        {
            if (m_hasLoggedMissingReferenceWarnings)
            {
                return;
            }

            m_hasLoggedMissingReferenceWarnings = true;
            LogMissingReferenceWarning(m_sentisModel, "Sentis", m_expectedSentisPath);
            LogMissingReferenceWarning(m_labelsAsset, "labels", m_expectedLabelsPath);
            LogMissingReferenceWarning(m_onnxModel, "ONNX metadata", m_expectedOnnxPath);
        }

        private void LogMissingReferenceWarning(UnityEngine.Object asset, string assetType, string expectedPath)
        {
            if (asset != null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(expectedPath))
            {
                expectedPath = "not specified";
            }

            Debug.LogWarning(
                "Model comparison profile '" + BenchmarkName +
                "' is missing " + assetType +
                " reference. Expected path: " + expectedPath);
        }
    }
}
