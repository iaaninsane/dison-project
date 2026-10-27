# Backlog do projeto

Lista única de tarefas pendentes, decisões em aberto e dívidas técnicas. Atualizada em 2026-10-27.

> **Como usar:** cada item aponta para a spec relacionada e traz um "ponto de partida" técnico. O ponto de partida é uma **leitura inicial do código, não uma solução aprovada**: antes de implementar, seguir o fluxo do CLAUDE.md (ler spec e código, propor plano, esperar aprovação). Ao concluir um item, removê-lo daqui e registrar o resultado na spec.

> **Redefinição de game design e level design (out/2026):** o rascunho do dev trocou as salas modulares por uma **fase hexagonal** (estruturas da esfera de Dyson interligadas), o ninho fixo por **spawners móveis** (objetivo da fase), retirou o **blecaute total** e adotou o **Cocoon** como referência de arte. Resumo no `docs/HANDOFF.md` (v2.0, seções 4 e 5). Os itens abaixo já estão atualizados.

## 1. Refinamentos do gameplay atual (prioridade do dev)

> **1.3 concluído (out/2026):** fase hexagonal, fases A e B validadas pelo dev (`docs/specs/hex-phase.md`). A câmara leva à fase pela `PortaSaida`; a fase tem eclusa (entrada e extração), transição de tela (`ScreenFade`) em toda troca de cena e recomeça do zero a cada entrada. Os pontos em aberto foram para a seção 2; a fase C da spec (spawners na fase) foi feita no 1.4.

> **1.7 concluído (out/2026):** câmara de partida no nó inicial, validada pelo dev (`docs/specs/start-chamber.md`). O jogo começa na fase (cena principal); o nó de entrada é a câmara (portões, paredes inertes, terminais do traje e "Liberar saídas") até o jogador liberar as saídas (sinal `IncursionStarted`); morte, ejeção (igual à morte) e extração pela eclusa recarregam a fase em modo câmara. A `sala_entrada` virou bancada.

> **1.4 concluído (out/2026):** spawners móveis e objetivo da fase, validados pelo dev (`docs/specs/mobile-spawners.md`; decisões S1 a S9, com a S6 revista: uma seta por spawner). Ao liberar as saídas, 10 spawners andam pelas rotas da fase (grafo tirado da geometria), geram inimigos só com o jogador perto, a partir de um estoque compartilhado (`EnemyPool`), num ritmo que acelera a cada spawner morto (`SpawnerDirector`); a luz os mata (fogem em pânico) e soltam biomassa; encostar fere; inimigos longe do jogador são recolhidos; HUD com a contagem, setas na borda e "Fase limpa: volte à eclusa". Pior caso medido: passo de física constante de ~4 ms; o engasgo isolado na divisão em massa virou o item 1.8. Corrigidos no caminho: a "cola" do jogador no spawner e a "carona" do modo Grounded (Floor Layers do `Player` sem a camada 2).

> **1.5 concluído (out/2026):** parede de fronteira, fases A e B (`docs/specs/dyson-wall.md`; decisões A1 a A6 e B1 a B6, achados e medições nas seções 8 e 10). As paredes externas da fase hexagonal são `DysonWall`, uma por trecho reto (286 no hexágono), com sensibilidade 100/40/30 por tipo de estrutura. A luz irrita cada peça; a peça no limite abre uma fenda (Esmaecer); a colisão abre um vão; e a sucção puxa jogador, inimigos, orbes e spawners. Os spawners resistem mais, andam num trajeto perto das paredes e voltam à rota com mola (`mobile-spawners.md`, S10 e S11). Quem cruza a fenda sai da fase: o jogador morre pelo `LostInSpace`; inimigos e orbes somem sem biomassa; o spawner conta como destruído, sem orbes. A fase A foi validada pelo dev, e a fase B foi testada por ele nos passos 1 a 6 (commits fb46839 a 4b5d7ee). **Observação:** o passo 7 foi verificado só no descartável: 10 fendas seguidas com troca de cena, sem erros nem nós órfãos; na tela cheia com aberturas, Renderização (CPU) de 0,04 ms e ~0,25 ms de Física por parede abrindo. A confirmação no editor e a medição na fase com a horda ficaram no 1.9. As fases C e D são etapa 2 (seção 4).

### 1.6 Visibilidade sem blecaute nas cenas atuais (pequeno, no editor)

- **Contexto:** o rascunho de out/2026 retirou o blecaute total (inimigos em silhueta). Estado das cenas em 2026-10-21: `CanvasModulate` preto na `sala_entrada`, `sala_desfiladeiro` e `arena`; oculto (`visible = false`) na `sala_ninho`, que estava em teste pelo dev. Na `sala_desfiladeiro`, o `Background` também está oculto, o que contraria a regra do fundo obrigatório: conferir se é de propósito.
- **O que fazer:** o dev escolhe uma cor provisória de `CanvasModulate` para testar as mecânicas sem blecaute. O `ColorRect` de fundo continua obrigatório (sem ele a luz da lanterna não aparece). Arte final na etapa 2.
- **Spec:** nenhuma (ajuste de cena). Conferir depois se a lanterna continua legível sobre fundos claros.

### 1.8 Engasgo de um quadro na divisão em massa (pool de fragmentos) (novo)

- **Contexto:** medido no fechamento do 1.4 (out/2026). Quando dezenas de inimigos se dividem no mesmo quadro (massacre com a lanterna na horda do último spawner), esse quadro passa do orçamento: pico isolado de ~27 ms no Processo de Física no editor (medição do dev, com inimigos de teste a 40 px/s). No descartável, dividir 60 inimigos no mesmo quadro (120 fragmentos) custou 28 a 39 ms, e 120 (240 fragmentos), 35 a 42 ms, com e sem aquecimento do .NET (não é a primeira execução do código). No quadro seguinte volta a 3 a 8 ms; o passo constante fica em ~4 ms.
- **Causa:** cada divisão instancia nós novos (`FractalEnemy.SpawnFragments`: `Instantiate`, `AddChild` adiado e `_Ready`), ~0,15 a 0,3 ms por fragmento.
- **Ponto de partida:** pool de fragmentos: o `FractalEnemy` pede os fragmentos ao `EnemyPool` (que hoje só guarda inimigos da geração 0) em vez de instanciar; cuidar do `PoolSize` (os fragmentos contam?) e do `MaxTotalEnemies`. Alternativa mais simples: espalhar a criação dos fragmentos por alguns quadros (orçamento de criação por quadro). Na `sala_ninho` (sem `EnemyPool`), os fragmentos continuam instanciados.
- **Spec:** criar quando for feito (ou ampliar a `fractal-enemy.md`, seção 5, "Custo de instanciar"). Medições na `mobile-spawners.md` (seção 10, "Desempenho").

### 1.9 Fase de balanceamento (novo)

- **Contexto:** pedido do dev (out/2026), depois das mecânicas do 1.4: uma passada de balanceamento dos valores para tornar o gameplay mais satisfatório. Os `[Export]` de todas as specs são "valores em teste" (pontos de partida); valores fora do padrão encontrados até lá ficam para esta fase.
- **Valor fora do padrão conhecido:** `fractal_enemy.tscn` > `Speed`. A linha `Speed = 10.0` saiu da cena no commit `d01d317` (o ↺ do Inspector voltou ao padrão do código, 80), e a correção não chegou ao disco: hoje os inimigos perseguem a 80 px/s em vez de 10. A geração por proximidade e o recolhimento do 1.4 foram pensados com 10 (`mobile-spawners.md`, seção 2), e a medição de desempenho do dev foi feita com 40.
- **O que revisar** (valores em teste, por spec):
  - spawners (`mobile-spawners.md`, seção 4): vida 300, velocidade 150, pânico ×2 por 2 s, orçamento de 1,5 inimigo/s +10% por morte, +5% de velocidade por morte, raio de geração 2000, recolhimento a 3000 px por 3 s, estoque 60 e teto 350, 20 orbes na morte;
  - inimigo (`fractal-enemy.md` e `fractal-enemy-movement.md`, seção 4): velocidade, vida, dano da luz (35/s), gerações e fragmentos, separação;
  - golpe e integridade (`player-health-death.md`): dano 10, empurrão 200, imunidade 0,8 s;
  - paredes por tipo (o `MaxIrritation` da `DysonWall`: 100, 40 e 30; `hex-phase.md`, seção 4, e `dyson-wall.md`, B2), a sucção das fendas (`dyson-wall.md`, seção 4: raio sem retorno, rajada, fatores de inimigos, orbes e spawners) e zooms da câmera;
  - biomassa e evolução (`biomass-collection.md`, `suit-evolution.md`): ímã, coleta, custos e ganhos das melhorias, taxa da morte;
  - jogador (`Player.cs`): empuxo 450, velocidade máxima 600, atrito.
- **Pendente do 1.5 (passo 7 da fase B):** medir a fase hexagonal com a horda (200+ inimigos), paredes acordadas e uma fenda aberta, e confirmar no editor as 10 fendas seguidas com troca de cena (roteiro na `dyson-wall.md`, seção 9, item 7). No descartável passou. Se o primeiro rompimento da sessão engasgar (pico de ~7,5 ms: a primeira compilação do código da abertura), aquecer esse código ao carregar a fase, debaixo do escurecer.
- **Ponto de partida:** definir com o dev o que é "satisfatório" em metas verificáveis (ex.: tempo para matar um spawner, duração de uma incursão, quantos golpes até morrer, ritmo da curva de dificuldade) e um roteiro de teste por meta; ajustar no Inspector, digitando os valores (o ↺ volta ao padrão do script, `CLAUDE.md`), e registrar os escolhidos nas specs.
- **Spec:** criar (ex.: `docs/specs/balancing.md`), com as metas e a tabela dos valores escolhidos.

## 2. Decisões de design em aberto

| Item | Onde está registrado | Observação |
|---|---|---|
| Deformação final da parede de fronteira | `dyson-wall.md` (seção 10) | Esmaecer é o padrão provisório. Referências sugeridas: exocitose, estômato, esfíncter/íris (Scorn), peristalse |
| Fendas como arma contra a horda e os spawners | `dyson-wall.md` (seção 10) | Observar nos testes (fase de balanceamento, 1.9) se vira estratégia dominante; se virar, ajustar `EnemySuctionFactor` e `SpawnerSuctionFactor`. Biomassa do inimigo sugado e sucção dos spawners já decididas (B3 e B4) |
| Tiros no ALERTAR | CLAUDE.md (core loop) e HANDOFF | O core loop cita "luz e tiros", mas não existe tiro; os "modos de tiro" do `TerminalModo` são modos da lanterna |
| Melhoria "Potência da lanterna" | `suit-evolution.md` (decisão 4) | Adiada até ter requisitos; volta junto com o valor no enum `SuitUpgrade.TargetStat` |
| Salvar progresso em disco | `suit-evolution.md` (decisão 2) | `RunData` já separado do nó; salvar = converter o `RunData` |
| Velocidade e separação dos fragmentos | `fractal-enemy.md` (fase 2) | Iguais às do pai; testar outras possibilidades (ex.: menor = mais rápido). Na fase de balanceamento (1.9) |
| Valores definitivos de `MaxConcurrentEnemies` e `MaxTotalEnemies` | `fractal-enemy.md` (seção 10), `mobile-spawners.md` (seção 4) | Hoje 200 e 350 na `sala_ninho`. Na fase (1.4): `PoolSize` 60 (papel do `MaxConcurrentEnemies`) e `MaxTotalEnemies` 350, no `EnemyPool`; no pior caso medido, passo constante de ~4 ms (`mobile-spawners.md`, seção 10). Continuam pontos de partida; revisar na fase de balanceamento (1.9). Variantes A2/A3 do teto anotadas |
| Cor do `CanvasModulate` (visibilidade sem blecaute) | HANDOFF v2.0 (3.2), item 1.6 | Provisória na etapa 1; arte na etapa 2 |
| Neon × tons pastel | HANDOFF v2.0 (seção 5), `fractal-enemy-render-3d.md` | Testar na etapa 2 |
| Cor da parede irritada | `dyson-wall.md` (seção 10) | Vermelho no `WallSensitive.cs` × verde-água na dyson-wall |
| Orbe de biomassa com luz própria | `biomass-collection.md` (seção 10) | Sem blecaute, passa a ser só estética (etapa 2); medir o custo antes |
| Sensibilidade das paredes por tipo de estrutura | `hex-phase.md` (seções 4 e 10), `dyson-wall.md` (B2) | Hoje 100 (nós e painéis), 40 (conectores de nós) e 30 (conectores de placa), o `MaxIrritation` da `DysonWall`: ~5 s, 2 s e 1,5 s de luz no centro do feixe até a fenda abrir. Provisórios, o dev vai testar e definir na fase de balanceamento (1.9) |
| Papel e recheio de cada tipo de estrutura | `hex-phase.md` (seção 10), HANDOFF v2.0 (seção 4) | Sugestão: nós = arenas, conectores = corredores com risco de parede, painéis = áreas abertas, placas = gargalos, nó central = clímax. O recheio (pilares, costelas, gargalos) ainda não existe: o 1.4 foi feito sem ele |
| "Horda que persegue": reposicionar perto do jogador, logo fora da tela, o inimigo que fica longe, em vez de recolhê-lo | `mobile-spawners.md` (seção 10, "Ideias para depois") | Ideia do dev (out/2026), para implementação futura. Referência: chefes do Vampire Survivors (os comuns somem). Muda a S5 (pressão longe dos spawners) e a compressão e o alívio; o ponto precisa estar no piso e sem parede no meio |
| Forma do trajeto dos spawners | `mobile-spawners.md` (S10) | Senoide variada, rente à parede trocando de lado ou ruído orgânico, escolhida no Inspector (`PathShape`). O dev decide depois se ficam as três ou uma |
| Voltar à estrutura depois de ejetado (empuxo alto ou "superboost") | `dyson-wall.md` (seção 10, "Ideias para depois") | Ideia do dev (out/2026), para implementação futura. Vencer o empurrão da fenda do lado de fora e voltar por ela. Parente dos "Foguetes de mega propulsão" (adiados para depois da demo) e da melhoria de empuxo (candidata, `suit-evolution.md`, decisão 4). Pede rever o `LostInSpace` de ~2 s (B1) |
| Hexágonos vizinhos e progressão entre fases | `hex-phase.md` (seções 3 e 10) | Provavelmente depois da demo. Hoje, com todos os spawners mortos: "Fase limpa: volte à eclusa" e extração (S7, `mobile-spawners.md`) |
| Referência de cenário em corte | HANDOFF v2.0 (seções 5 e 8) | Citada no rascunho como "Heavyness body"; registrada como Heavenly Bodies (2Pt Interactive, 2021), a confirmar |

## 3. Dívidas técnicas

| Item | Onde está registrado | Observação |
|---|---|---|
| Queda de FPS com o jogador do lado de fora da parede e a horda aglomerada do lado de dentro | `fractal-enemy-movement.md` (seção 11) | Prioridade baixa; começar por instrumentação temporária. Reavaliado no 1.4 (`mobile-spawners.md`, seção 10): com geração por proximidade, recolhimento e tetos, a horda fica perto do jogador e limitada; o pior caso medido (último spawner, ~270 inimigos num conector de placa) ficou em ~4 ms por passo. O cenário da parede fina entre jogador e horda não foi medido |
| `TakeDamage` soma `_time += 0.2f` a cada frame sob a luz (pulsação acelera e o flash fica sempre ligado) | `fractal-enemy.md` (seção 10) | Revisar junto com o flash de dano |
| Teste pendente da fase 1 do inimigo: a vida não regenera fora do feixe | `fractal-enemy.md` (seção 8) | Só falta o teste no editor |
| `Player` em Motion Mode Grounded | `fractal-enemy.md` (seção 10) | Avaliar Floating (afeta como o jogador desliza nas paredes) |
| Grupo `"Enimies"` (erro de digitação) na cena do inimigo | `fractal-enemy.md` (seção 7) | Nada depende dele hoje |
| `CollisionShape2D` do inimigo escalado (0.2) em vez de raio ajustado | `fractal-enemy.md` (seção 10) | Boa prática da Godot: ajustar o raio da forma |
| `Player._PhysicsProcess` longo, com três estados (normal, ejetado, morto) em `if/else` | código (`Player.cs`) | Se surgirem mais estados (ex.: ejetado para o espaço), considerar o padrão State. A sucção da dyson-wall não é um estado novo (o jogador mantém o controle): entra como aceleração externa |
| Entalhes nas quinas convexas das paredes geradas | `hex-phase.md` (seção 5) | Só visual, do lado de fora; resolver na arte (etapa 2) |
| Paredes da `sala_desfiladeiro` não são instâncias de uma cena de parede | `reactive-walls.md` (seção 4) | Cada uma tem os próprios `[Export]`: ajustar um valor exige mudar parede por parede ou o padrão no código. Prioridade baixa (as paredes de fronteira serão substituídas pela dyson-wall) |

## 4. Etapa 2 (não fazer agora)

- Fase hexagonal, compressão e alívio (`hex-phase.md`, decisão 7): paralaxe da megaestrutura vista pelos vãos (`Parallax2D`), estruturas em primeiro plano, contraste de luz (arenas iluminadas, corredores escuros), referências de escala e áudio (eco nas arenas, abafado nos corredores).
- Direção de arte Cocoon com contraluz: um modelo de luz único para parede, inimigo e jogador (`dyson-wall.md`, seção 2.7); cor da estrela laranja-avermelhada; neon × pastel.
- Visual 3D do inimigo fractal: `docs/specs/fractal-enemy-render-3d.md` (proposta; aplicável, com ramos facetados em vez de tubos lisos).
- Parede de fronteira, fases C (todos os modos de abertura) e D (contraluz): `docs/specs/dyson-wall.md`. Para a fase D, as recomendações do plano: iluminação por código e o sol num `SunAnchor` no centro do hexágono.
- Renderização em lote (bake + MultiMesh): adiada em `fractal-enemy.md` (seção 11).
- Partículas e iluminação na morte do inimigo, arte dos orbes e do HUD, efeitos de dano.
- Áudio: efeitos sonoros e trilha sonora (a definir).
