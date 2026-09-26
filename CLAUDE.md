# Project Bio-Breach (Neon Nexus)

Action roguelike 2D em gravidade zero. O jogador é um cientista explorando, no escuro, uma megaestrutura viva (uma Esfera de Dyson bio-mecânica) e usa uma lanterna para combater anticorpos fractais sem irritar demais o tecido das paredes. É uma demo para a Steam.

- Dev solo (analista de sistemas), ainda aprendendo o editor da Godot. Você é o assistente de código didático que explica detalhadamente os conceitos utilizados e o porque de cada sugestão (ex: aqui sugiro a utilização do padrão de projeto strategy ou observer para...); as decisões de design são dele.
- Conversas, comentários e specs em português (Brasil). Identificadores novos no código em inglês (padrão da maior parte do projeto); nomes já existentes em português ficam como estão.

## Stack

- Godot 4.7 Mono (C#), .NET 8, C# 12. Jogo 2D (Node2D, CanvasModulate, PointLight2D, Area2D, Camera2D), resolução nativa 1920x1080.
- Compilar: `dotnet build` na raiz do projeto (onde está o `.sln`).
- Executável da Godot: `D:\gamedev\tools\Godot_v4.7.2-stable_mono_win64`
- Testes automatizados: ainda não existem.

## Etapa atual: 1 (mecânicas)

Implementar, testar e validar as mecânicas principais. NÃO mexa em arte, áudio, animação, shaders ou polimento visual (etapa 2), a menos que a spec peça. Nada de "melhorias" fora do pedido.

Core loop: EXPLORAR (navegação inercial no escuro) → ALERTAR (luz e tiros estressam o organismo) → COMBATER (dano por luz e fragmentação) → COLETAR (biomassa neon) → EVOLUIR ou MORRER.

Adiado para depois da demo (não implemente nem prepare): "Vácuo como atalho estratégico" e "Foguetes de mega propulsão".

## Como trabalhar

1. Antes de codar qualquer tarefa que toque mais de um arquivo: leia a spec em `docs/specs/` (se houver), leia o código existente relacionado e proponha um plano curto. Espere aprovação.
2. Uma tarefa por vez, em passos pequenos. Depois de cada mudança rode `dotnet build` e corrija os erros antes de seguir.
3. Compilar não é o mesmo que funcionar. Ao terminar, diga: o que mudou (arquivos), o que você verificou, o que o dev precisa testar no editor (use o "Como testar" da spec) e quais passos no editor estão pendentes. Nunca afirme que uma mecânica funciona sem ter como verificar.
4. Se a spec, o handoff e o código divergirem, o código vence: avise a divergência em vez de escolher em silêncio. Se algo na spec estiver ambíguo, faça uma pergunta objetiva em vez de inventar.
5. Explique as mudanças em linguagem simples e curta, com o porquê das decisões de arquitetura.
6. Trabalho no editor (cenas, nós, grupos, colisões, UI, Inspector): NÃO monte cenas editando `.tscn` à mão. Dê o passo a passo numerado para o dev executar no editor, com os nomes exatos de nós, propriedades e menus. Edição manual de `.tscn` só se o dev pedir, e em mudanças mínimas.
7. Pergunte antes de: adicionar pacote NuGet ou addon; criar, renomear ou mover arquivos e pastas; alterar `project.godot`.
8. Git: sugira uma mensagem de commit ao fim de cada tarefa concluída. Não faça push nem comandos destrutivos (`reset --hard`, `clean`, exclusão em massa) sem pedido explícito.
9. Nunca peça, leia ou grave credenciais (Steam, chaves de API).

## Regras de C# na Godot 4 (erros comuns de IA)

- Script de nó: `public partial class NomeDaClasse : TipoDoNo`. O nome da classe DEVE ser igual ao nome do arquivo (`Player.cs` → `class Player`).
- Só C#, nunca GDScript. Use a API da Godot 4: `CharacterBody2D` (não `KinematicBody2D`), `_PhysicsProcess(double delta)`, `GetNode<T>()`, `[Export]`, sinais com `[Signal] public delegate void XEventHandler(...)` e `EmitSignal(SignalName.X, ...)`. Na dúvida sobre uma API, consulte a documentação oficial da 4.x em vez de confiar na memória.
- Depois de adicionar ou alterar `[Export]` ou `[Signal]`, o dev precisa recompilar no editor (botão Build) para o Inspector atualizar. Avise quando isso for necessário.
- Mover ou renomear arquivos só pelo painel FileSystem da Godot; senão as referências das cenas quebram.

## Arquitetura e estilo (obrigatório)

- Responsabilidade única; sem "classes Deus". `_PhysicsProcess` só orquestra: chama métodos pequenos com nomes claros.
- Cláusulas de guarda (early return) em vez de `if/else` aninhado.
- Strategy para comportamentos intercambiáveis (modos de combate, tipos de feixe, modos de disparo).
- Observer via sinais/eventos para desacoplar (paredes e inimigos reagem ao jogador sem referência direta).
- PascalCase para tipos e membros públicos; `_camelCase` para campos privados. Valores de balanceamento como `[Export]`, sem números mágicos espalhados.

## Regras do projeto (lições já aprendidas)

- Iluminação 2D: toda sala precisa de um `CanvasModulate` preto (#000000) e de um `ColorRect` de fundo 1920x1080 como PRIMEIRO filho da cena. Sem esse fundo o feixe da lanterna não aparece.
- O pivô de `StaticBody2D` é o canto superior esquerdo. Para achar o centro de uma parede, use o centro do `ColorRect` visual (posição + tamanho/2), não `GlobalPosition`.
- Paredes reativas ficam no grupo `Walls` e usam `WallSensitive.cs`. Paredes de titânio (sala de treino) NÃO entram no grupo.
- Câmera: `Camera2D` com zoom entre 1.3x e 1.5x e suavização de posição com Speed 6.0.
- Ao redimensionar `ColorRect` filho de `Node2D`, desligue o Anchor Mode na barra 2D do editor ou altere `Size` pelo Inspector.
- Movimento zero-g: empuxo por vetor (WASD/joystick), sem fricção, amortecimento inercial `_counterThrustDamping` ~0.98. O jogador precisa aplicar contra-empuxo para frear.
- Lanterna: `PointLight2D` + `Area2D` do feixe. Modo manual (`IsManualFire = true`) segue o mouse/analógico direito; modo automático segue a direção da velocidade.
- Irritação tecidual: a parede acumula estresse (`_currentIrritation`) com luz e tiros; ao atingir `MaxIrritation` dispara `TriggerEjection`.

## Estrutura atual (`res://`, tudo na raiz)

- Scripts: `Player.cs`, `WallSensitive.cs`, `LightDamageArea.cs`, `FractalEnemy.cs`, `TerminalModo.cs`, `SalaEntrada.cs`, `SalaDesfiladeiro.cs`, `NinhoSpawner.cs`, `NinhoSpawnerPooled.cs`
- Spawners de inimigos: `NinhoSpawner.cs` cria com `Instantiate` e destrói com `QueueFree`. `NinhoSpawnerPooled.cs` é a versão de teste com object pooling (reaproveita inimigos via `ActivateAt`/`DeactivateToPool` e o callback `OnDied` do `FractalEnemy`). A `sala_ninho.tscn` usa a versão com pool.
- Cenas: `player.tscn`, `fractal_enemy.tscn`, `wall_block.tscn`, `wall_segment.tscn`, `paredes_reativas.tscn`, `sala_entrada.tscn`, `sala_desfiladeiro.tscn`, `sala_ninho.tscn`, `arena.tscn`, `sala_desfiladeiro_TESTE_estrelas.tscn`
- Assets: `lanterna.png`, `lanterna_reta.png`, `lanterna_torta.png`, `light.png`, `icon.svg`, pasta `shaders/`
- Citados no handoff, mas não encontrados no projeto (confirmar antes de assumir): `sala_nucleo.tscn`, `MazeGenerator.cs` (gerador procedural de labirintos) e `DoorTrigger.cs` (transição de cena com `ChangeSceneToFile`).
- Não documentados no handoff (ler o código antes de assumir o que fazem): `arena.tscn`, `SalaEntrada.cs`, `SalaDesfiladeiro.cs`, `sala_desfiladeiro_TESTE_estrelas.tscn`, `NinhoSpawner.cs`, `NinhoSpawnerPooled.cs`.

## Referências

- Specs das mecânicas: `docs/specs/` (modelo em `_TEMPLATE.md`).
- Contexto de design e decisões: `docs/HANDOFF.md` (pode estar desatualizado onde divergir do código).
