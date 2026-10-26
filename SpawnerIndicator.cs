using Godot;
using System.Collections.Generic;

// Setas na borda da tela, uma para cada spawner vivo fora da tela (spec mobile-spawners.md, S6); a do mais próximo do
// jogador fica em destaque (maior e opaca), e as outras menores e meio transparentes. Spawner na tela não tem seta.
// Fica no PhaseHud (CanvasLayer): desenha em coordenadas de tela, e a posição do spawner no mundo vira posição na tela
// pela transformação do canvas (câmera e zoom).
public partial class SpawnerIndicator : Control
{
    [Export] public SpawnerDirector Director; // Quem sabe os spawners vivos (ligado no Inspector)
    [Export] public float EdgeMargin = 48f;   // Distância da ponta das setas até a borda da tela (px)

    [ExportGroup("Visual provisório (arte na etapa 2)")]
    [Export] public float ArrowSize = 28f;    // Comprimento da seta do mais próximo (px)
    [Export] public Color ArrowColor = new Color(1f, 0.55f, 0.85f); // A cor do anel do spawner
    [Export] public float OtherArrowScale = 0.6f; // Setas dos outros: menores...
    [Export] public float OtherArrowAlpha = 0.5f; // ...e meio transparentes

    private readonly record struct Arrow(Vector2 Tip, Vector2 Direction, bool IsNearest); // Ponta na tela

    private Node2D _player;
    private readonly List<Arrow> _arrows = new();

    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("Player") as Node2D;
    }

    public override void _Process(double delta)
    {
        UpdateArrows();
        QueueRedraw();
    }

    // Uma seta na borda para cada spawner vivo fora da tela, na direção dele; marca a do mais próximo do jogador
    private void UpdateArrows()
    {
        _arrows.Clear();
        if (Director == null || _player == null) return;

        MobileSpawner nearest = NearestSpawner();
        Rect2 area = GetViewportRect().Grow(-EdgeMargin);
        Vector2 center = area.GetCenter();
        Transform2D worldToScreen = GetViewport().GetCanvasTransform();
        foreach (MobileSpawner spawner in Director.AliveSpawners)
        {
            Vector2 onScreen = worldToScreen * spawner.GlobalPosition;
            if (area.HasPoint(onScreen)) continue; // Na tela: sem seta

            Vector2 direction = (onScreen - center).Normalized();
            Vector2 tip = center + direction * DistanceToEdge(direction, area.Size / 2f);
            _arrows.Add(new Arrow(tip, direction, spawner == nearest));
        }
    }

    // Quanto andar do centro, nessa direção, até a borda de um retângulo com essa metade de tamanho
    private static float DistanceToEdge(Vector2 direction, Vector2 halfSize)
    {
        float toSide = direction.X == 0f ? float.MaxValue : halfSize.X / Mathf.Abs(direction.X);
        float toTopOrBottom = direction.Y == 0f ? float.MaxValue : halfSize.Y / Mathf.Abs(direction.Y);
        return Mathf.Min(toSide, toTopOrBottom);
    }

    private MobileSpawner NearestSpawner()
    {
        MobileSpawner nearest = null;
        float bestDistanceSq = float.MaxValue;
        foreach (MobileSpawner spawner in Director.AliveSpawners)
        {
            float distanceSq = spawner.GlobalPosition.DistanceSquaredTo(_player.GlobalPosition);
            if (distanceSq >= bestDistanceSq) continue;

            bestDistanceSq = distanceSq;
            nearest = spawner;
        }
        return nearest;
    }

    // Primeiro as setas dos outros; a do mais próximo por último, por cima delas
    public override void _Draw()
    {
        var otherColor = new Color(ArrowColor, ArrowColor.A * OtherArrowAlpha);
        foreach (Arrow arrow in _arrows)
        {
            if (!arrow.IsNearest) DrawArrow(arrow, ArrowSize * OtherArrowScale, otherColor);
        }
        foreach (Arrow arrow in _arrows)
        {
            if (arrow.IsNearest) DrawArrow(arrow, ArrowSize, ArrowColor);
        }
    }

    // Triângulo com a ponta na borda, apontando para o spawner (em coordenadas deste Control)
    private void DrawArrow(Arrow arrow, float size, Color color)
    {
        Vector2 tip = arrow.Tip - GlobalPosition;
        Vector2 back = tip - arrow.Direction * size;
        Vector2 side = arrow.Direction.Orthogonal() * (size / 2f);
        DrawColoredPolygon(new[] { tip, back + side, back - side }, color);
    }
}
