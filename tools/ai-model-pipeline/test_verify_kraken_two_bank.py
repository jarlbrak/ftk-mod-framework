"""Synthetic duplicate clocks and corruption checks, not a Unity graph simulation."""
import copy,unittest
import numpy as np
from test_verify_kraken_modern_mixer import fixed_fixture
from verify_kraken_modern_mixer import BANK_MODE,BANK_CLIPS,FIXED_MODE,Checks,compare,validate_mixer

def fixture(case='a',start=1,intro=False,appearance=False):
 r,q,n=fixed_fixture(case,start,appearance or intro)
 if intro:
  r['scenario']=q['scenario']='intro';r['identity'].update(trigger='Intro',targetState='INTRO',idleStateHash=-1009909005,targetStateHash=1746749458)
  n=(n[0],n[1],{-1009909005:dict(clip='krakenIdle',loop=True,length=4.),1746749458:dict(clip='krakenIdle',loop=True,length=4.)})
  for i in range(241,361):
   f=copy.deepcopy(r['frames'][-1]);f.update(step=i,unityFrame=r['frames'][0]['unityFrame']+i,graphElapsedSeconds=i*float(np.float32(1/60)),manualDeltaSeconds=float(np.float32(1/60)));r['frames'].append(f)
  r['expectedFrames']=361;r['frame']=r['frames'][-1]['unityFrame']+10
  for f in r['frames']:
   for side in ('modern','old'):
    v=f[side]
    for role in ('current','next'):
     if v[role] is not None:v[role]['fullPathHash']=-1009909005 if v[role]['fullPathHash']==10 else 1746749458;v[role]['loop']=True
     for c in v[role+'Clips']:c.update(name='krakenIdle',instanceId=77)
 q['modernInputMixer']=BANK_MODE;summary=r['modernInputMixer'];meta=summary['identity'];summary['mode']=meta['mode']=BANK_MODE
 ids={name:100+i for i,name in enumerate(BANK_CLIPS)}
 for f in r['frames']:
  for c in f['modern']['currentClips']+f['modern']['nextClips']:ids[c['name']]=c['instanceId']
 slotids=[ids[name] for name in BANK_CLIPS]*2;handles=list(range(1000+start,1010+start));mh=1100+start
 meta.update(fixedClipOrder=BANK_CLIPS*2,fixedClipInstanceIds=slotids,bindingInitialization='Both five-clip banks connected before Animator.Rebind; fixed topology throughout run.',nodeHandleHashes=handles,mixerHandleHash=mh,slotRoles=['current']*5+['next']*5)
 excluded=0
 for f in r['frames']:
  v=f['modern'];s=f['modernInputMixer'];inputs=[]
  for role in (['current','next'] if v['inTransition'] else ['current']):
   st=v[role];c=v[role+'Clips'][0];phase=st['normalizedTime'];mapped=phase%1 if st['loop'] else min(1,max(0,phase))
   inputs.append(dict(role=role,clip=c['name'],clipInstanceId=c['instanceId'],state=copy.deepcopy(st),rawWeight=c['weight'],clipLength=c['length'],loop=st['loop'],mappedPhase=mapped,sampleSeconds=float(np.float32(mapped*c['length'])),requestedUnwrappedSeconds=phase*c['length']))
  weights=[0.]*10;times=[0.]*10
  for i in inputs:
   slot=(5 if i['role']=='next' else 0)+BANK_CLIPS.index(i['clip']);weights[slot]=i['rawWeight'];times[slot]=i['sampleSeconds']
  appearance_active=any(i['clip']=='kraken_appear' and i['rawWeight']>0 for i in inputs);excluded+=appearance_active
  s.update(mode=BANK_MODE,inputs=inputs,playableCount=11,inputWeights=weights,playableTimes=times,fixedClipInstanceIds=slotids,nodeHandleHashes=handles,mixerHandleHash=mh,resetCount=2*(f['step']+1),eligibleMainComparison=not appearance_active,appearanceZeroWeightBoundary=any(i['clip']=='kraken_appear' and i['rawWeight']==0 for i in inputs),scope='appearance_contribution_excluded_no_native_mixed_equivalence' if appearance_active else 'main_whole_controller_comparison_including_history',mainMaximumError=None if appearance_active else 0.,mainComparisonPassed=None if appearance_active else True)
 summary.update(eligibleFrames=len(r['frames'])-excluded,excludedAppearanceFrames=excluded,resets=2*len(r['frames']))
 return r,q,n
class Tests(unittest.TestCase):
 def bad(self,change,intro=True):
  r,q,n=fixture(intro=intro);change(r,q);checks=Checks()
  try:validate_mixer(r,q,n,checks,'bank')
  except (ValueError,KeyError):return
  self.assertTrue(checks.failures,'Corruption accepted')
 def test_intro_duplicate_clock_entry_exit_pair(self):
  r,q,n=fixture(intro=True);b,bq,_=fixture('b',500,True);v=compare(r,q,b,bq,n)
  self.assertEqual(v['status'],'modern_input_mixer_match');self.assertEqual(len(v['firstCoverage']['eligibleSteps']),361)
  self.assertTrue(v['firstCoverage']['duplicateClockEntrySteps']);self.assertTrue(v['firstCoverage']['duplicateClockExitSteps'])
 def test_old_main_and_appearance_bank_pairs(self):
  for appear in (False,True):
   r,q,n=fixture(appearance=appear);b,bq,_=fixture('b',500,appearance=appear);self.assertEqual(compare(r,q,b,bq,n)['status'],'modern_input_mixer_match')
 def test_bank_clocks_not_aggregated(self):
  self.bad(lambda r,q:r['frames'][18]['modernInputMixer']['playableTimes'].__setitem__(5,r['frames'][18]['modernInputMixer']['playableTimes'][0]))
  self.bad(lambda r,q:r['frames'][18]['modernInputMixer']['inputWeights'].__setitem__(0,1.))
 def test_inactive_attack_slot_must_remain_zero(self):
  self.bad(lambda r,q:r['frames'][0]['modernInputMixer']['playableTimes'].__setitem__(4,.1))
  self.bad(lambda r,q:r['frames'][0]['modernInputMixer']['inputWeights'].__setitem__(9,.1))
 def test_topology_mapping_and_handles_stable(self):
  self.bad(lambda r,q:r['frames'][18]['modernInputMixer'].update(playableCount=5))
  self.bad(lambda r,q:r['frames'][18]['modernInputMixer'].update(nodeHandleHashes=list(range(10))))
  self.bad(lambda r,q:r['modernInputMixer']['identity'].update(slotRoles=['next']*5+['current']*5))
  self.bad(lambda r,q:r['modernInputMixer']['identity'].update(fixedClipOrder=BANK_CLIPS[::-1]*2))
 def test_intro_requires_exact_mode_horizon_and_exit(self):
  self.bad(lambda r,q:q.update(modernInputMixer=FIXED_MODE))
  self.bad(lambda r,q:r.update(expectedFrames=241,frames=r['frames'][:241]))
  self.bad(lambda r,q:r['frames'].__delitem__(slice(120,None)))
 def test_source_mutation_cleanup_and_pose_corruption(self):
  self.bad(lambda r,q:r['frames'][60]['modernInputMixer'].update(nativeOldAfterSha256='f'*64))
  self.bad(lambda r,q:r['cleanup'].update(modernInputMixerClean=False))
  self.bad(lambda r,q:r['frames'][60]['modernInputMixer']['firstPose']['locals']['Root_M']['position'].__setitem__(0,5.))
 def test_integer_cycle_separation_is_not_distinct_sample_clock(self):
  r,q,n=fixture(intro=True)
  for f in r['frames']:
   if not f['modern']['inTransition']:continue
   for side in ('modern','old'):
    f[side]['current']['normalizedTime']=.25;f[side]['next']['normalizedTime']=1.25
   sample=f['modernInputMixer']
   for item in sample['inputs']:
    phase=.25 if item['role']=='current' else 1.25
    item.update(state=copy.deepcopy(f['modern'][item['role']]),mappedPhase=.25,sampleSeconds=1.,requestedUnwrappedSeconds=phase*4.)
   sample['playableTimes'][0]=sample['playableTimes'][5]=1.
  with self.assertRaisesRegex(ValueError,'Intro requires entry/exit'):validate_mixer(r,q,n,Checks(),'loop')
 def test_attack_state_forbidden(self):
  self.bad(lambda r,q:r['frames'][60]['modern']['current'].update(fullPathHash=-1274329858))
if __name__=='__main__':unittest.main()
