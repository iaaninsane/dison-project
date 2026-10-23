using Godot;
using System.Collections.Generic;

// Monta a fase hexagonal na cena a partir da geometria do HexPhaseLayout (spec hex-phase.md, H3).
// Fica na raiz da hex_phase.tscn. Cria fundo, pisos e paredes, posiciona a porta da eclusa e põe o jogador à frente dela.
public partial class HexPhaseBuilder : Node2D
{
    [Signal] public delegate void PlayerPlacedEventHandler(); // O jogador foi posto na eclusa (ao nascer)

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
    [Export] public Node2D Airlock;                   // Porta da eclusa, montada no editor (ligada no Inspector)
    [Export] public float AirlockInset = 60f;         // Distância da parede externa do nó até o centro da porta (px)
    [Export] public float AirlockSpawnDistance = 300f; // O jogador nasce a essa distância da porta, para dentro do nó (px)
    [Export] public Node2D StartChamber;              // Câmara de partida, montada no editor (ligada no Inspector); vazio = fase aberta

    [ExportGroup("Visual provisório (arte na etapa 2)")]
    [Export] public float BackgroundMargin = 2000f;   // Quanto o fundo passa da fase em cada lado
    [Export] public Color SpaceColor = new Color(0.12f, 0.08f, 0.16f);
    [Export] public Color NodeFloorColor = new Color(0.42f, 0.27f, 0.35f);
    [Export] public Color ConnectorFloorColor = new Color(0.36f, 0.23f, 0.30f);
    [Export] public Color PanelFloorColor = new Color(0.30f, 0.19f, 0.26f);
    [Export] public Color WallColor = new Color(0.59f, 0.29f, 0.24f);
    [Export] public Color GateColor = new Color(0.25f, 0.55f, 0.65f); // Portões da câmara de partida

    public HexPhaseLayout Layout { get; private set; } // Geometria da fase, para quem precisar perguntar "onde estou?"

    private const string WallsGroup = "Walls";                // Paredes reativas: a luz irrita, a ejeção atravessa
    private readonly List<WallSensitive> _entryWalls = new(); // Paredes do nó de entrada, inertes até liberar as saídas
    private Node2D _entryGates;                               // Portões nas bocas do nó de entrada (null = liberadas)

    public override void _Ready()
    {
        Layout = new HexPhaseLayout(CreateSettings());
        CreateBackground();
        CreateFloors();
        CreateWalls();
        CreateEntryGates();
        PlaceAirlock();
        PlaceStartChamber();
        PlacePlayerAtEntry(); // Nasce na eclusa
    }

    // Põe o jogador na eclusa (à frente da porta), parado, e avisa (a câmera se ajusta sem a fase precisar conhecê-la)
    public void PlacePlayerAtEntry()
    {
        if (Player == null) return;
        Player.GlobalPosition = ToGlobal(EntryPoint(AirlockInset + AirlockSpawnDistance));
        Player.Velocity = Vector2.Zero;
        EmitSignal(SignalName.PlayerPlaced);
    }

    // Porta na parede externa do nó de entrada (o lado oposto ao centro da fase, onde não sai corredor).
    // A porta é girada para fora: o eixo X dela aponta para o espaço.
    private void PlaceAirlock()
    {
        if (Airlock == null) return;
        Airlock.GlobalPosition = ToGlobal(EntryPoint(AirlockInset));
        Airlock.GlobalRotation = GlobalRotation + EntryOutward().Angle();
    }

    // Câmara de partida no centro do nó de entrada, girada como a eclusa (o eixo X dela aponta para a porta)
    private void PlaceStartChamber()
    {
        if (StartChamber == null) return;
        StartChamber.GlobalPosition = ToGlobal(Layout.PeripheralNodeCenter(EntryPeripheral));
        StartChamber.GlobalRotation = GlobalRotation + EntryOutward().Angle();
    }

    // Ponto no eixo que sai do centro da fase e passa pelo nó de entrada, a `distanceFromWall` da parede externa
    private Vector2 EntryPoint(float distanceFromWall) =>
        Layout.PeripheralNodeCenter(EntryPeripheral) + EntryOutward() * (NodeRadius - distanceFromWall);

    // "Para fora" no nó de entrada: do centro da fase para o centro do nó. Nó inválido (vira o central): a direita.
    private Vector2 EntryOutward()
    {
        Vector2 nodeCenter = Layout.PeripheralNodeCenter(EntryPeripheral);
        return nodeCenter == Vector2.Zero ? Vector2.Right : nodeCenter.Normalized();
    }

    // Libera as saídas da câmara de partida: os portões somem e as paredes do nó ficam reativas como as outras
    public void OpenEntryExits()
    {
        if (_entryGates == null) return; // Já liberadas

        _entryGates.QueueFree();
        _entryGates = null;
        foreach (WallSensitive wall in _entryWalls) wall.AddToGroup(WallsGroup);
        _entryWalls.Clear();
    }

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
    // Com a câmara de partida, as do nó de entrada ficam fora do grupo, inertes como titânio, até liberar as saídas.
    private void CreateWalls()
    {
        var walls = new Node2D { Name = "Walls" };
        AddChild(walls);
        MoveChild(walls, 2); // Acima do piso, abaixo do jogador

        HexStructure lockedNode = StartChamber != null ? Layout.PeripheralNode(EntryPeripheral) : null; // Sem câmara: nada travado
        for (int i = 0; i < Layout.WallSegments.Count; i++)
        {
            HexWallSegment segment = Layout.WallSegments[i];
            WallSensitive wall = CreateWall(segment, i);
            walls.AddChild(wall);

            if (segment.Owner == lockedNode) _entryWalls.Add(wall);
            else wall.AddToGroup(WallsGroup);
        }
    }

    // Portões nas bocas do nó de entrada: corpos rígidos fora do grupo Walls (a luz não irrita, a ejeção não atravessa)
    private void CreateEntryGates()
    {
        if (StartChamber == null) return; // Sem câmara de partida, a fase começa aberta

        _entryGates = new Node2D { Name = "EntryGates" };
        AddChild(_entryGates);
        MoveChild(_entryGates, 3); // Logo acima das paredes

        List<HexWallSegment> exits = Layout.ExitsOf(Layout.PeripheralNode(EntryPeripheral));
        for (int i = 0; i < exits.Count; i++)
        {
            _entryGates.AddChild(CreateSegmentBody(new StaticBody2D { Name = $"Gate{i}" }, exits[i], GateColor));
        }
    }

    private WallSensitive CreateWall(HexWallSegment segment, int index)
    {
        var wall = new WallSensitive
        {
            Name = $"Wall{index}",
            MaxIrritation = WallMaxIrritation(segment.Kind), // O perigo fica onde está o aperto
        };
        return CreateSegmentBody(wall, segment, WallColor);
    }

    // Corpo retangular ao longo de um trecho, no formato das paredes das cenas atuais: StaticBody2D com ColorRect
    // (nome exato) e CollisionShape2D, pivô no centro, crescendo para fora do piso. Os filhos entram antes de o
    // corpo ir para a árvore, porque o _Ready do WallSensitive procura o ColorRect.
    private T CreateSegmentBody<T>(T body, HexWallSegment segment, Color color) where T : StaticBody2D
    {
        var size = new Vector2(segment.Start.DistanceTo(segment.End), WallThickness);
        body.Position = (segment.Start + segment.End) / 2f + segment.Outward * (WallThickness / 2f);
        body.Rotation = (segment.End - segment.Start).Angle();
        body.AddChild(new ColorRect
        {
            Name = "ColorRect",
            Position = -size / 2f,
            Size = size,
            Color = color,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = size } });
        return body;
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
