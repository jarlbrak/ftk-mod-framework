#!/usr/bin/env python3
"""Seal original exported-asset review sheets and native-size comparisons."""
import hashlib,json
from pathlib import Path
from PIL import Image,ImageDraw
OUT=Path(__file__).resolve().parent/'apparel-review'
m=json.loads((OUT/'manifest.json').read_text())
bands=['street','burglar','guild','masterwork','locksmith','nightblade','wayfarer']
products=[]
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
for row in m['views']:
 p=OUT/row['file'];im=Image.open(p).convert('RGBA');Image.frombytes('RGBA',im.size,im.tobytes()).save(p);row['sha256']=sha(p)
 for entry in row['inputs']:assert sha(OUT.parent/entry['file'])==entry['sha256'],entry['file']
for sex in ['male','female']:
 for view in ['front','three-quarter','side','back']:
  sheet=Image.new('RGB',(1792,310),'#23292E');draw=ImageDraw.Draw(sheet)
  for j,band in enumerate(bands):
   row=next(r for r in m['views'] if r['band']==band and r['sex']==sex and r['view']==view)
   im=Image.open(OUT/row['file']).convert('RGBA').resize((256,256),Image.Resampling.LANCZOS)
   sheet.paste(im,(j*256,20),im);draw.text((j*256+12,281),band.title(),fill='#E1D9C9')
  filename='compare-'+sex+'-'+view+'.png';sheet.save(OUT/filename)
  products.append({'file':filename,'sha256':sha(OUT/filename),'scope':sex+' '+view+', same source framing; actual exports, native body omitted'})
 # Entire source frame is scaled uniformly so all tiers stay comparable. The
 # resulting garment heights are recorded; a missing native head is not invented.
 sheet=Image.new('RGB',(1400,410),'#23292E');draw=ImageDraw.Draw(sheet)
 for j,band in enumerate(bands):
  row=next(r for r in m['views'] if r['band']==band and r['sex']==sex and r['view']=='three-quarter')
  im=Image.open(OUT/row['file']).convert('RGBA')
  for height,y in [(96,30),(160,163)]:
   # Common authored outfit height is about 1.7 in a 2.4-unit frame.
   span=round(height*2.4/1.7);thumb=im.resize((span,span),Image.Resampling.LANCZOS)
   sheet.paste(thumb,(j*200+(200-span)//2,y),thumb)
  draw.text((j*200+12,378),band.title(),fill='#E1D9C9')
 filename='combat-scale-'+sex+'.png';sheet.save(OUT/filename)
 products.append({'file':filename,'sha256':sha(OUT/filename),'scope':'Two uniformly scaled rows, approximately 96 and 160 pixels apparel height. Native head/hair/body omitted.'})
m['lighting']={'worldColor':[.20,.20,.20],'areaLights':[{'delta':[-2,-4,5],'energyFactor':420},{'delta':[3,-2,2],'energyFactor':230},{'delta':[2,3,4],'energyFactor':340}],'positionFormula':'target + delta * 2.40','powerFormula':'energyFactor * 2.40 * 2.40','areaSize':7.2,'materialRoughness':.82,'materialMetallic':0}
m['derivedSheets']=products
m['finalizer']='finalize_apparel_review.py';m['finalizerSha256']=sha(Path(__file__))
(OUT/'manifest.json').write_text(json.dumps(m,indent=2)+'\n')
print('Sealed',len(m['views']),'renders and',len(products),'comparison sheets')
