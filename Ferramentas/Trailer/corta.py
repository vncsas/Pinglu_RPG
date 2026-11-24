# Corta o preto do começo de cada parte (o tempo vem do registro do Unity) e salva em Videos/Pinglu_trailer_partes/cortadas
import re,subprocess,os
LOGS=os.path.expanduser('~/.cache/pinglu_audio/logs'); V='/home/amaro/Videos/Pinglu_trailer_partes'; os.makedirs(V+'/cortadas',exist_ok=True)
N=['01_prologo','02_guardiao','03_heroi1_cavaleiro','04_heroi2_morgana','05_heroi3_tanque','06_heroi4_curandeiro','07_cavaleiro_caido','08_ladrao','09_fera_do_pantano','10_dragao','11_final']
for n in N:
    tr=0.0
    if n not in ('01_prologo','11_final'):
        for l in open('%s/unity_%s.log'%(LOGS,n),errors='ignore'):
            m=re.match(r'\[SOM\] ([\d.]+) parte',l)
            if m: tr=max(0.0,float(m.group(1))-0.35); break
    subprocess.run(['ffmpeg','-loglevel','error','-y','-ss','%.3f'%tr,'-i','%s/%s.mp4'%(V,n),'-c:v','libx264','-preset','fast','-crf','17','-pix_fmt','yuv420p','-r','60','%s/cortadas/%s.mp4'%(V,n)],check=True); print(n,round(tr,2))
