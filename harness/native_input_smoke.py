#!/usr/bin/env python3
"""Exercise native input in an already launched, isolated FTK test copy.

Requires a fresh title screen, English labels, a focused game, and an empty test
save namespace. It never launches a game or touches another process. Evidence
contains runtime UI/game state and belongs outside version control.
"""
import argparse
import json
from pathlib import Path
import time
import urllib.request
import uuid


# Protocol 3 deliberately rejects every command from the retired dispatch table.
RETIRED_ACTIONS = (
    'advance',
    'advance_room',
    'attack',
    'auto_combat',
    'auto_combat_turn',
    'choose_ability',
    'clear_dungeon',
    'cleared_room',
    'combat_status',
    'combat_turn',
    'dismiss_dialog',
    'dismiss_message',
    'dungeon_debug',
    'dungeon_encounter',
    'dungeon_regen',
    'dungeon_scroll_complete',
    'end_turn',
    'engage',
    'enter_dungeon',
    'enter_tile',
    'equip_item',
    'force_clear',
    'force_victory',
    'force_win',
    'list_adventures',
    'marketplace_ui',
    'move_to',
    'quest_advance',
    'quest_info',
    'resolve_turn',
    'select_choice',
    'session_debug',
    'set_focus',
    'set_target',
    'show_endgame',
    'snap_party',
    'snap_to',
    'start_run',
    'use_item',
    'win_combat',
)


class Trial:
    def __init__(self, url, output):
        self.url = url.rstrip("/")
        self.output = output
        output.mkdir(parents=True, exist_ok=False)
        self.checks = []
        self.last_id = None
        self.last_steps = None

    def request(self, path, payload=None):
        data = None if payload is None else json.dumps(payload).encode()
        request = urllib.request.Request(self.url + path, data=data,
                                         headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(request, timeout=12) as response:
            result = json.load(response)
        with (self.output / "protocol.jsonl").open("a") as stream:
            stream.write(json.dumps({"path": path, "request": payload, "response": result}) + "\n")
        return result

    def act(self, action, args=None):
        return self.request("/action", {"action": action, "args": args or {}})

    def check(self, condition, name):
        if not condition:
            raise AssertionError(name)
        self.checks.append(name)
        (self.output / "checks.json").write_text(json.dumps(self.checks, indent=2))
        print("PASS", name, flush=True)

    def wait_ui(self, predicate, timeout=45):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            ui = self.request("/ui")
            if predicate(ui):
                return ui
            time.sleep(.1)
        raise TimeoutError("UI condition did not become true")

    @staticmethod
    def controls(ui, label):
        return [c for c in ui["controls"] if label in c["labels"] and c["interactable"] and c["centerHit"]]

    def wait_label(self, label):
        return self.wait_ui(lambda ui: len(self.controls(ui, label)) == 1)

    def sequence(self, steps):
        self.last_id = uuid.uuid4().hex
        self.last_steps = steps
        reply = self.act("native_input", {"requestId": self.last_id, "steps": steps})
        if not reply.get("ok"):
            raise AssertionError(reply)
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            reply = self.request("/input")
            if not reply.get("ok"):
                raise AssertionError(reply)
            if reply["result"]["state"] == "completed":
                return reply
            time.sleep(.05)
        raise TimeoutError("native sequence did not complete")

    def click(self, label):
        ui = self.wait_label(label)
        c = self.controls(ui, label)[0]
        return self.sequence([{"x": c["x"], "y": c["y"], "buttons": [0], "frames": 2}, {"frames": 3}])

    def key(self, key):
        return self.sequence([{"keys": [key], "frames": 2}, {"frames": 3}])

    def capture(self, name):
        (self.output / (name + ".json")).write_text(json.dumps(self.request("/ui"), indent=2))
        with urllib.request.urlopen(self.url + "/screenshot", timeout=12) as response:
            (self.output / (name + ".png")).write_bytes(response.read())

    def run(self, start_run):
        health = self.request("/health")
        status = self.request("/input")["result"]
        self.check(health.get("protocolVersion") == 3 and status["available"], "native hooks available")
        (self.output / "identity.json").write_text(json.dumps({"health": health, "input": status}, indent=2))
        for action in RETIRED_ACTIONS:
            rejected = self.act(action)
            self.check(rejected.get("ok") is False and "unsupported action" in rejected.get("error", ""),
                       "retired action rejected: " + action)
        ui = self.wait_ui(lambda u: self.controls(u, "Understood") or self.controls(u, "New Game"))
        if self.controls(ui, "Understood"):
            self.click("Understood")
        self.wait_label("New Game")
        self.check(status["hooks"].get("framework", 0) > 0, "framework UI hooks available")
        self.click("Mods")
        self.click("Browse")
        self.click("Search mods")
        self.sequence([{"text": "NativeInput", "frames": 2}, {"frames": 3}])
        self.wait_ui(lambda u: any(t["text"].startswith("Search: NativeInput\n") for t in u["texts"]))
        self.check(True, "framework inputString search consumes text once")
        self.key("Backspace")
        self.wait_ui(lambda u: any(t["text"].startswith("Search: NativeInpu\n") for t in u["texts"]))
        self.check(True, "framework inputString receives Backspace edge")
        self.capture("mods-search")
        self.click("Back")
        self.click("Back to title")
        self.wait_label("New Game")
        self.click("Options")
        click_id, click_steps = self.last_id, self.last_steps
        self.wait_label("Audio")
        self.check(True, "native pointer click opens Options")
        self.key("Escape")
        self.wait_label("New Game")
        self.check(True, "mapped Escape closes Options")
        duplicate = self.act("native_input", {"requestId": click_id, "steps": click_steps})
        self.check(duplicate.get("duplicate") and duplicate["result"]["state"] == "completed", "duplicate returns original completed receipt")
        self.wait_label("New Game")
        self.check(True, "duplicate does not replay click")
        invalid = self.act("native_input", {"requestId": uuid.uuid4().hex, "steps": [{"frames": 0}]})
        self.check(not invalid["ok"], "invalid plan rejected before execution")

        ui = self.wait_label("Options")
        c = self.controls(ui, "Options")[0]
        hold = self.act("native_input", {"requestId": uuid.uuid4().hex,
            "steps": [{"x": c["x"], "y": c["y"], "buttons": [0], "frames": 120}]})
        self.check(hold["ok"], "held pointer accepted")
        time.sleep(.1)
        busy = self.act("native_input", {"requestId": uuid.uuid4().hex, "steps": [{"frames": 1}]})
        self.check(not busy["ok"], "overlapping sequence rejected")
        self.act("input_cancel")
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            cancelled = self.request("/input")
            if cancelled["result"]["state"] == "cancelled":
                break
            time.sleep(.05)
        self.check(cancelled["result"]["state"] == "cancelled", "cancel reaches neutral terminal state")
        self.wait_label("New Game")
        self.check(True, "cancelled press does not click Options")
        self.click("Options")
        self.wait_label("Audio")
        self.check(True, "fresh click works after cancellation")
        self.key("Escape")
        self.wait_label("New Game")
        self.check(self.act("prepare_offline")["ok"], "offline title configuration")
        self.click("New Game")
        before = self.wait_label("For the King")
        row = self.controls(before, "For the King")[0]
        self.sequence([{"x": row["x"], "y": row["y"], "scroll": -3, "frames": 2}, {"frames": 3}])
        after = self.wait_label("For the King")
        self.check(abs(self.controls(after, "For the King")[0]["y"] - row["y"]) > 1, "native wheel scroll moves campaign list")
        bars = [c for c in after["controls"] if c["type"] == "Scrollbar" and c["centerHit"]]
        self.check(len(bars) == 1, "campaign scrollbar observed")
        bar = bars[0]
        self.sequence([{"x": bar["x"], "y": bar["y"], "buttons": [0], "frames": 2},
                       {"x": bar["x"], "y": bar["y"] + 20, "buttons": [0], "frames": 3},
                       {"x": bar["x"], "y": bar["y"] + 70, "buttons": [0], "frames": 4}, {"frames": 3}])
        after_drag = self.request("/ui")
        new_bar = next(c for c in after_drag["controls"] if c["id"] == bar["id"])
        self.check(abs(new_bar["value"] - bar["value"]) > .01, "native drag changes scrollbar value")
        self.capture("campaign-input")
        if not start_run:
            self.click("Back")
            return
        self.click("For the King")
        self.click("Create Game")
        def name_buttons(ui):
            return [c for c in ui["controls"] if "/createTarget0/" in c["path"]
                    and c["path"].endswith("/NameInput/Button") and c["centerHit"] and c["interactable"]]
        ui = self.wait_ui(lambda u: len(name_buttons(u)) == 1)
        name_button = name_buttons(ui)[0]
        self.sequence([{"x": name_button["x"], "y": name_button["y"], "buttons": [0], "frames": 2}, {"frames": 3}])
        name = "Input " + uuid.uuid4().hex[:8]
        self.sequence([{"keys": ["LeftControl", "A"], "frames": 2},
                       {"text": name, "frames": 2},
                       {"keys": ["Return"], "frames": 2}, {"frames": 3}])
        self.wait_label(name)
        self.check(True, "native text entry and submit update character name")
        self.capture("named-party")
        self.click("Start")
        deadline = time.monotonic() + 60
        while time.monotonic() < deadline:
            state = self.request("/state")
            if state["inSession"]:
                break
            time.sleep(.2)
        self.check(state["inSession"] and state["singlePlayer"], "native start reaches single-player session")
        self.capture("adventure-start")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:8777")
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--start-run", action="store_true", help="Create a disposable test adventure through native UI")
    args = parser.parse_args()
    Trial(args.url, args.output).run(args.start_run)


if __name__ == "__main__":
    main()
