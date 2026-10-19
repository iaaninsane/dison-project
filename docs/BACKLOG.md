# Backlog do projeto

Lista única de tarefas pendentes, decisões em aberto e dívidas técnicas. Atualizada em 2026-10-19.

> **Como usar:** cada item aponta para a spec relacionada e traz um "ponto de partida" técnico. O ponto de partida é uma **leitura inicial do código, não uma solução aprovada**: antes de implementar, seguir o fluxo do CLAUDE.md (ler spec e código, propor plano, esperar aprovação). Ao concluir um item, removê-lo daqui e registrar o resultado na spec.

## 1. Refinamentos do gameplay atual (prioridade do dev)

### 1.3 Transição entre salas suave e nos dois sentidos

- **Problemas:** a troca de sala é abrupta (o jogador "nasce" instantaneamente na sala seguinte) e não há como voltar à sala anterior.
- **Ponto de partida:**
  - Hoje só existe uma porta de mão única: `DoorTrigger` na `PortaSaida` da câmara (`sala_entrada` → `sala_ninho`, com `ChangeSceneToFile`). A `SalaEntrada.cs` sempre coloca o jogador no único `SpawnPoint`.
  - É uma tarefa grande, que pede **spec própria**. Perguntas de design para a spec:
    - Trocar de cena com uma transição (ex.: escurecer e clarear a tela) **ou** salas conectadas na mesma cena, com a câmera passando de uma para outra?
    - Portas nos dois sentidos, cada uma com o seu ponto de chegada (a porta precisa saber "por onde o jogador entra" na sala de destino; o `GameState` pode guardar a porta de origem).
    - O que acontece com a sala que o jogador deixou: inimigos e orbes reaparecem do zero ou a sala guarda o estado?
    - A integridade do traje deve passar a persistir entre salas da mesma incursão? (hoje começa cheia em cada sala; spec `suit-evolution.md`, decisão 6)
    - Voltar à câmara vivo deposita a biomassa carregada (isso já funciona: o depósito acontece em toda entrada na câmara).
  - Relação com o futuro gerador de labirintos (`MazeGenerator.cs`, citado no handoff, não existe).
- **Spec:** criar `docs/specs/room-transition.md` (nome a confirmar).

## 2. Decisões de design em aberto

| Item | Onde está registrado | Observação |
|---|---|---|
| Tiros no ALERTAR | CLAUDE.md (core loop) e HANDOFF | O core loop cita "luz e tiros", mas não existe tiro; os "modos de tiro" do `TerminalModo` são modos da lanterna |
| Melhoria "Potência da lanterna" | `suit-evolution.md` (decisão 4) | Adiada até ter requisitos; volta junto com o valor no enum `SuitUpgrade.TargetStat` |
| Salvar progresso em disco | `suit-evolution.md` (decisão 2) | `RunData` já separado do nó; salvar = converter o `RunData` |
| Velocidade e separação dos fragmentos | `fractal-enemy.md` (fase 2) | Iguais às do pai; testar outras possibilidades (ex.: menor = mais rápido) |
| Orbe de biomassa com luz própria | `biomass-collection.md` (seção 10) | Testar a opção de uma `PointLight2D` fraca por orbe; medir o custo antes |
| Valores definitivos de `MaxConcurrentEnemies` e `MaxTotalEnemies` | `fractal-enemy.md` (seção 10) | Hoje 200 e 350 na `sala_ninho`; variantes A2/A3 do teto anotadas |
| Iluminação da `sala_ninho` | `fractal-enemy.md` (seção 10) | Em teste pelo dev (Background/CanvasModulate); não "corrigir" sem combinar |
| Sala final (`sala_nucleo`) e gerador de labirintos | HANDOFF | Nenhum dos dois existe no projeto |
| O que acontece do outro lado da parede depois da ejeção | `reactive-walls.md` (D5) | Na `sala_ninho`, a ejeção leva o jogador para fora da arena; toca no "vácuo", adiado para depois da demo |

## 3. Dívidas técnicas

| Item | Onde está registrado | Observação |
|---|---|---|
| Queda de FPS com o jogador do lado de fora da parede e a horda aglomerada do lado de dentro | `fractal-enemy-movement.md` (seção 11) | Prioridade baixa; começar por instrumentação temporária |
| `TakeDamage` soma `_time += 0.2f` a cada frame sob a luz (pulsação acelera e o flash fica sempre ligado) | `fractal-enemy.md` (seção 10) | Revisar junto com o flash de dano |
| Teste pendente da fase 1 do inimigo: a vida não regenera fora do feixe | `fractal-enemy.md` (seção 8) | Só falta o teste no editor |
| `Player` em Motion Mode Grounded | `fractal-enemy.md` (seção 10) | Avaliar Floating (afeta como o jogador desliza nas paredes) |
| Grupo `"Enimies"` (erro de digitação) na cena do inimigo | `fractal-enemy.md` (seção 7) | Nada depende dele hoje |
| `CollisionShape2D` do inimigo escalado (0.2) em vez de raio ajustado | `fractal-enemy.md` (seção 10) | Boa prática da Godot: ajustar o raio da forma |
| `TerminalModo` lê a tecla por polling | `suit-evolution.md` (notas) | Só importa se ele ganhar interface; usar `_UnhandledInput` como o `EvolutionTerminal` |
| Pool de fragmentos | `fractal-enemy.md` (seção 5) | Só se matar muitos inimigos de uma vez causar engasgo |
| `Player._PhysicsProcess` longo, com três estados (normal, ejetado, morto) em `if/else` | código (`Player.cs`) | Se surgirem mais estados, considerar o padrão State |
| Paredes da `sala_desfiladeiro` não são instâncias de uma cena de parede | `reactive-walls.md` (seção 4) | Cada uma tem os próprios `[Export]`: ajustar um valor exige mudar parede por parede ou o padrão no código. Prioridade baixa |
| `HANDOFF.md` desatualizado (ex.: `_counterThrustDamping`; a fórmula do centro da parede na seção 5.1) | `docs/HANDOFF.md` | O código e o CLAUDE.md valem mais |

## 4. Etapa 2 (não fazer agora)

- Visual 3D do inimigo fractal: `docs/specs/fractal-enemy-render-3d.md` (proposta).
- Renderização em lote (bake + MultiMesh): adiada em `fractal-enemy.md` (seção 11).
- Partículas e iluminação na morte do inimigo, arte dos orbes e do HUD, áudio, efeitos de dano.
