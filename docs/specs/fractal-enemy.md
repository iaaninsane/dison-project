# Spec: Inimigo fractal (dano por luz e divisão celular)

- **Status:** em andamento. A fase 1 (desenho, pulsação e dano por luz) já está implementada no código. A fase 2 (divisão em fragmentos) foi adiada.
- **Etapa:** 1 (mecânica)
- **Depende de:** lanterna e `LightDamageArea.cs` (dano por luz). Nenhuma outra spec.

> **Valores em teste:** esta é uma demo. Os atributos `[Export]` ainda estão em teste e não têm valores definitivos. A seção 4 mostra os valores que estão hoje no código e na cena, só como referência. Divergências entre esta spec e o Inspector são esperadas e não contam como erro. Quando os valores forem fechados, esta seção é atualizada.

## 1. Objetivo

O anticorpo fractal é o inimigo da demo. Ele é desenhado por código como uma árvore de ramos binários que pulsa e é destruído pela luz da lanterna. Na fase 2, ao morrer, ele vai se dividir em fragmentos menores (divisão celular), criando a pressão de horda da sala do ninho. Serve ao trecho COMBATER do core loop.

## 2. Comportamento esperado

### Fase 1: implementada

- Quando o inimigo é instanciado, então desenha, via `_Draw`, uma árvore fractal binária recursiva com 4 troncos (cima, baixo, esquerda e direita). A profundidade vem de `RecursionDepth`.
- Enquanto vive, o desenho pulsa em ciclo senoidal. O que pulsa é o **ângulo de abertura dos ramos** (decidido), não a escala.
- O redesenho é limitado a `RedrawsPerSecond` por segundo, para aguentar hordas grandes.
- Quando o feixe da lanterna sobrepõe o inimigo, então ele perde vida continuamente: `DamagePerSecond × delta` a cada frame de física.
- Ao receber dano, o inimigo pisca em branco por um instante (flash de dano, já existente no código).
- Quando o inimigo sai do feixe, então a vida para de cair e não regenera.
- Quando a vida chega a zero, então o inimigo morre. Sem pool: `QueueFree()`. Com pool: `Die()` chama o callback `OnDied` e o inimigo volta para o estoque do `NinhoSpawnerPooled`.

### Fase 2: adiada (não implementar ainda)

- Quando a vida chega a zero, então o inimigo gera 2 fragmentos da geração seguinte, cada um menor e com menos vida, afastados alguns pixels em direções opostas.
- Quando um inimigo da geração máxima morre, então não gera fragmentos.
- Fragmentos recém-nascidos ficam invulneráveis por um curto tempo, para não haver morte em cascata no mesmo instante.
- Ao ser reaproveitado pelo pool (`ActivateAt`), o inimigo volta para a geração 0.
- Quem cria os fragmentos: decidido que será o próprio inimigo. Os fragmentos ficam fora do pool e do `MaxConcurrentEnemies`.

## 3. Fora do escopo

- Ataque ao jogador (spec própria depois).
- Movimento e perseguição: **já existem no código** (perseguição simples em linha reta com `MoveAndSlide`), mas não fazem parte desta spec. Não mexer aqui; ajustes vão na spec própria de movimento. Exceção já decidida: o `CharacterBody2D` usa Motion Mode **Floating** (ver seção 11).
- Renderização em lote (bake de texturas + MultiMesh): adiada; ver seção 11.
- Partículas neon e iluminação temporária na morte (spec própria).
- Coleta de biomassa (spec própria).
- Dano por tiros ou explosões.
- Arte final, áudio e shaders (etapa 2).

## 4. Parâmetros ajustáveis (valores em teste)

Todos os valores abaixo estão **em teste** e não são definitivos (veja o aviso no topo). Quando a cena `fractal_enemy.tscn` sobrescreve o padrão do código, vale o valor da cena (Inspector).

### Implementados

| Parâmetro | Padrão no código | Valor na cena | Onde fica | Observação |
|---|---|---|---|---|
| `MaxHealth` (vida) | 100 | (padrão) | `FractalEnemy.cs` | `[Export]` |
| `DamagePerSecond` (dano da luz) | 35 | (padrão) | `LightDamageArea.cs` | `[Export]`. Mata o inimigo em ~2,9 s de luz contínua |
| `WallIrritationPerSecond` | 20 | (padrão) | `LightDamageArea.cs` | `[Export]`. Irritação nas paredes, citada só como referência |
| `RecursionDepth` (profundidade da árvore) | 5 | 4 | `FractalEnemy.cs` | `[Export]` |
| `BaseBranchLength` (tamanho do ramo principal) | 25 | 8 | `FractalEnemy.cs` | `[Export]` |
| `RedrawsPerSecond` | 20 | (padrão) | `FractalEnemy.cs` | `[Export]`. Otimização do redesenho |
| `NeonColor` | (0, 1, 0.5) | (padrão) | `FractalEnemy.cs` | `[Export]` |
| `Speed` | 80 | 10 | `FractalEnemy.cs` | `[Export]`. Movimento fora do escopo desta spec |
| Motion Mode (`CharacterBody2D`) | Grounded (padrão da Godot) | **Floating** | `fractal_enemy.tscn` | Propriedade do nó, não é valor de balanceamento. Decidido; ver seção 11 |
| `BaseBranchAngle` (ângulo médio dos ramos) | 0.4 rad | (padrão) | `FractalEnemy.cs` | `[Export]`. Centro da pulsação |
| `PulseAmplitude` (oscilação do ângulo) | 0.15 rad | (padrão) | `FractalEnemy.cs` | `[Export]`. Quanto o ângulo varia para cada lado |
| `PulseFrequency` (frequência da pulsação) | 0.6366 Hz | (padrão) | `FractalEnemy.cs` | `[Export]`. Pulsos completos por segundo; equivale ao antigo `Sin(_time × 4.0)` |
| Duração do flash de dano | 0.1 s | — | `FractalEnemy.cs` | Fixo no código |

### Fase 2 (adiados, ainda não existem no código)

| Parâmetro | Valor de partida | Observação |
|---|---|---|
| Fator de vida por geração | 0.5 | Multiplica a cada divisão |
| Profundidade por geração | −1 | Diminui a cada geração |
| Geração máxima | 2 | 1 + 2 + 4 = 7 inimigos por ancestral |
| Fator de escala por geração | 0.6 | Fragmentos menores |
| Invulnerabilidade ao nascer | ~0,3 s | Evita morte em cascata |

## 5. Casos de borda

- O feixe sobrepõe vários inimigos ao mesmo tempo: cada um recebe dano de forma independente. (Coberto: `LightDamageArea` mantém uma lista de inimigos no feixe.)
- O inimigo é removido da cena (troca de sala, `QueueFree`) enquanto está no feixe: não pode gerar erro nem deixar referência a um nó já liberado. (Coberto: `IsInstanceValid` antes de aplicar dano.)
- O jogador troca entre modo manual e automático da lanterna: o dano continua dependendo só da sobreposição com o feixe. (Coberto: o feixe é filho de `FlashlightAnchor`.)
- Um inimigo devolvido ao pool pode ficar um frame a mais na lista do feixe e receber `TakeDamage` com vida ≤ 0. Sem proteção, `Die()` rodaria de novo e, com `OnDied` já zerado, chamaria `QueueFree()` num inimigo que o pool ainda guarda. (Protegido pela trava `_isDead`; falta testar na `sala_ninho`.)
- Fase 2: fragmentos recém-nascidos dentro do feixe ficam invulneráveis por um curto tempo.

## 6. Arquivos e cenas

- Existentes: `FractalEnemy.cs`, `fractal_enemy.tscn`, `LightDamageArea.cs`.
- Somente leitura: `Player.cs` (como a lanterna e o feixe são criados), `player.tscn` (o nó `FlashlightAnchor/LightDamageArea` com `CollisionPolygon2D`), `NinhoSpawner.cs` e `NinhoSpawnerPooled.cs` (criação dos inimigos; a `sala_ninho.tscn` usa a versão com pool).
- Novos a criar: nenhum previsto.

## 7. Passos no editor (feitos pelo dev)

- Colisão: nem o inimigo nem o feixe definem camada ou máscara, então os dois usam o padrão (camada 1, máscara 1) e o feixe detecta o inimigo. Se alguém mudar as camadas no Inspector, a máscara da `LightDamageArea` precisa continuar incluindo a camada do `FractalEnemy`.
- Motion Mode: o nó raiz `FractalEnemy` usa **Floating** (Inspector > CharacterBody2D > Motion Mode). É o modo para visão de cima e gravidade zero; não voltar para Grounded.
- Grupo: a cena usa o grupo `"Enimies"` (com erro de digitação) e o `NinhoSpawner.cs` adiciona `"Enemies"`. O dano por luz não depende de grupo (identifica o inimigo pelo tipo), então isso não afeta esta spec.

## 8. Pronto quando

### Fase 1

- [ ] `dotnet build` sem erros
- [ ] O inimigo aparece com a árvore fractal desenhada e com o ângulo dos ramos pulsando
- [ ] Sob o feixe a vida cai na taxa configurada; fora do feixe não muda
- [ ] Os atributos `[Export]` da seção 4 podem ser ajustados no Inspector sem mexer no código
- [ ] Nenhum erro no painel Output/Debugger (Saída/Depurador) ao matar 10 inimigos seguidos e trocar de sala

### Fase 2 (adiada)

- [ ] Ao morrer, a geração 0 e a geração 1 geram 2 fragmentos menores; a geração 2 não gera nada
- [ ] Os parâmetros de geração podem ser ajustados no Inspector

## 9. Como testar (roteiro manual no editor)

1. Abra `arena.tscn` (cena de protótipo, fora da demo; tem jogador, `CanvasModulate`, fundo e 9 inimigos) e rode a cena com F6.
2. Confirme que os inimigos aparecem desenhados e com os ramos pulsando.
3. Aponte a lanterna para um inimigo e confirme que ele morre depois de alguns segundos de luz contínua. Tire o feixe no meio do caminho e confirme que a vida não regenera.
4. Mude `DamagePerSecond` da `LightDamageArea` (em `player.tscn`) no Inspector e confirme que o tempo para matar muda.
5. Fase 2 (quando implementada): mate um inimigo da geração 0 e confirme 2 fragmentos menores; mate os dois e confirme mais 2 cada; mate os últimos e confirme que nada nasce.

## 10. Decisões e notas

### Decididas

- O que pulsa: o **ângulo** dos ramos (como já está no código).
- Dano da luz: mantém o valor atual do código (35), ainda em teste.
- Flash de dano: não mexer agora.
- Divisão em fragmentos: adiada. Quando for feita, o próprio inimigo cria os fragmentos.
- Pulsação configurável: ângulo base, amplitude e frequência viraram `[Export]`, com a frequência em Hz (ciclos por segundo).
- Motion Mode Floating no inimigo (seção 11).
- Renderização em lote (R1–R3) adiada: o desenho não é o gargalo hoje (seção 11).
- Movimento e colisão da horda: separação (B) e descanso de inimigos travados (A) implementados em `docs/specs/fractal-enemy-movement.md`. Os inimigos agora ficam na camada 2 (máscara 1).

### Em aberto

- Limite de inimigos vivos ao mesmo tempo por sala, por desempenho (decidir na spec da `sala_ninho`).
- O dano por tiros entra numa spec própria ou nesta?

### Observações do código (sem ação por enquanto)

- `TakeDamage` é chamado a cada frame sob o feixe e soma `_time += 0.2f` a cada chamada. Com isso a pulsação acelera muito enquanto o inimigo está na luz, e o flash branco fica ligado o tempo todo. Registrado para revisar junto com o flash.
- O `CollisionShape2D` do inimigo tem um círculo de raio 30 com o nó escalado para 0.2 (raio efetivo de ~6 px). A recomendação da Godot é ajustar o raio da forma em vez de escalar o nó de colisão.
- O `Player` também usa Motion Mode Grounded (padrão). Avaliar Floating na spec do jogador; afeta como ele desliza nas paredes (game feel).

## 11. Diagnóstico de desempenho (set/2026)

### Medições

Cena `sala_ninho.tscn` com 300 inimigos (`NinhoSpawnerPooled`), V-Sync desligado só durante o teste, lanterna com dano zerado para os inimigos não morrerem. Valores dos Monitores da Godot (pior caso do último segundo) e do Analisador Gráfico.

| Teste | Configuração do `FractalEnemy` | FPS | Processo de Física | Pares de Colisão | Renderização (CPU) |
|---|---|---|---|---|---|
| **E1** (linha de base) | Grounded, camada 1 / máscara 1 | 48 | 16,29 ms | 777 | 1,55 ms |
| **E3a** | sem colisão (camada 0 / máscara 0) | 431 | 6,00 ms | 3 | 1,14 ms |
| **E3b** | camada 2 / máscara 1 (colide com paredes, não entre si) | quedas de ~400 para 30–40 quando a horda encosta na parede | — | — | — |
| **E4** | **Floating**, camada 1 / máscara 1 | 197 | 11,63 ms | 816 | 1,54 ms |

### Conclusões

- O gargalo é a **colisão**, não o desenho. A renderização custa ~1,5 ms e as chamadas de desenho já são poucas (9–25), porque a Godot agrupa as linhas dos inimigos.
- Colidir com outros inimigos e colidir com paredes pesam os dois (E3b). O fator comum é cada inimigo empurrar contra um obstáculo a cada passo de física, mesmo sem conseguir avançar.
- O modo Grounded (de jogo de plataforma) quase dobrava o custo de cada contato, com a lógica de chão, snap e rampa. O Floating reduziu a física em ~29% com praticamente os mesmos contatos.
- A física roda em passos fixos de 1/60 s (16,67 ms). Quando um passo passa desse limite, o motor executa vários passos por frame e o FPS despenca de forma desproporcional. No E4 o passo ficou abaixo do limite, mas com pouca folga: há picos perto de 17 ms.
- Mesmo sem colisão (E3a), 300 inimigos custam ~6 ms por passo (`_PhysicsProcess` em C# + `MoveAndSlide`).

### Direções para a spec de movimento (não decididas)

| Direção | Ideia | Observação |
|---|---|---|
| **A** | Inimigo bloqueado para de empurrar | Ataca a causa comum (paredes e inimigos) |
| **B** | Separação suave em código em vez de colisão física entre inimigos | Mexe na colisão mútua ("nuvem"); decisão de design |
| **C** | Atualizar o movimento a cada 2 ou 3 passos, de forma escalonada | Movimento pode ficar menos suave |
| **D** | Limite de inimigos vivos por sala | Decisão de design; ligada à spec da `sala_ninho` |
| E5 | Max Slides de 4 para 2 (só Inspector) | Teste opcional, ainda não feito |

### Adiado: renderização em lote (R1–R3)

Proposta avaliada e adiada, porque o desenho não é o gargalo hoje. Retomar quando partículas, novos inimigos ou efeitos pesarem na renderização (medir antes com o Analisador Gráfico).

- **R1:** gerar uma vez, via `SubViewport`, texturas das variações do fractal (e quadros da pulsação do ângulo) como `ImageTexture` em memória.
- **R2:** um `FractalSwarmRenderer` com `MultiMeshInstance2D` desenha todos os inimigos a partir de um registro central de inimigos ativos.
- **R3:** remover `_Draw`/`DrawFractalBranch` do `FractalEnemy.cs`.

Pontos já levantados para quando for retomado:
- O registro precisa cobrir os três fluxos de criação: pool (`ActivateAt`), inimigos colocados à mão na cena (`arena.tscn`) e `NinhoSpawner.cs` sem pool. Registrar ao entrar na cena ou ativar; desregistrar ao desativar ou sair da cena.
- Pulsação do ângulo exige quadros pré-gerados (textura estática não pulsa).
- Uma única chamada de desenho exige atlas + shader (exceção à regra de shaders da etapa 1). Sem shader, um MultiMesh por textura pode *aumentar* as chamadas de desenho em relação às 9–25 atuais.
- Parâmetros visuais passariam do inimigo para o renderizador (aparência compartilhada).
- Os fragmentos (fase 2) têm profundidade menor e precisariam de texturas próprias.
