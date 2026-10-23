using Godot;
using System.Collections.Generic;
using System.Linq;

// Diretor dos spawners móveis (spec mobile-spawners.md): quando a incursão começa, cria os spawners e os põe nas
// rotas da fase, longe do nó de entrada. Fica como filho da raiz da hex_phase.tscn.
// A dificuldade (quanto cada spawner gera, a velocidade) e os avisos de contagem também vão ficar aqui (passos 3 a 6).
public partial class SpawnerDirector : Node2D
{
    [Export] public HexPhaseBuilder Phase;     // A fase (rotas e nó de entrada), ligada no Inspector
    [Export] public StartChamber StartChamber; // Câmara de partida (ligada no Inspector); vazio = começa ao carregar a fase
    [Export] public PackedScene SpawnerScene;  // mobile_spawner.tscn (ligada no Inspector)
    [Export] public int SpawnerCount = 10;     // Spawners por fase

    // Cada spawner começa entre 25% e 75% da rota: no corredor, longe dos nós e dos painéis
    private const float MinStartFraction = 0.25f;
    private const float MaxStartFraction = 0.75f;

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

        for (int i = 0; i < SpawnerCount; i++)
        {
            CreateSpawner(routes[i % routes.Count]); // Rotas diferentes enquanto houver
        }
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
        spawner.Position = ToLocal(Phase.ToGlobal(route.PointAt(from, distance)));
        spawner.FollowRoute(Phase, route, from, distance);
        AddChild(spawner);
    }
}
