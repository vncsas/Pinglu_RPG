# PavingStones149 -> mix of shapes: each slab (connected light region) becomes either a ROUND/oval stone or a straight ROUNDED-SQUARE stone
# (shape intersected with the original slab, so the real stone detail is kept). Removed parts are filled with the joint/cobble colour.
import numpy as np, glob, sys, random
from PIL import Image
src = "C:/Users/Thiago/ambiente/dl/tex/PavingStones149/"; dst = sys.argv[1]
col = np.asarray(Image.open(glob.glob(src+"*_Color.jpg")[0]).convert("RGB")).astype(np.float32)/255
nor = np.asarray(Image.open(glob.glob(src+"*_NormalGL.jpg")[0]).convert("RGB")).astype(np.float32)/255
H, W = col.shape[:2]
def blur(a, s):
    fy = np.fft.fftfreq(H); fx = np.fft.fftfreq(W); k = np.exp(-2*(np.pi*s)**2*(fy[:, None]**2+fx[None, :]**2))
    if a.ndim == 2: return np.real(np.fft.ifft2(np.fft.fft2(a)*k))
    return np.stack([blur(a[..., c], s) for c in range(a.shape[2])], -1)
lum = col.mean(-1); sl = blur(lum, 3.0); m0 = sl > np.percentile(sl, 52)
# connected components (4-neighbour, periodic)
lab = -np.ones((H, W), np.int32); comps = []
ys, xs = np.nonzero(m0)
for y0, x0 in zip(ys, xs):
    if lab[y0, x0] >= 0: continue
    k = len(comps); st = [(y0, x0)]; lab[y0, x0] = k; pts = []
    while st:
        y, x = st.pop(); pts.append((y, x))
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            yy, xx = (y+dy) % H, (x+dx) % W
            if m0[yy, xx] and lab[yy, xx] < 0: lab[yy, xx] = k; st.append((yy, xx))
    comps.append(np.array(pts))
print("components", len(comps))
rnd = random.Random(4); yy, xx = np.mgrid[0:H, 0:W]
newm = np.zeros((H, W), bool)
def pdist(a, c, n): d = (a-c+n/2) % n-n/2; return d          # periodic signed distance
nround = nsq = 0
for k, pts in enumerate(comps):
    area = len(pts)
    if area < 350: newm[pts[:, 0], pts[:, 1]] = True; continue          # tiny cobbles stay as they are
    # circular mean centre
    cy = (np.angle(np.exp(2j*np.pi*pts[:, 0]/H).mean())/(2*np.pi)*H) % H; cx = (np.angle(np.exp(2j*np.pi*pts[:, 1]/W).mean())/(2*np.pi)*W) % W
    py = pdist(pts[:, 0].astype(float), cy, H); px = pdist(pts[:, 1].astype(float), cx, W)
    cov = np.cov(np.stack([px, py])); w, v = np.linalg.eigh(cov); ax = np.sqrt(np.maximum(w, 1e-3))*2.0           # semi-axes of the equivalent ellipse
    u, vv = pdist(xx.astype(float), cx, W), pdist(yy.astype(float), cy, H)
    box = (np.abs(u) < ax.max()*2.2) & (np.abs(vv) < ax.max()*2.2)
    comp_mask = np.zeros((H, W), bool); comp_mask[pts[:, 0], pts[:, 1]] = True
    kind = "round" if rnd.random() < 0.45 else "square"
    if kind == "round":
        a1, a2 = v[:, 1], v[:, 0]; s1, s2 = ax[1]*1.05, ax[0]*1.05
        q = ((u*a1[0]+vv*a1[1])/s1)**2+((u*a2[0]+vv*a2[1])/s2)**2
        shape = q < 1.0; nround += 1
    else:
        sx, sy = ax[1]*1.55*0.5*1.15, ax[0]*1.55*0.5*1.15                                  # axis-aligned half-sides (straight, not diamonds)
        sx, sy = np.sqrt(cov[0, 0])*1.75, np.sqrt(cov[1, 1])*1.75
        r = min(sx, sy)*0.38                                                                # corner radius
        qx = np.abs(u)-(sx-r); qy = np.abs(vv)-(sy-r)
        dd = np.sqrt(np.maximum(qx, 0)**2+np.maximum(qy, 0)**2)+np.minimum(np.maximum(qx, qy), 0)-r
        shape = dd < 0; nsq += 1
    newm |= (shape & comp_mask & box)
print("round", nround, "square", nsq)
mr = blur(newm.astype(np.float32), 1.0)
# fill the removed parts with REAL joint/cobble pixels taken from shifted copies of the texture (first shift whose source pixel is a joint)
rs = np.random.default_rng(3); joint_c = col.copy(); joint_n = nor.copy(); got = np.zeros((H, W), bool); jm = ~m0
jm_er = blur(jm.astype(np.float32), 2.5) > 0.9                              # joint pixels well inside a joint
for _ in range(40):
    dy, dx = int(rs.integers(0, H)), int(rs.integers(0, W))
    ok = np.roll(jm_er, (dy, dx), (0, 1)) & ~got
    if not ok.any(): continue
    joint_c[ok] = np.roll(col, (dy, dx), (0, 1))[ok]; joint_n[ok] = np.roll(nor, (dy, dx), (0, 1))[ok]; got |= ok
changed = np.clip((m0.astype(np.float32)-mr)*2.2, 0, 1)[..., None]                  # was slab, now joint
col2 = col*(1-changed)+joint_c*changed
nor2 = nor*(1-changed)+joint_n*changed
rim = np.clip(1-np.abs(mr-0.5)*2, 0, 1)[..., None]*0.14                              # darker rim = rounded bevel
col2 = np.clip(col2*(1-rim), 0, 1)
Image.fromarray((col2*255).astype(np.uint8)).save(dst+"PavingStones149_1K-JPG_Color.jpg", quality=93)
Image.fromarray((np.clip(nor2, 0, 1)*255).astype(np.uint8)).save(dst+"PavingStones149_1K-JPG_NormalGL.jpg", quality=93)
Image.fromarray((col2*255).astype(np.uint8)).resize((1000, int(1000*H/W))).save("C:/Users/Thiago/ambiente/sheet1.png")
