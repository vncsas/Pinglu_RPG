# Renders the mesh (x-ray) with the current skeleton guess, for checking joint placement.
# usage: blender -b norm.blend -P preview.py -- out_prefix zone [skeleton_module]
import bpy, sys, math, importlib
args = sys.argv[sys.argv.index("--")+1:]
out, zone = args[0], args[1]
sys.path.insert(0, bpy.path.abspath("//../scripts")); sys.path.insert(0, args[2] if len(args) > 2 else ".")
import skeleton; J = dict(skeleton.J)
import json, os
fj = os.path.join(args[2] if len(args) > 2 else ".", "..", "work", "fingers.json")
if os.path.exists(fj) and "hand" in args[1]:
    for side, fs in json.load(open(fj)).items():
        for fn, pts in fs.items():
            if any(x != x for p in pts for x in p): continue
            for k in range(3): J[f"{side}{fn}{k+1}"] = (pts[k], pts[k+1])
if "hand" in args[1]:
    for o in bpy.data.objects:
        if o.type == 'MESH' and not o.name.startswith(("Gloves", "Glove_Main", "Merged_Sword")): o.hide_render = True
sc = bpy.context.scene
w = bpy.data.worlds.new("w"); sc.world = w; w.color = (0.2, 0.2, 0.22)
from mathutils import Vector
for o in bpy.data.objects:
    if o.type == 'MESH': o.color = (0.8, 0.8, 0.8, 1)
R = float(args[3]) if len(args) > 3 else (0.0025 if 'hand' in args[1] else 0.006)
for n, (h, t) in J.items():           # bones drawn as red sticks, joints as yellow balls
    h, t = Vector(h), Vector(t); d = t-h
    bpy.ops.mesh.primitive_cylinder_add(radius=R, depth=d.length, location=(h+t)/2)
    c = bpy.context.object; c.rotation_mode = 'QUATERNION'; c.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d); c.color = (1, 0.1, 0.1, 1)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=R*1.8, location=h); bpy.context.object.color = (1, 0.9, 0, 1)
sc.render.engine = 'BLENDER_WORKBENCH'; sh = sc.display.shading
sh.light = 'STUDIO'; sh.show_xray = True; sh.xray_alpha = 0.35; sh.color_type = 'OBJECT'
sc.render.resolution_x = 1100; sc.render.resolution_y = 1100
bpy.ops.object.camera_add(); cam = bpy.context.object; sc.camera = cam; cam.data.type = 'ORTHO'
Z = {"full": (0, 0, 0.92, 1.95), "lhand": (0.32, 0.0, 0.88, 0.3), "rhand": (-0.29, 0.03, 0.88, 0.3),
     "feet": (0, 0, 0.18, 0.6), "top": (0, 0, 1.45, 0.8), "legs": (0, 0, 0.5, 1.1)}
cx, cy, zc, sz = Z[zone]
views = {"front": ((cx, cy-5), (math.pi/2, 0, 0)), "side": ((cx+5, cy), (math.pi/2, 0, math.pi/2)),
         "left": ((cx-5, cy), (math.pi/2, 0, -math.pi/2)), "top": None}
for name, v in views.items():
    if v is None:
        cam.location = (cx, cy, 5); cam.rotation_euler = (0, 0, 0)
    else:
        cam.location = (v[0][0], v[0][1], zc); cam.rotation_euler = v[1]
    cam.data.ortho_scale = sz
    sc.render.filepath = f"{out}_{zone}_{name}.png"; bpy.ops.render.render(write_still=True)
