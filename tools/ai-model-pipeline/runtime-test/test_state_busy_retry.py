"""Mocked HTTP transport; no game calls."""
import io
import json
import unittest
import urllib.error
from types import SimpleNamespace
from unittest.mock import Mock, patch
from run_case import Runner

BUSY={'error':'state read timed out (main thread busy)'}
def failure(body=BUSY,code=500):
    raw=body if isinstance(body,bytes) else json.dumps(body).encode()
    return urllib.error.HTTPError('http://test/state',code,'test',{},io.BytesIO(raw))
def runner():
    r=object.__new__(Runner);r.a=SimpleNamespace(port=8788,operation_timeout=2);r.check_inputs=Mock();r.log=Mock();return r

class BusyTests(unittest.TestCase):
    def test_explicit_busy_then_success_rechecks_inputs_and_logs(self):
        r=runner()
        with patch('run_case.urllib.request.urlopen',side_effect=[failure(),io.BytesIO(b'{"party":[]}')]) as transport,patch('run_case.time.sleep'):
            self.assertEqual(r.state(),{'party':[]})
        self.assertEqual(transport.call_count,2);self.assertEqual(r.check_inputs.call_count,2)
        errors=[c.args[1] for c in r.log.call_args_list if c.args[0]=='http-state-error']
        self.assertEqual(len(errors),1);self.assertTrue(errors[0]['busyRetryEligible'])
    def test_other_error_malformed_extra_fields_and_other_status_never_retry(self):
        for err in [failure({'error':'state read failed: boom'}),failure(b'not JSON'),failure(dict(BUSY,ok=False)),failure(code=503)]:
            with self.subTest(error=err),patch('run_case.urllib.request.urlopen',side_effect=err) as transport:
                with self.assertRaises(urllib.error.HTTPError):runner().state()
                self.assertEqual(transport.call_count,1)
    def test_nonstate_or_payload_never_retry(self):
        for path,payload in [('/action',{'action':'start_run'}),('/state',{}),('/other',None)]:
            with patch('run_case.urllib.request.urlopen',side_effect=failure()) as transport:
                with self.assertRaises(urllib.error.HTTPError):runner().http(path,payload)
                self.assertEqual(transport.call_count,1)
    def test_uncertain_action_timeout_never_retry(self):
        with patch('run_case.urllib.request.urlopen',side_effect=TimeoutError('uncertain')) as transport:
            with self.assertRaises(TimeoutError):runner().action('enter_dungeon')
            self.assertEqual(transport.call_count,1)
    def test_fixed_deadline_stops_busy(self):
        with patch('run_case.time.monotonic',side_effect=[0,0,2.1,2.1]),patch('run_case.urllib.request.urlopen',side_effect=failure()) as transport:
            with self.assertRaises(TimeoutError):runner().state()
            self.assertEqual(transport.call_count,1)
    def test_changed_input_stops_before_next_observation(self):
        r=runner();r.check_inputs.side_effect=[None,ValueError('changed binary')]
        with patch('run_case.urllib.request.urlopen',side_effect=failure()) as transport,patch('run_case.time.sleep'):
            with self.assertRaises(ValueError):r.state()
            self.assertEqual(transport.call_count,1)
if __name__=='__main__':unittest.main()
