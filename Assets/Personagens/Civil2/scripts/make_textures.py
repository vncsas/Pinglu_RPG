# Unity (URP Lit, Specular workflow) textures for Civilian 3 from the Unity-5-style maps (diffuse/specular/gloss/normal):
#   civil2_<Parte>_BaseColor.png (RGBA, alpha kept for hair/beard), civil2_<Parte>_Normal.png, civil2_<Parte>_Specular.png (RGB = specular, A = gloss)
# usage: blender -b -P make_textures.py -- <textures_dir> <out_dir>
import bpy, sys, os, numpy as np
a = sys.argv[sys.argv.index("--")+1:]; src, dst = a[0], a[1]; os.makedirs(dst, exist_ok=True)
PARTS = ["Body", "Bottom", "Top", "Shoes", "Hair", "Beard", "Moustache"]
def load(name):
    im = bpy.data.images.load(os.path.join(src, name)); im.colorspace_settings.name = 'Non-Color'
    w, h = im.size; px = np.empty(w*h*4, np.float32); im.pixels.foreach_get(px); bpy.data.images.remove(im)
    return px.reshape(h, w, 4)
def save(px, name, color):
    h, w = px.shape[:2]
    im = bpy.data.images.new(name, w, h, alpha=True); im.colorspace_settings.name = 'sRGB' if color else 'Non-Color'
    im.alpha_mode = 'CHANNEL_PACKED'
    im.pixels.foreach_set(px.ravel()); im.filepath_raw = os.path.join(dst, name); im.file_format = 'PNG'; im.save()
    bpy.data.images.remove(im); print("saved", name, w, h)
for p in PARTS:
    d = load(f"civilian 2_{p}_diffuse.png"); print("ALPHA", p, d.shape, "alpha min", d[..., 3].min(), "mean", round(float(d[..., 3].mean()), 3))
    if p not in ("Hair", "Beard", "Moustache"): d[..., 3] = 1
    save(d, f"civil2_{p}_BaseColor.png", True)
    n = load(f"civilian 2_{p}_normal.png"); n[..., 3] = 1; save(n, f"civil2_{p}_Normal.png", False)
    s = load(f"civilian 2_{p}_specular.png"); g = load(f"civilian 2_{p}_gloss.png")
    if g.shape[:2] != s.shape[:2]: print("SIZE MISMATCH", p, g.shape, s.shape)
    else:
        s[..., 3] = g[..., 0]; save(s, f"civil2_{p}_Specular.png", False)
