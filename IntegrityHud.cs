using Godot;

// HUD mínimo de teste: mostra a integridade do traje. Só escuta o sinal IntegrityChanged (Observer).
public partial class IntegrityHud : Label
{
    // Conectado pelo editor ao sinal IntegrityChanged do SuitIntegrity
    public void OnIntegrityChanged(int current) => Text = $"Traje: {current}";
}
