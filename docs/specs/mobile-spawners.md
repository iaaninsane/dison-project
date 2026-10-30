# Spec: Spawners móveis e objetivo da fase

- **Status:** **validada** (out/2026). Decisões S1 a S9 do dev (seção 10; a S6 foi revista depois do teste do passo 6). Plano de implementação (passos 1 a 7) feito e testado pelo dev. Um item do "Pronto quando" ficou parcial: o engasgo de um quadro na divisão em massa, que virou o item 1.8 do backlog e foi resolvido em out/2026 (estoque de fragmentos no `EnemyPool`; seção 10, "Desempenho"). Valores em teste. **Revista em out/2026** pela parede de fronteira (`dyson-wall.md`, fase B): decisões S10 (trajeto perto das paredes) e S11 (sucção e volta à rota), em implementação.
- **Relação com o level up (out/2026, `level-up.md` e `force-field.md`; decidido, ainda não implementado):**
  - Os 20 orbes do spawner valem 20 de experiência.
  - A potência da lanterna encurta o tempo para matá-lo: no nível 5, de ~8,6 s para ~4,3 s.
  - O campo de força absorve o golpe do spawner, sem empurrá-lo.
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
- O spawner percorre a rota a `MoveSpeed`, pelo trajeto escolhido (S10, abaixo). Ao chegar a um ponto, sorteia uma das rotas que saem dali, menos a de onde veio (passeio aleatório, sem pathfinding). Nenhum ponto é beco sem saída (todos têm 3 ou mais rotas).
- **Trajeto (S10, out/2026; provisório):** em vez da linha reta pelo eixo, o spawner se afasta do eixo para os lados, perto das paredes, para passar pelas fendas da parede de fronteira (`dyson-wall.md`). A forma é escolhida no Inspector (`PathShape`), para comparar:
  - **senoide variada:** comprimento de onda e fase sorteados por spawner;
  - **rente à parede, trocando de lado:** curvas em S em distâncias sorteadas;
  - **ruído orgânico:** deriva lateral suave (`FastNoiseLite`).

  Em qualquer forma, o afastamento:
  - nunca passa de meia largura do corredor − raio − folga (110 px nos conectores de nós, 70 px nos de placa);
  - vai a zero nos pontos de passagem, para a troca de rota não saltar;
  - é contado a partir da ponta A da rota, para a meia-volta do pânico não espelhar a forma.

  O dev decide depois se ficam as três ou uma.
- O spawner é movido por código, sempre dentro do piso; não desvia de nada, a não ser pela sucção das fendas (S11).
- Depois de liberar as saídas, o nó de entrada é um nó comum, e os spawners podem passar por ele.

### Vida e reação à luz (S1, S2)

- O feixe da lanterna fere o spawner como fere os inimigos (`DamagePerSecond` da `LightDamageArea`), mas com muito mais vida (`MaxHealth`). A vida não regenera.
- Quando é iluminado, o spawner **entra em pânico**: anda a `MoveSpeed × PanicSpeedMultiplier` e foge do jogador (vira na rota se o jogador estiver à frente; nos pontos, escolhe a rota cuja ponta fica mais longe do jogador), até `PanicDuration` depois de sair da luz. Com `PanicDuration` = 0, ele não entra em pânico. Visual provisório: em pânico, o corpo muda de cor (`PanicColor`).
- Quando a vida zera, o spawner some, solta `DeathBiomass` orbes de biomassa no lugar e avisa (`Destroyed`).

### Corpo e contato (S8)

- O spawner é **sólido e fere**: fica na camada de colisão 2, a mesma dos inimigos. Com isso:
  - o jogador (máscara 1 e 2) bate nele, e a lanterna (máscara 1 e 2) o enxerga, sem mudar o `player.tscn`;
  - os inimigos (máscara 1) o atravessam: nascem dentro dele sem enroscar;
  - o `PlayerHurtbox` (máscara 2) o percebe; encostar dá o **mesmo golpe do inimigo** (`SuitIntegrity`: `ContactDamage`, empurrão e imunidade; S8b).
- O spawner **empurra o jogador e segue a rota** (S8a): é um corpo movido por código (`AnimatableBody2D`, camada 2, **máscara 1**). Antes de andar, testa o movimento (`TestMove`); se o jogador está no caminho, move o jogador (`MoveAndCollide`) pela parte do movimento que vai contra ele e só então anda. Assim nunca entra no corpo do jogador (senão o jogador "cola", seção 5). Se o jogador não puder sair (encurralado contra uma parede), o spawner espera. O golpe de contato já joga o jogador para longe. No eixo do corredor, sobram ~120 px de cada lado nos conectores de nós e ~80 px nos de placa. Com o trajeto S10, o lado da parede fica mais estreito e o outro mais largo: o jogador passa pelo lado aberto.
- O jogador **não pega carona** no spawner: empurrado por ele (inclusive de baixo para cima) ou encostado por cima, fica para trás quando o spawner vira. Exige o **Floor Layers** do `Player` sem a camada 2 (seção 5).

### Geração de inimigos

- Os inimigos nascem em volta do spawner (até `SpawnScatter` do centro), da geração 0, tirados de um **estoque (pool) compartilhado** por todos os spawners e guardado num nó **parado** (`EnemyPool`).
  - **Por que o estoque fica fora do spawner (achado no código):** hoje os inimigos do pool são filhos do `NinhoSpawnerPooled`, e os fragmentos nascem no mesmo pai, em coordenadas locais (`Position + offset`). Na Godot, o filho herda a transformação do pai: se o pai andasse, a horda inteira andaria junto com ele.
- Um spawner só gera inimigos quando o jogador está a menos de `ActivationRadius` (S5). Longe do jogador, ele só anda.
- O relógio de geração só corre com o jogador perto e começa em zero: o primeiro inimigo nasce assim que o jogador chega. Depois, um a cada intervalo dado pelo diretor (o relógio soma o intervalo, para manter o ritmo exato).
- O estoque (`EnemyPool`) não solta inimigo quando `FractalSwarm.Count` ≥ `MaxTotalEnemies` (orçamento de física, como no ninho) nem quando está vazio (`PoolSize`: inimigos da geração 0 vivos na fase inteira). Não é erro: o spawner tenta de novo no próximo intervalo, sem mensagem no Output.
- Inimigos deixados para trás são recolhidos ao estoque pelo recolhimento por distância (abaixo): ~`DespawnDelay` depois de o jogador se afastar, o estoque volta a ter de onde tirar.
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
- **Setas na borda da tela**, uma para cada spawner vivo fora da tela, na direção dele (S6 revista pelo dev, out/2026). A do mais próximo do jogador (em linha reta) fica em destaque, maior e opaca; as outras ficam menores e meio transparentes. Spawner na tela não tem seta.
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
| `SpawnerScene` | `mobile_spawner.tscn` | Ligado no Inspector |
| `Phase`, `StartChamber`, `Pool` | — | Ligados no Inspector. `StartChamber` vazio = começa ao carregar; `Pool` vazio = ninguém gera (aviso no Output) |

### `EnemyPool` (estoque compartilhado, nó parado na fase)

| Parâmetro | Valor inicial | Observação |
|---|---|---|
| `EnemyScene` | `fractal_enemy.tscn` | Ligado no Inspector |
| `PoolSize` | 60 | Teto de inimigos da geração 0 vivos na fase toda (faz na fase o papel do `MaxConcurrentEnemies` do ninho). Com os fragmentos, até 420 em teoria; o `MaxTotalEnemies` corta antes. Criar os 60 leva ~15 ms (descartável), ao carregar a fase |
| `FragmentPoolSize` | 360 | Fragmentos guardados de antemão (backlog 1.8). Só desempenho, não é teto: se faltar, cria mais um, que fica, e a divisão nunca falha. O massacre de 60 → 120 → 240 usa até 360 de uma vez (quem volta ao estoque só serve no passo seguinte). Criar os 420 inimigos (60 + 360) leva ~60 ms ao carregar a fase (descartável), escondido pelo escurecer |
| `MaxTotalEnemies` | 350 | O mesmo orçamento de física medido no ninho (pico de 380 em massacre, FPS ≥ 100). Saiu do diretor no passo 4: o "posso criar mais um inimigo?" fica num lugar só |

### `MobileSpawner` (cena `mobile_spawner.tscn`)

| Parâmetro | Valor inicial | Observação |
|---|---|---|
| `MaxHealth` | 300 | ~8,6 s de luz contínua (35/s) |
| `MoveSpeed` | 150 px/s | ~41 s por conector de nós, ~24 s por rota de placa. O jogador voa a 600 |
| `PanicSpeedMultiplier` | 2 | O último em pânico: ~435 px/s, ainda abaixo do jogador |
| `PanicDuration` | 2 s | Depois de sair da luz; 0 = não entra em pânico |
| `ActivationRadius` | 2000 px | No zoom das arenas (0,7) a tela mostra ~2740 × 1540 px: começa a gerar um pouco antes de aparecer |
| `SpawnScatter` | 40 px | Raio em que os inimigos nascem |
| `DeathBiomass` | 20 orbes de 1 | Um ancestral rende 4: o spawner vale 5 ancestrais |
| `BiomassDropScatter` | 60 px | Raio em que os orbes se espalham |
| `BiomassOrbLifetime`, `MaxBiomassOrbs` | 30 s, 300 | Como no inimigo (o `BiomassField` usa o último teto informado) |
| Visual provisório | raio 40 (o da colisão), cores do corpo, do pânico e do anel, pulso | Grupo "Visual provisório" |

O dano do contato é o do `SuitIntegrity` (`ContactDamage` 10, `KnockbackImpulse` 200, `HitInvulnerability` 0,8 s), igual ao do inimigo (S8b).

### `FractalEnemy`

| Parâmetro | Valor inicial | Observação |
|---|---|---|
| `DespawnDistance` | 0 no código (desligado); 3000 px na `fractal_enemy.tscn` | Longe da tela em qualquer zoom (meia diagonal a 0,7: ~1570 px). Na `sala_ninho` nunca acontece |
| `DespawnDelay` | 3 s | Evita recolher quem só passou um instante longe |

### `SpawnerIndicator` (setas, no `PhaseHud`)

| Parâmetro | Valor inicial | Observação |
|---|---|---|
| `Director` | — | Ligado no Inspector |
| `EdgeMargin` | 48 px | Da ponta das setas até a borda da tela |
| `ArrowSize` | 28 px | Seta do mais próximo (visual provisório) |
| `OtherArrowScale`, `OtherArrowAlpha` | 0,6 e 0,5 | Setas dos outros: menores e meio transparentes (visual provisório) |
| `ArrowColor` | a cor do anel do spawner | Visual provisório |

## 5. Casos de borda

- **Spawner morto com inimigos dele vivos:** continuam. Ao morrer, voltam ao estoque compartilhado, que não depende do spawner (não há referência a um spawner já liberado).
- **Luz através da parede:** a área da luz não é bloqueada por corpos, mas os vãos entre estruturas têm ≥ 1000 px e o feixe ~220 px: na prática, só se ilumina um spawner na mesma estrutura ou na emenda entre duas.
- **Caçar no corredor:** o feixe que persegue o spawner pode irritar as paredes do corredor (~2 s; placas ~1,5 s). Risco × recompensa intencional.
- **Modo automático da lanterna:** o feixe segue a velocidade; voando atrás do spawner, ele fica iluminado. Combina com a fuga.
- **Encostar no spawner:** mesmo golpe do inimigo; o empurrão afasta o jogador, e a imunidade evita golpes seguidos. Se o jogador continuar no caminho, o spawner o empurra pela rota.
- **"Carona" no spawner (verificado no motor, passo 2):** o `Player` usa Motion Mode Grounded, que trata como "chão" a superfície com a normal para cima e acompanha um chão que se move. No descartável, o jogador encostado por cima (ou empurrado pelo spawner de baixo para cima) foi carregado e continuou em cima dele depois da curva (terminou a 3 px do centro do spawner, 495 px longe de onde estava). **Correção, só no Inspector:** `player.tscn` > `Player` > Moving Platform > **Floor Layers** sem a camada 2. Com ela, o jogador é empurrado e fica para trás na curva, como no modo Floating. Vale também para os inimigos, que estão na mesma camada.
- **"Cola" (achado no motor, passo 3; corrige o passo 2):** um corpo movido por código que só muda de posição entra ~3 px no jogador a cada passo. O `MoveAndSlide` do jogador trata a sobreposição como colisão e desliza o movimento ao longo dela, cortando a parte que o afastaria: parado na frente, o jogador ficou colado, levou 12 golpes em 10 s e acumulou velocidade até 1996 px/s (cada golpe soma 200 e nada a tira enquanto ele está colado). O modo Floating também cola. **Correção:** o spawner empurra o jogador antes de andar (seção 2, "Corpo e contato"): 2 golpes em 10 s, velocidade máxima de 228 px/s, nenhum passo colado, rota mantida.
- **Encurralado:** com o jogador entre o spawner e uma parede, o empurrão é barrado e o spawner espera rente a ele (folga de 0,07 px); o jogador leva um golpe por imunidade até sair pelo lado. Nas rotas da fase quase não acontece: não há parede atravessada no eixo dos corredores.
- **Inimigos nascendo dentro do spawner:** atravessam o corpo dele (máscara 1) e se espalham pela separação da horda.
- **Dois spawners na mesma rota ou no mesmo ponto:** sobrepõem-se (corpos movidos por código não se empurram). Aceito.
- **Fuga com o jogador à frente:** vira na rota; com o jogador parado no ponto de destino, o spawner volta pela rota e escolhe outra no ponto de origem.
- **Último spawner frenético e massacre:** o teto de física corta a geração; os vivos ainda se dividem, então o total pode passar um pouco do teto (como no ninho: pico de 380 com 350). Medir.
- **Liberar as saídas:** criar 10 spawners é barato. O estoque é criado ao carregar a fase (escondido pelo escurecer), não ao liberar (o engasgo apareceria na tela).
- **Inimigo recolhido dentro do feixe ou encostado no jogador:** as guardas existentes cobrem (`_isDead` no `TakeDamage`, `IsDead` no `PlayerHurtbox`). O spawner morto também responde `IsDead`.
- **Inimigo reaproveitado no passo em que voltou ao estoque** (golpe fantasma; backlog 1.8, verificado no motor): o traje ainda o tinha na lista e acusava um contato de longe. Por isso os estoques do `EnemyPool` são filas com descanso de um passo (`fractal-enemy.md`, seção 5).
- **Recargas seguidas:** registros estáticos limpos no `_ExitTree`; conferir "Nós Órfãos" = 0.
- **Setor de teste:** 3 rotas elegíveis (o nó 2 de entrada toca as outras 3); com 10 spawners, vários dividem a mesma rota.
- **Seta:** o mais próximo em linha reta pode estar do outro lado de um vão (o caminho é mais longo). Aceito na etapa 1.
- **Seta depois da fase limpa:** some (não há spawner vivo). Apontar para a eclusa nesse momento seria uma extensão possível; não está nesta spec.
- **"Spawners: 0/3" antes da fase limpa:** na morte do último, o HUD recebe a contagem (0) e, no mesmo passo, o aviso de fase limpa; o texto final é "Fase limpa: volte à eclusa".

## 6. Arquivos e cenas

- **Existentes, lidos:** `NinhoSpawnerPooled.cs` (modelo do estoque), `NinhoSpawner.cs`, `FractalSwarm.cs` (`Count`), `FractalEnemy.cs`, `LightDamageArea.cs`, `PlayerHurtbox.cs`, `SuitIntegrity.cs`, `HexPhaseLayout.cs`, `HexPhaseBuilder.cs`, `StartChamber.cs`, `BiomassField.cs`.
- **Novos (criar só com autorização):**
  - `MobileSpawner.cs` (`AnimatableBody2D`, o nó da Godot para corpos movidos por código) e a cena `mobile_spawner.tscn` (montada pelo dev): anda pelas rotas, gera inimigos, toma dano da luz, foge, morre e avisa. **Criado no passo 2** (andar pelas rotas e silhueta provisória).
  - `SpawnerDirector.cs` (`Node2D`, filho da raiz da fase): começa no `IncursionStarted`, cria e posiciona os spawners, calcula a curva de dificuldade e emite `SpawnersChanged` e `AllSpawnersDestroyed`. **Criado no passo 2** (começar e posicionar).
  - `EnemyPool.cs` (`Node2D` parado, filho da raiz da fase): o estoque compartilhado e o orçamento de inimigos (`PoolSize`, `MaxTotalEnemies`). **Criado no passo 4.** Ampliado no backlog 1.8 (out/2026): segundo estoque, o de fragmentos (`FragmentPoolSize`), entregue ao inimigo pelo contrato `IFragmentSource` (`IFragmentSource.cs`).
  - `ILightDamageable.cs` (interface C#): "o que a luz fere". **Criado no passo 3.**
  - `IContactHazard.cs` (interface C#): "o que fere o traje ao encostar". **Criado no passo 3.**
  - `SpawnerHud.cs` (`Label`) e `SpawnerIndicator.cs` (`Control`, a seta). **Criados no passo 6.**
- **Alterados (proposto):**
  - `HexPhaseLayout.cs`: o grafo de rotas (pontos e rotas), montado junto com as estruturas (passo 1); `HexRoute.PointAt` (passo 2).
  - `LightDamageArea.cs`: fere qualquer `ILightDamageable` (inimigo e spawner), em vez de só `FractalEnemy` (passo 3).
  - `PlayerHurtbox.cs`: percebe qualquer `IContactHazard` (inimigo e spawner), em vez de só `FractalEnemy` (passo 3).
  - `FractalEnemy.cs`: implementa as duas interfaces (já tinha `TakeDamage` e `IsDead`; passo 3); recolhimento por distância (`DespawnDistance`, `DespawnDelay`; passo 5).
  - `MobileSpawner.cs` (passo 3): vida, pânico, morte com biomassa, as duas interfaces e o empurrão antes de andar.
- **Cenas (no editor):** `mobile_spawner.tscn` (nova), `hex_phase.tscn` (nós novos), `fractal_enemy.tscn` (`DespawnDistance`), `player.tscn` (Floor Layers sem a camada 2, contra a "carona"; as máscaras já incluem a camada 2).
- **Sem mudança:** `SuitIntegrity.cs` (o golpe é o mesmo), `FractalSwarm.cs`, `NinhoSpawnerPooled.cs`, `NinhoSpawner.cs`, `StartChamber.cs` (só emite o sinal), `HexPhaseBuilder.cs`.

## 7. Passos no editor (resumo; o roteiro numerado completo vem com cada passo do plano)

1. **Build** depois de cada script com `[Export]` ou `[Signal]` novo.
2. `mobile_spawner.tscn`: raiz **`MobileSpawner`** (`AnimatableBody2D`, `MobileSpawner.cs`) com **`CollisionShape2D`** (círculo de raio 40); Collision **Layer** só 2; **Mask** só 1 (vazia no passo 2; a 1 entrou no passo 3, para o spawner enxergar o jogador e empurrá-lo).
3. `hex_phase.tscn`: **`SpawnerDirector`** (`Node2D`, `SpawnerDirector.cs`; passo 2) e **`EnemyPool`** (`Node2D`, `EnemyPool.cs`, campo **Enemy Scene**; passo 4) como filhos da raiz `HexPhase`, em (0, 0), nessa ordem e logo acima do `Player` (inimigos desenhados sobre os spawners e sob o jogador); campos do diretor a ligar: **Phase**, **Start Chamber**, **Spawner Scene** e **Pool** (passo 4).
4. `PhaseHud` > **`SpawnerLabel`** (`Label`, `SpawnerHud.cs`, em (16, 112)) e **`SpawnerIndicator`** (`Control`, `SpawnerIndicator.cs`, Layout Full Rect, Mouse Filter Ignore, campo **Director**); sinais `SpawnersChanged` → `SpawnerLabel.OnSpawnersChanged` e `AllSpawnersDestroyed` → `SpawnerLabel.OnAllSpawnersDestroyed` (passo 6).
5. `fractal_enemy.tscn`: **Despawn Distance** = 3000.
6. `player.tscn` > raiz **`Player`** > CharacterBody2D > Moving Platform > **Floor Layers**: sem a camada 2 (passo 2).

## 8. Pronto quando

Confirmado pelo dev no editor (out/2026), passo a passo (seção 10, plano):

- [x] `dotnet build` sem erros a cada passo
- [x] Na câmara travada não há spawner nem inimigo; ao liberar as saídas aparecem `SpawnerCount` spawners, longe do nó de entrada, e o HUD mostra "Spawners: 10/10"
- [x] Os spawners andam só pelos eixos dos corredores, trocando de rota nos nós e painéis, sem atravessar paredes nem sair da fase
- [x] Um spawner só gera inimigos com o jogador perto
- [x] O spawner é sólido para o jogador: encostar dá o golpe do inimigo; no caminho dele, o jogador é empurrado, sem "carona" e sem ficar preso na parede; os inimigos o atravessam
- [x] A luz fere o spawner; iluminado, ele foge mais rápido; com a vida zerada, some, solta biomassa e o HUD desconta
- [x] A cada spawner morto, os restantes geram mais rápido (intervalos da seção 2)
- [x] Há uma seta para cada spawner vivo fora da tela, e a do mais próximo fica em destaque; spawner na tela não tem seta
- [x] Inimigos longe do jogador somem sem soltar biomassa e voltam ao estoque
- [x] Com todos mortos, o HUD mostra "Fase limpa: volte à eclusa" e a eclusa extrai como antes
- [x] A `sala_ninho` (bancada) continua funcionando: a luz mata os inimigos e o contato fere
- [x] Pior caso (último spawner frenético, massacre):
  - o passo de física constante fica em ~4 ms (dev: 4,05 ms no editor; descartável: ~4 ms com ~270 inimigos), FPS 826 com V-Sync desligado e "Nós Órfãos" = 0 em todas as recargas;
  - o pico isolado de ~27 ms na divisão em massa saiu com o estoque de fragmentos (backlog 1.8, out/2026; seção 10, "Desempenho");
  - os picos que restam (~9 a 14 ms) são pausas do coletor de lixo do .NET (backlog 1.10).

## 9. Como testar (roteiro manual no editor)

1. F5: na câmara não há spawner nem inimigo. Libere as saídas: o HUD mostra 10/10 e aparecem as setas, a do mais próximo em destaque.
2. Siga a seta em destaque até um spawner (ao entrar na tela, a seta dele some e o destaque passa ao próximo); confira que ele anda pelo eixo do corredor e troca de rota nos nós e painéis.
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
6. **S6, rastreabilidade:** contador no HUD + seta na borda da tela para o mais próximo. Descartados: texto com a estrutura de cada spawner; sonar periódico. **Revista pelo dev depois do teste do passo 6** (out/2026): uma seta para cada spawner fora da tela, com a do mais próximo em destaque (maior e opaca; as outras menores e meio transparentes). Descartadas: todas iguais (setas sobrepostas sem dizer qual é o mais perto); tamanho caindo aos poucos com a distância.
7. **S7, quando todos morrem:** "Fase limpa: volte à eclusa"; a extração encerra. Descartadas: extração automática; tela de fim da demo.
8. **S8, corpo:** sólido e fere (camada 2, a mesma dos inimigos). Descartadas: atravessável sem dano (a recomendação, numa camada 3 só da luz); sólido sem dano.
   - **S8a:** empurra o jogador e segue a rota (`AnimatableBody2D`). Descartada: parar quando bloqueado pelo jogador (`CharacterBody2D`). No passo 3, o "empurrar" passou a ser explícito (o spawner move o jogador antes de andar), porque mover o corpo direto colava o jogador (seção 5); a decisão não mudou.
   - **S8b:** o dano do contato é o mesmo golpe do inimigo. Descartado: dano próprio, maior (mudaria o `SuitIntegrity` e o `PlayerHurtbox`).
9. **S9, valores iniciais:** os da seção 4, como pontos de partida.
10. **S10, trajeto perto das paredes** (out/2026, no teste da fase B da `dyson-wall.md`). O dev quer o spawner perto das paredes durante o trajeto, para ele passar pelas fendas. São três formas para comparar no Inspector (seção 2), e a escolha final fica para depois. Descartada por ora: senoide fixa, igual para todos (os spawners andariam em fila na mesma curva).
11. **S11, sucção e volta à rota** (out/2026; `dyson-wall.md`, B3). O spawner é sugado como os inimigos, mas resiste mais (`SuctionFactor` 0,5).
    - Desviado, volta à rota por uma **mola com leve balanço**: continua no embalo, curva, cruza a rota de leve e acomoda. Descartadas: mola sem balanço (mais mecânica); direção com aceleração limitada (arco largo, retomando a rota mais adiante).
    - Ao cruzar a fenda, conta como destruído, sem soltar os orbes.
    - **Achado no teste do dev (corrige o passo 6):** num `AnimatableBody2D` com Sync to Physics, a posição definida só vale no passo seguinte, e lida logo depois ainda é a antiga. O spawner definia a posição duas vezes por passo (a rota e depois o desvio somado à posição lida), e o desvio apagava o avanço da rota. Enquanto estava desviado, ele parava na tela e, ao zerar o desvio, saltava centenas de pixels (verificado no motor, 4.7.2). Correção: uma escrita só por passo (ponto da rota + trajeto + desvio).

### Arquitetura (implementada)

- **Diretor (`SpawnerDirector`):** decide a dificuldade; os spawners só executam. É a ideia do "AI Director" do *Left 4 Dead*: um ponto central regula o ritmo, e os atores não conhecem a curva. Trocar a curva é mexer num lugar só.
- **Observer:** o spawner avisa `Destroyed`; o diretor avisa `SpawnersChanged` e `AllSpawnersDestroyed`; HUD e seta só escutam. A `StartChamber` não conhece os spawners (só emite `IncursionStarted`, como já faz).
- **Interfaces pequenas (`ILightDamageable`, `IContactHazard`):** a lanterna fere "qualquer coisa que a luz fere", e o traje sente "qualquer coisa que fere ao encostar", sem conhecer cada tipo. Um alvo novo não muda a lanterna nem o traje (princípio aberto/fechado). São duas e não uma porque são perguntas diferentes (princípio da segregação de interfaces): no futuro pode haver algo que a luz fere e que não fere ao encostar, ou o contrário.
- **Object Pool compartilhado:** o mesmo padrão do `NinhoSpawnerPooled`, mas num nó parado e com um único estoque para a fase; o tamanho do estoque é o teto de ancestrais.
- **Grafo de pontos de passagem (waypoint graph):** a navegação dos spawners é um grafo pequeno tirado da geometria que já existe. Dispensa pathfinding porque todo corredor é reto e cada rota é o eixo dele.
- **Processar só o que importa** (lição do `CLAUDE.md`): o spawner longe do jogador só anda (um `_PhysicsProcess` barato por spawner); o inimigo longe é recolhido. É a mesma ideia da parede que dorme.
- **Conexão com o `IncursionStarted` por código,** pelo campo `StartChamber` do diretor: com o campo vazio, a fase começa aberta e o diretor começa ao carregar (a fase nunca fica sem spawners por falta de ligação).

### Problema conhecido de FPS (`fractal-enemy-movement.md`, seção 11)

- O cenário era 400 inimigos aglomerados contra uma parede, com o jogador do outro lado. Com a geração por proximidade, o recolhimento por distância e os tetos (`PoolSize`, `MaxTotalEnemies`), a horda fica perto do jogador e limitada.
- **Medido no passo 7:** com o último spawner frenético e a horda (~270 inimigos) apertada em volta do jogador num conector de placa, o passo de física constante ficou em ~4 ms (seção "Desempenho"). O cenário da parede fina entre o jogador e a horda não foi medido; a dívida continua no backlog, com prioridade baixa.
- Se voltar a pesar: os caminhos da seção 11 daquela spec (instrumentação primeiro) e, aqui, gerar só com o jogador à vista (um raio do spawner ao jogador antes de gerar).

### Verificações no descartável (todas feitas)

- Verificado no passo 2: uma `Area2D` com máscara 2 percebe o `AnimatableBody2D` movido por código na camada 2; o jogador bate nele e é empurrado; a "carona" do modo Grounded acontece e é corrigida pelo Floor Layers (seção 5).
- Verificado no passo 4: criar o estoque de 60 inimigos leva ~15 ms, no quadro de montagem da fase (escondido pelo escurecer).
- Verificado no passo 5: o recolhimento devolve ao estoque sem fragmentos nem orbes, e o inimigo reaproveitado renasce normal (não some na hora).

### Plano de implementação (aprovado pelo dev, out/2026)

Cada passo: verificação antes no descartável (Godot headless), apresentação do código atual, da mudança, do porquê e das consequências, autorização, `dotnet build` e teste do dev no editor.

0. **Commit da câmara de partida** (backlog 1.7), pelo dev, antes de começar.
1. **Grafo de rotas** (`HexPhaseLayout.cs`): pontos de passagem e rotas, montados junto com as estruturas. Sem efeito visível. **Feito e testado pelo dev** (out/2026: tudo igual a antes, sem erros no Output):
   - tipos novos `HexWaypoint` (centro de um nó ou painel, com as rotas que saem dele) e `HexRoute` (eixo de um corredor, com `Other` e `Touches`), no mesmo arquivo; listas `Waypoints` e `Routes`; `AddNodeConnector`, `AddPlateConnector` e `AddCorridor` devolvem a estrutura criada;
   - verificado no descartável (Godot 4.7.2 headless, receita 2×): setor com 4 pontos e 6 rotas, hexágono com 13 e 30; saídas por ponto 12 (central), 5 (periféricos) e 3 (painéis); rotas de 6200 px (conectores de nós) e 3580 px (placas); com a entrada no nó 2, 5 rotas tocam o nó (25 elegíveis; no setor, 3); em 13.892 amostras (a cada 10 px e a ±50 px do eixo), nenhuma fora do piso, e toda rota passa por "ponta A → corredor → ponta B"; um círculo de raio 40 (e até 110) ao longo das rotas não toca parede; paredes inalteradas (364 e 1268); geometria e grafo do hexágono em 3,4 ms.
2. **Spawner que anda** (novos `MobileSpawner.cs` e `SpawnerDirector.cs`; cena `mobile_spawner.tscn`): o diretor cria os spawners no `IncursionStarted`, longe do nó de entrada; passeio aleatório; silhueta provisória; corpo sólido na camada 2. Teste: os spawners circulam pela fase; o jogador bate neles e é empurrado. **Feito e testado pelo dev** (out/2026: spawners andando pelas rotas, empurrando sem "carona", sem erros no Output; montagem conferida no disco):
   - `HexRoute.PointAt` no layout; `MobileSpawner` anda `MoveSpeed` px por passo e, ao passar da ponta, sorteia outra rota (menos a de onde veio), sem perder o que sobrou do passo; desenha o corpo e o anel com o raio da `CollisionShape2D`. `SpawnerDirector` se inscreve no `IncursionStarted` (ou começa adiado, sem câmara), embaralha as rotas que não tocam o nó de entrada e põe cada spawner no ponto sorteado **antes** de ele entrar na árvore (com o Sync to Physics, a posição vale no passo seguinte; um "pulo" da origem viraria velocidade na física);
   - verificado no descartável (0 falhas): nada antes de liberar; ao liberar, 10 spawners em 10 rotas distintas, nenhuma tocando o nó 2, entre 25% e 74% da rota, já no ponto certo; 60 s a 3000 px/s: 36.110 amostras sem sair do piso, sem salto, 398 trocas de rota sem voltar pela mesma, sem tocar parede, 30 de 30 rotas visitadas; setor sem câmara começa sozinho (10 spawners nas 3 rotas elegíveis); `Area2D` com máscara 2 percebe o spawner (com máscara 1, não); inimigo (máscara 1) atravessado; "carona" confirmada e corrigida pelo Floor Layers (seção 5).
3. **Luz e contato** (novos `ILightDamageable.cs` e `IContactHazard.cs`; alterados `LightDamageArea.cs`, `PlayerHurtbox.cs` e `FractalEnemy.cs`; mais vida, pânico e morte no `MobileSpawner`): a luz fere e mata o spawner, que foge e solta biomassa; encostar fere. Teste: matar spawners; `sala_ninho` igual a antes. **Feito e testado pelo dev** (out/2026: a luz mata o spawner, ele foge e solta biomassa, o contato fere sem colar, `sala_ninho` igual, sem erros):
   - também corrige a "cola" do passo 2 (seção 5): o spawner empurra o jogador antes de andar (`PushBlocker`, com `TestMove` e `MoveAndCollide`) e espera se ele estiver encurralado;
   - verificado no descartável (0 falhas; cópias reais de `LightDamageArea`, `PlayerHurtbox`, `SuitIntegrity` e `FractalEnemy`): a luz mata o spawner parado em 8,62 s (esperado 8,57), com 20 orbes, e ele sai da árvore sem erros; regressão: a mesma luz mata o inimigo e os fragmentos (4 orbes); em pânico, meia-volta com o jogador à frente e 300 px/s, sem virar com o jogador atrás, de volta a 150 px/s 2 s depois da luz; no ponto, em pânico, a rota mais longe do jogador e, calmo, sorteio; contato: golpes de 10 com 0,8 s de imunidade e empurrão para longe; regressão: inimigo encostado fere; a verificação do passo 2 (rotas, máscaras, "carona") refeita com o spawner novo, sem falhas.
4. **Geração e curva** (novo `EnemyPool.cs`; geração no `MobileSpawner`; curva e contagem no `SpawnerDirector`): inimigos nascem perto do jogador; o intervalo encurta a cada spawner morto; tetos de estoque e de física. **Feito e testado pelo dev** (out/2026: inimigos nascem perto dos spawners e ficam para trás, os restantes aceleram a cada morte, sem erros, "Nós Órfãos" = 0; montagem conferida no disco):
   - `EnemyPool` (estoque de 60 criado ao carregar; `TrySpawn` respeita `PoolSize` e `MaxTotalEnemies`, que saiu do diretor); `MobileSpawner` com o sinal `Destroyed`, `ActivationRadius`, `SpawnScatter`, `Pool` e `SetPace` (intervalo e velocidade dados pelo diretor); `SpawnerDirector` conta vivos e mortos e reparte o orçamento (`ApplyPace`) ao criar e a cada `Destroyed`;
   - verificado no descartável (0 falhas): ritmo igual em todos os vivos e conforme a fórmula nos 10 níveis (6,667 s e ×1,00 com 10 vivos; 0,351 s e ×1,45 com o último); nenhum inimigo com o jogador longe; o último gerou 29 inimigos em 10 s a 217,4 px/s; numa reta, o primeiro na hora e depois um a cada 1,00 s, a até 39,6 px do spawner, parando com o jogador a ~2000 px; os inimigos são filhos do estoque e não acompanham o spawner (o primeiro andou 200 px em 20 s, o spawner ~3000); estoque de 5 esgota sem erro, inimigo morto volta e sai de novo, teto de física barra a geração; estoque de 60 criado em ~15 ms; as verificações dos passos 2 e 3 refeitas com o código novo, sem falhas.
5. **Recolhimento por distância** (`FractalEnemy.cs`; `DespawnDistance` na `fractal_enemy.tscn`). **Feito e testado pelo dev** (out/2026: tudo ok; Despawn Distance = 3000 na `fractal_enemy.tscn`, conferido no disco):
   - `TryDespawn` no começo do `_PhysicsProcess`: longe do jogador mais que `DespawnDistance` por `DespawnDelay`, o inimigo sai de cena sem se dividir nem soltar biomassa; perto, o relógio zera. A saída de cena do fim do `Die()` virou o `LeaveScene()` (volta ao estoque ou `QueueFree`), usado pela morte e pelo recolhimento; o `ActivateAt` zera o relógio;
   - verificado no descartável (0 falhas): com um perto e dois longe, aos 2,9 s todos vivos e aos 3,1 s os dois longe de volta ao estoque, sem orbes nem fragmentos; reaproveitado perto não some; 2 s longe e de volta continua vivo, 3,1 s longe é recolhido; fragmento longe sai da árvore; com `DespawnDistance` 0, nunca; com um spawner gerando, 3,2 s depois de o jogador ir embora o estoque está cheio de novo (10 de 10) e, com ele de volta, gera de novo; as verificações dos passos 3 e 4 refeitas com o inimigo novo, sem falhas.
6. **HUD, seta e fase limpa** (novos `SpawnerHud.cs` e `SpawnerIndicator.cs`; sinais `SpawnersChanged` e `AllSpawnersDestroyed`). **Feito e testado pelo dev** (out/2026; o `SpawnerIndicator` ficou sem Full Rect, o que não muda nada: a seta desenha descontando a posição do `Control`, e a Godot não corta o desenho fora dele). Depois do teste, o dev pediu uma seta para cada spawner (S6 revista): **revisão feita e testada pelo dev** (out/2026: uma seta por spawner fora da tela, a do mais próximo em destaque, sem erros):
   - revisão: o `SpawnerIndicator` monta uma lista de setas (`Arrow`: ponta, direção, se é o mais próximo), uma por spawner vivo fora da tela; desenha primeiro as dos outros (`OtherArrowScale` 0,6, `OtherArrowAlpha` 0,5) e por último a do mais próximo, por cima. Verificado no descartável (0 falhas): 31 setas conferidas contra a conta independente em 11 posições (zoom 1 e 0,7), todas com a direção e a ponta na borda certas; spawner na tela sem seta; um único destaque, só com o mais próximo fora da tela; HUD e fase limpa iguais;
   - versão original (uma seta só):
   - `SpawnerDirector` emite `SpawnersChanged(restantes, total)` ao criar os spawners e a cada morte, e `AllSpawnersDestroyed` quando o último morre; expõe `AliveSpawners` (só leitura) para a seta. `SpawnerHud` só escuta (como o `StructureHud`). `SpawnerIndicator` acha o vivo mais próximo do jogador e, se ele estiver fora da tela, desenha um triângulo na borda (a `EdgeMargin`), na direção dele; a posição na tela vem da transformação do canvas (câmera e zoom);
   - verificado no descartável (0 falhas; tela 1920 × 1080, configurações de exibição do projeto): a seta, comparada com a conta independente "centro da tela + (spawner − jogador) × zoom", acertou a direção (erro de 0,00°) com a ponta na borda em 5 posições fora da tela e sumiu nas 3 posições com o mais próximo na tela, com zoom 1 e 0,7; o HUD passou por "Spawners: 3/3" → "2/3" → "1/3" → "0/3" → "Fase limpa: volte à eclusa", com um único `AllSpawnersDestroyed`, e a seta sumiu sem spawners vivos; a verificação do passo 4 refeita com o diretor novo, sem falhas.
7. **Desempenho e ciclo** (medição do dev, seção 9, itens 8 e 9) e **fechamento**: esta spec, `BACKLOG.md`, `hex-phase.md` (fase C), `CLAUDE.md`, `HANDOFF.md` e a mensagem de commit. **Feito** (out/2026): medição do dev no editor e de referência no descartável (abaixo, "Desempenho"); o engasgo da divisão em massa virou o item 1.8 do backlog (decisão do dev: fechar o 1.4 e registrar). Documentos fechados: esta spec, `BACKLOG.md` (1.4 concluído, 1.8 novo), `hex-phase.md` (fase C), `fractal-enemy.md`, `fractal-enemy-movement.md`, `player-health-death.md`, `start-chamber.md`, `CLAUDE.md` e `HANDOFF.md`.

### Desempenho (passo 7, out/2026)

**Medição do dev no editor** (Depurador > Monitores, V-Sync desligado só no teste; último spawner frenético e massacre com a lanterna). Feita com valores de teste no inimigo (`Speed` 40 e `BaseBranchLength` 20 na `fractal_enemy.tscn`, revertidos para 10 e 8 depois), com inimigos mais rápidos e maiores que os do jogo:

| Monitor | Valor |
|---|---|
| FPS | 826 |
| Processo de Física | 4,05 ms constante; um pico isolado de ~27 ms (topo do gráfico) |
| Física 2D > Objetos Ativos | até 91 |
| Nós / Nós Órfãos | 4104 / 0 (0 em todas as recargas) |

**Referência no descartável** (Godot 4.7.2 headless, build Debug, hexágono com 1268 paredes, estoque 60, teto 350; tempo dos callbacks de física por passo, medido com sondas de prioridade mínima e máxima; o monitor do headless infla os valores):

| Cenário | Inimigos | Callbacks de física por passo |
|---|---|---|
| 10 spawners andando, sem inimigos | 0 | média 0,17 a 0,19 ms |
| Último spawner frenético (num conector de placa), estoque esgotado em volta do jogador | 60 | média 0,30 a 0,35 ms, máx. 0,53 ms |
| Depois de um massacre (60 → 120 → 240 fragmentos) e o estoque soltando de novo | 246 a 274 | média 3,9 a 4,1 ms, máx. 4,7 a 7,9 ms |

**O pico isolado:** é o quadro em que dezenas de inimigos se dividem ao mesmo tempo. Cada divisão instancia dois fragmentos novos (`Instantiate`, `AddChild` adiado e `_Ready`), e isso entra no quadro de física depois dos callbacks (as chamadas adiadas). Medindo o quadro inteiro no descartável: dividir 60 de uma vez custou 28 a 39 ms; dividir 120, 35 a 42 ms; o quadro seguinte volta a 3 a 8 ms. Com um "aquecimento" antes (o .NET já tendo compilado esses caminhos), o pico continuou, então não é a primeira execução do código: é o custo de criar nós (~0,15 a 0,3 ms por fragmento). Solução proposta no backlog 1.8 (pool de fragmentos).

**Estoque de fragmentos (backlog 1.8, out/2026):** na fase, os fragmentos saem de um segundo estoque do `EnemyPool` (`FragmentPoolSize` 360; `fractal-enemy.md`, seções 5 e 10).

*Descartável* (Godot 4.7.2 headless, campo aberto, quadro inteiro):
- dividir 60 de uma vez caiu de 13–18 ms para 4–6 ms;
- dividir 120, de 31–35 ms para 7–8 ms;
- criar os 420 inimigos ao carregar leva ~60 ms, escondido pelo escurecer;
- 60 vivos custam o mesmo com e sem o estoque parado (callbacks de 0,57 a 0,62 ms por passo).

*Teste do dev no editor* (`PoolSize` 60, `DespawnDistance` 3000, V-Sync desligado só no teste, massacre de propósito com 2 spawners restantes). Para atribuir cada pico, um diagnóstico temporário, retirado antes do commit, escreveu por segundo o pior passo de Física, os fragmentos e orbes criados (total e rajada num mesmo passo) e as coletas do GC (pausa e memória alocada):

| Medida | Valor |
|---|---|
| Processo de Física | ~1,5 ms fora dos picos. Todos os picos de 8 ms ou mais durante o jogo (8,6 a 14,2 ms) tiveram uma coleta do GC, com a pausa 1 a 2 ms abaixo do pico (ex.: 14,2 ms com pausa de 12,4 ms) |
| Divisões | No máximo 2 fragmentos por passo (uma divisão por vez), mesmo no massacre (até 43 vivos): a lanterna mata cada inimigo num momento |
| Morte de spawner (20 orbes) | 3,4 a 4,1 ms; 8,3 ms na primeira (primeira execução do código) |
| GC | Uma coleta quase todo segundo, quase sempre da geração 1, com pausa de 7 a 9 ms e só 0,3 a 0,7 MB alocados por segundo (backlog 1.10) |
| Estoque / Nós Órfãos | Nenhum fragmento criado no meio do jogo (o estoque não cresceu; o diagnóstico registraria) / 0, inclusive nas recargas |
| Carga | 61 a 67 ms no primeiro passo depois da carga e dois passos de ~14 ms na câmara, sem GC nem inimigos (fora do 1.8) |
| Contato e bancadas | O empurrão vem sempre de quem encosta (sem golpe fantasma); a `sala_ninho` e a `arena` dividem como antes |

Numa rodada de estresse anterior (`PoolSize` 200, sem recolhimento, horda de 130 a 200 Objetos Ativos), os picos foram de ~11 a ~27 ms, sem diagnóstico para atribuí-los; o de 27 ms veio logo depois de uma pausa do jogo. Pelos Nós, o estoque também não cresceu.

**Risco residual** (sem item próprio, decisão do dev): cada inimigo da última geração solta um orbe, nó criado na hora. No descartável, 240 orbes no mesmo passo custaram 16 a 20 ms; no jogo, as rajadas foram de até 20.

### Ideias para depois

- **"Horda que persegue"** (ideia do dev, out/2026; implementação futura, sem data). Em vez de recolher ao estoque o inimigo que fica longe (passo 5), reposicioná-lo logo fora da tela, perto do jogador, para a pressão acompanhá-lo para onde ele for.
  - **Referência:** no Vampire Survivors, os inimigos comuns somem quando o jogador se afasta (como no passo 5), mas os chefes são teleportados de volta para a tela. A sensação de horda constante lá vem de inimigos novos nascendo o tempo todo na borda da tela, em volta do jogador.
  - **Hoje:** os inimigos só nascem perto de um spawner (S5), e quem fica para trás é recolhido. Longe dos spawners a pressão cai, o que combina com a compressão e o alívio da fase (corredores sem spawner ficam calmos).
  - **Como poderia ser:** no `TryDespawn`, em vez do `LeaveScene`, achar um ponto válido no piso logo fora da tela, por exemplo sobre uma rota do grafo, na estrutura do jogador ou numa vizinha, a uns 1600 a 2000 px dele, sem parede no meio. O inimigo manteria a vida e a geração.
  - **Pontos a decidir antes:** muda a S5 (a pressão longe dos spawners) e o ritmo de compressão e alívio; mantém a horda sempre perto do jogador (custo de física, tetos); o ponto precisa ser alcançável (sem parede entre o inimigo e o jogador, para não recriar o problema conhecido de FPS da horda contra a parede); vale para todos os inimigos ou só para alguns (como os chefes do Vampire Survivors)?

### Relação com outras decisões

- Backlog, seção 2: "Valores definitivos de `MaxConcurrentEnemies` e `MaxTotalEnemies`": na fase, o `PoolSize` do `EnemyPool` (60) faz o papel do `MaxConcurrentEnemies`, e o `MaxTotalEnemies` (no `EnemyPool`) continua 350. Mantidos depois da medição (passo constante de ~4 ms no pior caso); continuam pontos de partida.
- `dyson-wall.md` (backlog 1.5, fase B): os spawners são sugados (B3), com o trajeto perto das paredes (S10) e a volta à rota com mola (S11).
- `hex-phase.md`: esta spec é a fase C (concluída); o recheio das estruturas fica para depois.
- Backlog 1.8 (concluído em out/2026): engasgo de um quadro na divisão em massa, resolvido pelo estoque de fragmentos no `EnemyPool`.
- Backlog 1.10: pausas do coletor de lixo do .NET (GC), achadas no teste do 1.8.
