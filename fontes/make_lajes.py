# (1) Tileable irregular FLAGSTONE floor texture (Voronoi slabs, dark grout, bluish stone) -> Color + NormalGL
# (2) Stepping-stone slabs (flat irregular rocks) as FBX for the stone trail, + swamp reeds and lily pads.
# usage: python make_lajes.py <out_dir>      (uses numpy + PIL; the meshes are written as simple .obj-like data by blender separately)
import numpy as np, sys, os
from PIL import Image
out = sys.argv[1]; os.makedirs(out + "/Lajes", exist_ok=True)
N = 1024; rng = np.random.default_rng(11)
pts = rng.random((34, 2)); pts[:, 0] *= 1; pts[:, 1] *= 1
yy, xx = np.mgrid[0:N, 0:N]/N
d = np.full((N, N), 9.0); d2 = np.full((N, N), 9.0); idx = np.zeros((N, N), int)
for k, (px, py) in enumerate(pts):
    dx = np.abs(xx-px); dx = np.minimum(dx, 1-dx); dy = np.abs(yy-py); dy = np.minimum(dy, 1-dy)
    # slightly stretched metric so the slabs look irregular
    dist = np.sqrt((dx*1.0)**2 + (dy*1.15)**2)
    m = dist < d; d2 = np.where(m, d, np.minimum(d2, dist)); idx = np.where(m, k, idx); d = np.where(m, dist, d)
edge = d2-d                                        # 0 on the borders between slabs
grout = np.clip(edge/0.010, 0, 1)**0.8             # 0 in the joint, 1 on the slab
bevel = np.clip(edge/0.05, 0, 1)
def noise(freq, seed):
    r = np.random.default_rng(seed); g = r.random((freq, freq))
    big = np.kron(g, np.ones((N//freq, N//freq)))
    from numpy.fft import fft2, ifft2
    k = np.exp(-((np.fft.fftfreq(N)[:, None])**2+(np.fft.fftfreq(N)[None, :])**2)*(N/freq*0.9)**2*0.5)
    return np.real(ifft2(fft2(big)*k))
n1 = noise(8, 1); n1 = (n1-n1.min())/(n1.max()-n1.min()); n2 = noise(64, 2); n2 = (n2-n2.min())/(n2.max()-n2.min()); n3 = noise(256, 3); n3 = (n3-n3.min())/(n3.max()-n3.min())
tone = rng.uniform(0.88, 1.06, 34)[idx]
base = np.stack([0.36, 0.39, 0.40], -1)[None, None, :]*np.ones((N, N, 3))
warm = np.stack([0.42, 0.39, 0.33], -1)[None, None, :]*np.ones((N, N, 3))
mix = (rng.random(34) < 0.10)[idx][..., None]
col = np.where(mix, warm, base)*tone[..., None]
col *= (0.80+0.10*n1[..., None]+0.16*n2[..., None]+0.22*n3[..., None])
moss = np.clip((0.55-edge/0.06)*np.clip(n2*1.4-0.2, 0, 1), 0, 1)[..., None]                # moss creeping from the joints
col = col*(1-moss*0.65)+np.stack([0.22, 0.30, 0.10], -1)[None, None, :]*moss*0.65
col = col*(0.35+0.65*grout[..., None]**1.5)                                                # dark joints
Image.fromarray((np.clip(col, 0, 1)*255).astype(np.uint8)).save(out+"/Lajes/Lajes_1K-JPG_Color.jpg", quality=92)
h = bevel*0.9+0.10*n2+0.05*n3+0.15*n1*bevel
gx = (np.roll(h, -1, 1)-np.roll(h, 1, 1))*18; gy = (np.roll(h, -1, 0)-np.roll(h, 1, 0))*18
nz = np.ones_like(h); l = np.sqrt(gx**2+gy**2+nz**2)
nm = np.stack([(-gx/l)*0.5+0.5, (gy/l)*0.5+0.5, (nz/l)*0.5+0.5], -1)
Image.fromarray((nm*255).astype(np.uint8)).save(out+"/Lajes/Lajes_1K-JPG_NormalGL.jpg", quality=92)
print("lajes ok")
