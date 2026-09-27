using Godot;
using System;
using System.Collections.Generic;

public partial class LightDamageArea : Area2D
{
    [Export] public float DamagePerSecond = 35f;       // Dano contínuo da luz básica
    [Export] public float WallIrritationPerSecond = 20f; // Quanto a luz "irrita" as paredes sensíveis por segundo

    // Listas dinâmicas para rastreamento de alvos
    private readonly List<FractalEnemy> _enemiesInLight = new();
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

        // 1. Aplica dano contínuo a todos os anticorpos sob a luz
        for (int i = _enemiesInLight.Count - 1; i >= 0; i--)
        {
            var enemy = _enemiesInLight[i];
            if (GodotObject.IsInstanceValid(enemy))
            {
                enemy.TakeDamage(DamagePerSecond * dt);
            }
            else
            {
                // Remove da lista se o inimigo foi destruído
                _enemiesInLight.RemoveAt(i);
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
        // Detecta se o objeto que entrou é um inimigo fractal
        if (body is FractalEnemy enemy)
        {
            if (!_enemiesInLight.Contains(enemy))
            {
                _enemiesInLight.Add(enemy);
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
        if (body is FractalEnemy enemy)
        {
            _enemiesInLight.Remove(enemy);
        }
        else if (body.IsInGroup("Walls"))
        {
            _wallsInLight.Remove(body);
        }
    }
}