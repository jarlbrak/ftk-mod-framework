import UnityPy, os, sys
DATA="/Users/tbrack/Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data"
files=["sharedassets0.assets","resources.assets","sharedassets1.assets","globalgamemanagers.assets","level0","level1"]
for fn in files:
    p=os.path.join(DATA,fn)
    if not os.path.exists(p): continue
    try:
        env=UnityPy.load(p)
    except Exception as e:
        print(f"{fn}: load err {e}"); continue
    meshes=[]; smrs=[]
    for obj in env.objects:
        tn=obj.type.name
        if tn=='Mesh':
            try:
                d=obj.read()
                nm=getattr(d,'m_Name','')
                if 'troll' in nm.lower() or 'enTroll' in nm:
                    meshes.append((nm, getattr(d,'m_VertexCount', '?')))
            except: pass
        elif tn=='SkinnedMeshRenderer':
            try:
                d=obj.read()
                bones=getattr(d,'m_Bones',[])
                smrs.append((obj.path_id, len(bones)))
            except: pass
    if meshes:
        print(f"\n### {fn}: troll meshes -> {meshes}")
        print(f"    SkinnedMeshRenderers in file: {len(smrs)}")
