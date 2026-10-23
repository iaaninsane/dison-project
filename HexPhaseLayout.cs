using Godot;
using System.Collections.Generic;

// Formato da fase (spec hex-phase.md, H4): o setor de teste (um triângulo do hexágono) ou o hexágono completo
public enum HexPhaseShape { Sector, Hexagon }

// Tipos de estrutura da esfera que viram salas da fase
public enum HexStructureKind { CentralNode, PeripheralNode, NodeConnector, PlateConnector, Panel }

// Medidas da fase (valores em teste; vêm dos [Export] do construtor)
public readonly record struct HexPhaseSettings(
    HexPhaseShape Shape,
    float NodeSpacing,         // Distância entre os centros de nós vizinhos
    float NodeRadius,          // Raio interno das câmaras dos nós
    int NodeSides,             // Lados do polígono que aproxima o círculo do nó
    float ConnectorWidth,      // Largura interna dos conectores de nós
    float PlateConnectorWidth, // Largura interna dos conectores de placa
    float PanelGap,            // Vão (espaço) entre o painel e as paredes dos conectores de nós
    float WallThickness,       // Espessura das paredes
    float MaxWallSegmentLength); // Paredes mais longas são divididas (cada trecho tem a sua irritação)

// Um trecho de parede: segmento na borda do piso, o lado de fora (para onde a parede cresce) e a estrutura dona
public readonly record struct HexWallSegment(Vector2 Start, Vector2 End, Vector2 Outward, HexStructure Owner)
{
    public HexStructureKind Kind => Owner.Kind;
}

// Uma estrutura da fase: tipo, nome (para o HUD de teste) e contorno interno (piso), em coordenadas da fase
public class HexStructure
{
    public HexStructureKind Kind { get; }
    public string Name { get; }
    public Vector2[] Outline { get; }
    public Rect2 Bounds { get; } // Para descartar rápido as estruturas longe de um ponto ou segmento

    public HexStructure(HexStructureKind kind, string name, Vector2[] outline)
    {
        Kind = kind;
        Name = name;
        Outline = outline;
        Bounds = RectAround(outline);
    }

    public bool Contains(Vector2 point) => Geometry2D.IsPointInPolygon(point, Outline);

    public static Rect2 RectAround(Vector2[] points)
    {
        var rect = new Rect2(points[0], Vector2.Zero);
        foreach (Vector2 point in points) rect = rect.Expand(point);
        return rect;
    }
}

// Geometria da fase hexagonal: só dados e matemática, sem nó (como o RunData).
// Origem no nó central; os nós periféricos ficam a cada 60°, começando acima e à direita (hexágono "deitado").
public class HexPhaseLayout
{
    private const int PeripheralCount = 6;
    private const float FirstPeripheralAngleDegrees = -60f; // Ângulo do nó periférico 1 (decisão de design: base dos painéis na horizontal)
    private const float MinWallPieceLength = 2f; // Sobras menores que isso (arredondamento nas emendas) não viram parede

    private readonly HexPhaseSettings _settings;
    private readonly List<HexStructure> _structures = new(); // Em ordem de prioridade: nós, conectores, placas, painéis
    private readonly List<HexWallSegment> _wallSegments = new();
    private readonly Dictionary<int, HexStructure> _peripheralNodes = new(); // Pelo índice (0 a 5)

    public IReadOnlyList<HexStructure> Structures => _structures;
    public IReadOnlyList<HexWallSegment> WallSegments => _wallSegments;
    public Rect2 Bounds { get; private set; } // Retângulo que envolve todas as estruturas (fundo e limites da câmera)

    public HexPhaseLayout(HexPhaseSettings settings)
    {
        _settings = settings;
        WarnIfCorridorsCollide();
        Build();
    }

    // Em cada nó, o conector de placa sai a 30° de um conector de nós. Na saída do nó, a distância entre os
    // eixos precisa caber meia largura de cada corredor mais uma parede; senão a parede de um invade o outro.
    private void WarnIfCorridorsCollide()
    {
        float halfConnector = _settings.ConnectorWidth / 2f;
        float exitRadius = Mathf.Sqrt(_settings.NodeRadius * _settings.NodeRadius - halfConnector * halfConnector);
        float axisDistance = exitRadius * Mathf.Sin(Mathf.DegToRad(30f));
        float needed = halfConnector + _settings.PlateConnectorWidth / 2f + _settings.WallThickness;
        if (axisDistance < needed)
        {
            GD.PushWarning($"[HexPhaseLayout]: NodeRadius pequeno demais para essas larguras: os corredores vizinhos " +
                           $"saem a {axisDistance:0} px um do outro e precisam de {needed:0} px (as paredes invadem o corredor ao lado).");
        }
    }

    // Em qual estrutura está o ponto? null = espaço (fora de todas).
    // Nas passagens vale a primeira da lista: o nó ganha do conector, e o conector de placa ganha do painel.
    public HexStructure FindStructureAt(Vector2 point)
    {
        foreach (HexStructure structure in _structures)
        {
            if (structure.Contains(point)) return structure;
        }
        return null;
    }

    private void Build()
    {
        List<int> peripherals = GetPeripheralIndices();
        List<(int A, int B)> neighborPairs = GetNeighborPairs(peripherals);
        Vector2 center = Vector2.Zero;

        // 1. Nós primeiro (nas passagens, o nó ganha)
        AddNode(HexStructureKind.CentralNode, "Nó central", center);
        foreach (int i in peripherals)
        {
            _peripheralNodes[i] = AddNode(HexStructureKind.PeripheralNode, $"Nó {i + 1}", PeripheralPosition(i));
        }

        // 2. Conectores de nós: raios (centro → periférico) e perímetro (periférico → vizinho)
        foreach (int i in peripherals)
        {
            AddNodeConnector($"Conector C-{i + 1}", center, PeripheralPosition(i));
        }
        foreach ((int a, int b) in neighborPairs)
        {
            AddNodeConnector($"Conector {a + 1}-{b + 1}", PeripheralPosition(a), PeripheralPosition(b));
        }

        // 3. Conectores de placa agora; os painéis vão para o fim da lista (a placa ganha do painel na passagem)
        var panels = new List<HexStructure>();
        foreach ((int a, int b) in neighborPairs)
        {
            Vector2[] corners = { center, PeripheralPosition(a), PeripheralPosition(b) };
            string[] cornerNames = { "C", $"{a + 1}", $"{b + 1}" };
            Vector2[] outline = BuildPanelOutline(corners, out Vector2[] cutMidpoints);
            panels.Add(new HexStructure(HexStructureKind.Panel, $"Painel {a + 1}-{b + 1}", outline));

            for (int k = 0; k < 3; k++)
            {
                AddPlateConnector($"Placa {a + 1}-{b + 1} ({cornerNames[k]})", cutMidpoints[k], corners[k]);
            }
        }
        _structures.AddRange(panels);
        Bounds = ComputeBounds();
        BuildWalls();
    }

    private Rect2 ComputeBounds()
    {
        Rect2 bounds = _structures[0].Bounds;
        foreach (HexStructure structure in _structures) bounds = bounds.Merge(structure.Bounds);
        return bounds;
    }

    // Paredes: toda borda de piso vira parede, menos os trechos que caem dentro de outra estrutura
    // (as passagens). Assim as aberturas saem sozinhas, sem caso especial para cada tipo de encaixe.
    private void BuildWalls()
    {
        foreach (HexStructure structure in _structures)
        {
            Vector2[] outline = structure.Outline;
            Vector2 centroid = Centroid(outline);
            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % outline.Length];
                Vector2 outward = OutwardNormal(a, b, centroid);
                foreach (Vector2[] piece in ClipByOtherStructures(a, b, structure))
                {
                    AddWallPieces(piece[0], piece[^1], outward, structure);
                }
            }
        }
    }

    // Trechos do segmento a → b que ficam fora de todas as outras estruturas
    private List<Vector2[]> ClipByOtherStructures(Vector2 a, Vector2 b, HexStructure owner)
    {
        Rect2 segmentBounds = HexStructure.RectAround(new[] { a, b });
        var pieces = new List<Vector2[]> { new[] { a, b } };
        foreach (HexStructure other in _structures)
        {
            if (other == owner || !other.Bounds.Intersects(segmentBounds, true)) continue;

            var remaining = new List<Vector2[]>();
            foreach (Vector2[] piece in pieces)
            {
                remaining.AddRange(Geometry2D.ClipPolylineWithPolygon(piece, other.Outline));
            }
            pieces = remaining;
        }
        return pieces;
    }

    // Divide o trecho em partes de no máximo MaxWallSegmentLength (cada parte é uma parede com a sua irritação)
    private void AddWallPieces(Vector2 start, Vector2 end, Vector2 outward, HexStructure owner)
    {
        float length = start.DistanceTo(end);
        if (length < MinWallPieceLength) return;

        int count = Mathf.CeilToInt(length / _settings.MaxWallSegmentLength);
        for (int k = 0; k < count; k++)
        {
            Vector2 from = start.Lerp(end, (float)k / count);
            Vector2 to = start.Lerp(end, (float)(k + 1) / count);
            _wallSegments.Add(new HexWallSegment(from, to, outward, owner));
        }
    }

    // Normal da borda apontando para fora do piso (as estruturas são convexas: "fora" é longe do centro)
    private static Vector2 OutwardNormal(Vector2 a, Vector2 b, Vector2 centroid)
    {
        Vector2 normal = (b - a).Orthogonal().Normalized();
        return normal.Dot((a + b) / 2f - centroid) >= 0f ? normal : -normal;
    }

    // Contorno fechado (volta ao primeiro ponto), para tratar a borda como uma linha só
    private static Vector2[] ClosedPolyline(Vector2[] outline)
    {
        var closed = new Vector2[outline.Length + 1];
        outline.CopyTo(closed, 0);
        closed[^1] = outline[0];
        return closed;
    }

    // Pontas de cada pedaço (uma boca vem em dois pedaços quando cruza o início do contorno)
    private static List<Vector2> EndPoints(Godot.Collections.Array<Vector2[]> pieces)
    {
        var points = new List<Vector2>();
        foreach (Vector2[] piece in pieces)
        {
            points.Add(piece[0]);
            points.Add(piece[^1]);
        }
        return points;
    }

    // Os dois pontos mais distantes entre si: as bordas da boca
    private static (Vector2, Vector2) FarthestPair(List<Vector2> points)
    {
        (Vector2, Vector2) best = (points[0], points[1]);
        float bestDistance = -1f;
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                float distance = points[i].DistanceSquaredTo(points[j]);
                if (distance <= bestDistance) continue;
                bestDistance = distance;
                best = (points[i], points[j]);
            }
        }
        return best;
    }

    private static Vector2 Centroid(Vector2[] points)
    {
        Vector2 sum = Vector2.Zero;
        foreach (Vector2 point in points) sum += point;
        return sum / points.Length;
    }

    // Setor: só os periféricos 1 e 2 (um triângulo com o centro). Hexágono: os 6.
    private List<int> GetPeripheralIndices()
    {
        int count = _settings.Shape == HexPhaseShape.Hexagon ? PeripheralCount : 2;
        var indices = new List<int>();
        for (int i = 0; i < count; i++) indices.Add(i);
        return indices;
    }

    // Pares de periféricos vizinhos que existem na fase: cada par tem um corredor de perímetro e um painel
    private static List<(int A, int B)> GetNeighborPairs(List<int> peripherals)
    {
        var pairs = new List<(int A, int B)>();
        foreach (int i in peripherals)
        {
            int next = (i + 1) % PeripheralCount;
            if (next != i && peripherals.Contains(next)) pairs.Add((i, next));
        }
        return pairs;
    }

    // Nó periférico pelo número usado nos nomes (1 a 6); se o nó não existe nesta fase, o nó central
    public HexStructure PeripheralNode(int number) =>
        _peripheralNodes.TryGetValue(number - 1, out HexStructure node) ? node : _structures[0];

    // Bocas de um nó: onde os corredores entram nele. Cada boca vira um trecho na borda do nó, de uma parede do
    // corredor à outra, alargado uma espessura de parede de cada lado para encostar nas paredes do corredor.
    public List<HexWallSegment> ExitsOf(HexStructure node)
    {
        var exits = new List<HexWallSegment>();
        Vector2 center = Centroid(node.Outline);
        Vector2[] rim = ClosedPolyline(node.Outline);
        foreach (HexStructure other in _structures)
        {
            if (other == node || !other.Bounds.Intersects(node.Bounds, true)) continue;

            List<Vector2> points = EndPoints(Geometry2D.IntersectPolylineWithPolygon(rim, other.Outline));
            if (points.Count < 2) continue; // Não encosta neste nó

            (Vector2 a, Vector2 b) = FarthestPair(points);
            Vector2 widen = (b - a).Normalized() * _settings.WallThickness;
            exits.Add(new HexWallSegment(a - widen, b + widen, OutwardNormal(a, b, center), node));
        }
        return exits;
    }

    // Centro do nó periférico pelo número usado nos nomes (1 a 6); se o nó não existe nesta fase, o nó central
    public Vector2 PeripheralNodeCenter(int number)
    {
        int index = number - 1;
        bool exists = index >= 0 && index < GetPeripheralIndices().Count;
        return exists ? PeripheralPosition(index) : Vector2.Zero;
    }

    // Começa em −60° (acima e à direita): o hexágono fica "deitado" e todo painel tem um lado horizontal.
    // No setor de teste, o painel aponta para cima, com a base no corredor do centro até o nó da direita.
    private Vector2 PeripheralPosition(int index) =>
        Vector2.FromAngle(Mathf.DegToRad(FirstPeripheralAngleDegrees + 60f * index)) * _settings.NodeSpacing;

    private HexStructure AddNode(HexStructureKind kind, string name, Vector2 position)
    {
        var node = new HexStructure(kind, name, RegularPolygon(position, _settings.NodeRadius, _settings.NodeSides));
        _structures.Add(node);
        return node;
    }

    // O corredor entra uma espessura de parede em cada nó, para não sobrar fresta na emenda
    private void AddNodeConnector(string name, Vector2 from, Vector2 to)
    {
        float inset = NodeInset(_settings.ConnectorWidth) - _settings.WallThickness;
        AddCorridor(HexStructureKind.NodeConnector, name, from, to, _settings.ConnectorWidth, inset, inset);
    }

    // Do corte no canto do painel até o nó; começa uma espessura de parede dentro do painel, pelo mesmo motivo
    private void AddPlateConnector(string name, Vector2 panelCut, Vector2 nodeCenter)
    {
        float nodeInset = NodeInset(_settings.PlateConnectorWidth) - _settings.WallThickness;
        AddCorridor(HexStructureKind.PlateConnector, name, panelCut, nodeCenter,
                    _settings.PlateConnectorWidth, -_settings.WallThickness, nodeInset);
    }

    // Retângulo de largura `width` ao longo de from → to, encurtado `startOffset` no início e `endOffset` no fim
    private void AddCorridor(HexStructureKind kind, string name, Vector2 from, Vector2 to,
                             float width, float startOffset, float endOffset)
    {
        Vector2 along = (to - from).Normalized();
        Vector2 side = along.Orthogonal() * (width / 2f);
        Vector2 start = from + along * startOffset;
        Vector2 end = to - along * endOffset;
        _structures.Add(new HexStructure(kind, name, new[] { start + side, end + side, end - side, start - side }));
    }

    // Distância do centro do nó até onde um corredor dessa largura encosta no círculo do nó
    private float NodeInset(float corridorWidth)
    {
        float halfWidth = corridorWidth / 2f;
        return Mathf.Sqrt(_settings.NodeRadius * _settings.NodeRadius - halfWidth * halfWidth);
    }

    // Painel: o triângulo entre os três nós, encolhido para ficar a PanelGap das paredes dos corredores,
    // com os cantos cortados na largura do conector de placa (num canto de 60°, o pedaço cortado é um
    // triângulo equilátero de lado PlateConnectorWidth). Devolve também o meio de cada corte.
    private Vector2[] BuildPanelOutline(Vector2[] corners, out Vector2[] cutMidpoints)
    {
        Vector2 centroid = (corners[0] + corners[1] + corners[2]) / 3f;
        float clearance = _settings.ConnectorWidth / 2f + _settings.WallThickness + _settings.PanelGap;
        float panelInradius = _settings.NodeSpacing / (2f * Mathf.Sqrt(3f)) - clearance;
        float cut = _settings.PlateConnectorWidth;

        if (panelInradius * Mathf.Sqrt(3f) <= cut)
        {
            GD.PushWarning("[HexPhaseLayout]: as medidas deixam o painel pequeno demais para os conectores de placa.");
        }

        var vertices = new Vector2[3];
        for (int i = 0; i < 3; i++)
        {
            // No triângulo equilátero, o vértice fica a 2 × raio inscrito do centro
            vertices[i] = centroid + (corners[i] - centroid).Normalized() * (2f * panelInradius);
        }

        var outline = new List<Vector2>();
        cutMidpoints = new Vector2[3];
        for (int i = 0; i < 3; i++)
        {
            Vector2 vertex = vertices[i];
            Vector2 cutA = vertex + (vertices[(i + 2) % 3] - vertex).Normalized() * cut;
            Vector2 cutB = vertex + (vertices[(i + 1) % 3] - vertex).Normalized() * cut;
            outline.Add(cutA);
            outline.Add(cutB);
            cutMidpoints[i] = (cutA + cutB) / 2f;
        }
        return outline.ToArray();
    }

    private static Vector2[] RegularPolygon(Vector2 center, float radius, int sides)
    {
        var points = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            points[i] = center + Vector2.FromAngle(Mathf.Tau * i / sides) * radius;
        }
        return points;
    }
}
