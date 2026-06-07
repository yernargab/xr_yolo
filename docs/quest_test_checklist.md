# Quest Test Checklist

Use this checklist for each model test on Quest.

## Before Testing

- [ ] Unity scene is `MultiObjectDetection`.
- [ ] Quest battery is sufficiently charged.
- [ ] Same room and lighting are used for every model.
- [ ] Test objects are placed in the same positions.
- [ ] Passthrough camera permissions are enabled.
- [ ] The app starts from a clean state.
- [ ] The correct Sentis model is assigned.
- [ ] The correct label file is assigned.
- [ ] Score threshold is fixed.
- [ ] IoU threshold is fixed.
- [ ] Backend setting is recorded.
- [ ] Console/debug logs are visible or captured.

## Model Assignment Checklist

| Model | Sentis model assigned | Label file assigned | Backend recorded | Notes |
| --- | --- | --- | --- | --- |
| Official `yolo9sentis` | [ ] | [ ] | [ ] |  |
| YOLOv8n COCO NMS FP32 | [ ] | [ ] | [ ] |  |
| YOLOv8n COCO NMS FP16 | [ ] | [ ] | [ ] |  |
| YOLOv8n VOC NMS FP16 | [ ] | [ ] | [ ] |  |

## Runtime Log Check

Confirm these logs or equivalent information:

- [ ] Assigned Sentis model name is correct.
- [ ] Assigned ONNX model name is correct, if used for debugging.
- [ ] Labels asset name is correct.
- [ ] Input shape is expected.
- [ ] Output count is expected.
- [ ] Official model uses 3-output mode.
- [ ] YOLOv8 NMS model uses single-output mode.
- [ ] No model loading errors.
- [ ] No output shape errors.
- [ ] Worker created successfully.

## Live Test Steps

For each model:

1. Start the scene.
2. Wait 15 seconds for warmup.
3. Look at each test object from the same approximate distance.
4. Move the headset slowly left and right.
5. Move closer and farther from the same objects.
6. Record detection behavior for 60 seconds.
7. Stop the test and write notes before switching models.

## Objects to Test

Use the same objects for every run.

| Object | Present | Expected in COCO | Expected in VOC | Notes |
| --- | --- | --- | --- | --- |
| Person | [ ] | [ ] | [ ] |  |
| Chair | [ ] | [ ] | [ ] |  |
| Bottle or cup | [ ] | [ ] | [ ] |  |
| Laptop or keyboard | [ ] | [ ] | [ ] |  |
| Phone | [ ] | [ ] | [ ] |  |
| Backpack or bag | [ ] | [ ] | [ ] |  |
| Book | [ ] | [ ] | [ ] |  |

## Per-Run Notes

| Run | Model | Backend | Avg inference ms | Approx FPS | Best detections | Problems | Pass/fail |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Official `yolo9sentis` | TBD | TBD | TBD | TBD | TBD | TBD |
| 1 | YOLOv8n COCO FP32 | TBD | TBD | TBD | TBD | TBD | TBD |
| 1 | YOLOv8n COCO FP16 | TBD | TBD | TBD | TBD | TBD | TBD |
| 1 | YOLOv8n VOC FP16 | TBD | TBD | TBD | TBD | TBD | TBD |

## Pass Criteria

A model passes the Quest test if:

- [ ] It loads without errors.
- [ ] It runs for at least 60 seconds.
- [ ] It produces detections for expected objects.
- [ ] It does not produce excessive false positives.
- [ ] Bounding boxes are stable enough for XR use.
- [ ] The app remains responsive.

## Failure Notes

Use this section if a model fails.

| Model | Failure type | Error/log | Likely cause | Next action |
| --- | --- | --- | --- | --- |
| TBD | TBD | TBD | TBD | TBD |
