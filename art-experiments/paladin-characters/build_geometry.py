#!/usr/bin/env python3
"""Original Paladin equipment only; native character bodies, faces and hair are retained."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import numpy as np
from PIL import Image

OUT=Path(__file__).resolve().parent
ROOT=OUT.parents[1]
spec=importlib.util.spec_from_file_location("original_surface",ROOT/"art-experiments/hearthveil-blacksmith/build_geometry.py")
primitives=importlib.util.module_from_spec(spec)
spec.loader.exec_module(primitives)
PALETTE=["172633","51687B","B8C9CE","97662D","E3B658","254C86","762535","201D28","81CDD4","EEE1B7","B68D70","3D2C25"]
primitives.PALETTE=PALETTE
Surface=primitives.Surface
sys.path.insert(0,str(ROOT/"tools/ai-model-pipeline"))
from export_ftk_glb import write_glb,write_static_glb


def offset(point,x=0,y=0,z=0):return point+np.array([x,y,z])


def emblem(s,center,bone,size=.16):
    s.ellipsoid("Order split diamond",center,(size*.65,size,.03),bone,4,sides=4)
    s.ellipsoid("Order dark slit",offset(center,z=.031),(size*.15,size*.64,.013),bone,7,sides=4)
    s.ellipsoid("Suspended oath bead",offset(center,y=-size*.7,z=.04),(size*.14,size*.14,.025),bone,9,sides=6)


def fitted_breastplate(s,chest,width,color):
    """Original shallow faceted shell follows the authored ribcage and narrows at its waist."""
    outline=[(-width*.84,.18,.185),(-width*.92,-.065,.20),(-width*.39,-.31,.22),(width*.39,-.31,.22),(width*.92,-.065,.20),(width*.84,.18,.185)]
    front=[chest+np.array(p) for p in outline]
    back=[p-np.array([0,0,.025]) for p in front]
    center=chest+np.array([0,-.025,.265]);rear=center-np.array([0,0,.025])
    begin=len(s.data["positions"])
    for i in range(len(front)):
        j=(i+1)%len(front)
        s.triangle([center,front[i],front[j]],["Chest_M"]*3,color)
        s.triangle([rear,back[j],back[i]],["Chest_M"]*3,color)
        s.triangle([front[i],back[i],back[j]],["Chest_M"]*3,4)
        s.triangle([front[i],back[j],front[j]],["Chest_M"]*3,4)
    s.pieces.append({"name":"Fitted faceted breastplate","vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
    for sign in [-1,1]:
        s.tube("Fitted chest edge",[chest+np.array([sign*width*.84,.18,.19]),chest+np.array([sign*width*.92,-.065,.205]),chest+np.array([sign*width*.39,-.31,.225])],[.016,.018,.014],[.016,.018,.014],["Chest_M"]*3,4,sides=4)


def armor(s,male,endgame,variant=None):
    B=s.B
    cloth=6 if endgame else 5
    steel=7 if endgame else 2
    width=.355 if male else .32
    spine=["Root_M","BackA_M","BackB_M","Chest_M"]
    s.tube("Articulated cuirass",[B[b] for b in spine],[width*.74,width*.80,width,width],[.205,.215,.23,.23],spine,[steel,steel,steel,4],sides=10)
    fitted_breastplate(s,B["Chest_M"],width,steel)
    emblem(s,offset(B["Chest_M"],y=.02,z=.288),"Chest_M",.105)
    s.tube("Gilded gorget",[offset(B["Chest_M"],y=.16),offset(B["Neck_M"],y=-.02)],[.245,.16],[.20,.145],["Chest_M","Neck_M"],4)
    s.tube("Belt",[offset(B["Root_M"],y=.075),offset(B["Root_M"],y=.12)],[width*.78]*2,[.218]*2,["Root_M"]*2,4)
    # Separate hip-weighted cloth panels retain leg articulation.
    for side,sign in [("R",1),("L",-1)]:
        shoulder,elbow,wrist=[f"{x}_{side}" for x in ["Shoulder","Elbow","Wrist"]]
        scap=f"Scapula_{side}"
        s.tube("Shoulder hinge "+side,[B[scap],B[shoulder]],[.16,.175] if variant=="oathkeeper" else [.20,.23],[.15,.17] if variant=="oathkeeper" else [.20,.23],[scap,shoulder],1)
        if variant in ["oathkeeper","highward"]:
            layers=1 if variant=="oathkeeper" else 2
            for layer in range(layers):
                center=offset(B[shoulder],x=sign*layer*.065,y=.085-layer*.07)
                reach=.205 if variant=="oathkeeper" else .25
                s.tube("Tier shoulder gold edge "+side+str(layer),[offset(center,y=-.05),offset(center,y=.015),offset(center,y=.055)],[reach,reach*.88,reach*.60],[reach*.93,reach*.81,reach*.56],[shoulder]*3,4,sides=6)
                s.tube("Tier shoulder steel plate "+side+str(layer),[offset(center,y=-.025),offset(center,y=.029),offset(center,y=.067)],[reach*.90,reach*.79,reach*.53],[reach*.83,reach*.71,reach*.48],[shoulder]*3,steel,sides=6)
        else:
            s.ellipsoid("Gilded pauldron rim "+side,offset(B[shoulder],y=.08),(.32 if endgame else .27,.22,.31),shoulder,4,sides=8)
            s.ellipsoid("Pauldron plate "+side,offset(B[shoulder],y=.12,z=.02),(.28 if endgame else .235,.19,.27),shoulder,steel,sides=8)
        if endgame:
            for step in [-1,1]:
                s.ellipsoid("Judicial shoulder tablet "+side,offset(B[shoulder],x=sign*.11,y=.25,z=step*.12),(.105,.16,.045),shoulder,4,sides=4)
        s.tube("Arm plate "+side,[B[shoulder],B[elbow],B[wrist]],[.165,.145,.12],[.165,.145,.12],[shoulder,elbow,wrist],steel)
        s.ellipsoid("Gold elbow "+side,offset(B[elbow],z=.06),(.17,.14,.14),elbow,4,sides=8)
        s.tube("Wrist cuff "+side,[offset(B[wrist],y=-.03),offset(B[wrist],y=.045)],[.14]*2,[.14]*2,[wrist]*2,4)
        hip,knee,ankle=[f"{x}_{side}" for x in ["Hip","Knee","Ankle"]]
        s.tube("Leg plate "+side,[B[hip],B[knee],B[ankle]],[.19,.105,.08],[.17,.098,.075],[hip,knee,ankle],steel)
        tabard_end=.19 if variant=="oathkeeper" else (.025 if variant=="highward" else .07)
        tabard_width=.108 if variant=="oathkeeper" else (.165 if variant=="highward" else .145)
        s.tube("Split tabard "+side,[offset(B[hip],z=.19),offset(B[knee],y=tabard_end,z=.19)],[tabard_width,tabard_width*1.13],[.026,.026],[hip,knee],cloth,sides=4)
        s.tube("Tabard gold hem "+side,[offset(B[knee],y=tabard_end+.005,z=.193),offset(B[knee],y=tabard_end-.025,z=.193)],[tabard_width*1.15]*2,[.028]*2,[knee]*2,4,sides=4)
        s.tube("Knee articulation "+side,[offset(B[knee],y=.055),offset(B[knee],y=-.045)],[.125,.118],[.119,.114],[knee]*2,steel,sides=8)
        if f"ThumbFinger1_{side}" in B:
            s.ellipsoid("Thumb armor "+side,B[f"ThumbFinger1_{side}"],(.04,.04,.05),f"ThumbFinger1_{side}",steel,sides=6)
        for toe in ["MiddleToe1","MiddleToe2"]:
            bone=f"{toe}_{side}"
            if bone in B:s.tube("Sabatons underlayer "+bone,[offset(B[bone],z=-.035),offset(B[bone],z=.045)],[.102,.098],[.032,.03],[bone]*2,steel,sides=8)
    if "Head_M" in B:
        s.ellipsoid("Back neck protection",offset(B["Head_M"],y=-.08,z=-.13),(.12,.07,.035),"Head_M",cloth)


def boots(s,endgame,variant=None):
    """Connected greave, ankle and overlapping sabaton volumes follow authored joint landmarks."""
    B=s.B; steel=7 if endgame else 2
    accent={"novice":6,"oathkeeper":5,"highward":5,"mercy":5,"censure":6,"verdict":9}.get(variant,6)
    for side in ["R","L"]:
        hip,knee,ankle,toe,tip=[f"{p}_{side}" for p in ["Hip","Knee","Ankle","MiddleToe1","MiddleToe2"]]
        s.tube("Connected boot underlayer "+side,[B[hip],B[knee],B[ankle],offset(B[toe],y=.025),offset(B[tip],y=.025)],[.12,.127,.113,.122,.103],[.11,.119,.108,.068,.05],[hip,knee,ankle,toe,tip],1 if not endgame else 7,sides=8)
        s.tube("Fitted shin shell "+side,[offset(B[knee],y=.05,z=.065),offset(B[knee],y=-.11,z=.075),offset(B[ankle],y=.065,z=.055)],[.126,.117,.097],[.063,.055,.049],[knee,knee,ankle],steel,sides=8)
        s.tube("Inset greave stripe "+side,[offset(B[knee],y=.014,z=.126),offset(B[knee],y=-.125,z=.125),offset(B[ankle],y=.075,z=.10)],[.043,.04,.028],[.01,.011,.01],[knee,knee,ankle],accent,sides=4)
        s.tube("Greave upper gold rim "+side,[offset(B[knee],y=.031),offset(B[knee],y=.066)],[.132,.129],[.132,.129],[knee]*2,4,sides=8)
        s.tube("Connected ankle cuff "+side,[offset(B[ankle],y=.09),offset(B[ankle],y=-.005)],[.116,.125],[.114,.12],[ankle]*2,steel,sides=8)
        # One continuous shaped foot avoids interpenetrating closed shell caps at the toes.
        floor=B[toe][1]-.04
        heel=np.array([B[ankle][0],floor+.067,B[ankle][2]-.075])
        instep=np.array([B[ankle][0],floor+.095,B[ankle][2]+.045])
        mid=np.array([B[toe][0],floor+.080,B[toe][2]+.015])
        end=np.array([B[tip][0],floor+.066,B[tip][2]+.050])
        s.tube("Shaped sabaton body "+side,[heel,instep,mid,end],[.115,.13,.13,.099],[.06,.085,.07,.054],[ankle,ankle,toe,tip],steel,sides=8)
        sole=[np.array([point[0],floor+.02,point[2]]) for point in [heel,mid,end]]
        s.tube("Continuous sabaton sole "+side,sole,[.118,.133,.103],[.022,.022,.022],[ankle,toe,tip],0,sides=8)
        # Shallow dark transverse seams read as overlapping articulation without competing surfaces.
        for index,(point,skin) in enumerate([(instep,ankle),(mid,toe)]):
            top=point+np.array([0,.081 if index==0 else .068,0])
            s.tube("Sabaton plate seam "+side+str(index),[top+np.array([-.09,-.015,0]),top,top+np.array([.09,-.015,0])],[.008]*3,[.008]*3,[skin]*3,1,sides=6,axis=(0,1,0))
        for sign in [-1,1]:
            s.tube("Sabaton side piping "+side+str(sign),[mid+np.array([sign*.113,.031,0]),end+np.array([sign*.085,.025,-.025])],[.008,.007],[.008,.007],[toe,tip],4,sides=6,axis=(0,1,0))



def helmet(endgame):
    s=Surface(["Head_M"],np.zeros((1,3)))
    color=6 if endgame else 4
    # Open-front hood or open-face crowned helm, deliberately preserving the face.
    for sign in [-1,1]:
        s.tube("Hood cheek" if endgame else "Helm cheek",[np.array([sign*.245,-.06,-.01]),np.array([sign*.255,.25,-.01]),np.array([sign*.15,.48,-.04])],[.055,.07,.055],[.22,.25,.16],["Head_M"]*3,color,sides=8)
    s.tube("Hood rear",[np.array([0,-.04,-.19]),np.array([0,.28,-.19]),np.array([0,.50,-.06])],[.20,.23,.15],[.075,.08,.08],["Head_M"]*3,color,sides=8)
    s.tube("Brow arch",[np.array([-.23,.34,.17]),np.array([0,.47,.18]),np.array([.23,.34,.17])],[.05]*3,[.05]*3,["Head_M"]*3,4,sides=6)
    emblem(s,np.array([0,.44,.235]),"Head_M",.09)
    if endgame:
        s.ellipsoid("Hood apex",np.array([0,.52,-.05]),(.16,.12,.13),"Head_M",6,sides=6)
    return s


def novice_front_plate(s,center,width):
    """Closed bent sheet with a shallow ridge, clipped armholes and a fitted waist."""
    # Each row is a measured design cross-section, not a shield-shaped flat polygon.
    rows=[(.15,.76,.010),(.075,1.0,.030),(-.10,.88,.025),(-.205,.66,.005)]
    grid=[]
    for y,reach,depth in rows:
        grid.append([center+np.array([x*width*reach,y,depth-.070*abs(x)]) for x in [-1,-.52,0,.52,1]])
    begin=len(s.data["positions"])
    for r in range(3):
        for c in range(4):
            q=[grid[r][c],grid[r+1][c],grid[r+1][c+1],grid[r][c+1]]
            s.triangle([q[0],q[1],q[2]],["Chest_M"]*3,1)
            s.triangle([q[0],q[2],q[3]],["Chest_M"]*3,1)
            q=[v-np.array([0,0,.018]) for v in q]
            s.triangle([q[2],q[1],q[0]],["Chest_M"]*3,1)
            s.triangle([q[3],q[2],q[0]],["Chest_M"]*3,1)
    edge=grid[0]+[grid[r][-1] for r in range(1,4)]+list(reversed(grid[-1][:-1]))+[grid[r][0] for r in [2,1]]
    for i,v in enumerate(edge):
        w=edge[(i+1)%len(edge)];back=np.array([0,0,-.018])
        s.triangle([v,w,w+back],["Chest_M"]*3,1)
        s.triangle([v,w+back,v+back],["Chest_M"]*3,1)
    s.pieces.append({"name":"Novice bent iron breastplate","vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
    for sign in [-1,1]:
        s.ellipsoid("Novice strap rivet",center+np.array([sign*width*.62,.105,.010]),(.012,.012,.007),"Chest_M",2,sides=6)


def novice_armor(s,male):
    """Plain iron breastplate over leather and cloth with small shoulder caps."""
    B=s.B; width=.315 if male else .30
    spine=["Root_M","BackA_M","BackB_M","Chest_M"]
    s.tube("Novice fitted gambeson",[B[b] for b in spine]+[offset(B["Chest_M"],y=.21),offset(B["Neck_M"],y=-.025)],
           [width*.79,width*.86,width,width,width*.86,.145],
           [.175,.18,.195,.195,.165,.12],spine+["Chest_M","Neck_M"],0,sides=12)
    s.tube("Novice leather belt",[offset(B["Root_M"],y=.075),offset(B["Root_M"],y=.135)],[width*.78]*2,[.19]*2,["Root_M"]*2,11)
    chest=B["Chest_M"]
    novice_front_plate(s,offset(chest,y=.095,z=.208),width*.81)
    s.ellipsoid("Novice iron belt buckle",offset(B["Root_M"],y=.105,z=.202),(.040,.031,.012),"Root_M",1,sides=4)
    for side,sign in [("R",1),("L",-1)]:
        s.tube("Novice leather shoulder strap "+side,[offset(chest,x=sign*.15,y=.255,z=.12),offset(chest,x=sign*.15,y=.205,z=.211)],[.028,.028],[.013,.013],["Chest_M"]*2,11,sides=4)
        scap,shoulder,elbow,wrist=[f"{x}_{side}" for x in ["Scapula","Shoulder","Elbow","Wrist"]]
        s.tube("Novice mail shoulder "+side,[B[scap],B[shoulder]],[.155,.17],[.145,.16],[scap,shoulder],0)
        # One compact iron cap covers each shoulder without ornamental edging.
        for layer in range(1):
            center=offset(B[shoulder],x=sign*layer*.065,y=.085-layer*.065)
            s.tube("Novice plain shoulder cap "+side+str(layer),[offset(center,y=-.028),offset(center,y=.005),offset(center,y=.037)],[.173,.163,.125],[.169,.154,.123],[shoulder]*3,1,sides=8)
        sleeve_end=B[shoulder]+(B[elbow]-B[shoulder])*.76
        sleeve_mid=B[shoulder]+(B[elbow]-B[shoulder])*.46
        s.tube("Novice short padded sleeve "+side,[B[shoulder],sleeve_mid,sleeve_end],
               [.155,.165,.16],[.15,.16,.155],[shoulder,shoulder,{shoulder:.8,elbow:.2}],0,sides=10)
        direction=(B[elbow]-B[shoulder]);direction/=np.linalg.norm(direction)
        s.tube("Novice rolled sleeve hem "+side,[sleeve_end-direction*.025,sleeve_end+direction*.015],
               [.172]*2,[.166]*2,[{shoulder:.8,elbow:.2}]*2,1,sides=10)
        forearm=(B[wrist]-B[elbow]);forearm/=np.linalg.norm(forearm)
        s.tube("Novice leather wrist wrap "+side,[B[wrist]-forearm*.045,B[wrist]+forearm*.018],
               [.125,.12],[.12,.116],[wrist]*2,11,sides=10)
        hip,knee,ankle=[f"{x}_{side}" for x in ["Hip","Knee","Ankle"]]
        s.tube("Novice mail leggings "+side,[B[hip],B[knee],B[ankle]],[.155,.103,.079],[.145,.097,.074],[hip,knee,ankle],0)
        s.tube("Novice narrow split apron "+side,[offset(B[hip],x=-sign*.025,z=.17),offset(B[knee],x=-sign*.025,y=.20,z=.15)],[.10,.105],[.018,.018],[hip,knee],0,sides=4)
        s.tube("Novice knee articulation "+side,[offset(B[knee],y=.045),offset(B[knee],y=-.04)],[.115,.11],[.112,.105],[knee]*2,0,sides=8)
        thumb=f"ThumbFinger1_{side}"
        if thumb in B:s.ellipsoid("Novice thumb guard "+side,B[thumb],(.032,.034,.041),thumb,1,sides=6)
        for toe in ["MiddleToe1","MiddleToe2"]:
            bone=f"{toe}_{side}"
            if bone in B:s.tube("Novice toe articulation "+bone,[offset(B[bone],z=-.035),offset(B[bone],z=.045)],[.070,.060],[.018,.015],[bone]*2,11,sides=8)
    if "Head_M" in B:s.ellipsoid("Novice rear collar",offset(B["Head_M"],y=-.08,z=-.14),(.09,.035,.022),"Head_M",11,sides=6)





def novice_boots(s):
    """Soft leather shafts and round toes, without plate armor ornament."""
    B=s.B
    for side in ["R","L"]:
        hip,knee,ankle,toe,tip=[f"{p}_{side}" for p in ["Hip","Knee","Ankle","MiddleToe1","MiddleToe2"]]
        s.tube("Novice trouser leg "+side,[B[hip],B[knee],B[ankle]],[.105,.090,.075],[.095,.085,.07],[hip,knee,ankle],0,sides=8)
        top=offset(B[knee],y=-.10)
        s.tube("Novice leather boot shaft "+side,[top,offset(B[ankle],y=.16),B[ankle]],[.14,.13,.125],[.14,.13,.12],[knee,ankle,ankle],11,sides=8)
        s.tube("Novice folded boot cuff "+side,[offset(top,y=.025),offset(top,y=-.035)],[.15,.148],[.15,.145],[knee]*2,11,sides=8)
        floor=B[toe][1]-.045
        # Fixed horizontal sections keep the sole level instead of twisting a tube along the instep.
        sections=[(B[ankle][2]-.082,.105,.122,ankle),
                  (B[ankle][2]+.015,.125,.165,ankle),
                  (B[toe][2]+.025,.13,.115,toe),
                  (B[tip][2]+.038,.105,.075,tip),
                  (B[tip][2]+.065,.071,.054,tip)]
        rings=[]
        for z,w,h,bone in sections:
            ring=[(-w*.78,.016),(-w,.036),(-w*.88,h*.78),(-w*.48,h),
                  (w*.48,h),(w*.88,h*.78),(w,.036),(w*.78,.016)]
            rings.append([np.array([B[ankle][0]+x,floor+y,z]) for x,y in ring])
        begin=len(s.data["positions"])
        for row in range(len(rings)-1):
            for j in range(8):
                k=(j+1)%8;bone=sections[row][3];next_bone=sections[row+1][3]
                s.triangle([rings[row][j],rings[row+1][j],rings[row+1][k]],[bone,next_bone,next_bone],11)
                s.triangle([rings[row][j],rings[row+1][k],rings[row][k]],[bone,next_bone,bone],11)
        for row,reverse in [(0,False),(len(rings)-1,True)]:
            for j in range(1,7):
                tri=[rings[row][0],rings[row][j],rings[row][j+1]]
                if reverse:tri.reverse()
                s.triangle(tri,[sections[row][3]]*3,11)
        s.pieces.append({"name":"Novice shaped leather shoe "+side,"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
        # Low dark sole follows the same outline and carries toe weighting.
        centers=[np.array([B[ankle][0],floor+.012,z]) for z,_,_,_ in sections]
        s.tube("Novice flat welt sole "+side,centers,[w for _,w,_,_ in sections],[.013]*len(sections),[b for _,_,_,b in sections],0,sides=8)


def novice_helmet():
    s=Surface(["Head_M"],np.zeros((1,3)))
    # Compact open face: leather sides and a plain iron cap, without crest or seal.
    for sign in [-1,1]:
        s.tube("Novice cloth cheek",[np.array([sign*.22,.16,-.045]),np.array([sign*.23,.21,-.055]),np.array([sign*.18,.36,-.045])],[.028,.035,.030],[.105,.13,.13],["Head_M"]*3,1,sides=6)
    s.tube("Novice close rear hood",[np.array([0,.15,-.19]),np.array([0,.23,-.19]),np.array([0,.39,-.075])],[.185,.202,.155],[.038,.047,.09],["Head_M"]*3,1,sides=8)
    s.ellipsoid("Novice plain iron cap",np.array([0,.365,-.05]),(.204,.075,.178),"Head_M",1,sides=8)
    s.tube("Novice plain iron brow",[np.array([-.20,.315,.14]),np.array([0,.35,.19]),np.array([.20,.315,.14])],[.023]*3,[.024]*3,["Head_M"]*3,1,sides=6)
    for point in s.data["positions"]:point[1]+=.025
    return s


def oathkeeper_helmet():
    s=Surface(["Head_M"],np.zeros((1,3)))
    # Compact open face: red cloth sides, steel crown, a low gold brow and original seal.
    for sign in [-1,1]:
        s.tube("Novice cloth cheek",[np.array([sign*.22,-.025,-.045]),np.array([sign*.23,.21,-.055]),np.array([sign*.18,.36,-.045])],[.031,.043,.036],[.14,.163,.13],["Head_M"]*3,6,sides=6)
    s.tube("Novice close rear hood",[np.array([0,.005,-.19]),np.array([0,.23,-.19]),np.array([0,.39,-.075])],[.185,.202,.155],[.038,.047,.09],["Head_M"]*3,6,sides=8)
    s.ellipsoid("Novice low red crown",np.array([0,.365,-.05]),(.204,.075,.178),"Head_M",6,sides=8)
    s.tube("Novice low gold brow",[np.array([-.20,.315,.14]),np.array([0,.35,.19]),np.array([.20,.315,.14])],[.023]*3,[.024]*3,["Head_M"]*3,4,sides=6)
    emblem(s,np.array([0,.355,.222]),"Head_M",.052)
    return s


def decorate(s,part,variant):
    """Shared original silhouette, with readable set-specific trim and relief."""
    if variant=="novice":return
    B=s.B
    cloth={"mercy":5,"censure":6,"verdict":9}.get(variant)
    if cloth is not None:
        for uv in s.data["uvs"]:
            if int(uv[0]*len(PALETTE))==6:uv[0]=(cloth+.5)/len(PALETTE)
    if part=="armor":
        for side,sign in [("R",1),("L",-1)]:
            bone="Shoulder_"+side
            if variant=="oathkeeper":
                pass
            elif variant=="highward":
                emblem(s,offset(B[bone],y=.09,z=.215),bone,.065)
            elif variant=="mercy":
                s.ellipsoid("Mercy lantern shoulder",offset(B[bone],y=.18,z=.28),(.075,.12,.045),bone,8,sides=6)
            elif variant=="censure":
                for step in [-1,1]:s.ellipsoid("Censure ward plate",offset(B[bone],x=step*.08,y=.20,z=.28),(.035,.13,.03),bone,6,sides=4)
            else:
                s.ellipsoid("Verdict shoulder seal",offset(B[bone],y=.18,z=.28),(.12,.10,.04),bone,9,sides=4)
        if variant=="highward":
            for side,sign in [("R",1),("L",-1)]:
                s.tube("Highward protective rib plate "+side,[offset(B["Chest_M"],x=sign*.27,y=.09,z=.10),offset(B["BackB_M"],x=sign*.24,y=.01,z=.13)],[.05,.045],[.105,.09],["Chest_M","BackB_M"],5,sides=6)
                hip="Hip_"+side
                s.tube("Highward segmented tasset "+side,[offset(B[hip],x=sign*.10,y=-.045,z=.13),offset(B[hip],x=sign*.105,y=-.23,z=.14)],[.10,.095],[.037,.028],[hip]*2,2,sides=6)
            s.ellipsoid("Highward gorget seal",offset(B["Neck_M"],y=-.02,z=.20),(.09,.08,.025),"Neck_M",4,sides=6)
    elif part=="boots":
        pass  # Colored inset stripes are integral to the connected greave generator.
    else:
        if variant=="mercy":s.ellipsoid("Mercy hood lantern",np.array([0,.57,.08]),(.065,.10,.055),"Head_M",8,sides=6)
        elif variant=="censure":
            for sign in [-1,1]:s.ellipsoid("Censure hood ward",np.array([sign*.19,.42,.13]),(.035,.12,.025),"Head_M",4,sides=4)
        elif variant=="verdict":s.ellipsoid("Verdict crest seal",np.array([0,.56,.12]),(.11,.10,.035),"Head_M",9,sides=4)
        elif variant=="highward":
            for sign in [-1,1]:s.ellipsoid("Highward crest",np.array([sign*.17,.48,-.03]),(.055,.16,.065),"Head_M",4,sides=4)
        else:pass


def helmet_mount_transform(bindings):
    # Native UpdateHelmet attaches to Hair_M with Euler(-90,90,0), while the
    # original design uses a Head_M-centered character-space coordinate system.
    mount=np.array([[0.,-1,0,0],[0,0,1,0],[-1,0,0,0],[0,0,0,1]])
    transforms=[]
    for sex in ['female','male']:
        binding=json.loads((bindings/(sex+'-body.json')).read_text())
        bones=binding['bone_names'];binds=np.array(binding['bindposes'])
        head=binds[bones.index('Head_M')];hair=binds[bones.index('Hair_M')]
        center=np.eye(4);center[:3,3]=np.linalg.inv(head)[:3,3]
        transforms.append(np.linalg.inv(mount)@hair@center)
    assert np.allclose(transforms[0],transforms[1],atol=1e-6)
    return transforms[0]


def apply_helmet_mount(surface,transform):
    positions=np.array(surface.data['positions']);normals=np.array(surface.data['normals'])
    surface.data['positions']=(positions@transform[:3,:3].T+transform[:3,3]).tolist()
    normals=normals@np.linalg.inv(transform[:3,:3])
    surface.data['normals']=(normals/np.linalg.norm(normals,axis=1)[:,None]).tolist()
    surface.data['authorToRuntime']=transform.tolist()


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bindings",type=Path,default=ROOT/"scratch/paladin-bindings")
    parser.add_argument("--output",type=Path,default=OUT)
    args=parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    helmet_transform=helmet_mount_transform(args.bindings)
    records=[]
    equipment_sets=[]
    for variant in ["novice","oathkeeper","highward","mercy","censure","verdict"]:
        endgame=variant in ["mercy","censure","verdict"]
        mapping={"set":variant,"band":"endgame" if endgame else {"novice":"starter","oathkeeper":"early","highward":"midgame"}[variant],"armor":{}}
        for part,sex in [("armor","female"),("armor","male"),("boots","shared"),("helmet","shared")]:
            binding_key="boots" if part=="boots" else sex+"-armor"
            binding=None
            if part=="helmet":
                s=novice_helmet() if variant=="novice" else (oathkeeper_helmet() if variant=="oathkeeper" else helmet(endgame))
                if variant=="oathkeeper":
                    for uv in s.data["uvs"]:
                        if int(uv[0]*len(PALETTE))==6:uv[0]=(5+.5)/len(PALETTE)
            else:
                path=args.bindings/(binding_key+".json");binding=json.loads(path.read_text())
                binds=np.array(binding["bindposes"]);s=Surface(binding["bone_names"],np.linalg.inv(binds)[:,:3,3])
                if part=="armor":
                    if variant=="novice":novice_armor(s,sex=="male")
                    else:armor(s,sex=="male",endgame,variant)
                else:
                    if variant=="novice":novice_boots(s)
                    else:boots(s,endgame,variant)
            decorate(s,part,variant)
            if part=='helmet':apply_helmet_mount(s,helmet_transform)
            key="paladin-"+(sex+"-" if part=="armor" else "")+variant+"-"+part
            if part=="helmet":
                for field in ["joints","weights","bone_names"]:del s.data[field]
            (args.output/(key+".source.json")).write_text(json.dumps(s.data,separators=(",",":"))+"\n")
            (args.output/(key+".pieces.json")).write_text(json.dumps(s.pieces,indent=2)+"\n")
            if binding:write_glb(args.output/(key+".glb"),s.data,{"bone_names":np.array(binding["bone_names"]),"bindposes":binds})
            else:write_static_glb(args.output/(key+".glb"),s.data)
            record={"key":key,"part":part,"sex":sex,"outfit":variant,"equipmentSet":True,"vertices":len(s.data["positions"]),"triangles":len(s.data["triangles"])}
            if binding:record.update(rendererId=binding["rendererId"],nativeMeshName=binding["nativeMeshName"],bindingInput=binding_key,bindingSha256=hashlib.sha256(path.read_bytes()).hexdigest(),bones=len(binding["bone_names"]))
            else:record["space"]="Hair_M mount-local coordinates for native Euler(-90,90,0). Live art fit pending."
            records.append(record)
            if part=="armor":mapping["armor"][sex]=key+".glb"
            else:mapping[part]=key+".glb"
        equipment_sets.append(mapping)
    image=Image.new("RGB",(len(PALETTE)*16,16))
    for i,color in enumerate(PALETTE):image.paste(tuple(int(color[j:j+2],16) for j in [0,2,4]),(i*16,0,(i+1)*16,16))
    image.save(args.output/"paladin-character-palette.png")
    output_names=[record["key"]+suffix for record in records for suffix in [".glb",".source.json",".pieces.json"]]+["paladin-character-palette.png"]
    files={name:hashlib.sha256((args.output/name).read_bytes()).hexdigest() for name in sorted(output_names)}
    manifest={"schema":"ftkmf.paladin-original-equipment-apparel.v1","generatorSha256":hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),"primitiveLibrary":"art-experiments/hearthveil-blacksmith/build_geometry.py","primitiveLibrarySha256":hashlib.sha256(Path(primitives.__file__).read_bytes()).hexdigest(),"provenance":"New authored Paladin surfaces and weights. Reuses original generic Surface geometry helper only, no Hearthveil design functions. Only names and inverse bind matrices are read from native metadata.","status":"Offline original art. Exact skinset assembly, equipment paths, placement, animation and visual acceptance are unverified.","equipmentSets":equipment_sets,"assets":records,"files":files}
    (args.output/"manifest.json").write_text(json.dumps(manifest,indent=2)+"\n")


if __name__=="__main__":main()
