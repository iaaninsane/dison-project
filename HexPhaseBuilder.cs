using Godot;

// Monta a fase hexagonal na cena a partir da geometria do HexPhaseLayout (spec hex-phase.md, H3).
// Fica na raiz da hex_phase.tscn. Por enquanto: fundo e piso; paredes, nascimento e câmera vêm nos próximos passos.
public partial class HexPhaseBuilder : Node2D
{
    [Signal] public delegate void PlayerPlacedEventHandler(); // O jogador foi posto na eclusa (ao nascer ou voltando do espaço)

    [ExportGroup("Formato e medidas")]
    [Export] public HexPhaseShape Shape = HexPhaseShape.Sector; // Setor de teste ou hexágono completo
    // Padrão: a "receita 2×" (corredores longos, nós e painéis amplos; spec hex-phase.md, seção 4)
    [Export] public float NodeSpacing = 6200f;        // Distância entre os centros de nós vizinhos
    [Export] public float NodeRadius = 1100f;         // Raio interno das câmaras dos nós (mínimo ~700 com as larguras atuais)
    [Export] public int NodeWallSegments = 32;        // Lados do polígono que aproxima o círculo do nó
    [Export] public float ConnectorWidth = 320f;      // Largura interna dos conectores de nós
    [Export] public float PlateConnectorWidth = 240f; // Largura interna dos conectores de placa
    [Export] public float PanelGap = 1000f;           // Vão entre o painel e as paredes dos conectores
    [Export] public float WallThickness = 40f;        // Espessura das paredes
    [Export] public float MaxWallSegmentLength = 200f; // Paredes mais longas são divididas (cada trecho tem a sua irritação)

    [ExportGroup("Sensibilidade das paredes")]
    // Irritação máxima por tipo de estrutura (o padrão do WallSensitive é 15). Com a lanterna atual (20/s de
    // irritação contra 15/s de recuperação), o tempo de luz contínua até ejetar é de ~MaxIrritation / 5 segundos.
    [Export] public float ArenaWallMaxIrritation = 25f;    // Nós e painéis: ~5 s (longe, raramente iluminadas sem querer)
    [Export] public float CorridorWallMaxIrritation = 10f; // Conectores de nós: ~2 s
    [Export] public float PlateWallMaxIrritation = 7.5f;   // Conectores de placa, os mais estreitos: ~1,5 s

    [ExportGroup("Jogador e eclusa")]
    [Export] public Player Player;                    // O jogador da cena (ligado no Inspector)
    [Export] public int EntryPeripheral = 2;          // Número do nó periférico da eclusa (como nos nomes: 1 a 6)

    [ExportGroup("Visual provisório (arte na etapa 2)")]
    [Export] public float BackgroundMargin = 2000f;   // Quanto o fundo passa da fase em cada lado
    [Export] public Color SpaceColor = new Color(0.12f, 0.08f, 0.16f);
    [Export] public Color NodeFloorColor = new Color(0.42f, 0.27f, 0.35f);
    [Export] public Color ConnectorFloorColor = new Color(0.36f, 0.23f, 0.30f);
    [Export] public Color PanelFloorColor = new Color(0.30f, 0.19f, 0.26f);
    [Export] public Color WallColor = new Color(0.59f, 0.29f, 0.24f);

    public HexPhaseLayout Layout { get; private set; } // Geometria da fase, para quem precisar perguntar "onde estou?"

    public override void _Ready()
    {
        Layout = new HexPhaseLayout(CreateSettings());
        CreateBackground();
        CreateFloors();
        CreateWalls();
        PlacePlayerAtEntry(); // Nasce na eclusa
    }

    // Põe o jogador na eclusa, parado, e avisa (a câmera se ajusta sem a fase precisar conhecê-la)
    public void PlacePlayerAtEntry()
    {
        if (Player == null) return;
        Player.GlobalPosition = ToGlobal(Layout.PeripheralNodeCenter(EntryPeripheral));
        Player.Velocity = Vector2.Zero;
        EmitSignal(SignalName.PlayerPlaced);
    }

    // Conectado pelo editor ao sinal LostInSpace do StructureTracker (regra provisória, spec hex-phase.md, seção 10)
    public void OnPlayerLostInSpace() => PlacePlayerAtEntry();

    private HexPhaseSettings CreateSettings() => new(
        Shape, NodeSpacing, NodeRadius, NodeWallSegments,
        ConnectorWidth, PlateConnectorWidth, PanelGap, WallThickness, MaxWallSegmentLength);

    // Fundo (o espaço) como PRIMEIRO filho da cena: regra do projeto, sem ele a luz da lanterna não aparece
    private void CreateBackground()
    {
        Rect2 area = Layout.Bounds.Grow(BackgroundMargin);
        var background = new ColorRect
        {
            Name = "Background",
            Position = area.Position,
            Size = area.Size,
            Color = SpaceColor,
            MouseFilter = Control.MouseFilterEnum.Ignore, // Não captura cliques do mouse
        };
        AddChild(background);
        MoveChild(background, 0);
    }

    // Piso: um polígono por estrutura, logo acima do fundo e abaixo do jogador
    private void CreateFloors()
    {
        var floors = new Node2D { Name = "Floors" };
        AddChild(floors);
        MoveChild(floors, 1);

        foreach (HexStructure structure in Layout.Structures)
        {
            floors.AddChild(new Polygon2D
            {
                Name = structure.Name,
                Polygon = structure.Outline,
                Color = FloorColor(structure.Kind),
            });
        }
    }

    // Paredes externas: fronteira com o espaço, reativas (WallSensitive, grupo Walls). Transitório até a DysonWall.
    private void CreateWalls()
    {
        var walls = new Node2D { Name = "Walls" };
        AddChild(walls);
        MoveChild(walls, 2); // Acima do piso, abaixo do jogador

        for (int i = 0; i < Layout.WallSegments.Count; i++)
        {
            walls.AddChild(CreateWall(Layout.WallSegments[i], i));
        }
    }

    // Mesmo formato das paredes das cenas atuais: StaticBody2D com ColorRect (nome exato) e CollisionShape2D,
    // pivô no centro. Os filhos entram antes da parede ir para a árvore, porque o _Ready dela procura o ColorRect.
    private WallSensitive CreateWall(HexWallSegment segment, int index)
    {
        var size = new Vector2(segment.Start.DistanceTo(segment.End), WallThickness);
        var wall = new WallSensitive
        {
            Name = $"Wall{index}",
            Position = (segment.Start + segment.End) / 2f + segment.Outward * (WallThickness / 2f), // Cresce para fora do piso
            Rotation = (segment.End - segment.Start).Angle(),
            MaxIrritation = WallMaxIrritation(segment.Kind), // O perigo fica onde está o aperto
        };
        wall.AddToGroup("Walls");
        wall.AddChild(new ColorRect
        {
            Name = "ColorRect",
            Position = -size / 2f,
            Size = size,
            Color = WallColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        wall.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size } });
        return wall;
    }

    private float WallMaxIrritation(HexStructureKind kind) => kind switch
    {
        HexStructureKind.NodeConnector => CorridorWallMaxIrritation,
        HexStructureKind.PlateConnector => PlateWallMaxIrritation,
        _ => ArenaWallMaxIrritation, // Nós e painéis
    };

    private Color FloorColor(HexStructureKind kind) => kind switch
    {
        HexStructureKind.CentralNode or HexStructureKind.PeripheralNode => NodeFloorColor,
        HexStructureKind.Panel => PanelFloorColor,
        _ => ConnectorFloorColor,
    };
}
