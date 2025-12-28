#include "tmlbuild.h"

sampler uImage0 : register(s0);

#define CYCLE_DURATION 3.0
#define SCAN_SPEED (1.0 / CYCLE_DURATION)
// Hardcoded (2/38) fractional because I'm lazy.  The item height is 38px.
#define SCANLINE_THICKNESS (1.0 / 19.0)
#define TRAIL_LENGTH 0.4
#define GLOW_INTENSITY 0.85

float uTime GLOBAL_TIME;

float4 main(float4 sample_color : COLOR0, float2 coords : TEXCOORD0) : COLOR0
{
    float4 base = tex2D(uImage0, coords);

    if (base.a <= 0)
    {
        return base;
    }

    // 70, 130, 180
    float lum = dot(base.rgb, float3(0.299, 0.587, 0.114));

    float3 gray = lum.xxx;
    float3 color = lerp(base.rgb, gray, 0.35);

    float scan_pos = uTime * SCAN_SPEED;
    {
        // Clamp specifically to the end of the sprite (1.0) PLUS the end of the
        // glow trail, so the full fade can complete for each cycle.
        scan_pos = fmod(scan_pos, 1.0 + TRAIL_LENGTH);
    }

    float scan_delta = coords.y - scan_pos;
    float scanline = smoothstep(SCANLINE_THICKNESS, 0.0, abs(scan_delta));

    float glow = 0.0;
    if (scan_delta < 0.0 && scan_delta > -TRAIL_LENGTH)
    {
        // 1 at delta = 0, 0 at delta = -TRAIL_LENGTH
        float trail_factor = saturate(1.0 + scan_delta / TRAIL_LENGTH);
        glow = trail_factor * trail_factor * GLOW_INTENSITY;
    }

    float scan_effect = (scanline + glow) * lerp(0.4, 1.0, lum);
    float3 final_color = (color + scan_effect) * float3(0.95, 1.0, 0.97);
    {
        return float4(saturate(final_color), base.a);
    }
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
