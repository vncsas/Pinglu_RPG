import bpy, sys, os
from mathutils import Vector as V
out = sys.argv[sys.argv.index("--")+1]; os.makedirs(out, exist_ok=True)
for nome, blend, acao in (("base", "rig_base.blend", None), ("idle", "rig_anim.blend", "Idle"), ("comer", "rig_anim.blend", "Comer")):
    bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-cavalo2/saida/" + blend)
    arm = bpy.data.objects["CavaloArmature"]; arm.hide_render = True
    if acao: arm.animation_data.action = bpy.data.actions[acao]; bpy.context.scene.frame_set(20)
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 700; sc.render.resolution_y = 600; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 1.4
    c = V((0.0, -0.55, 0.85)); d = V((-1, -0.35, 0.1)).normalized(); cam.location = c + d * 6; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(out, nome + ".png"); bpy.ops.render.render(write_still=True)
