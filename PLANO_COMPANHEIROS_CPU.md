# Plano: grupo de 4 aventureiros com companheiros CPU

Objetivo: o jogo sempre tem o grupo completo (4 personagens). Quem joga solo escolhe um personagem e os outros
são controlados por CPUs. Isso faz o "vocês, aventureiros" do roteiro fazer sentido.

> Estado: fases **0 a 6 implementadas** (compilam; ainda sem teste no Unity). Fase 7 (multiplayer) fica para depois da história completa.
> Arquivos: `EntradaDoPersonagem.cs`, `EntradaCPU.cs`, `Rede/GrupoDeAventureiros.cs`, `Rede/CerebroCompanheiro.cs`, `Combate/PerigoNoChao.cs`. Teclas: G reunir, Y ajudar, T atacar alvo da mira.

---------------------------------------------------------------------------------------------------

## 1. Regras do jogo (decisões de design)

| Tema | Regra proposta |
|---|---|
| Tamanho do grupo | 4 personagens, sempre (uma ficha em `Personagens/Jogadores/Fichas` por personagem). |
| Solo | O jogador escolhe 1; os outros 3 entram como CPU, automaticamente, sem repetir o escolhido. |
| Com amigos (rede) | Cada humano escolhe um personagem diferente; as vagas que sobram viram CPU. Se um humano entra depois, a CPU daquela classe sai. |
| Escolha das CPUs | Primeiro completa os papéis que faltam: **linha de frente** (Tanque/Cavaleiro), **cura** (Curandeiro), **dano à distância** (Maga), **dano corpo a corpo**. |
| Portal | Ao clicar em Jogar, todo o grupo entra no portal e sai junto (o palco de seleção já tem `animacaoAndar` por ficha). |
| Seguir | CPU nunca se afasta do jogador humano mais próximo quando o grupo viaja. |
| Lutar | Em batalha, CPU parte para cima dos inimigos, mas só dentro de um "raio de combate" em volta do humano. |
| Apoio | Curandeiro cura todos (humano e CPUs). Todo mundo olha a vida dos outros e socorre quem está mal. |
| Morrer | CPU não some: fica **caída** por ~15 s e levanta se o Curandeiro estiver vivo ou quando a luta acabar. (Decidir: ver perguntas.) |
| Equilíbrio | CPU tem reação humana (0,2 a 0,4 s) e causa ~80% do dano, para o jogador continuar sendo o protagonista. |

---------------------------------------------------------------------------------------------------

## 2. Ponto central da arquitetura: separar "quem decide" de "quem faz"

Hoje o `PlayerLocomotion` lê o teclado direto (`Input.GetAxis`, `Input.GetMouseButtonDown(0)`, `Input.GetKey(Q)`,
Shift, Ctrl, Espaço, H…), e o `VooTanque` lê F/Espaço/C. Isso impede uma CPU de usar o mesmo corpo.

**Solução:** uma camada de entrada.

```
EntradaDoPersonagem (componente abstrato)
  Vector2 Mover; bool Correr; bool PularApertou; bool RolarApertou;
  bool AtaqueApertou; bool DefenderSegurando; bool GuardarSacarApertou;
  bool HabilidadeApertou(int id); Vector3? DirecaoDeMira;

EntradaHumana : EntradaDoPersonagem   -> lê Input.* (comportamento igual ao de hoje)
EntradaCPU    : EntradaDoPersonagem   -> preenchida pelo CerebroCompanheiro
```

Por que isso é o mais importante:
- A CPU usa **exatamente** o mesmo corpo, animações, combos, cooldowns, hitboxes e especial do personagem.
  Ela "sabe o que o personagem faz" porque aciona as mesmas coisas que o jogador acionaria.
- Habilidades por Animation Event (Cura, Magia) continuam funcionando: a CPU dispara o mesmo trigger do Animator.
- Ficam fora da CPU: câmera (`ThirdPersonCameraFollow` só segue o humano local), HUD pessoal, cursor.

Arquivos que mudam nessa etapa (sem mudar o jogo): `Playerlocomotion.cs` (linhas ~180–340),
`VooTanque.cs`, `CombateJogador.cs`, e quem mais lê `Input` do personagem.

---------------------------------------------------------------------------------------------------

## 3. Componentes novos

| Arquivo | Função |
|---|---|
| `GrupoDeAventureiros` (estático) | Lista de membros (humanos + CPUs), humano principal, composição automática, spawn, `Membros`, `Vivos`, `Mais Ferido()`. Substitui as buscas `FindObjectsByType<PlayerLocomotion>` dos inimigos. |
| `EntradaDoPersonagem`, `EntradaHumana`, `EntradaCPU` | Camada de entrada (seção 2). |
| `CerebroCompanheiro` | Máquina de estados da CPU (seção 4). |
| `PerfilDeCPU` (ScriptableObject por ficha) | Dados do papel: distância ideal, alcance, lista de habilidades (id, condição, recarga, custo de especial), prioridade de alvo, quando recuar. |
| `PercepcaoDaCPU` | Enxerga inimigos, aliados, perigos. Atualiza ~5x por segundo (não todo frame). |
| `MovimentoDaCPU` | Anda até um ponto (steering + desvio de obstáculos), formação, teleporte de resgate. |
| `PerigoNoChao` (estático) | Chefes e inimigos registram "vou bater aqui, raio R, em T segundos". A CPU desvia/rola. |
| `HudDoGrupo` | Retratos pequenos com barra de vida dos 4 (canto da tela) e nome sobre a cabeça das CPUs. |
| `FalasDoGrupo` (opcional) | Balões curtos ("Cuidado!", "Curando!") reaproveitando o estilo do `DialogoUI`. |

---------------------------------------------------------------------------------------------------

## 4. Cérebro da CPU (estados)

```
Parado/Cena  -> durante cutscenes e diálogos: para, olha para quem fala
Seguir       -> viaja com o grupo (padrão fora de luta)
Combater     -> há inimigo no raio de combate
Apoiar       -> aliado precisa de ajuda (curar, proteger, provocar)
Esquivar     -> há PerigoNoChao chegando (rolar/sair da área); prioridade máxima
Caida        -> sem vida; espera reviver
```

Prioridade a cada decisão: `Cena > Esquivar > Apoiar (se crítico) > Combater > Seguir`.

### 4.1 Seguir (nunca sair de perto)
- Formação em arco **atrás/ao lado** do jogador (cada CPU tem um "slot" fixo, girando com a direção dele): 2,5 a 5 m.
- Velocidade acompanha o jogador: ele anda → anda; ele corre → corre; ele para → para (zona morta de 2 m para não ficar tremendo).
- Distância máxima de 12 m. Passou de 20 m, ou ficou preso mais de 4 s → **teleporta** para um slot atrás do jogador, fora do campo da câmera.
- Portal, escadas e castelo: ao jogador mudar de área, o grupo vai junto (teleporte para os slots).

### 4.2 Combater
- **Raio de combate** = 18 m em volta do jogador humano mais próximo. Inimigo fora dele é ignorado (evita a CPU correr atrás de um esqueleto até longe).
- Escolha de alvo: pontua `ameaça ao grupo` (quem está batendo no Curandeiro/ferido > quem está mais perto > mais fraco). Cada CPU evita o mesmo alvo das outras quando há alternativa.
- Terminou a luta (sem inimigos no raio por 2 s) → volta a Seguir.
- Chefes: usam os mesmos estados, mas `Esquivar` fica mais sensível e Curandeiro/Maga ficam longe do alcance do golpe.

### 4.3 Papéis (cada um com `PerfilDeCPU`)

| Personagem | Distância | Comportamento | Habilidades |
|---|---|---|---|
| **Cavaleiro** (corpo a corpo) | 1,8 m do alvo | Vai direto no inimigo, combo de 3 golpes, defende (botão direito) se vida < 40% ou chefe prestes a bater, rola para desviar. | Combo/golpe pesado, especial quando a barra enche. |
| **Tanque** (linha de frente) | À frente do grupo | Atrai inimigos: se coloca entre inimigos e Curandeiro/Maga; defende; usa o `Redirecionar` de dano dele para proteger aliado ferido. Voo (`VooTanque`) só se o roteiro/combate pedir, não por conta própria. | Escudo, provocação, golpe de área. |
| **Curandeiro** | 6 a 8 m, atrás | Prioridade: curar. Escolhe o aliado de menor `Vida.Fracao` (peso maior para humano e para quem está caído-quase), anda até ficar no `raio` da cura (7 m) e dispara a cura. Se ninguém precisa, ataca fraco à distância. | Cura (Animation Event `LancarCura`), cura em área, reviver. |
| **Maga** | 8 a 12 m | Mantém distância ("kite"): se inimigo chega a < 4 m, recua e/ou usa barreira. Esfera arcana no alvo da frente, raio (`SoltarRaio`) em linha de inimigos. Se um aliado está sendo atacado, mira no atacante dele. | Esfera, raio, barreira (protege aliado crítico). |
| **Cavaleiro Sombrio** | Corpo a corpo | Mesmo esquema do Cavaleiro com perfil mais agressivo. | (definir quando entrar no grupo) |

### 4.4 Ajuda mútua (o que o usuário descreveu: "se a bruxa está mal, ele ajuda")
- Todo membro publica em `GrupoDeAventureiros` seu estado: vida %, quem está batendo nele, se está caído.
- Alerta quando alguém fica **< 35% de vida**: 
  - Curandeiro: vira prioridade 1.
  - Tanque: provoca os inimigos que estão batendo no ferido.
  - Maga/Cavaleiro: trocam de alvo para o atacante do ferido.
  - O próprio ferido: recua para trás do Tanque e usa defesa/barreira.
- Alguém **caído**: Curandeiro corre para reviver (se a luta permitir); sem Curandeiro, outro CPU revive quando a área está limpa.
- Humano cai: as CPUs entram em modo de proteção por alguns segundos.

---------------------------------------------------------------------------------------------------

## 5. Integração com o que já existe

| Sistema | O que muda |
|---|---|
| **Inimigos** (`InimigoEsqueleto`, `ChefeGuardiao`, `InimigoCavaleiro`) | Hoje procuram `PlayerLocomotion`. Passam a usar `GrupoDeAventureiros.Vivos` para escolher alvo (também atacam CPUs). Chefes registram `PerigoNoChao` nos golpes grandes. |
| **Seleção/portal** (`PalcoSelecao`, `MenuRede`, `SalaDeEspera`, `PortalDoMundo`) | Ao confirmar, `GrupoDeAventureiros` monta os 3 companheiros e eles andam para o portal e saem juntos. |
| **Campanha/cutscenes** | CPUs ficam em modo `Cena` (param e olham). Na cena do roubo só o humano estica a mão; os outros assistem e reagem. `F1/F3/F4` teleportam o grupo inteiro junto. |
| **Diálogos** (`DialogoUI`) | Retrato mostra quem fala; opção de mostrar o grupo em miniatura. O roteiro usa "vocês" sempre, e fica coerente. |
| **Vida** (`Vida.cs`) | Já tem `Reviver`, `Curar`, `Redirecionar`, `FiltroDeDano`; reaproveita. Falta só o estado "caído". |
| **Minimapa/HUD** | Ícones das CPUs; `HudDoGrupo`. |
| **Multiplayer** | CPUs são **do host** (igual boss/campanha/cutscenes, que já são host-only). Nasce como NetworkObject no host, posição sincronizada como os outros jogadores. |

---------------------------------------------------------------------------------------------------

## 6. Movimento: decisão técnica

O mundo é grande (terreno 1000×1000, malhas mescladas) e **não existe NavMesh no projeto**.

- **Opção A (recomendada para começar):** steering próprio com `CharacterController` (o mesmo corpo do jogador): vai em linha reta ao destino, desvia obstáculos com `SphereCast`, desvia de buracos/água por raycast para baixo, e usa o **teleporte de resgate** quando trava. Seguir um jogador que já anda por lugares livres funciona bem assim, e não precisa de bake.
- **Opção B (depois, se precisar):** pacote *AI Navigation* + bake do terreno. Melhor em castelo/cemitério com paredes, mas o bake em mapa 1 km² com malhas mescladas é pesado e pode falhar às cegas.

Plano: começar com A (com teleporte), e só migrar para B nas áreas onde a CPU travar.

---------------------------------------------------------------------------------------------------

## 7. Fases de implementação (cada uma testável)

| Fase | Entrega | Teste |
|---|---|---|
| **0. Camada de entrada** | `EntradaDoPersonagem` + `EntradaHumana`; `PlayerLocomotion`, `CombateJogador`, `VooTanque` passam a usá-la. **O jogo continua idêntico.** | Jogar normalmente: andar, atacar, rolar, defender, curar, magia, voar. Nada mudou. |
| **1. Grupo e spawn** | `GrupoDeAventureiros`, composição automática, CPUs nascem ao lado do jogador, paradas, com nome/vida acima da cabeça; `HudDoGrupo`. | Apertar Jogar solo → 3 personagens aparecem junto, sem repetir o seu. |
| **2. Seguir** | `MovimentoDaCPU`, formação, velocidade, teleporte, entrada junto pelo portal. | Andar/correr pelo mapa, parar, entrar num prédio: grupo acompanha. |
| **3. Combate básico** | Percepção, alvo, leash, ataque corpo a corpo e à distância; inimigos passam a atacar também as CPUs. | Onda de esqueletos: todos lutam e voltam ao fim. |
| **4. Habilidades por papel** | Perfis do Cavaleiro, Tanque, Curandeiro, Maga. | Ferir uma CPU: Curandeiro cura. Maga mantém distância. Tanque protege. |
| **5. Ajuda mútua e perigo** | Alerta de vida baixa, `PerigoNoChao`, estado caído/reviver. | Boss do cemitério: CPUs desviam do golpe e socorrem. |
| **6. História** | Modo `Cena`, F1/F3/F4 levam o grupo, retrato/falas do grupo, roteiro com "vocês". | Rodar roubo do cristal e castelo com o grupo presente. |
| **7. Multiplayer** | CPUs no host, vagas vazias viram CPU, humano que entra substitui CPU. | 2 PCs: um humano + 2 CPUs; entra o segundo e uma CPU sai. |

---------------------------------------------------------------------------------------------------

## 8. Riscos e como lidar (o trabalho é às cegas, sem Unity aberto)

- **Fase 0 mexe no código mais sensível do jogo** (movimento e combo). Mitigação: refactor mecânico, só trocar `Input.X` por `entrada.X`, sem mudar lógica; o usuário testa antes de avançar.
- **CPU travando em obstáculo:** teleporte de resgate + log na tela do estado da CPU (texto sobre a cabeça: `Seguir`, `Combater: esqueleto`, `Curar: Maga 40%`). Ajuda o usuário a mandar print do problema.
- **Animation Events (Cura, Magia):** só tocam se o Animator estiver certo; a CPU usa os mesmos triggers e vai herdar o mesmo comportamento do jogador (se funciona para o jogador, funciona para ela).
- **Desempenho:** percepção a ~5 Hz e buscas por lista (`GrupoDeAventureiros`) em vez de `FindObjectsByType`.
- **Escopo:** é um sistema grande (várias sessões). As fases 0–4 já entregam o essencial; 5–7 refinam.

---------------------------------------------------------------------------------------------------

## 9. Decisões do usuário (respondidas)

1. **Grupo de 4**: Cavaleiro Sombrio, Morgana, Tanque, Curandeiro (as 4 cartas da seleção). O Cavaleiro "antigo" é só inimigo.
2. **CPU morre**: fica caída no chão (dificuldade). Só volta quando um **BOSS é derrotado**: nesse momento todos recuperam a vida toda e quem estava caído levanta. (Sem reviver durante luta comum.)
3. **Comandos**: sim. Teclas para mandar nas CPUs, incluindo **"Ajudar"**: a CPU mais próxima do alvo vem socorrer (humano ou CPU). Outros comandos a definir (venham / atacar alvo / segurar posição).

(Seção 9 original, só histórico:)

1. **Tamanho do grupo:** 4 (as 4 fichas que existem) ou 5 (há também o Cavaleiro Sombrio)?
2. **CPU morre:** fica caída e levanta (proposto), ou o jogador precisa reviver, ou só volta na próxima onda?
3. **Comandos:** quer teclas para mandar as CPUs (ex.: G = "venham", T = "atacar o alvo", Y = "segurar posição")? Fica fora das fases 0–7 se não.
4. **Multiplayer com 2+ humanos:** as CPUs completam sempre até 4, certo?
5. **Quando um personagem ainda não existe** (fichas 2–4 aparecem como "personagem 2/3/4" no menu): as CPUs só podem ser dos que estiverem `disponivel`.
