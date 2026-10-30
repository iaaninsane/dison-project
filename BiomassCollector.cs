using Godot;

// Coletor de biomassa do traje: atrai e recolhe os orbes próximos.
// Só recolhe e avisa quanto coletou (sinal BiomassCollected); quem transforma a coleta em experiência é o
// ExperienceTracker (spec level-up.md). Fica como nó filho do Player (composição), sem alterar o Player.cs.
public partial class BiomassCollector : Node2D
{
    [Signal] public delegate void BiomassCollectedEventHandler(int amount);

    [Export] public float PickupRadius = 12f;  // Distância em que o orbe é recolhido (px)
    [Export] public float MagnetRadius = 80f;  // Distância em que o ímã começa a puxar (px)
    [Export] public float MagnetSpeed = 700f;  // Velocidade do orbe puxado (px/s); acima do MaxSpeed do jogador (600)
    // Sucção das fendas nos orbes (spec dyson-wall.md, 2.6): quem move os orbes a cada passo é este coletor
    [Export] public float OrbSuctionFactor = 1f;  // Quanto os orbes sentem a sucção (OrbSuctionFactor da spec)
    [Export] public float OrbSuctionDecay = 300f; // Fora do campo, a velocidade ganha da sucção some neste ritmo (px/s²)

    public override void _PhysicsProcess(double delta)
    {
        int collected = BiomassField.Update((float)delta, GlobalPosition, PickupRadius, MagnetRadius, MagnetSpeed,
            OrbSuctionFactor, OrbSuctionDecay);
        if (collected <= 0) return;

        EmitSignal(SignalName.BiomassCollected, collected); // A coleta do passo inteiro num aviso só
    }
}
