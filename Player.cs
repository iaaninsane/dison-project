using Godot;
using System;

public partial class Player : CharacterBody2D
{
    [Export] public float ThrustForce = 450f; //default 150
    [Export] public float MaxSpeed = 600f;
    [Export] public float Friction = 0.1f;

    // --- CONTROLE DE MODO DE TIRO/MIRA ---
    [Export] public bool IsManualFire { get; set; } = true; // true = Manual (Mouse), false = Automático


    private Marker2D _flashlightAnchor;

    // Variável de controle para saber o último input usado
    private bool _isUsingGamepad = false;

    // --- ATRIBUTOS DE EJEÇÃO ---
    public bool IsEjected { get; private set; } = false;
    private float _ejectionTimer = 0f;
    private float _ejectionDuration = 0.8f; // Tempo de perda de controle (em segundos)

    public override void _Ready()
    {
        _flashlightAnchor = GetNode<Marker2D>("FlashlightAnchor");
    }

    // Listener de Input para alternar o modo de controle dinamicamente
    public override void _Input(InputEvent @event)
    {
        // Se mover o analógico ou apertar um botão do controle, muda para modo Joystick
        if (@event is InputEventJoypadButton || @event is InputEventJoypadMotion)
        {
            _isUsingGamepad = true;
        }
        // Se mexer o mouse ou apertar o teclado, muda para modo Teclado/Mouse
        else if (@event is InputEventMouseButton || @event is InputEventKey || @event is InputEventMouseMotion)
        {
            _isUsingGamepad = false;
        }
    }

    public override void _PhysicsProcess(double delta)
    {

        float dt = (float)delta;
        Vector2 currentVelocity = Velocity;

        // 1. GERENCIAMENTO DE ESTADO: EJEÇÃO VS. MOVIMENTAÇÃO NORMAL
        if (IsEjected)
        {
            // Reduz o temporizador de atordoamento/ejeção
            _ejectionTimer -= dt;
            if (_ejectionTimer <= 0f)
            {
                IsEjected = false;

                // Reativa a colisão física para que ele volte a colidir normalmente com outras estruturas
                SetCollisionMaskValue(1, true);

            }

            // Durante a ejeção, o jogador perde o controle do WASD/Analógico.
            // Ele apenas desliza descontroladamente, reduzindo a velocidade pela fricção linear do espaço.
            currentVelocity = currentVelocity.MoveToward(Vector2.Zero, Friction * ThrustForce * dt);
        }

        else
        {
            // MOVIMENTAÇÃO NA GRAVIDADE ZERO (Analógico Esquerdo ou WASD)
            Vector2 inputDirection = Input.GetVector("move_left", "move_right", "move_up", "move_down");

            if (inputDirection != Vector2.Zero)
            {
                // Adiciona aceleração com empuxo
                currentVelocity += inputDirection * ThrustForce * dt;
                currentVelocity = currentVelocity.LimitLength(MaxSpeed);
            }

            else
            {
                // Aplica a inércia espacial deslizando suavemente
                currentVelocity = currentVelocity.MoveToward(Vector2.Zero, Friction * ThrustForce * dt);
            }
        }

        // Aplica a velocidade calculada e processa as colisões físicas na Godot
        Velocity = currentVelocity;
        MoveAndSlide();

        // 2. CONTROLE DA LANTERNA (Analógico Direito ou Mouse)
        // Deixamos isso fora do bloco condicional para que o jogador ainda possa mirar e iluminar o pânico ao redor enquanto é ejetado!
        if (_flashlightAnchor != null)
        {
            if (IsManualFire)
            {
                // --- MODO MANUAL (Mira Direta) ---
                if (_isUsingGamepad)
                {
                    // Obtém a direção do analógico direito
                    Vector2 aimDirection = Input.GetVector("aim_left", "aim_right", "aim_up", "aim_down");

                    // Só rotaciona se o analógico estiver sendo empurrado (evita resetar a mira para o ângulo 0)
                    if (aimDirection.Length() > 0.1f)
                    {
                        _flashlightAnchor.Rotation = aimDirection.Angle();
                    }
                }
                else
                {
                    // Lógica clássica para mirar com o ponteiro do Mouse
                    Vector2 mousePosition = GetGlobalMousePosition();
                    _flashlightAnchor.LookAt(mousePosition);
                }
            }
            else
            {
                // --- MODO AUTOMÁTICO (Foco na Navegação em Zero-G) ---
                // A lanterna aponta automaticamente para a direção do vetor de velocidade atual
                if (Velocity.Length() > 10f)
                {
                    _flashlightAnchor.Rotation = Velocity.Angle();
                }
                else
                {
                    // Se o Player estiver parado, alinha a lanterna com a intenção do analógico/WASD de movimento
                    Vector2 moveInput = Input.GetVector("move_left", "move_right", "move_up", "move_down");
                    if (moveInput.Length() > 0.1f)
                    {
                        _flashlightAnchor.Rotation = moveInput.Angle();
                    }
                }
            }
        }
    }

    public void ApplyEjection(Vector2 impulse)
    {
        IsEjected = true;
        _ejectionTimer = _ejectionDuration;

        // Atribui a velocidade de ejeção diretamente à física newtoniana do jogador
        Velocity = impulse;

        // Desativa temporariamente a colisão com as paredes (ex: se as paredes estão na Layer 1)
        // Isso faz com que o jogador "afunde" no tecido biológico da parede
        SetCollisionMaskValue(1, false);
    }
}
