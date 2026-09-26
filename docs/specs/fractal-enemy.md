# Spec: Inimigo fractal (dano por luz e divisão celular)

- **Status:** rascunho. Valores marcados com (proposto) precisam da sua aprovação.
- **Etapa:** 1 (mecânica)
- **Depende de:** lanterna e `LightDamageArea.cs` (dano por luz). Nenhuma outra spec.

## 1. Objetivo

O anticorpo fractal é o inimigo da demo. Ele é desenhado por código como uma árvore de ramos binários que pulsa, e é destruído pela luz da lanterna. Ao morrer, divide-se em fragmentos menores (divisão celular), criando a pressão de horda da sala do ninho. Serve ao trecho COMBATER do core loop.

## 2. Comportamento esperado

- Quando o inimigo é instanciado, então desenha, via `_Draw`, uma árvore fractal binária recursiva com profundidade definida por parâmetro.
- Enquanto vive, o desenho pulsa em ciclo senoidal (proposto: a pulsação varia a escala).
- Quando o feixe da lanterna sobrepõe o inimigo, então ele perde vida continuamente: dano por segundo × `delta` (proposto).
- Quando o inimigo sai do feixe, então a vida para de cair e não regenera (proposto).
- Quando a vida chega a zero, então o inimigo morre e gera 2 fragmentos da geração seguinte, cada um menor e com menos vida, afastados alguns pixels em direções opostas.
- Quando um inimigo da geração máxima morre, então não gera fragmentos.

## 3. Fora do escopo

- Movimento, perseguição e ataque ao jogador (spec própria depois).
- Partículas neon e iluminação temporária na morte (spec própria).
- Coleta de biomassa (spec própria).
- Dano por tiros ou explosões.
- Arte final, áudio e shaders (etapa 2).

## 4. Parâmetros ajustáveis (todos propostos)

| Parâmetro | Valor inicial | Onde fica | Observação |
|---|---|---|---|
| Vida da geração 0 | 100 | `FractalEnemy.cs` | Geração 1 = 50, geração 2 = 25 |
| Fator de vida por geração | 0.5 | `FractalEnemy.cs` | Multiplica a cada divisão |
| Dano da luz por segundo | 40 | `LightDamageArea.cs` | Mata a geração 0 em ~2,5 s de luz contínua |
| Profundidade da árvore | 5 | `FractalEnemy.cs` | Diminui 1 por geração |
| Geração máxima | 2 | `FractalEnemy.cs` | 1 + 2 + 4 = 7 inimigos por ancestral |
| Fator de escala por geração | 0.6 | `FractalEnemy.cs` | Fragmentos menores |
| Pulsação | 1.5 ciclos/s, ±10% | `FractalEnemy.cs` | Frequência e amplitude separadas |

O agente confirma, lendo o código, onde cada valor deve ficar.

## 5. Casos de borda

- O feixe sobrepõe vários inimigos ao mesmo tempo: cada um recebe dano de forma independente.
- Fragmentos recém-nascidos dentro do feixe: ficam invulneráveis por ~0,3 s para não haver morte em cascata no mesmo instante (proposto).
- O inimigo é removido da cena (troca de sala, `QueueFree`) enquanto está no feixe: não pode gerar erro nem deixar sinal conectado a um nó já liberado.
- O jogador troca entre modo manual e automático da lanterna: o dano continua dependendo só da sobreposição com o feixe.

## 6. Arquivos e cenas

- Existentes (ler antes de planejar e resumir o que já está feito e o que falta): `FractalEnemy.cs`, `fractal_enemy.tscn`, `LightDamageArea.cs`. Somente leitura: `Player.cs` (como a lanterna e o feixe são criados).
- Novos a criar: nenhum previsto. O agente propõe no plano se precisar.

## 7. Passos no editor (feitos pelo dev)

O agente escreve o passo a passo depois de ler `fractal_enemy.tscn`. Ponto de atenção: o inimigo precisa ser detectável pela `Area2D` do feixe, então as camadas e máscaras de colisão dos dois precisam se encaixar.

## 8. Pronto quando

- [ ] `dotnet build` sem erros
- [ ] O inimigo aparece com a árvore fractal desenhada e pulsando
- [ ] Sob o feixe a vida cai na taxa configurada; fora do feixe não muda
- [ ] Ao morrer, a geração 0 e a geração 1 geram 2 fragmentos menores; a geração 2 não gera nada
- [ ] Todos os valores da seção 4 podem ser ajustados no Inspector sem mexer no código
- [ ] Nenhum erro no painel Output/Debugger (Saída/Depurador) ao matar 10 inimigos seguidos e trocar de sala

## 9. Como testar (roteiro manual no editor)

1. Abra uma cena que tenha o inimigo e o jogador (pode ser `arena.tscn`; confirmar) e rode a cena com F6.
2. Aponte a lanterna para o inimigo e confirme que a vida cai (o agente pode adicionar um `GD.Print` temporário; remover depois).
3. Mate um inimigo da geração 0 e confirme 2 fragmentos menores; mate os dois e confirme mais 2 cada; mate os últimos e confirme que nada nasce.
4. Mude o dano da luz no Inspector e confirme que o tempo para matar muda.

## 10. Decisões em aberto e notas

- O que pulsa: a escala (proposto) ou o brilho?
- Limite de inimigos vivos ao mesmo tempo por sala, por desempenho (decidir na spec da `sala_ninho`).
- O dano por tiros entra numa spec própria ou nesta?
