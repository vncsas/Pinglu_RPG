# Protótipo de terreno e ambientação — README para aplicar no mapa do jogo

Mapa de teste (Unity 6000.5.6f1, URP) feito só para validar **terreno, vegetação, água, pedras e iluminação**.
Nada aqui é conteúdo final: os prédios são blocos simples e as árvores são procedurais. O objetivo é copiar as **técnicas** para o mapa real.

- Cena: `Assets/Ambiente/Teste_Terreno.unity` (tudo é regenerável)
- Gerador: `Assets/Ambiente/Editor/MontarAmbiente.cs` (terreno, biomas, água, grama, atalhos) e `MontarVila.cs` (vila, árvores, muros/escadas, céu e pós-processamento)
- Menu/execução em batch: `-executeMethod MontarAmbiente.Montar` (monta a cena) e `MontarAmbiente.Fotos` (tira fotos de pontos fixos em `Fotos/`)
- Controles no teste: WASD, Shift, mouse, teclas **1–6** teleportam para cada região (vila, campo, estrada de barro, floresta, pântano, terras secas)

---

## 1. Terreno (Unity Terrain, 240 × 240 m, altura máx. 45 m)

O relevo é calculado por código em `heightmap` (SetHeights), somando funções:

| Elemento | Como é feito |
|---|---|
| Relevo base | ruído em várias oitavas, suavizado, mais plano perto da vila e do caminho |
| Estrada de barro | curva senoidal (`CaminhoZ`); `DistCaminho` dá a distância e achata/afunda levemente o leito |
| Lago do pântano | "bacia" cavada com **anel de colinas ao redor** (relevo dos dois lados, senão a água parece solta) |
| Terraços da vila | `Terraco(x,z)`: anéis de raio 56 e 72 m, degraus de 1,15 m, só nos arcos norte e sul; nas **rampas** (ângulos 75°, 112°, −75°, −112°) o degrau vira inclinação suave |

**Dicas que funcionaram**
- Crie o `TerrainData` como asset **antes** de preencher alphamaps, senão as camadas de textura não persistem (ficava tudo grama).
- Defina `alphamapResolution` antes de `terrainLayers`.
- Terreno menor = mais leve. 240 m já é suficiente para ver todos os biomas; no jogo, aplique em chunks.

### Biomas e texturas (todas ambientCG, CC0)
A função `Bioma(u,v)` decide a camada por posição/altura/declive (splatmap):

| Bioma | Textura |
|---|---|
| Grama verde | Grass004 |
| Grama escura (floresta) | Grass001 |
| Barro / estrada | Ground076 |
| Lama (pântano) | Ground109 |
| Pedra / encostas | Ground111 |
| Folhas (sub-bosque) | Ground106 |
| Grama seca / terras secas | ScatteredLeaves009 |
| Piso da praça da vila | PavingStones149 |

Misturar camadas por declive (pedra nas encostas) e por altura/umidade (lama perto da água) é o que mais tira o aspecto de "tapete verde".

---

## 2. Grama

A grama do Terrain Detail não funcionou bem (deitada, invisível), então foi feita **grama instanciada própria**:

- `Scripts/CampoDeGrama.cs` + shader `Shaders/GramaTufo.shader` (vento leve, 0.03).
- Tufos de lâminas **em pé** modelados em Blender (`make_grass.py`, com LOD). Exportar FBX com `bake_space_transform=True`, senão o tufo importa deitado (rotação de 270°).
- Desenhada com `Graphics.RenderMeshInstanced` em células ao redor da câmera, 3 faixas de densidade: perto 5,5 / meio 1,5 / longe 0,45 tufos por m².
- Altura 0,30–0,58 m (o primeiro teste ficou "capim" de tão alto — **baixar o tamanho melhorou muito**, sem tirar a grama).
- Clareiras (~62%) para não ficar uniforme; versão verde e versão seca; **removida onde a camada é barro (estrada) ou pavimento**.

Para o jogo: se já usam Terrain Details ou outra solução, o essencial é altura baixa, tufos em pé, variação seca/verde, clareiras e nada de grama na estrada.

---

## 3. Água do pântano

- Malha gerada **só nas células onde o terreno está abaixo do nível** (`NivelAgua = 8,8`). Um plano único boiava no vazio fora do lago.
- Shader `AguaSimples.shader`: cor turva/esverdeada (pântano), normal fraca, usa depth texture (requer **Depth Texture ligada no URP**) para escurecer com a profundidade.
- Decoração: juncos e vitórias-régias (`make_trilha.py`), árvores mortas/escuras na borda.

---

## 4. Pedras e a escadaria dos terraços

Foi a parte mais iterada. O que **não** funcionou: pedras soltas, e faixa de malha lisa com textura (parecia concreto). O que funcionou:

- **8 pedras modeladas** (`make_pedras_muro.py`): cubo subdividido, bem arredondado, ruído leve, poucos lascados, flat shading (~108 tris). Textura Rock030 cinza (tint 2.0 para clarear), tamanho grande (~0,85–1,45 m) para parecer antiga e empoeirada.
- Muro de pedra seca montado em **5 fileiras alternadas** (juntas desencontradas), cada pedra com escala/inclinação/UV aleatórios, pedras de remate achatadas no topo, **entulho** ao pé (28%) e um **miolo escuro** atrás para fechar vãos (também é o collider).
- Tudo **fundido em poucas malhas** (`Fundidor`, ~60 mil vértices cada, 1 material) → ~1950 pedras = poucos draw calls. Não usar GameObject por pedra.
- **Escadas** nas 4 rampas de cada anel: degraus de ~0,2 m calculados a partir da altura do terreno, cada degrau com 3 lajes grossas lado a lado + `BoxCollider` (o `CharacterController` tem `stepOffset 0.45`, sobe sem pular).
- Cuidado com **eixos do Blender**: z é a altura. Um erro nisso deixou as lajes em pé como paredes.

Reaproveitável em estradas, ruínas, cemitérios (muros baixos) e acesso a cavernas.

---

## 5. Iluminação, céu e pós-processamento (o que dá o clima medieval)

| Item | Valor |
|---|---|
| Sol (Directional) | cor `(1, 0.70, 0.42)`, intensidade 2.6, rotação `(11°, 235°, 0)`, sombras Soft, força 0.9 — **entardecer baixo e quente** |
| Céu | HDRI `belfast_sunset_puresky.hdr` (Poly Haven, CC0) em material Skybox/Panoramic, exposição 0.65 |
| Neblina | Exponential Squared, densidade 0.0105, cor `(0.62, 0.48, 0.42)` (cor quente que casa com o sol) |
| Tonemapping | ACES |
| Bloom | threshold 0.9, intensidade 0.55, tint quente |
| Color Adjustments | exposição −0.1, contraste +14, saturação −14, filtro `(1, 0.93, 0.84)` |
| Vignette | 0.32 / suavidade 0.55 |
| Film Grain | Thin1, 0.18 |
| Fogueiras/tochas | Point Light laranja (range 11–16, intensidade 4.5–7) com script `FogoLuz.cs` (flicker), sem sombra |

Dessaturar um pouco + neblina colorida + sol baixo é o que mais muda o visual. Para o ciclo dia/corrupção do roteiro, esses números (cor do sol, neblina, exposição, saturação) são os parâmetros a animar.

---

## 6. Como aplicar no mapa do jogo (ordem sugerida)

1. **Iluminação/pós** primeiro (seção 5): custa pouco e muda tudo. Copiar o Volume e os valores.
2. **Camadas do terreno** (seção 1): trocar texturas e pintar por declive/altura/umidade; adicionar estrada de barro e lama.
3. **Grama** (seção 2): substituir a atual pela instanciada, em pé e baixa.
4. **Água** (seção 3) onde houver lago/rio, sempre com relevo de ambos os lados.
5. **Pedras/escadas** (seção 4) nos pontos de transição de altura.
6. Relevo extra (terraços/colinas) por último, pois mexe no heightmap e em tudo que está em cima.

## 7. Limitações conhecidas (honestidade)

- Só validei por **fotos automáticas** e smoke test em Play Mode; a escada e o movimento **não foram percorridos com input real** — testar a subida das rampas andando.
- As pedras do muro ainda parecem escuras/uniformes de longe: falta variação de cor e musgo.
- Os 4 ângulos de rampa são fixos; nas rampas o muro abre e as escadas ficam isoladas, sem corrimão/laterais.
- Prédios são blocos; árvores são cartões de folha (as árvores Poly Haven pesadas estouraram a memória no Blender e foram descartadas).
- Não há caverna, cemitérios, arena nem ciclo dia/noite — são ideias do roteiro ainda não feitas.
- Texturas e HDRI são CC0 (ambientCG / Poly Haven), podem ser usadas no jogo.

## 8. Arquivos-fonte (fora do projeto Unity, em `C:\Users\Thiago\ambiente\`)

`scripts/make_grass.py`, `make_trees.py`, `make_buildings.py`, `make_trilha.py`, `make_pedras_muro.py`, `round_stones2.py` (Blender, rodar com `blender -b -P script.py -- pasta_saida`) e `unity/` (cópia dos Editor/Scripts/Shaders).

---

## 9. Kit Stylized Nature MegaKit (Quaternius, CC0) — teste de mistura

`Editor/MontarKit.cs` usa o kit para **árvores** (CommonTree, Pine, TwistedTree, DeadTree), **pedras** grandes (Rock_Medium), seixos, **arbustos, samambaias, flores, cogumelos** e a **trilha de pedras chatas** (RockPath) na estrada. Mantido do que já existia: terreno, grama própria, água, muro de pedras + escadas, vila, juncos/vitórias-régias, troncos/galhos do Poly Haven.

- Modelos em `Assets/Ambiente/Kit/Modelos` (pasta "FBX (Unity)" do kit), texturas em `Kit/Texturas`.
- Materiais URP criados por código (`PrepararKit`), trocando pelo nome do material do FBX; folhas com alpha clip + tint mais fosco para o clima medieval.
- Cada item é escalado por **altura alvo em metros** (as escalas do kit variam), com densidade por bioma `[campo, floresta, pântano, seco]`.
- Para voltar ao visual anterior: variável de ambiente `AMBIENTE_SEM_KIT=1` na hora de rodar `Montar`.
