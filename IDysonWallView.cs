using Godot;

// Quem desenha a DysonWall (spec dyson-wall.md, decisão A1). Hoje é a W1 (DysonWallView, desenho por código);
// quando for a hora, a W2 (malha + shader) cumpre o mesmo contrato. A parede só fala com este contrato:
// trocar quem desenha não muda a parede (Strategy).
public interface IDysonWallView
{
    // Recebe a geometria e o estilo uma vez, ao montar a parede
    void Build(DysonWallGeometry geometry, DysonWallStyle style);

    // A irritação mudou (0 a 1 por peça, no array da própria parede): mostrar de novo.
    // A parede só chama enquanto está acordada (com alguma peça irritada).
    void Refresh(float[] tileIrritation);
}

// Aparência da parede entregue a quem desenha: cores e tremor (provisórios; arte na etapa 2)
public readonly record struct DysonWallStyle(
    Color Membrane,
    Color PieceA,
    Color PieceB,
    Color Irritation,            // Brilho da peça irritada (verde-água na spec)
    float TremorStartIrritation, // Os sub-triângulos da peça tremem acima desta irritação (0 a 1)
    float TremorStrength);       // Tremor de cada sub-triângulo por quadro: até ±(irritação − início) × isto ÷ 2 px em cada eixo
