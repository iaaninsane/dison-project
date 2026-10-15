# Spec: Coleta de biomassa neon

- **Status:** validada (out/2026). Valores em teste.
- **Etapa:** 1 (mecânica)
- **Depende de:** `docs/specs/fractal-enemy.md` (morte do inimigo e divisão celular). A spec de evolução (gastar a biomassa) ainda não existe.

> **Valores em teste:** como nas demais specs da demo, os `[Export]` e os números desta spec são pontos de partida, não valores definitivos.

## 1. Objetivo

Todo anticorpo destruído pela luz libera biomassa neon, que o jogador recolhe navegando até ela. É a recompensa do combate e o recurso que, numa spec futura, será gasto em melhorias do traje na câmara inicial. Serve ao trecho COLETAR do core loop e liga o COMBATER ao EVOLUIR.

## 2. Comportamento esperado

### Liberação

- Só a **última geração** solta biomassa: quando um inimigo morre **sem se dividir** (com os valores atuais, a geração `MaxGeneration`), então libera `BiomassPerKill` unidades de biomassa no local da morte, como um orbe. Inimigos que se dividem não soltam nada. Com 2 fragmentos e geração máxima 2, cada ancestral rende 4 orbes.
- Vale com ou sem pool.
- Cada orbe nasce com um pequeno deslocamento aleatório (`DropScatter`), para os orbes de mortes próximas não ficarem empilhados.
- O orbe é adicionado à cena no fim do frame (mesmo cuidado da divisão celular: a morte acontece dentro do laço de dano da lanterna).

### Coleta

- O orbe não usa física: um sistema central mede a distância de cada orbe ao jogador num único laço em C#.
- O orbe é sempre visível, mesmo no escuro (material "unshaded", que ignora o `CanvasModulate`).
- Quando o jogador está a menos de `MagnetRadius` de um orbe, então o orbe é atraído em direção ao jogador a `MagnetSpeed`, atravessando paredes se houver alguma no caminho.
- Quando o jogador está a menos de `PickupRadius` de um orbe, então a biomassa é somada ao contador do jogador e o orbe desaparece.
- Quando o contador muda, então ele avisa quem estiver interessado (sinal `BiomassChanged`), sem conhecer quem escuta (padrão Observer). O primeiro ouvinte é um HUD mínimo de texto para teste.

### Limites

- Quando um orbe passa `OrbLifetime` segundos sem ser coletado, então desaparece.
- Quando já existem `MaxOrbs` orbes na cena, então o orbe mais antigo desaparece para dar lugar ao novo.

## 3. Fora do escopo

- Gastar a biomassa (melhorias do traje): spec de evolução.
- Perder a biomassa ao morrer: definido na spec `player-health-death.md` (a biomassa da sala é perdida, porque a sala é recarregada; guardar parte dela é assunto da spec de evolução).
- Persistência do contador entre salas ou entre partidas (a transição entre salas ainda não existe) e salvar em disco.
- Partículas, explosão e iluminação temporária na morte (spec própria).
- Arte final, animação, áudio e shaders (etapa 2). O orbe é desenhado por código, como forma simples.

## 4. Parâmetros ajustáveis (valores em teste)

Os pontos de partida foram aceitos pelo dev. A configuração de **quem produz** a biomassa fica no inimigo; a configuração **do traje** (ímã e coleta) fica no coletor, pronta para a futura spec de evolução.

| Parâmetro | Valor inicial | Onde fica | Observação |
|---|---|---|---|
| `BiomassPerKill` | 1 | `FractalEnemy.cs` | Só quem morre sem se dividir solta: 4 por ancestral com os valores atuais |
| `BiomassDropScatter` | 6 px | `FractalEnemy.cs` | Deslocamento aleatório ao nascer |
| `BiomassOrbLifetime` | 30 s | `FractalEnemy.cs` | Evita acúmulo infinito de orbes |
| `MaxBiomassOrbs` | 300 | `FractalEnemy.cs` | Teto de orbes vivos; o mais antigo some primeiro. O `BiomassField` usa o último valor informado por quem soltou um orbe |
| `PickupRadius` | 12 px | `BiomassCollector.cs` | Colisão do jogador tem ~10 px |
| `MagnetRadius` | 80 px | `BiomassCollector.cs` | Raio do ímã |
| `MagnetSpeed` | 700 px/s | `BiomassCollector.cs` | Acima do `MaxSpeed` do jogador (600), senão o orbe não alcança o jogador em fuga |

## 5. Casos de borda

- Massacre: dezenas de mortes no mesmo instante geram dezenas de orbes de uma vez. Não pode causar queda de FPS perceptível (ver seção 8).
- Inimigo do pool: libera biomassa ao morrer como qualquer outro (a liberação acontece antes de o inimigo voltar ao estoque).
- Morte e divisão no mesmo instante: os fragmentos e o orbe nascem juntos no mesmo ponto; o orbe não pode interferir na separação nem na colisão dos fragmentos.
- Orbe atraído com uma parede no caminho: atravessa a parede (decidido; sem física).
- Jogador ejetado pela parede (`TriggerEjection`): continua coletando normalmente.
- Troca de sala com orbes na cena: não pode deixar erro nem nós órfãos (testado pelo recarregamento da sala na morte do jogador; repetir quando existir transição entre salas diferentes).
- Orbe no escuro: sempre visível (material unshaded).
- Inimigo de geração intermediária que, por algum motivo, morre sem conseguir se dividir (ex.: `FragmentCount` = 0): conta como "morreu sem se dividir" e solta biomassa, para a recompensa nunca se perder.

## 6. Arquivos e cenas

- Alterados: `FractalEnemy.cs` (`SpawnFragments()` informa se dividiu; `Die()` chama `DropBiomass()` quando não houve divisão), `player.tscn` (nós `BiomassCollector` e `Hud/BiomassLabel` e a conexão do sinal).
- Novos:
  - `BiomassOrb.cs` (`Node2D`, criado por código, sem cena): só existe e se desenha (círculo, material unshaded compartilhado). Guarda quantidade, tempo de vida, idade e a cópia da posição em C#.
  - `BiomassField.cs` (classe estática): registra os orbes, aplica o teto e faz, num único laço por passo, a idade, o ímã e a coleta (mesma ideia do `FractalSwarm`).
  - `BiomassCollector.cs` (`Node2D`, filho do `Player`): chama o laço do `BiomassField` na posição do jogador, guarda o `Total` e emite o sinal `BiomassChanged(int total)`.
  - `BiomassHud.cs` (`Label`): HUD de teste "Biomassa: N", que só escuta o sinal.
- Somente leitura: `NinhoSpawnerPooled.cs`, `FractalSwarm.cs`.

## 7. Passos no editor (feitos pelo dev)

Feitos em out/2026, todos no `player.tscn`:

1. **Build** no editor depois dos scripts novos.
2. Nó raiz `Player` > filho **Node2D** chamado **`BiomassCollector`**, com o script `BiomassCollector.cs`, posição (0, 0).
3. Nó raiz `Player` > filho **CanvasLayer** chamado **`Hud`** > filho **Label** chamado **`BiomassLabel`**, com o script `BiomassHud.cs`, posição (16, 16).
4. Sinal: `BiomassCollector` > painel Nó > Sinais > `BiomassChanged(total: int)` conectado ao `BiomassLabel`, método receptor `OnBiomassChanged`.

Como o coletor e o HUD estão no `player.tscn`, funcionam em toda sala que tiver o jogador, sem montar nada na sala.

## 8. Pronto quando

- [x] `dotnet build` sem erros
- [x] Só os inimigos que morrem sem se dividir (última geração) liberam biomassa, no local da morte; 4 orbes por ancestral com os valores atuais
- [x] O orbe é visível no escuro, fora do feixe da lanterna (testado na `arena.tscn`)
- [x] Dentro do `MagnetRadius`, o orbe vai até o jogador; ao alcançá-lo, o contador sobe e o orbe some
- [x] O HUD de teste mostra o total de biomassa, atualizado pelo sinal `BiomassChanged`
- [x] Orbes não coletados somem depois do tempo de vida; nunca há mais que o teto de orbes
- [x] Na `sala_ninho` (200 no ninho, teto total 350), um massacre com orbes mantém o FPS ≥ 100 com V-Sync desligado (confirmado pelo dev, com tempo de vida dos orbes elevado a 120 s para acumular até o teto e coleta em massa atravessando a nuvem; números não anotados)
- [x] "Nós Órfãos" = 0 e nenhum erro no Output depois do massacre
- [x] Todos os valores da seção 4 ajustáveis no Inspector
- [x] Troca de sala com orbes na cena sem erros nem nós órfãos (testado pelo recarregamento da sala na morte do jogador, em out/2026; repetir quando existir transição entre salas diferentes)

## 9. Como testar (roteiro manual no editor)

1. `arena.tscn` (bancada de teste), F6: mate um inimigo e confirme que **não** sai orbe; mate os 2 fragmentos e confirme que também não sai; mate os 4 fragmentos finais e confirme um orbe no local de cada morte (4 por ancestral).
2. Aproxime-se: dentro do raio do ímã o orbe vem até o jogador; ao encostar, o HUD soma.
3. Deixe um orbe sem coletar e confirme que some depois de `OrbLifetime` (para testar rápido, use 5 s no Inspector).
4. `MaxOrbs` = 5 no Inspector: mate vários inimigos sem coletar e confirme que nunca há mais de 5 orbes.
5. `sala_ninho`, V-Sync desligado só no teste: massacre de 1 a 2 minutos sem coletar; anote FPS, Processo de Física e Nós Órfãos; depois colete tudo de uma vez passando pela nuvem de orbes.

## 10. Decisões e notas

### Decididas (out/2026)

1. **Quanto cada morte vale:** só a última geração solta biomassa (opção c). Regra implementável: "solta biomassa quem morre sem se dividir".
2. **Orbe sem física:** um sistema central mede a distância de cada orbe ao jogador num único laço em C#, sem consultas ao motor de física (a física é o gargalo medido com centenas de corpos).
3. **Visibilidade:** sempre visível, com material "unshaded" (opção a). Mesma técnica já usada no `Sprite2D` do jogador.
4. **Ímã:** sim, com `MagnetRadius`.
5. **Paredes:** o orbe atraído atravessa paredes.
6. **Contador:** componente no jogador, valendo para a sala ou partida atual. Persistência entre salas e o que acontece ao morrer ficam para as specs de evolução e de morte do jogador.
7. **Valores:** os pontos de partida da seção 4 foram aceitos.

### A testar no futuro

- Visibilidade com uma `PointLight2D` fraca por orbe (opção c): mais "neon", mas cara com centenas de orbes. Medir o custo com o Analisador Gráfico antes de adotar.
- Outras possibilidades de valores e regras (a esclarecer com o dev).

### Notas

- A `arena.tscn` serve só de bancada; nenhuma regra desta spec depende dela.
