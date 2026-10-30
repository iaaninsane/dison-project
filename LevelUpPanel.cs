using Godot;
using System.Collections.Generic;

// Menu de melhorias da incursão (spec level-up.md): abre com upgrade_menu quando há pontos, pausa o jogo e mostra as
// cartas da escolha atual (IncursionUpgrades); escolher uma gasta 1 ponto. Fica num CanvasLayer do player.tscn.
public partial class LevelUpPanel : PanelContainer
{
    private const string MenuAction = "upgrade_menu";

    [Export] public IncursionUpgrades Upgrades;  // Cartas e escolha (ligado no Inspector)
    [Export] public SuitUpgradeApplier Applier;  // Valores antes → depois de cada carta (ligado no Inspector)
    [Export] public Label PointsLabel;           // "Pontos: N"
    [Export] public Container CardList;          // Onde os botões das cartas são criados
    [Export] public Label MaxedLabel;            // "Traje no máximo", quando não há o que escolher
    [Export] public Button CloseButton;          // Fecha sem escolher (o ponto fica guardado)
    [Export] public float ChoiceLockTime = 0.3f; // Depois de cada escolha, as cartas novas ignoram cliques por este tempo (s)

    private readonly List<Button> _cards = new(); // Criados conforme a necessidade e reaproveitados
    private ulong _unlockAtMsec;                  // Antes disto (relógio real, que a pausa não para), as cartas ignoram cliques
    private bool _isPlayerDead;

    public override void _Ready()
    {
        Visible = false;
        ProcessMode = ProcessModeEnum.Always; // Recebe entrada com o jogo pausado
        if (CloseButton != null) CloseButton.Pressed += Close;
    }

    // Ao sair da cena aberto, nunca deixa o jogo pausado
    public override void _ExitTree()
    {
        if (Visible) GetTree().Paused = false;
    }

    // Conectado pelo editor ao sinal Died do SuitIntegrity: morto não escolhe melhorias
    public void OnPlayerDied() => _isPlayerDead = true;

    // _Input, e não _UnhandledInput: com uma carta em foco, a Godot usa o Tab para trocar o foco e não o repassa
    public override void _Input(InputEvent @event)
    {
        if (!@event.IsActionPressed(MenuAction)) return; // Sem eco: segurar a tecla não abre e fecha sem parar
        if (!Visible && !CanOpen()) return;

        if (Visible) Close();
        else Open();
        GetViewport().SetInputAsHandled();
    }

    // Esc ou o B do controle também fecham, como no painel do banco
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (!@event.IsActionPressed("ui_cancel")) return;

        Close();
        GetViewport().SetInputAsHandled();
    }

    private bool CanOpen()
    {
        if (Upgrades == null || Upgrades.Points <= 0) return false; // Sem ponto, nada acontece (L7)
        if (_isPlayerDead) return false;
        if (GetTree().Paused) return false;                         // Outro painel já pausou o jogo
        return !ScreenFade.IsChangingScene(this);                   // A pausa congelaria o escurecer da troca de cena
    }

    private void Open()
    {
        Visible = true;
        GetTree().Paused = true;
        ShowChoice();
    }

    private void Close()
    {
        Visible = false;
        GetTree().Paused = false;
    }

    // Mostra a escolha atual: os pontos e as cartas (as mesmas até gastar o ponto) ou "Traje no máximo"
    private void ShowChoice()
    {
        if (PointsLabel != null) PointsLabel.Text = $"Pontos: {Upgrades.Points}";

        IReadOnlyList<SuitUpgrade> offers = Upgrades.GetOffers();
        EnsureCards(offers.Count);
        for (int i = 0; i < _cards.Count; i++) ShowCard(_cards[i], i < offers.Count ? offers[i] : null);
        if (MaxedLabel != null) MaxedLabel.Visible = offers.Count == 0;

        Button focus = offers.Count > 0 ? _cards[0] : CloseButton;
        focus?.GrabFocus(); // Foco para teclado e controle
    }

    // Cria os botões que faltam; cada um lembra a sua posição na lista, e a carta é lida na hora do clique
    private void EnsureCards(int count)
    {
        if (CardList == null) return;

        while (_cards.Count < count)
        {
            int index = _cards.Count;
            var card = new Button { Alignment = HorizontalAlignment.Left };
            card.Pressed += () => OnCardPressed(index);
            CardList.AddChild(card);
            _cards.Add(card);
        }
    }

    private void ShowCard(Button card, SuitUpgrade upgrade)
    {
        card.Visible = upgrade != null;
        if (upgrade == null) return;
        card.Text = DescribeCard(upgrade);
    }

    private void OnCardPressed(int index)
    {
        if (Time.GetTicksMsec() < _unlockAtMsec) return; // Clique repetido logo depois de escolher

        IReadOnlyList<SuitUpgrade> offers = Upgrades.GetOffers();
        if (index >= offers.Count) return;
        if (!Upgrades.TryChoose(offers[index])) return;

        _unlockAtMsec = Time.GetTicksMsec() + (ulong)(Mathf.Max(ChoiceLockTime, 0f) * 1000f);
        if (Upgrades.Points <= 0)
        {
            Close(); // Último ponto: o jogo volta (L9)
            return;
        }
        ShowChoice(); // Pontos sobrando: um sorteio novo
    }

    // Nome e nível (guardado de 0 a 4; na tela, de 1 a 5), descrição e efeito
    private string DescribeCard(SuitUpgrade upgrade)
    {
        int level = Upgrades.GetLevel(upgrade);
        string text = $"{upgrade.DisplayName}  —  Nível {level + 1} → {level + 2}";
        if (!string.IsNullOrEmpty(upgrade.Description)) text += $"\n{upgrade.Description}";
        if (Applier != null) text += $"\n{DescribeEffect(upgrade)}";
        return text;
    }

    // O efeito antes → depois: em % do valor-base nas melhorias em porcentagem; em número nas fixas
    private string DescribeEffect(SuitUpgrade upgrade)
    {
        var (before, after, baseValue) = Applier.PreviewIncursionLevelUp(upgrade);
        if (upgrade.ValueMode == SuitUpgrade.BonusMode.Percent && baseValue > 0f)
            return $"{before / baseValue * 100f:0}% → {after / baseValue * 100f:0}%";
        return $"{before:0} → {after:0}";
    }
}
