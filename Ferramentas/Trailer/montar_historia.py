import subprocess, os, wave, numpy as np, soundfile as sf
from historia import SEG, FALAS, PRO_ABS
C=os.path.expanduser('~/.cache/pinglu_audio'); V='/home/amaro/Videos/Pinglu_trailer_partes/cortadas'; TMP=C+'/hist'; os.makedirs(TMP,exist_ok=True); SR=44100
# 1) narração nova
if not os.path.exists(TMP+'/voz.done'):
    from kokoro_onnx import Kokoro
    k=Kokoro(C+'/kokoro.onnx',C+'/voices.bin')
    for i,(s,o,t) in enumerate(FALAS):
        x,sr=k.create(t,voice='pm_alex',speed=0.80,lang='pt-br'); sf.write(TMP+'/%02d_raw.wav'%i,x,sr)
        subprocess.run(['ffmpeg','-loglevel','error','-y','-i',TMP+'/%02d_raw.wav'%i,'-af',open(C+'/filtro_voz.txt').read().strip()%sr,'-ar','44100','-ac','1',TMP+'/%02d.wav'%i],check=True)
    open(TMP+'/voz.done','w').write('1')
# 2) tempos originais de cada parte na trilha longa
import json; PO=json.load(open(C+'/tempos.json'))['P']
KEY={'01_prologo':'prologo','02_guardiao':'guardiao','03_heroi1_cavaleiro':'heroi0','04_heroi2_morgana':'heroi1','05_heroi3_tanque':'heroi2','06_heroi4_curandeiro':'heroi3','07_cavaleiro_caido':'cavaleiro','08_ladrao':'ladrao','09_fera_do_pantano':'fera','10_dragao':'dragao','11_final':'final'}
st=wave.open(C+'/sem_voz.wav'); full=np.frombuffer(st.readframes(st.getnframes()),dtype=np.int16).reshape(-1,2).astype(np.float32)/32768; st.close()
start={}; t=0.0; mus=[]; lst=[]
for i,(k,p,a,b) in enumerate(SEG):
    start[k]=t; d=b-a
    o=int((PO[KEY[p]]+a)*SR); seg=full[o:o+int(d*SR)].copy()
    n=len(seg); f=int(0.15*SR); seg[:f]*=np.linspace(0,1,f)[:,None]; seg[-f:]*=np.linspace(1,0,f)[:,None]
    mus.append(seg)
    out='%s/s%02d.mp4'%(TMP,i); lst.append(out)
    subprocess.run(['ffmpeg','-loglevel','error','-y','-ss',str(a),'-i','%s/%s.mp4'%(V,p),'-t',str(d),'-an','-c:v','libx264','-preset','fast','-crf','17','-pix_fmt','yuv420p','-r','60',out],check=True)
    t+=d
TOTAL=t; M=np.concatenate(mus)
open(TMP+'/l.txt','w').write(''.join("file '%s'\n"%x for x in lst))
subprocess.run(['ffmpeg','-loglevel','error','-y','-f','concat','-safe','0','-i',TMP+'/l.txt','-c','copy',TMP+'/mudo.mp4'],check=True)
# 3) voz + música abaixando
vo=np.zeros_like(M); duck=np.ones(len(M),dtype=np.float32)
def put(x,T):
    s=int(T*SR); e=min(len(vo),s+len(x)); vo[s:e,0]+=x[:e-s]; vo[s:e,1]+=x[:e-s]
    a=int(max(0,T-0.35)*SR); b=min(len(M),int((T+len(x)/SR+0.6)*SR)); duck[a:b]=np.minimum(duck[a:b],0.30)
def rd(f):
    w=wave.open(f); x=np.frombuffer(w.readframes(w.getnframes()),dtype=np.int16).astype(np.float32)/32768; w.close(); return x
for i,T in enumerate(PRO_ABS): put(rd(C+'/voz/%02d.wav'%i),T)
for i,(s,o,txt) in enumerate(FALAS):
    x=rd(TMP+'/%02d.wav'%i); T=start[s]+o; print(s,round(T,1),round(len(x)/SR,1),'s',txt[:30]); put(x,T)
kk=int(SR*0.35); duck=np.convolve(duck,np.ones(kk,dtype=np.float32)/kk,mode='same')
mix=np.tanh((M*duck[:,None]*1.0+vo)*1.05)/1.05; mix*=0.92/np.abs(mix).max()
w=wave.open(TMP+'/audio.wav','wb'); w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes((mix*32767).astype(np.int16).tobytes()); w.close()
out=os.path.expanduser('~/Videos/Trailer_historia_v1.mp4')
subprocess.run(['ffmpeg','-loglevel','error','-y','-i',TMP+'/mudo.mp4','-i',TMP+'/audio.wav','-map','0:v','-map','1:a','-c:v','copy','-c:a','aac','-b:a','256k','-shortest',out],check=True)
print('pronto',out,TOTAL)
