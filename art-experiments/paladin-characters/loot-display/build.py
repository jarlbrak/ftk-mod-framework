#!/usr/bin/env python3
"""Derive rigid shop-display art from existing original Paladin surfaces only."""
import argparse,hashlib,json,sys
from pathlib import Path
import numpy as np
OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[2]
sys.path.insert(0,str(ROOT/'tools/ai-model-pipeline'))
from export_ftk_glb import write_static_glb
SETS=['novice','oathkeeper','highward','mercy','censure','verdict']
EXCLUDED=('Arm plate','Gold elbow','Wrist cuff','Leg plate','Split tabard','Tabard gold hem','Knee shield','Thumb armor','Sabatons','Back neck','Novice articulated sleeves','Novice angular elbow','Novice wrist band','Novice mail leggings','Novice narrow split apron','Novice apron hem','Novice pointed knee plate','Novice thumb guard','Novice toe articulation','Novice rear collar','Knee articulation','Sabatons underlayer','Novice knee articulation','Highward segmented tasset')


def build(output):
 output.mkdir(parents=True,exist_ok=True);records=[];files={}
 for family in ['armor','boots']:
  for tier in SETS:
   key='paladin-'+tier+'-'+family+'-display'
   original='paladin-male-'+tier+'-armor' if family=='armor' else 'paladin-'+tier+'-boots'
   source_path=OUT.parent/(original+'.source.json');pieces_path=OUT.parent/(original+'.pieces.json')
   source=json.loads(source_path.read_text());pieces=json.loads(pieces_path.read_text());source_triangles=np.array(source['triangles'])
   selected=[p for p in pieces if family=='boots' or not p['name'].startswith(EXCLUDED)]
   data={k:[] for k in ['positions','normals','uvs','triangles']};outpieces=[]
   for piece in selected:
    start=piece['vertex_start'];end=start+piece['vertex_count'];base=len(data['positions'])
    for attribute in ['positions','normals','uvs']:data[attribute].extend(source[attribute][start:end])
    inside=(source_triangles>=start)&(source_triangles<end)
    assert not (inside.any(axis=1)&~inside.all(axis=1)).any(),piece['name']
    triangles=source_triangles[inside.all(axis=1)]
    triangle_start=len(data['triangles'])
    # Mirrored authored panels can reverse indices without rearranging vertex attributes.
    data['triangles'].extend((triangles-start+base).tolist())
    outpieces.append({'name':piece['name'],'vertex_start':base,'vertex_count':piece['vertex_count'],
                      'source_vertex_start':start,'source_vertex_count':piece['vertex_count'],
                      'triangle_start':triangle_start,'triangle_count':len(triangles)})
   points=np.array(data['positions']);center=(points.min(axis=0)+points.max(axis=0))/2
   scale=1.0/(points[:,1].max()-points[:,1].min())
   data['positions']=((points-center)*scale).tolist()
   (output/(key+'.source.json')).write_text(json.dumps(data,separators=(',',':'))+'\n')
   (output/(key+'.pieces.json')).write_text(json.dumps(outpieces,indent=2)+'\n')
   write_static_glb(output/(key+'.glb'),data)
   for suffix in ['.source.json','.pieces.json','.glb']:
    p=output/(key+suffix);files[p.name]=hashlib.sha256(p.read_bytes()).hexdigest()
   records.append({'key':key,'item':'paladin_'+family+'_'+tier,'family':family,'set':tier,'originalSource':source_path.relative_to(ROOT).as_posix(),'originalSourceSha256':hashlib.sha256(source_path.read_bytes()).hexdigest(),'originalPiecesSha256':hashlib.sha256(pieces_path.read_bytes()).hexdigest(),'selectedPieces':[p['name'] for p in selected],'originalCenter':center.tolist(),'uniformScale':float(scale),'vertices':len(data['positions']),'triangles':len(data['triangles'])})
 manifest={'schema':'ftkmf.paladin-original-loot-display.v1','generatorSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'provenance':'Rigid display versions of already-original Paladin cuirasses/pauldrons and paired plated boots. Native geometry never read. Selection and centering use original source surfaces only.','space':'Centered upright author frame, unit height, +Z front. Native loot attachment/camera fit require validation.','nativeBinding':{'armorTemplate':'armorHeavy1','rendererPath':'armorSplintVestDisplay','rendererMaterialSlots':1,'rootRotation':'Y180 before offscreen parenting; OffscreenCamera forces Y180','priorBootsTemplate':'bootsplayersmith has null loot prefab','bootsTemplate':'bootsHeavy3','bootsRendererPath':'bootsIronGreavesDisplay','bootsRendererMaterialSlots':1,'camera':'Fixed scale, no bounds normalization; live framing pending'},'textureSource':'art-experiments/paladin-characters/paladin-character-palette.png','assets':records,'files':files}
 (output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')


if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--output',type=Path,default=OUT);build(parser.parse_args().output)
