using Godot;
using System.Collections.Generic;
using System.Linq;

// Spawner móvel (spec mobile-spawners.md): anda pelas rotas da fase em passeio aleatório, pelo eixo dos corredores.
// Corpo sólido movido por código (AnimatableBody2D, camada 2, máscara 1): empurra o jogador do caminho antes de andar;
// os inimigos o atravessam. A luz o fere (ILightDamageable) e, iluminado, ele foge em pânico; encostar fere o traje
// (IContactHazard). Perto do jogador, gera inimigos do estoque da fase (EnemyPool).
// Quem cria, põe na rota e dá o ritmo (intervalo entre inimigos e velocidade) é o SpawnerDirector; o spawner só executa.
public partial class MobileSpawner : AnimatableBody2D, ILightDamageable, IContactHazard
{
    [Signal] public delegate void DestroyedEventHandler(MobileSpawner spawner); // Morreu (o diretor recalcula o ritmo)

    [Export] public float MoveSpeed = 150f; // Velocidade pela rota (px/s), antes do ritmo do diretor
    [Export] public float MaxHealth = 300f; // Vida (com a lanterna atual, 35/s: ~8,6 s de luz contínua)

    [ExportGroup("Geração de inimigos")]
    [Export] public float ActivationRadius = 2000f; // Só gera com o jogador a menos disto (px); longe, só anda
    [Export] public float SpawnScatter = 40f;       // Raio em que os inimigos nascem (px)

    [ExportGroup("Pânico (iluminado)")]
    [Export] public float PanicSpeedMultiplier = 2f; // Velocidade em pânico = MoveSpeed × isto
    [Export] public float PanicDuration = 2f;        // Quanto o pânico dura depois de sair da luz (s); 0 = não entra em pânico

    [ExportGroup("Biomassa na morte")]
    [Export] public int DeathBiomass = 20;           // Orbes de 1 soltos na morte (um ancestral do inimigo rende 4)
    [Export] public float BiomassDropScatter = 60f;  // Raio em que os orbes se espalham (px)
    [Export] public float BiomassOrbLifetime = 30f;  // Como no inimigo
    [Export] public int MaxBiomassOrbs = 300;        // Como no inimigo (o BiomassField usa o último valor informado)

    [ExportGroup("Visual provisório (arte na etapa 2)")]
    [Export] public Color BodyColor = new Color(0.85f, 0.25f, 0.65f);
    [Export] public Color PanicColor = new Color(1f, 0.9f, 0.3f); // Corpo em pânico: mostra que a luz o atingiu
    [Export] public Color RingColor = new Color(1f, 0.55f, 0.85f);
    [Export] public float PulseFrequency = 1f; // Pulsos do anel por segundo (Hz)
    [Export] public float PulseAmplitude = 16f; // Quanto o anel se afasta do corpo no auge do pulso (px)

    private const float DefaultRadius = 40f; // Se a cena não tiver um CircleShape2D (o raio de verdade vem da colisão)
    private const float RingWidth = 3f;
    private const int RingSegments = 48;

    public bool IsDead { get; private set; } = false; // Morto não fere nem toma dano (IContactHazard)
    public EnemyPool Pool { get; set; }               // Estoque da fase, dado pelo diretor (vazio = não gera)

    private Node2D _phase;     // Dono das coordenadas das rotas (a raiz da fase)
    private Node2D _player;    // De quem foge
    private HexRoute _route;   // Rota atual
    private HexWaypoint _from; // Ponta de onde veio
    private float _distance;   // Quanto já andou na rota, a partir de _from (px)
    private float _health;
    private float _panicTimer; // > 0 = em pânico
    private float _spawnInterval = 1f;   // Ritmo dado pelo diretor (SetPace)
    private float _speedMultiplier = 1f;
    private float _spawnTimer;           // Começa em 0: gera assim que o jogador chega perto
    private float _radius;
    private float _time;

    private bool IsPanicking => _panicTimer > 0f;

    // O desenho usa o raio da colisão: um valor só, ajustado na cena
    public override void _Ready()
    {
        var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape as CircleShape2D;
        _radius = shape?.Radius ?? DefaultRadius;
        _health = MaxHealth;
        _player = GetTree().GetFirstNodeInGroup("Player") as Node2D;
    }

    // Põe o spawner numa rota (o diretor chama antes de pô-lo na árvore). `distance`: quanto já andou desde `from` (px)
    public void FollowRoute(Node2D phase, HexRoute route, HexWaypoint from, float distance)
    {
        _phase = phase;
        _route = route;
        _from = from;
        _distance = distance;
    }

    // Ritmo dado pelo diretor (curva de dificuldade): intervalo entre inimigos e multiplicador da velocidade
    public void SetPace(float spawnInterval, float speedMultiplier)
    {
        _spawnInterval = spawnInterval;
        _speedMultiplier = speedMultiplier;
        _spawnTimer = Mathf.Min(_spawnTimer, spawnInterval); // Ficou mais rápido: não espera o intervalo antigo
    }

    // Chamado pela luz a cada passo em que ele está no feixe (ILightDamageable)
    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        _health -= amount;
        _panicTimer = PanicDuration; // Iluminado: em pânico até PanicDuration depois de sair da luz
        if (_health <= 0f) Die();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsDead) return;

        float dt = (float)delta;
        UpdatePanic(dt);
        UpdatePulse(dt);
        UpdateSpawning(dt);
        if (_route == null) return; // Sem rota (ex.: posto à mão numa cena de teste): fica parado

        FleeIfPlayerAhead();
        float step = CurrentSpeed() * dt;
        if (!PushBlocker(NextPosition(step) - GlobalPosition)) return; // Jogador encurralado no caminho: espera
        Advance(step);
    }

    // Onde o spawner estaria depois de andar `step` px na rota atual (sem passar da ponta)
    private Vector2 NextPosition(float step) => _phase.ToGlobal(_route.PointAt(_from, Mathf.Min(_distance + step, _route.Length)));

    // Empurra quem estiver no caminho (o jogador) antes de andar, para nunca entrar no corpo dele: um corpo movido por
    // código que entra no jogador o "cola" (o MoveAndSlide do jogador corta a saída). Só a parte do movimento que vai
    // contra o corpo empurra (de raspão, quase nada). Devolve false se ele não pôde sair (encurralado): o spawner espera.
    private bool PushBlocker(Vector2 motion)
    {
        var hit = new KinematicCollision2D();
        if (!TestMove(GlobalTransform, motion, hit)) return true;       // Caminho livre
        if (hit.GetCollider() is not CharacterBody2D body) return true; // Parede ou outro: segue (as rotas não tocam paredes)

        Vector2 normal = hit.GetNormal();                                // Do corpo atingido para o spawner
        float into = hit.GetRemainder().Dot(-normal);                    // Quanto do movimento vai contra o corpo
        if (into <= 0f) return true;
        return body.MoveAndCollide(-normal * into) == null;              // Barrado (encurralado): o spawner não anda
    }

    private void UpdatePanic(float dt)
    {
        if (_panicTimer <= 0f) return;
        _panicTimer -= dt;
    }

    private float CurrentSpeed() => MoveSpeed * _speedMultiplier * (IsPanicking ? PanicSpeedMultiplier : 1f);

    // Com o jogador perto, gera um inimigo do estoque a cada _spawnInterval; longe, o relógio para
    private void UpdateSpawning(float dt)
    {
        if (Pool == null || _player == null) return;
        if (GlobalPosition.DistanceSquaredTo(_player.GlobalPosition) > ActivationRadius * ActivationRadius) return;

        _spawnTimer -= dt;
        if (_spawnTimer > 0f) return;

        _spawnTimer += _spawnInterval; // Soma (não recomeça): mantém o ritmo exato, sem perder a sobra do passo
        Vector2 scatter = Vector2.Right.Rotated(GD.Randf() * Mathf.Tau) * GD.Randf() * SpawnScatter;
        Pool.TrySpawn(GlobalPosition + scatter); // Estoque vazio ou fase no teto: tenta de novo no próximo intervalo
    }

    // Em pânico, com o jogador à frente na rota, dá meia-volta (passa a contar a partir da outra ponta)
    private void FleeIfPlayerAhead()
    {
        if (!IsPanicking || _player == null) return;

        Vector2 ahead = _route.Other(_from).Position - _from.Position;
        Vector2 toPlayer = _phase.ToLocal(_player.GlobalPosition) - _route.PointAt(_from, _distance);
        if (ahead.Dot(toPlayer) <= 0f) return; // Jogador atrás (ou ao lado): segue em frente

        _from = _route.Other(_from);
        _distance = _route.Length - _distance;
    }

    // Anda `step` px; ao passar da ponta da rota, segue pela próxima sem perder o que sobrou do passo
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

    // Uma das rotas que saem do ponto, menos a de onde veio (todo ponto da fase tem 3 ou mais):
    // em pânico, a que mais o afasta do jogador; calmo, sorteada (passeio aleatório)
    private HexRoute ChooseNextRoute(HexWaypoint at)
    {
        List<HexRoute> options = at.Routes.Where(route => route != _route).ToList();
        if (options.Count == 0) return _route; // Beco sem saída: volta pela mesma rota
        if (IsPanicking && _player != null) return FarthestFromPlayer(options, at);
        return options[GD.RandRange(0, options.Count - 1)];
    }

    // A rota cuja outra ponta fica mais longe do jogador (em linha reta)
    private HexRoute FarthestFromPlayer(List<HexRoute> options, HexWaypoint at)
    {
        Vector2 player = _phase.ToLocal(_player.GlobalPosition);
        return options.OrderByDescending(route => route.Other(at).Position.DistanceSquaredTo(player)).First();
    }

    // Vida zerada: solta a biomassa e sai da fase
    private void Die()
    {
        IsDead = true;
        DropBiomass();
        EmitSignal(SignalName.Destroyed, this);
        QueueFree();
    }

    // DeathBiomass orbes de 1, espalhados em volta (os orbes nascem no pai, que fica parado)
    private void DropBiomass()
    {
        for (int i = 0; i < DeathBiomass; i++)
        {
            Vector2 scatter = Vector2.Right.Rotated(GD.Randf() * Mathf.Tau) * GD.Randf() * BiomassDropScatter;
            BiomassField.Drop(GetParent(), Position + scatter, 1, BiomassOrbLifetime, MaxBiomassOrbs);
        }
    }

    private void UpdatePulse(float dt)
    {
        _time += dt;
        QueueRedraw();
    }

    // Silhueta provisória: corpo cheio (outra cor em pânico) e um anel que pulsa (diferente da árvore do inimigo)
    public override void _Draw()
    {
        float pulse = (Mathf.Sin(_time * PulseFrequency * Mathf.Tau) + 1f) / 2f; // De 0 a 1
        DrawCircle(Vector2.Zero, _radius, IsPanicking ? PanicColor : BodyColor);
        DrawArc(Vector2.Zero, _radius + pulse * PulseAmplitude, 0f, Mathf.Tau, RingSegments, RingColor, RingWidth);
    }
}
