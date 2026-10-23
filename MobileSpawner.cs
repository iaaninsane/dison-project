using Godot;
using System.Collections.Generic;
using System.Linq;

// Spawner móvel (spec mobile-spawners.md): anda pelas rotas da fase em passeio aleatório, pelo eixo dos corredores.
// Corpo sólido movido por código (AnimatableBody2D, camada 2): o jogador bate nele e é empurrado; os inimigos o
// atravessam. Quem cria e põe na rota é o SpawnerDirector; o spawner só executa.
public partial class MobileSpawner : AnimatableBody2D
{
    [Export] public float MoveSpeed = 150f; // Velocidade pela rota (px/s)

    [ExportGroup("Visual provisório (arte na etapa 2)")]
    [Export] public Color BodyColor = new Color(0.85f, 0.25f, 0.65f);
    [Export] public Color RingColor = new Color(1f, 0.55f, 0.85f);
    [Export] public float PulseFrequency = 1f; // Pulsos do anel por segundo (Hz)
    [Export] public float PulseAmplitude = 16f; // Quanto o anel se afasta do corpo no auge do pulso (px)

    private const float DefaultRadius = 40f; // Se a cena não tiver um CircleShape2D (o raio de verdade vem da colisão)
    private const float RingWidth = 3f;
    private const int RingSegments = 48;

    private Node2D _phase;     // Dono das coordenadas das rotas (a raiz da fase)
    private HexRoute _route;   // Rota atual
    private HexWaypoint _from; // Ponta de onde veio
    private float _distance;   // Quanto já andou na rota, a partir de _from (px)
    private float _radius;
    private float _time;

    // O desenho usa o raio da colisão: um valor só, ajustado na cena
    public override void _Ready()
    {
        var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape as CircleShape2D;
        _radius = shape?.Radius ?? DefaultRadius;
    }

    // Põe o spawner numa rota (o diretor chama antes de pô-lo na árvore). `distance`: quanto já andou desde `from` (px)
    public void FollowRoute(Node2D phase, HexRoute route, HexWaypoint from, float distance)
    {
        _phase = phase;
        _route = route;
        _from = from;
        _distance = distance;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_route == null) return; // Sem rota (ex.: posto à mão numa cena de teste): fica parado

        float dt = (float)delta;
        Advance(MoveSpeed * dt);
        UpdatePulse(dt);
    }

    // Anda `step` px; ao passar da ponta da rota, segue pela próxima (sorteada) sem perder o que sobrou do passo
    private void Advance(float step)
    {
        _distance += step;
        while (_distance >= _route.Length)
        {
            _distance -= _route.Length;
            HexWaypoint arrived = _route.Other(_from);
            _route = ChooseNextRoute(arrived);
            _from = arrived;
        }
        GlobalPosition = _phase.ToGlobal(_route.PointAt(_from, _distance));
    }

    // Passeio aleatório: uma das rotas que saem do ponto, menos a de onde veio (todo ponto da fase tem 3 ou mais)
    private HexRoute ChooseNextRoute(HexWaypoint at)
    {
        List<HexRoute> options = at.Routes.Where(route => route != _route).ToList();
        if (options.Count == 0) return _route; // Beco sem saída: volta pela mesma rota
        return options[GD.RandRange(0, options.Count - 1)];
    }

    private void UpdatePulse(float dt)
    {
        _time += dt;
        QueueRedraw();
    }

    // Silhueta provisória: corpo cheio e um anel que pulsa (diferente da árvore do inimigo)
    public override void _Draw()
    {
        float pulse = (Mathf.Sin(_time * PulseFrequency * Mathf.Tau) + 1f) / 2f; // De 0 a 1
        DrawCircle(Vector2.Zero, _radius, BodyColor);
        DrawArc(Vector2.Zero, _radius + pulse * PulseAmplitude, 0f, Mathf.Tau, RingSegments, RingColor, RingWidth);
    }
}
