# Backlog do projeto

Lista única de tarefas pendentes, decisões em aberto e dívidas técnicas. Atualizada em 2026-10-21.

> **Como usar:** cada item aponta para a spec relacionada e traz um "ponto de partida" técnico. O ponto de partida é uma **leitura inicial do código, não uma solução aprovada**: antes de implementar, seguir o fluxo do CLAUDE.md (ler spec e código, propor plano, esperar aprovação). Ao concluir um item, removê-lo daqui e registrar o resultado na spec.

> **Redefinição de game design e level design (out/2026):** o rascunho do dev trocou as salas modulares por uma **fase hexagonal** (estruturas da esfera de Dyson interligadas), o ninho fixo por **spawners móveis** (objetivo da fase), retirou o **blecaute total** e adotou o **Cocoon** como referência de arte. Resumo no `docs/HANDOFF.md` (v2.0, seções 4 e 5). Os itens abaixo já estão atualizados.

## 1. Refinamentos do gameplay atual (prioridade do dev)

### 1.3 Fase hexagonal: estruturas interligadas (substitui "transição entre salas")

- **Contexto:** uma fase é um hexágono da esfera. Cada estrutura (nó central, nós periféricos, conectores de nós, conectores de placas e painéis) é uma sala, e todas são interligadas: o hexágono inteiro é a arena (HANDOFF v2.0, seção 4).
- **Problema atual:** só existe uma porta de mão única (`DoorTrigger` na `PortaSaida`, `sala_entrada` → `sala_ninho`, com `ChangeSceneToFile`), a troca é abrupta e cada sala recomeça do zero. A `SalaEntrada.cs` põe o jogador no único `SpawnPoint` e trava a câmera em 1920×1080.
- **Spec:** `docs/specs/hex-phase.md` (aprovada, out/2026). Decidido: planta da casca vista de fora da esfera (H1), uma cena por fase (H2), contornos por código e recheio no editor (H3), setor de teste primeiro e hexágono por parâmetro (H4), câmara inicial separada com entrada e extração pela eclusa e a fase recomeçando a cada entrada (H5).
- **Estado (out/2026): fase A validada, passos 1 a 6 testados pelo dev** (spec, seção 8):
  - geometria do setor e do hexágono (`HexPhaseLayout`);
  - fundo, pisos e paredes com aberturas gerados por código (`HexPhaseBuilder`);
  - giro de 30° (base dos painéis na horizontal);
  - estrutura atual no HUD e regra provisória do espaço (`StructureTracker`, `StructureHud`);
  - nascimento na eclusa, limites e zoom por tipo de estrutura (`PhaseCamera`);
  - sensibilidade das paredes por tipo;
  - hexágono completo por parâmetro, com a receita 2× como padrão e a parede que dorme fora da luz (Processo de Física da fase vazia de ~4 ms para 0,3 ms; spec, seção 4).
- **Próximo passo:** plano da fase B (porta da eclusa, transição de tela, extração, `PortaSaida` → fase, reinício na morte), a aprovar pelo dev.
- **Antes da fase B:** mover a `hex_phase.tscn` de `res://upgrades/` para `res://` pelo painel Sistema de Arquivos da Godot.
- **Em aberto na spec:** sensibilidade das paredes por tipo (a testar e definir pelo dev); papel e recheio de cada tipo de estrutura; hexágonos vizinhos; a regra provisória do espaço até a consequência da ejeção ser decidida. Simulação por proximidade fica com o 1.4. Geração procedural fica para depois.

### 1.4 Spawners móveis e objetivo da fase (novo)

- **Contexto:** cerca de 10 spawners por fase circulam pela fase inteira gerando inimigos o tempo todo. O objetivo do jogador é eliminá-los; a cada spawner morto, a dificuldade aumenta.
- **Ponto de partida:** `NinhoSpawnerPooled.cs` (pool, `MaxConcurrentEnemies`, `MaxTotalEnemies`) e `FractalSwarm.cs` (registro e separação). O ninho fixo da `sala_ninho` dá lugar aos spawners.
- **Perguntas para a spec:**
  - Vida do spawner e como matá-lo (pela luz, como os inimigos? por tempo de exposição?).
  - Movimento: pelas arestas do grafo da fase (sem pathfinding)? Foge da luz?
  - Curva de dificuldade (sugestão: orçamento global de spawn dividido entre os spawners vivos; ao matar um, os restantes aceleram, sem estourar o teto de física; o último fica frenético).
  - Rastreabilidade numa fase grande (indicador no HUD, pulso visível) e silhueta própria.
  - O que acontece quando todos morrem (fase vencida → próxima fase? volta à câmara?).
  - Tetos globais (`MaxTotalEnemies`) e um pool compartilhado entre os spawners.
  - Desempenho: reavaliar o problema conhecido da `fractal-enemy-movement.md` (seção 11).
- **Depende de:** 1.3 (estrutura e continuidade da fase).
- **Spec:** criar (nome a confirmar, ex.: `docs/specs/mobile-spawners.md`).

### 1.5 Parede de fronteira (dyson-wall), fases A e B (novo)

- **Contexto:** modelo-alvo decidido pelo dev (out/2026): só as paredes de fronteira com o espaço são reativas; elas abrem uma fenda, sugam e ejetam o jogador (`PlayerEjected`). Paredes internas entre estruturas ficam rígidas. O `WallSensitive.cs` (spec `reactive-walls.md`) é transitório.
- **Sucção (decidida, out/2026):** gradual pela distância, como numa descompressão (raio sem retorno, rajada no rompimento), sem tirar o controle do jogador; afeta também inimigos e orbes. Toca no `Player.cs` (aceleração externa), no `FractalEnemy.cs`/`FractalSwarm.cs` e no `BiomassField.cs` (spec, seções 2.6 e 6).
- **Spec:** `docs/specs/dyson-wall.md` (proposta). Protótipo de referência: `docs/reference/dyson-wall-prototype.html`.
- **Ponto de partida:** a fase A (geometria e irritação) pode começar numa cena de teste isolada (`wall_test.tscn`), sem depender do 1.3. A `LightDamageArea` já chama `AddIrritation` nos corpos do grupo `Walls`, mas entrega o corpo inteiro, não as peças (nota na seção 6 da spec).
- **Para fechar a fase B:** decidir a consequência da ejeção (seção 2) e onde ficam as fronteiras em cada estrutura (1.3). Decisões em aberto da própria spec: renderização (W1/W2), cálculo da luz, posição do sol, se a sucção afeta inimigos, respiração em repouso.

### 1.6 Visibilidade sem blecaute nas cenas atuais (pequeno, no editor)

- **Contexto:** o rascunho de out/2026 retirou o blecaute total (inimigos em silhueta). Estado das cenas em 2026-10-21: `CanvasModulate` preto na `sala_entrada`, `sala_desfiladeiro` e `arena`; oculto (`visible = false`) na `sala_ninho`, que estava em teste pelo dev. Na `sala_desfiladeiro`, o `Background` também está oculto, o que contraria a regra do fundo obrigatório: conferir se é de propósito.
- **O que fazer:** o dev escolhe uma cor provisória de `CanvasModulate` para testar as mecânicas sem blecaute. O `ColorRect` de fundo continua obrigatório (sem ele a luz da lanterna não aparece). Arte final na etapa 2.
- **Spec:** nenhuma (ajuste de cena). Conferir depois se a lanterna continua legível sobre fundos claros.

## 2. Decisões de design em aberto

| Item | Onde está registrado | Observação |
|---|---|---|
| Consequência de ser ejetado para o espaço | `dyson-wall.md` (2.6), `reactive-walls.md` (D5), `player-health-death.md` (decisão 7) | O espaço **não é navegável** ("vácuo como atalho" continua adiado). Opções: morte, dano, volta ao último nó, perda de parte da biomassa carregada. Sugestão: fade e volta ao último nó com uma perda. Provisório para testar a fase hexagonal: volta à eclusa sem perdas (`hex-phase.md`, seção 10) |
| Deformação final da parede de fronteira | `dyson-wall.md` (seção 10) | Esmaecer é o padrão provisório. Referências sugeridas: exocitose, estômato, esfíncter/íris (Scorn), peristalse |
| Detalhes da sucção nos inimigos e orbes | `dyson-wall.md` (seção 10) | Inimigo sugado solta biomassa? (proposto: não); spawners são afetados?; fendas viram estratégia dominante contra a horda? |
| Tiros no ALERTAR | CLAUDE.md (core loop) e HANDOFF | O core loop cita "luz e tiros", mas não existe tiro; os "modos de tiro" do `TerminalModo` são modos da lanterna |
| Melhoria "Potência da lanterna" | `suit-evolution.md` (decisão 4) | Adiada até ter requisitos; volta junto com o valor no enum `SuitUpgrade.TargetStat` |
| Salvar progresso em disco | `suit-evolution.md` (decisão 2) | `RunData` já separado do nó; salvar = converter o `RunData` |
| Velocidade e separação dos fragmentos | `fractal-enemy.md` (fase 2) | Iguais às do pai; testar outras possibilidades (ex.: menor = mais rápido) |
| Valores definitivos de `MaxConcurrentEnemies` e `MaxTotalEnemies` | `fractal-enemy.md` (seção 10) | Hoje 200 e 350 na `sala_ninho`; decidir com os spawners móveis (1.4). Variantes A2/A3 do teto anotadas |
| Cor do `CanvasModulate` (visibilidade sem blecaute) | HANDOFF v2.0 (3.2), item 1.6 | Provisória na etapa 1; arte na etapa 2 |
| Neon × tons pastel | HANDOFF v2.0 (seção 5), `fractal-enemy-render-3d.md` | Testar na etapa 2 |
| Cor da parede irritada | `dyson-wall.md` (seção 10) | Vermelho no `WallSensitive.cs` × verde-água na dyson-wall |
| Orbe de biomassa com luz própria | `biomass-collection.md` (seção 10) | Sem blecaute, passa a ser só estética (etapa 2); medir o custo antes |
| Referência de cenário em corte | HANDOFF v2.0 (seções 5 e 8) | Citada no rascunho como "Heavyness body"; registrada como Heavenly Bodies (2Pt Interactive, 2021), a confirmar |

## 3. Dívidas técnicas

| Item | Onde está registrado | Observação |
|---|---|---|
| Queda de FPS com o jogador do lado de fora da parede e a horda aglomerada do lado de dentro | `fractal-enemy-movement.md` (seção 11) | Prioridade baixa; começar por instrumentação temporária. Reavaliar com os spawners móveis (1.4) |
| `TakeDamage` soma `_time += 0.2f` a cada frame sob a luz (pulsação acelera e o flash fica sempre ligado) | `fractal-enemy.md` (seção 10) | Revisar junto com o flash de dano |
| Teste pendente da fase 1 do inimigo: a vida não regenera fora do feixe | `fractal-enemy.md` (seção 8) | Só falta o teste no editor |
| `Player` em Motion Mode Grounded | `fractal-enemy.md` (seção 10) | Avaliar Floating (afeta como o jogador desliza nas paredes) |
| Grupo `"Enimies"` (erro de digitação) na cena do inimigo | `fractal-enemy.md` (seção 7) | Nada depende dele hoje |
| `CollisionShape2D` do inimigo escalado (0.2) em vez de raio ajustado | `fractal-enemy.md` (seção 10) | Boa prática da Godot: ajustar o raio da forma |
| `TerminalModo` lê a tecla por polling | `suit-evolution.md` (notas) | Só importa se ele ganhar interface; usar `_UnhandledInput` como o `EvolutionTerminal` |
| Pool de fragmentos | `fractal-enemy.md` (seção 5) | Só se matar muitos inimigos de uma vez causar engasgo |
| `Player._PhysicsProcess` longo, com três estados (normal, ejetado, morto) em `if/else` | código (`Player.cs`) | Se surgirem mais estados (ex.: ejetado para o espaço), considerar o padrão State. A sucção da dyson-wall não é um estado novo (o jogador mantém o controle): entra como aceleração externa |
| Entalhes nas quinas convexas das paredes geradas | `hex-phase.md` (seção 5) | Só visual, do lado de fora; resolver na arte (etapa 2) |
| Paredes da `sala_desfiladeiro` não são instâncias de uma cena de parede | `reactive-walls.md` (seção 4) | Cada uma tem os próprios `[Export]`: ajustar um valor exige mudar parede por parede ou o padrão no código. Prioridade baixa (as paredes de fronteira serão substituídas pela dyson-wall) |

## 4. Etapa 2 (não fazer agora)

- Fase hexagonal, compressão e alívio (`hex-phase.md`, decisão 7): paralaxe da megaestrutura vista pelos vãos (`Parallax2D`), estruturas em primeiro plano, contraste de luz (arenas iluminadas, corredores escuros), referências de escala e áudio (eco nas arenas, abafado nos corredores).
- Direção de arte Cocoon com contraluz: um modelo de luz único para parede, inimigo e jogador (`dyson-wall.md`, seção 2.7); cor da estrela laranja-avermelhada; neon × pastel.
- Visual 3D do inimigo fractal: `docs/specs/fractal-enemy-render-3d.md` (proposta; aplicável, com ramos facetados em vez de tubos lisos).
- Parede de fronteira, fases C (todos os modos de abertura) e D (contraluz): `docs/specs/dyson-wall.md`.
- Renderização em lote (bake + MultiMesh): adiada em `fractal-enemy.md` (seção 11).
- Partículas e iluminação na morte do inimigo, arte dos orbes e do HUD, efeitos de dano.
- Áudio: efeitos sonoros e trilha sonora (a definir).
