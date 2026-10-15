using Godot;

// HUD mínimo de teste: mostra o total de biomassa.
// Só escuta o sinal BiomassChanged do coletor (Observer); não conhece o coletor.
public partial class BiomassHud : Label
{
    public override void _Ready() => ShowTotal(0); // O coletor começa com 0

    // Conectado pelo editor ao sinal BiomassChanged do BiomassCollector
    public void OnBiomassChanged(int total) => ShowTotal(total);

    private void ShowTotal(int total) => Text = $"Biomassa: {total}";
}
