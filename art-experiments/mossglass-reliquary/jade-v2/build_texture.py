"""Original seamless jade-only tile; retain native Repeat, 2x scale and UV scroll."""
import math,json,hashlib
from pathlib import Path
from PIL import Image
O=Path(__file__).resolve().parent
im=Image.new('RGB',(384,128))
for y in range(im.height):
 for x in range(im.width):
  u=x/im.width;v=y/im.height
  vein=(.5+.5*math.cos(math.tau*(v+0.055*math.sin(math.tau*u))))**14
  cloud=.5+.5*math.sin(math.tau*u)*math.sin(math.tau*v)
  im.putpixel((x,y),(int(30+22*cloud+36*vein),int(66+24*cloud+40*vein),int(46+17*cloud+27*vein)))
im.save(O/'mossglass_jade_v2.slot1.png')
assert all(g>r and g>b for r,g,b in im.getdata())
result=dict(status='PASS_JADE_ONLY_REPEATABLE_TILE',dimensions=im.size,formula='Periodic sine/cosine in U and V; whole texture contains jade colors only, no atlas regions.',pixelCount=im.width*im.height,allPixelsGreenDominant=True,nativeTextureScale=[2,2],nativeScrollRate=[0,.2],preserved=['V1 GLB bytes','Original source UVs','Native material scale and phase','Slot0 PNG'],limits=['Native lighting/emission still changes perceived color.','Combat-scale original pattern visibility pending live test.'])
(O/'texture-audit.json').write_text(json.dumps(result,indent=2)+'\n')
print(hashlib.sha256((O/'mossglass_jade_v2.slot1.png').read_bytes()).hexdigest())
