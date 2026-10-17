using Godot;

// Porta: quando o jogador entra, troca para a cena configurada no Inspector.
public partial class DoorTrigger : Area2D
{
    [Export(PropertyHint.File, "*.tscn")] public string TargetScenePath = ""; // Cena de destino

    private bool _used = false; // Evita pedir a troca de cena duas vezes

    public override void _Ready() => BodyEntered += OnBodyEntered;

    private void OnBodyEntered(Node2D body)
    {
        if (_used) return;
        if (body is not Player) return;
        if (string.IsNullOrEmpty(TargetScenePath))
        {
            GD.PushWarning("[DoorTrigger]: TargetScenePath vazio no Inspector.");
            return;
        }

        _used = true;
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, TargetScenePath); // Fora do callback de física
    }
}
