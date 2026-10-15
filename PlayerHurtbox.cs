using Godot;
using System.Collections.Generic;

// Área de contato do traje: só sabe quais inimigos estão encostados no jogador.
// A regra do dano fica no SuitIntegrity (responsabilidade única).
public partial class PlayerHurtbox : Area2D
{
    private readonly List<FractalEnemy> _touching = new();

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _PhysicsProcess(double delta) => RemoveGoneEnemies();

    // Procura o inimigo encostado mais próximo de "from". Devolve false se não houver nenhum.
    public bool TryGetNearestContact(Vector2 from, out Vector2 enemyPosition)
    {
        enemyPosition = Vector2.Zero;
        float bestDistanceSq = float.MaxValue;

        foreach (FractalEnemy enemy in _touching)
        {
            float distanceSq = from.DistanceSquaredTo(enemy.GlobalPosition);
            if (distanceSq >= bestDistanceSq) continue;

            bestDistanceSq = distanceSq;
            enemyPosition = enemy.GlobalPosition;
        }

        return bestDistanceSq < float.MaxValue;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not FractalEnemy enemy) return; // só inimigos interessam
        if (_touching.Contains(enemy)) return;
        _touching.Add(enemy);
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is FractalEnemy enemy) _touching.Remove(enemy);
    }

    // Tira da lista quem foi liberado ou morreu (inclusive os guardados no pool)
    private void RemoveGoneEnemies()
    {
        for (int i = _touching.Count - 1; i >= 0; i--)
        {
            FractalEnemy enemy = _touching[i];
            if (GodotObject.IsInstanceValid(enemy) && !enemy.IsDead) continue;
            _touching.RemoveAt(i);
        }
    }
}
