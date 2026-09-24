#!/usr/bin/env python3
"""Capture one real custom player's combat avatar without submitting game actions."""
import argparse
import re
from pathlib import Path
from run_case import read,digest,validate_inventory
from record_case import DEFAULT_CAPTURE_TIMEOUT, Recorder,guard,require_observation


def player_guard(state,enemy,class_id):
    result=guard(state,enemy)
    if len(state['party'])!=1 or state['party'][0].get('classId')!=class_id:
        raise RuntimeError('Exactly one actual hero of the registered custom class required')
    if state['party'][0]['fid']!=result['actingFid']:
        raise RuntimeError('Native acting hero differs from the sole owned party hero')
    return result


def player_assignments(profile):
    required=profile.get('renderers')
    apparel=profile.get('apparel',[])
    if not isinstance(required,list) or not 1<=len(required)<=16 or not isinstance(apparel,list) or len(apparel)>16:
        raise ValueError('Required renderers1..16 and optional apparel0..16 required')
    paths=set()
    for optional,assignments in ((False,required),(True,apparel)):
        for a in assignments:
            allowed={'rendererPath','glbFile','textureFile'}|({'expectedNativeMeshName'} if optional else set())
            if not isinstance(a,dict) or set(a)-allowed:raise ValueError('Invalid player assignment fields')
            path=a.get('rendererPath')
            if not isinstance(path,str) or not 1<=len(path)<=512 or path in paths or '\\' in path or (path!='.' and any(p in ('','.','..') for p in path.split('/'))):
                raise ValueError('Invalid or duplicate player renderer path')
            paths.add(path)
            if optional and (not isinstance(a.get('expectedNativeMeshName'),str) or not a['expectedNativeMeshName'].strip() or len(a['expectedNativeMeshName'])>160 or any(ord(c)<32 or 127<=ord(c)<=159 for c in a['expectedNativeMeshName'])):
                raise ValueError('Exact nonblank expectedNativeMeshName required')
            for field,ext in (('glbFile','glb'),('textureFile','png')):
                name=a.get(field)
                if field=='textureFile' and name is None:continue
                if not isinstance(name,str) or not re.fullmatch(r'[A-Za-z0-9_-][A-Za-z0-9_.-]{0,119}\.'+ext,name):
                    raise ValueError('Invalid player asset basename/type')
    return required,apparel


def validate_player_registration_assignments(profile,entry):
    if 'renderers' in entry and entry['renderers']!=profile['renderers']:
        raise ValueError('Registered required assignments differ from exact profile')
    if 'apparel' in profile:
        if entry.get('renderers')!=profile['renderers'] or entry.get('apparel')!=profile['apparel']:
            raise ValueError('Apparel profile requires matching registered required and conditional assignments')
    elif 'apparel' in entry:
        raise ValueError('Registration unexpectedly includes conditional apparel')


def player_outfit_coverage(renderers,profile):
    required,apparel=player_assignments(profile)
    validate_inventory({'renderers':renderers},{'renderers':required},require_active=False)
    present=[];absent=[]
    for a in apparel:
        matches=[r for r in renderers if r.get('celRelativeRendererPath')==a['rendererPath']]
        if not matches:absent.append(a['rendererPath']);continue
        if len(matches)!=1 or matches[0].get('mesh')!='ftkmf_glb_'+a['glbFile']:
            raise ValueError('Present conditional apparel must have exact custom GLB identity')
        present.append(a['rendererPath'])
    return {'presentApparelPaths':present,'absentApparelPaths':absent,
            'allConfiguredAssignmentsPresent':not absent,
            'activeUnmappedRenderers':[{'path':r.get('celRelativeRendererPath'),'mesh':r.get('mesh'),'instanceId':r.get('instanceId')} for r in renderers if r.get('active') and r.get('enabled') and r.get('celRelativeRendererPath') not in [a['rendererPath'] for a in required+apparel]],
            'status':'conditional_apparel_absent_actual_outfit_coverage_not_established' if absent else 'configured_assignment_identities_verified',
            'boundary':'Configured mesh identities only; not full outfit art acceptance or proof every native garment has a custom assignment. Original native mesh identity is verified before application, unavailable after replacement.'}


def select_player_renderer(inventory,equipment,profile,hero_id,path):
    heroes=equipment.get('heroes') or []
    if len(heroes)!=1 or heroes[0].get('heroInstanceId')!=hero_id or heroes[0].get('alive') is not True:
        raise RuntimeError('Exact single living native equipment owner required')
    cel=heroes[0].get('dummyCelInstanceId')
    if not isinstance(cel,int) or cel==0:raise RuntimeError('Owned hero has no current combat CEL')
    all_renderers=inventory.get('renderers') or []
    if not all_renderers or any(r.get('ownerKind')!='player-combat' for r in all_renderers):
        raise RuntimeError('Only explicit player-combat inventory is accepted')
    renderers=[r for r in all_renderers if r.get('celInstanceId')==cel]
    player_outfit_coverage(renderers,profile)
    if any(r.get('celInstanceId')!=cel for r in renderers):raise RuntimeError('Combat avatar identity mismatch')
    matches=[r for r in renderers if r.get('celRelativeRendererPath')==path]
    if len(matches)!=1 or path not in [a['rendererPath'] for group in player_assignments(profile) for a in group]:
        raise RuntimeError('Exact configured player renderer assignment required')
    if not matches[0].get('active') or not matches[0].get('enabled'):raise RuntimeError('Selected player renderer is not active/enabled')
    return matches[0]


class PlayerRecorder(Recorder):
    capture_scope='player-combat'

    def __init__(self,args):
        require_observation(args)
        for name in ('model-test-player-profiles.json','model-test-player-registration.json'):
            path=args.root/name
            if any(p.is_symlink() for p in (path,*path.parents)):raise ValueError('Symlink player metadata refused')
        super().__init__(args)
        self.log('player-capture-provenance',{'profile':self.player_profile,'expectedClassId':self.expected_class_id,
            'expectedSkinset':args.skinset,'playerProfileSha256':args.player_profile_sha256,'heroInstanceId':args.hero_instance_id,
            'scope':self.capture_scope,'boundary':'one actual party hero; exact equipment-owner dummyCEL join; skinset registration and all assigned GLB identities checked, native skinset field not independently queried'})

    def class_preflight(self):
        super().class_preflight()
        profiles=read(self.root/'model-test-player-profiles.json')['profiles']
        self.player_profile=next(p for p in profiles if p['key']==self.a.class_key)
        entry=next(p for p in read(self.root/'model-test-player-registration.json')['registered'] if p['key']==self.a.class_key)
        validate_player_registration_assignments(self.player_profile,entry)
        if self.player_profile['skinset']!=self.a.skinset:raise ValueError('Requested skinset differs from exact registered player profile')
        for assignment in [a for group in player_assignments(self.player_profile) for a in group]:
            for field in ('glbFile','textureFile'):
                name=assignment.get(field)
                if name is None:continue
                if Path(name).name!=name or '/' in name or '\\' in name:raise ValueError('Invalid player asset basename')
                path=self.root/'BepInEx/plugins/FTKModFramework_content/models'/name
                if any(p.is_symlink() for p in (path,*path.parents)):raise ValueError('Symlink player asset refused')
                self.asset_hashes[name]=digest(path)
        self.log('player-assets',self.asset_hashes)

    def check_inputs(self):
        super().check_inputs()
        if digest(self.root/'model-test-player-profiles.json')!=self.a.player_profile_sha256:
            raise RuntimeError('Player profile hash changed/mismatched')

    def combat_guard(self,state):
        return player_guard(state,self.a.enemy,self.expected_class_id)

    def select_renderer(self):
        equipment=self.helper('equipment-inventory')
        inventory=self.helper('inventory',{'scope':self.capture_scope})
        renderer=select_player_renderer(inventory,equipment,self.player_profile,self.a.hero_instance_id,self.a.renderer_path)
        self.log('player-outfit-coverage',player_outfit_coverage([r for r in inventory['renderers'] if r.get('celInstanceId')==renderer['celInstanceId']],self.player_profile))
        self.log('player-owner-join',{'heroInstanceId':self.a.hero_instance_id,'dummyCelInstanceId':renderer['celInstanceId'],
            'ownerInstanceId':renderer['ownerInstanceId'],'rendererInstanceId':renderer['instanceId'],'equipment':equipment})
        return renderer


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root',type=Path,required=True);p.add_argument('--port',type=int,required=True)
    p.add_argument('--enemy',required=True);p.add_argument('--profile-sha256',required=True,help='Expected enemy profile JSON SHA256')
    p.add_argument('--class-key',required=True);p.add_argument('--skinset',required=True)
    p.add_argument('--player-profile-sha256',required=True);p.add_argument('--hero-instance-id',type=int,required=True)
    p.add_argument('--renderer-path',required=True);p.add_argument('--action',choices=('observe',),required=True)
    p.add_argument('--operation-timeout',type=float,default=40);p.add_argument('--capture-timeout',type=float,default=DEFAULT_CAPTURE_TIMEOUT)
    args=p.parse_args();args.mode='record-player-action'
    recorder=PlayerRecorder(args);result=recorder.record();print(recorder.output);raise SystemExit(0 if result['ok'] else 1)


if __name__=='__main__':main()
