using Godot;
using System;
using System.Collections.Generic;

public partial class LightDamageArea : Area2D
{
    [Export] public float DamagePerSecond = 35f;       // Dano contínuo da luz básica
    [Export] public float WallIrritationPerSecond = 20f; // Quanto a luz "irrita" as paredes sensíveis por segundo

    // Listas dinâmicas para rastreamento de alvos: o que a luz fere (inimigos, spawners) e as paredes que ela irrita
    private readonly List<ILightDamageable> _targetsInLight = new();
    private readonly List<Node2D> _wallsInLight = new();

    // Formato do feixe, para o peso da luz em cada ponto (BeamWeightAt; spec dyson-wall.md, decisão A2)
    private CollisionPolygon2D _beamShape;
    private Vector2[] _beamPolygon = Array.Empty<Vector2>(); // Cópia dos vértices (ler do motor a cada pergunta custaria caro)
    private float _upperEdgeAngle;                           // Ângulo da borda de cada lado do eixo (rad): negativo em cima,
    private float _lowerEdgeAngle;                           // positivo embaixo
    private Transform2D _globalToBeam;                       // Do mundo para o feixe, lida do motor uma vez por passo de física
    private ulong _globalToBeamFrame = ulong.MaxValue;

    public override void _Ready()
    {
        // Conecta os sinais de colisão fisicamente de forma programática
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        CacheBeamShape();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        // 1. Aplica dano contínuo a tudo o que a luz fere (anticorpos e spawners)
        for (int i = _targetsInLight.Count - 1; i >= 0; i--)
        {
            var target = _targetsInLight[i];
            if (GodotObject.IsInstanceValid(target as GodotObject))
            {
                target.TakeDamage(DamagePerSecond * dt);
            }
            else
            {
                // Remove da lista se o alvo foi destruído
                _targetsInLight.RemoveAt(i);
            }
        }

        // 2. Aplica irritabilidade localizada nas paredes biológicas sob a luz
        for (int i = _wallsInLight.Count - 1; i >= 0; i--)
        {
            var wall = _wallsInLight[i];
            if (GodotObject.IsInstanceValid(wall))
            {
                IrritateWall(wall, WallIrritationPerSecond * dt);
            }
            else
            {
                _wallsInLight.RemoveAt(i);
            }
        }
    }

    // Parede de fronteira (ILightIrritable): recebe a lanterna e pergunta o peso da luz em cada peça.
    // WallSensitive (transitório): recebe a parede inteira, pelo nome do método, como antes.
    private void IrritateWall(Node2D wall, float amount)
    {
        if (wall is ILightIrritable irritable)
        {
            irritable.AddLightIrritation(amount, this);
            return;
        }
        if (wall.HasMethod("AddIrritation"))
        {
            wall.Call("AddIrritation", amount, GlobalPosition);
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        // Detecta se o objeto que entrou é algo que a luz fere (pela interface: não importa o tipo)
        if (body is ILightDamageable target)
        {
            if (!_targetsInLight.Contains(target))
            {
                _targetsInLight.Add(target);
            }
        }
        // Detecta se é uma parede celular sensível pelo grupo da Godot
        else if (body.IsInGroup("Walls"))
        {
            if (!_wallsInLight.Contains(body))
            {
                _wallsInLight.Add(body);
            }
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is ILightDamageable target)
        {
            _targetsInLight.Remove(target);
        }
        else if (body.IsInGroup("Walls"))
        {
            _wallsInLight.Remove(body);
        }
    }

    // Peso da luz num ponto do mundo (spec dyson-wall.md, decisão A2): 0 fora do feixe; dentro, 1 no eixo (para onde a
    // lanterna aponta), caindo até 0 na borda, pelo ângulo. Quem conhece o formato do feixe é a lanterna; a parede só pergunta.
    public float BeamWeightAt(Vector2 globalPoint)
    {
        if (_beamPolygon.Length < 3) return 0f; // Sem o formato do feixe

        Vector2 local = GlobalToBeam() * globalPoint;
        if (!Geometry2D.IsPointInPolygon(local, _beamPolygon)) return 0f;

        float angle = local.Angle();
        float edgeAngle = angle < 0f ? _upperEdgeAngle : _lowerEdgeAngle;
        if (Mathf.IsZeroApprox(edgeAngle)) return 1f; // Feixe sem abertura desse lado
        return Mathf.Clamp(1f - angle / edgeAngle, 0f, 1f);
    }

    // Guarda o formato do feixe uma vez: os vértices e o ângulo das bordas, medido a partir do eixo (o X local)
    private void CacheBeamShape()
    {
        _beamShape = GetNodeOrNull<CollisionPolygon2D>("CollisionPolygon2D");
        if (_beamShape == null)
        {
            GD.PushWarning("[LightDamageArea]: sem o nó 'CollisionPolygon2D'; o peso da luz será 0 e as paredes de fronteira não irritam.");
            return;
        }

        _beamPolygon = _beamShape.Polygon;
        foreach (Vector2 vertex in _beamPolygon)
        {
            float angle = vertex.Angle();
            _upperEdgeAngle = Mathf.Min(_upperEdgeAngle, angle);
            _lowerEdgeAngle = Mathf.Max(_lowerEdgeAngle, angle);
        }
    }

    // O feixe se move com o jogador e a mira; a transformação é lida do motor na primeira pergunta de cada passo
    // (como a "foto" do FractalSwarm) e reaproveitada pelas outras peças
    private Transform2D GlobalToBeam()
    {
        ulong frame = Engine.GetPhysicsFrames();
        if (frame == _globalToBeamFrame) return _globalToBeam;

        _globalToBeamFrame = frame;
        _globalToBeam = _beamShape.GlobalTransform.AffineInverse();
        return _globalToBeam;
    }
}
