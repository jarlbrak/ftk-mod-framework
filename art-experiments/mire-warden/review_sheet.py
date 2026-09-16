"""Arrange the actual Blender renders for visual review, using Pillow."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageOps

root=Path(__file__).resolve().parent
sheet=Image.new('RGB',(1600,1060),'#eeeae1')
draw=ImageDraw.Draw(sheet)
font_path='/System/Library/Fonts/Helvetica.ttc'
title=ImageFont.truetype(font_path,36)
label=ImageFont.truetype(font_path,20)
small=ImageFont.truetype(font_path,17)
draw.text((32,24),'MIREWARDEN',font=title,fill='#28392f')
draw.text((34,73),'Blender geometry study  /  2,482 triangles  /  original procedural model',font=label,fill='#586357')
sheet.paste(Image.open(root/'hero.png').resize((850,850)),(30,125))
for name,x in [('front',910),('back',1245)]:
    sheet.paste(Image.open(root/(name+'.png')).resize((325,325)),(x,125))
    draw.text((x,464),name.upper(),font=label,fill='#28392f')
draw.text((915,530),'COMBAT-SIZE READABILITY',font=label,fill='#28392f')
cut=Image.open(root/'cutout.png').convert('RGBA')
cut=cut.crop(cut.getchannel('A').getbbox())
cut.thumbnail((180,128),Image.Resampling.LANCZOS)
gray=ImageOps.grayscale(cut).convert('RGBA')
gray.putalpha(cut.getchannel('A'))
sil=Image.new('RGBA',cut.size,'#26352c')
sil.putalpha(cut.getchannel('A'))
for img,x,caption in [(cut,920,'128 px tall'),(gray,1135,'Value structure'),(sil,1350,'Silhouette')]:
    sheet.paste(img,(x,585),img)
    draw.text((x,737),caption,font=small,fill='#586357')
draw.text((915,818),'Editable carved slabs and palette materials.',font=label,fill='#28392f')
draw.text((915,852),'Unrigged. Game export and animation remain.',font=label,fill='#586357')
draw.text((32,1004),'ART DIRECTION TEST 01  |  Studio renders, not an in-game screenshot.',font=label,fill='#586357')
sheet.save(root/'review.png')
