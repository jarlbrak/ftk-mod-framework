#!/usr/bin/env python3
"""Run or assemble the exact two-owner Gloamfin Kraken production campaign.

The run command operates one already staged encounter in one fresh game process.
It never launches, deploys, stages, retries an uncertain action, clears UI, or
assigns a visual verdict. The assemble command is offline and requires reports
from two distinct helper sessions.
"""
import argparse
import hashlib
import json
import math
import re
import sys
import time
from pathlib import Path

PIPELINE = Path(__file__).resolve().parents[1]
if str(PIPELINE) not in sys.path:
    sys.path.insert(0, str(PIPELINE))

from run_case import Runner, digest, exact_combat, read, validate_inventory
from verify_kraken_production_adapter import (
    BINARY_KEYS, CAMPAIGN_SCHEMA, GLB_SHA256, MANIFEST_SHA256, PNG_SHA256,
    REGISTERED_ENEMY, load_campaign, verify_campaign,
)


ROLES = {
    'native-combat-death': {'companion': None, 'expectedTerminal': 'DEATH'},
    'enemy-victory-terminal': {'companion': 'ogreA', 'expectedTerminal': 'VICTORY'},
}


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def safe_output(path, parent=None):
    path = path.absolute()
    if any(part.is_symlink() for part in (path, *path.parents)):
        raise RuntimeError('Symlink output ancestry refused: ' + str(path))
    if parent is not None:
        parent = parent.resolve(strict=True)
        require(path.parent.resolve(strict=True) == parent,
                'Output must be a direct child of the selected campaign directory')
    require(not path.exists(), 'Refusing to overwrite output: ' + str(path))
    return path


def exact_json(path, value):
    with path.open('x') as stream:
        json.dump(value, stream, indent=2, allow_nan=False)
        stream.write('\n')


def copy_exact(source, destination):
    source = source.resolve(strict=True)
    safe_output(destination)
    before = source.stat()
    data = source.read_bytes()
    after = source.stat()
    require((before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) ==
            (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns),
            'Source changed during copy: ' + str(source))
    destination.write_bytes(data)
    require(digest(destination) == hashlib.sha256(data).hexdigest(),
            'Copied bytes changed: ' + str(destination))


def target(state, enemy_fid):
    return next((row for row in (state.get('combat') or {}).get('enemies') or []
                 if row.get('fid') == enemy_fid), None)


def ordinary_attack_result(result, enemy_fid):
    body = result.get('result', result)
    require(result.get('ok') is True and body.get('committed') == 'Attack' and
            body.get('target') == enemy_fid and body.get('whoseTurnWas') == 'player',
            'Bridge did not confirm one ordinary no-focus attack on the pinned Kraken')


class KrakenRun(Runner):
    def __init__(self, args):
        args.enemy = REGISTERED_ENEMY
        args.class_key = None
        args.mode = 'kraken-production-' + args.role
        super().__init__(args)
        self.role = args.role
        self.companion = ROLES[self.role]['companion']
        self.report = safe_output(args.report)
        self.deadline = time.monotonic() + args.run_timeout
        self.attacks = 0
        self.initial_party = None
        self.enemy_fid = None

    def remaining(self):
        remaining = self.deadline - time.monotonic()
        if remaining <= 0:
            raise TimeoutError('Campaign run timeout; no action retried')
        return remaining

    def claim(self):
        new_run = self.root / 'model-test-output' / ('new-run-session-' + self.session + '.json')
        require(new_run.is_file() and read(new_run).get('session') == self.session,
                'Stage this encounter with run_case.py new-run in the current process first')
        claim = self.root / 'model-test-output' / ('kraken-production-session-' + self.session + '.json')
        with claim.open('x') as stream:
            json.dump({'session': self.session, 'role': self.role, 'case': self.case,
                       'journal': str(self.journal)}, stream)
        self.log('exclusive-campaign-claim', {'path': str(claim), 'role': self.role,
            'rule': 'One production campaign role per fresh helper session; uncertain work is never resumed.'})

    def quiet(self, state, allow_terminal=False):
        require(state.get('singlePlayer') is True and state.get('inSession') is True,
                'Living single-player session changed')
        signals = state.get('signals') or {}
        if not allow_terminal:
            require(signals.get('modalOpen') is False and signals.get('choiceOpen') is False,
                    'Unexpected UI boundary; no automatic dismissal')

    def preflight(self):
        self.check_inputs()
        state = self.state()
        self.quiet(state)
        require(exact_combat(state, REGISTERED_ENEMY, self.companion),
                'Exact staged Kraken campaign room and native hero turn required')
        require(len(state.get('party') or []) == 1, 'Campaign requires exactly one hero')
        hero = state['party'][0]
        if self.role == 'native-combat-death':
            require(hero.get('hp') == hero.get('maxHp') and hero.get('maxHp', 0) >= 999,
                    'Death role requires the documented fortified 999-HP hero')
        else:
            require(hero.get('hp') == hero.get('maxHp') and 0 < hero.get('maxHp', 0) <= 64,
                    'Victory role requires an unfortified hero with at most 64 HP')
        self.initial_party = [row.get('fid') for row in state['party']]
        enemies = (state.get('combat') or {}).get('enemies') or []
        kraken = next(row for row in enemies if row.get('type') == REGISTERED_ENEMY)
        self.enemy_fid = kraken.get('fid')
        require(isinstance(self.enemy_fid, dict) and kraken.get('alive') is True and kraken.get('hp') == 324,
                'Campaign requires the exact live 324-HP registered Kraken')
        require(self.profile.get('baseEnemy') == 'krakenHead' and
                self.profile.get('resourcePrefab') == 'enkrakenhead' and
                self.profile.get('combatProfile') ==
                '03a4df71df2226d0ec711f80c3f1f3e0922c211ff18d3efb797a6501efe423fa',
                'Registered Kraken production profile changed')
        require(self.profile_asset_pins == {
            'gloamfin.glb': GLB_SHA256, 'gloamfin_basecolor.png': PNG_SHA256},
            'Exact Gloamfin model assets changed')
        inventory = self.helper('inventory', {'scope': 'enemies'})
        matched = validate_inventory(inventory, self.profile,
                                     visual_scale=getattr(self, 'visual_scale_contract', None))
        require(len(matched) == 1, 'Kraken profile must select exactly one renderer')
        selected = [row for row in inventory.get('renderers') or []
                    if row.get('instanceId') == matched[0]['rendererInstanceId']]
        require(len(selected) == 1 and selected[0].get('ownerInstanceId') == matched[0]['ownerInstanceId'],
                'Exact live Kraken renderer identity is unavailable')
        renderer = selected[0]
        require(type(renderer.get('celInstanceId')) is int and renderer['celInstanceId'] != 0,
                'Exact live Kraken CEL identity is unavailable')
        self.log('campaign-preflight', {'role': self.role, 'state': state,
            'renderer': renderer, 'matchedAssignments': matched})
        return renderer

    def arm(self, renderer):
        result = self.helper('kraken-production-adapter-arm', {
            'enemyDummyInstanceId': renderer['ownerInstanceId'],
            'celInstanceId': renderer['celInstanceId'],
            'rendererInstanceId': renderer['instanceId'],
            'manifestSha256': MANIFEST_SHA256,
            'campaignRun': self.role,
        })
        require(result.get('status') == 'passive-production-adapter-observer-armed' and
                (result.get('route') or {}).get('campaignRun') == self.role,
                'Kraken observer did not arm for the exact campaign role')
        return result

    def validate_identity(self, state, allow_terminal=False):
        self.quiet(state, allow_terminal)
        require([row.get('fid') for row in state.get('party') or []] == self.initial_party,
                'Hero identity changed during campaign')
        enemies = (state.get('combat') or {}).get('enemies') or []
        allowed = {REGISTERED_ENEMY} if self.companion is None else {REGISTERED_ENEMY, self.companion}
        require({row.get('type') for row in enemies} <= allowed,
                'Unexpected enemy appeared during campaign')
        current = target(state, self.enemy_fid)
        if current is not None:
            require(current.get('type') == REGISTERED_ENEMY, 'Pinned Kraken FID changed owner')
        return current

    def attack(self, state):
        require(self.attacks < self.a.max_attacks, 'Ordinary attack limit reached before terminal state')
        combat = state.get('combat') or {}
        require(combat.get('heroTurnReady') is True and
                (combat.get('whoseTurn') or {}).get('isPlayer') is True,
                'Ordinary attack requested outside the native hero turn')
        self.check_inputs()
        result = self.action('combat_turn', {
            'cheat': 'None', 'focus': False, 'targetFid': self.enemy_fid})
        ordinary_attack_result(result, self.enemy_fid)
        self.attacks += 1
        self.log('ordinary-attack-accepted', {'number': self.attacks, 'result': result})

    def drive_to_terminal(self):
        attacked_once = False
        while self.remaining() > 0:
            state = self.state()
            current = self.validate_identity(state, allow_terminal=True)
            party_dead = bool(state.get('party')) and all(row.get('hp', 1) <= 0 for row in state['party'])
            signals = state.get('signals') or {}
            if self.role == 'native-combat-death':
                if current is None or current.get('alive') is False or current.get('hp', 1) <= 0:
                    self.log('native-terminal-observed', {'role': self.role, 'state': state})
                    return
                require(not party_dead, 'Hero died before the Kraken')
                combat = state.get('combat') or {}
                if combat.get('heroTurnReady') is True and (combat.get('whoseTurn') or {}).get('isPlayer') is True:
                    self.attack(state)
            else:
                if party_dead and signals.get('allDead') is True:
                    require(current is not None and current.get('alive') is True and current.get('hp', 0) > 0,
                            'Kraken did not survive the native party loss')
                    self.log('native-terminal-observed', {'role': self.role, 'state': state})
                    return
                require(current is not None and current.get('alive') is True,
                        'Kraken died before native enemy victory')
                combat = state.get('combat') or {}
                if combat.get('heroTurnReady') is True and (combat.get('whoseTurn') or {}).get('isPlayer') is True:
                    require(not attacked_once, 'Hero turn returned before party loss; no second action issued')
                    self.attack(state)
                    attacked_once = True
            time.sleep(min(.25, self.remaining()))

    def collect_report(self):
        while self.remaining() > 0:
            result = self.helper('kraken-production-adapter-state')
            require(result.get('schema') == 'ftkmf.kraken-production-adapter-observation.v1' and
                    result.get('error') is None and result.get('overflow') is False and
                    result.get('identityDrift') is False,
                    'Kraken observer reported failure or identity drift')
            if result.get('status') == 'natural-teardown-observed':
                require((result.get('route') or {}).get('campaignRun') == self.role,
                        'Final observer report role changed')
                exact_json(self.report, result)
                return result
            time.sleep(min(.5, self.remaining()))

    def run_campaign(self):
        self.claim()
        renderer = self.preflight()
        self.arm(renderer)
        self.drive_to_terminal()
        report = self.collect_report()
        result = {'status': 'natural-teardown-observed', 'campaignRun': self.role,
                  'session': self.session, 'report': str(self.report),
                  'sha256': digest(self.report), 'journal': str(self.journal),
                  'ordinaryHeroAttacks': self.attacks,
                  'attackWindows': len(report.get('attackWindows') or []),
                  'cleanupCounts': report.get('cleanupCounts')}
        self.log('campaign-result', result)
        return result


def contained_copy(source, campaign_dir, label):
    source = source.resolve(strict=True)
    sha = digest(source)
    if source.parent == campaign_dir:
        return source, sha
    destination = campaign_dir / (label + '-' + sha[:16] + source.suffix)
    copy_exact(source, destination)
    return destination, sha


def assemble(args):
    campaign = safe_output(args.campaign)
    campaign.parent.mkdir(parents=True, exist_ok=True)
    campaign_dir = campaign.parent.resolve(strict=True)
    verification = safe_output(args.verification, campaign_dir)
    death_source = args.death_report.resolve(strict=True)
    victory_source = args.victory_report.resolve(strict=True)
    reports = [read(death_source), read(victory_source)]
    require([row.get('route', {}).get('campaignRun') for row in reports] ==
            ['native-combat-death', 'enemy-victory-terminal'],
            'Report arguments do not match their exact campaign roles')
    sessions = [row.get('session') for row in reports]
    require(all(isinstance(value, str) and re.fullmatch('[a-f0-9]{32}', value) for value in sessions) and
            len(set(sessions)) == 2, 'Two distinct fresh helper sessions are required')
    pin_names = {'framework': 'framework', 'runtimeHelper': 'runtimeHelper', 'runtimeContent': 'runtimeContent'}
    expected = {}
    for campaign_key, report_key in pin_names.items():
        values = [(row.get('pinsAtArm') or {}).get(report_key, {}).get('assemblyFileSha256') for row in reports]
        require(len(set(values)) == 1 and isinstance(values[0], str) and re.fullmatch('[a-f0-9]{64}', values[0]),
                'Report binary pins differ: ' + campaign_key)
        expected[campaign_key] = values[0]
    require(set(expected) == BINARY_KEYS, 'Exact campaign binary pins are required')
    receipt_path, receipt_sha = contained_copy(args.deployment_receipt, campaign_dir, 'deployment')
    death_path, death_sha = contained_copy(death_source, campaign_dir, 'native-combat-death')
    victory_path, victory_sha = contained_copy(victory_source, campaign_dir, 'enemy-victory-terminal')
    document = {'schema': CAMPAIGN_SCHEMA, 'expectedBinaries': expected,
        'deploymentReceipt': {'path': receipt_path.name, 'sha256': receipt_sha},
        'reports': [
            {'campaignRun': 'native-combat-death', 'path': death_path.name, 'sha256': death_sha},
            {'campaignRun': 'enemy-victory-terminal', 'path': victory_path.name, 'sha256': victory_sha},
        ]}
    exact_json(campaign, document)
    loaded, loaded_reports = load_campaign(campaign)
    result = verify_campaign(loaded, loaded_reports)
    exact_json(verification, result)
    return {'status': result['status'], 'campaign': str(campaign),
            'campaignSha256': digest(campaign), 'verification': str(verification),
            'verificationSha256': digest(verification), 'sessions': sessions}


def parser():
    value = argparse.ArgumentParser(description=__doc__)
    commands = value.add_subparsers(dest='command', required=True)
    run = commands.add_parser('run', help='Drive one exact already staged role to its native terminal state')
    run.add_argument('--root', type=Path, required=True)
    run.add_argument('--port', type=int, required=True)
    run.add_argument('--role', choices=tuple(ROLES), required=True)
    run.add_argument('--report', type=Path, required=True)
    run.add_argument('--operation-timeout', type=float, default=40)
    run.add_argument('--run-timeout', type=float, default=1200)
    run.add_argument('--max-attacks', type=int, default=128)
    package = commands.add_parser('assemble', help='Build and verify one two-session offline campaign')
    package.add_argument('--death-report', type=Path, required=True)
    package.add_argument('--victory-report', type=Path, required=True)
    package.add_argument('--deployment-receipt', type=Path, required=True)
    package.add_argument('--campaign', type=Path, required=True)
    package.add_argument('--verification', type=Path, required=True)
    return value


def main():
    value = parser()
    args = value.parse_args()
    if args.command == 'run':
        if (not 1 <= args.port <= 65535 or not math.isfinite(args.operation_timeout) or
                not 1 <= args.operation_timeout <= 120 or not math.isfinite(args.run_timeout) or
                not 30 <= args.run_timeout <= 3600 or not 1 <= args.max_attacks <= 256):
            value.error('Invalid port, timeout, or attack bound')
        runner = None
        try:
            runner = KrakenRun(args)
            result = runner.run_campaign()
        except Exception as error:
            result = {'status': 'stopped', 'error': str(error),
                      'note': 'No uncertain action was retried. Preserve the journal and start a fresh process.'}
            if runner is not None:
                runner.log('campaign-stopped', result)
                result['journal'] = str(runner.journal)
            print(json.dumps(result, indent=2))
            raise SystemExit(1)
    else:
        try:
            result = assemble(args)
        except Exception as error:
            print(json.dumps({'status': 'stopped', 'error': str(error)}, indent=2))
            raise SystemExit(1)
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
