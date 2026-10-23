# Spec: Câmara de partida no nó inicial

- **Status:** **validada** (ideia do dev, out/2026). Decisões D1 a D6 na seção 10; passos 2 a 7 feitos e testados pelo dev no editor, cada um verificado antes no descartável. Revê a decisão H5 da `hex-phase.md` (a câmara separada na `sala_entrada` virou bancada).
- **Etapa:** 1 (mecânica)
- **Depende de:** `docs/specs/hex-phase.md` (fases A e B), `docs/specs/suit-evolution.md` (terminais, banco, melhorias), `docs/specs/player-health-death.md` (morte e reinício). Decidiu a consequência da ejeção (backlog, seção 2). Relaciona-se com o backlog 1.4 (spawners móveis, que vão escutar `IncursionStarted`).

> **Valores em teste:** como nas demais specs da demo, os `[Export]` e os números desta spec são pontos de partida, não valores definitivos.

## 1. Objetivo

O jogador começa a partida dentro da fase, no primeiro nó, que funciona como câmara de partida: um refúgio selado com os terminais do traje. Ao liberar as saídas, o nó vira um nó comum e a incursão começa. Tira a troca de cena da câmara separada (combina com "uma cena por fase", H2) e cria um momento claro de início da incursão para o resto do jogo escutar (spawners, dificuldade).

## 2. Comportamento esperado

### Ideia do dev (out/2026)

- O jogador nasce no primeiro nó da fase, e não mais na `sala_entrada`.
- Os terminais da `sala_entrada` (modo da lanterna e evolução do traje) ficam nesse nó.
- Todas as saídas do nó (as bocas dos corredores) ficam bloqueadas, como portas a liberar.
- Um terceiro terminal, provisoriamente "Liberar saídas": ao interagir, as saídas se abrem, os demais terminais somem, o loop principal começa, e o nó passa a se comportar como qualquer outro.
- Ao morrer ou ser ejetado, o jogador volta a nascer no primeiro nó, de novo como câmara de partida, com todos os terminais.
- O escurecer e clarear do `ScreenFade` continua sempre que o jogador morre e quando começa no nó de partida.
- A `sala_entrada` passa a ser uma sala de testes e prototipagem, e pode ser descartada no futuro.

### Regras (aprovadas e implementadas, decisões D1 a D6)

- Quando o jogo começa (cena principal: `hex_phase.tscn`) ou a fase recarrega, então o nó de entrada está em modo câmara: saídas fechadas por portões, terminais visíveis, paredes do nó inertes (D4), eclusa desligada e o jogador à frente da eclusa (`HexPhaseBuilder.PlacePlayerAtEntry`).
- Quando a fase carrega, então a biomassa carregada é depositada no banco (`StartChamber`, como a `SalaEntrada` faz).
- Quando o jogador interage com o terminal "Liberar saídas", então os portões somem, as paredes do nó viram reativas, a eclusa liga, os terminais do nó somem e a câmara avisa que a incursão começou (sinal `IncursionStarted`).
- Quando o jogador morre, então, depois de `DeathRestartDelay`, a tela escurece e a fase recarrega em modo câmara (o `RestartOnDeath`, com destino vazio, recarrega a cena atual).
- Quando o jogador fica perdido no espaço (sinal `LostInSpace`), então o traje rompe (`SuitIntegrity.Breach`) e a incursão termina como na morte (D3).
- Quando o jogador toca a eclusa depois de liberar as saídas, então a tela escurece e a fase recarrega em modo câmara, com 100% da biomassa carregada depositada (extração, D2). Com o nó travado, a eclusa não age.
- A integridade começa cheia a cada recarga; banco, níveis das melhorias e modo da lanterna continuam no `GameState`.

## 3. Fora do escopo

- Os spawners móveis e o objetivo da fase (backlog 1.4): esta spec só cria o evento `IncursionStarted` que eles vão escutar.
- Arte dos portões, dos terminais e da câmara (etapa 2): visual provisório por código ou `ColorRect`.
- Salvar progresso em disco.
- Descartar a `sala_entrada` (fica como bancada; decisão futura).

## 4. Parâmetros ajustáveis (valores em teste)

| Parâmetro | Valor inicial | Onde fica | Observação |
|---|---|---|---|
| Espessura dos portões | `WallThickness` (40 px) | `HexPhaseBuilder` | Vedam a boca do corredor na linha das paredes do nó; cada portão passa uma espessura de parede de cada lado da boca |
| `GateColor` | provisória | `HexPhaseBuilder` (grupo "Visual provisório") | Distinta das paredes reativas |
| `StartChamber` | — | `HexPhaseBuilder` | Câmara de partida, ligada no Inspector; vazio = fase aberta, como antes (sem portões, eclusa ativa) |
| `SpaceReturnDelay` | 2 s | `StructureTracker` | Tempo no espaço até o `LostInSpace`. Da ejeção ao escurecer: `SpaceReturnDelay` + `DeathRestartDelay` + escurecer (~4,8 s) |
| `ScreenFadeDuration` | 0,8 s (`player.tscn`) | `ScreenFade` | Escurecer e clarear em toda troca de cena |
| `DeathRestartDelay` | 2 s | `RestartOnDeath` | Destino vazio: recarrega a cena atual |

## 5. Casos de borda

- **Bocas do nó inicial:** no hexágono, o nó 2 tem 5 saídas (3 conectores de nós e 2 de placa); no setor, 3. Verificado no descartável: com os portões, nenhuma de 108 sondas (setor: nós 1 e 2; hexágono: nós 1 a 6) escapou, inclusive deslizando nas quinas; depois de abrir, todas entraram nos corredores.
- **Portões fora do grupo `Walls`:** a luz não os irrita e a ejeção não os atravessa. Os inimigos (camada 2, máscara 1) também batem neles.
- **Paredes inertes até liberar** (D4): ficam fora do grupo `Walls` e entram nele ao liberar. Uma parede que já estiver dentro do feixe nesse momento só começa a irritar quando o feixe sair dela e voltar (a `LightDamageArea` registra paredes ao entrar no feixe). Desprezível.
- **Ejeção dentro da câmara travada:** impossível com as paredes inertes.
- **Paredes dos corredores do lado de fora dos portões:** são dos corredores, então continuam reativas. O feixe passa pelo portão (a `Area2D` da luz não é bloqueada por corpos) e pode irritá-las; o puxão então bate no portão (fora do grupo `Walls`), e o jogador só perde o controle por `EjectionDuration`. Aceito.
- **Ejeção = morte (D3):** o rastreador ignora o jogador morto, então o `LostInSpace` não se repete depois do traje rompido (verificado: uma morte só).
- **Eclusa com o nó travado:** `Monitoring` desligado (sem nada a extrair); a `StartChamber` liga ao liberar as saídas, adiado (`SetDeferred`) para não mexer na física no meio de um passo.
- **Três terminais no mesmo nó:** cada um reage ao evento em `_UnhandledInput` e marca o aperto como usado (`SetInputAsHandled`), então um aperto aciona um terminal só. Com o painel de evolução aberto, o jogo fica pausado e os terminais não recebem o aperto que fecha o painel. Verificado no motor: o botão com foco NÃO consome o aperto do A; por leitura da tecla (o `TerminalModo` antigo), um toque rápido que fechava o painel trocava o modo junto.
- **Colar nós com referências entre si** (passo 4): o editor mostra erros passageiros ("no common ancestor", "not in a scene tree") enquanto refaz as referências; conferir os campos salvos.
- **Recarga da fase:** a montagem leva ~0,1 s e fica escondida pelo escurecer. Os registros estáticos (`FractalSwarm`, `BiomassField`) se limpam no `_ExitTree`; "Nós Órfãos" = 0 em recargas seguidas (verificado pelo dev e no descartável).
- **Liberar duas vezes:** a `StartChamber` guarda `HasStarted` e ignora um segundo pedido; o terminal também só emite uma vez.
- **`StartChamber` sem ligar:** a fase começa aberta, como antes (lição dos campos `[Export]` vazios: o jogador nunca fica selado sem saída).
- **Spawners (1.4):** não podem gerar inimigos antes de `IncursionStarted`, nem dentro do nó inicial travado.
- **Bancadas** (`sala_entrada`, `sala_ninho` e outras): com o `RestartOnDeath` de destino vazio, morrer numa bancada recarrega a própria bancada.

## 6. Arquivos e cenas

- **Existentes, reaproveitados:** `ScreenFade.cs`, `DoorTrigger.cs`, `RestartOnDeath.cs` (troca de cena com escurecer e clarear), `StructureTracker.cs` (o sinal `LostInSpace`), `EvolutionTerminal.cs`, `UpgradePanel.cs`, `GameState.cs`, `SalaEntrada.cs` (o depósito ao entrar serviu de modelo).
- **Novos** (criados com autorização do dev, out/2026):
  - `ExitReleaseTerminal.cs` (`Area2D`, passo 3): terminal "Liberar saídas"; com o jogador dentro, o `interact` (por `_UnhandledInput`) emite o sinal `Activated` uma vez.
  - `StartChamber.cs` (`Node2D`, passo 3; `HasStarted`): contêiner montado no editor com os três terminais, posicionado pelo `HexPhaseBuilder` no nó de entrada (como a eclusa). Ao começar: deposita a biomassa carregada e desliga a eclusa. Ao liberar (ouvinte do `Activated`): pede à fase para abrir as saídas, liga a eclusa, apaga os terminais e emite `IncursionStarted`.
  - `EjectionConsequence.cs` (`Node`, passo 5): a consequência da ejeção como componente trocável, ouvinte do `LostInSpace`; acha o `SuitIntegrity` do jogador pelo nome e chama `Breach()`, e a morte segue o caminho de sempre.
- **Alterados:**
  - `HexPhaseLayout.cs` (passo 2): `HexWallSegment` guarda a estrutura dona (`Owner`; o `Kind` vem dela); `PeripheralNode(número)`; `ExitsOf(nó)` devolve as bocas, achadas pela interseção da borda do nó com cada corredor e alargadas uma espessura de parede de cada lado.
  - `HexPhaseBuilder.cs`: (passo 2) portões (`StaticBody2D`, fora do grupo `Walls`, `[Export] GateColor`) nas bocas do nó de entrada; paredes desse nó fora do grupo até abrir; `OpenEntryExits()` público; paredes e portões criados pelo mesmo `CreateSegmentBody<T>`. (Passo 3) `[Export] StartChamber` e `PlaceStartChamber()`; sem a câmara, nada fica travado. (Passo 5) sai o `OnPlayerLostInSpace` (regra provisória).
  - `SuitIntegrity.cs` (passo 5): `Breach()` (traje rompido de uma vez, mesmo sinal `Died`).
  - `TerminalModo.cs` (passo 4): interação por `_UnhandledInput` em vez de *polling* (D6). Comentários corrigidos no `ExitReleaseTerminal.cs` e no `EvolutionTerminal.cs`.
  - Só comentários (passo 5): `PhaseCamera.cs` e `StructureTracker.cs` (sem a regra provisória).
- **Cenas:** `hex_phase.tscn` (câmara de partida com os terminais copiados da `sala_entrada`, terminal "Liberar saídas", `EjectionConsequence`), `player.tscn` (`RestartOnDeath` com destino vazio), eclusa → `hex_phase.tscn` (extração), cena principal do projeto → `hex_phase.tscn` (`project.godot`, autorizado pelo dev no passo 6), `sala_entrada.tscn` vira bancada.

## 7. Passos no editor (feitos pelo dev, out/2026)

- **Passo 3:** **`StartChamber`** (`Node2D`, `StartChamber.cs`), filho da raiz `HexPhase`, com **Phase** = `HexPhase` e **Airlock** = `Airlock`. Filho dela: **`ExitReleaseTerminal`** (`Area2D`, `ExitReleaseTerminal.cs`) em (300, 0), com `CollisionShape2D` (círculo de raio 60) e `ColorRect` (Position (-40, -40), Size (80, 80), Mouse Filter = Ignore). Sinal **Activated → StartChamber.OnExitsReleased**. Na raiz: **Start Chamber** = `StartChamber`. As posições dos filhos são relativas à câmara: origem no centro do nó de entrada, eixo X apontando para a eclusa (o jogador nasce em (740, 0)).
- **Passo 4:** **`TerminalModo`**, **`EvolutionTerminal`** e **`EvolutionUi`** (com o `UpgradePanel`) copiados da `sala_entrada` para a `StartChamber` (Ctrl+C, Ctrl+V; a `sala_entrada` ficou com os dela). `TerminalModo` recentralizado (`CollisionShape2D` em (0, 0), `ColorRect` em (-41, -40)) e posto em (550, -350); `EvolutionTerminal` em (550, 350). Campos conferidos: **Panel** do `EvolutionTerminal` e **Catalog**, **Bank Label**, **Upgrade List** e **Close Button** do `UpgradePanel`.
- **Passo 5:** **`EjectionConsequence`** (`Node`, `EjectionConsequence.cs`), filho da raiz `HexPhase`, com **Player** = `Player`. Sinal **LostInSpace** do `StructureTracker` religado de `HexPhase.OnPlayerLostInSpace` (método removido) para **`EjectionConsequence.OnPlayerLostInSpace`**. **`Airlock`** > **Target Scene Path** = `hex_phase.tscn`. `player.tscn` > **`RestartOnDeath`** > **Return Scene Path** vazio (revertido).
- **Passo 6:** **Projeto > Configurações do Projeto > Aplicação > Executar > Cena Principal** = `hex_phase.tscn`.

## 8. Pronto quando

Confirmado pelo dev no editor (out/2026):

- [x] `dotnet build` sem erros a cada passo
- [x] F5 abre a fase no nó inicial em modo câmara: saídas fechadas, terminais visíveis, paredes do nó sem irritar
- [x] O terminal de modo e o de evolução funcionam no nó, sem um aperto acionar outro terminal
- [x] "Liberar saídas" abre os portões, faz os terminais sumirem e as paredes do nó voltam a reagir; o nó passa a ser um nó comum
- [x] Morrer escurece e recarrega a fase em modo câmara, com a taxa da morte depositada no banco
- [x] Ser ejetado tem a consequência decidida em D3 (igual à morte)
- [x] A extração pela eclusa deposita 100% e recarrega em modo câmara; com o nó travado, a eclusa não age
- [x] Recargas seguidas sem erros no Output e com "Nós Órfãos" = 0

## 9. Como testar (roteiro manual no editor)

1. F5: confira o modo câmara (tente sair pelas bocas, deslizando nas quinas, e irritar as paredes do nó; toque a eclusa: nada acontece).
2. Use os dois terminais do traje; feche o painel de evolução com o "A" e confira que ele não reabre e que o modo da lanterna não muda.
3. Libere as saídas; saia por um corredor e volte (o nó é comum, sem terminais).
4. Morra, seja ejetado e extraia, conferindo o banco a cada volta (com um ninho temporário, como na seção 7 da `hex-phase.md`, para ter biomassa e inimigos; apague-o depois).
5. Repita 5 vezes olhando o Output e "Nós Órfãos".

## 10. Decisões e notas

### Decididas pelo dev (out/2026)

Todas seguiram a recomendação da análise:

1. **D1, primeiro nó:** o nó de entrada (`EntryPeripheral`, hoje o nó 2), que já tem a eclusa e o nascimento. No hexágono, 5 saídas a fechar (3 conectores de nós e 2 de placa); no setor, 3. (Alternativa descartada: o nó central, com 12 saídas e sem eclusa.)
2. **D2, extração pela eclusa:** depois de liberar as saídas, voltar ao nó inicial e tocar a eclusa escurece e recarrega a fase em modo câmara, depositando 100%. Com o nó travado, a eclusa fica desligada. Mantém o risco × recompensa: sair vivo guarda tudo, morrer guarda metade. (Descartada: sem extração.)
3. **D3, ejeção igual à morte:** taxa da morte, escurecer e recarga em modo câmara. Um fluxo só; resolveu a decisão em aberto do backlog. (Descartada: reinício sem taxa.)
4. **D4, paredes do nó travado inertes:** não irritam nem ejetam (como o titânio da câmara antiga); viram reativas ao liberar. (Descartada: reativas desde o início.)
5. **D5, documentação:** esta spec; a `hex-phase.md` registra que a H5 foi revista.
6. **D6, `TerminalModo` por `_UnhandledInput`:** com três terminais na mesma sala, cada aperto aciona um terminal só, e o terminal pausado não recebe o aperto que fecha o painel. Resolveu a dívida do backlog. O motivo registrado antes ("a interface consome o aperto") estava errado (verificado no motor, passo 4) e foi corrigido no `CLAUDE.md` e na `suit-evolution.md`.

Da ideia do dev: depois de liberar, os terminais somem até o próximo reinício (não há compra de melhorias no meio da incursão).

### Análise (out/2026)

- **A favor:** a partida inteira numa cena (H2), sem a troca de cena da câmara; começo dentro do mundo (refúgio selado no organismo; liberar as saídas é o momento em que ele percebe o jogador); um evento claro de início da incursão para os spawners (1.4) e para a dificuldade.
- **Riscos de design:** com a ejeção encerrando a incursão (D3), todo o progresso da incursão se perde ao ser ejetado; com paredes que ejetam em 1,5 a 5 s de luz, isso pune bastante (ligado à sensibilidade das paredes, em aberto).
- **Viabilidade:** confirmada. Reaproveitou a fase B inteira (reinício por recarga da cena com `ScreenFade`, `GameState` persistente, posicionamento no nó de entrada pelo construtor, ponto de troca da regra do espaço no ouvinte do `LostInSpace`).

### Arquitetura (implementada)

- **Portões** gerados pelo `HexPhaseBuilder` (contornos por código, H3), fora do grupo `Walls`, e um método público para abrir as saídas (remove os portões e torna as paredes do nó reativas). As bocas saem da geometria (`HexPhaseLayout.ExitsOf`): é a regra das paredes ao contrário (o pedaço da borda que cai dentro do corredor).
- **Terminal "Liberar saídas"** só emite um sinal (Observer); quem reage é a câmara. A câmara conhece a ordem (abrir, ligar a eclusa, tirar os terminais, avisar); a fase só abre a geometria; os spawners (1.4) só vão escutar `IncursionStarted`.
- **Câmara de partida** como contêiner do editor posicionado pelo construtor (como a eclusa): aparência no editor, posição no código.
- **Consequência da ejeção** como componente trocável, ouvinte do `LostInSpace` (ideia do Strategy: trocar a regra é trocar o componente, sem mexer no rastreador). Ela rompe o traje e reaproveita o fluxo da morte (`Died` → `Player.OnDied` e `RestartOnDeath`), sem duplicar taxa, escurecer e recarga.
- **O estado "travado" não precisa ser guardado:** a fase recarregada nasce em modo câmara, que é o estado inicial da cena.
- **Cuidado mantido:** o `HexPhaseBuilder` só constrói e abre; o fluxo da partida fica nos componentes novos, para não virar uma "classe Deus".

### Plano de execução (concluído)

0. Commit do estado anterior (`a8ccca7`).
1. Decisões D1 a D6 registradas nesta spec e no backlog.
2. **Portões e paredes inertes** (`HexPhaseLayout.cs`, `HexPhaseBuilder.cs`). Feito e testado pelo dev. No descartável, nenhuma de 108 sondas escapou com os portões, e todas saíram depois de `OpenEntryExits`.
3. **Terminal e câmara** (`ExitReleaseTerminal.cs` e `StartChamber.cs`; o construtor posiciona a câmara). Feito e testado pelo dev. No descartável: depósito ao começar, eclusa desligada, "interact" fora do terminal sem efeito, liberação única, eclusa extraindo depois.
4. **Terminais do traje na câmara** (cópia no editor; `TerminalModo.cs` por `_UnhandledInput`). Feito e testado pelo dev. Achado no motor sobre o "A" (D6).
5. **Ejeção, morte e extração** (`EjectionConsequence.cs`; `SuitIntegrity.Breach`; sem o `OnPlayerLostInSpace`; `RestartOnDeath` com destino vazio; eclusa → `hex_phase.tscn`). Feito e testado pelo dev. No descartável, com recargas reais: ejeção com 10 carregados → banco +5, uma morte só; extração com 8 → banco +8; "Nós Órfãos" = 0.
6. **Cena principal → `hex_phase.tscn`** (autorizado pelo dev); a `sala_entrada` virou bancada. Ciclo completo testado pelo dev, sem erros e com "Nós Órfãos" = 0.
7. **Documentação** (abaixo) e mensagem de commit.

### Documentos atualizados no passo 7 (conflitos resolvidos)

- `hex-phase.md`: H5 revista (câmara dentro da fase), regra provisória do espaço substituída, eclusa como extração, decisão 19.
- `suit-evolution.md`: terminais e depósito no nó inicial; motivo do `_UnhandledInput` corrigido (passo 4).
- `player-health-death.md`: a morte recarrega a fase no nó inicial; decisão 7 (ejeção) resolvida.
- `reactive-walls.md` (D5) e `dyson-wall.md` (2.6): consequência da ejeção decidida.
- `BACKLOG.md`: item 1.7 concluído; decisão da ejeção resolvida; o 1.4 escuta `IncursionStarted`; dívida do `TerminalModo` resolvida.
- `HANDOFF.md`: seções 3.7, 4 e 6.7 e a lista de cenas.
- `CLAUDE.md`: estrutura, cena principal, câmara de partida, `sala_entrada` como bancada e a regra do controle na interface.

### Em aberto

- **Tempo da ejeção ao escurecer** (~4,8 s): ajustar `SpaceReturnDelay` e `DeathRestartDelay` no teste, se pesar.
- **Sensibilidade das paredes** (`hex-phase.md`): com a ejeção encerrando a incursão, ela pesa mais.
- **Descartar a `sala_entrada`** no futuro (decisão do dev).
