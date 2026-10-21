# Spec: Integridade do traje e morte do jogador

- **Status:** validada (out/2026). Valores em teste. O fluxo depois da morte foi definido pela spec `suit-evolution.md`: voltar à câmara inicial.
- **Etapa:** 1 (mecânica)
- **Depende de:** `docs/specs/fractal-enemy.md` (inimigos, camadas de colisão), `docs/specs/fractal-enemy-movement.md` (inimigos travados descansam encostados no jogador), `docs/specs/biomass-collection.md` (o que acontece com a biomassa na morte).

> **Valores em teste:** como nas demais specs da demo, os `[Export]` e os números desta spec são pontos de partida, não valores definitivos.

## 1. Objetivo

O anticorpo fractal passa a ser uma ameaça: encostar na horda danifica o traje do cientista, e o traje destruído é a morte. Dá risco ao combate e fecha o trecho MORRER do core loop. A forma final do que acontece depois da morte (voltar à câmara inicial, manter ou perder biomassa) depende da spec de evolução; esta spec define uma versão provisória e testável.

## 2. Comportamento esperado

### Integridade e dano

- O traje começa com `MaxIntegrity` de integridade.
- Quando um inimigo encosta no jogador, então o traje perde `ContactDamage` de integridade e o jogador fica invulnerável por `HitInvulnerability` segundos (janela de imunidade, "i-frames").
- Durante a imunidade, novos contatos não causam dano.
- O contato vale mesmo com o inimigo **parado** encostado no jogador (inimigos travados descansam sem chamar `MoveAndSlide`; ver spec de movimento).
- Ao tomar dano, o jogador recebe um pequeno empurrão (`KnockbackImpulse`) para longe do inimigo, aproveitando a inércia da gravidade zero, sem perder o controle (diferente da ejeção).
- Ao tomar dano, o sprite do jogador pisca durante a imunidade, para o jogador perceber o golpe (feedback mínimo de mecânica; arte e som ficam para a etapa 2).
- Quando a integridade muda, então o componente de integridade avisa por sinal (`IntegrityChanged`), sem conhecer quem escuta. Um HUD de texto de teste mostra o valor, como o de biomassa.
- A integridade não regenera.

### Morte

- Quando a integridade chega a zero, então o jogador morre: emite o sinal `Died`, perde o controle do movimento e da lanterna e para de receber dano.
- Na morte, a biomassa carregada perde a parte que não é guardada (`DeathKeepFraction`, 0,5: guarda metade, arredondado para baixo).
- Depois de `DeathRestartDelay` segundos, o jogo vai para a cena `ReturnScenePath` do `RestartOnDeath` (hoje, a câmara inicial `sala_entrada.tscn`), que deposita no banco o que sobrou. Com `ReturnScenePath` vazio, a sala atual é recarregada (o comportamento original, ainda disponível).
- A sala de destino recomeça do zero: integridade cheia, inimigos e orbes novos. Banco e níveis das melhorias ficam no `GameState` (spec `suit-evolution.md`).

## 3. Fora do escopo

- Gastar ou guardar biomassa entre mortes, melhorias do traje (spec de evolução).
- Voltar à câmara inicial e o fluxo completo de partida roguelike (spec de evolução ou de estrutura da partida).
- Ataque ativo dos inimigos (investida, projéteis), animações de ataque.
- Tela de game over, menus, salvar em disco.
- Arte, som, tremor de tela e efeitos de morte (etapa 2).
- Outras fontes de dano além do contato com inimigos, (decisão 7: a ejeção pela parede não causa dano).

## 4. Parâmetros ajustáveis (valores em teste)

| Parâmetro | Valor inicial | Onde fica | Observação |
|---|---|---|---|
| `MaxIntegrity` | 100 | componente de integridade | Integridade inicial do traje |
| `ContactDamage` | 10 | componente de integridade | Dano por golpe, igual para todas as gerações (decisão 3) |
| `HitInvulnerability` | 0,8 s | componente de integridade | Com 10 de dano, a taxa máxima é 12,5 por segundo, qualquer que seja o tamanho da horda: morte em no mínimo ~8 s de contato contínuo |
| `KnockbackImpulse` | 200 px/s | componente de integridade | Velocidade somada ao jogador no golpe; o `MaxSpeed` dele é 600 |
| `DeathRestartDelay` | 2 s | componente de morte | Tempo entre a morte e o recarregamento |
| Tamanho da área de contato | um pouco maior que a colisão do jogador (~10 px) | `player.tscn` (editor) | Precisa ser maior que o corpo, senão o inimigo bloqueado nunca a "toca" |

## 5. Casos de borda

- Horda inteira encostada no jogador: a janela de imunidade limita o dano (no máximo 1 golpe a cada `HitInvulnerability`). Decidido assim (decisão 2): a horda não mata instantaneamente.
- Inimigo travado (descansando) encostado no jogador: continua contando como contato.
- Fragmento recém-nascido (imune a dano por 0,3 s): causa dano normalmente; a imunidade dele é só para receber dano.
- Inimigo devolvido ao pool: com a colisão desligada, deixa de contar como contato.
- Jogador ejetado pela parede: continua podendo tomar dano por contato; o empurrão do golpe não cancela a ejeção.
- Morte durante a ejeção: morre normalmente; o recarregamento descarta o estado da ejeção.
- Morte com orbes e fragmentos sendo criados no mesmo frame: o recarregamento libera tudo; sem erros e com "Nós Órfãos" = 0. Este é o primeiro teste real de "troca de sala" do projeto (pendente nas specs do inimigo e da biomassa).
- Registros estáticos (`FractalSwarm`, `BiomassField`) ao recarregar a sala: os inimigos e orbes saem dos registros ao sair da árvore; nenhum registro pode ficar com referência a nó liberado.
- Lanterna ligada sobre a horda no momento da morte: a lanterna desliga junto com o controle, para inimigos não morrerem "sozinhos" depois da morte.

## 6. Arquivos e cenas

- Alterados:
  - `Player.cs`: `IsDead`; `OnDied()` (ouvinte do sinal `Died`) desliga o controle e chama `TurnOffFlashlight()`, que esconde o `FlashlightAnchor` e desativa o processamento dele (a `LightDamageArea` sai da física, sem mudar o `LightDamageArea.cs`); estado "morto" antes da ejeção no `_PhysicsProcess`; `ApplyKnockback(Vector2)` soma o empurrão à velocidade.
  - `FractalEnemy.cs`: uma linha, `public bool IsDead`, para a área de contato ignorar inimigos mortos ou guardados no pool (desvio aprovado: a spec listava o arquivo como somente leitura).
  - `player.tscn`: nós novos e conexões de sinal (seção 7).
- Novos:
  - `PlayerHurtbox.cs` (`Area2D`, filha do `Player`): lista os inimigos encostados (entrou/saiu), limpa a cada passo os mortos e liberados, e responde qual é o inimigo encostado mais próximo.
  - `SuitIntegrity.cs` (`Node`, filho do `Player`): integridade, golpe com imunidade, piscar, empurrão; sinais `IntegrityChanged(int)` e `Died()`. Recebe a área de contato e o sprite por `[Export]`; o estado inicial é emitido de forma adiada.
  - `RestartOnDeath.cs` (`Node`, filho do `Player`): ouvinte do `Died`; cobra a taxa da biomassa e, depois de `DeathRestartDelay` (`SceneTreeTimer`), troca para `ReturnScenePath` ou recarrega a sala se o campo estiver vazio. Alterado pela spec `suit-evolution.md`.
  - `IntegrityHud.cs` (`Label`): HUD de teste "Traje: N".
- Somente leitura: `FractalSwarm.cs`, `BiomassField.cs`, `WallSensitive.cs`, `LightDamageArea.cs`.

## 7. Passos no editor (feitos pelo dev)

Feitos em out/2026, todos no `player.tscn`:

1. **Build** no editor depois dos scripts novos.
2. `Player` > **Area2D** chamada **`Hurtbox`** (script `PlayerHurtbox.cs`) > **CollisionShape2D** com **CircleShape2D de raio 8**. Collision: **Layer** nenhuma, **Mask** só **2**; **Monitorable** desligado.
3. `Player` > **Node** chamado **`SuitIntegrity`** (script `SuitIntegrity.cs`), com **Hurtbox** = nó `Hurtbox` e **Blink Target** = nó `Sprite2D`.
4. `Player` > **Node** chamado **`RestartOnDeath`** (script `RestartOnDeath.cs`).
5. `Hud` > **Label** chamado **`IntegrityLabel`** (script `IntegrityHud.cs`), posição (16, 48).
6. Sinais do `SuitIntegrity`: `IntegrityChanged` → `IntegrityLabel.OnIntegrityChanged`; `Died` → `Player.OnDied`; `Died` → `RestartOnDeath.OnPlayerDied`.

O raio 8 da área de contato é maior que o corpo do jogador (10×10 px) de propósito: um inimigo bloqueado fica com o centro a ~11 px, e a área precisa alcançá-lo.

## 8. Pronto quando

- [x] `dotnet build` sem erros
- [x] Encostar num inimigo tira `ContactDamage` de integridade, e o HUD de teste mostra o valor novo
- [x] Durante `HitInvulnerability` o jogador não toma novo dano e o sprite pisca
- [x] Um inimigo parado (descansando) encostado no jogador também causa dano
- [x] O golpe empurra o jogador para longe do inimigo, sem tirar o controle
- [x] Com a integridade em zero: o jogador perde o controle e a lanterna desliga; depois de `DeathRestartDelay`, a sala recomeça do zero
- [x] Depois do recarregamento: integridade cheia, biomassa zerada, nenhum erro no Output e "Nós Órfãos" = 0
- [x] Morrer no meio de um massacre na `sala_ninho` (orbes e fragmentos sendo criados) não gera erro nem nós órfãos (testado com mortes seguidas; o monitor "Nós" volta a um valor parecido a cada recarregamento)
- [x] Todos os valores da seção 4 ajustáveis no Inspector

## 9. Como testar (roteiro manual no editor)

1. `arena.tscn`, F6: deixe um inimigo encostar e confirme o dano no HUD, o piscar e o empurrão. Fique encostado e confirme que o dano só volta depois da imunidade.
2. Fique parado e deixe um inimigo travar encostado em você: o dano continua.
3. Morra (para testar rápido, `MaxIntegrity` = 20 no Inspector): confirme a perda de controle, a lanterna desligada e o recarregamento depois do atraso, com integridade cheia e biomassa zerada.
4. `sala_ninho.tscn`: morra no meio de um massacre com orbes no chão; confira o Output e "Nós Órfãos" = 0 (Depurador > Monitores).

## 10. Decisões e notas

Todas as propostas abaixo foram **aceitas pelo dev** (out/2026). As alternativas ficam registradas para revisão futura.

1. **Como detectar o contato.** (a) `Area2D` de contato no jogador, com máscara 2, listando os inimigos que entram e saem, no mesmo padrão da `LightDamageArea` (proposto: detecta inimigos parados e respeita o tamanho de cada geração; é uma área só, barata); (b) distância calculada em C# a partir do registro `FractalSwarm` (sem física, mas precisaria considerar o tamanho de cada geração); (c) colisões do `MoveAndSlide` (não recomendado: inimigos travados não chamam `MoveAndSlide` e o jogador parado não colide com nada).
2. **Modelo de dano.** (a) golpe + janela de imunidade (proposto: legível, a horda não mata instantaneamente); (b) dano contínuo por segundo para cada inimigo encostado (a horda fica muito mais perigosa, risco de morte instantânea); (c) misto: golpe + imunidade, com o dano do golpe crescendo com o número de inimigos encostados.
3. **Dano por geração.** (a) igual para todas (proposto: simples); (b) proporcional ao tamanho (fragmentos causam menos dano). Se (b), o `ContactDamage` fica no `FractalEnemy`.
4. **Empurrão no golpe.** Proposto sim, pequeno, aproveitando a inércia da gravidade zero. Alternativa: sem empurrão.
5. **O que acontece na morte.** Primeiro, recarregar a sala atual (provisório). **Definido depois pela spec `suit-evolution.md`: voltar à câmara inicial** (`ReturnScenePath`).
6. **Biomassa na morte.** Primeiro, perdida (recarregar a sala). **Definido depois pela spec `suit-evolution.md`: guarda metade** (`DeathKeepFraction` = 0,5), depositada no banco ao chegar à câmara.
7. **Ejeção pela parede causa dano?** O high concept do handoff fala em "risco constante de ser ejetado para o vácuo". Proposto: **não** nesta spec (a ejeção continua só como empurrão e perda de controle); fica anotado como candidato para quando o vácuo tiver papel no jogo.
   - **Atualizado (out/2026):** o vácuo ganhou papel. O rascunho de game design define que a parede de fronteira ejeta o jogador para o espaço (`dyson-wall.md`, sinal `PlayerEjected`). A consequência (dano, morte, volta ao último nó, perda de biomassa) está em aberto no backlog; até ser decidida, a ejeção continua sem dano.

### Impacto do rascunho de game design (out/2026)

- "A sala de destino recomeça do zero" pressupunha salas isoladas. **Decidido na `hex-phase.md` (H5):** a morte continua voltando à câmara inicial (`RestartOnDeath`, sem mudança), e a fase hexagonal recomeça do zero a cada entrada (spawners, inimigos e orbes novos). Dentro da fase, a integridade se mantém entre as estruturas.
8. **Piscar o sprite no golpe.** Proposto sim, como feedback mínimo de mecânica (o jogador precisa saber que foi atingido). Alternativa: nenhum feedback visual até a etapa 2.
