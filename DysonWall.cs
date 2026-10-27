using Godot;
using System;

// Parede de fronteira com o espaço (spec dyson-wall.md): membrana viva de triângulos de Sierpinski que, irritada
// pela luz, abre uma fenda para o vácuo. Fase A: geometria, colisão, desenho e irritação das peças. Fase B: a abertura.
// A parede monta a geometria (DysonWallGeometry, classe pura), guarda a irritação de cada peça e a abertura em
// andamento, e entrega tudo a quem desenha (IDysonWallView). Ela não entra sozinha no grupo Walls: quem a monta decide
// (na cena de teste, o dev; na fase, o construtor, que deixa as paredes do nó de entrada inertes até liberar as saídas).
// O lado de fora (o espaço) é o de cima, sem rotação: -Y local. Quem monta a parede a gira para isso.
// É um AnimatableBody2D (corpo movido por código), mesmo parada: a colisão muda na abertura e, num StaticBody2D, a
// lanterna parada não percebe a forma religada quando o vão fecha (verificado no motor, 4.7.2).
public partial class DysonWall : AnimatableBody2D, ILightIrritable
{
    // O jogador cruzou a face de fora pela fenda (decisão B1: para som e câmera no futuro; a consequência vem do LostInSpace)
    [Signal] public delegate void PlayerEjectedEventHandler();

    [Export] public float Length = 1920f;      // Comprimento (px), no eixo X local, com o pivô no centro
    [Export] public float WallThickness = 40f; // Espessura (px) = altura dos triângulos; 40 como as paredes da fase (o protótipo usa 44)
    [Export(PropertyHint.Range, "0,3")] public int SierpinskiLevel = 2; // Cada peça vira 3^nível sub-triângulos

    [ExportGroup("Irritação")]
    // Luz acumulada para uma peça chegar a 1, na unidade da lanterna (WallIrritationPerSecond, 20/s): ~3 s no centro do feixe
    [Export] public float MaxIrritation = 60f;
    [Export] public float RecoveryRate = 15f; // Quanto a peça se acalma por segundo, fora do feixe (mesma unidade): ~4 s do máximo à calma

    [ExportGroup("Abertura (Esmaecer)")]
    [Export] public float OpenRadius = 64f;         // Raio do núcleo da abertura (px); o vão da colisão deixa isto livre de cada lado do centro
    [Export] public float FadeRadiusFactor = 1.15f; // As figuras a menos de OpenRadius × isto do centro somem
    [Export] public float WarningDuration = 0.35f;  // Fase 0, aviso: as peças da área tremem forte (s)
    [Export] public float OpeningDuration = 1.1f;   // Fase 1: a fenda abre (s)
    [Export] public float OpenDuration = 1.2f;      // Fase 2: aberta (s)
    [Export] public float ClosingDuration = 1.3f;   // Fase 3: fecha (s)
    [Export] public float WarningTremor = 2f;       // Tremor extra no aviso, no centro da abertura (px)

    [ExportGroup("Sucção")]
    [Export] public float NoReturnRadius = 120f;                // Distância em que a sucção empata com o empuxo máximo do traje (px)
    [Export] public float SuctionAccelerationAtNoReturn = 450f; // A sucção nessa distância (px/s²): o ThrustForce do jogador
    [Export] public float SuctionFalloffExponent = 2f;          // Queda com a distância (2: o ar que escapa em leque, arrasto ~ 1/r²)
    [Export] public float MaxSuctionAcceleration = 3000f;       // Teto perto da fenda, para a conta não ir ao infinito (px/s²)
    [Export] public float SuctionMaxRadius = 360f;              // Além disto, a fenda não puxa (px)
    [Export] public float GustMultiplier = 3f;                  // Rajada no rompimento: começa com esta força a mais
    [Export] public float GustDecay = 0.6f;                     // Quanto a rajada leva para cair até 1× (constante de tempo, s)

    [ExportGroup("Visual provisório (arte na etapa 2)")]
    [Export] public Color MembraneColor = Color.Color8(150, 74, 62); // #964A3E: tecido atrás do fractal (fecha os vãos)
    [Export] public Color PieceColorA = Color.Color8(92, 58, 76);    // #5C3A4C e #6C4658: cores base das peças,
    [Export] public Color PieceColorB = Color.Color8(108, 70, 88);   // alternando pela paridade
    [Export] public Color IrritationColor = Color.Color8(110, 240, 215); // #6EF0D7: brilho verde-água da peça irritada
    [Export] public float TremorStartIrritation = 0.5f; // Os sub-triângulos da peça tremem acima desta irritação (0 a 1)
    [Export] public float TremorStrength = 3f;          // Tremor de cada sub-triângulo: até ±(irritação − início) × isto ÷ 2 px em cada eixo (spec 2.2)

    private const uint GapBodyLayers = 1 | 2;       // Quem é empurrado para fora do vão ao fechar: jogador (camada 1), inimigos (2)
    private const float PushClearance = 2f;         // Folga entre a parede e quem foi empurrado (px)
    private const float DefaultBodyHalfExtent = 16f; // Metade do tamanho de um corpo sem a forma "CollisionShape2D" (px)

    private DysonWallGeometry _geometry;
    private IDysonWallView _view;      // Quem desenha (hoje, a W1)
    private CollisionShape2D _wholeCollision;                                      // A parede inteira (sem abertura)
    private readonly CollisionShape2D[] _sideCollisions = new CollisionShape2D[2]; // Os dois lados do vão, na abertura
    private bool _gapOpen;             // A colisão está dividida (vão aberto)
    private Node2D _player;            // Achado pelo grupo Player na primeira abertura (para o PlayerEjected)
    private bool _playerWasInside;     // O jogador estava do lado de dentro da face de fora no passo anterior
    private float[] _irritation;       // Irritação de cada peça, de 0 a 1: o estado da parede
    private ulong[] _litFrame;         // Último passo de física em que cada peça recebeu luz
    private float[] _tileTremorBoost;  // Tremor extra de cada peça (aviso da abertura)
    private float[] _subScales;        // Escala de cada sub-triângulo (1 = inteiro; a abertura encolhe)
    private float[] _membraneScales;   // Escala de cada membrana
    private DysonWallState _state;     // Os arrays acima, entregues a quem desenha
    private DysonWallOpening _opening; // Abertura em andamento (null = nenhuma; uma por parede de cada vez)
    private DysonWallFade _fade;       // Como as peças somem nessa abertura (Esmaecer)
    private SuctionBreach _breach;     // A fenda dessa abertura no campo de sucção

    public override void _Ready()
    {
        _geometry = new DysonWallGeometry(Length, WallThickness, SierpinskiLevel, (int)GD.Randi());
        _irritation = new float[_geometry.TileCount];
        _litFrame = new ulong[_geometry.TileCount];
        _tileTremorBoost = new float[_geometry.TileCount];
        _subScales = new float[_geometry.SubCount];
        _membraneScales = new float[_geometry.TileCount];
        Array.Fill(_subScales, 1f);
        Array.Fill(_membraneScales, 1f);
        _state = new DysonWallState(_irritation, _tileTremorBoost, _subScales, _membraneScales);
        CreateCollision();
        CreateView();

        // A Godot liga o _PhysicsProcess antes do _Ready; desligar aqui faz a parede calma não custar nada
        SetPhysicsProcess(false); // Calma: dorme até a luz chegar (AddLightIrritation acorda), como o WallSensitive
    }

    // A lista do campo de sucção é estática: uma fenda aberta não pode ficar nela quando a parede sai (troca de cena)
    public override void _ExitTree()
    {
        if (_breach != null) SuctionField.Unregister(_breach);
    }

    // Só roda enquanto há peça irritada ou abertura: acalma as peças fora do feixe, cuida da abertura, mostra e,
    // tudo calmo, volta a dormir
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        bool anyIrritated = Recover(dt);
        UpdateOpening(dt);
        _view.Refresh(_state);
        if (!anyIrritated && _opening == null) SetPhysicsProcess(false); // Calma de novo (o último desenho já saiu sem brilho)
    }

    // Chamado pela lanterna a cada passo em que o feixe toca a parede: cada peça com o centro no feixe sobe
    // conforme o peso da luz nela (decisão A2: 1 no eixo do feixe, 0 na borda). Na área de uma abertura em
    // andamento, a irritação não acumula (spec, seção 5).
    public void AddLightIrritation(float amount, LightDamageArea light)
    {
        float gain = amount / MaxIrritation;
        Transform2D toGlobal = GlobalTransform; // Lida do motor uma vez para todas as peças
        ulong frame = Engine.GetPhysicsFrames();
        bool gained = false;

        for (int t = 0; t < _irritation.Length; t++)
        {
            if (IsInOpening(t)) continue;
            float weight = light.BeamWeightAt(toGlobal * _geometry.TileCenters[t]);
            if (weight <= 0f) continue;

            _irritation[t] = Mathf.Min(1f, _irritation[t] + weight * gain);
            _litFrame[t] = frame;
            gained = true;
        }

        if (gained) SetPhysicsProcess(true); // Acorda para mostrar e, quando a luz sair, se acalmar aos poucos
    }

    // Decai as peças fora do feixe (decisão A3: dentro dele, não decai). Devolve se sobrou alguma irritada.
    // "No feixe" = recebeu luz neste passo ou no anterior: assim a ordem entre a parede e a lanterna na árvore não importa.
    private bool Recover(float dt)
    {
        float recovery = RecoveryRate / MaxIrritation * dt;
        ulong frame = Engine.GetPhysicsFrames();
        bool anyIrritated = false;

        for (int t = 0; t < _irritation.Length; t++)
        {
            if (frame - _litFrame[t] > 1) _irritation[t] = Mathf.Max(0f, _irritation[t] - recovery);
            if (_irritation[t] > 0f) anyIrritated = true;
        }
        return anyIrritated;
    }

    // Abertura (spec 2.3 e 2.5): começa onde uma peça chegou a 1, avança o relógio, encolhe as figuras e, quando a
    // última figura volta, termina
    private void UpdateOpening(float dt)
    {
        if (_opening == null) TryStartOpening();
        if (_opening == null) return;

        _opening.Advance(dt);
        if (_opening.Time >= _opening.TotalDuration + _fade.MaxDelay)
        {
            FinishOpening();
            return;
        }

        _fade.Apply(_opening, _geometry, _subScales, _membraneScales);
        _breach.Update(_opening.Intensity, _opening.TimeSinceRupture);
        for (int t = 0; t < _tileTremorBoost.Length; t++)
        {
            _tileTremorBoost[t] = _opening.WarningTremorAt(_geometry.TileCenters[t].X);
        }
        UpdateGap();
        CheckPlayerEjected();
    }

    // Uma peça chegou a 1? A abertura nasce nela (a primeira, se houver várias: a irritação para em 1).
    // Os valores vêm do Inspector a cada abertura: dá para ajustar com o jogo rodando.
    private void TryStartOpening()
    {
        int peak = Array.FindIndex(_irritation, irritation => irritation >= 1f);
        if (peak < 0) return;

        _opening = new DysonWallOpening(_geometry.TileCenters[peak].X, WarningDuration, OpeningDuration, OpenDuration,
            ClosingDuration, OpenRadius, WarningTremor);
        _fade = new DysonWallFade(OpenRadius, OpenRadius * FadeRadiusFactor);
        _player ??= GetTree().GetFirstNodeInGroup("Player") as Node2D;
        _playerWasInside = _player != null && IsInside(_player);

        // A fenda entra no campo já no aviso, com força zero (k = 0 até o rompimento). "Fora" = -Y local, no global.
        _breach = new SuctionBreach(ToGlobal(new Vector2(_opening.CenterX, 0f)), GlobalTransform.BasisXform(Vector2.Up).Normalized(),
            WallThickness / 2f, OpenRadius, new SuctionSettings(NoReturnRadius, SuctionAccelerationAtNoReturn, SuctionFalloffExponent, MaxSuctionAcceleration,
                SuctionMaxRadius, GustMultiplier, GustDecay));
        SuctionField.Register(_breach);
    }

    // Fim da abertura: as peças da área voltam à calma (spec 2.3) e as figuras, ao tamanho normal; a fenda sai do campo
    private void FinishOpening()
    {
        if (_gapOpen) CloseGap();
        SuctionField.Unregister(_breach);
        _breach = null;
        for (int t = 0; t < _irritation.Length; t++)
        {
            if (IsInOpening(t)) _irritation[t] = 0f;
        }
        Array.Fill(_subScales, 1f);
        Array.Fill(_membraneScales, 1f);
        Array.Fill(_tileTremorBoost, 0f);
        _opening = null;
        _fade = null;
    }

    private bool IsInOpening(int tile) => _opening != null && _fade.Affects(_opening, _geometry.TileCenters[tile].X);

    // Vão na colisão (spec, seção 5): aberto da fase 1 até o meio da fase 3
    private void UpdateGap()
    {
        bool shouldBeOpen = _opening.IsGapOpen;
        if (shouldBeOpen == _gapOpen) return;

        if (shouldBeOpen) OpenGap();
        else CloseGap();
    }

    // A parede vira dois retângulos, um de cada lado do centro da abertura, com OpenRadius livre para cada lado
    // (um pouco menos que o buraco desenhado: ninguém atravessa uma peça que ainda aparece)
    private void OpenGap()
    {
        float halfLength = Length / 2f;
        SetSide(_sideCollisions[0], -halfLength, _opening.CenterX - OpenRadius);
        SetSide(_sideCollisions[1], _opening.CenterX + OpenRadius, halfLength);
        _wholeCollision.Disabled = true;
        _gapOpen = true;
    }

    // Um lado do vão, de `from` a `to` no eixo da parede. Sem largura (abertura na ponta), fica desligado.
    private void SetSide(CollisionShape2D side, float from, float to)
    {
        side.Disabled = to <= from;
        if (side.Disabled) return;

        ((RectangleShape2D)side.Shape).Size = new Vector2(to - from, WallThickness);
        side.Position = new Vector2((from + to) / 2f, 0f);
    }

    // Fecha o vão: quem estiver nele sai antes de a parede voltar a ser inteira, para não ficar preso (spec, seção 5)
    private void CloseGap()
    {
        PushBodiesOutOfGap();
        _wholeCollision.Disabled = false;
        foreach (CollisionShape2D side in _sideCollisions) side.Disabled = true;
        _gapOpen = false;
    }

    // Consulta ao espaço físico (segura no passo de física, onde a parede roda): corpos que se movem dentro do vão
    private void PushBodiesOutOfGap()
    {
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = new RectangleShape2D { Size = new Vector2(OpenRadius * 2f, WallThickness) },
            Transform = GlobalTransform * new Transform2D(0f, new Vector2(_opening.CenterX, 0f)),
            CollisionMask = GapBodyLayers,
        };
        foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query))
        {
            if (hit["collider"].As<GodotObject>() is CharacterBody2D body) PushOutOfWall(body);
        }
    }

    // Leva o corpo para logo depois da face de dentro (para a câmara). Quem já passou da face de fora vai para fora.
    private void PushOutOfWall(CharacterBody2D body)
    {
        Vector2 local = ToLocal(body.GlobalPosition);
        float clearance = WallThickness / 2f + HalfExtentOf(body) + PushClearance;
        local.Y = local.Y >= -WallThickness / 2f ? clearance : -clearance;
        body.GlobalPosition = ToGlobal(local);
    }

    // Metade do maior lado da forma de colisão do corpo (o jogador e o inimigo têm uma chamada "CollisionShape2D")
    private static float HalfExtentOf(CharacterBody2D body)
    {
        var shape = body.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (shape?.Shape == null) return DefaultBodyHalfExtent;

        Vector2 size = shape.Shape.GetRect().Size * shape.GlobalScale.Abs();
        return Mathf.Max(size.X, size.Y) / 2f;
    }

    // O jogador passou de dentro para fora da face de fora, pela fenda? Avisa (uma vez a cada travessia)
    private void CheckPlayerEjected()
    {
        if (_player == null || !IsInstanceValid(_player)) return;

        bool inside = IsInside(_player);
        bool crossed = _playerWasInside && !inside && Mathf.Abs(ToLocal(_player.GlobalPosition).X - _opening.CenterX) <= OpenRadius;
        _playerWasInside = inside;
        if (crossed) EmitSignal(SignalName.PlayerEjected);
    }

    // Do lado de dentro da face de fora (na câmara ou dentro da faixa da parede)
    private bool IsInside(Node2D body) => ToLocal(body.GlobalPosition).Y >= -WallThickness / 2f;

    // Colisão fixa: um retângulo do tamanho da parede. A deformação das peças é só visual (spec, seção 5). Durante a
    // abertura, ele dá lugar a dois retângulos, um de cada lado do vão (OpenGap).
    private void CreateCollision()
    {
        _wholeCollision = AddCollisionRectangle(new Vector2(Length, WallThickness), false);
        for (int i = 0; i < _sideCollisions.Length; i++) _sideCollisions[i] = AddCollisionRectangle(Vector2.One, true);
    }

    private CollisionShape2D AddCollisionRectangle(Vector2 size, bool disabled)
    {
        var shape = new CollisionShape2D { Shape = new RectangleShape2D { Size = size }, Disabled = disabled };
        AddChild(shape);
        return shape;
    }

    // O único ponto que conhece a W1: para usar a W2, troca-se o nó criado aqui
    private void CreateView()
    {
        var view = new DysonWallView { Name = "View" };
        AddChild(view);
        _view = view;
        _view.Build(_geometry, new DysonWallStyle(
            MembraneColor, PieceColorA, PieceColorB, IrritationColor, TremorStartIrritation, TremorStrength));
    }
}
