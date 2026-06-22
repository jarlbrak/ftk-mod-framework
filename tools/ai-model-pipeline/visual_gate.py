"""Shared visual-gate analysis for the FTK custom-enemy 3D pipeline.

This module implements the ONE segmentation-plus-criteria function used by both
the offline Blender preview (work item #67) and the in-game screenshot analyzer
(work item #70). There is exactly one `analyze()` so "the offline preview predicts
the in-game verdict" is true by construction (one function, two callers), not by
two parallel implementations that can drift.

Hard constraints (spec #66, FR-3 / NFR-4):
  - Analysis uses numpy, scipy, and Pillow ONLY. No ML, no trained classifier,
    no heavy new dependency. The render stage (Blender) lives in the caller.
  - No hidden state: every input is an explicit argument; the result is a plain
    dict that the caller serializes to `*_verdict.json` (schema `ftk-visual-gate/1`).
  - All placement/scale thresholds are normalized (fractions of frame size or
    ratios to the baseline), never raw pixel constants, so a Blender-resolution
    preview and a `Screen.width` x `Screen.height` in-game frame compare on the
    same criteria (NFR-5).

Verdict precedence (spec #66, FR-7): INCONCLUSIVE is decided BEFORE any PASS/FAIL
and is mutually exclusive with them. A degenerate frame (unlit/empty, boss not in
frame) or a caller-supplied capture error short-circuits to inconclusive. Only on
a non-degenerate frame are the four mechanical criteria evaluated; the overall
verdict is `pass` only if all four pass, else `fail`.
"""

import json
import os

import numpy as np
from PIL import Image
from scipy import ndimage


SCHEMA = "ftk-visual-gate/1"

# ---------------------------------------------------------------------------
# Tunable constants. Every one is a concrete, documented number. The four
# mechanical thresholds come from spec #66 FR-4 (the initial-threshold table).
# The segmentation / inconclusive floors are documented sanity floors chosen so
# that a valid lit Workbench preview (dark background, lit subject) is NOT
# flagged inconclusive, while a black or empty frame is. Final game-anchored
# tuning happens in #70 against the captured baseline; nothing here hardcodes a
# value that prevents that (baseline-anchored criteria read from `baseline`).
# ---------------------------------------------------------------------------

# Region-of-interest band, as fractions of frame height/width. Excludes the
# bottom UI / health-bar strip and the top vignette so neither biases the mask.
ROI_TOP = 0.06       # drop the top 6 percent (vignette)
ROI_BOTTOM = 0.88    # drop everything below 88 percent (UI / health bar)
ROI_LEFT = 0.04
ROI_RIGHT = 0.96

# --- Unified foreground rule (works in BOTH contexts; #70) -----------------
# The ONE segmentation must isolate the boss against two very different
# backgrounds, so it keys on HUE, not luminance:
#   - IN-GAME: the boss is colorful (cool purple/teal torso, green chest, a
#     pink/magenta cone, a bright sword) against warm desaturated tan/brown
#     stone, warm candle alcoves, and a near-black top vignette. The stone and
#     candles are WARM (red is the dominant channel), so any rule that demands a
#     cool / green / magenta hue rejects them while the boss pops.
#   - OFFLINE preview: a bright near-white emission silhouette on a near-black
#     world. Warm/cool hue does not separate it (it is colorless), but it is very
#     bright and unsaturated, so the bright_white clause catches it.
# A pixel is foreground if it satisfies ANY of the four clauses below. The
# per-channel deltas and saturation floors were tuned empirically against the
# committed fixtures (baseline_boss.png, candidate_boss.png) and the offline
# white render so the troll silhouette (not the room) is the largest component
# in-game and the white body is one clean blob offline.
FG_COOL_BR_DELTA = 10.0     # cool: blue >= red + this (purple/teal break warm)
FG_COOL_BG_DELTA = 5.0      # ... and blue >= green - this
FG_COOL_SAT = 0.20
FG_GREEN_DELTA = 25.0       # green: green >= red+ AND green >= blue+ this
FG_GREEN_SAT = 0.25
FG_MAGENTA_RG = 40.0        # magenta/pink: red >= green + this ...
FG_MAGENTA_BG = 25.0        # ... and blue >= green + this (green is the min)
FG_MAGENTA_SAT = 0.30
FG_WHITE_LUM = 200.0        # bright_white (offline silhouette): luminance above ...
FG_WHITE_SAT = 0.25         # ... and saturation below this (near-colorless)

# Morphological closing kernel, as a fraction of min(H, W). The hue rule leaves
# thin internal gaps where the boss has dark shadow seams (between shoulder and
# torso, around the sword); a small close bridges them so a single coherent body
# reads as ONE component, while a genuine shatter (many separated colored shards)
# stays fragmented. Tuned at 0.010 * min(H,W): on the 2294x1432 in-game frame the
# stock troll closes to count=1 / fill~0.95 (pass) and the shatter stays count~6 /
# fill~0.40 (fail); a larger kernel would wrongly fuse the shards. Normalized so
# the 720px offline render uses a proportionally smaller kernel.
CLOSE_KERNEL_FRAC = 0.010

# Connected-component noise floor for COUNTING significant components. A
# component counts only if it is a meaningful fraction of the LARGEST component
# (not just a sliver). This is the right discriminator for "single coherent body
# vs shard cloud": a real shatter has many components of COMPARABLE size, so it
# counts many; a coherent body has one giant plus only slivers (detached low-poly
# triangle islands), so it counts one. The absolute ROI floor below is a secondary
# guard so an all-speckle frame cannot inflate the count either.
NOISE_FLOOR_OF_LARGEST = 0.05    # a part must be >=5 percent of the largest to count
NOISE_FLOOR_FRAC = 0.0008        # ... and at least this fraction of the ROI area

# INCONCLUSIVE floors (documented; FR-7).
INCONCLUSIVE_MEAN_LUM_FLOOR = 6.0   # whole-frame mean luminance below this -> unlit
INCONCLUSIVE_FG_FRAC_FLOOR = 0.004  # foreground fraction of the ROI below this -> empty
INCONCLUSIVE_BOSS_SIZE_FLOOR = 0.01 # largest comp below this fraction of ROI -> not in frame
CORNER_ROI_FRAC = 0.18              # centroid inside a corner box this size -> not in frame

# --- Four mechanical criteria, FINALIZED thresholds (#70) -----------------
# Finalized against the real captured baseline (fixtures/baseline_boss.png) with
# the unified hue segmentation above. Numbers verified empirically:
#   stock troll silhouette -> connected PASS (count=1, fill~0.95), upright PASS
#     (angle~1.9deg, aspect~1.37), centered PASS (cx~0.495, cy~0.628).
#   runtime shatter (candidate_boss.png) -> connected FAIL (count~6, fill~0.40).

# connected (baseline-free): a coherent body is one near-solid blob; a shatter is
# many comparable shards. Floor 0.85 of the largest component over all foreground,
# count cap 2 (body plus an optional baked lantern). Stock troll fill ~0.95.
CONNECTED_FILL_FLOOR = 0.85
CONNECTED_MAX_COMPONENTS = 2

# upright (angle baseline-free, aspect baseline-anchored). Stock troll major-axis
# angle is ~1.9deg from vertical and aspect ~1.37; 15deg tolerance and a 0.9 *
# baseline-aspect floor leave comfortable margin while a toppled / wider-than-tall
# shatter (candidate angle ~18deg, aspect ~0.80) fails.
UPRIGHT_ANGLE_TOL_DEG = 15.0
UPRIGHT_ASPECT_BASELINE_FRAC = 0.9  # aspect must be >= 0.9 * baseline aspect

# centered (fractions of frame size), anchored to baseline.centroid_norm
# (~0.495, 0.628). The boss stands center-x but low-center-y (the diorama frames
# it in the lower-middle), so dy is naturally larger than dx; tolerances bracket
# normal combat-settle jitter without admitting an off-screen body.
CENTERED_DX_TOL = 0.06
CENTERED_DY_TOL = 0.08

# scaled (ratios vs baseline). Decompile-verified: the custom boss body renders at
# EnemyVisual.scale = 1.4 (localScale.Y set ABSOLUTELY on a fresh 1.0 clone in
# EnemyVisualPatch; the game applies NO extra spawn scale, per
# EnemyDummy.InitEnemyDummyForCombat / FTKHub.GetEnemyPrefab). So the authoritative
# signal is the HEIGHT (Y) ratio, centered on 1.4. We do NOT key on width/diagonal
# (X/Z carry widthBoost, broader than tall) and do NOT compare against m_MarkerScale
# = 1.568 (a collider footprint, not visible height). With widthBoost = 1 the area
# ratio is ~1.4^2 = 1.96, kept as a secondary check centered there. The band
# DELIBERATELY excludes 1.0, so the scale-1.0 stock troll baseline does NOT pass
# scaled: correct gate behavior (it is the stock chassis, not the upscaled body).
SCALED_HEIGHT_LO = 1.20             # excludes the scale-1.0 stock troll (ratio ~1.0)
SCALED_HEIGHT_HI = 1.65             # centered on 1.4 (1.42 mid)
SCALED_AREA_LO = 1.45               # centered on ~1.96 (1.4^2)
SCALED_AREA_HI = 2.60

# --- Offline defaults (baseline is None) ---------------------------------
# When no game baseline exists yet (THIS offline phase, #67), the three
# baseline-anchored criteria degrade gracefully so the preview still returns a
# sensible mechanical verdict:
#   - centered  : measured against frame-center (cx = cy = 0.5).
#   - upright   : the aspect part is checked against an absolute aspect floor
#                 (a coherent upright body is clearly taller than wide).
#   - scaled    : checked against an absolute coherent-fill sanity band on the
#                 largest component's bbox area as a fraction of the ROI, since
#                 there is no baseline height/area to ratio against. This only
#                 fails a frame that is wildly mis-framed, not a normal preview.
# These are explicitly OFFLINE defaults. The real game-anchored numbers are
# finalized in #70 once `baseline.json` exists; pass a baseline to use them.
OFFLINE_CENTERED_CX = 0.5
OFFLINE_CENTERED_CY = 0.5
OFFLINE_UPRIGHT_ASPECT_FLOOR = 0.9   # height/width of an upright body, absolute
OFFLINE_SCALED_BBOX_FRAC_LO = 0.05   # largest-comp bbox area / ROI area, sanity band
OFFLINE_SCALED_BBOX_FRAC_HI = 0.92


def _load_baseline(baseline):
    """Normalize the `baseline` argument to a dict or None.

    Accepts: None, a path to a JSON file, or an already-loaded dict. A baseline
    carries the stock-troll silhouette: keys `bbox` [x,y,w,h], `centroid_norm`
    [cx,cy], `height` (normalized bbox height), and `aspect` (height/width).
    """
    if baseline is None:
        return None
    if isinstance(baseline, dict):
        return baseline
    with open(baseline, "r") as f:
        return json.load(f)


def _hue_foreground(r, g, b, lum, sat):
    """Per-pixel boolean foreground from the four hue clauses (see constants).

    r/g/b/lum/sat are matching float arrays. A pixel is foreground if it is a
    cool (purple/teal), green, or magenta/pink subject color, OR a bright
    near-white silhouette pixel (the offline render). Warm stone and warm candle
    glow have red as the dominant channel and pass none of the colored clauses.
    """
    cool = (b >= r + FG_COOL_BR_DELTA) & (b >= g - FG_COOL_BG_DELTA) & (sat > FG_COOL_SAT)
    green = (g >= r + FG_GREEN_DELTA) & (g >= b + FG_GREEN_DELTA) & (sat > FG_GREEN_SAT)
    magenta = (r >= g + FG_MAGENTA_RG) & (b >= g + FG_MAGENTA_BG) & (sat > FG_MAGENTA_SAT)
    white = (lum > FG_WHITE_LUM) & (sat < FG_WHITE_SAT)
    return cool | green | magenta | white


def _foreground_mask(rgb):
    """Return (mask, roi_slice, lum) for the single binary foreground mask.

    rgb is an (H, W, 3) uint8/float array. The mask is True for foreground and
    is restricted to the center ROI band; pixels outside the band are False.
    Foreground is the unified hue rule (_hue_foreground), then a small
    size-normalized morphological close bridges shadow seams so a coherent body
    is one component. `lum` (whole-frame luminance) is returned for the
    inconclusive unlit check.
    """
    a = rgb.astype(np.float32)
    h, w = a.shape[0], a.shape[1]

    lum = 0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2]
    mx = a.max(axis=2)
    mn = a.min(axis=2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1.0), 0.0)

    y0, y1 = int(ROI_TOP * h), int(ROI_BOTTOM * h)
    x0, x1 = int(ROI_LEFT * w), int(ROI_RIGHT * w)

    fg = np.zeros((h, w), dtype=bool)
    fg[y0:y1, x0:x1] = _hue_foreground(
        a[y0:y1, x0:x1, 0], a[y0:y1, x0:x1, 1], a[y0:y1, x0:x1, 2],
        lum[y0:y1, x0:x1], sat[y0:y1, x0:x1],
    )

    k = max(3, int(round(CLOSE_KERNEL_FRAC * min(h, w))) | 1)  # odd, >= 3
    mask = ndimage.binary_closing(fg, structure=np.ones((k, k), dtype=bool))
    # Closing can only grow into the ROI interior; re-clip to the band for safety.
    clipped = np.zeros((h, w), dtype=bool)
    clipped[y0:y1, x0:x1] = mask[y0:y1, x0:x1]
    return clipped, (y0, y1, x0, x1), lum


def _pca_major_axis_angle_deg(ys, xs):
    """Angle (degrees) between the largest component's major axis and VERTICAL.

    Plain-numpy PCA: the major axis is the eigenvector of the 2D covariance of
    the component's pixel coordinates with the largest eigenvalue. We return the
    deviation from vertical in [0, 90]; an upright body is near 0.
    """
    if len(xs) < 3:
        return 90.0
    pts = np.stack([xs.astype(np.float64), ys.astype(np.float64)], axis=1)
    pts = pts - pts.mean(axis=0, keepdims=True)
    cov = np.cov(pts, rowvar=False)
    evals, evecs = np.linalg.eigh(cov)
    major = evecs[:, int(np.argmax(evals))]  # (dx, dy) in image coords (y down)
    # Vertical is (0, 1). Angle of the major axis from vertical, folded to [0,90].
    ang = np.degrees(np.arctan2(abs(major[0]), abs(major[1])))
    return float(ang)


def _criterion(passed, value, threshold):
    return {"pass": bool(passed), "value": float(value), "threshold": threshold}


def analyze(frame_png_path, baseline, capture_error=None):
    """Segment one frame and return the verdict dict (schema `ftk-visual-gate/1`).

    Args:
      frame_png_path: path to the full-frame PNG to analyze.
      baseline: None, a path to `baseline.json`, or a loaded baseline dict. When
        None (the offline #67 phase), the baseline-anchored criteria use the
        documented offline defaults above.
      capture_error: optional string naming a capture-time failure (e.g.
        "screenshot-503-no-session"). When set, the verdict is INCONCLUSIVE with
        that exact reason and no segmentation is attempted.

    Returns: a plain dict matching the verdict schema. The caller writes it to
    `*_verdict.json` and may overwrite `annotated_image` / `crop_image` /
    `recognizable` (the in-game analyzer #70 produces the annotated/crop images).
    """
    bl = _load_baseline(baseline)
    baseline_path = baseline if isinstance(baseline, str) else None

    # A caller-supplied capture error names its own cause and short-circuits.
    if capture_error:
        return _inconclusive_shell(frame_png_path, baseline_path, 0, 0, capture_error)

    img = Image.open(frame_png_path).convert("RGB")
    rgb = np.asarray(img)
    image_h, image_w = rgb.shape[0], rgb.shape[1]

    mask, (y0, y1, x0, x1), lum = _foreground_mask(rgb)
    roi_area = float((y1 - y0) * (x1 - x0))
    fg_count = float(mask.sum())
    fg_frac = fg_count / max(roi_area, 1.0)
    mean_lum = float(lum.mean())

    # --- INCONCLUSIVE: unlit or empty frame (runs first) ---
    if mean_lum < INCONCLUSIVE_MEAN_LUM_FLOOR or fg_frac < INCONCLUSIVE_FG_FRAC_FLOOR:
        return _inconclusive_shell(
            frame_png_path, baseline_path, image_w, image_h, "unlit-or-empty-frame"
        )

    # Connected components on the single mask.
    labeled, n = ndimage.label(mask)
    if n == 0:
        return _inconclusive_shell(
            frame_png_path, baseline_path, image_w, image_h, "unlit-or-empty-frame"
        )
    sizes = ndimage.sum(np.ones_like(labeled), labeled, index=np.arange(1, n + 1))
    largest_label = int(np.argmax(sizes)) + 1
    largest_area = float(sizes[largest_label - 1])
    # A component is "significant" only if it is both a fraction of the largest
    # component AND of the ROI; everything else is sliver noise.
    noise_floor_px = max(NOISE_FLOOR_FRAC * roi_area, NOISE_FLOOR_OF_LARGEST * largest_area)
    component_count = int((sizes >= noise_floor_px).sum())
    fill_fraction = largest_area / max(fg_count, 1.0)

    comp = labeled == largest_label
    ys, xs = np.nonzero(comp)
    bx, by = int(xs.min()), int(ys.min())
    bw, bh = int(xs.max() - bx + 1), int(ys.max() - by + 1)
    cx = float((bx + bw / 2.0) / image_w)
    cy = float((by + bh / 2.0) / image_h)
    largest_frac_of_roi = largest_area / max(roi_area, 1.0)

    # --- INCONCLUSIVE: boss not in frame ---
    in_corner = (
        (cx < CORNER_ROI_FRAC or cx > 1.0 - CORNER_ROI_FRAC)
        and (cy < CORNER_ROI_FRAC or cy > 1.0 - CORNER_ROI_FRAC)
    )
    if largest_frac_of_roi < INCONCLUSIVE_BOSS_SIZE_FLOOR or in_corner:
        return _inconclusive_shell(
            frame_png_path, baseline_path, image_w, image_h, "boss-not-in-frame",
            metrics_override={
                "fill_fraction": fill_fraction,
                "component_count": component_count,
                "bbox": [bx, by, bw, bh],
                "centroid_norm": [cx, cy],
                "height_ratio": None,
            },
        )

    # --- Mechanical metrics ---
    angle = _pca_major_axis_angle_deg(ys, xs)
    aspect = float(bh) / max(float(bw), 1.0)
    height_norm = float(bh) / image_h
    area_norm = largest_area / (image_w * image_h)

    # --- Criterion: connected (baseline-free) ---
    connected = _criterion(
        fill_fraction >= CONNECTED_FILL_FLOOR and component_count <= CONNECTED_MAX_COMPONENTS,
        fill_fraction,
        "fill_fraction>=%.2f AND component_count<=%d (count=%d)"
        % (CONNECTED_FILL_FLOOR, CONNECTED_MAX_COMPONENTS, component_count),
    )

    # --- Criterion: upright (angle baseline-free; aspect baseline-anchored) ---
    angle_ok = angle <= UPRIGHT_ANGLE_TOL_DEG
    if bl is not None:
        base_aspect = float(bl["aspect"])
        aspect_floor = UPRIGHT_ASPECT_BASELINE_FRAC * base_aspect
        aspect_note = "aspect>=%.2f*baseline_aspect=%.2f" % (
            UPRIGHT_ASPECT_BASELINE_FRAC, aspect_floor)
    else:
        aspect_floor = OFFLINE_UPRIGHT_ASPECT_FLOOR
        aspect_note = "aspect>=%.2f (offline absolute floor; no baseline)" % aspect_floor
    upright = _criterion(
        angle_ok and aspect >= aspect_floor,
        angle,
        "major_axis_angle<=%.1fdeg AND %s (aspect=%.2f)"
        % (UPRIGHT_ANGLE_TOL_DEG, aspect_note, aspect),
    )

    # --- Criterion: centered (baseline-anchored; offline uses frame center) ---
    if bl is not None:
        base_cx, base_cy = float(bl["centroid_norm"][0]), float(bl["centroid_norm"][1])
        centered_note = "|cx-%.3f|<=%.2f AND |cy-%.3f|<=%.2f (baseline)" % (
            base_cx, CENTERED_DX_TOL, base_cy, CENTERED_DY_TOL)
    else:
        base_cx, base_cy = OFFLINE_CENTERED_CX, OFFLINE_CENTERED_CY
        centered_note = "|cx-%.2f|<=%.2f AND |cy-%.2f|<=%.2f (offline frame-center)" % (
            base_cx, CENTERED_DX_TOL, base_cy, CENTERED_DY_TOL)
    dx = abs(cx - base_cx)
    dy = abs(cy - base_cy)
    centered = _criterion(
        dx <= CENTERED_DX_TOL and dy <= CENTERED_DY_TOL,
        max(dx, dy),
        centered_note,
    )

    # --- Criterion: scaled (baseline-anchored; offline uses bbox-fill sanity band) ---
    if bl is not None:
        base_height = float(bl["height"])
        base_area = float(bl["area"]) if "area" in bl else (base_height * (base_height / float(bl["aspect"])))
        height_ratio = height_norm / max(base_height, 1e-9)
        area_ratio = area_norm / max(base_area, 1e-9)
        scaled = _criterion(
            SCALED_HEIGHT_LO <= height_ratio <= SCALED_HEIGHT_HI
            and SCALED_AREA_LO <= area_ratio <= SCALED_AREA_HI,
            height_ratio,
            "height_ratio in [%.2f,%.2f] AND area_ratio in [%.2f,%.2f] (area_ratio=%.2f)"
            % (SCALED_HEIGHT_LO, SCALED_HEIGHT_HI, SCALED_AREA_LO, SCALED_AREA_HI, area_ratio),
        )
    else:
        height_ratio = None
        scaled = _criterion(
            OFFLINE_SCALED_BBOX_FRAC_LO <= largest_frac_of_roi <= OFFLINE_SCALED_BBOX_FRAC_HI,
            largest_frac_of_roi,
            "bbox_area/ROI in [%.2f,%.2f] (offline coherent-fill sanity; no baseline)"
            % (OFFLINE_SCALED_BBOX_FRAC_LO, OFFLINE_SCALED_BBOX_FRAC_HI),
        )

    criteria = {
        "connected": connected,
        "upright": upright,
        "centered": centered,
        "scaled": scaled,
    }
    overall_pass = all(c["pass"] for c in criteria.values())

    return {
        "schema": SCHEMA,
        "verdict": "pass" if overall_pass else "fail",
        "inconclusive_reason": None,
        "source_image": frame_png_path,
        "baseline_path": baseline_path,
        "image_w": int(image_w),
        "image_h": int(image_h),
        "criteria": criteria,
        "metrics": {
            "fill_fraction": fill_fraction,
            "component_count": component_count,
            "bbox": [bx, by, bw, bh],
            "centroid_norm": [cx, cy],
            "height_ratio": height_ratio,
        },
        "annotated_image": None,
        "crop_image": None,
        "recognizable": "pending",
    }


def _inconclusive_shell(source, baseline_path, w, h, reason, metrics_override=None):
    """Build an INCONCLUSIVE verdict. Criteria are present but not passing."""
    metrics = {
        "fill_fraction": 0.0,
        "component_count": 0,
        "bbox": [0, 0, 0, 0],
        "centroid_norm": [0.0, 0.0],
        "height_ratio": None,
    }
    if metrics_override:
        metrics.update(metrics_override)
    blank = {"pass": False, "value": 0.0, "threshold": "not evaluated (inconclusive)"}
    return {
        "schema": SCHEMA,
        "verdict": "inconclusive",
        "inconclusive_reason": reason,
        "source_image": source,
        "baseline_path": baseline_path,
        "image_w": int(w),
        "image_h": int(h),
        "criteria": {
            "connected": dict(blank),
            "upright": dict(blank),
            "centered": dict(blank),
            "scaled": dict(blank),
        },
        "metrics": metrics,
        "annotated_image": None,
        "crop_image": None,
        "recognizable": "pending",
    }


def write_verdict(verdict, out_path):
    """Serialize a verdict dict to `out_path` as pretty JSON. Returns out_path."""
    with open(out_path, "w") as f:
        json.dump(verdict, f, indent=2)
        f.write(os.linesep)
    return out_path
