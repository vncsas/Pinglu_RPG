import bpy
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-angler/saida/prep0.blend")
m=bpy.data.objects["Angler"]; bpy.context.view_layer.objects.active=m; m.select_set(True)
md=m.modifiers.new("d",'DECIMATE'); md.ratio=90000/1912248.0; md.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier="d")
m.scale=(3,3,3); bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.object.shade_smooth()
print("TRIS", sum(len(p.vertices)-2 for p in m.data.polygons), "UV", len(m.data.uv_layers))
bpy.ops.wm.save_as_mainfile(filepath="C:/Users/Thiago/Documentos/rig-angler/saida/prep.blend")
