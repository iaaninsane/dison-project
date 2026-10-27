using Godot;

// Orbe de biomassa neon: só existe e se desenha. Idade, ímã e coleta ficam no BiomassField.
public partial class BiomassOrb : Node2D
{
    private const float DrawRadius = 3f;                         // Raio do círculo desenhado (px)
    private static readonly Color OrbColor = new(0.2f, 1f, 1f);  // Ciano neon (provisório, arte é etapa 2)
    private static CanvasItemMaterial _unshadedMaterial;         // Um material para todos os orbes

    public int Amount;      // Quanto de biomassa este orbe vale
    public float Lifetime;  // Tempo até sumir se não for coletado (s)
    public float Age;       // Tempo de vida decorrido (s), avançado pelo BiomassField
    public Vector2 WorldPosition; // Cópia da posição em C#: lida do motor ao registrar, escrita só quando o orbe se move
    public Vector2 SuctionVelocity; // Velocidade ganha da sucção das fendas (spec dyson-wall.md, 2.6), avançada pelo BiomassField

    // Par simétrico: entrou na árvore → inscreve; saiu → remove
    public override void _EnterTree() => BiomassField.Register(this);
    public override void _ExitTree() => BiomassField.Unregister(this);

    public override void _Ready()
    {
        Material = GetUnshadedMaterial(); // Sempre visível, mesmo no escuro
    }

    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, DrawRadius, OrbColor);
    }

    // Cria o material na primeira vez e reaproveita nas seguintes
    private static CanvasItemMaterial GetUnshadedMaterial()
    {
        if (_unshadedMaterial != null) return _unshadedMaterial;
        _unshadedMaterial = new CanvasItemMaterial { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded };
        return _unshadedMaterial;
    }
}
