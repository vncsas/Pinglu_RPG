# Pinglu_RPG

RPG de fantasia em 3D feito na Unity, projeto em grupo da faculdade.

## Requisitos

| Ferramenta | Versão |
|---|---|
| Unity Editor | **6000.6.1f1** (Unity 6.6). Confirme em `ProjectSettings/ProjectVersion.txt` |
| Git | qualquer versão recente |
| Git LFS | obrigatório (veja abaixo) |
| Pipeline gráfico | URP |

Use a **mesma versão do Unity** em todos os computadores. Versões diferentes reescrevem arquivos do projeto e causam conflito.

## Como acessar o projeto

O repositório é **privado**. Quem for entrar precisa:

1. Ter uma conta no GitHub.
2. Aceitar o convite de colaborador (chega por e-mail e em github.com/notifications).

### 1. Instalar o Git LFS antes de clonar

Os arquivos pesados (modelos 3D, texturas, imagens) ficam no Git LFS. Se você clonar **sem** o LFS instalado, esses arquivos chegam como pequenos arquivos de texto e o projeto abre com tudo rosa ou quebrado.

Baixe em https://git-lfs.com, instale e rode uma vez:

```powershell
git lfs install
```

### 2. Configurar seu nome e e-mail no Git

Use o e-mail da sua conta do GitHub. Se o computador também é usado para outros projetos (trabalho, por exemplo), configure só para este repositório, dentro da pasta do projeto:

```powershell
git config --local user.name "Seu Nome"
git config --local user.email "seu-email@exemplo.com"
```

### 3. Clonar o repositório

```powershell
git clone https://github.com/vncsas/Pinglu_RPG.git
```

> Evite clonar de novo sem necessidade. Cada clone baixa todos os arquivos do LFS (mais de 2 GB) e desconta da cota mensal de banda do dono do repositório. Para atualizar, use `git pull`.

### 4. Abrir no Unity

1. Abra o **Unity Hub** e clique em **Add → Add project from disk**.
2. Selecione a pasta `Pinglu_RPG`.
3. Escolha o Editor **6000.6.1f1** (instale pelo Hub se não tiver).
4. Na primeira abertura a Unity demora, porque recria a pasta `Library`. É normal.
5. Abra a cena `Assets/Scenes/SampleScene`.

## Controles do personagem

| Ação | Tecla |
|---|---|
| Andar | W A S D |
| Correr | Shift esquerdo (segurando) |
| Pular | Espaço |
| Rolar / esquivar | Ctrl esquerdo |
| Ataque leve | Botão esquerdo do mouse |
| Ataque pesado | Botão direito do mouse |
| Bloquear | Q (segurando) |

## Estrutura principal

```
Assets/
├── Personagens/
│   └── Sword and Shield Pack/   animações e Animator Controller (XBot_Animator)
├── script/                      Playerlocomotion.cs, ThirdPersonCameraFollow.cs
├── Scenes/                      cenas do jogo
├── Holotna/                     pacote de cenário (montanhas, pedras, pedregulhos)
└── Polytope Studio/             pacote de ambiente (árvores, grama, cercas)
```

## Fluxo de trabalho no dia a dia

Antes de começar a mexer:

```powershell
git pull
```

Ao terminar uma parte do trabalho:

```powershell
git add .
git commit -m "Descreva o que foi feito"
git push
```

### Regras para evitar conflito

- **Cada pessoa trabalha em uma cena diferente.** Cenas (`.unity`) são difíceis de juntar quando duas pessoas editam a mesma. O mapa fica numa cena própria e a animação em outra. Depois combinamos as duas.
- **Sempre dê `git pull` antes de começar** e `git push` ao terminar, para não trabalhar em cima de uma versão antiga.
- **Nunca apague arquivos `.meta`.** Eles guardam as ligações entre os assets. Sem eles, o projeto quebra para os outros.
- **Mova e renomeie arquivos dentro da Unity** (aba Project), nunca pelo Explorer do Windows, para o `.meta` acompanhar.
- **Feche a Unity antes do `git pull`** se o pull trouxer muitas mudanças.
- Não commite as pastas `Library`, `Temp`, `Logs` e `UserSettings`. O `.gitignore` já cuida disso.

## Problemas comuns

**Tudo rosa ou modelos 3D quebrados**
O Git LFS não estava instalado na hora do clone. Dentro da pasta do projeto, rode:

```powershell
git lfs install
git lfs pull
```

**Erro de permissão (403) ao dar push ou clone**
O Windows pode estar usando outra conta do GitHub salva. Abra o **Gerenciador de Credenciais** do Windows, procure `git:https://github.com` e remova. Na próxima vez, entre com a conta certa.

**A Unity pede para atualizar o projeto ao abrir**
Você está com uma versão do Editor diferente da do projeto. Feche e abra com a versão **6000.6.1f1**.

**`git push` recusado**
Alguém enviou mudanças antes de você. Rode `git pull`, resolva o que pedir e envie de novo.