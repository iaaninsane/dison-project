using Godot;
using System;

public partial class SalaEntrada : Node2D
{
    private Marker2D _spawnPoint;

    public override void _Ready()
    {
        _spawnPoint = GetNodeOrNull<Marker2D>("SpawnPoint");

        // Encontra o Player ativo e o posiciona no SpawnPoint
        var player = GetTree().GetFirstNodeInGroup("Player") as Player;
        if (player != null && _spawnPoint != null)
        {
            // 1. Posiciona o Player no Spawn
            if (_spawnPoint != null) {
                player.GlobalPosition = _spawnPoint.GlobalPosition;
                player.Velocity = Vector2.Zero;
            }

            // 2. Trava a Camera2D do Player dentro dos limites de 1920x1080 desta sala
            var camera = player.GetNodeOrNull<Camera2D>("Camera2D");
            if (camera != null) {
                camera.LimitLeft = 0;
                camera.LimitTop = 0;
                camera.LimitRight = 1920;
                camera.LimitBottom = 1080;
            }
        }
    }
}
