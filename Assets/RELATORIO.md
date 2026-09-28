# Relatório — Mine (clone de Minecraft em Unity)

![Mundo gerado, rodando no executável Windows](../Docs/screenshots/01_mundo_build_standalone.png)

## Resumo

Jogo de blocos no estilo Minecraft feito do zero em **Unity 6.3 (6000.3.19f1) + URP 17**, com o **Input System** novo.
Os **7 requisitos obrigatórios** e os **8 bônus** do README estão implementados. Não foi usado nenhum asset externo:
texturas, sons, música, modelos dos animais, ícones e sprites da interface são gerados por código na inicialização,
e todos os shaders foram escritos à mão.

- O projeto compila **sem erros nem avisos** no editor e no build Windows 64-bit.
- **25 testes automatizados** (EditMode) passam.
- Tem **câmera em terceira pessoa** (costas e frente) com um **personagem próprio**, cuja skin é pintada por código.
- O build roda a **120 FPS estáveis** (limitado à taxa do monitor), com ~1,1 ms de CPU na thread principal e ~2 ms de GPU numa RTX 3070 Laptop.

## Como rodar

| Onde | Como |
|---|---|
| Editor | Abrir `Assets/Scenes/Mine.unity` (única cena do build) e dar Play. No menu: **Novo mundo** (seed opcional; vazio = aleatória) ou **Carregar mundo salvo**. |
| Executável | `Builds/Windows/Mine.exe` (build Windows 64-bit já gerado). |
| Testes | Window → General → Test Runner → EditMode → Run All (25 testes). |

Parâmetros de linha de comando, úteis para testar rápido:

| Parâmetro | Efeito |
|---|---|
| `-autostart [seed]` | Pula o menu e cria um mundo novo. |
| `-autoshot arquivo.png` | Salva um screenshot 4 s depois de o mundo carregar. |
| `-autoquit` | Fecha o jogo 8 s depois de carregar e registra FPS por segundo no `Player.log`. |
| `-perfprobe` | Registra no log, a cada segundo, onde vai o tempo do frame (marcadores do profiler + CPU/GPU). |

## Controles

| Tecla | Ação |
|---|---|
| **W A S D** / **Mouse** | Andar / olhar |
| **Espaço** | Pular; nadar para cima; subir voando |
| **Shift** | Agachar (não cai das bordas); descer voando |
| **Ctrl** ou **W duas vezes** | Correr |
| **Botão esquerdo** (segurar) | Quebrar bloco (com rachaduras) / atacar animal |
| **Botão direito** | Colocar bloco; abrir a bancada de trabalho |
| **Botão do meio** | Selecionar o bloco mirado |
| **1–9** / **Scroll** | Escolher o slot da hotbar |
| **E** | Inventário e criação (crafting) |
| **G** | Alternar modo Criativo / Sobrevivência |
| **V** | Câmera: 1ª pessoa → 3ª pessoa (costas) → 3ª pessoa (frente) |
| **F** ou **Espaço duas vezes** | Voar (modo criativo) |
| **T** (segurar) | Acelerar o tempo (ver dia e noite rápido) |
| **F1** / **F3** | Ajuda / informações de debug |
| **F5** / **F9** | Salvar / carregar |
| **Esc** | Pausa (opções: distância de visão, sensibilidade, modo, música, salvar e sair) |

## O que foi feito

### Requisitos obrigatórios

| # | Requisito | Implementação |
|---|---|---|
| 1 | Mundo voxel procedural com relevo e seed aleatória | Ruído Perlin com seed própria (`Perlin.cs`) em camadas: continentes, colinas, montanhas com cristas (ridged) e máscara, rios que cavam vales. Biomas por temperatura e umidade: planície, floresta, floresta de bétulas, deserto, praia, oceano/lagos, montanhas e picos nevados. Seed aleatória ou digitada no menu (texto vira número por hash estável). `TerrainGenerator.cs` |
| 2 | Chunks com malha combinada, sem faces escondidas | Chunks de 16×128×16 e uma malha por chunk com 3 sub-malhas (opaco, recorte alpha e água). Só são geradas faces cujo vizinho não é opaco, inclusive através da borda com o chunk vizinho. `ChunkMesher.cs`, `VoxelWorld.cs` |
| 3 | Primeira pessoa com mouse e WASD, pulo, gravidade e colisão | Física própria de AABB contra a grade de voxels, resolvida eixo a eixo e com sub-passos (sem colliders da Unity): gravidade, pulo, correr, agachar sem cair das bordas, nadar e voar. `PlayerController.cs`, `VoxelPhysics.cs` |
| 4 | Quebrar (esquerdo) e colocar (direito), com contorno no bloco mirado | Raycast DDA na grade. Contorno preto em wireframe e rachaduras progressivas enquanto o bloco quebra (tempo depende da dureza; instantâneo no criativo). O jogo não deixa colocar bloco dentro do jogador nem de um animal. `PlayerInteraction.cs`, `BlockOutline.cs` |
| 5 | Pelo menos 6 tipos de bloco | **28 tipos**: grama, terra, pedra, pedregulho, areia, cascalho, 3 troncos e 3 folhas (carvalho, bétula, pinheiro), tábuas, vidro, água, rocha matriz, neve, minérios de carvão e ferro, tijolos, tijolos de pedra, bancada de trabalho, arenito, lâmpada (emite luz), lã, grama alta e 2 flores. `Blocks.cs` |
| 6 | Hotbar com teclas 1–9 e scroll | Barra de 9 slots com ícones isométricos gerados por código, quantidade, moldura de seleção e o nome do item ao trocar. `UIManager.cs` |
| 7 | Árvores | Carvalho, bétula e pinheiro, distribuídos por bioma numa grade com deslocamento aleatório. Árvores que cruzam a borda entre chunks saem idênticas nos dois lados, porque a geração é determinística por posição. |

### Bônus (todos implementados)

| Bônus | Implementação |
|---|---|
| Ciclo de dia e noite | Céu procedural com gradiente, brilho de pôr do sol, sol e lua quadrados e estrelas que giram. A luz do céu escurece à noite. A neblina usa a mesma cor do horizonte. Um dia dura 10 min (segure **T** para acelerar). `DayNightCycle.cs`, `Sky.shader` |
| Água | Lagos, rios e oceanos no nível do mar. Água translúcida e animada, superfície rebaixada, natação, neblina e tinta azul debaixo d'água, som de mergulho. A água ocupa o buraco quando se quebra um bloco ao lado dela. |
| Cavernas | Túneis "espaguete" (interseção de dois ruídos 3D) e cavernas maiores no fundo, com ruído interpolado numa grade 4×4×4 para ficar rápido. As cavernas ficam escuras de verdade graças ao sistema de luz. |
| Chunks infinitos | Carregamento e descarregamento circular em volta do jogador. Geração e malha rodam em threads de trabalho, com prioridade pela distância. Distância de visão ajustável de 4 a 16 chunks. |
| Som gerado por código | Síntese com ruído filtrado (biquads) para quebrar, colocar, passos e batidas, por material (pedra, madeira, terra, grama, areia, cascalho, vidro, neve, folhas, lã). Vozes de porco, ovelha e vaca com formantes. Vento e grilos em loop. **Música generativa** de piano com reverb, gerada em thread de fundo. `Synth.cs`, `SoundLibrary.cs`, `MusicGenerator.cs` |
| Mobs simples | Porco, ovelha e vaca montados com caixas e peles pintadas por código. Andam, pulam degraus de 1 bloco, evitam penhascos e água, olham para o jogador, fogem quando atacados, morrem com fumaça, e a ovelha dá lã. Aparecem só na grama ao céu aberto. `Mob.cs`, `MobFactory.cs`, `MobManager.cs` |
| Inventário e crafting | 36 slots com clique, botão direito (metade ou um) e Shift+clique. 10 receitas, algumas só na bancada de trabalho. No modo criativo aparece uma paleta com todos os blocos. `Inventory.cs`, `Recipes.cs`, `InventoryView.cs` |
| Salvar e carregar | `level.json` (seed, jogador, inventário, hora, modo) e `chunks.bin` só com os chunks modificados (RLE). O resto é regenerado pela seed. Autosave ao fechar o jogo. `SaveSystem.cs` |

### Extras além do pedido

- **Câmera em terceira pessoa** (tecla **V** ou opção na pausa), com três modos como o F5 do Minecraft: primeira pessoa, terceira pessoa por trás e terceira pessoa pela frente.
  - A câmera fica a 4 blocos do olho do personagem.
  - Cinco raios em volta do eixo de visão detectam blocos no caminho: a câmera se aproxima quando há uma parede e volta suavemente quando o caminho libera.
  - Com a câmera colada no personagem, o corpo some para a cabeça não tapar a tela.
  - A mira continua exata por trás, porque os raios de interação saem do olho do personagem na direção do olhar. Pela frente a mira some, como no Minecraft.
  - A escolha de câmera fica salva nas preferências.
- **Personagem próprio (skin original)**: um explorador "sardinha" feito de caixas (cabeça, corpo, braços, pernas e mochila), com skin 64×64 pintada pixel a pixel em código. Tem cabelo castanho, olhos azuis, sorriso, moletom turquesa com uma sardinha no peito, cordões e bolso canguru, jeans, botas e mochila de couro com fivela.
  - O corpo gira seguindo a cabeça: na hora ao andar, e com zona morta de 50° parado.
  - A cabeça olha para cima e para baixo junto com a câmera.
  - Pernas e braços balançam ao andar.
  - Ao agachar, o tronco inclina para a frente.
  - O braço direito faz o golpe ao quebrar ou colocar bloco e segura o bloco selecionado.
  - O personagem é iluminado pela luz do mundo no ponto onde está, então escurece à noite e nas cavernas.
- **Iluminação no estilo Minecraft**: luz do céu e luz de blocos (níveis 0–15) propagada por flood fill, luz suave e oclusão ambiente por vértice. A lâmpada ilumina cavernas e construções à noite.
- **Minérios** em veios, grama alta e flores, nuvens em blocos que se movem com o vento.
- **Item na mão** com animação de golpe, **partículas** ao quebrar, passos com som do material do chão, sons de pouso.
- **Menu principal**, tela de carregamento com progresso, pausa com opções salvas em `PlayerPrefs`, **F3** com posição, bioma, luz, seed, chunks e mobs.
- **Testes automatizados** de lógica pura: RLE, inventário, receitas, determinismo do terreno, culling de faces, luz de céu e de lâmpada, física de raio e caixa.

## Arquitetura

```
Assets/
  Scripts/                 (assembly "Mine")
    Core/        Blocks.cs (registro + tabelas planas para as threads), MathUtil.cs (hash determinístico)
    World/       Perlin, TerrainGenerator, Chunk, ChunkMesher, WorkerPool, VoxelWorld, VoxelPhysics
    Rendering/   TextureFactory (todas as texturas), MaterialLibrary, MeshUtil, BlockOutline, BlockParticles,
                 BlockItemMesh (malha de um bloco solto, usada nas duas mãos)
    Environment/ DayNightCycle, CloudLayer
    Player/      PlayerController, PlayerInteraction, HeldItemView (bloco na mão em 1ª pessoa),
                 CameraModeController (1ª/3ª pessoa + colisão da câmera), PlayerModel (personagem animado),
                 PlayerSkin (skin pintada por código)
    Items/       Inventory, Recipes
    Mobs/        Mob, MobFactory, MobManager
    Audio/       Synth, SoundLibrary, MusicGenerator, AudioManager
    Save/        SaveSystem (+ RLE)
    UI/          UIFactory, UIManager, InventoryView (uGUI montado por código)
    Game/        GameManager (bootstrap e máquina de estados), PerfProbe (diagnóstico)
  Shaders/       Voxel, Water, Sky, Clouds, Overlay, Entity, Particle, UnlitColor (+ MineCommon.hlsl)
  Resources/Materials/   materiais base (garantem que os shaders entram no build)
  Tests/EditMode/        25 testes NUnit
  Scenes/Mine.unity      cena única: câmera, sol, volume de pós e o objeto "Game"
```

Decisões principais:

- **Threads**: um `WorkerPool` com N threads (núcleos − 2, até 6) gera e faz a malha dos chunks. A thread principal só envia as malhas prontas para a GPU, com orçamento de 5 ms por frame. Os jobs são escolhidos pela distância ao jogador. Edições têm prioridade máxima e o número de malhas em voo é limitado.
- **Mesher**: copia a vizinhança 3×3 com margem de 15 blocos (46×130×46) e calcula a luz por BFS. Codifica luz do céu, luz de bloco e AO×sombreamento da face na cor do vértice. Troca a diagonal do quad para evitar artefatos de AO.
- **Render**: shaders URP próprios, compatíveis com o SRP Batcher, e texturas num `Texture2DArray` (45 camadas de 16×16 com mipmaps e filtro point). O SSAO do renderer foi desligado porque a AO já vem pronta nos vértices. Volume de pós `MineVolumeProfile` com bloom e vinheta, sem tonemapping.
- **Física**: AABB do jogador e dos mobs contra a grade de voxels. Chunks ainda não carregados contam como parede, para ninguém cair no vazio.

## Desempenho medido

| Cenário | Resultado |
|---|---|
| Carregar mundo novo (221 chunks com malha, raio 8) | ~2 s |
| Voo a ~40 blocos/s (streaming contínuo) | 124–179 FPS no editor, sem fila acumulada |
| Build Windows (RTX 3070 Laptop, 16 threads) | 120 FPS travados no refresh do monitor; CPU ~1,1 ms, GPU ~1,5–2 ms, pior frame < 10 ms |

Um problema encontrado e corrigido no caminho: com VSync ligado, o build ficava em ~7 FPS nesta máquina, com CPU e GPU quase paradas, esperando dentro de `WaitForTargetFPS`. O `-perfprobe` mostrou isso. A solução foi desligar o VSync e limitar o FPS pela taxa do monitor (`Application.targetFrameRate`).

## Como foi verificado

- Compilação limpa no editor e no build Windows; os 25 testes EditMode passam. Dois deles cobrem a skin: as faces cabem no atlas sem sobreposição e o rosto tem olhos.
- Terceira pessoa conferida em screenshots, por trás e pela frente. Com uma parede a 1,2 bloco atrás, a câmera parou em 1,2 bloco em vez de 4. Ao voltar para a primeira pessoa, a câmera retorna ao olho, o personagem some e o bloco da mão em 1ª pessoa reaparece.
- Testes em Play Mode automatizados pelo MCP do Unity:
  - geração com várias seeds;
  - streaming ao teleportar 2000 blocos (chunks antigos são descarregados);
  - save e load: um pilar construído voltou, uma alteração feita depois do save foi desfeita e o inventário foi restaurado;
  - quebrar e colocar chamando os métodos reais: tempo de quebra pela dureza, drop no inventário, recusa de colocar dentro do jogador;
  - luz de lâmpada, telas de UI e o executável rodando sozinho com `-autostart -autoshot -autoquit`.
- **Limitação do teste**: não foi possível simular mouse e teclado reais no editor sem foco. Os controles foram validados lendo o código e chamando a lógica diretamente. Vale uma rodada manual rápida (andar, pular, quebrar, colocar, 1–9, scroll, E, Esc).

## Problemas conhecidos e limitações

- A água é estática: não escorre, só ocupa o buraco quando se quebra um bloco ao lado dela.
- Areia e cascalho não caem com gravidade.
- Folhas vizinhas não desenham a face entre elas (como o modo "rápido" do Minecraft). Vista de dentro, uma copa parece oca.
- Não há vida, dano, fome nem ferramentas. O modo sobrevivência se resume ao inventário, ao consumo de blocos e ao tempo de quebra.
- Mobs não são salvos e reaparecem perto do jogador. Às vezes um mob cai num buraco ou entra na água.
- Cada edição refaz a malha dos 9 chunks em volta, porque a luz pode mudar até 15 blocos de distância. Na borda de um chunk o vizinho pode aparecer 1 frame depois.
- A água transparente não é ordenada face a face dentro do chunk. Olhando por várias faces de água ao mesmo tempo, a mistura pode sair imperfeita.
- A altura máxima de construção é 127, e só existe um slot de save.
- Mudar o código do gerador altera o terreno de saves antigos nas partes que não foram modificadas, porque só os chunks editados são gravados.
- Sem VSync pode haver tearing. O FPS fica limitado à taxa do monitor.
- A fonte da UI é a padrão da Unity (`LegacyRuntime`), não uma fonte em pixel.
- Terceira pessoa: o personagem não projeta sombra, e agachar só muda a animação (a caixa de colisão continua com 1,8 de altura). A animação de andar não foi vista em movimento real durante os testes automatizados, porque não dava para simular o teclado; a lógica é simples, mas vale conferir jogando.
- Em primeira pessoa aparece só o bloco segurado, sem o braço do personagem.
- Durante os testes pelo MCP, o console mostrou avisos "PlayerLoop called recursively". Eles vêm da ferramenta de captura de tela do MCP e não aparecem jogando normalmente.

## Assets e pacotes

- **Nenhum asset da Asset Store nem baixado da internet.** Tudo foi gerado por código:
  - texturas e ícones: `TextureFactory.cs`;
  - sons e música: `Synth.cs` e `MusicGenerator.cs`;
  - animais e malhas auxiliares: `MobFactory.cs` e `MeshUtil.cs`;
  - personagem do jogador e a skin dele: `PlayerModel.cs` e `PlayerSkin.cs`;
  - céu, nuvens, água e blocos: shaders próprios.
- Pacotes usados: só os oficiais da Unity que já estavam no projeto (URP, Input System, uGUI, Test Framework). O "MCP for Unity", que também já estava instalado, foi usado só como ferramenta de desenvolvimento e o jogo não depende dele.

## Galeria

| | |
|---|---|
| ![Terceira pessoa, por trás](../Docs/screenshots/12_terceira_pessoa_costas.png) | ![Terceira pessoa, pela frente](../Docs/screenshots/13_terceira_pessoa_frente.png) |
| ![Rio e vale](../Docs/screenshots/02_rio_e_vale.png) | ![Vaca e flores](../Docs/screenshots/03_mob_vaca_flores.png) |
| ![Noite com lâmpadas](../Docs/screenshots/04_noite_construcao_lampadas.png) | ![Caverna iluminada](../Docs/screenshots/05_caverna_lampada.png) |
| ![Pôr do sol](../Docs/screenshots/06_por_do_sol.png) | ![Debaixo d'água](../Docs/screenshots/07_subaquatico.png) |
| ![Contorno do bloco mirado](../Docs/screenshots/08_contorno_bloco_mirado.png) | ![Partículas ao quebrar](../Docs/screenshots/09_particulas_quebra.png) |
| ![Inventário e crafting](../Docs/screenshots/10_inventario_crafting.png) | ![Debug (F3) e paleta criativa](../Docs/screenshots/11_debug_paleta_criativa.png) |
