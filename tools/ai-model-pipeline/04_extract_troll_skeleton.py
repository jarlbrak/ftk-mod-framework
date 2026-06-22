import UnityPy, os, json
import numpy as np
DATA="/Users/tbrack/Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data"
env=UnityPy.load(os.path.join(DATA,"resources.assets"))
# index by path_id
byid={}
for o in env.objects: byid[o.path_id]=o
def read(o):
    try: return o.read()
    except: return None
# find enTroll01 mesh path_id
troll_mesh_id=None
for o in env.objects:
    if o.type.name=='Mesh':
        d=read(o)
        if d and getattr(d,'m_Name','')=='enTroll01': troll_mesh_id=o.path_id; break
print("enTroll01 mesh path_id:", troll_mesh_id)
# find SMR whose m_Mesh == that mesh
smr=None
for o in env.objects:
    if o.type.name=='SkinnedMeshRenderer':
        d=read(o)
        if not d: continue
        mp=getattr(d,'m_Mesh',None)
        mid=getattr(mp,'m_PathID',getattr(mp,'path_id',None)) if mp is not None else None
        if mid==troll_mesh_id: smr=d; break
print("found SMR for enTroll01:", smr is not None)
if smr is None: raise SystemExit("no SMR")
bones=getattr(smr,'m_Bones',[])
print("bone count:", len(bones))
def pptr_id(p): return getattr(p,'m_PathID',getattr(p,'path_id',None))
def go_name(tr):
    gp=getattr(tr,'m_GameObject',None); 
    if gp is None: return "?"
    go=byid.get(pptr_id(gp)); go=read(go) if go else None
    return getattr(go,'m_Name','?') if go else "?"
out=[]
for bp in bones:
    tid=pptr_id(bp); to=byid.get(tid); tr=read(to) if to else None
    if tr is None: out.append({"name":"?","pid":tid}); continue
    lp=getattr(tr,'m_LocalPosition',None); lr=getattr(tr,'m_LocalRotation',None); ls=getattr(tr,'m_LocalScale',None)
    fp=getattr(tr,'m_Father',None)
    def v3(v): return [getattr(v,'x',0),getattr(v,'y',0),getattr(v,'z',0)] if v else [0,0,0]
    def v4(v): return [getattr(v,'x',0),getattr(v,'y',0),getattr(v,'z',0),getattr(v,'w',1)] if v else [0,0,0,1]
    out.append({"pid":tid,"name":go_name(tr),"lp":v3(lp),"lr":v4(lr),"ls":v3(ls),"father":pptr_id(fp) if fp else None})
names=[b["name"] for b in out]
print("bone names:", names)
json.dump({"bones":out}, open("/tmp/troll_skel.json","w"))
print("saved /tmp/troll_skel.json")
