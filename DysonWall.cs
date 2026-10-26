using Godot;

// Parede de fronteira com o espaço (spec dyson-wall.md): membrana viva de triângulos de Sierpinski que, irritada
// pela luz, vai abrir uma fenda para o vácuo (fase B). Fase A, passo 1: geometria, colisão e desenho parado.
// A parede monta a geometria (DysonWallGeometry, classe pura) e a entrega a quem desenha (IDysonWallView).
// Ela não entra sozinha no grupo Walls: quem a monta decide (na cena de teste, o dev; na fase, o construtor,
// que deixa as paredes do nó de entrada inertes até liberar as saídas).
public partial class DysonWall : StaticBody2D
{
    [Export] public float Length = 1920f;      // Comprimento (px), no eixo X local, com o pivô no centro
    [Export] public float WallThickness = 40f; // Espessura (px) = altura dos triângulos; 40 como as paredes da fase (o protótipo usa 44)
    [Export(PropertyHint.Range, "0,3")] public int SierpinskiLevel = 2; // Cada peça vira 3^nível sub-triângulos

    [ExportGroup("Visual provisório (arte na etapa 2)")]
    [Export] public Color MembraneColor = Color.Color8(150, 74, 62); // #964A3E: tecido atrás do fractal (fecha os vãos)
    [Export] public Color PieceColorA = Color.Color8(92, 58, 76);    // #5C3A4C e #6C4658: cores base das peças,
    [Export] public Color PieceColorB = Color.Color8(108, 70, 88);   // alternando pela paridade

    private DysonWallGeometry _geometry;
    private IDysonWallView _view; // Quem desenha (hoje, a W1)

    public override void _Ready()
    {
        _geometry = new DysonWallGeometry(Length, WallThickness, SierpinskiLevel, (int)GD.Randi());
        CreateCollision();
        CreateView();
    }

    // Colisão fixa: um retângulo do tamanho da parede. A deformação das peças é só visual (spec, seção 5).
    private void CreateCollision()
    {
        AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(Length, WallThickness) } });
    }

    // O único ponto que conhece a W1: para usar a W2, troca-se o nó criado aqui
    private void CreateView()
    {
        var view = new DysonWallView { Name = "View" };
        AddChild(view);
        _view = view;
        _view.Build(_geometry, new DysonWallPalette(MembraneColor, PieceColorA, PieceColorB));
    }
}
