# Spec: Movimento da horda fractal (separação e bloqueio na parede)

- **Status:** implementada e integrada na `main` (valores em teste). Há um problema de desempenho conhecido, adiado; ver seção 11.
- **Etapa:** 1 (mecânica)
- **Depende de:** `docs/specs/fractal-enemy.md` (diagnóstico de desempenho na seção 11; Motion Mode Floating já adotado)

> **Valores em teste:** como na spec do inimigo, os atributos `[Export]` desta spec estão em teste e não têm valores definitivos. Os valores da seção 4 são pontos de partida para ajuste no Inspector.

## 1. Objetivo

A horda do ninho deve poder se aglomerar perto das paredes e do jogador sem derrubar o FPS, mantendo a sensação de "nuvem" de anticorpos. Hoje, com 400 inimigos aglomerados perto de uma parede, o FPS cai abaixo de 60, porque cada inimigo empurra os vizinhos e a parede a cada passo de física. Serve ao trecho COMBATER do core loop (pressão de horda).

## 2. Comportamento esperado

### Separação (direção B)

- Os inimigos não colidem fisicamente entre si (camada própria). Continuam colidindo com paredes, pilares e jogador.
- Quando dois inimigos estão a menos de `SeparationRadius`, então cada um recebe um empurrão para longe do outro. O empurrão é mais forte quanto mais perto estão e cai até zero na borda do raio.
- Quando dois inimigos estão exatamente na mesma posição (ex.: nasceram no mesmo marcador), então são afastados em direções diferentes e determinísticas (desempate pelo ID de cada um).
- A velocidade desejada do inimigo = perseguição (direção do jogador × `Speed`) + separação.
- As posições usadas na separação são lidas uma única vez por passo de física (snapshot), e todos os inimigos calculam sobre essa mesma foto.
- A separação vale para todos os fluxos de criação: pool (`NinhoSpawnerPooled`), inimigos colocados à mão na cena (`arena.tscn`, cena de protótipo fora da demo, usada só como bancada de teste desse fluxo) e `NinhoSpawner` sem pool.

### Bloqueio (direção A): detecção por progresso e descanso

O bloqueio é detectado pelo **efeito** (o inimigo tentou andar e não saiu do lugar), não pela geometria da parede. Isso cobre paredes, quinas, pilares e o jogador com um único critério.

- Quando a velocidade desejada (perseguição + separação) é menor que `RestSpeedThreshold`, então o inimigo não se move nesse passo e **não chama `MoveAndSlide`** (ex.: perseguição e separação se anulam no meio da nuvem).
- Quando o inimigo tenta se mover e a velocidade real depois do `MoveAndSlide` (`GetRealVelocity()`) fica abaixo de `RestSpeedThreshold`, então ele está **travado**: descansa por `BlockedRestTime` segundos sem chamar `MoveAndSlide` (custo de física zero enquanto espera).
- Quando, durante o descanso, a direção desejada gira mais que `WakeAngleDegrees` em relação à direção em que travou (ex.: o jogador se moveu para o lado), então o inimigo acorda na hora e volta a se mover.
- Quando o descanso termina sem a direção mudar, então o inimigo tenta andar de novo; se continuar travado, volta a descansar.
- O deslizamento ao longo das superfícies continua a cargo do `MoveAndSlide`: quem desliza está andando e não conta como travado.

### Interação com jogador e lanterna

- O jogador continua sendo bloqueado pelos inimigos (decidido: opção a).
- A lanterna continua detectando e ferindo os inimigos.

## 3. Fora do escopo

- Dano ao jogador por contato.
- Desvio de obstáculos ou busca de caminho (pathfinding).
- Grade espacial para a separação: só se a medição mostrar que o cálculo simples (todos contra todos) ficou caro.
- Atualização escalonada (direção C) e limite de inimigos por sala (direção D).
- Mudanças no `Player.cs` (inclusive na ejeção).
- Renderização em lote e fragmentos (fase 2 da spec do inimigo).

## 4. Parâmetros ajustáveis (valores em teste)

| Parâmetro | Valor inicial (proposto) | Onde fica | Observação |
|---|---|---|---|
| `SeparationRadius` | 16 px (na cena: 20) | `FractalEnemy.cs` | Raio de colisão efetivo do inimigo é ~6 px |
| `SeparationStrength` | 10 px/s | `FractalEnemy.cs` | Velocidade máxima de afastamento. Precisa ser maior que `Speed` para a nuvem ter volume (ver nota abaixo) |
| `RestSpeedThreshold` | 2 px/s | `FractalEnemy.cs` | Abaixo disso: nada a mover, ou o inimigo conta como travado. Valor absoluto; o efeito depende do `Speed` |
| `BlockedRestTime` | 0,25 s | `FractalEnemy.cs` | Tempo de descanso depois de travar, antes de tentar de novo |
| `WakeAngleDegrees` | 30° | `FractalEnemy.cs` | Giro da direção desejada que acorda o inimigo antes do fim do descanso |

**Nota de ajuste (separação):** para um inimigo atrás de outro que parou, o espaçamento de equilíbrio entre os centros é aproximadamente `SeparationRadius × (1 − Speed / SeparationStrength)`. Com `SeparationStrength` ≤ `Speed`, a nuvem colapsa (inimigos se sobrepõem). Exemplos com `Speed` 10: raio 16 / força 40 ≈ 12 px; raio 24 / força 20 ≈ 12 px; raio 32 / força 20 ≈ 16 px. Referência: a colisão física mantinha os centros a ~12 px.

**Nota de ajuste (bloqueio):** um inimigo que chega quase de frente a uma superfície desliza a `Speed × sen(ângulo)`. Com `Speed` 10 e limiar 2 px/s, quem chega a menos de ~11,5° da perpendicular passa a descansar em vez de "rastejar" pela parede; se os inimigos parecerem grudar nos pilares, baixe `RestSpeedThreshold`.

## 5. Casos de borda

- Vários inimigos nascendo no mesmo marcador (distância zero entre eles): desempate determinístico, sem divisão por zero.
- Inimigo devolvido ao pool: sai do registro e não entra no snapshot.
- Inimigo liberado (`QueueFree`) ou troca de sala: sai do registro ao sair da árvore de cena; o registro nunca guarda referência inválida.
- Inimigo parado na parede quando o jogador se move: volta a se mover. Se o jogador se afastar **na mesma linha**, a direção desejada não gira e o inimigo só reage ao fim do descanso (atraso de até `BlockedRestTime`).
- Quinas e paredes diagonais: cobertas pela detecção por progresso (não depende da normal da parede); o inimigo não pode ficar preso tremendo entre duas superfícies.
- Inimigo alinhado com um pilar e com o jogador (bate de frente): descansa em vez de empurrar. Contornar obstáculos exige navegação, fora do escopo.
- Inimigo reaproveitado do pool: começa acordado (`ActivateAt` zera o descanso).
- No modo Floating toda colisão conta como "parede", inclusive o jogador e os pilares: o inimigo encostado no jogador também para de empurrá-lo.
- Ejeção do jogador: o `Player.cs` desliga só a máscara 1 durante a ejeção; com a camada 2 na máscara do jogador, ele continua colidindo com inimigos enquanto ejetado (hoje não colide).

## 6. Arquivos e cenas

- Existentes: `FractalEnemy.cs`, `fractal_enemy.tscn`, `player.tscn`. Somente leitura: `NinhoSpawnerPooled.cs`, `NinhoSpawner.cs`, `LightDamageArea.cs` (a detecção continua por tipo, só a máscara muda no editor).
- Novos: `FractalSwarm.cs` (classe estática, não é nó): registro de inimigos ativos, snapshot das posições e cálculo da separação.

## 7. Passos no editor (feitos pelo dev, após o passo 3)

1. Em `fractal_enemy.tscn`, nó raiz `FractalEnemy`: Inspector > CollisionObject2D > Collision > **Layer** só com o **2**; **Mask** só com o **1**.
2. Em `player.tscn`, nó `FlashlightAnchor/LightDamageArea`: Collision > **Mask** com **1** e **2**.
3. Em `player.tscn`, nó raiz: Collision > **Mask** com **1** e **2**.
4. Clicar em **Build** no editor (novos `[Export]`).

Opcional (altera o `project.godot`, pedir antes): nomear as camadas em Projeto > Configurações do Projeto > Nomes de Camadas > Física 2D (1 = mundo, 2 = inimigos).

## 8. Pronto quando

- [x] `dotnet build` sem erros
- [ ] Com 400 inimigos aglomerados perto de uma parede, o FPS fica acima de 60 com V-Sync desligado, e o Processo de Física fica abaixo de 16,67 ms (**parcial**: quedas ficaram menos frequentes, mas ainda ocorrem no cenário da seção 11)
- [x] Pares de Colisão bem abaixo do E4 (816 com 300 inimigos): 166–350 com 400 inimigos
- [x] A horda forma uma nuvem com espaçamento, sem empilhar num único ponto
- [ ] Inimigos parados na parede voltam a se mover quando o jogador se move
- [ ] A lanterna continua matando inimigos e o jogador continua bloqueado por eles
- [ ] Na `arena.tscn`, os inimigos colocados à mão também se separam
- [ ] Nenhum erro no Output/Debugger ao matar inimigos em sequência e trocar de sala

## 9. Como testar (roteiro manual no editor)

1. Temporariamente, desligue o V-Sync (Projeto > Configurações do Projeto > Exibição > Janela > V-Sync > Modo V-Sync = Desativado). Desfaça no fim e não faça commit.
2. Rode a `sala_ninho.tscn` (F6) com 400 inimigos. Deixe a horda se aglomerar perto de uma parede e anote FPS, Processo de Física e Pares de Colisão (Depurador > Monitores).
3. Mova o jogador ao longo da parede e confirme que os inimigos parados voltam a segui-lo.
4. Aponte a lanterna para a nuvem e confirme que os inimigos morrem. Encoste o jogador na nuvem e confirme que ele é bloqueado.
5. Rode a `arena.tscn` e confirme que os inimigos colocados à mão se separam.
6. Ajuste `SeparationRadius` e `SeparationStrength` no Inspector de `fractal_enemy.tscn` e observe a nuvem mudar.

## 10. Decisões e notas

### Decididas

- Testar A e B juntas, numa branch de experimento; merge na `main` só se o resultado agradar.
- Jogador continua bloqueado pelos inimigos: camada 2 na máscara do `Player` (opção a).
- Separação calculada "todos contra todos" sobre um snapshot das posições (~160 mil comparações em C# puro com 400 inimigos). Grade espacial só se a medição pedir.
- Registro central em classe estática (`FractalSwarm`), para cobrir os três fluxos de criação sem mexer em cenas nem no `project.godot`.
- Bloqueio detectado por progresso (velocidade real × desejada) com descanso e despertar por mudança de direção, em vez de remover a componente da velocidade na direção da normal da parede. Motivo: a abordagem pela normal falha nas quinas (duas normais), onde o inimigo alternaria entre as paredes sem nunca descansar.
- O descanso não impede navegação futura: um inimigo seguindo um caminho está andando e não trava; e a mudança da direção desejada o acorda.

### Referência de desempenho (antes deste experimento)

- E4 (spec do inimigo, seção 11): 300 inimigos em Floating, 197 FPS, 11,63 ms de física, 816 pares de colisão.
- Com 400 inimigos aglomerados perto de uma parede, o FPS cai abaixo de 60 (observação do dev, sem números anotados).

## 11. Resultados e problema conhecido (set/2026)

### Medição após os passos 3 e 4

`sala_ninho.tscn`, 400 inimigos, V-Sync desligado só durante o teste. Valores dos Monitores (pior caso do último segundo), no momento de uma queda.

| Monitor | Valor |
|---|---|
| FPS | 43 |
| Processo de Física | 18,45 ms |
| Pares de Colisão | 350 |
| Ilhas | 282 |

- A separação eliminou a colisão entre inimigos: os pares de colisão caíram para 166–350 com 400 inimigos (eram 816 com 300 no E4).
- As quedas de FPS ficaram menos frequentes e mais curtas, segundo a observação do dev.
- Um primeiro teste foi invalidado por um erro de configuração: o `Player` ficou fora da camada 1, e os inimigos o atravessavam. Corrigido: o `Player` continua na camada 1, com máscara 1 e 2.

### Problema conhecido (adiado)

**Situação:** o jogador está do lado de **fora** de uma parede da sala, e a horda se aglomera do lado de **dentro**, formando um semicírculo denso contra a parede. Nesse cenário o FPS ainda cai abaixo de 60 com 400 inimigos.

**Prioridade:** baixa. Na avaliação do dev, a situação deve ocorrer pouco ou nunca no gameplay normal. Resolver quando voltar a ser relevante (ex.: mais inimigos, fragmentos da fase 2 ou salas com paredes finas entre jogador e horda).

**Hipóteses (não verificadas):**
- Só a camada da horda encostada na parede trava e descansa. O resto do semicírculo fica em "empurra-empurra" (a perseguição puxa para a parede e a separação empurra para fora), a direção desejada gira e os acorda, e quase todos chamam `MoveAndSlide` em todo passo.
- Com `SeparationStrength` igual ao `Speed`, a nuvem tende a colapsar, o que aumenta o empurra-empurra (ver nota de ajuste da seção 4).
- Custo base por inimigo (~20 µs por passo mesmo sem colisão, medido no E3a da spec do inimigo): ~8 ms com 400 inimigos.
- A separação "todos contra todos" pode custar mais que o estimado.

**Caminhos para quando for retomado:**
1. Instrumentação temporária para medir, por segundo: quantos inimigos se movem e quantos descansam, e o tempo gasto na separação e no `MoveAndSlide`. Decide entre as opções abaixo com dados.
2. Ajustar `SeparationStrength` acima do `Speed`, para a nuvem se estabilizar.
3. Critério de despertar usando só a direção da perseguição (ignorando a separação).
4. Comportamento para jogador inalcançável (ex.: parar de perseguir ou vagar quando não há como chegar ao jogador). É decisão de design e se relaciona com navegação.
5. Direções C (atualização escalonada) e D (limite de inimigos por sala).
