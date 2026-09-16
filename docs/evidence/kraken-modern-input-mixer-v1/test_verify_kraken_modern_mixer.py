"""Nonconstant synthetic report corruption tests; no Unity mixer simulation claim."""
import copy,json,tempfile,unittest
from pathlib import Path
import numpy as np
from test_verify_kraken_endpoint_fixture import fixture as endpoint_fixture,make_pose
from verify_kraken_modern_mixer import compare,validate_mixer,Checks,MODE,run


def fixture(case='a',start=1,appearance=False):
 report,request,native=endpoint_fixture(case,start);request.pop('endpointPolicy');request['modernInputMixer']=MODE
 report.update(endpointPolicy=None,originalSkinProbe=None,provenance='owned_native_controller_graph_observation_no_adapter')
 report['cleanup']['modernInputMixerClean']=True
 if not appearance:
  report['scenario']=request['scenario']='damaged';report['identity'].update(trigger='Damaged',targetState='DAMAGED')
  native[2][20]['clip']='krakenDamage'
 for frame in report['frames']:
  for side in ('modern','old'):
   for field in ('currentClips','nextClips'):
    for c in frame[side][field]:
     if not appearance and c['name']=='kraken_appear':c['name']='krakenDamage'
  frame.pop('endpointPolicy')
 rest=copy.deepcopy(report['modernRest'])
 for row in rest['locals'].values():row['instanceId']+=500000
 meta=dict(mode=MODE,rootInstanceId=600000+start,animatorInstanceId=600001+start,avatarInstanceId=88,avatarIsHuman=False,rest=rest,
  immutableRestLocalTrsSha256='a'*64,sourcePrefabLocalTrsSha256='b'*64,transformCount=50,initializedAfterRebindAndEvaluate=True,normalizeWeights=False,inputConvention='Full reset experimental input convention')
 excluded=0
 for frame in report['frames']:
  view=frame['modern'];roles=['current','next'] if view['inTransition'] else ['current'];inputs=[]
  for role in roles:
   state=view[role];clip=view[role+'Clips'][0];phase=state['normalizedTime'];mapped=phase%1 if state['loop'] else min(1,max(0,phase))
   inputs.append(dict(role=role,clip=clip['name'],clipInstanceId=clip['instanceId'],state=copy.deepcopy(state),rawWeight=clip['weight'],clipLength=clip['length'],loop=state['loop'],mappedPhase=mapped,sampleSeconds=float(np.float32(mapped*clip['length'])),requestedUnwrappedSeconds=phase*clip['length']))
  excluded_frame=any(i['clip']=='kraken_appear' and i['rawWeight']>0 for i in inputs);excluded+=excluded_frame
  actual=copy.deepcopy(view['pose'])
  for path,row in actual['locals'].items():row['instanceId']=rest['locals'][path]['instanceId']
  frame['modernInputMixer']=dict(mode=MODE,eligibleMainComparison=not excluded_frame,appearanceZeroWeightBoundary=any(i['clip']=='kraken_appear' and i['rawWeight']==0 for i in inputs),scope='appearance_contribution_excluded_no_native_mixed_equivalence' if excluded_frame else 'main_whole_controller_comparison_including_history',inputs=inputs,inactiveNextClipMetadataIgnored=len(inputs)==1,
   samplerRootInstanceId=meta['rootInstanceId'],samplerAnimatorInstanceId=meta['animatorInstanceId'],avatarInstanceId=88,
   runtimeControllerAssigned=False,fireEvents=False,applyRootMotion=False,footIK=False,avatarIsHuman=False,playableIKControlAvailable=False,playableIK=None,graphMode='Manual',playableCount=3,
   inputWeights=[i['rawWeight'] for i in inputs]+([0.] if len(inputs)==1 else []),playableTimes=[i['sampleSeconds'] for i in inputs]+([0.] if len(inputs)==1 else []),resetCount=2*(frame['step']+1),firstPose=actual,repeatedPose=copy.deepcopy(actual),repeatMaximumError=0.,repeatPassed=True,mainMaximumError=None if excluded_frame else 0.,mainComparisonPassed=None if excluded_frame else True,
   nativeModernBeforeSha256='c'*64,nativeModernAfterSha256='c'*64,nativeOldBeforeSha256='d'*64,nativeOldAfterSha256='d'*64,sourcePrefabAfterSha256='b'*64)
 report['modernInputMixer']=dict(mode=MODE,identity=meta,eligibleFrames=241-excluded,excludedAppearanceFrames=excluded,resets=482,failedMainFrames=0,failedRepeatFrames=0,maximumMainError=0.,maximumRepeatError=0.,reportedNumericalComparisonPassed=True,graphDisposed=True,targetUnityNull=True,sourceUnchanged=True)
 return report,request,native


class Tests(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.base,cls.request,cls.native=fixture()
 def bad(self,change):
  report=copy.deepcopy(self.base);change(report);checks=Checks()
  try:validate_mixer(report,self.request,self.native,checks,'test')
  except (ValueError,KeyError):return
  self.assertTrue(checks.failures,'Corrupted report passed')
 def test_nonconstant_compound_main_transition_and_repeat(self):
  b,br,_=fixture('b',500);result=compare(self.base,self.request,b,br,self.native)
  self.assertEqual(result['status'],'modern_input_mixer_match');self.assertEqual(len(result['firstCoverage']['eligibleSteps']),241);self.assertTrue(result['firstCoverage']['mainTransitionSteps'])
 def test_explicit_appearance_exclusion_and_post_idle(self):
  a,ar,n=fixture(appearance=True);b,br,_=fixture('b',500,True);result=compare(a,ar,b,br,n)
  self.assertEqual(result['status'],'modern_input_mixer_match');self.assertTrue(result['firstCoverage']['excludedAppearanceSteps']);self.assertIn(240,result['firstCoverage']['eligibleSteps'])
 def test_clocks_rawweights_and_stale_next(self):
  self.bad(lambda r:r['frames'][18]['modernInputMixer']['inputs'][1].update(sampleSeconds=.111))
  self.bad(lambda r:r['frames'][18]['modernInputMixer']['inputs'][0].update(rawWeight=.5))
  self.bad(lambda r:r['frames'][18]['modernInputMixer'].update(inputWeights=[.5,.5]))
  r=copy.deepcopy(self.base);r['frames'][240]['modern']['nextClips']=copy.deepcopy(r['frames'][240]['modern']['currentClips'])
  checks=Checks();validate_mixer(r,self.request,self.native,checks,'stale');self.assertFalse(checks.failures)
 def test_repeat_and_native_matrix_checks_not_labels(self):
  self.bad(lambda r:r['frames'][40]['modernInputMixer']['firstPose']['prefabRootModels']['Root_M/base/body'][0].__setitem__(3,1))
  self.bad(lambda r:r['frames'][40]['modernInputMixer']['repeatedPose']['locals']['Root_M/base/body']['position'].__setitem__(0,1))
 def test_identity_cleanup_source_and_frame_gaps(self):
  self.bad(lambda r:r['frames'].pop(4));self.bad(lambda r:r['cleanup'].update(modernInputMixerClean=False))
  self.bad(lambda r:r['frames'][4]['modernInputMixer'].update(samplerRootInstanceId=r['frames'][4]['modern']['rootInstanceId']))
  self.bad(lambda r:r['frames'][4]['modernInputMixer'].update(sourcePrefabAfterSha256='0'*64))
  self.bad(lambda r:r['frames'][4]['modernInputMixer'].update(resetCount=1))
 def test_cannot_skip_difficult_main_transition(self):
  self.bad(lambda r:r['frames'][18]['modernInputMixer'].update(eligibleMainComparison=False,scope='appearance_contribution_excluded_no_native_mixed_equivalence'))
 def test_no_controllers_receivers_or_normalization(self):
  self.bad(lambda r:r['modernInputMixer']['identity'].update(normalizeWeights=True))
  self.bad(lambda r:r['frames'][40]['modernInputMixer'].update(runtimeControllerAssigned=True))
 def test_honest_numerical_mismatch_keeps_all_frames(self):
  r=copy.deepcopy(self.base);sample=r['frames'][40]['modernInputMixer'];locals_={p:np.array(v['matrix']) for p,v in sample['firstPose']['locals'].items()}
  locals_['Root_M'][0,3]+=.01
  sample['firstPose']=make_pose(locals_,sample['firstPose']);sample['repeatedPose']=copy.deepcopy(sample['firstPose'])
  sample.update(mainMaximumError=.01,mainComparisonPassed=False)
  r['modernInputMixer'].update(failedMainFrames=1,maximumMainError=.01,reportedNumericalComparisonPassed=False)
  b,br,_=fixture('b',500);result=compare(r,self.request,b,br,self.native)
  self.assertEqual(result['status'],'modern_input_mixer_mismatch');self.assertEqual(len(result['firstCoverage']['eligibleSteps']),241);self.assertEqual(result['firstCoverage']['failedMainFrames'],1)
 def test_clip_identity_cannot_change_with_matching_sample(self):
  def change(r):
   f=r['frames'][40];f['modern']['currentClips'][0]['instanceId']+=999;f['modernInputMixer']['inputs'][0]['clipInstanceId']+=999
  self.bad(change)
 def test_unpinned_manifest_rejected(self):
  with tempfile.TemporaryDirectory() as temp:
   p=Path(temp)/'manifest.json';p.write_text(json.dumps(dict(schema='kraken-modern-input-mixer-v1',evidence={})))
   with self.assertRaises(ValueError):run(p)
if __name__=='__main__':unittest.main()
