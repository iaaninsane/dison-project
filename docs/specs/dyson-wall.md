# Spec: Parede de fronteira da esfera de Dyson (membrana fractal que abre para o vácuo)

- **Status:** proposta. **Modelo-alvo das paredes de fronteira com o espaço** (decisão do dev, out/2026): quando implementada, substitui nessas paredes o puxão da `reactive-walls.md`. Fase A pode começar já (mecânica); fases C e D são visuais.
- **Etapa:** fases A e B são etapa 1 (mecânica). Fases C e D são etapa 2 (arte e shaders), exceto se o dev liberar antes.
- **Depende de:** `LightDamageArea.cs` (já tem `WallIrritationPerSecond`). Relacionada: `docs/specs/reactive-walls.md` (implementação atual da irritação e da ejeção, transitória). Para posicionar as paredes no jogo: a spec da fase hexagonal (backlog 1.3).
- **Referência visual:** `docs/reference/dyson-wall-prototype.html`. Abrir no navegador e segurar o clique sobre a parede. É um protótipo em JavaScript, **não é código para portar linha a linha**, mas as fórmulas de cada abertura, as curvas de tempo e o modelo de luz devem ser reproduzidos a partir dele. Nomes de funções citados nesta spec (`params`, `local`, `amtAt`, `prism`, etc.) são as funções desse arquivo.

> **Valores em teste:** todos os números desta spec vêm do protótipo, que foi feito em pixels de uma tela de ~380 px de largura. São pontos de partida. Escalar para a resolução do jogo e expor como `[Export]`. Divergências entre spec e Inspector são esperadas.

## 1. Objetivo

As paredes superior e inferior de cada câmara são a fronteira com o espaço sideral. Elas não são metal rígido: são uma **membrana viva de peças geométricas** (triângulos de Sierpinski por padrão) que, quando irritada o suficiente pela luz da lanterna, se deforma e **abre uma fenda para o vácuo**, sugando e ejetando o jogador. A parede é ao mesmo tempo cenário, ameaça e linguagem visual: o movimento das peças telegrafa o perigo antes de ele acontecer.

Serve ao core loop como pressão ambiental: usar a lanterna perto das paredes tem custo.

## 2. Comportamento esperado

### 2.1 Geometria

- Quando a parede é instanciada, então gera por código uma faixa de peças entre uma borda superior (`TOP`) e inferior (`BOT`), com altura `WallThickness`.
- **Forma padrão: triângulos de Sierpinski.** Triângulos equiláteros alternando ponta para cima e ponta para baixo, encaixados lado a lado (ver `build()`, caso `tri`). Cada triângulo externo é subdividido recursivamente em `SierpinskiLevel` níveis (0 a 3); no nível n há 3ⁿ sub-triângulos, e o vão central de cada subdivisão fica vazio.
- Formas alternativas (para teste, mesma interface): hexágonos com triângulos de preenchimento e octógonos com triângulos de preenchimento (`build()`, casos `hex` e `oct`). Essas formas não têm sub-peças.
- Cada peça guarda: vértices de repouso, centro, um valor aleatório fixo `rnd` (0–1, sorteado na criação) e um índice de paridade `sh` (alterna 0/1). Cada sub-triângulo também tem seu `rnd`.
- **Membrana de fundo:** atrás de cada triângulo de Sierpinski há um polígono do tamanho do triângulo externo, cor `MembraneColor`. Ele preenche os vãos do fractal para a parede não parecer furada.

### 2.2 Irritação

- Enquanto o feixe da lanterna sobrepõe um trecho da parede, então cada peça dentro do feixe acumula irritação de 0 a 1, na taxa `WallIrritationPerSecond` (já existe em `LightDamageArea.cs`; converter a escala para 0–1).
- Fora do feixe, a irritação decai em `IrritationDecayPerSecond`.
- Quando a irritação passa de 0,5, então a peça treme (deslocamento aleatório por quadro proporcional a `(irr − 0,5) × 3` px).
- A peça irritada ganha brilho verde-água (`IrritationColor`) proporcional à irritação: mistura de até 55% na cor das faces mais um halo aditivo ao redor.
- Quando alguma peça chega a 1, então dispara uma abertura centrada nela (a de maior irritação, se houver várias). Só uma abertura por segmento de parede por vez.

### 2.3 Ciclo da abertura

Toda abertura tem 4 fases, com duração em `OpenPhaseDurations` (ver `DUR()` e `phaseAt()`):

| Fase | Padrão | Esmaecer | O que acontece |
|---|---|---|---|
| 0. Aviso | 0,35 s | 0,35 s | Tremor forte nas peças da área. Nenhum deslocamento ainda |
| 1. Abrir | 0,60 s | 1,10 s | A intensidade `k` vai de 0 a 1 |
| 2. Aberta | 1,40 s | 1,20 s | `k` = 1. Sucção ativa |
| 3. Fechar | 0,75 s | 1,30 s | `k` volta a 0 com curva suave |

- Curva da fase 1: **elástica** (passa do ponto e volta) para os modos orgânicos; cúbica de saída para os demais; suave (ease in-out) para Esmaecer (ver `elastic()`, `smooth()`, `amtAt()`).
- Curva da fase 3: sempre suave.
- Modos orgânicos (usam a curva elástica e um tremor secundário nas vizinhas): `peri, iris, inflar, rasgar, cilios, girar, torcer, esticar, gelatina, murchar, colapsar`.
- Cada peça tem um peso de proximidade em relação ao centro da abertura `ox`: `f = max(0, 1 − |d| / OpenRadius)` (núcleo) e `g = max(0, 1 − |d| / (2,5 × OpenRadius))` (halo).
- Ao terminar, a irritação das peças da área volta a 0.

### 2.4 Os 17 modos de abertura

A transformação acontece em **dois níveis hierárquicos**: primeiro cada sub-triângulo se transforma em relação ao próprio centro ou base (`local()`), depois a peça inteira aplica a sua transformação ao conjunto (`params()`). Para formas sem sub-peças, só o nível da peça se aplica. As fórmulas exatas estão nessas duas funções; abaixo, o resumo do que cada uma faz.

**Movimento (a parede reage como conjunto)**

| Modo | Peça inteira | Sub-triângulos |
|---|---|---|
| `slide` Deslizar | Desloca para longe do centro e encolhe até 35% | Encolhem um pouco |
| `petal` Pétalas | Gira em torno do vértice da base mais distante do centro | Giram nas próprias bases |
| `scatter` Dispersar | Sobe, afasta, gira e encolhe | Se espalham a partir do centro da peça e giram aleatoriamente |
| `peri` Peristalse | Onda senoidal percorre a parede antes e durante a abertura | Ondulam com fase própria |
| `iris` Íris | Gira em espiral em torno do centro da fenda e se afasta | Espiral menor dentro da peça |
| `inflar` Inflar | Estufa em cúpula até 60% de `k`, depois rompe e se afasta | Se expandem por dentro |
| `rasgar` Rasgar | Pétala com atraso proporcional à distância + aleatório | Descolam em ângulos aleatórios |
| `cilios` Cílios | Curva-se na base e continua batendo durante a fase aberta | Batem como cílios menores |

**Peça a peça (cada figura gira ou deforma)**

| Modo | Peça inteira | Sub-triângulos |
|---|---|---|
| `girar` Girar | Gira no próprio eixo (sentido alterna pela paridade `sh`) e encolhe | Giram no sentido oposto ao pai |
| `virar` Virar | Achata no eixo X como carta virando, em cascata | Viram com atraso aleatório |
| `torcer` Torcer | Gira e achata no eixo Y | Idem, menor |
| `esticar` Esticar | **Deforma vértices:** puxados para longe do centro como borracha | Se afastam entre si |
| `gelatina` Gelatina | **Deforma vértices:** cada vértice oscila com frequência própria | Herdam a oscilação |
| `murchar` Murchar | **Deforma vértices:** contração irregular por vértice | Encolhem em ritmos diferentes |
| `colapsar` Colapsar | Encolhe até um ponto, em cascata | Colapsam com atraso aleatório |
| `dobrar` Dobrar | Dobra no eixo Y em torno da borda superior, como origami | Dobram com fatores aleatórios |
| `esmaecer` Esmaecer | Ver 2.5 | Ver 2.5 |

- `virar` e `dobrar` simulam 3D: quando a peça passa de 90°, ela mostra a face de trás, que **está voltada para o sol e portanto é clara** (parâmetro `back`/`dark` em `prism()`, que mistura a cor das faces com âmbar).
- Todos os modos somam uma "respiração" permanente: `dy += sin(tempo × 1,6 + cx × 0,04) × 0,7 px`.

### 2.5 Esmaecer (padrão provisório da demo)

A escolha do Esmaecer como padrão da demo é **provisória** (out/2026: a forma final da deformação voltou a ficar em aberto; ver seção 10). As regras do modo em si estão decididas; não alterar sem consultar o dev:

- **Somente redução de escala. Nenhuma transparência, em nenhum objeto.**
- Todas as figuras cujo centro está a menos de `FadeRadius` (= `OpenRadius × 1,15`) do centro da abertura **encolhem até escala zero e somem por completo**: a figura irritada e as vizinhas. Fora desse raio, nada muda. A distância define apenas **quando** cada figura começa, não **quanto** encolhe.
- Atraso de cada figura: `(distância / OpenRadius) × 0,45 s + rnd × 0,06 s`. Isso cria a cascata do centro para fora.
- **No Sierpinski, o triângulo externo fica parado** e cada sub-triângulo encolhe sozinho em torno do próprio centro, com o atraso calculado pela posição **do sub-triângulo** (não do pai). Sub-triângulos vizinhos de triângulos externos diferentes entram em sequência contínua.
- A membrana de fundo encolhe junto, com escala igual à média das escalas dos seus sub-triângulos (sem transparência). Quando todos somem, a fenda mostra o espaço e o sol.
- O retorno (fase 3) segue a mesma ordem, com as figuras crescendo de zero a 1.
- Figuras menores que ~0,3 px não são desenhadas.

### 2.6 Sucção e ejeção (descompressão)

Modelo decidido pelo dev (out/2026): sucção **gradual pela distância**, como numa descompressão, com uma **rajada** no rompimento. Substitui o modelo do protótipo (velocidade `40 + t × 160` px/s, igual em toda a área e crescendo com o tempo).

- **Por que assim:** o ar que escapa por um furo se espalha em leque. No plano do jogo, a velocidade do ar cai com 1/distância e a força de arrasto (que cresce com o quadrado da velocidade) cai com 1/distância². Longe é fraco; perto é avassalador. No tempo, o fluxo é máximo logo depois do rompimento e então se estabiliza enquanto a fenda está aberta.
- Enquanto a fenda está aberta (fases 1 a 3), ela gera um **campo de sucção** em direção ao centro da abertura, com aceleração `a(r) = SuctionAccelerationAtNoReturn × (NoReturnRadius / r) ^ SuctionFalloffExponent`, limitada a `MaxSuctionAcceleration` e nula além de `SuctionMaxRadius`.
- **Ponto sem retorno:** em `r = NoReturnRadius`, a sucção empata com o empuxo máximo do traje (`SuctionAccelerationAtNoReturn` = `ThrustForce` do `Player`, 450 px/s², por padrão). Fora desse raio, o jogador consegue escapar com contra-empuxo; dentro, não. No dobro do raio a sucção é ¼ do empuxo; na metade, 4×. O ajuste é feito em pixels, que dá para ver na tela.
- **Intensidade no tempo:** o campo é multiplicado pela intensidade da abertura `k` (seção 2.3) e por uma rajada que começa em `GustMultiplier` quando a fenda começa a abrir e cai até 1 com a constante de tempo `GustDecay`. Com o Esmaecer, o pico fica por volta do fim da fase 1, quando a fenda se completa; na fase 3 a força some junto com `k`.
- **O jogador mantém o controle** durante a sucção: a força soma à velocidade dele, sem substituí-la (diferente do `ApplyEjection` atual).
- **Inimigos e orbes de biomassa também são sugados** (decisão do dev, out/2026), com a mesma aceleração multiplicada por `EnemySuctionFactor` e `OrbSuctionFactor`. Ao cruzar a fenda, saem da fase: o inimigo volta ao pool (ou é liberado) **sem soltar biomassa** (proposto); o orbe é perdido. Isso faz da fenda uma ferramenta tática (atrair a horda para perto de uma parede) e um risco (perder a biomassa que está no chão).
- Quando o jogador cruza a borda externa da parede pela fenda, então é ejetado para o espaço. **O que acontece depois (dano, morte, perda de biomassa, resgate) fica fora desta spec.** Emitir um sinal `PlayerEjected` e parar aí. (**Decidido em out/2026**, `start-chamber.md` D3: ser ejetado é igual à morte. Na fase, quem aplica é o `EjectionConsequence`, hoje ouvinte do `LostInSpace` do `StructureTracker`; o `PlayerEjected` pode ser ligado ao mesmo componente, ou o jogador ejetado cai no `LostInSpace` ao ficar fora das estruturas.) O espaço não é navegável: o "vácuo como atalho" continua adiado para depois da demo.
- Partículas verde-água saem da fenda em direção ao espaço durante a abertura.

### 2.7 Iluminação em contraluz (sol ao fundo)

O sol está **atrás** da parede, no centro da esfera de Dyson, visto pelas janelas e pela própria fenda. A luz é toda de contraluz. Ver `prism()` e `draw()`.

- **Faces frontais em silhueta.** O topo de cada peça é dividido em facetas triangulares que partem do centro, como uma pirâmide baixa (altura = 0,55 × raio médio da peça). Cada faceta recebe luz: `k = 0,34 + 0,28 × I`, com `I` = luz de preenchimento vinda de baixo/frente (vetor (0, 0,55, 0,83) na convenção do protótipo) mais um "wrap" âmbar de até 45% nas facetas inclinadas para o sol. Resultado: faces escuras, com volume legível.
- **Extrusão em direção ao sol.** Cada peça tem laterais extrudadas na direção do vetor peça→sol (comprimento 6 px × fator do nível). Só as laterais cuja normal aponta para o sol são desenhadas, e elas recebem luz rasante âmbar (mistura com `SunColor` de 25% a 85% conforme o alinhamento e a proximidade do sol).
- **Contorno de luz (rim light).** Toda aresta recebe um traço aditivo âmbar com intensidade `0,8 × alinhamento_com_o_sol × proximidade + 0,06`. A proximidade é `clamp(1 − distância_ao_sol / (0,8 × largura_da_tela), 0,25, 1)`. As peças perto do sol ganham uma auréola mais forte.
- **Vazamento pelas frestas.** Atrás da parede fica um degradê radial centrado no sol (`#FFE9C4` → `#F6A86A` → `#7A3E44`). Ele aparece nos vãos entre peças e nos buracos do Sierpinski. A membrana de fundo usa um tom quente (`#964A3E`), como tecido iluminado por trás.
- **Feixe da fenda.** Durante a abertura, um trapézio aditivo de luz do sol sai da fenda e se abre em perspectiva a partir do sol, invadindo a câmara, mais um clarão radial no ponto de abertura. A largura acompanha a intensidade da abertura.
- **Jogador e inimigos também em contraluz:** corpo escuro com um arco de luz no lado voltado para o sol. (Aplicar ao jogador só se for trivial; senão, registrar para a spec de arte.) O mesmo modelo de luz vale para o inimigo 3D (`fractal-enemy-render-3d.md`, seção 10): sol atrás = contorno; lanterna = luz frontal.
- Cores base das peças: `#5C3A4C` e `#6C4658` (alternando por paridade), com variação de até 15% por sub-triângulo usando `rnd`.

### 2.8 Atmosfera (opcional nesta spec)

Do protótipo, para referência e implementação futura: degradê do espaço, halo do sol, raios radiais suaves, anéis distantes da esfera, poeira flutuante iluminada pela proximidade do sol, névoa na base e vinheta. Podem ser feitos com `GradientTexture2D`, `GPUParticles2D` e sprites aditivos. Não bloqueiam o "pronto quando".

## 3. Fora do escopo

- Consequência de ser ejetado (dano, morte, resgate). Só emitir o sinal. (Decidida fora desta spec: igual à morte, `start-chamber.md` D3.)
- Escolha final de quais modos de abertura entram no jogo e onde. Esta spec implementa todos como opções selecionáveis por `[Export]`; a decisão de design vem depois.
- Estrutura da fase hexagonal e posicionamento das paredes de fronteira em cada estrutura (spec própria; backlog 1.3). Geração procedural fica para depois.
- Curvatura do anel (a parede é reta na lógica; a curvatura é visual e será tratada na spec do cenário).
- Fundo da câmara (placas hexagonais e janelas).
- Áudio.
- Inimigos fractais 3D (`fractal-enemy-render-3d.md`).

## 4. Parâmetros ajustáveis (pontos de partida)

| Parâmetro | Valor no protótipo | Observação |
|---|---|---|
| `Shape` | Sierpinski | Enum: Sierpinski, Hexagon, Octagon |
| `SierpinskiLevel` | 2 | 0 a 3. Nível 3 = 27 sub-peças por triângulo |
| `WallThickness` | 44 px | Altura da faixa |
| `OpenMode` | Esmaecer | Enum com os 17 modos |
| `OpenRadius` | 64 px | Raio do núcleo da abertura |
| `FadeRadiusFactor` | 1,15 | Esmaecer: raio = `OpenRadius × fator` |
| `OpenPhaseDurations` | (0,35; 0,6; 1,4; 0,75) | Esmaecer usa (0,35; 1,1; 1,2; 1,3) |
| `IrritationThreshold` | 1,0 | Dispara a abertura |
| `IrritationDecayPerSecond` | 0,25 | Decaimento fora do feixe |
| `TremorStartIrritation` | 0,5 | Começa a tremer |
| `NoReturnRadius` | 120 px (proposto) | Distância em que a sucção empata com o empuxo máximo do traje (seção 2.6). Substitui o `SuctionStrength` do protótipo (40 + 160·t px/s) |
| `SuctionAccelerationAtNoReturn` | 450 px/s² | Igual ao `ThrustForce` do `Player`. Se o empuxo subir (ex.: melhoria futura), o ponto sem retorno real encolhe: `r = NoReturnRadius × √(450 / ThrustForce)` |
| `SuctionFalloffExponent` | 2 | Queda com a distância (arrasto ~ 1/r² no plano) |
| `MaxSuctionAcceleration` | 3000 px/s² (proposto) | Teto perto da fenda, para a fórmula não ir ao infinito |
| `SuctionMaxRadius` | 360 px (proposto) | Além disso, força zero (3 × `NoReturnRadius`; legibilidade e custo) |
| `GustMultiplier` | 3 (proposto) | Rajada no rompimento: multiplicador inicial da força |
| `GustDecay` | 0,6 s (proposto) | Constante de tempo da queda da rajada até 1× |
| `EnemySuctionFactor` | 1 (proposto) | Quanto os inimigos sentem a sucção (testar fragmentos mais leves, ex.: maior nas gerações menores) |
| `OrbSuctionFactor` | 1 (proposto) | Quanto os orbes de biomassa sentem a sucção |
| `ExtrudeLength` | 6 px | Multiplicado por 1 / 0,7 / 0,5 / 0,35 nos níveis 0–3 |
| `FacetHeight` | 0,55 | Fração do raio da peça |
| `SunPosition` | centro horizontal, acima da parede | Global da cena; ver seção 10 |
| `SunColor` | #FFC482 | Luz rasante e rim |
| `IrritationColor` | #6EF0D7 | Verde-água das juntas vivas |
| `MembraneColor` | #964A3E | Membrana de fundo |
| `PieceColorA/B` | #5C3A4C / #6C4658 | Cores base |

## 5. Casos de borda

- **Feixe ilumina a parede durante uma abertura:** a irritação não acumula na área da abertura em andamento.
- **Duas áreas atingem 1 ao mesmo tempo em segmentos distantes:** cada segmento abre de forma independente. No mesmo segmento, a segunda espera a primeira terminar.
- **Jogador além de `SuctionMaxRadius` quando a fenda abre:** não é afetado. Dentro, sente a força conforme a distância.
- **Jogador escapa da sucção antes de cruzar a parede:** nada acontece, a fenda fecha normalmente.
- **Inimigos e orbes perto da fenda:** são sugados (seção 2.6). Ao cruzar a fenda, saem da fase sem erro nem nó órfão: o inimigo volta ao pool pelo mesmo caminho da morte (`OnDied`) ou é liberado; o orbe sai do `BiomassField`.
- **Inimigo travado (descansando) perto da fenda:** a sucção muda a direção desejada e o acorda (`WakeAngleDegrees`, spec de movimento). Como os inimigos não têm inércia (a velocidade é recalculada a cada passo), a sucção acumula uma velocidade própria no inimigo, que decai fora do campo, somada à perseguição e à separação.
- **Ímã de biomassa × sucção:** as duas forças se somam no orbe; perto da fenda, a sucção vence.
- **Várias fendas abertas ao mesmo tempo:** as acelerações se somam.
- **Desempenho:** o campo entra nos laços que já existem (`FractalSwarm` e `BiomassField`): um cálculo de distância por elemento, por fenda ativa, só dentro de `SuctionMaxRadius`.
- **Colisão durante modos que deformam vértices** (`esticar`, `gelatina`, `murchar`): não recalcular colisão por quadro. A parede tem colisão fixa por segmento; na área da fenda, a colisão é desligada da fase 1 até o meio da fase 3 e religada depois. A deformação é puramente visual.
- **Jogador dentro da área da fenda quando ela fecha:** empurrar para dentro da câmara antes de religar a colisão, para não prender o jogador na parede.
- **Troca de sala no meio de uma abertura:** a parede é liberada sem erros nem tweens órfãos.

## 6. Arquivos e cenas

- Existentes, somente leitura: `LightDamageArea.cs` (como o feixe detecta alvos e o `WallIrritationPerSecond`), `player.tscn`.
- Existentes a alterar pela sucção (seção 2.6; detalhar no plano): `Player.cs` (receber uma aceleração externa contínua, sem perder o controle), `FractalEnemy.cs` / `FractalSwarm.cs` (velocidade de sucção dos inimigos e saída pela fenda) e `BiomassField.cs` (sucção e perda dos orbes). Sugestão de arquitetura: um registro estático das fendas abertas (como o `FractalSwarm`), que responde "qual a aceleração de sucção neste ponto?"; jogador, inimigos e orbes perguntam a ele, e a parede não precisa conhecer nenhum deles (desacoplamento, mesmo espírito do Observer).
- Existente a alterar (mínimo): `LightDamageArea.cs`, apenas para detectar a parede e chamar a irritação nela, se ainda não fizer isso. **Antes de mexer, investigar o que hoje usa `WallIrritationPerSecond` e me apresentar.**
  - Levantado (out/2026): a `LightDamageArea` guarda os corpos do grupo `Walls` que entram no feixe e, a cada passo de física, chama neles `AddIrritation(WallIrritationPerSecond × delta, posição do jogador)` por `HasMethod`/`Call`. Hoje só o `WallSensitive.cs` tem esse método. A `DysonWall` pode reaproveitar o contrato (corpo no grupo `Walls` com `AddIrritation`), mas a área entrega o **corpo inteiro**, não as peças: decidir quais peças estão dentro do feixe fica com a parede.
- Existente que convive até a substituição: `WallSensitive.cs` (spec `reactive-walls.md`) continua nas salas atuais. Quando a `DysonWall` existir, ela entra nas paredes de fronteira; as paredes internas entre estruturas ficam rígidas (fora do grupo `Walls`).
- Novos a criar (sugestão de nomes, ajustar ao padrão do projeto):
  - `DysonWall.cs` + `dyson_wall.tscn`: geração, estado, irritação, ciclo de abertura, colisão, sucção.
  - `WallOpenModes.cs` (ou equivalente): as funções de transformação por modo, puras, recebendo o estado e devolvendo a transformação de cada peça/sub-peça.
  - Desenho: ver seção 10 (opção de renderização).
  - Cena de teste isolada `wall_test.tscn`: um trecho de parede, um jogador, uma luz no lugar do sol.
- Referência: `docs/reference/dyson-wall-prototype.html`.

## 7. Passos no editor (feitos pelo dev)

- Posicionar o nó do sol (ou o ponto de referência do sol) na cena de teste.
- Ajustar `OpenRadius`, `WallThickness` e `ExtrudeLength` olhando a cena de teste lado a lado com o protótipo no navegador.
- Conferir camadas de colisão: o feixe da lanterna precisa detectar a parede.

## 8. Pronto quando

### Fase A: geometria e irritação (etapa 1)
- [ ] `dotnet build` sem erros
- [ ] A parede de Sierpinski aparece nos níveis 0 a 3, com a membrana de fundo
- [ ] O feixe da lanterna irrita as peças; elas brilham em verde-água e tremem acima de 0,5
- [ ] A irritação decai fora do feixe

### Fase B: abertura, colisão e sucção (etapa 1)
- [ ] Ao chegar a 1, a abertura no modo `Esmaecer` executa as 4 fases com a cascata da seção 2.5, sem transparência
- [ ] A colisão é desligada e religada na área da fenda conforme a seção 5
- [ ] A sucção cresce com a proximidade: quase nada perto de `SuctionMaxRadius`, empata com o empuxo em `NoReturnRadius` e vence dentro dele; a rajada do rompimento é perceptível
- [ ] O jogador mantém o controle durante a sucção e consegue escapar de fora do raio sem retorno; ao cruzar a fenda, o sinal `PlayerEjected` é emitido
- [ ] Inimigos e orbes perto da fenda são sugados e, ao cruzá-la, saem da fase sem erros nem nós órfãos
- [ ] Nenhum erro no Output ao abrir 10 fendas seguidas e trocar de sala

### Fase C: todos os modos (etapa 2)
- [ ] Os 17 modos funcionam e podem ser trocados por `[Export]`, comparáveis visualmente ao protótipo
- [ ] Formas hexágono e octógono funcionam

### Fase D: contraluz (etapa 2)
- [ ] Facetas, extrusão em direção ao sol, rim light e vazamento pelas frestas conforme 2.7
- [ ] Feixe de luz na fenda durante a abertura
- [ ] Mover o sol na cena de teste muda a direção da extrusão e do contorno

### Desempenho
- [ ] Na cena de teste, uma tela cheia de parede no nível 2, com uma abertura em andamento, não aumenta o tempo de Renderização (CPU) de forma relevante em relação à linha de base de 1,55 ms da `sala_ninho`. Medir com o mesmo protocolo da seção 11 da `fractal-enemy.md` e registrar os números aqui
- [ ] A física não é afetada pela deformação visual (colisão fixa)

## 9. Como testar (roteiro manual no editor)

1. Abra o protótipo HTML no navegador e deixe ao lado do editor.
2. Rode `wall_test.tscn` com F6. Compare a parede no nível 2 com o protótipo.
3. Aponte a lanterna para um trecho da parede: confirme brilho, tremor e, ao chegar no limite, a abertura em Esmaecer.
4. Fique a distâncias diferentes da fenda (longe, perto do raio sem retorno, dentro dele): confirme que a força cresce com a proximidade, que de fora do raio dá para escapar com contra-empuxo e que, ao cruzar, o sinal `PlayerEjected` aparece no Output.
5. Fique além de `SuctionMaxRadius`: confirme que nada acontece com o jogador.
6. Com inimigos e orbes perto da parede (ex.: na `arena.tscn` ou numa cena de teste com inimigos colocados à mão), abra a fenda: confirme que eles são sugados e somem ao cruzá-la, sem erros e com "Nós Órfãos" = 0.
7. (Fase C) Troque `OpenMode` no Inspector e compare cada modo com o botão equivalente no protótipo.
8. (Fase D) Mova o sol e confirme que extrusão e contorno acompanham.

## 10. Decisões e notas

### Decididas
- **Modelo-alvo (out/2026):** esta membrana é a parede de fronteira com o espaço e substitui ali o puxão da `reactive-walls.md`. Paredes internas entre estruturas são rígidas, fora do grupo `Walls`. Até a implementação, o `WallSensitive.cs` continua valendo nas salas atuais.
- **Sem blecaute total (rascunho de game design, out/2026):** a luz da cena é a contraluz do sol; a lanterna é a luz frontal.
- **Sucção gradual pela distância, com rajada no rompimento e sem tirar o controle (dev, out/2026; seção 2.6).** Escolhida no lugar do empuxo repentino do `WallSensitive` (que não depende da distância, tira o controle e não combina com uma fenda que leva ~1,5 s para abrir) e no lugar da fórmula do protótipo (que cresce com o tempo e é igual em toda a área). Motivos: simula a descompressão (arrasto ~ 1/r², pico no rompimento) e dá contra-jogo com o empuxo do traje.
- **A sucção afeta inimigos e orbes** (dev, out/2026).
- Forma padrão: triângulos de Sierpinski. Hexágono e octógono ficam como opções de teste.
- Modo padrão da demo: **Esmaecer, provisório** (out/2026: a forma final da deformação voltou a ficar em aberto). Regras do modo: só redução de escala, desaparecimento total na área, sub-triângulos independentes em cascata (seção 2.5).
- Luz em contraluz vinda do sol ao fundo da esfera (seção 2.7).
- A deformação é visual; a colisão é fixa por segmento e só é desligada na fenda.
- A irritação vem da lanterna (`WallIrritationPerSecond`).

### Em aberto (perguntar ao dev antes de decidir)
- **Deformação final da parede** (o Esmaecer é só o padrão provisório). Referências sugeridas (out/2026): **exocitose** (a membrana envolve o corpo estranho numa vesícula e o expele: o organismo expulsando o patógeno, que é a premissa do jogo); **estômato** (duas células-guarda abrem um poro, encaixa em pares de triângulos); **esfíncter/íris** (portas biomecânicas de Scorn; já existe o modo `iris`); **peristalse** (onda que leva o jogador até a fenda; já existe o modo `peri`). Game feel da sucção: a descompressão de Heavenly Bodies.
- **O que acontece depois da ejeção** (dano, morte, volta ao último nó, perda de biomassa): decisão em aberto no backlog. O espaço não é navegável.
- **Cor da irritação:** verde-água aqui × vermelho no `WallSensitive.cs` atual. Decidir na spec de arte.
- **Cor do sol:** o rascunho pede uma estrela laranja puxando para o avermelhado (temperatura média/baixa); `SunColor` #FFC482 é laranja claro. Avaliar tons mais vermelhos na spec de arte.
- **Renderização.** Opções:
  - **W1 (recomendada para começar):** um único nó por segmento de parede desenha todas as peças em `_Draw()` (`DrawPolygon` com cor por vértice para as facetas). Simples e próximo do protótipo. Redesenhar só quando houver irritação ou abertura no segmento; em repouso, só a "respiração".
  - **W2:** uma `ArrayMesh` por segmento com os triângulos de todas as facetas, deformada por shader (transformações por vértice com índice de peça e sub-peça em atributos). Mais eficiente, mas usa shader (etapa 2).
  - Medir W1 antes de considerar W2.
- **Iluminação:** calcular a luz por código em C# (como o protótipo, cores por vértice) ou usar `Light2D` + normal maps procedurais. Por código é previsível e não depende das camadas de luz; `Light2D` integra com a lanterna. Sugestão: por código para o sol (fixo), e decidir depois se a lanterna também deve iluminar a parede.
- **Onde fica o sol:** um nó global na cena da câmara (`SunAnchor`) que a parede consulta, ou uma posição fixa por câmara.
- **Inimigo sugado pela fenda solta biomassa?** Proposto: não (sai da fase "perdido" pelo organismo). Se soltar, os orbes também seriam sugados.
- **Os spawners móveis (`mobile-spawners.md`; corpo movido por código, que empurra o jogador) são afetados pela sucção?** Ex.: pesados, resistem; ou imunes.
- **Fendas como arma contra a horda:** consequência da decisão acima. Observar no teste se vira uma estratégia dominante (ex.: irritar a parede de propósito perto da horda); se virar, ajustar `EnemySuctionFactor`.
- **Respiração em repouso:** manter (vida) ou desligar (desempenho)?
- **Integração com a curvatura do anel:** a parede é reta; a curvatura visual vem depois.

### Notas
- Os modos `esticar`, `gelatina` e `murchar` reescrevem vértices; os demais são só transformações (posição, rotação, escala, escala por eixo) e podem ser aplicados por matriz.
- `virar` e `dobrar` usam escala por eixo + escurecimento/clareamento para simular 3D; com o sol atrás, a face revelada é a clara.
