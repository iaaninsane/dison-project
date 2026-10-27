# Spec: Parede de fronteira da esfera de Dyson (membrana fractal que abre para o vácuo)

- **Status:** **fase A concluída e validada pelo dev** (out/2026; decisões na seção 10, A1 a A6; arquivos na seção 6; medição na seção 8). **Fase B: decisões tomadas pelo dev** (out/2026, seção 10, B1 a B6). Fases C e D propostas. **Modelo-alvo das paredes de fronteira com o espaço** (decisão do dev, out/2026): quando implementada, substitui nessas paredes o puxão da `reactive-walls.md`. Próxima: fase B (mecânica); fases C e D são visuais.
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
- **Pontas (fase A):** a faixa começa e termina com peças inteiras cortadas na extremidade da parede (o que passa dela fica achatado, sem área). Assim a subdivisão nas pontas é igual à das outras peças; subdividir um meio triângulo à parte deixava o vão espelhado.

### 2.2 Irritação

- Enquanto o feixe da lanterna sobrepõe um trecho da parede, então cada peça (triângulo externo; os sub-triângulos usam a irritação do pai, como no protótipo) com o centro dentro do feixe acumula irritação de 0 a 1: a cada passo, `peso × WallIrritationPerSecond × dt ÷ MaxIrritation`. O `WallIrritationPerSecond` (20/s, em `LightDamageArea.cs`) é a potência da lanterna; o `MaxIrritation` é a sensibilidade da parede, na mesma unidade do `WallSensitive` (decisão A3).
- **Peso pelo eixo do feixe** (decisão A2): 1 no centro do feixe, caindo até 0 na borda. A fenda abre onde a lanterna mira, e o brilho em degradê mostra o alvo antes. Quem calcula o peso de um ponto é a lanterna, que conhece o formato do feixe; a parede só pergunta.
- Fora do feixe, a irritação decai em `RecoveryRate ÷ MaxIrritation` por segundo. Dentro do feixe ela não decai, diferente do `WallSensitive`, que se acalma mesmo iluminado (decisão A3).
- Quando a irritação passa de 0,5 (`TremorStartIrritation`), então os sub-triângulos da peça tremem, cada um por conta própria, sem sincronia com os vizinhos: deslocamento aleatório por quadro, sorteado para cada sub-triângulo, proporcional a `(irr − 0,5) × 3` px (`TremorStrength`). A membrana de fundo fica parada, então o fundo não aparece entre as peças (decisão A5; no protótipo, a peça inteira treme junto).
- A peça irritada ganha brilho verde-água (`IrritationColor`) proporcional à irritação: mistura de até 55% na cor das peças. O halo aditivo ao redor (protótipo) fica para a etapa 2 (decisão A6).
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
- Todos os modos somam uma "respiração" permanente: `dy += sin(tempo × 1,6 + cx × 0,04) × 0,7 px`. **Desligada na etapa 1** (decisão A4): na W1 ela obrigaria a redesenhar toda parede visível a cada quadro; volta com a W2 (etapa 2), por shader.

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
- Enquanto a fenda está aberta (fases 1 a 3), ela gera um **campo de sucção** em direção ao centro da abertura, com aceleração `a(r) = SuctionAccelerationAtNoReturn × (NoReturnRadius / r) ^ SuctionFalloffExponent`, limitada a `MaxSuctionAcceleration` e nula além de `SuctionMaxRadius`. Do meio da espessura da parede em diante (a "garganta" da fenda), o campo empurra para o espaço, pela normal da parede, com a mesma intensidade: quem passou não é puxado de volta pela fenda (decisão B5). Trocar a direção só depois da borda externa criava um poço no vão: entre o meio e a borda, o campo (no máximo, perto do centro) devolvia à câmara quem já estava passando (verificado no passo 3).
- **Ponto sem retorno:** em `r = NoReturnRadius`, a sucção empata com o empuxo máximo do traje (`SuctionAccelerationAtNoReturn` = `ThrustForce` do `Player`, 450 px/s², por padrão). Fora desse raio, o jogador consegue escapar com contra-empuxo; dentro, não. No dobro do raio a sucção é ¼ do empuxo; na metade, 4×. O ajuste é feito em pixels, que dá para ver na tela.
- **Intensidade no tempo:** o campo é multiplicado pela intensidade da abertura `k` (seção 2.3) e por uma rajada que começa em `GustMultiplier` quando a fenda começa a abrir e cai até 1 com a constante de tempo `GustDecay`. Com o Esmaecer, o pico fica por volta do fim da fase 1, quando a fenda se completa; na fase 3 a força some junto com `k`.
- **O jogador mantém o controle** durante a sucção: a força soma à velocidade dele, sem substituí-la (diferente do `ApplyEjection` atual).
- **Inimigos e orbes de biomassa também são sugados** (decisão do dev, out/2026), com a mesma aceleração multiplicada por `EnemySuctionFactor` e `OrbSuctionFactor`. Ao cruzar a fenda, saem da fase: o inimigo volta ao pool (ou é liberado) **sem soltar biomassa** (decisão B4); o orbe é perdido. Isso faz da fenda uma ferramenta tática (atrair a horda para perto de uma parede) e um risco (perder a biomassa que está no chão).
- **Os spawners móveis também são sugados** (decisão B3), mas resistem mais: a aceleração é multiplicada por `SpawnerSuctionFactor`, menor que 1.
  - Deslocado pela sucção, o spawner volta à rota quando sai do campo.
  - Ao cruzar a fenda, sai da fase e conta como destruído para o objetivo (`Destroyed`, e `AllSpawnersDestroyed` se for o último), **sem soltar os orbes**.
- Quando o jogador cruza a borda externa da parede pela fenda, então é ejetado para o espaço. **O que acontece depois (dano, morte, perda de biomassa, resgate) fica fora desta spec.** Emitir um sinal `PlayerEjected` e parar aí. (**Decidido em out/2026**, `start-chamber.md` D3: ser ejetado é igual à morte.)
  - **Como a consequência chega (decisão B1):** a parede emite `PlayerEjected`, para som e câmera no futuro. Quem encerra a incursão é o `LostInSpace` que já existe: fora das estruturas por `SpaceReturnDelay` (~2 s), o `EjectionConsequence` rompe o traje.
  - Se o jogador voltar pela fenda antes disso, sobrevive.
  - O espaço não é navegável: o "vácuo como atalho" continua adiado para depois da demo.
- Partículas verde-água saem da fenda em direção ao espaço durante a abertura. **Ficam para a etapa 2** (decisão B6), como o halo (A6).

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
| `MaxIrritation` | 1,0 na escala 0–1; **60 no jogo** (decisão A3) | Luz acumulada para uma peça chegar a 1 e disparar a abertura: ~3 s no centro do feixe com a lanterna atual (20/s). Na fase, por tipo de estrutura (fase B) |
| `RecoveryRate` | 0,25/s na escala 0–1; **15/s no jogo** (decisão A3) | Decaimento fora do feixe, na unidade do `MaxIrritation` (15 ÷ 60 = 0,25/s): ~4 s do máximo à calma |
| `TremorStartIrritation` | 0,5 | Começa a tremer |
| `TremorStrength` | 3 | Tremor de cada sub-triângulo: até ±(irr − 0,5) × 3 ÷ 2 px em cada eixo, sorteado a cada quadro (decisão A5) |
| `NoReturnRadius` | 120 px (proposto) | Distância em que a sucção empata com o empuxo máximo do traje (seção 2.6). Substitui o `SuctionStrength` do protótipo (40 + 160·t px/s) |
| `SuctionAccelerationAtNoReturn` | 450 px/s² | Igual ao `ThrustForce` do `Player`. Se o empuxo subir (ex.: melhoria futura), o ponto sem retorno real encolhe: `r = NoReturnRadius × √(450 / ThrustForce)` |
| `SuctionFalloffExponent` | 2 | Queda com a distância (arrasto ~ 1/r² no plano) |
| `MaxSuctionAcceleration` | 3000 px/s² (proposto) | Teto perto da fenda, para a fórmula não ir ao infinito |
| `SuctionMaxRadius` | 360 px (proposto) | Além disso, força zero (3 × `NoReturnRadius`; legibilidade e custo) |
| `GustMultiplier` | 3 (proposto) | Rajada no rompimento: multiplicador inicial da força |
| `GustDecay` | 0,6 s (proposto) | Constante de tempo da queda da rajada até 1× |
| `EnemySuctionFactor` | 1 (proposto) | Quanto os inimigos sentem a sucção (testar fragmentos mais leves, ex.: maior nas gerações menores) |
| `OrbSuctionFactor` | 1 (proposto) | Quanto os orbes de biomassa sentem a sucção |
| `SpawnerSuctionFactor` | 0,5 (proposto) | Quanto os spawners móveis sentem a sucção: menos que o jogador e os inimigos (B3) |
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
- **Spawner móvel perto da fenda** (B3): sente a sucção × `SpawnerSuctionFactor` e sai da rota. Um corpo movido por código não para nas paredes sozinho, então o deslocamento também é testado contra elas (como o empurrão do jogador, `TestMove`). Fora do campo, volta à rota. Ao cruzar, emite `Destroyed` sem soltar orbes.
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
  - Resolvido no passo 2 da fase A (out/2026): quem implementa o contrato `ILightIrritable` recebe `AddLightIrritation(quantidade, lanterna)` e pergunta à lanterna o peso da luz em cada ponto (`BeamWeightAt`, decisão A2). Os corpos sem o contrato (o `WallSensitive`) seguem pelo caminho antigo (`AddIrritation` por `HasMethod`/`Call`).
- Existente que convive até a substituição: `WallSensitive.cs` (spec `reactive-walls.md`) continua nas salas atuais. Quando a `DysonWall` existir, ela entra nas paredes de fronteira; as paredes internas entre estruturas ficam rígidas (fora do grupo `Walls`).
- **Criados na fase A (out/2026):**
  - `DysonWall.cs` (`StaticBody2D`, implementa `ILightIrritable`): parâmetros (`[Export]`) e irritação de cada peça (0 a 1), com subida e decaimento (2.2). Dorme calma e a luz a acorda. No `_Ready`, cria por código a colisão fixa (um retângulo do tamanho da parede) e o nó de desenho. Não entra sozinha no grupo `Walls`: quem a monta decide.
  - `DysonWallGeometry.cs` (classe C# comum, sem nó): peças e sub-triângulos de Sierpinski em arrays, com as pontas cortadas (2.1).
  - `IDysonWallView.cs`: o contrato de quem desenha (`Build` e `Refresh`) e o estilo entregue a ele (`DysonWallStyle`: cores e tremor). É a Strategy da A1: a W2 cumpre o mesmo contrato.
  - `DysonWallView.cs`: a W1. Uma lista de triângulos por parede (`RenderingServer.CanvasItemAddTriangleArray`), refeita só enquanto a parede está acordada. As membranas, paradas (A5), entram na lista uma vez só.
  - `ILightIrritable.cs`: o contrato de quem quer a luz peça a peça.
  - Alterado: `LightDamageArea.cs` (`BeamWeightAt`, peso pelo eixo do feixe, A2).
  - `wall_test.tscn` (montada pelo dev): 5 paredes (níveis 0 a 3 e uma girada) no grupo `Walls`, e o jogador.
- A criar nas fases seguintes (sugestão de nomes, ajustar ao padrão do projeto):
  - Na `DysonWall.cs`: ciclo de abertura e sucção (fase B). A `dyson_wall.tscn` sugerida antes não foi precisa na fase A, porque a parede é um script num `StaticBody2D` e cria a colisão e o desenho por código. Decidir no plano da fase B se o construtor da fase cria a parede por código (como já faz com as paredes `WallSensitive`) ou instancia uma cena.
  - `WallOpenModes.cs` (ou equivalente): as funções de transformação por modo, puras, recebendo o estado e devolvendo a transformação de cada peça/sub-peça.
  - Na `wall_test.tscn`: uma luz no lugar do sol (fase D).
- Referência: `docs/reference/dyson-wall-prototype.html`.

## 7. Passos no editor (feitos pelo dev)

- Posicionar o nó do sol (ou o ponto de referência do sol) na cena de teste.
- Ajustar `OpenRadius`, `WallThickness` e `ExtrudeLength` olhando a cena de teste lado a lado com o protótipo no navegador.
- Conferir camadas de colisão: o feixe da lanterna precisa detectar a parede.

## 8. Pronto quando

### Fase A: geometria e irritação (etapa 1)

Concluída e validada pelo dev na `wall_test.tscn` (out/2026), nos testes dos passos 1 a 3 e da decisão A5.

- [x] `dotnet build` sem erros
- [x] A parede de Sierpinski aparece nos níveis 0 a 3, com a membrana de fundo
- [x] O feixe da lanterna irrita as peças; elas brilham em verde-água e, acima de 0,5, os sub-triângulos tremem cada um por conta própria, com a membrana parada (A5)
- [x] A irritação decai fora do feixe

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
- [ ] Na cena de teste, uma tela cheia de parede no nível 2, com uma abertura em andamento, não aumenta o tempo de Renderização (CPU) de forma relevante em relação à linha de base de 1,55 ms da `sala_ninho`. Medir com o mesmo protocolo da seção 11 da `fractal-enemy.md` e registrar os números aqui. **Fase A: ok, sem abertura** (medição abaixo); falta medir com uma abertura em andamento (fase B).
- [ ] A física não é afetada pela deformação visual (colisão fixa). Fase A: ok (o tremor é só visual e a colisão é um retângulo fixo); conferir com a abertura (fase B).

**Medição da fase A (out/2026):**
- **Cena:** 27 paredes de 1920 px empilhadas a cada 40 px (a tela inteira, zoom 1). Medida numa cópia do código num projeto descartável, na mesma máquina da linha de base (RTX 3060 Ti), com V-Sync desligado, 2 s de aquecimento e 4 s de medição por cenário. Rodada duas vezes, com os mesmos números.
- **Como ler:** Processo e Física são o pior tempo de cada segundo (é o que os Monitores da Godot guardam); a coluna mostra a média e o maior desses valores. Renderização (CPU) é por quadro: o tempo medido do viewport mais a preparação do quadro.

| Cenário | FPS | Processo | Física | Renderização (CPU) | Chamadas de desenho | Paredes acordadas |
|---|---|---|---|---|---|---|
| Vazio (só o fundo) | 2405 | 1,24 / 2,31 ms | 0,15 / 0,29 ms | 0,03 / 0,25 ms | 1 | 0 |
| Nível 2, calmas | 2145 | 1,01 / 1,11 ms | 0,05 / 0,07 ms | 0,05 / 0,15 ms | 28 | 0 |
| Nível 2, lanterna varrendo | 2074 | 1,06 / 1,17 ms | 1,48 / 1,58 ms | 0,04 / 0,37 ms | 28 | 5,1 |
| Nível 2, todas acordadas (pior caso) | 1737 | 1,09 / 1,13 ms | 4,25 / 5,57 ms | 0,04 / 0,30 ms | 28 | 27 |
| Nível 3, calmas | 2147 | 1,04 / 1,18 ms | 0,04 / 0,06 ms | 0,04 / 0,19 ms | 28 | 0 |
| Nível 3, todas acordadas (pior caso) | 680 | 1,10 / 1,33 ms | 11,30 / 12,08 ms | 0,06 / 0,42 ms | 28 | 27 |

- **Renderização (CPU):** de 0,03 ms (vazio) para 0,04 a 0,06 ms com a tela cheia de paredes (pior 0,42 ms), muito abaixo da linha de base de 1,55 ms. É uma chamada de desenho por parede; a placa de vídeo desenha 22 mil triângulos no nível 2 e 63 mil no nível 3.
- **Parede calma:** custo zero na CPU (o sono da parede e o desenho guardado pela Godot).
- **Parede acordada:** o custo vai para a Física, não para o Processo. O redesenho pedido no `_PhysicsProcess` roda no fim do passo de física, por isso o Processo não muda e a Física cresce com as paredes acordadas; ele acontece 60 vezes por segundo, seja qual for o FPS.
  - Nível 2: ~0,16 ms por parede acordada. Com a lanterna varrendo (~5 acordadas, contando as que se acalmam), ~1,5 ms; com todas as 27, ~4,3 ms.
  - Nível 3: ~0,42 ms por parede; com todas as 27, ~11 ms, perto da Física da `sala_ninho` com 300 inimigos (11,6 ms) num orçamento de 16,67 ms por passo.
- **Conclusões:** no nível 2, a W1 dá conta, e a medição não pede a W2 (A1). O nível 3 fica só para testes. Na fase B, medir a fase hexagonal com a horda e algumas paredes acordadas ao mesmo tempo, e com uma abertura em andamento.

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
- **Fase A, decisões do dev (out/2026):**
  - **A1, renderização: W1 por enquanto, preparada para a W2.** Desenho por código (`_Draw`), para testar e validar ideias; a W2 (malha + shader) entra quando for a hora (etapa 2, ou antes se a medição de desempenho pedir). "Preparada" quer dizer: geometria (classe pura), estado da parede e desenho separados; o desenho num nó próprio, atrás de um contrato pequeno (Strategy), para trocar quem desenha sem mexer na parede; dados por peça em arrays, o formato que a W2 vai mandar à placa de vídeo. Nada da W2 é escrito agora. Comparativo: a W1 não custa nada com a parede calma (a Godot repete o desenho guardado) e só recalcula, na CPU, as paredes acordadas; a W2 ganha quando muitas paredes se mexem ao mesmo tempo, mas é shader (etapa 2), mais difícil de depurar, e cada modo teria de ser reescrito na linguagem de shader.
  - **A2, cálculo da luz na irritação: peso pelo eixo do feixe** (seção 2.2). Alternativas descartadas: uniforme no feixe (abertura imprevisível com o feixe varrendo) e eixo + distância (o feixe é curto, ~220 px; mais um valor para balancear).
  - **A3, ritmo: regra da spec** (sobe só no feixe, decai só fora), com `MaxIrritation` 60 e `RecoveryRate` 15/s. A parede "lembra": varrer várias vezes acumula, e o brilho fica alguns segundos depois de tirar a luz, como aviso. Descartada: a regra do `WallSensitive` (decai mesmo iluminada), que com o peso pelo eixo só deixaria acumular a faixa central do feixe (peso acima de 0,75). Para manter na fase os tempos de hoje (5 s, 2 s e 1,5 s por tipo de estrutura), os valores por tipo seriam 100, 40 e 30.
  - **A4, respiração em repouso: desligada na etapa 1**; volta com a W2, por shader.
  - **A5, tremor: cada sub-triângulo por conta própria, com a membrana parada** (dev, out/2026, no teste do passo 3; seção 2.2). No protótipo, a peça inteira (membrana e sub-triângulos) treme junto. No teste, isso abria fendas escuras e retas entre as peças e deixava a parede com cara de blocos encaixados, pouco orgânica. Comparados no editor:
    - peça inteira (protótipo);
    - membrana e cada sub-triângulo independentes: ainda abria fendas menores entre as peças, porque as membranas vizinhas se afastam;
    - sub-triângulos independentes com a membrana parada: **escolhido**.

    As membranas se encaixam sem vãos, então nas frestas aparece o tecido quente da membrana, nunca o fundo. É a mesma lógica do Esmaecer (2.5): o triângulo externo fica parado e cada sub-triângulo age sozinho.
    - **Custo:** um sorteio por sub-triângulo em vez de um por peça, só nas paredes acordadas. Numa parede de 1920 px no nível 2, com todas as peças tremendo, são 0,10 ms por desenho, contra 0,07 ms antes. A placa de vídeo desenha os mesmos triângulos.
    - **Nível 0:** não tem membrana (não há vãos), então a peça é o próprio sub-triângulo e o fundo ainda aparece entre as peças ao tremer.
    - **Consequência para a fase D (2.7):** durante o tremor, o vazamento de luz entre as peças fica por conta do tom quente da membrana. O degradê do sol aparece onde a membrana some (a fenda).
    - **Proposta para a fase B:** o tremor forte do Aviso (2.3) segue o mesmo modo.
  - **A6, halo da irritação: fica para a etapa 2** (dev, out/2026, passo 3). Na fase A, só o brilho nas peças (mistura de até 55%). Testado: com a mistura normal da Godot, o halo verde-água translúcido sobre o fundo escuro vira uma névoa acinzentada. O aditivo (como o `lighter` do protótipo) fica certo, mas precisa de um nó próprio com `CanvasItemMaterial` em modo Add; é acabamento visual e combina com a W2.
- **Fase B, decisões do dev (out/2026):**
  - **B1, ejeção: aviso + `LostInSpace`** (seção 2.6). A parede emite `PlayerEjected` (para som e câmera no futuro), e quem encerra a incursão é o `LostInSpace`, pelo caminho já testado: ~2 s fora das estruturas. Voltar pela fenda antes disso salva o jogador: risco com contra-jogo. Descartada: `PlayerEjected` ligado direto ao `EjectionConsequence` (morte ao cruzar, sem chance de voltar).
  - **B2, fronteiras: todas as paredes externas da fase viram `DysonWall`**, com:
    - uma parede por aresta reta, e não trechos de 200 px, para a cascata e a regra "uma abertura por parede" funcionarem;
    - a sensibilidade por tipo de estrutura em 100, 40 e 30 (nós e painéis; conectores de nós; conectores de placa), que mantém os 5 s, 2 s e 1,5 s de hoje no centro do feixe (A3);
    - as paredes do nó de entrada inertes até liberar as saídas, como hoje.

    Descartada por ora: trechos de membrana entre costelas rígidas (pede o "recheio" das estruturas, que ainda não existe).
  - **B3, spawners móveis: sugados como os inimigos, mas mais resistentes** (`SpawnerSuctionFactor` menor que 1; 0,5 proposto). Fora do campo, voltam à rota. Ao cruzar a fenda, contam como destruídos para o objetivo, sem soltar os orbes: a fenda vira um jeito arriscado de cumprir o objetivo, sem a recompensa da luz. Descartadas: imunes; pesados (sentem pouco e voltam); reaparecer em outra rota.
  - **B4, inimigo sugado não solta biomassa:** sai da fase "perdido" pelo organismo e volta ao estoque. Descartada: soltar o orbe ao cruzar (ele nasceria dentro do campo e seria sugado também).
  - **B5, campo do lado de fora: empurra para o espaço, pela normal da parede.** Dentro da câmara, o campo puxa para o centro da fenda; do meio da espessura em diante (a garganta), empurra para fora, como o ar que escapa, e quem passou não volta puxado (sem efeito elástico). Esclarece a seção 2.6, que só falava do centro da abertura. Descartada: sempre para o centro (quem passou oscilaria no buraco).
  - **B6, partículas da fenda: etapa 2**, como o halo (A6). A fase B testa se a sucção é legível só com a fenda abrindo e o movimento; se não for, entra uma pista visual simples depois.

### Em aberto (perguntar ao dev antes de decidir)
- **Deformação final da parede** (o Esmaecer é só o padrão provisório). Referências sugeridas (out/2026): **exocitose** (a membrana envolve o corpo estranho numa vesícula e o expele: o organismo expulsando o patógeno, que é a premissa do jogo); **estômato** (duas células-guarda abrem um poro, encaixa em pares de triângulos); **esfíncter/íris** (portas biomecânicas de Scorn; já existe o modo `iris`); **peristalse** (onda que leva o jogador até a fenda; já existe o modo `peri`). Game feel da sucção: a descompressão de Heavenly Bodies.
- **Fendas como arma contra a horda e contra os spawners:** consequência das decisões B3 e B4. Observar no teste se vira uma estratégia dominante (ex.: irritar a parede de propósito perto da horda ou de um spawner); se virar, ajustar `EnemySuctionFactor` e `SpawnerSuctionFactor`.
- **Iluminação** (fase D, etapa 2): calcular a luz por código em C# (como o protótipo, cores por vértice) ou usar `Light2D` + normal maps procedurais. Por código é previsível e não depende das camadas de luz; `Light2D` integra com a lanterna. Recomendação: por código para o sol (fixo), e decidir depois se a lanterna também deve iluminar a parede. Com a W2, a mesma conta vai para o shader.
- **Onde fica o sol** (fase D, etapa 2). Na fase, a estrela fica atrás do plano do jogo (`hex-phase.md`, H1), então a posição 2D do sol é uma projeção. Recomendação: um nó `SunAnchor` (`Marker2D`) que a parede consulta; na fase, o construtor o põe no centro do hexágono (onde a estrela atrás do plano se projeta); na `wall_test.tscn`, o dev o move à mão. Alternativas: direção fixa (sol no infinito, luz sempre do mesmo lado da tela) ou seguir a câmera.
- **Cor da irritação:** verde-água aqui × vermelho no `WallSensitive.cs` atual. Decidir na spec de arte; na fase A, `IrritationColor` é um `[Export]` com o verde-água como padrão.
- **Cor do sol:** o rascunho pede uma estrela laranja puxando para o avermelhado (temperatura média/baixa); `SunColor` #FFC482 é laranja claro. Avaliar tons mais vermelhos na spec de arte.
- **Integração com a curvatura do anel:** a parede é reta; a curvatura visual vem depois.

### Ideias para depois (do dev)
- **Voltar à estrutura depois de ejetado** (out/2026, para implementação futura).
  - **A ideia:** do lado de fora, o campo da fenda empurra para o espaço (B5): quem está fora e irrita a parede "por fora" é empurrado para longe. O dev quer manter esse empurrão e acrescentar a volta: com os foguetes de direção (empuxo) num nível bem alto, ou com um "superboost" equipado, o astronauta vence o empurrão e volta pela fenda.
  - **Números de hoje:** o empuxo só vence o empurrão a mais de `NoReturnRadius × √(força × 450 ÷ ThrustForce)` do centro da fenda (força = intensidade da abertura × rajada).
    - Com o `ThrustForce` atual (450), isso dá ~140 px no pico da rajada e 120 px com a fenda aberta.
    - Para chegar até a garganta, o empuxo teria de passar do teto vezes a força: ~4100 px/s² no pico, ~1500 px/s² no meio da fase 3, quando o vão fecha.
    - Um empuxo de nível alto encurta a distância, mas alcançar o vão ainda aberto pede algo como um impulso (o "superboost").
  - **Parentes nos documentos:**
    - "Foguetes de mega propulsão" (HANDOFF, adiado para depois da demo, sem descrição) é o mais próximo do superboost;
    - a melhoria de empuxo é candidata na `suit-evolution.md` (decisão 4) e já encolhe o ponto sem retorno de dentro (seção 4, `SuctionAccelerationAtNoReturn`);
    - a B1 já salva quem volta pela fenda antes do `LostInSpace`.
  - **A decidir quando for feita:** com a B1, o `LostInSpace` encerra a incursão ~2 s depois de sair, menos que o tempo de irritar uma parede por fora (1,5 a 5 s de luz, mais ~1,5 s até a fenda completa).
    - Para a volta valer, rever o `SpaceReturnDelay` (ex.: oxigênio do traje) ou permitir a volta só pela fenda que ejetou, enquanto ela está aberta.
    - Até lá, o espaço segue não navegável ("vácuo como atalho", adiado).

### Notas
- Os modos `esticar`, `gelatina` e `murchar` reescrevem vértices; os demais são só transformações (posição, rotação, escala, escala por eixo) e podem ser aplicados por matriz.
- `virar` e `dobrar` usam escala por eixo + escurecimento/clareamento para simular 3D; com o sol atrás, a face revelada é a clara.
