using Godot;
using System.Collections.Generic;

// Estoque de inimigos da fase (spec mobile-spawners.md), compartilhado pelos spawners móveis: object pooling, como no
// NinhoSpawnerPooled. Também guarda o orçamento: no máximo PoolSize inimigos da geração 0 vivos e MaxTotalEnemies no
// total (com os fragmentos). Fica num nó parado: os inimigos são filhos dele, e os fragmentos nascem no mesmo pai, em
// coordenadas locais (um pai que andasse levaria a horda junto).
// Os fragmentos da divisão também saem daqui (IFragmentSource; backlog 1.8): criar nós no meio de um massacre custava
// um quadro de 28 a 42 ms. São dois estoques do mesmo nó: o dos ancestrais (o tamanho é o teto de game design) e o dos
// fragmentos (só desempenho: se faltar, cria mais um, que fica).
public partial class EnemyPool : Node2D, IFragmentSource
{
    [Export] public PackedScene EnemyScene;        // fractal_enemy.tscn (ligada no Inspector)
    [Export] public int PoolSize = 60;             // Inimigos da geração 0 vivos na fase toda, no máximo
    [Export] public int FragmentPoolSize = 360;    // Fragmentos criados de antemão; se faltar, cria mais (ver SpawnFragment)
    [Export] public int MaxTotalEnemies = 350;     // Teto de inimigos vivos contando fragmentos (orçamento de física)

    // Cada estoque é uma fila: sai primeiro quem voltou há mais tempo, e só a partir do passo seguinte ao da volta
    private readonly record struct Stocked(FractalEnemy Enemy, ulong ReadyFrame);
    private readonly Queue<Stocked> _ancestorStock = new();
    private readonly Queue<Stocked> _fragmentStock = new();

    // Cria os estoques inteiros ao carregar a fase (escondido pelo escurecer), e não no meio do jogo
    public override void _Ready()
    {
        if (EnemyScene == null)
        {
            GD.PushWarning("[EnemyPool]: ligue a Enemy Scene (fractal_enemy.tscn) no Inspector.");
            return;
        }

        for (int i = 0; i < PoolSize; i++) _ancestorStock.Enqueue(new Stocked(CreateEnemy(), 0));
        for (int i = 0; i < FragmentPoolSize; i++) _fragmentStock.Enqueue(new Stocked(CreateEnemy(), 0));
    }

    // Tira um inimigo do estoque e o põe vivo em `globalPosition`. Devolve false se o estoque está vazio ou a fase já
    // está no teto de física: quem pediu espera (não é erro)
    public bool TrySpawn(Vector2 globalPosition)
    {
        if (FractalSwarm.Count >= MaxTotalEnemies) return false;

        FractalEnemy enemy = TakeRested(_ancestorStock);
        if (enemy == null) return false;

        Activate(enemy, globalPosition, 0);
        return true;
    }

    // Divisão de um inimigo daqui: o fragmento sai do estoque. A divisão nunca falha (é game design): sem fragmento
    // descansado, cria mais um, que volta para cá ao morrer (o estoque cresce). Os fragmentos não esperam o MaxTotalEnemies.
    // O massacre de 60 → 120 → 240 usa até 360 de uma vez: os 120 que morrem num passo só voltam a servir no seguinte
    public void SpawnFragment(Vector2 globalPosition, int generation)
    {
        FractalEnemy fragment = TakeRested(_fragmentStock) ?? CreateEnemy();
        Activate(fragment, globalPosition, generation);
    }

    // Cria um inimigo guardado (escondido, sem física), filho deste nó
    private FractalEnemy CreateEnemy()
    {
        var enemy = EnemyScene.Instantiate<FractalEnemy>();
        AddChild(enemy);
        enemy.DeactivateToPool();
        return enemy;
    }

    private void Activate(FractalEnemy enemy, Vector2 globalPosition, int generation)
    {
        enemy.ActivateAt(globalPosition, generation);
        enemy.OnDied = ReturnToStock;  // Ao morrer, volta para cá em vez de ser destruído
        enemy.FragmentSource = this;   // E, se dividir, os fragmentos também saem daqui
    }

    // O que voltou há mais tempo, se já descansou um passo; senão, null. Quem voltou neste passo ainda está nas listas
    // da lanterna e do traje (a Godot só avisa a saída no passo seguinte): reaproveitado agora, longe, o traje sentiria
    // um contato "fantasma" e levaria um golpe vindo de lá (verificado no motor, 4.7.2)
    private static FractalEnemy TakeRested(Queue<Stocked> stock)
    {
        if (stock.Count == 0) return null;
        if (stock.Peek().ReadyFrame > Engine.GetPhysicsFrames()) return null;
        return stock.Dequeue().Enemy;
    }

    // Volta ao estoque de onde saiu (a geração diz qual: só os ancestrais nascem na geração 0), pronto no passo seguinte
    private void ReturnToStock(FractalEnemy enemy)
    {
        enemy.DeactivateToPool();
        var stocked = new Stocked(enemy, Engine.GetPhysicsFrames() + 1);
        if (enemy.Generation == 0) _ancestorStock.Enqueue(stocked);
        else _fragmentStock.Enqueue(stocked);
    }
}
