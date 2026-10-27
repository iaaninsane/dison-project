using Godot;
using System;

public partial class FractalEnemy : CharacterBody2D, ILightDamageable, IContactHazard // A luz fere; encostar fere o traje
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

    // --- GERAÇÕES (divisão celular) ---
    [Export] public int Generation = 0;                     // 0 = original; fragmentos são 1, 2...
    [Export] public float HealthFactorPerGeneration = 0.5f; // Vida multiplicada a cada geração
    [Export] public float ScaleFactorPerGeneration = 0.6f;  // Tamanho multiplicado a cada geração
    [Export] public int DepthLossPerGeneration = 1;         // Níveis da árvore perdidos a cada geração
    [Export] public int MaxGeneration = 2;                  // Geração que morre sem se dividir
    [Export] public int FragmentCount = 2;                  // Fragmentos gerados em cada divisão
    [Export] public float FragmentSpawnOffset = 8f;         // Distância do centro em que cada fragmento nasce (px)
    [Export] public float SpawnInvulnerability = 0.3f;      // Tempo imune a dano ao nascer como fragmento (s)
    private float _invulnerableTimer = 0f;
    private int _drawDepth;                                 // Profundidade da árvore desta geração
    private float _drawBranchLength;                        // Ramo principal desta geração
    private Vector2 _baseCollisionScale;                    // Escala original do nó de colisão (da cena)

    // --- BIOMASSA (recompensa de quem morre sem se dividir) ---
    [Export] public int BiomassPerKill = 1;                 // Biomassa do orbe solto na morte
    [Export] public float BiomassDropScatter = 6f;          // Deslocamento aleatório do orbe ao nascer (px)
    [Export] public float BiomassOrbLifetime = 30f;         // Tempo até um orbe não coletado sumir (s)
    [Export] public int MaxBiomassOrbs = 300;               // Teto de orbes na cena; o mais antigo some primeiro

    // --- RECOLHIMENTO (inimigo longe do jogador sai de cena em silêncio; spec mobile-spawners.md) ---
    [Export] public float DespawnDistance = 0f;             // Longe do jogador mais que isto (px) por DespawnDelay: recolhido (0 = nunca)
    [Export] public float DespawnDelay = 3f;                // Tempo longe antes de recolher (s)
    private float _farTimer = 0f;

    // --- SUCÇÃO (fendas da parede de fronteira; spec dyson-wall.md, 2.6 e seção 5) ---
    [Export] public float SuctionFactor = 1f;          // Quanto este inimigo sente a sucção (EnemySuctionFactor da spec)
    [Export] public float SuctionVelocityDecay = 300f; // Fora do campo, a velocidade ganha da sucção some neste ritmo (px/s²)
    private Vector2 _suctionVelocity;                  // Velocidade própria ganha da sucção (o inimigo não tem inércia)

       // --- NOVOS ATRIBUTOS DE SISTEMA DE COMBATE ---
    [Export] public float MaxHealth = 100f;
    private float _currentHealth;
    private float _damageFlashTimer = 0f;
    private bool _isDead = false; // Garante que a morte aconteça uma única vez (ver Die)
    public bool IsDead => _isDead; // Leitura para quem precisa ignorar inimigos mortos (ex.: PlayerHurtbox)

    // Guardamos a cor original do neon para o efeito de piscar
    private Color _originalColor;

    private Node2D _player;
    private float _time = 0f;
    private float _currentBranchAngle = 0.5f;     // Ângulo de abertura em radianos

    // --- CONTROLE DE TAXA DE REDESENHO (otimização pra hordas grandes) ---
    [Export] public float RedrawsPerSecond = 20f;
    private float _redrawTimer = 0f;

    // --- SUPORTE A OBJECT POOLING (opcional; não afeta o uso sem pooling) ---
    // Se um estoque (pool) assinar este callback, a saída de cena (morte ou recolhimento) avisa em vez de destruir.
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
        _originalColor = NeonColor;
        _collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _baseCollisionScale = _collisionShape?.Scale ?? Vector2.One;
        _tieBreakDirection = ComputeTieBreakDirection();
        ApplyGeneration();
    }

    // Calcula os valores desta geração a partir da configuração base (os [Export] não são alterados)
    private void ApplyGeneration()
    {
        float scale = Mathf.Pow(ScaleFactorPerGeneration, Generation);

        _currentHealth = MaxHealth * Mathf.Pow(HealthFactorPerGeneration, Generation);
        _drawDepth = Mathf.Max(1, RecursionDepth - DepthLossPerGeneration * Generation);
        _drawBranchLength = BaseBranchLength * scale;
        _invulnerableTimer = Generation > 0 ? SpawnInvulnerability : 0f; // só fragmentos nascem imunes

        if (_collisionShape == null) return;
        _collisionShape.Scale = _baseCollisionScale * scale;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (TryDespawn(dt)) return; // Longe do jogador há tempo: saiu de cena
        _time += dt;
        UpdateDamageFlash(dt);
        UpdateInvulnerability(dt);

        // 1. Movimento (com a sucção das fendas); saiu por uma fenda: deixa a fase
        UpdateSuction(dt);
        UpdateMovement(dt);
        if (TryLeaveThroughBreach()) return;

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

        Vector2 desired = ComputeChaseVelocity() + ComputeSeparationVelocity() + _suctionVelocity; // Perseguir + afastar dos vizinhos + sucção
        if (ShouldRest(desired, dt))                                            // Parado: sem MoveAndSlide
        {
            Velocity = Vector2.Zero;
            return;
        }

        Velocity = desired;
        MoveAndSlide();          // Executar o movimento
        SlideSuctionVelocity();
        CheckIfBlocked(desired); // Travou? Começa a descansar
    }

    // A sucção acumula uma velocidade própria, somada à perseguição e à separação (que são recalculadas a cada passo).
    // Dentro do campo, ela cresce e acorda o inimigo (a direção desejada mudou); fora, some aos poucos.
    private void UpdateSuction(float dt)
    {
        Vector2 acceleration = SuctionField.AccelerationAt(GlobalPosition) * SuctionFactor;
        if (acceleration == Vector2.Zero)
        {
            _suctionVelocity = _suctionVelocity.MoveToward(Vector2.Zero, SuctionVelocityDecay * dt);
            return;
        }
        _suctionVelocity += acceleration * dt;
        _restTimer = 0f;
    }

    // Batendo em algo, a parte da velocidade da sucção que vai contra a superfície se perde, como no MoveAndSlide;
    // senão ela se acumularia contra a parede e sairia de uma vez quando o caminho abrisse
    private void SlideSuctionVelocity()
    {
        if (_suctionVelocity == Vector2.Zero) return;

        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            Vector2 normal = GetSlideCollision(i).GetNormal();
            float into = _suctionVelocity.Dot(normal);
            if (into < 0f) _suctionVelocity -= normal * into;
        }
    }

    // Saiu por uma fenda para o espaço: deixa a fase sem se dividir nem soltar biomassa (decisão B4).
    // Devolve true se saiu (o resto do passo não roda)
    private bool TryLeaveThroughBreach()
    {
        if (!SuctionField.HasCrossed(GlobalPosition)) return false;

        LeaveSilently();
        return true;
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
        DrawFractalBranch(Vector2.Zero, Vector2.Up * _drawBranchLength, _drawDepth);
        DrawFractalBranch(Vector2.Zero, Vector2.Down * _drawBranchLength, _drawDepth);
        DrawFractalBranch(Vector2.Zero, Vector2.Left * _drawBranchLength, _drawDepth);
        DrawFractalBranch(Vector2.Zero, Vector2.Right * _drawBranchLength, _drawDepth);
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
        if (_invulnerableTimer > 0f) return; // Recém-nascido: imune por um instante

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

    private void UpdateInvulnerability(float dt)
    {
        if (_invulnerableTimer <= 0f) return;
        _invulnerableTimer -= dt;
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
        bool divided = SpawnFragments(); // Divisão celular (se ainda não for a última geração)
        if (!divided) DropBiomass();     // Quem não se divide solta biomassa
        LeaveScene();
    }

    // Longe do jogador por DespawnDelay: sai de cena sem se dividir nem soltar biomassa (fora da tela, o jogador não vê).
    // Devolve true se saiu (o resto do passo não roda)
    private bool TryDespawn(float dt)
    {
        if (DespawnDistance <= 0f || _player == null) return false;
        if (GlobalPosition.DistanceSquaredTo(_player.GlobalPosition) <= DespawnDistance * DespawnDistance)
        {
            _farTimer = 0f; // Perto: o relógio volta a zero
            return false;
        }

        _farTimer += dt;
        if (_farTimer < DespawnDelay) return false;

        LeaveSilently();
        return true;
    }

    // Sai de cena sem se dividir nem soltar biomassa (recolhimento e saída pela fenda)
    private void LeaveSilently()
    {
        _isDead = true;
        FractalSwarm.Unregister(this);
        LeaveScene();
    }

    // Saída de cena (morte ou recolhimento): volta ao estoque, se houver um esperando, ou sai da árvore
    private void LeaveScene()
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

    // Divisão celular: cria os fragmentos da próxima geração em volta da posição atual.
    // Devolve true se dividiu (quem não se divide solta biomassa no Die)
    private bool SpawnFragments()
    {
        if (Generation >= MaxGeneration) return false; // última geração: morre sem se dividir
        if (FragmentCount <= 0) return false;

        Node parent = GetParent();
        if (parent == null) return false;

        var scene = GD.Load<PackedScene>(SceneFilePath); // a própria cena (a Godot guarda em cache)
        if (scene == null) return false;

        float startAngle = GD.Randf() * Mathf.Tau;  // ângulo aleatório a cada morte
        float angleStep = Mathf.Tau / FragmentCount; // ângulos iguais (2 → opostos)

        for (int i = 0; i < FragmentCount; i++)
        {
            var fragment = scene.Instantiate<FractalEnemy>();
            fragment.Generation = Generation + 1;    // definido ANTES de entrar na árvore
            Vector2 offset = Vector2.Right.Rotated(startAngle + angleStep * i) * FragmentSpawnOffset;
            fragment.Position = Position + offset;   // mesmo pai → mesmo espaço de coordenadas
            parent.CallDeferred(Node.MethodName.AddChild, fragment);
        }
        return true;
    }

    // Recompensa: quem morre sem se dividir (a última geração) solta um orbe de biomassa
    private void DropBiomass()
    {
        Vector2 scatter = Vector2.Right.Rotated(GD.Randf() * Mathf.Tau) * GD.Randf() * BiomassDropScatter;
        BiomassField.Drop(GetParent(), Position + scatter, BiomassPerKill, BiomassOrbLifetime, MaxBiomassOrbs);
    }

    // --- MÉTODOS NOVOS, USADOS SÓ PELO SPAWNER COM POOLING ---

    public void ActivateAt(Vector2 position)
    {
        GlobalPosition = position;
        _isDead = false; // Inimigo reaproveitado do pool volta a estar vivo
        Generation = 0;  // Do pool sempre sai um inimigo original
        ApplyGeneration();
        _damageFlashTimer = 0f;
        NeonColor = _originalColor;
        _time = 0f;
        Velocity = Vector2.Zero;
        _restTimer = 0f;                     // Reaproveitado do pool começa acordado
        _blockedDirection = Vector2.Zero;
        _farTimer = 0f;                      // E com o relógio do recolhimento zerado
        _suctionVelocity = Vector2.Zero;     // E sem a sucção da vida anterior

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
