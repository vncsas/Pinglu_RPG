# Medieval buildings (blockouts with real proportions, meant to be textured with tiling CC0 materials in Unity).
# Meshes in metres, origin at the bottom centre, +Y forward in Blender (Unity: Z), door on the front (-Y in Blender).
# Material slots (by name, Unity maps them): Reboco (plaster), Madeira (timber), Telhado (roof), Pedra (stone), Porta
# usage: blender -b -P make_buildings.py -- <out_dir>
import bpy, bmesh, sys, os, math, random
from mathutils import Vector, Matrix
out = sys.argv[sys.argv.index("--")+1]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
MATS = {}
def mat(name):
    if name not in MATS: MATS[name] = bpy.data.materials.new(name)
    return MATS[name]

class B:
    def __init__(self, name): self.name = name; self.bm = bmesh.new(); self.slots = []
    def slot(self, m):
        if m not in self.slots: self.slots.append(m)
        return self.slots.index(m)
    def box(self, c, size, m, rot=0.0, uvs=2.0):
        bm = self.bm; sx, sy, sz = size
        ret = bmesh.ops.create_cube(bm, size=1.0)
        verts = ret["verts"]
        for v in verts: v.co = Vector((v.co.x*sx, v.co.y*sy, v.co.z*sz))
        if rot: bmesh.ops.rotate(bm, verts=verts, cent=(0, 0, 0), matrix=Matrix.Rotation(rot, 3, 'Z'))
        bmesh.ops.translate(bm, verts=verts, vec=Vector(c))
        faces = {f for v in verts for f in v.link_faces}
        si = self.slot(m)
        for f in faces:
            f.material_index = si
        self.uv(faces, uvs); return faces
    def prism_roof(self, c, w, d, h, over, m, thick=0.12, uvs=2.0, gable=None):
        """gabled roof along Y: ridge at the top, eaves overhanging by `over`"""
        bm = self.bm; hw = w/2+over; hd = d/2+over; cx, cy, cz = c
        v = [bm.verts.new(p) for p in ((cx-hw, cy-hd, cz), (cx+hw, cy-hd, cz), (cx+hw, cy+hd, cz), (cx-hw, cy+hd, cz),
                                        (cx, cy-hd, cz+h), (cx, cy+hd, cz+h),
                                        (cx-hw, cy-hd, cz-thick), (cx+hw, cy-hd, cz-thick), (cx+hw, cy+hd, cz-thick), (cx-hw, cy+hd, cz-thick),
                                        (cx, cy-hd, cz+h-thick), (cx, cy+hd, cz+h-thick))]
        si = self.slot(m); fs = []
        fs.append(bm.faces.new((v[0], v[3], v[5], v[4])))     # left slope (top)
        fs.append(bm.faces.new((v[1], v[4], v[5], v[2])))     # right slope (top)
        fs.append(bm.faces.new((v[6], v[10], v[11], v[9])))   # left underside
        fs.append(bm.faces.new((v[7], v[8], v[11], v[10])))   # right underside
        fs.append(bm.faces.new((v[0], v[4], v[10], v[6])))    # front gable edge
        fs.append(bm.faces.new((v[1], v[7], v[10], v[4])))
        fs.append(bm.faces.new((v[3], v[9], v[11], v[5])))    # back gable edge
        fs.append(bm.faces.new((v[2], v[5], v[11], v[8])))
        fs.append(bm.faces.new((v[0], v[6], v[7], v[1])))     # eave edge front... (thin)
        fs.append(bm.faces.new((v[3], v[2], v[8], v[9])))
        for f in fs: f.material_index = si
        self.uv(fs, uvs)
        if gable:                                             # plaster triangles closing the front/back of the roof
            gs = self.slot(gable); tr = []
            for sy in (-1, 1):
                y = cy+sy*d/2
                a = bm.verts.new((cx-w/2, y, cz)); bb = bm.verts.new((cx+w/2, y, cz)); cc = bm.verts.new((cx, y, cz+h-0.02))
                f = bm.faces.new((a, bb, cc) if sy < 0 else (bb, a, cc)); f.material_index = gs; tr.append(f)
            self.uv(tr, 2.5)
        return fs
    def uv(self, faces, scale):
        uvl = self.bm.loops.layers.uv.verify()
        for f in faces:
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for l in f.loops:
                p = l.vert.co
                u, v = ((p.y, p.z) if ax == 0 else (p.x, p.z) if ax == 1 else (p.x, p.y))
                l[uvl].uv = (u/scale, v/scale)
    def finish(self):
        bm = self.bm
        me = bpy.data.meshes.new(self.name); bm.to_mesh(me); bm.free()
        for s in self.slots: me.materials.append(mat(s))
        o = bpy.data.objects.new(self.name, me); bpy.context.scene.collection.objects.link(o)
        for p in me.polygons: p.use_smooth = False
        return o

def timber_wall(b, c, w, d, h, with_door=None, floors=1):
    """plaster walls + exposed timber frame (posts, rails, diagonal braces) on the four sides"""
    cx, cy, cz = c
    b.box((cx, cy, cz+h/2), (w, d, h), "Reboco", uvs=2.5)
    t = 0.14
    for sx in (-1, 1):                                    # corner posts
        for sy in (-1, 1): b.box((cx+sx*(w/2), cy+sy*(d/2), cz+h/2), (t*1.3, t*1.3, h+0.05), "Madeira", uvs=1.0)
    levels = [cz+0.05, cz+h-0.1] + ([cz+h/2] if floors > 1 else [])
    for z in levels:                                       # horizontal rails
        b.box((cx, cy-d/2-0.01, z), (w+0.1, t*0.9, t), "Madeira", uvs=1.0); b.box((cx, cy+d/2+0.01, z), (w+0.1, t*0.9, t), "Madeira", uvs=1.0)
        b.box((cx-w/2-0.01, cy, z), (t*0.9, d+0.1, t), "Madeira", uvs=1.0); b.box((cx+w/2+0.01, cy, z), (t*0.9, d+0.1, t), "Madeira", uvs=1.0)
    n = max(2, int(w//1.8))                                # vertical studs + braces on front/back
    for i in range(1, n):
        x = cx-w/2+i*w/n
        for sy in (-1, 1): b.box((x, cy+sy*(d/2+0.01), cz+h/2), (t*0.8, t*0.8, h), "Madeira", uvs=1.0)
    for sy in (-1, 1):
        for (x0, s) in ((cx-w/2+0.2, 1), (cx+w/2-0.2, -1)):
            ang = math.atan2(h*0.55, w*0.2)
            b.box((x0+s*w*0.1, cy+sy*(d/2+0.012), cz+h*0.35), (w*0.24, t*0.7, t*0.7), "Madeira", rot=0, uvs=1.0)
    if with_door:
        dx, dz = with_door
        b.box((cx+dx, cy-d/2-0.04, cz+dz/2), (1.0, 0.1, dz), "Porta", uvs=1.5)
        b.box((cx+dx, cy-d/2-0.06, cz+dz+0.06), (1.25, 0.14, 0.14), "Madeira", uvs=1.0)

def stone_base(b, c, w, d, h):
    cx, cy, cz = c
    b.box((cx, cy, cz+h/2), (w+0.25, d+0.25, h), "Pedra", uvs=2.0)

def window(b, c, side=-1):
    cx, cy, cz = c
    b.box((cx, cy+side*0.03, cz), (0.7, 0.08, 0.8), "Madeira", uvs=1.0)
    b.box((cx, cy+side*0.07, cz), (0.5, 0.04, 0.6), "Janela", uvs=1.0)

def chimney(b, c, h):
    b.box((c[0], c[1], c[2]+h/2), (0.8, 0.8, h), "Pedra", uvs=1.5)

def house_small():
    b = B("Casa_Pequena"); w, d, h = 5.0, 4.5, 2.6
    stone_base(b, (0, 0, 0), w, d, 0.5); timber_wall(b, (0, 0, 0.5), w, d, h, with_door=(-1.0, 1.9))
    window(b, (1.2, -d/2, 1.7)); window(b, (-w/2-0.0, 0.6, 1.7)) if False else None
    b.prism_roof((0, 0, 0.5+h), w, d, 1.9, 0.55, "Telhado", uvs=2.0, gable="Reboco")
    chimney(b, (w/2-0.6, 0.3, 0.5+h+0.5), 2.2); return b.finish()

def house_big():
    b = B("Casa_Grande"); w, d, h = 7.0, 5.5, 5.0
    stone_base(b, (0, 0, 0), w, d, 0.7); timber_wall(b, (0, 0, 0.7), w, d, h, with_door=(-1.5, 2.0), floors=2)
    for x in (-2.4, 0.4, 2.4): window(b, (x, -d/2, 3.6))
    window(b, (1.6, -d/2, 1.6))
    b.prism_roof((0, 0, 0.7+h), w, d, 2.6, 0.7, "Telhado", uvs=2.0, gable="Reboco")
    chimney(b, (-w/2+0.8, 0.5, 0.7+h+0.6), 3.0)
    b.box((w/2+0.9, 0, 1.0), (1.6, 2.4, 2.0), "Madeira", uvs=1.0)           # lean-to shed
    b.prism_roof((w/2+0.9, 0, 2.0), 1.6, 2.4, 0.5, 0.2, "Telhado", uvs=2.0, gable="Madeira")
    return b.finish()

def tavern():
    b = B("Taverna"); w, d, h = 9.0, 6.5, 5.6
    stone_base(b, (0, 0, 0), w, d, 0.8); timber_wall(b, (0, 0, 0.8), w, d, h, with_door=(0.0, 2.1), floors=2)
    for x in (-3.2, -1.6, 1.6, 3.2): window(b, (x, -d/2, 1.8)); window(b, (x, -d/2, 4.2))
    b.prism_roof((0, 0, 0.8+h), w, d, 3.0, 0.8, "Telhado", uvs=2.0, gable="Reboco")
    chimney(b, (w/2-1.0, 0.0, 0.8+h+0.8), 3.4)
    b.box((0.0, -d/2-0.9, 3.0), (3.0, 0.15, 0.6), "Madeira", uvs=1.0)       # hanging sign board
    b.box((-1.4, -d/2-0.45, 2.0), (0.14, 1.0, 0.14), "Madeira", uvs=1.0); b.box((1.4, -d/2-0.45, 2.0), (0.14, 1.0, 0.14), "Madeira", uvs=1.0)
    return b.finish()

def well():
    b = B("Poco"); r = 0.9
    bm = b.bm; segs = 14; si = b.slot("Pedra"); fs = []
    for z0, z1, ro in ((0.0, 0.9, r), ):
        ring_o = [Vector((math.cos(a*math.tau/segs)*ro, math.sin(a*math.tau/segs)*ro, z)) for z in (z0, z1) for a in range(segs)]
    vo = [[bm.verts.new((math.cos(a*math.tau/segs)*r, math.sin(a*math.tau/segs)*r, z)) for a in range(segs)] for z in (0.0, 0.9)]
    vi = [[bm.verts.new((math.cos(a*math.tau/segs)*(r-0.22), math.sin(a*math.tau/segs)*(r-0.22), z)) for a in range(segs)] for z in (0.0, 0.9)]
    for a in range(segs):
        b2 = (a+1) % segs
        fs.append(bm.faces.new((vo[0][a], vo[0][b2], vo[1][b2], vo[1][a])))
        fs.append(bm.faces.new((vi[0][b2], vi[0][a], vi[1][a], vi[1][b2])))
        fs.append(bm.faces.new((vo[1][a], vo[1][b2], vi[1][b2], vi[1][a])))
    for f in fs: f.material_index = si
    b.uv(fs, 1.5)
    for sx in (-1, 1): b.box((sx*0.95, 0, 1.6), (0.14, 0.14, 1.6), "Madeira", uvs=1.0)
    b.box((0, 0, 0.45), (1.4, 1.4, 0.02), "Agua", uvs=1.0)
    b.prism_roof((0, 0, 2.3), 1.2, 1.9, 0.7, 0.3, "Telhado", uvs=1.5, gable="Madeira")
    b.box((0, 0, 1.8), (0.08, 1.0, 0.08), "Madeira", uvs=1.0)
    return b.finish()

def stall():
    b = B("Barraca");
    for sx in (-1, 1):
        for sy in (-1, 1): b.box((sx*1.2, sy*0.8, 1.1), (0.12, 0.12, 2.2), "Madeira", uvs=1.0)
    b.box((0, 0.0, 0.9), (2.5, 1.7, 0.1), "Madeira", uvs=1.0); b.box((0, 0.7, 0.45), (2.4, 0.2, 0.9), "Madeira", uvs=1.0)
    b.prism_roof((0, 0, 2.2), 2.4, 1.6, 0.6, 0.3, "Pano", uvs=1.5); return b.finish()

def fence(length=3.0):
    b = B("Cerca_3m"); n = int(length/1.5)+1
    for i in range(n+1): b.box((-length/2+i*length/n, 0, 0.55), (0.12, 0.12, 1.1), "Madeira", uvs=1.0)
    for z in (0.4, 0.85): b.box((0, 0, z), (length, 0.07, 0.12), "Madeira", uvs=1.0)
    return b.finish()

def wall_stone(length=4.0):
    b = B("Muro_4m"); b.box((0, 0, 0.9), (length, 0.7, 1.8), "Pedra", uvs=2.0)
    b.box((0, 0, 1.85), (length+0.05, 0.85, 0.15), "Pedra", uvs=2.0); return b.finish()

def tower():
    b = B("Torre_Vigia"); bm = b.bm; segs = 16; r = 2.4; h = 9.0; si = b.slot("Pedra"); fs = []
    ring = [[bm.verts.new((math.cos(a*math.tau/segs)*r*(1.0-0.08*zi), math.sin(a*math.tau/segs)*r*(1.0-0.08*zi), z)) for a in range(segs)] for zi, z in enumerate((0.0, h))]
    for a in range(segs):
        b2 = (a+1) % segs; fs.append(bm.faces.new((ring[0][a], ring[0][b2], ring[1][b2], ring[1][a])))
    for f in fs: f.material_index = si
    b.uv(fs, 2.0)
    for i in range(10):                                   # battlements
        a = i*math.tau/10; b.box((math.cos(a)*r*0.95, math.sin(a)*r*0.95, h+0.4), (0.9, 0.9, 0.8), "Pedra", rot=a, uvs=1.5)
    b.box((0, -r*0.98, 0.9), (1.2, 0.3, 1.8), "Porta", uvs=1.5)
    for z in (4.0, 7.0): b.box((0, -r*0.93, z), (0.3, 0.3, 1.0), "Janela", uvs=1.0)
    return b.finish()

def campfire_logs():
    b = B("Fogueira_Troncos")
    for i in range(6):
        a = i*math.tau/6; b.box((math.cos(a)*0.35, math.sin(a)*0.35, 0.12), (0.8, 0.14, 0.14), "Madeira", rot=a+0.3, uvs=1.0)
    return b.finish()

objs = [house_small(), house_big(), tavern(), well(), stall(), fence(), wall_stone(), tower(), campfire_logs()]
for o in objs:
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, o.name+".fbx"), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
        path_mode='STRIP', mesh_smooth_type='FACE', add_leaf_bones=False, mesh_smooth_type_unused=None) if False else bpy.ops.export_scene.fbx(
        filepath=os.path.join(out, o.name+".fbx"), use_selection=True, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL',
        path_mode='STRIP', mesh_smooth_type='FACE', add_leaf_bones=False)
    print("tris", o.name, sum(len(p.vertices)-2 for p in o.data.polygons), [m.name for m in o.data.materials])
