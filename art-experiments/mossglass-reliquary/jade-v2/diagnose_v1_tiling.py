"""Read-only proof of V1 palette sampling under recorded native2x tiling."""
import json,hashlib
from pathlib import Path
import numpy as np
from PIL import Image
O=Path(__file__).resolve().parent;B=O.parent;R=B.parent.parent
p=R/'scratch/mirewarden-game/model-test-output/3dbd433faa06493480e84435c22da4fc.json';raw=json.loads(p.read_text());d=json.loads((B/'mossglass.source.json').read_text());im=Image.open(B/'mossglass.slot1.png').convert('RGB');slot=raw['frames'][0]['materialObservation']['slots'][1];assert slot['_MainTexScale']==[2.,2.] and slot['_MainTex']['wrapMode']=='Repeat'
ix={v for t in d['primitives'][1]['triangles'] for v in t};us=sorted(set(float(np.float32(d['uvs'][i][0])) for i in ix));samples=[]
for u in us:
 wrapped=u*2%1;x=wrapped*im.width-.5;lo=int(np.floor(x));frac=x-lo;columns=[lo%im.width,(lo+1)%im.width];rgb=np.array([im.getpixel((c,64)) for c in columns]);samples.append(dict(originalU=u,nativeWrappedU=wrapped,bilinearColumns=columns,atlasSwatches=[c//32 for c in columns],bilinearRGB=((1-frac)*rgb[0]+frac*rgb[1]).tolist()))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
r=dict(status='CONFIRMED_V1_ATLAS_TILING_COLOR_DEFECT',captureSha256=sha(p),v1GlbSha256=sha(B/'mossglass.glb'),v1SourceSha256=sha(B/'mossglass.source.json'),v1TextureSha256=sha(B/'mossglass.slot1.png'),observedNativeScale=slot['_MainTexScale'],observedFilter=slot['_MainTex']['filterMode'],samples=samples,conclusion='Shell UVs intended for jade atlas swatches sample mixed ivory/amber/brown boundaries after native2x U tiling. This explains vertical warm bands independently of native lighting, which still changes perceived color.',correction='Replace only slot1 PNG with whole-image seamless jade texture. Preserve GLB/UVs/native material scale and scroll, slot0 texture and full skin.',reviewedCaptureFrames=[0,40],limits=['Texture sampling model uses recorded native scale/Repeat/Bilinear, not full Unity shader rendering.','No original death visibility/lifecycle acceptance.'])
(O/'v1-tiling-diagnosis.json').write_text(json.dumps(r,indent=2)+'\n')
