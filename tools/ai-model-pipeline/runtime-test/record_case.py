#!/usr/bin/env python3
"""Capture an already prepared isolated combat avatar without submitting game actions."""
import argparse
import importlib.util
import json
import math
import profile_materials
import time
import uuid
from pathlib import Path
from run_case import Runner, read, digest, exact_combat, validate_inventory


# A fixed 120-frame capture can spend several minutes encoding full-size PNGs on
# an isolated FTK process. Keep the default above the observed 205-second write
# span so a valid capture is not abandoned merely because recording is expensive
# in wall-clock time.
DEFAULT_CAPTURE_TIMEOUT = 360


def guard(state, enemy):
    signals=state.get('signals') or {}; combat=state.get('combat') or {}
    if (state.get('singlePlayer') is not True or state.get('inSession') is not True
        or not exact_combat(state,enemy) or combat['enemies'][0].get('alive') is not True
        or signals.get('modalOpen') is not False or signals.get('choiceOpen') is not False
        or signals.get('allDead') is not False or signals.get('victoryShowing') is not False
        or (combat.get('readyParts') or {}).get('initialized') is not True
        or (combat.get('whoseTurn') or {}).get('isPlayer') is not True):
        raise RuntimeError('Living single-player exact enemy/native hero-turn guard failed')
    return {'enemyFid':combat['enemies'][0]['fid'], 'actingFid':combat['readyParts']['actingFid'],
            'partyFids':[p['fid'] for p in state['party']]}


RECORD_GUIDANCE = ("Direct pass/attack/kill-fixture recording is retired. Use --action observe "
    "to capture an already prepared encounter; drive gameplay separately with harness ftk_ui/ftk_input. "
    "Capture alone does not establish an attack, hit, death, or ordinary-gameplay outcome.")


def require_observation(args):
    if getattr(args, 'action', None) != 'observe' or getattr(args, 'focus', False):
        raise ValueError(RECORD_GUIDANCE)


def verify_action(action,result,target_fid=None,focus=False):
    # Bridge responses currently wrap details in result; tolerate only that documented envelope.
    body=result.get('result',result)
    expected='KillSingle' if action=='kill-fixture' else ('Attack(focus)' if action=='attack' and focus else 'Attack')
    if result.get('ok') is not True:
        raise RuntimeError('Action response did not confirm success')
    if action=='pass':
        if body.get('ended')!='combat':raise RuntimeError('Action response did not confirm native end_turn')
    elif body.get('committed')!=expected:
        raise RuntimeError('Action response committed '+repr(body.get('committed'))+' instead of '+expected)
    if action!='pass' and target_fid is None:
        raise RuntimeError('Freshly guarded enemy FID is missing')
    if action!='pass' and body.get('target')!=target_fid:
        raise RuntimeError('Action response target differs from freshly guarded enemy FID: '+repr(body.get('target'))+' != '+repr(target_fid))


def kill_fixture_handoff(state,target_fid):
    """Classify the post-KillSingle observation without taking another game action.

    FTK keeps EncounterSession.m_IsInCombat true while its native Loot vote is
    open. A dead target, zero live enemies, a winning player, and a closed
    battle stance are therefore a victory handoff, not the hp-zero/alive-true
    stuck signature. The caller records this distinction but never resolves it.
    """
    combat=state.get('combat') or {}
    enemies=combat.get('enemies') or []
    target=next((enemy for enemy in enemies if enemy.get('fid')==target_fid),None)
    result={'targetFid':target_fid,'combatActive':combat.get('active'),
            'liveEnemies':combat.get('liveEnemies'),'heroTurnReady':combat.get('heroTurnReady'),
            'winningPlayerFid':combat.get('winningPlayerFid'),'stuck':combat.get('stuck')}
    if target is not None:
        result['targetHp']=target.get('hp');result['targetAlive']=target.get('alive')
    target_dead=(target is None or (target.get('alive') is False
                                    and isinstance(target.get('hp'),(int,float))
                                    and not isinstance(target.get('hp'),bool)
                                    and target.get('hp')<=0))
    if combat.get('stuck') is True:
        result['status']='stuck_signature'
    elif (combat.get('active') is True and combat.get('liveEnemies')==0
          and combat.get('heroTurnReady') is False and combat.get('winningPlayerFid') is not None
          and target_dead):
        result['status']='victory_pending_native_loot'
    elif combat.get('active') is False:
        result['status']='combat_closed'
    else:
        result['status']='death_transition_unclassified'
    return result


class Recorder(Runner):
    capture_scope="enemies"
    def __init__(self,args):
        require_observation(args)
        if not 1<=args.port<=65535 or not all(math.isfinite(v) and v>0 for v in (args.operation_timeout,args.capture_timeout)):
            raise ValueError('Valid explicit port and positive timeouts required')
        for name in ('model-test-output','model-test-session.json','model-test-profiles.json','model-test-registration.json'):
            path=args.root/name
            if any(p.is_symlink() for p in (path,*path.parents)):
                raise ValueError('Symlink input/output ancestry refused')
        super().__init__(args)

    def log(self,kind,data):
        if kind=='profile-assets': self.asset_hashes=dict(data)
        super().log(kind,data)

    def check_inputs(self):
        super().check_inputs()
        if digest(self.root/'model-test-profiles.json')!=self.a.profile_sha256:
            raise RuntimeError('Profile input hash changed/mismatched')
        for name,expected in self.asset_hashes.items():
            path=self.root/'BepInEx/plugins/FTKModFramework_content/models'/name
            if any(p.is_symlink() for p in (path,*path.parents)) or digest(path)!=expected:
                raise RuntimeError('Profile asset changed or symlink encountered: '+name)

    def begin_capture(self,renderer):
        self.check_inputs(); command_path=self.root/'model-test-command.json'
        if command_path.exists():
            previous=read(command_path)
            if previous.get('session')==self.session and not (self.root/'model-test-output'/(previous['id']+'.json')).exists():
                raise RuntimeError('Existing helper operation pending; refusing overwrite')
        identity={'rendererId':renderer['instanceId'],'ownerInstanceId':renderer['ownerInstanceId'],
                  'rendererPath':renderer['rendererPath'],'expectedMesh':renderer['mesh'],
                  'boneSignature':renderer['boneSignature'],'scope':self.capture_scope}
        self.capture_id=uuid.uuid4().hex
        command=dict(identity,id=self.capture_id,session=self.session,op='capture',seconds=10,fps=12,maxWidth=1280,fixedStep=True)
        if getattr(self.a,'motion_evidence',False):command['motionObservation']=True
        assignment=next((a for a in getattr(self,'profile',{}).get('renderers',[]) if a.get('rendererPath')==renderer.get('celRelativeRendererPath')),None)
        self.capture_material_assignment=assignment if self.capture_scope=='enemies' and assignment is not None and profile_materials.slots(assignment) is not None else None
        if self.capture_material_assignment is not None:command['materialObservation']=True
        self.capture_path=self.root/'model-test-output'/(self.capture_id+'.json')
        self.log('capture-request',command)
        temporary=self.root/('model-test-'+self.capture_id+'.tmp')
        with temporary.open('x') as stream:json.dump(command,stream)
        temporary.replace(command_path)

    def wait_first_frame(self):
        deadline=time.monotonic()+self.a.operation_timeout
        while time.monotonic()<deadline:
            self.check_inputs()
            if self.capture_path.exists():raise RuntimeError('Capture completed/failed before observation; capture outcome incomplete')
            frame=self.capture_path.with_suffix('')/'0000.png'
            if frame.is_file() and frame.stat().st_size>8:
                with frame.open('rb') as stream:
                    if stream.read(8)!=b'\x89PNG\r\n\x1a\n':raise RuntimeError('Invalid first PNG')
                self.log('first-frame',{'path':str(frame)});return
            time.sleep(.05)
        raise TimeoutError('No first PNG in budget; observation stopped, capture execution uncertain')

    def collect_capture(self,renderer):
        deadline=time.monotonic()+self.a.capture_timeout
        while not self.capture_path.exists():
            self.check_session()
            if time.monotonic()>=deadline:raise TimeoutError('Capture incomplete; preserve existing PNGs/result path, no retry')
            time.sleep(.1)
        doc=read(self.capture_path);self.log('capture-result',{'path':str(self.capture_path),'result':doc})
        if doc.get('session')!=self.session or doc.get('id')!=self.capture_id:raise RuntimeError('Capture result identity changed')
        spec=importlib.util.spec_from_file_location('ftk_capture_summary',Path(__file__).resolve().parents[1]/'summarize_capture.py')
        module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
        summary=module.summarize(self.capture_path)
        # Keep raw partial capture unsuccessful. The exercise coordinator can separately
        # classify a validated death boundary and decide whether cleanup may progress.
        if (getattr(self.a,'action',None)=='kill-fixture'
                and (doc.get('motionObservation') or {}).get('termination') is not None):
            spec=importlib.util.spec_from_file_location('record_capture_boundary',Path(__file__).resolve().parents[1]/'compare_kraken_native.py')
            boundary_module=importlib.util.module_from_spec(spec);spec.loader.exec_module(boundary_module)
            summary['captureBoundary']=boundary_module.capture_boundary({'allowDeathControllerTeardownPrefix':True},doc)
        with (self.output/'capture-summary.json').open('x') as stream:json.dump(summary,stream,indent=2)
        frames=doc.get('frames') or []
        valid=(doc.get('ok') is True and len(frames)==120 and doc.get('fixedStep') is True
               and doc.get('scope')==self.capture_scope and doc.get('ownerInstanceId')==renderer['ownerInstanceId'] and doc.get('celInstanceId')==renderer['celInstanceId'])
        material_assignment=getattr(self,'capture_material_assignment',None)
        if material_assignment is not None and doc.get('materialObservation') is not True:raise RuntimeError('Requested material observation missing from capture')
        for index,frame in enumerate(frames):
            if material_assignment is not None:profile_materials.validate_observation(frame.get('materialObservation'),renderer,material_assignment,index==0)
            valid &= all(frame.get(field)==renderer.get(field) for field in ('instanceId','ownerInstanceId','celInstanceId','mesh','boneSignature'))
            valid &= frame.get('ownerKind')==self.capture_scope
            png=self.capture_path.with_suffix('')/f'{index:04d}.png'
            valid &= png.is_file() and png.stat().st_size>8
            if png.is_file():
                with png.open('rb') as stream:valid &= stream.read(8)==b'\x89PNG\r\n\x1a\n'
            valid &= frame.get('timeScale',0)>0 and frame.get('captureFramerate')==12
        if not valid:raise RuntimeError('Partial/paused/changed capture; preserve evidence without acceptance')
        if frames[-1]['gameSeconds']-frames[0]['gameSeconds']<9:raise RuntimeError('Insufficient sampled game-time span')
        self.check_inputs()
        return {'result':str(self.capture_path),'summary':str(self.output/'capture-summary.json'),'frames':len(frames)}

    def combat_guard(self,state):
        return guard(state,self.a.enemy)

    def select_renderer(self):
        inventory=self.helper('inventory',{'scope':self.capture_scope});validate_inventory(inventory,self.profile,
            visual_scale=getattr(self,'visual_scale_contract',None))
        matches=[r for r in inventory['renderers'] if r.get('celRelativeRendererPath')==self.a.renderer_path]
        if len(matches)!=1 or self.a.renderer_path not in [r['rendererPath'] for r in self.profile['renderers']]:
            raise RuntimeError('Explicit profile renderer path missing/ambiguous')
        if matches[0].get('rendererKind','SkinnedMeshRenderer')!='SkinnedMeshRenderer':
            raise RuntimeError('Motion capture requires a skinned renderer. Verify rigid assignments through inventory and lifetime evidence.')
        return matches[0]

    def record(self):
        require_observation(self.a)
        errors=[]; capture=None; renderer=None
        try:
            self.check_inputs(); initial=self.state(); original_guard=self.combat_guard(initial)
            self.log('before',initial)
            self.log('enemy-health-fixture',{'minimumBaseHealth':self.profile.get('minimumBaseHealth'),
                'actualBeforeEnemies':initial['combat']['enemies'],
                'provenance':'optional custom clone base-health fixture; actual HP is recorded independently; no native health setter'})
            renderer=self.select_renderer();self.begin_capture(renderer);self.wait_first_frame()
            self.check_inputs();fresh=self.state()
            fresh_guard=self.combat_guard(fresh)
            if fresh_guard!=original_guard:raise RuntimeError('Combat identity changed; observation stopped')
            if self.capture_path.exists():raise RuntimeError('Capture ended before observation; capture outcome incomplete')
            self.log('capture-observation',fresh)
            self.log('observation-only', {'gameActionsSubmitted': 0,
                'boundary': 'Capture only; operate native input separately and verify its outcome.'})
        except Exception as error:
            errors.append(str(error));self.log('error',{'error':str(error),'gameActionsSubmitted':0})
        finally:
            if getattr(self,'capture_id',None):
                try:capture=self.collect_capture(renderer)
                except Exception as error:errors.append(str(error));self.log('capture-error',{'error':str(error),'resultPath':str(self.capture_path)})
            try:
                after=self.state();self.log('after',after)
            except Exception as error:errors.append(str(error));self.log('after-error',{'error':str(error)})
        note='No visual acceptance verdict or game action submitted. Capture does not establish a gameplay outcome.'
        result={'ok':not errors,'status':'recorded_observation_frames' if not errors else 'partial_or_uncertain',
                'captureScope':self.capture_scope,'classKey':getattr(self.a,'class_key',None),
                'action':'observe','gameActionsSubmitted':0,'minimumBaseHealth':self.profile.get('minimumBaseHealth'),
                'capture':capture,'errors':errors,'note':note}
        with (self.output/'result.json').open('x') as stream:json.dump(result,stream,indent=2)
        self.log('result',result)
        return result


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root',type=Path,required=True);parser.add_argument('--port',type=int,required=True)
    parser.add_argument('--enemy',required=True);parser.add_argument('--renderer-path',required=True)
    parser.add_argument('--profile-sha256',required=True,help='Expected SHA256 of model-test-profiles.json')
    parser.add_argument('--action',choices=('observe',),required=True)
    parser.add_argument('--motion-evidence',action='store_true',help='Request bounded passive target-CEL action/trigger telemetry during this existing capture.')
    parser.add_argument('--operation-timeout',type=float,default=40);parser.add_argument('--capture-timeout',type=float,default=DEFAULT_CAPTURE_TIMEOUT)
    args=parser.parse_args();args.class_key=None;args.mode='record-action'
    recorder=Recorder(args);result=recorder.record();print(recorder.output);raise SystemExit(0 if result['ok'] else 1)


if __name__=='__main__':main()
