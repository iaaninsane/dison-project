using Godot;
using System;
using System.Collections.Generic;

// VERSÃO DE TESTE — mesma função do NinhoSpawner.cs, mas com object pooling:
// em vez de Instantiate() + QueueFree() a cada spawn/morte, pré-cria um estoque
// de inimigos desativados uma única vez e só reaproveita eles.
// Não substitui o NinhoSpawner.cs — é pra rodar em paralelo e comparar.
public partial class NinhoSpawnerPooled : Node2D
{
    [Export] public PackedScene FractalEnemyScene;
    [Export] public Marker2D[] SpawnPoints;
    [Export] public float SpawnInterval = 2.5f;
    [Export] public int MaxConcurrentEnemies = 8;
    [Export] public int PoolSize = 100; // Deixe igual ou um pouco acima do MaxConcurrentEnemies que for testar

    private readonly List<FractalEnemy> _pool = new();
    private readonly List<FractalEnemy> _activeEnemies = new();
    private float _spawnTimer = 0f;
    private readonly Random _rng = new();

    public override void _Ready()
    {
        if (FractalEnemyScene == null)
        {
            GD.PrintErr("[NinhoSpawnerPooled]: Configure FractalEnemyScene no Inspector.");
            return;
        }

        // Pré-cria todo o estoque de uma vez só, já desativado.
        // Isso pode causar um pequeno engasgo no INÍCIO da cena (carregar tudo de uma vez),
        // que é exatamente o preço que estamos pagando aqui em vez de pagar toda hora depois.
        for (int i = 0; i < PoolSize; i++)
        {
            var enemy = FractalEnemyScene.Instantiate<FractalEnemy>();
            AddChild(enemy);
            enemy.DeactivateToPool();
            _pool.Add(enemy);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _spawnTimer -= (float)delta;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = SpawnInterval;
            TrySpawnEnemy();
        }
    }

    private void TrySpawnEnemy()
    {
        if (SpawnPoints == null || SpawnPoints.Length == 0)
        {
            GD.PrintErr("[NinhoSpawnerPooled]: Configure SpawnPoints no Inspector.");
            return;
        }

        if (_activeEnemies.Count >= MaxConcurrentEnemies)
        {
            return;
        }

        if (_pool.Count == 0)
        {
            GD.PrintErr("[NinhoSpawnerPooled]: Estoque do pool esgotado — aumente PoolSize.");
            return;
        }

        // Retira o último do estoque (remoção O(1), não importa a ordem)
        int lastIndex = _pool.Count - 1;
        FractalEnemy enemy = _pool[lastIndex];
        _pool.RemoveAt(lastIndex);

        Marker2D chosenNest = SpawnPoints[_rng.Next(SpawnPoints.Length)];
        enemy.ActivateAt(chosenNest.GlobalPosition);

        // Quando esse inimigo morrer, ele volta pro estoque em vez de ser destruído de verdade
        enemy.OnDied = ReturnToPool;

        _activeEnemies.Add(enemy);
    }

    private void ReturnToPool(FractalEnemy enemy)
    {
        _activeEnemies.Remove(enemy);
        enemy.DeactivateToPool();
        _pool.Add(enemy);
    }
}
