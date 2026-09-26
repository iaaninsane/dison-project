# PROJECT BIO-BREACH (NEON NEXUS)
## Documento de Handoff, Arquitetura, Design e Decisões Técnicas (v1.0)

Este documento foi gerado para servirem de guia de transição e referência técnica completa para qualquer desenvolvedor, analista ou designer que venha a dar continuidade ao projeto **Project Bio-Breach** (nome de trabalho alternativo: *Neon Nexus*).

---

## 1. Visão Geral do Projeto e Sinopse Narrativa

O **Project Bio-Breach** é um jogo indie de ação, sobrevivência e exploração espacial (*Action Roguelike / Bullet Heaven*) em perspectiva 2D Top-Down, construído em **C# na Godot Engine 4.x (Mono)**.

* **Sinopse Narrativa:** O jogador assume o papel de um cientista isolado explorando o interior de uma estrutura colossal aparentemente inanimada à deriva no espaço sideral (uma *Esfera de Dyson*). Ao adentrar seus corredores biológicos, a estrutura revela ser um organismo bio-mecânico vivo. A nave interpreta a presença do cientista como uma infecção ou corpo estranho, ativando seus anticorpos fractais e defesas teciduais para expurgar o invasor.
* **High Concept:** Navegação claustrofóbica em gravidade zero dentro de uma estrutura viva, utilizando o feixe de luz de uma lanterna para caçar anticorpos fractais na escuridão total, sob o risco constante de irritar o tecido biológico das paredes e ser ejetado para o vácuo espacial.
* **Pilar Visual & Atmosférico:** Blecaute total controlado via iluminação 2D (`CanvasModulate`), com paredes e organismos exibindo iluminação bioluminescente em tons neon (ciano, magenta e roxo) por meio de Shaders e efeitos de Bloom/Glow (`WorldEnvironment`).
* **Eficiência para Dev Solo:** Substituição do trabalho manual massivo de animação por simulações físicas da engine (física *Soft-Body*), geração procedural de labirintos por blocos/chunks e renderização programática de inimigos fractais via código (`_Draw`).

---

## 2. Definição de Escopo e Decisões de Arquitetura

Uma decisão fundamental de engenharia de software tomada durante o ciclo inicial de desenvolvimento foi a manutenção de uma **fronteira de escopo rígida e enxuta** para a entrega da demonstração jogável (MVP/Demo):

### Mecânicas Ativas Mantidas no Core Loop:
1. **Movimentação inercial em gravidade zero (Zero-G)** sem fricção de solo.
2. **Sistema de iluminação cônica por lanterna** e blecaute ambiente.
3. **Combate por luz** e dano contínuo contra anticorpos.
4. **Irritabilidade tecidual das paredes (`SensitiveWall.cs`)** e acúmulo de estresse bioluminescente.
5. **Mecanismo de contração e ejeção física para o vácuo** ao atingir limite de estresse.
6. **Troca modular de modos de combate** (Modo Manual vs. Modo Automático).

### Mecânicas Oficialmente Adiadas (Pós-Demo):
1. **Vácuo como Atalho Estratégico:** A mecânica de arremessar o personagem propositalmente para o vácuo como atalho entre salas foi postergada.
2. **Foguetes de Mega Propulsão:** O uso de propulsores extras para navegação em alta velocidade no vácuo foi suspenso.
* **Justificativa de Engenharia:** Evitou-se o acúmulo de complexidade física e matemática antes do polimento do loop básico dentro das salas, priorizando a estabilidade do *game feel*.

### Core Loop Validado:
`EXPLORAR` (Navegação inercial no escuro) ➔ `ALERTAR` (Luz e disparos estressam a parede) ➔ `COMBATER` (Dano por iluminação e fragmentação) ➔ `COLETAR` (Extração de biomassa neon) ➔ `EVOLUIR / MORRER` (Melhorias no traje na câmara inicial).

---

## 3. Detalhamento das Mecânicas e Física Implementadas

### 3.1 Movimentação em Gravidade Zero (Zero-G)
* **Física Newtoniana:** O cientista se desloca aplicando impulsos vetoriais (WASD / Joystick). Não há atrito ou atrito padrão do solo; o personagem desliza indefinidamente se nenhum impulso contrário for aplicado.
* **Amortecimento Inercial (*Damping*):** Implementou-se um controle de desaceleração suave via coeficiente de damping (`_counterThrustDamping ~ 0.98f`), exigindo contra-empuxo do jogador para frenagens de precisão.
* **Colisão Soft-Body:** Colisões contra as paredes biológicas utilizam respostas elásticas gelatinosas, onde o impacto de alta velocidade deforma a parede e repele o cientista suavemente.

### 3.2 Visão e Iluminação por Lanterna
* **Blecaute Global:** Nó `CanvasModulate` com cor definida em preto absoluto (`#000000`) para apagar a iluminação ambiente por padrão.
* **Lanterna Cônica:** Estruturada no nó do jogador através de um `PointLight2D` combinado com uma `Area2D` em formato de setor circular para detecção física de área do feixe de luz.
* **Superfície de Reflexão (Background):** Cada sala necessita de um `ColorRect` (1920x1080) posicionado no topo da árvore de nós (fundo), atuando como superfície `CanvasItem` para refletir a iluminação da lanterna no chão.

### 3.3 Sistema de Irritabilidade Tecidual e Ejeção (Bio-Reação)
* **Paredes Sensíveis (`SensitiveWall.cs`):** Segmentos de parede pertencentes ao grupo `"Walls"` acumulam a variável `_currentIrritation` quando detectados pelo feixe da lanterna ou atingidos por disparos.
* **Mecanismo de Ejeção:** Ao atingir o limite `MaxIrritation`, a parede dispara o método `TriggerEjection`. A parede sofre contração muscular visual, gera espasmos e aplica uma força atrativa/repulsiva que puxa o cientista para o seu centro geométrico e o ejeta.

### 3.4 Modos de Combate Modulares (Padrão Strategy)
* **Modo Manual (`IsManualFire = true`):** A lanterna segue o ponteiro do mouse (ou o analógico direito do Gamepad), garantindo mira cirúrgica para iluminação focal sem estressar paredes desnecessariamente.
* **Modo Automático (`IsManualFire = false`):** A lanterna orienta-se automaticamente para o vetor de deslocamento (`Velocity.Angle()`), permitindo que o jogador foque 100% na navegação e esquiva de obstáculos.
* **Terminal de Troca (`TerminalModo.cs`):** Interagível via tecla 'E'/Espaço na câmara inicial (`sala_entrada.tscn`), alterando a propriedade no `Player.cs` em tempo real.

---

## 4. Estrutura e Templates de Salas Modulares (Chunks 1920x1080)

Para integração com o gerador procedural de labirintos (`MazeGenerator.cs`), o mapa é composto por cenas modulares em resolução **1920x1080 pixels**:

1. **`sala_entrada.tscn` (Câmara de Descompressão / Treino):**
   * Ponto de *spawn* seguro do cientista.
   * Paredes de titânio metálico (não pertencentes ao grupo `"Walls"`, sem irritabilidade).
   * Contém o `TerminalModo.cs` para troca de modo de iluminação e o nó `PortaSaida` com `DoorTrigger.cs`.
2. **`sala_desfiladeiro.tscn` (O Gargalo Paranoico):**
   * Corredores estreitos em zigue-zague (formato "S" ou "Z").
   * Revestido 100% por `ParedesReativas` (`SensitiveWall.cs` + Grupo `"Walls"`).
   * Possui fendas com `JanelasVacuo` (`ColorRect` ciano translúcido + `PointLight2D` suave) revelando o vácuo do espaço.
3. **`sala_ninho.tscn` (Arena de Combate - Em planejamento/desenvolvimento):**
   * Arena circular ampla para encontros com hordas de anticorpos fractais.
4. **`sala_nucleo.tscn` (Clímax / Reator - Em planejamento/desenvolvimento):**
   * Sala de objetivo final da demonstração.

---

## 5. Registro de Problemas Técnicos Resolvidos (Fixes de Código)

### 5.1 Correção da Direção de Atração nas Paredes Reativas (`SensitiveWall.cs`)
* **Sintoma:** Ao atingir estresse máximo, o vetor de sucção puxava o jogador para o canto superior esquerdo (quina `0,0`) do bloco.
* **Causa Raiz:** O cálculo utilizava `GlobalPosition` do `StaticBody2D`, cujo pivô padrão na Godot fica no canto superior esquerdo.
* **Solução:** Atualização no método `TriggerEjection` para calcular o centro geométrico real do `_visualRect`:
```csharp
Vector2 wallCenter = GlobalPosition;
if (_visualRect != null)
{
    wallCenter = _visualRect.GlobalPosition + (_visualRect.Size / 2f);
}
Vector2 pullDirection = (wallCenter - playerPosition).Normalized();
player.ApplyEjection(pullDirection * EjectionForce);
```

### 5.2 Correção do Erro de Compilação `IsManualFire`
* **Sintoma:** Erro `Player does not contain a definition for IsManualFire` no `TerminalModo.cs`.
* **Solução:** Adicionada a propriedade `[Export] public bool IsManualFire { get; set; } = true;` no `Player.cs` e refatorado o bloco de rotação da lanterna.

### 5.3 Correção da Lanterna Invisível no Chão
* **Sintoma:** O feixe de luz do `PointLight2D` não aparecia no chão da sala.
* **Causa Raiz:** A Viewport padrão da Godot não é um `CanvasItem`.
* **Solução:** Adição de um nó `ColorRect` (1920x1080) na primeira posição da árvore de nós da cena (`Background`) e um nó `CanvasModulate` (`#000000`).

### 5.4 Ajuste de Suavização e Limites da Câmera (`Camera2D`)
* **Sintoma:** O efeito de suavização (`Position Smoothing`) parecia não funcionar.
* **Causa Raiz:** Em mapa 1920x1080 com `Camera Limits` cravados exatamente em `(0,0,1920,1080)`, a tela cobria 100% da câmara, travando o deslocamento.
* **Solução:** Aumento do `Zoom` da `Camera2D` para `1.3x ~ 1.5x` e ajuste da velocidade de suavização para `Speed = 6.0`.

### 5.5 Aviso de Ancoragem de Interface ao Redimensionar `JanelasVacuo`
* **Sintoma:** Aviso `Não é possível modificar deslocamentos de ancoragem quando o pai tem um tamanho de 0`.
* **Solução:** Desativação do Modo de Ancoragem (Anchor Mode) na barra de ferramentas 2D do editor ao manipular `ColorRect` dentro de um `Node2D`, ou ajuste via propriedade `Size` do Inspetor.

### 5.6 Script de Transição Modular entre Cenas (`DoorTrigger.cs`)
* **Solução:** Implementação de um script desacoplado anexado a nós `Area2D`:
```csharp
using Godot;

public partial class DoorTrigger : Area2D
{
    [Export(PropertyHint.File, "*.tscn")] 
    public string TargetScenePath { get; set; } = "";

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player && !string.IsNullOrEmpty(TargetScenePath))
        {
            GetTree().ChangeSceneToFile(TargetScenePath);
        }
    }
}
```

---

## 6. Arquitetura de Código C# e Diretrizes Clean Code

Para manter a sustentabilidade do código e evitar a criação de "Classes Deus" (*God Classes*), os novos scripts em C# devem respeitar as seguintes diretrizes:

1. **Princípio da Responsabilidade Única (SRP):** O método `_PhysicsProcess` atua apenas como orquestrador de alto nível, delegando tarefas para métodos privados ou componentes especializados.
2. **Cláusulas de Guarda (*Early Returns*):** Eliminação de blocos `if/else` profundamente aninhados para manter a execução linear e legível.
3. **Padrão Strategy:** Alternância de modos de disparo e comportamentos isolados em propriedades ou componentes intercambiáveis.
4. **Padrão Observer (Signals/Events):** Comunicação entre salas, paredes e jogador via eventos e sinais do Godot para desacoplamento total.

---

## 7. Referências Visuais, de Gameplay e Estratégia de Mercado

* **Pathogenic (Aberrant Labs):** Referência primária para estética celular, física *soft-body* deformável, membranas translúcidas com Shaders e movimentação baseada em fluidez.
* **Sir, We Have an Orc Problem (Mumpitz Games):** Referência para sensação de volume massivo de hordas, partículas neon e ciclo de metaprogressão roguelite (desenvolvido na Godot Engine por 2 pessoas em 4 meses, faturando > US$ 360k no dia 1).
* **Balatro & Vampire Survivors:** Provas de mercado de que loops mecânicos hiper-focados e gratificação imediata superam custos massivos de produção gráfica.
* **Chris Zukowski (How To Market A Game):** Estratégia comercial com precificação em US$ 14.99 (+20% de margem para promoções), meta de 30.000 wishlists, Supporter Pack DLC no Dia 1 (conversão de 5% a 11%) e lançamento de demo pública 3 a 4 meses antes do Steam Next Fest.
* **Jonathan Blow (Order of the Sinking Star / The Witness):** Foco em sofisticação sistêmica e qualidade mecânica sobre narrativas lineares dispendiosas.

---

## 8. Próximos Passos Recomendados para Continuidade

1. **Montagem do Template 3 (`sala_ninho.tscn`):** Construir a arena circular de combate com ninhos laterais e estruturas rígidas para cobertura.
2. **Sistema de Partículas Bioluminescentes (`GPUParticles2D`):** Implementar a explosão de partículas neon ao derrotar os anticorpos fractais, gerando iluminação temporária no ambiente.
3. **Gerador Procedural de Labirintos (`MazeGenerator.cs`):** Conectar as cenas modulares (`sala_entrada`, `sala_desfiladeiro`, `sala_ninho`, `sala_nucleo`) em uma grade 2x2 ou 3x3 e ajustar dinamente os `CameraLimits` ao trocar de sala.
4. **Inimigos Fractais (`FractalEnemy.cs`):** Implementar a renderização programática recursiva de ramificações binárias no método `_Draw` com pulsação senoidal e divisão celular ao morrer.
