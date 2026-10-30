# Validates a rigged character FBX before it goes to Unity (Humanoid).
# usage: blender -b -P validate.py -- personagem.fbx [--tris 100000] [--influences 4]
# Exit code 1 if any FAIL. Bone names follow the project convention (Unity Humanoid names).
import bpy, sys, math
from mathutils import Vector

args = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
path = args[0]
def opt(name, default):
    return type(default)(args[args.index(name)+1]) if name in args else default
MAX_TRIS, MAX_INF = opt("--tris", 100000), opt("--influences", 4)

REQUIRED = {  # bone: required ancestor (direct or indirect)
    "Hips": None, "Spine": "Hips", "Head": "Spine",
    "LeftUpperLeg": "Hips", "LeftLowerLeg": "LeftUpperLeg", "LeftFoot": "LeftLowerLeg",
    "RightUpperLeg": "Hips", "RightLowerLeg": "RightUpperLeg", "RightFoot": "RightLowerLeg",
    "LeftUpperArm": "Spine", "LeftLowerArm": "LeftUpperArm", "LeftHand": "LeftLowerArm",
    "RightUpperArm": "Spine", "RightLowerArm": "RightUpperArm", "RightHand": "RightLowerArm"}
OPTIONAL = ["Chest", "UpperChest", "Neck", "LeftShoulder", "RightShoulder", "LeftToes", "RightToes"]
CONTROL_PREFIXES = ("CTRL_", "IK_", "MCH_", "RF_", "POLE_", "WGT", "DEF-", "ORG-")

results = []
def report(level, msg): results.append((level, msg))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)
arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
if len(arms) != 1:
    report("FAIL", f"esperado 1 armature, encontrado {len(arms)}")
else:
    arm = arms[0]; bones = arm.data.bones
    report("INFO", f"armature '{arm.name}': {len(bones)} ossos")
    # humanoid bones + hierarchy
    for b, anc in REQUIRED.items():
        if b not in bones: report("FAIL", f"osso obrigatório do Humanoid ausente: {b}"); continue
        if anc:
            p = bones[b].parent
            while p and p.name != anc: p = p.parent
            if not p: report("FAIL", f"{b} deveria descender de {anc}")
    missing_opt = [b for b in OPTIONAL if b not in bones]
    report("WARN" if missing_opt else "OK", f"ossos opcionais ausentes: {missing_opt}" if missing_opt else "ossos opcionais do Humanoid presentes")
    if "Hips" in bones and bones["Hips"].parent:
        report("WARN", f"Hips tem pai '{bones['Hips'].parent.name}' (o Humanoid usa Hips como raiz do corpo)")
    leaf = [b.name for b in bones if b.name.lower().endswith("_end")]
    report("FAIL" if leaf else "OK", f"leaf bones exportados: {leaf}" if leaf else "sem leaf bones")
    ctrl = [b.name for b in bones if b.name.startswith(CONTROL_PREFIXES)]
    report("FAIL" if ctrl else "OK", f"ossos de controle no FBX (exportar só deform): {ctrl}" if ctrl else "só ossos de deformação")
    # scale
    for o in [arm]+meshes:
        if any(abs(s-1) > 1e-3 for s in o.scale): report("WARN", f"{o.name}: escala {tuple(round(s, 3) for s in o.scale)} (esperado 1)")
    # rest pose (T-pose check)
    for S in ("Left", "Right"):
        n = S+"UpperArm"
        if n in bones:
            dvec = (arm.matrix_world.to_3x3() @ (bones[n].tail_local-bones[n].head_local)).normalized()
            up = Vector((0, 0, 1)) if abs(arm.matrix_world.to_3x3().col[2].z) > 0.5 else Vector((0, 1, 0))
            elev = math.degrees(math.asin(max(-1, min(1, dvec.dot(up)))))
            report("OK" if abs(elev) < 15 else "WARN", f"{n}: {elev:+.0f}° em relação à horizontal ({'T-pose' if abs(elev) < 15 else 'não é T-pose'})")
    # meshes
    total = 0
    for o in meshes:
        tris = sum(len(p.vertices)-2 for p in o.data.polygons); total += tris
        skinned = any(m.type == 'ARMATURE' for m in o.modifiers)
        if o.parent_type == 'BONE' or (o.parent and o.parent.type == 'ARMATURE' and o.parent_bone):
            report("INFO", f"{o.name}: prop rígido preso no osso '{o.parent_bone}' ({tris} tris)")
            continue
        if not skinned:
            report("WARN", f"{o.name}: malha sem skin e sem osso pai ({tris} tris)"); continue
        names = {g.index: g.name for g in o.vertex_groups}
        orphan = [n for n in names.values() if n not in bones]
        if orphan: report("WARN", f"{o.name}: vertex groups sem osso: {orphan}")
        unw = over = badsum = 0
        for v in o.data.vertices:
            ws = [g.weight for g in v.groups if g.weight > 1e-4 and names.get(g.group) in bones]
            if not ws: unw += 1; continue
            if len(ws) > MAX_INF: over += 1
            if abs(sum(ws)-1) > 0.01: badsum += 1
        report("FAIL" if unw else "OK", f"{o.name}: {unw} vértices sem peso" if unw else f"{o.name}: todos os {len(o.data.vertices)} vértices com peso")
        report("FAIL" if over else "OK", f"{o.name}: {over} vértices com mais de {MAX_INF} ossos" if over else f"{o.name}: no máximo {MAX_INF} ossos por vértice")
        report("WARN" if badsum else "OK", f"{o.name}: {badsum} vértices com soma de pesos ≠ 1" if badsum else f"{o.name}: pesos normalizados")
        report("INFO", f"{o.name}: {tris} tris, skin em {len(names)} ossos")
    report("OK" if total <= MAX_TRIS else "WARN", f"total {total} triângulos (limite {MAX_TRIS})")
    # floor
    zs = [(o.matrix_world @ v.co).z for o in meshes for v in o.data.vertices]
    if zs:
        up_is_z = True
        report("OK" if abs(min(zs)) < 0.03 else "WARN", f"altura {max(zs)-min(zs):.2f} m, pé em z={min(zs):+.3f}")

print("\n=== VALIDAÇÃO:", path)
for lvl, msg in results: print(f"[{lvl:4}] {msg}")
fails = sum(1 for l, _ in results if l == "FAIL"); warns = sum(1 for l, _ in results if l == "WARN")
print(f"=== {fails} FAIL, {warns} WARN ->", "REPROVADO" if fails else "APROVADO")
sys.exit(1 if fails else 0)
