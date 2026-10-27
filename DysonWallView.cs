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
    private const float MinFigureSize = 0.3f;       // Figura menor que isto (px) não aparece (spec 2.5)

    private DysonWallGeometry _geometry;
    private DysonWallStyle _style;
    private int _membraneCount;                            // Membranas desenhadas (0 no nível 0, que não tem vãos)
    private Vector2[] _points = Array.Empty<Vector2>();    // Vértices de todos os triângulos, de 3 em 3
    private Color[] _colors = Array.Empty<Color>();        // Cor de cada vértice
    private Color[] _subBaseColors = Array.Empty<Color>(); // Cor de cada sub-triângulo com a parede calma
    private DysonWallState _state;                         // Estado da parede (arrays dela), dado pelo Refresh
    private bool _hasState;                                // false = nunca acordou: tudo calmo e inteiro
    private readonly Random _random = new();               // Sorteio do tremor

    public void Build(DysonWallGeometry geometry, DysonWallStyle style)
    {
        _geometry = geometry;
        _style = style;
        _membraneCount = geometry.Level > 0 ? geometry.TileCount : 0;
        int triangles = _membraneCount + geometry.SubCount;
        _points = new Vector2[triangles * 3];
        _colors = new Color[triangles * 3];
        _subBaseColors = new Color[geometry.SubCount];

        for (int s = 0; s < geometry.SubCount; s++)
        {
            int tile = s / geometry.SubsPerTile;
            Color baseColor = geometry.TileParities[tile] == 0 ? style.PieceA : style.PieceB;
            _subBaseColors[s] = baseColor.Lerp(PieceTint, geometry.SubRandoms[s] * PieceTintAmount);
        }
        FillMembranes();
        FillSubTriangles();
        QueueRedraw();
    }

    public void Refresh(DysonWallState state)
    {
        _state = state;
        _hasState = true;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_points.Length == 0) return; // Parede sem peças (comprimento ou espessura zero)

        if (_hasState)
        {
            FillMembranes();    // Paradas (A5), mas encolhem na abertura
            FillSubTriangles(); // Brilho, tremor e escala novos a cada desenho
        }
        RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), Array.Empty<int>(), _points, _colors);
    }

    // Membranas no começo da lista: paradas atrás, como tecido de fundo (decisão A5). Elas se encaixam sem vãos,
    // então o fundo não aparece entre as peças quando os sub-triângulos tremem. Na abertura, encolhem no lugar.
    private void FillMembranes()
    {
        for (int t = 0; t < _membraneCount; t++)
        {
            float scale = VisibleScale(MembraneScaleOf(t), _geometry.TileSide);
            AddTriangle(t * 3, _geometry.TileVertices, t, _geometry.TileCenters[t], scale, Vector2.Zero, _style.Membrane);
        }
    }

    // Sub-triângulos depois das membranas, com o brilho da peça, um tremor sorteado para cada um, sem sincronia com os
    // vizinhos (decisão A5), e a escala da abertura, em volta do próprio centro
    private void FillSubTriangles()
    {
        int vertex = _membraneCount * 3;
        float subSide = _geometry.SubSide;
        for (int t = 0; t < _geometry.TileCount; t++)
        {
            float glow = IrritationOf(t) * IrritationColorMix; // Brilho e força do tremor são da peça: uma conta por peça
            float tremor = TremorStrengthOf(t);
            int firstSub = t * _geometry.SubsPerTile;
            for (int s = firstSub; s < firstSub + _geometry.SubsPerTile; s++)
            {
                float scale = VisibleScale(SubScaleOf(s), subSide);
                Color color = _subBaseColors[s].Lerp(_style.Irritation, glow);
                vertex = AddTriangle(vertex, _geometry.SubVertices, s, _geometry.SubCenters[s], scale, TremorOffset(tremor), color);
            }
        }
    }

    // Força do tremor dos sub-triângulos de uma peça (spec 2.2): pela irritação, acima do início, mais o tremor extra
    // do aviso da abertura (2.3)
    private float TremorStrengthOf(int tile) =>
        Mathf.Max(0f, IrritationOf(tile) - _style.TremorStartIrritation) * _style.TremorStrength + TremorBoostOf(tile);

    // Tremor de um sub-triângulo neste desenho: sorteado de novo a cada desenho, sem sincronia com os vizinhos (A5)
    private Vector2 TremorOffset(float strength)
    {
        if (strength <= 0f) return Vector2.Zero;
        return new Vector2(_random.NextSingle() - 0.5f, _random.NextSingle() - 0.5f) * strength;
    }

    // Figura que ficaria menor que MinFigureSize some de vez (escala 0: o triângulo vira um ponto e não aparece)
    private static float VisibleScale(float scale, float size) => scale * size < MinFigureSize ? 0f : scale;

    private float IrritationOf(int tile) => _hasState ? _state.TileIrritation[tile] : 0f;
    private float TremorBoostOf(int tile) => _hasState ? _state.TileTremorBoost[tile] : 0f;
    private float SubScaleOf(int sub) => _hasState ? _state.SubScales[sub] : 1f;
    private float MembraneScaleOf(int tile) => _hasState ? _state.MembraneScales[tile] : 1f;

    // Copia o triângulo `triangle` de `source` (3 vértices por triângulo) para a lista de desenho: escalado em volta
    // de `center` (só na abertura; inteiro, quase sempre, pula a conta), deslocado e com uma cor só
    private int AddTriangle(int vertex, Vector2[] source, int triangle, Vector2 center, float scale, Vector2 offset, Color color)
    {
        for (int k = 0; k < 3; k++)
        {
            Vector2 point = source[triangle * 3 + k];
            if (scale != 1f) point = center + (point - center) * scale;
            _points[vertex] = point + offset;
            _colors[vertex] = color;
            vertex++;
        }
        return vertex;
    }
}
