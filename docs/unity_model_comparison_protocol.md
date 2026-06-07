# Unity/Quest Model Comparison Protocol

## Goal

Compare the official Meta `yolo9sentis` baseline against the optimized YOLOv8n Sentis models in the same Meta `Unity-PassthroughCameraApiSamples` `MultiObjectDetection` scene.

The comparison should answer:

- Which model runs fastest on Quest?
- Which model gives the most stable detections in passthrough camera use?
- Which model has acceptable accuracy for the internship XR object detection goal?
- Which model is the best practical choice for the final demo?

## Models to Compare

| ID | Model | Dataset | Format | Notes |
| --- | --- | --- | --- | --- |
| M0 | `yolo9sentis` | Official sample classes | Sentis | Baseline Meta sample model |
| M1 | `yolov8n_coco_pretrained_416_nms_fp32_sentis` | COCO | YOLOv8 NMS FP32 | Main COCO FP32 candidate |
| M2 | `yolov8n_coco_pretrained_416_nms_fp16_sentis` | COCO | YOLOv8 NMS FP16 | Main speed candidate |
| M3 | `yolov8n_voc_416_50epochs_nms_fp16_sentis` | VOC | YOLOv8 NMS FP16 | Smaller class set candidate |

## Controlled Offline Test

Use this test to compare models under repeatable conditions before testing live Quest passthrough.

1. Use the same Unity scene: `Assets/PassthroughCameraApiSamples/MultiObjectDetection/MultiObjectDetection.unity`.
2. Use the same camera input source or same saved test images/video frames if an offline path is available.
3. Test the same object set for every model.
4. Keep score threshold, IoU threshold, backend, labels file, lighting, and scene settings fixed for a full comparison run.
5. Record console logs, output format, input shape, runtime errors, and detection counts.

Recommended controlled object set:

- Person
- Chair
- Cup or bottle
- Laptop or keyboard
- Phone
- Backpack or bag
- Book

For the VOC model, only evaluate classes that exist in the VOC label file.

## Live Quest Test

Use this test to measure real XR behavior in passthrough.

1. Build and deploy the same Unity project to the same Quest device.
2. Use the same room and lighting for all models.
3. Keep the headset position and test path as consistent as possible.
4. Run each model for the same duration.
5. Repeat each model test at least 3 times.
6. Restart the scene or app between models if needed to avoid warm-state bias.
7. Record visible detection stability, missed objects, false positives, and perceived responsiveness.

Suggested run duration:

- Warmup: 15 seconds
- Measurement: 60 seconds
- Repeat count: 3 runs per model

## Test Scene Setup

Use the `MultiObjectDetection` scene.

For each model:

1. Select the object that has `SentisInferenceRunManager`.
2. Assign the Sentis model in `m_sentisModel`.
3. Assign the matching label file in `m_labelsAsset`.
4. Keep the same detection UI prefab and detection manager references.
5. Confirm the debug log prints the expected input and output format.

Expected label assets:

| Model | Label asset |
| --- | --- |
| Official `yolo9sentis` | `SentisYoloClasses.txt` |
| YOLOv8n COCO models | `exports/labels/coco_80_classes.txt` |
| YOLOv8n VOC model | `exports/labels/voc_20_classes.txt` |

## Fixed Thresholds

Keep these fixed during one comparison run:

| Setting | Recommended value | Notes |
| --- | --- | --- |
| Score threshold | `0.23` | Current sample default |
| IoU threshold | `0.6` | Current sample default |
| Input resolution | `416 x 416` for YOLOv8 models | Official model may differ |
| Camera resolution | Same Quest passthrough resolution | Do not change between models |
| Labels file | Match model dataset | COCO labels for COCO, VOC labels for VOC |

If threshold tuning is tested later, do it as a separate experiment after the fixed-threshold comparison.

## Backend Settings to Test

Run at least one full pass using the current project default backend. If time allows, test both:

| Backend | Purpose |
| --- | --- |
| CPU | Stable baseline and compatibility check |
| GPUCompute or available Unity Inference backend | Speed candidate for Quest deployment |

Only compare backend results within the same model and same scene conditions.

## Metrics to Record

Record both performance and detection quality.

Performance:

- Average inference time per frame, in ms
- Minimum and maximum inference time, in ms
- Approximate FPS during detection
- App responsiveness and visible stutter
- Startup/warmup time
- Any runtime errors or warnings

Detection quality:

- True positives
- False positives
- Missed objects
- Wrong class labels
- Bounding box stability
- Bounding box placement quality in XR space
- Detection flicker
- Detection latency when a new object enters view

Practical XR usability:

- Does the label appear on the correct object?
- Is the object marker stable enough to use?
- Does the model detect useful objects for the demo?
- Does it remain usable while moving the headset?

## Results Table Template

| Run | Model ID | Backend | Labels | Score threshold | IoU threshold | Avg inference ms | Approx FPS | True positives | False positives | Misses | Stability 1-5 | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | M0 | CPU | Official | 0.23 | 0.6 | TBD | TBD | TBD | TBD | TBD | TBD | TBD |
| 1 | M1 | CPU | COCO | 0.23 | 0.6 | TBD | TBD | TBD | TBD | TBD | TBD | TBD |
| 1 | M2 | CPU | COCO | 0.23 | 0.6 | TBD | TBD | TBD | TBD | TBD | TBD | TBD |
| 1 | M3 | CPU | VOC | 0.23 | 0.6 | TBD | TBD | TBD | TBD | TBD | TBD | TBD |

## Decision Rule

Choose the better model using this priority order:

1. It must run without runtime errors in the `MultiObjectDetection` scene.
2. It must use the correct labels and output parser.
3. It must keep detections stable enough for XR placement.
4. It should have the lowest average inference time among models with acceptable detection quality.
5. If two models have similar speed, choose the one with fewer false positives and missed objects.
6. If COCO and VOC are close, choose based on the project demo goal:
   - Choose COCO for broader object coverage.
   - Choose VOC for a smaller class set if it is faster and covers the required demo objects.

Final recommendation format:

| Chosen model | Reason | Tradeoff |
| --- | --- | --- |
| TBD | TBD | TBD |
