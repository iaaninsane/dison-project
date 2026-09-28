# Spec: <nome da mecânica>

> Como usar: copie este arquivo para `docs/specs/<nome-da-mecanica>.md` e preencha pelo menos as seções 1, 2, 3 e 8. O resto pode ser proposto pelo agente e aprovado por você. Apague esta linha depois.

- **Status:** rascunho | aprovada | em implementação | pronta para teste | validada
- **Etapa:** 1 (mecânica)
- **Depende de:** <outras specs, cenas ou "nada">

## 1. Objetivo

1 a 3 frases: o que o jogador faz ou sente e como isso serve ao core loop (explorar → alertar → combater → coletar → evoluir).

## 2. Comportamento esperado

Regras curtas e verificáveis, uma por linha, no formato "Quando X, então Y".

- Quando ..., então ...

## 3. Fora do escopo

O que NÃO fazer nesta spec (outras mecânicas, arte final, áudio, polimento).

- ...

## 4. Parâmetros ajustáveis

Valores de balanceamento que devem ser `[Export]` (editáveis no Inspector), sem número mágico no código.

| Parâmetro | Valor inicial | Onde fica | Observação |
|---|---|---|---|
| ... | ... | `Classe.cs` | ... |

## 5. Casos de borda

- ...

## 6. Arquivos e cenas

- Existentes (o agente lê antes de planejar e diz o que já está feito): ...
- Novos a criar: ...

## 7. Passos no editor (feitos pelo dev)

Só o que exige a interface da Godot: nós, grupos, camadas de colisão, propriedades no Inspector. O agente escreve o passo a passo numerado; o dev executa.

1. ...

## 8. Pronto quando

Critérios testáveis. Se não dá para marcar sim ou não, reescreva.

- [ ] `dotnet build` sem erros
- [ ] ...

## 9. Como testar (roteiro manual no editor)

1. ...

## 10. Decisões em aberto e notas

- ...
