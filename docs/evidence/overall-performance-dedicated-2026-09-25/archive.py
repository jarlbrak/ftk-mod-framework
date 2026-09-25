"""Rebuild the overall benchmark summary from retained isolated frame captures."""
import csv
import hashlib
import json
import statistics
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
GAME = ROOT / 'scratch' / 'perf-game'
source = [json.loads(line) for line in (ROOT / 'scratch' / 'dedicated-20260925.jsonl').read_text().splitlines()]
frames = {}
rows = []
for item in source:
    ident = item['id']
    capture = json.loads((GAME / ('frame-profile-' + ident + '.json')).read_text())
    path = GAME / capture['csv']
    assert hashlib.sha256(path.read_bytes()).hexdigest() == item['csvSha256']
    frames[ident] = [float(row['frame_ms']) for row in csv.DictReader(path.open())]
    resources = item['resources']
    row = {key: value for key, value in item.items() if key not in ('resources', 'binaries')}
    row['binaries'] = {b['name']: b['sha256'] for b in item['binaries'] if b['name'] in (
        'FTKModFramework.dll', 'FtkFrameProfiler.dll', 'FTKPerfProbe.dll', 'Assembly-CSharp.dll',
        'FtkRuntimeModelTest.dll', 'FtkRuntimeModelTestContent.dll')}
    row['resources'] = {key: resources[key] for key in ('processCpuSeconds', 'processCpuObservationWallSeconds',
        'processCpuPercentOneCore', 'captureWallSeconds')}
    row['unityAllocatedStart'] = resources['start']['unityAllocatedBytes']
    row['unityAllocatedEnd'] = resources['end']['unityAllocatedBytes']
    row['processCpuMsPerFrame'] = resources['processCpuSeconds'] * 1000 / item['frames']
    row['hostLoadSeries'] = [sample['load'] for sample in item['hostLoadSeries']]
    rows.append(row)


def aggregate(group):
    samples = sorted(x for row in group for x in frames[row['id']])
    cpu = sum(row['resources']['processCpuSeconds'] for row in group)
    wall = sum(row['resources']['processCpuObservationWallSeconds'] for row in group)
    footprint = [row[k]['footprint'] / 2**30 for row in group for k in ('hostMemoryBefore', 'hostMemoryAfter')]
    return {'captures': len(group), 'frames': len(samples), 'fps': 1000 * len(samples) / sum(samples),
        'captureFpsRange': [min(row['fps'] for row in group), max(row['fps'] for row in group)],
        'p95Ms': samples[int((len(samples)-1)*.95)], 'p99Ms': samples[int((len(samples)-1)*.99)],
        'over33Percent': 100 * sum(t > 33.333 for t in samples) / len(samples),
        'cpuPercentOneCore': 100 * cpu / wall, 'cpuMsPerFrame': 1000 * cpu / len(samples),
        'medianFootprintGiB': statistics.median(footprint), 'footprintRangeGiB': [min(footprint), max(footprint)]}

assert len(rows) == 8 and all(row['included'] for row in rows)
eligible = rows
primary = {arm: aggregate([r for r in eligible if r['arm'] == arm]) for arm in ('baseline', 'current')}
launches = {name: aggregate([r for r in eligible if r['id'].startswith(name + '-')]) for name in ('dedicated-a1', 'dedicated-b1', 'dedicated-b2', 'dedicated-a2')}
result = {
    'scope': 'Game plus pre-performance framework 5561401f versus current 12b2be3e, isolated single-player stationary Oarton overworld, empty content package.',
    'environment': 'Apple M5, macOS 26.6.2, x86_64 Unity 2017.2.2p2/Mono, 1280x720, quality4, VSync0, uncapped.',
    'method': {'plannedOrder': ['baseline', 'current', 'current', 'baseline'],
        'captureSeconds': 30, 'capturesPerLaunch': 2, 'postCameraSettleSeconds': 60, 'profilerWarmupFrames': 120,
        'samplers': [], 'managedCallbackTiming': False, 'forcedGc': False,
        'currentDefaults': {'OptimizeWaterMeshes': True, 'AvoidUnusedEncounterPortraitTextures': False, 'nativeWaterExperiment': False},
        'includedRule': 'All frames focused, no focus transitions. Unfocused captures retained but excluded.',
        'aggregation': 'Pooled raw frames; FPS total frames / summed frame intervals. Percentiles floor((n-1)*p). CPU total process time / observation wall time, 100%=one logical CPU. Memory median of capture endpoint physical footprints, not RSS, peaks or summed Unity/driver memory.',
        'followup': 'None planned; four fresh launches in ABBA order.'},
    'primaryABBA': primary, 'launches': launches,
    'setupExclusion': 'One closing-baseline launch missed the camera target and was restarted before any timed captures. No timed captures excluded.',
    'sleepPrevention': 'Temporary display/system idle-sleep assertion for the final accepted baseline process only; ended when process stopped.',
    'trials': rows,
    'limitations': ['Two captures per launch are correlated; the original ABBA has four independent launches.',
        'Dedicated foreground window with one game instance at setup and no competing build observed; backup and ordinary host services remained active. No unrelated processes stopped. Host load recorded, not fully controlled. No statistical significance or causal attribution to one patch.',
        'Baseline includes older framework behavior; current also contains unrelated reporting/UI/gameplay changes.',
        'Static overworld does not exercise mod-heavy textures, repeated encounters, combat, co-op or long-session behavior.',
        'Matched scene/settings and setup screenshots were inspected; screenshot capture and inventory happen outside timed sampling.',
        'Equal post-camera settling, but process lifetime and memory endpoints are not a controlled leak experiment.']
}
Path(__file__).with_name('summary.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({'primaryABBA': primary, 'launches': launches}, indent=2))
