# Spec: Spawners móveis e objetivo da fase

- **Status:** aprovada (decisões S1 a S9 do dev, out/2026; seção 10). Plano de implementação aprovado pelo dev (seção 10); passos 1 e 2 feitos e testados pelo dev; próximo: passo 3 (luz e contato). Valores em teste.
- **Etapa:** 1 (mecânica). É a "fase C" da `hex-phase.md` (spawners na fase e simulação por proximidade).
- **Depende de:** `docs/specs/hex-phase.md` (a fase, o `HexPhaseLayout`), `docs/specs/start-chamber.md` (sinal `IncursionStarted`; nada nasce no nó inicial travado), `docs/specs/fractal-enemy.md` e `docs/specs/fractal-enemy-movement.md` (inimigo, horda, tetos e o problema conhecido de FPS), `docs/specs/biomass-collection.md` (recompensa), `docs/specs/player-health-death.md` (golpe de contato). Relaciona-se com a `docs/specs/dyson-wall.md` (a sucção afeta os spawners? Em aberto lá).

> **Valores em teste:** como nas demais specs da demo, os `[Export]` e os números desta spec são pontos de partida, não valores definitivos.

## 1. Objetivo

Dar à fase um objetivo: cerca de 10 spawners circulam pelas estruturas do hexágono gerando anticorpos, e o jogador precisa achá-los e destruí-los com a luz. Cada spawner morto deixa os restantes mais agressivos, até o último ficar frenético. Serve ao EXPLORAR (caçar pela fase com a navegação inercial), ao COMBATER (o alvo principal da incursão) e ao COLETAR (recompensa grande por spawner).

## 2. Comportamento esperado

### Início e fim da incursão

- Quando a `StartChamber` emite `IncursionStarted`, então o diretor dos spawners cria `SpawnerCount` spawners em rotas que **não tocam o nó de entrada**, em rotas diferentes enquanto houver, cada um num ponto sorteado entre 25% e 75% da rota. Antes disso não existe spawner nem inimigo na fase.
- Com o campo `StartChamber` do diretor vazio (fase aberta, como já vale para os portões), os spawners começam ao carregar a fase.
- Quando o último spawner morre, então o diretor emite `AllSpawnersDestroyed`, o HUD mostra "Fase limpa: volte à eclusa" e os inimigos que sobraram continuam. A extração pela eclusa (já existe) encerra a incursão, com 100% da biomassa depositada (S7).
- Morte, ejeção e extração recarregam a fase em modo câmara: os spawners renascem todos (a fase recomeça do zero, H5).

### Movimento pelas rotas (S3)

- As rotas são as arestas de um **grafo de pontos de passagem** tirado da geometria da fase:
  - pontos: o centro de cada nó (7) e o centro de cada painel (6);
  - rotas pelos eixos dos corredores: 12 conectores de nós (de centro a centro de nó, 6200 px) e 18 conectores de placa (do centro do painel ao centro do nó, ~3580 px, em linha reta pelo eixo da placa);
  - hexágono: 13 pontos e 30 rotas (o nó central tem 12 saídas; cada periférico, 5; cada painel, 3). Setor: 4 pontos e 6 rotas, todos com 3 saídas.
- O spawner anda em linha reta pela rota a `MoveSpeed`. Ao chegar a um ponto, sorteia uma das rotas que saem dali, menos a de onde veio (passeio aleatório, sem pathfinding). Nenhum ponto é beco sem saída (todos têm 3 ou mais rotas).
- O spawner é movido por código pelo eixo do corredor, sempre dentro do piso; não desvia de nada.
- Depois de liberar as saídas, o nó de entrada é um nó comum, e os spawners podem passar por ele.

### Vida e reação à luz (S1, S2)

- O feixe da lanterna fere o spawner como fere os inimigos (`DamagePerSecond` da `LightDamageArea`), mas com muito mais vida (`MaxHealth`). A vida não regenera.
- Quando é iluminado, o spawner **entra em pânico**: anda a `MoveSpeed × PanicSpeedMultiplier` e foge do jogador (vira na rota se o jogador estiver à frente; nos pontos, escolhe a rota cuja ponta fica mais longe do jogador), até `PanicDuration` depois de sair da luz. Com `PanicSpeedMultiplier` = 1, ele não foge.
- Quando a vida zera, o spawner some, solta `DeathBiomass` orbes de biomassa no lugar e avisa (`Destroyed`).

### Corpo e contato (S8)

- O spawner é **sólido e fere**: fica na camada de colisão 2, a mesma dos inimigos. Com isso:
  - o jogador (máscara 1 e 2) bate nele, e a lanterna (máscara 1 e 2) o enxerga, sem mudar o `player.tscn`;
  - os inimigos (máscara 1) o atravessam: nascem dentro dele sem enroscar;
  - o `PlayerHurtbox` (máscara 2) o percebe; encostar dá o **mesmo golpe do inimigo** (`SuitIntegrity`: `ContactDamage`, empurrão e imunidade; S8b).
- O spawner **empurra o jogador e segue a rota** (S8a): é um corpo movido por código (`AnimatableBody2D`), que não para por causa do jogador. O golpe de contato já joga o jogador para longe. Como o spawner anda no eixo do corredor, sobram ~120 px de cada lado nos conectores de nós e ~80 px nos de placa: o jogador passa ao lado dele e não é esmagado na parede.
- O jogador **não pega carona** no spawner: empurrado por ele (inclusive de baixo para cima) ou encostado por cima, fica para trás quando o spawner vira. Exige o **Floor Layers** do `Player` sem a camada 2 (seção 5).

### Geração de inimigos

- Os inimigos nascem em volta do spawner (até `SpawnScatter` do centro), da geração 0, tirados de um **estoque (pool) compartilhado** por todos os spawners e guardado num nó **parado** (`EnemyPool`).
  - **Por que o estoque fica fora do spawner (achado no código):** hoje os inimigos do pool são filhos do `NinhoSpawnerPooled`, e os fragmentos nascem no mesmo pai, em coordenadas locais (`Position + offset`). Na Godot, o filho herda a transformação do pai: se o pai andasse, a horda inteira andaria junto com ele.
- Um spawner só gera inimigos quando o jogador está a menos de `ActivationRadius` (S5). Longe do jogador, ele só anda.
- O spawner não gera quando `FractalSwarm.Count` ≥ `MaxTotalEnemies` (orçamento de física, como no ninho) nem quando o estoque está vazio (`PoolSize`: inimigos da geração 0 vivos na fase inteira). Estoque vazio não é erro: o spawner espera, sem mensagem no Output.
- Inimigo longe do jogador (mais que `DespawnDistance` por `DespawnDelay`) é **recolhido em silêncio** (S5): volta ao estoque (ou é liberado, se for fragmento), sem se dividir e sem soltar biomassa. Acontece fora da tela.
  - **Por que (achado no código):** os inimigos andam a 10 px/s (`Speed` na `fractal_enemy.tscn`) e o jogador voa a 600. Sem recolher, os inimigos ficariam espalhados pelas rotas, longe do jogador, empurrando paredes na direção dele (o cenário do problema conhecido de FPS) e ocupando o teto: o spawner perto do jogador deixaria de gerar.

### Curva de dificuldade (S4)

- **Orçamento global:** somando todos os spawners, a fase gera `BaseSpawnRate` inimigos por segundo, mais `SpawnRateGrowthPerKill` (fração) por spawner morto. Cada spawner vivo recebe a sua parte:
  - intervalo de cada spawner = vivos ÷ (`BaseSpawnRate` × (1 + `SpawnRateGrowthPerKill` × mortos))
- Com 10 spawners, 1,5/s e +10% por morte:

| Vivos | Mortos | Taxa total | Intervalo de cada spawner |
|---|---|---|---|
| 10 | 0 | 1,5/s | 6,7 s |
| 5 | 5 | 2,25/s | 2,2 s |
| 2 | 8 | 2,7/s | 0,74 s |
| 1 | 9 | 2,85/s | 0,35 s |

- Com a geração por proximidade, o jogador quase sempre enfrenta um spawner por vez. O que ele sente é o intervalo de cada spawner, que cai de 6,7 s para 0,35 s ao longo da incursão (19 vezes mais rápido): o último fica frenético.
- A velocidade dos spawners também sobe: + `SpeedGrowthPerKill` (fração) por spawner morto.
- O teto de física (`MaxTotalEnemies`) continua valendo: a dificuldade sobe sem estourar o orçamento medido.

### Rastreabilidade (S6)

- HUD de teste: "Spawners: 7/10" (sinal `SpawnersChanged`), que aparece ao começar a incursão, e "Fase limpa: volte à eclusa" no fim.
- **Seta na borda da tela** apontando para o spawner vivo mais próximo (em linha reta), escondida quando ele está na tela.
- **Silhueta provisória** desenhada por código, diferente da do inimigo (círculo grande com um anel pulsando, cor própria). Arte na etapa 2.
- O rastro de inimigos lentos que o spawner deixa pela rota também ajuda a segui-lo.

## 3. Fora do escopo

- Sucção da parede de fronteira nos spawners (backlog 1.5; `dyson-wall.md`, seção 10).
- Próxima fase, hexágonos vizinhos e tela de fim da demo.
- Mapa e minimapa.
- Pathfinding; escolta que segue o spawner (os inimigos continuam perseguindo o jogador, como hoje).
- Tipos diferentes de spawner, ataque à distância, dano de contato próprio por tipo (S8b).
- Arte, áudio, partículas e animação (etapa 2).
- Mudar o `NinhoSpawnerPooled`, o `NinhoSpawner` e a `sala_ninho` (ficam como bancada).
- Recheio das estruturas (pilares, gargalos).

## 4. Parâmetros ajustáveis (valores em teste, aceitos pelo dev como pontos de partida; S9)

### `SpawnerDirector` (diretor dos spawners, nó na fase)

| Parâmetro | Valor inicial | Observação |
|---|---|---|
| `SpawnerCount` | 10 | No setor de teste há 3 rotas elegíveis: os spawners dividem a mesma rota |
| `BaseSpawnRate` | 1,5 inimigos/s | Soma de todos os spawners (orçamento global) |
| `SpawnRateGrowthPerKill` | 0,1 | +10% na taxa total por spawner morto |
| `SpeedGrowthPerKill` | 0,05 | +5% na velocidade dos spawners por spawner morto |
| `MaxTotalEnemies` | 350 | O mesmo orçamento de física medido no ninho (pico de 380 em massacre, FPS ≥ 100) |
| `SpawnerScene` | `mobile_spawner.tscn` | Ligado no Inspector |
| `Phase`, `StartChamber`, `Pool` | — | Ligados no Inspector. `StartChamber` vazio = começa ao carregar |

### `EnemyPool` (estoque compartilhado, nó parado na fase)

| Parâmetro | Valor inicial | Observação |
|---|---|---|
| `EnemyScene` | `fractal_enemy.tscn` | Ligado no Inspector |
| `PoolSize` | 60 | Teto de inimigos da geração 0 vivos na fase toda (faz na fase o papel do `MaxConcurrentEnemies` do ninho). Com os fragmentos, até 420 em teoria; o `MaxTotalEnemies` corta antes |

### `MobileSpawner` (cena `mobile_spawner.tscn`)

| Parâmetro | Valor inicial | Observação |
|---|---|---|
| `MaxHealth` | 300 | ~8,6 s de luz contínua (35/s) |
| `MoveSpeed` | 150 px/s | ~41 s por conector de nós, ~24 s por rota de placa. O jogador voa a 600 |
| `PanicSpeedMultiplier` | 2 | 1 = não foge. O último em pânico: ~435 px/s, ainda abaixo do jogador |
| `PanicDuration` | 2 s | Depois de sair da luz |
| `ActivationRadius` | 2000 px | No zoom das arenas (0,7) a tela mostra ~2740 × 1540 px: começa a gerar um pouco antes de aparecer |
| `SpawnScatter` | 40 px | Raio em que os inimigos nascem |
| `DeathBiomass` | 20 orbes de 1 | Um ancestral rende 4: o spawner vale 5 ancestrais |
| Visual provisório | raio 40, cor própria | Grupo "Visual provisório" |

O dano do contato é o do `SuitIntegrity` (`ContactDamage` 10, `KnockbackImpulse` 200, `HitInvulnerability` 0,8 s), igual ao do inimigo (S8b).

### `FractalEnemy`

| Parâmetro | Valor inicial | Observação |
|---|---|---|
| `DespawnDistance` | 0 no código (desligado); 3000 px na `fractal_enemy.tscn` | Longe da tela em qualquer zoom (meia diagonal a 0,7: ~1570 px). Na `sala_ninho` nunca acontece |
| `DespawnDelay` | 3 s | Evita recolher quem só passou um instante longe |

## 5. Casos de borda

- **Spawner morto com inimigos dele vivos:** continuam. Ao morrer, voltam ao estoque compartilhado, que não depende do spawner (não há referência a um spawner já liberado).
- **Luz através da parede:** a área da luz não é bloqueada por corpos, mas os vãos entre estruturas têm ≥ 1000 px e o feixe ~220 px: na prática, só se ilumina um spawner na mesma estrutura ou na emenda entre duas.
- **Caçar no corredor:** o feixe que persegue o spawner pode irritar as paredes do corredor (~2 s; placas ~1,5 s). Risco × recompensa intencional.
- **Modo automático da lanterna:** o feixe segue a velocidade; voando atrás do spawner, ele fica iluminado. Combina com a fuga.
- **Encostar no spawner:** mesmo golpe do inimigo; o empurrão afasta o jogador, e a imunidade evita golpes seguidos. Se o jogador continuar no caminho, o spawner o empurra pela rota.
- **"Carona" no spawner (verificado no motor, passo 2):** o `Player` usa Motion Mode Grounded, que trata como "chão" a superfície com a normal para cima e acompanha um chão que se move. No descartável, o jogador encostado por cima (ou empurrado pelo spawner de baixo para cima) foi carregado e continuou em cima dele depois da curva (terminou a 3 px do centro do spawner, 495 px longe de onde estava). **Correção, só no Inspector:** `player.tscn` > `Player` > Moving Platform > **Floor Layers** sem a camada 2. Com ela, o jogador é empurrado e fica para trás na curva, como no modo Floating. Vale também para os inimigos, que estão na mesma camada.
- **Empurrão de frente:** o jogador chega a entrar até ~3 px no spawner antes de ser empurrado (a posição do corpo movido por código vale no passo de física seguinte). Imperceptível.
- **Inimigos nascendo dentro do spawner:** atravessam o corpo dele (máscara 1) e se espalham pela separação da horda.
- **Dois spawners na mesma rota ou no mesmo ponto:** sobrepõem-se (corpos movidos por código não se empurram). Aceito.
- **Fuga com o jogador à frente:** vira na rota; com o jogador parado no ponto de destino, o spawner volta pela rota e escolhe outra no ponto de origem.
- **Último spawner frenético e massacre:** o teto de física corta a geração; os vivos ainda se dividem, então o total pode passar um pouco do teto (como no ninho: pico de 380 com 350). Medir.
- **Liberar as saídas:** criar 10 spawners é barato. O estoque é criado ao carregar a fase (escondido pelo escurecer), não ao liberar (o engasgo apareceria na tela).
- **Inimigo recolhido dentro do feixe ou encostado no jogador:** as guardas existentes cobrem (`_isDead` no `TakeDamage`, `IsDead` no `PlayerHurtbox`). O spawner morto também responde `IsDead`.
- **Recargas seguidas:** registros estáticos limpos no `_ExitTree`; conferir "Nós Órfãos" = 0.
- **Setor de teste:** 3 rotas elegíveis (o nó 2 de entrada toca as outras 3); com 10 spawners, vários dividem a mesma rota.
- **Seta:** o mais próximo em linha reta pode estar do outro lado de um vão (o caminho é mais longo). Aceito na etapa 1.

## 6. Arquivos e cenas

- **Existentes, lidos:** `NinhoSpawnerPooled.cs` (modelo do estoque), `NinhoSpawner.cs`, `FractalSwarm.cs` (`Count`), `FractalEnemy.cs`, `LightDamageArea.cs`, `PlayerHurtbox.cs`, `SuitIntegrity.cs`, `HexPhaseLayout.cs`, `HexPhaseBuilder.cs`, `StartChamber.cs`, `BiomassField.cs`.
- **Novos (criar só com autorização):**
  - `MobileSpawner.cs` (`AnimatableBody2D`, o nó da Godot para corpos movidos por código) e a cena `mobile_spawner.tscn` (montada pelo dev): anda pelas rotas, gera inimigos, toma dano da luz, foge, morre e avisa. **Criado no passo 2** (andar pelas rotas e silhueta provisória).
  - `SpawnerDirector.cs` (`Node2D`, filho da raiz da fase): começa no `IncursionStarted`, cria e posiciona os spawners, calcula a curva de dificuldade e emite `SpawnersChanged` e `AllSpawnersDestroyed`. **Criado no passo 2** (começar e posicionar).
  - `EnemyPool.cs` (`Node2D` parado, filho da raiz da fase): o estoque compartilhado.
  - `ILightDamageable.cs` (interface C#): "o que a luz fere".
  - `IContactHazard.cs` (interface C#): "o que fere o traje ao encostar".
  - `SpawnerHud.cs` (`Label`) e `SpawnerIndicator.cs` (`Control`, a seta).
- **Alterados (proposto):**
  - `HexPhaseLayout.cs`: o grafo de rotas (pontos e rotas), montado junto com as estruturas (passo 1); `HexRoute.PointAt` (passo 2).
  - `LightDamageArea.cs`: fere qualquer `ILightDamageable` (inimigo e spawner), em vez de só `FractalEnemy`.
  - `PlayerHurtbox.cs`: percebe qualquer `IContactHazard` (inimigo e spawner), em vez de só `FractalEnemy`.
  - `FractalEnemy.cs`: implementa as duas interfaces (já tem `TakeDamage` e `IsDead`); recolhimento por distância (`DespawnDistance`, `DespawnDelay`).
- **Cenas (no editor):** `mobile_spawner.tscn` (nova), `hex_phase.tscn` (nós novos), `fractal_enemy.tscn` (`DespawnDistance`), `player.tscn` (Floor Layers sem a camada 2, contra a "carona"; as máscaras já incluem a camada 2).
- **Sem mudança:** `SuitIntegrity.cs` (o golpe é o mesmo), `FractalSwarm.cs`, `NinhoSpawnerPooled.cs`, `NinhoSpawner.cs`, `StartChamber.cs` (só emite o sinal), `HexPhaseBuilder.cs`.

## 7. Passos no editor (resumo; o roteiro numerado completo vem com cada passo do plano)

1. **Build** depois de cada script com `[Export]` ou `[Signal]` novo.
2. `mobile_spawner.tscn`: raiz **`MobileSpawner`** (`AnimatableBody2D`, `MobileSpawner.cs`) com **`CollisionShape2D`** (círculo de raio 40); Collision **Layer** só 2, **Mask** vazia.
3. `hex_phase.tscn`: **`EnemyPool`** (`Node2D`, `EnemyPool.cs`, campo **Enemy Scene**) e **`SpawnerDirector`** (`Node2D`, `SpawnerDirector.cs`) como filhos da raiz `HexPhase`, em (0, 0); campos do diretor a ligar: **Phase**, **Start Chamber**, **Pool** e **Spawner Scene**.
4. `PhaseHud` > **`SpawnerLabel`** (`Label`, `SpawnerHud.cs`) e **`SpawnerIndicator`** (`Control`, `SpawnerIndicator.cs`, campo **Director**); sinais `SpawnersChanged` e `AllSpawnersDestroyed` → `SpawnerLabel`.
5. `fractal_enemy.tscn`: **Despawn Distance** = 3000.
6. `player.tscn` > raiz **`Player`** > CharacterBody2D > Moving Platform > **Floor Layers**: sem a camada 2 (passo 2).

## 8. Pronto quando

- [ ] `dotnet build` sem erros a cada passo
- [ ] Na câmara travada não há spawner nem inimigo; ao liberar as saídas aparecem `SpawnerCount` spawners, longe do nó de entrada, e o HUD mostra "Spawners: 10/10"
- [ ] Os spawners andam só pelos eixos dos corredores, trocando de rota nos nós e painéis, sem atravessar paredes nem sair da fase
- [ ] Um spawner só gera inimigos com o jogador perto
- [ ] O spawner é sólido para o jogador: encostar dá o golpe do inimigo; no caminho dele, o jogador é empurrado, sem "carona" e sem ficar preso na parede; os inimigos o atravessam
- [ ] A luz fere o spawner; iluminado, ele foge mais rápido; com a vida zerada, some, solta biomassa e o HUD desconta
- [ ] A cada spawner morto, os restantes geram mais rápido (intervalos da seção 2)
- [ ] A seta aponta para o spawner vivo mais próximo e some quando ele está na tela
- [ ] Inimigos longe do jogador somem sem soltar biomassa e voltam ao estoque
- [ ] Com todos mortos, o HUD mostra "Fase limpa: volte à eclusa" e a eclusa extrai como antes
- [ ] A `sala_ninho` (bancada) continua funcionando: a luz mata os inimigos e o contato fere
- [ ] Pior caso (último spawner frenético, massacre): Processo de Física abaixo de 16,67 ms (V-Sync desligado só no teste); "Nós Órfãos" = 0 depois de recargas seguidas

## 9. Como testar (roteiro manual no editor)

1. F5: na câmara não há spawner nem inimigo. Libere as saídas: o HUD mostra 10/10 e a seta aparece.
2. Siga a seta até um spawner; confira que ele anda pelo eixo do corredor e troca de rota nos nós e painéis.
3. Aproxime-se e afaste-se: ele só gera inimigos com você perto. Encoste nele (golpe e empurrão), fique parado no caminho dele (empurrado pela rota) e encoste por cima dele (sem ser arrastado).
4. Ilumine-o: ele foge mais rápido; persiga até matar. Confira a biomassa e o HUD.
5. Mate vários e repare o intervalo de geração encurtando (para testar rápido: `MaxHealth` baixo e `SpawnerCount` 3 no Inspector).
6. Afaste-se de uma nuvem de inimigos e volte: ela sumiu.
7. Mate todos: "Fase limpa"; extraia pela eclusa e confira o banco.
8. Desempenho: V-Sync desligado só no teste; com o último spawner frenético, anote FPS, Processo de Física e Objetos Ativos. Morra e recarregue 5 vezes olhando o Output e "Nós Órfãos".
9. `sala_ninho` (F6): a lanterna mata os inimigos e o contato fere, como antes.

## 10. Decisões e notas

### Decididas pelo dev (out/2026)

Todas seguiram a recomendação da análise, menos a S8 (o dev escolheu o spawner sólido e que fere).

1. **S1, vida e como matar:** luz, com vida alta (300, ~8,6 s) e sem regenerar. Descartadas: escudo enquanto a escolta for grande; vida que regenera fora do feixe.
2. **S2, reação à luz:** pânico: acelera (×2) e foge do jogador enquanto iluminado e por 2 s depois. Descartadas: não reagir; pânico com rajada de inimigos.
3. **S3, movimento:** passeio aleatório pelas rotas, sem voltar pela mesma. Descartadas: circuito fixo; busca no grafo (BFS).
4. **S4, curva de dificuldade:** orçamento global repartido entre os vivos + crescimento por morte. Descartadas: só o orçamento repartido; taxa fixa por spawner + crescimento.
5. **S5, simulação por proximidade:** gera só perto do jogador e recolhe os inimigos longe dele. Descartadas: gerar em todo lugar com os inimigos longe "dormindo"; sem regra (medir antes).
6. **S6, rastreabilidade:** contador no HUD + seta na borda da tela para o mais próximo. Descartados: texto com a estrutura de cada spawner; sonar periódico.
7. **S7, quando todos morrem:** "Fase limpa: volte à eclusa"; a extração encerra. Descartadas: extração automática; tela de fim da demo.
8. **S8, corpo:** sólido e fere (camada 2, a mesma dos inimigos). Descartadas: atravessável sem dano (a recomendação, numa camada 3 só da luz); sólido sem dano.
   - **S8a:** empurra o jogador e segue a rota (`AnimatableBody2D`). Descartada: parar quando bloqueado pelo jogador (`CharacterBody2D`).
   - **S8b:** o dano do contato é o mesmo golpe do inimigo. Descartado: dano próprio, maior (mudaria o `SuitIntegrity` e o `PlayerHurtbox`).
9. **S9, valores iniciais:** os da seção 4, como pontos de partida.

### Arquitetura (proposta, com o plano)

- **Diretor (`SpawnerDirector`):** decide a dificuldade; os spawners só executam. É a ideia do "AI Director" do *Left 4 Dead*: um ponto central regula o ritmo, e os atores não conhecem a curva. Trocar a curva é mexer num lugar só.
- **Observer:** o spawner avisa `Destroyed`; o diretor avisa `SpawnersChanged` e `AllSpawnersDestroyed`; HUD e seta só escutam. A `StartChamber` não conhece os spawners (só emite `IncursionStarted`, como já faz).
- **Interfaces pequenas (`ILightDamageable`, `IContactHazard`):** a lanterna fere "qualquer coisa que a luz fere", e o traje sente "qualquer coisa que fere ao encostar", sem conhecer cada tipo. Um alvo novo não muda a lanterna nem o traje (princípio aberto/fechado). São duas e não uma porque são perguntas diferentes (princípio da segregação de interfaces): no futuro pode haver algo que a luz fere e que não fere ao encostar, ou o contrário.
- **Object Pool compartilhado:** o mesmo padrão do `NinhoSpawnerPooled`, mas num nó parado e com um único estoque para a fase; o tamanho do estoque é o teto de ancestrais.
- **Grafo de pontos de passagem (waypoint graph):** a navegação dos spawners é um grafo pequeno tirado da geometria que já existe. Dispensa pathfinding porque todo corredor é reto e cada rota é o eixo dele.
- **Processar só o que importa** (lição do `CLAUDE.md`): o spawner longe do jogador só anda (um `_PhysicsProcess` barato por spawner); o inimigo longe é recolhido. É a mesma ideia da parede que dorme.
- **Conexão com o `IncursionStarted` por código,** pelo campo `StartChamber` do diretor: com o campo vazio, a fase começa aberta e o diretor começa ao carregar (a fase nunca fica sem spawners por falta de ligação).

### Problema conhecido de FPS (`fractal-enemy-movement.md`, seção 11)

- O cenário era 400 inimigos aglomerados contra uma parede, com o jogador do outro lado. Com a geração por proximidade, o recolhimento por distância e os tetos (`PoolSize`, `MaxTotalEnemies`), a horda fica perto do jogador e limitada; o cenário fica improvável, mas o pior caso (último spawner frenético) precisa ser medido (seção 8).
- Se ainda pesar: os caminhos da seção 11 daquela spec (instrumentação primeiro) e, aqui, gerar só com o jogador à vista (um raio do spawner ao jogador antes de gerar).

### A verificar no descartável, antes de cada passo

- Verificado no passo 2: uma `Area2D` com máscara 2 percebe o `AnimatableBody2D` movido por código na camada 2; o jogador bate nele e é empurrado; a "carona" do modo Grounded acontece e é corrigida pelo Floor Layers (seção 5).
- O custo de criar o estoque (60 inimigos) no quadro de montagem da fase.
- O recolhimento devolve ao estoque sem fragmentos nem orbes, e o inimigo reaproveitado renasce normal.

### Plano de implementação (aprovado pelo dev, out/2026)

Cada passo: verificação antes no descartável (Godot headless), apresentação do código atual, da mudança, do porquê e das consequências, autorização, `dotnet build` e teste do dev no editor.

0. **Commit da câmara de partida** (backlog 1.7), pelo dev, antes de começar.
1. **Grafo de rotas** (`HexPhaseLayout.cs`): pontos de passagem e rotas, montados junto com as estruturas. Sem efeito visível. **Feito e testado pelo dev** (out/2026: tudo igual a antes, sem erros no Output):
   - tipos novos `HexWaypoint` (centro de um nó ou painel, com as rotas que saem dele) e `HexRoute` (eixo de um corredor, com `Other` e `Touches`), no mesmo arquivo; listas `Waypoints` e `Routes`; `AddNodeConnector`, `AddPlateConnector` e `AddCorridor` devolvem a estrutura criada;
   - verificado no descartável (Godot 4.7.2 headless, receita 2×): setor com 4 pontos e 6 rotas, hexágono com 13 e 30; saídas por ponto 12 (central), 5 (periféricos) e 3 (painéis); rotas de 6200 px (conectores de nós) e 3580 px (placas); com a entrada no nó 2, 5 rotas tocam o nó (25 elegíveis; no setor, 3); em 13.892 amostras (a cada 10 px e a ±50 px do eixo), nenhuma fora do piso, e toda rota passa por "ponta A → corredor → ponta B"; um círculo de raio 40 (e até 110) ao longo das rotas não toca parede; paredes inalteradas (364 e 1268); geometria e grafo do hexágono em 3,4 ms.
2. **Spawner que anda** (novos `MobileSpawner.cs` e `SpawnerDirector.cs`; cena `mobile_spawner.tscn`): o diretor cria os spawners no `IncursionStarted`, longe do nó de entrada; passeio aleatório; silhueta provisória; corpo sólido na camada 2. Teste: os spawners circulam pela fase; o jogador bate neles e é empurrado. **Feito e testado pelo dev** (out/2026: spawners andando pelas rotas, empurrando sem "carona", sem erros no Output; montagem conferida no disco):
   - `HexRoute.PointAt` no layout; `MobileSpawner` anda `MoveSpeed` px por passo e, ao passar da ponta, sorteia outra rota (menos a de onde veio), sem perder o que sobrou do passo; desenha o corpo e o anel com o raio da `CollisionShape2D`. `SpawnerDirector` se inscreve no `IncursionStarted` (ou começa adiado, sem câmara), embaralha as rotas que não tocam o nó de entrada e põe cada spawner no ponto sorteado **antes** de ele entrar na árvore (com o Sync to Physics, a posição vale no passo seguinte; um "pulo" da origem viraria velocidade na física);
   - verificado no descartável (0 falhas): nada antes de liberar; ao liberar, 10 spawners em 10 rotas distintas, nenhuma tocando o nó 2, entre 25% e 74% da rota, já no ponto certo; 60 s a 3000 px/s: 36.110 amostras sem sair do piso, sem salto, 398 trocas de rota sem voltar pela mesma, sem tocar parede, 30 de 30 rotas visitadas; setor sem câmara começa sozinho (10 spawners nas 3 rotas elegíveis); `Area2D` com máscara 2 percebe o spawner (com máscara 1, não); inimigo (máscara 1) atravessado; "carona" confirmada e corrigida pelo Floor Layers (seção 5).
3. **Luz e contato** (novos `ILightDamageable.cs` e `IContactHazard.cs`; alterados `LightDamageArea.cs`, `PlayerHurtbox.cs` e `FractalEnemy.cs`; mais vida, pânico e morte no `MobileSpawner`): a luz fere e mata o spawner, que foge e solta biomassa; encostar fere. Teste: matar spawners; `sala_ninho` igual a antes.
4. **Geração e curva** (novo `EnemyPool.cs`; geração no `MobileSpawner`; curva e contagem no `SpawnerDirector`): inimigos nascem perto do jogador; o intervalo encurta a cada spawner morto; tetos de estoque e de física.
5. **Recolhimento por distância** (`FractalEnemy.cs`; `DespawnDistance` na `fractal_enemy.tscn`).
6. **HUD, seta e fase limpa** (novos `SpawnerHud.cs` e `SpawnerIndicator.cs`; sinais `SpawnersChanged` e `AllSpawnersDestroyed`).
7. **Desempenho e ciclo** (medição do dev, seção 9, itens 8 e 9) e **fechamento**: esta spec, `BACKLOG.md`, `hex-phase.md` (fase C), `CLAUDE.md`, `HANDOFF.md` e a mensagem de commit.

### Relação com outras decisões

- Backlog, seção 2: "Valores definitivos de `MaxConcurrentEnemies` e `MaxTotalEnemies`": na fase, o `PoolSize` do `EnemyPool` (60) faz o papel do `MaxConcurrentEnemies`, e o `MaxTotalEnemies` do diretor continua 350. Definitivos depois da medição (passo 7).
- `dyson-wall.md`, seção 10: "os spawners são afetados pela sucção?" continua lá (1.5).
- `hex-phase.md`: esta spec é a fase C; o recheio das estruturas pode vir depois.
