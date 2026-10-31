using Godot;
using System.Collections.Generic;

// Campo de força do traje (spec force-field.md): absorve os golpes de contato enquanto tiver carga e repele os inimigos
// encostados. Esgotado, desliga e volta cheio depois da recarga. Só conta tempo enquanto recarrega.
// Quem decide QUANDO há golpe é o SuitIntegrity; aqui se decide se ele é absorvido.
// Fica como nó filho do Player (composição): a recarga da cena traz um campo novo e cheio.
public partial class ForceField : Node2D
{
    [Signal] public delegate void ChargesChangedEventHandler(int current, int max);

    [Export] public int BaseCharges = 1;       // Cargas no nível 1
    [Export] public float RechargeTime = 5f;   // Do desligar ao voltar cheio (s)
    [Export] public float RepelImpulse = 300f; // Velocidade dada a cada inimigo repelido (px/s)

    [ExportGroup("Visual provisório")]
    [Export] public float VisualRadius = 16f;  // Raio do círculo (px); o corpo do jogador tem 10 × 10
    [Export] public Color FieldColor = new(0.3f, 0.7f, 1f, 0.8f);

    private const float DrawWidth = 1.5f; // Espessura do círculo (px)
    private const int DrawPoints = 32;    // Pontos do círculo

    private int _maxCharges;
    private int _charges;
    private float _rechargeTimer;

    public override void _Ready()
    {
        _maxCharges = BaseCharges;
        _charges = _maxCharges;
        Material = new CanvasItemMaterial { LightMode = CanvasItemMaterial.LightModeEnum.Unshaded }; // Visível no escuro
        SetPhysicsProcess(false); // Cheio: não há o que contar
        Callable.From(NotifyCharges).CallDeferred(); // Estado inicial, depois que o HUD ficou pronto
    }

    public override void _Draw()
    {
        DrawArc(Vector2.Zero, VisualRadius, 0f, Mathf.Tau, DrawPoints, FieldColor, DrawWidth);
    }

    // Só roda durante a recarga
    public override void _PhysicsProcess(double delta)
    {
        _rechargeTimer -= (float)delta;
        if (_rechargeTimer > 0f) return;

        Recharge();
    }

    // Pedido do SuitIntegrity a cada golpe. Com carga: gasta 1, repele quem está encostado e devolve true
    public bool TryAbsorbHit(IReadOnlyList<IContactHazard> contacts)
    {
        if (_charges <= 0) return false; // Desligado: o golpe fere o traje

        _charges--;
        Repel(contacts);
        if (_charges == 0) StartRecharge();
        NotifyCharges();
        return true;
    }

    // Empurra pelo contrato, sem conhecer o tipo: o spawner fere ao encostar, mas não é IRepellable
    private void Repel(IReadOnlyList<IContactHazard> contacts)
    {
        foreach (IContactHazard contact in contacts)
            if (contact is IRepellable repellable) repellable.Repel(GlobalPosition, RepelImpulse);
    }

    private void StartRecharge()
    {
        _rechargeTimer = RechargeTime;
        Visible = false;
        SetPhysicsProcess(true);
    }

    private void Recharge()
    {
        _charges = _maxCharges;
        Visible = true;
        SetPhysicsProcess(false);
        NotifyCharges();
    }

    private void NotifyCharges() => EmitSignal(SignalName.ChargesChanged, _charges, _maxCharges);
}
