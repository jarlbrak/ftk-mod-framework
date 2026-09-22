#!/usr/bin/env python3
"""Verify hot reload against an already running protected scratch game. Never launch or deploy."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import uuid

REPO = Path(__file__).resolve().parents[3]
ZERO = ('ownedObjects', 'icons', 'paths', 'guardianClasses', 'guardianEquipment',
        'itemModels', 'displayModels', 'apparelModels')
BASE = dict(ownedObjects=77, icons=37, paths=135, guardianClasses=1,
            guardianEquipment=8, itemModels=24, displayModels=36, apparelModels=12)


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


class Runner:
    def __init__(self, args):
        self.args = args
        self.root = args.root.resolve(strict=True)
        require(args.root.is_absolute() and args.root.absolute() == self.root,
                '--root must be absolute and contain no symlinks')
        require(self.root == REPO / 'scratch' / 'hot-reload-game',
                'Only this checkout/scratch/hot-reload-game is admitted')
        require(args.helper.resolve(strict=True) == self.root / 'BepInEx/ftkmf/ftkmf-launcher-helper',
                '--helper must be the helper installed in this protected copy')
        require(args.game_assembly.resolve(strict=True).is_relative_to(self.root),
                '--game-assembly must belong to this protected copy')
        self.session = args.session
        self.guard()
        require(not args.output.exists(), '--output must be a new dedicated evidence directory')
        args.output.mkdir(parents=True)
        self.output = args.output.resolve()
        self.lock = self.root / 'hot-reload-runner.lock'
        self.last_command = None
        self.pid = None
        self.enabled_identity = None

    def guard(self):
        session = json.loads((self.root / 'model-test-session.json').read_text())
        profile = json.loads((self.root / 'hot-reload-profile.json').read_text())
        owner = json.loads((self.root / 'owned-process.json').read_text())
        require(session['session'] == self.session, 'Session changed; refusing further commands')
        require(profile['bundleIdentifier'] == 'com.ftkmf.hotreload.test' and
                profile['sentinelValue'] == self.session and profile['savePath'] == session['savePath'],
                'Protected profile/session mismatch')
        require(Path(owner['root']).resolve() == self.root, 'Owned process root mismatch')
        return owner['pid']

    def log(self, name, value):
        with (self.output / (name + '.jsonl')).open('a') as stream:
            stream.write(json.dumps(value, sort_keys=True) + '\n')

    def idle_transport(self):
        command = self.root / 'model-test-command.json'
        if command.exists():
            previous = json.loads(command.read_text())
            require(previous.get('session') == self.session, 'Previous command belongs to another session')
            require((self.root / 'model-test-output' / (previous['id'] + '.json')).exists(),
                    'Previous command is unresolved; do not overwrite or retry it')
            if self.last_command is not None:
                require(previous['id'] == self.last_command, 'Another command producer used this game session')

    def request(self, **payload):
        owner_pid = self.guard()
        self.idle_transport()
        key = uuid.uuid4().hex
        request = dict(payload, op='hot-reload', id=key, session=self.session)
        temporary = self.root / ('hot-reload-' + key + '.tmp')
        temporary.write_text(json.dumps(request))
        temporary.replace(self.root / 'model-test-command.json')
        self.last_command = key
        result_path = self.root / 'model-test-output' / (key + '.json')
        deadline = time.monotonic() + self.args.timeout
        while not result_path.exists():
            self.guard()
            current = json.loads((self.root / 'model-test-command.json').read_text())
            require(current['id'] == key, 'Concurrent command replaced the active request')
            require(time.monotonic() < deadline,
                    'Timed out; request may still be running. Inspect ' + str(result_path))
            time.sleep(.1)
        result = json.loads(result_path.read_text())
        self.log('requests', dict(request=request, result=result))
        require(result.get('session') == self.session and result.get('id') == key, 'Result identity mismatch')
        require(result.get('pid') == owner_pid, 'Result process differs from owned process')
        if self.pid is None:
            self.pid = owner_pid
        require(owner_pid == self.pid, 'Game PID changed')
        require(not result.get('faulted'), 'Coordinator faulted: ' + str(result.get('notice')))
        return result

    def settled(self):
        deadline = time.monotonic() + self.args.timeout
        while True:
            result = self.request(action='status')
            if not result.get('busy'):
                require(result.get('blockedReason') is None, 'Activation boundary closed: ' + str(result))
                return result
            require(time.monotonic() < deadline, 'Activation failed to settle')
            time.sleep(.2)

    def prepare(self, selection):
        self.guard()
        self.idle_transport()
        command = [sys.executable, str(Path(__file__).with_name('hot_reload_fixtures.py')), 'prepare',
                   '--fixtures', str(self.args.fixtures), '--helper', str(self.args.helper),
                   '--game-assembly', str(self.args.game_assembly),
                   '--state-root', str(self.root / 'BepInEx/ftkmf/marketplace'), '--selection', selection]
        process = subprocess.run(command, capture_output=True, text=True, timeout=150)
        self.log('prepare', dict(selection=selection, exitCode=process.returncode,
                                 stdout=process.stdout, stderr=process.stderr))
        require(process.returncode == 0, 'Preparation failed: ' + process.stdout + process.stderr)
        return json.loads(process.stdout)

    def apply(self, selection, reject=False):
        before = self.settled()
        target = self.prepare(selection)
        started = time.monotonic()
        accepted = self.request(action='apply')
        require(accepted.get('ok'), 'Apply request rejected: ' + str(accepted))
        after = self.settled()
        self.log('transitions', dict(selection=selection, before=before, target=target,
                                     accepted=accepted, after=after, elapsedSeconds=time.monotonic()-started))
        require(after['epoch'] == before['epoch'] + (0 if reject else 1), 'Unexpected epoch change')
        if reject:
            require(after['active'] == before['active'] and after['identity'] == before['identity'],
                    'Failed transaction changed active generation or identities')
        else:
            require(after['active'] == target['pending'], 'Activated generation differs from prepared candidate')
        require(all(after[key] == 0 for key in ('pendingDestroy', 'rendererLeases', 'rendererResources')),
                'Retired resources or renderer leases remain')
        return after

    def audit(self, enabled, cycle, update=False):
        result = self.request(action='audit')
        require(result.get('ok') and not result.get('busy'), 'Audit not at a settled boundary')
        require(len(result['ids']) == (64 if enabled else 0), 'Unexpected registered identity count')
        tables = result.get('nativeTables', {})
        require(len(tables) == 5, 'All five native table diagnostics required')
        require(all(not table['indexNull'] and table['indexCount'] == table['rows'] and
                    table['unresolvedRows'] == 0 for table in tables.values()),
                'Native row lookup/index mismatch, including vanilla definitions')
        require(result['proficiencyInstances'] == self.baseline['proficiencyInstances'] + (3 if enabled else 0),
                'Proficiency instance count drift')
        require(result['proficiencyChildren'] == self.baseline['proficiencyChildren'] + (3 if enabled else 0),
                'Proficiency child count drift')
        if enabled and not update:
            require(all(result[key] == count for key, count in BASE.items()), 'Base resource counts drifted')
            require(result['noviceHammerDamage'] == 10, 'Base weapon stat mismatch')
            if self.enabled_identity is None:
                self.enabled_identity = result['identity']
            require(result['identity'] == self.enabled_identity, 'History changed final enabled identities')
        elif enabled:
            require(result['noviceHammerDamage'] == 11 and result['guardianEquipment'] == 7,
                    'Update content was not replaced')
            require(result['identity'] == self.enabled_identity, 'Compatible update changed IDs')
        else:
            require(all(result[key] == 0 for key in ZERO), 'Disabled definitions/resources survived')
            require(result['identity'] == self.baseline['identity'], 'Disabled identity differs from baseline')
        rss = subprocess.check_output(['ps', '-p', str(self.pid), '-o', 'rss='], text=True).strip()
        result.update(cycle=cycle, selection='update' if update else ('enabled' if enabled else 'disabled'),
                      rssKiB=int(rss))
        self.log('audits', result)
        return result

    def run(self):
        descriptor = os.open(self.lock, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        try:
            os.write(descriptor, json.dumps(dict(pid=os.getpid(), session=self.session)).encode())
            self.idle_transport()
            self.settled()
            self.baseline = self.request(action='audit')
            require(len(self.baseline['ids']) == 0, 'Start from empty/disabled runtime; runner does not normalize it')
            require(all(self.baseline[key] == 0 for key in ZERO), 'Initial disabled resources are not empty')
            self.log('baseline', self.baseline)
            for cycle in range(1, self.args.cycles + 1):
                self.apply('enabled'); self.audit(True, cycle)
                self.apply('disabled'); self.audit(False, cycle)
                print('PASS cycle', cycle, 'PID', self.pid, flush=True)
            if self.args.transactions:
                for point in ('after-reset', 'after-load', 'after-cache', 'before-commit'):
                    injected = self.request(action='inject', point=point)
                    require(injected.get('ok'), 'Failure injection rejected')
                    self.apply('enabled', reject=True); self.audit(False, point)
                self.apply('enabled'); self.audit(True, 'install')
                self.apply('update'); self.audit(True, 'update', update=True)
                self.apply('bad', reject=True); self.audit(True, 'bad-rollback', update=True)
                self.apply('removed'); self.audit(False, 'remove')
                self.apply('enabled'); self.audit(True, 'reinstall')
                self.apply('disabled'); self.audit(False, 'final')
            (self.output / 'summary.json').write_text(json.dumps(dict(ok=True, pid=self.pid, session=self.session,
                cycles=self.args.cycles, transactions=self.args.transactions,
                limitation='Memory samples are evidence, not a proven leak bound; no fresh-adventure test performed.'), indent=2)+'\n')
        finally:
            os.close(descriptor)
            self.lock.unlink()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('root', 'fixtures', 'helper', 'game-assembly', 'output'):
        parser.add_argument('--'+name, required=True, type=Path)
    parser.add_argument('--session', required=True, help='Exact current model-test-session.json session token')
    parser.add_argument('--cycles', type=int, required=True)
    parser.add_argument('--transactions', action='store_true', help='Also verify updates, removal and rollback injections')
    parser.add_argument('--timeout', type=float, default=45)
    args = parser.parse_args()
    if args.cycles < 1 or args.cycles > 1000 or args.timeout <= 0:
        parser.error('cycles must be 1..1000 and timeout positive')
    Runner(args).run()


if __name__ == '__main__':
    main()
