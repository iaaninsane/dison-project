# Spec: Inimigo fractal (dano por luz e divisão celular)

- **Status:** validada (fase 2) e implementada (fase 1), valores em teste. Pendência na seção 8: um item da fase 1 (a vida não regenera fora do feixe).
- **Etapa:** 1 (mecânica)
- **Depende de:** lanterna e `LightDamageArea.cs` (dano por luz). Nenhuma outra spec.

> **Valores em teste:** esta é uma demo. Os atributos `[Export]` ainda estão em teste e não têm valores definitivos. A seção 4 mostra os valores que estão hoje no código e na cena, só como referência. Divergências entre esta spec e o Inspector são esperadas e não contam como erro. Quando os valores forem fechados, esta seção é atualizada.

## 1. Objetivo

O anticorpo fractal é o inimigo da demo. Ele é desenhado por código como uma árvore de ramos binários que pulsa e é destruído pela luz da lanterna. Na fase 2, ao morrer, ele se divide em fragmentos menores (divisão celular), criando a pressão de horda da sala do ninho. Serve ao trecho COMBATER do core loop.

## 2. Comportamento esperado

### Fase 1: implementada

- Quando o inimigo é instanciado, então desenha, via `_Draw`, uma árvore fractal binária recursiva com 4 troncos (cima, baixo, esquerda e direita). A profundidade vem de `RecursionDepth`.
- Enquanto vive, o desenho pulsa em ciclo senoidal. O que pulsa é o **ângulo de abertura dos ramos** (decidido), não a escala.
- O redesenho é limitado a `RedrawsPerSecond` por segundo, para aguentar hordas grandes.
- Quando o feixe da lanterna sobrepõe o inimigo, então ele perde vida continuamente: `DamagePerSecond × delta` a cada frame de física.
- Ao receber dano, o inimigo pisca em branco por um instante (flash de dano, já existente no código).
- Quando o inimigo sai do feixe, então a vida para de cair e não regenera.
- Quando a vida chega a zero, então o inimigo morre. Sem pool: `QueueFree()`. Com pool: `Die()` chama o callback `OnDied` e o inimigo volta para o estoque do `NinhoSpawnerPooled`.

### Fase 2: implementada (divisão celular)

- Todo inimigo tem uma geração (`Generation`): 0 é o original; fragmentos são 1, 2...
- Cada geração é calculada a partir da configuração base, sem alterar os `[Export]` (`ApplyGeneration()`): vida = `MaxHealth × HealthFactorPerGeneration^g`; tamanho (ramo do desenho e escala do nó de colisão) × `ScaleFactorPerGeneration^g`; profundidade da árvore = `RecursionDepth − DepthLossPerGeneration × g` (mínimo 1).
- Quando a vida chega a zero e a geração é menor que `MaxGeneration`, então o inimigo gera `FragmentCount` fragmentos da geração seguinte, distribuídos em ângulos iguais (com 2, opostos) a partir de um ângulo aleatório a cada morte, a `FragmentSpawnOffset` px do centro.
- Quando um inimigo da geração máxima morre, então não gera fragmentos.
- O próprio inimigo cria os fragmentos: instancia a própria cena (`SceneFilePath`), define a geração **antes** de entrar na árvore e adiciona o fragmento ao mesmo pai com `CallDeferred` (fim do frame).
- A divisão acontece uma única vez por morte (protegida pela trava `_isDead`).
- Fragmentos recém-nascidos ficam imunes a dano por `SpawnInvulnerability` segundos. Inimigos do ninho (geração 0) nascem vulneráveis.
- Ao ser reaproveitado pelo pool (`ActivateAt`), o inimigo volta para a geração 0.
- Os fragmentos ficam fora do pool e do `MaxConcurrentEnemies`, mas contam no teto total `MaxTotalEnemies` dos spawners (seção 10; na fase hexagonal, no `EnemyPool`, spec `mobile-spawners.md`).
- Velocidade (`Speed`) e raio de separação (`SeparationRadius`) dos fragmentos: iguais aos do pai por enquanto. **A testar no futuro:** outras possibilidades (ex.: proporcionais à geração, "menor = mais rápido").

## 3. Fora do escopo

- Ataque ao jogador (spec própria depois).
- Movimento e perseguição: **já existem no código** (perseguição simples em linha reta com `MoveAndSlide`), mas não fazem parte desta spec. Não mexer aqui; ajustes vão na spec própria de movimento. Exceção já decidida: o `CharacterBody2D` usa Motion Mode **Floating** (ver seção 11).
- Renderização em lote (bake de texturas + MultiMesh): adiada; ver seção 11.
- Partículas neon e iluminação temporária na morte (spec própria).
- Coleta de biomassa (spec própria).
- Dano por tiros ou explosões.
- Arte final, áudio e shaders (etapa 2).

## 4. Parâmetros ajustáveis (valores em teste)

Todos os valores abaixo estão **em teste** e não são definitivos (veja o aviso no topo). Quando a cena `fractal_enemy.tscn` sobrescreve o padrão do código, vale o valor da cena (Inspector).

### Implementados

| Parâmetro | Padrão no código | Valor na cena | Onde fica | Observação |
|---|---|---|---|---|
| `MaxHealth` (vida) | 100 | (padrão) | `FractalEnemy.cs` | `[Export]` |
| `DamagePerSecond` (dano da luz) | 35 | (padrão) | `LightDamageArea.cs` | `[Export]`. Mata o inimigo em ~2,9 s de luz contínua |
| `WallIrritationPerSecond` | 20 | (padrão) | `LightDamageArea.cs` | `[Export]`. Irritação nas paredes, citada só como referência |
| `RecursionDepth` (profundidade da árvore) | 5 | 4 | `FractalEnemy.cs` | `[Export]` |
| `BaseBranchLength` (tamanho do ramo principal) | 25 | 8 | `FractalEnemy.cs` | `[Export]` |
| `RedrawsPerSecond` | 20 | (padrão) | `FractalEnemy.cs` | `[Export]`. Otimização do redesenho |
| `NeonColor` | (0, 1, 0.5) | (padrão) | `FractalEnemy.cs` | `[Export]` |
| `Speed` | 80 | 10 | `FractalEnemy.cs` | `[Export]`. Movimento fora do escopo desta spec |
| Motion Mode (`CharacterBody2D`) | Grounded (padrão da Godot) | **Floating** | `fractal_enemy.tscn` | Propriedade do nó, não é valor de balanceamento. Decidido; ver seção 11 |
| `BaseBranchAngle` (ângulo médio dos ramos) | 0.4 rad | (padrão) | `FractalEnemy.cs` | `[Export]`. Centro da pulsação |
| `PulseAmplitude` (oscilação do ângulo) | 0.15 rad | (padrão) | `FractalEnemy.cs` | `[Export]`. Quanto o ângulo varia para cada lado |
| `PulseFrequency` (frequência da pulsação) | 0.6366 Hz | (padrão) | `FractalEnemy.cs` | `[Export]`. Pulsos completos por segundo; equivale ao antigo `Sin(_time × 4.0)` |
| Duração do flash de dano | 0.1 s | — | `FractalEnemy.cs` | Fixo no código |

### Fase 2 (divisão celular)

| Parâmetro | Padrão no código | Valor na cena | Onde fica | Observação |
|---|---|---|---|---|
| `Generation` | 0 | (padrão) | `FractalEnemy.cs` | Definido pelo pai ao criar o fragmento. **Nunca alterar no `fractal_enemy.tscn` base** (todos nasceriam como fragmentos); para testar, alterar só em instâncias |
| `HealthFactorPerGeneration` | 0.5 | (padrão) | `FractalEnemy.cs` | Vida multiplicada a cada geração |
| `ScaleFactorPerGeneration` | 0.6 | (padrão) | `FractalEnemy.cs` | Tamanho do desenho e da colisão multiplicado a cada geração |
| `DepthLossPerGeneration` | 1 | (padrão) | `FractalEnemy.cs` | Níveis da árvore perdidos a cada geração |
| `MaxGeneration` | 2 | (padrão) | `FractalEnemy.cs` | Geração que morre sem se dividir. Com 2 fragmentos: 1 + 2 + 4 = 7 inimigos por ancestral |
| `FragmentCount` | 2 | (padrão) | `FractalEnemy.cs` | Fragmentos por divisão |
| `FragmentSpawnOffset` | 8 px | (padrão) | `FractalEnemy.cs` | Distância do centro em que cada fragmento nasce |
| `SpawnInvulnerability` | 0,3 s | (padrão) | `FractalEnemy.cs` | Imunidade dos fragmentos ao nascer |

Com os valores atuais da cena (`RecursionDepth` 4, `BaseBranchLength` 8, `MaxHealth` 100, dano da luz 35/s):

| Geração | Vida | Tempo sob o feixe | Profundidade | Ramo principal | Raio de colisão |
|---|---|---|---|---|---|
| 0 | 100 | ~2,9 s | 4 | 8 px | ~6 px |
| 1 | 50 | ~1,4 s | 3 | 4,8 px | ~3,6 px |
| 2 | 25 | ~0,7 s | 2 | 2,9 px | ~2,2 px |

## 5. Casos de borda

- O feixe sobrepõe vários inimigos ao mesmo tempo: cada um recebe dano de forma independente. (Coberto: `LightDamageArea` mantém uma lista de inimigos no feixe.)
- O inimigo é removido da cena (troca de sala, `QueueFree`) enquanto está no feixe: não pode gerar erro nem deixar referência a um nó já liberado. (Coberto: `IsInstanceValid` antes de aplicar dano.)
- O jogador troca entre modo manual e automático da lanterna: o dano continua dependendo só da sobreposição com o feixe. (Coberto: o feixe é filho de `FlashlightAnchor`.)
- Um inimigo devolvido ao pool pode ficar um frame a mais na lista do feixe e receber `TakeDamage` com vida ≤ 0. Sem proteção, `Die()` rodaria de novo e, com `OnDied` já zerado, chamaria `QueueFree()` num inimigo que o pool ainda guarda. (Protegido pela trava `_isDead`; testado na `sala_ninho` em out/2026: massacre sem erros e inimigo reaproveitado do pool renasce vivo e vulnerável.)
- Fragmentos que nascem dentro do feixe: imunes por `SpawnInvulnerability`; continuam na lista do feixe (a detecção funciona durante a imunidade) e passam a tomar dano depois.
- Recurso de colisão compartilhado: o `CircleShape2D` da cena é o mesmo objeto para todas as instâncias. O tamanho por geração escala o **nó** `CollisionShape2D` (individual), nunca o raio do recurso.
- Troca de sala no mesmo frame de uma divisão: o `AddChild` adiado pode não executar e os fragmentos ficam como nós órfãos (monitor "Nós Órfãos"). Improvável; observar nos testes.
- Custo de instanciar: cada divisão cria nós novos (mais caro que reaproveitar do pool). Se matar muitos inimigos no mesmo instante causar engasgo, considerar um pool de fragmentos (não implementado). **Medido no fechamento dos spawners móveis (out/2026):** dividir dezenas no mesmo quadro causa um engasgo de um quadro (~27 ms no editor; 28 a 42 ms no descartável, ~0,15 a 0,3 ms por fragmento). Item 1.8 do backlog.
- Total de inimigos vivos: os fragmentos não contam no `MaxConcurrentEnemies`; o teto total dos spawners (`MaxTotalEnemies`) pausa o ninho, mas inimigos já vivos continuam se dividindo, então o total pode passar um pouco do teto durante massacres (medido: pico de 380 com teto 350).

## 6. Arquivos e cenas

- Existentes: `FractalEnemy.cs`, `fractal_enemy.tscn`, `LightDamageArea.cs`.
- Somente leitura: `Player.cs` (como a lanterna e o feixe são criados), `player.tscn` (o nó `FlashlightAnchor/LightDamageArea` com `CollisionPolygon2D`).
- Alterados pela fase 2: `NinhoSpawner.cs` e `NinhoSpawnerPooled.cs` (teto total `MaxTotalEnemies`; a `sala_ninho.tscn` usa a versão com pool).
- Usado: `FractalSwarm.cs` (registro de inimigos ativos; `FractalSwarm.Count` inclui fragmentos e exclui inimigos guardados no pool).
- Leitura externa: `FractalEnemy.IsDead` (público) é usado pelo `PlayerHurtbox` (spec `player-health-death.md`) para ignorar inimigos mortos ou guardados no pool.
- Novos a criar: nenhum previsto.

## 7. Passos no editor (feitos pelo dev)

- Colisão (definida na spec de movimento):
  - `FractalEnemy` (nó raiz): **Layer 2**, **Mask 1**. Inimigos não colidem entre si; colidem com paredes, pilares e jogador.
  - `Player` (nó raiz): **Layer 1**, **Mask 1 e 2** (é bloqueado pelos inimigos).
  - `FlashlightAnchor/LightDamageArea`: **Layer 1**, **Mask 1 e 2** (detecta os inimigos).
  - Se alguém mudar as camadas no Inspector, a máscara da `LightDamageArea` precisa continuar incluindo a camada do `FractalEnemy`, e o `Player` precisa continuar na camada 1 (o `TerminalModo` e a `PortaSaida` da `sala_entrada` detectam o jogador pela máscara 1).
- Motion Mode: o nó raiz `FractalEnemy` usa **Floating** (Inspector > CharacterBody2D > Motion Mode). É o modo para visão de cima e gravidade zero; não voltar para Grounded.
- Grupo: a cena usa o grupo `"Enimies"` (com erro de digitação) e o `NinhoSpawner.cs` adiciona `"Enemies"`. O dano por luz não depende de grupo (identifica o inimigo pelo tipo), então isso não afeta esta spec.

## 8. Pronto quando

### Fase 1

- [x] `dotnet build` sem erros
- [x] O inimigo aparece com a árvore fractal desenhada e com o ângulo dos ramos pulsando
- [ ] Sob o feixe a vida cai na taxa configurada; fora do feixe não muda (a morte sob o feixe foi confirmada; falta o passo 3 da seção 9: tirar o feixe no meio e confirmar que a vida não regenera)
- [x] Os atributos `[Export]` da seção 4 podem ser ajustados no Inspector sem mexer no código
- [x] Nenhum erro no painel Output/Debugger (Saída/Depurador) ao matar 10 inimigos seguidos e trocar de sala (troca testada pelo recarregamento da sala na morte do jogador, em out/2026; a transição entre salas diferentes ainda não existe)

### Fase 2

- [x] `dotnet build` sem erros
- [x] Ao morrer, a geração 0 e a geração 1 geram 2 fragmentos menores; a geração 2 não gera nada
- [x] Fragmentos nascidos dentro do feixe ficam imunes por `SpawnInvulnerability` antes de perder vida
- [x] Inimigo reaproveitado do pool volta como geração 0 (teste com `MaxConcurrentEnemies` = `PoolSize` = 3: renasce com tamanho normal, vida cheia, vulnerável e se divide de novo)
- [x] Os parâmetros de geração podem ser ajustados no Inspector
- [x] "Nós Órfãos" = 0 depois de matar a horda (testado em massacre, fechando o jogo no meio dele e recarregando a sala pela morte do jogador no meio de um massacre, com mortes seguidas). Refazer quando existir transição entre salas diferentes.
- [x] Com 200 no ninho e teto total de 350, o FPS não cai abaixo de 100 (V-Sync desligado) mesmo em massacre (pico de 380 inimigos ativos)

## 9. Como testar (roteiro manual no editor)

1. Abra `arena.tscn` (cena de protótipo, fora da demo; tem jogador, `CanvasModulate`, fundo e 2 inimigos) e rode a cena com F6.
2. Confirme que os inimigos aparecem desenhados e com os ramos pulsando.
3. Aponte a lanterna para um inimigo e confirme que ele morre depois de alguns segundos de luz contínua. Tire o feixe no meio do caminho e confirme que a vida não regenera.
4. Mude `DamagePerSecond` da `LightDamageArea` (em `player.tscn`) no Inspector e confirme que o tempo para matar muda.
5. Fase 2, na `arena.tscn`: mate um inimigo da geração 0 com a lanterna parada sobre ele e confirme 2 fragmentos menores, que ficam um instante sem perder vida; mate os dois e confirme mais 2 de cada; mate os 4 últimos e confirme que nada nasce.
6. Para ver a imunidade com clareza: `SpawnInvulnerability` = 2 no `fractal_enemy.tscn`, repita o passo 5 e volte para 0,3.
7. Para ver uma geração sem matar ninguém: numa instância da `arena.tscn`, mude `Generation` para 1 ou 2 (nunca na cena base). Volte para 0 depois.
8. Desempenho, na `sala_ninho` (V-Sync desligado só no teste): com 200 no ninho e `MaxTotalEnemies` 350, varra a horda com a lanterna e anote o pico de `Física 2D > Objetos Ativos`, o FPS e o Processo de Física. Confira "Nós Órfãos" = 0.

## 10. Decisões e notas

### Decididas

- O que pulsa: o **ângulo** dos ramos (como já está no código).
- Dano da luz: mantém o valor atual do código (35), ainda em teste.
- Flash de dano: não mexer agora.
- Divisão em fragmentos: implementada (fase 2). O próprio inimigo cria os fragmentos; `FragmentCount` como `[Export]`; ângulo aleatório a cada morte.
- Teto total de inimigos (variante **A1**): os spawners têm `[Export] MaxTotalEnemies` (350) e pausam o ninho enquanto `FractalSwarm.Count` (ninho + fragmentos) estiver no teto. `MaxConcurrentEnemies` (200 na `sala_ninho`) é game design (pressão do ninho); `MaxTotalEnemies` é orçamento técnico (física). Medido: pico de 380 em massacre, FPS sempre ≥ 100 com V-Sync desligado.
  - O teto não é rígido: inimigos já vivos continuam se dividindo. Variantes anotadas para o caso de os picos voltarem a pesar: **A2** (orçamento reservado: cada inimigo conta seus descendentes futuros, 4/2/1 por geração; garante o teto, mas o ninho enche bem menos) e **A3** (A1 + teto duro acima do qual o inimigo morre sem se dividir).
- Pulsação configurável: ângulo base, amplitude e frequência viraram `[Export]`, com a frequência em Hz (ciclos por segundo).
- Motion Mode Floating no inimigo (seção 11).
- Renderização em lote (R1–R3) adiada: o desenho não é o gargalo hoje (seção 11).
- Movimento e colisão da horda: separação (B) e descanso de inimigos travados (A) implementados em `docs/specs/fractal-enemy-movement.md`. Os inimigos agora ficam na camada 2 (máscara 1).
- Desde os spawners móveis (out/2026, `mobile-spawners.md`): o inimigo cumpre as interfaces `ILightDamageable` e `IContactHazard` (a lanterna e o traje o tratam pela interface) e tem o recolhimento por distância (`DespawnDistance` 3000 na cena, `DespawnDelay` 3 s): longe do jogador, volta ao estoque ou sai da árvore, sem se dividir nem soltar biomassa. A saída de cena (morte ou recolhimento) fica no `LeaveScene`.

### Em aberto

- Quando a transição entre salas diferentes existir: repetir os testes de "trocar de sala" da seção 8 (erros no Output e "Nós Órfãos" = 0, inclusive trocando de sala no meio de uma divisão). O recarregamento da mesma sala, pela morte do jogador, já passou nesses testes.

- Valores definitivos de `MaxConcurrentEnemies` e `MaxTotalEnemies`: na fase, definidos na `mobile-spawners.md` (no `EnemyPool`: `PoolSize` 60 e `MaxTotalEnemies` 350, medidos no pior caso; continuam pontos de partida). A `sala_ninho` continua com 200 e 350.
- O dano por tiros entra numa spec própria ou nesta?

### Observações do código (sem ação por enquanto)

- `TakeDamage` é chamado a cada frame sob o feixe e soma `_time += 0.2f` a cada chamada. Com isso a pulsação acelera muito enquanto o inimigo está na luz, e o flash branco fica ligado o tempo todo. Registrado para revisar junto com o flash.
- O `CollisionShape2D` do inimigo tem um círculo de raio 30 com o nó escalado para 0.2 (raio efetivo de ~6 px). A recomendação da Godot é ajustar o raio da forma em vez de escalar o nó de colisão.
- O `Player` também usa Motion Mode Grounded (padrão). Avaliar Floating na spec do jogador; afeta como ele desliza nas paredes (game feel).
- `sala_ninho.tscn`: a iluminação estava em teste pelo dev. **Resolvido (out/2026):** o rascunho de game design retirou o blecaute total (inimigos visíveis em silhueta, contraluz do sol). As cenas ainda estão com o `CanvasModulate` antigo até o dev ajustá-lo (backlog 1.6); não "corrigir" sem combinar.

## 11. Diagnóstico de desempenho (set/2026)

### Medições

Cena `sala_ninho.tscn` com 300 inimigos (`NinhoSpawnerPooled`), V-Sync desligado só durante o teste, lanterna com dano zerado para os inimigos não morrerem. Valores dos Monitores da Godot (pior caso do último segundo) e do Analisador Gráfico.

| Teste | Configuração do `FractalEnemy` | FPS | Processo de Física | Pares de Colisão | Renderização (CPU) |
|---|---|---|---|---|---|
| **E1** (linha de base) | Grounded, camada 1 / máscara 1 | 48 | 16,29 ms | 777 | 1,55 ms |
| **E3a** | sem colisão (camada 0 / máscara 0) | 431 | 6,00 ms | 3 | 1,14 ms |
| **E3b** | camada 2 / máscara 1 (colide com paredes, não entre si) | quedas de ~400 para 30–40 quando a horda encosta na parede | — | — | — |
| **E4** | **Floating**, camada 1 / máscara 1 | 197 | 11,63 ms | 816 | 1,54 ms |

### Conclusões

- O gargalo é a **colisão**, não o desenho. A renderização custa ~1,5 ms e as chamadas de desenho já são poucas (9–25), porque a Godot agrupa as linhas dos inimigos.
- Colidir com outros inimigos e colidir com paredes pesam os dois (E3b). O fator comum é cada inimigo empurrar contra um obstáculo a cada passo de física, mesmo sem conseguir avançar.
- O modo Grounded (de jogo de plataforma) quase dobrava o custo de cada contato, com a lógica de chão, snap e rampa. O Floating reduziu a física em ~29% com praticamente os mesmos contatos.
- A física roda em passos fixos de 1/60 s (16,67 ms). Quando um passo passa desse limite, o motor executa vários passos por frame e o FPS despenca de forma desproporcional. No E4 o passo ficou abaixo do limite, mas com pouca folga: há picos perto de 17 ms.
- Mesmo sem colisão (E3a), 300 inimigos custam ~6 ms por passo (`_PhysicsProcess` em C# + `MoveAndSlide`).

### Direções para a spec de movimento (não decididas)

| Direção | Ideia | Observação |
|---|---|---|
| **A** | Inimigo bloqueado para de empurrar | Ataca a causa comum (paredes e inimigos) |
| **B** | Separação suave em código em vez de colisão física entre inimigos | Mexe na colisão mútua ("nuvem"); decisão de design |
| **C** | Atualizar o movimento a cada 2 ou 3 passos, de forma escalonada | Movimento pode ficar menos suave |
| **D** | Limite de inimigos vivos por sala | Decisão de design; ligada à spec da `sala_ninho` |
| E5 | Max Slides de 4 para 2 (só Inspector) | Teste opcional, ainda não feito |

### Adiado: renderização em lote (R1–R3)

Proposta avaliada e adiada, porque o desenho não é o gargalo hoje. Retomar quando partículas, novos inimigos ou efeitos pesarem na renderização (medir antes com o Analisador Gráfico).

- **R1:** gerar uma vez, via `SubViewport`, texturas das variações do fractal (e quadros da pulsação do ângulo) como `ImageTexture` em memória.
- **R2:** um `FractalSwarmRenderer` com `MultiMeshInstance2D` desenha todos os inimigos a partir de um registro central de inimigos ativos.
- **R3:** remover `_Draw`/`DrawFractalBranch` do `FractalEnemy.cs`.

Pontos já levantados para quando for retomado:
- O registro precisa cobrir os três fluxos de criação: pool (`ActivateAt`), inimigos colocados à mão na cena (`arena.tscn`) e `NinhoSpawner.cs` sem pool. Registrar ao entrar na cena ou ativar; desregistrar ao desativar ou sair da cena.
- Pulsação do ângulo exige quadros pré-gerados (textura estática não pulsa).
- Uma única chamada de desenho exige atlas + shader (exceção à regra de shaders da etapa 1). Sem shader, um MultiMesh por textura pode *aumentar* as chamadas de desenho em relação às 9–25 atuais.
- Parâmetros visuais passariam do inimigo para o renderizador (aparência compartilhada).
- Os fragmentos (fase 2) têm profundidade menor e precisariam de texturas próprias.
