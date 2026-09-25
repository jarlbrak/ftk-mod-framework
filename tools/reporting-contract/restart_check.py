#!/usr/bin/env python3
"""Synthetic restart-offer policy checks; no disk, locks, or game IO."""
import copy
import check


def offer(previous, *, lease, title_ready, pending=None):
    if lease != 'owned' or not title_ready:
        return None
    candidate = pending if pending is not None else previous
    if not candidate or not candidate.get('valid'):
        return None
    if candidate['shutdownObserved'] or candidate['disposition'] != 'pending':
        return None
    return candidate['sessionId']


def new_report(current, previous=None):
    value = check.draft()
    value['choices']['diagnostics'] = True
    check.request(value, 'automatic-metadata')
    sections = {'currentSession': check.section(payload=copy.deepcopy(current))}
    if previous is not None:
        sections['previousSession'] = check.section(payload=copy.deepcopy(previous))
    check.complete(value, 'automatic-metadata', sections)
    check.validate(value)
    return value


def main():
    prior = dict(valid=True, sessionId='prior', checkpointId='checkpoint-a',
                 shutdownObserved=False, disposition='pending', mods=['old-mod'])
    original = copy.deepcopy(prior)
    assert offer(prior, lease='owned', title_ready=True) == 'prior'
    for ownership in ('busy', 'unsupported', 'failed'):
        assert offer(prior, lease=ownership, title_ready=True) is None
    assert offer(prior, lease='owned', title_ready=False) is None
    for patch in ({'valid': False}, {'shutdownObserved': True},
                  {'disposition': 'dismissed'}, {'disposition': 'linked-report'}):
        candidate = dict(prior, **patch)
        assert offer(candidate, lease='owned', title_ready=True) is None
    assert offer(None, lease='owned', title_ready=True) is None
    # A later failed launch must not evict an already pending incident.
    later = dict(prior, sessionId='later')
    assert offer(later, lease='owned', title_ready=True, pending=prior) == 'prior'
    typing = check.draft()
    typing['choices']['diagnostics'] = True
    check.request(typing, 'initial')
    typing['narrative']['summary'] = 'Edited while collecting'
    check.mutate(typing, narrative_only=True)
    assert check.complete(typing, 'initial', {'context': check.section()})
    assert typing['narrative']['summary'] == 'Edited while collecting'
    assert typing['selectedCaptureId'] == 'initial'
    check.validate(typing)
    current = dict(sessionId='current', mods=['new-mod'])
    manual = new_report(current)
    restarted = new_report(current, prior)
    for value in (manual, restarted):
        assert value['choices'] == {'diagnostics': True, 'logs': False}
        assert value['selectedCaptureId'] == 'automatic-metadata'
        assert value['review'] is None and value['handoff'] is None
    sections = restarted['captures'][0]['sections']
    assert sections['previousSession']['payload']['mods'] == ['old-mod']
    assert sections['currentSession']['payload']['mods'] == ['new-mod']
    current['mods'].append('later-mutation')
    assert sections['currentSession']['payload']['mods'] == ['new-mod']
    assert prior == original
    print('PASS: automatic metadata, logs off, separate restart provenance, ownership/readiness/disposition policy')


if __name__ == '__main__':
    main()
