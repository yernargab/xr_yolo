// Copyright (c) Meta Platforms, Inc. and affiliates.
using System;
using System.IO;
using System.Reflection;
using Meta.XR.Samples;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;

namespace PassthroughCameraSamples.MultiObjectDetection.Editor
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    [CustomEditor(typeof(SentisInferenceRunManager))]
    public class SentisModelEditorConverter : UnityEditor.Editor
    {
        private const string OfficialSentisPath = "Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/yolov9sentis.sentis";
        private SentisInferenceRunManager m_targetClass;
        private float m_iouThreshold;
        private float m_scoreThreshold;

        public void OnEnable()
        {
            m_targetClass = (SentisInferenceRunManager)target;
            m_iouThreshold = serializedObject.FindProperty("m_iouThreshold").floatValue;
            m_scoreThreshold = serializedObject.FindProperty("m_scoreThreshold").floatValue;
        }

        public override void OnInspectorGUI()
        {
            _ = DrawDefaultInspector();

            if (GUILayout.Button("Generate Yolov9 Sentis model with Non-Max-Supression layer"))
            {
                OnEnable(); // Get the latest values from the serialized object
                ConvertModel(); // convert the ONNX model to sentis
            }
        }

        private void ConvertModel()
        {
            if (m_targetClass.OnnxModel == null)
            {
                Debug.LogError("Sentis model generation failed: no ONNX model is selected.");
                return;
            }

            var onnxPath = (AssetDatabase.GetAssetPath(m_targetClass.OnnxModel) ?? string.Empty).Replace("\\", "/");
            if (string.IsNullOrEmpty(onnxPath))
            {
                Debug.LogError("Sentis model generation failed: selected ONNX model has no asset path.");
                return;
            }

            var outputPath = GetGeneratedSentisPath(onnxPath);
            var fileAlreadyExists = File.Exists(outputPath);

            Debug.Log("Selected ONNX model name: " + m_targetClass.OnnxModel.name);
            Debug.Log("Selected ONNX model path: " + onnxPath);
            Debug.Log("Generated Sentis model path: " + outputPath);
            Debug.Log("Generated Sentis model already exists: " + fileAlreadyExists);

            if (!string.Equals(Path.GetExtension(onnxPath), ".onnx", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("Selected model does not have an .onnx extension. Continuing with conversion anyway: " + onnxPath);
            }

            if (string.Equals(outputPath, OfficialSentisPath, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("Sentis model generation stopped to avoid overwriting the official sample model: " + OfficialSentisPath);
                return;
            }

            if (fileAlreadyExists)
            {
                Debug.LogWarning("Generated Sentis model already exists and will be overwritten: " + outputPath);
            }

            try
            {
                var outputDirectory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                //Load model
                var model = ModelLoader.Load(m_targetClass.OnnxModel);
                Debug.Log("Loaded ONNX output count: " + model.outputs.Count);
                if (TryGetOutputShape(model, 0, out var outputShape))
                {
                    Debug.Log("Loaded ONNX first output shape: " + outputShape);
                }

                if (IsYoloV8NmsOutput(model, out outputShape))
                {
                    Debug.Log("Detected YOLOv8 NMS ONNX output [1, 300, 6]. Saving single-output Sentis model without adding the sample NMS graph.");
                    SaveGeneratedModel(outputPath, ref model);
                    return;
                }

                //Here we transform the output of the model by feeding it through a Non-Max-Suppression layer.
                var graph = new FunctionalGraph();
                var input = graph.AddInput(model, 0);

                var centersToCornersData = new[]
                {
                            1,      0,      1,      0,
                            0,      1,      0,      1,
                            -0.5f,  0,      0.5f,   0,
                            0,      -0.5f,  0,      0.5f
                };
                var centersToCorners = Functional.Constant(new TensorShape(4, 4), centersToCornersData);
                var modelOutput = Functional.Forward(model, input)[0];  //shape(1,N,85)
                // Following for yolo model. in (1, 84, N) out put shape
                var boxCoords = modelOutput[0, ..4, ..].Transpose(0, 1);
                var allScores = modelOutput[0, 4.., ..].Transpose(0, 1);
                var scores = Functional.ReduceMax(allScores, 1);                                    //shape=(N)
                var classIDs = Functional.ArgMax(allScores, 1);                                     //shape=(N)
                var corners = Functional.MatMul(boxCoords, centersToCorners);                    //shape=(N,4)
                var modelFinal = graph.Compile(corners, classIDs, scores);

                //Export the model to Sentis format
                SaveGeneratedModel(outputPath, ref modelFinal);
            }
            catch (Exception e)
            {
                Debug.LogError("Sentis model generation failed for " + m_targetClass.OnnxModel.name + " -> " + outputPath);
                Debug.LogException(e);
            }
        }

        private static string GetGeneratedSentisPath(string onnxPath)
        {
            var outputDirectory = Path.GetDirectoryName(onnxPath);
            var outputFileName = Path.GetFileNameWithoutExtension(onnxPath) + "_sentis.sentis";
            return Path.Combine(outputDirectory, outputFileName).Replace("\\", "/");
        }

        private static bool IsYoloV8NmsOutput(Model model, out TensorShape outputShape)
        {
            if (model.outputs.Count == 1 && TryGetOutputShape(model, 0, out outputShape))
            {
                return outputShape.rank == 3 && outputShape[0] == 1 && outputShape[1] == 300 && outputShape[2] == 6;
            }

            outputShape = default;
            return false;
        }

        private static bool TryGetOutputShape(Model model, int outputIndex, out TensorShape outputShape)
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

        private static void SaveGeneratedModel(string outputPath, ref Model model)
        {
            ModelQuantizer.QuantizeWeights(QuantizationType.Uint8, ref model);
            ModelWriter.Save(outputPath, model);

            // refresh assets
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Sentis model generation succeeded: " + outputPath);
        }
    }
}
