"""Render the existing crown brand into Steam layouts. Requires Pillow, build-time only."""
from pathlib import Path
import math
from PIL import Image, ImageDraw, ImageFont
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'assets/steam'; OUT.mkdir(parents=True,exist_ok=True)
GOLD='#e7b53c'; WHITE='#f4f6fb'; MUTED='#9fb0c7'
font_path='/System/Library/Fonts/Avenir Next.ttc'
def font(n): return ImageFont.truetype(font_path,n)
def bg(w,h):
 im=Image.new('RGB',(w,h));p=im.load()
 for y in range(h):
  for x in range(w):
   glow=max(0,1-math.hypot((x/w-.68)*1.3,(y/h-.32)))*12
   p[x,y]=(int(13+glow*.5),int(21+glow),int(30+glow*1.2))
 d=ImageDraw.Draw(im)
 for i in range(15):
  pts=[(int(x*w/80),int(h*(.38+i*.045)+math.sin(x/10+i*.19)*h*.085)) for x in range(81)]
  d.line(pts,fill=(28+i//3,43+i//2,53+i//2),width=max(1,w//1200))
 d.rectangle((w*.035,h*.035,w*.965,h*.965),outline='#38434b',width=max(1,w//1500))
 return im
# Crown geometry is the repo's assets/brand/ftk-mark.svg, scaled without new brand artwork.
def crown(im,cx,y,w):
 d=ImageDraw.Draw(im);s=w/160;x=cx-w/2
 def pt(a,b):return (x+a*s,y+b*s)
 d.polygon([pt(*p) for p in [(0,104),(14,44),(46,76),(80,26),(114,76),(146,44),(160,104)]],fill=GOLD)
 d.rounded_rectangle((*pt(0,106),*pt(160,128)),radius=6*s,fill=GOLD)
 for a,b,r in [(80,60,9),(36,78,6),(124,78,6)]:d.ellipse((*pt(a-r,b-r),*pt(a+r,b+r)),fill='#1b2330')
def label(im,s,y,size,color=WHITE):
 d=ImageDraw.Draw(im);d.text((im.width/2,y),s,font=font(size),fill=color,anchor='mt')
def save(im,n): im.save(OUT/n)
im=bg(600,900);crown(im,300,120,265);label(im,'FOR THE KING',420,48);label(im,'MODDED',493,70,GOLD);label(im,'FTK MOD FRAMEWORK',760,22,MUTED);save(im,'capsule.png')
im=bg(920,430);crown(im,155,91,185);d=ImageDraw.Draw(im);d.text((300,115),'FOR THE KING',font=font(52),fill=WHITE);d.text((300,175),'MODDED',font=font(65),fill=GOLD);d.text((303,280),'FTK MOD FRAMEWORK',font=font(20),fill=MUTED);save(im,'header.png')
im=bg(3840,1240);crown(im,2820,190,820);save(im,'hero.png')
im=Image.new('RGBA',(1280,480),(0,0,0,0));crown(im,145,72,200);d=ImageDraw.Draw(im);d.text((300,90),'FOR THE KING',font=font(83),fill=WHITE);d.text((300,190),'MODDED',font=font(106),fill=GOLD);save(im,'logo.png')
im=bg(1024,1024);crown(im,512,160,640);label(im,'MODDED',820,88);save(im,'icon.png');im.save(OUT/'icon.ico',sizes=[(16,16),(32,32),(48,48),(64,64),(128,128),(256,256)]);im.save(OUT/'icon.icns')
