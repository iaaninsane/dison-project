# PROJECT BIO-BREACH (NEON NEXUS)
## Documento de Handoff, Arquitetura, Design e Decisões Técnicas (v2.0, out/2026)

Guia de transição e referência para qualquer desenvolvedor, analista ou designer que venha a dar continuidade ao projeto **Project Bio-Breach** (nome de trabalho alternativo: *Neon Nexus*).

> **v2.0 (out/2026):** incorpora o rascunho de redefinição de game design e level design do dev (cenário, fase hexagonal, spawners móveis, fim do blecaute total, nova direção de arte) e foi revisado contra o código. Onde este documento divergir do código, **o código vale**. Tarefas e decisões em aberto ficam no `docs/BACKLOG.md`; regras de cada mecânica, nas specs de `docs/specs/`.

---

## 1. Visão geral e sinopse

O **Project Bio-Breach** é um action roguelike 2D em gravidade zero, feito em **C# na Godot 4.7 (Mono)**, para uma demo na Steam.

- **Sinopse:** um cientista isolado explora o interior de uma estrutura colossal à deriva no espaço: uma **esfera de Dyson** que envolve uma estrela **laranja puxando para o avermelhado** (estrela de temperatura de superfície média/baixa). Ao entrar, ele descobre que a estrutura é um organismo bio-mecânico vivo, que o trata como infecção e ativa anticorpos fractais e defesas teciduais para expulsá-lo.
- **High concept:** navegação inercial em gravidade zero pelas estruturas da esfera, vistas em corte, usando o feixe de uma lanterna para destruir anticorpos fractais, sob o risco de irritar o tecido das paredes e ser ejetado para o espaço.
- **A esfera:** uma malha geodésica de hexágonos formados por triângulos, com nós luminosos nos vértices e no centro de cada hexágono, conectores entre os nós e painéis triangulares (imagem de referência: `docs/reference/dyson-sphere-reference.png`). É com essa estrutura que as fases são construídas (seção 4).
- **Visual:** sem blecaute total. Os inimigos ficam visíveis em **silhueta**, com muitos efeitos de luz e sombra; a luz principal é a **contraluz** do sol, ao fundo. Referência de arte: **Cocoon** (seção 5).
- **Eficiência para dev solo:** desenho por código (inimigos fractais em `_Draw`, orbes, paredes geradas), sistemas centrais sem física onde possível (registros estáticos) e cenas pequenas e componentizadas.

---

## 2. Escopo e decisões de arquitetura

Fronteira de escopo enxuta para a demo.

### Mecânicas do core loop

1. Movimentação inercial em gravidade zero.
2. Lanterna: feixe que ilumina, causa dano aos inimigos e irrita as paredes.
3. Combate por luz e divisão celular dos inimigos (fragmentação).
4. Irritabilidade das paredes e ejeção do jogador para o espaço.
5. Coleta de biomassa e evolução do traje.
6. Troca entre modo manual e automático da lanterna.
7. **Novo (out/2026):** objetivo da fase: destruir os spawners móveis (seção 4.3; ainda sem spec).

### Adiadas para depois da demo

1. **Vácuo como atalho estratégico:** arremessar o personagem para o vácuo de propósito como atalho entre estruturas. Ser ejetado para o espaço é uma **consequência** (em aberto, seção 3.3); navegar pelo vácuo continua adiado.
2. **Foguetes de mega propulsão.**

### Core loop

`EXPLORAR` (navegação inercial pelas estruturas do hexágono) ➔ `ALERTAR` (luz e tiros estressam o organismo) ➔ `COMBATER` (dano por luz e fragmentação; destruir os spawners) ➔ `COLETAR` (biomassa neon) ➔ `EVOLUIR` (melhorias do traje na câmara inicial) ou `MORRER`.

Tiros ainda não existem: o "estresse por tiros" e os "modos de tiro" são decisões em aberto (backlog).

---

## 3. Mecânicas implementadas (conforme o código, out/2026)

Resumo; as regras completas e os valores em teste estão nas specs.

### 3.1 Movimento em gravidade zero (`Player.cs`)

- Empuxo por vetor (WASD/analógico esquerdo): `ThrustForce` 450, `MaxSpeed` 600.
- Sem input, a velocidade cai devagar e de forma **linear** (`Friction` 0,1 × `ThrustForce` = 45 px/s², com `MoveToward`). Para frear rápido, o jogador aplica contra-empuxo. (A v1.0 citava um amortecimento `_counterThrustDamping`, que não existe no código.)
- O golpe de um inimigo (`ApplyKnockback`) **soma** um impulso à velocidade. A ejeção (`ApplyEjection`) **substitui** a velocidade e tira o controle por `EjectionDuration` (0,8 s).
- Não há colisão soft-body (citada na v1.0); o jogador é um `CharacterBody2D` em Motion Mode Grounded (avaliar Floating: dívida técnica no backlog).

### 3.2 Lanterna e visibilidade

- `FlashlightAnchor` (`Marker2D`) com um `PointLight2D` e a `LightDamageArea` (`Area2D` em cone de ~220 px), que dá dano aos inimigos (`DamagePerSecond`) e irrita as paredes do grupo `Walls` (`WallIrritationPerSecond`).
- **Modos:** manual (segue o mouse ou o analógico direito) e automático (segue a direção da velocidade). O modo fica no `RunData` e vale em todas as salas; muda só por `GameState.ToggleFlashlightMode()` (sinal `FlashlightModeChanged`, escutado pelo `Player`), pelo `TerminalModo` da câmara inicial. Hoje o modo é um `bool`; vira padrão Strategy se surgirem mais modos.
- **Visibilidade (out/2026): não há mais blecaute total.** Cada sala ainda precisa de um `ColorRect` de fundo como primeiro filho (sem ele, a luz da lanterna não tem onde aparecer). A cor do `CanvasModulate` virou decisão de arte; as cenas atuais ainda estão no estado antigo até o ajuste do dev (backlog 1.6). Proposta de linguagem visual: o sol ao fundo deixa tudo em silhueta, e a lanterna é a única luz frontal, que revela cor e volume do que atinge.

### 3.3 Paredes reativas e ejeção

- **Hoje (`WallSensitive.cs`, spec `reactive-walls.md`):** as paredes do grupo `Walls` acumulam irritação sob a luz e se recuperam fora dela. No limite (`MaxIrritation`), puxam o jogador para o **centro real** da parede (calculado pela transformação global do `ColorRect`) com `EjectionForce`; durante a ejeção, ele atravessa só essa parede e as do grupo `Walls` no caminho até ela (exceção de colisão), e bate em todo o resto. Paredes de titânio (câmara inicial) e pilares ficam fora do grupo.
- **Modelo-alvo (decisão do dev, out/2026; spec `dyson-wall.md`, proposta):** só as **paredes de fronteira com o espaço** são reativas. Elas são uma membrana de peças geométricas (triângulos de Sierpinski) que, irritada, se deforma, **abre uma fenda** e **suga** o jogador; ao cruzar a fenda, ele é ejetado para o espaço (sinal `PlayerEjected`). A sucção simula uma descompressão: é **gradual pela distância** (fraca longe, avassaladora perto, com um raio "sem retorno" em que ela empata com o empuxo do traje) e tem uma **rajada** no rompimento. O jogador mantém o controle e pode fugir com contra-empuxo. **Inimigos e orbes de biomassa também são sugados**, o que faz da fenda uma ferramenta tática e um risco. Paredes internas entre estruturas são rígidas. O `WallSensitive.cs` é transitório até a `DysonWall` existir.
- **Deformação:** o modo Esmaecer (as peças encolhem em cascata até sumir) é o padrão **provisório**; a forma final está em aberto (referências sugeridas na spec).
- **Depois da ejeção (decidido em out/2026, `start-chamber.md` D3):** ficar perdido no espaço é igual à morte. O `StructureTracker` avisa (`LostInSpace`), o `EjectionConsequence` rompe o traje, e o fluxo da morte segue (taxa da biomassa, escurecer, recarga da fase em modo câmara). O espaço não é navegável.

### 3.4 Inimigo fractal e horda (`FractalEnemy.cs`, `FractalSwarm.cs`)

- Árvore fractal binária desenhada em `_Draw` (4 troncos), com o ângulo dos ramos pulsando; morre sob a luz e se divide em fragmentos menores até a geração máxima (divisão celular).
- `CharacterBody2D` em Motion Mode **Floating**, camada 2 (máscara 1). Os inimigos não colidem entre si: o registro estático `FractalSwarm` calcula uma separação suave e põe para descansar quem está travado.
- Spawners atuais: `NinhoSpawnerPooled.cs` (com pool; usado na `sala_ninho`) e `NinhoSpawner.cs` (sem pool). Tetos: `MaxConcurrentEnemies` (pressão do ninho) e `MaxTotalEnemies` (orçamento de física, contando fragmentos). Medido: com 200 no ninho e teto 350, FPS ≥ 100 (V-Sync desligado). O ninho fixo dá lugar aos spawners móveis (seção 4.3).
- Specs: `fractal-enemy.md`, `fractal-enemy-movement.md`; visual 3D (etapa 2): `fractal-enemy-render-3d.md`.

### 3.5 Biomassa (`BiomassOrb.cs`, `BiomassField.cs`, `BiomassCollector.cs`)

- Quem morre sem se dividir solta um orbe (sem física, sempre visível). O registro estático `BiomassField` faz idade, ímã e coleta num único laço. A coleta vai para a biomassa carregada do `GameState`. Spec: `biomass-collection.md`.

### 3.6 Integridade e morte (componentes no `player.tscn`)

- `PlayerHurtbox` (contato com inimigos), `SuitIntegrity` (golpe, imunidade, piscar, empurrão; sinais `IntegrityChanged` e `Died`), `RestartOnDeath` (cobra a taxa da biomassa e, depois de um atraso, recarrega a cena atual: na fase, a própria fase em modo câmara). `SuitIntegrity.Breach()` rompe o traje de uma vez (usado pela ejeção). Specs: `player-health-death.md` e `start-chamber.md`.

### 3.7 Estado da partida e evolução (`GameState.cs`, `RunData.cs`)

- Autoload `GameState` com um `RunData` (banco, biomassa carregada, níveis das melhorias por `Id`, modo da lanterna), separado do nó para facilitar salvar em disco no futuro.
- Na **câmara de partida** (o nó de entrada da fase, `StartChamber`; spec `start-chamber.md`): deposita a biomassa carregada no banco ao carregar a fase; o `TerminalModo` troca o modo da lanterna; o `EvolutionTerminal` abre o `UpgradePanel`; o terminal "Liberar saídas" (`ExitReleaseTerminal`) abre o nó e começa a incursão. A eclusa extrai (recarrega a fase, depositando 100%). Melhorias como recursos `SuitUpgrade` (pasta `upgrades/`), aplicadas pelo `SuitUpgradeApplier`. Spec: `suit-evolution.md`.
- A `sala_entrada` foi a câmara inicial até out/2026; hoje é bancada (os terminais e a `PortaSaida` → fase continuam lá).

---

## 4. Level design: a fase hexagonal (rascunho de out/2026)

Substitui a v1.0, que previa salas modulares de 1920×1080 (entrada, desfiladeiro, ninho, núcleo) ligadas por um gerador de labirintos em grade (`MazeGenerator.cs`, nunca criado). Spec: `docs/specs/hex-phase.md` (aprovada, out/2026).

### 4.1 Estrutura

- **Uma fase = um hexágono** da malha da esfera.
- Estruturas de cada hexágono, e cada uma é uma **sala** onde o jogador combate, coleta e evita ser ejetado:
  - **nó central** (centro do hexágono);
  - **nós periféricos** (vértices do hexágono);
  - **conectores de nós** (ligações entre nós);
  - **conectores de placas** (ligações entre os nós e as placas triangulares);
  - **painéis** (as placas triangulares).
- Todas as estruturas da fase são interligadas: o hexágono inteiro forma a **arena**.
- Leitura da imagem de referência, a confirmar na spec: 1 nó central, 6 nós periféricos (compartilhados com os hexágonos vizinhos), 12 conectores de nós (6 raios e 6 no perímetro), 6 painéis e os conectores de placa. São mais de 25 salas por hexágono.

### 4.2 Como a sala é vista

- **Decidido (out/2026): planta da casca.** A câmera olha a esfera **de fora**, com o teto das estruturas removido. O hexágono fica deitado no plano do jogo (nós = câmaras, conectores = corredores a 60°, painéis = salas triangulares) e o vão entre as estruturas é o espaço. A estrela fica **atrás do plano**: contraluz, com a luz vazando pelos vãos e por janelas no piso.
- É a leitura do mockup do rascunho (`docs/reference/connector-cut-mockup.png`): um conector visto de cima, com o sol atrás, janelas hexagonais no piso e as paredes de cima e de baixo dando para o espaço. A curvatura do mockup fica só no visual.
- Jogos com o mesmo plano: FTL, Heat Signature (inclusive a sucção pelo rombo no casco, que afeta guardas e jogador) e Space Haven. Mantém o movimento atual (vista de cima, gravidade zero, todas as direções).

### 4.3 Spawners móveis e objetivo da fase

- Os inimigos são criados por **spawners** (ex.: 10 por fase) que **circulam pela fase inteira** gerando inimigos o tempo todo.
- **Objetivo da fase:** eliminar os spawners. A cada spawner morto, a dificuldade aumenta.
- Em aberto (spec própria, backlog 1.4): vida e como matar um spawner, como ele se move, a curva de dificuldade, o que acontece quando todos morrem.

### 4.4 Decisões da spec da fase e sugestões em aberto

Decidido na `hex-phase.md` (out/2026):

- **Continuidade:** uma cena por fase; o jogador voa entre as estruturas sem troca de cena.
- **Montagem:** contornos (piso, paredes, aberturas) gerados por código a partir da geometria; recheio em cenas pequenas feitas no editor.
- **Escopo:** setor de teste primeiro (um triângulo: 3 nós, 3 conectores de nós, 1 painel, 3 conectores de placa); hexágono completo por parâmetro.
- **Câmara de partida (revista em out/2026, `start-chamber.md`):** o jogo começa na fase; o nó de entrada é a câmara (portões nas bocas, paredes inertes, terminais do traje e "Liberar saídas") até o jogador liberar as saídas, quando vira um nó comum e a incursão começa (`IncursionStarted`). Morte, ejeção (igual à morte) e extração pela eclusa recarregam a fase em modo câmara, e a fase recomeça do zero. Toda troca de cena escurece e clareia (`ScreenFade`). (A primeira versão, na fase B, tinha a câmara como cena separada.)
- **Medidas:** receita 2× como padrão (nós e painéis de ~2200 px, conectores de nós de ~4000 px). O hexágono completo tem 1268 trechos de parede; a parede calma não processa (Processo de Física da fase vazia ~0,3 ms).
- **Orientação (implementação):** hexágono "deitado" (giro de 30°): todo painel tem um lado horizontal; no setor de teste, o painel aponta para cima.
- **Compressão e alívio:** arenas (nós e painéis) amplas e corredores estreitos e longos. A câmera afasta nas arenas e aproxima nos corredores, e as paredes dos corredores e das placas são mais sensíveis: o perigo fica onde está o aperto.

Sugestões em aberto (backlog, seção 2, e 1.4):
- **Grafo da fase:** os spawners andam pelas arestas (sem pathfinding); o mapa sai do grafo; só a sala atual e as vizinhas são simuladas por completo (o orçamento de física medido é de ~350 a 400 inimigos).
- **Papel de cada estrutura:** nós = arenas de combate; conectores = corredores com risco de parede; painéis = áreas abertas com muita fronteira (muito risco de ejeção); conectores de placa = gargalos; nó central = clímax. As cenas atuais viram protótipos: `sala_desfiladeiro` → conector, `sala_ninho` → nó.
- **Dificuldade dos spawners:** um orçamento global de spawn dividido entre os spawners vivos; ao matar um, os restantes aceleram (a dificuldade sobe sem estourar o teto de física) e o último fica frenético. Spawners rastreáveis (indicador ou pulso visível) e com silhueta própria.

### 4.5 Cenas atuais

- `hex_phase.tscn` (**cena principal**): a fase hexagonal, montada por código (`HexPhaseBuilder`), com a câmara de partida no nó de entrada (`StartChamber` e terminais), a eclusa (`Airlock`), o `StructureTracker`, o `PhaseCamera` e o `EjectionConsequence`.
- `sala_entrada.tscn`: bancada (foi a câmara inicial até out/2026): spawn, paredes de titânio fora do grupo `Walls`, `TerminalModo`, `EvolutionTerminal` e `PortaSaida` → fase.
- `sala_desfiladeiro.tscn`: corredores em zigue-zague com paredes reativas e janelas para o vácuo (`JanelasVacuo`).
- `sala_ninho.tscn`: arena circular de segmentos reativos (`wall_segment.tscn`), pilares de cobertura (`cover_pillar.tscn`) e o ninho com pool.
- `arena.tscn`: protótipo do início do projeto, só bancada de teste.
- `sala_nucleo.tscn` não existe; o papel de clímax passa ao nó central do hexágono.

---

## 5. Direção de arte e áudio (etapa 2)

Nada disto entra na etapa 1, salvo o que uma spec pedir.

- **Referência principal: Cocoon:** gráficos low poly, efeitos de luz e sombra e silhuetas que simulam 3D.
- **Cenário em corte:** as estruturas são vistas de cima, sem teto (como em FTL e Heat Signature). A referência citada no rascunho como "Heavyness body", registrada como **Heavenly Bodies** (2Pt Interactive, 2021, a confirmar), é vista de lado; fica como referência de game feel em gravidade zero e da descompressão.
- **Contraluz:** o sol, no centro da esfera, fica ao fundo; paredes, inimigos e jogador aparecem como silhuetas com contorno de luz. Modelo de luz detalhado na `dyson-wall.md` (seção 2.7), a ser compartilhado pelo inimigo 3D.
- **Cores:** o neon vibrante foi mantido, mas a testar contra tons pastel. Estrela laranja-avermelhada.
- **Paredes:** sem transparência (regra do modo Esmaecer); deformação final em aberto.
- **Inimigos com aspecto 3D:** a spec `fractal-enemy-render-3d.md` continua aplicável, com ramos facetados (prismas low poly) em vez de tubos lisos.
- **Áudio:** efeitos sonoros e trilha sonora a definir.

---

## 6. Registro de problemas técnicos resolvidos

### 6.1 Direção do puxão nas paredes reativas

- **v1.0:** o puxão ia para o canto do bloco, porque o cálculo usava o `GlobalPosition` do `StaticBody2D`. A correção da época (`_visualRect.GlobalPosition + Size / 2`) só funciona em paredes sem rotação nem escala.
- **out/2026 (backlog 1.2):** em paredes giradas a conta errava de 120 a 280 px (verificado no motor) e a parede empurrava em vez de puxar. Correção: `ColorRect.GetGlobalTransform() * (Size / 2)` (`WallSensitive.GetWallCenter`). O pivô do `StaticBody2D` fica onde a cena o colocou (canto na `paredes_reativas.tscn`, centro na `wall_block.tscn`). Spec: `reactive-walls.md`.

### 6.2 Jogador atravessava todas as paredes na ejeção

- **Causa:** a ejeção desligava a máscara de colisão 1 inteira.
- **Solução (out/2026):** exceção de colisão (`AddCollisionExceptionWith`) só com as paredes do caminho, encontradas por uma consulta ao espaço físico (`IntersectShape`). Spec: `reactive-walls.md`.

### 6.3 Erro de compilação `IsManualFire`

- **Sintoma:** `Player does not contain a definition for IsManualFire` no `TerminalModo.cs`. Resolvido na época com uma propriedade no `Player.cs`.
- **Hoje (out/2026):** o modo fica no `RunData` e o `TerminalModo` pede a troca ao `GameState` (backlog 1.1, nota na `suit-evolution.md`).

### 6.4 Lanterna invisível no chão

- **Causa:** a luz 2D precisa de um `CanvasItem` para iluminar; a viewport sozinha não é um.
- **Solução:** um `ColorRect` de fundo (1920×1080) como primeiro nó da cena. (O `CanvasModulate` preto da solução original era o blecaute, retirado em out/2026.)

### 6.5 Suavização e limites da câmera (`Camera2D`)

- **Sintoma:** a suavização parecia não funcionar com os limites cravados em (0, 0, 1920, 1080), porque a tela cobria a sala inteira.
- **Solução da época:** `Zoom` entre 1,3× e 1,5× e `Position Smoothing Speed` 6,0.
- **Hoje (out/2026):** o `Camera2D` do `player.tscn` está com zoom 1× (o ajuste de zoom não ficou no código) e suavização 6,0. Na fase hexagonal, o `PhaseCamera` aplica os limites da fase e o zoom por tipo de estrutura; as salas separadas continuam travando a câmera em 1920×1080 (`SalaEntrada.cs`).

### 6.6 Aviso de ancoragem ao redimensionar `ColorRect` dentro de `Node2D`

- **Solução:** desligar o Anchor Mode na barra 2D do editor ou alterar `Size` pelo Inspector.

### 6.7 Transição de cena (`DoorTrigger.cs`)

- `Area2D` com `[Export] TargetScenePath`; ao detectar o `Player` vivo, pede a troca ao `ScreenFade` (`ScreenFade.ChangeScene`): a tela escurece, a troca acontece fora do callback de física e a cena nova clareia. Trava contra troca dupla e aviso se o destino estiver vazio. Usado na eclusa da fase (`Airlock`, extração: recarrega a própria fase; só ativa depois de liberar as saídas) e na `PortaSaida` da `sala_entrada` (bancada). O `RestartOnDeath` usa o mesmo caminho. Specs: `hex-phase.md` (fase B) e `start-chamber.md`.

### 6.8 Desempenho da horda

- O gargalo é a colisão, não o desenho: Motion Mode Floating, camada própria dos inimigos e separação por código. Medições e problema conhecido nas specs `fractal-enemy.md` (seção 11) e `fractal-enemy-movement.md` (seção 11).

---

## 7. Arquitetura C# e diretrizes de código

1. **Responsabilidade única:** o `_PhysicsProcess` só orquestra, chamando métodos pequenos; comportamentos do jogador como componentes no `player.tscn` (integridade, contato, coleta, aplicador de melhorias).
2. **Cláusulas de guarda** (early return) em vez de `if/else` aninhado.
3. **Strategy** para comportamentos intercambiáveis (modos da lanterna quando houver mais de dois, alvos das melhorias quando crescerem).
4. **Observer (sinais):** paredes, inimigos, HUD e jogador reagem sem referência direta (ex.: `BiomassChanged`, `IntegrityChanged`, `FlashlightModeChanged`).
5. **Registros estáticos** para sistemas com centenas de elementos (`FractalSwarm`, `BiomassField`): um laço em C#, sem consultas à física.
6. **Estado da partida separado do nó** (`RunData`), pronto para salvar em disco.

---

## 8. Referências visuais, de gameplay e de mercado

- **Cocoon (Geometric Interactive):** referência visual principal (low poly, luz e sombra, silhuetas).
- **FTL (Subset Games), Heat Signature (Suspicious Developments) e Space Haven (Bugbyte):** plano de jogo da fase (estruturas vistas de cima, sem teto, com o espaço em volta). Heat Signature também é referência da sucção por rombo no casco, que afeta inimigos e jogador.
- **Heavenly Bodies (2Pt Interactive), a confirmar:** game feel de gravidade zero e da descompressão (é visto de lado, diferente do plano da fase).
- **Pathogenic (Aberrant Labs):** referência de **comportamento orgânico** (tecido que reage). Desde out/2026, não é mais referência visual (membranas translúcidas e soft-body saíram do pilar visual).
- **Scorn (Ebb Software):** aberturas biomecânicas (esfíncteres), referência para a deformação da parede.
- **Sir, We Have an Orc Problem (Mumpitz Games):** volume de hordas, partículas e metaprogressão roguelite (feito na Godot por 2 pessoas em 4 meses, mais de US$ 360 mil no dia 1).
- **Balatro e Vampire Survivors:** loops mecânicos focados e gratificação imediata superam custos altos de produção gráfica.
- **Chris Zukowski (How To Market A Game):** preço de US$ 14,99 (+20% de margem para promoções), meta de 30.000 wishlists, Supporter Pack DLC no dia 1 (conversão de 5% a 11%) e demo pública 3 a 4 meses antes do Steam Next Fest.
- **Jonathan Blow (Order of the Sinking Star / The Witness):** sofisticação sistêmica e qualidade mecânica acima de narrativa linear cara.

---

## 9. Próximos passos

A lista de tarefas, decisões em aberto e dívidas técnicas fica no **`docs/BACKLOG.md`** (a v1.0 mantinha os próximos passos aqui). Ordem sugerida: decidir a estrutura da fase hexagonal (backlog 1.3), que destrava os spawners móveis (1.4) e o posicionamento da parede de fronteira (1.5).
