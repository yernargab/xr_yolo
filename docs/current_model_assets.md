# Current Model Assets

This file tracks the model and label assets used for the Unity/Quest comparison.

Project scene:

`Assets/PassthroughCameraApiSamples/MultiObjectDetection/MultiObjectDetection.unity`

Model folder:

`Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model`

Exports folder:

`Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/exports`

## Official Baseline

| Asset | Path | Status | Notes |
| --- | --- | --- | --- |
| `yolov9onnx.onnx` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/yolov9onnx.onnx` | Present | Official ONNX source |
| `yolov9sentis.sentis` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/yolov9sentis.sentis` | Present | Official baseline Sentis asset |
| `SentisYoloClasses.txt` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/SentisYoloClasses.txt` | Present | Official labels |

## YOLOv8n ONNX Exports

| Asset | Path | Status | Expected output |
| --- | --- | --- | --- |
| `yolov8n_coco_pretrained_416_nms_fp32.onnx` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/exports/yolov8n_coco_pretrained_416_nms_fp32.onnx` | Present | `[1, 300, 6]` |
| `yolov8n_coco_pretrained_416_nms_fp16.onnx` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/exports/yolov8n_coco_pretrained_416_nms_fp16.onnx` | Present | `[1, 300, 6]` |
| `yolov8n_voc_416_50epochs_nms_fp32.onnx` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/exports/yolov8n_voc_416_50epochs_nms_fp32.onnx` | Present | `[1, 300, 6]` |
| `yolov8n_voc_416_50epochs_nms_fp16.onnx` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/exports/yolov8n_voc_416_50epochs_nms_fp16.onnx` | Present | `[1, 300, 6]` |

All listed YOLOv8n NMS exports use input shape `[1, 3, 416, 416]`.

## Generated Sentis Assets

| Source ONNX | Expected generated Sentis asset | Status |
| --- | --- | --- |
| `yolov8n_coco_pretrained_416_nms_fp32.onnx` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/exports/yolov8n_coco_pretrained_416_nms_fp32_sentis.sentis` | Present |
| `yolov8n_coco_pretrained_416_nms_fp16.onnx` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/exports/yolov8n_coco_pretrained_416_nms_fp16_sentis.sentis` | To generate |
| `yolov8n_voc_416_50epochs_nms_fp32.onnx` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/exports/yolov8n_voc_416_50epochs_nms_fp32_sentis.sentis` | Optional, not in main comparison |
| `yolov8n_voc_416_50epochs_nms_fp16.onnx` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/exports/yolov8n_voc_416_50epochs_nms_fp16_sentis.sentis` | To generate |

## Label Assets

| Dataset | Label asset | Path | Status |
| --- | --- | --- | --- |
| COCO | `coco_80_classes.txt` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/exports/labels/coco_80_classes.txt` | Present |
| VOC | `voc_20_classes.txt` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/exports/labels/voc_20_classes.txt` | Present |
| Official sample | `SentisYoloClasses.txt` | `Assets/PassthroughCameraApiSamples/MultiObjectDetection/SentisInference/Model/SentisYoloClasses.txt` | Present |

## Comparison Assignments

| Comparison ID | Sentis model | Labels | Output mode |
| --- | --- | --- | --- |
| M0 | `yolov9sentis.sentis` | `SentisYoloClasses.txt` | Official 3-output format |
| M1 | `yolov8n_coco_pretrained_416_nms_fp32_sentis.sentis` | `coco_80_classes.txt` | YOLOv8 NMS single-output `[1, 300, 6]` |
| M2 | `yolov8n_coco_pretrained_416_nms_fp16_sentis.sentis` | `coco_80_classes.txt` | YOLOv8 NMS single-output `[1, 300, 6]` |
| M3 | `yolov8n_voc_416_50epochs_nms_fp16_sentis.sentis` | `voc_20_classes.txt` | YOLOv8 NMS single-output `[1, 300, 6]` |

## Notes

- Do not overwrite the official `yolov9sentis.sentis` asset.
- Generate separate Sentis assets for each ONNX model before testing.
- Keep model, label file, score threshold, IoU threshold, and backend recorded for every run.
- The VOC FP32 model is available as an ONNX export, but the main comparison list uses the VOC FP16 model.
