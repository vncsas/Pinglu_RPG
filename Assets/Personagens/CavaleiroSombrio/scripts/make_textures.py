# Unity (URP Lit) textures from the original 4K PBR maps, at 2048:
#   BaseColor (= D), Normal (= N), Occlusion (= AO), MetallicSmoothness (RGB = M, A = 1 - R)
# usage: blender -b -P make_textures.py -- <orig_textures_dir> <out_dir> [size]
import bpy, sys, os, numpy as np
args = sys.argv[sys.argv.index("--")+1:]; src, dst = args[0], args[1]; size = int(args[2]) if len(args) > 2 else 2048
os.makedirs(dst, exist_ok=True)
def load(name):
    im = bpy.data.images.load(os.path.join(src, name)); im.colorspace_settings.name = 'Non-Color'   # raw values, no conversion
    im.scale(size, size); a = np.empty(size*size*4, np.float32); im.pixels.foreach_get(a); bpy.data.images.remove(im)
    return a.reshape(size, size, 4)
def save(a, name):
    im = bpy.data.images.new(name, size, size, alpha=True); im.colorspace_settings.name = 'Non-Color'
    im.pixels.foreach_set(a.ravel()); im.filepath_raw = os.path.join(dst, name); im.file_format = 'PNG'; im.save()
    bpy.data.images.remove(im); print("saved", name)
for k, key in (("cloth", "tecido"), ("metal", "metal"), ("sword", "espada")):
    pre = f"cavaleiro_sombrio_{key}_"
    d = load(f"T_{k}_D.png"); d[..., 3] = 1; save(d, pre+"BaseColor.png")
    n = load(f"T_{k}_N.png"); n[..., 3] = 1; save(n, pre+"Normal.png")
    ao = load(f"T_{k}_AO.png"); ao[..., 3] = 1; save(ao, pre+"Occlusion.png")
    m = load(f"T_{k}_M.png"); r = load(f"T_{k}_R.png")
    ms = np.empty_like(m); ms[..., :3] = m[..., :1]; ms[..., 3] = 1-r[..., 0]; save(ms, pre+"MetallicSmoothness.png")
