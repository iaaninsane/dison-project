# Spec: Câmara de partida no nó inicial

- **Status:** rascunho para **implementação futura** (ideia do dev, out/2026). Não implementar até o dev decidir as questões da seção 10. Enquanto isso, vale o fluxo atual da `hex-phase.md` (câmara separada na `sala_entrada`, entrada e extração pela eclusa).
- **Etapa:** 1 (mecânica)
- **Depende de:** `docs/specs/hex-phase.md` (fases A e B validadas), `docs/specs/suit-evolution.md` (terminais, banco, melhorias), `docs/specs/player-health-death.md` (morte e reinício). Revê a decisão H5 da `hex-phase.md` e a consequência da ejeção (backlog, seção 2). Relaciona-se com o backlog 1.4 (spawners móveis).

> **Valores em teste:** como nas demais specs da demo, os `[Export]` e os números desta spec são pontos de partida, não valores definitivos. Itens marcados "(proposto)" ainda não foram aprovados.

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

### Regras (proposto)

- Quando a fase carrega, então o nó inicial está em modo câmara: saídas fechadas por portões, terminais visíveis, paredes do nó inertes (D4) e o jogador à frente da eclusa (`HexPhaseBuilder.PlacePlayerAtEntry`).
- Quando a fase carrega, então a biomassa carregada é depositada no banco (como a `SalaEntrada` faz hoje).
- Quando o jogador interage com o terminal "Liberar saídas", então os portões somem, as paredes do nó viram reativas, os terminais do nó somem e a fase avisa que a incursão começou (sinal `IncursionStarted`).
- Quando o jogador morre, então, depois de `DeathRestartDelay`, a tela escurece e a fase recarrega em modo câmara (o `RestartOnDeath` aponta para a própria fase).
- Quando o jogador é ejetado para o espaço (sinal `LostInSpace`), então a incursão termina como na morte (D3).
- Quando o jogador toca a eclusa depois de liberar as saídas, então a tela escurece e a fase recarrega em modo câmara, com 100% da biomassa carregada depositada (extração, D2). Com o nó travado, a eclusa fica desligada.
- A integridade começa cheia a cada recarga; banco, níveis das melhorias e modo da lanterna continuam no `GameState`.

## 3. Fora do escopo

- Os spawners móveis e o objetivo da fase (backlog 1.4): esta spec só cria o evento `IncursionStarted` que eles vão escutar.
- Arte dos portões, dos terminais e da câmara (etapa 2): visual provisório por código ou `ColorRect`.
- Salvar progresso em disco.
- Descartar a `sala_entrada` (fica como bancada; decisão futura).

## 4. Parâmetros ajustáveis (proposto)

| Parâmetro | Valor inicial | Onde fica | Observação |
|---|---|---|---|
| Espessura dos portões | `WallThickness` (40 px) | `HexPhaseBuilder` | Vedam a boca do corredor na linha das paredes do nó |
| Cor dos portões | provisória | `HexPhaseBuilder` (grupo "Visual provisório") | Distinta das paredes reativas |
| Fração guardada na ejeção | igual a `DeathKeepFraction` (0,5) | a definir | Só se D3 escolher uma taxa diferente da morte |
| `ScreenFadeDuration` | 0,8 s (`player.tscn`) | `ScreenFade` | Já existe |
| `DeathRestartDelay` | 2 s | `RestartOnDeath` | Já existe; destino passa a ser a própria fase |

## 5. Casos de borda

- **Bocas do nó inicial:** no hexágono, o nó 2 tem 5 saídas (3 conectores de nós e 2 de placa); no setor, 3. Os portões precisam vedar a boca sem fresta (verificar no descartável com um teste de física, como nas paredes).
- **Portões fora do grupo `Walls`:** a luz não os irrita e a ejeção não os atravessa. Os inimigos (camada 2, máscara 1) também batem neles.
- **Paredes inertes até liberar** (D4): ficam fora do grupo `Walls` e entram nele ao liberar. Uma parede que já estiver dentro do feixe nesse momento só começa a irritar quando o feixe sair dela e voltar (a `LightDamageArea` registra paredes ao entrar no feixe). Desprezível.
- **Ejeção dentro da câmara travada:** impossível com as paredes inertes.
- **Eclusa com o nó travado:** desligada (sem nada a extrair). Ligar ao liberar as saídas.
- **Interação no mesmo nó com três terminais:** cada terminal reage ao evento em `_UnhandledInput`, para o aperto do "A" que a interface já usou (ex.: fechar o painel de evolução) não acionar outro terminal (D6).
- **Recarga da fase:** a montagem leva ~0,1 s e fica escondida pelo escurecer. Os registros estáticos (`FractalSwarm`, `BiomassField`) se limpam no `_ExitTree`; conferir "Nós Órfãos" = 0 em recargas seguidas.
- **Spawners (1.4):** não podem gerar inimigos antes de `IncursionStarted`, nem dentro do nó inicial travado.
- **Bancadas** (`sala_ninho` e outras): com o `RestartOnDeath` do `player.tscn` apontando para a fase, morrer numa bancada leva à fase. Alternativa: destino vazio (recarrega a sala atual), que também recarrega a fase quando a morte é nela.

## 6. Arquivos e cenas

- **Existentes, reaproveitados:**
  - `HexPhaseBuilder.cs`: já posiciona a eclusa e o nascimento no nó de entrada; passaria a criar os portões e a posicionar a câmara de partida.
  - `HexPhaseLayout.cs`: já sabe as passagens (as paredes descontam os trechos dentro de outras estruturas); passaria a informar as bocas de um nó.
  - `ScreenFade.cs`, `DoorTrigger.cs`, `RestartOnDeath.cs`: troca de cena com escurecer e clarear.
  - `StructureTracker.cs`: o sinal `LostInSpace` (trocar o ouvinte para a nova consequência da ejeção).
  - `EvolutionTerminal.cs`, `UpgradePanel.cs`, `TerminalModo.cs`, `GameState.cs`, `SalaEntrada.cs` (o depósito ao entrar serve de modelo).
- **Novos (proposto; nomes a confirmar na implementação):**
  - Terminal "Liberar saídas" (`Area2D`, `interact` por `_UnhandledInput`, só emite um sinal).
  - Câmara de partida (`Node2D` montado no editor com os três terminais, posicionado pelo construtor no nó de entrada; deposita a biomassa ao começar; faz os terminais sumirem e emite `IncursionStarted` ao liberar).
  - Consequência da ejeção como componente trocável, ouvinte do `LostInSpace` (talvez um método novo no `SuitIntegrity` para encerrar a incursão pelo mesmo caminho da morte).
- **Alterados (proposto):** `HexPhaseLayout.cs`, `HexPhaseBuilder.cs`, `TerminalModo.cs` (D6), talvez `SuitIntegrity.cs` (D3).
- **Cenas:** `hex_phase.tscn` (câmara de partida com os terminais copiados da `sala_entrada`), `player.tscn` (destino do `RestartOnDeath`), cena principal do projeto → `hex_phase.tscn` (altera o `project.godot`: pedir autorização), `sala_entrada.tscn` vira bancada.

## 7. Passos no editor (proposto)

A detalhar na implementação, com nomes exatos e todos os campos a ligar. Em linhas gerais:

1. Copiar os terminais da `sala_entrada` (`TerminalModo`, `EvolutionTerminal` e o `EvolutionUi` com o `UpgradePanel`) para a câmara de partida na `hex_phase.tscn` (selecionar, Ctrl+C, abrir a fase, Ctrl+V) e ligar o **Panel** do `EvolutionTerminal`.
2. Montar o terminal "Liberar saídas" e conectar o sinal dele à abertura das saídas.
3. Ligar no Inspector da raiz `HexPhase` a câmara de partida (campo novo).
4. `player.tscn` > `RestartOnDeath`: **Return Scene Path** = `hex_phase.tscn`.
5. Eclusa: **Target Scene Path** = `hex_phase.tscn` (extração = recarregar a fase em modo câmara, D2).
6. **Projeto > Configurações do Projeto > Aplicação > Executar > Cena Principal** = `hex_phase.tscn`.

## 8. Pronto quando

- [ ] `dotnet build` sem erros
- [ ] F5 abre a fase no nó inicial em modo câmara: saídas fechadas, terminais visíveis, paredes do nó sem irritar
- [ ] O terminal de modo e o de evolução funcionam no nó, sem um aperto acionar outro terminal
- [ ] "Liberar saídas" abre os portões, faz os terminais sumirem e as paredes do nó voltam a reagir; o nó passa a ser um nó comum
- [ ] Morrer escurece e recarrega a fase em modo câmara, com a taxa da morte depositada no banco
- [ ] Ser ejetado tem a consequência decidida em D3
- [ ] A extração pela eclusa (se D2 = A) deposita 100% e recarrega em modo câmara; com o nó travado, a eclusa não age
- [ ] Recargas seguidas sem erros no Output e com "Nós Órfãos" = 0

## 9. Como testar (roteiro manual no editor)

1. F5: confira o modo câmara (tente sair pelas bocas e irritar as paredes do nó).
2. Use os dois terminais do traje; feche o painel de evolução com o "A" dentro da área do terminal de modo e confira que o modo não muda.
3. Libere as saídas; saia por um corredor e volte (o nó é comum, sem terminais).
4. Morra, seja ejetado e extraia (se houver extração), conferindo o banco a cada volta.
5. Repita 5 vezes olhando o Output e "Nós Órfãos".

## 10. Decisões em aberto e notas

### Decisões para o dev (com a recomendação da análise de out/2026)

| # | Decisão | Opções | Recomendação |
|---|---|---|---|
| D1 | Qual é o "primeiro nó" | O nó de entrada (`EntryPeripheral`, hoje o nó 2) ou outro | O nó de entrada: já tem a eclusa e o nascimento |
| D2 | Extração (sair vivo com 100%) | **A:** a eclusa continua; depois de liberar, tocar a porta recarrega a fase em modo câmara e deposita 100%. **B:** sem extração; a incursão só termina na morte ou na ejeção (50%) | A: mantém o risco × recompensa (sair vivo guarda tudo, morrer guarda metade) |
| D3 | Consequência da ejeção | Igual à morte (taxa e reinício), reinício sem perda, ou outra fração | Igual à morte: um fluxo só. Resolve a decisão em aberto do backlog |
| D4 | Paredes do nó travado | Reativas ou inertes até liberar | Inertes, como o titânio da câmara atual |
| D5 | Onde documentar | Spec própria ou revisar a `hex-phase.md` | Esta spec; a `hex-phase.md` registra só que a H5 foi revista |
| D6 | Polling do `TerminalModo` (dívida do backlog) | Manter ou corrigir na mudança | Corrigir para `_UnhandledInput` (três terminais na mesma sala) |

Também confirmar: sem terminais depois de liberar, não há compra de melhorias no meio da incursão (parece intencional).

### Análise (out/2026)

- **A favor:** a partida inteira numa cena (H2), sem a troca de cena da câmara; começo dentro do mundo (refúgio selado no organismo; liberar as saídas é o momento em que ele percebe o jogador); um evento claro de início da incursão para os spawners (1.4) e para a dificuldade.
- **Riscos de design:** com a ejeção encerrando a incursão (D3), todo o progresso da incursão se perde ao ser ejetado; com paredes que ejetam em 1,5 a 5 s de luz, isso pune bastante (ligado à sensibilidade das paredes, em aberto).
- **Viabilidade:** alta. Reaproveita a fase B inteira (reinício por recarga da cena com `ScreenFade`, `GameState` persistente, posicionamento no nó de entrada pelo construtor, ponto de troca da regra do espaço no ouvinte do `LostInSpace`).

### Arquitetura proposta

- **Portões** gerados pelo `HexPhaseBuilder` (contornos por código, H3), fora do grupo `Walls`, e um método público para abrir as saídas (remove os portões e torna as paredes do nó reativas).
- **Terminal "Liberar saídas"** só emite um sinal (Observer); quem reage é a câmara e a fase.
- **Câmara de partida** como contêiner do editor posicionado pelo construtor (como a eclusa): aparência no editor, posição no código.
- **Consequência da ejeção** como componente trocável, ouvinte do `LostInSpace` (ideia do Strategy: trocar a regra é trocar o componente, sem mexer no rastreador).
- **O estado "travado" não precisa ser guardado:** a fase recarregada nasce em modo câmara, que é o estado inicial da cena.
- **Cuidado:** o `HexPhaseBuilder` só constrói e abre; o fluxo da partida fica nos componentes novos, para não virar uma "classe Deus".

### Plano de execução (proposto)

0. Commit do estado atual.
1. Decisões D1 a D6 e aprovação desta spec; item no backlog à frente do 1.4.
2. Portões e paredes inertes (`HexPhaseLayout`, `HexPhaseBuilder`).
3. Terminal "Liberar saídas" ligado à abertura.
4. Câmara de partida (terminais copiados da `sala_entrada`; depósito ao começar; sumiço ao liberar; correção do `TerminalModo`).
5. Reinício, ejeção e extração (`RestartOnDeath` → fase; componente da ejeção; eclusa como extração ligada só depois de liberar).
6. Cena principal → `hex_phase.tscn` (autorizar a mudança no `project.godot`); `sala_entrada` vira bancada; teste do ciclo completo.
7. Documentação (abaixo) e mensagem de commit.

Cada passo segue o fluxo do projeto: verificação no descartável, apresentação ao dev, autorização, `dotnet build` e roteiro no editor.

### Documentos a atualizar quando for implementada (conflitos)

- `hex-phase.md`: H5 revista (câmara dentro da fase), regra provisória do espaço substituída, eclusa como extração.
- `suit-evolution.md`: terminais e depósito no nó inicial; destino da `PortaSaida`.
- `player-health-death.md`: a morte volta ao nó inicial; decisão 7 (ejeção).
- `reactive-walls.md` (D5) e `dyson-wall.md` (2.6): consequência da ejeção decidida.
- `BACKLOG.md`: decisão da ejeção resolvida; o 1.4 escuta `IncursionStarted`; dívida do `TerminalModo` resolvida (se D6).
- `HANDOFF.md`: seções 3.7, 4 e 6.7 e a lista de cenas.
- `CLAUDE.md`: estrutura, cena principal, câmara de partida e a `sala_entrada` como bancada.
