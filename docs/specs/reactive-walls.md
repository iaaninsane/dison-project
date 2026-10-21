# Spec: Paredes reativas (irritação e ejeção)

- **Status:** validada (out/2026). Decisões D1 a D6 aceitas pelo dev; D3 revista depois do teste (seção 10). Valores em teste.
- **Transitória:** o modelo-alvo das paredes de fronteira com o espaço passou a ser a `docs/specs/dyson-wall.md` (membrana que abre uma fenda e suga o jogador; decisão do dev, out/2026). Esta mecânica continua valendo nas salas atuais até a `DysonWall` existir. Paredes internas entre estruturas serão rígidas (fora do grupo `Walls`).
- **Etapa:** 1 (mecânica)
- **Depende de:** nada. Corrige a mecânica descrita no HANDOFF (seção 3.3) e o item 1.2 do backlog.

> **Valores em teste:** como nas demais specs da demo, os `[Export]` e os números desta spec são pontos de partida, não valores definitivos.

## 1. Objetivo

A parede viva reage à luz: acumula estresse e, no limite, puxa o cientista para dentro de si e o expele do outro lado. É o risco do ALERTAR: iluminar sem cuidado custa a posição do jogador.

## 2. Comportamento esperado

- Quando a lanterna ilumina uma parede do grupo `Walls`, então a irritação dela sobe; fora da luz, ela cai com `RecoveryRate` (como hoje).
- Quando a irritação chega a `MaxIrritation`, então a parede puxa o jogador em direção ao seu **centro geométrico real**, em qualquer rotação ou escala. Nunca empurra para longe (D1).
- Durante a ejeção, o jogador atravessa **a parede que disparou e as paredes do grupo `Walls` no caminho até o centro dela** (um "túnel" com a largura do jogador e uma sobra depois do centro; D2, D3 revista) e colide normalmente com as outras paredes, os pilares e as paredes de titânio.
- Quando a ejeção termina, então o jogador volta a colidir com todas as paredes atravessadas.
- A ejeção tira o controle por `EjectionDuration`; a lanterna continua mirando (como hoje).

## 3. Fora do escopo

- O que acontece do outro lado da parede (vácuo, volta à sala, dano): decisão em aberto D5, registrada no backlog.
- Contração e espasmo visual, cores e shader (etapa 2).
- Irritação por tiros (não existem tiros).
- Balanceamento dos valores.

## 4. Parâmetros ajustáveis (valores em teste)

| Parâmetro | Valor inicial | Onde fica | Observação |
|---|---|---|---|
| `MaxIrritation` | 15 | `WallSensitive.cs` | Limite que dispara a ejeção |
| `RecoveryRate` | 15/s | `WallSensitive.cs` | Queda da irritação por segundo |
| `EjectionForce` | 800 | `WallSensitive.cs` | Velocidade da ejeção (substitui a velocidade do jogador) |
| `EjectionPathWidth` | 16 px | `WallSensitive.cs` | Largura do túnel do puxão (o jogador tem 10 px; em diagonal ocupa ~14) |
| `EjectionPathOvershoot` | 60 px | `WallSensitive.cs` | Quanto o túnel passa do centro (saída pela face de trás em ângulo raso). Curto demais: raspa na saída; longo demais: alcança a parede de trás |
| `WallIrritationPerSecond` | 20/s | `LightDamageArea.cs` | Irritação aplicada pela luz |
| `EjectionDuration` | 0,8 s | `Player.cs` | `[Export]` desde esta spec (D6); ajustar no `player.tscn` vale para todas as salas |

As paredes da `sala_desfiladeiro` não são instâncias de uma cena de parede: cada uma tem os próprios `[Export]` (dívida técnica no backlog). Para mudar todas de uma vez, mudar o valor padrão no código.

## 5. Casos de borda

- Parede sem `ColorRect`: o centro é o `GlobalPosition` do corpo (como hoje).
- Duas paredes no limite ao mesmo tempo: só a primeira ejeta; a outra fica no máximo e dispara assim que a ejeção acabar, se ainda estiver iluminada (comportamento atual, mantido).
- Uma parede atravessada some durante a ejeção (troca de sala): remover a exceção de colisão só se ela ainda existir (`IsInstanceValid`).
- Nova ejeção por cima de outra: o `Player` desfaz as exceções anteriores antes (não depende de a parede checar `IsEjected`).
- Morte durante a ejeção: o jogador morto continua sem colidir com as paredes atravessadas até a sala recarregar (2 s). Aceito; antes acontecia o mesmo com todas as paredes.
- Fim da ejeção com o jogador ainda dentro da parede (raro: o percurso é de ~600 px e a parede tem 40): a física da Godot o expulsa. Observar no teste.

## 6. Arquivos e cenas

- Alterados:
  - `WallSensitive.cs`: `GetWallCenter()` (centro pela transformação global do `ColorRect`), `GetWallsOnPath()` (consulta `IntersectShape` do túnel, filtrando o grupo `Walls`), comentários da direção corrigidos, logs a cada frame e "SUCESSO" removidos (D4); `[Export]` `EjectionPathWidth` e `EjectionPathOvershoot`.
  - `Player.cs`: `ApplyEjection(impulse, passThroughWalls)` cria exceção de colisão com cada parede da lista, em vez de desligar a máscara 1 inteira; `EndEjection()` desfaz as exceções; `EjectionDuration` como `[Export]` (D6).
- Somente leitura: `LightDamageArea.cs`.
- Cenas: nenhuma muda.
- Docs ao concluir: regra do pivô no `CLAUDE.md`; item 1.2 removido do `docs/BACKLOG.md` e D5 registrada como decisão em aberto.

## 7. Passos no editor (feitos pelo dev)

Nenhuma montagem de cena. Só apertar **Build** no editor por causa dos `[Export]` novos (`EjectionDuration`, `EjectionPathWidth`, `EjectionPathOvershoot`). Feito em out/2026.

## 8. Pronto quando

Confirmado pelo dev no editor (out/2026):

- [x] `dotnet build` sem erros
- [x] `sala_ninho`: nos 6 `WallSegment`, a parede iluminada até o limite puxa o jogador em direção ao bloco iluminado, nunca para longe
- [x] `sala_desfiladeiro`: o mesmo nas paredes giradas (45°, −39,5° e 90°), dos dois lados da parede
- [x] Durante a ejeção, o jogador atravessa só a parede que disparou e as paredes no caminho até o centro dela, e bate nas outras paredes e nos pilares
- [x] Dois blocos vizinhos iluminados em diagonal: atravessa os dois, sem raspão nem ricochete
- [x] Saída em ângulo raso sem enganchar
- [x] Depois da ejeção, as paredes atravessadas voltam a ser sólidas
- [x] `EjectionDuration` aparece no Inspector do `Player` e altera o tempo sem controle
- [x] Output sem mensagens a cada frame e sem erros

## 9. Como testar (roteiro manual no editor)

1. Na `sala_ninho`, ilumine cada `WallSegment` de dentro da arena até ele ficar vermelho e confira a direção do puxão.
2. Repita na `sala_desfiladeiro`, iluminando as paredes giradas dos dois lados.
3. Ilumine dois blocos vizinhos em diagonal ao mesmo tempo: o jogador atravessa os dois, sem ricochete.
4. Ilumine um bloco em ângulo bem raso: o jogador sai pela face de trás sem enganchar.
5. Provoque uma ejeção perto de um pilar ou de outra parede e confira que o jogador bate nele.
6. Depois da ejeção, encoste nas paredes atravessadas: devem estar sólidas.

## 10. Decisões e notas

### Diagnóstico (leitura do código, out/2026)

- **Direção errada (problema 1 do backlog):** o centro era `_visualRect.GlobalPosition + _visualRect.Size / 2`. O `GlobalPosition` do `ColorRect` já vem girado com a parede, mas o `Size / 2` é somado sem rotação, então em parede girada o ponto cai fora dela. Exemplo (conta à mão a partir da cena): `WallSegment6` da `sala_desfiladeiro` (400×40, girada 90°) tem centro real na origem do corpo, mas o código calculava 220 px à direita e 180 px acima; com o jogador à direita da parede, a direção apontava para longe (empurrava). Na `sala_ninho`, 5 dos 6 `WallSegment` são girados (erros de 40 a 110 px). Correção: transformar o centro local do `ColorRect` (`Size / 2`) pela transformação global dele, que aplica posição, rotação e escala de todos os pais. Vale para os dois tipos de parede do projeto: pivô no canto (`paredes_reativas.tscn`) e pivô no centro (`wall_block.tscn`, `sala_desfiladeiro`).
- **Atravessar todas as paredes (problema 2 do backlog):** `ApplyEjection` chamava `SetCollisionMaskValue(1, false)`; por 0,8 s o jogador ignorava tudo na camada 1 (paredes reativas, pilares, paredes de titânio). Correção: exceção de colisão só com a parede que disparou (`AddCollisionExceptionWith` / `RemoveCollisionExceptionWith`).
- O comentário "do centro da parede apontando para o jogador" estava invertido: `wallCenter − playerPosition` aponta do jogador para a parede (puxa).

### Decididas pelo dev (out/2026)

1. **D1, direção:** puxar para o centro da parede (como no HANDOFF).
2. **D2, atravessar:** o jogador atravessa só a parede que disparou.
3. **D3, "o trecho":** só o bloco que disparou. Na `sala_ninho` cada `WallSegment` tem 4 blocos de 100 px; se o puxão em diagonal raspar no bloco vizinho, reavaliar ampliar para o segmento inteiro.
   - **Revista depois do teste (out/2026):** ao iluminar dois blocos vizinhos em diagonal, o caminho até o centro do bloco que disparou cruzava o bloco vizinho, já sólido, e o jogador batia nele (efeito aparente de repulsão). Opções avaliadas: (1) atravessar as paredes do grupo `Walls` no caminho; (2) o segmento inteiro (descartada: "blocos irmãos" não serve como regra, pois na `sala_desfiladeiro` todas as paredes são irmãs, e não cobre a junção entre segmentos); (3) "tecido irritado amolece", toda parede com irritação > 0 (descartada depois de um comparativo: a `LightDamageArea` detecta paredes atrás de outras, então a parede de trás do corredor ficaria mole e o problema 2 voltaria em parte); (4) aceitar o raspão (contraria D1). **Escolhida pelo dev: opção 1**, com o túnel do jogador até `EjectionPathOvershoot` depois do centro.
4. **D4, logs:** remover o `GD.Print` a cada frame do `AddIrritation` e o "SUCESSO" do `_Ready`; o `PrintErr` de `ColorRect` ausente fica.
5. **D5, do outro lado da parede:** fora desta spec (ser ejetado da `sala_ninho` leva o jogador para fora da arena, como já acontece hoje). Registrar no backlog como decisão em aberto; toca no "vácuo", adiado para depois da demo.
   - **Atualizado (out/2026):** o rascunho de game design define que a ejeção leva o jogador para fora da estrutura, no espaço (modelo da `dyson-wall.md`, sinal `PlayerEjected`). O espaço não é navegável (o "vácuo como atalho" continua adiado); a **consequência** (morte, dano, volta ao último nó, perda de biomassa) continua em aberto no backlog.
6. **D6, `EjectionDuration`:** vira `[Export]` no `Player`.

### Notas de implementação (out/2026)

- **Verificado no motor** (projeto descartável no scratchpad, Godot 4.7.2 headless): a conta antiga do centro errava 284 px (pivô no centro, 90°), 122 px (pivô no canto, 45°) e 131 px (pai girado 180° e escalado); a conta nova acertou em todos os casos. A consulta do túnel devolveu os blocos cruzados (inclusive o da saída em ângulo raso) e deixou de fora o pilar e uma parede fora do túnel.
- **Consulta ao espaço físico** (`GetWorld2D().DirectSpaceState.IntersectShape`): só é segura durante o passo de física. A ejeção nasce no `_PhysicsProcess` da `LightDamageArea` (→ `AddIrritation` → `TriggerEjection`); se outra fonte passar a disparar a ejeção (ex.: tiros), ela também precisa rodar no passo de física.
- **A parede decide, o jogador executa:** o `WallSensitive` monta a lista de paredes atravessadas; o `Player` só aplica e desfaz as exceções e não conhece o grupo `Walls`.
- A ejeção **substitui** a velocidade (`Velocity = impulse`); o golpe (`ApplyKnockback`) é que soma. O CLAUDE.md dizia que os dois somavam e foi corrigido.
