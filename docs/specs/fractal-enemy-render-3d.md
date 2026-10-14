# Spec: Visual 3D do inimigo fractal (projeção no shader + linhas em tubo)

- **Status:** proposta. Não implementar antes de concluir a etapa 1.
- **Etapa:** 2 (arte e shaders)
- **Depende de:** `docs/specs/fractal-enemy.md` (fases 1 e 2 implementadas). Como a divisão celular (fase 2) já existe, esta spec precisa cobrir os fragmentos (seção 5): profundidade e tamanho variam por geração (`_drawDepth`, `_drawBranchLength` em `FractalEnemy.cs`).

> **Valores em teste:** como nas demais specs da demo, os `[Export]` e os números desta spec são pontos de partida, não valores definitivos.

## 1. Objetivo

Dar ao anticorpo fractal a aparência de uma estrutura 3D girando, sem mudar nada da mecânica: a mesma árvore binária, a mesma pulsação de ângulo, o mesmo dano por luz, a mesma colisão. Muda só o desenho.

Duas técnicas combinadas:

1. **Projeção 3D no shader de vértice:** os ramos passam a ter coordenadas 3D. Rotação e perspectiva são calculadas na GPU.
2. **Linhas em tubo com normais procedurais:** cada ramo é desenhado como uma faixa cujo shader calcula uma normal cilíndrica. Assim, o feixe da lanterna, que já causa dano, também revela o volume do inimigo, reforçando visualmente a mecânica.

## 2. Comportamento esperado

- Quando o inimigo é instanciado, então exibe a árvore fractal com a mesma topologia da fase 1 (troncos, ramos binários, profundidade de `RecursionDepth`), agora distribuída em 3D.
- Enquanto vive, a árvore gira lentamente em 3D, com fase própria por inimigo (inimigos vizinhos não giram sincronizados).
- Enquanto vive, o **ângulo de abertura dos ramos continua pulsando** (decisão da spec original), com `BaseBranchAngle`, `PulseAmplitude` e `PulseFrequency`.
- Quando o feixe da lanterna ilumina o inimigo, então os tubos mostram lado iluminado e lado em sombra conforme a direção da luz.
- Ao receber dano, o flash branco continua funcionando, agora como parâmetro do shader.
- A visibilidade do inimigo fora do feixe (no escuro do `CanvasModulate`) deve ser a mesma de hoje. Esta spec não muda a legibilidade da horda no escuro.
- Colisão, movimento, dano e pool não mudam.

## 3. Fora do escopo

- Mecânica de dano, vida, morte e pool (spec `fractal-enemy.md`).
- Movimento e colisão da horda (spec `fractal-enemy-movement.md`).
- Divisão celular (fase 2 da spec original), exceto o suporte visual a profundidades menores.
- Partículas e iluminação na morte (spec própria).
- Chefes com fractais por raymarching (spec própria).

## 4. Parâmetros ajustáveis (pontos de partida)

| Parâmetro | Valor de partida | Observação |
|---|---|---|
| `NeonColor` | o atual de `FractalEnemy.cs` | Continua sendo a fonte da cor. Ver decisão em aberto sobre a cor |
| `RotationSpeed` | 0.3 rad/s | Velocidade média da rotação 3D |
| `RotationSpeedVariance` | 0.15 rad/s | Variação aleatória por inimigo |
| `TubeWidth` | 2 px | Largura base do tubo |
| `PerspectiveStrength` | a calibrar | Quanto os ramos encolhem ao se afastar |
| `PulseKeyframes` | 8 | Só se a opção P2 (seção 10) for escolhida |

## 5. Casos de borda

- **Inimigo reaproveitado pelo pool (`ActivateAt`):** a fase de rotação e o tempo da pulsação são sorteados de novo, para o inimigo não "renascer" sincronizado com outro.
- **Fragmentos da fase 2:** têm profundidade menor, então precisam de geometria própria por profundidade (uma malha por valor de profundidade, gerada uma vez e compartilhada).
- **Tempo da pulsação sob o feixe:** hoje `TakeDamage` soma `_time += 0.2f` a cada frame, o que acelera a pulsação na luz (observação da spec original). Na versão em shader, o tempo da pulsação deve vir só do relógio do inimigo, sem ser alterado por `TakeDamage`. Se a aceleração for desejada como feedback de dano, ela deve virar um parâmetro explícito.
- **Flash de dano contínuo:** hoje o flash fica ligado o tempo todo sob o feixe. Esta spec só migra o flash para o shader, sem mudar o comportamento. A revisão do flash continua pendente na spec original.

## 6. Arquivos e cenas

- Existentes a alterar: `FractalEnemy.cs` (remover `_Draw`/`DrawFractalBranch` só depois que o novo visual estiver validado), `fractal_enemy.tscn`.
- Novos a criar: gerador da geometria 3D da árvore, shader `canvas_item` do fractal, e uma cena de teste isolada.
- Somente leitura: `Player.cs` e `player.tscn` (lanterna e feixe), `NinhoSpawnerPooled.cs`.

## 7. Passos no editor (feitos pelo dev)

- Conferir se a lanterna é uma `Light2D` que afeta a camada de desenho dos inimigos (`Item Cull Mask` e `Light Mask` compatíveis).
- Ajustar `PerspectiveStrength` e `TubeWidth` no Inspector olhando a cena de teste.

## 8. Pronto quando

- [ ] `dotnet build` sem erros
- [ ] Na cena de teste, a árvore gira em 3D, com ramos passando à frente e atrás uns dos outros
- [ ] A pulsação de ângulo continua visível e respeita os três `[Export]` de pulsação
- [ ] O feixe da lanterna ilumina um lado dos tubos e deixa o outro em sombra
- [ ] O flash de dano continua funcionando
- [ ] Na `sala_ninho` com 300 inimigos, seguindo o mesmo protocolo da seção 11 da spec original (V-Sync desligado, dano zerado), o tempo de Renderização (CPU) é registrado e comparado com a linha de base de 1,55 ms, e o FPS não cai em relação ao último teste de movimento
- [ ] Nenhuma mudança no comportamento de colisão, dano ou pool

## 9. Como testar (roteiro manual no editor)

1. Abra a cena de teste isolada com um único inimigo e uma `PointLight2D` móvel. Confirme a rotação 3D e a reação dos tubos à luz.
2. Abra `arena.tscn`, rode com F6 e aponte a lanterna para os inimigos. Confirme volume, pulsação e flash de dano.
3. Mate 10 inimigos seguidos e troque de sala. Confirme que não há erros no Output/Debugger.
4. Repita o teste de desempenho da seção 11 da spec original na `sala_ninho`.

## 10. Decisões e notas

### Decididas

- O visual 3D é etapa 2. Não entra antes do fechamento da etapa 1.
- A topologia da árvore é a mesma do anticorpo atual (sem trocar por outros tipos de fractal).
- A pulsação continua sendo de ângulo, não de escala.
- Esta spec não toca em física nem em colisão, que é o gargalo medido hoje.

### Em aberto

- **Como distribuir a árvore em 3D.**
  - **T1:** manter os 4 troncos no plano e alternar o plano de bifurcação a cada nível (cada nível gira 90° em torno do eixo do ramo pai). A silhueta continua reconhecível, com volume.
  - **T2:** 6 troncos, um para cada direção dos eixos X, Y e Z. Mais volumétrico, mas muda a silhueta e dobra o número de segmentos de tronco.
- **Como manter a pulsação de ângulo no shader.** Mudar o ângulo de um ramo mexe em todos os descendentes, então não é uma rotação simples por vértice.
  - **P1:** o C# recalcula os pontos 3D na taxa de `RedrawsPerSecond` e atualiza a malha. É simples, mas cada inimigo precisa da própria malha.
  - **P2 (recomendada):** gerar uma vez `PulseKeyframes` versões da árvore ao longo do ciclo da pulsação. Como a topologia não muda, os segmentos se correspondem um a um, e o shader interpola entre as duas versões vizinhas conforme a fase. A malha é compartilhada e a pulsação continua suave.
- **Onde ficam os pontos 3D no shader.** Verificar quais atributos de vértice existem em shaders `canvas_item` na versão de Godot do projeto. Se não houver espaço, guardar os segmentos (e os quadros de P2) numa textura de dados e buscá-los com `texelFetch`.
- **Como desenhar a horda.**
  - **D1 (começar por aqui):** cada inimigo ganha um nó filho com a malha compartilhada e o material do shader. Mantém a arquitetura atual, sem registro central. Verificar se a Godot permite parâmetros por instância nesse tipo de shader 2D (fase de rotação, tempo da pulsação, flash) sem duplicar o material, porque material duplicado pode quebrar o agrupamento das chamadas de desenho.
  - **D2:** retomar a renderização em lote (R2 da spec original) com `MultiMeshInstance2D`, passando os parâmetros por `INSTANCE_CUSTOM`. Neste caso, o shader 3D substitui o bake de texturas do R1, e os pontos já levantados sobre o registro central continuam válidos. Só ir para D2 se as medições de D1 mostrarem que o desenho virou gargalo.
- **Blending aditivo e iluminação.** O brilho aditivo dispensa ordenar os ramos por profundidade, mas pode "lavar" o efeito da luz da lanterna. Testar: corpo do tubo com iluminação normal e um brilho aditivo separado e mais fraco, ou só iluminação.
- **Cor.** O código usa `NeonColor` verde; os mockups de direção de arte usaram magenta para os inimigos. Decidir na spec de arte. Esta spec só lê `NeonColor`.
