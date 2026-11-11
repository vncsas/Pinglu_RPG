# Ambientação extra (só acrescenta; nada do mapa existente foi removido ou alterado)

- Cena: `Assets/Scenes/SampleScene_Ambientacao.unity` (cópia da SampleScene + ambientação). A SampleScene original não foi alterada.
- Menu `Pinglu > 71. Ambientação extra: grama e pedras` aplica na cena aberta; `72` remove.
- Grama: `Scripts/PingluCampoDeGrama.cs` + `Shaders/PingluGramaTufoInstanciada.shader` (tufos em pé, GPU instancing em volta da câmera, não usa Terrain Details nem pacote novo).
  Nasce por camada de textura do Terrain; estradas, barro, areia, pedra, ruas/praça (mascara `MascaraGrama.asset`) e colliders ficam limpos.
- Pedras: Stylized Nature MegaKit (Quaternius, CC0, licença em `Texturas/License_Standard.txt`): pedras grandes, seixos e pedras de trilha na beira das estradas.
- Tamanho do mapa (1000 x 1000) e objetos existentes: intocados.

## Chao novo (v2)
- A cena usa uma COPIA do terreno (`Assets/Ambientacao/Terreno/Terreno_Ambientacao.asset`); o `Assets/New Terrain.asset` original nao foi alterado.
- Capim antigo (billboards) removido so nessa copia; a grama agora e a instanciada. As folhas do chao (laranja/vermelha) continuam.
- Texturas novas nas camadas (ambientCG, CC0): campo (Grass004), mata (Ground106), folhas (ScatteredLeaves009), lama, pedra, cascalho.
- Estradas fora da vila viram barro (camada `Barro`, mais larga e irregular); as ruas de pedra da vila ficam como eram.
- Manchas de folhas caidas em volta das arvores.
- Folhas do chao (detalhes 3 e 4 do terreno): so permanecem perto de arvores; o resto sai.
- Calcamento da vila: ruas e praca refeitas com ~1600 lajes modeladas (redondas e quadradas-arredondadas, `Pavimento/Calc_*.fbx`) sobre chao de terra; seixos nas bordas. O "papel de pedra" original foi trocado de material so nessa cena.
- Relevo: colinas suaves (ate ~5 m) SO nas areas vazias (longe de construcoes, estradas, agua e vila), aplicado na copia do terreno.
- Plantas do kit (arbustos, samambaias, flores, cogumelos; sem arvores) nas areas vazias.
- Relevo v2: aplicado no MAPA TODO (colinas/vales em 3 escalas; mais forte onde o mapa e vazio). Fica em zero (com transicao de 30 m) so em torno da vila, castelo, cemiterio, montanhas, calcadas e pontos de jogo. ~450 objetos pequenos sao reassentados no novo chao. Rode sempre a partir da SampleScene ORIGINAL (nao reaplique numa cena ja processada).
- `PingluAmbientacao_Analise` (editor): relatorio somente-leitura do mapa (inclinacao, camadas, ocupacao, desempenho).
- Pavimento da vila (v3): em vez de pedras modeladas, usa o material PBR PavingStones149 (ambientCG) nas ruas e na praca, com normal map, oclusao e paralaxe. Sem pedras soltas.
