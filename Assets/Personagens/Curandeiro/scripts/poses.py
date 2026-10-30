# Test poses for the tank rig (FK with posekit), rendered front and side.
# usage: blender -b tanque_rig.blend -P poses.py -- <out_dir> <pose,...> '[0,90]'   (env DIST, CAMZ, CX)
import bpy, sys, os, math, json
from mathutils import Vector
args = sys.argv[sys.argv.index("--")+1:]; outdir, which, views = args[0], args[1].split(","), json.loads(args[2])
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from posekit import Kit
arm = bpy.data.objects["Armature"]; K = Kit(arm); P = arm.pose.bones
for k in list(P["CTRL_Root"].keys()):
    if k.startswith("ik_"): P["CTRL_Root"][k] = 0.0
def wings(open_, fold=0.0):
    """open_: spread the wings out to the sides (0 = as modelled); fold: tuck them against the back"""
    for S, sx in (("L", 1), ("R", -1)):
        K.rot_world(f"Wing_{S}_1", 'Y', -sx*(25*open_-15*fold)); K.rot_world(f"Wing_{S}_1", 'Z', sx*(20*open_-25*fold))
        K.rot_local(f"Wing_{S}_3", 'X', -20*open_+20*fold); K.rot_local(f"Wing_{S}_4", 'X', -10*open_+15*fold)
def pose_tpose(): K.reset(apose=False)
def pose_original(): K.reset(apose=True)
def pose_bracos_acima():
    K.reset(apose=True)
    K.aim("LeftUpperArm", (0.35, -0.1, 0.93)); K.aim("LeftLowerArm", (0.1, -0.2, 1)); K.aim("RightUpperArm", (-0.35, -0.1, 0.93)); K.aim("RightLowerArm", (-0.1, -0.2, 1))
def pose_curar():                      # both palms forward, slight lean, fingers open
    K.reset(apose=True); K.hips(drop=0.03)
    K.rot_world("Spine", 'X', 6); K.rot_world("Head", 'X', 8)
    for S, sx in (("Left", 1), ("Right", -1)):
        K.aim(S+"UpperArm", (sx*0.35, -0.8, -0.2)); K.aim(S+"LowerArm", (sx*0.1, -1, 0.05)); K.aim(S+"Hand", (sx*0.05, -1, 0.4))
        K.fist(S, -0.3)
    wings(0.6)
def pose_asas_abertas():
    K.reset(apose=True); wings(1.0)
def pose_asas_fechadas():
    K.reset(apose=True); wings(0.0, 1.0)
def pose_passo():
    K.reset(apose=True)
    K.rot_world("LeftUpperLeg", 'X', -25); K.rot_world("LeftLowerLeg", 'X', 20)
    K.rot_world("RightUpperLeg", 'X', 18); K.rot_world("RightLowerLeg", 'X', 10)
    K.aim("LeftUpperArm", (0.25, 0.3, -0.92)); K.aim("RightUpperArm", (-0.25, -0.3, -0.92))
POSES = dict(tpose=pose_tpose, original=pose_original, bracos_acima=pose_bracos_acima, curar=pose_curar, asas_abertas=pose_asas_abertas, asas_fechadas=pose_asas_fechadas, passo=pose_passo)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = 700; sc.render.resolution_y = 900
sc.view_settings.view_transform = 'AgX'; sc.view_settings.exposure = 0.0
w = bpy.data.worlds.new("w"); sc.world = w; w.color = (0.35, 0.37, 0.42)
for name, loc, e in (("key", (-2.5, -3, 3), 700), ("fill", (3, -2.5, 1.5), 300), ("rim", (1, 3.5, 3), 700)):
    l = bpy.data.lights.new(name, 'AREA'); l.energy = e; l.size = 3; o = bpy.data.objects.new(name, l); sc.collection.objects.link(o)
    o.location = loc; o.rotation_euler = Vector((0, 0, 1)).rotation_difference(Vector(loc)-Vector((0, 0, 1))).to_euler()
bpy.ops.mesh.primitive_plane_add(size=10); fl = bpy.context.object; m = bpy.data.materials.new("floor"); m.diffuse_color = (0.08, 0.08, 0.09, 1); fl.data.materials.append(m)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.lens = 50
os.makedirs(outdir, exist_ok=True)
for p in which:
    POSES[p]()
    for i, ang in enumerate(views):
        dist = float(os.environ.get("DIST", "4.8")); tz = float(os.environ.get("CAMZ", "0.95")); cx = float(os.environ.get("CX", "0"))
        a = math.radians(ang); cam.location = (cx+dist*math.sin(a), -dist*math.cos(a), tz+0.2)
        cam.rotation_euler = (Vector((cx, 0, tz))-cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = f"{outdir}/{p}_{i}.png"; bpy.ops.render.render(write_still=True)
