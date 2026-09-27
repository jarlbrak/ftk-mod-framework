from pathlib import Path
import json,time,sys
root=Path(sys.argv[2]).resolve()
if root.parent.name != 'scratch':
 raise RuntimeError('Only an explicitly authorized isolated game under scratch is supported')
def command(prefix,data,timeout=20):
 p=root/(prefix+'-command.tmp');p.write_text(json.dumps(data));p.replace(root/(prefix+'-command.json'))
 result=root/(prefix+'-'+data['id']+'.json');deadline=time.monotonic()+timeout
 while time.monotonic()<deadline:
  try:
   d=json.loads(result.read_text())
   if d.get('status','complete')=='complete':return d
  except (ValueError,FileNotFoundError):pass
  time.sleep(.3)
 raise RuntimeError(str(result))
label=sys.argv[1]
if 'baseline' in label:command('resource',{'id':label+'-track','action':'portrait-track'})
results=[]
for op,extra in [('create',{}),('run',{'enemy':'None','count':50}),('state',{}),('run',{'enemy':'banditA','count':1}),('state',{}),('run',{'enemy':'banditA','count':20}),('state',{}),('run',{'enemy':'None','count':1}),('state',{}),('destroy',{}),('state',{})]:
 ident=label+'-'+str(len(results))+'-'+op
 r=command('resource',{'id':ident,'action':'portrait-'+op,**extra},90);results.append(r)
 live=[t for t in r.get('newTextures',[]) if not t['destroyed']];print(ident,'live textures',len(live),'allocatedMiB',r.get('unityAllocated',0)/2**20,'image',r.get('gpuSha256'),flush=True)
 Path('scratch/'+label+'-summary.json').write_text(json.dumps(results,indent=2));time.sleep(2)
if 'baseline' in label:
 print(command('resource',{'id':label+'-cleanup','action':'portrait-cleanup-baseline'}),flush=True)
