using Godot;
using System.Collections.Generic;

// Área de contato do traje: só sabe o que está encostado no jogador e fere ao encostar (inimigos, spawners).
// A regra do dano fica no SuitIntegrity (responsabilidade única).
public partial class PlayerHurtbox : Area2D
{
    private readonly List<IContactHazard> _touching = new();

    public IReadOnlyList<IContactHazard> Touching => _touching; // O que está encostado agora (o campo de força repele)

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _PhysicsProcess(double delta) => RemoveGoneHazards();

    // Procura o perigo encostado mais próximo de "from". Devolve false se não houver nenhum.
    public bool TryGetNearestContact(Vector2 from, out Vector2 hazardPosition)
    {
        hazardPosition = Vector2.Zero;
        float bestDistanceSq = float.MaxValue;

        foreach (IContactHazard hazard in _touching)
        {
            float distanceSq = from.DistanceSquaredTo(hazard.GlobalPosition);
            if (distanceSq >= bestDistanceSq) continue;

            bestDistanceSq = distanceSq;
            hazardPosition = hazard.GlobalPosition;
        }

        return bestDistanceSq < float.MaxValue;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not IContactHazard hazard) return; // só o que fere ao encostar interessa
        if (_touching.Contains(hazard)) return;
        _touching.Add(hazard);
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is IContactHazard hazard) _touching.Remove(hazard);
    }

    // Tira da lista quem foi liberado ou morreu (inclusive os inimigos guardados no pool)
    private void RemoveGoneHazards()
    {
        for (int i = _touching.Count - 1; i >= 0; i--)
        {
            IContactHazard hazard = _touching[i];
            if (GodotObject.IsInstanceValid(hazard as GodotObject) && !hazard.IsDead) continue;
            _touching.RemoveAt(i);
        }
    }
}
