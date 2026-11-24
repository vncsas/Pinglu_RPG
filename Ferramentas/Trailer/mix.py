# Monta a trilha completa (música + efeitos sincronizados com o registro do Unity + narração) -> trilha.wav
import numpy as np, subprocess, re, os, wave, json
from lib import *
from falas import FALAS
C=os.path.expanduser('~/.cache/pinglu_audio'); LOGS=C+'/logs'; VOZ=C+'/voz'
VID='/home/amaro/Videos/Pinglu_trailer_partes/cortadas'
ORDEM=[('prologo','01_prologo'),('guardiao','02_guardiao'),('heroi0','03_heroi1_cavaleiro'),('heroi1','04_heroi2_morgana'),('heroi2','05_heroi3_tanque'),('heroi3','06_heroi4_curandeiro'),('cavaleiro','07_cavaleiro_caido'),('ladrao','08_ladrao'),('fera','09_fera_do_pantano'),('dragao','10_dragao'),('final','11_final')]
def dur(f): return float(subprocess.check_output(['ffprobe','-v','error','-show_entries','format=duration','-of','default=nw=1:nk=1',f]).decode())
P={};D={};t=0.0
for k,n in ORDEM:
    d=dur('%s/%s.mp4'%(VID,n)); P[k]=t; D[k]=d; t+=d
TOTAL=t; print('total',TOTAL,P)
EV={}
for k,n in ORDEM:
    EV[k]=[]
    try:
        for l in open('%s/unity_%s.log'%(LOGS,n),errors='ignore'):
            m=re.match(r'\[SOM\] ([\d.]+) (\S+) ?(.*)',l)
            if m: EV[k].append((float(m.group(1)),m.group(2),m.group(3).strip()))
    except FileNotFoundError: pass
def evs(part,tipo): return [e for e in EV[part] if e[1]==tipo]
OFF={}
for k,_ in ORDEM:
    p=evs(k,'parte'); OFF[k]=max(0.0,p[0][0]-0.35) if (p and k not in ('prologo','final')) else 0.0
M=Mix(TOTAL+3); SFX=Mix(TOTAL+3); V=Mix(TOTAL+3)
def mus(sig,at,g=1.0,pan=0.0): M.add(sig,at,g,pan)
def sfx(sig,at,g=1.0,pan=0.0): SFX.add(sig,at,g,pan)
def pad(at,notes,dur,g=0.8,kind='strings',vowel='a'):
    for i,n in enumerate(notes):
        f=mid(n)
        if kind=='strings': mus(strings(f,dur),at,g,-0.5+i*0.5)
        elif kind=='choir': mus(choir(f,dur,1.0,vowel),at,g,-0.5+i*0.5)
        elif kind=='horn': mus(horn(f,dur),at,g,0.0)
def ost(t0,t1,bpm,roots,g=0.6,sub=2,vel=1.0):
    step=60.0/bpm/sub; bar=60.0/bpm*4; k=0; tt=t0
    while tt<t1:
        r=roots[int(((tt-t0)//bar))%len(roots)]; n=[r+12,r+19,r+12,r+24][k%4]
        mus(strings(mid(n),0.12,vel),tt,g*(1.0 if k%4==0 else 0.7),-0.3+0.2*(k%4)); k+=1; tt+=step
def tempo_timp(t0,t1,bpm,notes,g=0.8,pat=(1,0,1,0)):
    beat=60.0/bpm; k=0; tt=t0
    while tt<t1:
        if pat[k%len(pat)]: mus(timp(mid(notes[(k//4)%len(notes)]),1.0),tt,g*(1.0 if k%4==0 else 0.6))
        k+=1; tt+=beat
def roll(t0,t1,f=73.4,g=0.5):
    tt=t0
    while tt<t1:
        x=(tt-t0)/(t1-t0); mus(timp(f,0.5,0.5),tt,g*(0.3+0.8*x)); tt+=0.11-0.06*x
def hits(t,g=1.0): mus(boom(1.4,2.2,95,30),t,g)
def riser(t,dur=2.2,g=0.6): mus(swell(dur,4500,True),t-dur,g)
def wind(t0,t1,g=0.1):
    n=int((t1-t0)*SR); x=lpn(n,500); m=0.5+0.5*np.sin(2*np.pi*np.arange(n)/SR/7.0+1); mus(x*m*g*3,t0,1.0)
def drone(t0,t1,root=33,g=0.5):
    n=int((t1-t0)*SR); tt=np.arange(n)/SR; s=sum(a*np.sin(2*np.pi*mid(root+iv)*tt*(1+d)) for iv,a,d in [(0,1,0),(0,.6,0.0035),(7,.5,0),(12,.35,0.002)])
    e=np.minimum(tt/3,1)*np.minimum((t1-t0-tt)/3,1); mus(s*e*0.18*g,t0,1.0)
THEME=[57,60,64,62,60,59,57]            # tema do jogo: lá dó mi ré dó si lá (lá menor, melancólico)
def tema(t0,inst='pluck',g=0.7,step=1.1,base=0):
    for i,n in enumerate(THEME):
        f=mid(n+base)
        if inst=='pluck': mus(pluck(f,2.4),t0+i*step,g)
        elif inst=='horn': mus(horn(f,step*1.05),t0+i*step,g)
        elif inst=='strings': mus(strings(f,step*1.1),t0+i*step,g)
        elif inst=='choir': mus(choir(f,step*1.1,1.0,'o'),t0+i*step,g)
# ---------------- PRÓLOGO (tempos medidos nas imagens)
drone(0,77.2,33,0.7); wind(0,72,0.09)
tema(1.0,'pluck',0.8,1.15); mus(choir(mid(45),7,1.0,'o'),2,0.7)
pad(8,[45,52,57,60],7,0.7)
mus(horn(mid(33),8,1.0),15,0.9); roll(16,19.2,73.4,0.6)
for a in (19.5,21.5,23.5,25.5): mus(timp(mid(33),1.0),a,0.9)
pad(19,[45,52,57],7,0.7)
for i,a in enumerate(np.arange(27.0,38.5,0.55)): mus(bell(mid([81,84,88,86,84,83][i%6]),3.0),a,0.5,-0.6+0.25*(i%6))
pad(27,[45,52,57,64],6,0.7,'choir','a'); pad(33,[41,48,53,57],6,0.7,'choir','a')
pad(38.5,[45,46,52],11,0.8)
for a in np.arange(41,49,0.12): mus(strings(mid(45 if int(a*3)%2 else 46),0.1),a,0.28)
for a in np.arange(40,49,2.0): mus(boom(0.7,0.4,150,55),a,0.5); mus(boom(0.6,0.4,150,55),a+0.3,0.3)
riser(41.5,3.0,0.7)
pad(49.5,[38,45,50],10,0.7,'choir','u'); wind(49,60,0.14)
for a in (57,58.2,59.5): mus(swell(1.2,1800,True,0.5),a,0.3)
tema(61.5,'horn',0.8,1.05); pad(61,[45,52,57,60],11,0.75)
for a in np.arange(61.0,72.0,1.5): mus(timp(mid(45),1.0),a,0.7)
for a in np.arange(66,72,0.18): mus(snare(0.5),a,0.25+0.4*(a-66)/6)
pad(72.3,[45,52,57,64],5,0.9,'horn'); pad(72.3,[45,52,57],5,0.8,'strings'); roll(72.3,77.1,73.4,0.8); hits(72.2,0.9)
def rugido(vol=1.0,dur=2.4):
    n=int(dur*SR); t=np.arange(n)/SR; f=70+30*np.sin(2*np.pi*3.2*t)+40*np.exp(-t*1.2)
    ph=2*np.pi*np.cumsum(f)/SR; s=np.zeros(n)
    for h in range(1,18): s+=np.sin(h*ph)/h**0.8*np.exp(-((h*f.mean()-900)/700)**2*0.3)
    g=lpn(n,2800)*0.6; e=np.sin(np.pi*np.minimum(t/dur,1))**0.7*np.exp(-t*0.35)
    return (s*0.08+g*0.35)*e*vol
sfx(rugido(1.0,2.6),20.3,0.9,-0.2); sfx(rugido(0.8,2.2),24.0,0.8,0.3)
for a in (21,22.5,25): sfx(sweep_noise(1.2,300,900,0.7,0.8)+swell(1.4,2500,False,0.5)[:int(1.2*SR)],a,0.4)
for a in (33.5,46.0,52.5):
    n=int(2.2*SR); tt=np.arange(n)/SR; sfx(lpn(n,160)*np.exp(-tt*1.2)*3.0,a,0.8)
# ---------------- HERÓIS
roots_h=[45,41,43,45]
for k,cls_ in [('heroi0','Cavaleiro'),('heroi1','Maga'),('heroi2','Tanque'),('heroi3','Curandeiro')]:
    s0=P[k]; s1=s0+D[k]; bpm=72; bar=60/bpm*4
    riser(s0+0.1,2.0,0.5); hits(s0,0.8); drone(s0,s1,33,0.6)
    if k=='heroi3':
        pad(s0+0.5,[57,64,69],s1-s0-1,0.8,'choir','a'); tempo_timp(s0+1,s1-1,bpm,[33,28],0.35,(1,0,0,0))
        for i,a in enumerate(np.arange(s0+1,s1-1,0.6)): mus(bell(mid([81,84,88,91][i%4]),2.0),a,0.35,-0.6+0.4*(i%4))
        ost(s0+1,s1-0.5,bpm,roots_h,0.35,2)
    else:
        ost(s0+0.6,s1-0.4,bpm,roots_h,0.7 if k!='heroi2' else 0.6,2)
        tempo_timp(s0+0.6,s1-0.4,bpm,[33,29,31,33],1.0 if k=='heroi2' else 0.8,(1,0,1,1) if k=='heroi2' else (1,0,1,0))
        for b in range(int((s1-s0)/bar)): pad(s0+0.6+b*bar,[roots_h[b%4]+12,roots_h[b%4]+19],bar+0.4,0.55,'horn')
        if k=='heroi0': tema(s0+1.5,'horn',0.55,1.6)
        if k=='heroi1':
            for i,a in enumerate(np.arange(s0+1.5,s1-1,0.35)): mus(pluck(mid(roots_h[int((a-s0)//bar)%4]+36+[0,3,7,10,12,10,7,3][i%8]),1.2),a,0.4,-0.7+0.2*(i%8))
        pad(s0+0.6,[57,64,69],s1-s0-1,0.5,'choir','o')
# ---------------- CHEFES
def chefe(k,bpm=64,roots=(38,34,31,33),dragon=False):
    s0=P[k]; s1=s0+D[k]; bar=60/bpm*4; beat=60/bpm
    drone(s0,s1,26,0.9); riser(s0+0.1,2.0,0.6); hits(s0,1.0)
    nm=evs(k,'nome'); n0=(nm[0][0]-OFF[k]+s0) if nm else s0+1.0
    riser(n0,2.4,0.8); mus(boom(1.8,2.8,90,28),n0,1.3); pad(n0,[50,57,62],5,0.9,'choir','a'); mus(metal(300,2.5,1.0),n0,0.4)
    f2=evs(k,'fase2'); t2=(f2[0][0]-OFF[k]+s0) if f2 else None
    for b in range(int((s1-s0)/bar)+1):
        a=s0+1.0+b*bar
        if a>=s1-0.8: break
        r=roots[b%len(roots)]; late=t2 is not None and a>=t2
        pad(a,[r+12,r+19,r+24],bar+0.6,0.55 if not late else 0.8,'choir','o' if not late else 'a')
        pad(a,[r,r+12],bar+0.6,0.7,'horn')
    ost(s0+2.0,s1-0.5,bpm,list(roots),0.55,2)
    if t2:
        ost(t2,s1-0.5,bpm*2,list(roots),0.5,2); riser(t2,1.6,0.8); mus(boom(1.8,3.0,80,26),t2,1.4); mus(metal(220,3,1.0),t2,0.5)
    tt=s0+1.0; j=0
    while tt<s1-0.6:
        late=t2 is not None and tt>=t2
        if j%4==0: mus(boom(1.2,1.1,115,35),tt,0.95)
        elif j%4==2: mus(boom(0.9,0.7,100,40),tt,0.7)
        if late and j%2==1: mus(boom(0.7,0.5,110,45),tt,0.55)
        if j%4 in (1,3): mus(snare(0.6),tt,0.3 if not late else 0.45)
        j+=1; tt+=beat
    for i,a in enumerate(np.arange(s0+1.0,s1-1,bar*2)): mus(metal(150+30*(i%3),2.0,1.0),a,0.12)
chefe('guardiao'); chefe('cavaleiro',68,(38,34,36,33)); chefe('ladrao',68,(40,36,33,35)); chefe('fera',62,(36,32,29,31)); chefe('dragao',80,(38,34,31,33),True)
# ---------------- FINAL
s0=P['final']; drone(s0,s0+D['final']-1,33,0.8)
tema(s0+0.6,'strings',0.8,1.5); tema(s0+0.6,'horn',0.6,1.5); pad(s0+0.6,[45,52,57,60],9,0.8,'choir','a')
mus(bell(mid(57),9),s0+1.4,1.2); mus(boom(1.8,3,80,28),s0+1.4,1.0)
pad(s0+9.5,[45,52,57,61],5,0.7,'strings'); mus(bell(mid(69),9),s0+5.2,0.6)
# ---------------- EFEITOS sincronizados com o que acontece na tela
def dedupe(lst,gap):
    out=[];last=-9
    for e in lst:
        if e[0]-last>=gap: out.append(e); last=e[0]
    return out
def sfx_ev(part):
    s0=P[part]
    for t,tipo,arg in EV[part]:
        T=s0+t-OFF[part]
        if tipo=='atk':
            c=arg
            if c=='Cavaleiro': sfx(sweep_noise(0.22,900,4200,0.5,0.9),T,0.45,-0.3); sfx(metal(2300,0.35,0.5),T+0.08,0.25,-0.3)
            elif c=='Tanque': sfx(sweep_noise(0.3,300,1500,0.6,1.0),T,0.5,0.3); sfx(boom(0.5,0.3,160,70),T+0.12,0.4,0.3)
            elif c=='Maga':
                n=int(0.3*SR); tt=np.arange(n)/SR; sfx(np.sin(2*np.pi*np.cumsum(500+1400*tt/0.3)/SR)*np.exp(-tt*9)*0.5,T,0.35,0.4); sfx(bpn(n,4000,9000)*np.exp(-tt*14),T,0.2,0.4)
            else: sfx(sweep_noise(0.25,400,1800,0.6,0.8),T,0.35,0.0)
        elif tipo=='hit':
            n=int(0.2*SR); tt=np.arange(n)/SR; sfx(bpn(n,2000,6000)*np.exp(-tt*35)*1.0+boom(0.6,0.2,140,60)[:n],T,0.55,rng.uniform(-.4,.4))
            for j in range(3): sfx(metal(rng.uniform(1500,2500),0.12,0.35),T+0.02*j,0.15)
        elif tipo=='skill':
            c,i=arg.split(); i=int(i)
            if c=='Cavaleiro':
                if i==0:
                    for j in range(4): sfx(sweep_noise(0.3,700,4000,0.5,0.9),T+j*0.14,0.5,-0.5+0.33*j)
                elif i==1: sfx(sweep_noise(0.35,500,5000,0.5,1.0),T,0.6); sfx(metal(1700,0.8,1.0),T+0.2,0.45); sfx(boom(1.0,0.6,150,50),T+0.2,0.7)
                else: sfx(swell(0.6,5000,True,0.8),T-0.5,0.5); sfx(boom(1.3,1.4,120,38),T+0.1,0.9); sfx(lpn(int(1.2*SR),1500)*np.exp(-np.arange(int(1.2*SR))/SR*3),T+0.1,0.5)
            elif c=='Tanque':
                if i==0: sfx(metal(450,1.0,1.0),T,0.8); sfx(boom(1.0,0.6,150,55),T,0.7)
                elif i==1: sfx(boom(1.5,1.6,110,32),T,1.0); sfx(lpn(int(1.5*SR),900)*np.exp(-np.arange(int(1.5*SR))/SR*3)*1.2,T,0.6)
                else: sfx(rugido(0.5,1.8)*0.9,T-0.2,0.6); sfx(boom(1.2,1.2,100,36),T+0.3,0.8)
            elif c=='Maga':
                if i==0: sfx(sweep_noise(0.9,200,2500,0.9,1.0),T-0.3,0.6); sfx(boom(1.2,1.2,120,40),T+0.5,0.9)
                elif i==1:
                    n=int(1.6*SR); tt=np.arange(n)/SR; sfx(np.where(tt<0.05,lpn(n,9000),0)*1.5+lpn(n,200)*np.exp(-tt*3)*2.0,T,0.9)
                    sfx(bpn(int(0.3*SR),2000,12000)*np.exp(-np.arange(int(0.3*SR))/SR*10),T,0.8)
                else:
                    n=int(1.2*SR); tt=np.arange(n)/SR; sfx(np.sin(2*np.pi*np.cumsum(300+1800*(tt/1.2)**2)/SR)*np.sin(np.pi*tt/1.2)*0.6,T-0.4,0.5); sfx(boom(1.4,1.6,110,32),T+0.8,1.0); sfx(bell(mid(96),2.0),T+0.8,0.4)
            else:
                for j,nn in enumerate([76,79,83] if i!=2 else [72,76,79,84]): sfx(bell(mid(nn),2.5),T+j*0.16,0.5,-0.4+0.3*j)
                sfx(swell(1.0,3000,True,0.5),T-0.2,0.35)
                if i==2: sfx(choir(mid(60),2.5,1.0,'a'),T,0.5)
        elif tipo=='raio':
            n=int(2.8*SR); tt=np.arange(n)/SR; sfx(bpn(n,1500,9000)*np.exp(-tt*30)*1.5+lpn(n,220)*np.exp(-tt*1.4)*2.8,T+0.05,0.9,rng.uniform(-.7,.7))
    if part in ('guardiao','cavaleiro','ladrao','fera','dragao') and not evs(part,'bosshit'):
        for t,tipo,arg in EV[part]:
            if tipo=='atk' and arg in ('Cavaleiro','Tanque'): sfx(boom(0.8,0.4,140,60),s0+t-OFF[part]+0.3,0.4,rng.uniform(-.4,.4))
    for t,tipo,arg in dedupe(evs(part,'hurt'),0.45):
        T=s0+t-OFF[part]
        if arg=='guardiao': sfx(boom(1.4,1.3,100,32),T,0.9); sfx(lpn(int(0.9*SR),1800)*np.exp(-np.arange(int(0.9*SR))/SR*5)*1.4,T,0.7)
        elif arg in ('cavaleiro','ladrao'): sfx(metal(rng.uniform(500,900),0.9,1.0),T,0.7); sfx(boom(0.9,0.4,160,60),T,0.55)
        elif arg=='fera': sfx(lpn(int(1.0*SR),1500)*np.exp(-np.arange(int(1.0*SR))/SR*3)*1.5,T,0.7); sfx(boom(1.0,0.8,100,36),T,0.7)
        else: sfx(boom(1.0,0.5,130,45),T,0.6)
    for t,tipo,arg in dedupe(evs(part,'bosshit'),0.35):
        T=s0+t-OFF[part]; sfx(boom(0.8,0.4,140,60),T,0.45,rng.uniform(-.4,.4)); sfx(bpn(int(0.1*SR),1000,5000)*np.exp(-np.arange(int(0.1*SR))/SR*40),T,0.3)
    for t,tipo,arg in evs(part,'fase2'):
        T=s0+t-OFF[part]
        if part=='guardiao': sfx(rugido(1.2,3.5),T,1.0); sfx(lpn(int(3*SR),400)*np.exp(-np.arange(int(3*SR))/SR*1.0)*2.5,T,0.9); sfx(sweep_noise(2.0,200,2000,0.6,1.0),T,0.6)
        else: sfx(rugido(1.4,3.5),T,1.0)
    for t,tipo,arg in evs(part,'nome'):
        T=s0+t-OFF[part]
        if part=='guardiao': sfx(rugido(1.2,3.0),T,0.7)
        if part=='fera': sfx(rugido(1.2,2.8),T+0.1,0.8)
        if part=='dragao': sfx(rugido(1.5,3.4),T,1.0)
        if part in ('cavaleiro','ladrao'): sfx(metal(400,2.0,1.2),T,0.6)
for k,_ in ORDEM:
    if k not in ('prologo','final'): sfx_ev(k)
wind(P['guardiao'],P['guardiao']+D['guardiao'],0.13)
for k in ('cavaleiro','ladrao'): wind(P[k],P[k]+D[k],0.16)
s0=P['fera']
for a in np.arange(s0,s0+D['fera'],0.9):
    n=int(0.4*SR); sfx(lpn(n,700)*np.sin(np.pi*np.arange(n)/n)*0.5,a+rng.uniform(0,0.4),0.25,rng.uniform(-.8,.8))
wind(s0,s0+D['fera'],0.12); wind(P['dragao'],P['dragao']+D['dragao'],0.2)
# ---------------- VOZ (a música abaixa quando o velho fala)
duck=np.ones(M.N,dtype=np.float32)
for i,(part,ref,off,txt) in enumerate(FALAS):
    w=wave.open('%s/%02d.wav'%(VOZ,i)); x=np.frombuffer(w.readframes(w.getnframes()),dtype=np.int16).astype(np.float32)/32768; w.close()
    if ref=='abs': T=off
    elif ref=='ini': T=P[part]+off
    elif ref=='nome': e=evs(part,'nome'); T=(P[part]+e[0][0]-OFF[part]+off) if e else P[part]+1.2
    else: e=evs(part,'fase2'); T=(P[part]+e[0][0]-OFF[part]+off) if e else P[part]+16
    V.add(x,T,1.0,0.0)
    s=int(max(0,T-0.35)*SR); e2=min(M.N,int((T+len(x)/SR+0.6)*SR)); duck[s:e2]=np.minimum(duck[s:e2],0.30)
k=int(SR*0.35); duck=np.convolve(duck,np.ones(k,dtype=np.float32)/k,mode='same').astype(np.float32)
ir=reverb_ir(2.8)
def rv(L,R,wet): return L+wet*conv_blocks(L,ir),R+wet*conv_blocks(R,reverb_ir(2.8,9))
ML,MR=rv(M.L,M.R,0.45); SL,SR_=rv(SFX.L,SFX.R,0.22)
def norm(a,b,peak):
    m=max(np.abs(a).max(),np.abs(b).max())+1e-9; return a/m*peak,b/m*peak
ML,MR=norm(ML,MR,0.55); SL,SR_=norm(SL,SR_,0.8)
# versão sem narração, usada pela montagem "só história" (montar_historia.py)
_t=np.arange(len(ML))/SR; _f=np.minimum(1,np.maximum(0,(TOTAL+1-_t)/2.5))
_a=np.tanh((ML*0.8+SL*0.9)*1.1)/1.1*_f; _b=np.tanh((MR*0.8+SR_*0.9)*1.1)/1.1*_f; _g=0.9/max(np.abs(_a).max(),np.abs(_b).max())
_w=wave.open(C+'/sem_voz.wav','wb'); _w.setnchannels(2); _w.setsampwidth(2); _w.setframerate(SR); _w.writeframes((np.stack([_a,_b],1)*_g*32767).astype(np.int16)[:int(TOTAL*SR)].tobytes()); _w.close()
L=ML*duck+SL*(0.55+0.45*duck)+V.L; R=MR*duck+SR_*(0.55+0.45*duck)+V.R
L=np.tanh(L*1.1)/1.1; R=np.tanh(R*1.1)/1.1
t=np.arange(len(L))/SR; f=np.minimum(np.minimum(t/1.5,1),np.maximum(0,(TOTAL+1-t)/2.5)); L*=f; R*=f
pk=max(np.abs(L).max(),np.abs(R).max()); g=0.92/pk
st=(np.stack([L,R],1)*g*32767).astype(np.int16)[:int(TOTAL*SR)]
w=wave.open(C+'/trilha.wav','wb'); w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes(st.tobytes()); w.close()
json.dump({'P':P,'D':D,'total':TOTAL},open(C+'/tempos.json','w')); print('ok',TOTAL,pk)
