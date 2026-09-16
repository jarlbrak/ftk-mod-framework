"""Offline tests for evidence boundaries; synthetic data, no game assets or Unity required."""
import unittest
from summarize_capture import summarize_ragdoll


def frame(enabled=True,ragdoll=True,kinematic=False,velocity=0.,angular=0.,position=0.):
    return {'animator':{'enabled':enabled},'ragdoll':{'m_DoRagdoll':ragdoll,'rigidbodyCount':1,'truncated':False,
        'rigidbodies':[{'instanceId':7,'path':'Root/Chest','active':True,'isKinematic':kinematic,
                       'position':[position,0,0],'velocity':[velocity,0,0],'angularVelocity':[angular,0,0]}]}}


class EvidenceTests(unittest.TestCase):
    def test_legacy_capture_never_infers_physics_from_renderer_or_frozen_animator(self):
        result=summarize_ragdoll([{'enabled':False,'animator':{'layers':[{'normalizedTime':.165}]}}]*3,[0,1,2])
        self.assertEqual(result['animatorEnabled']['status'],'not_recorded')
        self.assertEqual(result['nativeRagdollFlag']['status'],'not_recorded')
        self.assertEqual(result['rigidbodies']['status'],'not_recorded')

    def test_animator_handoff_motion_and_quiet_tail_are_separate_observations(self):
        frames=[frame(kinematic=True),frame(enabled=False,velocity=2,position=1),
                frame(enabled=False,position=2),frame(enabled=False,position=2)]
        result=summarize_ragdoll(frames,[0,.5,1,1.5])
        self.assertEqual(result['animatorEnabled']['transitions'],[{'fromFrame':0,'toFrame':1,'from':True,'to':False,'gameSeconds':.5}])
        body=result['rigidbodies']['bodies'][0]
        self.assertEqual(body['activeNonKinematicSampleCount'],3)
        self.assertEqual(body['sampledWorldPositionPathLength'],1)
        self.assertEqual(body['maxRecordedLinearSpeed'],2)
        self.assertEqual(body['trailingLowVelocitySamples'],2)
        self.assertEqual(body['trailingLowVelocityGameSeconds'],.5)
        self.assertNotIn('ragdollPassed',result)

    def test_no_motion_or_elapsed_quiet_claim_from_paused_samples(self):
        result=summarize_ragdoll([frame(position=i) for i in range(3)],[0,0,0])
        body=result['rigidbodies']['bodies'][0]
        self.assertEqual(body['consecutiveDynamicPositionPairs'],0)
        self.assertIsNone(body['sampledWorldPositionPathLength'])
        self.assertEqual(body['trailingLowVelocityGameSeconds'],0)

    def test_missing_samples_break_transitions_and_tail(self):
        result=summarize_ragdoll([frame(enabled=True),{},frame(enabled=False)],[0,1,2])
        self.assertEqual(result['animatorEnabled']['transitions'],[])
        self.assertEqual(result['rigidbodies']['bodies'][0]['trailingLowVelocitySamples'],1)
        result=summarize_ragdoll([frame(),{}],[0,1])
        self.assertEqual(result['rigidbodies']['bodies'][0]['trailingLowVelocitySamples'],0)

    def test_missing_angular_velocity_cannot_support_quiet_tail(self):
        f=frame();del f['ragdoll']['rigidbodies'][0]['angularVelocity']
        body=summarize_ragdoll([f,f],[0,1])['rigidbodies']['bodies'][0]
        self.assertEqual(body['trailingLowVelocitySamples'],0)
        self.assertIsNone(body['maxRecordedAngularSpeed'])

    def test_kinematic_motion_is_not_dynamic_motion(self):
        body=summarize_ragdoll([frame(kinematic=True,velocity=9,position=0),frame(kinematic=True,velocity=9,position=4)],[0,1])['rigidbodies']['bodies'][0]
        self.assertEqual(body['activeNonKinematicSampleCount'],0)
        self.assertIsNone(body['maxRecordedLinearSpeed'])
        self.assertIsNone(body['sampledWorldPositionPathLength'])

    def test_nonfinite_velocity_and_duplicate_identity_reject(self):
        with self.assertRaisesRegex(ValueError,'Non-finite'):
            summarize_ragdoll([frame(velocity=float('nan'))],[0])
        f=frame();f['ragdoll']['rigidbodies']*=2
        with self.assertRaisesRegex(ValueError,'Duplicate'):
            summarize_ragdoll([f],[0])

    def test_truncation_is_retained_as_a_coverage_limit(self):
        f=frame();f['ragdoll']['truncated']=True;f['ragdoll']['rigidbodyCount']=130
        report=summarize_ragdoll([f],[0])['rigidbodies']
        self.assertEqual(report['truncatedFrames'],[0])
        self.assertEqual(report['bodyCounts'][0]['reportedTotal'],130)
        self.assertEqual(report['bodyCounts'][0]['recordedCount'],1)


if __name__=='__main__':unittest.main()
