shader_type canvas_item;

// Quantas "células" de estrela cabem na largura/altura do retângulo (mais = estrelas menores e mais numerosas)
uniform float star_density : hint_range(50.0, 400.0) = 180.0;
// Velocidade do piscar das estrelas
uniform float twinkle_speed : hint_range(0.0, 5.0) = 1.5;
// Cor de fundo do "espaço" por trás das estrelas
uniform vec4 sky_color : source_color = vec4(0.01, 0.02, 0.05, 1.0);

// Gera um número pseudo-aleatório a partir de uma coordenada 2D (sem precisar de textura de ruído).
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

void fragment() {
    vec2 grid_uv = UV * star_density;
    vec2 cell_id = floor(grid_uv);
    vec2 cell_local = fract(grid_uv);

    float star_chance = hash(cell_id);
    vec3 color = sky_color.rgb;

    // Só uma fração das células vira estrela. Baixe o 0.94 pra ter mais estrelas, suba pra ter menos.
    if (star_chance > 0.94) {
        float dist = distance(cell_local, vec2(0.5));
        float twinkle = 0.6 + 0.4 * sin(TIME * twinkle_speed + star_chance * 30.0);
        float star_glow = smoothstep(0.15, 0.0, dist) * twinkle;
        color += vec3(star_glow);
    }

    COLOR = vec4(color, 1.0);
}
