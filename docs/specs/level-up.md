# Spec: Level up na incursão (biomassa vira experiência)

- **Status:** rascunho (out/2026), a aprovar pelo dev. Decisões L1 a L21 na seção 10: as respostas do dev às perguntas da análise do documento de origem.
- **Etapa:** 1 (mecânica)
- **Origem:** `docs/reference/documentacao_level_up_demo_atualizada.md` (documento de design do dev, independente de engine). Onde esta spec diverge dele, vale esta spec: as diferenças vêm das respostas do dev (seção 10).
- **Depende de:** `docs/specs/biomass-collection.md` (orbes e ímã), `docs/specs/suit-evolution.md` (melhorias `SuitUpgrade`, aplicador e banco, que fica pronto para outra moeda), `docs/specs/player-health-death.md` (integridade e morte), `docs/specs/start-chamber.md` (início da incursão), `docs/specs/mobile-spawners.md` (biomassa do spawner), `docs/specs/force-field.md` (o campo de força, um dos atributos).

> **Valores em teste:** como nas demais specs da demo, os `[Export]` e os números desta spec são pontos de partida, não valores definitivos.

## 1. Objetivo

Durante a incursão, a biomassa coletada vira experiência, e cada nível novo dá um ponto de melhoria. O jogador gasta os pontos quando quiser, num menu que pausa o jogo e oferece 3 melhorias sorteadas. A build se monta ao longo da incursão e zera na próxima. Liga o COLETAR ao EVOLUIR dentro da própria incursão (inspiração: Vampire Survivors e Nova Drift).

## 2. Comportamento esperado

### Experiência e nível

- Quando o jogador coleta orbes, então cada unidade de biomassa soma 1 de experiência (XP). Com os valores atuais, um ancestral do inimigo rende 4, e um spawner, 20. Orbes sugados pela fenda ou que somem pelo tempo de vida não dão XP.
- O jogador começa a incursão no nível 1, com 0 de XP e 0 pontos.
- Quando a XP do nível chega ao requisito do próximo, então o jogador sobe de nível, ganha 1 ponto de melhoria, e a XP que sobrou passa para o nível seguinte.
- Quando uma coleta cruza mais de um requisito, então todos os níveis são processados, com 1 ponto por nível.
- **Requisito de cada nível:** `FirstLevelXp` para o 1 → 2. Depois, cada requisito é o anterior mais um acréscimo, que começa em `FirstIncrement` e cresce `IncrementGrowth` por nível. Com 10, 5 e 1:
  - 10, 15, 21, 28, 36, 45, 55, 66 e 78 (a tabela do documento de origem, até o nível 10);
  - e segue sem teto: 91, 105…
- Subir de nível não pausa nem abre o menu.

### HUD

- No lugar do rótulo "Biomassa: N", uma linha com três indicadores separados:
  - o nível ("Nível 3");
  - a barra de XP: XP do nível ÷ requisito do próximo (5 de 10 = metade);
  - os pontos ("Pontos: 2").
- A barra atualiza a cada coleta e a cada nível. Ganhar ou gastar pontos não mexe na barra.

### Menu de melhorias

- Quando o jogador aperta `upgrade_menu` (Tab, ou o Y do controle) com pelo menos 1 ponto, vivo e com o jogo rodando, então o menu abre e o jogo pausa. Sem pontos, nada acontece.
- O menu mostra os pontos e até `OptionsPerChoice` (3) cartas, sorteadas sem repetir entre as melhorias elegíveis (abaixo do nível máximo).
  - Com menos de 3 elegíveis, mostra só as que existem.
  - Sem nenhuma elegível, mostra "Traje no máximo" e não gasta ponto.
- Cada carta mostra:
  - o nome e "Nível N → N+1";
  - a descrição;
  - o efeito antes → depois: em % do valor-base para potência, alcance e ímã; em número para integridade e cargas.
  - Sem ícone na etapa 1.
- Funciona com mouse, teclado e controle (foco nos botões, como no painel do banco).
- Quando o jogador escolhe uma carta, então gasta 1 ponto, o nível da melhoria sobe e o efeito vale na hora.
  - Com pontos sobrando, um novo sorteio aparece no menu.
  - Sem pontos, o menu fecha e o jogo volta.
- Quando o jogador fecha o menu sem escolher ("Fechar", `ui_cancel` ou `upgrade_menu` de novo), então o jogo volta e o ponto continua guardado.
- Quando o jogador reabre o menu sem ter escolhido, então vê as mesmas cartas: o sorteio só muda depois de gastar o ponto.
- Uma escolha vale para uma carta só: cliques repetidos, aperto mantido e eventos duplicados não gastam mais de um ponto nem aplicam o efeito duas vezes.

### Atributos (nível 1 a 5)

| Atributo (documento) | Nome no jogo | Por nível | Nível 1 → 5 | O que muda no código |
|---|---|---|---|---|
| Vida | Integridade do traje | +20 | 100 → 180 | `SuitIntegrity.MaxIntegrity`, com cura de 20 (abaixo) |
| Coletor magnético | Ímã de biomassa | +25% do raio-base | 80 → 160 px | `BiomassCollector.MagnetRadius` (não o `PickupRadius`) |
| Dano da lanterna | Potência da lanterna | +25% | 100% → 200% | Da `LightDamageArea`: `DamagePerSecond` (35 → 70/s; inimigos e spawners) e `WallIrritationPerSecond` (20 → 40/s) |
| Alcance da lanterna | Alcance da lanterna | +15% | 100% → 160% | O cone do feixe (~220 → ~352 px, mesma abertura) e a luz visível (`PointLight2D`), juntos |
| Campo de força | Campo de força | +1 carga | 1 → 5 cargas | `ForceField` (spec `force-field.md`) |

- **Integridade:** o máximo sobe 20, e a integridade atual também (cura de 20), sem passar do máximo.
  - É o `SetMaxIntegrity` de hoje, que mantém o dano já sofrido: 30/100 vira 50/120.
  - É a única cura do jogo, uma exceção a "a integridade não regenera" (`player-health-death.md`).
- **Potência:** no nível 5, a fenda das paredes abre na metade do tempo. Risco × recompensa, decidido pelo dev (L12).

  | Estrutura | Nível 1 | Nível 5 |
  |---|---|---|
  | Nós e painéis | ~5 s | ~2,5 s |
  | Conectores de nós | ~2 s | ~1 s |
  | Conectores de placa | ~1,5 s | ~0,75 s |

- **Alcance:** o cone cresce mantendo o ângulo. A largura do feixe a cada distância fica igual, e o feixe passa a alcançar inimigos e paredes mais longe.

### Incursão e reinício

- A "partida" do documento de origem é a **incursão**. Ela começa quando a fase carrega (câmara de partida) e termina na morte, na ejeção ou na extração, que recarregam a fase.
- A cada incursão nova, voltam ao início o nível, a XP, os pontos, as cartas guardadas e os níveis dos atributos; os atributos voltam aos valores-base.
- O modo da lanterna (`RunData`) não é atributo e continua guardado.
- Não há progressão permanente nesta versão. O banco fica pronto para isso (abaixo), ainda sem moeda.

### Banco reservado para melhorias permanentes (créditos)

- A biomassa deixa de ir para o banco.
- O banco continua no código, pronto para uma moeda futura: provisoriamente "créditos" (`Credits`; ex.: créditos colhidos no jogo).
- Ainda não há fonte de créditos, então o saldo fica em 0. O fluxo continua o mesmo, em créditos: o que é carregado vai para o banco; a câmara e a extração depositam 100%; a morte guarda `DeathKeepFraction`.
- O catálogo do banco fica vazio até o dev definir as melhorias permanentes. As duas de hoje (integridade e ímã) passam para o level up.
- O terminal de evolução sai da câmara de partida e fica só na `sala_entrada` (bancada), mostrando "Créditos: N".
- Quando houver melhorias permanentes, os bônus delas somam com os do level up no mesmo atributo: o aplicador junta as duas fontes.

## 3. Fora do escopo

- Melhorias especiais e sinergias (documento de origem, seção 9): feixe concentrado, feixe ampliado, luz persistente, repulsão reforçada, recuperação acelerada, pulso defensivo, coleta eficiente.
- A moeda permanente (de onde vem, quanto vale) e as melhorias permanentes: só o banco fica pronto.
- Salvar em disco.
- Arte (ícones, cartas, barra animada, efeito de level up), áudio e layout final (etapa 2). Menu e HUD usam nós padrão de UI.
- Balanceamento final da curva e dos atributos (backlog 1.9).
- Abrir o menu sozinho ao subir de nível.

## 4. Parâmetros ajustáveis (valores em teste)

| Parâmetro | Valor inicial | Onde fica | Observação |
|---|---|---|---|
| `FirstLevelXp` | 10 | `ExperienceTracker` | Requisito do nível 1 → 2 |
| `FirstIncrement` | 5 | `ExperienceTracker` | Acréscimo do 2 → 3 |
| `IncrementGrowth` | 1 | `ExperienceTracker` | Quanto o acréscimo cresce por nível |
| `OptionsPerChoice` | 3 | `IncursionUpgrades` | Cartas por escolha |
| Integridade do traje | +20 fixo, 4 níveis | `upgrade_suit_integrity.tres` | |
| Ímã de biomassa | +25% do valor-base, 4 níveis | `upgrade_magnet.tres` | |
| Potência da lanterna | +25%, 4 níveis | `upgrade_flashlight_power.tres` (novo) | Dano e irritação |
| Alcance da lanterna | +15%, 4 níveis | `upgrade_beam_range.tres` (novo) | Feixe e luz |
| Campo de força | +1 carga, 4 níveis | `upgrade_force_field.tres` (novo) | Carga inicial e recarga na `force-field.md` |
| Ação `upgrade_menu` | Tab; botão 3 do controle (Y) | Mapa de Entrada (`project.godot`) | O `interact` usa E e A |
| XP por orbe | 1 | `BiomassPerKill` (`FractalEnemy`), `DeathBiomass` (`MobileSpawner`) | Sem mudança: 4 por ancestral, 20 por spawner |

Cada nível de melhoria custa 1 ponto: no catálogo do level up, `Costs` = [1, 1, 1, 1]. A lista dá o nível máximo, como no banco (seção 10, arquitetura).

**Ritmo (referência para o 1.9):**
- o nível 10 pede 354 XP no total: os 10 spawners rendem 200, e o resto são ~39 ancestrais;
- os 5 atributos no máximo pedem 20 pontos, ou seja, o nível 21 e 2290 XP: os 10 spawners mais ~523 ancestrais.

## 5. Casos de borda

- **Coleta grande num passo só:** os 20 orbes de um spawner, por exemplo, com `FirstLevelXp` em 2, cruzam vários níveis. Todos os níveis e pontos entram de uma vez, a sobra é preservada e a barra mostra o resto.
- **Menu aberto:** o jogo pausa. Enquanto o jogador escolhe, não há coleta, golpe, morte nem recarga do campo.
- **Jogador morto** (no atraso antes da recarga): o menu não abre.
- **Troca de cena em andamento** (o escurecer da extração, de 0,8 s): se a pausa parar o escurecer, a troca espera o menu fechar. Verificar no motor; se for o caso, bloquear a abertura durante a troca.
- **Outro painel pausando o jogo** (o terminal de evolução da bancada): o menu não abre por cima.
- **Aperto mantido ou repetição do teclado (eco) depois de escolher:** não pode escolher uma carta do novo sorteio sem um aperto novo. Verificar no motor no passo do menu.
- **Todas no máximo, com pontos sobrando:** o menu mostra "Traje no máximo", e os pontos continuam no HUD.
- **Integridade em 1 de 100 ao escolher a integridade:** vai a 21/120.
- **Escolher outra melhoria não mexe na integridade:** o aplicador recalcula tudo, mas o máximo não muda, então não cura.
- **Alcance com o jogo rodando:** a forma do feixe muda na hora. Verificar no motor:
  - que a área percebe os corpos dentro do cone novo (inimigos já iluminados, paredes), sem esperar o feixe se mexer;
  - que o peso da luz nas paredes (`BeamWeightAt`) usa a forma nova.
- **Bancadas** (`sala_entrada`, `sala_ninho`, `arena`): o level up funciona em qualquer cena com o jogador, porque os componentes ficam no `player.tscn`, e zera a cada carga de cena. Passar da `sala_entrada` para a fase é uma incursão nova.
- **Recargas seguidas:** sem erros no Output e com "Nós Órfãos" = 0. Os sinais ligados por código a nós globais são desligados no `_ExitTree`.

## 6. Arquivos e cenas

Mapa dos nomes conceituais do documento de origem para o projeto:

| Documento | Projeto |
|---|---|
| `ExperienceManager` | `ExperienceTracker` (novo) |
| `UpgradeManager` | `IncursionUpgrades` (novo) |
| `UpgradeDefinition` | `SuitUpgrade` (existente, ampliado) |
| `PlayerStats` | `SuitUpgradeApplier` (existente, ampliado) |
| `LevelUpUI` | `LevelUpPanel` (novo) e `ExperienceHud` (novo) |

- **Novos** (criar com autorização, no passo do plano):
  - `ExperienceTracker.cs` (`Node`, filho do `Player`):
    - guarda XP, nível e pontos;
    - métodos `AddExperience` e `TrySpendPoint`;
    - sinais `ExperienceChanged(level, xpInLevel, xpForNext)` e `UpgradePointsChanged(points)`.
  - `ExperienceHud.cs` (no `Hud` do `player.tscn`): nível, barra e pontos; só escuta os sinais.
  - `IncursionUpgrades.cs` (`Node`, filho do `Player`):
    - guarda os níveis dos atributos na incursão (por `Id`) e as cartas sorteadas;
    - faz a escolha, gastando o ponto no `ExperienceTracker`;
    - sinal `UpgradeLevelChanged(id, level)`.
  - `LevelUpPanel.cs` (`PanelContainer`, num `CanvasLayer` do `player.tscn`): abre com `upgrade_menu`, pausa, monta as cartas, envia a escolha e fecha.
  - Recursos:
    - `upgrades/levelup_catalog.tres` (catálogo do level up);
    - `upgrades/upgrade_flashlight_power.tres`;
    - `upgrades/upgrade_beam_range.tres`;
    - `upgrades/upgrade_force_field.tres`.
- **Alterados:**
  - `BiomassCollector.cs`: a cada coleta, emite `BiomassCollected(amount)`, ouvido pelo `ExperienceTracker`. Deixa de somar ao banco e de emitir `BiomassChanged(total)`.
  - `SuitUpgrade.cs`:
    - ganha `Description` e `ValueMode` (soma fixa ou porcentagem do valor-base);
    - ganha alvos novos no enum, um por passo, cada um junto com o seu efeito. É a regra da `suit-evolution.md`, decisão 4: nada de opção sem efeito no Inspector.
  - `SuitUpgradeApplier.cs`:
    - junta, por alvo, as duas fontes: o banco no `GameState` e a incursão no `IncursionUpgrades`;
    - aplica `base × (1 + soma das %) + soma fixa`;
    - ganha os alvos novos.
  - `LightDamageArea.cs`: potência (dano e irritação a partir dos valores da cena) e alcance (forma do feixe e luz a partir da cena).
  - `RunData.cs` e `GameState.cs`: banco e biomassa carregada passam a ser créditos (`BankedCredits`, `CarriedCredits` e `AddCarriedCredits`, ainda sem chamador).
  - `UpgradePanel.cs`: "Créditos: N".
  - Só comentários: `StartChamber.cs`, `SalaEntrada.cs` e `RestartOnDeath.cs` (depósito e taxa em créditos).
  - Recursos:
    - `upgrade_suit_integrity.tres` e `upgrade_magnet.tres`: valores novos, para o catálogo do level up;
    - `upgrade_catalog.tres`: o catálogo do banco, vazio.
- **Removido** (com autorização): `BiomassHud.cs`. O rótulo "Biomassa: N" dá lugar ao `ExperienceHud`.
- **Cenas e projeto (no editor):**
  - `player.tscn`: nós novos e ligações;
  - `hex_phase.tscn`: a `StartChamber` fica sem `EvolutionTerminal` e `EvolutionUi`;
  - `project.godot`: ação `upgrade_menu`.
- **Somente leitura:**
  - `SuitIntegrity.cs` (o `SetMaxIntegrity` já cura do jeito decidido);
  - `BiomassField.cs`, `FractalEnemy.cs`, `MobileSpawner.cs` e `EvolutionTerminal.cs`.

## 7. Passos no editor (feitos pelo dev)

O roteiro numerado completo, com os nomes exatos, vem em cada passo do plano (seção 10). Resumo:

1. **Build** depois de cada script com `[Export]` ou `[Signal]` novo.
2. **Mapa de Entrada** (Projeto > Configurações do Projeto > Mapa de Entrada): ação `upgrade_menu` com Tab e o botão 3 do controle (Y).
3. **`player.tscn`:**
   - `ExperienceTracker` e `IncursionUpgrades`, filhos do `Player`;
   - `Hud` > `ExperienceHud` no lugar do `BiomassLabel` (16, 16), com o rótulo do nível, a barra (`ProgressBar`) e o rótulo dos pontos;
   - `LevelUpUi` (`CanvasLayer`) > `LevelUpPanel`;
   - sinais e campos do `SuitUpgradeApplier` ligados.
4. **`hex_phase.tscn`:** apagar `StartChamber` > `EvolutionTerminal` e `EvolutionUi`.
5. **Recursos:** criar os novos, ajustar os dois existentes e montar os catálogos (salvos pelo disquete do Inspector).

## 8. Pronto quando

- [ ] `dotnet build` sem erros a cada passo
- [ ] Coletar biomassa enche a barra de XP (1 por orbe)
- [ ] Ao atingir o requisito, o nível sobe e ganha exatamente 1 ponto; o jogo não pausa nem abre o menu
- [ ] A XP que sobra continua no nível seguinte; uma coleta que cruza vários requisitos dá todos os níveis e pontos
- [ ] Os pontos acumulam; o HUD mostra nível, barra e pontos separados
- [ ] O menu só abre com pontos; abrir pausa e fechar retoma
- [ ] Fechar sem escolher guarda o ponto, e reabrir mostra as mesmas cartas
- [ ] Cada escolha oferece até 3 cartas distintas, sem melhorias no máximo, e o sorteio se refaz a cada ponto gasto
- [ ] Com tudo no máximo, o menu mostra "Traje no máximo" sem gastar ponto
- [ ] Escolher aplica o efeito na hora e gasta exatamente 1 ponto; cliques repetidos ou aperto mantido não gastam a mais
- [ ] Os valores efetivos dos 5 atributos ficam certos em cada nível: integridade e cura, raio do ímã, dano e irritação, alcance e luz, cargas
- [ ] A integridade nunca passa do máximo
- [ ] Morte, ejeção e extração começam uma incursão do zero; o modo da lanterna continua
- [ ] Na câmara não há terminal de evolução; na `sala_entrada`, o painel do banco mostra "Créditos: 0"
- [ ] Bancadas (`sala_ninho`, `arena`) funcionando; recargas seguidas sem erros e com "Nós Órfãos" = 0

## 9. Como testar (roteiro manual no editor)

1. F5, libere as saídas e mate inimigos: a barra enche; no requisito, o nível sobe e os pontos aparecem, sem pausa.
   - Para testar rápido: `FirstLevelXp` = 2 no Inspector do `ExperienceTracker`. Depois, digite 10 de novo.
2. Ainda com `FirstLevelXp` em 2, mate um spawner e colete os 20 orbes de uma vez: vários níveis de uma vez, com a sobra na barra.
3. Abra o menu (Tab ou Y): o jogo pausa.
   - Feche sem escolher e reabra: as mesmas cartas.
   - Escolha: o efeito vale na hora. Com pontos sobrando, aparecem cartas novas; no último ponto, o menu fecha.
4. Integridade: tome dois golpes (80/100) e escolha a integridade: 100/120.
5. Ímã, potência e alcance: compare antes e depois:
   - o raio em que os orbes são atraídos;
   - o tempo para matar um spawner (~8,6 s no nível 1);
   - o tempo até a fenda abrir;
   - o comprimento do feixe.
6. Leve uma melhoria ao máximo: ela some das cartas. Com todas no máximo, o menu mostra "Traje no máximo".
7. Clique várias vezes numa carta e segure o A ao escolher: um ponto por escolha.
8. Morra, seja ejetado e extraia: a cada vez, nível 1, barra vazia, 0 pontos e atributos-base; o modo da lanterna continua.
9. Bancadas:
   - na `sala_entrada`, o terminal abre o painel com "Créditos: 0" e a lista vazia;
   - na `sala_ninho` e na `arena`, tudo como antes.
10. Faça 5 recargas seguidas, olhando o Output e "Nós Órfãos".

## 10. Decisões e notas

### Decididas pelo dev (out/2026)

Na análise do documento de origem, o dev aceitou as recomendações, com duas exceções (L1 e L12). Os números seguem os das perguntas.

- **L1, banco × level up:** o level up substitui o banco como progressão, e a biomassa vira só experiência. **Exceção do dev:** o banco fica pronto para melhorias permanentes futuras, com outra moeda (ex.: créditos colhidos no jogo).
  - **L1a:** o fluxo do banco fica inteiro, só trocando a moeda para créditos: o que é carregado vai para o banco; a câmara e a extração depositam 100%; a morte guarda metade. Sem fonte por enquanto, o saldo fica em 0.
  - **L1b:** o terminal de evolução sai da câmara da fase e fica na `sala_entrada` (bancada) até os créditos existirem.
  - **L1c:** as duas melhorias do banco (integridade e ímã) saem do catálogo dele, que fica vazio. Quando houver melhorias permanentes, os bônus delas somam com os do level up.
  - **L1d:** nome provisório da moeda no código: `Credits`.
  - **Consequência aceita:** por enquanto, a extração perde o prêmio e vira só "terminar a fase".
- **L2, partida = incursão:** começa ao carregar a fase e termina na morte, na ejeção ou na extração; as três zeram o level up. O modo da lanterna continua guardado.
- **L3, 1 orbe = 1 XP:** biomassa perdida (pela fenda ou pelo tempo de vida) não dá XP.
- **L4, curva:** a regra da tabela continua depois do nível 10, sem teto, com 3 números no Inspector (10, 5 e 1).
- **L5, tudo no máximo:** os pontos acumulam, e o menu mostra "Traje no máximo" sem gastar.
- **L6, abrir o menu:** ação nova `upgrade_menu`, com Tab e Y.
- **L7, sem pontos:** o menu não abre.
- **L8, reabrir sem escolher:** as mesmas cartas até gastar o ponto. Sem isso, daria para trocar as cartas de graça, só abrindo e fechando.
- **L9, último ponto:** o menu fecha sozinho.
- **L10, integridade:**
  - nome "Integridade do traje";
  - a cura é a do `SetMaxIntegrity` atual;
  - fica registrada a exceção ao "não regenera".
- **L11, ímã:** a melhoria aumenta o `MagnetRadius`, não o `PickupRadius`.
- **L12, potência:** vale para inimigos, spawners **e paredes**: exceção do dev, a irritação sobe junto com o dano.
  - **L12b:** o nome é "Potência da lanterna", que estava reservado na `suit-evolution.md`. Esta spec resolve a decisão 4 de lá.
- **L13, alcance:** o cone cresce mantendo a abertura, e a luz visível cresce junto.
- **L14 a L18, campo de força:** registradas na `force-field.md` (F1 a F5).
- **L19 a L21, documentos:**
  - o campo de força tem spec própria (`force-field.md`);
  - o level up é esta spec (`level-up.md`);
  - o documento de origem foi movido para `docs/reference/`.

### Arquitetura (proposta, a aprovar com a spec)

- **Progressão no jogador:** o `ExperienceTracker` e o `IncursionUpgrades` são componentes do `player.tscn`, como a integridade.
  - **Por quê:** toda incursão termina recarregando a cena, e o jogador nasce com componentes novos. O reinício pedido pelo documento de origem (seção 7) vem de graça, sem um código de "zerar" que alguém possa esquecer de chamar.
  - **Alternativa:** guardar no `GameState`. Ele sobrevive à troca de cena, mas pede um reinício explícito na câmara. Vale a pena se um dia a incursão cruzar cenas (hexágonos vizinhos, depois da demo).
- **Observer:**
  - o coletor avisa `BiomassCollected`;
  - o `ExperienceTracker` avisa `ExperienceChanged` e `UpgradePointsChanged`;
  - o `IncursionUpgrades` avisa `UpgradeLevelChanged`;
  - HUD, menu e aplicador só escutam.

  Nenhum componente afetado (`SuitIntegrity`, `BiomassCollector`, `LightDamageArea`, `ForceField`) sabe que as melhorias existem, como já é hoje.
- **Dados no Inspector (design orientado a dados):** o mesmo recurso `SuitUpgrade` serve aos dois catálogos; o que muda é a moeda de quem o usa.
  - No banco, `Costs` é o custo em créditos; no level up, em pontos (todos 1).
  - O nível máximo continua sendo o tamanho de `Costs`.
  - Os níveis são guardados de 0 (sem melhoria) a 4; na tela, aparecem de 1 a 5.
- **Aplicador:** recalcula tudo do zero a cada mudança (idempotente, como hoje), somando as duas fontes por alvo.
  - Com 5 alvos, o `switch` continua: uma linha por alvo, que chama um método do componente.
  - A regra de cada alvo mora no componente. Por exemplo, a potência mexe em dois valores, e o alcance, na forma e na luz.
  - O Strategy (uma classe por alvo) fica para quando um alvo precisar de um cálculo próprio.
- **Sorteio:** embaralha as melhorias elegíveis e pega as primeiras (Fisher–Yates), guardando as cartas até a escolha.
- **Pausa:** o mesmo mecanismo do `UpgradePanel`: `GetTree().Paused`, e o painel com `ProcessMode = Always`.

### Plano de implementação (a aprovar)

Cada passo segue o ritual dos anteriores:
1. verificação antes no descartável (Godot headless);
2. apresentação do código atual, da mudança, do porquê e das consequências;
3. autorização do dev;
4. `dotnet build`;
5. teste do dev no editor e a mensagem de commit sugerida.

Os passos:

0. **Commit das specs** (pelo dev).
1. **XP, nível e HUD:**
   - código: `ExperienceTracker.cs` e `ExperienceHud.cs` (novos), `BiomassCollector.cs`; sai o `BiomassHud.cs`;
   - a biomassa deixa de ir para o banco;
   - teste: seção 9, itens 1 e 2.
2. **Banco em créditos, fora da câmara:**
   - código: `RunData.cs`, `GameState.cs`, `UpgradePanel.cs` e os comentários;
   - no editor: catálogo do banco vazio e terminal fora da `StartChamber`;
   - teste: câmara sem o terminal; "Créditos: 0" na `sala_entrada`; morte e extração sem erros.
3. **Menu e escolha, com integridade e ímã:**
   - código: `IncursionUpgrades.cs` e `LevelUpPanel.cs` (novos); `SuitUpgrade.cs` com `Description` e `ValueMode`; `SuitUpgradeApplier.cs` com as duas fontes;
   - no editor: `levelup_catalog.tres` e a ação `upgrade_menu`;
   - teste: seção 9, itens 3, 4, 6 e 7 (com 2 atributos, no máximo 2 cartas).
4. **Potência da lanterna:**
   - código: alvo novo e `LightDamageArea.cs`; o recurso;
   - teste: item 5 (tempo para matar e para abrir a fenda).
5. **Alcance da lanterna:**
   - código: alvo novo, forma do feixe e luz; o recurso;
   - teste: item 5 (comprimento e peso da luz nas paredes).
6. **Campo de força:** os passos da `force-field.md`, com o alvo do level up por último.
7. **Fechamento:**
   - ciclo completo (itens 8 a 10);
   - documentos: esta spec, a `force-field.md`, as specs revistas, `BACKLOG.md`, `HANDOFF.md` e `CLAUDE.md`;
   - a mensagem de commit.

### Notas

- O documento de origem traz uma arquitetura "independente de engine". Os valores-base dele eram exemplos (dano de 10/s); os reais são os da seção 2.
- O Vampire Survivors tem as duas progressões: gemas de XP na partida e ouro para melhorias permanentes. O banco reservado (L1) deixa essa porta aberta.
