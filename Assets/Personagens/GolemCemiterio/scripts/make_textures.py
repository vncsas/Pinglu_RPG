# Converts the golem's four JPEG textures to the Unity set (2048 px PNG):
#   golem_BaseColor.png, golem_Normal.png, golem_MetallicSmoothness.png (RGB = metallic, A = smoothness = 1 - roughness)
# usage: blender -b -P make_textures.py -- <textures_dir> <out_dir>
import bpy, sys, os, numpy as np
a = sys.argv[sys.argv.index("--")+1:]; src, out = a[0], a[1]; os.makedirs(out, exist_ok=True)
def load(n):
    im = bpy.data.images.load(os.path.join(src, f"Zyklus9_Zeile7_{n}.jpeg")); im.colorspace_settings.name = 'Non-Color'
    p = np.empty(len(im.pixels), np.float32); im.pixels.foreach_get(p); return p.reshape(im.size[1], im.size[0], 4)
def down(x, n=2048):
    f = x.shape[0]//n
    return x if f <= 1 else x.reshape(n, f, n, f, 4).mean((1, 3))
def save(name, x, srgb):
    im = bpy.data.images.new(name, x.shape[1], x.shape[0], alpha=True); im.colorspace_settings.name = 'sRGB' if srgb else 'Non-Color'
    im.pixels.foreach_set(np.ascontiguousarray(x, np.float32).ravel()); im.filepath_raw = os.path.join(out, name); im.file_format = 'PNG'; im.save()
base = down(load("basecolor")); base[..., 3] = 1.0; save("golem_BaseColor.png", base, True)
met = down(load("metallic")); rough = down(load("roughness"))
ms = np.zeros_like(met); ms[..., :3] = met[..., :1]; ms[..., 3] = 1.0-rough[..., 0]; save("golem_MetallicSmoothness.png", ms, False)
nrm = down(load("normal")); nrm[..., 3] = 1.0; save("golem_Normal.png", nrm, False)
print("TEXOK metal %.3f smooth %.3f" % (met[..., 0].mean(), ms[..., 3].mean()))
