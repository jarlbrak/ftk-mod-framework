#!/usr/bin/env python3
"""Verify one enabled native scroller from immutable end-of-frame observations."""
import argparse
import gzip
import hashlib
import json
import math
from pathlib import Path
import struct


def require(value, message):
    if not value:
        raise ValueError(message)


def f32(value):
    require(type(value) in (int, float) and math.isfinite(value), 'Finite numeric sample required')
    return struct.unpack('<f', struct.pack('<f', value))[0]


def verify(raw):
    require(raw.get('ok') is True, 'Successful raw capture required')
    frames = raw.get('frames', [])
    require(len(frames) >= 3, 'At least three timed frames required')
    first = frames[0]['materialObservation']
    require(len(first['scrollers']) == 1, 'Exactly one scroller required by this verifier')
    scroller = first['scrollers'][0]
    require(scroller['textureName'] == '_MainTex', 'Only observed _MainTex supported')
    rate = scroller['rate']
    require(len(rate) == 2 and any(f32(v) != 0 for v in rate), 'Nonzero two-axis rate required')
    slots = first['slots']
    require(2 <= len(slots) <= 4, 'Explicit multi-slot observation required')
    require([s['slot'] for s in slots] == list(range(len(slots))), 'Contiguous ordered slots required')
    material_ids = [s['instanceId'] for s in slots]
    require(len(set(material_ids)) == len(slots), 'Distinct material objects required')
    target = scroller['materialIndex']
    require(type(target) is int and 0 <= target < len(slots), 'Valid target slot required')
    geometry = first['geometry']
    require(geometry['submeshCount'] == len(slots), 'Actual submesh count must match slots')
    require([s['nativeMaterialSlot'] for s in geometry['submeshes']] == list(range(len(slots))), 'Actual submesh mapping required')
    require(all(s['indices'] > 0 and s['indices'] % 3 == 0 for s in geometry['submeshes']), 'Nonempty triangle surfaces required')
    pinned = ('ownerInstanceId', 'celInstanceId', 'rendererInstanceId', 'enemy')
    previous = None
    for index, frame in enumerate(frames):
        obs = frame['materialObservation']
        require(all(obs[k] == first[k] for k in pinned), 'Owner/renderer identity changed')
        require(obs['samplePhase'] == 'end-of-frame-before-png-readback', 'Unexpected sample phase')
        require(obs['rendererEnabled'] is True and obs['rendererActive'] is True, 'Enabled active renderer required; disabled behavior is a separate fixture')
        require(len(obs['scrollers']) == 1, 'Scroller count changed')
        current = obs['scrollers'][0]
        require(all(current[k] == scroller[k] for k in ('instanceId', 'materialIndex', 'textureName', 'rate', 'rendererInstanceId')), 'Scroller identity/settings changed')
        require(current['enabled'] is True and current['active'] is True and current['propertySupported'] is True, 'Active supported scroller required')
        require(current['rendererInstanceId'] == obs['rendererInstanceId'] and current['targetMaterialInstanceId'] == material_ids[target], 'Scroller target mismatch')
        require([s['instanceId'] for s in obs['slots']] == material_ids, 'Material identity changed')
        require(obs['lease']['leaseId'] == first['lease']['leaseId'] and obs['lease']['acquired'] is True and obs['lease']['applied'] is True, 'Current applied lease changed')
        for slot, original in zip(obs['slots'], slots):
            require(slot['slot'] == original['slot'], 'Slot order changed')
            require(slot['inCurrentLeaseResources'] is True and slot['instanceId'] in obs['leaseResourceInstanceIds'], 'Material not owned by observed lease')
            require(slot['_MainTex'] == original['_MainTex'], 'Texture identity/metadata changed')
            texture = slot['_MainTex']
            require(texture is not None and texture['inCurrentLeaseResources'] is True and texture['instanceId'] in obs['leaseResourceInstanceIds'], 'Configured PNG not owned by observed lease')
            require(slot['_MainTexScale'] == original['_MainTexScale'], 'Texture scale changed')
            if slot['slot'] != target:
                require(slot['_MainTexOffset'] == original['_MainTexOffset'], 'Unscrolled slot moved')
        require(obs['slots'][target]['_MainTexOffset'] == current['phase'] == current['currentPropertyOffset'], 'Final offset differs from observed native phase')
        phase = current['phase']
        require(len(phase) == 2 and all(f32(v) == v for v in phase), 'Finite float32 phase required')
        if previous is not None:
            require(obs['frame'] > previous['frame'] and obs['gameTime'] > previous['gameTime'], 'Timed samples must progress')
            require(obs['deltaTime'] > 0, 'Positive deltaTime required')
            expected = [f32(previous['scrollers'][0]['phase'][i] + f32(f32(rate[i]) * f32(obs['deltaTime']))) for i in range(2)]
            require(phase == expected, 'Native float32 phase recurrence mismatch at sample ' + str(index))
        previous = obs
    require(first['scrollers'][0]['phase'] != previous['scrollers'][0]['phase'], 'No observed scroll displacement')
    return {'status': 'single_enabled_scroller_observed', 'frames': len(frames), 'materialIds': material_ids,
            'leaseId': first['lease']['leaseId'], 'targetSlot': target, 'rate': rate,
            'firstPhase': scroller['phase'], 'lastPhase': previous['scrollers'][0]['phase'],
            'maximumFloat32RecurrenceError': 0,
            'limits': ['Current owner membership only; no clone independence or final disposal proof.',
                       'Exactly one enabled _MainTex scroller with owned textures; other layouts need another verifier.',
                       'Numeric metadata does not establish appearance, visible death, or finished model quality.']}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    data = args.capture.read_bytes()
    if args.capture.suffix == '.gz':
        data = gzip.decompress(data)
    result = verify(json.loads(data))
    result['rawCaptureSha256'] = hashlib.sha256(data).hexdigest()
    args.output.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
