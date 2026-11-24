# Trailer "história + chefes em corte rápido" (v2). Prólogo + montagem no ritmo da música + cartões + título. Sem dragão e sem apresentação dos heróis.
import subprocess, os, wave, json, numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from lib import *
C=os.path.expanduser('~/.cache/pinglu_audio'); V=os.path.expanduser('~/Videos/Pinglu_trailer_partes/cortadas'); T=C+'/v2'; os.makedirs(T,exist_ok=True)
FONT='/home/amaro/Downloads/Pinglu_RPG/Assets/Resources/Fontes/CinzelBold.ttf'
BPM=100; BEAT=60/BPM
def run(*a): subprocess.run(['ffmpeg','-loglevel','error','-y']+list(a),check=True)
ENC=['-an','-c:v','libx264','-preset','fast','-crf','17','-pix_fmt','yuv420p','-r','60']
# ---------- cartão de texto (imagem 1920x1080)
def cartao(txt,out,bg=None,tam=84,dark=0.55):
    im=Image.new('RGB',(1920,1080),(4,4,6)) if bg is None else Image.open(bg).convert('RGB').resize((1920,1080))
    if bg is not None:
        im=im.filter(ImageFilter.GaussianBlur(2)); im=Image.eval(im,lambda p:int(p*(1-dark)))
    d=ImageDraw.Draw(im); f=ImageFont.truetype(FONT,tam); w=d.textlength(txt,font=f)
    d.text(((1920-w)/2+3,490+3),txt,font=f,fill=(0,0,0)); d.text(((1920-w)/2,490),txt,font=f,fill=(240,226,190)); im.save(out)
def img_video(png,dur,out,fade=0.3,zoom=False):
    vf="fade=t=in:st=0:d=%.2f,fade=t=out:st=%.2f:d=%.2f"%(fade,dur-fade,fade)
    if zoom: vf="zoompan=z='1+0.0006*on':d=%d:s=1920x1080:fps=60,"%int(dur*60)+vf; run('-i',png,'-t',str(dur),'-vf',vf,*ENC,out)
    else: run('-loop','1','-framerate','60','-i',png,'-t',str(dur),'-vf',vf,*ENC,out)
parts=[]; tl=[]  # (arquivo_video, duração)
def add(f,d): parts.append(f); tl.append(d)
# ---------- 1) prólogo
run('-t','71','-i',V+'/01_prologo.mp4',*ENC,T+'/pro.mp4'); add(T+'/pro.mp4',71.0)
# ---------- 2) cartão antes da montagem
cartao('OS GUARDIÕES NÃO CAIRÃO SEM LUTA',T+'/c0.png',tam=68); img_video(T+'/c0.png',2.6,T+'/c0.mp4'); add(T+'/c0.mp4',2.6)
# ---------- 3) montagem rápida (duração em batidas); tempos dentro das partes já gravadas
SH=[('02_guardiao',7.0,2),('07_cavaleiro_caido',7.0,2),('02_guardiao',10.0,2),('08_ladrao',9.0,2),
    ('09_fera_do_pantano',14.0,1.5),('07_cavaleiro_caido',13.0,1.5),('02_guardiao',14.6,1.5),('08_ladrao',16.0,1.5),
    ('09_fera_do_pantano',20.0,1),('02_guardiao',17.5,1),('07_cavaleiro_caido',20.0,1),('09_fera_do_pantano',26.0,1)]
CLARO='eq=brightness=0.06:contrast=1.12:saturation=1.25:gamma=1.12'
cortes=[]; tm=0.0
for i,(p,a,b) in enumerate(SH):
    d=b*BEAT; vf=CLARO
    if i==0: vf+=',fade=t=in:st=0:d=0.2:color=white'
    if i==len(SH)-1: vf+=',fade=t=out:st=%.3f:d=0.25:color=white'%(d-0.25)
    run('-ss',str(a),'-i','%s/%s.mp4'%(V,p),'-t','%.4f'%d,'-vf',vf,*ENC,'%s/m%02d.mp4'%(T,i)); add('%s/m%02d.mp4'%(T,i),d); cortes.append(tm); tm+=d
MONT=tm
# ---------- 4) final: vila ao entardecer (sem dragão), cartões curtos e título
run('-ss','66.5','-i',V+'/01_prologo.mp4','-frames:v','1',T+'/vila.png')
FIM=13.0; bg=T+'/fundo.png'; Image.open(T+'/vila.png').save(bg)
cartao('',T+'/f_bg.png',bg=bg,dark=0.35)
run('-loop','1','-framerate','60','-i',T+'/f_bg.png','-t',str(FIM),'-vf',"zoompan=z='1+0.0005*on':d=%d:s=1920x1080:fps=60,fade=t=in:st=0:d=0.6,fade=t=out:st=%.1f:d=1.2"%(int(FIM*60),FIM-1.2),*ENC,T+'/fim_bg.mp4')
def txt_png(t,out,tam):
    im=Image.new('RGBA',(1920,1080),(0,0,0,0)); d=ImageDraw.Draw(im); f=ImageFont.truetype(FONT,tam); w=d.textlength(t,font=f)
    d.text(((1920-w)/2+3,490+3),t,font=f,fill=(0,0,0,255)); d.text(((1920-w)/2,490),t,font=f,fill=(245,230,195,255)); im.save(out)
TX=[('Sem tostão no bolso.',0.2,1.9,76),('O prêmio?',1.9,3.0,76),('Um cabaré.',3.0,5.2,96),('TRIAL OF THE FORSAKEN',6.6,12.6,92)]
args=['-i',T+'/fim_bg.mp4']; fc=''; last='0:v'
for i,(t,a,b,s) in enumerate(TX):
    txt_png(t,'%s/t%d.png'%(T,i),s); args+=['-loop','1','-framerate','60','-i','%s/t%d.png'%(T,i)]
    fc+="[%d:v]format=rgba,fade=t=in:st=%.2f:d=0.25:alpha=1,fade=t=out:st=%.2f:d=0.25:alpha=1,setpts=PTS-STARTPTS[o%d];[%s][o%d]overlay=shortest=0:enable='between(t,%.2f,%.2f)'[v%d];"%(i+1,a,b-0.25,i,last,i,a,b,i); last='v%d'%i
run(*args,'-filter_complex',fc.rstrip(';'),'-map','[%s]'%last,'-t',str(FIM),*ENC,T+'/fim.mp4'); add(T+'/fim.mp4',FIM)
open(T+'/l.txt','w').write(''.join("file '%s'\n"%x for x in parts))
run('-f','concat','-safe','0','-i',T+'/l.txt','-c','copy',T+'/mudo.mp4')
TOTAL=sum(tl); T0=71.0; TM=T0+2.6; TF=TM+MONT
print('total',TOTAL,'montagem em',TM,'final em',TF)
# ---------- áudio
st=wave.open(C+'/sem_voz.wav'); full=np.frombuffer(st.readframes(st.getnframes()),dtype=np.int16).reshape(-1,2).astype(np.float32)/32768; st.close()
Mx=Mix(TOTAL+3)
def pad(at,notes,dur,g=0.8,kind='strings',v='a'):
    for i,n in enumerate(notes):
        f=mid(n); s={'strings':lambda:strings(f,dur),'choir':lambda:choir(f,dur,1.0,v),'horn':lambda:horn(f,dur)}[kind](); Mx.add(s,at,g,-0.5+i*0.5)
# cartão: drone grave + sino
Mx.add(swell(2.8,2500,True,0.8),T0-0.2,0.5); pad(T0,[33,40],3.2,0.8,'strings'); Mx.add(bell(mid(57),3.0),T0+0.3,0.7)
# montagem: tempo marcado (timpano na batida, caixa nas contratempos, cordas em colcheias, bumbo em cada corte)
Mx.add(swell(2.0,5000,True,0.8),TM-1.9,0.7)
roots=[45,41,43,45]; n=int(MONT/BEAT*2)
for k in range(n):
    t=TM+k*BEAT/2; r=roots[int(k/8)%4]
    Mx.add(strings(mid([r+12,r+19,r+12,r+24][k%4]),0.12),t,0.75 if k%4==0 else 0.5,-0.3+0.2*(k%4))
    if k%2==0: Mx.add(timp(mid(33 if (k//2)%4!=3 else 31),1.0),t,0.9 if k%4==0 else 0.6)
    else: Mx.add(snare(0.6),t,0.3+0.3*k/n)
for i,c in enumerate(cortes):
    t=TM+c; Mx.add(boom(1.0,0.7,130,40),t,0.8); Mx.add(metal(500+120*(i%4),0.7,0.9),t,0.25)
pad(TM,[45,52,57,60],MONT,0.6,'horn'); pad(TM,[45,52,57],MONT,0.5,'choir','o')
Mx.add(boom(1.8,3.0,90,28),TM,1.1); Mx.add(boom(1.8,3.2,85,26),TF-0.15,1.2); Mx.add(metal(260,3.0,1.0),TF-0.15,0.4)
# final: tema em trompa + cordas, sinos
THEME=[57,60,64,62,60,59,57]
for i,nn in enumerate(THEME):
    Mx.add(horn(mid(nn),1.2),TF+5.8+i*1.15,0.7); Mx.add(strings(mid(nn),1.3),TF+5.8+i*1.15,0.6)
pad(TF,[45,52,57],6.5,0.55,'choir','a'); pad(TF+6.0,[45,52,57,60],7,0.8,'choir','a'); Mx.add(bell(mid(57),6),TF+6.6,1.0); Mx.add(boom(1.8,3,80,28),TF+6.6,1.0)
for t in (0.2,1.9,3.0): Mx.add(bell(mid(69),2.5),TF+t,0.5)
ir=reverb_ir(2.8); L=Mx.L+0.4*conv_blocks(Mx.L,ir); R=Mx.R+0.4*conv_blocks(Mx.R,reverb_ir(2.8,9))
m=max(np.abs(L).max(),np.abs(R).max()); L,R=L/m*0.55,R/m*0.55
N=int(TOTAL*SR); A=np.zeros((N,2),np.float32); A[:int(71*SR)]=full[:int(71*SR)]
nn_=min(len(L),N); A[int(71*SR):nn_,0]+=L[int(71*SR):nn_]; A[int(71*SR):nn_,1]+=R[int(71*SR):nn_]
# voz: narração do prólogo (já na trilha antiga) NÃO está no stem; coloca de novo + falas do final
def rd(f):
    w=wave.open(f); x=np.frombuffer(w.readframes(w.getnframes()),dtype=np.int16).astype(np.float32)/32768; w.close(); return x
duck=np.ones(N,np.float32); vo=np.zeros((N,2),np.float32)
def put(x,t):
    s=int(t*SR); e=min(N,s+len(x)); vo[s:e,0]+=x[:e-s]; vo[s:e,1]+=x[:e-s]; a=int(max(0,t-0.35)*SR); b=min(N,int((t+len(x)/SR+0.6)*SR)); duck[a:b]=np.minimum(duck[a:b],0.3)
for i,t in enumerate([0.8,15.6,27.2,38.6,49.6,61.0]): put(rd(C+'/voz/%02d.wav'%i),t)
put(rd(C+'/hist/12.wav'),TF+0.3); put(rd(C+'/hist/11.wav'),TF+7.0)
kk=int(SR*0.35); duck=np.convolve(duck,np.ones(kk,np.float32)/kk,mode='same')
A=np.tanh((A*duck[:,None]+vo)*1.05)/1.05; A*=0.92/np.abs(A).max()
f=np.minimum(1,np.maximum(0,(TOTAL-np.arange(N)/SR)/1.5)); A*=f[:,None]
w=wave.open(T+'/audio.wav','wb'); w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes((A*32767).astype(np.int16).tobytes()); w.close()
out=os.path.expanduser('~/Videos/Trailer_historia_v2.mp4')
run('-i',T+'/mudo.mp4','-i',T+'/audio.wav','-map','0:v','-map','1:a','-c:v','copy','-c:a','aac','-b:a','256k','-shortest',out); print('pronto',out)
