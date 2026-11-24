# Voz natural offline (XTTS-v2, roda no computador; licença Coqui CPML, uso não comercial, aceita pelo usuário).
# uso: python voz_xtts.py "Nome do falante" saida.wav "texto" [velocidade]
import os, sys
os.environ['COQUI_TOS_AGREED'] = '1'
from TTS.api import TTS
_t = None
def sintetiza(textos, falante, saidas, vel=0.92):
    global _t
    if _t is None: _t = TTS('tts_models/multilingual/multi-dataset/xtts_v2').to('cpu')
    for tx, out in zip(textos, saidas):
        _t.tts_to_file(text=tx, speaker=falante, language='pt', file_path=out, speed=vel, temperature=0.55, repetition_penalty=5.0)
if __name__ == '__main__':
    sintetiza([sys.argv[3]], sys.argv[1], [sys.argv[2]], float(sys.argv[4]) if len(sys.argv) > 4 else 0.92)
