"""Generate bounded portrait allocation evidence from retained isolated captures."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCRATCH = ROOT / 'scratch'


def capture(label):
    source = json.loads((SCRATCH / (label + '-summary.json')).read_text())
    rows = []
    for r in source:
        row = {k: r[k] for k in ('id', 'enemy', 'count', 'milliseconds', 'unityAllocated',
                                 'aliasesMatch', 'sharedUnknown', 'gpuSha256', 'widgetAlive') if k in r}
        row['liveObservedTextures'] = sum(not t['destroyed'] for t in r.get('newTextures', []))
        rows.append(row)
    return rows


result = {
    'scope': 'Constructed widget invoking native Initialize in isolated macOS x86_64 Unity 2017.2.2p2, not an end-to-end menu benchmark.',
    'fixtureSha256': 'de445650145eaddf8bbbd093e05438b24d301e9f1a3e5f850684a4140208cf1e',
    'gameAssemblySha256': '94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8',
    'baselineFrameworkSha256': 'aac00e1e03a1fb53ba9bfbd8b4017c2f82bfecfe03d4531a6f1906d2993a2223',
    'baseline': capture('portrait-baseline-a'),
    'rejectedReuse': {'reason': 'Per-initialization reference census caused severe latency regression.',
                      'frameworkSha256': '9af7fa6b37f7c062036c5bf154e17e83387347dc6f0c374366a4f68b0200be28',
                      'captures': capture('portrait-candidate-a')},
    'rejectedDeferred': {'reason': 'Batched census still added a 24 to 40 ms synchronous cleanup hitch.',
                         'frameworkSha256': '8b95b94dfb1670459a09ae8ad44db547712514ec2a5d113999620b5c9b5af4f4',
                         'captures': capture('portrait-deferred-a'),
                         'drainMeasurements': [json.loads(p.read_text()) for p in sorted(SCRATCH.glob('portrait-deferred-a-*-drain.json'))]},
    'candidateFrameworkSha256': 'b307c20a76bec2d0cacabbd9f17c167e3f722f142e39ac9ee6bdbe15801c75c8',
    'prematureMenuFixture': {'reason': 'Ran before overworld setup completed; known-enemy image differs with scene lighting. Excluded from matched claims.', 'captures': capture('portrait-unused-a')},
    'duringSetupFixture': {'reason': 'Overworld loaded but camera setup still running; retained as an unmatched capture.', 'captures': capture('portrait-unused-b')},
    'afterPrematureSceneCapture': capture('portrait-unused-c'),
    'candidate': capture('portrait-unused-final'),
    'sameProcessNativeControl': capture('portrait-control-final'),
    'candidatePlayerTransition': capture('portrait-final-player'),
    'nativeWaterCompatibility': [json.loads((SCRATCH / 'perf-game' / ('native-water-portrait-final-water-' + action + '.json')).read_text()) for action in ('selftest', 'on', 'inventory', 'off')],
    'limits': ['Baseline records exact constructor allocations; candidate observes new 328x280 ARGB32 textures and displayed images.',
               'Unity allocated bytes include unrelated engine activity; they are not process RSS or graphics-driver memory.',
               'GPU readback verifies tested image pixels, not complete UI composition, every enemy, Deimos, or co-op.',
               'Known-enemy texture retention remains native; no general leak reclamation or total CPU/FPS claim.']
}
candidate = result['candidate']
control = result['sameProcessNativeControl']
assert candidate[2]['liveObservedTextures'] == 0
assert control[2]['liveObservedTextures'] == 50
for index in (2, 4, 6, 8):
    assert candidate[index]['gpuSha256'] == control[index]['gpuSha256']
    assert candidate[index]['aliasesMatch'] and control[index]['aliasesMatch']
assert candidate[-1]['liveObservedTextures'] == 21
assert control[-1]['liveObservedTextures'] == 72
result['unknownFiftySettledDeltaMiB'] = {
    'candidate': (candidate[2]['unityAllocated'] - candidate[0]['unityAllocated']) / 2**20,
    'control': (control[2]['unityAllocated'] - control[0]['unityAllocated']) / 2**20
}
Path(__file__).with_name('summary.json').write_text(json.dumps(result, indent=2) + '\n')
