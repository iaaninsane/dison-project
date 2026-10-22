using Godot;

// Transição de tela entre cenas (spec hex-phase.md, H5): quem chega clareia; quem sai escurece e só então troca de cena.
// Fica no player.tscn (o jogador está em todas as salas), sem autoload: cada cena traz a sua.
public partial class ScreenFade : CanvasLayer
{
    private const string GroupName = "ScreenFade";
    private const float MaxFrameStep = 1f / 30f; // Passo máximo por quadro (s): o quadro longo da montagem da fase não faz o clarear "pular"

    [Export] public float ScreenFadeDuration = 0.4f; // Duração do escurecer e do clarear (s)

    private ColorRect _cover;        // Cobre a tela inteira; a transparência dele é a transição
    private float _targetAlpha = 0f; // 0 = clarear (quem chega), 1 = escurecer (quem sai)
    private string _nextScenePath;   // Cena a abrir quando a tela ficar preta; null = nenhuma troca pedida

    public override void _Ready()
    {
        AddToGroup(GroupName);
        _cover = CreateCover();
        AddChild(_cover); // Começa opaco, com o _Process ligado: quem chega clareia
    }

    // Só roda durante a transição; ao terminar, dorme (e troca de cena, se for o caso)
    public override void _Process(double delta)
    {
        StepCover((float)delta);
        if (!Mathf.IsEqualApprox(_cover.Modulate.A, _targetAlpha)) return;

        SetProcess(false);
        if (_nextScenePath != null) ChangeSceneNow();
    }

    // Quem pede a troca não conhece o fade: procura o da cena atual pelo grupo.
    // Cena sem fade: troca direto, adiado (fora de callbacks de física), como o DoorTrigger fazia.
    public static void ChangeScene(Node caller, string scenePath)
    {
        SceneTree tree = caller.GetTree();
        if (tree.GetFirstNodeInGroup(GroupName) is ScreenFade fade)
        {
            fade.FadeOutAndChangeScene(scenePath);
            return;
        }
        tree.CallDeferred(SceneTree.MethodName.ChangeSceneToFile, scenePath);
    }

    // Quem sai escurece; a troca acontece no _Process, quando a tela fica preta
    public void FadeOutAndChangeScene(string scenePath)
    {
        if (_nextScenePath != null) return; // Já está saindo (trava contra pedido duplo)

        _nextScenePath = scenePath;
        _targetAlpha = 1f;
        SetProcess(true);
    }

    private void StepCover(float delta)
    {
        float step = Mathf.Min(delta, MaxFrameStep) / Mathf.Max(ScreenFadeDuration, 0.01f);
        Color modulate = _cover.Modulate;
        modulate.A = Mathf.MoveToward(modulate.A, _targetAlpha, step);
        _cover.Modulate = modulate;
    }

    private void ChangeSceneNow()
    {
        Error error = GetTree().ChangeSceneToFile(_nextScenePath);
        if (error == Error.Ok) return;

        GD.PushError($"[ScreenFade]: não foi possível abrir a cena '{_nextScenePath}' ({error}).");
        _nextScenePath = null;
        _targetAlpha = 0f; // Não deixa a tela preta para sempre: volta a clarear
        SetProcess(true);
    }

    // Retângulo preto de tela inteira; começa opaco, para o primeiro quadro da cena já sair preto
    private static ColorRect CreateCover()
    {
        var cover = new ColorRect
        {
            Name = "Cover",
            Color = Colors.Black,
            MouseFilter = Control.MouseFilterEnum.Ignore, // Não bloqueia cliques na interface
        };
        cover.SetAnchorsPreset(Control.LayoutPreset.FullRect); // Tela inteira, em qualquer resolução
        return cover;
    }
}
