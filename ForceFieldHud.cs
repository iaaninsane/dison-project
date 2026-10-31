using Godot;

// HUD mínimo de teste do campo de força. Só escuta o sinal ChargesChanged (Observer).
public partial class ForceFieldHud : Label
{
    // Conectado pelo editor ao sinal ChargesChanged do ForceField; sem carga, o campo está sempre recarregando
    public void OnChargesChanged(int current, int max) =>
        Text = current > 0 ? $"Campo: {current}/{max}" : "Campo: recarregando";
}
