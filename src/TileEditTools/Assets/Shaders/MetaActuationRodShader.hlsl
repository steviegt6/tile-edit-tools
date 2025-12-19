#include "tmlbuild.h"

sampler uImage0 : register(s0);

float uTime GLOBAL_TIME;

// Really simple hue shift function with angle in radians using YUV color space.
// https://en.wikipedia.org/wiki/Y%E2%80%B2UV
float3 hue_shift(float3 color, float angle)
{
    const float3 k_rgb_to_y = float3(0.299, 0.587, 0.114);
    float y = dot(color, k_rgb_to_y);

    float u = dot(color, float3(-0.14713, -0.28886, 0.436));
    float v = dot(color, float3(0.615, -0.51499, -0.10001));

    float cos_a = cos(angle);
    float sin_a = sin(angle);

    float u2 = u * cos_a - v * sin_a;
    float v2 = u * sin_a + v * cos_a;

    return y + u2 * float3(0.0, -0.39465, 2.03211)
        + v2 * float3(1.13983, -0.58060, 0.0);
}

float4 main(float4 sample_color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 tex_color = tex2D(uImage0, coords);
    return float4(hue_shift(tex_color.rgb, uTime * 3.14), tex_color.a) * sample_color;
}

#ifdef FX
technique Technique1
{
    pass HueShader
    {
        PixelShader = compile ps_3_0 main();
    }
}
#endif // FX
