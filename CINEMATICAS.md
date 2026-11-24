# Cinemáticas em vídeo (Abertura e Final)

O jogo já sabe tocar vídeo pronto no lugar das cenas desenhadas em código. É assim que estúdios fazem: a cinemática é produzida numa ferramenta própria e o jogo só reproduz.

## Como ligar um vídeo
1. Coloque o arquivo em `Assets/Resources/Videos/`:
   - `Abertura.webm` — toca no começo do jogo, antes da seleção de personagens.
   - `Final.webm` — toca no fim da história; depois vêm o título e os créditos.
2. (Opcional) Legendas em `Assets/Resources/Videos/Abertura_legendas.txt` (e `Final_legendas.txt`), uma por linha: `inicio|fim|texto` em segundos.
3. Pronto. Sem o arquivo, o jogo usa a cena em código de antes. Esc pula o vídeo.

**Formato recomendado:** WebM (VP8) + áudio Vorbis, 1920x1080, 24 ou 30 fps. Para converter qualquer vídeo:
`ffmpeg -i entrada.mp4 -c:v libvpx -b:v 6M -c:a libvorbis Abertura.webm`
(No Linux o Unity só toca bem WebM; no Windows .mp4 também funciona.)

## Como produzir o vídeo (escolha uma)
- **Gerador de vídeo por IA** (Runway, Kling, Veo, Luma, Pika...): gere um clipe por cena com os prompts abaixo, junte no CapCut/DaVinci Resolve (grátis), ponha música e exporte. Mais rápido; mantenha o mesmo prompt de estilo em todos os clipes para o dragão não mudar de aparência.
- **Blender** (grátis): anima em 3D e exporta o vídeo. Mais controle e mais trabalho.
- **Gravar dentro do Unity** (pacote Recorder): usa o visual real do jogo.

Estilo para colar em todo prompt: *"epic dark fantasy cinematic, painterly, volumetric light, dramatic lighting, film grain, 16:9, a colossal black dragon with glowing orange eyes and a dark purple aura, island village of Valverde"*

## ABERTURA (≈ 1 min 30) — cenas e legendas
| # | Cena | Legenda (já usada no jogo) |
|---|------|---------|
| 1 | Ilha entre brumas no mar, amanhecer frio, câmera aérea lenta | Valverde... uma pequena ilha esquecida entre as brumas do mar. |
| 2 | Dragão colossal cruzando o sol, vilas em chamas lá embaixo | Há muito tempo, um dragão colossal despertou e incendiou as vilas da ilha, uma por uma. |
| 3 | Herói num penhasco sob tempestade, de capa, enfrentando o dragão e o fogo | Um herói enfrentou a fera, mas nenhum aço foi capaz de matá-la. |
| 4 | Herói dividindo o poder em três cristais brilhantes (azul, verde, roxo) | Então ele dividiu o poder do dragão em três cristais e os selou, bem longe uns dos outros. |
| 5 | Cemitério enevoado, lápides, um guardião de pedra, cristal azul | Um deles ficou no cemitério, vigiado pelo Guardião das Lápides. |
| 6 | Castelo em ruínas ao pôr do sol, velho cavaleiro fazendo a ronda | Outro, no castelo caído, onde um velho cavaleiro ainda faz a ronda. |
| 7 | Templo submerso num pântano escuro, cristal verde sob a água | E o último, no templo do pântano, sob as águas escuras. |
| 8 | Os três cristais rachando, fumaça negra e magia vermelha os corrompendo | Séculos se passaram... até que uma magia maligna começou a corromper os cristais. |
| 9 | Mortos-vivos saindo da terra à luz da lua, vila de Valverde ao fundo | Os mortos acordaram, e a vila de Valverde prometeu uma grande recompensa a quem trouxesse a paz de volta. |
| 10 | Quatro aventureiros (cavaleiro, maga, tanque, curandeiro) chegando na estrada, de bolsos vazios, tom bem-humorado | E é aí que chegam alguns aventureiros... sem um tostão no bolso. |

Exemplo de `Abertura_legendas.txt` (ajuste os tempos ao seu vídeo):
```
2|9|Valverde... uma pequena ilha esquecida entre as brumas do mar.
11|20|Há muito tempo, um dragão colossal despertou e incendiou as vilas da ilha, uma por uma.
```

## FINAL (≈ 1 min) — cenas e legendas
| # | Cena | Legenda |
|---|------|---------|
| 1 | Morte do dragão: despenca, tremores, a fumaça negra do Ladrão sai do peito rumo ao céu, o corpo vira cinzas ao vento | Drakthar caiu. O poder dos cristais, já sem dono, se desfez no vento para nunca mais voltar. |
| 2 | Amanhecer sobre Valverde: céu limpo, luz dourada, sem cinzas e sem mortos | Valverde acordou numa manhã sem cinzas, sem mortos e sem medo. |
| 3 | A praça da vila com uma placa nova brilhando | E no centro da vila, uma placa nova brilhava: CABARÉ DOS AVENTUREIROS. |
| 4 | Os heróis felizes diante do balcão do cabaré, moedas tilintando | Os heróis, enfim, tinham um tostão no bolso... e um balcão inteiro para gastá-lo. |

Depois do vídeo o jogo mostra sozinho o título "Pinglu RPG", os créditos (edite a lista `creditos` em `FinalDoJogo.cs`) e a tela final.

## Observação sobre as cenas de luta (morte do dragão, fusão)
Essas continuam no jogo, com o dragão 3D de verdade. Na morte do dragão, `Shift+F9` toca a versão em 3D com Timeline e `F9` a versão ilustrada em código (a mais fraca).
