import bpy, sys, math
from mathutils import Matrix, Vector
fbx, out = sys.argv[-2:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
o=[o for o in bpy.data.objects if o.type=='MESH'][0]
o.name="Knight"
me=o.data
# face -Y, scale to 1.8m, feet at z=0
M=Matrix.Rotation(-math.pi/2,4,'Z')
me.transform(o.matrix_world @ M if False else M @ o.matrix_world)
o.matrix_world=Matrix.Identity(4)
zs=[v.co.z for v in me.vertices]; h=max(zs)-min(zs)
s=1.8/h
me.transform(Matrix.Scale(s,4))
zmin=min(v.co.z for v in me.vertices)
xs=[v.co.x for v in me.vertices]; ys=[v.co.y for v in me.vertices]
me.transform(Matrix.Translation((0,0,-zmin)))
print("scale",s,"x",min(xs),max(xs),"y",min(ys),max(ys))
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=out)
