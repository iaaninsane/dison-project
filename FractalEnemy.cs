using Godot;
using System;

public partial class FractalEnemy : CharacterBody2D
{
    [Export] public float Speed = 80f;            // Velocidade de perseguição lenta e implacável
    [Export] public int RecursionDepth = 5;       // Profundidade da árvore fractal (complexidade)
    [Export] public float BaseBranchLength = 25f; // Tamanho das ramificações principais
    [Export] public Color NeonColor = new Color(0f, 1f, 0.5f); // Verde bio-luminescente neon

       // --- NOVOS ATRIBUTOS DE SISTEMA DE COMBATE ---
    [Export] public float MaxHealth = 100f;
    private float _currentHealth;
    private float _damageFlashTimer = 0f;

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

    public override void _Ready()
    {
        // Busca segura pelo Player na árvore de nós da cena principal
        _player = GetTree().GetFirstNodeInGroup("Player") as Node2D;
        _currentHealth = MaxHealth;
        _originalColor = NeonColor;
        _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        UpdateDamageFlash(dt);

        // 1. IA de Perseguição Simples
        if (_player != null)
        {
            Vector2 direction = (_player.GlobalPosition - GlobalPosition).Normalized();
            Velocity = direction * Speed;
            MoveAndSlide();
        }

        // 2. Animação Orgânica (Pulsação Trigonométrica)
        _currentBranchAngle = 0.4f + Mathf.Sin(_time * 4.0f) * 0.15f;

        // 3. Redesenha só na taxa configurada, não a cada tick de física
        _redrawTimer -= dt;
        if (_redrawTimer <= 0f)
        {
            _redrawTimer = 1f / Mathf.Max(RedrawsPerSecond, 1f);
            QueueRedraw();
        }
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
        _currentHealth = MaxHealth;
        _damageFlashTimer = 0f;
        NeonColor = _originalColor;
        _time = 0f;
        Velocity = Vector2.Zero;

        Visible = true;
        SetPhysicsProcess(true);
        if (_collisionShape != null)
        {
            _collisionShape.Disabled = false;
        }
    }

    public void DeactivateToPool()
    {
        Visible = false;
        SetPhysicsProcess(false);
        Velocity = Vector2.Zero;
        if (_collisionShape != null)
        {
            _collisionShape.Disabled = true;
        }
    }
}
