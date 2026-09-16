"""Original diagnostic checker and wave stripes; no game texture input."""
import argparse
from pathlib import Path
from PIL import Image
p=argparse.ArgumentParser();p.add_argument('--output-dir',type=Path,required=True);a=p.parse_args();a.output_dir.mkdir(parents=True,exist_ok=True)
for slot in range(2):
    im=Image.new('RGB',(128,128))
    for y in range(128):
        for x in range(128):
            if slot==0:color=(185,145,82)if (x//16+y//16)%2 else (244,221,162)
            else:color=(46,120,142)if (y+(x//16%2)*8)%32<20 else (185,235,228)
            im.putpixel((x,y),color)
    im.save(a.output_dir/f'original{slot}.png')
