using Godot;
using System;

public partial class FractalEnemy : CharacterBody2D
{
    [Export] public float Speed = 80f;            // Velocidade de perseguição lenta e implacável
    [Export] public int RecursionDepth = 5;       // Profundidade da árvore fractal (complexidade)
    [Export] public float BaseBranchLength = 25f; // Tamanho das ramificações principais
    [Export] public Color NeonColor = new Color(0f, 1f, 0.5f); // Verde bio-luminescente neon

    // --- PULSAÇÃO DO ÂNGULO DOS RAMOS ---
    [Export] public float BaseBranchAngle = 0.4f;    // Ângulo médio de abertura dos ramos (radianos)
    [Export] public float PulseAmplitude = 0.15f;    // Quanto o ângulo oscila para cada lado (radianos)
    [Export] public float PulseFrequency = 0.6366f;  // Pulsos completos por segundo (Hz)

    // --- SEPARAÇÃO (horda sem colisão física entre inimigos) ---
    [Export] public float SeparationRadius = 16f;    // Distância em que vizinhos se empurram (px)
    [Export] public float SeparationStrength = 10f;  // Velocidade máxima de afastamento (px/s)
    private Vector2 _tieBreakDirection;              // Direção única deste inimigo, para desempate

    // --- BLOQUEIO (inimigo travado para de empurrar) ---
    [Export] public float RestSpeedThreshold = 2f;   // Abaixo disso (px/s): nada a mover, ou conta como travado
    [Export] public float BlockedRestTime = 0.25f;   // Tempo parado depois de travar, antes de tentar de novo (s)
    [Export] public float WakeAngleDegrees = 30f;    // Se a direção desejada girar mais que isso, acorda antes
    private float _restTimer = 0f;
    private Vector2 _blockedDirection = Vector2.Zero;

       // --- NOVOS ATRIBUTOS DE SISTEMA DE COMBATE ---
    [Export] public float MaxHealth = 100f;
    private float _currentHealth;
    private float _damageFlashTimer = 0f;
    private bool _isDead = false; // Garante que a morte aconteça uma única vez (ver Die)

    // Guardamos a cor original do neon para o efeito de piscar
    private Color _originalColor;

    private Node2D _player;
    private float _time = 0f;
    private float _currentBranchAngle = 0.5f;     // Ângulo de abertura em radianos

    // --- CONTROLE DE TAXA DE REDESENHO (otimização pra hordas grandes) ---
    [Export] public float RedrawsPerSecond = 20f;
    private float _redrawTimer = 0f;

    // --- SUPORTE A OBJECT POOLING (opcional; não afeta o uso sem pooling) ---
    // Se um spawner com pooling assinar este callback, Die() avisa em vez de se destruir.
    // Se ninguém assinar (fluxo atual, sem pooling), o comportamento continua idêntico ao original.
    public Action<FractalEnemy> OnDied;
    private CollisionShape2D _collisionShape;

    // Par simétrico: entrou na árvore de cena → inscreve; saiu → remove
    public override void _EnterTree()
    {
        FractalSwarm.Register(this);
    }

    public override void _ExitTree()
    {
        FractalSwarm.Unregister(this);
    }

    public override void _Ready()
    {
        // Busca segura pelo Player na árvore de nós da cena principal
        _player = GetTree().GetFirstNodeInGroup("Player") as Node2D;
        _currentHealth = MaxHealth;
        _originalColor = NeonColor;
        _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _tieBreakDirection = ComputeTieBreakDirection();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        UpdateDamageFlash(dt);

        // 1. Movimento
        UpdateMovement(dt);

        // 2. Animação Orgânica (Pulsação Trigonométrica)
        UpdateBranchPulse();

        // 3. Redesenha só na taxa configurada, não a cada tick de física
        _redrawTimer -= dt;
        if (_redrawTimer <= 0f)
        {
            _redrawTimer = 1f / Mathf.Max(RedrawsPerSecond, 1f);
            QueueRedraw();
        }
    }

    private void UpdateMovement(float dt)
    {
        if (_player == null) return;

        Vector2 desired = ComputeChaseVelocity() + ComputeSeparationVelocity(); // Decidir: perseguir + afastar dos vizinhos
        if (ShouldRest(desired, dt))                                            // Parado: sem MoveAndSlide
        {
            Velocity = Vector2.Zero;
            return;
        }

        Velocity = desired;
        MoveAndSlide();          // Executar o movimento
        CheckIfBlocked(desired); // Travou? Começa a descansar
    }

    // Decide se fica parado neste passo (e avança o tempo de descanso)
    private bool ShouldRest(Vector2 desired, float dt)
    {
        if (desired.Length() < RestSpeedThreshold) return true; // nada a mover
        if (_restTimer <= 0f) return false;                      // não está descansando

        _restTimer -= dt;
        float wakeDot = Mathf.Cos(Mathf.DegToRad(WakeAngleDegrees));
        if (desired.Normalized().Dot(_blockedDirection) < wakeDot)
        {
            _restTimer = 0f; // a direção mudou: acorda antes do tempo
            return false;
        }
        return true;
    }

    // Queria andar, mas quase não saiu do lugar? Então está travado
    private void CheckIfBlocked(Vector2 desired)
    {
        if (GetRealVelocity().Length() >= RestSpeedThreshold) return; // conseguiu andar

        _restTimer = BlockedRestTime;
        _blockedDirection = desired.Normalized();
    }

    private Vector2 ComputeChaseVelocity()
    {
        Vector2 direction = (_player.GlobalPosition - GlobalPosition).Normalized();
        return direction * Speed;
    }

    private Vector2 ComputeSeparationVelocity()
    {
        Vector2 push = FractalSwarm.ComputeSeparation(this, GlobalPosition, SeparationRadius, _tieBreakDirection);
        return push * SeparationStrength;
    }

    // Direção fixa derivada do ID, usada quando dois inimigos ocupam o mesmo ponto
    private Vector2 ComputeTieBreakDirection()
    {
        // Hash multiplicativo de Knuth: espalha IDs próximos em ângulos bem diferentes
        ulong hash = GetInstanceId() * 2654435761UL;
        float angle = (hash % 3600) / 3600f * Mathf.Tau;
        return Vector2.Right.Rotated(angle);
    }

    private void UpdateBranchPulse()
    {
        // Mathf.Tau (2π) converte ciclos por segundo em radianos por segundo para o seno
        _currentBranchAngle = BaseBranchAngle + Mathf.Sin(_time * PulseFrequency * Mathf.Tau) * PulseAmplitude;
    }

    public override void _Draw()
    {
        DrawFractalBranch(Vector2.Zero, Vector2.Up * BaseBranchLength, RecursionDepth);
        DrawFractalBranch(Vector2.Zero, Vector2.Down * BaseBranchLength, RecursionDepth);
        DrawFractalBranch(Vector2.Zero, Vector2.Left * BaseBranchLength, RecursionDepth);
        DrawFractalBranch(Vector2.Zero, Vector2.Right * BaseBranchLength, RecursionDepth);
    }

    private void DrawFractalBranch(Vector2 start, Vector2 direction, int depth)
    {
        if (depth <= 0) return;

        Vector2 end = start + direction;
        DrawLine(start, end, NeonColor, depth * 0.8f);

        Vector2 leftBranchDirection = direction.Rotated(_currentBranchAngle) * 0.7f;
        Vector2 rightBranchDirection = direction.Rotated(-_currentBranchAngle) * 0.7f;

        DrawFractalBranch(end, leftBranchDirection, depth - 1);
        DrawFractalBranch(end, rightBranchDirection, depth - 1);
    }
/****MÉTODOS AUXILIARES PARA COMBATE****** */

    public void TakeDamage(float amount)
    {
        if (_isDead) return; // Morto (ou guardado no pool) ignora dano

        _currentHealth -= amount;

        NeonColor = new Color(2f, 2f, 2f);
        _damageFlashTimer = 0.1f;
        _time += 0.2f;

        QueueRedraw(); // Flash de dano visível na hora, mesmo com o redesenho throttled

        if (_currentHealth <= 0f)
        {
            Die();
        }
    }

    private void UpdateDamageFlash(float delta)
    {
        if (_damageFlashTimer > 0f)
        {
            _damageFlashTimer -= delta;
            if (_damageFlashTimer <= 0f)
            {
                NeonColor = _originalColor;
            }
        }
    }

    private void Die()
    {
        _isDead = true;
        FractalSwarm.Unregister(this); // Sai do registro na hora, sem esperar o fim do frame

        if (OnDied != null)
        {
            // Tem um pool "escutando" — devolve o inimigo pro estoque em vez de destruir
            var callback = OnDied;
            OnDied = null;
            callback.Invoke(this);
        }
        else
        {
            // Fluxo original, sem pooling: comportamento idêntico a antes
            QueueFree();
        }
    }

    // --- MÉTODOS NOVOS, USADOS SÓ PELO SPAWNER COM POOLING ---

    public void ActivateAt(Vector2 position)
    {
        GlobalPosition = position;
        _isDead = false; // Inimigo reaproveitado do pool volta a estar vivo
        _currentHealth = MaxHealth;
        _damageFlashTimer = 0f;
        NeonColor = _originalColor;
        _time = 0f;
        Velocity = Vector2.Zero;
        _restTimer = 0f;                     // Reaproveitado do pool começa acordado
        _blockedDirection = Vector2.Zero;

        FractalSwarm.Register(this);

        Visible = true;
        SetPhysicsProcess(true);
        if (_collisionShape != null)
        {
            _collisionShape.Disabled = false;
        }
    }

    public void DeactivateToPool()
    {
        FractalSwarm.Unregister(this);

        Visible = false;
        SetPhysicsProcess(false);
        Velocity = Vector2.Zero;
        if (_collisionShape != null)
        {
            _collisionShape.Disabled = true;
        }
    }
}
