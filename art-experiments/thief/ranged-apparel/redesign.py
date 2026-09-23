"""Original open-face Thief apparel, authored from permitted joint landmarks."""
import math
import numpy as np

COLORS=['343F49','644834','8B6B4D','4F6776','365759','9B865A','C3B798','63333B',
        '5F6850','838D92','ABB8B7','282F38','333537','536773','453C35','77816B']
MOTIFS=[
 'Short repaired leather jerkin, one tool pouch, low oxblood neckerchief, wrapped shoes',
 'Overlapping dark jack, single shoulder fold, side-tied scarf, ankle boots',
 'Clean blue split hem, framed guild fastening, folded low cowl, fitted calf boots',
 'Layered blue-gray leather panels, double shoulder folds, stepped cowl, reinforced boots',
 'Petrol leather tool coat, compact pick roll, clasped asymmetric cowl, strapped work boots',
 'Short charcoal angled jack, sparse trim, wine scarf, close black split-cuff boots',
 'Moss field coat with longer rear skirt, rolled travel scarf, laced leather boots']


def install(m):
    p,offset,tube,bead,panel=m.p,m.offset,m.tube,m.bead,m.panel
    def solid(s,label,vertices,faces,skins,colors):
        start=len(s.data['positions']);v=[np.asarray(x,float) for x in vertices]
        sk=[skins]*len(v) if isinstance(skins,(str,dict)) else skins
        cs=colors if isinstance(colors,list) else [colors]*len(faces)
        for f,c in zip(faces,cs):
            for j in range(1,len(f)-1):
                ids=[f[0],f[j],f[j+1]];s.triangle([v[k] for k in ids],[sk[k] for k in ids],c)
        s.pieces.append({'name':label,'vertex_start':start,'vertex_count':len(s.data['positions'])-start})
    def ribbon(s,label,rows,color,depth=.010):
        verts=[];skins=[]
        normal=np.cross(rows[0][1]-rows[0][0],rows[1][0]-rows[0][0]);normal=normal/np.linalg.norm(normal)*depth
        for rear in [False,True]:
            for a,b,sk in rows:
                verts.extend([a-normal if rear else a,b-normal if rear else b]);skins.extend([sk,sk])
        n=2*len(rows);faces=[]
        for j in range(len(rows)-1):
            a,b,c,d=j*2,j*2+1,j*2+2,j*2+3
            faces.extend([(a,b,d,c),(a+n,c+n,d+n,b+n),(a,c,c+n,a+n),(b,b+n,d+n,d)])
        faces.extend([(0,n,n+1,1),(n-2,n-1,2*n-1,2*n-2)])
        solid(s,label,verts,faces,skins,color)
    def seam(s,label,points,color,skin):tube(s,label,points,.005,.004,color,skin,4)
    def coat(s,i,male):
        B=s.B;w=.242 if male else .220;outer=[1,14,3,13,4,12,8][i]
        trim=[2,9,5,9,5,9,5][i];belt=14 if i!=5 else 11
        # The waist narrows before the chest widens. Broad leather masses carry
        # the costume at native camera size; the sleeves remain continuous cloth.
        spine=['Root_M','BackA_M','BackB_M','Chest_M']
        s.tube('Tapered leather jerkin body',[B[x] for x in spine],
               [w*.86,w*.79,w*.94,w],[.125,.121,.141,.151],spine,outer,sides=12)
        s.tube('Leather shoulder yoke',[B['Chest_M'],B['Chest_M']+p(y=.17),B['Neck_M']-p(y=.03)],
               [w,w*.89,.089],[.151,.122,.083],['Chest_M','Chest_M','Neck_M'],outer,sides=12)
        s.tube('Continuous trouser pelvis',[B['Root_M']-p(y=.13),B['Root_M']+p(y=.08)],
               [w*.66,w*.92],[.110,.122],['Root_M']*2,11,sides=10)
        # A curved overlapping front is attached to the authored torso's surface.
        rows=[]
        for bone,y,rx,rz,left,right in [('Root_M',.045,w*.86,.125,-.11,.16),
          ('BackA_M',0,w*.79,.121,-.10,.145),('BackB_M',0,w*.94,.141,-.14,.13),
          ('Chest_M',.13,w*.92,.130,-.145,-.04)]:
            def q(x):return B[bone]+p(x,y,rz*math.sqrt(max(.1,1-(x/rx)**2))+.009)
            rows.append((q(left),q(right),bone))
        ribbon(s,'Broad curved overlapping leather front',rows,outer,.009)
        # Cloth V stays below the neck and reads as an opening in the jerkin.
        q=B['Chest_M']
        panel(s,'Visible slate shirt opening',[q+p(-.068,.232,.111),q+p(.066,.232,.111),q+p(.014,.080,.157)],.009,0,'Chest_M')
        for sign in [-1,1]:
            side='R' if sign==1 else 'L';hip,knee,ankle=[x+'_'+side for x in ['Hip','Knee','Ankle']]
            s.tube('Fitted cloth trousers '+side,[B[hip],B[knee],B[ankle]],
                   [.108,.078,.056],[.096,.071,.052],[hip,knee,ankle],11,sides=8)
            length=[.10,.155,.205,.240,.195,.105,.275][i]
            if i==5 and sign<0:length+=.070
            if i==0: length+=.018 if sign<0 else 0
            hemcolor=outer
            ribbon(s,'Short split leather hem '+side,[
                (B['Root_M']+p(sign*.014,.045,.150),B['Root_M']+p(sign*w*.90,.045,.093),'Root_M'),
                (B['Root_M']+p(sign*.032,-length,.147),B['Root_M']+p(sign*w*1.07,-length+.035,.052),{hip:.25,'Root_M':.75})],hemcolor)
            ribbon(s,'Side and rear leather hem '+side,[
                (B['Root_M']+p(sign*w*.86,.044,.093),B['Root_M']+p(sign*w*.86,.044,-.107),'Root_M'),
                (B['Root_M']+p(sign*w*1.05,-length+.035,.052),B['Root_M']+p(sign*w*.99,-length-(.04 if i==6 else 0),-.113),{hip:.25,'Root_M':.75})],hemcolor)
            ribbon(s,'Split rear leather skirt '+side,[
                (B['Root_M']+p(sign*.011,.043,-.137),B['Root_M']+p(sign*w*.86,.043,-.108),'Root_M'),
                (B['Root_M']+p(sign*.030,-length-(.04 if i==6 else 0),-.149),B['Root_M']+p(sign*w*.99,-length-(.04 if i==6 else 0),-.113),{hip:.25,'Root_M':.75})],hemcolor)
            scap,sh,el,wr=[x+'_'+side for x in ['Scapula','Shoulder','Elbow','Wrist']]
            s.tube('Fitted sleeve '+side,[B[scap],B[sh],B[el],B[wr]+p(x=sign*.015)],
                   [.096,.112,.080,.057],[.098,.109,.077,.057],[scap,sh,el,wr],0,sides=8,axis=(0,0,1))
            start=B[el]*.7+B[wr]*.3
            s.tube('Shaped leather bracer '+side,[start,B[el]*.3+B[wr]*.7,B[wr]+p(x=sign*.009)],
                   [.097,.088,.073],[.095,.086,.071],[{el:.75,wr:.25},{el:.25,wr:.75},wr],belt,sides=8,axis=(0,0,1))
            q=B[el]*.5+B[wr]*.5
            s.tube('Bracer retaining strap '+side,[q-p(x=sign*.018),q+p(x=sign*.018)],
                   [.099]*2,[.097]*2,[{el:.5,wr:.5}]*2,2 if i==0 else 11,sides=8,axis=(0,0,1))
            if side=='L' or i==3:
                # A folded wedge provides asymmetry without a rigid shoulder orb.
                edge=.132 if i in [3,6] else .106
                v=[B[scap]+p(.025*sign,.016,.073),B[sh]+p(sign*.040,.103,.022),
                   B[sh]+p(sign*edge,-.024,.080),B[sh]+p(sign*.073,-.077,.116),B[scap]+p(sign*.025,-.020,.130)]
                rear=[x+p(z=-.180,y=-.012) for x in v]
                solid(s,'Soft layered shoulder mantle '+side,v+rear,
                    [(0,1,2,3,4),(9,8,7,6,5),(0,5,6,1),(1,6,7,2),(2,7,8,3),(3,8,9,4),(4,9,5,0)],
                    [scap,sh,sh,sh,scap]*2,[outer,belt,outer,belt,belt,outer,belt])
            for extra in ['ThumbFinger1','ThumbFinger2','MiddleToe1','MiddleToe2']:
                b=extra+'_'+side
                if b in B:
                    toe='Toe' in b
                    bead(s,'Glove or trouser underlining '+b,B[b]+p(y=.018,z=-.035) if toe else B[b]-p(z=.008),
                         (.007,.007,.009) if toe else (.027,.025,.025),11,b,6)
        s.tube('Wide dark leather belt',[B['Root_M']+p(y=.025),B['Root_M']+p(y=.100)],
               [w*.96]*2,[.163]*2,['Root_M']*2,belt,sides=12)
        q=B['Root_M']+p(-.037,.065,.173)
        panel(s,'Dull functional belt clasp',[q+p(-.030,-.024),q+p(.030,-.024),q+p(.030,.024),q+p(-.030,.024)],.010,trim,'Root_M')
        panel(s,'Buckle dark center',[q+p(-.020,-.014,.011),q+p(.020,-.014,.011),q+p(.020,.014,.011),q+p(-.020,.014,.011)],.005,belt,'Root_M')
        # Exactly one pouch body, with a broad useful flap, in every tier.
        q=B['Root_M']+p(w*.90,-.020,.100)
        size=(.061,.082,.047) if i!=4 else (.078,.070,.041)
        bead(s,'Single attached tool pouch',q,size,belt,'Root_M',6)
        panel(s,'Broad tool pouch flap',[q+p(-size[0],.046,.038),q+p(size[0],.046,.038),q+p(size[0]*.8,-.004,.054),q+p(0,-.024,.058),q+p(-size[0]*.8,-.004,.054)],.007,2 if i in [0,6] else outer,'Root_M')
        bead(s,'Pouch fastening',q+p(0,-.006,.062),(.008,.010,.004),trim,'Root_M',6)
        s.tube('Low cloth neck collar',[B['Neck_M']-p(y=.075),B['Neck_M']-p(y=.031)],
               [.103,.098],[.091,.086],['Neck_M']*2,0,sides=10)
        if 'Head_M' in B:bead(s,'Rear collar lining',B['Head_M']+p(y=-.12,z=-.08),(.052,.029,.022),0,{'Head_M':.4,'Neck_M':.6},6)
        if i==0:
            q=B['BackA_M']+p(-.12,-.04,.108)
            panel(s,'Broad repaired leather patch',[q+p(-.030,-.026),q+p(.032,-.021),q+p(.025,.030),q+p(-.030,.023)],.008,2,'BackA_M')
            for x in [-.020,0,.020]:seam(s,'Patch stitch',[q+p(x,-.024,.003),q+p(x+.004,-.016,.003)],6,'BackA_M')
        if i in [2,3]:
            # Large stepped seams, visible without relying on minute stitching.
            for side,sign in [('L',-1),('R',1)]:
                q=B['BackB_M']+p(sign*.13,0,.130)
                panel(s,'Tailored waist inset '+side,[q+p(-.030,-.085),q+p(.029,-.074),q+p(.018,.090),q+p(-.021,.065)],.007,14,'BackB_M')
        if i==3:
            for j in range(2):
                q=B['Chest_M']+p(.115,.070+j*.070,.120-j*.012)
                panel(s,'Articulated chest leather tab '+str(j),[q+p(-.035,-.022),q+p(.045,-.012),q+p(.040,.022),q+p(-.030,.024)],.012,3,'Chest_M')
        if i==4:
            for j in range(3):
                q=B['Root_M']+p(.145+j*.029,.035,.170)
                tube(s,'Visible tool roll sleeve '+str(j),[q,q+p(y=.078)],.009,.009,14,'Root_M',6)
                tube(s,'Functional pick handle '+str(j),[q+p(y=.06,z=.005),q+p(y=.101,z=.005)],.004,.004,5,'Root_M',4)
        if i==5:
            q=B['Chest_M']
            panel(s,'Angled covert collar',[q+p(-.15,.162,.115),q+p(-.08,.252,.090),q+p(-.035,.150,.149),q+p(-.069,.091,.154)],.012,12,'Chest_M')
        if i==6:
            q=B['BackB_M']+p(0,0,-.149)
            panel(s,'Broad field coat rear reinforcement',[q+p(-.120,-.080),q+p(.120,-.080),q+p(.090,.095),q+p(-.090,.095)],.008,15,'BackB_M')
    def boots(s,i):
        B=s.B;color=12 if i==5 else (14 if i in [1,3] else 1)
        height=[.13,.195,.25,.28,.24,.22,.29][i]
        for side in ['R','L']:
            hip,knee,ankle,toe,tip=[x+'_'+side for x in ['Hip','Knee','Ankle','MiddleToe1','MiddleToe2']]
            blend={knee:.30,ankle:.70}
            s.tube('Close trouser underlayer '+side,[B[hip],B[knee],B[ankle]],
                   [.107,.078,.056],[.095,.071,.053],[hip,knee,ankle],11,sides=8)
            s.tube('Shaped leather boot shaft '+side,[B[ankle]+p(y=height),B[ankle]+p(y=.10),B[ankle]-p(y=.055)],
                   [.100,.092,.085],[.090,.085,.084],[blend,ankle,ankle],color,sides=8)
            if i!=5:
                s.tube('Folded boot cuff '+side,[B[ankle]+p(y=height-.032),B[ankle]+p(y=height+.006)],
                    [.104,.109],[.094,.096],[blend]*2,[2,14,3,13,4,12,8][i],sides=8)
            else:
                q=B[ankle]+p(y=height,z=.088)
                panel(s,'Low angled split boot cuff '+side,[q+p(-.086,-.031),q+p(.079,-.031),q+p(.067,.021),q+p(.006,-.002),q+p(-.079,.014)],.010,12,blend)
            floor=B[toe][1]-.035
            centers=[p(B[ankle][0],floor+.071,B[ankle][2]-.065),p(B[ankle][0],floor+.083,B[ankle][2]+.034),p(B[toe][0],floor+.066,B[toe][2]),p(B[tip][0],floor+.046,B[tip][2]+.009)]
            s.tube('Rounded leather foot '+side,centers,[.089,.106,.101,.068],[.059,.069,.050,.030],[ankle,ankle,toe,tip],color,sides=8)
            sole=[p(v[0],floor+.012,v[2]) for v in [centers[0],centers[2],centers[3]]]
            s.tube('Flexible dark sole '+side,sole,[.094,.107,.071],[.013]*3,[ankle,toe,tip],11,sides=8)
            q=B[ankle]+p(y=.085)
            s.tube('Broad ankle strap '+side,[q-p(y=.018),q+p(y=.018)],[.096]*2,[.092]*2,[ankle]*2,2 if i==0 else 11,sides=8)
            if i in [2,3,4]:bead(s,'Small side boot fastening '+side,q+p(x=.091,z=.020),(.011,.024,.022),5 if i==4 else 9,ankle,6)
            if i in [3,4]:
                q=B[toe]+p(y=.035)
                bead(s,'Shaped reinforced leather toe '+side,q,(.099,.044,.056),14,toe,8)
            if i==6:
                for j in range(3):
                    q=B[ankle]+p(y=.135+j*.043,z=.095)
                    tube(s,'Crossed field boot lace '+side+str(j),[q+p(-.043,-.012),q+p(.043,.012)],.006,.005,2,blend,4)
    def hood(i):
        s=m.rigid();cloth=[7,3,3,13,4,7,8][i]
        # No crown geometry. This existing rigid item mount carries only a low
        # neckerchief. Native hair and face selection remain the game's task.
        vertices=[];n=12
        for inner,y in [(False,-.157),(False,-.101),(True,-.157),(True,-.101)]:
            for j in range(n):
                t=j*math.tau/n;front=max(0,math.sin(t))
                rx=.103 if inner else .119;rz=.082 if inner else .098
                vertices.append(p(rx*math.cos(t),y-(.014*front if y<-.14 else 0),-.025+rz*math.sin(t)))
        faces=[]
        for j in range(n):
            k=(j+1)%n
            faces.extend([(j,k,n+k,n+j),(2*n+j,3*n+j,3*n+k,2*n+k),
                          (n+j,n+k,3*n+k,3*n+j),(j,2*n+j,2*n+k,k)])
        solid(s,'Continuous low folded neckerchief',vertices,faces,'Root',cloth)
        tail=[.070,.095,.085,.095,.080,.110,.125][i]
        x=-.055 if i in [1,4] else .045
        panel(s,'Short tucked cloth end',[p(x-.050,-.153,.078),p(x+.043,-.152,.079),p(x+.012,-.175-tail,.063),p(x-.024,-.177-tail*.7,.068)],.010,cloth)
        bead(s,'Small side cloth knot',p(.096 if x>0 else -.096,-.147,.046),(.023,.021,.020),cloth,sides=6)
        if i>=2:
            # Later tiers add a lowered cowl at the nape, never a skull shell.
            q=p(0,-.080,-.140)
            bead(s,'Lowered cloth cowl at nape',q,([.16,.18,.16,.14,.19][i-2],.051,.074),11 if i==5 else cloth,sides=8)
            tube(s,'Soft rear cowl fold',[q+p(-.12,0,-.028),q+p(0,-.038,-.051),q+p(.12,0,-.028)],.016,.014,cloth,sides=6)
        if i in [2,4]:
            bead(s,'Dull cowl fastening',p(.085 if x>0 else -.085,-.154,.066),(.012,.017,.005),5,sides=6)
        if i==3:
            panel(s,'Stepped second cowl fold',[p(-.145,-.132,.058),p(-.039,-.169,.118),p(-.025,-.208,.084),p(-.136,-.166,.026)],.010,3)
        if i==6:
            tube(s,'Rolled travel scarf fold',[p(-.137,-.101,.010),p(-.11,-.130,.080),p(.015,-.148,.119)],.018,.013,15,sides=6)
        return s
    def charm(i):
        s=m.rigid();ring=m.ring
        if i in [0,2,6]:
            n=8 if i==0 else 10
            outline=[p(.155*math.cos(j*math.tau/n),.155*math.sin(j*math.tau/n),.020 if i!=0 or j<4 else .004) for j in range(n)]
            panel(s,'Faceted token case',outline,.043,2 if i==0 else 5)
            ring(s,'Raised broad token rim',p(z=.026),.139,.139,.008,2 if i==0 else 5,steps=n)
            if i==0:
                panel(s,'Hammered copper folded face',[p(-.100,-.070,.028),p(.070,-.085,.011),p(.085,.059,.034),p(-.063,.102,.036)],.008,2)
                tube(s,'Old copper strike',[p(-.060,-.034,.037),p(.057,.065,.040)],.010,.004,14,sides=4)
            if i==2:
                panel(s,'Clean guild rook seal',[p(-.048,-.081,.040),p(.048,-.081,.040),p(.035,.042,.040),p(.075,.042,.040),p(.075,.097,.040),p(.024,.081,.040),p(0,.105,.040),p(-.024,.081,.040),p(-.075,.097,.040),p(-.075,.042,.040),p(-.035,.042,.040)],.012,3)
            if i==6:
                bead(s,'Dark compass dial',p(z=.033),(.119,.119,.011),8,sides=10)
                panel(s,'Pale compass north pointer',[p(-.026,-.004,.052),p(0,.107,.052),p(.026,-.004,.052)],.007,6)
                panel(s,'Dark compass south pointer',[p(-.026,-.004,.052),p(.026,-.004,.052),p(0,-.092,.052)],.007,14)
                for x,y in [(-.099,0),(.099,0),(0,-.099)]:bead(s,'Dial cardinal stud',p(x,y,.049),(.008,.008,.004),5,sides=4)
        elif i==1:
            ring(s,'Worn brass pick loop',p(y=.115),.067,.064,.016,5,steps=10)
            panel(s,'Forged flattened pick shank',[p(-.015,.062,.012),p(.014,.062,.012),p(.011,-.134,.012),p(.075,-.161,.012),p(.069,-.183,.012),p(-.014,-.151,.012)],.024,5)
            tube(s,'Dark grip sleeve',[p(y=-.032),p(y=.026)],.019,.019,14,sides=8)
        elif i==3:
            tube(s,'Silver rook stepped foot',[p(y=-.143),p(y=-.116),p(y=-.095)],[.105,.109,.079],[.077,.078,.062],9,sides=8)
            tube(s,'Silver rook tapered tower',[p(y=-.094),p(y=.094)],[.078,.057],[.063,.052],10,sides=8)
            tube(s,'Rook collar',[p(y=.082),p(y=.123)],.091,.065,9,sides=8)
            for x in [-.065,0,.065]:
                panel(s,'Squared rook merlon',[p(x-.021,.112,.059),p(x+.021,.112,.059),p(x+.021,.164,.059),p(x-.021,.164,.059)],.111,10)
        elif i==4:
            ring(s,'Master keyring',p(y=.082),.119,.093,.016,5,steps=12)
            for j in range(3):
                x=-.075+j*.078;z=.015+j*.025
                ring(s,'Individual key bow '+str(j),p(x,.025,z),.028,.035,.010,5,steps=8)
                tube(s,'Key stem '+str(j),[p(x,-.006,z),p(x,-.169-j*.019,z)],.011,.009,5,sides=6)
                for k in range(j+1):tube(s,'Distinct key ward '+str(j)+' '+str(k),[p(x,-.105-k*.023,z),p(x+.036,-.105-k*.023,z)],.010,.010,5,sides=4)
        else:
            tube(s,'Blackened candle holder',[p(y=-.106),p(y=-.075),p(y=-.047)],[.081,.114,.093],[.067,.083,.073],12,sides=8)
            tube(s,'Short snuffed candle',[p(y=-.053),p(y=.086)],[.052,.046],[.044,.040],6,sides=8)
            tube(s,'Bent extinguished wick',[p(y=.086),p(.009,.130),p(.025,.139)],.008,.007,11,sides=6)
            tube(s,'Heavy cooled wax run',[p(.041,.075,.020),p(.046,.012,.020),p(.040,-.006,.020)],[.012,.010,.013],[.010,.008,.008],6,sides=6)
            tube(s,'Socket carrying bail',[p(.085,-.075,-.025),p(.107,.070,-.025),p(0,.169,-.025)],.009,.008,9,sides=6)
        if i in [0,2,6]:tube(s,'Attached suspension lug',[p(y=.134,z=-.003),p(y=.193,z=-.003)],.014,.012,5,sides=6)
        if i in [0,2,5,6]:ring(s,'Cord eye',p(y=.206 if i!=5 else .179),.026,.030,.009,5,steps=8)
        return s
    m.coat,m.boots,m.hood,m.charm=coat,boots,hood,charm
