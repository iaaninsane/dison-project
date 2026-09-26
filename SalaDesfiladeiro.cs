using Godot;
using System;

public partial class SalaDesfiladeiro : Node2D
{
    [Export] public Vector2 RoomDimensions = new Vector2(1920f, 1080f);

    private Marker2D _entradaPoint;
    private Marker2D _saidaPoint;

    public override void _Ready()
    {
        InitializeMarkers();
        SetupPlayerPosition();
    }

    private void InitializeMarkers()
    {
        _entradaPoint = GetNodeOrNull<Marker2D>("EntradaPoint");
        _saidaPoint = GetNodeOrNull<Marker2D>("SaidaPoint");
    }

    private void SetupPlayerPosition()
    {
        var player = GetTree().GetFirstNodeInGroup("Player") as Player;
        if (player == null) 
            return;

        ResetPlayerInertia(player);
        SetPlayerAtEntrance(player);
        ConfigureCameraLimits(player);
    }

    private void ResetPlayerInertia(Player player)
    {
        player.Velocity = Vector2.Zero;
    }

    private void SetPlayerAtEntrance(Player player)
    {
        if (_entradaPoint != null)
        {
            player.GlobalPosition = _entradaPoint.GlobalPosition;
        }
    }

    private void ConfigureCameraLimits(Player player)
    {
        var camera = player.GetNodeOrNull<Camera2D>("Camera2D");
        if (camera == null) 
            return;

        camera.LimitLeft = 0;
        camera.LimitTop = 0;
        camera.LimitRight = (int)RoomDimensions.X;
        camera.LimitBottom = (int)RoomDimensions.Y;
    }
}