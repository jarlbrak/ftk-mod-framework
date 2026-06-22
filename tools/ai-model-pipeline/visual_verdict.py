"""In-game verdict.json analyzer + crop for the FTK custom-enemy 3D pipeline.

This is the IN-GAME half of the visual-verification gate (spec #66; work item
#70). It takes a full-frame screenshot PNG already captured by the harness driver
(#69), runs the SAME shared `visual_gate.analyze(...)` the offline preview (#67)
uses, and writes three artifacts next to a `verdict.json`:

  - `verdict.json`     : the `ftk-visual-gate/1` verdict for the FULL FRAME (all
                         four mechanical criteria, metrics, image_w/h). The four
                         criteria come from the full-frame mask, never from a crop,
                         so a flattering crop can NEVER manufacture a PASS.
  - `<name>_annotated.png` : the full frame with the detected bbox, the centroid,
                         and the ROI band drawn over it, for a quick eyeball.
  - `<name>_crop.png`  : a zoomed crop of the detected boss region (with margin),
                         the image a human / agent looks at to make the
                         recognizability call. `recognizable` stays "pending":
                         it is an agent judgement, never machine-set.

The verdict also records `reference_render` (the offline preview render of the
SAME asset) so a reviewer can view the in-game crop side by side with what the
asset SHOULD look like. A divergence (offline coherent, in-game shattered) is the
exact signal this gate is built to surface.

Run it with the venv interpreter (numpy/scipy/Pillow live there):

  /Users/tbrack/Documents/Projects/FTK/.venv-3dgen/bin/python \
    tools/ai-model-pipeline/visual_verdict.py \
    --frame tools/ai-model-pipeline/fixtures/candidate_boss.png \
    --baseline tools/ai-model-pipeline/baseline.json \
    [--out-dir /tmp/ftk_verdict] \
    [--reference-render /tmp/ftk_preview/preview_render.png] \
    [--capture-error "screenshot-503-no-session"]

The segmentation is NOT duplicated here: it imports `visual_gate`. This module
only adds the in-game presentation (annotate + crop) on top of the shared verdict.
Exit code mirrors the verdict: 0 pass, 1 fail, 3 inconclusive, 2 usage error.
"""

import argparse
import json
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import visual_gate  # noqa: E402  (after sys.path setup so the venv run finds it)

# Crop margin around the detected bbox, as a fraction of the bbox size, so the
# crop shows the boss with a little breathing room (never tighter than the bbox).
CROP_MARGIN_FRAC = 0.20


def _roi_box(image_w, image_h):
    """The same ROI band the shared mask uses, in pixels (for annotation)."""
    y0 = int(visual_gate.ROI_TOP * image_h)
    y1 = int(visual_gate.ROI_BOTTOM * image_h)
    x0 = int(visual_gate.ROI_LEFT * image_w)
    x1 = int(visual_gate.ROI_RIGHT * image_w)
    return x0, y0, x1, y1


def annotate_frame(frame_png, verdict, out_png):
    """Draw the bbox, centroid, and ROI band over the full frame. Returns out_png.

    Reads geometry straight from the verdict so the drawing can never disagree
    with the numbers the criteria were computed from.
    """
    img = Image.open(frame_png).convert("RGB")
    draw = ImageDraw.Draw(img)
    w, h = img.size

    # ROI band (the analysis region), thin gray rectangle.
    x0, y0, x1, y1 = _roi_box(w, h)
    draw.rectangle([x0, y0, x1, y1], outline=(120, 120, 120), width=2)

    bx, by, bw, bh = verdict["metrics"]["bbox"]
    if bw > 0 and bh > 0:
        # Detected bbox: green if the overall verdict passed, else red.
        color = (60, 220, 60) if verdict["verdict"] == "pass" else (230, 60, 60)
        draw.rectangle([bx, by, bx + bw, by + bh], outline=color, width=4)

    cx_n, cy_n = verdict["metrics"]["centroid_norm"]
    if cx_n or cy_n:
        cx, cy = int(cx_n * w), int(cy_n * h)
        r = max(6, int(0.006 * min(w, h)))
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(255, 220, 0), width=4)

    img.save(out_png)
    return out_png


def crop_boss(frame_png, verdict, out_png):
    """Save a zoomed crop of the detected boss region (with margin). Returns the
    out_png path, or None when there is no bbox (inconclusive / empty frame)."""
    bx, by, bw, bh = verdict["metrics"]["bbox"]
    if bw <= 0 or bh <= 0:
        return None
    img = Image.open(frame_png).convert("RGB")
    w, h = img.size
    mx = int(CROP_MARGIN_FRAC * bw)
    my = int(CROP_MARGIN_FRAC * bh)
    left = max(0, bx - mx)
    top = max(0, by - my)
    right = min(w, bx + bw + mx)
    bottom = min(h, by + bh + my)
    img.crop((left, top, right, bottom)).save(out_png)
    return out_png


def main():
    ap = argparse.ArgumentParser(
        description="In-game verdict.json analyzer + crop (shared visual_gate).")
    ap.add_argument("--frame", required=True,
                    help="full-frame in-game screenshot PNG to analyze")
    ap.add_argument("--baseline", default=os.path.join(HERE, "baseline.json"),
                    help="path to baseline.json (the stock-troll silhouette)")
    ap.add_argument("--out-dir", default="/tmp/ftk_verdict",
                    help="directory for verdict.json, the annotated frame, and the crop")
    ap.add_argument("--reference-render", default=None,
                    help="path to the offline preview render of the same asset "
                         "(recorded as reference_render for side-by-side review)")
    ap.add_argument("--capture-error", default=None,
                    help="name a capture-time failure -> INCONCLUSIVE with that reason")
    args = ap.parse_args()

    if not args.capture_error and not os.path.exists(args.frame):
        print("ERROR: --frame does not exist: " + args.frame)
        return 2

    os.makedirs(args.out_dir, exist_ok=True)
    frame = os.path.abspath(args.frame)
    base = os.path.basename(frame)
    stem = os.path.splitext(base)[0]
    verdict_path = os.path.join(args.out_dir, "verdict.json")

    # ANALYSIS: the SAME shared analyze() the offline preview (#67) uses. The four
    # mechanical criteria are computed here from the FULL-FRAME mask.
    verdict = visual_gate.analyze(frame, args.baseline, capture_error=args.capture_error)

    # Presentation artifacts (in-game only). On an inconclusive / empty frame
    # there is no bbox, so the crop is skipped; the annotated frame still renders
    # the ROI band so a reviewer can see what was analyzed.
    annotated_path = os.path.join(args.out_dir, stem + "_annotated.png")
    crop_path = os.path.join(args.out_dir, stem + "_crop.png")
    if not args.capture_error and os.path.exists(frame):
        verdict["annotated_image"] = annotate_frame(frame, verdict, annotated_path)
        verdict["crop_image"] = crop_boss(frame, verdict, crop_path)
    else:
        verdict["annotated_image"] = None
        verdict["crop_image"] = None
    verdict["reference_render"] = (
        os.path.abspath(args.reference_render) if args.reference_render else None)

    visual_gate.write_verdict(verdict, verdict_path)

    print("\n=== IN-GAME VERDICT -> %s ===" % verdict_path)
    print(json.dumps(verdict, indent=2))

    v = verdict["verdict"]
    if v == "pass":
        print("\nVERDICT PASS: all four mechanical criteria pass on the full frame.")
        print("Recognizability is still 'pending' (an agent reviews the crop).")
        return 0
    if v == "fail":
        failed = [k for k, d in verdict["criteria"].items() if not d["pass"]]
        print("\nVERDICT FAIL: criteria failed: %s. See the annotated frame and crop."
              % ", ".join(failed))
        return 1
    print("\nVERDICT INCONCLUSIVE: %s (no pass/fail decision)." % verdict["inconclusive_reason"])
    return 3


if __name__ == "__main__":
    sys.exit(main())
