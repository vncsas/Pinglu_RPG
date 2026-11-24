# Trailer v5 (sem cabaré; narração nova; olho do dragão no final): prólogo curto (ilha, vila de perto, vila em chamas com a sombra, cristais, corrupção, aventureiros) + chefes em corte rápido (câmera em movimento) + final. Sem dragão.
import subprocess, os, wave, re, sys, numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import soundfile as sf
from lib import *
C=os.path.expanduser('~/.cache/pinglu_audio'); P=os.path.expanduser('~/Videos/Pinglu_trailer_partes'); T=C+'/v5'; os.makedirs(T,exist_ok=True)
FONT='/home/amaro/Downloads/Pinglu_RPG/Assets/Resources/Fontes/CinzelBold.ttf'
PRO=sys.argv[1] if len(sys.argv)>1 else P+'/v9_01_prologo.mp4'
BPM=140; BEAT=60/BPM
def run(*a): subprocess.run(['ffmpeg','-loglevel','error','-y']+list(a),check=True)
ENC=['-an','-c:v','libx264','-preset','fast','-crf','17','-pix_fmt','yuv420p','-r','60']
# ---------- acha os cartões do prólogo (intervalos escuros) para sincronizar tudo
run('-i',PRO,'-vf','fps=10,scale=64:36,signalstats,metadata=print:key=lavfi.signalstats.YAVG:file='+T+'/y.txt','-f','null','-')
t=None; v=[]
for l in open(T+'/y.txt'):
    m=re.search(r'pts_time:([\d.]+)',l); t=float(m.group(1)) if m else t
    m=re.search(r'YAVG=([\d.]+)',l)
    if m: v.append((t,float(m.group(1))))
cur=None; esc=[]
for t,y in v:
    if y<20:
        if cur is None: cur=t
    else:
        if cur is not None and t-cur>1.2: esc.append((cur,t))
        cur=None
print('intervalos escuros:',esc)
assert len(esc)>=6, 'não achei os 6 cartões'
CARD=[a for a,b in esc[:6]]; CARD[0]=max(CARD[0],esc[0][1]-2.6)      # o 1º intervalo inclui o preto do começo
A0=CARD[0]-0.3; FIMPRO=esc[5][1]+9.3                                  # prólogo cortado: do 1º cartão até o fim da cena dos aventureiros
CT=[c-A0 for c in CARD]; DPRO=FIMPRO-A0
print('cartões (s no prólogo cortado):',[round(c,1) for c in CT],'duração',round(DPRO,1))
run('-ss','%.3f'%A0,'-i',PRO,'-t','%.3f'%DPRO,*ENC,T+'/pro.mp4')
# ---------- cartão de texto
def cartao(txt,out,tam=68):
    im=Image.new('RGB',(1920,1080),(4,4,6)); d=ImageDraw.Draw(im); f=ImageFont.truetype(FONT,tam); w=d.textlength(txt,font=f)
    d.text(((1920-w)/2+3,490+3),txt,font=f,fill=(0,0,0)); d.text(((1920-w)/2,490),txt,font=f,fill=(240,226,190)); im.save(out)
def img_video(png,dur,out,fade=0.3):
    run('-loop','1','-framerate','60','-i',png,'-t',str(dur),'-vf',"fade=t=in:st=0:d=%.2f,fade=t=out:st=%.2f:d=%.2f"%(fade,dur-fade,fade),*ENC,out)
parts=[T+'/pro.mp4']; tl=[DPRO]
cartao('OS GUARDIÕES NÃO CAIRÃO SEM LUTA',T+'/c0.png'); img_video(T+'/c0.png',2.6,T+'/c0.mp4'); parts.append(T+'/c0.mp4'); tl.append(2.6)
# ---------- montagem rápida: (parte, tempo no arquivo, batidas)
SH=[('v7_02_guardiao',9.0,4),('v7_07_cavaleiro',9.0,4),('v7_02_guardiao',13.7,4),('v7_08_ladrao',14.5,4),
    ('v7_09_fera',24.0,3),('v7_07_cavaleiro',15.0,3),('v7_02_guardiao',18.2,3),('v7_08_ladrao',18.0,3),
    ('v7_09_fera',21.0,2),('v7_02_guardiao',23.8,2),('v7_07_cavaleiro',19.5,2),('v7_09_fera',33.0,2)]
CLARO='eq=brightness=0.05:contrast=1.12:saturation=1.25:gamma=1.1'
cortes=[]; tm=0.0
for i,(p,a,b) in enumerate(SH):
    d=b*BEAT; vf=CLARO
    if i==0: vf+=',fade=t=in:st=0:d=0.2:color=white'
    if i==len(SH)-1: vf+=',fade=t=out:st=%.3f:d=0.25:color=white'%(d-0.25)
    run('-ss',str(a),'-i','%s/%s.mp4'%(P,p),'-t','%.4f'%d,'-vf',vf,*ENC,'%s/m%02d.mp4'%(T,i)); parts.append('%s/m%02d.mp4'%(T,i)); tl.append(d); cortes.append(tm); tm+=d
MONT=tm
# ---------- final: vila ao entardecer (frame do prólogo), cartões e título
run('-ss','%.2f'%(FIMPRO-3.0),'-i',PRO,'-frames:v','1',T+'/vila.png')
FIM=13.0
im=Image.open(T+'/vila.png').convert('RGB').resize((1920,1080)); im=Image.eval(im,lambda p:int(p*0.62)); im.save(T+'/f_bg.png')
run('-loop','1','-framerate','60','-i',T+'/f_bg.png','-t',str(FIM),'-vf',"zoompan=z='1+0.0005*on':d=%d:s=1920x1080:fps=60,fade=t=in:st=0:d=0.6,fade=t=out:st=%.1f:d=1.2"%(int(FIM*60),FIM-1.2),*ENC,T+'/fim_bg.mp4')
def txt_png(t,out,tam):
    im=Image.new('RGBA',(1920,1080),(0,0,0,0)); d=ImageDraw.Draw(im); f=ImageFont.truetype(FONT,tam); w=d.textlength(t,font=f)
    d.text(((1920-w)/2+3,490+3),t,font=f,fill=(0,0,0,255)); d.text(((1920-w)/2,490),t,font=f,fill=(245,230,195,255)); im.save(out)
TX=[('Sem ouro. Sem fama.',0.2,2.4,92),('Mas com coragem de sobra.',2.7,5.4,86),('TRIAL OF THE FORSAKEN',6.6,12.6,104)]
args=['-i',T+'/fim_bg.mp4']; fc=''; last='0:v'
for i,(t,a,b,s) in enumerate(TX):
    txt_png(t,'%s/t%d.png'%(T,i),s); args+=['-loop','1','-framerate','60','-i','%s/t%d.png'%(T,i)]
    fc+="[%d:v]format=rgba,fade=t=in:st=%.2f:d=0.25:alpha=1,fade=t=out:st=%.2f:d=0.25:alpha=1,setpts=PTS-STARTPTS[o%d];[%s][o%d]overlay=shortest=0:enable='between(t,%.2f,%.2f)'[v%d];"%(i+1,a,b-0.25,i,last,i,a,b,i); last='v%d'%i
run(*args,'-filter_complex',fc.rstrip(';'),'-map','[%s]'%last,'-t',str(FIM),*ENC,T+'/fim.mp4'); parts.append(T+'/fim.mp4'); tl.append(FIM)
EYE=9.0
if not os.path.exists(T+'/olho.mp4'): subprocess.run([sys.executable,os.path.dirname(os.path.abspath(__file__))+'/olho.py',T+'/olho.mp4',str(EYE)],check=True)
parts.append(T+'/olho.mp4'); tl.append(EYE)
open(T+'/l.txt','w').write(''.join("file '%s'\n"%x for x in parts))
run('-f','concat','-safe','0','-i',T+'/l.txt','-c','copy',T+'/mudo.mp4')
TOTAL=sum(tl); TM=DPRO+2.6; TF=TM+MONT; print('total',TOTAL,'montagem',TM,'final',TF)
# ---------- narração nova (voz de ancião)
FAL=[(CT[0]+0.4,"Aproxime-se, viajante... há histórias que o mar tenta esconder. Esta é a de Valverde."),
     (CT[1]+2.9,"Ali, o povo vivia em paz... e ninguém temia a noite."),
     (CT[2]+2.6,"Até que, uma noite, uma fera enorme caiu do céu... e queimou tudo."),
     (CT[3]+2.5,"Um herói selou o seu poder em três cristais, bem longe uns dos outros."),
     (CT[4]+2.6,"Séculos depois, uma magia maligna corrompeu os cristais... e os mortos despertaram."),
     (CT[5]+2.6,"Então, das estradas distantes, chegam aventureiros... sem ouro, sem fama... e com muito a provar."),
     (TF+0.3,"Sem ouro, sem fama... mas com coragem de sobra."),(TF+7.0,"Trial of the Forsaken."),(TF+FIM+2.6,"Mas algo... muito antigo... está despertando.")]
vozes=[]
if not os.path.exists(T+'/voz.txt') or open(T+'/voz.txt').read()!=repr([f[1] for f in FAL]):
    from kokoro_onnx import Kokoro
    k=Kokoro(C+'/kokoro.onnx',C+'/voices.bin')
    for i,(tt,txt) in enumerate(FAL):
        x,sr=k.create(txt,voice='pm_alex',speed=0.80,lang='pt-br'); sf.write(T+'/%02d_raw.wav'%i,x,sr)
        run('-i',T+'/%02d_raw.wav'%i,'-af',open(C+'/filtro_voz.txt').read().strip()%sr,'-ar','44100','-ac','1',T+'/%02d.wav'%i)
    open(T+'/voz.txt','w').write(repr([f[1] for f in FAL]))
# ---------- música e efeitos
M=Mix(TOTAL+3)
def pad(at,notes,dur,g=0.8,kind='strings',v='a'):
    for i,n in enumerate(notes):
        f=mid(n); s={'strings':lambda:strings(f,dur),'choir':lambda:choir(f,dur,1.0,v),'horn':lambda:horn(f,dur)}[kind](); M.add(s,at,g,-0.5+i*0.5)
def wind(t0,t1,g=0.1):
    n=int((t1-t0)*SR); x=lpn(n,500); m=0.5+0.5*np.sin(2*np.pi*np.arange(n)/SR/7.0+1); M.add(x*m*g*3,t0,1.0)
def drone(t0,t1,root=33,g=0.5):
    n=int((t1-t0)*SR); tt=np.arange(n)/SR; s=sum(a*np.sin(2*np.pi*mid(root+iv)*tt*(1+d)) for iv,a,d in [(0,1,0),(0,.6,0.0035),(7,.5,0),(12,.35,0.002)])
    e=np.minimum(tt/3,1)*np.minimum((t1-t0-tt)/3,1); M.add(s*e*0.18*g,t0,1.0)
def rugido(vol=1.0,dur=2.4):
    n=int(dur*SR); t=np.arange(n)/SR; f=70+30*np.sin(2*np.pi*3.2*t)+40*np.exp(-t*1.2); ph=2*np.pi*np.cumsum(f)/SR; s=np.zeros(n)
    for h in range(1,18): s+=np.sin(h*ph)/h**0.8*np.exp(-((h*f.mean()-900)/700)**2*0.3)
    g=lpn(n,2800)*0.6; e=np.sin(np.pi*np.minimum(t/dur,1))**0.7*np.exp(-t*0.35); return (s*0.08+g*0.35)*e*vol
THEME=[57,60,64,62,60,59,57]
def tema(t0,inst,g,step):
    for i,n in enumerate(THEME):
        f=mid(n); M.add({'pluck':lambda:pluck(f,2.4),'horn':lambda:horn(f,step*1.05),'strings':lambda:strings(f,step*1.1)}[inst](),t0+i*step,g)
drone(0,DPRO,33,0.7); wind(0,DPRO,0.09)
# ilha
tema(CT[0]+0.6,'pluck',0.8,1.15); M.add(choir(mid(45),7,1.0,'o'),CT[0]+1,0.7); pad(CT[0]+3,[45,52,57,60],6,0.7)
# vila em paz (calma, arpejo de cordas dedilhadas)
for i,a in enumerate(np.arange(CT[1]+2.6,CT[2]-0.3,0.55)): M.add(pluck(mid([57,64,60,69,64,60][i%6]),1.6),a,0.45,-0.4+0.16*(i%6))
pad(CT[1]+2.6,[45,52,57],8,0.55,'strings')
# fogo: tensão, estouro, tambores crescendo, rugido quando a sombra passa
M.add(swell(2.6,4500,True,0.8),CT[2]+0.0,0.7); M.add(boom(1.8,3.0,95,30),CT[2]+2.4,1.2); pad(CT[2]+2.4,[38,45,50],10,0.8,'horn')
for a in np.arange(CT[2]+2.6,CT[3]-0.5,0.6): M.add(timp(mid(33),1.0),a,0.7+0.3*(a-CT[2])/10)
for a in np.arange(CT[2]+6.5,CT[3]-0.2,0.2): M.add(snare(0.5),a,0.18+0.35*(a-CT[2]-6.5)/4)
M.add(rugido(1.0,2.8),CT[2]+6.9,0.9,-0.2); M.add(boom(1.6,2.0,90,28),CT[2]+6.9,1.0)
def fogo(t0,t1,g=1.0,pan=0.0,seed=0):
    r=np.random.default_rng(100+seed); dur=t1-t0; n=int(dur*SR); tt=np.arange(n)/SR
    ruido=r.standard_normal(n); X=np.fft.rfft(ruido); f=np.fft.rfftfreq(n,1/SR)
    banda=np.fft.irfft(X*np.exp(-((np.log(np.maximum(f,1)/320.0))**2)/(2*0.9**2)),n)                    # rugido grave do fogo (centro ~320 Hz)
    env=0.55+0.25*np.sin(2*np.pi*0.37*tt+r.uniform(0,6))+0.2*np.sin(2*np.pi*1.1*tt+r.uniform(0,6)); roar=banda*env*3.0
    est=np.zeros(n)                                                                                         # estalos: rajadas curtas de ruído agudo, de força muito variada
    for t in np.cumsum(r.exponential(1/34.0,int(dur*60))):
        if t>=dur-0.05: break
        L=int(r.uniform(0.003,0.02)*SR); amp=min(r.pareto(2.2)+0.15,3.0); x=r.standard_normal(L); x=np.diff(x,prepend=0)*np.exp(-np.arange(L)/(L*0.35)); i=int(t*SR); est[i:i+L]+=x[:max(0,min(L,n-i))]*amp*0.55
    pops=np.zeros(n)                                                                                        # estouros maiores (madeira rachando)
    for t in np.cumsum(r.exponential(1/2.6,int(dur*6))):
        if t>=dur-0.2: break
        L=int(0.09*SR); k=np.arange(L)/SR; x=(r.standard_normal(L)*0.6+np.sin(2*np.pi*r.uniform(140,260)*k))*np.exp(-k/0.025)*r.uniform(0.5,1.4); i=int(t*SR); pops[i:i+L]+=x[:max(0,min(L,n-i))]
    chia=bpn(n,2500,7500)*0.12*(0.5+0.5*np.sin(2*np.pi*0.23*tt+r.uniform(0,6)))                                # chiado das chamas
    x=roar+est*0.9+pops*0.9+chia; fa=np.minimum(1,np.minimum(tt/2.0,(dur-tt)/2.5)); x=x*fa; x=x/np.abs(x).max(); M.add(x*g,t0,1.0,pan)
fogo(CT[2]+2.0,CT[3]+0.5,0.5,-0.55,1); fogo(CT[2]+2.3,CT[3]+0.5,0.4,0.55,2)
# cristais: sinos
for i,a in enumerate(np.arange(CT[3]+2.6,CT[4]-0.2,0.5)): M.add(bell(mid([81,84,88,86,84,83][i%6]),3.0),a,0.5,-0.6+0.25*(i%6))
pad(CT[3]+2.6,[45,52,57,64],5,0.7,'choir','a')
# corrupção: graves e pulso
pad(CT[4]+2.6,[45,46,52],8,0.8)
for a in np.arange(CT[4]+2.6,CT[5]-0.3,0.12): M.add(strings(mid(45 if int(a*3)%2 else 46),0.1),a,0.28)
for a in (CT[4]+2.6,CT[4]+5.0,CT[4]+7.4): M.add(boom(0.9,0.7,150,55),a,0.7); M.add(swell(1.4,3000,True,0.5),a-1.3,0.4)
# aventureiros: coral e trompa esperançosa
pad(CT[5]+2.5,[38,45,50],8,0.7,'choir','u'); tema(CT[5]+3.0,'horn',0.8,1.05); pad(CT[5]+3.0,[45,52,57,60],7,0.7)
for a in np.arange(CT[5]+3.0,DPRO-0.5,1.5): M.add(timp(mid(45),1.0),a,0.7)
# cartão dos guardiões + montagem
M.add(swell(2.8,2500,True,0.8),DPRO-0.2,0.5); pad(DPRO,[33,40],3.2,0.8,'strings'); M.add(bell(mid(57),3.0),DPRO+0.3,0.7)
# ---- MÚSICA DE BATALHA (ré menor, 140 BPM): ostinato de cordas em galope, taiko, metais, coral, caixa e pratos
M.add(swell(1.8,6000,True,0.9),TM-1.7,0.8)
B=BEAT; BAR=4*B; nb=int(round(MONT/BAR))+1
prog=[(38,(0,3,7)),(34,(0,4,7)),(36,(0,4,7)),(33,(0,4,7))]                 # Dm | Bb | C | A
def crash(t,g=0.8): M.add(sweep_noise(2.2,2500,9000,0.9,1.0)*0.6,t,g); M.add(metal(3200,2.0,0.6),t,g*0.4)
for bar in range(nb):
    t0=TM+bar*BAR
    if t0>=TM+MONT: break
    r,ch=prog[bar%4]; forte=bar>=4
    if bar%4==0 and bar<8: crash(t0,0.9 if bar==0 else 0.7)
    for beat in range(4):
        tb=t0+beat*B
        for q,vel in enumerate((1.0,0.0,0.7,0.85)):                          # galope: X . x x
            if vel>0: M.add(strings(mid(r+12),0.11,1.0),tb+q*B/4,0.78*vel*(1.15 if forte else 1.0),-0.3+0.2*q)
        if forte:
            for q in (1,3): M.add(strings(mid(r+24),0.09,1.0),tb+q*B/4,0.28,0.3)
        if beat in (0,2): M.add(boom(1.3,1.0,115,38),tb,0.95); M.add(timp(mid(r),1.0),tb,0.75)
        elif beat==3: M.add(boom(0.8,0.5,120,45),tb+B/2,0.55)
        if beat in (1,3): M.add(snare(0.8),tb,0.55 if not forte else 0.7)
        if forte: M.add(metal(5200,0.12,0.4),tb+B/2,0.18)
    for off in (0.0,1.5*B,2.5*B):                                              # golpes de metais no acorde
        for iv in ch: M.add(horn(mid(r+12+iv),B*0.9),t0+off,0.55)
    pad(t0,[r+24,r+31],BAR,0.42,'choir','a')
    if bar%4==3 and bar<8:                                                     # virada de caixa antes da próxima seção
        for q in range(8): M.add(snare(0.6),t0+3*B+q*B/8,0.25+0.3*q/8)
for i,nn in enumerate([62,65,69,67,65,64,62]): M.add(horn(mid(nn+12),B*1.7),TM+4*BAR+i*B*1.0,0.75)            # tema em trompa na 2ª metade
for i,c in enumerate(cortes): M.add(boom(1.0,0.7,130,40),TM+c,0.7)
for q in range(16): M.add(snare(0.7),TF-1.8+q*0.11,0.3+0.5*q/16)
M.add(boom(1.8,3.2,85,26),TF-0.15,1.3); M.add(metal(260,3.0,1.0),TF-0.15,0.4); crash(TF-0.1,1.0)
# final
for i,nn in enumerate(THEME): M.add(horn(mid(nn),1.2),TF+5.8+i*1.15,0.7); M.add(strings(mid(nn),1.3),TF+5.8+i*1.15,0.6)
pad(TF,[45,52,57],6.5,0.55,'choir','a'); pad(TF+6.0,[45,52,57,60],7,0.8,'choir','a'); M.add(bell(mid(57),6),TF+6.6,1.0); M.add(boom(1.8,3,80,28),TF+6.6,1.0)
for t in (0.2,2.7): M.add(bell(mid(69),2.5),TF+t,0.5)
# olho do dragão: silêncio pesado, respiração grave, batida de coração, grunhido no corte
TE=TF+FIM
drone(TE,TE+EYE,28,1.3); wind(TE+0.5,TE+EYE-0.5,0.05)
for i,a in enumerate(np.arange(TE+1.6,TE+7.4,1.15)): M.add(boom(0.9,0.7,70,28),a,0.35+0.35*i/5); M.add(boom(0.6,0.5,80,32),a+0.28,0.2+0.2*i/5)
M.add(swell(3.0,1400,True,0.7),TE+2.2,0.5)
def respira(t0,dur,g):
    n=int(dur*SR); tt=np.arange(n)/SR; x=lpn(n,700)*np.sin(np.pi*tt/dur)**2; M.add(x*g,t0,1.0)
respira(TE+3.0,2.6,0.5); respira(TE+5.8,2.2,0.6)
M.add(rugido(1.3,2.6),TE+6.3,1.0,0.0); M.add(boom(2.0,2.6,75,24),TE+6.3,1.2)
ir=reverb_ir(2.8); L=M.L+0.4*conv_blocks(M.L,ir); R=M.R+0.4*conv_blocks(M.R,reverb_ir(2.8,9)); m=max(np.abs(L).max(),np.abs(R).max()); L,R=L/m*0.55,R/m*0.55
N=int(TOTAL*SR); A=np.zeros((N,2),np.float32); nn_=min(len(L),N); A[:nn_,0]=L[:nn_]; A[:nn_,1]=R[:nn_]
def rd(f):
    w=wave.open(f); x=np.frombuffer(w.readframes(w.getnframes()),dtype=np.int16).astype(np.float32)/32768; w.close(); return x
duck=np.ones(N,np.float32); vo=np.zeros((N,2),np.float32)
for i,(tt,txt) in enumerate(FAL):
    x=rd(T+'/%02d.wav'%i); s=int(tt*SR); e=min(N,s+len(x)); vo[s:e,0]+=1.4*x[:e-s]; vo[s:e,1]+=1.4*x[:e-s]
    a=int(max(0,tt-0.35)*SR); b=min(N,int((tt+len(x)/SR+0.6)*SR)); duck[a:b]=np.minimum(duck[a:b],0.16 if tt<TF+FIM else 0.3); print('voz',round(tt,1),'dura',round(len(x)/SR,1),txt[:28])
kk=int(SR*0.35); duck=np.convolve(duck,np.ones(kk,np.float32)/kk,mode='same')
def rms_db(x): return 20*np.log10(np.sqrt(np.mean(np.square(x)))+1e-9)
for tt,txt in FAL:
    x=rd(T+'/%02d.wav'%FAL.index((tt,txt))); s0=int(tt*SR); s1=min(N,s0+len(x))
    print('  voz %5.1fs: voz %.1f dB | música sob a voz %.1f dB | diferença %.1f dB'%(tt,rms_db(vo[s0:s1]),rms_db((A*duck[:,None])[s0:s1]),rms_db(vo[s0:s1])-rms_db((A*duck[:,None])[s0:s1])))
for nome,a0,a1 in (('prólogo',0,DPRO),('batalha',TM,TF),('final',TF,TOTAL)): print('  trecho %-8s música+efeitos: %.1f dB'%(nome,rms_db(A[int(a0*SR):int(a1*SR)])))
A=np.tanh((A*duck[:,None]+vo)*1.05)/1.05; A*=0.92/np.abs(A).max(); f=np.minimum(1,np.maximum(0,(TOTAL-np.arange(N)/SR)/1.6)); A*=f[:,None]
w=wave.open(T+'/audio.wav','wb'); w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes((A*32767).astype(np.int16).tobytes()); w.close()
out=os.path.expanduser('~/Videos/Trailer_historia_v5.mp4')
run('-i',T+'/mudo.mp4','-i',T+'/audio.wav','-map','0:v','-map','1:a','-c:v','copy','-c:a','aac','-b:a','256k','-shortest',out); print('pronto',out)
