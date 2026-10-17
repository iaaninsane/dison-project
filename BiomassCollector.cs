using Godot;

// Coletor de biomassa do traje: atrai e recolhe os orbes próximos.
// O total mora no GameState (sobrevive à troca de sala); este nó só recolhe, entrega e avisa.
// Fica como nó filho do Player (composição), sem alterar o Player.cs.
public partial class BiomassCollector : Node2D
{
    [Signal] public delegate void BiomassChangedEventHandler(int total);

    [Export] public float PickupRadius = 12f;  // Distância em que o orbe é recolhido (px)
    [Export] public float MagnetRadius = 80f;  // Distância em que o ímã começa a puxar (px)
    [Export] public float MagnetSpeed = 700f;  // Velocidade do orbe puxado (px/s); acima do MaxSpeed do jogador (600)

    public int Total => GameState.Instance?.Data.CarriedBiomass ?? 0; // Biomassa carregada na incursão

    public override void _Ready()
    {
        Callable.From(NotifyTotal).CallDeferred(); // Mostra a biomassa carregada ao entrar na sala
    }

    public override void _PhysicsProcess(double delta)
    {
        int collected = BiomassField.Update((float)delta, GlobalPosition, PickupRadius, MagnetRadius, MagnetSpeed);
        if (collected <= 0) return;

        GameState.Instance?.AddCarried(collected);
        NotifyTotal();
    }

    private void NotifyTotal() => EmitSignal(SignalName.BiomassChanged, Total);
}
