import argparse
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
PIPELINE = HERE.parent
for directory in (str(HERE), str(PIPELINE)):
    if directory not in sys.path:
        sys.path.insert(0, directory)

import run_kraken_production_campaign as campaign
from test_verify_kraken_production_adapter import BINARIES, campaign_fixture


class KrakenProductionRunnerTests(unittest.TestCase):
    def test_ordinary_attack_requires_exact_no_focus_confirmation(self):
        target = {'photonId': -1, 'turnIndex': 0}
        campaign.ordinary_attack_result({'ok': True, 'result': {
            'committed': 'Attack', 'target': target, 'whoseTurnWas': 'player'}}, target)
        for changed in (
                {'committed': 'Attack(focus)', 'target': target, 'whoseTurnWas': 'player'},
                {'committed': 'KillSingle', 'target': target, 'whoseTurnWas': 'player'},
                {'committed': 'Attack', 'target': {'photonId': -1, 'turnIndex': 1},
                 'whoseTurnWas': 'player'}):
            with self.assertRaises(RuntimeError):
                campaign.ordinary_attack_result({'ok': True, 'result': changed}, target)

    def test_assemble_requires_and_verifies_two_distinct_sessions(self):
        _, reports = campaign_fixture()
        reports[0]['session'] = 'a' * 32
        reports[1]['session'] = 'b' * 32
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            death = root / 'death-source.json'
            victory = root / 'victory-source.json'
            receipt = root / 'receipt-source.json'
            death.write_text(json.dumps(reports[0]))
            victory.write_text(json.dumps(reports[1]))
            receipt.write_text(json.dumps({'status': 'VERIFIED_COMPLETE', 'new': {
                'BepInEx/plugins/FTKModFramework.dll': {'sha256': BINARIES['framework']},
                'BepInEx/plugins/FtkRuntimeModelTest.dll': {'sha256': BINARIES['runtimeHelper']},
                'BepInEx/plugins/FtkRuntimeModelTestContent.dll': {'sha256': BINARIES['runtimeContent']},
            }}))
            output = root / 'campaign'
            args = argparse.Namespace(death_report=death, victory_report=victory,
                deployment_receipt=receipt, campaign=output / 'campaign.json',
                verification=output / 'verification.json')
            result = campaign.assemble(args)
            self.assertEqual(result['status'], 'production_observation_campaign_satisfied')
            self.assertEqual(json.loads(args.verification.read_text())['reportCount'], 2)
            manifest = json.loads(args.campaign.read_text())
            for descriptor in manifest['reports']:
                path = output / descriptor['path']
                self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), descriptor['sha256'])

    def test_assemble_rejects_same_helper_session(self):
        _, reports = campaign_fixture()
        reports[0]['session'] = reports[1]['session'] = 'a' * 32
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            paths = []
            for name, report in zip(('death.json', 'victory.json'), reports):
                path = root / name
                path.write_text(json.dumps(report))
                paths.append(path)
            receipt = root / 'receipt.json'
            receipt.write_text('{}')
            args = argparse.Namespace(death_report=paths[0], victory_report=paths[1],
                deployment_receipt=receipt, campaign=root / 'out' / 'campaign.json',
                verification=root / 'out' / 'verification.json')
            with self.assertRaises(RuntimeError):
                campaign.assemble(args)

    def test_active_campaign_rejected_before_any_initialization(self):
        with self.assertRaisesRegex(ValueError, 'retired'):
            campaign.KrakenRun(argparse.Namespace())
        source = (HERE / 'run_kraken_production_campaign.py').read_text()
        self.assertNotIn("self.action(", source)


if __name__ == '__main__':
    unittest.main()
