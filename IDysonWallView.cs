using Godot;

// Quem desenha a DysonWall (spec dyson-wall.md, decisão A1). Hoje é a W1 (DysonWallView, desenho por código);
// quando for a hora, a W2 (malha + shader) cumpre o mesmo contrato. A parede só fala com este contrato:
// trocar quem desenha não muda a parede (Strategy).
public interface IDysonWallView
{
    // Recebe a geometria e as cores uma vez, ao montar a parede
    void Build(DysonWallGeometry geometry, DysonWallPalette palette);
}

// Cores da parede entregues a quem desenha (provisórias; arte na etapa 2)
public readonly record struct DysonWallPalette(Color Membrane, Color PieceA, Color PieceB);
