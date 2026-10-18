# Backlog do projeto

Lista única de tarefas pendentes, decisões em aberto e dívidas técnicas. Atualizada em 2026-10-17.

> **Como usar:** cada item aponta para a spec relacionada e traz um "ponto de partida" técnico. O ponto de partida é uma **leitura inicial do código, não uma solução aprovada**: antes de implementar, seguir o fluxo do CLAUDE.md (ler spec e código, propor plano, esperar aprovação). Ao concluir um item, removê-lo daqui e registrar o resultado na spec.

## 1. Refinamentos do gameplay atual (prioridade do dev)

### 1.1 Modo da lanterna deve valer em todas as salas

- **Problema:** a escolha do modo da lanterna (manual/automático) feita no `TerminalModo` da câmara volta ao padrão ao trocar de sala.
- **Ponto de partida:** o modo é a propriedade `Player.IsManualFire` (`[Export]`, padrão `true`). O `TerminalModo` altera essa propriedade no jogador da sala atual, e cada sala cria um jogador novo a partir do `player.tscn`, por isso a escolha se perde. O lugar natural para guardar a escolha é o `GameState`/`RunData` (já sobrevive às trocas de cena e já está preparado para salvar em disco), com o jogador lendo o valor ao nascer.
- **Spec:** nenhuma ainda (é pequeno; pode entrar como nota na spec da lanterna ou da evolução).

### 1.2 Empuxo das paredes reativas (direção e colisão)

- **Problemas:**
  1. Alguns blocos empurram em vez de puxar; outros puxam na direção errada.
  2. Durante o empuxo, o jogador atravessa **todas** as paredes da sala, não só o trecho irritado.
- **Ponto de partida (`WallSensitive.cs` e `Player.cs`):**
  - O centro da parede é calculado como `_visualRect.GlobalPosition + _visualRect.Size / 2`. Isso ignora rotação e escala: em paredes **rotacionadas** (ex.: os segmentos diagonais da `sala_ninho`), o ponto calculado não é o centro real, e a direção do empuxo sai errada — candidato forte para o problema 1. Calcular o centro transformando o centro local do `ColorRect` pela transformação global dele resolve rotação e escala. Confirmar testando.
  - O vetor é `(wallCenter - playerPosition)`, ou seja, puxa o jogador **para** a parede; o comentário no código diz o contrário ("do centro da parede apontando para o jogador"). Confirmar com o dev qual é o comportamento desejado (o handoff fala em força que "puxa o cientista para o seu centro geométrico e o ejeta").
  - `Player.ApplyEjection` chama `SetCollisionMaskValue(1, false)`: a máscara 1 inteira é desligada por `_ejectionDuration` (0,8 s), e todas as paredes (camada 1) deixam de colidir — causa do problema 2. A Godot permite exceção de colisão com um corpo específico (`AddCollisionExceptionWith` / `RemoveCollisionExceptionWith`), o que limitaria o "atravessar" à parede irritada.
  - Bônus: `AddIrritation` imprime um `GD.Print` a cada chamada (todo frame sob a luz), poluindo o Output; e o `_Ready` imprime "SUCESSO" para cada parede.
- **Spec:** nenhuma ainda (mecânica do HANDOFF, seção 3.3). Propor uma spec curta de "paredes reativas" antes de corrigir.

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
| `HANDOFF.md` desatualizado (ex.: `_counterThrustDamping`) | `docs/HANDOFF.md` | O código e o CLAUDE.md valem mais |

## 4. Etapa 2 (não fazer agora)

- Visual 3D do inimigo fractal: `docs/specs/fractal-enemy-render-3d.md` (proposta).
- Renderização em lote (bake + MultiMesh): adiada em `fractal-enemy.md` (seção 11).
- Partículas e iluminação na morte do inimigo, arte dos orbes e do HUD, áudio, efeitos de dano.
