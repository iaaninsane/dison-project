using Godot;
using System.Collections.Generic;

// Estoque de inimigos da fase (spec mobile-spawners.md), compartilhado pelos spawners móveis: object pooling, como no
// NinhoSpawnerPooled. Também guarda o orçamento: no máximo PoolSize inimigos da geração 0 vivos e MaxTotalEnemies no
// total (com os fragmentos). Fica num nó parado: os inimigos são filhos dele, e os fragmentos nascem no mesmo pai, em
// coordenadas locais (um pai que andasse levaria a horda junto).
public partial class EnemyPool : Node2D
{
    [Export] public PackedScene EnemyScene;        // fractal_enemy.tscn (ligada no Inspector)
    [Export] public int PoolSize = 60;             // Inimigos da geração 0 vivos na fase toda, no máximo
    [Export] public int MaxTotalEnemies = 350;     // Teto de inimigos vivos contando fragmentos (orçamento de física)

    private readonly List<FractalEnemy> _stock = new();

    // Cria o estoque inteiro ao carregar a fase (escondido pelo escurecer), e não no meio do jogo
    public override void _Ready()
    {
        if (EnemyScene == null)
        {
            GD.PushWarning("[EnemyPool]: ligue a Enemy Scene (fractal_enemy.tscn) no Inspector.");
            return;
        }

        for (int i = 0; i < PoolSize; i++)
        {
            var enemy = EnemyScene.Instantiate<FractalEnemy>();
            AddChild(enemy);
            enemy.DeactivateToPool();
            _stock.Add(enemy);
        }
    }

    // Tira um inimigo do estoque e o põe vivo em `globalPosition`. Devolve false se o estoque está vazio ou a fase já
    // está no teto de física: quem pediu espera (não é erro)
    public bool TrySpawn(Vector2 globalPosition)
    {
        if (_stock.Count == 0) return false;
        if (FractalSwarm.Count >= MaxTotalEnemies) return false;

        int last = _stock.Count - 1; // Retira o último (remoção O(1); a ordem não importa)
        FractalEnemy enemy = _stock[last];
        _stock.RemoveAt(last);
        enemy.ActivateAt(globalPosition);
        enemy.OnDied = ReturnToStock; // Ao morrer, volta para cá em vez de ser destruído
        return true;
    }

    private void ReturnToStock(FractalEnemy enemy)
    {
        enemy.DeactivateToPool();
        _stock.Add(enemy);
    }
}
