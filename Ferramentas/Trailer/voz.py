# Gera a narração (voz de ancião) com o modelo local Kokoro. Saída: ~/.cache/pinglu_audio/voz/NN.wav
import subprocess, os, soundfile as sf
from kokoro_onnx import Kokoro
from falas import FALAS
C=os.path.expanduser('~/.cache/pinglu_audio'); os.makedirs(C+'/voz',exist_ok=True)
k=Kokoro(C+'/kokoro.onnx',C+'/voices.bin')
for i,(p,r,o,txt) in enumerate(FALAS):
    s,sr=k.create(txt,voice='pm_alex',speed=0.80,lang='pt-br'); raw=C+'/voz/%02d_raw.wav'%i; sf.write(raw,s,sr)
    subprocess.run(['ffmpeg','-loglevel','error','-y','-i',raw,'-af',
      "asetrate=%d*0.90,aresample=44100,vibrato=f=4.6:d=0.22,highpass=f=80,lowpass=f=5200,equalizer=f=200:t=q:w=1.0:g=4,equalizer=f=3200:t=q:w=1.5:g=-3,acompressor=threshold=-22dB:ratio=3,aecho=0.8:0.5:70|150|260:0.30|0.22|0.12,volume=1.5"%sr,
      '-ar','44100','-ac','1',C+'/voz/%02d.wav'%i],check=True)
    print(i,round(len(s)/sr,1),flush=True)
