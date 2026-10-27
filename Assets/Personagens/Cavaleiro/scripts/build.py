import bpy, sys, time, math, numpy as np
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
d=sys.argv[-2]; outdir=sys.argv[-1]; sys.path.insert(0,d)
import segment
from skeleton import J, PARENT, CONNECT
T0=time.time()
def log(*a): print(f"[{time.time()-T0:6.1f}s]",*a, flush=True)

body=bpy.data.objects["Knight"]; me=body.data
# ---------- 1. segment + separate
v=np.empty(len(me.vertices)*3); me.vertices.foreach_get("co",v); v=v.reshape(-1,3)
E=np.empty(len(me.edges)*2,np.int64); me.edges.foreach_get("vertices",E); E=E.reshape(-1,2)
def nearest(src,q):
    kd=KDTree(len(src))
    for i in src: kd.insert(v[i],int(i))
    kd.balance(); return np.array([kd.find(v[j])[2] for j in q])
sw,sh=segment.segment(v,E,nearest,log)
lab=np.zeros(len(v),np.int8); lab[sw]=1; lab[sh]=2
# face label by vertex majority
loops=np.empty(len(me.loops),np.int64); me.loops.foreach_get("vertex_index",loops)
ls=np.empty(len(me.polygons),np.int64); me.polygons.foreach_get("loop_start",ls)
lt=np.empty(len(me.polygons),np.int64); me.polygons.foreach_get("loop_total",lt)
fl=lab[loops]; cnt=np.stack([np.add.reduceat((fl==k).astype(int),ls) for k in range(3)],1)
flab=np.argmax(cnt,1)
bpy.context.view_layer.objects.active=body
def separate(k,name):
    bpy.ops.object.mode_set(mode='OBJECT')
    n=len(body.data.polygons)
    sel=(flab_cur==k)
    body.data.polygons.foreach_set("select",sel)
    bpy.ops.object.select_all(action='DESELECT'); body.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_mode(type='FACE')
    bpy.ops.object.mode_set(mode='OBJECT'); body.data.polygons.foreach_set("select",sel)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.separate(type='SELECTED'); bpy.ops.object.mode_set(mode='OBJECT')
    new=[o for o in bpy.context.selected_objects if o!=body][0]; new.name=name; new.data.name=name
    bpy.ops.object.select_all(action='DESELECT')
    return new, flab_cur[~sel]
flab_cur=flab
sword,flab_cur=separate(1,"Sword")
shield,flab_cur=separate(2,"Shield")
body.name="Body"; body.data.name="Body"
for o in (body,sword,shield):
    bpy.context.view_layer.objects.active=o; o.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.delete_loose()  # stray verts/edges without faces
    bpy.ops.object.mode_set(mode='OBJECT'); o.select_set(False)
    log(o.name, len(o.data.vertices),"verts", len(o.data.polygons),"faces")
# weapon origins at their attach points
def set_origin(o,p):
    p=Vector(p); o.data.transform(Matrix.Translation(-p)); o.location=p

# ---------- 2. armature
arm_data=bpy.data.armatures.new("Armature"); arm=bpy.data.objects.new("Armature",arm_data)
bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm; arm.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
eb=arm_data.edit_bones
for n,(h,t) in J.items():
    b=eb.new(n); b.head=h; b.tail=t
for n,p in PARENT.items():
    eb[n].parent=eb[p]; eb[n].use_connect = n in CONNECT
# rolls: Z axis of every bone points forward (-Y) for torso/legs, consistent for arms
for b in eb:
    b.align_roll(Vector((0,-1,0)) if not ('Arm' in b.name or 'Hand' in b.name or 'Shoulder' in b.name) else Vector((0,0,1)))
# weapon sockets (extra, non-humanoid bones)
P0=segment.SWORD_P0; D=Vector(segment.SWORD_D)
grip=Vector(P0)+D*0.42
b=eb.new("RightHandWeapon"); b.head=grip; b.tail=grip-D*0.15; b.parent=eb["RightHand"]; b.align_roll(Vector((0,0,1)))
fa=(Vector(J['LeftLowerArm'][0])+Vector(J['LeftLowerArm'][1]))/2
b=eb.new("LeftForearmShield"); b.head=fa; b.tail=fa+Vector((0.12,0,0)); b.parent=eb["LeftLowerArm"]; b.align_roll(Vector((0,0,1)))
bpy.ops.object.mode_set(mode='OBJECT')
for n in ("RightHandWeapon","LeftForearmShield"): arm_data.bones[n].use_deform=False  # excluded from auto weights

# ---------- 3. body auto weights
# Bone heat fails on this dense 1.8 m mesh at real scale (numerical precision); solve at 10x scale.
for o in (body,arm): o.scale=(10,10,10)
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active=arm
log("auto weights start")
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
log("auto weights done")
body.parent=None
for o in (body,arm): o.scale=(1,1,1); o.location=(0,0,0)
body.parent=arm; body.matrix_parent_inverse=Matrix.Identity(4)
bpy.context.view_layer.update()
# fill verts heat weighting left empty, and re-weight small loose islands (rivets, spike bits: heat
# weighting gives them arbitrary bones), from the nearest vertex of the main connected surface
bv=body.data.vertices
bco=np.empty(len(bv)*3); bv.foreach_get("co",bco); bco=bco.reshape(-1,3)
bE=np.empty(len(body.data.edges)*2,np.int64); body.data.edges.foreach_get("vertices",bE); bE=bE.reshape(-1,2)
blab=segment.components(bE,np.ones(len(bv),bool)); ids,cnt=np.unique(blab,return_counts=True)
wl=[[(g.group,g.weight) for g in vv.groups if g.weight>0.001] for vv in bv]
src=(blab==ids[np.argmax(cnt)])&np.array([bool(w) for w in wl])
fix=np.nonzero(~src)[0]
log("re-weighting",len(fix),"verts (empty or loose islands)")
kd=KDTree(int(src.sum()))
for i in np.nonzero(src)[0]: kd.insert(bco[i],int(i))
kd.balance()
for i in fix:
    for g in list(bv[int(i)].groups): body.vertex_groups[g.group].remove([int(i)])
    j=kd.find(bco[i])[1]
    for gi,w in wl[j]: body.vertex_groups[gi].add([int(i)],w,'REPLACE')
# ---------- 3b. decimate (after weighting: bone heat only converges on the dense mesh; Decimate interpolates the vertex groups) for real-time use (collapse keeps UVs, so the original textures still map)
TARGET_TRIS=dict(Body=int(sys.argv[-3].split(',')[0]),Shield=int(sys.argv[-3].split(',')[1]),Sword=int(sys.argv[-3].split(',')[2]))
for o in (body,sword,shield):
    tris=sum(len(p.vertices)-2 for p in o.data.polygons)
    m=o.modifiers.new("Decimate",'DECIMATE'); m.decimate_type='COLLAPSE'; m.ratio=min(1.0,TARGET_TRIS[o.name]/tris)
    m.use_collapse_triangulate=True
    bpy.ops.object.select_all(action='DESELECT'); bpy.context.view_layer.objects.active=o; o.select_set(True)
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.mesh.customdata_custom_splitnormals_clear()
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    o.select_set(False)
    log(o.name,"decimated",tris,"->",len(o.data.polygons),"tris")

# armour is rigid: sharpen heat weights (w^p, renormalised) so each plate follows one bone and
# blending is confined to a narrow band around the joints
SHARPEN=float(sys.argv[-4])
bv=body.data.vertices
for vv in bv:
    gs=[(g.group,g.weight) for g in vv.groups if g.weight>0]
    if len(gs)<2: continue
    tot=sum(w**SHARPEN for _,w in gs)
    for gi,w in gs: body.vertex_groups[gi].add([vv.index],(w**SHARPEN)/tot,'REPLACE')
# Unity: max 4 influences, normalized
bpy.context.view_layer.objects.active=body; bpy.ops.object.select_all(action='DESELECT'); body.select_set(True)
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL',limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)
bpy.ops.object.vertex_group_clean(group_select_mode='ALL',limit=0.001)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)
log("groups:",[g.name for g in body.vertex_groups])

# ---------- 4. weapons: rigid skin to socket bones
for n in ("RightHandWeapon","LeftForearmShield"): arm_data.bones[n].use_deform=True
for o,bn in ((sword,"RightHandWeapon"),(shield,"LeftForearmShield")):
    set_origin(o, arm_data.bones[bn].head_local)
    g=o.vertex_groups.new(name=bn); g.add(range(len(o.data.vertices)),1.0,'REPLACE')
    o.parent=arm; o.matrix_parent_inverse=arm.matrix_world.inverted()
    m=o.modifiers.new("Armature",'ARMATURE'); m.object=arm
arm.show_in_front=True
bpy.ops.wm.save_as_mainfile(filepath=outdir+"/cavaleiro_rig.blend")
log("saved blend")
# ---------- 5. export FBX
bpy.ops.object.select_all(action='DESELECT')
for o in (arm,body,sword,shield): o.select_set(True)
bpy.ops.export_scene.fbx(filepath=outdir+"/cavaleiro_rigged.fbx", use_selection=True,
    object_types={'ARMATURE','MESH'}, apply_scale_options='FBX_SCALE_ALL',
    use_armature_deform_only=True, add_leaf_bones=False, bake_anim=False,
    primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
    mesh_smooth_type='FACE', path_mode='COPY', embed_textures=True)
log("exported fbx")
