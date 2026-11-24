#!/bin/bash
# Grava partes do trailer, uma por vez, com o Unity fechado.  uso: gravar.sh "nome|parte|cena" ...  (cena é opcional)
PROJ=/home/amaro/Downloads/Pinglu_RPG
OUT=/home/amaro/Videos/Pinglu_trailer_partes
UNITY=/home/amaro/Unity/Hub/Editor/6000.6.1f1/Editor/Unity
LOGS=/home/amaro/.cache/pinglu_audio/logs
LOG=$LOGS/gravar.log
echo "inicio $(date +%T)" > $LOG
for g in "$@"; do
  IFS='|' read nome partes cena <<< "$g"
  echo "[$(date +%T)] gravando $nome" >> $LOG
  rm -f $PROJ/Recordings/trailer_final.webm
  PINGLU_TRAILER=1 PINGLU_TRAILER_CENA="$cena" PINGLU_TRAILER_PARTES="$partes" $UNITY -projectPath $PROJ -executeMethod PingluTrailerFinal.GravarEFechar -logFile $LOGS/unity_$nome.log > /dev/null 2>&1
  if [ -f $PROJ/Recordings/trailer_final.webm ]; then
    mv $PROJ/Recordings/trailer_final.webm $OUT/$nome.webm
    ffmpeg -loglevel error -y -i $OUT/$nome.webm -c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -r 60 $OUT/$nome.mp4
    echo "[$(date +%T)] pronto $nome.mp4" >> $LOG
  else echo "[$(date +%T)] FALHOU $nome" >> $LOG; fi
done
echo "fim $(date +%T)" >> $LOG
