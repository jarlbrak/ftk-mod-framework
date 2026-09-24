"""Client protocol tests; no MCP installation or game required."""
import importlib.util
from pathlib import Path
import sys
import types
import unittest
from unittest.mock import patch


class FakeMcp:
    def __init__(self, name):
        self.registered = []

    def tool(self):
        def register(fn):
            self.registered.append(fn.__name__)
            return fn
        return register


fake = types.ModuleType("mcp.server.fastmcp")
fake.FastMCP = FakeMcp
spec = importlib.util.spec_from_file_location("ftk_server_test", Path(__file__).parents[1] / "ftk_mcp_server.py")
server = importlib.util.module_from_spec(spec)
with patch.dict(sys.modules, {"mcp.server.fastmcp": fake}):
    spec.loader.exec_module(server)


def status(state="completed", request_id="test"):
    return {"ok": True, "result": {"state": state, "requestId": request_id}}


class InputTests(unittest.TestCase):
    def test_public_surface_has_no_arbitrary_gameplay_action(self):
        self.assertEqual(set(server.mcp.registered), {
            "ftk_observe", "ftk_ui", "ftk_prepare_offline", "ftk_input",
            "ftk_input_status", "ftk_input_cancel", "ftk_wait_for", "ftk_screenshot"})
        self.assertFalse(hasattr(server, "ftk_act"))

    def test_offline_preparation_posts_only_configuration(self):
        with patch.object(server, "_action", return_value={"ok": True}) as action:
            self.assertTrue(server.ftk_prepare_offline()["ok"])
        action.assert_called_once_with("prepare_offline", {})

    def test_sequence_posts_once_and_waits_through_release(self):
        steps = [{"keys": ["Escape"], "frames": 2}, {"frames": 2}]
        with patch.object(server, "_action", return_value=status("queued")) as act, patch.object(
            server, "ftk_input_status", side_effect=[status("running"), status("releasing"), status()]
        ), patch.object(server.time, "sleep"):
            reply = server.ftk_input(steps, request_id="test")
        self.assertTrue(reply["ok"])
        act.assert_called_once_with("native_input", {"requestId": "test", "steps": steps})

    def test_completed_duplicate_returns_receipt_without_polling_latest(self):
        with patch.object(server, "_action", return_value=status()) as act, patch.object(server, "ftk_input_status") as poll:
            reply = server.ftk_input([{"frames": 1}], request_id="test")
        self.assertEqual(reply["result"]["state"], "completed")
        act.assert_called_once()
        poll.assert_not_called()

    def test_rejected_submit_is_not_polled_or_retried(self):
        with patch.object(server, "_action", return_value={"ok": False, "error": "busy"}) as act, patch.object(server, "ftk_input_status") as poll:
            reply = server.ftk_input([], request_id="test")
        self.assertFalse(reply["ok"])
        self.assertEqual(reply["requestId"], "test")
        act.assert_called_once()
        poll.assert_not_called()

    def test_transport_failure_keeps_id_for_safe_recovery(self):
        with patch.object(server, "_post_json", side_effect=OSError("closed")) as post:
            reply = server.ftk_input([{"frames": 2}], request_id="stable")
        self.assertFalse(reply["ok"])
        self.assertEqual(reply["requestId"], "stable")
        post.assert_called_once()

    def test_nonblocking_returns_generated_id(self):
        with patch.object(server, "_action", return_value=status("queued")) as act:
            reply = server.ftk_input([{"frames": 1}], wait=False)
        self.assertEqual(len(reply["requestId"]), 32)
        self.assertEqual(reply["requestId"], act.call_args.args[1]["requestId"])

    def test_runtime_failure_remains_failed(self):
        failure = {"ok": False, "error": "native input exceeded 15 seconds",
                   "result": {"requestId": "test", "state": "failed"}}
        with patch.object(server, "_action", return_value=status("queued")), patch.object(server, "ftk_input_status", return_value=failure):
            reply = server.ftk_input([{"frames": 120}], request_id="test")
        self.assertFalse(reply["ok"])
        self.assertEqual(reply["result"]["state"], "failed")

    def test_cancellation_waits_for_release_and_reports_cancelled(self):
        with patch.object(server, "_action", return_value=status("queued")), patch.object(
            server, "ftk_input_status", side_effect=[status("cancelling"), status("cancelled")]
        ), patch.object(server.time, "sleep"):
            reply = server.ftk_input([{"frames": 120}], request_id="test")
        self.assertEqual(reply["result"]["state"], "cancelled")

    def test_lost_ownership_fails_without_replay(self):
        with patch.object(server, "_action", return_value=status("queued")) as act, patch.object(server, "ftk_input_status", return_value=status(request_id="other")):
            reply = server.ftk_input([{"frames": 1}], request_id="test")
        self.assertFalse(reply["ok"])
        act.assert_called_once()

    def test_wait_timeout_does_not_cancel_or_resubmit(self):
        with patch.object(server, "_action", return_value=status("queued")) as act, patch.object(server.time, "monotonic", side_effect=[0, 2]):
            reply = server.ftk_input([{"frames": 120}], request_id="test", timeout_s=1)
        self.assertTrue(reply["timeout"])
        act.assert_called_once()

    def test_invalid_wait_bound_has_no_side_effect(self):
        with patch.object(server, "_action") as act:
            for bound in [0, -1, 61, float("nan"), float("inf"), "10"]:
                self.assertFalse(server.ftk_input([], timeout_s=bound)["ok"])
        act.assert_not_called()

    def test_observation_and_cancel_routes(self):
        with patch.object(server, "_get", return_value=(200, "application/json", b'{"controls":[]}')) as get:
            self.assertEqual(server.ftk_ui(), {"controls": []})
            get.assert_called_once_with("/ui", server.STATE_TIMEOUT)
        with patch.object(server, "_action", return_value=status("cancelled")) as act:
            server.ftk_input_cancel()
            act.assert_called_once_with("input_cancel", {})


if __name__ == "__main__":
    unittest.main()
