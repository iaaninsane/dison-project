using Godot;

// Integridade do traje: aplica o dano de contato com janela de imunidade e avisa por sinal.
// Fica como nó filho do Player (composição).
public partial class SuitIntegrity : Node
{
    [Signal] public delegate void IntegrityChangedEventHandler(int current);
    [Signal] public delegate void DiedEventHandler();

    [Export] public int MaxIntegrity = 100;          // Integridade inicial do traje
    [Export] public int ContactDamage = 10;          // Dano por golpe de contato
    [Export] public float HitInvulnerability = 0.8f; // Imunidade depois de cada golpe (s)
    [Export] public float KnockbackImpulse = 200f;   // Velocidade somada ao jogador no golpe (px/s)
    [Export] public float BlinkInterval = 0.1f;      // Ritmo do piscar durante a imunidade (s)
    [Export] public PlayerHurtbox Hurtbox;           // Área de contato (ligada no Inspector)
    [Export] public CanvasItem BlinkTarget;          // O que pisca: o Sprite2D do jogador (ligado no Inspector)

    public int Current { get; private set; }
    private float _invulnerableTimer = 0f;
    private Player _player;                          // Dono deste componente (o pai)

    public override void _Ready()
    {
        Current = MaxIntegrity;
        _player = GetParent() as Player;
        Callable.From(NotifyIntegrity).CallDeferred(); // Estado inicial, depois que todos ficaram prontos
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateInvulnerability((float)delta);
        UpdateBlink();
        TryTakeContactHit();
    }

    private void UpdateInvulnerability(float dt)
    {
        if (_invulnerableTimer <= 0f) return;
        _invulnerableTimer -= dt;
    }

    // Pisca enquanto está imune; fora da imunidade, fica sempre visível
    private void UpdateBlink()
    {
        if (BlinkTarget == null) return;
        if (_invulnerableTimer <= 0f)
        {
            BlinkTarget.Visible = true;
            return;
        }

        int phase = (int)(_invulnerableTimer / Mathf.Max(BlinkInterval, 0.01f));
        BlinkTarget.Visible = phase % 2 == 0; // alterna a cada BlinkInterval
    }

    private void TryTakeContactHit()
    {
        if (Current <= 0) return;            // Traje destruído: morto não toma mais golpes
        if (_invulnerableTimer > 0f) return; // Imune depois do último golpe
        if (Hurtbox == null) return;
        if (!Hurtbox.TryGetNearestContact(Hurtbox.GlobalPosition, out Vector2 enemyPosition)) return; // Ninguém encostado

        TakeHit(enemyPosition);
    }

    private void TakeHit(Vector2 enemyPosition)
    {
        Current = Mathf.Max(0, Current - ContactDamage);
        _invulnerableTimer = HitInvulnerability;
        ApplyKnockback(enemyPosition);
        NotifyIntegrity();
        if (Current == 0) EmitSignal(SignalName.Died); // Traje destruído
    }

    // Empurra o jogador para longe do inimigo que causou o golpe
    private void ApplyKnockback(Vector2 enemyPosition)
    {
        if (_player == null) return;
        Vector2 awayFromEnemy = (_player.GlobalPosition - enemyPosition).Normalized();
        _player.ApplyKnockback(awayFromEnemy * KnockbackImpulse);
    }

    private void NotifyIntegrity() => EmitSignal(SignalName.IntegrityChanged, Current);
}
