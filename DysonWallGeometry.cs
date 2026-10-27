using Godot;
using System;

// Geometria da DysonWall (spec dyson-wall.md, 2.1): só dados e matemática, sem nó (como o HexPhaseLayout).
// Uma faixa de triângulos alternados (ponta para cima e para baixo) com a altura da espessura da parede. Cada
// triângulo (a "peça") é dividido em 3^nível sub-triângulos de Sierpinski; o vão central de cada divisão fica vazio.
// Coordenadas locais da parede: eixo X ao longo dela, com o pivô no centro; Y de -espessura/2 a +espessura/2.
// Os dados ficam em arrays, peça a peça: é o formato que a W2 (malha + shader) vai precisar (decisão A1).
public class DysonWallGeometry
{
    public const int MaxLevel = 3; // Nível 3 = 27 sub-triângulos por peça

    public int Level { get; }       // Nível de Sierpinski usado (0 a MaxLevel)
    public int TileCount { get; }   // Peças (triângulos externos), contando os meios-triângulos das pontas
    public int SubsPerTile { get; } // 3^nível
    public int SubCount => TileCount * SubsPerTile;
    public float TileSide { get; }                   // Lado de uma peça inteira (px)
    public float SubSide => TileSide / (1 << Level); // Lado de um sub-triângulo (px): cada nível divide o lado ao meio

    // Peças: 3 vértices por peça, centro, sorteio fixo (0 a 1) e paridade (alterna 0 e 1)
    public Vector2[] TileVertices { get; }
    public Vector2[] TileCenters { get; }
    public float[] TileRandoms { get; }
    public int[] TileParities { get; }

    // Sub-triângulos: 3 vértices cada, centro e sorteio. Os da peça t vão de t × SubsPerTile a (t + 1) × SubsPerTile - 1
    public Vector2[] SubVertices { get; }
    public Vector2[] SubCenters { get; }
    public float[] SubRandoms { get; }

    private readonly Random _random;
    private readonly float _halfLength; // Metade do comprimento: o que passa das pontas é cortado (ClipToWall)
    private int _nextSub;               // Próximo sub-triângulo a preencher (só durante a montagem)

    public DysonWallGeometry(float length, float thickness, int level, int seed)
    {
        Level = Mathf.Clamp(level, 0, MaxLevel);
        SubsPerTile = PowerOfThree(Level);

        // Uma peça a cada meio lado do triângulo equilátero com a altura da parede (h / √3), arredondado para a
        // faixa caber exata no comprimento. As peças das duas pontas ficam pela metade (cortadas na borda).
        bool hasSize = length > 0f && thickness > 0f;
        int halfSteps = hasSize ? Math.Max(1, Mathf.RoundToInt(length / (thickness / Mathf.Sqrt(3f)))) : 0;
        TileCount = hasSize ? halfSteps + 1 : 0;

        TileVertices = new Vector2[TileCount * 3];
        TileCenters = new Vector2[TileCount];
        TileRandoms = new float[TileCount];
        TileParities = new int[TileCount];
        SubVertices = new Vector2[SubCount * 3];
        SubCenters = new Vector2[SubCount];
        SubRandoms = new float[SubCount];

        if (TileCount == 0) return; // Comprimento ou espessura zero: parede sem peças

        _random = new Random(seed);
        _halfLength = length / 2f;
        float halfSide = length / halfSteps;
        TileSide = halfSide * 2f;
        for (int i = 0; i < TileCount; i++)
        {
            BuildTile(i, -length / 2f + i * halfSide, halfSide, thickness / 2f);
        }
    }

    // Peça i com o ápice em apexX: as pares apontam para cima (base embaixo), as ímpares para baixo.
    // Toda peça é montada e dividida inteira; só depois o que passa das pontas é cortado (ClipToWall).
    private void BuildTile(int i, float apexX, float halfSide, float halfThickness)
    {
        bool pointsUp = i % 2 == 0;
        float baseY = pointsUp ? halfThickness : -halfThickness;
        var a = new Vector2(apexX - halfSide, baseY);
        var b = new Vector2(apexX + halfSide, baseY);
        var c = new Vector2(apexX, -baseY);

        Vector2 clippedA = ClipToWall(a), clippedB = ClipToWall(b), clippedC = ClipToWall(c);
        TileVertices[i * 3] = clippedA;
        TileVertices[i * 3 + 1] = clippedB;
        TileVertices[i * 3 + 2] = clippedC;
        TileCenters[i] = (clippedA + clippedB + clippedC) / 3f;
        TileRandoms[i] = _random.NextSingle();
        TileParities[i] = i % 2;

        Subdivide(a, b, c, Level);
    }

    // Sierpinski: divide o triângulo pelos pontos médios dos lados e fica só com os 3 dos cantos (o do meio é o vão)
    private void Subdivide(Vector2 a, Vector2 b, Vector2 c, int depth)
    {
        if (depth == 0)
        {
            AddSub(a, b, c);
            return;
        }

        Vector2 ab = (a + b) / 2f, bc = (b + c) / 2f, ca = (c + a) / 2f;
        Subdivide(a, ab, ca, depth - 1);
        Subdivide(ab, b, bc, depth - 1);
        Subdivide(ca, bc, c, depth - 1);
    }

    private void AddSub(Vector2 a, Vector2 b, Vector2 c)
    {
        int s = _nextSub++;
        a = ClipToWall(a);
        b = ClipToWall(b);
        c = ClipToWall(c);
        SubVertices[s * 3] = a;
        SubVertices[s * 3 + 1] = b;
        SubVertices[s * 3 + 2] = c;
        SubCenters[s] = (a + b + c) / 3f;
        SubRandoms[s] = _random.NextSingle();
    }

    // Corta o que passa das pontas da parede, levando cada vértice de fora até a borda. A borda passa pelo ápice das
    // peças das pontas, que é o eixo de simetria delas: no Sierpinski, só o sub-triângulo do ápice cruza esse eixo, e
    // ele é simétrico, então levar o vértice de fora até a borda é o mesmo que cortá-lo ao meio. Os sub-triângulos
    // inteiros do lado de fora viram triângulos achatados (área zero, não aparecem), e toda peça mantém 3^nível.
    private Vector2 ClipToWall(Vector2 point) => new(Mathf.Clamp(point.X, -_halfLength, _halfLength), point.Y);

    private static int PowerOfThree(int exponent)
    {
        int result = 1;
        for (int i = 0; i < exponent; i++) result *= 3;
        return result;
    }
}
