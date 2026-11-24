# Normaliza o golem: escala em metros (aplica 0.01 do armature), mantem as 15 pecas separadas, nomes limpos.
import bpy
base = "C:/Users/Thiago/Documentos/rig-golem/"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath="C:/Users/Thiago/Documentos/character-free-golem-rock-by-oscar-creativo/source/Character Free Golem Rock by Oscar Creativo.fbx")
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; ms = [o for o in bpy.data.objects if o.type == 'MESH']
for a in list(bpy.data.actions): bpy.data.actions.remove(a)          # ignora as acoes "TempMotion" que vieram do exportador
bpy.ops.object.select_all(action='DESELECT')
for m in ms: m.select_set(True)
bpy.context.view_layer.objects.active = ms[0]
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')            # as pecas ficam com o 0.01 delas
bpy.ops.object.select_all(action='DESELECT')
for m in ms: m.select_set(True)
bpy.context.view_layer.objects.active = ms[0]; bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for m in ms:
    m.parent = arm; m.matrix_parent_inverse = arm.matrix_world.inverted()
    for mod in m.modifiers:
        if mod.type == 'ARMATURE': mod.object = arm
    m.name = "Golem_" + m.name.capitalize(); m.data.name = m.name
arm.name = "Golem_Rig"; arm.data.name = "Golem_Rig"
zs = [(m.matrix_world @ v.co).z for m in ms for v in m.data.vertices]; print("ALTURA", round(min(zs), 3), round(max(zs), 3))
print("PECAS", len(ms), "OSSOS", len(arm.data.bones), "escala arm", tuple(round(x, 3) for x in arm.scale))
# pesos: todos os vertices tem algum peso?
sem = 0
for m in ms:
    for v in m.data.vertices:
        if not any(g.weight > 0.001 for g in v.groups): sem += 1
print("VERTICES SEM PESO", sem)
bpy.ops.wm.save_as_mainfile(filepath=base + "saida/golem_base.blend")
