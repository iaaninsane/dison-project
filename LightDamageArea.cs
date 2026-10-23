using Godot;
using System;
using System.Collections.Generic;

public partial class LightDamageArea : Area2D
{
    [Export] public float DamagePerSecond = 35f;       // Dano contínuo da luz básica
    [Export] public float WallIrritationPerSecond = 20f; // Quanto a luz "irrita" as paredes sensíveis por segundo

    // Listas dinâmicas para rastreamento de alvos: o que a luz fere (inimigos, spawners) e as paredes que ela irrita
    private readonly List<ILightDamageable> _targetsInLight = new();
    private readonly List<Node2D> _wallsInLight = new();

    public override void _Ready()
    {
        // Conecta os sinais de colisão fisicamente de forma programática
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // 1. Aplica dano contínuo a tudo o que a luz fere (anticorpos e spawners)
        for (int i = _targetsInLight.Count - 1; i >= 0; i--)
        {
            var target = _targetsInLight[i];
            if (GodotObject.IsInstanceValid(target as GodotObject))
            {
                target.TakeDamage(DamagePerSecond * dt);
            }
            else
            {
                // Remove da lista se o alvo foi destruído
                _targetsInLight.RemoveAt(i);
            }
        }

        // 2. Aplica irritabilidade localizada nas paredes biológicas sob a luz
        for (int i = _wallsInLight.Count - 1; i >= 0; i--)
        {
            var wall = _wallsInLight[i];
            if (GodotObject.IsInstanceValid(wall))
            {
                // Verifica se a parede possui o método de irritação e o executa
                if (wall.HasMethod("AddIrritation"))
                {
                    wall.Call("AddIrritation", WallIrritationPerSecond * dt, GlobalPosition);
                }
            }
            else
            {
                _wallsInLight.RemoveAt(i);
            }
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        // Detecta se o objeto que entrou é algo que a luz fere (pela interface: não importa o tipo)
        if (body is ILightDamageable target)
        {
            if (!_targetsInLight.Contains(target))
            {
                _targetsInLight.Add(target);
            }
        }
        // Detecta se é uma parede celular sensível pelo grupo da Godot
        else if (body.IsInGroup("Walls"))
        {
            if (!_wallsInLight.Contains(body))
            {
                _wallsInLight.Add(body);
            }
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is ILightDamageable target)
        {
            _targetsInLight.Remove(target);
        }
        else if (body.IsInGroup("Walls"))
        {
            _wallsInLight.Remove(body);
        }
    }
}