# Glue the _tmp_<view>_<frame>.png renders written by render_anim.py into one image.
# usage: python sheet.py <dir> <out.png> <frames>
import sys, os, glob
from PIL import Image
d, out, frames = sys.argv[1], sys.argv[2], [int(f) for f in sys.argv[3].split(",")]
rows = [[Image.open(os.path.join(d, f"_tmp_{v}_{f}.png")).convert("RGB") for f in frames] for v in ("lado", "frente")]
W, H = rows[0][0].size; s = Image.new("RGB", (W*len(frames), H*2))
for r, row in enumerate(rows):
    for c, im in enumerate(row): s.paste(im, (c*W, r*H))
s.save(out)
for p in glob.glob(os.path.join(d, "_tmp_*.png")): os.remove(p)
