# Left-hand finger joints on the fused sculpt (norm.blend, model pose): geodesic distance over the hand
# surface from the wrist, fingertips = distance maxima, where neighbouring fingers part = the webs,
# each finger's centre line = centroids of its geodesic rings (smoothed). Joints are placed along the
# centre line (proximal 45% / intermediate 30% / distal 25%; thumb 40/33/27).
# Writes fingers.json {"Left": {finger: [MCP, PIP, DIP, tip], "palmar": {finger: [3 vectors]}}}
# usage: blender -b norm.blend -P fingers.py -- <scripts_dir> <out.json>
import bpy, sys, json, heapq, numpy as np
args = sys.argv[sys.argv.index("--")+1:]; sys.path.insert(0, args[0])
from skeleton import J
o = bpy.data.objects["Maga"]; me = o.data
co = np.empty(len(me.vertices)*3); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
elbow, wrist = np.array(J["LeftLowerArm"][0]), np.array(J["LeftLowerArm"][1])
d = (wrist-elbow)/np.linalg.norm(wrist-elbow)
along = (co-wrist) @ d
region = np.nonzero((np.linalg.norm(co-wrist, axis=1) < 0.16) & (along > -0.01))[0]
idx = {int(v): k for k, v in enumerate(region)}
E = np.empty(len(me.edges)*2, np.int64); me.edges.foreach_get("vertices", E); E = E.reshape(-1, 2)
adj = [[] for _ in region]
for a, b in E:
    if a in idx and b in idx:
        w = float(np.linalg.norm(co[a]-co[b])); adj[idx[a]].append((idx[b], w)); adj[idx[b]].append((idx[a], w))
P = co[region]; n = len(P)
# skin vs cloth from the base colour texture (pale skin, dark coat): only skin counts as the hand
img = [im for im in bpy.data.images if "basecolor" in im.name.lower()][0]
W_, H_ = img.size; px = np.empty(W_*H_*4, np.float32); img.pixels.foreach_get(px); px = px.reshape(H_, W_, 4)
uv = me.uv_layers.active.data; vuv = np.zeros((len(me.vertices), 2))
lv = np.empty(len(me.loops), np.int64); me.loops.foreach_get("vertex_index", lv)
luv = np.empty(len(me.loops)*2); uv.foreach_get("uv", luv); vuv[lv] = luv.reshape(-1, 2)
q = vuv[region]; col = px[np.clip((q[:, 1]*H_).astype(int), 0, H_-1), np.clip((q[:, 0]*W_).astype(int), 0, W_-1), :3]
lum = col @ np.array([0.3, 0.59, 0.11]); skin = lum > float(np.percentile(lum, 60))*0.0+0.35
print("skin share in the region: %.2f (lum p10 %.2f p50 %.2f p90 %.2f)" % (skin.mean(), *np.percentile(lum, [10, 50, 90])))
# the hand: connected part of the region holding the vertex farthest along the forearm (a fingertip)
start = int(np.argmax(np.where(skin, along[region], -9))); comp = {start}; st = [start]
while st:
    k = st.pop()
    for j, _ in adj[k]:
        if j not in comp and skin[j]: comp.add(j); st.append(j)
hand = np.array(sorted(k for k in comp if skin[k])); inhand = np.zeros(n, bool); inhand[hand] = True
dist = np.full(n, np.inf); pq = []
for k in hand:                                    # sources: the wrist ring (just past the wrist plane)
    if (P[k]-wrist) @ d < 0.01: dist[k] = 0.0; heapq.heappush(pq, (0.0, k))
while pq:
    dd, k = heapq.heappop(pq)
    if dd > dist[k]: continue
    for j, w in adj[k]:
        if inhand[j] and dd+w < dist[j]: dist[j] = dd+w; heapq.heappush(pq, (dist[j], j))
ok = np.isfinite(dist)
print("hand verts", len(hand), "geodesic max", round(float(dist[ok].max()), 3))
def ball(k, r):
    seen = {k: 0.0}; pq = [(0.0, k)]
    while pq:
        dd, a = heapq.heappop(pq)
        for j, w in adj[a]:
            if inhand[j] and dd+w < r and dd+w < seen.get(j, 9): seen[j] = dd+w; heapq.heappush(pq, (dd+w, j))
    return seen
tips = []
for k in np.argsort(-np.where(ok, dist, -1)):
    if not ok[k] or dist[k] < 0.04: break
    if any(np.linalg.norm(P[k]-P[t]) < 0.018 for t in tips): continue
    if all(dist[j] <= dist[k] for j in ball(int(k), 0.02)): tips.append(int(k))
    if len(tips) == 5: break
print("tips", len(tips), [round(float(dist[t]), 3) for t in tips])
T = np.array([P[t] for t in tips])
far = [np.linalg.norm(T[i]-np.delete(T, i, 0).mean(0)) for i in range(len(T))]
thumb = int(np.argmax(far))                                         # the thumb tip stands apart from the others
fing = [i for i in range(len(T)) if i != thumb]
ring0 = np.array([k for k in hand if (P[k]-wrist) @ d < 0.015])     # skin just past the wrist plane
wr = P[ring0].mean(0) if len(ring0) > 5 else wrist
hand_dir = T[fing].mean(0)-wr; hand_dir /= np.linalg.norm(hand_dir)
fing.sort(key=lambda i: np.linalg.norm(T[i]-T[thumb]))             # index nearest the thumb ... little farthest
names = ["Thumb", "Index", "Middle", "Ring", "Little"]
out = {}
for nm, i in zip(names, [thumb]+fing):
    tip = T[i]
    if nm == "Thumb":
        base = wr+(tip-wr)*0.22; fr = (0.0, 0.42, 0.72, 1.0)       # CMC low in the palm near the wrist
    else:
        base = wr+(tip-wr)*0.52; fr = (0.0, 0.45, 0.75, 1.0)       # knuckle row ~ half way wrist -> tip
    out[nm] = [(base+(tip-base)*f).tolist() for f in fr]
    print(nm, "len %.3f" % np.linalg.norm(tip-base), np.round(out[nm], 3).tolist())
out["wrist"] = wr.tolist()
# palm side: normal of the hand plane (PCA), signed toward where the fingers curl
H = P[hand]; c = H.mean(0); ev, evec = np.linalg.eigh(np.cov((H-c).T)); nrm = evec[:, 0]
bend = sum(np.array(out[f][3])-(np.array(out[f][0])+np.array(out[f][2]))/2 for f in names[1:])
bend = bend-d*(bend @ d)
if nrm @ bend < 0: nrm = -nrm
pal = {}
for nm in names:
    pts = np.array(out[nm]); vs = []
    for k in range(3):
        y = pts[k+1]-pts[k]; y /= np.linalg.norm(y); z = nrm-y*(nrm @ y); vs.append((z/np.linalg.norm(z)).tolist())
    pal[nm] = vs
out["palmar"] = pal
json.dump({"Left": out}, open(args[1], "w"), indent=1)
