using Godot;

// Acompanha em qual estrutura da fase o jogador está e avisa por sinal quando ela muda (Observer).
// Fica como filho da raiz da hex_phase.tscn. Também aplica a regra provisória do espaço (spec hex-phase.md, seção 10).
public partial class StructureTracker : Node
{
    [Signal] public delegate void StructureChangedEventHandler(string structureName);
    [Signal] public delegate void LostInSpaceEventHandler(); // Ficou no espaço por SpaceReturnDelay

    [Export] public HexPhaseBuilder Phase;           // A fase (geometria e jogador), ligada no Inspector
    [Export] public float SpaceReturnDelay = 2f;     // Provisório: tempo no espaço até voltar à eclusa (s)

    private const string SpaceName = "Espaço";

    public HexStructure Current { get; private set; } // null = espaço
    private bool _hasReported = false;               // Garante o primeiro aviso, mesmo começando no espaço
    private float _timeInSpace = 0f;

    // O Layout da fase só existe depois do _Ready da raiz (os filhos ficam prontos antes do pai),
    // por isso tudo acontece no passo de física, com guarda.
    public override void _PhysicsProcess(double delta)
    {
        if (Phase?.Layout == null || Phase.Player == null) return;

        UpdateCurrentStructure();
        UpdateTimeInSpace((float)delta);
    }

    private void UpdateCurrentStructure()
    {
        Vector2 pointInPhase = Phase.ToLocal(Phase.Player.GlobalPosition);
        HexStructure found = Phase.Layout.FindStructureAt(pointInPhase);
        if (_hasReported && found == Current) return; // Só avisa quando muda

        Current = found;
        _hasReported = true;
        EmitSignal(SignalName.StructureChanged, found?.Name ?? SpaceName);
    }

    // Regra provisória, até a consequência da ejeção ser decidida: no espaço por SpaceReturnDelay, avisa
    private void UpdateTimeInSpace(float dt)
    {
        if (Current != null || Phase.Player.IsDead)
        {
            _timeInSpace = 0f;
            return;
        }

        _timeInSpace += dt;
        if (_timeInSpace < SpaceReturnDelay) return;

        _timeInSpace = 0f;
        EmitSignal(SignalName.LostInSpace);
    }
}
