from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).parent
SOURCE = (ROOT / 'NativeCuffEnvelope.cs').read_text()
ROUTE = (ROOT / 'ItemVisualObservation.cs').read_text()


class CuffEnvelopeTests(unittest.TestCase):
    def test_reducer_executes_triangle_clipping_and_boundaries(self):
        with tempfile.TemporaryDirectory() as directory:
            target = Path(directory)
            (target / 'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
            (target / 'CuffEnvelopeMath.cs').write_text((ROOT / 'CuffEnvelopeMath.cs').read_text())
            (target / 'Program.cs').write_text('''using System;
class Program {
 static CuffEnvelopeMath.Point P(double t,double u,double v,double w=1){return new CuffEnvelopeMath.Point(t,u,v,w);}
 static void Check(bool ok){if(!ok)throw new Exception("Support invariant failed");}
 static bool Near(double a,double b){return Math.Abs(a-b)<1e-9;}
 static void Main(){
  var x=new CuffEnvelopeMath();
  // No source vertex lies inside the middle band; clipping must still measure it.
  x.AddTriangle(P(0,-.1,0),P(1,.1,0),P(1,0,.1));
  Check(x.bands[1].triangles==1);
  Check(Near(x.bands[1].min[0],-.1+(.1/3)) && Near(x.bands[1].max[0],-.1+.2*2/3));
  Check(Near(x.bands[1].max[1],.1*2/3));
  Check(Near(x.bands[1].max[2],(-.1+.2*2/3)/Math.Sqrt(2)));
  var low=new CuffEnvelopeMath();low.AddTriangle(P(0,0,0,0),P(1,.1,0,.24),P(1,0,.1,0));
  Check(low.rejectedArmTriangles==1 && low.bands[1].triangles==0);
  var weighted=new CuffEnvelopeMath();weighted.AddTriangle(P(0,0,0,0),P(1,.1,0,1),P(1,0,.1,1));
  Check(weighted.bands[0].triangles==1 && Near(weighted.bands[0].max[0],.1/3));
  var radial=new CuffEnvelopeMath();radial.AddTriangle(P(.1,.36,0),P(.2,.36,.01),P(.3,.36,0));
  Check(radial.bands[0].triangles==0 && radial.bands[0].rejectedRadialTriangles==1);
  var edge=new CuffEnvelopeMath();edge.AddTriangle(P(.1,.35,0),P(.2,.34,.01),P(.3,.34,0));
  Check(edge.bands[0].triangles==1 && Near(edge.bands[0].max[0],.35));
  var outside=new CuffEnvelopeMath();outside.AddTriangle(P(2,0,0),P(3,.1,0),P(3,0,.1));
  Check(outside.bands[0].triangles==0 && outside.bands[2].triangles==0);
  bool rejected=false;try{x.AddTriangle(P(double.NaN,0,0),P(0,0,0),P(1,0,0));}catch(InvalidOperationException){rejected=true;}Check(rejected);
  Check(!CuffEnvelopeMath.UseScaleFreeBake(.0001,.0002,.0001));
  Check(!CuffEnvelopeMath.UseScaleFreeBake(.0001,.1,.1));
  Check(CuffEnvelopeMath.UseScaleFreeBake(.3,.0002,.3));
  foreach(double[] bad in new[]{new[]{.1,.2,.1},new[]{.001,.001,.003},new[]{double.NaN,0.0,0.0},new[]{-1.0,0.0,0.0}}){
   bool fail=false;try{CuffEnvelopeMath.UseScaleFreeBake(bad[0],bad[1],bad[2]);}catch(InvalidOperationException){fail=true;}Check(fail);
  }
  Console.WriteLine("Cuff reducer: 8 geometry and 7 mapping cases passed");
 }
}''')
            result = subprocess.run(['dotnet', 'run', '--project', str(target / 'Test.csproj'), '-c', 'Release'], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

    def test_exact_package_inventory_and_native_prefab_guards(self):
        for guard in ('RequirePackageFitIsolation();', 'packageFitUncertain', 'ExactPackageFitHero(', 'PackageFitItems(Str(command,"configSha256"))',
                      'm_ContainerBody.GetOne()!=gear.itemId', 'ReferenceEquals(template.sharedMesh,body.sharedMesh)',
                      'body.transform.parent!=clone.transform', 'source.vertexCount>100000', 'totalIndices>1200000',
                      'Time.frameCount!=frame', 'inventory.m_InventoryOwner!=hero', 'inventory.m_IsUpdatePaperDoll'):
            self.assertIn(guard, SOURCE)
        self.assertIn('cuffProbe && (source != "inventory" || Str(command, "session") != sessionId', ROUTE)
        self.assertLess(ROUTE.index('cel.GetInstanceID() != cloneId'), ROUTE.index('NativeCuffEnvelope(command, hero, cel)'))

    def test_ephemeral_mesh_and_no_native_array_export_or_mutation(self):
        self.assertIn('finally {if(baked!=null)UnityEngine.Object.DestroyImmediate(baked);}', SOURCE)
        for forbidden in ('"vertices"', '"indices"', '"weights"', '"normals"', '"uv"', '.sharedMesh =', '.sharedMaterials =', '.bones =', '.enabled =', '.Render(', 'Input.Get', 'FTKInput.'):
            self.assertNotIn(forbidden, SOURCE)
        self.assertIn('body.BakeMesh(baked)', SOURCE)
        self.assertIn('unknown; exact native material slot identity only', SOURCE)

    def test_bake_mapping_is_verified_without_native_state_changes(self):
        for value in ('source.blendShapeCount!=0', 'influences!=4', 'source.bindposes', 'source.vertices',
                      'Math.Abs(sum-1)>0.001f', 'world.m03-=origin.x', 'UseScaleFreeBake(fullMax,rigidMax,gapMax)',
                      '"fullMatrixRms"', '"scaleFreeRms"', '"bakeMappingVerification"',
                      'ReferenceEquals(originalBones[elbow],nativeBones[nativeElbow])'):
            self.assertIn(value, SOURCE)
        for forbidden in ('body.transform.localScale =', 'body.bones =', 'body.sharedMesh =', '"nativeBindposes"'):
            self.assertNotIn(forbidden, SOURCE)

    def test_coordinate_contract_and_bounded_aggregate(self):
        for value in ('originalBones[elbow].localToWorldMatrix*bindposes[elbow]', 'skin.inverse',
                      'worldToOriginal*bakedToWorld', 'Vector3.Cross(axis,u)',
                      'Vector3.up-axis*Vector3.Dot(Vector3.up,axis)', 'for(int i=0;i<3;i++)',
                      '"diagonalPlus","diagonalMinus"', 'band.triangles==0?null'):
            self.assertIn(value, SOURCE)


if __name__ == '__main__':
    unittest.main()
