# Ambiente de teste (Pinglu RPG)

Mapa de teste usado para validar **terreno, grama, pedras, agua, iluminacao e vila medieval** antes de levar para o jogo real.
Nada daqui e conteudo final: serve de referencia de tecnicas.

- `README_AMBIENTE.md`: explica cada tecnica e como aplicar no mapa do jogo (ordem sugerida, valores de iluminacao/pos-processamento, limitacoes).
- `ANALISE_DO_MAPA.md`: analise do mapa real (`SampleScene`), o que foi aplicado e o que falta. A implementacao no jogo esta na branch `ambientacao-terreno`.
- `Projeto/`: parte do projeto Unity (6000.5.6f1, URP). Copie `Projeto/Assets/Ambiente` para um projeto URP; a cena e `Assets/Ambiente/Teste_Terreno.unity`.
  Os geradores estao em `Assets/Ambiente/Editor/` (`MontarAmbiente.Montar`, `MontarAmbiente.Fotos`).
  O personagem usado no teste (`Assets/CavaleiroSombrio`) NAO esta aqui; use o do jogo ou deixe o `Jogador` sem modelo.
- `fontes/`: scripts do Blender que geram os modelos procedurais (grama, arvores, predios, pedras do muro, lajes, pavimento).
- Texturas: ambientCG (CC0). Modelos/HDRI: Poly Haven (CC0). Stylized Nature MegaKit: Quaternius (CC0).
