"""Archive bounded live measurements; no game logs, assets or personal paths."""
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
GAME = ROOT / 'scratch' / 'perf-game'

def read(name):
    return json.loads((GAME / (name + '.json')).read_text())

live = read('resource-scroll-phase-live-final')
assert live['originalOffsetUnchanged']
for row in live['cases']:
    assert row['phaseEqual'] and row['materialEqual']
    assert row['ownedMaterialUnchanged'] == (row['mode'] != 'foreign-fallback')
foliage = read('resource-foliage-keywords-a')
assert foliage['outputsMatch'] and foliage['externalKeywordRepair']
foliage['commonOutput'] = foliage['rows'][0]['output']
for row in foliage['rows']:
    row['outputMatchesCommon'] = row.pop('output') == foliage['commonOutput']
    assert row['outputMatchesCommon']
result = {
    'frameworkSha256': '883065872d32b7ce717e37fd8a5eccaa9aa6d54c526eab2fbbff4eccea151c31',
    'fixtureSha256': 'fd855050492bf4ca4446d9352cf68c7568f6baa4fcd4f028e9622d2a8722674f',
    'gameAssemblySha256': '94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8',
    'hostTest': {'checks': 17, 'warmedCalls': 100000, 'reflectionAllocatedBytes': 4800000,
                 'byReferenceAllocatedBytes': 0,
                 'scope': 'Host CLR phase access and linked prefix with allocation-free Unity/resource stubs; actual Unity material array allocations remain.'},
    'live': live,
    'earlyFixture': {'result': read('resource-scroll-phase-live-a'),
                     'reason': 'Inactive hierarchy did not enter owned prefix. Matching native offsets did not establish patch acceptance; fixture corrected and rerun.'},
    'preHardeningFrameworkSha256': 'a7790258a9a7f9ba4de57d9ab13cd6bbdb46eba85fe9f34a9994a3d608da612a',
    'preHardeningLive': read('resource-scroll-phase-live-b'),
    'rejectedFoliage': {'result': foliage, 'reason': 'No consistent CPU benefit. No production keyword change.'},
    'waterCompatibility': [read('native-water-scroll-final-' + a) for a in ('selftest','on','inventory','off')],
    'limits': ['No measured total process RAM, CPU-utilization or FPS reduction.',
               'Live constructed material fixture verifies Harmony field injection and phase semantics, not full avatar appearance or animation.',
               'No new clone-lifecycle, co-op, other-platform or long-session coverage.']
}
Path(__file__).with_name('summary.json').write_text(json.dumps(result, indent=2) + '\n')
