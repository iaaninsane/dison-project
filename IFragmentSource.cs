using Godot;

// De onde vêm os fragmentos de um inimigo que se divide (backlog 1.8): o estoque da fase (EnemyPool) cumpre este
// contrato e reaproveita nós guardados, sem criar nada no meio do jogo. O inimigo sem fonte instancia a própria cena.
public interface IFragmentSource
{
    void SpawnFragment(Vector2 globalPosition, int generation); // Põe vivo um fragmento desta geração neste ponto
}
