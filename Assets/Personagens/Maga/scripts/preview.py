# X-ray renders of the mesh with a 10 cm grid and the current skeleton guess (red sticks, yellow joints).
# usage: blender -b norm.blend -P preview.py -- <out_prefix> <zone> <scripts_dir>
import bpy, sys, math, importlib
from mathutils import Vector
args = sys.argv[sys.argv.index("--")+1:]; out, zone = args[0], args[1]; sys.path.insert(0, args[2])
import skeleton, json, os; J = dict(skeleton.J)
fj = os.path.join(args[2], "..", "work", "fingers.json")
if os.path.exists(fj):
    F = json.load(open(fj))["Left"]
    for fn in ("Thumb", "Index", "Middle", "Ring", "Little"):
        for k in range(3): J[f"Left{fn}{k}"] = (F[fn][k], F[fn][k+1])
    J["LeftHand"] = (F["wrist"], F["Middle"][0])
sc = bpy.context.scene
w = bpy.data.worlds.new("w"); sc.world = w; w.color = (0.2, 0.2, 0.22)
for o in bpy.data.objects:
    if o.type == 'MESH': o.color = (0.8, 0.8, 0.8, 1)
def stick(h, t, r, col):
    h, t = Vector(h), Vector(t); d = t-h
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=max(d.length, 1e-4), location=(h+t)/2, vertices=8)
    c = bpy.context.object; c.rotation_mode = 'QUATERNION'; c.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d); c.color = col
for i in range(-8, 9):                       # grid: x and y lines every 10 cm (blue), z lines (green)
    stick((i*0.1, 0.6, 0), (i*0.1, 0.6, 1.8), 0.0015, (0.2, 0.4, 1, 1)); stick((-0.6, i*0.1, 0), (-0.6, i*0.1, 1.8), 0.0015, (0.2, 0.4, 1, 1))
for k in range(0, 19):
    stick((-0.8, 0.6, k*0.1), (0.8, 0.6, k*0.1), 0.0015, (0.2, 1, 0.3, 1)); stick((-0.6, -0.8, k*0.1), (-0.6, 0.8, k*0.1), 0.0015, (0.2, 1, 0.3, 1))
R = 0.004 if zone == "full" else 0.0025
for n, (h, t) in J.items():
    stick(h, t, R, (1, 0.1, 0.1, 1))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=R*1.8, location=h, segments=8, ring_count=6); bpy.context.object.color = (1, 0.9, 0, 1)
sc.render.engine = 'BLENDER_WORKBENCH'; sh = sc.display.shading
sh.light = 'STUDIO'; sh.show_xray = True; sh.xray_alpha = 0.35; sh.color_type = 'OBJECT'
sc.render.resolution_x = 1000; sc.render.resolution_y = 1100
bpy.ops.object.camera_add(); cam = bpy.context.object; sc.camera = cam; cam.data.type = 'ORTHO'
Z = {"full": (0, 0, 0.85, 1.9), "top": (0, 0, 1.3, 0.9), "legs": (0, 0, 0.5, 1.1), "rhand": (-0.25, -0.2, 1.05, 0.4), "lhand": (0.53, -0.01, 0.84, 0.22)}
cx, cy, zc, sz = Z[zone]
VIEWS = {"front": ((cx, cy-5), (math.pi/2, 0, 0)), "side": ((cx+5, cy), (math.pi/2, 0, math.pi/2))}
if zone.endswith("hand"): VIEWS["inner"] = ((cx-5, cy), (math.pi/2, 0, -math.pi/2))      # palm side of the left hand
for name, (loc, rot) in VIEWS.items():
    cam.location = (loc[0], loc[1], zc); cam.rotation_euler = rot; cam.data.ortho_scale = sz
    sc.render.filepath = f"{out}_{zone}_{name}.png"; bpy.ops.render.render(write_still=True)
