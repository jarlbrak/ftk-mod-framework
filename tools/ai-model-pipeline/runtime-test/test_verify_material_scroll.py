import copy
import unittest
from verify_material_scroll import verify, f32


def fixture():
    slots = [{'slot': i, 'instanceId': 10+i, 'inCurrentLeaseResources': True,
              '_MainTex': {'instanceId': 20+i, 'inCurrentLeaseResources': True},
              '_MainTexScale': [1, 1], '_MainTexOffset': [0, 0]} for i in range(2)]
    base = {'ownerInstanceId': 1, 'celInstanceId': 2, 'rendererInstanceId': 3, 'enemy': 'test',
            'samplePhase': 'end-of-frame-before-png-readback', 'rendererEnabled': True,
            'rendererActive': True, 'slots': slots,
            'lease': {'leaseId': 4, 'acquired': True, 'applied': True},
            'leaseResourceInstanceIds': [10, 11, 20, 21],
            'geometry': {'submeshCount': 2, 'submeshes': [{'nativeMaterialSlot': i, 'indices': 3} for i in range(2)]},
            'scrollers': [{'instanceId': 5, 'textureName': '_MainTex', 'rate': [0, f32(.2)],
                          'materialIndex': 1, 'rendererInstanceId': 3, 'targetMaterialInstanceId': 11,
                          'enabled': True, 'active': True, 'propertySupported': True}]}
    frames = []
    phase = 0
    for i in range(4):
        obs = copy.deepcopy(base)
        phase = f32(phase + f32(f32(.2) * f32(.125)))
        obs.update(frame=i+100, gameTime=i*.125+10, deltaTime=.125)
        obs['slots'][1]['_MainTexOffset'] = [0, phase]
        obs['scrollers'][0].update(phase=[0, phase], currentPropertyOffset=[0, phase])
        frames.append({'materialObservation': obs})
    return {'ok': True, 'frames': frames}


class ScrollEvidenceTests(unittest.TestCase):
    def test_valid_variable_phase(self):
        self.assertEqual(verify(fixture())['frames'], 4)

    def rejected(self, mutate):
        raw = fixture()
        mutate(raw['frames'][2]['materialObservation'])
        with self.assertRaises(ValueError):
            verify(raw)

    def test_consistent_but_wrong_phase(self):
        def mutate(o):
            o['slots'][1]['_MainTexOffset'] = [0, 1]
            o['scrollers'][0].update(phase=[0, 1], currentPropertyOffset=[0, 1])
        self.rejected(mutate)

    def test_wrong_material_target(self):
        self.rejected(lambda o: o['scrollers'][0].update(targetMaterialInstanceId=10))

    def test_missing_owned_resource(self):
        self.rejected(lambda o: o['leaseResourceInstanceIds'].remove(21))

    def test_native_slot_also_scrolls(self):
        self.rejected(lambda o: o['slots'][0].update(_MainTexOffset=[0, .5]))

    def test_owner_changed(self):
        self.rejected(lambda o: o.update(ownerInstanceId=88))

    def test_repeated_time(self):
        self.rejected(lambda o: o.update(gameTime=10))

    def test_disabled_renderer_requires_other_fixture(self):
        self.rejected(lambda o: o.update(rendererEnabled=False))

    def test_second_writer_is_unsupported(self):
        self.rejected(lambda o: o['scrollers'].append(copy.deepcopy(o['scrollers'][0])))

    def test_empty_surface(self):
        raw = fixture()
        raw['frames'][0]['materialObservation']['geometry']['submeshes'][0]['indices'] = 0
        with self.assertRaises(ValueError):
            verify(raw)


if __name__ == '__main__':
    unittest.main()
