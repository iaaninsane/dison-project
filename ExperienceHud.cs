using Godot;

// HUD mínimo de teste da experiência (spec level-up.md): nível, barra de XP e pontos, em três indicadores separados.
// Só escuta os sinais do ExperienceTracker (Observer); não conhece o rastreador.
public partial class ExperienceHud : HBoxContainer
{
    [Export] public Label LevelLabel;   // "Nível N" (ligado no Inspector)
    [Export] public ProgressBar XpBar;  // XP do nível ÷ requisito do próximo (ligada no Inspector)
    [Export] public Label PointsLabel;  // "Pontos: N" (ligado no Inspector)

    // Conectado pelo editor ao sinal ExperienceChanged do ExperienceTracker
    public void OnExperienceChanged(int level, int xpInLevel, int xpForNext)
    {
        ShowLevel(level);
        ShowXp(xpInLevel, xpForNext);
    }

    // Conectado pelo editor ao sinal UpgradePointsChanged do ExperienceTracker
    public void OnUpgradePointsChanged(int points)
    {
        if (PointsLabel == null) return;
        PointsLabel.Text = $"Pontos: {points}";
    }

    private void ShowLevel(int level)
    {
        if (LevelLabel == null) return;
        LevelLabel.Text = $"Nível {level}";
    }

    private void ShowXp(int xpInLevel, int xpForNext)
    {
        if (XpBar == null) return;
        XpBar.MaxValue = xpForNext; // O máximo antes do valor: a barra corta o valor no máximo que ela tem na hora
        XpBar.Value = xpInLevel;
    }
}
