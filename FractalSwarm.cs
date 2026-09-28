using Godot;
using System.Collections.Generic;

// Registro central dos inimigos fractais ativos + separação da horda.
// Os inimigos se inscrevem e se removem sozinhos; quem precisar da lista só lê.
public static class FractalSwarm
{
    // Abaixo desta distância (ao quadrado) dois inimigos contam como "no mesmo ponto"
    private const float MinDistanceSq = 0.0001f;
    private const int InitialCapacity = 256;

    private static readonly List<FractalEnemy> _activeEnemies = new();

    // A "foto": referências + posições copiadas uma vez por passo de física
    private static FractalEnemy[] _snapshotEnemies = new FractalEnemy[InitialCapacity];
    private static Vector2[] _snapshotPositions = new Vector2[InitialCapacity];
    private static int _snapshotCount;
    private static ulong _snapshotFrame = ulong.MaxValue;

    public static int Count => _activeEnemies.Count;

    public static void Register(FractalEnemy enemy)
    {
        if (_activeEnemies.Contains(enemy)) return; // já inscrito
        _activeEnemies.Add(enemy);
    }

    public static void Unregister(FractalEnemy enemy)
    {
        _activeEnemies.Remove(enemy); // se não estiver na lista, não faz nada
    }

    // Soma dos empurrões dos vizinhos dentro do raio, com tamanho máximo 1
    public static Vector2 ComputeSeparation(FractalEnemy self, Vector2 selfPosition, float radius, Vector2 tieBreakDirection)
    {
        EnsureSnapshot();

        float radiusSq = radius * radius;
        Vector2 push = Vector2.Zero;

        for (int i = 0; i < _snapshotCount; i++)
        {
            if (_snapshotEnemies[i] == self) continue; // não se empurra

            Vector2 offset = selfPosition - _snapshotPositions[i];
            float distanceSq = offset.LengthSquared();
            if (distanceSq >= radiusSq) continue; // longe demais

            if (distanceSq < MinDistanceSq)
            {
                push += tieBreakDirection; // mesmo ponto: sem direção definida, usa o desempate
                continue;
            }

            float distance = Mathf.Sqrt(distanceSq);
            push += offset / distance * (1f - distance / radius); // mais perto = mais forte
        }

        return push.LimitLength(1f);
    }

    // Tira a foto só uma vez por passo de física; os demais pedidos reaproveitam
    private static void EnsureSnapshot()
    {
        ulong frame = Engine.GetPhysicsFrames();
        if (frame == _snapshotFrame) return;
        _snapshotFrame = frame;

        EnsureCapacity(_activeEnemies.Count);

        _snapshotCount = _activeEnemies.Count;
        for (int i = 0; i < _snapshotCount; i++)
        {
            _snapshotEnemies[i] = _activeEnemies[i];
            _snapshotPositions[i] = _activeEnemies[i].GlobalPosition;
        }
    }

    // Cresce os arrays só quando falta espaço (sem alocar a cada passo)
    private static void EnsureCapacity(int required)
    {
        if (_snapshotEnemies.Length >= required) return;

        int newCapacity = Mathf.Max(required, _snapshotEnemies.Length * 2);
        _snapshotEnemies = new FractalEnemy[newCapacity];
        _snapshotPositions = new Vector2[newCapacity];
    }
}
