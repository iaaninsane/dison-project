using Godot;
using System.Collections.Generic;
using System.Linq;

// Diretor dos spawners móveis (spec mobile-spawners.md): quando a incursão começa, cria os spawners e os põe nas
// rotas da fase, longe do nó de entrada. Fica como filho da raiz da hex_phase.tscn.
// Dá o ritmo (curva de dificuldade): um orçamento global de geração repartido entre os spawners vivos, que cresce a
// cada spawner morto, e a velocidade deles. Os spawners só executam. Avisa a contagem e a fase limpa (Observer):
// o HUD e a seta só escutam.
public partial class SpawnerDirector : Node2D
{
    [Signal] public delegate void SpawnersChangedEventHandler(int remaining, int total); // Ao começar e a cada morte
    [Signal] public delegate void AllSpawnersDestroyedEventHandler();                   // Fase limpa

    [Export] public HexPhaseBuilder Phase;     // A fase (rotas e nó de entrada), ligada no Inspector
    [Export] public StartChamber StartChamber; // Câmara de partida (ligada no Inspector); vazio = começa ao carregar a fase
    [Export] public PackedScene SpawnerScene;  // mobile_spawner.tscn (ligada no Inspector)
    [Export] public EnemyPool Pool;            // Estoque de inimigos da fase (ligado no Inspector); vazio = ninguém gera
    [Export] public int SpawnerCount = 10;     // Spawners por fase

    [ExportGroup("Curva de dificuldade")]
    [Export] public float BaseSpawnRate = 1.5f;          // Inimigos por segundo, somando todos os spawners vivos
    [Export] public float SpawnRateGrowthPerKill = 0.1f; // +10% na taxa total por spawner morto
    [Export] public float SpeedGrowthPerKill = 0.05f;    // +5% na velocidade dos spawners por spawner morto

    // Cada spawner começa entre 25% e 75% da rota: no corredor, longe dos nós e dos painéis
    private const float MinStartFraction = 0.25f;
    private const float MaxStartFraction = 0.75f;

    private readonly List<MobileSpawner> _alive = new();
    private int _killed;
    private int _total;

    public IReadOnlyList<MobileSpawner> AliveSpawners => _alive; // Para a seta (só leitura)

    // Os avisos vêm de nós da própria cena; ligar por código usa as mesmas referências do Inspector (um fio só)
    public override void _Ready()
    {
        if (StartChamber != null)
        {
            StartChamber.IncursionStarted += OnIncursionStarted;
            return;
        }
        Callable.From(OnIncursionStarted).CallDeferred(); // Fase aberta: começa já, depois que a raiz montou o Layout
    }

    // Par simétrico do _Ready
    public override void _ExitTree()
    {
        if (StartChamber != null) StartChamber.IncursionStarted -= OnIncursionStarted;
    }

    private void OnIncursionStarted()
    {
        if (Phase?.Layout == null) return;
        if (SpawnerScene == null)
        {
            GD.PushWarning("[SpawnerDirector]: ligue a Spawner Scene (mobile_spawner.tscn) no Inspector.");
            return;
        }

        List<HexRoute> routes = ShuffledStartRoutes();
        if (routes.Count == 0) return;

        if (Pool == null) GD.PushWarning("[SpawnerDirector]: sem o Pool (EnemyPool), os spawners não geram inimigos.");

        for (int i = 0; i < SpawnerCount; i++)
        {
            CreateSpawner(routes[i % routes.Count]); // Rotas diferentes enquanto houver
        }
        _total = _alive.Count;
        ApplyPace();
        NotifyCount();
    }

    private void OnSpawnerDestroyed(MobileSpawner spawner)
    {
        _alive.Remove(spawner);
        _killed++;
        ApplyPace();
        NotifyCount();
        if (_alive.Count == 0) EmitSignal(SignalName.AllSpawnersDestroyed);
    }

    private void NotifyCount() => EmitSignal(SignalName.SpawnersChanged, _alive.Count, _total);

    // Orçamento global: a taxa total cresce a cada spawner morto e é repartida entre os vivos (o último fica frenético).
    // Taxa total 0: intervalo infinito, ninguém gera.
    private void ApplyPace()
    {
        if (_alive.Count == 0) return;

        float totalRate = BaseSpawnRate * (1f + SpawnRateGrowthPerKill * _killed);
        float interval = _alive.Count / totalRate;
        float speed = 1f + SpeedGrowthPerKill * _killed;
        foreach (MobileSpawner spawner in _alive) spawner.SetPace(interval, speed);
    }

    // Rotas que não tocam o nó de entrada (onde o jogador começa), em ordem aleatória
    private List<HexRoute> ShuffledStartRoutes()
    {
        HexStructure entry = Phase.Layout.PeripheralNode(Phase.EntryPeripheral);
        return Phase.Layout.Routes.Where(route => !route.Touches(entry))
                                  .OrderBy(_ => GD.Randf())
                                  .ToList();
    }

    // Ponto e sentido sorteados na rota. A posição é dada antes de o spawner entrar na árvore: ele já nasce no lugar
    // (um corpo movido por código que "pulasse" da origem até a rota levaria essa velocidade para a física)
    private void CreateSpawner(HexRoute route)
    {
        HexWaypoint from = GD.Randf() < 0.5f ? route.A : route.B;
        float distance = route.Length * (float)GD.RandRange(MinStartFraction, MaxStartFraction);

        var spawner = SpawnerScene.Instantiate<MobileSpawner>();
        spawner.FollowRoute(Phase, route, from, distance);
        spawner.Position = ToLocal(spawner.PathPosition); // No trajeto (afastado do eixo conforme a forma), não no eixo
        spawner.Pool = Pool;
        spawner.Destroyed += OnSpawnerDestroyed;
        _alive.Add(spawner);
        AddChild(spawner);
    }
}
