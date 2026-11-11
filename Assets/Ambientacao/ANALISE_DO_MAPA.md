# Analise do mapa (SampleScene original) e o que foi feito

## Numeros do terreno (1000 x 1000 m, altura max 600 m)
- Altura 0,9 a 82 m (media 23,6). Inclinacao: <2° 13% | 2-5° 18% | 5-10° 31% | 10-20° 32% | 20-35° 5% | >35° 1%.
- Rugosidade media (desvio de altura numa janela de 100 m): 4,4 m -> o mapa inclina, mas em rampas suaves e parecidas, sem vales, penhascos, platos nem terracos.
- Camadas de textura (16): MataSombria 32,5% | CampoVivo 21% | CampoSeco 12% | Mata 11% | Campo 9% | MusgoSombrio 6,6% | Rocha 4% | caminhos 2,5% | TerraMorta 1%.
  Grass_Dry, Grass_Moss, Muddy, Black_Sand, Heather e Pebbles estavam com 0%: o chao era quase todo igual.
- 10.556 arvores no Terrain, 5 prototipos de detalhe (3 de capim "CapimBaixo", 2 de folhas no chao).

## Ocupacao (objetos+arvores por celula de 40 m)
- ~29% das celulas com menos de 15 objetos. A faixa a LESTE (x > ~720) e a mais vazia e lisa; o resto esta coberto de mata.
- Pontos fortes ja existentes: vila (ruas, praca, casas, colheita), castelo, cemiterio enorme (3.436 pecas), acampamentos, clima por bioma (ClimaDosBiomas: nevoa/vento/efeito diferentes em Floresta Sombria, Pantano, Terras Queimadas, Reino Destruido e Floresta Assombrada).

## Agua
- Nao existia nenhum corpo d'agua: o bioma "Pantano" (centro ~190, 490) so tinha arvores secas. Agora ha o Lago do Pantano.

## Iluminacao e render (o que mais afasta o jogo do mapa de teste)
- URP `PC_RPAsset`: **renderScale 0,55** (o jogo renderiza a 55% da resolucao: e o motivo do aspecto serrilhado/pixelado de grama, texturas e pedras), sombras so ate **20 m**, 2 cascatas, sem MSAA. HDR desligado.
- Pos-processamento: tonemapping Neutral, bloom, vignette, split toning (azul/sombras, quente/luz), film grain, color adjustments (saturacao -22). Sol azulado de intensidade 1,13 e ambiente Trilight azul-acinzentado: tudo muito frio e uniforme. No mapa de teste: ceu HDRI de por do sol, nevoa quente, ACES e sol de 2,6.
- Nevoa exponencial 0,007 cinza-azulada + nevoa extra por bioma.

## Desempenho (contagens)
- 4.183 LODGroups, 990 MeshColliders, 66 luzes pontuais, 171 particulas, treeDistance 600, basemapDistance 1000, pixelError 4.

## Visual por area (fotos na cena nova)
- Vila: ruas/praca eram textura lisa ("papel de pedra"), casas de bloco regular (cara de Minecraft); ruas em malha elevada em relacao ao terreno.
- Castelo/Reino Destruido: muros de tijolo liso e pecas de bloco retas, textura repetida; chao seco e vazio.
- Cemiterio: muito denso, bom como cenario fechado.
- Floresta Sombria: tronco/galho repetidos, chao uniforme e escuro.
- Leste: campo liso, pouca coisa.

## O que foi implementado na cena `SampleScene_Ambientacao` (a original nao foi alterada)
Relevo no mapa todo (com objetos reassentados), chao com 6 camadas novas em manchas, estrada de barro, grama instanciada (sem capim antigo), pedras do kit, plantas do kit, lago do Pantano (agua com o shader do mapa de teste), pavimento PBR na vila, casas com pedra antiga, terreno encostado nas ruas, atalhos 1-9/0 e janela `Pinglu > Atalhos do mapa`.

## Ainda NAO feito / proximos passos
1. Subir o renderScale (0,55 -> 0,8+) e as sombras (20 -> 60 m): arquivo global `Assets/Settings/PC_RPAsset.asset`, afeta todo mundo (pedir ok do grupo).
2. Aquecer o sol e a nevoa ao entardecer/amanhecer (ciclo ja existe em ClimaDeDia) e usar ACES.
3. Estradas: curvas, sulcos, placas, cercas e pedras na beira.
4. Terraco de pedra seca com escadas na vila.
5. Castelo e muros: trocar o tijolo liso por pedra PBR antiga (como nas casas).
6. Montanha do canto sudeste (bloco escuro e chapado).
7. Variedade de arvores na Floresta Sombria (hoje poucas formas repetidas).
