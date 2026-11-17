# Endireita pernas/bracos (pose do scan e um passo largo): gira cada osso p/ a direcao alvo, aplica na malha, sobe tudo para os pes tocarem z=0, escala p/ 2.3 m
import bpy, sys
sys.path.insert(0, "C:/Users/Thiago/Documentos/rig-pantano/scripts")
from mathutils import Vector as V, Matrix
import mdef as M
base = "C:/Users/Thiago/Documentos/rig-pantano/"
bpy.ops.wm.open_mainfile(filepath=base + "saida/rig_scan.blend")
arm = bpy.data.objects["MonstroArmature"]; mesh = bpy.data.objects["Monstro"]
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='POSE'); pb = arm.pose.bones
def girar(nome, tgt):
    b = pb[nome]; Mx = b.matrix.copy(); cur = (b.tail - b.head).normalized()
    R = cur.rotation_difference(tgt.normalized()).to_matrix().to_4x4(); loc = Mx.translation.copy(); Mx.translation = V((0, 0, 0))
    b.matrix = Matrix.Translation(loc) @ R @ Mx; bpy.context.view_layer.update()
for nm in ("Spine1", "Spine2", "Chest", "Neck", "Head", "L_Clavicle", "R_Clavicle"): girar(nm, M.ALVO[nm])
for s in "LR":
    for suf in ("Thigh", "Shin", "Foot", "Toe", "Arm", "Forearm", "Hand", "Fingers"): girar("%s_%s" % (s, suf), M.ALVO["%s_%s" % (s, suf)])
bpy.ops.object.mode_set(mode='OBJECT'); bpy.ops.object.select_all(action='DESELECT'); mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
bpy.ops.object.modifier_apply(modifier="Armature")
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='POSE'); bpy.ops.pose.armature_apply(selected=False); bpy.ops.object.mode_set(mode='OBJECT')
mesh.modifiers.new("Armature", 'ARMATURE').object = arm
zmin = min(v.co.z for v in mesh.data.vertices); zmax = max(v.co.z for v in mesh.data.vertices)
print("ZMIN", zmin, "ZMAX", zmax)
# subir + escalar: mover malha e armature juntos (parent), depois aplicar
sc = 2.3 / (zmax - zmin)
mesh.parent = None
for o in (arm, mesh): o.select_set(True)
arm.location = (0, 0, 0); 
# aplica deslocamento/escala na malha e nos ossos
mesh.data.transform(Matrix.Scale(sc, 4) @ Matrix.Translation((0, 0, -zmin)))
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
for eb in arm.data.edit_bones:
    h = (eb.head + V((0, 0, -zmin))) * sc; t = (eb.tail + V((0, 0, -zmin))) * sc; r = eb.roll; eb.head = h; eb.tail = t; eb.roll = r
bpy.ops.object.mode_set(mode='OBJECT')
mesh.parent = arm
mesh.matrix_parent_inverse = arm.matrix_world.inverted()
for o in (arm, mesh): print(o.name, tuple(o.location), tuple(o.scale))
for eb in arm.data.bones:
    if eb.name.endswith("Toe"): print("TOE", eb.name, tuple(round(c, 3) for c in eb.tail_local))
bpy.ops.wm.save_as_mainfile(filepath=base + "saida/rig_empe.blend")
