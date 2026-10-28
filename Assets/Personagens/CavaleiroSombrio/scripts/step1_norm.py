# Step 1: import the dark knight FBX, bake transforms, straighten the yaw (character faces -Y,
# his left = +X), centre the pelvis on x=y=0, feet on z=0, group the pieces.
# usage: blender -b -P step1_norm.py -- Knight_All.fbx norm.blend
import bpy, sys, math, numpy as np
from mathutils import Matrix, Vector
fbx, out = sys.argv[sys.argv.index("--")+1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
ms = [o for o in bpy.data.objects if o.type == 'MESH']
for o in ms:                                   # bake object transforms into the mesh data
    o.data.transform(o.matrix_world); o.parent = None; o.matrix_world = Matrix.Identity(4)
def centre(o):
    c = np.empty(len(o.data.vertices)*3); o.data.vertices.foreach_get("co", c); return c.reshape(-1, 3).mean(0)
# lateral axis from mirrored shoulder plates (_low1 = character's left, _low2 = right)
lat = []
for k in ("Shoulder_Plate_01", "Shoulder_Plate_02", "Shoulder_Plate_03"):
    a = [o for o in ms if o.name.startswith(k+"_low1")][0]; b = [o for o in ms if o.name.startswith(k+"_low2")][0]
    lat.append(centre(a)-centre(b))
lat = np.mean(lat, 0); yaw = math.atan2(lat[1], lat[0])
print("yaw (deg):", math.degrees(yaw))
R = Matrix.Rotation(-yaw, 4, 'Z')
for o in ms: o.data.transform(R)
allv = np.concatenate([np.array([v.co[:] for v in o.data.vertices]) for o in ms])
pants = [o for o in ms if o.name.startswith("Padded_Pants")][0]; pc = centre(pants)
T = Matrix.Translation((-pc[0], -pc[1], -allv[:, 2].min()))
for o in ms: o.data.transform(T)
for o in ms:
    o.name = o.name.split(":")[0] if o.name.count(":") == 1 and sum(1 for p in ms if p.name.split(":")[0] == o.name.split(":")[0]) == 1 else o.name.replace(":", "_")
allv = np.concatenate([np.array([v.co[:] for v in o.data.vertices]) for o in ms])
print("bbox", allv.min(0).round(3), allv.max(0).round(3))
for o in sorted(ms, key=lambda o: o.name):
    v = np.array([p.co[:] for p in o.data.vertices])
    print(f"{o.name:40s} v={len(v):5d} min={v.min(0).round(3)} max={v.max(0).round(3)}")
bpy.ops.wm.save_as_mainfile(filepath=out)
