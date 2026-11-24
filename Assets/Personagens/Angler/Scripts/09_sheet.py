import sys, glob
from PIL import Image
pat, out, w, h = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]); cols = int(sys.argv[5]) if len(sys.argv) > 5 else 8
fs = sorted(glob.glob(pat)); ims = [Image.open(f).resize((w, h)) for f in fs]; rows = (len(ims) + cols - 1) // cols
W = Image.new('RGB', (w * min(cols, len(ims)), h * rows), 'white')
for i, im in enumerate(ims): W.paste(im, (w * (i % cols), h * (i // cols)))
W.save(out)
