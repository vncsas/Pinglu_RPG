# Ambientação extra (só acrescenta; nada do mapa existente foi removido ou alterado)

- Cena: `Assets/Scenes/SampleScene_Ambientacao.unity` (cópia da SampleScene + ambientação). A SampleScene original não foi alterada.
- Menu `Pinglu > 71. Ambientação extra: grama e pedras` aplica na cena aberta; `72` remove.
- Grama: `Scripts/PingluCampoDeGrama.cs` + `Shaders/PingluGramaTufoInstanciada.shader` (tufos em pé, GPU instancing em volta da câmera, não usa Terrain Details nem pacote novo).
  Nasce por camada de textura do Terrain; estradas, barro, areia, pedra, ruas/praça (mascara `MascaraGrama.asset`) e colliders ficam limpos.
- Pedras: Stylized Nature MegaKit (Quaternius, CC0, licença em `Texturas/License_Standard.txt`): pedras grandes, seixos e pedras de trilha na beira das estradas.
- Tamanho do mapa (1000 x 1000) e objetos existentes: intocados.
