using Godot;

// Relógio de uma abertura da DysonWall (spec dyson-wall.md, 2.3): onde ela está, em qual das 4 fases e com que
// intensidade. Classe C# comum (sem nó), a parte comum a todos os modos de abertura; como as peças somem é do modo
// (hoje, só o Esmaecer: DysonWallFade).
public class DysonWallOpening
{
    public float CenterX { get; }           // Centro da abertura no eixo X local da parede (a peça que chegou a 1)
    public float Time { get; private set; } // Tempo desde o início (s)
    public float TotalDuration { get; }     // Soma das 4 fases (s)

    private readonly float[] _durations;    // Aviso, abrir, aberta, fechar (s)
    private readonly float _openRadius;     // Raio do núcleo da abertura (px)
    private readonly float _warningTremor;  // Tremor extra no aviso, no centro da abertura (px)

    public DysonWallOpening(float centerX, float warningDuration, float openingDuration, float openDuration,
        float closingDuration, float openRadius, float warningTremor)
    {
        CenterX = centerX;
        _durations = new[] { warningDuration, openingDuration, openDuration, closingDuration };
        TotalDuration = warningDuration + openingDuration + openDuration + closingDuration;
        _openRadius = openRadius;
        _warningTremor = warningTremor;
    }

    public void Advance(float dt) => Time += dt;

    public float Intensity => IntensityAt(Time);

    // Fase num instante (0 aviso, 1 abrir, 2 aberta, 3 fechar, 4 terminou) e quanto dela já passou, de 0 a 1 (phaseAt)
    public (int Phase, float Progress) PhaseAt(float time)
    {
        for (int phase = 0; phase < _durations.Length; phase++)
        {
            if (time < _durations[phase]) return (phase, time / _durations[phase]);
            time -= _durations[phase];
        }
        return (_durations.Length, 1f);
    }

    // Intensidade k da abertura num instante, de 0 a 1 (amtAt): 0 no aviso, sobe na fase 1, 1 com a fenda aberta e
    // desce na fase 3. Curva suave, a do Esmaecer (os outros modos, na fase C, têm a sua)
    public float IntensityAt(float time)
    {
        if (time < 0f) return 0f; // Figura que ainda não começou (atraso da cascata)

        var (phase, progress) = PhaseAt(time);
        return phase switch
        {
            1 => Smooth(progress),
            2 => 1f,
            3 => 1f - Smooth(progress),
            _ => 0f, // Aviso ou terminada
        };
    }

    // Tremor extra de uma peça no aviso (fase 0, "tremor forte nas peças da área"): o maior no centro, zero a OpenRadius
    public float WarningTremorAt(float x)
    {
        if (PhaseAt(Time).Phase != 0) return 0f;
        return Mathf.Max(0f, 1f - Mathf.Abs(x - CenterX) / _openRadius) * _warningTremor;
    }

    // Curva suave (smooth do protótipo): começa e termina devagar
    private static float Smooth(float u) => u < 0.5f ? 2f * u * u : 1f - Mathf.Pow(-2f * u + 2f, 2f) / 2f;
}
