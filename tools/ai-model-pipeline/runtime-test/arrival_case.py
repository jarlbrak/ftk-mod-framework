#!/usr/bin/env python3
"""Once-only passive arrival evidence; no hero actions, retries, or art verdicts."""
import argparse
import json
import math
import re
import time
from pathlib import Path
from run_case import Runner, read, digest, living, validate_registration_freshness
from exercise_case import ready_slot, party_identity


def require(value, message):
    if not value:
        raise RuntimeError(message)


def terminal_capture(path, arm, session):
    require(path.is_file() and not any(p.is_symlink() for p in (path, *path.parents)), 'Capture must be an existing nonsymlink file')
    before = digest(path)
    raw = read(path)
    require(digest(path) == before, 'Capture changed during observation')
    require(raw.get('id') == arm['captureId'] and raw.get('session') == session, 'Capture ID/session mismatch')
    arrival = raw.get('arrival') or {}
    require(arrival.get('captureId') == arm['captureId'] and arrival.get('armCommandId') == arm['id'], 'Capture belongs to a different arm')
    for key in ('profile', 'catalogSha256', 'assets', 'pinnedReady', 'core', 'helper', 'content', 'gameAssembly'):
        require(arrival.get(key) == arm[key], 'Capture arm pin changed: ' + key)
    require(type(raw.get('ok')) is bool and isinstance(raw.get('frames'), list), 'Missing terminal capture outcome/frames')
    require(len(raw['frames']) <= 120, 'Capture exceeds requested sample bound')
    return {'rawCapturePath': str(path), 'rawCaptureSha256': before, 'rawCaptureOk': raw['ok'],
            'rawError': raw.get('error'), 'retainedFrames': len(raw['frames']),
            'terminal': True, 'status': 'terminal_capture_observed_no_acceptance'}


class Arrival(Runner):
    def __init__(self, args):
        super().__init__(args)
        require(self.session == args.session and self.profile_input_pin == args.profile_sha256, 'Explicit session/catalog pin mismatch')
        require(self.profile.get('bindingKind', 'explicit-plural') == 'explicit-plural', 'Arrival requires explicit plural binding; legacy registration is not accepted')
        matches = [r for r in self.profile['renderers'] if r.get('rendererPath') == args.renderer_path]
        require(len(matches) == 1, 'Exact unique configured renderer path required')
        self.source_pins = {str(p): digest(p) for p in (Path(__file__).resolve(), *(Path(__file__).resolve().parent / name for name in
                           ('run_case.py', 'exercise_case.py', 'record_case.py', 'profile_materials.py', 'story_setup.py', 'entry_setup.py')))}
        self.registration_pin = digest(self.root / 'model-test-registration.json')
        self.log('arrival-source-pins', {'files': self.source_pins, 'registrationSha256': self.registration_pin,
                 'boundary': 'Python source/on-disk inputs only; helper arm verifies actual native plural binding. No catalog decode acceptance is implied.'})

    def check_inputs(self):
        super().check_inputs()
        for path, sha in getattr(self, 'source_pins', {}).items():
            require(digest(Path(path)) == sha, 'Runner source changed during case')
        if hasattr(self, 'registration_pin'):
            require(digest(self.root / 'model-test-registration.json') == self.registration_pin, 'Registration changed during case')
            validate_registration_freshness(read(self.root / 'model-test-registration.json'), self.session_mtime,
                                            (self.root / 'model-test-profiles.json').stat().st_mtime, time.time(),
                                            self.content_registration_run)

    def ready_guard(self):
        self.check_inputs()
        state = self.state()
        party = party_identity(state)
        require(len(party) == 1, 'Exactly one living real hero required')
        if hasattr(self, 'party'):
            require(party == self.party, 'Party identity changed')
        else:
            self.party = party
        dungeon = state.get('dungeon') or {}
        require(dungeon.get('inDungeon') is True and dungeon.get('level') == self.a.level and dungeon.get('room') == self.a.room, 'Exact current dungeon indices required')
        fixture = self.helper('fixture-state')
        require((fixture.get('identity') or {}).get('root') == str(self.root) and fixture['identity'].get('session') == self.session, 'Fixture root/session mismatch')
        ready_slot(fixture, self.a.level, self.a.room)
        identity = dungeon.get('dungeonId')
        require(isinstance(identity, (str, int)) and not isinstance(identity, bool), 'Bridge dungeon identity unavailable')
        if hasattr(self, 'dungeon_id'):
            require(identity == self.dungeon_id, 'Bridge dungeon changed')
        else:
            self.dungeon_id = identity
        return fixture

    def wait_for_ready_guard(self):
        """Wait read-only for the exact native Ready slot before claiming the run.

        The native monkey suicide route can finish before ``run_case`` ever sees
        a hero turn. State reads observe that transition. Once the bridge reports
        the requested dungeon slot, a bounded ``fixture-state`` read verifies the
        strict Ready surface. No staging, Ready click, or other gameplay action is
        issued here.
        """
        deadline = time.monotonic() + self.a.ready_timeout
        next_fixture_read = 0.0
        while time.monotonic() < deadline:
            self.check_inputs()
            state = self.state()
            if any(isinstance(hero.get('hp'), (int, float)) and hero['hp'] <= 0
                   for hero in state.get('party') or []):
                raise RuntimeError('Party death while waiting for passive-arrival Ready')
            dungeon = state.get('dungeon') or {}
            now = time.monotonic()
            if (state.get('singlePlayer') is True and living(state)
                    and dungeon.get('inDungeon') is True
                    and dungeon.get('level') == self.a.level
                    and dungeon.get('room') == self.a.room
                    and now >= next_fixture_read):
                fixture = self.helper('fixture-state')
                try:
                    ready_slot(fixture, self.a.level, self.a.room)
                    self.log('arrival-ready-preflight', {
                        'level': self.a.level, 'room': self.a.room,
                        'boundary': 'Read-only strict Ready observation before permanent arrival claim.',
                    })
                    return fixture
                except RuntimeError as error:
                    self.log('arrival-ready-wait', {
                        'level': self.a.level, 'room': self.a.room,
                        'error': str(error), 'retry': 'read-only-state-only',
                    })
                    next_fixture_read = now + 5.0
            time.sleep(.5)
        raise TimeoutError('Native strict Ready slot not observed before passive-arrival deadline; no arrival claim or gameplay action issued')

    def wait_for_native_ready(self, level, room):
        """Observe the exact post-arrival Ready state without issuing another action."""
        deadline = time.monotonic() + self.a.post_ready_timeout
        next_fixture_read = 0.0
        while time.monotonic() < deadline:
            self.check_inputs()
            state = self.state()
            if any(isinstance(hero.get('hp'), (int, float)) and hero['hp'] <= 0
                   for hero in state.get('party') or []):
                raise RuntimeError('Party death while waiting for post-arrival Ready')
            dungeon = state.get('dungeon') or {}
            now = time.monotonic()
            if (state.get('singlePlayer') is True and living(state)
                    and dungeon.get('inDungeon') is True
                    and dungeon.get('level') == level and dungeon.get('room') == room
                    and now >= next_fixture_read):
                fixture = self.helper('fixture-state')
                ready = fixture.get('strictReady') or {}
                if ready.get('ok') is True and ready.get('level') == level and ready.get('room') == room:
                    self.log('arrival-post-ready', {
                        'level': level, 'room': room,
                        'boundary': 'Read-only strict Ready observation after the terminal arrival capture.',
                    })
                    return {
                        'strictReady': ready,
                        'dungeon': fixture.get('dungeon'),
                        'identity': fixture.get('identity'),
                    }
                next_fixture_read = now + 5.0
            time.sleep(.5)
        raise TimeoutError('Post-arrival strict Ready was not observed; no gameplay action retried')

    def run_arrival(self):
        out = {'status': 'stopped', 'terminal': False, 'session': self.session, 'journal': str(self.journal),
               'arm': None, 'ready': None, 'rawCapturePath': None, 'errors': [],
               'boundary': 'Passive arrival; nominal120 samples; no animation/art acceptance. Fortify999 is a test fixture.'}
        try:
            self.check_inputs()
            preflight = self.wait_for_ready_guard()
            out['readyPreflight'] = {
                'strictReady': preflight.get('strictReady'),
                'dungeon': preflight.get('dungeon'),
                'identity': preflight.get('identity'),
            }
            claim = self.root / 'model-test-output' / ('arrival-session-' + self.session + '.json')
            with claim.open('x') as stream:
                json.dump({'case': self.case, 'journal': str(self.journal), 'session': self.session}, stream)
            self.log('arrival-permanent-claim', {'path': str(claim), 'retry': False})
            self.ready_guard()
            self.helper('stage-next-enemy', {'enemy': self.a.enemy, 'level': self.a.level, 'room': self.a.room})
            self.ready_guard()
            self.helper('fortify-party', {'targetMaxHp': 999, 'allowReady': True})
            self.ready_guard()
            arm = self.helper('enemy-arrival-arm', {'enemy': self.a.enemy, 'catalogSha256': self.a.profile_sha256,
                                                   'rendererPath': self.a.renderer_path})
            out['arm'] = arm
            require(arm.get('status') == 'enemy-arrival-armed' and isinstance(arm.get('captureId'), str)
                    and re.fullmatch('[a-f0-9]{32}', arm['captureId']) and arm['captureId'] != arm['id'], 'Invalid arrival arm response')
            require(arm.get('profile') == self.profile and arm.get('catalogSha256') == self.a.profile_sha256, 'Arm profile mismatch')
            pinned = arm.get('pinnedReady') or {}
            require(type(pinned.get('dungeonId')) is int and pinned['dungeonId'] != 0 and pinned.get('level') == self.a.level
                    and pinned.get('room') == self.a.room and pinned.get('queuedEnemy') == self.a.enemy, 'Arm Ready identity mismatch')
            capture = self.root / 'model-test-output' / (arm['captureId'] + '.json')
            out['rawCapturePath'] = str(capture)
            require(not capture.exists(), 'Predetermined capture already exists before Ready')
            self.ready_guard()
            try:
                out['ready'] = self.helper('ready')
            except Exception as error:
                out['errors'].append('Ready not confirmed: ' + str(error))
                self.log('arrival-ready-uncertain', {'error': str(error), 'retry': False, 'captureId': arm['captureId']})
            # No mutating helper command is issued after Ready. Gather the pinned
            # capture first; an optional later fixture-state poll is read-only.
            deadline = time.monotonic() + self.a.capture_timeout
            while True:
                self.check_inputs()
                if capture.exists():
                    out.update(terminal_capture(capture, arm, self.session))
                    self.log('arrival-terminal-capture', out.copy())
                    if self.a.post_ready_level is not None:
                        try:
                            out['postArrivalReady'] = self.wait_for_native_ready(
                                self.a.post_ready_level, self.a.post_ready_room)
                        except Exception as error:
                            out['postArrivalReadyError'] = str(error)
                            out['status'] = 'terminal_capture_observed_post_ready_pending'
                    break
                if time.monotonic() >= deadline:
                    out['status'] = 'observation_timeout_capture_pending'
                    self.log('arrival-observation-timeout', {'path': str(capture), 'terminal': False, 'retry': False})
                    break
                time.sleep(.5)
        except Exception as error:
            out['errors'].append(str(error))
            self.log('arrival-stopped', {'error': str(error), 'retry': False, 'rawCapturePath': out['rawCapturePath']})
        result = self.output / 'arrival-case-result.json'
        with result.open('x') as stream:
            json.dump(out, stream, indent=2)
            stream.write('\n')
        return result, out


def arguments(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root', type=Path, required=True)
    p.add_argument('--port', type=int, required=True)
    p.add_argument('--session', required=True)
    p.add_argument('--enemy', required=True)
    p.add_argument('--renderer-path', required=True)
    p.add_argument('--profile-sha256', required=True, help='SHA256 of entire model-test-profiles.json')
    p.add_argument('--level', type=int, required=True)
    p.add_argument('--room', type=int, required=True)
    p.add_argument('--capture-timeout', type=float, default=360)
    p.add_argument('--ready-timeout', type=float, default=420)
    p.add_argument('--post-ready-level', type=int)
    p.add_argument('--post-ready-room', type=int)
    p.add_argument('--post-ready-timeout', type=float, default=300)
    a = p.parse_args(argv)
    if not a.root.is_absolute() or not 1 <= a.port <= 65535 or a.level < 0 or a.room < 0:
        p.error('Absolute root, valid port and nonnegative indices required')
    if not re.fullmatch('[a-f0-9]{32}', a.session) or not re.fullmatch('[a-f0-9]{64}', a.profile_sha256):
        p.error('Exact lowercase session/catalog SHA256 required')
    if not math.isfinite(a.capture_timeout) or not 1 <= a.capture_timeout <= 1800:
        p.error('Capture timeout must be finite1..1800 seconds')
    if not math.isfinite(a.ready_timeout) or not 1 <= a.ready_timeout <= 1800:
        p.error('Ready timeout must be finite1..1800 seconds')
    if (a.post_ready_level is None) != (a.post_ready_room is None):
        p.error('Post-ready level and room must be supplied together')
    if a.post_ready_level is not None and (a.post_ready_level < 0 or a.post_ready_room < 0):
        p.error('Post-ready level and room must be nonnegative')
    if not math.isfinite(a.post_ready_timeout) or not 1 <= a.post_ready_timeout <= 1800:
        p.error('Post-ready timeout must be finite1..1800 seconds')
    a.class_key = None
    a.mode = 'passive-arrival'
    a.operation_timeout = 40
    a.wait_timeout = 120
    return a


def main():
    result, out = Arrival(arguments()).run_arrival()
    print(result)
    return 0 if out.get('status') == 'terminal_capture_observed_no_acceptance' else 2


if __name__ == '__main__':
    raise SystemExit(main())
