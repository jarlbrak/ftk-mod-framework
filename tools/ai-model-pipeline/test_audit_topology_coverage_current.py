"""Current-build regression coverage for explicit non-enemy topology ownership."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import unittest

import audit_topology_coverage as audit
from local_inputs import skip_without_local_inputs

ROOT = Path(__file__).resolve().parents[2]
CANDIDATES = ROOT / 'docs/model-skeleton-candidates.json'
MAPPING = ROOT / 'scratch/enemy-rig-mapping-reproducible.json'
RIGS = ROOT / 'scratch/rig-candidate-classification.json'
RESOURCE_PATHS = ROOT / 'scratch/unresolved-resource-paths.json'
RESOURCES = ROOT / 'scratch/resource-enemy-base-preflight.json'
CATALOG = ROOT / 'scratch/mirewarden-game/model-test-profiles.json'
INDEX = ROOT / 'docs/model-runtime-validation.json'
OWNERSHIP = ROOT / 'docs/evidence/nonenemy-topology-ownership-v1/findings.json'


def read(path: Path):
    return json.loads(path.read_text())


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


@skip_without_local_inputs(MAPPING, RIGS, RESOURCE_PATHS, RESOURCES, CATALOG)
class CurrentTopologyOwnershipTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.candidates = read(CANDIDATES)
        cls.mapping = read(MAPPING)
        cls.rigs = read(RIGS)
        cls.resource_paths = read(RESOURCE_PATHS)
        cls.resources = read(RESOURCES)
        cls.catalog = read(CATALOG)
        cls.index = read(INDEX)
        cls.ownership = read(OWNERSHIP)
        cls.groups = {row['topology_fingerprint']: row for row in cls.candidates['candidate_families']}
        cls.classifications = {row['topologyGroup']: row for row in cls.ownership['classifications']}
        cls.unmapped = {row['renderer_path_id']: row for row in cls.mapping['unmapped_renderers']}
        cls.rig_rows = {row['renderer_path_id']: row for row in cls.rigs['renderers']}

    def test_pinned_sources_and_exact_group_membership(self) -> None:
        for name, reference in self.ownership['inputs'].items():
            path = ROOT / name
            self.assertTrue(path.is_file(), name)
            self.assertEqual(digest(path), reference['sha256'], name)
        self.assertEqual(set(self.classifications), {
            '6a28ac3cf4523c24', '4082b6c4e777f922', '45c7a9b9fb730195',
            '85c742f628ea6d37', '94dbc18f21284ec2', '7313dc39dab041dd',
        })
        for group, classification in self.classifications.items():
            self.assertEqual(classification['sourceRendererIds'], self.groups[group]['renderer_path_ids'])
            for renderer_id in classification['sourceRendererIds']:
                self.assertEqual(self.unmapped[renderer_id]['enemy_db_mapping_status'], 'not_referenced_by_any_nonnull_vanilla_enemy_row')

    def test_player_skinset_and_unbound_player_monk_routes_stay_separate(self) -> None:
        for group in ('4082b6c4e777f922', '85c742f628ea6d37'):
            classification = self.classifications[group]
            self.assertEqual(classification['category'], 'player_avatar_plus_unbound_resource')
            for renderer_id in classification['skinsetRendererIds']:
                row = self.rig_rows[renderer_id]
                self.assertEqual(row['positive_role'], 'player_skinset_avatar')
                self.assertTrue(row['skinset_avatar_references'])
            for resource in classification['unboundResourceRenderers']:
                reference = next(row for row in self.resource_paths['references'] if row['resource_load_path'] == resource['loadPath'])
                self.assertEqual(reference['object_id'], resource['gameObjectId'])
                for renderer_id in resource['rendererIds']:
                    row = self.rig_rows[renderer_id]
                    self.assertEqual(row['positive_role'], 'unresolved_character_prefab')
                    self.assertFalse(row['skinset_avatar_references'])
        seven = self.classifications['45c7a9b9fb730195']
        self.assertEqual(seven['category'], 'player_avatar')
        self.assertTrue(all(self.rig_rows[row]['positive_role'] == 'player_skinset_avatar' for row in seven['sourceRendererIds']))

    def test_resource_variants_and_empty_mayor_keep_their_actual_routes(self) -> None:
        for group, resource_path in (('6a28ac3cf4523c24', 'enkrakenhead'), ('7313dc39dab041dd', 'enbaseysnake')):
            classification = self.classifications[group]
            self.assertEqual(classification['category'], 'resource_prefab_variant')
            resource = classification['resource']
            row = next(row for row in self.resources['rows'] if row['resource_load_path'] == resource_path)
            self.assertEqual(row['renderer_id'], resource['rendererId'])
            self.assertEqual(row['renderer_path'], resource['rendererPath'])
            self.assertFalse(row['topology_matches_any_base_renderer'])
        mayor = self.classifications['94dbc18f21284ec2']
        finding = read(ROOT / mayor['evidence'])
        self.assertEqual(mayor['category'], 'unsupported_empty')
        self.assertEqual(finding['rendererId'], 121018)
        self.assertEqual(finding['meshPointer']['m_PathID'], 0)
        self.assertEqual(finding['matchingNativeSkinsetAvatarRows'], [])

    def test_current_plan_has_no_unresolved_ownership_and_keeps_routes_separate(self) -> None:
        inputs = [CANDIDATES, MAPPING, INDEX, RESOURCES, CATALOG, OWNERSHIP]
        plan = audit.build_plan(ROOT, self.candidates, self.mapping, self.index, self.resources,
                                self.catalog, self.ownership, inputs)
        summary = plan['summary']
        self.assertEqual(summary['topologyGroups'], 49)
        self.assertEqual(summary['unresolvedOwnershipGroups'], 0)
        self.assertEqual(summary['groupsWithAnyIndexedOriginalDirectEnemyEvidence'], 43)
        self.assertEqual(summary['groupsWithAnyIndexedPlayerEvidence'], 3)
        self.assertEqual(summary['groupsWithExplicitOwnershipClassification'], 6)
        self.assertEqual(summary['explicitPlayerAvatarGroups'], 3)
        self.assertEqual(summary['explicitUnsupportedGroups'], 1)
        groups = {row['topologyGroup']: row for row in plan['groups']}
        self.assertEqual(groups['6a28ac3cf4523c24']['nativeDirectEnemyPairs'], 0)
        self.assertEqual(groups['6a28ac3cf4523c24']['nativeResourcePrefabPairs'], 1)
        self.assertEqual(groups['7313dc39dab041dd']['nativeDirectEnemyPairs'], 0)
        self.assertEqual(groups['7313dc39dab041dd']['nativeResourcePrefabPairs'], 1)
        self.assertEqual(groups['7313dc39dab041dd']['status'], 'resource_prefab_evidence_separate_not_native_enemy_acceptance')
        for group in ('4082b6c4e777f922', '85c742f628ea6d37'):
            evidence = groups[group]['playerEvidence']
            self.assertEqual(len(evidence), 3)
            self.assertEqual({row['indexPointer'] for row in evidence}, {
                '/original_player_models/0', '/original_player_models/1', '/original_player_models/5',
            })
            self.assertEqual({row['status'] for row in evidence}, {
                'live_binding_motion_equipment_cleanup_and_native_progression_observed_preview_pending',
                'supplemental_native_character_creation_preview_and_idle_observed',
                'canonical_native_player_route_reviewed',
            })
        fish_hair = groups['45c7a9b9fb730195']['playerEvidence']
        self.assertEqual(len(fish_hair), 4)
        self.assertEqual({row['indexPointer'] for row in fish_hair}, {
            '/original_player_models/2', '/original_player_models/3', '/original_player_models/4',
            '/original_player_models/6',
        })
        wildbloom = next(row for row in fish_hair
                          if row['profile'] == 'ftkmf_modeltest_player_wildbloom_herbalist_female')
        self.assertEqual(wildbloom['indexPointer'], '/original_player_models/2')
        self.assertEqual(wildbloom['skinset'], 'herbalist_Female')
        self.assertEqual(wildbloom['rendererPaths'], ['hairBottom'])
        self.assertEqual(wildbloom['status'], 'native_character_creation_preview_and_idle_observed')
        canonical_wildbloom = next(row for row in fish_hair
                                   if row['indexPointer'] == '/original_player_models/6')
        self.assertEqual(canonical_wildbloom['status'], 'canonical_native_player_route_reviewed')
        tideglass_v1 = next(row for row in fish_hair
                            if row['indexPointer'] == '/original_player_models/3')
        self.assertEqual(tideglass_v1['profile'], 'ftkmf_modeltest_player_tideglass_fishsmith')
        self.assertEqual(tideglass_v1['skinset'], 'blacksmith_Fish')
        self.assertEqual(tideglass_v1['rendererPaths'], ['hairBottom'])
        self.assertEqual(tideglass_v1['status'], 'native_character_creation_preview_and_idle_observed_art_revision_pending')
        tideglass_v2 = next(row for row in fish_hair
                            if row['indexPointer'] == '/original_player_models/4')
        self.assertEqual(tideglass_v2['profile'], 'ftkmf_modeltest_player_tideglass_fishsmith')
        self.assertEqual(tideglass_v2['skinset'], 'blacksmith_Fish')
        self.assertEqual(tideglass_v2['rendererPaths'], ['hairBottom'])
        self.assertEqual(tideglass_v2['status'], 'native_character_creation_preview_and_idle_observed_v2_preview_art_accepted')

    def test_markdown_surfaces_one_exact_unrecorded_route_without_family_promotion(self) -> None:
        inputs = [CANDIDATES, MAPPING, INDEX, RESOURCES, CATALOG, OWNERSHIP]
        plan = audit.build_plan(ROOT, self.candidates, self.mapping, self.index, self.resources,
                                self.catalog, self.ownership, inputs)
        acid_blob_b = next(group for group in plan['groups'] if group['topologyGroup'] == '82d606d9b5a0d2e1')
        self.assertEqual(acid_blob_b['exactSourcePairsWithIndexedOriginalEvidence'], 2)
        self.assertEqual(acid_blob_b['remainingExactSourcePairRepresentatives'], [])
        kraken = next(group for group in plan['groups'] if group['topologyGroup'] == '6a28ac3cf4523c24')
        self.assertEqual(kraken['exactSourcePairsWithIndexedOriginalEvidence'], 1)
        self.assertEqual(kraken['remainingExactSourcePairRepresentatives'], [])
        rendered = audit.markdown(plan)
        self.assertIn('## First unrecorded exact route per incomplete topology', rendered)
        self.assertIn('`clamB`', rendered)
        self.assertNotIn('|`enkrakenhead`|', rendered)
        self.assertIn('does not make sibling routes covered', rendered)


if __name__ == '__main__':
    unittest.main()
