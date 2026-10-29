# Unity (URP Lit) textures for the sorceress from the 4K Tripo maps, at 2048:
#   maga_BaseColor, maga_Normal, maga_MetallicSmoothness (RGB = metallic, A = 1 - roughness)
# usage: blender -b -P make_textures.py -- <textures_dir> <out_dir> [size]
import bpy, sys, os, numpy as np
a = sys.argv[sys.argv.index("--")+1:]; src, dst = a[0], a[1]; size = int(a[2]) if len(a) > 2 else 2048
os.makedirs(dst, exist_ok=True)
def load(name):
    im = bpy.data.images.load(os.path.join(src, name)); im.colorspace_settings.name = 'Non-Color'
    im.scale(size, size); px = np.empty(size*size*4, np.float32); im.pixels.foreach_get(px); bpy.data.images.remove(im)
    return px.reshape(size, size, 4)
def save(px, name):
    im = bpy.data.images.new(name, size, size, alpha=True); im.colorspace_settings.name = 'Non-Color'
    im.pixels.foreach_set(px.ravel()); im.filepath_raw = os.path.join(dst, name); im.file_format = 'PNG'; im.save()
    bpy.data.images.remove(im); print("saved", name)
pre = "Zyklus5_Zeile1_"
c = load(pre+"basecolor.jpeg"); c[..., 3] = 1; save(c, "maga_BaseColor.png")
n = load(pre+"normal.jpeg"); n[..., 3] = 1; save(n, "maga_Normal.png")
m = load(pre+"metallic.jpeg"); r = load(pre+"roughness.jpeg")
ms = np.empty_like(m); ms[..., :3] = m[..., :1]; ms[..., 3] = 1-r[..., 0]; save(ms, "maga_MetallicSmoothness.png")
