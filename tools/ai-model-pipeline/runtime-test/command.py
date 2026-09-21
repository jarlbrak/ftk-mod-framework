#!/usr/bin/env python3
"""Send one explicit request to an already running, isolated FTK model test plugin."""
import argparse
import json
from pathlib import Path
import time
import uuid

OPS = ('hero-damage-fixture', 'material-lifecycle-fixture', 'native-row-portrait-fixture', 'portrait-texture-capture', 'inventory', 'reload', 'capture', 'play', 'combat-trigger-capture', 'select-room', 'stage-enemy', 'fortify-party', 'return-to-title', 'quiet-tutorials', 'lease-test', 'stage-next-enemy', 'ready', 'fixture-state', 'collect-loot', 'equipment-inventory', 'equip-body', 'unequip-body', 'catalog-preflight', 'playercatalog-preflight', 'material-state', 'story-state', 'story-submit', 'native-create-character-preflight', 'native-create-character-input-state', 'native-create-character-screen', 'player-preview-state', 'lease-watch-preview', 'lease-watch', 'lease-watch-state', 'lease-watch-clear', 'kraken-sample-fixture', 'kraken-production-adapter-arm', 'kraken-production-adapter-state', 'kraken-production-adapter-clear')


OPS += ('native-party-start', 'native-party-class', 'guardian-state', 'guardian-damage-fixture')

OPS += ('town-stock-state',)
OPS += ('native-combat-focus',)
OPS += ('world-input-state',)
OPS += ('player-studio',)
OPS += ('custom-loot-fixture',)
OPS += ('preview-race',)
OPS += ('native-fight-trace',)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', required=True, type=Path)
    parser.add_argument('op', choices=OPS)
    parser.add_argument('--payload', type=Path, help='JSON object with operation fields; session/id/op are supplied here')
    parser.add_argument('--timeout', type=float, default=40)
    args = parser.parse_args()
    root = args.root.resolve(strict=True)
    if root.parent.name != 'scratch' or root != args.root.absolute():
        parser.error('--root must be an absolute, resolved game-copy directory directly under scratch')
    session = json.loads((root / 'model-test-session.json').read_text())['session']
    command = json.loads(args.payload.read_text()) if args.payload else {}
    if not isinstance(command, dict):
        parser.error('--payload must contain a JSON object')
    command.update(id=uuid.uuid4().hex, session=session, op=args.op)
    command_path = root / 'model-test-command.json'
    # Atomic rename ensures the game's main thread never reads a partial command.
    temporary = root / ('model-test-' + command['id'] + '.tmp')
    temporary.write_text(json.dumps(command))
    temporary.replace(command_path)
    result = root / 'model-test-output' / (command['id'] + '.json')
    deadline = time.monotonic() + args.timeout
    while time.monotonic() < deadline:
        if result.exists():
            data = json.loads(result.read_text())
            print(result)
            print(json.dumps(data, indent=2))
            raise SystemExit(0 if data.get('ok') else 1)
        time.sleep(.2)
    raise SystemExit('Timed out. Request may still be running; inspect the result file before retrying: ' + str(result))


if __name__ == '__main__':
    main()
