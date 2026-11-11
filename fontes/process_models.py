# Poly Haven glTF (1k) -> light FBX props for Unity. One FBX per mesh object, origin at the bottom centre, decimated to a triangle budget.
# usage: blender -b -P process_models.py -- <models_dir> <out_dir>
import bpy, sys, os, glob, json, shutil, math
from mathutils import Vector
src, out = sys.argv[sys.argv.index("--")+1:][:2]; os.makedirs(out+"/tex", exist_ok=True)
BUDGET = {"tree": 7000, "barrel": 3500, "crate": 2500, "chest": 4000, "lantern": 3500, "basket": 3000, "bucket": 2500, "stool": 1500, "fire_pit": 3500, "ladder": 2500, "broom": 2000, "shield": 3000, "axe": 2000, "jug": 2000, "pot": 2000, "table": 3500, "rock": 2200, "boulder": 3500, "stone": 1500, "stump": 4500, "trunk": 5000, "branches": 3000, "root": 3500, "bark": 2500, "moss": 2500}
manifest = {}
def budget(name):
    for k, v in sorted(BUDGET.items(), key=lambda kv: (kv[0] == "tree")):
        if k in name: return v
    return 9000
for d in sorted(glob.glob(src+"/*/")):
    mid = os.path.basename(os.path.normpath(d)); gl = glob.glob(d+"*.gltf")
    if not gl: continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    try: bpy.ops.import_scene.gltf(filepath=gl[0])
    except Exception as e:
        print('SKIP', mid, str(e)[:60]); continue
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    if not meshes: continue
    # textures: copy with stable names
    texmap = {}
    for f in glob.glob(d+"textures/*"):
        b = os.path.basename(f).lower()
        kind = "diff" if "_diff" in b else "nor" if "_nor_gl" in b else "rough" if "_rough" in b else "alpha" if "alpha" in b or "opacity" in b else None
        if kind:
            dst = f"{out}/tex/{mid}_{kind}{os.path.splitext(b)[1]}"; shutil.copy(f, dst); texmap[kind] = os.path.basename(dst)
    for k, o in enumerate(meshes):
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        if o.parent: bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
        tris = sum(len(p.vertices)-2 for p in o.data.polygons)
        target = budget(mid)
        if tris > target:
            md = o.modifiers.new("dec", 'DECIMATE'); md.ratio = target/tris; md.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=md.name)
        vs = [o.matrix_world @ v.co for v in o.data.vertices]
        mn = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs))); mx = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
        c = Vector(((mn.x+mx.x)/2, (mn.y+mx.y)/2, mn.z)); o.data.transform(__import__("mathutils").Matrix.Translation(-c)); o.location = (0, 0, 0)
        name = f"{mid}" if len(meshes) == 1 else f"{mid}_{k+1}"
        o.name = name
        bpy.ops.export_scene.fbx(filepath=f"{out}/{name}.fbx", use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
                                 path_mode='STRIP', mesh_smooth_type='FACE', add_leaf_bones=False)
        t2 = sum(len(p.vertices)-2 for p in o.data.polygons); dim = [round(x, 2) for x in (mx-mn)]
        manifest[name] = {"id": mid, "tris": t2, "size": dim, "tex": texmap}
        print("MODEL", name, tris, "->", t2, dim, list(texmap))
json.dump(manifest, open(out+"/manifest.json", "w"), indent=1)
