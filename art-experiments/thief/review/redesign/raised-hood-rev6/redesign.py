"""Original layered Thief apparel; dimensions are authored against joint landmarks."""
import math
import numpy as np

def install(m):
    p,offset,tube,bead,panel=m.p,m.offset,m.tube,m.bead,m.panel
    def strip(s,label,rows,color):
        # Closed ribbon with explicit per-row skinning follows the torso in motion.
        start=len(s.data['positions']); depth=np.cross(rows[0][1]-rows[0][0],rows[1][0]-rows[0][0]);depth=depth/np.linalg.norm(depth)*.009
        for j in range(len(rows)-1):
            a,b,sa=rows[j];c,d,sb=rows[j+1]
            for tri,skins in [([a,b,d],[sa,sa,sb]),([a,d,c],[sa,sb,sb]),
                              ([a-depth,d-depth,b-depth],[sa,sb,sa]),
                              ([a-depth,c-depth,d-depth],[sa,sb,sb])]:s.triangle(tri,skins,color)
            for u,v,su,sv in [(a,c,sa,sb),(d,b,sb,sa)]:
                s.triangle([u,v,v-depth],[su,sv,sv],color)
                s.triangle([u,v-depth,u-depth],[su,sv,su],color)
        for a,b,sk in [rows[0],rows[-1]]:
            s.triangle([a,b,b-depth],[sk]*3,color);s.triangle([a,b-depth,a-depth],[sk]*3,color)
        s.pieces.append({'name':label,'vertex_start':start,'vertex_count':len(s.data['positions'])-start})
    def coat(s,i,male):
        B=s.B; w=.222 if male else .201; cloth=m.shade(i); leather=1; trim=m.trim(i)
        spine=['Root_M','BackA_M','BackB_M','Chest_M']
        s.tube('Tailored cloth undershirt',[B[b] for b in spine],[w*.78,w*.80,w*.93,w],[.102,.106,.12,.129],spine,0,sides=10)
        s.tube('Fitted shoulder shirt',[B['Chest_M'],offset(B['Chest_M'],y=.19),offset(B['Neck_M'],y=-.035)],[w,w*.94,.087],[.129,.105,.078],['Chest_M','Chest_M','Neck_M'],0,sides=10)
        s.tube('Continuous trouser pelvis',[offset(B['Root_M'],y=-.13),offset(B['Root_M'],y=.09)],[w*.62,w*.94],[.107,.117],['Root_M']*2,11,sides=10)
        # Two shaped vest fronts form a deep V and a fitted waist, with visible overlap.
        for sign in [-1,1]:
            strip(s,'Overlapping leather vest '+str(sign),[
              (offset(B['Root_M'],x=sign*.016,y=.04,z=.142),offset(B['Root_M'],x=sign*w*.78,y=.04,z=.116),'Root_M'),
              (offset(B['BackA_M'],x=sign*.012,z=.16),offset(B['BackA_M'],x=sign*w*.82,z=.109),'BackA_M'),
              (offset(B['Chest_M'],x=sign*.05,y=.12,z=.163),offset(B['Chest_M'],x=sign*w*.99,y=.12,z=.091),'Chest_M'),
              (offset(B['Chest_M'],x=sign*.086,y=.245,z=.098),offset(B['Chest_M'],x=sign*w*.75,y=.21,z=.094),'Chest_M')],cloth)
            hip,knee,ankle=[n+('_R' if sign==1 else '_L') for n in ['Hip','Knee','Ankle']]
            s.tube('Fitted cloth trousers '+str(sign),[B[hip],B[knee],B[ankle]],[.105,.074,.054],[.091,.064,.049],[hip,knee,ankle],11,sides=8)
            # Short split coat tails widen at the hip; unequal tips keep the silhouette light.
            length=[.15,.20,.24,.22,.27,.23,.30][i]+(.022 if sign<0 else 0)
            strip(s,'Split layered coat skirt '+str(sign),[
               (offset(B['Root_M'],x=sign*.008,y=.035,z=.160),offset(B['Root_M'],x=sign*w*.95,y=.035,z=.094),'Root_M'),
               (offset(B['Root_M'],x=sign*.037,y=-length,z=.148),offset(B['Root_M'],x=sign*w*1.07,y=-length+.035,z=.061),hip)],cloth)
            strip(s,'Overlapping side coat tail '+str(sign),[
               (offset(B['Root_M'],x=sign*w*.84,y=.033,z=.078),offset(B['Root_M'],x=sign*w*.84,y=.033,z=-.097),'Root_M'),
               (offset(B['Root_M'],x=sign*w*1.045,y=-length+.045,z=.061),offset(B['Root_M'],x=sign*w*.93,y=-length+.015,z=-.103),hip)],cloth)
        # Broad leather belt stays readable at combat distance, with an off-center clasp.
        s.tube('Wide dark leather belt',[offset(B['Root_M'],y=.035),offset(B['Root_M'],y=.105)],[w*.98]*2,[.176]*2,['Root_M']*2,1,sides=10)
        c=offset(B['Root_M'],x=-.045,y=.073,z=.194)
        panel(s,'Square dull metal belt clasp',[c+p(-.036,-.029),c+p(.036,-.029),c+p(.036,.029),c+p(-.036,.029)],.009,trim,'Root_M')
        panel(s,'Inset leather buckle center',[c+p(-.022,-.016,.012),c+p(.022,-.016,.012),c+p(.022,.016,.012),c+p(-.022,.016,.012)],.005,11,'Root_M')
        # A diagonal baldric adds a clear value break and supports the hip pouch.
        rows=[]
        for bone,x,y,z in [('Root_M',.14,.115,.147),('BackA_M',.055,.03,.164),('Chest_M',-.08,.105,.17),('Chest_M',-.15,.23,.13)]:
            q=offset(B[bone],x=x,y=y,z=z);rows.append((q+p(-.024,.018),q+p(.024,-.018),bone))
        strip(s,'Diagonal leather baldric',rows,1)
        for side,sign in [('R',1),('L',-1)]:
            scap,sh,el,wr=[x+'_'+side for x in ['Scapula','Shoulder','Elbow','Wrist']]
            s.tube('Fitted sleeve '+side,[B[scap],B[sh],B[el],B[wr]],[.081,.100,.069,.047],[.089,.098,.068,.05],[scap,sh,el,wr],0,sides=8,axis=(0,0,1))
            # Leather vambraces swell toward elbow and narrow at the wrist.
            start=B[el]*.62+B[wr]*.38
            s.tube('Shaped leather bracer '+side,[start,B[wr]],[.081,.056],[.079,.057],[el,wr],1,sides=8,axis=(0,0,1))
            for t in [.47,.83]:
                q=B[el]*(1-t)+B[wr]*t
                s.tube('Bracer retaining strap '+side+str(t),[q-p(x=sign*.014),q+p(x=sign*.014)],[.078 if t<.5 else .064]*2,[.077 if t<.5 else .065]*2,[el if t<.5 else wr]*2,11,sides=8,axis=(0,0,1))
            if side=='L' or i>=3:
                # Small rounded leather shoulder mantle sits across shoulder rather than a slab.
                q=B[sh]+p(y=.032)
                bead(s,'Soft layered shoulder mantle '+side,q,(.126,.099,.122),cloth,sh,8)
                tube(s,'Shoulder mantle rim '+side,[q+p(-sign*.08,.055,.079),q+p(sign*.10,-.01,.07)],.013,.010,1,sh,6)
            for extra in ['ThumbFinger1','MiddleToe1','MiddleToe2']:
                b=extra+'_'+side
                if b in B:
                    toe='Toe' in b
                    center=offset(B[b],y=.018,z=-.035) if toe else B[b]
                    bead(s,'Glove or trouser underlining '+b,center,(.006,.006,.006) if toe else (.019,.019,.021),11,b,6)
        # Two deliberately different bags, each visibly attached to the belt.
        for sign,size,y in [(1,(.057,.072,.034),-.026),(-1,(.047,.054,.030),-.014)]:
            q=offset(B['Root_M'],x=sign*w*.88,y=y,z=.121)
            bead(s,'Attached leather belt pouch '+str(sign),q,size,1,'Root_M',6)
            panel(s,'Pouch shaped flap '+str(sign),[q+p(-size[0],.032,.029),q+p(size[0],.032,.029),q+p(size[0]*.8,-.003,.039),q+p(0,-.022,.039),q+p(-size[0]*.8,-.003,.039)],.006,2,'Root_M')
            bead(s,'Pouch brass stud '+str(sign),q+p(0,-.008,.047),(.007,.008,.004),trim,'Root_M',6)
        # A short cloth neck wrap fills the collar seam and identifies the tier.
        s.tube('Folded neck scarf',[offset(B['Neck_M'],y=-.075),offset(B['Neck_M'],y=-.025)],[.105,.099],[.092,.086],['Neck_M']*2,7 if i==5 else 3,sides=10)
        if 'Head_M' in B:bead(s,'Rear collar lining',offset(B['Head_M'],y=-.11,z=-.08),(.065,.026,.018),cloth,'Head_M',6)
        if i==0:
            q=offset(B['Chest_M'],x=.135,y=.06,z=.118)
            panel(s,'Small honest cloth repair',[q+p(-.024,-.026),q+p(.029,-.021),q+p(.025,.031),q+p(-.028,.025)],.006,3,'Chest_M')
        if i==4:
            for j in range(3):
                q=offset(B['Root_M'],x=.07+j*.028,y=.12,z=.152)
                tube(s,'Lockpick sleeve '+str(j),[q,q+p(y=.08)],.01,.007,11,'Root_M',4)
                tube(s,'Visible brass pick '+str(j),[q+p(y=.07,z=.008),q+p(y=.11,z=.008)],.004,.004,5,'Root_M',4)
    def boots(s,i):
        B=s.B
        for side in ['R','L']:
            hip,knee,ankle,toe,tip=[x+'_'+side for x in ['Hip','Knee','Ankle','MiddleToe1','MiddleToe2']]
            s.tube('Close trouser underlayer '+side,[B[hip],B[knee],B[ankle]],[.103,.073,.056],[.089,.065,.052],[hip,knee,ankle],11,sides=8)
            height=[.12,.18,.21,.24,.23,.25,.27][i];floor=B[toe][1]-.035
            s.tube('Tapered soft leather boot shaft '+side,[offset(B[ankle],y=height),offset(B[ankle],y=.07),offset(B[ankle],y=-.06)],[.091,.078,.075],[.081,.073,.075],[{knee:.35,ankle:.65},ankle,ankle],1,sides=8)
            s.tube('Folded boot cuff '+side,[offset(B[ankle],y=height-.04),offset(B[ankle],y=height+.015)],[.096,.102],[.085,.089],[{knee:.35,ankle:.65}]*2,m.shade(i),sides=8)
            centers=[p(B[ankle][0],floor+.064,B[ankle][2]-.052),p(B[ankle][0],floor+.073,B[ankle][2]+.04),p(B[toe][0],floor+.061,B[toe][2]),p(B[tip][0],floor+.045,B[tip][2]+.004)]
            s.tube('Supple rounded leather foot '+side,centers,[.078,.091,.087,.055],[.053,.064,.043,.026],[ankle,ankle,toe,tip],1,sides=8)
            sole=[p(v[0],floor+.013,v[2]) for v in [centers[0],centers[2],centers[3]]]
            s.tube('Soft dark sole '+side,sole,[.082,.091,.060],[.013]*3,[ankle,toe,tip],0,sides=8)
            q=offset(B[ankle],y=.056,z=.075)
            tube(s,'Boot diagonal leather strap '+side,[q+p(-.069,-.012),q+p(.070,.022)],.013,.011,11,ankle,4)
            bead(s,'Boot side buckle '+side,q+p(.066,.014,.003),(.015,.021,.009),m.trim(i),ankle,4)
    def hood(i):
        s=m.rigid();cloth=m.shade(i)
        if i==0:
            # Soft sloped cap with short peak and a narrow band rather than a helmet dome.
            s.tube('Slouched cloth cap',[p(-.01,.256,-.10),p(-.024,.327,-.095),p(-.025,.407,-.095),p(-.035,.452,-.10)],[.225,.266,.214,.060],[.213,.259,.218,.066],['Root']*4,3,sides=10)
            tube(s,'Narrow leather cap band',[p(-.211,.284,.09),p(0,.29,.17),p(.211,.284,.09)],.022,.017,11)
            panel(s,'Short curved cap peak',[p(-.15,.293,.135),p(-.12,.279,.216),p(.12,.279,.216),p(.15,.293,.135)],.011,1)
            return s
        # Faceted cloth shell wraps around the skull with a single open face aperture.
        n=12; rings=[]
        angles=[j*math.tau/n for j in range(n)]
        # Recess cheek edges behind the brow so the nose/face reads in profile.
        # Crown narrows early; lower rear cloth follows the native hair clearance and
        # draws forward at the nape instead of extending the whole crown backward.
        rings.append([p(.242*math.cos(a),.218+.334*math.sin(a),-.035+.135*max(0,math.sin(a))+.078*max(0,-math.sin(a))) for a in angles])
        for width,height,cy,z in [(.248,.327,.226,-.15),(.226,.285,.19,-.30),(.202,.230,.15,-.382)]:
            rings.append([p(width*math.cos(a),cy+height*math.sin(a),z+.037*max(0,-math.sin(a))) for a in angles])
        start=len(s.data['positions'])
        for r in range(len(rings)-1):
            for j in range(n):
                k=(j+1)%n;a,b,c,d=rings[r][j],rings[r][k],rings[r+1][k],rings[r+1][j]
                for tri in [[a,b,c],[a,c,d]]:s.triangle(tri,['Root']*3,cloth)
        center=p(0,.11,-.425)
        for j in range(n):s.triangle([rings[-1][j],rings[-1][(j+1)%n],center],['Root']*3,cloth)
        # The rim is dark leather; the aperture remains clear rather than filled by a panel.
        for j in range(n):
            k=(j+1)%n;a,b=rings[0][j],rings[0][k];ai=a+p(z=-.019);bi=b+p(z=-.019)
            s.triangle([a,ai,bi],['Root']*3,11);s.triangle([a,bi,b],['Root']*3,11)
        s.pieces.append({'name':'Curved fitted hood shell','vertex_start':start,'vertex_count':len(s.data['positions'])-start})
        tube(s,'Soft folded hood face edge',rings[0]+[rings[0][0]],.010,.009,1 if i<3 else 11,sides=6)
        # Two cloth fold planes cover the retained rear hair with local clearance.
        # The seam follows the skull then draws in at the neck; it is not a full
        # uniformly enlarged shell. Unequal fold widths avoid a machined helmet ridge.
        spine=[(.455,-.247,.067),(.31,-.403,.095),(.185,-.455,.132),(.015,-.405,.100),(-.072,-.318,.048)]
        for sign in [-1,1]:
            rows=[]
            for y,z,width in spine:
                center=p(-.012,y,z-.012)
                outer=p(-.012+sign*width,y,z)
                rows.append((center,outer,'Root'))
            strip(s,'Draped rear hood cloth fold '+str(sign),rows,cloth)

        # Short neck wrap, not broad shoulder blocks.
        tube(s,'Soft gathered neck cowl',[p(-.18,-.035,.045),p(0,-.108,.11),p(.18,-.035,.045)],.042,.041,7 if i==5 else cloth,sides=8)
        if i>=2:bead(s,'Single dull hood clasp',p(.18,-.014,.105),(.022,.024,.014),m.trim(i),sides=6)
        return s
    m.coat,m.boots,m.hood=coat,boots,hood
