# Renders some frames of an animation (side + front) into one strip: <out.png>
# usage: blender -b file.blend -P render_anim.py -- out.png 1,7,13,19 [action]
import bpy, sys, math, os
from mathutils import Vector
args = sys.argv[sys.argv.index("--")+1:]; out = args[0]; frames = [int(f) for f in args[1].split(",")]
if len(args) > 2:                                     # optional: action name to show on the Armature
    arm = [o for o in bpy.data.objects if o.type == "ARMATURE"][0]; arm.animation_data.action = bpy.data.actions[args[2]]
    if arm.animation_data.action_slot is None and arm.animation_data.action.slots:
        arm.animation_data.action_slot = arm.animation_data.action.slots[0]
sc = bpy.context.scene; sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = 500; sc.render.resolution_y = 700
sc.view_settings.view_transform = 'AgX'; sc.view_settings.exposure = -0.3
w = bpy.data.worlds.new("w"); sc.world = w; w.color = (0.35, 0.37, 0.42)
for name, loc, e in (("key", (-2.5, -3, 3), 600), ("rim", (1, 3.5, 3), 600), ("fill", (3, -2.5, 1.5), 200)):
    l = bpy.data.lights.new(name, 'AREA'); l.energy = e; l.size = 3; o = bpy.data.objects.new(name, l); sc.collection.objects.link(o)
    o.location = loc; o.rotation_euler = Vector((0, 0, 1)).rotation_difference(Vector(loc)-Vector((0, 0, 1))).to_euler()
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
tmp = []
for v, ang in (("lado", 90), ("frente", 20)):
    d, h, tz = (1.3, 1.1, 0.95) if os.environ.get("CLOSE") else (4.4, 1.2, 0.95)      # CLOSE=1: right hand close-up
    if os.environ.get("LEGS"): d, h, tz = 2.2, 0.75, 0.45                                # LEGS=1: legs close-up
    a = math.radians(ang); cam.location = (d*math.sin(a)-(0.3 if os.environ.get("CLOSE") else 0), -d*math.cos(a), h)
    cam.rotation_euler = (Vector((-0.3 if os.environ.get("CLOSE") else 0, 0, tz))-cam.location).to_track_quat('-Z', 'Y').to_euler()
    for f in frames:
        sc.frame_set(f); p = os.path.join(os.path.dirname(out), f"_tmp_{v}_{f}.png"); sc.render.filepath = p
        bpy.ops.render.render(write_still=True); tmp.append(p)
print("FRAMES", ";".join(tmp))

