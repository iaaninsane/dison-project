using Godot;

// Câmera da fase hexagonal: limites da fase, zoom por tipo de estrutura e câmera no lugar (sem deslizar) quando o
// jogador é posto na eclusa. Afasta nas arenas e aproxima nos corredores: compressão e alívio (spec hex-phase.md).
// Fica como filho da raiz da hex_phase.tscn e usa o Camera2D do jogador.
public partial class PhaseCamera : Node
{
    [Export] public HexPhaseBuilder Phase;            // A fase (ligada no Inspector)
    [Export] public StructureTracker Tracker;         // Quem avisa a troca de estrutura (ligado no Inspector)
    [Export] public float ArenaZoom = 0.7f;           // Nós e painéis. Menor que 1 = câmera mais longe (vê mais)
    [Export] public float CorridorZoom = 1.2f;        // Conectores de nós. Maior que 1 = câmera mais perto
    [Export] public float PlateConnectorZoom = 1.3f;  // Conectores de placa (os mais estreitos)
    [Export] public float ZoomTransitionTime = 0.8f;  // Duração da troca de zoom (s)
    [Export] public float CameraMargin = 600f;        // Quanto a câmera pode mostrar além da borda da fase (px)

    private Camera2D _camera;
    private Tween _zoomTween;
    private bool _hasZoom = false; // O primeiro zoom (ao nascer) é aplicado na hora, sem transição

    // Os avisos vêm de nós da própria cena; ligar por código usa as mesmas referências do Inspector (um fio só)
    public override void _Ready()
    {
        if (Phase != null) Phase.PlayerPlaced += OnPlayerPlaced;
        if (Tracker != null) Tracker.StructureChanged += OnStructureChanged;
    }

    // Par simétrico do _Ready
    public override void _ExitTree()
    {
        if (Phase != null) Phase.PlayerPlaced -= OnPlayerPlaced;
        if (Tracker != null) Tracker.StructureChanged -= OnStructureChanged;
    }

    // O jogador foi posto na eclusa (ao nascer ou voltando do espaço): limites da fase e câmera no lugar
    private void OnPlayerPlaced()
    {
        Camera2D camera = GetCamera();
        if (camera == null) return;

        ApplyLimits(camera);
        camera.ResetSmoothing(); // Sem isso a câmera "desliza" da posição antiga até o jogador
    }

    private void OnStructureChanged(string structureName)
    {
        HexStructure current = Tracker.Current;
        if (current == null) return; // No espaço, mantém o zoom de onde veio

        Camera2D camera = GetCamera();
        if (camera == null) return;

        SetZoom(camera, ZoomFor(current.Kind));
    }

    private float ZoomFor(HexStructureKind kind) => kind switch
    {
        HexStructureKind.NodeConnector => CorridorZoom,
        HexStructureKind.PlateConnector => PlateConnectorZoom,
        _ => ArenaZoom, // Nós e painéis
    };

    private void SetZoom(Camera2D camera, float zoom)
    {
        _zoomTween?.Kill(); // Uma troca nova interrompe a que estava em andamento

        if (!_hasZoom)
        {
            camera.Zoom = Vector2.One * zoom;
            _hasZoom = true;
            return;
        }

        _zoomTween = CreateTween();
        _zoomTween.TweenProperty(camera, "zoom", Vector2.One * zoom, ZoomTransitionTime)
                  .SetTrans(Tween.TransitionType.Sine)
                  .SetEase(Tween.EaseType.InOut);
    }

    private void ApplyLimits(Camera2D camera)
    {
        Rect2 area = Phase.Layout.Bounds.Grow(CameraMargin);
        Vector2 topLeft = Phase.ToGlobal(area.Position);
        Vector2 bottomRight = Phase.ToGlobal(area.End);
        camera.LimitLeft = Mathf.FloorToInt(topLeft.X);
        camera.LimitTop = Mathf.FloorToInt(topLeft.Y);
        camera.LimitRight = Mathf.CeilToInt(bottomRight.X);
        camera.LimitBottom = Mathf.CeilToInt(bottomRight.Y);
    }

    private Camera2D GetCamera()
    {
        if (_camera != null) return _camera;

        _camera = Phase?.Player?.GetNodeOrNull<Camera2D>("Camera2D");
        if (_camera == null) GD.PushWarning("[PhaseCamera]: o jogador não tem um Camera2D chamado 'Camera2D'.");
        return _camera;
    }
}
