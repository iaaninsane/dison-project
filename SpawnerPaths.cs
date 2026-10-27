using Godot;
using System.Collections.Generic;

// Trajetos dos spawners móveis (spec mobile-spawners.md, S10): quanto o spawner se afasta do eixo do corredor em cada
// ponto da rota, para passar perto das paredes (e das fendas da parede de fronteira). Classes C# comuns, sem nó.
// Strategy: o MobileSpawner só conhece o contrato ISpawnerPath; a forma é escolhida no Inspector (PathShape).
// O resultado vai de -1 a 1 (um lado e o outro do eixo): o spawner o multiplica pelo espaço do corredor e o leva a zero
// nas pontas da rota. É uma função do ponto da rota (contado a partir da ponta A), não do tempo: na meia-volta do
// pânico, o spawner refaz o mesmo desenho ao contrário.

// Formas para comparar no Inspector (o dev decide depois se ficam as três ou uma)
public enum SpawnerPathShape { Sine, WallHug, Noise }

public interface ISpawnerPath
{
    // Afastamento do eixo, de -1 a 1, a `fromA` px da ponta A da rota
    float LateralAt(HexRoute route, float fromA);
}

// Senoide variada: comprimento de onda e fase sorteados para cada spawner (dois spawners não fazem a mesma curva)
public class SinePath : ISpawnerPath
{
    private readonly float _wavelength;
    private readonly float _phase;

    public SinePath(RandomNumberGenerator random, float minWavelength, float maxWavelength)
    {
        _wavelength = random.RandfRange(minWavelength, maxWavelength);
        _phase = random.Randf() * Mathf.Tau;
    }

    public float LateralAt(HexRoute route, float fromA) => Mathf.Sin(fromA / _wavelength * Mathf.Tau + _phase);
}

// Rente à parede: fica colado num lado e troca para o outro com uma curva em S, em distâncias sorteadas. Os pontos de
// troca de cada rota são sorteados na primeira passagem (com a semente do spawner) e guardados: a mesma rota dá sempre
// o mesmo desenho, nos dois sentidos.
public class WallHugPath : ISpawnerPath
{
    private readonly ulong _seed;
    private readonly float _minRun, _maxRun, _switchLength;
    private readonly Dictionary<HexRoute, (List<float> Switches, float FirstSide)> _plans = new();

    public WallHugPath(RandomNumberGenerator random, float minRun, float maxRun, float switchLength)
    {
        _seed = random.Randi();
        _minRun = minRun;
        _maxRun = maxRun;
        _switchLength = switchLength;
    }

    public float LateralAt(HexRoute route, float fromA)
    {
        var (switches, side) = PlanFor(route);
        foreach (float start in switches)
        {
            if (fromA <= start) return side;
            if (fromA < start + _switchLength) return Mathf.Lerp(side, -side, Mathf.SmoothStep(start, start + _switchLength, fromA));
            side = -side;
        }
        return side;
    }

    // Onde começa cada troca de lado nesta rota, e de que lado ela começa
    private (List<float>, float) PlanFor(HexRoute route)
    {
        if (_plans.TryGetValue(route, out var plan)) return plan;

        var random = new RandomNumberGenerator { Seed = _seed ^ (ulong)(uint)route.GetHashCode() };
        var switches = new List<float>();
        for (float at = random.RandfRange(_minRun, _maxRun); at < route.Length; at += _switchLength + random.RandfRange(_minRun, _maxRun))
        {
            switches.Add(at);
        }
        plan = (switches, random.Randf() < 0.5f ? -1f : 1f);
        _plans[route] = plan;
        return plan;
    }
}

// Ruído orgânico: deriva lateral suave, sem padrão (FastNoiseLite, o ruído que a Godot já traz). Cada rota lê uma
// faixa diferente do ruído; cada spawner tem a sua semente.
public class NoisePath : ISpawnerPath
{
    private const float Gain = 2.5f;          // O Perlin da Godot quase nunca passa de ±0,5: o ganho o leva perto das paredes (o limite corta)
    private const float RouteSpacing = 10000f; // Distância entre as faixas do ruído lidas por rotas diferentes
    private readonly FastNoiseLite _noise;

    public NoisePath(RandomNumberGenerator random, float wavelength)
    {
        _noise = new FastNoiseLite
        {
            Seed = (int)random.Randi(),
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            FractalType = FastNoiseLite.FractalTypeEnum.None,
            Frequency = 1f / wavelength,
        };
    }

    public float LateralAt(HexRoute route, float fromA) =>
        Mathf.Clamp(_noise.GetNoise2D(fromA, (route.GetHashCode() % 997) * RouteSpacing) * Gain, -1f, 1f);
}
