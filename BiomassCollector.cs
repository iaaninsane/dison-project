using Godot;

// Coletor de biomassa do traje: atrai e recolhe os orbes próximos e guarda o total.
// Fica como nó filho do Player (composição), sem alterar o Player.cs.
public partial class BiomassCollector : Node2D
{
    [Signal] public delegate void BiomassChangedEventHandler(int total);

    [Export] public float PickupRadius = 12f;  // Distância em que o orbe é recolhido (px)
    [Export] public float MagnetRadius = 80f;  // Distância em que o ímã começa a puxar (px)
    [Export] public float MagnetSpeed = 700f;  // Velocidade do orbe puxado (px/s); acima do MaxSpeed do jogador (600)

    public int Total { get; private set; } = 0;

    public override void _PhysicsProcess(double delta)
    {
        int collected = BiomassField.Update((float)delta, GlobalPosition, PickupRadius, MagnetRadius, MagnetSpeed);
        if (collected <= 0) return;

        Total += collected;
        EmitSignal(SignalName.BiomassChanged, Total);
    }
}
