in vec2 UV;
uniform vec4 input_Color;
out vec4 outColor;
#include LogDepth.glsl
#include Lighting.glsl

void finishFragment(vec4 color){
	outColor = vec4((color.xyz) * DiffuseComponent, color.a);
	updateDepth();
}