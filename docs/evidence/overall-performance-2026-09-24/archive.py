"""Rebuild the overall benchmark summary from retained isolated frame captures."""
import csv
import hashlib
import json
import statistics
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
GAME = ROOT / 'scratch' / 'perf-game'
source = [json.loads(line) for line in (ROOT / 'scratch' / 'overall-20260924.jsonl').read_text().splitlines()]
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
    row['phase'] = 'followup' if ident.startswith('overall-b3') else 'ABBA'
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

eligible = [row for row in rows if row['included']]
primary = {arm: aggregate([r for r in eligible if r['arm'] == arm and r['phase'] == 'ABBA']) for arm in ('baseline', 'current')}
all_runs = {arm: aggregate([r for r in eligible if r['arm'] == arm]) for arm in ('baseline', 'current')}
result = {
    'scope': 'Game plus pre-performance framework 5561401f versus current 12b2be3e, isolated single-player stationary Oarton overworld, empty content package.',
    'environment': 'Apple M5, macOS 26.6.2, x86_64 Unity 2017.2.2p2/Mono, 1280x720, quality4, VSync0, uncapped.',
    'method': {'plannedOrder': ['baseline', 'current', 'current', 'baseline'],
        'captureSeconds': 30, 'capturesPerLaunch': 2, 'postCameraSettleSeconds': 20, 'profilerWarmupFrames': 120,
        'samplers': [], 'managedCallbackTiming': False, 'forcedGc': False,
        'currentDefaults': {'OptimizeWaterMeshes': True, 'AvoidUnusedEncounterPortraitTextures': False, 'nativeWaterExperiment': False},
        'includedRule': 'All frames focused, no focus transitions. Unfocused captures retained but excluded.',
        'aggregation': 'Pooled raw frames; FPS total frames / summed frame intervals. Percentiles floor((n-1)*p). CPU total process time / observation wall time, 100%=one logical CPU. Memory median of capture endpoint physical footprints, not RSS, peaks or summed Unity/driver memory.',
        'followup': 'Additional current launch after original ABBA because the current-build slowdown required checking; reported separately and in all-run aggregate.'},
    'primaryABBA': primary, 'includingFollowup': all_runs,
    'followup': aggregate([r for r in eligible if r['phase'] == 'followup']) if any(r['phase'] == 'followup' for r in eligible) else None,
    'trials': rows,
    'limitations': ['Two captures per launch are correlated; the original ABBA has four independent launches.',
        'Competing host workloads were not controlled; no unrelated processes were stopped. No statistical significance or causal attribution to one patch.',
        'Baseline includes older framework behavior; current also contains unrelated reporting/UI/gameplay changes.',
        'Static overworld does not exercise mod-heavy textures, repeated encounters, combat, co-op or long-session behavior.',
        'Matched scene/settings and setup screenshots were inspected; screenshot capture and inventory happen outside timed sampling.',
        'Focus retries extended baseline process age before accepted captures; memory figures are endpoint observations, not a controlled leak/lifetime experiment.']
}
Path(__file__).with_name('summary.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({'primaryABBA': primary, 'includingFollowup': all_runs, 'followup': result['followup']}, indent=2))
