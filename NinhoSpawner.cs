using Godot;
using System;
using System.Collections.Generic;

public partial class NinhoSpawner : Node2D
{
    
    [Export] public PackedScene FractalEnemyScene;   // Arraste o fractal_enemy.tscn aqui no Inspector
    [Export] public Marker2D[] SpawnPoints;           // Arraste os 4 NinhoSpawn aqui no Inspector
    [Export] public float SpawnInterval = 2.5f;       // Intervalo entre tentativas de spawn (segundos)
    [Export] public int MaxConcurrentEnemies = 8;     // Teto de inimigos vivos ao mesmo tempo
 
    // Mesma estratégia de lista + IsInstanceValid já usada no LightDamageArea.cs,
    // pra não depender do grupo gravado na cena (evita repetir o problema do "Walls"/"Enimies").
    private readonly List<FractalEnemy> _activeEnemies = new();
    private float _spawnTimer = 0f;
    private readonly Random _rng = new();
 
    public override void _PhysicsProcess(double delta)
    {
        // Remove da lista qualquer inimigo que já morreu (QueueFree já rodou)
        for (int i = _activeEnemies.Count - 1; i >= 0; i--)
        {
            if (!GodotObject.IsInstanceValid(_activeEnemies[i]))
            {
                _activeEnemies.RemoveAt(i);
            }
        }
 
        _spawnTimer -= (float)delta;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = SpawnInterval;
            TrySpawnEnemy();
        }
    }
 
    private void TrySpawnEnemy()
    {
        if (FractalEnemyScene == null || SpawnPoints == null || SpawnPoints.Length == 0)
        {
            GD.PrintErr("[NinhoSpawner]: Configure FractalEnemyScene e SpawnPoints no Inspector antes de rodar.");
            return;
        }
 
        if (_activeEnemies.Count >= MaxConcurrentEnemies)
        {
            return; // Teto atingido, espera algum morrer pra abrir vaga
        }
 
        // Escolhe um dos ninhos aleatoriamente
        int index = _rng.Next(SpawnPoints.Length);
        Marker2D chosenNest = SpawnPoints[index];
 
        var enemyInstance = FractalEnemyScene.Instantiate<FractalEnemy>();
 
        // Adiciona à árvore ANTES de mexer em GlobalPosition (senão a transform ainda não existe)
        AddChild(enemyInstance);
        enemyInstance.GlobalPosition = chosenNest.GlobalPosition;
        enemyInstance.AddToGroup("Enemies"); // Grupo certo, garantido em código
 
        _activeEnemies.Add(enemyInstance);
    }

}
