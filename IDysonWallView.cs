using Godot;

// Quem desenha a DysonWall (spec dyson-wall.md, decisão A1). Hoje é a W1 (DysonWallView, desenho por código);
// quando for a hora, a W2 (malha + shader) cumpre o mesmo contrato. A parede só fala com este contrato:
// trocar quem desenha não muda a parede (Strategy).
public interface IDysonWallView
{
    // Recebe a geometria e o estilo uma vez, ao montar a parede
    void Build(DysonWallGeometry geometry, DysonWallStyle style);

    // A parede mudou (irritação, tremor do aviso ou escalas da abertura): mostrar de novo.
    // A parede só chama enquanto está acordada (com alguma peça irritada ou uma abertura em andamento).
    void Refresh(DysonWallState state);
}

// Aparência da parede entregue a quem desenha: cores e tremor (provisórios; arte na etapa 2)
public readonly record struct DysonWallStyle(
    Color Membrane,
    Color PieceA,
    Color PieceB,
    Color Irritation,            // Brilho da peça irritada (verde-água na spec)
    float TremorStartIrritation, // Os sub-triângulos da peça tremem acima desta irritação (0 a 1)
    float TremorStrength);       // Tremor de cada sub-triângulo por quadro: até ±(irritação − início) × isto ÷ 2 px em cada eixo

// O que muda na parede, peça a peça, entregue a quem desenha. São os arrays da própria parede (a W2 os mandaria
// à placa de vídeo como texturas de dados).
public readonly record struct DysonWallState(
    float[] TileIrritation,  // Irritação de cada peça (0 a 1)
    float[] TileTremorBoost, // Tremor extra de cada peça (px): o aviso da abertura
    float[] SubScales,       // Escala de cada sub-triângulo (1 = inteiro, 0 = sumiu)
    float[] MembraneScales); // Escala de cada membrana
