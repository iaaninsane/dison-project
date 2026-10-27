using Godot;

// Esmaecer (spec dyson-wall.md, 2.5), o modo de abertura da demo: só redução de escala, sem transparência.
// Cada sub-triângulo a menos de FadeRadius do centro encolhe até sumir e depois volta, com um atraso pela distância
// (cascata do centro para fora) e um pouco de sorte; fora desse raio, nada muda. O triângulo externo não se move:
// a membrana encolhe com a média das escalas dos seus sub-triângulos. Classe C# comum (sem nó).
public class DysonWallFade
{
    private const float CascadeDelay = 0.45f; // Atraso de cada OpenRadius de distância do centro (s)
    private const float RandomDelay = 0.06f;  // Atraso extra máximo, pelo sorteio de cada figura (s)

    public float FadeRadius { get; } // As figuras com o centro mais perto que isto do centro da abertura somem (px)

    // A figura mais atrasada volta este tempo depois do fim das 4 fases: só então a abertura termina
    public float MaxDelay => FadeRadius / _openRadius * CascadeDelay + RandomDelay;

    private readonly float _openRadius;

    public DysonWallFade(float openRadius, float fadeRadius)
    {
        _openRadius = openRadius;
        FadeRadius = fadeRadius;
    }

    // O ponto x (no eixo da parede) está na área que some?
    public bool Affects(DysonWallOpening opening, float x) => Mathf.Abs(x - opening.CenterX) < FadeRadius;

    // Escala de cada sub-triângulo e de cada membrana neste instante da abertura (1 = inteira, 0 = sumiu)
    public void Apply(DysonWallOpening opening, DysonWallGeometry geometry, float[] subScales, float[] membraneScales)
    {
        for (int s = 0; s < geometry.SubCount; s++)
        {
            float distance = Mathf.Abs(geometry.SubCenters[s].X - opening.CenterX);
            if (distance >= FadeRadius)
            {
                subScales[s] = 1f;
                continue;
            }
            float delay = distance / _openRadius * CascadeDelay + geometry.SubRandoms[s] * RandomDelay;
            subScales[s] = 1f - opening.IntensityAt(opening.Time - delay);
        }

        for (int t = 0; t < geometry.TileCount; t++)
        {
            float sum = 0f;
            int firstSub = t * geometry.SubsPerTile;
            for (int s = firstSub; s < firstSub + geometry.SubsPerTile; s++) sum += subScales[s];
            membraneScales[t] = sum / geometry.SubsPerTile;
        }
    }
}
