# Finger joints from the glove's phalanx plates: every finger segment carries one small metal plate
# (3 per finger), so each plate gives a phalanx axis. Plates are chained by touching ends; the joint
# line is pushed from the plate (back of the finger) to the finger's centre using the glove mesh.
# Writes fingers.json  {side: {finger: [MCP, PIP, DIP, tip]}}
# usage: blender -b norm.blend -P fingers.py -- <scripts_dir> <out.json>
import bpy, sys, json, numpy as np
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0])
from skeleton import J

def islands(o):
    me = o.data; n = len(me.vertices); par = list(range(n))
    def f(i):
        while par[i] != i: par[i] = par[par[i]]; i = par[i]
        return i
    for e in me.edges:
        a, b = f(e.vertices[0]), f(e.vertices[1])
        if a != b: par[a] = b
    co = np.array([v.co[:] for v in me.vertices]); lab = np.array([f(i) for i in range(n)])
    return [co[lab == k] for k in np.unique(lab)]
plates = []
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name.startswith("Glove_MainPlate"):
        for pts in [q for q in islands(o) if len(q) >= 8]:
            c = pts.mean(0); ev, evec = np.linalg.eigh(np.cov((pts-c).T)); ax = evec[:, 2]
            t = (pts-c) @ ax
            plates.append(dict(n=len(pts), c=c, ax=ax, nrm=evec[:, 0], a=c+ax*t.min(), b=c+ax*t.max(), pts=pts))
print("plates:", len(plates), sorted(p["n"] for p in plates))

res = {}
for side, sx, glove in (("Left", 1, "Gloves_Gloves_low1"), ("Right", -1, "Gloves_Gloves_low2")):
    gco = np.array([v.co[:] for v in bpy.data.objects[glove].data.vertices])
    wrist = np.array(J[side+"Hand"][0])
    P = [p for p in plates if p["c"][0]*sx > 0 and p["n"] <= 110]
    # chain plates: greedy links between nearest plate ends (each plate max 2 links, no cycles);
    # thumb plates (bigger) only link among themselves
    back = np.mean([p["c"] for p in plates if p["c"][0]*sx > 0 and p["n"] > 110], axis=0)   # back-of-hand plates
    def endd(p, q): return min(np.linalg.norm(x-y) for x in (p["a"], p["b"]) for y in (q["a"], q["b"]))
    pairs = sorted((endd(P[i], P[j]), i, j) for i in range(len(P)) for j in range(i+1, len(P))
                   if (P[i]["n"] > 60) == (P[j]["n"] > 60))
    deg = [0]*len(P); comp = list(range(len(P))); links = {i: [] for i in range(len(P))}
    def f(i):
        while comp[i] != i: i = comp[i]
        return i
    for dist, i, j in pairs:
        lim = 0.03 if P[i]["n"] > 60 else 0.02
        if dist < lim and deg[i] < 2 and deg[j] < 2 and f(i) != f(j):
            deg[i] += 1; deg[j] += 1; comp[f(i)] = f(j); links[i].append(j); links[j].append(i)
    chains = []
    for root in {f(i) for i in range(len(P))}:
        mem = [i for i in range(len(P)) if f(i) == root]
        ends = [i for i in mem if deg[i] <= 1]
        s0 = min(ends, key=lambda i: np.linalg.norm(P[i]["c"]-back))     # proximal = nearest the back plate
        ch = [s0]
        while len(ch) < len(mem): ch.append([j for j in links[ch[-1]] if j not in ch][0])
        chains.append(ch)
    # orient each plate along the chain (a = proximal end)
    for ch in chains:
        for k, i in enumerate(ch):
            p = P[i]
            ref = P[ch[k+1]]["c"] if k+1 < len(ch) else None
            if ref is not None:
                if np.linalg.norm(p["a"]-ref) < np.linalg.norm(p["b"]-ref): p["a"], p["b"] = p["b"], p["a"]
            else:
                prv = P[ch[k-1]]["c"] if k > 0 else back
                if np.linalg.norm(p["b"]-prv) < np.linalg.norm(p["a"]-prv): p["a"], p["b"] = p["b"], p["a"]
    print(side, "chains:", [[P[i]["n"] for i in ch] for ch in chains])
    def centre_offset(p):
        # finger centre: glove verts close to the plate's axis segment, averaged
        a, b = p["a"], p["b"]; ab = b-a; t = np.clip((gco-a) @ ab/ab.dot(ab), 0, 1)
        dd = np.linalg.norm(gco-(a+np.outer(t, ab)), axis=1); m = dd < 0.014
        return (gco[m].mean(0)-p["c"]) if m.sum() > 5 else np.zeros(3)
    fingers, palm = [], []
    for ch in chains:
        ps = [P[i] for i in ch]; off = [centre_offset(p) for p in ps]
        pts = [ps[0]["a"]+off[0]]
        for k in range(len(ps)-1):
            pts.append((ps[k]["b"]+ps[k+1]["a"])/2+(off[k]+off[k+1])/2)
        pts.append(ps[-1]["b"]+off[-1])
        fingers.append(np.array(pts))
        # palm side of each phalanx: from the plate (on the back of the finger) toward the finger centre
        palm.append([p["nrm"] for p in ps])
    order = [i for i, ch in enumerate(chains) if P[ch[0]]["n"] > 90][:1]
    rest = [i for i, ch in enumerate(chains) if P[ch[0]]["n"] <= 90 and len(ch) == 3]
    rest.sort(key=lambda i: np.linalg.norm(fingers[i][0]-fingers[order[0]][0]))     # index = nearest the thumb,
    rest = rest[:1]+sorted(rest[1:], key=lambda i: np.linalg.norm(fingers[i][0]-fingers[rest[0]][0]))   # then outward
    out = {nm: fingers[i].tolist() for nm, i in zip(["Thumb", "Index", "Middle", "Ring", "Little"], order+rest)}
    # palm side of each phalanx = its plate normal, with the sign made consistent:
    # fingers: toward the side the finger curls to; nearly straight: first phalanx toward the palm
    # (back-of-hand plate -> hand centre), the next ones follow the previous;
    # thumb: toward the middle finger's knuckle
    back = np.mean([p["c"] for p in plates if p["c"][0]*sx > 0 and p["n"] > 110], axis=0)
    centre = np.mean([wrist]+[fingers[i][0] for i in rest], axis=0); palm_dir = centre-back
    out["palmar"] = {}
    for nm, i in zip(["Thumb", "Index", "Middle", "Ring", "Little"], order+rest):
        vs = []
        for k, n in enumerate(palm[i]):
            if nm == "Thumb":
                seg = fingers[i][k+1]-fingers[i][k]; seg /= np.linalg.norm(seg)
                ref = fingers[rest[1]][0]-fingers[i][k]; ref = ref-seg*(ref @ seg)
            else:
                ref = palm_dir if k == 0 else vs[-1]
                pts = fingers[i]
                if k < 2:   # a bent finger curls toward the palm: the next phalanx tells the side
                    d0 = pts[k+1]-pts[k]; d1 = pts[k+2]-pts[k+1]
                    bend = d1/np.linalg.norm(d1)-d0/np.linalg.norm(d0)
                    if np.linalg.norm(bend) > 0.25: ref = bend
            vs.append(n if n @ ref >= 0 else -n)
        out["palmar"][nm] = [v.tolist() for v in vs]
    for k, v in out.items():
        if k == "palmar": continue
        v = np.array(v); print(side, k, "len %.3f" % np.linalg.norm(np.diff(v, axis=0), axis=1).sum(), np.round(v, 3).tolist())
    res[side] = out
json.dump(res, open(args[1], "w"), indent=1)
