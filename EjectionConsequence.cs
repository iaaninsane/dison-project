using Godot;

// Consequência de ser ejetado para o espaço (spec start-chamber.md, D3): igual à morte. Ouve o LostInSpace do
// StructureTracker e rompe o traje do jogador; a morte segue o caminho de sempre (taxa da biomassa, escurecer e
// recarga da fase em modo câmara). Para mudar a regra, troca-se este componente (ideia do Strategy).
public partial class EjectionConsequence : Node
{
    [Export] public Player Player; // O jogador da cena (ligado no Inspector)

    // Conectado pelo editor ao sinal LostInSpace do StructureTracker
    public void OnPlayerLostInSpace()
    {
        SuitIntegrity suit = Player?.GetNodeOrNull<SuitIntegrity>("SuitIntegrity");
        if (suit == null)
        {
            GD.PushWarning("[EjectionConsequence]: o jogador não tem um nó SuitIntegrity (ou o campo Player está vazio).");
            return;
        }
        suit.Breach();
    }
}
