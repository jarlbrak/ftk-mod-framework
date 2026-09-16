"""Composed-arm routing/identity checks; full geometry math remains in original skin tests."""
import copy,unittest
from unittest.mock import patch
from verify_kraken_composed_skin import inspect_composed,COMPOSED

class Tests(unittest.TestCase):
 def inputs(self):
  arm={'id':'a'*32,'session':'f'*32,'op':'kraken-skin-probe-arm','scenario':'appear','manifestSha256':'h','variant':'gloamfin-v1','endpointPolicy':COMPOSED}
  report={'endpointPolicy':COMPOSED,'originalSkinProbe':{'identity':{'armRequest':copy.deepcopy(arm)}}}
  request={'endpointPolicy':COMPOSED};result={'endpointPolicy':COMPOSED};return report,request,arm,result,{'variant':'gloamfin-v1'},None,{},None
 def test_exact_arm_join_before_explicit_derived_geometry_view(self):
  data=self.inputs();before=copy.deepcopy(data)
  with patch('verify_kraken_composed_skin.inspect_run',return_value={'checked':'geometry'}) as inspector:
   self.assertEqual(inspect_composed(*data),{'checked':'geometry'})
   args=inspector.call_args.args;self.assertNotIn('endpointPolicy',args[2]);self.assertEqual(args[0]['originalSkinProbe']['identity']['armRequest'],args[2]);self.assertNotIn('endpointPolicy',args[3])
  self.assertEqual(data,before)
 def test_wrong_or_unpinned_arm_policy_never_reaches_geometry(self):
  for index in (0,1,2,3):
   data=list(self.inputs());data[index]['endpointPolicy']='main-appearance-local-v1'
   with patch('verify_kraken_composed_skin.inspect_run') as inspector:
    with self.assertRaises(ValueError):inspect_composed(*data)
    inspector.assert_not_called()
 def test_actual_arm_identity_and_original_asset_required(self):
  data=list(self.inputs());data[0]['originalSkinProbe']['identity']['armRequest']['id']='b'*32
  with self.assertRaises(ValueError):inspect_composed(*data)
  data=list(self.inputs());data[4]['variant']=None
  with self.assertRaises(ValueError):inspect_composed(*data)
if __name__=='__main__':unittest.main()
