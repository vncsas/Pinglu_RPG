# Sintetizador do trailer: instrumentos (cordas, trompa, coro, harpa, sino, tambores) e efeitos, tudo em numpy.
import numpy as np
SR=44100
rng=np.random.default_rng(11)
def mid(m): return 440.0*2**((m-69)/12.0)
def env_adsr(n,a,r,sus=1.0):
    e=np.ones(n)*sus; na=min(n,int(a*SR))
    if na>0: e[:na]=np.linspace(0,sus,na)
    nr=min(n,int(r*SR))
    if nr>0: e[n-nr:]*=np.linspace(1,0,nr)
    return e
def lpn(n,cut,order=2):
    x=rng.standard_normal(n); X=np.fft.rfft(x); f=np.fft.rfftfreq(n,1/SR); X*=1/(1+(f/max(cut,1))**(2*order)); return np.fft.irfft(X,n)
def bpn(n,lo,hi):
    x=rng.standard_normal(n); X=np.fft.rfft(x); f=np.fft.rfftfreq(n,1/SR); X*=((f>lo)&(f<hi)).astype(float); return np.fft.irfft(X,n)
def sweep_noise(dur,f0,f1,q=0.35,gain=1.0):
    n=int(dur*SR); x=rng.standard_normal(n); out=np.zeros(n); blocks=24; bl=n//blocks+1
    for b in range(blocks):
        s=b*bl; e=min(n,s+bl)
        if s>=e: break
        fc=f0*(f1/f0)**(b/(blocks-1)); seg=x[s:e]; X=np.fft.rfft(seg); f=np.fft.rfftfreq(len(seg),1/SR)
        X*=np.exp(-((np.log2(np.maximum(f,1)/fc))/q)**2); out[s:e]=np.fft.irfft(X,len(seg))
    t=np.linspace(0,1,n); e=np.sin(np.pi*t)**1.5
    return out/(np.abs(out).max()+1e-9)*e*gain
def harm(f,n,nh,weights,vib=0.0,vibf=5.0):
    t=np.arange(n)/SR; ph=2*np.pi*f*t
    if vib: ph=ph+(vib*f/vibf)*np.sin(2*np.pi*vibf*t)
    s=np.zeros(n)
    for h in range(1,nh+1):
        if f*h>SR*0.45: break
        s+=weights(h,f*h)*np.sin(h*ph+h*1.3)
    return s
def strings(f,dur,vel=1.0):
    n=int((dur+0.8)*SR); s=np.zeros(n)
    for d in (-7,0,7):
        ff=f*2**(d/1200.0); s+=harm(ff,n,14,lambda h,fh:1.0/h**1.1/(1+(fh/3200)**2),vib=0.004)
    return s*env_adsr(n,0.35,0.7)*0.18*vel
def horn(f,dur,vel=1.0):
    n=int((dur+0.5)*SR); t=np.arange(n)/SR; base=env_adsr(n,min(0.5,dur*0.5),0.5); s=np.zeros(n)
    for h in range(1,15):
        if f*h>SR*0.45: break
        s+=(1.0/h**0.8)*np.sin(2*np.pi*f*h*t+h)*base**(0.4+0.45*h)
    return s*0.22*vel*base
def choir(f,dur,vel=1.0,vowel='a'):
    F={'a':[(800,90),(1150,90),(2800,150)],'o':[(450,80),(800,80),(2830,150)],'u':[(325,70),(700,80),(2700,150)]}[vowel]
    n=int((dur+1.0)*SR); s=np.zeros(n)
    for d in (-12,0,11):
        ff=f*2**(d/1200.0); s+=harm(ff,n,28,lambda h,fh:sum(np.exp(-((fh-fc)/bw)**2) for fc,bw in F)+0.04/h,vib=0.006,vibf=5.4)
    br=bpn(n,1500,5000)*0.04
    return (s*0.07+br*0.2)*env_adsr(n,0.8,0.9)*vel
def pluck(f,dur=2.2,vel=1.0):
    n=int(dur*SR); t=np.arange(n)/SR; s=np.zeros(n)
    for h in range(1,12):
        if f*h>SR*0.45: break
        s+=np.sin(2*np.pi*f*h*t+h)*np.exp(-t*(2.2+h*1.4))/h**0.9
    s[:200]*=np.linspace(0,1,200); return s*0.3*vel
def bell(f,dur=5.0,vel=1.0):
    n=int(dur*SR); t=np.arange(n)/SR; s=np.zeros(n)
    for r,a,d in [(0.5,0.5,0.5),(1,1,0.7),(1.19,0.6,0.9),(1.56,0.4,1.1),(2.0,0.5,1.3),(2.51,0.3,1.7),(3.01,0.25,2.0),(4.2,0.12,3.0)]:
        s+=a*np.sin(2*np.pi*f*r*t)*np.exp(-t*d)
    return s*0.2*vel
def boom(vol=1.0,dur=0.9,f0=130,f1=40,noise=0.5):
    n=int(SR*dur); t=np.arange(n)/SR; fr=f1+(f0-f1)*np.exp(-t*9); ph=2*np.pi*np.cumsum(fr)/SR
    return (np.sin(ph)*np.exp(-t*4.0)+noise*lpn(n,900)*np.exp(-t*20))*vol
def timp(f,vol=1.0,dur=1.4):
    n=int(SR*dur); t=np.arange(n)/SR; fr=f*(1+0.35*np.exp(-t*25)); ph=2*np.pi*np.cumsum(fr)/SR
    return (np.sin(ph)*np.exp(-t*3.2)+0.35*np.sin(2*ph)*np.exp(-t*6)+0.4*lpn(n,1200)*np.exp(-t*30))*vol
def snare(vol=1.0):
    n=int(SR*0.35); t=np.arange(n)/SR; return (bpn(n,1500,7500)*np.exp(-t*16)+0.4*np.sin(2*np.pi*190*t)*np.exp(-t*30))*vol*1.2
def swell(dur,cut=3000,rev=False,gain=1.0):
    n=int(SR*dur); x=lpn(n,cut); e=np.linspace(0,1,n)**2.2; return x*(e if rev else e[::-1])*gain
def metal(f=900,dur=0.5,vol=1.0):
    n=int(SR*dur); t=np.arange(n)/SR; s=np.zeros(n)
    for r,a in [(1,1),(1.47,.8),(2.09,.6),(2.56,.5),(3.3,.35),(4.1,.25)]: s+=a*np.sin(2*np.pi*f*r*t+rng.uniform(0,6))*np.exp(-t*(7+r*3))
    s+=bpn(n,3000,9000)*np.exp(-t*60)*0.8; return s*vol*0.35
class Mix:
    def __init__(s,dur):
        s.N=int(dur*SR); s.L=np.zeros(s.N,dtype=np.float32); s.R=np.zeros(s.N,dtype=np.float32)
    def add(s,sig,at,gain=1.0,pan=0.0):
        i=int(at*SR)
        if i>=s.N or i<-len(sig): return
        a=max(0,-i); sig=sig[a:]; i=max(i,0); x=sig[:s.N-i].astype(np.float32)
        gl=gain*np.cos((pan+1)*np.pi/4); gr=gain*np.sin((pan+1)*np.pi/4)
        s.L[i:i+len(x)]+=x*gl*1.414; s.R[i:i+len(x)]+=x*gr*1.414
def reverb_ir(sec=2.6,seed=3):
    r=np.random.default_rng(seed); n=int(sec*SR); t=np.arange(n)/SR
    ir=r.standard_normal(n)*np.exp(-t*2.4); X=np.fft.rfft(ir); f=np.fft.rfftfreq(n,1/SR); X*=1/(1+(f/3500)**2); ir=np.fft.irfft(X,n); ir[:int(0.02*SR)]*=np.linspace(0,1,int(0.02*SR))
    return (ir/np.abs(ir).max()).astype(np.float32)
def conv_blocks(x,ir,B=1<<19):
    n=len(x); out=np.zeros(n+len(ir),dtype=np.float32); M=len(ir); fft_n=1<<int(np.ceil(np.log2(B+M))); IR=np.fft.rfft(ir,fft_n)
    for s in range(0,n,B):
        seg=x[s:s+B]; y=np.fft.irfft(np.fft.rfft(seg,fft_n)*IR,fft_n)[:len(seg)+M-1]; out[s:s+len(y)]+=y.astype(np.float32)
    return out[:n]
