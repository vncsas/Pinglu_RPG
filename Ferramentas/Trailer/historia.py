# Trailer "só história": usa trechos curtos das partes já gravadas + narração nova.  Saída: ~/Videos/Trailer_historia_v1.mp4
# (chave, parte, início_s, fim_s) — tempos dentro dos vídeos já cortados (cortadas/)
SEG=[('pro','01_prologo',0,71),
 ('h1','03_heroi1_cavaleiro',13,19.5),('h2','04_heroi2_morgana',9.5,16),('h3','05_heroi3_tanque',11.5,18),('h4','06_heroi4_curandeiro',3,9.5),
 ('g1','02_guardiao',0.5,8.5),('g2','02_guardiao',12.0,19.0),
 ('c','07_cavaleiro_caido',0.5,8),('l','08_ladrao',0.5,8),
 ('f1','09_fera_do_pantano',0.5,5),('f2','09_fera_do_pantano',12,18),
 ('d','10_dragao',0.5,16),('fim','11_final',0,13)]
# (segmento, deslocamento_s, texto). 'pro' reaproveita as 6 falas do prólogo (voz 00..05)
FALAS=[('h1',0.3,"Um cavaleiro, cuja lâmina corta até a escuridão."),
 ('h2',0.3,"Uma feiticeira, que acende a noite."),
 ('h3',0.3,"Um guerreiro, muralha contra o mal."),
 ('h4',0.3,"E um curandeiro... para que ninguém caia."),
 ('g1',0.9,"No cemitério, o Guardião das Lápides vigia os mortos."),
 ('g2',2.0,"Mas quando a pedra se parte... desperta a sua fúria."),
 ('c',0.8,"No castelo, um cavaleiro caído faz uma ronda que nunca termina."),
 ('l',0.8,"E um ladrão sorri, esperando que os heróis façam o trabalho sujo."),
 ('f1',0.9,"No pântano, algo desperta sob as águas escuras."),
 ('d',0.9,"E quando o último selo cair... Drakthar, o devastador, voltará."),
 ('d',9.2,"Até os deuses se calarão."),
 ('fim',1.5,"Trial of the Forsaken."),('fim',5.2,"Sem tostão no bolso. O prêmio? Um cabaré.")]
PRO_ABS=[0.8,15.6,27.2,38.6,49.6,61.0]
