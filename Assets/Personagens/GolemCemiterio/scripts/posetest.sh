B="/c/Program Files/Blender Foundation/Blender 5.2/blender.exe"; D=C:/Users/Thiago/Documentos/rig-golem
rm -f $D/work/pt/*.png; mkdir -p $D/work/pt
POSETEST=$1 POSEOUT=$D/work/pt "$B" -b $D/work/anim_rt.blend -P $D/scripts/golem_anim.py -- $D/scripts $D/work/x.blend 2>&1 | grep -E "Error|Trace|line [0-9]|File"
python -I - "$1" <<'PY'
import sys
from PIL import Image
names=sys.argv[1].split(",")
ims=[[Image.open(f"C:/Users/Thiago/Documentos/rig-golem/work/pt/{n}_{i}.png") for i in range(3)] for n in names]
w,h=ims[0][0].size; sh=Image.new("RGB",(w*3,h*len(names)))
for r,row in enumerate(ims):
    for c,im in enumerate(row): sh.paste(im,(c*w,r*h))
sh=sh.resize((sh.size[0]*3//4,sh.size[1]*3//4)); sh.save("C:/Users/Thiago/Documentos/rig-golem/work/pt/sheet.png"); print(sh.size)
PY
