using Godot;
using System.Collections.Generic;

// Sistema central da biomassa: registra os orbes, controla o teto e, num único laço, idade, ímã e coleta.
public static class BiomassField
{
    private static readonly List<BiomassOrb> _orbs = new(); // Ordem de chegada: o mais antigo no início
    private static int _maxOrbs = int.MaxValue;              // Último teto informado por quem soltou um orbe

    public static int Count => _orbs.Count;

    // Cria o orbe no fim do frame (a morte acontece dentro do laço de dano da lanterna)
    public static void Drop(Node parent, Vector2 localPosition, int amount, float lifetime, int maxOrbs)
    {
        if (parent == null || amount <= 0) return;

        _maxOrbs = maxOrbs;
        var orb = new BiomassOrb { Amount = amount, Lifetime = lifetime, Position = localPosition };
        parent.CallDeferred(Node.MethodName.AddChild, orb);
    }

    public static void Register(BiomassOrb orb)
    {
        if (_orbs.Contains(orb)) return;

        orb.WorldPosition = orb.GlobalPosition; // Lida do motor uma única vez
        _orbs.Add(orb);
        RemoveOldestOverLimit();
    }

    public static void Unregister(BiomassOrb orb) => _orbs.Remove(orb);

    // Um único laço por passo de física para todos os orbes: idade, ímã e coleta.
    // Devolve a biomassa coletada neste passo.
    public static int Update(float dt, Vector2 collectorPosition, float pickupRadius, float magnetRadius, float magnetSpeed)
    {
        int collected = 0;
        float pickupSq = pickupRadius * pickupRadius;
        float magnetSq = magnetRadius * magnetRadius;

        for (int i = _orbs.Count - 1; i >= 0; i--) // de trás para frente: dá para remover sem pular ninguém
        {
            BiomassOrb orb = _orbs[i];

            orb.Age += dt;
            if (orb.Age >= orb.Lifetime) { RemoveAt(i); continue; } // venceu: some

            float distanceSq = (collectorPosition - orb.WorldPosition).LengthSquared();
            if (distanceSq <= pickupSq) { collected += orb.Amount; RemoveAt(i); continue; } // coletado
            if (distanceSq > magnetSq) continue;                                            // longe: fica parado

            orb.WorldPosition = orb.WorldPosition.MoveToward(collectorPosition, magnetSpeed * dt); // ímã
            orb.GlobalPosition = orb.WorldPosition; // só aqui o motor recebe a posição
        }

        return collected;
    }

    // Teto de orbes: o mais antigo some primeiro
    private static void RemoveOldestOverLimit()
    {
        while (_orbs.Count > _maxOrbs)
        {
            RemoveAt(0);
        }
    }

    // Remove da lista e libera o nó (usado pela idade, pela coleta e pelo teto)
    private static void RemoveAt(int index)
    {
        BiomassOrb orb = _orbs[index];
        _orbs.RemoveAt(index);
        orb.QueueFree();
    }
}
