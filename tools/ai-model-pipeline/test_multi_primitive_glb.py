"""Independent bounded multi-primitive checks using only original synthetic geometry."""
import copy,contextlib,hashlib,io,json,struct,tempfile,unittest
from pathlib import Path
import numpy as np
from export_ftk_glb import write_glb
from validate_glb import validate


def fixture():
    p=[[0,0,0],[1,0,0],[0,1,0],[0,0,1],[1,0,1],[0,1,1]]
    data=dict(positions=p,normals=[[0,0,1]]*6,uvs=[[.25,.5]]*3+[[.75,.5]]*3,joints=[[0,0,0,0]]*6,weights=[[1,0,0,0]]*6,triangles=[[0,1,2],[3,4,5]])
    ref=dict(bone_names=np.array(['root']),bindposes=np.array([np.eye(4)]),positions=np.array(p),normals=np.array(data['normals']),triangles=np.array(data['triangles']))
    return data,ref


def mutate(path,fn):
    raw=path.read_bytes();n=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+n]);binary=raw[28+n:];fn(doc);j=json.dumps(doc,separators=(',',':')).encode();j+=b' '*(-len(j)%4)
    path.write_bytes(struct.pack('<III',0x46546c67,2,28+len(j)+len(binary))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(binary),0x004e4942)+binary)

class MultiPrimitiveTests(unittest.TestCase):
    def test_valid_and_rejections(self):
        d,r=fixture();d['primitives']=[dict(triangles=[d['triangles'][0]]),dict(triangles=[d['triangles'][1]])]
        with tempfile.TemporaryDirectory()as t,contextlib.redirect_stdout(io.StringIO()):
            p=Path(t)/'two.glb';write_glb(p,d,r);report=validate(p,r);self.assertEqual(report['triangles_per_primitive'],[1,1]);original=p.read_bytes()
            cases=[lambda x:x['meshes'][0]['primitives'][1]['attributes'].update(POSITION=1),lambda x:x['meshes'][0]['primitives'][1].update(indices=x['meshes'][0]['primitives'][0]['indices']),lambda x:x['meshes'][0]['primitives'][1].update(material=0),lambda x:x['meshes'][0]['primitives'][1].update(mode=1),lambda x:x['meshes'][0]['primitives'][1].update(targets=[]),lambda x:x['accessors'][x['meshes'][0]['primitives'][1]['indices']].update(componentType=5125)]
            for change in cases:
                p.write_bytes(original);mutate(p,change)
                with self.assertRaises((AssertionError,ValueError,KeyError)):validate(p,r)
            for groups in [[],[dict(triangles=[])],[dict(triangles=[[0,1,2]])]*5,[dict(triangles=[[0,1,2]]),dict(triangles=[])]]:
                d['primitives']=groups
                with self.assertRaises(ValueError):write_glb(p,d,r)
    def test_legacy_writer_byte_regression(self):
        root=Path(__file__).resolve().parents[2]
        reference=root/'scratch/skeleton-audit/121328/reference.npz'
        if not reference.exists():self.skipTest('Local pre-change reference is not distributed')
        d=json.loads((root/'art-experiments/basilight-cockatrice/basilight.source.json').read_text())
        with tempfile.TemporaryDirectory()as t,contextlib.redirect_stdout(io.StringIO()):
            p=Path(t)/'legacy.glb';write_glb(p,d,np.load(reference))
            self.assertEqual(hashlib.sha256(p.read_bytes()).hexdigest(),'6efeaeff8c015cee0d80cfbdc06891186f5a574189b9c7a643fc5f6717be96d9')

if __name__=='__main__':unittest.main()
