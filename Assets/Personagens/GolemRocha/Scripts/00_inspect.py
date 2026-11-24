import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath="C:/Users/Thiago/Documentos/character-free-golem-rock-by-oscar-creativo/source/Character Free Golem Rock by Oscar Creativo.fbx")
for o in bpy.data.objects:
    print("OBJ", o.name, o.type, tuple(round(x,3) for x in o.dimensions), "loc", tuple(round(x,3) for x in o.location), "scale", tuple(round(x,3) for x in o.scale), "parent", o.parent.name if o.parent else None)
    if o.type=='MESH':
        print("  verts", len(o.data.vertices), "tris", sum(len(p.vertices)-2 for p in o.data.polygons), "vgroups", len(o.vertex_groups), "mods", [m.type for m in o.modifiers], "uv", len(o.data.uv_layers), "mats", [m.name for m in o.data.materials])
    if o.type=='ARMATURE':
        print("  bones", len(o.data.bones))
        for b in o.data.bones: print("   ", b.name, b.parent.name if b.parent else None)
print("ACTIONS", [a.name for a in bpy.data.actions])
