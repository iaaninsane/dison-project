# Spec: Campo de força do traje

- **Status:** **aprovada** pelo dev (out/2026), com a arquitetura e o plano de implementação (seção 10). Decisões F1 a F5 (respostas 14 a 18 da análise do documento de origem) e propostas P1 a P3, aprovadas com a spec. É o passo 6 do plano da `level-up.md`.
- **Etapa:** 1 (mecânica)
- **Origem:** `docs/reference/documentacao_level_up_demo_atualizada.md`, seção 5.3.
- **Depende de:**
  - `docs/specs/player-health-death.md`: golpe de contato, imunidade e empurrão;
  - `docs/specs/mobile-spawners.md`: o spawner fere ao encostar;
  - `docs/specs/fractal-enemy.md` e `docs/specs/fractal-enemy-movement.md`: movimento e descanso do inimigo;
  - `docs/specs/level-up.md`: as cargas sobem com o nível.

> **Valores em teste:** como nas demais specs da demo, os `[Export]` e os números desta spec são pontos de partida, não valores definitivos.

## 1. Objetivo

Um campo em volta do traje absorve os golpes de contato enquanto tiver carga: protege a integridade e afasta os inimigos, sem feri-los. Esgotado, ele desliga e volta depois de uma recarga. Dá ao jogador uma margem de erro no COMBATER e é um dos atributos do level up (EVOLUIR).

## 2. Comportamento esperado

- Quando o jogador nasce (em toda incursão), então o campo está ativo, com `BaseCharges` cargas (1 no nível 1).
- Quando um inimigo ou spawner encosta no jogador fora da imunidade e o campo tem carga, então o golpe é absorvido:
  - a integridade não muda, e o campo gasta 1 carga;
  - a imunidade (`HitInvulnerability`) começa, e o jogador recebe o empurrão (`KnockbackImpulse`), como num golpe;
  - todo inimigo encostado nesse momento é repelido: ganha `RepelImpulse` para longe do jogador, sem dano (P1);
  - o spawner não é repelido, porque anda preso à rota (S8a), mas o golpe dele também gasta 1 carga.
- Com a horda encostada, a imunidade limita o gasto: no máximo 1 carga a cada `HitInvulnerability` (0,8 s), e não uma por inimigo.
- Quando a última carga é gasta, então o campo desliga e começa a recarga (`RechargeTime`).
  - Desligado, ele deixa os golpes ferirem o traje, como hoje.
  - No fim da recarga, volta com todas as cargas.
- Quando o nível do campo sobe (level up), então o máximo de cargas sobe 1:
  - com o campo ativo, a carga nova entra na hora (P3);
  - desligado, ele volta já com o máximo novo.
- O campo não age sobre a ruptura no espaço (`Breach`), a sucção, a ejeção nem as paredes.
- Aviso mínimo (etapa 1):
  - um círculo em volta do jogador enquanto o campo está ativo, desenhado por código e visível no escuro;
  - no HUD, "Campo: 2/3" ou "Campo: recarregando", ouvindo o sinal `ChargesChanged(current, max)`.

## 3. Fora do escopo

- Repulsão reforçada, recuperação acelerada e pulso defensivo (melhorias especiais, documento de origem, seção 9).
- Dano do campo.
- Arte, som e animação do campo (etapa 2).
- Recarga de uma carga por vez (alternativa descartada na F4).

## 4. Parâmetros ajustáveis (valores em teste)

| Parâmetro | Valor inicial | Onde fica | Observação |
|---|---|---|---|
| `BaseCharges` | 1 | `ForceField` | Cargas no nível 1 |
| Cargas por nível | +1, 4 níveis | `upgrade_force_field.tres` | 1 → 5 cargas (`level-up.md`) |
| `RechargeTime` | 5 s | `ForceField` | Do desligar ao voltar cheio |
| `RepelImpulse` | 300 px/s | `ForceField` | Velocidade dada ao inimigo. Some no ritmo do `SuctionVelocityDecay` do inimigo (300 px/s²): dura ~1 s e afasta ~150 px |
| `VisualRadius` | 16 px | `ForceField` | Visual provisório. O corpo do jogador tem 10 × 10 px, e a área de contato, raio 8 |
| `FieldColor` | provisória | `ForceField` | Grupo "Visual provisório" |

## 5. Casos de borda

- **Inimigo descansando (travado) encostado:** o impulso o acorda, como a sucção já faz. Senão, ele ficaria parado no lugar.
- **Inimigo repelido contra uma parede:** desliza nela. A parte do impulso que vai contra a parede some, como na sucção.
- **Inimigo devolvido ao estoque e reaproveitado:** nasce sem o impulso da vida anterior, porque o `ActivateAt` já zera a velocidade que vem de fora.
- **Fragmento recém-nascido encostado:** é repelido como qualquer inimigo.
- **Spawner encostado com o campo ativo:** o campo absorve e gasta 1 carga; o spawner segue empurrando o jogador (S8a), e a próxima carga só sai depois da imunidade.
- **Campo que desliga no meio de uma horda:** o golpe seguinte, depois da imunidade, já fere o traje.
- **Menu de melhorias aberto:** o jogo pausa, e a recarga pausa junto.
- **Morte:** morto não toma golpe, então o campo não age. A recarga da cena traz um campo novo e cheio.
- **Bancadas:** o campo vale em toda cena com o jogador, porque é um componente do `player.tscn`.

## 6. Arquivos e cenas

- **Novos** (com autorização):
  - `ForceField.cs` (`Node2D`, filho do `Player`): cargas, recarga e desenho do círculo; métodos `TryAbsorbHit(...)` e `SetBonusCharges(int)`; sinal `ChargesChanged(current, max)`.
  - `ForceFieldHud.cs` (`Label`): "Campo: N/M"; só escuta o sinal.
  - `IRepellable.cs` (interface C#): "o que o campo empurra". Fica separada do `IContactHazard` (segregação de interfaces), porque o spawner fere ao encostar, mas não é empurrado.
- **Alterados:**
  - `SuitIntegrity.cs`: ganha `[Export] ForceField` e, antes de tirar integridade, pergunta ao campo se ele absorve o golpe.
  - `PlayerHurtbox.cs`: entrega os perigos encostados, para o campo repelir, e não só a posição do mais próximo.
  - `FractalEnemy.cs`: cumpre `IRepellable`. O impulso soma à velocidade que ele ganha de fora (a mesma da sucção) e o acorda.
  - `SuitUpgrade.cs` e `SuitUpgradeApplier.cs`: alvo `ForceFieldCharges` (passo 2).
- **Cenas:** `player.tscn` (nó `ForceField`, rótulo no `Hud` e o campo novo do `SuitIntegrity`).
- **Somente leitura:** `MobileSpawner.cs`.

## 7. Passos no editor (feitos pelo dev)

O roteiro completo, com os nomes exatos, vem em cada passo do plano. Resumo:

1. **Build** depois dos scripts novos.
2. `player.tscn` > `Player` > **Node2D** chamado **`ForceField`** (script `ForceField.cs`), na posição (0, 0).
3. `SuitIntegrity` > **Force Field** = nó `ForceField`.
4. `Hud` > **Label** chamado **`ForceFieldLabel`** (script `ForceFieldHud.cs`), ao lado do `IntegrityLabel` (posição a combinar no passo).
5. Sinal: `ForceField` > `ChargesChanged` → `ForceFieldLabel.OnChargesChanged`.
6. Recurso `upgrade_force_field.tres` no catálogo do level up (passo 2).

## 8. Pronto quando

- [ ] `dotnet build` sem erros
- [ ] O jogador nasce com o campo ativo: círculo visível e "Campo: 1/1"
- [ ] Com carga, encostar num inimigo não tira integridade, gasta 1 carga e afasta os inimigos encostados; o jogador é empurrado e fica imune como num golpe
- [ ] A horda encostada gasta no máximo 1 carga por janela de imunidade
- [ ] O golpe do spawner também é absorvido, e o spawner não é repelido
- [ ] Sem carga, o campo desliga, os golpes ferem e, depois de `RechargeTime`, ele volta cheio
- [ ] Subir o nível do campo dá +1 carga (na hora, se ele estiver ativo)
- [ ] A ruptura no espaço, a sucção e a ejeção não são afetadas
- [ ] Bancadas funcionando; recargas seguidas sem erros e com "Nós Órfãos" = 0
- [ ] Valores da seção 4 ajustáveis no Inspector

## 9. Como testar (roteiro manual no editor)

1. `arena.tscn` (bancada com inimigos), F6: deixe um inimigo encostar.
   - Confira: integridade igual, "Campo: 0/1", o inimigo afastado e o círculo apagado.
   - Encoste de novo: golpe normal.
2. Espere `RechargeTime` (5 s): o campo volta ("Campo: 1/1").
3. Deixe a horda encostar com o campo ativo: só 1 carga por janela de imunidade, e os inimigos encostados se afastam.
4. Na fase, encoste num spawner com o campo ativo: o campo absorve, e o spawner segue a rota.
5. Com o level up (passo 2), suba o nível do campo e confira as cargas: a nova entra na hora com ele ativo e, depois de esgotar, ele volta com o máximo novo.
6. Seja sugado por uma fenda e saia: morte normal, porque o campo não absorve a ruptura.

## 10. Decisões e notas

### Decididas pelo dev (out/2026)

- **F1 (pergunta 14):** o campo fica ativo desde o início de cada incursão, no nível 1, com 1 carga.
- **F2 (pergunta 15):** o campo age no momento do golpe de contato (área de contato e integridade): com carga, absorve o golpe, repele e gasta 1 carga. A imunidade também vale.
  - Descartada: uma bolha maior que o corpo, que repele antes de encostar. A horda esvaziaria o campo na hora.
- **F3 (pergunta 16):** o golpe do spawner também é absorvido. O spawner não é repelido, e o jogador continua sendo empurrado.
- **F4 (pergunta 17):** o campo só recarrega ao zerar e volta cheio depois de `RechargeTime` (5 s, valor em teste).
  - Descartada: uma carga por vez.
- **F5 (pergunta 18):** aviso mínimo: o círculo enquanto ativo e "Campo: N/M" no HUD.

### Propostas (aprovadas pelo dev com a spec, out/2026)

- **P1:** ao absorver, repelir todos os inimigos encostados naquele momento, e não só o mais próximo. O documento de origem fala em repelir "inimigos".
- **P2:** no golpe absorvido, o empurrão do jogador e a imunidade são os de um golpe normal, e o sprite pisca do mesmo jeito, mostrando a imunidade.
  - Alternativa: não piscar, já que o traje não foi atingido. Isso pede um segundo relógio.
- **P3:** subir o nível com o campo ativo dá a carga nova na hora.

### Arquitetura (aprovada pelo dev, out/2026)

- **Uma responsabilidade cada:** o `SuitIntegrity` continua decidindo **quando** há golpe (contato fora da imunidade), e o `ForceField` decide se o golpe é **absorvido**, e repele.
- **Contrato `IRepellable`:** o campo empurra pelo contrato, sem conhecer o tipo do inimigo, como a lanterna fere pelo `ILightDamageable`.
- **O campo não sabe do level up:** recebe o bônus de cargas pelo `SuitUpgradeApplier`, como os outros atributos.
- **Processar só o que importa** (lição do `CLAUDE.md`): o `ForceField` só conta tempo enquanto recarrega, e o círculo só é redesenhado quando o estado muda.

### Plano de implementação (passo 6 da `level-up.md`)

Mesmo ritual dos outros passos: verificação no descartável, apresentação, autorização, `dotnet build` e teste do dev.

1. **Campo no nível 1:**
   - código: `ForceField.cs`, `ForceFieldHud.cs` e `IRepellable.cs` (novos); `SuitIntegrity.cs`, `PlayerHurtbox.cs` e `FractalEnemy.cs`;
   - teste: seção 9, itens 1 a 4 e 6.
2. **Campo no level up:**
   - código: alvo `ForceFieldCharges` no `SuitUpgrade` e no aplicador;
   - no editor: `upgrade_force_field.tres` no catálogo do level up;
   - teste: item 5.
