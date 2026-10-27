using Godot;
using System;

// Desenho da DysonWall pela W1 (spec dyson-wall.md, decisão A1): todas as peças em C#, numa única lista de
// triângulos (um só comando de desenho por parede). A Godot guarda o desenho e o repete a cada quadro; o C# só
// refaz a lista quando alguém pede (QueueRedraw). Ordem: as membranas atrás, depois os sub-triângulos.
public partial class DysonWallView : Node2D, IDysonWallView
{
    private const float PieceTintAmount = 0.15f; // Variação máxima da cor de cada sub-triângulo, pelo sorteio (spec 2.7)
    private static readonly Color PieceTint = Color.Color8(160, 110, 120); // Para onde a variação puxa a cor (protótipo)
    private const float IrritationColorMix = 0.55f; // Peça no máximo: 55% da cor da irritação (spec 2.2)

    private DysonWallGeometry _geometry;
    private DysonWallStyle _style;
    private int _membraneCount;                              // Membranas desenhadas (0 no nível 0, que não tem vãos)
    private Vector2[] _points = Array.Empty<Vector2>();      // Vértices de todos os triângulos, de 3 em 3
    private Color[] _colors = Array.Empty<Color>();          // Cor de cada vértice
    private Color[] _subBaseColors = Array.Empty<Color>();   // Cor de cada sub-triângulo com a parede calma
    private Vector2[] _tileOffsets = Array.Empty<Vector2>(); // Tremor de cada peça neste desenho
    private float[] _tileIrritation;                         // Irritação de cada peça (array da parede; null = nunca irritada)
    private readonly Random _random = new();                 // Sorteio do tremor

    public void Build(DysonWallGeometry geometry, DysonWallStyle style)
    {
        _geometry = geometry;
        _style = style;
        _membraneCount = geometry.Level > 0 ? geometry.TileCount : 0;
        int triangles = _membraneCount + geometry.SubCount;
        _points = new Vector2[triangles * 3];
        _colors = new Color[triangles * 3];
        _subBaseColors = new Color[geometry.SubCount];
        _tileOffsets = new Vector2[geometry.TileCount];

        for (int s = 0; s < geometry.SubCount; s++)
        {
            int tile = s / geometry.SubsPerTile;
            Color baseColor = geometry.TileParities[tile] == 0 ? style.PieceA : style.PieceB;
            _subBaseColors[s] = baseColor.Lerp(PieceTint, geometry.SubRandoms[s] * PieceTintAmount);
        }
        FillTriangles();
        QueueRedraw();
    }

    public void Refresh(float[] tileIrritation)
    {
        _tileIrritation = tileIrritation;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_points.Length == 0) return; // Parede sem peças (comprimento ou espessura zero)

        if (_tileIrritation != null) FillTriangles(); // Irritada: brilho e tremor novos a cada desenho
        RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), Array.Empty<int>(), _points, _colors);
    }

    // Monta a lista de triângulos: membranas e sub-triângulos, com o tremor e o brilho de cada peça.
    // O tremor é sorteado uma vez por peça: a membrana e os sub-triângulos dela tremem juntos.
    private void FillTriangles()
    {
        for (int t = 0; t < _geometry.TileCount; t++) _tileOffsets[t] = TremorOffset(t);

        int vertex = 0;
        for (int t = 0; t < _membraneCount; t++)
        {
            vertex = AddTriangle(vertex, _geometry.TileVertices, t, _tileOffsets[t], _style.Membrane);
        }
        for (int t = 0; t < _geometry.TileCount; t++)
        {
            float glow = IrritationOf(t) * IrritationColorMix;
            int firstSub = t * _geometry.SubsPerTile;
            for (int s = firstSub; s < firstSub + _geometry.SubsPerTile; s++)
            {
                vertex = AddTriangle(vertex, _geometry.SubVertices, s, _tileOffsets[t], _subBaseColors[s].Lerp(_style.Irritation, glow));
            }
        }
    }

    // Tremor da peça neste desenho (spec 2.2): só acima do início, sorteado de novo a cada desenho
    private Vector2 TremorOffset(int tile)
    {
        float strength = (IrritationOf(tile) - _style.TremorStartIrritation) * _style.TremorStrength;
        if (strength <= 0f) return Vector2.Zero;
        return new Vector2(_random.NextSingle() - 0.5f, _random.NextSingle() - 0.5f) * strength;
    }

    private float IrritationOf(int tile) => _tileIrritation?[tile] ?? 0f;

    // Copia o triângulo `triangle` de `source` (3 vértices por triângulo) para a lista de desenho, deslocado e com uma cor só
    private int AddTriangle(int vertex, Vector2[] source, int triangle, Vector2 offset, Color color)
    {
        for (int k = 0; k < 3; k++)
        {
            _points[vertex] = source[triangle * 3 + k] + offset;
            _colors[vertex] = color;
            vertex++;
        }
        return vertex;
    }
}
