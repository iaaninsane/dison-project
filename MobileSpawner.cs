using Godot;
using System.Collections.Generic;
using System.Linq;

// Spawner móvel (spec mobile-spawners.md): anda pelas rotas da fase em passeio aleatório, pelos corredores, num trajeto
// que passa perto das paredes (S10).
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

    [ExportGroup("Trajeto (spec mobile-spawners.md, S10; formas para comparar)")]
    [Export] public SpawnerPathShape PathShape = SpawnerPathShape.Sine; // Senoide variada, rente à parede ou ruído orgânico
    [Export] public float PathWallClearance = 10f;    // Folga entre o corpo e a parede no ponto mais afastado do eixo (px)
    [Export] public float PathEndRamp = 600f;         // Perto das pontas da rota (centros de nós e painéis), o afastamento vai a zero ao longo disto (px)
    [Export] public float SineMinWavelength = 900f;   // Senoide: comprimento de onda sorteado entre estes dois (px)
    [Export] public float SineMaxWavelength = 1600f;
    [Export] public float WallHugMinRun = 600f;       // Rente à parede: quanto anda num lado antes de trocar, sorteado entre estes dois (px)
    [Export] public float WallHugMaxRun = 1500f;
    [Export] public float WallHugSwitchLength = 400f; // Rente à parede: comprimento da curva em S da troca de lado (px)
    [Export] public float NoiseWavelength = 1200f;    // Ruído: tamanho típico das ondulações (px)

    [ExportGroup("Sucção das fendas (spec dyson-wall.md, B3; mobile-spawners.md, S11)")]
    [Export] public float SuctionFactor = 0.5f;      // Quanto sente a sucção: menos que o jogador e os inimigos (SpawnerSuctionFactor da spec)
    [Export] public float RouteReturnTime = 1.5f;    // Fora do campo, a mola o assenta de novo no trajeto em ~este tempo (s)
    [Export] public float RouteReturnDamping = 0.7f; // Amortecimento da mola: 1 = sem balanço; 0,7 = passa um pouco do trajeto e acomoda

    [ExportGroup("Visual provisório (arte na etapa 2)")]
    [Export] public Color BodyColor = new Color(0.85f, 0.25f, 0.65f);
    [Export] public Color PanicColor = new Color(1f, 0.9f, 0.3f); // Corpo em pânico: mostra que a luz o atingiu
    [Export] public Color RingColor = new Color(1f, 0.55f, 0.85f);
    [Export] public float PulseFrequency = 1f; // Pulsos do anel por segundo (Hz)
    [Export] public float PulseAmplitude = 16f; // Quanto o anel se afasta do corpo no auge do pulso (px)

    private const float DefaultRadius = 40f; // Se a cena não tiver um CircleShape2D (o raio de verdade vem da colisão)
    private const float SettledSq = 0.01f;   // Desvio e velocidade abaixo de 0,1 (ao quadrado): assentou no trajeto
    private const float InsideWallDepth = 2f; // Sobreposto a uma parede por mais que isto (px): preso dentro dela
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
    private ISpawnerPath _path;       // A forma do trajeto (Strategy), com os sorteios deste spawner
    private Vector2 _suctionOffset;   // Desvio do trajeto causado pela sucção (px, global)
    private Vector2 _suctionVelocity; // Velocidade do desvio: a sucção aumenta, a mola traz de volta
    private CircleShape2D _insideWallProbe; // Círculo um pouco menor que o corpo, para saber se ele está preso numa parede

    private bool IsPanicking => _panicTimer > 0f;

    // Onde ele está no trajeto agora (global): o diretor o põe aqui antes de pô-lo na árvore
    public Vector2 PathPosition => PathPoint(_route, _from, _distance);

    public override void _Ready()
    {
        _radius = ReadRadius();
        _path ??= CreatePath();
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
        _radius = ReadRadius(); // Antes do _Ready: o trajeto precisa do raio para saber o espaço do corredor
        _path ??= CreatePath();
    }

    // O raio da colisão: um valor só, ajustado na cena (o desenho e o espaço do trajeto usam)
    private float ReadRadius() => (GetNodeOrNull<CollisionShape2D>("CollisionShape2D")?.Shape as CircleShape2D)?.Radius ?? DefaultRadius;

    // A forma do trajeto escolhida no Inspector, com sorteios próprios deste spawner
    private ISpawnerPath CreatePath()
    {
        var random = new RandomNumberGenerator();
        random.Randomize();
        return PathShape switch
        {
            SpawnerPathShape.WallHug => new WallHugPath(random, WallHugMinRun, WallHugMaxRun, WallHugSwitchLength),
            SpawnerPathShape.Noise => new NoisePath(random, NoiseWavelength),
            _ => new SinePath(random, SineMinWavelength, SineMaxWavelength),
        };
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
        UpdateSuction(dt);
        if (!TryMove(CurrentSpeed() * dt, _suctionVelocity * dt, out Vector2 position)) return; // Jogador encurralado: espera
        if (SuctionField.HasCrossed(position)) LeaveThroughBreach();
    }

    // Anda `step` px pelo trajeto e soma o desvio deste passo, numa escrita só da posição: num AnimatableBody2D com Sync
    // to Physics, a posição definida só vale no passo seguinte (lida logo depois, ainda é a antiga), e uma segunda escrita
    // no mesmo passo apagaria a primeira (S11). Antes, testa o movimento inteiro: o jogador é empurrado (encurralado, o
    // spawner espera); uma parede barra (anda só até ela). Depois, a rota acompanha o corpo: o ponto da rota passa a ser
    // a projeção dele no eixo, e o desvio fica só de lado. Segurado pela sucção, ele não fica "para trás" da rota.
    private bool TryMove(float step, Vector2 offsetStep, out Vector2 position)
    {
        position = GlobalPosition;
        var (route, from, distance) = (_route, _from, _distance);
        AdvanceRoute(step);
        Vector2 motion = PathPoint(_route, _from, _distance) + _suctionOffset + offsetStep - position;

        var hit = new KinematicCollision2D();
        if (TestMove(GlobalTransform, motion, hit))
        {
            if (hit.GetCollider() is not CharacterBody2D)
            {
                if (!IsInsideWall()) motion = StopAtWall(hit); // Preso numa fenda que fechou: a mola o tira aos poucos, sem o teste
            }
            else if (!PushBlocker(motion))
            {
                (_route, _from, _distance) = (route, from, distance); // Não andou: a rota também não
                return false;
            }
        }

        position += motion;
        _distance = Mathf.Clamp(DistanceAlongRoute(position), 0f, _route.Length);
        _suctionOffset = position - PathPoint(_route, _from, _distance); // O que sobra entre o corpo e o trajeto: o desvio de lado
        GlobalPosition = position;
        return true;
    }

    // Quanto do ponto já foi percorrido na rota: a projeção dele no eixo, a partir de _from
    private float DistanceAlongRoute(Vector2 globalPosition)
    {
        Vector2 along = (_route.Other(_from).Position - _from.Position).Normalized();
        return (_phase.ToLocal(globalPosition) - _from.Position).Dot(along);
    }

    // Preso dentro de uma parede (uma fenda fechou com ele no vão; a parede só empurra para fora corpos CharacterBody2D)?
    // Um círculo um pouco menor que o corpo ainda encosta nela. Só encostar na face não conta.
    private bool IsInsideWall()
    {
        _insideWallProbe ??= new CircleShape2D();
        _insideWallProbe.Radius = Mathf.Max(1f, _radius - InsideWallDepth);
        var query = new PhysicsShapeQueryParameters2D
        {
            Shape = _insideWallProbe,
            Transform = GlobalTransform,
            CollisionMask = CollisionMask,
            Exclude = new Godot.Collections.Array<Rid> { GetRid() },
        };
        foreach (var hit in GetWorld2D().DirectSpaceState.IntersectShape(query, 4))
        {
            if (hit["collider"].As<GodotObject>() is not CharacterBody2D) return true;
        }
        return false;
    }

    // Uma parede no caminho: anda só até ela, e a parte da velocidade do desvio que ia contra ela se perde
    private Vector2 StopAtWall(KinematicCollision2D hit)
    {
        Vector2 normal = hit.GetNormal();
        float into = _suctionVelocity.Dot(normal);
        if (into < 0f) _suctionVelocity -= normal * into;
        return hit.GetTravel();
    }

    // Ponto do trajeto a `distance` px de `from` na rota: o eixo do corredor + o afastamento da forma escolhida (global)
    private Vector2 PathPoint(HexRoute route, HexWaypoint from, float distance)
    {
        float fromA = from == route.A ? distance : route.Length - distance;
        Vector2 side = (route.B.Position - route.A.Position).Normalized().Orthogonal();
        return _phase.ToGlobal(route.PointAt(from, distance) + side * LateralOffset(route, fromA));
    }

    // Afastamento do eixo (px): a forma (-1 a 1) × o espaço do corredor (meia largura − raio − folga), indo a zero perto
    // das pontas da rota, para a troca de rota nos nós e painéis não saltar
    private float LateralOffset(HexRoute route, float fromA)
    {
        float room = Mathf.Max(0f, route.HalfWidth - _radius - PathWallClearance);
        float ends = Mathf.SmoothStep(0f, PathEndRamp, fromA) * Mathf.SmoothStep(0f, PathEndRamp, route.Length - fromA);
        return _path.LateralAt(route, fromA) * room * ends;
    }

    // Empurra quem estiver no caminho (o jogador) antes de andar, para nunca entrar no corpo dele: um corpo movido por
    // código que entra no jogador o "cola" (o MoveAndSlide do jogador corta a saída). Só a parte do movimento que vai
    // contra o corpo empurra (de raspão, quase nada). Devolve false se ele não pôde sair (encurralado): o spawner espera.
    private bool PushBlocker(Vector2 motion)
    {
        var hit = new KinematicCollision2D();
        if (!TestMove(GlobalTransform, motion, hit)) return true;       // Caminho livre
        if (hit.GetCollider() is not CharacterBody2D body) return true; // Parede ou outro: quem cuida é o TryMove

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

    // Anda `step` px na rota (só a conta, sem mexer no corpo); ao passar da ponta, segue pela próxima sem perder o que
    // sobrou do passo
    private void AdvanceRoute(float step)
    {
        _distance += step;
        while (_distance >= _route.Length)
        {
            _distance -= _route.Length;
            HexWaypoint arrived = _route.Other(_from);
            _route = ChooseNextRoute(arrived);
            _from = arrived;
        }
    }

    // Velocidade do desvio (S11): no campo, a sucção a aumenta; fora dele, uma mola amortecida puxa o desvio de volta ao
    // trajeto. Com amortecimento menor que 1, o spawner continua no embalo, curva, passa um pouco do trajeto e acomoda.
    private void UpdateSuction(float dt)
    {
        Vector2 acceleration = SuctionField.AccelerationAt(GlobalPosition) * SuctionFactor;
        if (acceleration == Vector2.Zero) acceleration = SpringAcceleration();
        _suctionVelocity += acceleration * dt;

        if (_suctionOffset.LengthSquared() > SettledSq || _suctionVelocity.LengthSquared() > SettledSq) return;
        _suctionOffset = Vector2.Zero; // Assentou no trajeto
        _suctionVelocity = Vector2.Zero;
    }

    // Mola amortecida (oscilador harmônico): a = −ω²·desvio − 2·ζ·ω·velocidade, com ζ = RouteReturnDamping. O tempo para
    // assentar é ~4 / (ζ·ω), daí ω vem do RouteReturnTime.
    private Vector2 SpringAcceleration()
    {
        if (_suctionOffset == Vector2.Zero && _suctionVelocity == Vector2.Zero) return Vector2.Zero;

        float omega = 4f / (RouteReturnDamping * RouteReturnTime);
        return -omega * omega * _suctionOffset - 2f * RouteReturnDamping * omega * _suctionVelocity;
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

    // Saiu pela fenda: deixa a fase e conta como destruído para o objetivo, sem soltar a biomassa (decisão B3)
    private void LeaveThroughBreach()
    {
        IsDead = true;
        EmitSignal(SignalName.Destroyed, this);
        QueueFree();
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
