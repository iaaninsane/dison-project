using Godot;
using System.Collections.Generic;

// Painel de compra do banco: monta um botão por melhoria do catálogo, mostra os créditos e pausa o jogo enquanto aberto.
public partial class UpgradePanel : PanelContainer
{
    [Export] public UpgradeCatalog Catalog;  // Lista de melhorias (o .tres da pasta upgrades)
    [Export] public Label BankLabel;         // Mostra o saldo do banco (créditos)
    [Export] public Container UpgradeList;   // Onde os botões das melhorias são criados
    [Export] public Button CloseButton;      // Fecha o painel

    private readonly Dictionary<SuitUpgrade, Button> _buyButtons = new();

    public override void _Ready()
    {
        Visible = false;
        ProcessMode = ProcessModeEnum.Always; // Continua funcionando com o jogo pausado
        BuildButtons();
        if (CloseButton != null) CloseButton.Pressed += Close;

        if (GameState.Instance == null) return;
        GameState.Instance.BankChanged += OnBankChanged;
        GameState.Instance.UpgradeLevelChanged += OnUpgradeLevelChanged;
    }

    // Par simétrico: o painel é da cena, o GameState é global; ao sair, desconecta e nunca deixa o jogo pausado
    public override void _ExitTree()
    {
        if (Visible) GetTree().Paused = false;
        if (GameState.Instance == null) return;
        GameState.Instance.BankChanged -= OnBankChanged;
        GameState.Instance.UpgradeLevelChanged -= OnUpgradeLevelChanged;
    }

    public void Open()
    {
        Refresh();
        Visible = true;
        GetTree().Paused = true;
        CloseButton?.GrabFocus(); // Foco inicial para teclado e controle (seguro: não compra nada sem querer)
    }

    private void Close()
    {
        Visible = false;
        GetTree().Paused = false;
    }

    // Esc (ou o botão de cancelar do controle) também fecha
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (!@event.IsActionPressed("ui_cancel")) return;

        Close();
        GetViewport().SetInputAsHandled();
    }

    // Um botão por melhoria do catálogo: o texto mostra nome, nível e preço
    private void BuildButtons()
    {
        if (Catalog == null || UpgradeList == null) return;

        foreach (SuitUpgrade upgrade in Catalog.Upgrades)
        {
            if (upgrade == null) continue;
            var button = new Button();
            button.Pressed += () => GameState.Instance?.TryBuyUpgrade(upgrade);
            UpgradeList.AddChild(button);
            _buyButtons[upgrade] = button;
        }
    }

    // Atualiza o banco e todos os botões com o estado atual
    private void Refresh()
    {
        GameState state = GameState.Instance;
        if (state == null) return;

        if (BankLabel != null) BankLabel.Text = $"Créditos: {state.Data.BankedCredits}";

        foreach (var (upgrade, button) in _buyButtons)
        {
            int level = state.GetUpgradeLevel(upgrade);
            int cost = upgrade.GetCostForNextLevel(level);
            string price = cost < 0 ? "MÁXIMO" : $"comprar por {cost}";
            button.Text = $"{upgrade.DisplayName}  —  nível {level}/{upgrade.MaxLevel}  —  {price}";
            button.Disabled = !state.CanBuy(upgrade);
        }
    }

    private void OnBankChanged(int banked) => Refresh();
    private void OnUpgradeLevelChanged(string upgradeId, int level) => Refresh();
}
