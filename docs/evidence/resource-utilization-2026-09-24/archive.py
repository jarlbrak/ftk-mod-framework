"""Rebuild the bounded public resource evidence from retained isolated captures."""
import hashlib
import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
SCRATCH = REPO / 'scratch'
GAME = SCRATCH / 'perf-game'
OUT = Path(__file__).with_name('summary.json')

def read(name):
    return json.loads((GAME / name).read_text())

def rows(name):
    return [json.loads(line) for line in (SCRATCH / (name + '-summary.jsonl')).read_text().splitlines()]

def clean(value):
    if isinstance(value, dict):
        return {k: clean(v) for k, v in value.items() if k not in ('processId', 'monotonic')}
    if isinstance(value, list):
        return [clean(v) for v in value]
    return value

def cpu_series(name):
    trials = rows(name)
    aggregate = {}
    for arm in ('control', 'native'):
        group = [r for r in trials if r['arm'] == arm]
        cpu = sum(r['resources']['processCpuSeconds'] for r in group)
        wall = sum(r['resources']['processCpuObservationWallSeconds'] for r in group)
        count = sum(r['frames'] for r in group)
        aggregate[arm] = {'cpuPercentOneCore': 100 * cpu / wall, 'cpuMillisecondsPerFrame': 1000 * cpu / count,
                          'fps': count / sum(r['frames'] / r['fps'] for r in group)}
    return {'name': name, 'trials': clean(trials), 'aggregate': aggregate}

result = {
    'date': '2026-09-24',
    'scope': 'Isolated macOS x86_64 Unity 2017.2.2p2 on Apple M5; package-only overworld plus original constructed model-resource fixtures.',
    'binaries': {
        'cpuOriginalFramework': '7e16f411cf704dddb38d24b2547354a02ab2b6b869ac8f4f74a14d54ccad929e',
        'textureBeforeSharingFramework': 'c512c7de7b5e0ea238aceefc0269e00492940f79a9c17ea4287a842ccc2d3fa1',
        'finalFramework': 'aac00e1e03a1fb53ba9bfbd8b4017c2f82bfecfe03d4531a6f1906d2993a2223',
        'profiler': 'e4588426dc49b28dc686a052840b1afe64a37d59ee395feb00f358e507ae4382',
        'textureFixture': '5b7464944a67ef772c0c20efce2ee1d6831054f17685650489a44cd3888bf4d9',
        'finalNativeWaterTool': 'b15f624718cb1890cea4bf669b61dc3d3cc946e54443852415402eda08414295',
    },
    'method': {
        'screen': [1280, 720], 'qualityLevel': 4, 'vSyncCount': 0,
        'cpu': '120 warmup frames, empty sampler list, alternating native-water on/off. Boundary Process.TotalProcessorTime sums all threads. CPU% may exceed 100. Named focused series must use recorded focus fields, not their label.',
        'hostCounters': 'macOS proc_pid_rusage v2; user/system Mach ticks converted with mach_timebase_info 125/3 ns. Cross-checked against ps CPU time. Host interval includes warmup and command/output overhead, unlike profiler interval.',
        'memory': 'Unity allocated is an allocator snapshot, not process memory. Physical footprint includes compressed memory and differs from RSS. Do not sum overlapping counters. Per-object native-memory and Mono profiler counters returned zero; WorkingSet64 unavailable.',
        'texture': '20 original 1024x1024 RGBA PNG assignments. Readable control changes only LoadImage markNonReadable to false in the actual loader. Settled snapshot after >=3 seconds is taken before temporary GPU readback buffers. Explicit has no mipmaps; legacy retains native mipmap policy.',
        'sharing': 'Same20-part single-batch fixture before/after transaction-local sharing. Separate20-batch fixture verifies independent ownership, not the sharing speedup baseline.',
        'hostLoad': 'Other user-owned game instances and host workloads continued; focus and load changed. No broad FPS/stutter or uncontended CPU% claim.'
    },
    'cpuSeries': [cpu_series(n) for n in ('resources-60-a', 'resources-30-a', 'resources-30-focused', 'resources-60-focused', 'resources-final-60')],
    'textures': [],
    'clone': {name: read('resource-texture-after-shared-upload-' + name + '.json') for name in ('clone', 'survivor', 'cleared')},
    'textureQualityTradeoff': clean(rows('texture-quality')),
    'nativeSelftestFinal': read('native-water-resource-final-selftest.json'),
    'cleanup': json.loads((SCRATCH / 'resource-final-cleanup.json').read_text()),
    'limits': ['Constructed renderer fixtures are not a full avatar, all shaders, combat, co-op, or other-platform test.',
               'GPU readback matches tested image only; public materials remain private but file-backed model textures are now immutable and non-readable.',
               'Texture-quality override is experimental visual degradation, not shipped or counted as lossless RAM savings.',
               'UI preview/splash visual and logo-destruction live gates remain separate from model fixtures.']
}
for series in ('texture-before', 'texture-after'):
    for r in rows(series):
        r['settledUnityDeltaBytes'] = r['settled']['unityAllocated'] - r['creation']['allocatedBefore']
        result['textures'].append(clean(r))
result['ownedFixtureSourceSha256'] = {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
    for p in sorted(Path(__file__).with_name('owned-fixture-source').iterdir()) if p.is_file()}
OUT.write_text(json.dumps(result, indent=2) + '\n')
print(OUT.relative_to(REPO))
