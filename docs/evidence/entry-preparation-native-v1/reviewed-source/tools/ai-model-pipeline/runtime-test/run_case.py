#!/usr/bin/env python3
"""Prepare one native model test encounter in an already owned isolated FTK game.

No launch, deployment, combat actions, retries of uncertain actions, or art verdicts.
"""
import argparse
import hashlib
import json
import re
import time
import uuid
import urllib.request
import urllib.error
from datetime import datetime, timezone
from pathlib import Path
import story_setup
import entry_setup
import profile_materials


def read(path):
    return json.loads(path.read_text())


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


DEPLOYED_BINARIES = {'framework':'BepInEx/plugins/FTKModFramework.dll',
                     'helper':'BepInEx/plugins/FtkRuntimeModelTest.dll'}


def measured_binaries(root):
    result={}
    for name,relative in DEPLOYED_BINARIES.items():
        path=root/relative
        if any(p.is_symlink() for p in (path,*path.parents)) or not path.is_file():
            raise RuntimeError('Missing or symlink deployed binary: '+relative)
        before=path.stat()
        if not 0<before.st_size<=64*1024*1024:raise RuntimeError('Invalid deployed binary size: '+relative)
        sha=digest(path);after=path.stat()
        if (before.st_dev,before.st_ino,before.st_size,before.st_mtime_ns)!=(after.st_dev,after.st_ino,after.st_size,after.st_mtime_ns):
            raise RuntimeError('Deployed binary changed during measurement: '+relative)
        result[name]={'path':str(path),'relativePath':relative,'sha256':sha,'bytes':after.st_size}
    return result


def validate_registration_freshness(report, session_mtime, profile_mtime, now):
    updated = datetime.fromisoformat(report['updatedUtc'].replace('Z', '+00:00'))
    if updated.tzinfo is None:
        raise ValueError('Registration timestamp must contain a timezone')
    timestamp = updated.timestamp()
    if not session_mtime <= timestamp <= now or profile_mtime > timestamp:
        raise ValueError('Registration freshness cannot be established for this session/profile input')
    if not re.fullmatch('[a-f0-9]{32}', report.get('run', '')):
        raise ValueError('Registration run identity missing')


def journal_has_new_run(path, session):
    with path.open() as stream:
        for _ in range(20):
            line=stream.readline(1024*1024+1)
            if not line: break
            if len(line)>1024*1024: raise ValueError('Oversized case journal preamble')
            record=json.loads(line)
            if record.get('kind')=='provenance':
                data=record.get('data') or {}
                return data.get('session')==session and data.get('mode')=='new-run'
    return False


def living(state):
    party = state.get('party')
    return bool(party) and all(isinstance(p.get('hp'), (int, float)) and p['hp'] > 0 for p in party)


def exact_combat(state, enemy):
    combat = state.get('combat') or {}
    enemies = combat.get('enemies') or []
    return (living(state) and combat.get('active') is True and combat.get('heroTurnReady') is True
            and len(enemies) == 1 and enemies[0].get('type') == enemy)


def validate_inventory(inventory, profile, require_active=True):
    renderers = inventory.get('renderers', [])
    owners = {r.get('ownerInstanceId') for r in renderers}
    if len(owners) != 1 or None in owners or 0 in owners:
        raise ValueError('Expected exactly one native enemy owner in inventory')
    matched = []
    for assignment in profile['renderers']:
        candidates = [r for r in renderers if r.get('celRelativeRendererPath') == assignment['rendererPath']]
        if len(candidates) != 1:
            raise ValueError('Missing/ambiguous CEL renderer: ' + assignment['rendererPath'])
        renderer = candidates[0]
        if renderer.get('mesh') != 'ftkmf_glb_' + assignment['glbFile']:
            raise ValueError('Expected original GLB not bound at ' + assignment['rendererPath'])
        if require_active and (not renderer.get('active') or not renderer.get('enabled')):
            raise ValueError('Expected renderer disabled or inactive')
        matched.append({'rendererPath': assignment['rendererPath'], 'glbFile': assignment['glbFile'],
                        'rendererInstanceId': renderer['instanceId'], 'ownerInstanceId': renderer['ownerInstanceId'],
                        'boneSignature': renderer.get('boneSignature')})
    return matched


class Runner:
    def __init__(self, args):
        self.a = args
        self.root = args.root.resolve(strict=True)
        if not args.root.is_absolute() or self.root != args.root.absolute() or self.root.parent.name != 'scratch':
            raise ValueError('--root must be an absolute resolved isolated directory directly under scratch')
        if any(p.is_symlink() for p in (args.root, *args.root.parents)):
            raise ValueError('Symlink root ancestry refused')
        self.session = read(self.root / 'model-test-session.json')['session']
        self.session_mtime = (self.root / 'model-test-session.json').stat().st_mtime
        if not re.fullmatch('[a-f0-9]{32}', self.session):
            raise ValueError('Invalid helper session nonce')
        self.case = uuid.uuid4().hex
        self.output = self.root / 'model-test-output' / ('case-' + self.case)
        self.output.mkdir(parents=True, exist_ok=False)
        self.journal = self.output / 'journal.jsonl'
        self.seq = 0
        self.binary_pins = measured_binaries(self.root)
        self.log('deployed-binaries', {'root':str(self.root),'session':self.session,'binaries':self.binary_pins,
            'measurement':'on_disk_binary_sha256','boundary':'Measured isolated DLL files before case operations; does not prove loaded process memory identity.'})
        self.expected_class_id = None
        self.profile = self.profile_preflight()
        if args.class_key:
            self.class_preflight()
        self.log('provenance', {'root': str(self.root), 'port': args.port, 'session': self.session,
            'case': self.case, 'mode': args.mode, 'enemy': args.enemy, 'class': args.class_key,
            'profile': self.profile, 'profileInputSha256': digest(self.root/'model-test-profiles.json'),
            'portOwnership': 'explicit operator-selected bridge port; bridge has no root/session identity endpoint'})

    def log(self, kind, data):
        self.seq += 1
        item = {'sequence': self.seq, 'utc': datetime.now(timezone.utc).isoformat(),
                'monotonic': time.monotonic(), 'kind': kind, 'data': data}
        with self.journal.open('a') as stream:
            stream.write(json.dumps(item, separators=(',', ':')) + '\n')

    def profile_preflight(self):
        profiles = read(self.root/'model-test-profiles.json')['profiles']
        hits = [p for p in profiles if p['key'] == self.a.enemy]
        if len(hits) != 1:
            raise ValueError('--enemy must match exactly one configured custom profile key')
        registration = read(self.root/'model-test-registration.json')
        validate_registration_freshness(registration, self.session_mtime,
            (self.root/'model-test-profiles.json').stat().st_mtime, time.time())
        if registration.get('status') != 'registered' or not any(p['key']==self.a.enemy for p in registration['registered']):
            raise ValueError('Enemy profile is not in the successful registration report')
        self.registration_run = registration['run']
        self.log('enemy-registration', {'report':registration,
            'sessionFileMtime':self.session_mtime, 'freshness':'report timestamp at or after current helper nonce creation'})
        assets = {}
        for name in profile_materials.asset_names(hits[0]):
            path=self.root/'BepInEx/plugins/FTKModFramework_content/models'/name
            if any(p.is_symlink() for p in (path,*path.parents)):raise ValueError('Symlink asset refused')
            assets[name]=digest(path)
        self.profile_asset_pins=dict(assets)
        self.profile_input_pin=digest(self.root/'model-test-profiles.json')
        self.log('profile-assets', assets)
        return hits[0]

    def class_preflight(self):
        path = self.root/'model-test-player-profiles.json'
        profiles = read(path)['profiles']
        matches = [p for p in profiles if p['key'] == self.a.class_key]
        report = read(self.root/'model-test-player-registration.json')
        validate_registration_freshness(report, self.session_mtime, path.stat().st_mtime, time.time())
        entries = [p for p in report.get('registered', []) if p['key'] == self.a.class_key]
        if len(matches)!=1 or len(entries)!=1 or report.get('status')!='registered' or report['run']!=self.registration_run:
            raise ValueError('Requested class lacks a matching current player registration')
        if (entries[0].get('baseClass')!=matches[0]['baseClass'] or entries[0].get('skinset')!=matches[0]['skinset']
                or entries[0].get('defaultSkinType')!=matches[0]['defaultSkinType']
                or entries[0].get('startingArmor')!=matches[0].get('startingArmor')):
            raise ValueError('Registered class metadata differs from selected player profile')
        class_id = entries[0].get('id')
        if type(class_id) is not int or class_id < 0:
            raise ValueError('Registered class ID is invalid')
        self.expected_class_id = class_id
        self.log('player-registration', {'profile':matches[0], 'report':report,
            'profileInputSha256':digest(path), 'expectedClassId':class_id})

    def verify_party_class(self, state):
        if self.expected_class_id is not None:
            party = state.get('party') or []
            if len(party)!=1 or party[0].get('classId')!=self.expected_class_id:
                raise RuntimeError('Actual party class differs from requested registered player profile')

    def check_inputs(self):
        self.check_session()
        if getattr(self,'profile_input_pin',None) is not None and digest(self.root/'model-test-profiles.json')!=self.profile_input_pin:
            raise RuntimeError('Profile input changed since initial measurement')
        for name,expected in getattr(self,'profile_asset_pins',{}).items():
            path=self.root/'BepInEx/plugins/FTKModFramework_content/models'/name
            if any(p.is_symlink() for p in (path,*path.parents)) or digest(path)!=expected:
                raise RuntimeError('Profile asset changed since initial measurement: '+name)
        if measured_binaries(self.root)!=self.binary_pins:
            raise RuntimeError('Deployed binary files changed since initial case measurement; action withheld')

    def check_session(self):
        if read(self.root/'model-test-session.json')['session'] != self.session:
            raise RuntimeError('Game helper session changed; stopping')

    def claim_first_run(self):
        for journal in (self.root/'model-test-output').glob('case-*/journal.jsonl'):
            if journal!=self.journal and journal_has_new_run(journal,self.session):
                raise RuntimeError('A prior new-run journal exists for this helper session; fresh game process required: '+str(journal))
        marker=self.root/'model-test-output'/('new-run-session-'+self.session+'.json')
        # Exclusive creation also closes the race between two copies of this runner.
        # Keep the marker even after rejection/timeout; an uncertain action is never retried.
        with marker.open('x') as stream:
            json.dump({'session':self.session,'case':self.case,'journal':str(self.journal)},stream)
        self.log('fresh-process-claim',{'marker':str(marker),'prerequisite':'operator confirms first run of this game process'})
        self.startup_log=self.root/'BepInEx/LogOutput.log'
        self.startup_offset=self.startup_log.stat().st_size if self.startup_log.exists() else 0
        self.startup_log_carry=''

    def check_startup_errors(self):
        if not self.startup_log.exists(): return
        if self.startup_log.stat().st_size<self.startup_offset:self.startup_offset=0
        with self.startup_log.open('rb') as stream:
            stream.seek(self.startup_offset);chunk=stream.read(1024*1024);self.startup_offset=stream.tell()
        text=self.startup_log_carry+chunk.decode('utf-8',errors='replace')
        self.startup_log_carry=text[-8192:]
        if 'start_run failed' in text or '_createRealmCasterTable' in text:
            self.log('startup-log-failure',{'log':str(self.startup_log),'excerpt':text[-16384:]})
            raise RuntimeError('Native/start_run asynchronous startup failure in BepInEx log; no retry. Fresh process and diagnosis required.')

    def http(self, path, payload=None):
        # Only an explicit read-only busy response permits another observation.
        deadline=None
        attempt=0
        while True:
            self.check_inputs()
            if deadline is None:deadline=time.monotonic()+self.a.operation_timeout
            remaining=deadline-time.monotonic()
            if remaining<=0:raise TimeoutError('State observation deadline exhausted; no action retried')
            attempt+=1
            operation_id=uuid.uuid4().hex
            self.log('http-request', {'id': operation_id, 'path': path, 'payload': payload, 'attempt':attempt})
            request=urllib.request.Request(f'http://127.0.0.1:{self.a.port}{path}',
                data=None if payload is None else json.dumps(payload).encode(),
                headers={'Content-Type':'application/json','X-FTK-Case-Operation':operation_id})
            try:
                with urllib.request.urlopen(request, timeout=remaining) as response:
                    result=json.loads(response.read())
            except urllib.error.HTTPError as error:
                if path!='/state' or payload is not None or error.code!=500:raise
                try:body=error.read(16385)
                finally:error.close()
                try:failure=json.loads(body) if len(body)<=16384 else None
                except (ValueError, UnicodeError):failure=None
                busy=(failure=={'error':'state read timed out (main thread busy)'})
                self.log('http-state-error',{'id':operation_id,'attempt':attempt,'status':500,
                    'response':failure,'busyRetryEligible':busy,'remainingSeconds':max(0,deadline-time.monotonic())})
                if not busy:raise
                remaining=deadline-time.monotonic()
                if remaining<=0:raise TimeoutError('State busy observation deadline exhausted; no action retried') from error
                time.sleep(min(.25,remaining))
                continue
            self.log('http-result', {'id': operation_id, 'result': result})
            if result.get('ok') is False:
                raise RuntimeError('Bridge rejected request: '+str(result.get('error')))
            return result

    def state(self):
        return self.http('/state')

    def action(self, name, args=None, tolerate_no_message=False):
        # Only this exact transition rejection is observationally harmless.
        try:
            result = self.http('/action', {'action':name,'args':args or {}})
            if result.get('ok') is not True:
                raise RuntimeError('Bridge action returned no explicit success: ' + name)
            return result
        except RuntimeError as error:
            if tolerate_no_message and str(error)=='Bridge rejected request: no message open':
                self.log('transition-no-message', {'action':name})
                return None
            raise

    def helper(self, op, payload=None):
        self.check_inputs()
        command_path=self.root/'model-test-command.json'
        if command_path.exists():
            previous=read(command_path)
            if previous.get('session')==self.session and not (self.root/'model-test-output'/(previous['id']+'.json')).exists():
                raise RuntimeError('Another helper command is pending; refusing to overwrite it')
        operation_id=uuid.uuid4().hex
        command=dict(payload or {}, id=operation_id, session=self.session, op=op)
        self.log('helper-request', command)
        temporary=self.root/('model-test-'+operation_id+'.tmp')
        with temporary.open('x') as stream: json.dump(command,stream)
        temporary.replace(command_path)
        result_path=self.root/'model-test-output'/(operation_id+'.json')
        deadline=time.monotonic()+self.a.operation_timeout
        while time.monotonic()<deadline:
            self.check_session()
            if result_path.exists():
                result=read(result_path)
                self.log('helper-result', {'path':str(result_path),'result':result})
                if result.get('session')!=self.session or result.get('id')!=operation_id:
                    raise RuntimeError('Mismatched helper result identity')
                if result.get('ok') is not True:
                    raise RuntimeError('Helper rejected '+op+': '+str(result.get('error')))
                return result
            time.sleep(.1)
        raise TimeoutError('Uncertain helper timeout; do not retry automatically. Inspect '+str(result_path))

    def wait(self, predicate, label):
        deadline=time.monotonic()+self.a.wait_timeout
        while time.monotonic()<deadline:
            if label=='actual living party':self.check_startup_errors()
            state=self.state()
            if predicate(state): return state
            if any(isinstance(p.get('hp'),(int,float)) and p['hp']<=0 for p in state.get('party') or []):
                raise RuntimeError('Party death while waiting for '+label)
            time.sleep(.25)
        raise TimeoutError('Timed out waiting for '+label+'; no action retried')

    def clear_intro(self):
        return story_setup.clear(self)

    def verify_story_clear(self):
        state=self.helper('story-state');story_setup.scope(state)
        if not story_setup.complete(state):raise RuntimeError('Native story changed before dungeon entry; no action retried')
        self.log('story-clear-before-entry',state)

    def prepare_entry(self):
        return entry_setup.verify(self,entry_setup.prepare(self))

    def staging_result(self,matches,final):
        if not exact_combat(final,self.a.enemy):raise RuntimeError('Combat state changed during inventory verification')
        status=story_setup.staging_status(final)
        return {'status':status,'enemy':self.a.enemy,'matches':matches,'finalState':final,
            'session':self.session,'journal':str(self.journal),
            'limitations':'No appearance, motion, gameplay, material-quality or full asset-content acceptance.',
            'storyBoundary':'Story during combat is never auto-operated; pending_story_message is not capture readiness.'}

    def encounter_observed(self,state):
        # A newly triggered story can prevent combat creation entirely. Do not
        # wait for combat before recognizing this boundary or operate its UI.
        status=story_setup.staging_status(state)
        return status=='pending_story_message' or exact_combat(state,self.a.enemy)

    def wait_for_encounter(self):
        state=self.wait(self.encounter_observed,'exact single enemy and heroTurnReady or pending story')
        if story_setup.staging_status(state)=='pending_story_message':
            result={'status':'pending_story_message','enemy':self.a.enemy,'matches':[],
                'finalState':state,'session':self.session,'journal':str(self.journal),
                'limitations':'Story observed before binding verification. No inventory, binding, readiness or visual acceptance.',
                'storyBoundary':'No story UI operated and no staging action retried.'}
            self.log('post-stage-story-boundary',result)
            return result
        return None

    def run(self):
        initial=self.state()
        if self.a.mode=='new-run':
            if initial.get('phase')!='menu': raise ValueError('new-run requires bridge phase menu')
            self.claim_first_run()
            payload={'adventure':'HollowMire','party':1}
            if self.a.class_key: payload['class']=self.a.class_key
            self.action('start_run',payload)
            party_state = self.wait(lambda s: s.get('singlePlayer') is True and living(s),'actual living party')
            self.verify_party_class(party_state)
            self.helper('quiet-tutorials')
            self.helper('fortify-party',{'targetMaxHp':999})
            self.clear_intro()
            if self.expected_class_id is not None:
                self.verify_party_class(self.state())
            self.verify_story_clear()
            self.prepare_entry()
            self.action('enter_dungeon',{'dungeonId':'FloodedCrypt'})
            # Deliberately no state read, delay, modal handling, or extra bridge action here.
            self.helper('stage-enemy',{'enemy':self.a.enemy,'level':0,'room':1,'regenerate':True})
        else:
            if not living(initial) or initial.get('singlePlayer') is not True:
                raise ValueError('next-case requires a living single-player party')
            dungeon=initial.get('dungeon') or {}
            if dungeon.get('level')!=self.a.level or dungeon.get('room')!=self.a.room:
                raise ValueError('Supplied indices differ from current dungeon slot')
            # Authoritative helper additionally requires both native Ready votes, enabled
            # vote FSM, empty fight order, living heroes, and an active Ready button.
            self.helper('stage-next-enemy',{'enemy':self.a.enemy,'level':self.a.level,'room':self.a.room})
            self.helper('fortify-party',{'targetMaxHp':999,'allowReady':True})
            self.helper('ready')
        pending=self.wait_for_encounter()
        if pending is not None:return pending
        inventory=self.helper('inventory',{'scope':'enemies'})
        matches=validate_inventory(inventory,self.profile)
        final=self.state()
        return self.staging_result(matches,final)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('mode',choices=('new-run','next-case'))
    p.add_argument('--root',type=Path,required=True)
    p.add_argument('--port',type=int,required=True)
    p.add_argument('--enemy',required=True)
    p.add_argument('--class',dest='class_key')
    p.add_argument('--level',type=int)
    p.add_argument('--room',type=int)
    p.add_argument('--operation-timeout',type=float,default=40)
    p.add_argument('--wait-timeout',type=float,default=120)
    a=p.parse_args()
    if not 1<=a.port<=65535 or not 1<=a.operation_timeout<=120 or not 1<=a.wait_timeout<=600:
        p.error('Invalid port/timeouts')
    if a.mode=='next-case' and (a.level is None or a.room is None or a.class_key):
        p.error('next-case requires --level/--room and does not accept --class')
    if a.mode=='next-case' and (a.level<0 or a.room<0):
        p.error('Current level and room must be nonnegative')
    if a.mode=='new-run' and (a.level is not None or a.room is not None):
        p.error('new-run stages fixed level0/room1; do not supply indices')
    runner=None
    try:
        runner=Runner(a); result=runner.run()
    except Exception as error:
        result={'status':'stopped','error':str(error),'note':'No automatic retry; inspect game state before another action.'}
        if runner:
            runner.log('stopped',result);result['journal']=str(runner.journal)
        print(json.dumps(result,indent=2));raise SystemExit(1)
    runner.log('result',result)
    (runner.output/'result.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))
    if result['status']!='binding_metadata_observed':raise SystemExit(2)


if __name__=='__main__':main()
