# Unity textures for the tank: the source uses UDIM tiles (1001..1033, a 3 x 4 grid, 4K each), which Unity
# does not read. Each map is packed into one atlas (tile u,v -> cell u, 3-v from the top), 1024 per tile:
#   tanque_BaseColor, tanque_Normal, tanque_MetallicSmoothness (RGB metallic, A = 1 - roughness),
#   tanque_Emissive. step1_norm.py moved the UVs to match (u/3, v/4).
# Missing tiles: the eyes (1033) have no texture -> dark glossy; emissive tiles not given -> black.
# usage: python make_textures.py <textures_dir> <out_dir> [tile_size]
import sys, os
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]; T = int(sys.argv[3]) if len(sys.argv) > 3 else 1024
os.makedirs(dst, exist_ok=True)
PRE = "MODEL_BAJA_CREATURE_"
def tile(kind, u, v, mode):
    p = os.path.join(src, f"{PRE}{kind}.{1001+u+10*v}.jpeg")
    return Image.open(p).convert(mode).resize((T, T), Image.LANCZOS) if os.path.exists(p) else None
def atlas(mode, fill, get):
    im = Image.new(mode, (3*T, 4*T), fill)
    for v in range(4):
        for u in range(3):
            t = get(u, v)
            if t is not None: im.paste(t, (u*T, (3-v)*T))
    return im
atlas("RGB", (12, 10, 14), lambda u, v: tile("BaseColor", u, v, "RGB")).save(os.path.join(dst, "tanque_BaseColor.png"))
atlas("RGB", (128, 128, 255), lambda u, v: tile("Normal", u, v, "RGB")).save(os.path.join(dst, "tanque_Normal.png"))
atlas("RGB", (0, 0, 0), lambda u, v: tile("Emissive", u, v, "RGB")).save(os.path.join(dst, "tanque_Emissive.png"))
def ms(u, v):
    m, r = tile("Metallic", u, v, "L"), tile("Roughness", u, v, "L")
    if m is None: return Image.merge("RGBA", [Image.new("L", (T, T), 0)]*3+[Image.new("L", (T, T), 230)])   # glossy eye
    return Image.merge("RGBA", (m, m, m, r.point(lambda x: 255-x)))
atlas("RGBA", (0, 0, 0, 230), ms).save(os.path.join(dst, "tanque_MetallicSmoothness.png"))
print("atlas", 3*T, "x", 4*T, "->", dst)
