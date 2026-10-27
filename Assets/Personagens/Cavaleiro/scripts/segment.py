# Segmentation of fused Tripo mesh into body / sword / shield. Usable from Blender (pass verts, edges).
import numpy as np
from numpy.lib.stride_tricks import sliding_window_view as swv

def components(E, allowed):
    N=len(allowed)
    e=E[allowed[E[:,0]]&allowed[E[:,1]]]
    lab=np.arange(N)
    while True:
        m=np.minimum(lab[e[:,0]],lab[e[:,1]])
        new=lab.copy(); np.minimum.at(new,e[:,0],m); np.minimum.at(new,e[:,1],m)
        new=new[new]; new=new[new]
        if np.array_equal(new,lab): return lab
        lab=new

def seg_dist(p,a,b):
    ab=b-a; t=np.clip(((p-a)@ab)/(ab@ab),0,1); return np.linalg.norm(p-(a+np.outer(t,ab)),axis=1)

SWORD_P0=np.array([-0.554,-0.071,0.60]); SWORD_D=np.array([0.174,0.4,0.3]); SWORD_D/=np.linalg.norm(SWORD_D)
FOREARM=(np.array([0.41,0.11,1.12]),np.array([0.44,0.04,0.95]))
TOPLINE=np.array([(-0.30,1.80),(-0.05,1.77),(0.15,1.42),(0.55,1.56),(0.80,1.60)])  # shield top edge in (y,z)

def segment(v,E,nearest_fn,log=print):
    # ---- sword: radial profile around blade axis
    D=v-SWORD_P0; t=D@SWORD_D; r=np.linalg.norm(D-np.outer(t,SWORD_D),axis=1)
    SW=((t<0.345)&(r<0.2))|((t>=0.345)&(t<0.48)&(r<0.028))|((t>=0.48)&(t<0.75)&(r<0.05))
    lab=components(E,SW)
    tip=np.argmin(np.where(SW,t,9)); pom=np.argmax(np.where(SW,t,-9))
    sword=SW&((lab==lab[tip])|(lab==lab[pom])|((t>=0.345)&(t<0.48)))
    # ---- shield: outermost shell seen from +X
    cs=0.01; iy=((v[:,1]+0.6)/cs).astype(int); iz=(v[:,2]/cs).astype(int)
    H=np.full((iy.max()+1,iz.max()+1),-9.0); np.maximum.at(H,(iy,iz),v[:,0])
    P=np.pad(H,3,mode='edge'); Hs=np.median(swv(P,(7,7)),axis=(2,3))
    ztop=np.interp(v[:,1],TOPLINE[:,0],TOPLINE[:,1])
    below=v[:,2]<ztop+0.01
    farm=seg_dist(v,*FOREARM)
    base=(~sword)&below&(v[:,0]>0.2)&(farm>0.05)
    outer=base&(v[:,0]>Hs[iy,iz]-0.035)
    lab=components(E,outer); seed=np.argmax(np.where(outer,v[:,0],-9)); outer&=lab==lab[seed]
    # include back face / oblique parts: anything close to the outer shell
    cand=np.nonzero(base&~outer)[0]
    dist=nearest_fn(np.nonzero(outer)[0],cand)
    shield=outer.copy(); shield[cand[dist<0.03]]=True
    lab=components(E,shield); shield&=lab==lab[seed]
    # ---- cleanup: small body islands go to the part they touch
    body=~(sword|shield)
    lab=components(E,body); ids,cnt=np.unique(lab[body],return_counts=True)
    main=ids[np.argmax(cnt)]
    small=body&(lab!=main)
    # adjacency of small islands to sword/shield
    e=E; a,b=e[:,0],e[:,1]
    for part,name in ((shield,'shield'),(sword,'sword')):
        touch=np.unique(np.concatenate([lab[a[small[a]&part[b]]],lab[b[small[b]&part[a]]]]))
        mv=small&np.isin(lab,touch); part|=mv; small&=~mv
        log(f"moved {mv.sum()} island verts to {name}")
    # remaining loose islands (e.g. fragments floating near the shield): attach to a weapon if within 3 cm
    for i in np.unique(lab[small]):
        isl=np.nonzero(small&(lab==i))[0]
        ds=nearest_fn(np.nonzero(shield)[0],isl).min(); dw=nearest_fn(np.nonzero(sword)[0],isl).min()
        if min(ds,dw)<0.03:
            (shield if ds<=dw else sword)[isl]=True; small[isl]=False
            log(f"island {len(isl)} verts at {np.round(v[isl].mean(0),2)} -> {'shield' if ds<=dw else 'sword'}")
    log(f"sword {sword.sum()} shield {shield.sum()} remaining small body islands {small.sum()}")
    return sword,shield
