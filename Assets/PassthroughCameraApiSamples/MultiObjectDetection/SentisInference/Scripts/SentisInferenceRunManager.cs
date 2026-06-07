// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
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
        [SerializeField, Range(0, 1)] private float m_iouThreshold = 0.6f;
        [SerializeField, Range(0, 1)] private float m_scoreThreshold = 0.23f;

        [Header("UI display references")]
        [SerializeField] private SentisInferenceUiManager m_uiInference;

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

        private void Awake()
        {
            if (m_sentisModel == null)
            {
                Debug.LogError("m_sentisModel is NULL. Assign a generated .sentis / ModelAsset before running.");
                return;
            }

            DebugInspectModels();

            var model = ModelLoader.Load(m_sentisModel);

            if (model.inputs.Count == 0)
            {
                Debug.LogError("Loaded Sentis model has no inputs.");
                return;
            }

            if (model.outputs.Count == 3)
            {
                m_outputMode = ModelOutputMode.MetaSampleThreeOutputs;
                Debug.Log("Runtime output mode: Meta sample 3-output format: boxes, class IDs, scores.");
            }
            else if (model.outputs.Count == 1)
            {
                if (TryGetModelOutputShape(model, 0, out var outputShape) && !IsYoloV8NmsShape(outputShape))
                {
                    Debug.LogError("Loaded single-output Sentis model shape is " + outputShape + ". Expected YOLOv8 NMS shape [1, 300, 6].");
                    return;
                }

                m_outputMode = ModelOutputMode.YoloV8NmsSingleOutput;
                Debug.Log("Runtime output mode: YOLOv8 NMS single-output format. Expected tensor shape at runtime: [1, 300, 6].");
            }
            else
            {
                Debug.LogError(
                    "Loaded Sentis model output count is " + model.outputs.Count +
                    ". Supported formats are exactly 3 outputs for the Meta sample, or 1 YOLOv8 NMS output with shape [1, 300, 6]."
                );
                return;
            }

            var inputShape = model.inputs[0].shape;
            m_inputSize = new Vector2Int(inputShape.Get(2), inputShape.Get(3));

            Debug.Log("Runtime input size: " + m_inputSize);
            Debug.Log("Creating worker...");

            m_engine = new Worker(model, m_backend);

            Debug.Log("Worker created successfully.");
        }

        private IEnumerator Start()
        {
            m_uiInference.SetLabels(m_labelsAsset);

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
            if (m_engine == null)
            {
                return;
            }

            m_engine.PeekOutput(0)?.CompleteAllPendingOperations();
            if (m_outputMode == ModelOutputMode.MetaSampleThreeOutputs)
            {
                m_engine.PeekOutput(1)?.CompleteAllPendingOperations();
                m_engine.PeekOutput(2)?.CompleteAllPendingOperations();
            }
            m_engine.Dispose();
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
                    yield break;
                }

                NonMaxSuppression(m_detections, boxes, classIDs, scores, m_iouThreshold, m_scoreThreshold);
            }

            // Checking if spatial anchor is tracked ensures bounding boxes are placed at correct world space positIons.
            if (!m_cameraAccess.IsPlaying || m_detectionManager.m_spatialAnchor == null || !m_detectionManager.m_spatialAnchor.IsTracked)
            {
                yield break;
            }

            // Update UI.
            m_uiInference.DrawUIBoxes(m_detections, m_inputSize, cachedCameraPose);
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
