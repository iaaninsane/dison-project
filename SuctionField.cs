using Godot;
using System.Collections.Generic;

// Campo de sucção das fendas abertas (spec dyson-wall.md, 2.6): registro estático, como o FractalSwarm e o BiomassField.
// Cada parede registra a sua fenda enquanto a abertura dura e a tira ao terminar ou ao sair da árvore (a lista é
// estática e sobreviveria à troca de cena). Quem é sugado (jogador, inimigos, orbes, spawners) pergunta "qual a
// aceleração aqui?": a parede não conhece nenhum deles, e eles não conhecem as paredes.
public static class SuctionField
{
    private static readonly List<SuctionBreach> _breaches = new();

    public static int Count => _breaches.Count;

    public static void Register(SuctionBreach breach)
    {
        if (!_breaches.Contains(breach)) _breaches.Add(breach);
    }

    public static void Unregister(SuctionBreach breach) => _breaches.Remove(breach);

    // Soma da sucção de todas as fendas neste ponto (px/s²). Várias fendas abertas: as acelerações se somam (seção 5).
    public static Vector2 AccelerationAt(Vector2 point)
    {
        Vector2 total = Vector2.Zero;
        foreach (SuctionBreach breach in _breaches) total += breach.AccelerationAt(point);
        return total;
    }
}

// Como uma fenda puxa (spec 2.6 e seção 4), dado pela parede a cada abertura
public readonly record struct SuctionSettings(
    float NoReturnRadius,         // Distância em que a sucção empata com o empuxo máximo do traje (px)
    float AccelerationAtNoReturn, // A sucção nessa distância (px/s²): o ThrustForce do jogador
    float FalloffExponent,        // Queda com a distância (2: arrasto ~ 1/r²)
    float MaxAcceleration,        // Teto perto da fenda (px/s²)
    float MaxRadius,              // Além disto, força zero (px)
    float GustMultiplier,         // Rajada no rompimento: multiplicador inicial
    float GustDecay);             // Constante de tempo da queda da rajada até 1× (s)

// Uma fenda aberta, vista pelo campo: onde está, para onde é "fora" e com que força puxa neste passo
public class SuctionBreach
{
    public Vector2 Center { get; }  // Centro da fenda (global), no meio da espessura da parede: a "garganta"
    public Vector2 Outward { get; } // Normal da parede para o espaço (global, unitária)
    public float Strength { get; private set; } // Intensidade da abertura × rajada, neste passo (0 = não puxa)

    private readonly SuctionSettings _settings;

    public SuctionBreach(Vector2 center, Vector2 outward, SuctionSettings settings)
    {
        Center = center;
        Outward = outward;
        _settings = settings;
    }

    // A parede atualiza a cada passo: intensidade k da abertura e o tempo desde o rompimento (início da fase 1)
    public void Update(float intensity, float timeSinceRupture) => Strength = intensity * GustAt(timeSinceRupture);

    // a(r) = AccelerationAtNoReturn × (NoReturnRadius / r)^FalloffExponent, com teto e raio máximo, × Strength.
    // Do lado da câmara, para o centro da fenda; do meio da espessura em diante, para o espaço, pela normal (decisão B5).
    // Trocar só depois da face de fora criava um poço no vão: entre o meio e a face, o campo (no máximo, perto do
    // centro) puxava de volta para a câmara quem já estava passando.
    public Vector2 AccelerationAt(Vector2 point)
    {
        if (Strength <= 0f) return Vector2.Zero;

        Vector2 offset = point - Center;
        float distance = offset.Length();
        if (distance >= _settings.MaxRadius) return Vector2.Zero;

        float magnitude = distance > 0f
            ? Mathf.Min(_settings.MaxAcceleration, _settings.AccelerationAtNoReturn * Mathf.Pow(_settings.NoReturnRadius / distance, _settings.FalloffExponent))
            : _settings.MaxAcceleration;
        bool pastThroat = offset.Dot(Outward) >= 0f;
        Vector2 direction = pastThroat ? Outward : -offset / distance;
        return direction * magnitude * Strength;
    }

    // Rajada: começa em GustMultiplier quando a fenda rompe e cai até 1 com a constante de tempo GustDecay
    private float GustAt(float timeSinceRupture)
    {
        if (timeSinceRupture <= 0f) return _settings.GustMultiplier;
        return 1f + (_settings.GustMultiplier - 1f) * Mathf.Exp(-timeSinceRupture / _settings.GustDecay);
    }
}
