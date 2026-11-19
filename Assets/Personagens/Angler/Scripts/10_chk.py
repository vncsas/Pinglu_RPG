import bpy
bpy.ops.wm.open_mainfile(filepath="C:/Users/Thiago/Documentos/rig-angler/saida/rig_anim.blend")
arm = bpy.data.objects["AnglerArmature"]; mesh = bpy.data.objects["Angler"]
for nome in ("Idle", "Walk", "Run", "Attack_Mordida", "Attack_Garras", "Attack_Cauda", "Attack_Salto", "Rugido", "Dano", "Morte"):
    ac = bpy.data.actions[nome]; arm.animation_data.action = ac; f0, f1 = int(ac.frame_range[0]), int(ac.frame_range[1]); zs = []
    for f in range(f0, f1 + 1, 3):
        bpy.context.scene.frame_set(f); dg = bpy.context.evaluated_depsgraph_get(); me = mesh.evaluated_get(dg).to_mesh(); zs.append((min(v.co.z for v in me.vertices), max(v.co.z for v in me.vertices)))
    print("CHK %-15s zmin min %.2f  max %.2f | zmax max %.2f | primeiro %.2f ultimo %.2f" % (nome, min(z[0] for z in zs), max(z[0] for z in zs), max(z[1] for z in zs), zs[0][0], zs[-1][0]))
