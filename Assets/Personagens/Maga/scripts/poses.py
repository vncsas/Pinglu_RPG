# Test poses for the sorceress rig (FK posing with posekit), rendered front and side.
# usage: blender -b maga_rig.blend -P poses.py -- <out_dir> <pose,pose,...> '[0,90]'
import bpy, sys, os, math, json
from mathutils import Vector, Matrix
args = sys.argv[sys.argv.index("--")+1:]; outdir, which, views = args[0], args[1].split(","), json.loads(args[2])
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from posekit import Kit
arm = bpy.data.objects["Armature"]; K = Kit(arm); P = arm.pose.bones; B = arm.data.bones
for k in list(P["CTRL_Root"].keys()):
    if k.startswith("ik_"): P["CTRL_Root"][k] = 0.0
ANK = {S: B[S+"Foot"].head_local.copy() for S in ("Left", "Right")}
def pose_tpose(): K.reset(apose=False)
def pose_original(): K.reset(apose=True)
def pose_conjurar():                       # staff raised, left hand pushed forward
    K.reset(apose=True); K.hips(drop=0.05)
    K.foot('L', ANK["Left"]+Vector((0.02, -0.15, 0)), yaw=8); K.foot('R', ANK["Right"]+Vector((-0.02, 0.15, 0)), yaw=-15)
    K.rot_world("Spine", 'X', 6); K.rot_world("Chest", 'Z', 10)
    K.aim("RightUpperArm", (-0.45, -0.55, 0.7)); K.aim("RightLowerArm", (-0.15, -0.3, 0.95))
    K.aim("LeftUpperArm", (0.35, -0.9, 0.15)); K.aim("LeftLowerArm", (0.1, -1, 0.2)); K.aim("LeftHand", (0.0, -1, 0.5))
def pose_caminhada():
    K.reset(apose=True); K.hips(drop=0.04)
    K.foot('L', ANK["Left"]+Vector((0, -0.25, 0)), pitch=-10); K.foot('R', ANK["Right"]+Vector((0, 0.30, 0.02)), pitch=30)
    K.aim("LeftUpperArm", (0.25, 0.35, -0.9)); K.aim("LeftLowerArm", (0.15, 0.05, -1))
def pose_agachada():
    K.reset(apose=True); K.hips(drop=0.30)
    K.foot('L', ANK["Left"]+Vector((0.04, -0.25, 0)), yaw=10); K.foot('R', ANK["Right"]+Vector((-0.04, 0.2, 0)), pitch=35, yaw=-10)
    K.rot_world("Spine", 'X', 15)
def pose_bracos_acima():
    K.reset(apose=True)
    K.aim("LeftUpperArm", (0.4, 0, 0.9)); K.aim("LeftLowerArm", (0.15, 0, 1)); K.aim("RightUpperArm", (-0.4, 0, 0.9)); K.aim("RightLowerArm", (-0.15, -0.1, 1))
def pose_punho():                        # T-pose, left hand closed
    K.reset(apose=False); K.fist("Left", 1.0)
def pose_dedos_meio():
    K.reset(apose=False); K.fist("Left", 0.5)
def pose_dedos_abertos():                 # fingers straightened / spread a little
    K.reset(apose=False)
    for k, ph in enumerate(("Proximal", "Intermediate", "Distal")):
        for fn, spread in (("Index", -8), ("Middle", 0), ("Ring", 8), ("Little", 16)):
            K.rot_local(f"Left{fn}{ph}", 'X', -(15, 10, 5)[k])
            if k == 0: K.rot_local(f"Left{fn}{ph}", 'Z', spread)
POSES = dict(punho=pose_punho, dedos_meio=pose_dedos_meio, dedos_abertos=pose_dedos_abertos, tpose=pose_tpose, original=pose_original, conjurar=pose_conjurar, caminhada=pose_caminhada, agachada=pose_agachada, bracos_acima=pose_bracos_acima)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = 700; sc.render.resolution_y = 900
sc.view_settings.view_transform = 'AgX'; sc.view_settings.exposure = -0.2
w = bpy.data.worlds.new("w"); sc.world = w; w.color = (0.35, 0.37, 0.42)
for name, loc, e in (("key", (-2.5, -3, 3), 600), ("fill", (3, -2.5, 1.5), 250), ("rim", (1, 3.5, 3), 600)):
    l = bpy.data.lights.new(name, 'AREA'); l.energy = e; l.size = 3; o = bpy.data.objects.new(name, l); sc.collection.objects.link(o)
    o.location = loc; o.rotation_euler = Vector((0, 0, 1)).rotation_difference(Vector(loc)-Vector((0, 0, 1))).to_euler()
bpy.ops.mesh.primitive_plane_add(size=10); fl = bpy.context.object; m = bpy.data.materials.new("floor"); m.diffuse_color = (0.08, 0.08, 0.09, 1); fl.data.materials.append(m)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.lens = 50
os.makedirs(outdir, exist_ok=True)
for p in which:
    POSES[p]()
    for i, ang in enumerate(views):
        dist = float(os.environ.get("DIST", "4.6")); tz = float(os.environ.get("CAMZ", "0.9")); cx = float(os.environ.get("CX", "0"))
        a = math.radians(ang); cam.location = (cx+dist*math.sin(a), -dist*math.cos(a), tz+0.2)
        cam.rotation_euler = (Vector((cx, 0, tz))-cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = f"{outdir}/{p}_{i}.png"; bpy.ops.render.render(write_still=True)
