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


def novice_jacket_shell(s,width):
    """Tailored padded jack with planar front panels and an open, notched neckline."""
    B=s.B
    rows=[
        (offset(B["Root_M"],y=.045),width*.75,.121,.086,"Root_M"),
        (B["BackA_M"],width*.78,.130,.087,"BackA_M"),
        (B["BackB_M"],width*.90,.135,.091,"BackB_M"),
        (offset(B["Chest_M"],y=.08),width,.130,.094,"Chest_M"),
        (offset(B["Chest_M"],y=.205),width*.82,.115,.080,"Chest_M"),
        (offset(B["Chest_M"],y=.287),width*.43,.081,.069,{"Chest_M":.75,"Neck_M":.25}),
    ]
    rings=[]
    for row,(center,w,front,back,_) in enumerate(rows):
        ring=[(0,0,front),(w*.72,0,front*.92),(w,0,front*.25),
              (w*.78,0,-back*.82),(0,0,-back),(-w*.78,0,-back*.82),
              (-w,0,front*.25),(-w*.72,0,front*.92)]
        if row==len(rows)-1:
            # The front neckline sits lower than its leather shoulder yoke.
            ring[0]=(0,-.027,front)
            ring[1]=(w*.72,-.008,front*.92)
            ring[7]=(-w*.72,-.008,front*.92)
        rings.append([center+np.array(point) for point in ring])
    begin=len(s.data["positions"])
    for row in range(len(rows)-1):
        low,high=rows[row][4],rows[row+1][4]
        for side in range(8):
            j=(side+1)%8
            # Broad oxblood front/back panels, dark leather side gussets and yoke.
            color=11 if side in [2,5] or row==len(rows)-2 else 6
            s.triangle([rings[row][side],rings[row][j],rings[row+1][j]],[low,low,high],color)
            s.triangle([rings[row][side],rings[row+1][j],rings[row+1][side]],[low,high,high],color)
    # Neck and waist remain open around the retained native body.
    s.pieces.append({"name":"Novice tailored oxblood padded jack","vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
    return rows


def novice_jacket_tail(s,width):
    """Two flexible coat skirts continue the jack below its belt with a narrow vent."""
    root=s.B["Root_M"]
    for face,z in [("front",.130),("back",-.088)]:
        for side,sign in [("R",1),("L",-1)]:
            hip="Hip_"+side
            pts=[root+np.array([sign*.018,.071,z]),
                 root+np.array([sign*width*.74,.071,z*.87]),
                 root+np.array([sign*width*.83,-.130,z*.94]),
                 root+np.array([sign*.033,-.150,z])]
            skins=["Root_M","Root_M",{"Root_M":.4,hip:.6},{"Root_M":.4,hip:.6}]
            start=len(s.data["positions"])
            # Thin closed cloth has distinct front and reverse surfaces, avoiding coplanar faces.
            inward=np.array([0,0,-.006 if face=="front" else .006])
            reverse_pts=[point+inward for point in pts]
            for indices in [(0,2,1),(0,3,2)]:
                if (sign<0) != (face=="back"):indices=tuple(reversed(indices))
                s.triangle([pts[i] for i in indices],[skins[i] for i in indices],6)
                s.triangle([reverse_pts[i] for i in reversed(indices)],[skins[i] for i in reversed(indices)],11)
            for i in range(4):
                j=(i+1)%4
                quad=[pts[i],pts[j],reverse_pts[j],reverse_pts[i]]
                weights=[skins[i],skins[j],skins[j],skins[i]]
                if (sign<0) != (face=="back"):
                    quad.reverse();weights.reverse()
                s.triangle([quad[0],quad[1],quad[2]],[weights[0],weights[1],weights[2]],11)
                s.triangle([quad[0],quad[2],quad[3]],[weights[0],weights[2],weights[3]],11)
            s.tube("Novice bound coat hem "+face+side,[pts[3],pts[2]],[.008,.008],[.005,.005],[skins[3],skins[2]],11,sides=4,axis=(0,0,1))
            s.pieces.append({"name":"Novice coat skirt "+face+side,"vertex_start":start,"vertex_count":len(s.data["positions"])-start})


def novice_armor(s,male):
    """An initiate's red padded jack over undyed linen, with practical leather binding."""
    B=s.B; width=.268 if male else .253
    rows=novice_jacket_shell(s,width)
    novice_jacket_tail(s,width)
    s.tube("Novice leather belt",[offset(B["Root_M"],y=.062),offset(B["Root_M"],y=.110)],
           [width*.80]*2,[.142]*2,["Root_M"]*2,11,sides=12)
    # A narrow center closure follows the actual authored front, not a floating plate.
    closure=[offset(center,z=front+.004) for center,_,front,_,_ in rows[:-1]]
    s.tube("Novice stitched center closure",closure,[.007]*len(closure),[.004]*len(closure),
           [row[4] for row in rows[:-1]],11,sides=4)
    for index in [1,2,3]:
        center=closure[index]
        s.ellipsoid("Novice plain horn fastening "+str(index),offset(center,z=.007),(.009,.012,.004),rows[index][4],3,sides=4)
    # A small square iron buckle is the sole bright fitting on the jacket.
    buckle=offset(B["Root_M"],y=.087,z=.148)
    s.ellipsoid("Novice plain iron buckle",buckle,(.029,.022,.007),"Root_M",1,sides=4)
    s.ellipsoid("Novice buckle leather inset",offset(buckle,z=.008),(.014,.010,.004),"Root_M",11,sides=4)
    for side,sign in [("R",1),("L",-1)]:
        shoulder,elbow=[f"{x}_{side}" for x in ["Shoulder","Elbow"]]
        direction=B[elbow]-B[shoulder]
        start=B[shoulder]+direction*.025
        mid=B[shoulder]+direction*.26
        end=B[shoulder]+direction*.55
        # Cream fabric and a rolled cuff give the upper arm a cloth reading.
        s.tube("Novice linen short sleeve "+side,[start,mid,end],[.082,.090,.083],[.085,.092,.082],
               [shoulder,shoulder,{shoulder:.85,elbow:.15}],9,sides=8)
        unit=direction/np.linalg.norm(direction)
        s.tube("Novice rolled linen cuff "+side,[end-unit*.024,end+unit*.009],[.090,.088],[.087,.086],
               [{shoulder:.85,elbow:.15}]*2,9,sides=8)
        # Tailored leather yoke bridges the neckline to the sleeve's upper half.
        # Its folded surfaces follow the shoulder, rather than floating over the arm.
        upper_skin={"Chest_M":.30,shoulder:.70}
        inner_skin={"Chest_M":.75,"Neck_M":.25}
        neck_front=offset(B["Chest_M"],x=sign*width*.31,y=.281,z=.077)
        neck_back=offset(B["Chest_M"],x=sign*width*.32,y=.287,z=-.057)
        sleeve_front=offset(start,z=.093)
        sleeve_top=offset(start,y=.090)
        sleeve_back=offset(start,z=-.093)
        chest_front=offset(B["Chest_M"],x=sign*width*.74,y=.203,z=.110)
        chest_back=offset(B["Chest_M"],x=sign*width*.70,y=.203,z=-.067)
        panels=[([neck_front,chest_front,sleeve_front,sleeve_top],[inner_skin,"Chest_M",upper_skin,upper_skin],np.array([0,1,1])),
                ([neck_front,sleeve_top,sleeve_back,neck_back],[inner_skin,upper_skin,upper_skin,inner_skin],np.array([0,1,0])),
                ([neck_back,sleeve_back,chest_back],[inner_skin,upper_skin,"Chest_M"],np.array([0,1,-1]))]
        begin=len(s.data["positions"])
        for points,skins,outside in panels:
            for i in range(1,len(points)-1):
                tri=[points[0],points[i],points[i+1]];skin=[skins[0],skins[i],skins[i+1]]
                if np.dot(np.cross(tri[1]-tri[0],tri[2]-tri[0]),outside)<0:
                    tri.reverse();skin.reverse()
                s.triangle(tri,skin,11)
        s.pieces.append({"name":"Novice fitted shoulder yoke "+side,"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
        # The seam at the sleeve root is narrow enough to read as binding, not a cap.
        s.tube("Novice shoulder seam binding "+side,[start-unit*.006,start+unit*.018],[.089,.090],[.094,.094],
               [shoulder]*2,11,sides=8)
    if "Head_M" in B:
        s.ellipsoid("Novice rear collar seam",offset(B["Head_M"],y=-.115,z=-.130),(.065,.015,.010),"Head_M",11,sides=6)


def novice_boots(s):
    """Calf-height leather boots with a continuous ankle, heel and flat welted foot."""
    B=s.B
    for side in ["R","L"]:
        hip,knee,ankle,toe,tip=[f"{p}_{side}" for p in ["Hip","Knee","Ankle","MiddleToe1","MiddleToe2"]]
        top=offset(B[ankle],y=.245,z=.018)
        s.tube("Novice fitted wool trousers "+side,[B[hip],B[hip]*.45+B[knee]*.55,B[knee],offset(top,y=-.026)],
               [.099,.092,.081,.073],[.087,.081,.074,.071],[hip,{hip:.5,knee:.5},knee,{knee:.48,ankle:.52}],0,sides=8)
        # Extending below the ankle and over the heel avoids the old daylight gap.
        s.tube("Novice continuous leather boot shaft "+side,
               [top,offset(B[ankle],y=.145,z=.010),offset(B[ankle],y=.015,z=-.004),offset(B[ankle],y=-.077,z=-.010)],
               [.095,.089,.082,.087],[.088,.085,.083,.097],
               [{knee:.40,ankle:.60},{knee:.13,ankle:.87},ankle,ankle],11,sides=8)
        s.tube("Novice turned boot cuff "+side,[offset(top,y=.007),offset(top,y=-.030)],
               [.101,.101],[.093,.093],[{knee:.40,ankle:.60}]*2,11,sides=8)
        # The slim leather strap stays the same color as the boot, with a modest iron stud.
        strap=offset(B[ankle],y=.075)
        s.tube("Novice boot ankle strap "+side,[offset(strap,y=-.012),offset(strap,y=.012)],
               [.090,.090],[.091,.091],[ankle]*2,11,sides=8)
        s.ellipsoid("Novice ankle strap stud "+side,offset(strap,x=.085 if side=="R" else -.085,z=.020),(.008,.013,.013),ankle,1,sides=4)
        floor=B[toe][1]-.044
        sections=[(B[ankle][2]-.086,.083,.132,ankle),
                  (B[ankle][2]-.012,.095,.195,ankle),
                  (B[ankle][2]+.089,.106,.168,{ankle:.8,toe:.2}),
                  (B[toe][2]+.032,.107,.114,toe),
                  (B[tip][2]+.010,.088,.083,tip),
                  (B[tip][2]+.043,.064,.064,tip)]
        rings=[]
        for z,w,h,bone in sections:
            cross=[(-w*.82,.024),(-w,.044),(-w*.91,h*.80),(-w*.49,h),
                   (w*.49,h),(w*.91,h*.80),(w,.044),(w*.82,.024)]
            rings.append([np.array([B[ankle][0]+x,floor+y,z]) for x,y in cross])
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
        s.pieces.append({"name":"Novice joined leather instep and toe "+side,"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
        centers=[np.array([B[ankle][0],floor+.022,z]) for z,_,_,_ in sections]
        s.tube("Novice dark flat welt sole "+side,centers,[w+.004 for _,w,_,_ in sections],[.021]*len(sections),[b for _,_,_,b in sections],0,sides=8)


def novice_helmet():
    """Plain iron travelling helm with a defined brow rim and low reinforcing rib."""
    s=Surface(["Head_M"],np.zeros((1,3)))
    count=12
    rings=[]
    for row,(radius,height) in enumerate([(1.,.0),(.97,.115),(.68,.203),(.20,.242)]):
        ring=[]
        for j in range(count):
            angle=2*np.pi*j/count;x,z=np.cos(angle),np.sin(angle)
            base=.214+.034*max(z,0)-.078*max(-z,0)
            y=base*(1-row/3)+.214*(row/3)+height
            ring.append(np.array([x*.216*radius,y,z*.198*radius-.025]))
        rings.append(ring)
    start=len(s.data["positions"])
    for row in range(3):
        for j in range(count):
            k=(j+1)%count
            s.triangle([rings[row][j],rings[row+1][j],rings[row+1][k]],["Head_M"]*3,1)
            s.triangle([rings[row][j],rings[row+1][k],rings[row][k]],["Head_M"]*3,1)
    apex=np.array([0,.458,-.025])
    for j in range(count):
        k=(j+1)%count
        s.triangle([rings[-1][j],apex,rings[-1][k]],["Head_M"]*3,1)
    s.pieces.append({"name":"Novice angular iron helm crown","vertex_start":start,"vertex_count":len(s.data["positions"])-start})
    # A shallow outward brim is visibly metal head protection rather than a cloth cap.
    start=len(s.data["positions"])
    for j in range(count):
        k=(j+1)%count
        a,b=rings[0][j],rings[0][k]
        def rim(point):
            return np.array([point[0]*1.14,point[1]-.012,(point[2]+.025)*1.18-.025])
        c,d=rim(a),rim(b)
        s.triangle([a,b,d],["Head_M"]*3,1)
        s.triangle([a,d,c],["Head_M"]*3,1)
        s.triangle([c,d,offset(d,y=-.019)],["Head_M"]*3,11)
        s.triangle([c,offset(d,y=-.019),offset(c,y=-.019)],["Head_M"]*3,11)
    s.pieces.append({"name":"Novice flared brow rim and leather lining","vertex_start":start,"vertex_count":len(s.data["positions"])-start})
    # The crown rib is a practical narrow strap, never a crest or ornament.
    ridge=[np.array([0,.162,-.226]),np.array([0,.284,-.215]),
           np.array([0,.399,-.161]),np.array([0,.465,-.063]),
           np.array([0,.466,.015]),np.array([0,.422,.112]),
           np.array([0,.343,.172]),np.array([0,.252,.190])]
    s.tube("Novice low iron crown reinforcing band",ridge,[.015]*len(ridge),[.007]*len(ridge),["Head_M"]*len(ridge),2,sides=4)
    for sign in [-1,1]:
        s.ellipsoid("Novice helm brow rivet "+str(sign),np.array([sign*.107,.258,.158]),(.009,.009,.006),"Head_M",3,sides=6)
    return s


def oathkeeper_panel(s,label,points,skins,color,depth=.009,center_skin=None):
    """Closed shallow panel with explicit outward front and a subdued reverse."""
    front=[np.array(point) for point in points]
    rear=[offset(point,z=-depth) for point in front]
    center=np.mean(front,axis=0);center[2]+=.003
    rear_center=np.mean(rear,axis=0)
    if center_skin is None:
        center_skin={}
        for skin in skins:
            for bone,weight in ({skin:1.0} if isinstance(skin,str) else skin).items():
                center_skin[bone]=center_skin.get(bone,0)+weight/len(skins)
    begin=len(s.data["positions"])
    for i in range(len(front)):
        j=(i+1)%len(front)
        indices=[i,j]
        if np.cross(front[i]-center,front[j]-center)[2]<0:indices.reverse()
        left,right=indices
        s.triangle([center,front[left],front[right]],[center_skin,skins[left],skins[right]],color)
        s.triangle([rear_center,rear[right],rear[left]],[center_skin,skins[right],skins[left]],1)
        s.triangle([front[left],rear[left],rear[right]],[skins[left],skins[left],skins[right]],1)
        s.triangle([front[left],rear[right],front[right]],[skins[left],skins[right],skins[right]],1)
    s.pieces.append({"name":label,"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})


def oathkeeper_armor(s,male):
    """Compact steel guard plates over the proven fitted squire garment pattern."""
    novice_armor(s,male)
    # Reuse the tailored pattern, then give this tier its own steel structure and longer front panels.
    for uv in s.data["uvs"]:
        color=int(uv[0]*len(PALETTE))
        uv[0]=({6:5,9:0}.get(color,color)+.5)/len(PALETTE)
    for piece in s.pieces:piece["name"]=piece["name"].replace("Novice","Oathkeeper underlayer")
    B=s.B;chest=B["Chest_M"]
    for side,sign in [("R",1),("L",-1)]:
        shoulder,elbow,wrist=[f"{x}_{side}" for x in ["Shoulder","Elbow","Wrist"]]
        scap="Scapula_"+side
        # Two chest plates leave a narrow blue central channel and a mobile cloth waist.
        coords=[(.027,.206,.145),(.139,.213,.137),(.220,.137,.135),
                (.219,-.035,.146),(.151,-.105,.159),(.027,-.115,.166)]
        # The lower edges follow the jacket's BackB bend rather than cutting into it in idle.
        plate_skins=["Chest_M"]*3+[{"Chest_M":.5,"BackB_M":.5},
                                  {"Chest_M":.15,"BackB_M":.85},{"Chest_M":.15,"BackB_M":.85}]
        oathkeeper_panel(s,"Oathkeeper fitted chest half "+side,
                         [chest+np.array([sign*x,y,z]) for x,y,z in coords],plate_skins,2,.012)
        # A shallow steel cap wraps only the upper shoulder, never the whole upper arm.
        root=B[shoulder]
        cap_rows=[(-.050,.106,.130),(.035,.124,.143),(.155,.096,.124)]
        rings=[]
        for x,height,depth in cap_rows:
            rings.append([root+np.array([sign*x,.016+np.sin(angle)*height,np.cos(angle)*depth])
                          for angle in np.linspace(0,np.pi,7)])
        begin=len(s.data["positions"])
        for row in range(2):
            for j in range(6):
                pts=[rings[row][j],rings[row+1][j],rings[row+1][j+1],rings[row][j+1]]
                skin=[{scap:.25,shoulder:.75},shoulder,shoulder,{scap:.25,shoulder:.75}] if row==0 else [shoulder]*4
                for ids in [(0,1,2),(0,2,3)]:
                    tri=[pts[i] for i in ids];weights=[skin[i] for i in ids]
                    outward=np.mean(tri,axis=0)-offset(root,x=sign*.035,y=.015)
                    outward[0]=0
                    if np.dot(np.cross(tri[1]-tri[0],tri[2]-tri[0]),outward)<0:
                        tri.reverse();weights.reverse()
                    s.triangle(tri,weights,2)
        s.pieces.append({"name":"Oathkeeper compact arched steel shoulder "+side,"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
        for index in [0,2]:
            s.tube("Oathkeeper dark shoulder edge "+side+str(index),rings[index],[.009]*7,[.006]*7,[shoulder]*7,1,sides=4,axis=(1,0,0))
        # Restrained vambraces leave the hands and elbow joint readable.
        forearm=B[wrist]-B[elbow]
        s.tube("Oathkeeper leather bracer lining "+side,[B[elbow]+forearm*.18,B[elbow]+forearm*.83],
               [.080,.060],[.078,.059],[{elbow:.85,wrist:.15},{elbow:.2,wrist:.8}],11,sides=8)
        s.tube("Oathkeeper steel forearm guard "+side,[offset(B[elbow]+forearm*.22,z=.053),offset(B[elbow]+forearm*.80,z=.043)],
               [.060,.047],[.035,.026],[{elbow:.8,wrist:.2},{elbow:.2,wrist:.8}],1,sides=6)
        hip,knee,ankle=[f"{x}_{side}" for x in ["Hip","Knee","Ankle"]]
        s.tube("Oathkeeper articulated trouser underlayer "+side,[B[hip],B[knee],B[ankle]],
               [.087,.073,.057],[.080,.066,.055],[hip,knee,ankle],0,sides=8)
        # Long central surcoat halves stay narrower than the hip line.
        root=B["Root_M"]
        tabard=[root+np.array([sign*.016,.061,.151]),root+np.array([sign*.148,.061,.150]),
                root+np.array([sign*.146,-.265,.168]),root+np.array([sign*.075,-.323,.173]),
                root+np.array([sign*.025,-.282,.172])]
        skins=["Root_M","Root_M",{hip:.9,"Root_M":.1},{hip:.9,"Root_M":.1},{hip:.9,"Root_M":.1}]
        oathkeeper_panel(s,"Oathkeeper pointed blue surcoat "+side,tabard,skins,5,.008)
        s.tube("Oathkeeper pale surcoat hem "+side,tabard[2:],[.007]*3,[.004]*3,skins[2:],9,sides=4,axis=(0,0,1))
        kneecap=B[knee]
        coords=[(-.070,.060,.083),(0,.085,.109),(.070,.060,.083),(.066,-.043,.087),(0,-.073,.110),(-.066,-.043,.087)]
        oathkeeper_panel(s,"Oathkeeper compact knee guard "+side,[kneecap+np.array(v) for v in coords],[knee]*6,2,.008)
        thumb="ThumbFinger1_"+side
        if thumb in B:s.ellipsoid("Oathkeeper thumb-side glove seam "+side,B[thumb],(.021,.019,.020),thumb,11,sides=6)
        for prefix in ["MiddleToe1","MiddleToe2"]:
            toe=prefix+"_"+side
            if toe in B:s.ellipsoid("Oathkeeper toe underboot "+toe,offset(B[toe],y=.014),(.060,.020,.045),toe,0,sides=6)
    # One small, pale order stitch is earned at this tier; broad gilding belongs later.
    s.ellipsoid("Oathkeeper collar diamond stitch",offset(chest,y=.248,z=.106),(.021,.029,.005),"Chest_M",9,sides=4)


def oathkeeper_boots(s):
    """The connected leather travel boot reinforced with a fitted greave and toe plate."""
    novice_boots(s)
    for piece in s.pieces:piece["name"]=piece["name"].replace("Novice","Oathkeeper underboot")
    B=s.B
    for side in ["R","L"]:
        knee,ankle,toe,tip=[prefix+"_"+side for prefix in ["Knee","Ankle","MiddleToe1","MiddleToe2"]]
        high=offset(B[knee],y=-.050,z=.099);low=offset(B[ankle],y=.080,z=.099)
        points=[offset(high,x=-.074),offset(high,y=.018,z=.012),offset(high,x=.074),
                offset(low,x=.060),offset(low,y=-.025,z=.012),offset(low,x=-.060)]
        skins=[{knee:.85,ankle:.15}]*3+[ankle]*3
        oathkeeper_panel(s,"Oathkeeper tapered steel greave "+side,points,skins,2,.012)
        s.tube("Oathkeeper greave blue inset "+side,[offset(high,y=-.020,z=.016),offset(low,y=.020,z=.016)],
               [.021,.017],[.004,.004],[{knee:.8,ankle:.2},ankle],5,sides=4)
        floor=B[toe][1]-.044
        sections=[(B[toe][2]+.007,.100,.132,toe),(B[tip][2]+.010,.086,.090,tip),(B[tip][2]+.043,.062,.071,tip)]
        rings=[]
        for z,w,h,bone in sections:
            rings.append([np.array([B[ankle][0]+x,floor+y,z]) for x,y in [(-w,h*.78),(-w*.5,h),(w*.5,h),(w,h*.78)]])
        begin=len(s.data["positions"])
        for row in range(2):
            for j in range(3):
                pts=[rings[row][j],rings[row+1][j],rings[row+1][j+1],rings[row][j+1]]
                skins=[sections[row][3],sections[row+1][3],sections[row+1][3],sections[row][3]]
                s.triangle([pts[0],pts[1],pts[2]],[skins[0],skins[1],skins[2]],2)
                s.triangle([pts[0],pts[2],pts[3]],[skins[0],skins[2],skins[3]],2)
        s.pieces.append({"name":"Oathkeeper fitted toe plate "+side,"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})


def oathkeeper_helmet():
    """Low open iron sallet: a complete crown, guarded temples and a blue rear lining."""
    s=Surface(["Head_M"],np.zeros((1,3)))
    count=12;rings=[]
    for row,(radius,height) in enumerate([(1.,.0),(.98,.335),(.67,.429),(.22,.462)]):
        ring=[]
        for j in range(count):
            angle=2*np.pi*j/count;x,z=np.cos(angle),np.sin(angle)
            base=.093+.159*max(z,0)+.016*max(-z,0)
            y=base if row==0 else height
            ring.append(np.array([x*.223*radius,y,z*.208*radius-.027]))
        rings.append(ring)
    begin=len(s.data["positions"])
    for row in range(3):
        for j in range(count):
            k=(j+1)%count
            color=2 if row>0 or (j in [1,2,3,4]) else 1
            s.triangle([rings[row][j],rings[row+1][j],rings[row+1][k]],["Head_M"]*3,color)
            s.triangle([rings[row][j],rings[row+1][k],rings[row][k]],["Head_M"]*3,color)
    apex=np.array([0,.469,-.027])
    for j in range(count):
        k=(j+1)%count
        s.triangle([rings[-1][j],apex,rings[-1][k]],["Head_M"]*3,2)
        a,b=rings[0][j],rings[0][k]
        s.triangle([a,offset(a,y=-.014),offset(b,y=-.014)],["Head_M"]*3,1)
        s.triangle([a,offset(b,y=-.014),b],["Head_M"]*3,1)
    s.pieces.append({"name":"Oathkeeper open sallet crown and temples","vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
    # A flush dark structural strap breaks up the bright crown at native camera scale.
    strap_centers=[ring[9] for ring in rings]+[apex]+[ring[3] for ring in reversed(rings)]
    strap_rings=[]
    for point in strap_centers:
        outward=point-np.array([0,.240,-.027]);outward[0]=0
        outward/=np.linalg.norm(outward)
        center=point+outward*.005
        strap_rings.append([offset(center,x=-.019),offset(center,x=.019)])
    begin=len(s.data["positions"])
    for row in range(len(strap_rings)-1):
        points=strap_rings[row]+list(reversed(strap_rings[row+1]))
        for ids in [(0,1,2),(0,2,3)]:
            tri=[points[i] for i in ids]
            outward=np.mean(tri,axis=0)-np.array([0,.240,-.027]);outward[0]=0
            if np.dot(np.cross(tri[1]-tri[0],tri[2]-tri[0]),outward)<0:tri.reverse()
            s.triangle(tri,["Head_M"]*3,1)
    s.pieces.append({"name":"Oathkeeper flush crown reinforcing strap","vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
    # Raised brow bar frames the face without a visor or nose guard.
    brow=[np.array([-.187,.220,.093]),np.array([-.112,.280,.156]),np.array([0,.294,.190]),
          np.array([.112,.280,.156]),np.array([.187,.220,.093])]
    s.tube("Oathkeeper plain brow reinforcement",brow,[.021]*5,[.010]*5,["Head_M"]*5,1,sides=4,axis=(0,1,0))
    # Rear-only lining can sit behind native ears and hair without masking the face.
    s.tube("Oathkeeper blue nape lining",[np.array([0,.091,-.190]),np.array([0,.176,-.205])],
           [.169,.184],[.026,.026],["Head_M"]*2,5,sides=8)
    s.ellipsoid("Oathkeeper small brow rivet",np.array([0,.312,.184]),(.012,.016,.006),"Head_M",3,sides=6)
    return s


def highward_torso_skin(s,y):
    """Follow the original fitted garment's spine rows at each breastplate height."""
    rows=[("BackA_M",s.B["BackA_M"][1]),("BackB_M",s.B["BackB_M"][1]),
          ("Chest_M",s.B["Chest_M"][1]+.08)]
    if y<=rows[0][1]:return rows[0][0]
    for (low,low_y),(high,high_y) in zip(rows,rows[1:]):
        if y<high_y:
            amount=(y-low_y)/(high_y-low_y)
            if amount<=1e-8:return low
            return {low:1-amount,high:amount}
    return "Chest_M"


def highward_shoulder(s,side,sign):
    """Two shallow overlapping shells, with a stepped silhouette and restrained gold hems."""
    B=s.B;shoulder="Shoulder_"+side;scap="Scapula_"+side
    for layer,rows in enumerate([
            [(-.052,.125,.141),(.032,.148,.156),(.157,.113,.139)],
            [(.105,.089,.139),(.177,.098,.144),(.233,.047,.117)]]):
        base=offset(B[shoulder],y=.012 if layer==0 else -.022)
        rings=[[base+np.array([sign*x,np.sin(angle)*height,np.cos(angle)*depth])
                for angle in np.linspace(0,np.pi,7)] for x,height,depth in rows]
        begin=len(s.data["positions"])
        for row in range(2):
            for j in range(6):
                points=[rings[row][j],rings[row+1][j],rings[row+1][j+1],rings[row][j+1]]
                skins=[{scap:.20,shoulder:.80},shoulder,shoulder,{scap:.20,shoulder:.80}] if layer==0 and row==0 else [shoulder]*4
                for ids in [(0,1,2),(0,2,3)]:
                    tri=[points[i] for i in ids];weights=[skins[i] for i in ids]
                    exterior=np.mean(tri,axis=0)-base;exterior[0]=0
                    if np.dot(np.cross(tri[1]-tri[0],tri[2]-tri[0]),exterior)<0:
                        tri.reverse();weights.reverse()
                    s.triangle(tri,weights,2 if layer==0 else 1)
        s.pieces.append({"name":"Highward overlapping shoulder shell "+side+str(layer),"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
        s.tube("Highward shoulder gold hem "+side+str(layer),rings[-1],[.010]*7,[.006]*7,[shoulder]*7,4,sides=4,axis=(1,0,0))
        for j in [0,6]:
            s.tube("Highward shoulder side hem "+side+str(layer)+str(j),[ring[j] for ring in rings],
                   [.008]*3,[.005]*3,[shoulder]*3,4,sides=4,axis=(0,1,0))


def highward_armor(s,male):
    """Practical silver cuirass and compact guards distinguish the midgame guardian."""
    novice_armor(s,male)
    for uv in s.data["uvs"]:
        color=int(uv[0]*len(PALETTE))
        uv[0]=({6:5,9:0,11:1,3:4}.get(color,color)+.5)/len(PALETTE)
    for piece in s.pieces:piece["name"]=piece["name"].replace("Novice","Highward fitted underlayer")
    B=s.B;chest=B["Chest_M"]
    outline=[(-.134,.231,.139),(-.214,.167,.143),(-.237,-.025,.151),(-.186,-.118,.162),
             (-.080,-.183,.171),(0,-.199,.178),(.080,-.183,.171),(.186,-.118,.162),
             (.237,-.025,.151),(.214,.167,.143),(.134,.231,.139)]
    front=[chest+np.array(point) for point in outline]
    skins=[highward_torso_skin(s,point[1]) for point in front]
    center_skin=highward_torso_skin(s,float(np.mean(front,axis=0)[1]))
    oathkeeper_panel(s,"Highward single shield cuirass",front,skins,2,.014,center_skin=center_skin)
    edge=[offset(point,z=.005) for point in front]
    s.tube("Highward narrow gold cuirass frame",edge+[edge[0]],[.008]*(len(edge)+1),[.005]*(len(edge)+1),skins+[skins[0]],4,sides=4)
    banner=[chest+np.array(point) for point in [(-.054,.215,.162),(.054,.215,.162),(.059,-.105,.191),(0,-.149,.201),(-.059,-.105,.191)]]
    banner_skin=[highward_torso_skin(s,point[1]) for point in banner]
    oathkeeper_panel(s,"Highward royal blue chest inset",banner,banner_skin,5,.005,
                     center_skin=highward_torso_skin(s,float(np.mean(banner,axis=0)[1])))
    emblem(s,offset(chest,y=.060,z=.203),"Chest_M",.054)
    # A low neck guard does not become a jaw-covering gorget.
    s.tube("Highward low steel collar",[offset(B["Neck_M"],y=-.055),offset(B["Neck_M"],y=-.010)],
           [.127,.113],[.084,.078],["Neck_M"]*2,1,sides=10)
    s.tube("Highward collar gold edge",[offset(B["Neck_M"],y=-.015),offset(B["Neck_M"],y=-.006)],
           [.116,.113],[.081,.078],["Neck_M"]*2,4,sides=10)
    s.ellipsoid("Highward square belt clasp",offset(B["Root_M"],y=.088,z=.159),(.037,.029,.010),"Root_M",4,sides=4)
    for side,sign in [("R",1),("L",-1)]:
        shoulder,elbow,wrist=[name+"_"+side for name in ["Shoulder","Elbow","Wrist"]]
        highward_shoulder(s,side,sign)
        forearm=B[wrist]-B[elbow]
        skins=[{elbow:.9,wrist:.1},{elbow:.25,wrist:.75}]
        s.tube("Highward articulated steel bracer "+side,[B[elbow]+forearm*.15,B[elbow]+forearm*.82],
               [.083,.064],[.081,.063],skins,1,sides=8)
        s.tube("Highward silver bracer ridge "+side,[offset(B[elbow]+forearm*.19,z=.073),offset(B[elbow]+forearm*.80,z=.057)],
               [.054,.044],[.017,.013],skins,2,sides=6)
        unit=forearm/np.linalg.norm(forearm)
        cuff=B[elbow]+forearm*.80
        s.tube("Highward bracer narrow gold edge "+side,[cuff-unit*.014,cuff+unit*.003],[.069]*2,[.068]*2,[skins[-1]]*2,4,sides=8)
        hip,knee,ankle=[name+"_"+side for name in ["Hip","Knee","Ankle"]]
        s.tube("Highward trouser underlayer "+side,[B[hip],B[knee],B[ankle]],
               [.087,.073,.057],[.080,.066,.055],[hip,knee,ankle],0,sides=8)
        root=B["Root_M"]
        for face,z,length in [("front",.168,.343),("back",-.110,.315)]:
            points=[root+np.array([sign*.016,.073,z]),root+np.array([sign*.173,.073,z*.94]),
                    root+np.array([sign*.201,-length+.018,z*1.05]),root+np.array([sign*.041,-length,z*1.08])]
            lower={hip:.73,knee:.27}
            panel_skins=["Root_M","Root_M",lower,lower]
            # Back panels reverse their exterior while retaining separated front/reverse surfaces.
            if face=="back":
                points=[np.array([point[0],point[1],-point[2]]) for point in points]
                begin=len(s.data["positions"])
                oathkeeper_panel(s,"Highward long divided tabard "+face+side,points,panel_skins,5,.008)
                for i in range(begin,len(s.data["positions"])):
                    s.data["positions"][i][2]*=-1;s.data["normals"][i][2]*=-1
                for tri in s.data["triangles"]:
                    if tri[0]>=begin:tri.reverse()
                points=[np.array([point[0],point[1],-point[2]]) for point in points]
            else:
                oathkeeper_panel(s,"Highward long divided tabard "+face+side,points,panel_skins,5,.008)
            s.tube("Highward gold tabard hem "+face+side,[points[1],points[2],points[3]],
                   [.008]*3,[.005]*3,[panel_skins[1],lower,lower],4,sides=4,axis=(0,0,1))
        knee_center=B[knee]
        points=[knee_center+np.array(point) for point in [(-.078,.073,.093),(0,.105,.116),(.078,.073,.093),(.081,-.044,.097),(0,-.088,.125),(-.081,-.044,.097)]]
        oathkeeper_panel(s,"Highward framed knee shield "+side,points,[knee]*6,2,.010)
        s.tube("Highward gold knee edge "+side,[offset(p,z=.005) for p in points+[points[0]]],
               [.006]*7,[.004]*7,[knee]*7,4,sides=4)
        thumb="ThumbFinger1_"+side
        if thumb in B:s.ellipsoid("Highward thumb glove seam "+side,B[thumb],(.021,.019,.020),thumb,1,sides=6)
        for prefix in ["MiddleToe1","MiddleToe2"]:
            toe=prefix+"_"+side
            if toe in B:s.ellipsoid("Highward toe underboot "+toe,offset(B[toe],y=.014),(.060,.020,.045),toe,0,sides=6)
    highward_plain_fittings(s)


def highward_plain_fittings(s):
    """Reserve gilded borders for endgame; retain one small order seal as a family mark."""
    for piece in s.pieces:
        if piece["name"].startswith("Order ") or piece["name"]=="Suspended oath bead":continue
        for index in range(piece["vertex_start"],piece["vertex_start"]+piece["vertex_count"]):
            if int(s.data["uvs"][index][0]*len(PALETTE))==4:
                s.data["uvs"][index][0]=(1+.5)/len(PALETTE)
        piece["name"]=piece["name"].replace("gold","steel")


def highward_boots(s):
    """Steel-faced continuous boots with a segmented instep and narrow gold frame."""
    oathkeeper_boots(s)
    for uv in s.data["uvs"]:
        if int(uv[0]*len(PALETTE))==11:uv[0]=(1+.5)/len(PALETTE)
    for piece in s.pieces:piece["name"]=piece["name"].replace("Oathkeeper","Highward foundation")
    B=s.B
    for side in ["R","L"]:
        knee,ankle,toe,tip=[name+"_"+side for name in ["Knee","Ankle","MiddleToe1","MiddleToe2"]]
        high=offset(B[knee],y=-.050,z=.101);low=offset(B[ankle],y=.080,z=.101)
        border=[offset(high,x=-.078),offset(high,y=.020,z=.012),offset(high,x=.078),
                offset(low,x=.065),offset(low,y=-.027,z=.012),offset(low,x=-.065)]
        skins=[{knee:.85,ankle:.15}]*3+[ankle]*3
        s.tube("Highward gold greave frame "+side,border+[border[0]],[.007]*7,[.005]*7,skins+[skins[0]],4,sides=4)
        # Two thin overlapping instep plates extend the steel above the retained toe plate.
        floor=B[toe][1]-.044
        for index,(z0,z1,y0,y1) in enumerate([(B[ankle][2]+.027,B[ankle][2]+.105,.193,.164),
                                              (B[ankle][2]+.104,B[toe][2]+.031,.169,.122)]):
            skin=ankle if index==0 else {ankle:.6,toe:.4}
            widths=[.092,.100]
            rings=[]
            for z,y,w in [(z0,y0,widths[0]),(z1,y1,widths[1])]:
                rings.append([np.array([B[ankle][0]+x,floor+h,z]) for x,h in [(-w,y*.82),(-w*.5,y),(w*.5,y),(w,y*.82)]])
            start=len(s.data["positions"])
            for j in range(3):
                pts=[rings[0][j],rings[1][j],rings[1][j+1],rings[0][j+1]]
                s.triangle([pts[0],pts[1],pts[2]],[skin]*3,2)
                s.triangle([pts[0],pts[2],pts[3]],[skin]*3,2)
            s.pieces.append({"name":"Highward articulated instep plate "+side+str(index),"vertex_start":start,"vertex_count":len(s.data["positions"])-start})
            s.tube("Highward instep dark articulation "+side+str(index),rings[1],[.005]*4,[.004]*4,[skin]*4,1,sides=4,axis=(0,0,1))
    highward_plain_fittings(s)


def highward_helmet():
    """Face-clear steel helmet with a practical low keel and one small order seal."""
    s=oathkeeper_helmet()
    for piece in s.pieces:
        if any(label in piece["name"] for label in ["brow reinforcement","flush crown reinforcing strap","small brow rivet"]):
            for index in range(piece["vertex_start"],piece["vertex_start"]+piece["vertex_count"]):
                s.data["uvs"][index][0]=(4+.5)/len(PALETTE)
        piece["name"]=piece["name"].replace("Oathkeeper","Highward foundation")
    # The low metal keel changes the profile without a plume or tall side horns.
    sections=[(.183,.330,.348,.021),(.112,.425,.455,.023),(-.014,.461,.503,.025),
              (-.115,.443,.475,.023),(-.222,.331,.357,.019)]
    rings=[[np.array([x,y,z]) for x,y in [(-w,low),(w,low),(w*.65,high),(-w*.65,high)]]
           for z,low,high,w in sections]
    begin=len(s.data["positions"])
    for row in range(len(rings)-1):
        interior=np.mean(rings[row]+rings[row+1],axis=0)
        for j in range(4):
            k=(j+1)%4;points=[rings[row][j],rings[row+1][j],rings[row+1][k],rings[row][k]]
            exterior=np.mean(points,axis=0)-interior
            for ids in [(0,1,2),(0,2,3)]:
                tri=[points[i] for i in ids]
                if np.dot(np.cross(tri[1]-tri[0],tri[2]-tri[0]),exterior)<0:tri.reverse()
                s.triangle(tri,["Head_M"]*3,2)
    for ring,direction in [(rings[0],1),(rings[-1],-1)]:
        for ids in [(0,1,2),(0,2,3)]:
            tri=[ring[i] for i in ids]
            if np.cross(tri[1]-tri[0],tri[2]-tri[0])[2]*direction<0:tri.reverse()
            s.triangle(tri,["Head_M"]*3,2)
    s.pieces.append({"name":"Highward solid low crown keel","vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
    emblem(s,np.array([0,.323,.207]),"Head_M",.040)
    for sign in [-1,1]:
        s.tube("Highward temple gold binding "+str(sign),[np.array([sign*.227,.108,-.015]),np.array([sign*.225,.303,-.015])],
               [.008]*2,[.010]*2,["Head_M"]*2,4,sides=4)
    highward_plain_fittings(s)
    return s


def mercy_remove_underlayers(s,labels):
    """Remove covered starter pieces so differently weighted cloth cannot intersect in idle."""
    removed=set()
    for piece in s.pieces:
        if any(label in piece["name"] for label in labels):
            removed.update(range(piece["vertex_start"],piece["vertex_start"]+piece["vertex_count"]))
    keep=[i for i in range(len(s.data["positions"])) if i not in removed]
    mapping={old:new for new,old in enumerate(keep)}
    for field in ["positions","normals","uvs","joints","weights"]:
        s.data[field]=[s.data[field][i] for i in keep]
    s.data["triangles"]=[[mapping[i] for i in tri] for tri in s.data["triangles"] if all(i in mapping for i in tri)]
    pieces=[]
    for piece in s.pieces:
        remaining=[mapping[i] for i in range(piece["vertex_start"],piece["vertex_start"]+piece["vertex_count"]) if i in mapping]
        if remaining:pieces.append(dict(piece,vertex_start=min(remaining),vertex_count=len(remaining)))
    s.pieces=pieces


def mercy_cloth_panel(s,label,points,skins,depth=.009,center_skin=None):
    begin=len(s.data["positions"])
    oathkeeper_panel(s,label,points,skins,9,depth,center_skin)
    # Ivory cloth has ivory reverse faces, including the visible divided skirt interiors.
    for i in range(begin,len(s.data["positions"])):
        if int(s.data["uvs"][i][0]*len(PALETTE))==1:s.data["uvs"][i][0]=(9+.5)/len(PALETTE)


def mercy_torso_skin(s,y):
    rows=[("Root_M",s.B["Root_M"][1]+.045),("BackA_M",s.B["BackA_M"][1]),
          ("BackB_M",s.B["BackB_M"][1]),("Chest_M",s.B["Chest_M"][1]+.080)]
    if y<=rows[0][1]:return rows[0][0]
    for (low,low_y),(high,high_y) in zip(rows,rows[1:]):
        if y<high_y:
            amount=(y-low_y)/(high_y-low_y)
            return low if amount<=1e-8 else {low:1-amount,high:amount}
    return "Chest_M"


def mercy_mantle(s):
    """A thin closed cloth capelet with a neck fold and a soft shoulder-length hem."""
    B=s.B;center=offset(B["Neck_M"],y=.008,z=.010);count=16
    rings=[];weights=[]
    for row,(width,depth,height) in enumerate([(.119,.083,.0),(.239,.139,.013),(.384,.192,-.006)]):
        ring=[];skins=[]
        for j in range(count):
            angle=2*np.pi*j/count;x,z=np.cos(angle),np.sin(angle)
            ring.append(center+np.array([x*width,height-abs(z)*(.025 if row==0 else .130*row/2),z*depth]))
            if row==0:skin={"Neck_M":.45,"Chest_M":.55}
            else:
                side="R" if x>=0 else "L"
                amount=abs(x)*(.70 if row==1 else .90)
                skin="Chest_M" if amount<1e-8 else {"Chest_M":1-amount,"Shoulder_"+side:amount*.78,"Scapula_"+side:amount*.22}
            skins.append(skin)
        rings.append(ring);weights.append(skins)
    begin=len(s.data["positions"])
    for row in range(2):
        for j in range(count):
            k=(j+1)%count
            points=[rings[row][j],rings[row+1][j],rings[row+1][k],rings[row][k]]
            skins=[weights[row][j],weights[row+1][j],weights[row+1][k],weights[row][k]]
            for ids in [(0,1,2),(0,2,3)]:
                tri=[points[i] for i in ids];skin=[skins[i] for i in ids]
                if np.cross(tri[1]-tri[0],tri[2]-tri[0])[1]<0:tri.reverse();skin.reverse()
                s.triangle(tri,skin,9)
                s.triangle([offset(p,y=-.009) for p in reversed(tri)],list(reversed(skin)),0)
    for row in [0,2]:
        for j in range(count):
            k=(j+1)%count;a,b=rings[row][j],rings[row][k];wa,wb=weights[row][j],weights[row][k]
            points=[a,b,offset(b,y=-.009),offset(a,y=-.009)]
            skins=[wa,wb,wb,wa]
            if row==0:points.reverse();skins.reverse()
            s.triangle(points[:3],skins[:3],3)
            s.triangle([points[0],points[2],points[3]],[skins[0],skins[2],skins[3]],3)
    s.pieces.append({"name":"Mercy closed ivory prayer mantle","vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
    for row,color,size in [(0,0,.010),(2,3,.008)]:
        s.tube("Mercy mantle bound edge "+str(row),rings[row]+[rings[row][0]],
               [size]*(count+1),[.005]*(count+1),weights[row]+[weights[row][0]],color,sides=4,axis=(0,1,0))
    # A narrow blue fold outlines the neck opening without raising a heavy gorget.
    s.tube("Mercy folded blue collar",[offset(B["Neck_M"],y=-.006),offset(B["Neck_M"],y=.021)],
           [.116,.109],[.079,.074],["Neck_M"]*2,5,sides=12)


def mercy_armor(s,male):
    """Ivory prayer panels and a sheltering mantle over a fitted blue guardian's jack."""
    novice_armor(s,male)
    mercy_remove_underlayers(s,["Novice coat skirt"])
    for uv in s.data["uvs"]:
        color=int(uv[0]*len(PALETTE));uv[0]=({6:5,11:0}.get(color,color)+.5)/len(PALETTE)
    for piece in s.pieces:piece["name"]=piece["name"].replace("Novice","Mercy fitted underlayer")
    B=s.B;chest=B["Chest_M"];root=B["Root_M"]
    mercy_mantle(s)
    for side,sign in [("R",1),("L",-1)]:
        # The long prayer stole follows the garment's spine rows and continues below its belt.
        points=[chest+np.array([sign*.035,.171,.153]),chest+np.array([sign*.187,.170,.144]),
                chest+np.array([sign*.205,-.025,.155]),root+np.array([sign*.166,.076,.157]),
                root+np.array([sign*.039,.071,.161])]
        skins=[mercy_torso_skin(s,p[1]) for p in points]
        mercy_cloth_panel(s,"Mercy ivory prayer stole chest "+side,points,skins,.008,
                         center_skin=mercy_torso_skin(s,float(np.mean(points,axis=0)[1])))
        inner=[offset(points[0],z=.006),offset(points[-1],z=.006)]
        s.tube("Mercy muted gold stole inner seam "+side,inner,[.007]*2,[.004]*2,[skins[0],skins[-1]],3,sides=4)
        hip,knee,ankle=[name+"_"+side for name in ["Hip","Knee","Ankle"]]
        lower={hip:.84,knee:.16}
        tail=[root+np.array([sign*.037,.080,.177]),root+np.array([sign*.168,.080,.166]),
              root+np.array([sign*.197,-.328,.180]),root+np.array([sign*.152,-.371,.190]),
              root+np.array([sign*.044,-.363,.190])]
        tail_skins=["Root_M","Root_M",lower,lower,lower]
        mercy_cloth_panel(s,"Mercy rounded divided prayer stole "+side,tail,tail_skins,.009)
        s.tube("Mercy muted gold stole hem "+side,tail[2:],[.008]*3,[.004]*3,[lower]*3,3,sides=4)
        # A back coat panel stays short enough to leave native backpack and leg motion visible.
        back=[root+np.array([sign*.033,.070,-.112]),root+np.array([sign*.174,.070,-.103]),
              root+np.array([sign*.194,-.295,-.127]),root+np.array([sign*.045,-.330,-.133])]
        begin=len(s.data["positions"])
        back=[np.array([p[0],p[1],-p[2]]) for p in back]
        mercy_cloth_panel(s,"Mercy ivory rear coat panel "+side,back,["Root_M","Root_M",lower,lower],.008)
        for i in range(begin,len(s.data["positions"])):
            s.data["positions"][i][2]*=-1;s.data["normals"][i][2]*=-1
        for tri in s.data["triangles"]:
            if tri[0]>=begin:tri.reverse()
        s.tube("Mercy blue trouser underlayer "+side,[B[hip],B[knee],B[ankle]],
               [.087,.073,.057],[.080,.066,.055],[hip,knee,ankle],0,sides=8)
        # Small rounded knee plates and cloth-wrapped bracers keep the supporting silhouette light.
        coords=[(-.063,.051,.086),(0,.070,.100),(.063,.051,.086),(.061,-.038,.090),(0,-.057,.107),(-.061,-.038,.090)]
        oathkeeper_panel(s,"Mercy rounded silver knee plate "+side,[B[knee]+np.array(p) for p in coords],[knee]*6,2,.008)
        elbow,wrist=[name+"_"+side for name in ["Elbow","Wrist"]];forearm=B[wrist]-B[elbow]
        skins=[{elbow:.85,wrist:.15},{elbow:.2,wrist:.8}]
        s.tube("Mercy wrapped ivory bracer "+side,[B[elbow]+forearm*.18,B[elbow]+forearm*.83],
               [.081,.063],[.079,.062],skins,9,sides=8)
        s.tube("Mercy blue bracer guard "+side,[offset(B[elbow]+forearm*.24,z=.068),offset(B[elbow]+forearm*.78,z=.056)],
               [.051,.040],[.020,.014],skins,1,sides=6)
        unit=forearm/np.linalg.norm(forearm);cuff=B[elbow]+forearm*.82
        s.tube("Mercy bracer bronze binding "+side,[cuff-unit*.012,cuff+unit*.003],[.068]*2,[.066]*2,[skins[-1]]*2,3,sides=8)
        thumb="ThumbFinger1_"+side
        if thumb in B:s.ellipsoid("Mercy glove thumb seam "+side,B[thumb],(.021,.019,.020),thumb,0,sides=6)
        for prefix in ["MiddleToe1","MiddleToe2"]:
            toe=prefix+"_"+side
            if toe in B:s.ellipsoid("Mercy toe underboot "+toe,offset(B[toe],y=.014),(.060,.020,.045),toe,0,sides=6)
    # A clasp, not a weapon-like crest, is the focal point between the mantle and stole.
    center=offset(chest,y=.182,z=.182)
    s.ellipsoid("Mercy circular muted gold mantle clasp",center,(.039,.039,.009),"Chest_M",3,sides=8)
    s.ellipsoid("Mercy ivory clasp inset",offset(center,z=.010),(.025,.025,.006),"Chest_M",9,sides=8)
    s.ellipsoid("Mercy blue clasp bead",offset(center,z=.017),(.008,.014,.004),"Chest_M",5,sides=6)


def mercy_boots(s):
    """Dark continuous boots with ivory gaiters and small, fitted silver shin guards."""
    novice_boots(s)
    mercy_remove_underlayers(s,["Novice turned boot cuff","Novice boot ankle strap","Novice ankle strap stud"])
    for uv in s.data["uvs"]:
        if int(uv[0]*len(PALETTE))==11:uv[0]=(0+.5)/len(PALETTE)
    for piece in s.pieces:piece["name"]=piece["name"].replace("Novice","Mercy underboot")
    B=s.B
    for side in ["R","L"]:
        knee,ankle=[name+"_"+side for name in ["Knee","Ankle"]]
        top=offset(B[ankle],y=.257,z=.018);bottom=offset(B[ankle],y=.059,z=.006)
        skin={knee:.40,ankle:.60}
        s.tube("Mercy ivory calf gaiter "+side,[top,offset(top,y=-.065),bottom],
               [.106,.103,.096],[.100,.100,.099],[skin,{knee:.22,ankle:.78},ankle],9,sides=10)
        s.tube("Mercy gaiter bronze top binding "+side,[offset(top,y=.004),offset(top,y=-.012)],
               [.110]*2,[.104]*2,[skin]*2,3,sides=10)
        s.tube("Mercy ivory gaiter lower binding "+side,[offset(bottom,y=.012),offset(bottom,y=-.007)],
               [.100]*2,[.103]*2,[ankle]*2,9,sides=10)
        high=offset(top,y=-.023,z=.098);low=offset(bottom,y=.002,z=.103)
        points=[offset(high,x=-.050),offset(high,y=.013,z=.006),offset(high,x=.050),
                offset(low,x=.042),offset(low,y=-.016,z=.005),offset(low,x=-.042)]
        oathkeeper_panel(s,"Mercy short silver shin guard "+side,points,[skin]*3+[ankle]*3,2,.008)
        s.tube("Mercy shin blue seam "+side,[offset(high,y=-.010,z=.011),offset(low,y=.014,z=.011)],
               [.011]*2,[.004]*2,[skin,ankle],5,sides=4)


def mercy_helmet():
    """A rounded full-crown chapel helm with an open face and a quiet bronze brow frame."""
    s=Surface(["Head_M"],np.zeros((1,3)));count=16;rings=[]
    for row,(radius,height) in enumerate([(1.,0),(.99,.316),(.83,.404),(.46,.452)]):
        ring=[]
        for j in range(count):
            angle=2*np.pi*j/count;x,z=np.cos(angle),np.sin(angle)
            base=.108+.157*max(z,0)+.014*max(-z,0)
            ring.append(np.array([x*.226*radius,base if row==0 else height,z*.212*radius-.030]))
        rings.append(ring)
    begin=len(s.data["positions"])
    for row in range(3):
        for j in range(count):
            k=(j+1)%count
            color=2 if row>0 else (9 if j in range(1,7) else 1)
            s.triangle([rings[row][j],rings[row+1][j],rings[row+1][k]],["Head_M"]*3,color)
            s.triangle([rings[row][j],rings[row+1][k],rings[row][k]],["Head_M"]*3,color)
    apex=np.array([0,.466,-.030])
    for j in range(count):
        k=(j+1)%count
        s.triangle([rings[-1][j],apex,rings[-1][k]],["Head_M"]*3,2)
        a,b=rings[0][j],rings[0][k]
        s.triangle([a,offset(a,y=-.010),offset(b,y=-.010)],["Head_M"]*3,0)
        s.triangle([a,offset(b,y=-.010),b],["Head_M"]*3,0)
    s.pieces.append({"name":"Mercy rounded chapel helm crown","vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
    # Two ivory side bindings distinguish the rounded crown from the central ridge of Highward.
    for sign,index in [(1,1),(-1,7)]:
        tangent=np.array([-np.sin(index*np.pi/8),0,np.cos(index*np.pi/8)])
        strips=[]
        for row in range(4):
            point=rings[row][index]
            outward=point-np.array([0,.235,-.030]);outward/=np.linalg.norm(outward)
            center=point+outward*.003
            width=.018 if row<3 else .010
            strips.append([center-tangent*width,center+tangent*width])
        begin=len(s.data["positions"])
        for row in range(3):
            points=strips[row]+list(reversed(strips[row+1]))
            for ids in [(0,1,2),(0,2,3)]:
                tri=[points[i] for i in ids]
                exterior=np.mean(tri,axis=0)-np.array([0,.235,-.030])
                if np.dot(np.cross(tri[1]-tri[0],tri[2]-tri[0]),exterior)<0:tri.reverse()
                s.triangle(tri,["Head_M"]*3,9)
        s.pieces.append({"name":"Mercy flush ivory crown binding "+str(sign),"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
    brow=[np.array([-.197,.208,.085]),np.array([-.134,.270,.146]),np.array([-.055,.283,.184]),
          np.array([.055,.283,.184]),np.array([.134,.270,.146]),np.array([.197,.208,.085])]
    s.tube("Mercy open bronze brow frame",brow,[.015]*6,[.008]*6,["Head_M"]*6,3,sides=4,axis=(0,1,0))
    s.ellipsoid("Mercy ivory brow clasp",np.array([0,.307,.188]),(.024,.025,.007),"Head_M",9,sides=8)
    s.tube("Mercy blue nape lining",[np.array([0,.100,-.193]),np.array([0,.175,-.208])],
           [.171,.187],[.025,.025],["Head_M"]*2,5,sides=8)
    return s


def censure_plate(s,label,points,skins,color=0,center_skin=None):
    """Closed angular plate with an authored shallow central ridge and subdued reverse."""
    points=[np.array(p) for p in points];center=np.mean(points,axis=0);center[2]+=.017
    if center_skin is None:
        center_skin={}
        for skin in skins:
            for bone,weight in ({skin:1.} if isinstance(skin,str) else skin).items():
                center_skin[bone]=center_skin.get(bone,0)+weight/len(skins)
    rear=[offset(p,z=-.010) for p in points];back=np.mean(rear,axis=0)
    begin=len(s.data["positions"])
    for j in range(len(points)):
        k=(j+1)%len(points);a,b=j,k
        if np.cross(points[a]-center,points[b]-center)[2]<0:a,b=b,a
        s.triangle([center,points[a],points[b]],[center_skin,skins[a],skins[b]],color)
        s.triangle([back,rear[b],rear[a]],[center_skin,skins[b],skins[a]],7)
        s.triangle([points[a],rear[a],rear[b]],[skins[a],skins[a],skins[b]],1)
        s.triangle([points[a],rear[b],points[b]],[skins[a],skins[b],skins[b]],1)
    s.pieces.append({"name":label,"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})


def censure_shoulder(s,side,sign):
    """An asymmetric layered judgment mantle, with the larger pauldron on the weapon arm."""
    shoulder="Shoulder_"+side;scap="Scapula_"+side;root=s.B[shoulder]
    for label,color,rows in [
            ("battle mantle",6,[(-.058,.069,.131),(.101,.081,.140),(.216,.012,.127)]),
            ("angular steel cap",0,[(-.049,.112,.137),(.059,.132,.148),(.163,.076,.133)])]:
        rings=[]
        for x,high,depth in rows:
            rings.append([root+np.array([sign*x,y,z]) for y,z in [(-.015,depth),(high*.72,depth*.81),(high,0),(high*.72,-depth*.81),(-.015,-depth)]])
        begin=len(s.data["positions"])
        for row in range(2):
            for j in range(4):
                points=[rings[row][j],rings[row+1][j],rings[row+1][j+1],rings[row][j+1]]
                inner={scap:.23,shoulder:.77}
                skins=[inner,shoulder,shoulder,inner] if row==0 else [shoulder]*4
                for ids in [(0,1,2),(0,2,3)]:
                    tri=[points[i] for i in ids];weights=[skins[i] for i in ids]
                    outward=np.mean(tri,axis=0)-root;outward[0]=0
                    if np.dot(np.cross(tri[1]-tri[0],tri[2]-tri[0]),outward)<0:tri.reverse();weights.reverse()
                    s.triangle(tri,weights,color)
                    # The thin underside closes the visible mantle and cap edge without coplanar faces.
                    s.triangle([offset(p,y=-.006) for p in reversed(tri)],list(reversed(weights)),7)
        s.pieces.append({"name":"Censure compact "+label+side,"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
        s.tube("Censure bound shoulder edge "+label+side,rings[-1],[.009]*5,[.006]*5,[shoulder]*5,1 if color==0 else 7,sides=4,axis=(1,0,0))
        for j in [0,4]:
            s.tube("Censure shoulder side seam "+label+side+str(j),[ring[j] for ring in rings],
                   [.006]*3,[.005]*3,[shoulder]*3,1 if color==0 else 7,sides=4,axis=(0,1,0))
    for z in [-.094,.094]:
        s.ellipsoid("Censure shoulder fastening "+side+str(z),offset(root,x=sign*.043,y=.099,z=z),(.010,.009,.009),shoulder,3,sides=6)
    # Armor grows above and outside the shoulder, preserving the native torso and head scale.
    layers=3 if side=="R" else 2
    for layer in range(layers):
        reach=.235+layer*.076
        high=.218+layer*.035 if side=="R" else .162+layer*.025
        coords=[(.015+layer*.050,.125,.157),(.105+layer*.060,high,.169),
                (reach,high+.057,.123),(reach+.036,.028-layer*.026,.140),
                (.145+layer*.063,-.028-layer*.034,.178)]
        front=[root+np.array([sign*x,y,z]) for x,y,z in coords]
        censure_plate(s,"Censure judgment wing "+side+str(layer),front,[shoulder]*5,7 if layer%2==0 else 0)
        s.tube("Censure gilded wing blade "+side+str(layer),front[1:4],
               [.013]*3,[.007]*3,[shoulder]*3,4,sides=4)
        # A raised red lozenge gives the dark plate a readable inner plane.
        center=root+np.array([sign*(.125+layer*.062),high-.044,.190])
        s.ellipsoid("Censure wing crimson inlay "+side+str(layer),center,(.030,.058,.007),shoulder,6,sides=4)


def censure_armor(s,male):
    """Endgame judgment armor with gilded chevrons, a blade mantle and deep crimson cloth."""
    novice_armor(s,male)
    mercy_remove_underlayers(s,["Novice coat skirt"])
    for uv in s.data["uvs"]:
        color=int(uv[0]*len(PALETTE));uv[0]=({9:7,11:7}.get(color,color)+.5)/len(PALETTE)
    for piece in s.pieces:piece["name"]=piece["name"].replace("Novice","Censure fitted underlayer")
    B=s.B;chest=B["Chest_M"];root=B["Root_M"]
    # Three stepped chevrons articulate down the original garment's spine rows.
    for row,(top,bottom,width,depth) in enumerate([(.218,.055,.217,.147),(.078,-.063,.209,.154),(-.046,-.198,.188,.159)]):
        points=[chest+np.array(p) for p in [(-width*.86,top,depth),(0,top-.024,depth+.014),(width*.86,top,depth),
                 (width,bottom+.033,depth+.005),(0,bottom-.030,depth+.025),(-width,bottom+.033,depth+.005)]]
        skins=[mercy_torso_skin(s,p[1]) for p in points]
        censure_plate(s,"Censure overlapping cuirass chevron "+str(row),points,skins,0 if row!=1 else 1,
                      center_skin=mercy_torso_skin(s,float(np.mean(points,axis=0)[1])))
        edge=[offset(points[i],z=.006) for i in [3,4,5]]
        s.tube("Censure gold judgment chevron "+str(row),edge,[.012]*3,[.007]*3,[skins[i] for i in [3,4,5]],4,sides=4)
    # An oxblood heraldic stitch carries a modest order seal, with no giant gilded relief.
    mark=offset(chest,y=.145,z=.202)
    s.ellipsoid("Censure oxblood chest badge",mark,(.049,.069,.010),"Chest_M",6,sides=4)
    emblem(s,offset(mark,z=.017),"Chest_M",.053)
    s.tube("Censure low battle collar",[offset(B["Neck_M"],y=-.043),offset(B["Neck_M"],y=-.007)],
           [.124,.111],[.083,.075],["Neck_M"]*2,7,sides=10)
    s.ellipsoid("Censure small bronze belt clasp",offset(root,y=.086,z=.159),(.028,.025,.009),"Root_M",3,sides=4)
    for side,sign in [("R",1),("L",-1)]:
        censure_shoulder(s,side,sign)
        elbow,wrist=[name+"_"+side for name in ["Elbow","Wrist"]];forearm=B[wrist]-B[elbow]
        skins=[{elbow:.88,wrist:.12},{elbow:.22,wrist:.78}]
        s.tube("Censure dark fitted vambrace "+side,[B[elbow]+forearm*.15,B[elbow]+forearm*.83],
               [.082,.063],[.078,.061],skins,7,sides=6)
        s.tube("Censure slate vambrace ridge "+side,[offset(B[elbow]+forearm*.19,z=.063),offset(B[elbow]+forearm*.80,z=.048)],
               [.059,.045],[.028,.021],skins,1,sides=4)
        hip,knee,ankle=[name+"_"+side for name in ["Hip","Knee","Ankle"]]
        s.tube("Censure articulated dark trousers "+side,[B[hip],B[knee],B[ankle]],
               [.087,.073,.057],[.080,.066,.055],[hip,knee,ankle],7,sides=8)
        # Sloped tassets cover the hip while short pointed red cloth preserves leg separation.
        points=[root+np.array([sign*.110,.058,.171]),root+np.array([sign*.232,.058,.154]),
                root+np.array([sign*.239,-.137,.170]),root+np.array([sign*.127,-.182,.188])]
        lower={"Root_M":.15,hip:.85};skins=["Root_M","Root_M",lower,lower]
        censure_plate(s,"Censure angled hip tasset "+side,points,skins,0)
        s.tube("Censure oxblood tasset binding "+side,[points[2],points[3]],[.011]*2,[.006]*2,[lower]*2,6,sides=4)
        cloth=[root+np.array([sign*.024,.059,.186]),root+np.array([sign*.158,.056,.177]),
               root+np.array([sign*.193,-.475,.207]),root+np.array([sign*.105,-.548,.225]),
               root+np.array([sign*.038,-.486,.215])]
        cloth_skins=["Root_M","Root_M",{hip:.6,knee:.4},{hip:.55,knee:.45},{hip:.6,knee:.4}]
        begin=len(s.data["positions"])
        oathkeeper_panel(s,"Censure divided oxblood battle cloth "+side,cloth,cloth_skins,6,.008)
        for i in range(begin,len(s.data["positions"])):
            if int(s.data["uvs"][i][0]*len(PALETTE))==1:s.data["uvs"][i][0]=(6+.5)/len(PALETTE)
        s.tube("Censure gilt pointed cloth hem "+side,cloth[1:],[.011]*4,[.005]*4,cloth_skins[1:],4,sides=4)
        # Vertical judgment piping is broad enough to survive the ordinary combat camera.
        s.tube("Censure tabard judgment inlay "+side,[root+np.array([sign*.084,-.015,.198]),root+np.array([sign*.107,-.397,.224])],
               [.013,.015],[.005,.005],[hip,{hip:.65,knee:.35}],4,sides=4)
        # Rear hip guards retain the native backpack and leave the back leg silhouette light.
        rear=[root+np.array([sign*.045,.063,-.107]),root+np.array([sign*.184,.062,-.095]),
              root+np.array([sign*.194,-.161,-.121]),root+np.array([sign*.072,-.188,-.135])]
        begin=len(s.data["positions"])
        censure_plate(s,"Censure rear hip guard "+side,[np.array([p[0],p[1],-p[2]]) for p in rear],skins,0)
        for i in range(begin,len(s.data["positions"])):
            s.data["positions"][i][2]*=-1;s.data["normals"][i][2]*=-1
        for tri in s.data["triangles"]:
            if tri[0]>=begin:tri.reverse()
        coords=[(-.072,.048,.087),(0,.082,.111),(.072,.048,.087),(.065,-.050,.092),(0,-.083,.116),(-.065,-.050,.092)]
        censure_plate(s,"Censure angular knee protector "+side,[B[knee]+np.array(p) for p in coords],[knee]*6,0)
        thumb="ThumbFinger1_"+side
        if thumb in B:s.ellipsoid("Censure thumb glove seam "+side,B[thumb],(.021,.019,.020),thumb,7,sides=6)
        for prefix in ["MiddleToe1","MiddleToe2"]:
            toe=prefix+"_"+side
            if toe in B:s.ellipsoid("Censure toe underboot "+toe,offset(B[toe],y=.014),(.060,.020,.045),toe,7,sides=6)


def censure_boots(s):
    """Tall angular greaves and squared segmented toe guards on a continuous dark boot."""
    novice_boots(s)
    mercy_remove_underlayers(s,["Novice turned boot cuff","Novice boot ankle strap","Novice ankle strap stud"])
    for uv in s.data["uvs"]:
        if int(uv[0]*len(PALETTE)) in [0,11]:uv[0]=(7+.5)/len(PALETTE)
    for piece in s.pieces:piece["name"]=piece["name"].replace("Novice","Censure underboot")
    B=s.B
    for side in ["R","L"]:
        knee,ankle,toe,tip=[name+"_"+side for name in ["Knee","Ankle","MiddleToe1","MiddleToe2"]]
        high=offset(B[knee],y=-.049,z=.037);low=offset(B[ankle],y=.066,z=.013)
        skin={knee:.84,ankle:.16}
        s.tube("Censure oxblood greave lining "+side,[high,offset(low,y=.018)],
               [.110,.099],[.098,.099],[skin,ankle],6,sides=8)
        points=[offset(high,x=-.086,z=.110),offset(high,y=.020,z=.130),offset(high,x=.086,z=.110),
                offset(low,x=.074,z=.110),offset(low,y=-.022,z=.126),offset(low,x=-.074,z=.110)]
        censure_plate(s,"Censure tall folded shin armor "+side,points,[skin]*3+[ankle]*3,0)
        s.tube("Censure gold greave judgment edge "+side,[offset(points[i],z=.006) for i in [0,1,2]],
               [.010]*3,[.006]*3,[skin]*3,4,sides=4)
        s.tube("Censure crimson greave channel "+side,[offset(high,y=-.025,z=.139),offset(low,y=.020,z=.139)],
               [.020,.012],[.005,.005],[skin,ankle],6,sides=4)
        for sign in [-1,1]:
            s.ellipsoid("Censure greave bronze fastening "+side+str(sign),offset(high,x=sign*.059,y=-.007,z=.124),(.008,.008,.006),skin,3,sides=6)
        floor=B[toe][1]-.044
        for index,(z0,z1,h0,h1,w0,w1) in enumerate([
                (B[ankle][2]+.029,B[ankle][2]+.114,.199,.164,.089,.103),
                (B[ankle][2]+.109,B[toe][2]+.045,.175,.121,.098,.103),
                (B[toe][2]+.038,B[tip][2]+.042,.128,.085,.104,.071)]):
            skina=ankle if index==0 else ({ankle:.55,toe:.45} if index==1 else toe)
            skinb={ankle:.7,toe:.3} if index==0 else (toe if index==1 else tip)
            rings=[]
            for z,h,w in [(z0,h0,w0),(z1,h1,w1)]:
                rings.append([np.array([B[ankle][0]+x,floor+y,z]) for x,y in [(-w,h*.79),(-w*.6,h),(w*.6,h),(w,h*.79)]])
            begin=len(s.data["positions"])
            for j in range(3):
                pts=[rings[0][j],rings[1][j],rings[1][j+1],rings[0][j+1]]
                s.triangle([pts[0],pts[1],pts[2]],[skina,skinb,skinb],0 if index%2==0 else 1)
                s.triangle([pts[0],pts[2],pts[3]],[skina,skinb,skina],0 if index%2==0 else 1)
            s.pieces.append({"name":"Censure squared articulated foot plate "+side+str(index),"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
            s.tube("Censure slate toe plate lip "+side+str(index),rings[1],[.006]*4,[.004]*4,[skinb]*4,1,sides=4,axis=(0,0,1))


def censure_helmet():
    """Narrow open judgment helm with a gold crowned brow and a swept crimson plume."""
    s=Surface(["Head_M"],np.zeros((1,3)));count=12;rings=[]
    for row,(radius,height) in enumerate([(1.,0),(.98,.326),(.71,.426),(.31,.464)]):
        ring=[]
        for j in range(count):
            angle=2*np.pi*j/count;x,z=np.cos(angle),np.sin(angle)
            base=.107+.159*max(z,0)+.010*max(-z,0)
            ring.append(np.array([x*.220*radius,base if row==0 else height,z*.212*radius-.029]))
        rings.append(ring)
    begin=len(s.data["positions"])
    for row in range(3):
        for j in range(count):
            k=(j+1)%count;color=1 if j in [2,3] else (0 if row>0 else 7)
            s.triangle([rings[row][j],rings[row+1][j],rings[row+1][k]],["Head_M"]*3,color)
            s.triangle([rings[row][j],rings[row+1][k],rings[row][k]],["Head_M"]*3,color)
    apex=np.array([0,.477,-.029])
    for j in range(count):
        k=(j+1)%count
        s.triangle([rings[-1][j],apex,rings[-1][k]],["Head_M"]*3,0)
        a,b=rings[0][j],rings[0][k]
        s.triangle([a,offset(a,y=-.012),offset(b,y=-.012)],["Head_M"]*3,1)
        s.triangle([a,offset(b,y=-.012),b],["Head_M"]*3,1)
    s.pieces.append({"name":"Censure angular dark guardian crown","vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
    brow=[np.array([-.193,.212,.091]),np.array([-.120,.288,.157]),np.array([0,.311,.190]),
          np.array([.120,.288,.157]),np.array([.193,.212,.091])]
    s.tube("Censure crowned gold brow",brow,[.021]*5,[.012]*5,["Head_M"]*5,4,sides=4,axis=(0,1,0))
    s.tube("Censure oxblood nape lining",[np.array([0,.104,-.193]),np.array([0,.180,-.209])],
           [.172,.187],[.025,.025],["Head_M"]*2,6,sides=8)
    for sign in [-1,1]:
        s.ellipsoid("Censure small bronze brow rivet "+str(sign),np.array([sign*.117,.288,.171]),(.010,.010,.005),"Head_M",3,sides=6)
        points=[np.array([sign*.068,.314,.183]),np.array([sign*.137,.294,.161]),
                np.array([sign*.146,.469,.107]),np.array([sign*.095,.408,.153])]
        censure_plate(s,"Censure rising crown tine "+str(sign),points,["Head_M"]*4,4)
    emblem(s,np.array([0,.345,.209]),"Head_M",.060)
    s.tube("Censure gold plume socket",[np.array([0,.454,.116]),np.array([0,.472,-.121])],
           [.038,.034],[.020,.020],["Head_M"]*2,4,sides=6)
    # A swept faceted crest creates vertical rank without widening the native face.
    s.tube("Censure tall swept crimson plume",[np.array([0,.485,.089]),np.array([0,.675,.017]),
               np.array([0,.748,-.137]),np.array([0,.687,-.294]),np.array([0,.559,-.380])],
           [.029,.055,.050,.036,.010],[.041,.082,.084,.062,.016],["Head_M"]*5,6,sides=6)
    return s


def verdict_shoulder(s,side,sign):
    """A compact heraldic shoulder guard above a short navy mantle fold."""
    shoulder="Shoulder_"+side;scap="Scapula_"+side;root=s.B[shoulder]
    for label,color,rows in [
            ("navy mantle fold",0,[(-.063,.073,.131),(.073,.085,.143),(.224,.008,.119)]),
            ("silver heraldic guard",2,[(-.048,.104,.137),(.068,.125,.152),(.176,.075,.127)])]:
        rings=[]
        for x,height,depth in rows:
            rings.append([root+np.array([sign*x,y,z]) for y,z in [(-.015,depth),(height*.67,depth*.86),
                          (height,depth*.36),(height,-depth*.36),(height*.67,-depth*.86),(-.015,-depth)]])
        begin=len(s.data["positions"])
        for row in range(2):
            for j in range(5):
                pts=[rings[row][j],rings[row+1][j],rings[row+1][j+1],rings[row][j+1]]
                inner={scap:.22,shoulder:.78};skins=[inner,shoulder,shoulder,inner] if row==0 else [shoulder]*4
                for ids in [(0,1,2),(0,2,3)]:
                    tri=[pts[i] for i in ids];weights=[skins[i] for i in ids]
                    outward=np.mean(tri,axis=0)-root;outward[0]=0
                    if np.dot(np.cross(tri[1]-tri[0],tri[2]-tri[0]),outward)<0:tri.reverse();weights.reverse()
                    s.triangle(tri,weights,color)
                    s.triangle([offset(p,y=-.007) for p in reversed(tri)],list(reversed(weights)),0)
        s.pieces.append({"name":"Verdict compact "+label+side,"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
        s.tube("Verdict shoulder bound outer edge "+label+side,rings[-1],[.009]*6,[.006]*6,[shoulder]*6,3 if color==2 else 1,sides=4,axis=(1,0,0))
        for j in [0,5]:
            s.tube("Verdict shoulder side binding "+label+side+str(j),[ring[j] for ring in rings],
                   [.007]*3,[.005]*3,[shoulder]*3,3 if color==2 else 1,sides=4,axis=(0,1,0))
    center=offset(root,x=sign*.062,y=.040,z=.146)
    s.ellipsoid("Verdict navy shoulder field "+side,center,(.043,.038,.009),shoulder,0,sides=4)
    s.ellipsoid("Verdict small shoulder order bar "+side,offset(center,z=.010),(.007,.024,.004),shoulder,3,sides=4)


def verdict_cloth(s,label,points,skins):
    begin=len(s.data["positions"])
    oathkeeper_panel(s,label,points,skins,0,.008)
    for i in range(begin,len(s.data["positions"])):
        if int(s.data["uvs"][i][0]*len(PALETTE))==1:s.data["uvs"][i][0]=(0+.5)/len(PALETTE)


def verdict_armor(s,male):
    """A bright structured cuirass, navy heraldry and compact layered tassets."""
    novice_armor(s,male)
    mercy_remove_underlayers(s,["Novice coat skirt"])
    for uv in s.data["uvs"]:
        color=int(uv[0]*len(PALETTE));uv[0]=({6:0,9:0,11:1}.get(color,color)+.5)/len(PALETTE)
    for piece in s.pieces:piece["name"]=piece["name"].replace("Novice","Verdict fitted underlayer")
    B=s.B;chest=B["Chest_M"];root=B["Root_M"]
    coords=[(-.127,.225,.143),(-.217,.161,.143),(-.229,-.033,.155),(-.174,-.150,.165),
            (0,-.208,.184),(.174,-.150,.165),(.229,-.033,.155),(.217,.161,.143),(.127,.225,.143)]
    points=[chest+np.array(p) for p in coords];skins=[mercy_torso_skin(s,p[1]) for p in points]
    censure_plate(s,"Verdict structured silver breastplate",points,skins,2,
                  center_skin=mercy_torso_skin(s,float(np.mean(points,axis=0)[1])))
    s.tube("Verdict burnished breastplate frame",[offset(p,z=.005) for p in points+[points[0]]],
           [.008]*(len(points)+1),[.005]*(len(points)+1),skins+[skins[0]],3,sides=4)
    # A broad navy heraldic chevron differs from Highward's narrow vertical chest panel.
    for side,sign in [("R",1),("L",-1)]:
        banner=[chest+np.array([sign*x,y,z]) for x,y,z in [(.025,.031,.190),(.184,.134,.168),
                 (.196,.068,.178),(.033,-.039,.195)]]
        banner_skins=[mercy_torso_skin(s,p[1]) for p in banner]
        verdict_cloth(s,"Verdict navy chest heraldry "+side,banner,banner_skins)
        s.tube("Verdict gold heraldic chevron binding "+side,[offset(banner[2],z=.005),offset(banner[3],z=.005)],
               [.008]*2,[.004]*2,[banner_skins[2],banner_skins[3]],3,sides=4)
    # A small central seal unites the chevron rather than covering the chest with ornament.
    seal=offset(chest,y=-.005,z=.213)
    s.ellipsoid("Verdict burnished order seal",seal,(.039,.047,.010),"Chest_M",3,sides=6)
    s.ellipsoid("Verdict bright seal inset",offset(seal,z=.011),(.016,.028,.006),"Chest_M",2,sides=4)
    s.tube("Verdict low navy collar",[offset(B["Neck_M"],y=-.044),offset(B["Neck_M"],y=-.005)],
           [.124,.112],[.084,.076],["Neck_M"]*2,0,sides=10)
    s.tube("Verdict restrained gold collar binding",[offset(B["Neck_M"],y=-.013),offset(B["Neck_M"],y=-.004)],
           [.115,.112],[.079,.077],["Neck_M"]*2,3,sides=10)
    s.ellipsoid("Verdict broad belt clasp",offset(root,y=.085,z=.161),(.043,.028,.011),"Root_M",3,sides=4)
    for side,sign in [("R",1),("L",-1)]:
        verdict_shoulder(s,side,sign)
        elbow,wrist=[name+"_"+side for name in ["Elbow","Wrist"]];forearm=B[wrist]-B[elbow]
        fore_skins=[{elbow:.87,wrist:.13},{elbow:.21,wrist:.79}]
        s.tube("Verdict close silver vambrace "+side,[B[elbow]+forearm*.17,B[elbow]+forearm*.82],
               [.081,.063],[.078,.061],fore_skins,2,sides=8)
        s.tube("Verdict navy vambrace inset "+side,[offset(B[elbow]+forearm*.22,z=.075),offset(B[elbow]+forearm*.76,z=.060)],
               [.038,.028],[.013,.010],fore_skins,0,sides=4)
        unit=forearm/np.linalg.norm(forearm);cuff=B[elbow]+forearm*.82
        s.tube("Verdict gold vambrace rim "+side,[cuff-unit*.012,cuff+unit*.004],[.068]*2,[.066]*2,[fore_skins[-1]]*2,3,sides=8)
        hip,knee,ankle=[name+"_"+side for name in ["Hip","Knee","Ankle"]]
        s.tube("Verdict fitted navy trousers "+side,[B[hip],B[knee],B[ankle]],
               [.087,.073,.057],[.080,.066,.055],[hip,knee,ankle],0,sides=8)
        # Long navy panels sit behind two compact articulated silver tassets.
        for face,z,length in [("front",.174,.416),("back",-.119,.325)]:
            cloth=[root+np.array([sign*.030,.071,z]),root+np.array([sign*.162,.071,z*.96]),
                   root+np.array([sign*.194,-length+.026,z*1.10]),root+np.array([sign*.057,-length,z*1.12])]
            lower={hip:.79,knee:.21};cloth_skins=["Root_M","Root_M",lower,lower]
            begin=len(s.data["positions"])
            author=[np.array([p[0],p[1],-p[2]]) for p in cloth] if face=="back" else cloth
            verdict_cloth(s,"Verdict divided navy tabard "+face+side,author,cloth_skins)
            if face=="back":
                for i in range(begin,len(s.data["positions"])):
                    s.data["positions"][i][2]*=-1;s.data["normals"][i][2]*=-1
                for tri in s.data["triangles"]:
                    if tri[0]>=begin:tri.reverse()
            s.tube("Verdict gold tabard hem "+face+side,[cloth[1],cloth[2],cloth[3]],
                   [.008]*3,[.004]*3,["Root_M",lower,lower],3,sides=4)
        for layer,(top,bottom,depth) in enumerate([(.063,-.123,.192),(-.104,-.263,.203)]):
            coords=[(.112,top,depth),(.238,top-.007,depth-.026),(.249,bottom+.022,depth-.014),(.142,bottom,depth+.014)]
            plate=[root+np.array([sign*x,y,z]) for x,y,z in coords]
            upper={"Root_M":.45,hip:.55} if layer==0 else {"Root_M":.13,hip:.87}
            lower={"Root_M":.13,hip:.87} if layer==0 else {hip:.9,knee:.1}
            censure_plate(s,"Verdict layered silver tasset "+side+str(layer),plate,[upper,upper,lower,lower],2 if layer==0 else 1)
            s.tube("Verdict tasset gold lower lip "+side+str(layer),[offset(plate[2],z=.005),offset(plate[3],z=.005)],
                   [.008]*2,[.005]*2,[lower]*2,3,sides=4)
        coords=[(-.072,.055,.089),(0,.089,.113),(.072,.055,.089),(.069,-.041,.094),(0,-.077,.115),(-.069,-.041,.094)]
        censure_plate(s,"Verdict bright knee guard "+side,[B[knee]+np.array(p) for p in coords],[knee]*6,2)
        s.ellipsoid("Verdict navy knee seal "+side,offset(B[knee],y=.007,z=.132),(.019,.028,.006),knee,0,sides=4)
        thumb="ThumbFinger1_"+side
        if thumb in B:s.ellipsoid("Verdict thumb glove seam "+side,B[thumb],(.021,.019,.020),thumb,0,sides=6)
        for prefix in ["MiddleToe1","MiddleToe2"]:
            toe=prefix+"_"+side
            if toe in B:s.ellipsoid("Verdict toe underboot "+toe,offset(B[toe],y=.014),(.060,.020,.045),toe,0,sides=6)


def verdict_boots(s):
    """Bright calf armor with burnished bindings, navy insteps and a gilded toe guard."""
    novice_boots(s)
    mercy_remove_underlayers(s,["Novice turned boot cuff","Novice boot ankle strap","Novice ankle strap stud"])
    for uv in s.data["uvs"]:
        if int(uv[0]*len(PALETTE))==11:uv[0]=(0+.5)/len(PALETTE)
    for piece in s.pieces:piece["name"]=piece["name"].replace("Novice","Verdict underboot")
    B=s.B
    for side in ["R","L"]:
        knee,ankle,toe,tip=[name+"_"+side for name in ["Knee","Ankle","MiddleToe1","MiddleToe2"]]
        top=offset(B[ankle],y=.286,z=.023);middle=offset(B[ankle],y=.174,z=.015);bottom=offset(B[ankle],y=.059,z=.005)
        top_skin={knee:.5,ankle:.5};mid_skin={knee:.18,ankle:.82}
        s.tube("Verdict fitted silver calf armor "+side,[top,middle,bottom],
               [.108,.103,.095],[.103,.101,.098],[top_skin,mid_skin,ankle],2,sides=10)
        for label,center,skin,width,depth in [("upper",top,top_skin,.112,.107),("lower",bottom,ankle,.101,.104)]:
            s.tube("Verdict burnished greave "+label+" binding "+side,[offset(center,y=.006),offset(center,y=-.010)],
                   [width]*2,[depth]*2,[skin]*2,3,sides=10)
        panel=[offset(top,x=-.033,y=-.023,z=.108),offset(top,x=.033,y=-.023,z=.108),
               offset(bottom,x=.027,y=.023,z=.105),offset(bottom,x=-.027,y=.023,z=.105)]
        oathkeeper_panel(s,"Verdict navy greave field "+side,panel,[top_skin]*2+[ankle]*2,0,.006)
        s.tube("Verdict greave center gold stitch "+side,[offset(top,y=-.045,z=.119),offset(bottom,y=.043,z=.115)],
               [.006]*2,[.004]*2,[top_skin,ankle],3,sides=4)
        # One low burnished toe guard is distinct from Censure's stacked steel foot plates.
        floor=B[toe][1]-.044
        sections=[(B[toe][2]+.018,.104,.127,toe),(B[tip][2]+.010,.087,.091,tip),(B[tip][2]+.043,.065,.072,tip)]
        rings=[]
        for z,w,h,bone in sections:
            rings.append([np.array([B[ankle][0]+x,floor+y,z]) for x,y in [(-w,h*.76),(-w*.48,h),(w*.48,h),(w,h*.76)]])
        begin=len(s.data["positions"])
        for row in range(2):
            for j in range(3):
                pts=[rings[row][j],rings[row+1][j],rings[row+1][j+1],rings[row][j+1]]
                skins=[sections[row][3],sections[row+1][3],sections[row+1][3],sections[row][3]]
                s.triangle([pts[0],pts[1],pts[2]],[skins[0],skins[1],skins[2]],3)
                s.triangle([pts[0],pts[2],pts[3]],[skins[0],skins[2],skins[3]],3)
        s.pieces.append({"name":"Verdict low burnished toe guard "+side,"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
        s.tube("Verdict toe bright edge "+side,rings[-1],[.005]*4,[.004]*4,[tip]*4,2,sides=4,axis=(0,0,1))


def verdict_helmet():
    """A complete steel crown with a low three-plaque coronet and an unobstructed face."""
    s=Surface(["Head_M"],np.zeros((1,3)));count=16;rings=[]
    for row,(radius,height) in enumerate([(1.,0),(.99,.329),(.79,.419),(.36,.466)]):
        ring=[]
        for j in range(count):
            angle=2*np.pi*j/count;x,z=np.cos(angle),np.sin(angle)
            base=.107+.157*max(z,0)+.012*max(-z,0)
            ring.append(np.array([x*.226*radius,base if row==0 else height,z*.212*radius-.029]))
        rings.append(ring)
    begin=len(s.data["positions"])
    for row in range(3):
        for j in range(count):
            k=(j+1)%count;color=0 if j in [3,4,11,12] else (2 if row>0 else 1)
            s.triangle([rings[row][j],rings[row+1][j],rings[row+1][k]],["Head_M"]*3,color)
            s.triangle([rings[row][j],rings[row+1][k],rings[row][k]],["Head_M"]*3,color)
    apex=np.array([0,.480,-.029])
    for j in range(count):
        k=(j+1)%count
        s.triangle([rings[-1][j],apex,rings[-1][k]],["Head_M"]*3,2)
        a,b=rings[0][j],rings[0][k]
        s.triangle([a,offset(a,y=-.012),offset(b,y=-.012)],["Head_M"]*3,0)
        s.triangle([a,offset(b,y=-.012),b],["Head_M"]*3,0)
    s.pieces.append({"name":"Verdict complete steel and navy crown","vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
    # A low band follows the forehead; plaques rise along the crown rather than above it.
    band=[np.array([np.cos(j*2*np.pi/count)*.230,.307,np.sin(j*2*np.pi/count)*.216-.029]) for j in range(count)]
    s.tube("Verdict burnished coronet band",band+[band[0]],[.020]*(count+1),[.009]*(count+1),["Head_M"]*(count+1),3,sides=4,axis=(0,1,0))
    for index in [2,4,6]:
        angle=index*np.pi/8;normal=np.array([np.cos(angle),0,np.sin(angle)]);tangent=np.array([-normal[2],0,normal[0]])
        base=np.array([normal[0]*.230,.314,normal[2]*.216-.029]);height=.079 if index==4 else .058;width=.026 if index==4 else .023
        # Tapered closed plaques are broad and blunt, never spikes or projecting horns.
        front=[base-tangent*width,base+tangent*width,base+tangent*width*.70+np.array([0,height,0])-normal*.018,
               base-tangent*width*.70+np.array([0,height,0])-normal*.018]
        rear=[p-normal*.010 for p in front];begin=len(s.data["positions"])
        for face,reverse,color in [(front,False,3),(rear,True,1)]:
            for ids in [(0,1,2),(0,2,3)]:
                tri=[face[i] for i in ids]
                if (np.dot(np.cross(tri[1]-tri[0],tri[2]-tri[0]),normal)<0)!=reverse:tri.reverse()
                s.triangle(tri,["Head_M"]*3,color)
        for j in range(4):
            k=(j+1)%4;pts=[front[j],rear[j],rear[k],front[k]]
            s.triangle(pts[:3],["Head_M"]*3,3);s.triangle([pts[0],pts[2],pts[3]],["Head_M"]*3,3)
        s.pieces.append({"name":"Verdict low crown plaque "+str(index),"vertex_start":begin,"vertex_count":len(s.data["positions"])-begin})
    s.ellipsoid("Verdict bright brow seal",np.array([0,.342,.196]),(.012,.017,.006),"Head_M",2,sides=4)
    s.tube("Verdict navy nape lining",[np.array([0,.103,-.193]),np.array([0,.180,-.209])],
           [.172,.187],[.025,.025],["Head_M"]*2,0,sides=8)
    return s


def decorate(s,part,variant):
    """Shared original silhouette, with readable set-specific trim and relief."""
    if variant in ["novice","highward","mercy","censure","verdict"]:return
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
                s=novice_helmet() if variant=="novice" else (oathkeeper_helmet() if variant=="oathkeeper" else (highward_helmet() if variant=="highward" else (mercy_helmet() if variant=="mercy" else (censure_helmet() if variant=="censure" else verdict_helmet()))))
                if variant=="oathkeeper":
                    for uv in s.data["uvs"]:
                        if int(uv[0]*len(PALETTE))==6:uv[0]=(5+.5)/len(PALETTE)
            else:
                path=args.bindings/(binding_key+".json");binding=json.loads(path.read_text())
                binds=np.array(binding["bindposes"]);s=Surface(binding["bone_names"],np.linalg.inv(binds)[:,:3,3])
                if part=="armor":
                    if variant=="novice":novice_armor(s,sex=="male")
                    elif variant=="oathkeeper":oathkeeper_armor(s,sex=="male")
                    elif variant=="highward":highward_armor(s,sex=="male")
                    elif variant=="mercy":mercy_armor(s,sex=="male")
                    elif variant=="censure":censure_armor(s,sex=="male")
                    elif variant=="verdict":verdict_armor(s,sex=="male")
                    else:armor(s,sex=="male",endgame,variant)
                else:
                    if variant=="novice":novice_boots(s)
                    elif variant=="oathkeeper":oathkeeper_boots(s)
                    elif variant=="highward":highward_boots(s)
                    elif variant=="mercy":mercy_boots(s)
                    elif variant=="censure":censure_boots(s)
                    elif variant=="verdict":verdict_boots(s)
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
