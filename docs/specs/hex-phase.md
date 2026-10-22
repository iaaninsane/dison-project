# Spec: Fase hexagonal (estruturas interligadas da esfera)

- **Status:** aprovada (out/2026); **fase A validada** (passos 1 a 6, testados pelo dev no editor); **fase B validada** (passos B0 a B5, testados pelo dev no editor, out/2026). A fase C entra com os spawners móveis (backlog 1.4). Decisões na seção 10. Valores em teste.
- **Etapa:** 1 (mecânica). Fases A e B nesta spec; a fase C entra junto com os spawners móveis (backlog 1.4).
- **Depende de:** `docs/HANDOFF.md` (v2.0, seção 4), `docs/specs/reactive-walls.md` (paredes atuais), `docs/specs/suit-evolution.md` (câmara inicial e banco), `docs/specs/player-health-death.md` (morte). Futuro: `docs/specs/dyson-wall.md` (paredes de fronteira).
- **Referências:** `docs/reference/dyson-sphere-reference.png` (a malha da esfera) e `docs/reference/connector-cut-mockup.png` (mockup de um conector). Jogos com o mesmo plano: FTL, Heat Signature e Space Haven (estruturas vistas de cima, sem teto, com o espaço em volta); a câmera do Cocoon também olha o mundo de cima.

> **Valores em teste:** como nas demais specs da demo, os `[Export]` e os números desta spec são pontos de partida, não valores definitivos.

## 1. Objetivo

Trocar as salas isoladas por uma **fase**: um hexágono da esfera (de início, um setor dele) em que as estruturas são interligadas e o jogador voa de uma para outra sem tela de carregamento. Serve ao EXPLORAR (navegação inercial pelas estruturas) e é o palco do COMBATER contra os spawners móveis (backlog 1.4) e das paredes de fronteira (backlog 1.5).

## 2. Comportamento esperado

### Plano de jogo (H1)

- A câmera olha a esfera **de fora**, com o teto das estruturas removido (planta em corte). O hexágono fica deitado no plano do jogo, e a estrela fica **atrás do plano** (contraluz).
- Estruturas:
  - **Nó central** e **nós periféricos:** câmaras circulares (polígonos de `NodeWallSegments` lados).
  - **Conectores de nós:** corredores retos entre os centros de dois nós vizinhos.
  - **Painéis:** salas triangulares entre três nós, com os cantos cortados na largura do conector de placa.
  - **Conectores de placa:** passagens do canto de um painel até o nó daquele canto.
- **Orientação:** hexágono "deitado" (o nó periférico 1 fica a −60°, acima e à direita do centro). Todo painel tem um lado horizontal; no setor de teste, o painel aponta para cima, com a base no corredor do nó central até o nó 2.
- O vão entre as estruturas é o **espaço**. O jogador não entra nele voando (as paredes impedem); só sai para ele ejetado.

### Continuidade (H2)

- **Uma cena por fase.** O jogador voa de uma estrutura para outra sem troca de cena; a câmera o segue.
- Integridade, biomassa carregada e modo da lanterna continuam entre as estruturas (mesma cena, mesmo jogador).

### Montagem (H3)

- **Contornos por código:** fundo, piso e paredes são gerados a partir da geometria da fase (`HexPhaseLayout`), montados pelo `HexPhaseBuilder`.
- **Regra das paredes:** toda borda de piso vira parede, **menos os trechos que caem dentro de outra estrutura** (as passagens). As aberturas saem sozinhas, sem caso especial por tipo de encaixe. As paredes crescem para fora do piso (a sala mantém o tamanho da geometria) e são divididas em trechos de até `MaxWallSegmentLength` (cada trecho tem a sua irritação e o seu centro de puxão).
- **Recheio no editor:** pilares, coberturas e marcadores de cada tipo de estrutura em cenas pequenas feitas pelo dev, posicionadas pelo construtor. Ainda não existe: o setor de teste nasce vazio.
- **Paredes:** toda parede externa é fronteira com o espaço, portanto reativa (grupo `Walls`). Hoje: `WallSensitive` gerado por código, no mesmo formato das cenas (`StaticBody2D` + `ColorRect` + `CollisionShape2D`, pivô no centro), transitório até a `DysonWall` (backlog 1.5). Obstáculos internos serão rígidos.
- **Parede calma não processa:** fora da luz, a parede "dorme" (sem `_PhysicsProcess`); a luz a acorda e ela volta a dormir quando a irritação zera. Com centenas de paredes na fase, o processamento sempre ligado pesava (passo 6, seção 4).

### Paredes por tipo de estrutura (compressão e alívio)

- A irritação máxima de cada parede depende da estrutura dona: arenas (nós e painéis) menos sensíveis, corredores mais, conectores de placa ainda mais. O perigo das paredes se concentra onde está o aperto.
- Com a lanterna atual (20/s de irritação contra 15/s de recuperação), o tempo de luz contínua até ejetar é de ~`MaxIrritation` ÷ 5 segundos.

### Escopo (H4)

- **Primeira versão: setor de teste.** Um triângulo do hexágono: nó central e 2 nós periféricos, 3 conectores de nós, 1 painel e 3 conectores de placa (10 estruturas).
- **Hexágono completo:** 7 nós, 12 conectores de nós, 6 painéis e 18 conectores de placa (43 estruturas), escolhido pelo parâmetro `Shape`, sem mudar código.

### Câmara inicial e reinício (H5)

- A câmara inicial (`sala_entrada`) continua uma cena separada.
- A `PortaSaida` leva à fase. O jogador nasce na **eclusa** de um nó periférico (`EntryPeripheral`), parado e com a câmera já no lugar. A porta da eclusa (`Airlock`) fica na **parede externa** do nó (o lado oposto ao centro da fase, onde não sai corredor), virada para fora, e o jogador nasce à frente dela, também ao voltar do espaço (B3).
- A eclusa também leva de volta à câmara (**extração**) **ao tocar a porta**, como a `PortaSaida`: ao chegar, a biomassa carregada é depositada no banco (a `SalaEntrada` já faz isso a cada entrada). Se extrair sem querer virar problema nos testes, passa a exigir `interact`.
- As trocas de cena usam uma transição de tela (escurecer e clarear, `ScreenFadeDuration`): entre a câmara e a fase e também na morte. O jogo não pausa durante o escurecer.
- Morrer volta à câmara, como hoje (`RestartOnDeath`).
- **A fase recomeça do zero a cada entrada:** spawners, inimigos e orbes novos. Banco, níveis das melhorias e modo da lanterna continuam no `GameState`.
- A integridade começa cheia ao entrar na fase e se mantém entre as estruturas.

### Estrutura atual

- A geometria responde, por conta e sem física, em qual estrutura está um ponto (ou "espaço", se estiver fora de todas). Nas passagens, vale a ordem fixa: nós, depois conectores de nós, depois conectores de placa, depois painéis.
- O `StructureTracker` pergunta isso a cada passo de física e emite `StructureChanged` **só quando a estrutura muda** (Observer). Ouvintes: o rótulo de teste do HUD (`StructureHud`) e a câmera (`PhaseCamera`). Usos futuros: spawners (1.4), ponto de retorno, mapa.
- **Regra provisória do espaço** (até a consequência da ejeção ser decidida): fora de todas as estruturas por `SpaceReturnDelay`, o rastreador emite `LostInSpace`, e a fase põe o jogador na eclusa. Para trocar a regra, basta trocar o ouvinte de `LostInSpace`; o rastreador não muda.

### Câmera

- Sem limites por sala: os limites são os da fase, com `CameraMargin`.
- **Zoom por tipo de estrutura** (`PhaseCamera`): afasta nas arenas (nós e painéis) e aproxima nos corredores e, mais ainda, nos conectores de placa, com transição suave (Tween). O primeiro zoom, ao nascer, é instantâneo; no espaço, o zoom não muda.
- Ao pôr o jogador na eclusa (ao nascer ou voltando do espaço), a fase emite `PlayerPlaced`, e a câmera aplica os limites e reinicia a suavização (`ResetSmoothing`), sem "deslizar" pela fase.
- A suavização de posição continua a do `player.tscn` (Speed 6,0). Fora da fase, o zoom do `player.tscn` é 1×.

## 3. Fora do escopo

- Spawners móveis, objetivo da fase e simulação por proximidade (backlog 1.4; fase C).
- A consequência definitiva da ejeção (decisão em aberto; a regra provisória está na seção 2).
- A parede de fronteira `DysonWall` (backlog 1.5): aqui as paredes externas usam o `WallSensitive`.
- Mapa e minimapa, geração procedural, hexágonos vizinhos e progressão entre fases.
- Conteúdo de cada estrutura além do mínimo para teste (o papel de cada tipo é sugestão; seção 10).
- Curvatura da esfera, arte e áudio (etapa 2; ideias na seção 10).

## 4. Parâmetros ajustáveis (valores em teste)

### Formato e medidas (`HexPhaseBuilder`, raiz da `hex_phase.tscn`)

| Parâmetro | Padrão no código | Observação |
|---|---|---|
| `Shape` | Sector | Sector (setor de teste) ou Hexagon (completo). A `hex_phase.tscn` usa Hexagon desde o passo 6 |
| `NodeSpacing` | 6200 px | Distância entre os centros de nós vizinhos |
| `NodeRadius` | 1100 px | Raio interno dos nós. Mínimo ~700 px com as larguras atuais; o `HexPhaseLayout` avisa no Output se ficar pequeno demais |
| `NodeWallSegments` | 32 | Lados do polígono que aproxima o círculo do nó |
| `ConnectorWidth` | 320 px | Largura interna dos conectores de nós |
| `PlateConnectorWidth` | 240 px | Largura interna dos conectores de placa |
| `PanelGap` | 1000 px | Vão (espaço) entre o painel e as paredes dos conectores de nós |
| `WallThickness` | 40 px | Espessura das paredes geradas |
| `MaxWallSegmentLength` | 200 px | Trecho máximo de parede |
| `BackgroundMargin` | 2000 px | Quanto o fundo (espaço) passa da fase |
| Cores do piso, das paredes e do espaço | provisórias | Grupo "Visual provisório"; arte na etapa 2 |

**Padrão: a receita 2×** (decidida pelo dev no passo 6, out/2026; seção 10). A `hex_phase.tscn` não sobrescreve nenhuma medida: usa os padrões do código. Os valores da primeira proposta (2400 / 720 / 200 / 24) estão na tabela de configurações abaixo.

### Sensibilidade das paredes (`HexPhaseBuilder`)

Valores provisórios: a sensibilidade de cada tipo será testada e definida no futuro (dev, out/2026).

| Parâmetro | Padrão | Tempo de luz até ejetar (lanterna atual) |
|---|---|---|
| `ArenaWallMaxIrritation` | 25 | ~5 s (nós e painéis) |
| `CorridorWallMaxIrritation` | 10 | ~2 s (conectores de nós) |
| `PlateWallMaxIrritation` | 7,5 | ~1,5 s (conectores de placa) |

O padrão do `WallSensitive` é 15 (~3 s). A recuperação (15/s) e a força do puxão não mudam por tipo.

### Jogador, eclusa e espaço

| Parâmetro | Padrão | Onde fica | Observação |
|---|---|---|---|
| `Player` | — | `HexPhaseBuilder` | Ligado no Inspector |
| `EntryPeripheral` | 2 | `HexPhaseBuilder` | Número do nó periférico da eclusa (1 a 6, como nos nomes) |
| `Airlock` | — | `HexPhaseBuilder` | Porta da eclusa (`Area2D` com `DoorTrigger.cs`), montada no editor e ligada no Inspector (B3) |
| `SpaceReturnDelay` | 2 s | `StructureTracker` | Só da regra provisória do espaço |

### Câmera (`PhaseCamera`)

Zooms aprovados pelo dev no passo 6 (out/2026).

| Parâmetro | Padrão | Observação |
|---|---|---|
| `ArenaZoom` | 0,7 | Nós e painéis (menor que 1 = câmera mais longe) |
| `CorridorZoom` | 1,2 | Conectores de nós (maior que 1 = mais perto) |
| `PlateConnectorZoom` | 1,3 | Conectores de placa |
| `ZoomTransitionTime` | 0,8 s | Duração da troca de zoom |
| `CameraMargin` | 600 px | Quanto a câmera pode mostrar além da borda da fase |

### Fase B

| Parâmetro | Valor inicial | Onde fica | Observação |
|---|---|---|---|
| `ScreenFadeDuration` | 0,4 s no código; **0,8 s no `player.tscn`** (escolha do dev, B1) | `ScreenFade` (`player.tscn`) | Escurecer e clarear em toda troca de cena |
| `AirlockInset` | 60 px | `HexPhaseBuilder` | Da parede externa do nó até o centro da porta (porta de 40 × 240: sobra ~28 px até a parede) |
| `AirlockSpawnDistance` | 300 px | `HexPhaseBuilder` | O jogador nasce a essa distância da porta, para dentro do nó |

### Como as medidas se relacionam (com as larguras atuais)

- Conector de nós: ≈ `NodeSpacing − 2 × NodeRadius`.
- Conector de placa: ≈ `2 × PanelGap + 600 − NodeRadius`.
- Lado do painel: ≈ `NodeSpacing − 3,5 × (PanelGap + 200)`.
- Para **alongar os corredores mantendo nós e painel**, somar ΔS ao `NodeSpacing` e ~ΔS ÷ 3,5 ao `PanelGap`. O comprimento extra vai para os corredores e para os vãos (mais espaço visível).
- **Raio mínimo dos nós:** em cada nó, o conector de placa sai a 30° de um conector de nós (no hexágono, o nó central tem uma abertura a cada 30°). Para a parede de um não invadir o piso do outro, os eixos precisam estar a meia largura do conector (160) + parede (40) + meia largura da placa (120) = 320 px na saída do nó, o que exige um raio de ~700 px. Com 600 (primeira proposta), a parede entrava ~10 px no corredor vizinho (achado no teste do passo 3).

### Configurações testadas no motor (out/2026)

Todas sem frestas, sem parede invadindo o piso e com todos os corredores livres:

| | Primeira proposta | Ampla | Receita 1,5× | Receita 2× (padrão) |
|---|---|---|---|---|
| `NodeSpacing` / `NodeRadius` / `PanelGap` / lados | 2400 / 720 / 200 / 24 | 4200 / 1100 / 400 / 32 | 5000 / 1100 / 650 / 32 | 6200 / 1100 / 1000 / 32 |
| Nó (diâmetro) | 1440 | 2200 | 2200 | 2200 |
| Conector de nós | ~1000 × 320 | ~2020 × 320 | ~2820 × 320 | ~4020 × 320 |
| Conector de placa | ~300 × 240 | ~310 × 240 | ~810 × 240 | ~1510 × 240 |
| Painel (lado) | ~1000 | ~2120 | ~2060 | ~2040 |
| Paredes: setor / hexágono | 129 / 438 | 271 / 830 | 310 / 1016 | 364 / 1268 |
| Hexágono (área) | ~6200 × 5600 | ~10600 × 9500 | — | ~14600 × 12900 |

A 600 px/s, um conector de ~4000 px leva cerca de 7 s, contando a aceleração. Corredores longos pedem recheio (gargalos, costelas, inimigos) para não cansar. Montagem: a geometria do hexágono leva ~3 ms; criar os nós (fundo, pisos e 1268 paredes) leva de 75 a 110 ms, uma vez ao carregar a cena (medido no descartável; a transição de tela da fase B esconde esse tempo).

### Desempenho (passo 6, out/2026)

Medido pelo dev no editor, com o hexágono vazio (sem inimigos) e o V-Sync desligado só durante o teste. O monitor da Godot mostra o pior valor do último segundo.

| | Antes: ampla, parede sempre processando | Depois: receita 2×, parede que dorme |
|---|---|---|
| Paredes / Nós | 830 / 2561 | 1268 / 3875 |
| Processo de Física, parado no nó | 2,92 ms | 0,30 ms |
| Processo de Física, pior caso medido | 3,86 ms (voando num conector de nós) | 0,46 ms (iluminando uma parede) |
| FPS (V-Sync desligado) | 1290 / 1154 | 1583 / 1596 |
| Nós Órfãos | 0 | 0 |

- **De onde vinha o custo** (descartável, Godot 4.7.2 headless, build Debug, mesma máquina): cada `_PhysicsProcess` em C# atravessa do motor (C++) para o .NET e volta, ~3,2 µs por parede a cada passo, mesmo calma. No hexágono: 1,4 ms (primeira proposta), 2,6 ms (ampla), 3,3 ms (1,5×) e 4,0 ms (2×) por passo. Com a parede que dorme: ~0,005 ms em todas. O passo do servidor de física com até 1268 corpos estáticos custa ~0,01 ms.
- **Por que o FPS quase não mudava:** a física roda a 60 passos por segundo, independente do FPS. A ~1300 FPS, só 1 em cada ~20 quadros tem um passo de física. Com V-Sync a 60 Hz, cada quadro tem um passo: antes, só as paredes calmas tomavam de 16% a 24% do orçamento de 16,7 ms.

## 5. Casos de borda

- **Jogador na passagem entre duas estruturas:** a ordem fixa decide, e o sinal só é emitido quando a estrutura muda, sem "piscar" na fronteira.
- **Ponto fora de todas as estruturas** (ejetado): a estrutura atual é "espaço". Os vãos entre as estruturas são cercados de paredes: sem a regra provisória, o jogador ejetado fica preso neles.
- **Frestas nas junções:** os corredores entram uma espessura de parede nos nós e no painel; as passagens saem da regra das paredes, e nenhuma emenda deixa fresta (verificado no motor).
- **Quinas convexas:** sobra um entalhe pequeno do lado de fora da parede (por dentro, as paredes se encontram sem fresta). É só visual (etapa 2).
- **Paredes giradas:** o `WallSensitive` já calcula o centro real de paredes giradas (`reactive-walls.md`). As paredes geradas seguem o formato da cena, com o filho `ColorRect` (nome exato) adicionado antes de a parede entrar na árvore (o `_Ready` dela procura o `ColorRect`).
- **Quantidade de paredes:** de 129 (setor da primeira proposta) a 1268 (hexágono da receita 2×, o padrão). Medido no passo 6: o `_PhysicsProcess` de cada parede, mesmo calma, custava ~3,2 µs por passo (4 ms com 1268). Resolvido com a parede que dorme fora da luz; a fase vazia caiu para 0,3 ms de física (seção 4).
- **Parede que dorme:** nasce dormindo (`SetPhysicsProcess(false)` no `_Ready`; a Godot liga o `_PhysicsProcess` antes do `_Ready`, por isso desligar ali vale). A luz a acorda (`AddIrritation`); ela decai e volta a dormir ao zerar, inclusive logo depois de ejetar (volta à cor normal no passo seguinte; antes podia ficar vermelha parada se a luz saísse dela). O tempo até ejetar não mudou (diferença máxima de 1 passo, conforme a ordem dos nós).
- **Ordem do `_Ready`:** o `StructureTracker` e o `PhaseCamera` são filhos da raiz e ficam prontos antes dela, quando o `Layout` ainda não existe. O rastreador só trabalha no passo de física, com guarda; a câmera assina os avisos no `_Ready` e recebe o primeiro `PlayerPlaced`, emitido no `_Ready` da raiz.
- **Campo `[Export]` de nó vazio** (ex.: `Phase` do `StructureTracker`): o componente não age, sem erro (as guardas retornam cedo). No teste do passo 4, o rótulo ficou em "Estrutura: -" por isso. Conferir os campos ao montar a cena.
- **Câmera ao nascer ou ao voltar do espaço:** `ResetSmoothing` evita que ela atravesse a fase deslizando.
- **Entrar e sair rápido de um corredor:** cada troca de zoom interrompe a anterior (`Kill` no Tween), sem engasgo.
- **Jogador morto no espaço:** a regra provisória não age; quem age é o `RestartOnDeath`.
- **Trocas repetidas entre câmara e fase** (fase B): sem erros e com "Nós Órfãos" = 0 (os registros estáticos `FractalSwarm` e `BiomassField` se limpam no `_ExitTree`).
- **Morrer durante o escurecer de uma porta** (B2): a porta já pediu a troca e vence (o `ScreenFade` ignora o segundo pedido). A taxa da morte já foi cobrada. O temporizador da morte dispara depois da troca, mas a Godot não chama o método de um nó já liberado: sem erro e sem segunda troca (verificado no descartável).
- **Corpo deslizando para uma porta** (B2): morto não usa porta (`DoorTrigger` ignora `IsDead`); quem age é o `RestartOnDeath`. Sem a guarda, o corpo poderia "extrair" pela eclusa no lugar de morrer.
- **Porta da eclusa** (B3): a posição e o giro do `Airlock` no editor não valem; o construtor o move ao rodar. Com o campo `Airlock` vazio, a porta não sai do lugar e nada avisa (o nascimento usa o ponto novo mesmo assim). `EntryPeripheral` fora dos nós que existem (1 e 2 no setor, 1 a 6 no hexágono) cai no nó central, onde à direita sai um corredor: não usar.
- **Pedido de troca dentro de um callback de física** (`BodyEntered`): o pedido só marca o fade para escurecer; a troca de verdade acontece no `_Process` do `ScreenFade`, fora da física.
- **Inimigos e orbes para teste:** o ninho atual (`NinhoSpawnerPooled`) pode ser colocado num nó só para testar combate na fase, até os spawners móveis existirem.

## 6. Arquivos e cenas

- **Novos** (criados em out/2026, na raiz do projeto, com autorização do dev):
  - `HexPhaseLayout.cs` (classes C# comuns, sem nó): os tipos `HexPhaseShape`, `HexStructureKind`, `HexPhaseSettings`, `HexWallSegment` e `HexStructure`, e a classe `HexPhaseLayout`. Ela cuida da geometria do setor ou do hexágono, da pergunta `FindStructureAt`, do retângulo `Bounds`, dos `WallSegments` (com o tipo da estrutura dona), do `PeripheralNodeCenter` e da checagem do raio mínimo.
  - `HexPhaseBuilder.cs` (`Node2D`, raiz da cena): monta o fundo, os pisos e as paredes (`WallSensitive`, com `MaxIrritation` por tipo). Põe o jogador na eclusa (`PlacePlayerAtEntry`), emite `PlayerPlaced` e responde a `LostInSpace` (`OnPlayerLostInSpace`).
  - `StructureTracker.cs` (`Node`): estrutura atual, sinais `StructureChanged` e `LostInSpace` (regra provisória do espaço).
  - `StructureHud.cs` (`Label`): HUD de teste "Estrutura: …".
  - `PhaseCamera.cs` (`Node`): limites da fase, zoom por tipo de estrutura, `ResetSmoothing`.
  - `hex_phase.tscn`, montada pelo dev, em `res://` (movida de `upgrades/` pelo painel Sistema de Arquivos no passo B0).
- **Alterado no passo 6** (com autorização do dev): `WallSensitive.cs`, a parede dorme fora da luz (`SetPhysicsProcess(false)` no `_Ready` e ao zerar a irritação; `SetPhysicsProcess(true)` no `AddIrritation`). A fase continua preenchendo só o `[Export]` `MaxIrritation`. Também mudaram os padrões de medidas do `HexPhaseBuilder.cs` (receita 2×).
- **Fase B (feita, out/2026):**
  - Novo (B1, feito): `ScreenFade.cs` (`CanvasLayer`, nó no `player.tscn`): transição de tela, sem autoload; quem chega clareia, quem sai escurece e troca de cena. Método estático `ScreenFade.ChangeScene(quem pede, cena)` acha o fade da cena pelo grupo `ScreenFade` (cena sem fade: troca direto).
  - Alterados (B2, feito): `DoorTrigger.cs` (troca pelo `ScreenFade`; ignora o jogador morto) e `RestartOnDeath.cs` (troca pelo `ScreenFade`; destino vazio = abrir de novo o arquivo da cena atual).
  - Alterado (B3, feito): `HexPhaseBuilder.cs` (`[Export]` `Airlock`, `AirlockInset` e `AirlockSpawnDistance`; `PlaceAirlock` põe a porta na parede externa do nó de entrada; `PlacePlayerAtEntry` põe o jogador à frente dela).
  - Cenas, montadas pelo dev: `player.tscn` (nó `ScreenFade`), `hex_phase.tscn` (nó `Airlock` com `DoorTrigger.cs`), `sala_entrada.tscn` (a `PortaSaida` leva à `hex_phase.tscn`; a `sala_ninho` fica como bancada).
- **Somente leitura:** `Player.cs`, `GameState.cs`, `SalaEntrada.cs`.
- **Ficam como bancadas:** `sala_ninho.tscn`, `sala_desfiladeiro.tscn` e `arena.tscn` (protótipos do recheio de nós e conectores).

## 7. Passos no editor (feitos pelo dev)

Feitos em out/2026 (fase A):

1. **Build** no editor depois de cada script com `[Export]` ou `[Signal]` novo.
2. `hex_phase.tscn`: raiz **`HexPhase`** (`Node2D`) com o `HexPhaseBuilder.cs` e uma instância do `player.tscn` em (0, 0). No Inspector da raiz: **Player** = `Player`, **Entry Peripheral** = 2, medidas da configuração ampla.
3. **`StructureTracker`** (`Node`, `StructureTracker.cs`), filho da raiz, com **Phase** = `HexPhase`.
4. **`PhaseHud`** (`CanvasLayer`), filho da raiz, > **`StructureLabel`** (`Label`, `StructureHud.cs`) em (16, 80).
5. Sinais do `StructureTracker`: `StructureChanged` → `StructureLabel.OnStructureChanged`; `LostInSpace` → `HexPhase.OnPlayerLostInSpace`.
6. **`PhaseCamera`** (`Node`, `PhaseCamera.cs`), filho da raiz, com **Phase** = `HexPhase` e **Tracker** = `StructureTracker` (os avisos são ligados por código).
7. Passo 6: **Shape** = Hexagon na `HexPhase`; **Node Spacing** e **Panel Gap** revertidos (↺) para os padrões do código (receita 2×). A cena só guarda os valores diferentes do padrão do script.

Feitos na fase B:

8. B0: `hex_phase.tscn` movida de `upgrades/` para `res://` (painel Sistema de Arquivos).
9. B1: **`ScreenFade`** (`CanvasLayer`, `ScreenFade.cs`), filho da raiz do `player.tscn`, com **Layer** = 10 (acima do HUD, na 1, e do painel de evolução, na 2) e **Screen Fade Duration** = 0,8. Sem campos de nó para ligar.

10. B3: **`Airlock`** (`Area2D`, `DoorTrigger.cs`, **Target Scene Path** = `sala_entrada.tscn`), filho da raiz `HexPhase`, logo acima do `Player` (o jogador é desenhado por cima da porta), com **`CollisionShape2D`** (retângulo 40 × 240) e **`ColorRect`** (Position (-20, -120), Size (40, 240), Mouse Filter = Ignore, cor provisória). Na raiz: **Airlock** = `Airlock`.

11. B4: **`PortaSaida`** da `sala_entrada` com **Target Scene Path** = `hex_phase.tscn`. Para o teste do ciclo, um ninho temporário (`TesteNinho` > `NinhoSpawn1` (6000, -300), `NinhoSpawn2` (6000, 300) e `NinhoSpawner` com `NinhoSpawnerPooled.cs`: intervalo 1 s, 15 simultâneos, estoque 30) no nó 2, apagado depois do teste.

Nenhum passo pendente na fase B.

## 8. Pronto quando

### Fase A: o setor navegável

Confirmado pelo dev no editor (out/2026):

- [x] `dotnet build` sem erros
- [x] Rodando a `hex_phase.tscn` (F6), o setor aparece com as 10 estruturas, as paredes são contínuas (sem frestas) e as passagens entre estruturas estão abertas
- [x] O painel tem a base horizontal e, no setor, aponta para cima
- [x] O jogador voa por todas as estruturas sem troca de cena, e a câmera o segue sem os limites de 1920×1080
- [x] O rótulo de teste mostra a estrutura atual e só muda quando o jogador troca de estrutura
- [x] As paredes externas irritam e ejetam (`WallSensitive`) em qualquer ângulo; ejetado no vão, o jogador volta à eclusa em ~2 s (regra provisória)
- [x] O jogador nasce na eclusa com a câmera no lugar; o zoom afasta nas arenas e aproxima nos corredores e placas, sem engasgo; a volta do espaço não desliza
- [x] As paredes de nós e do painel levam ~5 s de luz até ejetar, as de corredor ~2 s e as de placa ~1,5 s
- [x] Com `Shape` = Hexagon, o hexágono completo é montado sem mudar código (receita 2×: 43 estruturas, 1268 paredes, 3875 nós), sem frestas e com as passagens abertas, e o FPS não cai de forma perceptível com a fase vazia: Processo de Física de 0,30 ms parado e 0,46 ms no pior caso medido (seção 4)
- [x] Com a parede que dorme, as paredes continuam irritando, ejetando e se recuperando como antes

### Fase B: o fluxo da partida

Confirmado pelo dev no editor (out/2026), com o ninho temporário na fase:

- [x] `dotnet build` sem erros a cada passo
- [x] Da câmara, a `PortaSaida` leva à fase com escurecer e clarear; o jogador nasce na eclusa, parado, com a câmera no lugar
- [x] A eclusa leva de volta à câmara, e a biomassa carregada é depositada no banco
- [x] Morrer na fase volta à câmara; entrar de novo encontra a fase do zero
- [x] A integridade se mantém entre as estruturas e começa cheia a cada entrada
- [x] Trocas repetidas entre câmara e fase sem erros no Output e com "Nós Órfãos" = 0

## 9. Como testar (roteiro manual no editor)

1. F6 na `hex_phase.tscn`: percorra todas as estruturas, confira o rótulo e procure frestas nas junções (encoste nas emendas).
2. Irrite paredes de nó, de corredor e de placa, contando o tempo até a ejeção (~5 s, ~2 s, ~1,5 s); ejetado, confirme a volta à eclusa em ~2 s, sem a câmera deslizar.
3. Entre e saia de corredores e placas e confira o zoom (afasta nas arenas, aproxima nos corredores).
4. **Passo 6:** com `Shape` = Hexagon, repita o passo 1 rapidamente e anote FPS, Processo de Física e Nós (Depurador > Monitores; V-Sync desligado só durante o teste). Referência na seção 4.
5. (Fase B) F5: câmara → fase → eclusa → câmara (o banco sobe); morra na fase e entre de novo (fase nova); repita 5 vezes olhando o Output e "Nós Órfãos". Enquanto a fase não tem spawners, use um ninho temporário (seção 7, item 11) e apague-o depois: oculto, ele continuaria criando inimigos.

## 10. Decisões e notas

### Decididas pelo dev (out/2026)

1. **H1, plano de jogo: planta da casca** (opção A). A câmera olha a esfera de fora, sem o teto das estruturas; o hexágono fica no plano do jogo; a estrela fica atrás. Escolhida depois de comparar com o corte lateral do anel (B: só a fileira cortada pelo plano aparece, e o hexágono não cabe) e com telas separadas (C). Mantém o movimento atual (vista de cima, todas as direções) e é a leitura do mockup do conector.
2. **H2, continuidade:** uma cena por fase. Spawners que circulam pela fase inteira precisam que ela exista toda ao mesmo tempo; a integridade e a biomassa passam entre as estruturas sem esforço.
3. **H3, montagem:** contornos por código a partir da geometria; recheio em cenas pequenas do editor. Ângulos de 60° e encaixes exatos ficam no código; a `DysonWall` é gerada ao longo de segmentos, então os contornos já ficam prontos para ela; o que é design visual continua no editor.
4. **H4, escopo:** setor de teste primeiro; hexágono completo por parâmetro.
5. **H5, câmara inicial e reinício:** câmara separada; entrada e extração pela eclusa de um nó periférico; morte volta à câmara; a fase recomeça a cada entrada. Resolve a decisão em aberto 3 da `suit-evolution.md` (voltar vivo à câmara). Consequência aceita: destruir todos os spawners exige uma única incursão.
6. **Orientação (implementação, passo 3b):** o hexágono gira 30° ("deitado"), para todo painel ter um lado horizontal; no setor, o painel aponta para cima. É uma constante (`FirstPeripheralAngleDegrees`), não `[Export]`: é decisão de design, não balanceamento.
7. **Compressão e alívio (princípio de level design):** a megaestrutura não precisa ser toda grande; precisa de contraste. Corredores estreitos fazem as arenas parecerem enormes, e vice-versa. Na fase: arenas (nós e painéis) amplas; corredores e placas estreitos e longos, com paredes mais sensíveis e câmera mais próxima. Implementados na etapa 1: zoom por tipo (`PhaseCamera`) e sensibilidade por tipo (`HexPhaseBuilder`). Ideias para a etapa 2 (ver também o backlog):
   - paralaxe da megaestrutura vista pelos vãos (hexágonos vizinhos, conectores sumindo na distância, o outro lado da esfera, a estrela; nó `Parallax2D`);
   - estruturas em primeiro plano passando na frente da câmera;
   - contraste de luz (arenas banhadas pela estrela, corredores escuros, iluminados quase só pela lanterna);
   - referências de escala (maquinário e costelas gigantes nas arenas, hordas de centenas);
   - áudio com eco nas arenas e abafado nos corredores;
   - variações nos corredores com o recheio (gargalos, costelas atravessadas, paredes que "respiram").
8. **Componentes separados (responsabilidade única):** o construtor monta uma vez; o `StructureTracker` observa a todo passo; o `PhaseCamera` cuida da câmera. Avisos (Observer): `StructureChanged` → HUD e câmera; `LostInSpace` → fase (regra provisória); `PlayerPlaced` → câmera.
9. **Raio mínimo dos nós** (passo 3): `NodeRadius` padrão de 600 para 720, com a checagem `WarnIfCorridorsCollide` no layout (seção 4).
10. **Divergência do zoom resolvida:** o CLAUDE.md citava zoom de 1,3× a 1,5×, mas o `player.tscn` está em 1×. Na fase, vale o zoom por tipo; fora dela, 1×.
11. **Medidas padrão: receita 2×** (passo 6): `NodeSpacing` 6200, `NodeRadius` 1100, `PanelGap` 1000, 32 lados. Nós e painéis de ~2200 px e corredores longos (~4000 px nos conectores de nós, ~1500 px nos de placa): o maior contraste de compressão e alívio entre as configurações testadas. Viraram os padrões do código.
12. **Zoom aprovado** (passo 6): 0,7 nas arenas, 1,2 nos corredores e 1,3 nas placas.
13. **A parede dorme fora da luz** (passo 6, `WallSensitive.cs`): em vez de cada parede perguntar a todo passo se está irritada (*polling*), ela reage ao aviso da luz (`AddIrritation`) e só processa enquanto se acalma. É a ideia do "sono" dos corpos rígidos (`RigidBody2D.CanSleep`) e do "só atualize os objetos ativos" (*Game Programming Patterns*, "Update Method"). Alternativa descartada: trechos de parede maiores nos corredores (reduz só parte das paredes e muda o jogo: o centro do puxão e o tamanho do trecho irritado). A mesma ideia vale para a `DysonWall` (backlog 1.5).
14. **Plano da fase B** (aprovado pelo dev, out/2026). Já funciona sem código novo: a fase recomeça do zero e a integridade começa cheia a cada entrada (cada troca cria instâncias novas), a câmara deposita a biomassa a cada entrada e a morte volta à câmara. Passos:
    - **B0** (editor): mover a `hex_phase.tscn` para `res://`.
    - **B1:** `ScreenFade.cs` e o nó `ScreenFade` no `player.tscn` (clareia ao chegar).
    - **B2:** `DoorTrigger` e `RestartOnDeath` trocam de cena pelo `ScreenFade` (teste no fluxo atual, câmara ↔ `sala_ninho`).
    - **B3:** porta da eclusa (`Airlock`) posicionada pelo `HexPhaseBuilder` na parede externa do nó de entrada, com o nascimento à frente.
    - **B4** (editor): `PortaSaida` → `res://hex_phase.tscn` e o teste do ciclo completo (seção 9, item 5), com um ninho temporário num nó para testar a morte.
    - **B5:** fechamento (spec, backlog, CLAUDE.md, HANDOFF, mensagem de commit).
    - **Resultado:** os seis passos foram feitos e testados pelo dev em out/2026, cada um verificado antes no descartável. Nenhum script novo além do `ScreenFade.cs`, nenhum autoload e nada no `project.godot`.
15. **Decisões da fase B** (recomendadas e aceitas): porta na parede externa do nó de entrada (alternativa descartada: porta no centro, armada depois da primeira saída); extração ao tocar (`interact` se houver extração sem querer); a morte também usa a transição; sem pausa durante o escurecer (a pausa continua depois da troca de cena, e o painel de evolução também pausa).
16. **Transição no `player.tscn`, sem autoload:** o jogador está em todas as salas, então todas ganham a transição sem montagem por cena (o HUD já mora lá). Quem pede a troca acha o fade da cena pelo grupo `ScreenFade`; cena sem fade troca direto. A cena que sai termina preta e a que chega nasce preta; a Godot troca a cena antes de desenhar o quadro, então nenhum quadro sai sem cena (verificado no descartável).
17. **Fade sem Tween** (B1): o quadro em que a fase é montada dura ~130 a 145 ms, e um Tween (que avança pelo tempo real) fazia o preto pular de 96% para 60% de uma vez (medido no descartável). O `ScreenFade` anima a transparência no `_Process`, com o passo limitado a 1/30 s por quadro (`MaxFrameStep`), e só processa durante a transição. Abaixo de 30 FPS, a transição fica um pouco mais longa. Se a cena de destino não abrir, mostra o erro e volta a clarear.
18. **Porta da eclusa** (B3): a posição sai do código (depende das medidas e do `EntryPeripheral`) e a aparência fica no editor (H3). O construtor recebe a porta como um `Node2D` qualquer, sem saber que é um `DoorTrigger` (baixo acoplamento: a eclusa pode virar uma cena com animação sem mudar o construtor). "Para fora" é o vetor do centro da fase ao centro do nó; a porta fica a `AirlockInset` da parede, girada para fora (eixo X local para o espaço). O nascimento sai da geometria, não da porta: vale sem porta e na volta do espaço. Verificado no descartável nos nós 1 e 2 do setor e 1 a 6 do hexágono (receita 2×): porta dentro do piso do nó, nascimento a 300 px, porta sem disparar no nascimento.

### Em aberto

- **Sensibilidade das paredes por tipo** (25 / 10 / 7,5, seção 4): provisória; o dev vai testar e definir no futuro.
- **Consequência da ejeção** (backlog, seção 2). A regra provisória (volta à eclusa sem perdas) sai quando ela for decidida; trocar só o ouvinte de `LostInSpace`.
- **Papel de cada tipo de estrutura** (sugestão): nós = arenas de combate; conectores = corredores com risco de parede; painéis = áreas abertas com muita fronteira; conectores de placa = gargalos; nó central = clímax.
- **Recheio de cada tipo** e quando entra (pode acompanhar os spawners, 1.4).
- **Hexágonos vizinhos** e progressão entre fases (provavelmente depois da demo).
- **Câmara de partida no nó inicial** (ideia do dev, out/2026, para o futuro): `docs/specs/start-chamber.md` (backlog 1.7). Se adotada, revê a H5 (câmara separada), a extração pela eclusa e a regra provisória do espaço.

### Notas

- Cada passo da fase A foi verificado antes num projeto descartável no scratchpad (Godot 4.7.2 headless): contagem de estruturas, ponto do meio de cada estrutura, frestas e invasões das paredes, ordem dos nós, rastreio, regra do espaço, zoom, sensibilidade e, no passo 6, o custo das paredes (sondas no primeiro e no último nó do passo de física, por prioridade de processamento) e os estados e o tempo até ejetar da parede que dorme. A irritação, a ejeção, a sensação e o desempenho no editor foram testados pelo dev.
- **Medir no editor:** o Processo de Física dos Monitores é o número que importa para a física; o FPS com V-Sync desligado esconde o custo de algo que só roda no passo de física. No headless, o monitor de física não é confiável (valores inflados): lá, medir com sondas.
- A `SalaEntrada.cs` e a `SalaDesfiladeiro.cs` repetem a mesma lógica (posicionar o jogador num marcador, zerar a inércia, travar a câmera em 1920×1080). A fase não usa essa lógica; com a câmara como única sala separada, não vale criar um componente comum agora.
- O `PhaseCamera` e o `HexPhaseBuilder` separam arena, corredor e placa em dois `switch` parecidos. Se uma terceira coisa precisar da mesma separação, criar um lugar único.
