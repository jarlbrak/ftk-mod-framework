#!/usr/bin/env python3
"""Compose an offline review board from the actual source-mesh renders."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
OUT=Path(__file__).resolve().parent

def font(size):
    for path in ['/System/Library/Fonts/Supplemental/Avenir Next.ttc','/System/Library/Fonts/Supplemental/Arial.ttf']:
        if Path(path).exists():return ImageFont.truetype(path,size)
    return ImageFont.load_default()
def main():
    manifest=json.loads((OUT/'manifest.json').read_text())
    board=Image.new('RGB',(1800,950),'#111C29');d=ImageDraw.Draw(board)
    d.text((40,26),'PALADIN  /  OATHS & KEEPSAKES',font=font(31),fill='#EDDFC2')
    d.text((42,74),'Twelve original display objects  •  Actual exported source geometry  •  Offline art review',font=font(18),fill='#A8B4C1')
    accents=['#A8B7BC','#B69A68','#86B9CD','#D8D6C0','#B75D5F','#D7AF56']
    for i,r in enumerate(manifest['assets']):
        col=i%6;row=i//6;x=30+col*294;y=130+row*365
        d.rounded_rectangle((x,y,x+275,y+343),radius=12,fill='#1C2A39',outline='#344252',width=1)
        d.line((x+20,y+18,x+255,y+18),fill=accents[col],width=3)
        im=Image.open(OUT/(r['key']+'-icon.png')).convert('RGBA');board.paste(im,(x+10,y+26),im)
        d.text((x+16,y+282),r['name'],font=font(18),fill='#EAE3D4')
        d.text((x+16,y+312),r['family'].upper()+'  /  '+r['slot'].upper(),font=font(12),fill=accents[col])
    d.text((42,890),'Display-only accessories. No avatar attachment. Native display fit and gameplay remain untested.',font=font(16),fill='#A8B4C1')
    board.save(OUT/'actual-mesh-contact-sheet.png')
    ui=Image.new('RGB',(840,186),'#202C39');d=ImageDraw.Draw(ui)
    d.text((16,10),'Actual-size 64 px icon readability',font=font(17),fill='#EAE3D4')
    for i,r in enumerate(manifest['assets']):
        icon=Image.open(OUT/(r['key']+'-icon.png')).convert('RGBA').resize((64,64),Image.Resampling.LANCZOS)
        ui.paste(icon,(16+i*68,64),icon)
    ui.save(OUT/'icon-review-64px.png')
if __name__=='__main__':main()
