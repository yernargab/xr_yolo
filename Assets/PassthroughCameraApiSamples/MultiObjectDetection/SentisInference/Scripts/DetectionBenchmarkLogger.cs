// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Meta.XR.Samples;
using UnityEngine;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    [DisallowMultipleComponent]
    public class DetectionBenchmarkLogger : MonoBehaviour
    {
        [Header("Benchmark Logging")]
        [SerializeField] private bool m_enableLogging = true;
        [SerializeField] private bool m_exportCsv = true;
        [SerializeField] private bool m_logEveryFrame;
        [SerializeField, Min(1)] private int m_summaryEveryFrames = 30;
        [SerializeField, Min(1)] private int m_flushCsvEveryFrames = 30;
        [SerializeField] private string m_csvFilePrefix = "detection_benchmark";

        private readonly List<double> m_sortedLatenciesMs = new();
        private readonly StringBuilder m_csvBuffer = new();

        private string m_modelName = "Uninitialized";
        private string m_backend = "Unknown";
        private string m_outputMode = "Unknown";
        private string m_csvPath;
        private double m_totalLatencyMs;
        private int m_totalDetections;
        private int m_frameIndex;
        private double m_startTime;
        private bool m_csvReady;

        public string CsvPath => m_csvPath;

        public void Initialize(string modelName, string backend, string outputMode)
        {
            m_modelName = string.IsNullOrWhiteSpace(modelName) ? "UnknownModel" : modelName;
            m_backend = string.IsNullOrWhiteSpace(backend) ? "UnknownBackend" : backend;
            m_outputMode = string.IsNullOrWhiteSpace(outputMode) ? "UnknownOutputMode" : outputMode;

            m_sortedLatenciesMs.Clear();
            m_csvBuffer.Clear();
            m_totalLatencyMs = 0d;
            m_totalDetections = 0;
            m_frameIndex = 0;
            m_startTime = Time.realtimeSinceStartupAsDouble;
            m_csvReady = false;
            m_csvPath = null;

            Debug.Log(
                "DetectionBenchmarkLogger initialized | model: " + m_modelName +
                " | backend: " + m_backend +
                " | output mode: " + m_outputMode);

            if (!m_enableLogging)
            {
                Debug.Log("DetectionBenchmarkLogger is disabled.");
                return;
            }

            if (!m_exportCsv)
            {
                Debug.Log("DetectionBenchmarkLogger CSV export is disabled.");
                return;
            }

            CreateCsvFile();
        }

        public void RecordFrame(double inferenceLatencyMs, int detectionCount)
        {
            if (!m_enableLogging)
            {
                return;
            }

            m_frameIndex++;
            m_totalLatencyMs += inferenceLatencyMs;
            m_totalDetections += detectionCount;
            InsertSortedLatency(inferenceLatencyMs);

            var averageLatencyMs = m_totalLatencyMs / m_frameIndex;
            var medianLatencyMs = GetMedianLatencyMs();
            var elapsedSeconds = Math.Max(0.0001d, Time.realtimeSinceStartupAsDouble - m_startTime);
            var fpsEstimate = m_frameIndex / elapsedSeconds;
            var averageDetections = (double)m_totalDetections / m_frameIndex;
            var timestampUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            if (m_csvReady)
            {
                AppendCsvRow(
                    timestampUtc,
                    inferenceLatencyMs,
                    detectionCount,
                    averageLatencyMs,
                    medianLatencyMs,
                    fpsEstimate,
                    averageDetections);

                if (m_frameIndex % Mathf.Max(1, m_flushCsvEveryFrames) == 0)
                {
                    FlushCsv();
                }
            }

            if (m_logEveryFrame || m_frameIndex % Mathf.Max(1, m_summaryEveryFrames) == 0)
            {
                Debug.Log(
                    "Detection benchmark | model: " + m_modelName +
                    " | frame: " + m_frameIndex +
                    " | latency ms: " + FormatNumber(inferenceLatencyMs) +
                    " | avg ms: " + FormatNumber(averageLatencyMs) +
                    " | median ms: " + FormatNumber(medianLatencyMs) +
                    " | fps estimate: " + FormatNumber(fpsEstimate) +
                    " | detections: " + detectionCount +
                    " | avg detections: " + FormatNumber(averageDetections));
            }
        }

        private void OnDestroy()
        {
            if (m_csvReady)
            {
                FlushCsv();
                Debug.Log("DetectionBenchmarkLogger CSV saved to: " + m_csvPath);
            }
        }

        private void CreateCsvFile()
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                var fileName = SafeFileName(m_csvFilePrefix + "_" + m_modelName + "_" + timestamp) + ".csv";
                m_csvPath = Path.Combine(Application.persistentDataPath, fileName);

                File.WriteAllText(
                    m_csvPath,
                    "timestamp_utc,model_name,backend,output_mode,frame_index,inference_latency_ms,detection_count,average_latency_ms,median_latency_ms,fps_estimate,average_detections_per_frame" +
                    Environment.NewLine);

                m_csvReady = true;
                Debug.Log("DetectionBenchmarkLogger CSV will be saved to: " + m_csvPath);
            }
            catch (Exception e)
            {
                m_csvReady = false;
                Debug.LogError("DetectionBenchmarkLogger failed to create CSV in Application.persistentDataPath: " + Application.persistentDataPath);
                Debug.LogException(e);
            }
        }

        private void AppendCsvRow(
            string timestampUtc,
            double inferenceLatencyMs,
            int detectionCount,
            double averageLatencyMs,
            double medianLatencyMs,
            double fpsEstimate,
            double averageDetections)
        {
            m_csvBuffer
                .Append(CsvEscape(timestampUtc)).Append(',')
                .Append(CsvEscape(m_modelName)).Append(',')
                .Append(CsvEscape(m_backend)).Append(',')
                .Append(CsvEscape(m_outputMode)).Append(',')
                .Append(m_frameIndex).Append(',')
                .Append(FormatNumber(inferenceLatencyMs)).Append(',')
                .Append(detectionCount).Append(',')
                .Append(FormatNumber(averageLatencyMs)).Append(',')
                .Append(FormatNumber(medianLatencyMs)).Append(',')
                .Append(FormatNumber(fpsEstimate)).Append(',')
                .Append(FormatNumber(averageDetections)).AppendLine();
        }

        private void FlushCsv()
        {
            if (!m_csvReady || m_csvBuffer.Length == 0)
            {
                return;
            }

            try
            {
                File.AppendAllText(m_csvPath, m_csvBuffer.ToString());
                m_csvBuffer.Clear();
            }
            catch (Exception e)
            {
                m_csvReady = false;
                Debug.LogError("DetectionBenchmarkLogger failed to write CSV: " + m_csvPath);
                Debug.LogException(e);
            }
        }

        private void InsertSortedLatency(double latencyMs)
        {
            var index = m_sortedLatenciesMs.BinarySearch(latencyMs);
            if (index < 0)
            {
                index = ~index;
            }

            m_sortedLatenciesMs.Insert(index, latencyMs);
        }

        private double GetMedianLatencyMs()
        {
            if (m_sortedLatenciesMs.Count == 0)
            {
                return 0d;
            }

            var middle = m_sortedLatenciesMs.Count / 2;
            if (m_sortedLatenciesMs.Count % 2 == 1)
            {
                return m_sortedLatenciesMs[middle];
            }

            return (m_sortedLatenciesMs[middle - 1] + m_sortedLatenciesMs[middle]) * 0.5d;
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("F3", CultureInfo.InvariantCulture);
        }

        private static string CsvEscape(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        private static string SafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "detection_benchmark";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                builder.Append(Array.IndexOf(invalidChars, c) >= 0 || char.IsWhiteSpace(c) ? '_' : c);
            }

            return builder.ToString();
        }
    }
}
