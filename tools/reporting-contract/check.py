#!/usr/bin/env python3
"""Deterministic synthetic A2 examples and focused contract assertions, no game IO."""
import argparse
import copy
import json
from pathlib import Path

TIME = '2026-09-23T12:00:00Z'
REASONS = {
    'complete': {'none'},
    'partial': {'source_error', 'scene_changed', 'limit_reached', 'transition_in_progress', 'deadline_exceeded'},
    'unavailable': {'source_absent', 'not_initialized', 'unsupported', 'transition_in_progress', 'runtime_faulted'},
    'omitted': {'user_declined', 'policy_excluded'},
    'cancelled': {'user_cancelled', 'superseded'},
    'failed': {'source_error', 'deadline_exceeded'},
}


def section(status='complete', reason='none', payload=None):
    return dict(status=status, reason=reason, observedAt=TIME if status == 'complete' or payload is not None else None,
                payload=payload, retainedCount=None, totalCount=None)


def draft():
    return dict(schemaVersion=1, reportId='synthetic-report-a', revision=1, collectionGeneration=0, createdAt=TIME,
                narrative={'summary': 'Synthetic menu issue', 'actual': 'Focus changed',
                           'expected': 'Focus retained', 'repro': 'Open, cancel, reopen'},
                suspicion={'area': 'not_sure', 'modId': None},
                entryContext=section(payload={'phase': 'title', 'role': None, 'adventure': None}),
                selectedCaptureId=None, captures=[], choices={'diagnostics': False, 'logs': False},
                review=None, handoff=None)


def mutate(d, narrative_only=False):
    d['revision'] += 1
    d['review'] = None
    if narrative_only:
        return
    d['collectionGeneration'] += 1
    for c in d['captures']:
        if c['completedAt'] is None:
            c['completedAt'] = TIME
            c['status'] = 'cancelled'
            c['reason'] = 'superseded'


def request(d, capture_id):
    assert not any(c['completedAt'] is None for c in d['captures'])
    assert capture_id not in [c['captureId'] for c in d['captures']]
    mutate(d)
    d['captures'].append(dict(captureId=capture_id, reportId=d['reportId'],
                             requestRevision=d['revision'], collectionGeneration=d['collectionGeneration'], requestedAt=TIME,
                             startedAt=TIME, completedAt=None, status=None, reason=None, sections={}))


def complete(d, capture_id, sections, status='complete', reason='none'):
    c = next(c for c in d['captures'] if c['captureId'] == capture_id)
    if c['completedAt'] is not None or c['collectionGeneration'] != d['collectionGeneration']:
        return False
    c['completedAt'] = TIME
    c['sections'] = sections
    c['status'], c['reason'] = status, reason
    mutate(d)
    if status in ('complete', 'partial'):
        d['selectedCaptureId'] = capture_id
    else:
        d['choices'] = {'diagnostics': False, 'logs': False}
    return True


def approve(d):
    assert all(c['completedAt'] is not None for c in d['captures'])
    d['review'] = dict(reportId=d['reportId'], revision=d['revision'],
                       captureId=d['selectedCaptureId'] if d['choices']['diagnostics'] else None,
                       choices=copy.deepcopy(d['choices']))


def validate(d):
    assert d['schemaVersion'] == 1 and d['revision'] > 0
    assert d['handoff'] in (None, 'requested', 'failed')
    ids = [c['captureId'] for c in d['captures']]
    assert len(ids) == len(set(ids))
    assert d['selectedCaptureId'] is None or d['selectedCaptureId'] in ids
    assert not d['choices']['logs'] or d['choices']['diagnostics']
    for c in d['captures']:
        assert c['reportId'] == d['reportId']
        if c['completedAt'] is not None:
            assert c['reason'] in REASONS[c['status']]
        if c['captureId'] == d['selectedCaptureId']:
            assert c['status'] in ('complete', 'partial')
        for s in c['sections'].values():
            assert s['reason'] in REASONS[s['status']]
            assert s['totalCount'] is None or s['totalCount'] >= 0
            if s['retainedCount'] is not None and s['totalCount'] is not None:
                assert 0 <= s['retainedCount'] <= s['totalCount']
    if d['review']:
        assert all(c['completedAt'] is not None for c in d['captures'])
        assert d['review'] == dict(reportId=d['reportId'], revision=d['revision'],
                                  captureId=d['selectedCaptureId'] if d['choices']['diagnostics'] else None,
                                  choices=d['choices'])


def examples():
    cases = {'text-only-title': draft()}
    d = draft()
    d['choices']['diagnostics'] = True
    request(d, 'synthetic-capture-1')
    complete(d, 'synthetic-capture-1', {
        'context': section(payload={'phase': 'in-session', 'role': None, 'adventure': None}),
        'inventory': section(payload=[{'guid': 'synthetic.mod', 'enabled': True,
                                      'pendingEnabled': False, 'outcome': 'unknown'}]),
        'active': section(payload={'generation': 'synthetic-generation-a'}),
        'pending': section(payload={'generation': 'synthetic-generation-b'}),
        'logs': section('omitted', 'user_declined'),
    })
    approve(d)
    cases['complete-changed-context-pending-not-loaded'] = copy.deepcopy(d)
    p = copy.deepcopy(d)
    p['captures'][0]['sections']['loader'] = section('partial', 'source_error',
                                                   {'cachedRows': 2, 'parsedEntries': 3})
    p['captures'][0]['sections']['plugins'] = section('unavailable', 'not_initialized')
    p['captures'][0]['status'], p['captures'][0]['reason'] = 'partial', 'source_error'
    mutate(p)
    cases['partial-loader-absent-source'] = p
    request(d, 'synthetic-capture-2')
    complete(d, 'synthetic-capture-2', {'context': section('failed', 'source_error')}, 'failed', 'source_error')
    cases['failed-recapture-previous-not-shared'] = copy.deepcopy(d)
    d['choices']['diagnostics'] = True  # Explicit user selection of the labelled previous capture.
    mutate(d)
    approve(d)
    cases['explicit-previous-reuse-reviewed'] = copy.deepcopy(d)
    return cases


def stress():
    # Each scalar is exactly 256 UTF-8 bytes; escapes increase serialized size.
    scalar = 'é"\\' * 64
    assert len(scalar.encode('utf-8')) == 256
    groups = {name: [dict(id=f'synthetic-{i:03}', name=scalar, version=scalar)
                     for i in range(256)]
              for name in ('discovery', 'active', 'pending', 'plugins')}
    lengths = [262] * 499 + [334]
    logs = ''.join('x' * (n - 1) + '\n' for n in lengths)
    assert len(logs.encode('utf-8')) == 128 * 1024 and len(logs.splitlines()) == 500
    return dict(inventory=groups, logs=logs)


def rejected(value):
    try:
        validate(value)
    except AssertionError:
        return
    raise AssertionError('Invalid fixture was accepted')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', type=Path)
    args = parser.parse_args()
    cases = examples()
    for value in cases.values():
        validate(value)
    good = cases['complete-changed-context-pending-not-loaded']
    stale = copy.deepcopy(good)
    stale['revision'] += 1
    rejected(stale)
    mixed = copy.deepcopy(good)
    mixed['captures'][0]['reportId'] = 'different-report'
    rejected(mixed)
    remote = copy.deepcopy(good)
    remote['handoff'] = 'submitted'
    rejected(remote)
    late = draft()
    request(late, 'late')
    mutate(late)  # Collection-affecting changes invalidate the attempt generation.
    before = copy.deepcopy(late)
    assert not complete(late, 'late', {'context': section()})
    assert late == before
    approve(late)  # An invalidated worker must not prevent text-only recovery.
    request(late, 'replacement')
    assert complete(late, 'replacement', {'context': section()})
    validate(late)
    bad_selection = copy.deepcopy(cases['failed-recapture-previous-not-shared'])
    bad_selection['selectedCaptureId'] = 'synthetic-capture-2'
    rejected(bad_selection)
    cases['maximum-input'] = stress()
    if args.write:
        args.write.mkdir(parents=True, exist_ok=True)
        for name, value in cases.items():
            target = args.write / (name + '.json')
            with target.open('x', encoding='utf-8') as output:
                json.dump(value, output, ensure_ascii=False, sort_keys=True, indent=2)
                output.write('\n')
    print('PASS: 5 semantic fixtures, maximum-input dimensions, 4 invalid cases, late completion and recovery')


if __name__ == '__main__':
    main()
