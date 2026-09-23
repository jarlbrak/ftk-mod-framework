#!/usr/bin/env python3
"""Separate original compact Nightblade cap and lowered cowl prototype."""
import importlib.util,json,sys
from pathlib import Path
import numpy as np
OUT=Path(__file__).resolve().parent
BASE=OUT.parent/'ranged-apparel'
sys.path.insert(0,str(BASE))
spec=importlib.util.spec_from_file_location('thief_apparel_base',BASE/'build.py')
m=importlib.util.module_from_spec(spec);sys.modules[spec.name]=m;spec.loader.exec_module(m)
s=m.rigid();p=m.p
# Cloth crown keeps native side/back hair intentionally visible; no visor plane.
s.tube('Soft asymmetric cloth crown',[p(-.01,.252,-.105),p(-.022,.322,-.10),p(-.03,.408,-.105),p(-.045,.452,-.11)],[.226,.263,.213,.055],[.212,.258,.217,.061],['Root']*4,0,sides=10)
points=[p(.225*np.cos(t),.269,-.105+.215*np.sin(t)) for t in np.linspace(0,2*np.pi,13)]
m.tube(s,'Cloth crown lower folded band',points,.016,.012,7,sides=6)
# A gathered hood worn down reads as cloth at the neck, leaving the face and ears clear.
m.tube(s,'Folded neck wrap',[p(-.18,-.035,.02),p(-.105,-.115,.085),p(.03,-.146,.105),p(.18,-.055,.02)],[.040,.038,.037,.041],[.040,.037,.035,.043],7,sides=8)
m.panel(s,'Short overlapping cloth wrap end',[p(.01,-.132,.143),p(.18,-.076,.061),p(.13,-.193,.041),p(.07,-.224,.059)],.009,7)
m.bead(s,'Gathered lowered hood at nape',p(0,-.06,-.26),(.175,.081,.11),0,sides=8)
m.orient_outward(s)
data={k:v for k,v in s.data.items() if k not in ['joints','weights','bone_names']}
m.save(OUT/'nightblade-cap-cowl-display.source.json',data)
m.write_static_glb(OUT/'nightblade-cap-cowl-display.glb',data)
data=dict(data);data['positions']=[(np.array(v)+p(y=-.55)).tolist() for v in data['positions']]
m.save(OUT/'nightblade-cap-cowl.source.json',data);m.save(OUT/'nightblade-cap-cowl.pieces.json',s.pieces)
m.write_static_glb(OUT/'nightblade-cap-cowl.glb',data)
print('Separate prototype only; production revision6 untouched')
