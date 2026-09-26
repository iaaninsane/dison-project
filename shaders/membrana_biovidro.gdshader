shader_type canvas_item;

// Cor do "miolo" translúcido da membrana (vidro biológico)
uniform vec4 base_color : source_color = vec4(0.0, 0.898, 1.0, 0.35);
// Cor que aparece nas bordas (borda mais saturada/opaca, efeito de contorno de vidro)
uniform vec4 edge_color : source_color = vec4(0.6, 1.0, 1.0, 0.9);
// Quão concentrada a borda fica (maior = borda mais fina e definida)
uniform float edge_power : hint_range(0.5, 8.0) = 3.0;
// Velocidade da pulsação de "respiração" da membrana
uniform float pulse_speed : hint_range(0.0, 5.0) = 1.2;
// Intensidade da variação de opacidade pela pulsação (0 = sem pulso)
uniform float pulse_strength : hint_range(0.0, 1.0) = 0.15;

void fragment() {
    // UV vai de 0.0 a 1.0 dentro do retângulo do ColorRect.
    // Centraliza para -1.0..1.0 para medir a distância até a borda mais próxima.
    vec2 centered = UV * 2.0 - 1.0;
    float dist_to_edge = max(abs(centered.x), abs(centered.y));

    // Curva de intensidade: quase 0 no centro, sobe perto da borda (efeito fresnel retangular).
    float rim = pow(dist_to_edge, edge_power);

    // Pulsação suave ao longo do tempo, como se a membrana "respirasse".
    float pulse = 1.0 + sin(TIME * pulse_speed) * pulse_strength;

    vec4 col = mix(base_color, edge_color, rim);
    col.a *= pulse;

    COLOR = col;
}
