# Documentação de Implementação --- Sistema de Level Up da Demo

**Status:** especificação funcional inicial\
**Objetivo:** documentar as decisões de design do sistema de progressão
para implementação na ferramenta de desenvolvimento assistida por IA.\
**Escopo:** progressão durante uma partida (run), pontos de melhoria,
seleção de upgrades, atributos e valores iniciais sugeridos.

------------------------------------------------------------------------

## 1. Objetivo do sistema

Implementar um sistema de progressão inspirado na combinação de ideias
de **Vampire Survivors** e **Nova Drift**. Durante a partida, o jogador
coleta biomassa deixada por inimigos derrotados, acumula experiência e
sobe de nível. Cada level up concede um ponto de melhoria que pode ser
gasto quando o jogador decidir abrir o menu de melhorias.

O sistema deve permitir que o jogador construa seu personagem
gradualmente durante a partida, sem exigir que ele interrompa a ação
automaticamente a cada level up.

## 2. Decisões de design confirmadas

As regras abaixo representam as decisões tomadas para esta versão da
demo.

1.  **Biomassa funciona como experiência (XP).** A biomassa coletada
    pelo jogador alimenta a progressão de nível.
2.  **Subir de nível concede um ponto de melhoria.** Cada nível
    alcançado depois do nível inicial concede exatamente 1 ponto.
3.  **O jogo não pausa automaticamente ao subir de nível.** A partida
    continua normalmente quando um ponto é recebido.
4.  **Os pontos podem ser acumulados.** O jogador não é obrigado a
    gastar um ponto assim que o recebe.
5.  **O jogador abre o menu de melhorias manualmente**, quando quiser
    gastar pontos disponíveis.
6.  **O jogo pausa enquanto o menu de melhorias estiver aberto.** Ao
    fechar o menu, a partida retoma o estado normal.
7.  **Cada escolha apresenta 3 melhorias aleatórias.** O jogador escolhe
    uma das três opções disponíveis.
8.  **Cada escolha consome 1 ponto de melhoria.** Se houver mais pontos
    disponíveis, o jogador pode fazer outra escolha.
9.  **Cada nova escolha gera um novo conjunto de opções aleatórias.**
    Por exemplo, ao gastar o primeiro de três pontos, o próximo conjunto
    de três opções deve ser sorteado novamente.
10. **Não deve haver opções duplicadas dentro do mesmo conjunto de três
    cartas.**
11. **Uma melhoria não pode ser oferecida se o atributo correspondente
    já estiver no nível máximo.**
12. **A progressão é válida somente durante a partida atual.** Ao
    iniciar uma nova partida, os atributos, níveis de melhoria, XP e
    pontos retornam aos valores iniciais. Não há melhorias permanentes
    entre partidas nesta demo.
13. **O sistema deve respeitar os limites de nível de cada atributo.**
    Uma melhoria pode ser escolhida repetidamente em escolhas
    diferentes, desde que o atributo ainda não esteja no nível máximo.
14. **A interface deve mostrar claramente os pontos disponíveis** e as
    informações necessárias para comparar as três opções.

## 3. Fluxo de experiência e level up

### 3.1 Coleta de biomassa

-   Inimigos derrotados podem deixar fragmentos de biomassa, conforme já
    previsto na demo.
-   Quando o jogador coleta um fragmento, a quantidade correspondente
    deve ser adicionada à experiência atual.
-   O valor de XP concedido por fragmento ainda precisa ser definido ou
    confirmado a partir da implementação existente. Não presumir que
    cada fragmento vale 1 XP se o jogo já possuir outra regra.

### 3.2 Subida de nível

-   O sistema compara a XP acumulada com o requisito para o próximo
    nível.
-   Ao atingir o requisito, o jogador sobe de nível e recebe 1 ponto de
    melhoria.
-   A XP excedente deve ser preservada para o próximo nível; não deve
    ser descartada ao cruzar um limite.
-   Se uma única coleta adicionar XP suficiente para cruzar mais de um
    limite, o sistema deve processar todos os level ups correspondentes
    e conceder 1 ponto por nível conquistado.
-   O level up não abre automaticamente o menu e não pausa o jogo.
-   O menu só é aberto por solicitação do jogador, se houver pelo menos
    1 ponto disponível.

### 3.3 Fórmula ou tabela de XP

A curva abaixo é uma **proposta inicial de balanceamento**, não uma
decisão definitiva. Deve ser configurável e ajustada durante os testes.

  -----------------------------------------------------------------------
  Transição de nível       XP necessária para a   XP total acumulada como
                                      transição                referência
  ------------------- ------------------------- -------------------------
  Nível 1 → 2                                10                        10

  Nível 2 → 3                                15                        25

  Nível 3 → 4                                21                        46

  Nível 4 → 5                                28                        74

  Nível 5 → 6                                36                       110

  Nível 6 → 7                                45                       155

  Nível 7 → 8                                55                       210

  Nível 8 → 9                                66                       276

  Nível 9 → 10                               78                       354
  -----------------------------------------------------------------------

**Implementação recomendada:** manter o nível atual, a XP acumulada e a
XP necessária para o próximo nível em um componente responsável pela
experiência. A tabela/fórmula não deve ficar espalhada pela lógica de
combate, coleta ou interface.

### 3.4 Barra visual de biomassa/XP

A interface da partida deve exibir uma barra de XP para que o jogador acompanhe visualmente seu progresso até o próximo nível.

- **Visibilidade:** a barra deve permanecer visível durante a partida, seguindo o layout e a linguagem visual já usados pela HUD da demo.
- **Preenchimento:** representa a XP acumulada em relação à XP necessária para alcançar o próximo nível. Exemplo: se o jogador tem 5 de 10 XP, a barra deve estar preenchida em 50%.
- **Atualização:** atualizar o preenchimento sempre que o jogador coletar biomassa e sempre que ocorrer uma mudança de nível.
- **Indicador de nível:** exibir o nível atual junto à barra ou em uma posição visualmente próxima.
- **Ao subir de nível:** atualizar a referência da barra para o requisito do próximo nível. A XP excedente deve ser preservada e refletida no novo preenchimento; não descartar XP excedente apenas para fazer a barra voltar a zero.
- **Múltiplos níveis em uma coleta:** se uma coleta provocar mais de um level up, processar todos os níveis correspondentes e deixar a barra refletindo corretamente o progresso restante em direção ao próximo nível.
- **Pontos de melhoria separados:** mostrar a quantidade de pontos disponíveis em um indicador separado da barra de XP. Ganhar ou gastar pontos não deve, por si só, alterar a XP nem o preenchimento da barra.
- **Menu fechado:** a barra continua atualizando normalmente durante a partida, mesmo quando o jogador ainda não gastou seus pontos.
- **Coerência com o sistema atual:** antes de criar uma nova barra, inspecionar a HUD existente e reutilizar ou adaptar seus elementos, evitando duplicação visual ou lógica.

A implementação deve distinguir claramente:
1. **XP acumulada para o próximo nível**, representada pela barra;
2. **Nível atual do jogador**, exibido como número;
3. **Pontos de melhoria disponíveis**, usados no menu de upgrades.

O visual exato (cores, dimensões, animação e posição) deve seguir o estilo já existente na demo, em vez de impor uma nova identidade visual.

## 4. Pontos de melhoria e menu

### 4.1 Abrir o menu

-   O menu pode ser aberto quando houver pelo menos 1 ponto de melhoria
    disponível.
-   A abertura deve pausar a simulação da partida, incluindo movimento e
    combate, conforme o mecanismo de pausa já usado pelo projeto.
-   Se não houver pontos disponíveis, o menu não deve permitir uma
    escolha. A interface pode impedir a abertura ou exibir que não há
    pontos, conforme o padrão visual existente.
-   O jogador deve poder fechar o menu sem gastar um ponto, mantendo os
    pontos disponíveis para depois.

### 4.2 Exibir opções

Ao abrir o menu para gastar um ponto:

1.  Consultar as melhorias elegíveis naquele momento.
2.  Sortear até 3 melhorias distintas.
3.  Não incluir atributos que já estejam no nível máximo.
4.  Exibir as opções em três cartas, quando houver três opções
    elegíveis.
5.  Se restarem menos de três melhorias elegíveis, exibir apenas as
    opções disponíveis, sem duplicá-las artificialmente.
6.  Se não houver nenhuma melhoria elegível, não consumir o ponto
    automaticamente; apresentar uma mensagem adequada ou tratar a
    situação conforme a regra de fim de progressão definida pelo
    projeto.

A aleatoriedade ocorre **a cada escolha**, não quando o jogador ganha o
ponto. Assim, se o jogador tiver vários pontos acumulados, cada ponto
gasto gera um conjunto novo de opções.

### 4.3 Escolher uma melhoria

Quando o jogador seleciona uma carta válida:

1.  Validar que ainda há pelo menos 1 ponto disponível.
2.  Validar que a melhoria continua elegível e que o atributo não
    atingiu o máximo.
3.  Aplicar a melhoria ao estado da partida.
4.  Atualizar o nível do atributo e seus valores efetivos.
5.  Consumir exatamente 1 ponto.
6.  Atualizar a interface.
7.  Se ainda houver pontos, permitir outra escolha, com um novo sorteio
    de opções.
8.  Se não houver mais pontos, fechar o menu automaticamente ou permitir
    que o jogador o feche, de acordo com a convenção de interface
    adotada. Esta decisão de comportamento visual pode ser definida
    durante a integração.

A operação deve ser protegida contra cliques repetidos ou eventos
duplicados, para evitar consumir mais de um ponto ou aplicar a mesma
melhoria duas vezes por uma única seleção.

## 5. Atributos da demo

A demo possui cinco atributos de progressão. A proposta inicial é usar
**níveis de atributo de 1 a 5**, sendo nível 1 o valor inicial da
partida.

> Os valores abaixo são referências de balanceamento para iniciar a
> implementação. Devem ser conferidos contra os valores-base já
> existentes no projeto e ajustados por teste. Não substituir
> silenciosamente parâmetros existentes sem verificar a implementação
> atual.

### 5.1 Vida

-   **Função:** define a vida máxima do jogador.
-   **Valor inicial sugerido:** 100.
-   **Nível máximo sugerido:** 5.
-   **Progressão sugerida:**

  Nível     Vida máxima
  ------- -------------
  1                 100
  2                 120
  3                 140
  4                 160
  5                 180

-   **Efeito sugerido por melhoria:** aumentar a vida máxima em 20
    pontos e recuperar imediatamente até 20 pontos de vida.
-   A vida atual nunca pode ultrapassar a vida máxima atual.
-   Se a vida atual estiver baixa, a recuperação deve respeitar o limite
    da nova vida máxima. A implementação deve definir claramente como
    tratar a cura imediata para não ultrapassar esse limite.

### 5.2 Coletor magnético de biomassa

-   **Função:** atrai/coleta fragmentos de biomassa próximos ao jogador.
-   **Efeito de progressão:** aumenta o raio de coleta.
-   **Nível máximo sugerido:** 5.
-   **Multiplicadores sugeridos em relação ao raio-base:**

  Nível     Raio de coleta
  ------- ----------------
  1                   100%
  2                   125%
  3                   150%
  4                   175%
  5                   200%

-   A melhoria deve alterar o raio de coleta usado pelo sistema atual,
    sem criar um segundo sistema de coleta concorrente.

### 5.3 Campo de força básico

-   **Função:** repele inimigos quando entram em contato com o campo.
-   **Dano:** o campo não causa dano aos inimigos.
-   **Resistência:** na configuração inicial, o campo suporta 1 colisão
    e depois desativa.
-   **Efeito de progressão:** aumenta em 1 o número de colisões
    suportadas por nível.

  Nível     Colisões suportadas antes de desativar
  ------- ----------------------------------------
  1                                              1
  2                                              2
  3                                              3
  4                                              4
  5                                              5

-   **Reativação:** foi sugerido que o campo desativado volte a
    funcionar após um tempo de recarga fixo. O tempo de recarga ainda
    precisa ser definido/confirmado com a implementação existente; não
    inventar um valor definitivo.
-   A progressão deve ajustar a resistência do campo sem alterar sua
    função básica de repulsão sem dano.

### 5.4 Dano da lanterna

-   **Função:** arma principal do jogador.
-   **Comportamento:** um feixe de luz causa dano contínuo enquanto
    estiver apontado para um inimigo e atingindo-o.
-   **Efeito de progressão:** aumenta o dano por segundo (DPS) do feixe.
-   **Nível máximo sugerido:** 5.

  Nível     Dano relativo ao valor-base
  ------- -----------------------------
  1                                100%
  2                                125%
  3                                150%
  4                                175%
  5                                200%

Exemplo ilustrativo: se o dano-base for 10 unidades por segundo, os
níveis seriam 10, 12,5, 15, 17,5 e 20 DPS. O valor-base real deve vir da
implementação atual.

### 5.5 Alcance da lanterna

-   **Função:** define a distância máxima na qual o feixe da lanterna
    pode atingir inimigos.
-   **Efeito de progressão:** aumenta o alcance máximo.
-   **Nível máximo sugerido:** 5.

  Nível     Alcance relativo ao valor-base
  ------- --------------------------------
  1                                   100%
  2                                   115%
  3                                   130%
  4                                   145%
  5                                   160%

-   A melhoria deve atualizar o limite real do feixe. Não deve alterar o
    dano, que pertence ao atributo "Dano da lanterna".

## 6. Interface do menu de melhorias

Cada carta de melhoria deve apresentar, quando aplicável:

-   Nome da melhoria.
-   Ícone ou representação visual.
-   Nível atual do atributo.
-   Nível após a escolha.
-   Descrição curta e objetiva do efeito.
-   Indicação de que a opção pode ser escolhida.
-   Quantidade de pontos de melhoria disponíveis em local visível no
    menu.

Exemplo de conteúdo para uma carta:

**Dano da lanterna**\
Nível 2 → Nível 3\
Aumenta o dano contínuo do feixe de 125% para 150% do valor-base.

A interface deve se adaptar ao sistema de entrada já existente (mouse,
teclado e/ou controle), sem pressupor uma tecnologia de UI específica. O
menu deve seguir a linguagem visual da demo.

## 7. Reinicialização no início de uma nova partida

Ao iniciar uma nova run, reinicializar:

-   Nível do jogador para o nível inicial.
-   XP acumulada.
-   XP necessária para o próximo nível, conforme a curva definida.
-   Pontos de melhoria disponíveis para zero.
-   Nível de todos os atributos para 1.
-   Valores efetivos dos atributos para seus valores-base.
-   Estado de ativação/recarga do campo de força, conforme a regra
    normal de início de partida.

Não persistir melhorias de uma partida para outra nesta versão.

## 8. Organização lógica sugerida

A arquitetura abaixo é uma sugestão independente de engine. Adaptar aos
padrões e componentes já existentes; não criar sistemas duplicados caso
o projeto já tenha componentes equivalentes.

### `ExperienceManager` (ou componente equivalente)

Responsabilidades: - Registrar XP recebida da coleta de biomassa. -
Manter o nível atual e a XP acumulada. - Verificar e processar um ou
vários level ups. - Conceder 1 ponto por nível conquistado. - Preservar
XP excedente. - Informar a interface sobre mudanças relevantes.

### `UpgradeManager`

Responsabilidades: - Manter a quantidade de pontos disponíveis, ou
coordenar sua gestão com o componente de progressão. - Encontrar
melhorias elegíveis. - Sortear até três opções sem duplicatas. - Validar
a escolha. - Aplicar a melhoria e consumir um ponto. - Solicitar um novo
sorteio quando houver outra escolha.

### `UpgradeDefinition` (ou configuração equivalente)

Representar os dados de uma melhoria, por exemplo: - Identificador
único. - Nome. - Descrição. - Ícone, se usado. - Atributo afetado. -
Nível máximo. - Regras de elegibilidade, se houver. - Dados necessários
para calcular/aplicar o efeito.

Os dados de balanceamento devem ficar em um local fácil de ajustar, de
preferência em configurações ou recursos de dados compatíveis com a
engine já usada.

### `PlayerStats` (ou componente equivalente)

Responsabilidades: - Manter os níveis atuais dos atributos. - Calcular
ou armazenar os valores efetivos. - Garantir que valores derivados sejam
recalculados ao aplicar uma melhoria. - Oferecer uma forma consistente
para os sistemas de combate, coleta, vida e campo de força consultarem
os valores atuais.

### `LevelUpUI` (ou componente equivalente)

Responsabilidades: - Abrir e fechar o menu. - Pausar e retomar a partida
pelo mecanismo existente. - Mostrar pontos disponíveis. - Mostrar as
opções sorteadas. - Enviar a escolha ao gerenciador. - Atualizar-se após
a escolha ou quando o menu for aberto novamente.

**Importante:** esses nomes são conceituais. Antes de criar classes
novas, inspecionar a estrutura atual do projeto e reutilizar/estender
componentes existentes quando apropriado.

## 9. Melhorias futuras --- fora do escopo inicial

As seguintes ideias foram discutidas como possibilidades inspiradas em
Nova Drift, mas **não fazem parte da implementação obrigatória
inicial**. Não implementar nesta etapa, a menos que seja solicitado
separadamente.

-   **Feixe concentrado:** mais dano e menos alcance.
-   **Feixe ampliado:** mais alcance e menos dano.
-   **Luz persistente:** o feixe continua causando dano por um breve
    período após deixar de atingir o alvo.
-   **Repulsão reforçada:** aumenta o efeito de repulsão do campo de
    força.
-   **Recuperação acelerada:** reduz o tempo de recarga do campo de
    força.
-   **Pulso defensivo:** efeito defensivo adicional relacionado ao
    campo.
-   **Coleta eficiente:** concede uma pequena quantidade adicional de XP
    ao coletar biomassa.
-   Outras sinergias entre atributos e melhorias especiais.

Se forem implementadas no futuro, devem ter requisitos de nível e regras
de elegibilidade próprios, e continuar respeitando o limite de três
opções aleatórias por escolha.

## 10. Critérios de aceitação

A implementação pode ser considerada funcional quando os seguintes
cenários passarem:

1.  Coletar biomassa aumenta a XP do jogador.
2.  Atingir o limite de XP aumenta o nível e concede exatamente 1 ponto
    de melhoria.
3.  Subir de nível não abre o menu nem pausa automaticamente a partida.
4.  A XP excedente é mantida após um level up.
5.  Uma coleta que ultrapasse vários limites processa todos os níveis e
    concede todos os pontos correspondentes.
6.  O jogador pode acumular pontos sem gastá-los.
7.  O menu só permite comprar melhorias quando há pontos disponíveis.
8.  Abrir o menu pausa a partida; fechá-lo retoma a partida.
9.  Cada escolha oferece até três opções aleatórias distintas.
10. A lista de opções é sorteada novamente para cada ponto gasto.
11. Um atributo no nível máximo nunca aparece como opção elegível.
12. Selecionar uma melhoria aplica o efeito correto e consome exatamente
    1 ponto.
13. Se restarem pontos após a escolha, o jogador pode continuar
    escolhendo melhorias.
14. Os valores efetivos de vida, raio de coleta, resistência do campo,
    dano e alcance são atualizados corretamente.
15. A vida atual nunca ultrapassa a vida máxima.
16. Iniciar uma nova run restaura todos os atributos e recursos de
    progressão aos valores iniciais.
17. Não há progressão permanente entre runs.
18. A interface mostra os pontos disponíveis e informa claramente o
    efeito das opções.
19. Eventos duplicados de seleção não aplicam a mesma escolha mais de
    uma vez nem consomem pontos indevidamente.
## 11. Orientação para a IA que implementará o sistema

Antes de modificar o código:

1.  Inspecione a engine, a estrutura de cenas/objetos, os componentes
    existentes e os sistemas atuais de coleta, XP, vida, dano, alcance,
    campo de força e pausa.
2.  Identifique quais componentes podem ser estendidos e evite duplicar
    sistemas já existentes.
3.  Preserve o comportamento atual da demo fora das mudanças necessárias
    para implementar o level up.
4.  Trate os números indicados como valores iniciais de balanceamento,
    não como autorização para substituir arbitrariamente valores-base
    existentes.
5.  Implemente primeiro o fluxo principal e os cinco atributos. Deixe
    melhorias especiais e sinergias para uma etapa futura.
6.  Ao concluir, informe quais arquivos/componentes foram alterados,
    quais decisões de balanceamento exigiram adaptação e como testar
    manualmente cada critério de aceitação.
7.  Se uma decisão necessária não estiver especificada (por exemplo,
    tecla de abertura do menu, valor de XP por fragmento, tempo de
    recarga do campo ou comportamento visual após gastar o último
    ponto), procure primeiro a convenção já usada no projeto. Se não
    houver uma, explicite a escolha proposta em vez de assumir que ela
    já foi decidida.

------------------------------------------------------------------------

## 12. Resumo rápido das regras

-   Biomassa = XP.
-   Cada nível novo = 1 ponto de melhoria.
-   Sem pausa automática ao subir de nível.
-   Pontos acumuláveis.
-   Menu aberto manualmente pelo jogador.
-   Jogo pausado enquanto o menu estiver aberto.
-   Três opções aleatórias por escolha, sem duplicatas.
-   Novo sorteio para cada ponto gasto.
-   Sem opções de atributos no nível máximo.
-   Cinco atributos: vida, coletor magnético, campo de força, dano da
    lanterna e alcance da lanterna.
-   Níveis sugeridos dos atributos: 1 a 5.
-   Melhorias válidas somente durante a run.
-   Nova run restaura o estado inicial.
-   Melhorias especiais e sinergias ficam para depois.
