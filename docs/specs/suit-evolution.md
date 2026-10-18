# Spec: Evolução do traje (gastar biomassa na câmara inicial)

- **Status:** validada (out/2026). Valores em teste. A melhoria da lanterna está adiada (seção 10).
- **Etapa:** 1 (mecânica)
- **Depende de:** `docs/specs/biomass-collection.md` (contador de biomassa), `docs/specs/player-health-death.md` (morte e fluxo provisório de recarregar a sala).

> **Valores em teste:** como nas demais specs da demo, os `[Export]` e os números desta spec são pontos de partida, não valores definitivos.

## 1. Objetivo

Fechar o core loop: a biomassa coletada no combate é gasta, na câmara inicial (`sala_entrada.tscn`), em melhorias permanentes do traje. Morrer devolve o cientista à câmara, levando parte da biomassa. Cada incursão deixa o próximo mergulho um pouco mais forte. Serve ao trecho EVOLUIR do core loop e substitui o fluxo provisório da morte (recarregar a sala).

## 2. Comportamento esperado

### Biomassa carregada e banco

- Existem dois saldos: a **biomassa carregada** (a que o jogador coletou na incursão atual, mostrada no HUD) e o **banco** (a biomassa guardada, usada para comprar melhorias).
- A biomassa carregada sobrevive à troca de sala dentro da mesma incursão (hoje ela zera, porque o jogador é recriado em cada sala).
- Quando o jogador morre, então volta à câmara inicial e `DeathKeepFraction` da biomassa carregada (arredondado para baixo) vai para o banco; o resto se perde. A biomassa carregada volta a zero.
- Quando o jogador entra na câmara inicial vivo (decisão em aberto 3), então toda a biomassa carregada vai para o banco.

### Morte e saída da câmara

- Quando o jogador morre, então, depois de `DeathRestartDelay`, a cena carregada é a câmara inicial (`sala_entrada.tscn`), em vez de recarregar a sala atual.
- Quando o jogador entra na porta de saída da câmara (`PortaSaida`), então a cena carregada é a sala de combate configurada no Inspector (hoje, `sala_ninho.tscn`).
- Ao entrar em qualquer sala, o traje começa com a integridade cheia (decisão em aberto 6).

### Terminal de evolução

- A câmara inicial tem um terminal de evolução, no mesmo estilo do `TerminalModo` (área + tecla de interação).
- Quando o jogador está no terminal e aperta interagir, então abre um painel simples com o saldo do banco e, para cada melhoria: nome, nível atual, nível máximo, custo do próximo nível e um botão de compra.
- Quando o jogador compra uma melhoria com saldo suficiente, então o custo sai do banco, o nível sobe e o efeito vale imediatamente e em todas as salas seguintes.
- Quando o saldo não basta ou a melhoria já está no nível máximo, então a compra é recusada (botão desabilitado).
- Fechar o painel devolve o controle ao jogador.

### Melhorias iniciais

- **Integridade do traje:** aumenta `MaxIntegrity` do `SuitIntegrity`.
- **Ímã de biomassa:** aumenta `MagnetRadius` do `BiomassCollector`.
- **Potência da lanterna:** **adiada** (out/2026) até ter requisitos bem definidos. Quando for definida, basta criar o recurso e incluí-lo no catálogo.
- As melhorias são aplicadas por um componente do jogador ao entrar em cada sala, lendo os níveis comprados. Nenhum dos componentes afetados (`SuitIntegrity`, `BiomassCollector`) precisa saber que melhorias existem.

## 3. Fora do escopo

- Salvar o progresso em disco (o progresso vale enquanto o jogo estiver aberto; decisão em aberto 2).
- Gerador de labirintos, várias salas encadeadas e a sala final (`sala_nucleo`).
- Porta de volta da sala de combate para a câmara (extração), salvo se a decisão em aberto 3 pedir.
- Integridade que persiste entre salas da mesma incursão.
- Tiros, novas armas, modos novos de lanterna.
- Arte, áudio, animação e layout final do painel (etapa 2). O painel é funcional, com nós padrão de UI.
- Balanceamento final dos custos e efeitos.

## 4. Parâmetros ajustáveis (valores em teste)

Pontos de partida aceitos pelo dev, para ajustar depois.

| Parâmetro | Valor inicial | Onde fica | Observação |
|---|---|---|---|
| `DeathKeepFraction` | 0,5 | estado da partida | Fração da biomassa carregada que vai para o banco na morte |
| Cena de destino da morte | `sala_entrada.tscn` | componente de morte (`RestartOnDeath`) | Vazio = recarregar a sala atual (comportamento de hoje) |
| Cena de destino da `PortaSaida` | `sala_ninho.tscn` | porta (Inspector) | |
| Integridade do traje | +20 por nível, 3 níveis, custos 20 / 40 / 80 | definição da melhoria | 100 → 160 |
| Ímã de biomassa | +30 px por nível, 3 níveis, custos 15 / 30 / 60 | definição da melhoria | 80 → 170 px |
| Potência da lanterna (adiada) | proposta: +10 por nível, 3 níveis, custos 25 / 50 / 100 | — | Fora do catálogo até ter requisitos definidos |

Estimativa de ritmo (só para referência): cada ancestral rende 4 de biomassa. Uma incursão de ~2 minutos na `sala_ninho` matando ~30 ancestrais coleta ~120; morrendo, ~60 vão para o banco. Comprar as 2 melhorias atuais custa 245, ou seja, ~4 incursões (420 e ~7 incursões quando a da lanterna entrar).

## 5. Casos de borda

- Rodar uma sala de combate direto (F6) sem passar pela câmara: tudo funciona com o estado inicial (banco 0, sem melhorias); ao morrer, vai para a câmara.
- Morrer com 0 de biomassa carregada: nada vai para o banco, sem erro.
- Arredondamento: 7 × 0,5 = 3 (para baixo).
- Comprar sem saldo ou acima do nível máximo: recusado, sem mudar nada.
- Melhoria comprada com o jogador já na câmara: o efeito vale na hora (ex.: o HUD de integridade mostra o novo máximo).
- Trocas de cena repetidas (morrer e voltar várias vezes): o estado da partida não pode duplicar nem perder valores; "Nós Órfãos" = 0.
- O estado da partida sobrevive às trocas de cena; os registros estáticos (`FractalSwarm`, `BiomassField`) continuam se limpando pelo `_ExitTree`.
- `TerminalModo` e terminal de evolução coexistem na câmara; a tecla de interação só age no terminal em que o jogador está.
- Painel aberto: o jogador não deve se mover nem disparar a lanterna enquanto escolhe (decisão em aberto 5).

## 6. Arquivos e cenas

- Novos:
  - `RunData.cs` (classe C# comum): só dados da partida — banco, biomassa carregada e níveis das melhorias por `Id`. Separada do nó para facilitar salvar em disco no futuro.
  - `GameState.cs` (**autoload** `GameState`): dono do `RunData`, vivo entre as cenas. `AddCarried`, `ApplyDeathPenalty`, `DepositCarried`, `CanBuy`, `TryBuyUpgrade`; sinais `BankChanged` e `UpgradeLevelChanged`; atalho `GameState.Instance`.
  - `DoorTrigger.cs` (`Area2D`): troca para a cena do Inspector quando o jogador entra (troca adiada, com trava contra troca dupla).
  - `SuitUpgrade.cs` (`Resource`, `[GlobalClass]`): definição de uma melhoria (`Id`, `DisplayName`, `Target`, `ValuePerLevel`, `Costs`; o nível máximo é o tamanho de `Costs`).
  - `UpgradeCatalog.cs` (`Resource`, `[GlobalClass]`): lista única de melhorias, usada pelo painel e pelo aplicador.
  - `EvolutionTerminal.cs` (`Area2D`): abre o painel com a ação `interact`, reagindo ao **evento** em `_UnhandledInput` (não por polling), para o aperto que a interface já usou não reabrir o painel.
  - `UpgradePanel.cs` (`PanelContainer`): cria um botão por melhoria a partir do catálogo, mostra o banco, pausa o jogo enquanto aberto (`ProcessMode = Always`), fecha com o botão "Fechar" ou `ui_cancel`; conecta e desconecta os sinais do `GameState`.
  - `SuitUpgradeApplier.cs` (`Node`, filho do `Player`): recalcula do zero `base + nível × valor` ao nascer e a cada compra (idempotente), somando os bônus por alvo.
  - Dados: pasta `upgrades/` com `upgrade_suit_integrity.tres`, `upgrade_magnet.tres` e `upgrade_catalog.tres`.
- Alterados:
  - `BiomassCollector.cs`: o total passa a ser a biomassa carregada do `GameState` (sobrevive à troca de sala); o aviso inicial ao HUD é adiado.
  - `RestartOnDeath.cs`: `ReturnScenePath` (vazio = recarregar a sala) e `DeathKeepFraction`; cobra a taxa na hora da morte.
  - `SalaEntrada.cs` (desvio aprovado): deposita a biomassa carregada no banco ao entrar na câmara.
  - `SuitIntegrity.cs` (desvio aprovado): `SetMaxIntegrity(int)` preserva o dano já sofrido e avisa o HUD.
  - `project.godot`: autoload `GameState`; `ui_accept` com o botão 0 do controle (A) e `ui_cancel` com o botão 1 (B).
- Somente leitura: `LightDamageArea.cs`, `TerminalModo.cs`.

## 7. Passos no editor (feitos pelo dev)

Feitos em out/2026:

1. **Autoload:** Projeto > Configurações do Projeto > Globais > Autoload: `res://GameState.cs` com o nome `GameState`.
2. **Mapa de Entrada** (com "Mostrar Ações Integradas"): `ui_accept` + botão 0 do controle; `ui_cancel` + botão 1 do controle. Na Godot 4.7 essas ações não têm botões de controle por padrão.
3. **`player.tscn`:** `RestartOnDeath` com `Return Scene Path` = `res://sala_entrada.tscn` e `Death Keep Fraction` = 0.5; nó `SuitUpgradeApplier` (script `SuitUpgradeApplier.cs`) com `Catalog`, `Integrity` e `Collector` ligados.
4. **`sala_entrada.tscn`:**
   - `PortaSaida` com `DoorTrigger.cs`, `Target Scene Path` = `res://sala_ninho.tscn` e um `CollisionShape2D` (retângulo 60×250 em (1930, 574)) cobrindo a abertura da parede direita.
   - `EvolutionTerminal` (`Area2D` em (600, 250), círculo de raio 60, `ColorRect` magenta), com `Panel` ligado ao `UpgradePanel`.
   - `EvolutionUi` (`CanvasLayer`, `Layer` = 2) > `UpgradePanel` (âncora no centro, tamanho mínimo 700×300) > `Content` (`VBoxContainer`) > `BankLabel`, `UpgradeList` (vazio; os botões são criados pelo código) e `CloseButton` ("Fechar"). `UpgradePanel` com `Catalog`, `BankLabel`, `UpgradeList` e `CloseButton` ligados.
5. **Recursos:** pasta `upgrades/` com as 2 melhorias e o catálogo (salvos pelo disquete do Inspector; o Ctrl+S salva a cena, não o recurso).

## 8. Pronto quando

- [x] `dotnet build` sem erros
- [x] Da câmara, a `PortaSaida` leva à `sala_ninho`; a biomassa carregada continua no HUD depois da troca de sala
- [x] Ao morrer, o jogador volta à câmara e `DeathKeepFraction` da biomassa carregada (para baixo) vai para o banco
- [x] O terminal de evolução abre o painel com o saldo do banco e as melhorias do catálogo (nível, máximo, custo), com mouse, teclado e controle
- [x] Comprar desconta do banco, sobe o nível e o efeito vale na hora e nas salas seguintes (integridade no HUD, ímã maior)
- [x] Compra sem saldo ou no nível máximo é recusada
- [x] Banco e níveis sobrevivem a várias mortes seguidas, sem duplicar nem perder valores
- [x] Trocas de cena repetidas sem erros no Output e com "Nós Órfãos" = 0
- [x] Custos e efeitos das melhorias ajustáveis no Inspector

## 9. Como testar (roteiro manual no editor)

1. Rode a `sala_entrada.tscn` (F6). Passe pela `PortaSaida` e confirme a `sala_ninho`.
2. Colete biomassa, anote o valor do HUD e morra (para testar rápido, `MaxIntegrity` baixo). Confirme a volta à câmara e o banco com metade (para baixo).
3. Abra o terminal de evolução: confira saldo, níveis e custos. Compre uma melhoria barata e confirme o desconto e o efeito (ex.: HUD de integridade com o novo máximo).
4. Tente comprar sem saldo e acima do nível máximo: recusado.
5. Repita incursões e mortes 3 ou 4 vezes: banco e níveis acumulam certo; Output sem erros; "Nós Órfãos" = 0.

## 10. Decisões e notas

### Decididas pelo dev (out/2026)

1. Estado da partida: **autoload** (opção a).
2. Persistência: só enquanto o jogo estiver aberto, **mas a implementação deve facilitar salvar em disco depois**: os dados da partida ficam numa classe de dados separada do nó, identificados por chaves estáveis (o `Id` de cada melhoria, não a posição na lista).
3. Biomassa na morte: **guardar metade** (`DeathKeepFraction` = 0,5), confirmado pelo dev. Modelo: a morte cobra a "taxa" (perde `1 − DeathKeepFraction` da biomassa carregada) e a câmara inicial deposita no banco tudo o que estiver carregado.
4. Melhorias iniciais: integridade do traje e ímã de biomassa. A potência da lanterna foi **adiada** pelo dev (out/2026) até ter requisitos bem definidos. Outras entram conforme surgir a necessidade. O valor `LightDamagePerSecond` foi removido do enum `SuitUpgrade.TargetStat` (decisão do dev: não deixar no Inspector uma opção sem efeito); volta junto com a melhoria da lanterna.
5. Painel de compra: painel com botões, pausando o jogo enquanto aberto.
6. Integridade cheia ao entrar em cada sala.
7. Melhorias como recurso (`Resource`) editável no Inspector, numa pasta própria.
8. Criar o `DoorTrigger.cs` mínimo agora.
9. Valores da seção 4: pontos de partida, ajustar depois.

### Notas de implementação (out/2026)

- **Controle na interface:** na Godot 4.7, `ui_accept` e `ui_cancel` não têm botões de controle por padrão (verificado direto no motor); foram mapeados no projeto. Com isso, o botão A passa a ser `ui_accept` e `interact` ao mesmo tempo: por isso o terminal reage ao evento em `_UnhandledInput` (a interface consome o aperto que usou) em vez de ler a tecla por polling.
- **Ordem do `_Ready`:** filhos ficam prontos antes dos pais. O coletor lê a biomassa do `GameState` só no fim do frame (depois do depósito feito pela `SalaEntrada`), e o aplicador aplica as melhorias de forma adiada (depois do `SuitIntegrity` ficar pronto).
- **Para salvar em disco no futuro:** converter só o `RunData` (banco, carregada, níveis por `Id` e modo da lanterna); os `Id` das melhorias não devem mudar depois de lançados.
- **Possível evolução do aplicador:** hoje um `switch` com 2 alvos. Se os alvos crescerem ou ganharem regras próprias, trocar por uma interface por alvo (padrão Strategy).
- **Modo da lanterna em todas as salas (backlog 1.1, out/2026):** o modo escolhido no `TerminalModo` passou a viver no `RunData` (`IsManualFire`, padrão manual) e só muda por `GameState.ToggleFlashlightMode()`, que emite `FlashlightModeChanged(bool)`. O `Player` lê o modo ao nascer e escuta o sinal (Observer); `IsManualFire` deixou de ser `[Export]` e passou a `private set`. O `TerminalModo` não guarda mais referência ao jogador e, ao carregar a câmara, pinta o quadrado com o modo guardado (ciano = manual, amarelo = automático). Por isso o `TerminalModo.cs` deixou de ser somente leitura (seção 6). Testado pelo dev: troca ao vivo, `sala_ninho` e volta pela morte, sem erros no Output.

### Alternativas registradas na proposta original

1. **Onde vive o estado da partida (precisa sobreviver à troca de cena).** (a) **Autoload** (proposto): um nó global que a Godot cria uma vez e mantém vivo entre as cenas; é o recurso padrão da Godot para estado de jogo, aparece na árvore Remota para depuração e pode emitir sinais. Exige registrar o script em Projeto > Configurações do Projeto > Globais (altera o `project.godot`). (b) Classe estática em C#, como `FractalSwarm` e `BiomassField`: não mexe no `project.godot`, mas fica invisível no editor.
2. **Persistência.** Proposto: só enquanto o jogo estiver aberto (sem salvar em disco), suficiente para a demo e para testar o loop. Salvar em disco fica para uma spec própria.
3. **O que acontece com a biomassa na morte.** (a) Guardar uma fração (`DeathKeepFraction` = 0,5) (proposto: progresso garantido, morte ainda custa); (b) guardar tudo (morrer só custa tempo); (c) perder tudo e só guardar ao voltar vivo à câmara (tensão de "extração", mas exige uma porta de volta na sala de combate, que hoje não existe).
4. **Melhorias iniciais.** Proposto: integridade, ímã e potência da lanterna (todas usam valores que já são `[Export]`). Outras candidatas: imunidade após golpe (`HitInvulnerability`), empuxo (`ThrustForce`), velocidade máxima (`MaxSpeed`), abertura do feixe.
5. **Painel de compra.** Proposto: painel simples com botões (funciona com mouse e com foco de controle/gamepad), pausando o movimento do jogador enquanto aberto. Alternativa: compra por teclas (1, 2, 3) sem painel, só com texto.
6. **Integridade ao entrar numa sala.** Proposto: cheia a cada sala, por enquanto (só existe uma sala de combate). Integridade que persiste entre salas da incursão fica para quando houver várias salas encadeadas.
7. **Definição das melhorias como recurso (`Resource`).** Proposto: cada melhoria é um arquivo de recurso editável no Inspector (nome, alvo, valor por nível, custos), em vez de números no código. É o "design orientado a dados": adicionar uma melhoria nova vira criar um arquivo, não escrever código. Criar a pasta dos recursos (ex.: `upgrades/`) precisa da autorização do dev.
8. **`DoorTrigger.cs`.** O handoff cita esse script para a `PortaSaida`, mas ele não existe no projeto. Proposto: criá-lo agora, mínimo (destino configurável no Inspector), como base para a transição entre salas no futuro.
