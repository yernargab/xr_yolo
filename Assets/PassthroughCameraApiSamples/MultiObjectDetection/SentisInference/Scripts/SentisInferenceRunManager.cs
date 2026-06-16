// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Meta.XR;
using Meta.XR.Samples;
using Unity.Collections;
using Unity.InferenceEngine;
using UnityEngine;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    public class SentisInferenceRunManager : MonoBehaviour
    {
        [SerializeField] private PassthroughCameraAccess m_cameraAccess;
        [SerializeField] private DetectionUiMenuManager m_uiMenuManager;
        [SerializeField] private DetectionManager m_detectionManager;

        [Header("Sentis Model config")]
        [SerializeField] private BackendType m_backend = BackendType.CPU;
        [SerializeField] private ModelAsset m_sentisModel;
        [SerializeField] private TextAsset m_labelsAsset;
        [SerializeField] private ModelComparisonOutputMode m_modelOutputMode = ModelComparisonOutputMode.Auto;
        [SerializeField, Range(0, 1)] private float m_iouThreshold = 0.6f;
        [SerializeField, Range(0, 1)] private float m_scoreThreshold = 0.23f;

        [Header("UI display references")]
        [SerializeField] private SentisInferenceUiManager m_uiInference;

        [Header("[Optional] Runtime Model Comparison")]
        [SerializeField] private ModelComparisonModeController m_modelComparisonModeController;

        [Header("[Optional] Benchmark Logging")]
        [SerializeField] private bool m_enableBenchmarkLogging = true;
        [SerializeField] private DetectionBenchmarkLogger m_benchmarkLogger;

        [Header("[Editor Only] Convert to Sentis")]
        public ModelAsset OnnxModel;


        private enum ModelOutputMode
        {
            MetaSampleThreeOutputs,
            YoloV8NmsSingleOutput
        }

        private Worker m_engine;
        private Vector2Int m_inputSize;
        private ModelOutputMode m_outputMode;
        private int m_loadedOutputCount;
        private string m_activeModelName;
        private readonly List<(int classId, Vector4 boundingBox)> m_detections = new List<(int classId, Vector4 boundingBox)>();


        [ContextMenu("DEBUG: Inspect ONNX and Sentis Models")]
        private void DebugInspectModels()
        {
            Debug.Log("========== SENTIS MODEL DEBUG START ==========");

            Debug.Log("Backend: " + m_backend);
            Debug.Log("Score threshold: " + m_scoreThreshold);
            Debug.Log("IoU threshold: " + m_iouThreshold);

            Debug.Log("Assigned Sentis Model: " + (m_sentisModel != null ? m_sentisModel.name : "NULL"));
            Debug.Log("Assigned ONNX Model: " + (OnnxModel != null ? OnnxModel.name : "NULL"));
            Debug.Log("Labels Asset: " + (m_labelsAsset != null ? m_labelsAsset.name : "NULL"));

            if (m_sentisModel != null)
            {
                Debug.Log("---- Current Sentis Model ----");
                TryPrintModelInfo(m_sentisModel);
            }
            else
            {
                Debug.LogWarning("m_sentisModel is NULL.");
            }

            if (OnnxModel != null)
            {
                Debug.Log("---- ONNX Model Field ----");
                TryPrintModelInfo(OnnxModel);
            }
            else
            {
                Debug.LogWarning("OnnxModel is NULL. Drag your YOLOv8 ONNX asset into the Onnx Model field.");
            }

            Debug.Log("========== SENTIS MODEL DEBUG END ==========");
        }


        private void TryPrintModelInfo(ModelAsset modelAsset)
        {
            try
            {
                var model = ModelLoader.Load(modelAsset);

                Debug.Log("ModelAsset name: " + modelAsset.name);

                Debug.Log("Input count: " + model.inputs.Count);

                for (int i = 0; i < model.inputs.Count; i++)
                {
                    Debug.Log(
                        "Input " + i +
                        " | name: " + GetMemberString(model.inputs[i], "name") +
                        " | shape: " + model.inputs[i].shape
                    );
                }

                Debug.Log("Output count: " + model.outputs.Count);

                for (int i = 0; i < model.outputs.Count; i++)
                {
                    var outputShape = TryGetModelOutputShape(model, i, out var shape) ? shape.ToString() : "unknown";
                    Debug.Log(
                        "Output " + i +
                        " | name: " + GetMemberString(model.outputs[i], "name") +
                        " | shape: " + outputShape +
                        " | dataType: " + GetMemberString(model.outputs[i], "dataType")
                    );
                }

                if (model.outputs.Count == 3)
                {
                    Debug.Log("FORMAT CHECK: This model has 3 outputs. It may match the Meta sample format: boxes, class IDs, scores.");
                }
                else if (model.outputs.Count == 1)
                {
                    Debug.LogWarning(
                        "FORMAT CHECK: This model has 1 output. " +
                        "If the shape is [1, 300, 6], it matches the YOLOv8 NMS format: x1, y1, x2, y2, score, class_id. " +
                        "Raw Ultralytics output such as [1, 84, N] still needs converter post-processing."
                    );
                }
                else
                {
                    Debug.LogWarning("FORMAT CHECK: Unexpected output count: " + model.outputs.Count);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Failed to load/inspect ModelAsset: " + modelAsset.name);
                Debug.LogError(e.GetType().Name + ": " + e.Message);
                Debug.LogError(e.StackTrace);
            }
        }


        private string GetMemberString(object obj, string memberName)
        {
            if (obj == null)
            {
                return "NULL";
            }

            var type = obj.GetType();

            var property = type.GetProperty(memberName);
            if (property != null)
            {
                var value = property.GetValue(obj);
                return value != null ? value.ToString() : "NULL";
            }

            var field = type.GetField(memberName);
            if (field != null)
            {
                var value = field.GetValue(obj);
                return value != null ? value.ToString() : "NULL";
            }

            return "not found";
        }

        private static bool TryGetModelOutputShape(Model model, int outputIndex, out TensorShape outputShape)
        {
            outputShape = default;

            if (outputIndex < 0 || outputIndex >= model.outputs.Count)
            {
                return false;
            }

            var getShapeMethod = typeof(Model).GetMethod("GetShape", BindingFlags.Instance | BindingFlags.NonPublic);
            var shapeValue = getShapeMethod?.Invoke(model, new object[] { model.outputs[outputIndex].index });
            if (shapeValue is DynamicTensorShape dynamicShape && dynamicShape.IsStatic())
            {
                outputShape = dynamicShape.ToTensorShape();
                return true;
            }

            return false;
        }

        private IEnumerator InitializeRuntimeMode()
        {
            yield return null;

            m_modelComparisonModeController = ResolveModelComparisonModeController();

            if (m_modelComparisonModeController != null && m_modelComparisonModeController.IsDeveloperComparisonMode)
            {
                Debug.Log(
                    "SentisInferenceRunManager detected DeveloperComparisonMode on " +
                    m_modelComparisonModeController.GetHierarchyPath() +
                    ". Default Inspector model load skipped.");
                m_modelComparisonModeController.BeginSelection(this);
                yield break;
            }

            if (m_modelComparisonModeController != null)
            {
                Debug.Log(
                    "SentisInferenceRunManager detected DefaultSampleMode on " +
                    m_modelComparisonModeController.GetHierarchyPath() +
                    ". Loading Inspector-assigned model.");
            }
            else
            {
                Debug.LogWarning("SentisInferenceRunManager found no ModelComparisonModeController. Loading Inspector-assigned model.");
            }

            _ = LoadModelFromCurrentSettings(m_sentisModel != null ? m_sentisModel.name : null);
        }

        private ModelComparisonModeController ResolveModelComparisonModeController()
        {
            var allControllers = FindObjectsByType<ModelComparisonModeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allControllers.Length > 1)
            {
                LogMultipleModelComparisonControllers(allControllers);
            }

            ModelComparisonModeController ignoredAssignedController = null;
            if (m_modelComparisonModeController != null && !IsLiveModelComparisonController(m_modelComparisonModeController))
            {
                ignoredAssignedController = m_modelComparisonModeController;
                m_modelComparisonModeController = null;
            }

            var sameGameObject = GetComponent<ModelComparisonModeController>();
            if (TryResolveLiveModelComparisonController(sameGameObject, "same live GameObject", out var resolvedController))
            {
                LogIgnoredAssignedModelComparisonController(ignoredAssignedController);
                return resolvedController;
            }

            var parent = GetComponentInParent<ModelComparisonModeController>(true);
            if (TryResolveLiveModelComparisonController(parent, "parent hierarchy", out resolvedController))
            {
                LogIgnoredAssignedModelComparisonController(ignoredAssignedController);
                return resolvedController;
            }

            var child = GetComponentInChildren<ModelComparisonModeController>(true);
            if (TryResolveLiveModelComparisonController(child, "child hierarchy", out resolvedController))
            {
                LogIgnoredAssignedModelComparisonController(ignoredAssignedController);
                return resolvedController;
            }

            for (var i = 0; i < allControllers.Length; i++)
            {
                if (TryResolveLiveModelComparisonController(allControllers[i], "scene-wide fallback", out resolvedController))
                {
                    LogIgnoredAssignedModelComparisonController(ignoredAssignedController);
                    return resolvedController;
                }
            }

            LogIgnoredAssignedModelComparisonController(ignoredAssignedController);
            return null;
        }

        private bool TryResolveLiveModelComparisonController(
            ModelComparisonModeController controller,
            string source,
            out ModelComparisonModeController resolvedController)
        {
            resolvedController = null;
            if (!IsLiveModelComparisonController(controller))
            {
                return false;
            }

            m_modelComparisonModeController = controller;
            resolvedController = controller;
            LogResolvedLiveModelComparisonController(source, controller);
            return true;
        }

        private static bool IsLiveModelComparisonController(ModelComparisonModeController controller)
        {
            if (controller == null)
            {
                return false;
            }

            var scene = controller.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private void LogMultipleModelComparisonControllers(ModelComparisonModeController[] controllers)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Multiple ModelComparisonModeController components found. SentisInferenceRunManager will prefer the same live GameObject, then parent, child, then scene-wide fallback.");
            for (var i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                builder
                    .Append(" - ")
                    .Append(controller.gameObject.name)
                    .Append(" | scene: ")
                    .Append(GetSceneNameForLog(controller))
                    .Append(" | scene valid: ")
                    .Append(controller.gameObject.scene.IsValid())
                    .Append(" | scene loaded: ")
                    .Append(controller.gameObject.scene.isLoaded)
                    .Append(" | hierarchy: ")
                    .Append(controller.GetHierarchyPath())
                    .Append(" | mode: ")
                    .Append(controller.Mode)
                    .AppendLine();
            }

            Debug.LogWarning(builder.ToString());
        }

        private void LogResolvedLiveModelComparisonController(string source, ModelComparisonModeController controller)
        {
            Debug.Log(
                "Resolved live ModelComparisonModeController" +
                " | object: " + controller.gameObject.name +
                " | scene: " + controller.gameObject.scene.name +
                " | hierarchy: " + controller.GetHierarchyPath() +
                " | mode: " + controller.Mode +
                " | source: " + source);
        }

        private static void LogIgnoredAssignedModelComparisonController(ModelComparisonModeController controller)
        {
            if (controller == null)
            {
                return;
            }

            Debug.LogWarning(
                "Ignoring assigned ModelComparisonModeController because it is not a live loaded scene object" +
                " | object: " + controller.gameObject.name +
                " | scene: " + GetSceneNameForLog(controller) +
                " | scene valid: " + controller.gameObject.scene.IsValid() +
                " | scene loaded: " + controller.gameObject.scene.isLoaded +
                " | mode: " + controller.Mode);
        }

        private static string GetSceneNameForLog(ModelComparisonModeController controller)
        {
            var scene = controller.gameObject.scene;
            return string.IsNullOrEmpty(scene.name) ? "<empty>" : scene.name;
        }

        public bool ApplyModelProfile(ModelComparisonProfile profile)
        {
            if (profile == null)
            {
                Debug.LogError("Cannot apply model profile: profile is null.");
                return false;
            }

            if (profile.SentisModel == null)
            {
                Debug.LogError("Cannot apply model profile '" + profile.BenchmarkName + "': Sentis model is not assigned.");
                return false;
            }

            m_sentisModel = profile.SentisModel;
            if (profile.LabelsAsset != null)
            {
                m_labelsAsset = profile.LabelsAsset;
            }
            else
            {
                Debug.LogWarning("Model profile '" + profile.BenchmarkName + "' has no labels asset. Keeping the currently assigned labels asset.");
            }

            m_modelOutputMode = profile.OutputMode;
            m_backend = profile.Backend;
            m_iouThreshold = profile.IouThreshold;
            m_scoreThreshold = profile.ScoreThreshold;

            Debug.Log("Selected model profile: " + profile.BenchmarkName);
            Debug.Log("Using output mode: " + m_modelOutputMode);
            return LoadModelFromCurrentSettings(profile.BenchmarkName, true);
        }

        public void StartDetectionFromComparisonMode()
        {
            if (m_engine == null)
            {
                Debug.LogWarning("Cannot start detection from comparison mode before a model worker is ready.");
                return;
            }

            if (m_uiMenuManager == null)
            {
                Debug.LogWarning("Cannot start detection from comparison mode because DetectionUiMenuManager is not assigned.");
                return;
            }

            m_uiMenuManager.StartDetectionFromExternalUi();
        }

        public void SetComparisonSelectionActive(bool active)
        {
            if (m_uiMenuManager == null)
            {
                Debug.LogWarning("Cannot update comparison selection UI state because DetectionUiMenuManager is not assigned.");
                return;
            }

            m_uiMenuManager.SetExternalSelectionActive(active);
        }

        private bool LoadModelFromCurrentSettings(string activeModelName, bool allowDeveloperComparisonLoad = false)
        {
            if (m_modelComparisonModeController != null &&
                m_modelComparisonModeController.IsDeveloperComparisonMode &&
                !allowDeveloperComparisonLoad)
            {
                Debug.LogError("DeveloperComparisonMode safety check blocked default Inspector model load before runtime model selection.");
                return false;
            }

            if (m_sentisModel == null)
            {
                Debug.LogError("m_sentisModel is NULL. Assign a generated .sentis / ModelAsset before running.");
                return false;
            }

            DebugInspectModels();

            Model model;
            try
            {
                model = ModelLoader.Load(m_sentisModel);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Failed to load Sentis model: " + m_sentisModel.name);
                Debug.LogException(e);
                return false;
            }

            if (model.inputs.Count == 0)
            {
                Debug.LogError("Loaded Sentis model has no inputs.");
                return false;
            }

            if (!TryResolveOutputMode(model, out var resolvedOutputMode))
            {
                return false;
            }

            var inputShape = model.inputs[0].shape;
            m_inputSize = new Vector2Int(inputShape.Get(2), inputShape.Get(3));

            Debug.Log("Runtime input size: " + m_inputSize);
            Debug.Log("Creating worker...");

            Worker newEngine;
            try
            {
                newEngine = new Worker(model, m_backend);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Failed to create Sentis worker for model: " + m_sentisModel.name);
                Debug.LogException(e);
                return false;
            }

            DisposeWorker();

            m_engine = newEngine;
            m_outputMode = resolvedOutputMode;
            m_loadedOutputCount = model.outputs.Count;
            m_activeModelName = string.IsNullOrWhiteSpace(activeModelName) ? m_sentisModel.name : activeModelName;

            ApplyLabelsToUi();
            InitializeBenchmarkLogger();

            Debug.Log(
                "Worker created successfully. Selected model: " + m_activeModelName +
                " | Sentis asset: " + m_sentisModel.name +
                " | backend: " + m_backend +
                " | output mode: " + m_outputMode);

            return true;
        }

        private IEnumerator Start()
        {
            yield return InitializeRuntimeMode();

            while (m_engine == null || IsWaitingForComparisonSelection())
            {
                yield return null;
            }

            while (true)
            {
                while (m_uiMenuManager.IsPaused)
                {
                    yield return null;
                }
                yield return RunInference();
            }
        }

        private void OnDestroy()
        {
            DisposeWorker();
        }

        private bool IsWaitingForComparisonSelection()
        {
            return m_modelComparisonModeController != null &&
                   m_modelComparisonModeController.IsDeveloperComparisonMode &&
                   !m_modelComparisonModeController.HasSelectedProfile;
        }

        private bool TryResolveOutputMode(Model model, out ModelOutputMode resolvedOutputMode)
        {
            resolvedOutputMode = ModelOutputMode.MetaSampleThreeOutputs;

            if (m_modelOutputMode == ModelComparisonOutputMode.Auto)
            {
                if (model.outputs.Count == 3)
                {
                    resolvedOutputMode = ModelOutputMode.MetaSampleThreeOutputs;
                }
                else if (model.outputs.Count == 1)
                {
                    resolvedOutputMode = ModelOutputMode.YoloV8NmsSingleOutput;
                }
            }
            else if (m_modelOutputMode == ModelComparisonOutputMode.MetaSampleRawOutput)
            {
                resolvedOutputMode = ModelOutputMode.MetaSampleThreeOutputs;
            }
            else if (m_modelOutputMode == ModelComparisonOutputMode.YoloNmsSingleOutput)
            {
                resolvedOutputMode = ModelOutputMode.YoloV8NmsSingleOutput;
            }

            if (resolvedOutputMode == ModelOutputMode.MetaSampleThreeOutputs)
            {
                if (model.outputs.Count != 3)
                {
                    Debug.LogError("Model output mode is MetaSampleRawOutput, but loaded model output count is " + model.outputs.Count + ".");
                    return false;
                }

                Debug.Log("Runtime output mode: Meta sample 3-output format: boxes, class IDs, scores.");
                return true;
            }

            if (resolvedOutputMode == ModelOutputMode.YoloV8NmsSingleOutput)
            {
                if (model.outputs.Count != 1)
                {
                    Debug.LogError("Model output mode is YoloNmsSingleOutput, but loaded model output count is " + model.outputs.Count + ".");
                    return false;
                }

                if (TryGetModelOutputShape(model, 0, out var outputShape) && !IsYoloV8NmsShape(outputShape))
                {
                    Debug.LogError("Loaded single-output Sentis model shape is " + outputShape + ". Expected YOLOv8 NMS shape [1, 300, 6].");
                    return false;
                }

                Debug.Log("Runtime output mode: YOLOv8 NMS single-output format. Expected tensor shape at runtime: [1, 300, 6].");
                return true;
            }

            Debug.LogError(
                "Loaded Sentis model output count is " + model.outputs.Count +
                ". Supported formats are exactly 3 outputs for the Meta sample, or 1 YOLOv8 NMS output with shape [1, 300, 6]."
            );
            return false;
        }

        private void ApplyLabelsToUi()
        {
            if (m_uiInference == null)
            {
                return;
            }

            if (m_labelsAsset == null)
            {
                Debug.LogError("Labels asset is NULL. Detection UI labels cannot be updated.");
                return;
            }

            m_uiInference.SetLabels(m_labelsAsset);
        }

        private void InitializeBenchmarkLogger()
        {
            if (!m_enableBenchmarkLogging)
            {
                return;
            }

            if (m_benchmarkLogger == null)
            {
                m_benchmarkLogger = GetComponent<DetectionBenchmarkLogger>();
                if (m_benchmarkLogger == null)
                {
                    m_benchmarkLogger = gameObject.AddComponent<DetectionBenchmarkLogger>();
                }
            }

            m_benchmarkLogger?.Initialize(m_activeModelName, m_backend.ToString(), m_outputMode.ToString());
        }

        private void DisposeWorker()
        {
            if (m_engine == null)
            {
                return;
            }

            for (var i = 0; i < m_loadedOutputCount; i++)
            {
                m_engine.PeekOutput(i)?.CompleteAllPendingOperations();
            }

            m_engine.Dispose();
            m_engine = null;
            m_loadedOutputCount = 0;
        }

        internal static void PreloadModel(ModelAsset modelAsset)
        {
            // Load model
            var model = ModelLoader.Load(modelAsset);
            var inputShape = model.inputs[0].shape;

            // Create engine to run model
            using var worker = new Worker(model, BackendType.CPU);

            // Run inference with an empty image to load the model in the memory. The first inference blocks the main thread for a long time, so we're doing it on the app launch
            Texture tempTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            var textureTransform = new TextureTransform().SetDimensions(tempTexture.width, tempTexture.height, 3);
            using var input = new Tensor<float>(new TensorShape(1, 3, inputShape.Get(2), inputShape.Get(3)));
            TextureConverter.ToTensor(tempTexture, input, textureTransform);
            worker.Schedule(input);

            // Complete the inference immediately and destroy the temporary texture
            for (var i = 0; i < model.outputs.Count; i++)
            {
                worker.PeekOutput(i).CompleteAllPendingOperations();
            }
            Destroy(tempTexture);
        }

        private IEnumerator RunInference()
        {
            if (m_engine == null)
            {
                yield break;
            }

            if (!m_cameraAccess.IsPlaying)
            {
                yield break;
            }

            [DllImport("OVRPlugin", CallingConvention = CallingConvention.Cdecl)]
            static extern OVRPlugin.Result ovrp_GetNodePoseStateAtTime(double time, OVRPlugin.Node nodeId, out OVRPlugin.PoseStatef nodePoseState);
            if (!ovrp_GetNodePoseStateAtTime(OVRPlugin.GetTimeInSeconds(), OVRPlugin.Node.Head, out _).IsSuccess())
            {
                Debug.Log("ovrp_GetNodePoseStateAtTime failed, which means 'm_cameraAccess.GetCameraPose()' is not reliable, skipping.");
                yield break;
            }

            var cachedCameraPose = m_cameraAccess.GetCameraPose();

            // Update Capture data
            Texture targetTexture = m_cameraAccess.GetTexture();

            // Convert the texture to a Tensor and schedule the inference
            var textureTransform = new TextureTransform().SetDimensions(targetTexture.width, targetTexture.height, 3);
            using var input = new Tensor<float>(new TensorShape(1, 3, m_inputSize.x, m_inputSize.y));
            TextureConverter.ToTensor(targetTexture, input, textureTransform);

            // Schedule all model layers
            var inferenceStartTime = Time.realtimeSinceStartupAsDouble;
            m_engine.Schedule(input);

            // Get the results. ReadbackAndCloneAsync waits for all layers to complete before returning the result.
            if (m_outputMode == ModelOutputMode.YoloV8NmsSingleOutput)
            {
                var outputTensor = m_engine.PeekOutput(0) as Tensor<float>;
                if (outputTensor == null)
                {
                    Debug.LogError("YOLOv8 NMS output tensor is not a float tensor.");
                    yield break;
                }

                var detectionsAwaiter = outputTensor.ReadbackAndCloneAsync().GetAwaiter();
                while (!detectionsAwaiter.IsCompleted)
                {
                    yield return null;
                }
                using var detections = detectionsAwaiter.GetResult();
                if (!ParseYoloV8NmsOutput(m_detections, detections, m_scoreThreshold))
                {
                    yield break;
                }
            }
            else
            {
                var boxesAwaiter = (m_engine.PeekOutput(0) as Tensor<float>).ReadbackAndCloneAsync().GetAwaiter();
                while (!boxesAwaiter.IsCompleted)
                {
                    yield return null;
                }
                using var boxes = boxesAwaiter.GetResult();
                if (boxes.shape[0] == 0)
                {
                    RecordBenchmarkFrame(inferenceStartTime, 0);
                    yield break;
                }

                var classIDsAwaiter = (m_engine.PeekOutput(1) as Tensor<int>).ReadbackAndCloneAsync().GetAwaiter();
                while (!classIDsAwaiter.IsCompleted)
                {
                    yield return null;
                }
                using var classIDs = classIDsAwaiter.GetResult();
                if (classIDs.shape[0] == 0)
                {
                    Debug.LogError("classIDs.shape[0] == 0");
                    RecordBenchmarkFrame(inferenceStartTime, 0);
                    yield break;
                }

                var scoresAwaiter = (m_engine.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync().GetAwaiter();
                while (!scoresAwaiter.IsCompleted)
                {
                    yield return null;
                }
                using var scores = scoresAwaiter.GetResult();
                if (scores.shape[0] == 0)
                {
                    Debug.LogError("scores.shape[0] == 0");
                    RecordBenchmarkFrame(inferenceStartTime, 0);
                    yield break;
                }

                NonMaxSuppression(m_detections, boxes, classIDs, scores, m_iouThreshold, m_scoreThreshold);
            }

            RecordBenchmarkFrame(inferenceStartTime, m_detections.Count);

            // Checking if spatial anchor is tracked ensures bounding boxes are placed at correct world space positIons.
            if (!m_cameraAccess.IsPlaying || m_detectionManager.m_spatialAnchor == null || !m_detectionManager.m_spatialAnchor.IsTracked)
            {
                yield break;
            }

            // Update UI.
            m_uiInference.DrawUIBoxes(m_detections, m_inputSize, cachedCameraPose);
        }

        private void RecordBenchmarkFrame(double inferenceStartTime, int detectionCount)
        {
            if (m_benchmarkLogger == null)
            {
                return;
            }

            var inferenceLatencyMs = (Time.realtimeSinceStartupAsDouble - inferenceStartTime) * 1000d;
            m_benchmarkLogger.RecordFrame(inferenceLatencyMs, detectionCount);
        }

        private static bool ParseYoloV8NmsOutput(List<(int classId, Vector4 boundingBox)> outDetections, Tensor<float> detections, float scoreThreshold)
        {
            outDetections.Clear();

            if (!IsYoloV8NmsShape(detections.shape))
            {
                Debug.LogError("YOLOv8 NMS output shape is " + detections.shape + ". Expected [1, 300, 6].");
                return false;
            }

            const int valuesPerDetection = 6;
            var detectionRows = detections.shape[1];
            NativeArray<float>.ReadOnly detectionsArray = detections.AsReadOnlyNativeArray();

            for (var i = 0; i < detectionRows; i++)
            {
                var offset = i * valuesPerDetection;
                var score = detectionsArray[offset + 4];
                if (score < scoreThreshold)
                {
                    continue;
                }

                var classId = Mathf.RoundToInt(detectionsArray[offset + 5]);
                var boundingBox = new Vector4(
                    detectionsArray[offset],
                    detectionsArray[offset + 1],
                    detectionsArray[offset + 2],
                    detectionsArray[offset + 3]);

                outDetections.Add((classId, boundingBox));
            }

            return true;
        }

        private static bool IsYoloV8NmsShape(TensorShape shape)
        {
            return shape.rank == 3 && shape[0] == 1 && shape[2] == 6;
        }

        private static void NonMaxSuppression(List<(int classId, Vector4 boundingBox)> outDetections, Tensor<float> boxes, Tensor<int> classIDs, Tensor<float> scores, float iouThreshold, float scoreThreshold)
        {
            outDetections.Clear();

            // Filter by score threshold first
            List<int> filteredIndices = new List<int>();
            NativeArray<float>.ReadOnly scoresArray = scores.AsReadOnlyNativeArray();
            for (int i = 0; i < scoresArray.Length; i++)
            {
                if (scoresArray[i] >= scoreThreshold)
                {
                    filteredIndices.Add(i);
                }
            }

            if (filteredIndices.Count == 0)
            {
                return;
            }

            // Sort filtered indices by scores in descending order
            filteredIndices.Sort((a, b) => scoresArray[b].CompareTo(scoresArray[a]));

            // Apply NMS algorithm
            bool[] suppressed = new bool[filteredIndices.Count];
            for (int i = 0; i < filteredIndices.Count; i++)
            {
                if (suppressed[i])
                    continue;

                int idx = filteredIndices[i];

                // Add this detection to results
                outDetections.Add((classIDs[idx], GetBox(idx)));

                // Suppress overlapping boxes regardless of class
                for (int j = i + 1; j < filteredIndices.Count; j++)
                {
                    if (suppressed[j])
                        continue;

                    int jdx = filteredIndices[j];

                    float iou = CalculateIoU(GetBox(idx), GetBox(jdx));
                    if (iou > iouThreshold)
                    {
                        suppressed[j] = true;
                    }
                }
            }

            Vector4 GetBox(int i) => new Vector4(boxes[i, 0], boxes[i, 1], boxes[i, 2], boxes[i, 3]);
        }

        internal static float CalculateIoU(Vector4 boxA, Vector4 boxB)
        {
            // Boxes are in format (topLeftX, topLeftY, bottomRightX, bottomRightY)
            // Calculate intersection coordinates
            float x1 = Mathf.Max(boxA.x, boxB.x);
            float y1 = Mathf.Max(boxA.y, boxB.y);
            float x2 = Mathf.Min(boxA.z, boxB.z);
            float y2 = Mathf.Min(boxA.w, boxB.w);

            // Calculate intersection area
            float intersectionWidth = Mathf.Max(0, x2 - x1);
            float intersectionHeight = Mathf.Max(0, y2 - y1);
            float intersectionArea = intersectionWidth * intersectionHeight;

            // Calculate individual box areas
            float boxAArea = (boxA.z - boxA.x) * (boxA.w - boxA.y);
            float boxBArea = (boxB.z - boxB.x) * (boxB.w - boxB.y);

            // Calculate union area
            float unionArea = boxAArea + boxBArea - intersectionArea;

            // Return IoU (Intersection over Union)
            if (unionArea == 0)
                return 0;

            return intersectionArea / unionArea;
        }
    }
}
