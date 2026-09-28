#ifndef MINE_COMMON_INCLUDED
#define MINE_COMMON_INCLUDED

// Globals driven by DayNightCycle.cs
half _MineSkyDarken;      // 0 at noon, ~10 at midnight (sky light levels removed)
half _MineMinBrightness;  // floor so caves are never pitch black
half4 _MineSkyTint;       // colour of sky light (bluish at night, warm at dusk)

// Vertex colour layout written by ChunkMesher: r = sky light / 15, g = AO * face shade, b = block light / 15.
// Brightness curve 0.8^(15 - level) mirrors Blocks.Brightness() on the CPU.
half3 MineLight(half4 c)
{
    half sky = max(c.r * 15.0h - _MineSkyDarken, 0.0h);
    half blk = c.b * 15.0h;
    half skyB = pow(0.8h, 15.0h - sky);
    half blkB = pow(0.8h, 15.0h - blk);
    half3 l = max(skyB * _MineSkyTint.rgb, blkB * half3(1.0h, 0.86h, 0.66h));
    l = max(l, _MineMinBrightness.xxx);
    return l * c.g;
}

#endif
