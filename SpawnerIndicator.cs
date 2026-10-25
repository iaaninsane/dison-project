using Godot;

// Seta na borda da tela apontando para o spawner vivo mais próximo do jogador (spec mobile-spawners.md, S6).
// Some quando ele está na tela ou quando não há spawner vivo. Fica no PhaseHud (CanvasLayer): desenha em coordenadas
// de tela, e a posição do spawner no mundo vira posição na tela pela transformação do canvas (câmera e zoom).
public partial class SpawnerIndicator : Control
{
    [Export] public SpawnerDirector Director; // Quem sabe os spawners vivos (ligado no Inspector)
    [Export] public float EdgeMargin = 48f;   // Distância da ponta da seta até a borda da tela (px)

    [ExportGroup("Visual provisório (arte na etapa 2)")]
    [Export] public float ArrowSize = 28f;    // Comprimento da seta (px)
    [Export] public Color ArrowColor = new Color(1f, 0.55f, 0.85f); // A cor do anel do spawner

    private Node2D _player;
    private bool _hasArrow;
    private Vector2 _arrowTip;   // Na tela
    private Vector2 _arrowDirection;

    public override void _Ready()
    {
        _player = GetTree().GetFirstNodeInGroup("Player") as Node2D;
    }

    public override void _Process(double delta)
    {
        UpdateArrow();
        QueueRedraw();
    }

    // Spawner vivo mais próximo do jogador; se estiver fora da tela, a seta vai na borda, na direção dele
    private void UpdateArrow()
    {
        _hasArrow = false;
        MobileSpawner target = NearestSpawner();
        if (target == null) return;

        Rect2 area = GetViewportRect().Grow(-EdgeMargin);
        Vector2 onScreen = GetViewport().GetCanvasTransform() * target.GlobalPosition;
        if (area.HasPoint(onScreen)) return; // Na tela: sem seta

        Vector2 center = area.GetCenter();
        _arrowDirection = (onScreen - center).Normalized();
        _arrowTip = center + _arrowDirection * DistanceToEdge(_arrowDirection, area.Size / 2f);
        _hasArrow = true;
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
        if (Director == null || _player == null) return null;

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

    // Triângulo com a ponta na borda, apontando para o spawner (em coordenadas deste Control)
    public override void _Draw()
    {
        if (!_hasArrow) return;

        Vector2 tip = _arrowTip - GlobalPosition;
        Vector2 back = tip - _arrowDirection * ArrowSize;
        Vector2 side = _arrowDirection.Orthogonal() * (ArrowSize / 2f);
        DrawColoredPolygon(new[] { tip, back + side, back - side }, ArrowColor);
    }
}
