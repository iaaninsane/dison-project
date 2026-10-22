using Godot;

// Porta: quando o jogador (vivo) entra, escurece a tela e troca para a cena configurada no Inspector.
public partial class DoorTrigger : Area2D
{
    [Export(PropertyHint.File, "*.tscn")] public string TargetScenePath = ""; // Cena de destino

    private bool _used = false; // Evita pedir a troca de cena duas vezes

    public override void _Ready() => BodyEntered += OnBodyEntered;

    private void OnBodyEntered(Node2D body)
    {
        if (_used) return;
        if (body is not Player player || player.IsDead) return; // Morto não usa porta: quem age é o RestartOnDeath
        if (string.IsNullOrEmpty(TargetScenePath))
        {
            GD.PushWarning("[DoorTrigger]: TargetScenePath vazio no Inspector.");
            return;
        }

        _used = true;
        ScreenFade.ChangeScene(this, TargetScenePath); // Escurece e troca (cena sem ScreenFade: troca direto)
    }
}
