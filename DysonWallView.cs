using Godot;
using System;

// Desenho da DysonWall pela W1 (spec dyson-wall.md, decisão A1): todas as peças em C#, numa única lista de
// triângulos (um só comando de desenho por parede). A Godot guarda o desenho e o repete a cada quadro; o C# só
// refaz a lista quando alguém pede (QueueRedraw). Ordem: as membranas atrás, depois os sub-triângulos.
public partial class DysonWallView : Node2D, IDysonWallView
{
    private const float PieceTintAmount = 0.15f; // Variação máxima da cor de cada sub-triângulo, pelo sorteio (spec 2.7)
    private static readonly Color PieceTint = Color.Color8(160, 110, 120); // Para onde a variação puxa a cor (protótipo)

    private Vector2[] _points = Array.Empty<Vector2>(); // Vértices de todos os triângulos, de 3 em 3
    private Color[] _colors = Array.Empty<Color>();     // Cor de cada vértice

    public void Build(DysonWallGeometry geometry, DysonWallPalette palette)
    {
        int membranes = geometry.Level > 0 ? geometry.TileCount : 0; // No nível 0 não há vãos para a membrana fechar
        int triangles = membranes + geometry.SubCount;
        _points = new Vector2[triangles * 3];
        _colors = new Color[triangles * 3];

        int vertex = 0;
        for (int t = 0; t < membranes; t++)
        {
            vertex = AddTriangle(vertex, geometry.TileVertices, t, palette.Membrane);
        }
        for (int s = 0; s < geometry.SubCount; s++)
        {
            int tile = s / geometry.SubsPerTile;
            Color baseColor = geometry.TileParities[tile] == 0 ? palette.PieceA : palette.PieceB;
            vertex = AddTriangle(vertex, geometry.SubVertices, s, baseColor.Lerp(PieceTint, geometry.SubRandoms[s] * PieceTintAmount));
        }

        QueueRedraw();
    }

    // Copia o triângulo `triangle` de `source` (3 vértices por triângulo) para a lista de desenho, com uma cor só
    private int AddTriangle(int vertex, Vector2[] source, int triangle, Color color)
    {
        for (int k = 0; k < 3; k++)
        {
            _points[vertex] = source[triangle * 3 + k];
            _colors[vertex] = color;
            vertex++;
        }
        return vertex;
    }

    public override void _Draw()
    {
        if (_points.Length == 0) return; // Parede sem peças (comprimento ou espessura zero)
        RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), Array.Empty<int>(), _points, _colors);
    }
}
