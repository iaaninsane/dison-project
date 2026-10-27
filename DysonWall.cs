using Godot;

// Parede de fronteira com o espaço (spec dyson-wall.md): membrana viva de triângulos de Sierpinski que, irritada
// pela luz, vai abrir uma fenda para o vácuo (fase B). Fase A: geometria, colisão, desenho e irritação das peças.
// A parede monta a geometria (DysonWallGeometry, classe pura), guarda a irritação de cada peça e entrega as duas a
// quem desenha (IDysonWallView). Ela não entra sozinha no grupo Walls: quem a monta decide (na cena de teste, o dev;
// na fase, o construtor, que deixa as paredes do nó de entrada inertes até liberar as saídas).
public partial class DysonWall : StaticBody2D, ILightIrritable
{
    [Export] public float Length = 1920f;      // Comprimento (px), no eixo X local, com o pivô no centro
    [Export] public float WallThickness = 40f; // Espessura (px) = altura dos triângulos; 40 como as paredes da fase (o protótipo usa 44)
    [Export(PropertyHint.Range, "0,3")] public int SierpinskiLevel = 2; // Cada peça vira 3^nível sub-triângulos

    [ExportGroup("Irritação")]
    // Luz acumulada para uma peça chegar a 1, na unidade da lanterna (WallIrritationPerSecond, 20/s): ~3 s no centro do feixe
    [Export] public float MaxIrritation = 60f;
    [Export] public float RecoveryRate = 15f; // Quanto a peça se acalma por segundo, fora do feixe (mesma unidade): ~4 s do máximo à calma

    [ExportGroup("Visual provisório (arte na etapa 2)")]
    [Export] public Color MembraneColor = Color.Color8(150, 74, 62); // #964A3E: tecido atrás do fractal (fecha os vãos)
    [Export] public Color PieceColorA = Color.Color8(92, 58, 76);    // #5C3A4C e #6C4658: cores base das peças,
    [Export] public Color PieceColorB = Color.Color8(108, 70, 88);   // alternando pela paridade
    [Export] public Color IrritationColor = Color.Color8(110, 240, 215); // #6EF0D7: brilho verde-água da peça irritada
    [Export] public float TremorStartIrritation = 0.5f; // Os sub-triângulos da peça tremem acima desta irritação (0 a 1)
    [Export] public float TremorStrength = 3f;          // Tremor de cada sub-triângulo: até ±(irritação − início) × isto ÷ 2 px em cada eixo (spec 2.2)

    private DysonWallGeometry _geometry;
    private IDysonWallView _view; // Quem desenha (hoje, a W1)
    private float[] _irritation;  // Irritação de cada peça, de 0 a 1: o estado da parede
    private ulong[] _litFrame;    // Último passo de física em que cada peça recebeu luz

    public override void _Ready()
    {
        _geometry = new DysonWallGeometry(Length, WallThickness, SierpinskiLevel, (int)GD.Randi());
        _irritation = new float[_geometry.TileCount];
        _litFrame = new ulong[_geometry.TileCount];
        CreateCollision();
        CreateView();

        // A Godot liga o _PhysicsProcess antes do _Ready; desligar aqui faz a parede calma não custar nada
        SetPhysicsProcess(false); // Calma: dorme até a luz chegar (AddLightIrritation acorda), como o WallSensitive
    }

    // Só roda enquanto há peça irritada: acalma as que estão fora do feixe, mostra e, tudo calmo, volta a dormir
    public override void _PhysicsProcess(double delta)
    {
        bool anyIrritated = Recover((float)delta);
        _view.Refresh(_irritation);
        if (!anyIrritated) SetPhysicsProcess(false); // Calma de novo (o último desenho já saiu sem brilho)
    }

    // Chamado pela lanterna a cada passo em que o feixe toca a parede: cada peça com o centro no feixe sobe
    // conforme o peso da luz nela (decisão A2: 1 no eixo do feixe, 0 na borda)
    public void AddLightIrritation(float amount, LightDamageArea light)
    {
        float gain = amount / MaxIrritation;
        Transform2D toGlobal = GlobalTransform; // Lida do motor uma vez para todas as peças
        ulong frame = Engine.GetPhysicsFrames();
        bool gained = false;

        for (int t = 0; t < _irritation.Length; t++)
        {
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

    // Colisão fixa: um retângulo do tamanho da parede. A deformação das peças é só visual (spec, seção 5).
    private void CreateCollision()
    {
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Length, WallThickness) } });
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
