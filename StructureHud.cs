using Godot;

// HUD mínimo de teste: mostra em qual estrutura da fase o jogador está.
// Só escuta o sinal StructureChanged do StructureTracker (Observer); não conhece o rastreador.
public partial class StructureHud : Label
{
    public override void _Ready() => ShowStructure("-"); // Até o primeiro aviso do rastreador

    // Conectado pelo editor ao sinal StructureChanged do StructureTracker
    public void OnStructureChanged(string structureName) => ShowStructure(structureName);

    private void ShowStructure(string structureName) => Text = $"Estrutura: {structureName}";
}
