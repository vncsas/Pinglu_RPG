import bpy, os
from mathutils import Vector as V
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-golem/saida/golem_final.blend")
arm = bpy.data.objects["Golem_Rig"]; arm.hide_render = True; sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.render.resolution_x = 640; sc.render.resolution_y = 640
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.25, 0.27, 0.32)
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = 3.5
c = V((0, -0.1, 1.25))
for nome, d in (("frente", V((0, 1, 0))), ("lado", V((1, 0, 0))), ("tres", V((0.7, 0.7, -0.1)))):
    cam.location = c - d.normalized() * 8; cam.rotation_euler = d.normalized().to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = "C:/Users/Thiago/Documentos/rig-golem/saida/pf/tex_%s.png" % nome; bpy.ops.render.render(write_still=True)
