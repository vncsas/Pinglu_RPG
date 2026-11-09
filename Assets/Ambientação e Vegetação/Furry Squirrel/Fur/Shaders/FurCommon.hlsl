#ifndef FURBRUSH_COMMON_INCLUDED
#define FURBRUSH_COMMON_INCLUDED

// Pipeline-agnostic helpers shared by every Squirrel fur shader: control-map decode,
// hashes / strand pattern, and the Kajiya-Kay anisotropic highlight. No URP/HDRP
// includes here on purpose so this file can be reused anywhere (incl. the Blit brush).

// ----------------------------------------------------------------------------
// Control map  (R = length 0..1, G = mask 0..1, B,A = comb direction packed 0..1)
// ----------------------------------------------------------------------------
struct FurControl
{
    float  length;   // 0..1  multiply by _MaxLength
    float  mask;     // 0..1  fur presence / density
    float2 comb;     // -1..1 tangent-space flow direction
};

FurControl DecodeFurControl(float4 c, float hasControlMap)
{
    FurControl o;
    if (hasControlMap < 0.5)
    {
        o.length = 1.0;
        o.mask   = 1.0;
        o.comb   = float2(0.0, 0.0);
    }
    else
    {
        o.length = c.r;
        o.mask   = c.g;
        o.comb   = c.ba * 2.0 - 1.0;
    }
    return o;
}

// Generates a stable, continuous orthonormal basis for a normal n.
// Swizzled Frisvad method placing the singularity at n.y = -1.0 (bottom of mesh).
void GetStableBasis(float3 n, out float3 t, out float3 b)
{
    if (n.y < -0.9999)
    {
        t = float3(1.0, 0.0, 0.0);
        b = float3(0.0, 0.0, 1.0);
        return;
    }
    float a = 1.0 / (1.0 + n.y);
    float d = -n.x * n.z * a;
    t = float3(1.0 - n.x * n.x * a, -n.x, d);
    b = float3(d, -n.z, 1.0 - n.z * n.z * a);
}

// ----------------------------------------------------------------------------
// Hashes  (Dave Hoskins, https://www.shadertoy.com/view/4djSRW)
// ----------------------------------------------------------------------------
float FurHash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float FurHash21(float2 p)
{
    float3 p3 = frac(p.xyx * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 FurHash22(float2 p)
{
    float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

// Smooth value noise for low-frequency clumping.
float FurValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = FurHash21(i + float2(0, 0));
    float b = FurHash21(i + float2(1, 0));
    float c = FurHash21(i + float2(0, 1));
    float d = FurHash21(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

// ----------------------------------------------------------------------------
// Helper to safely normalize a 2D vector with fallback
// ----------------------------------------------------------------------------
float2 FurSafeDir(float2 v, float2 fallback)
{
    float l2 = dot(v, v);
    return l2 > 1e-8 ? v * rsqrt(l2) : fallback;
}

// ----------------------------------------------------------------------------
// Strand coverage for a shell at normalized height t (0 = skin, 1 = tip).
//   uv        : UV0 (will be scaled by density internally)
//   density   : strands per UV unit
//   t         : current shell height 0..1
//   thinness  : 0..1 how fast strands taper toward the tip
//   jitter    : 0..1 random per-strand height variation
//   clumping  : extra low-freq modulation of strand height
//   flowDir   : direction to stretch the strand along
//   flowStretch : stretch scale factor (>= 1.0)
// Returns soft coverage in 0..1.
// ----------------------------------------------------------------------------
float FurStrandCoverage(float2 uv, float density, float t, float thinness, float jitter, float clumping, float pixelSize, float2 flowDir, float flowStretch)
{
    float2 cell   = uv * density;
    float2 id     = floor(cell);
    float2 f      = frac(cell);
    float2 center = 0.5 + (FurHash22(id) - 0.5) * 0.5;

    // Pull strands together into tufts (clumping)
    if (clumping > 0.01)
    {
        float2 clumpId = floor(cell * 0.1);
        
        // 1. Randomize clump activity: 15% of clumps are completely flat/normal
        float clumpActive = step(0.15, FurHash21(clumpId + 83.2));
        
        if (clumpActive > 0.5)
        {
            // 2. Randomize the center of the clump
            float2 clumpCenter = (clumpId + FurHash22(clumpId + 13.7)) * 10.0;
            float2 toClump = clumpCenter - (id + center);
            
            // 3. Randomize the clump-wide pull strength (some clumps are tighter, some looser)
            float clumpRandStrength = FurHash21(clumpId + 71.3);
            float pullStrength = clumping * lerp(0.3, 1.7, clumpRandStrength);
            
            // 4. Randomize per-strand pull (creates a tight core and loose outer hairs)
            float strandRand = FurHash21(id + 42.1);
            float pull = saturate(pullStrength * 0.12 * lerp(0.4, 1.6, strandRand)) * t;
            
            center += toClump * pull;
        }
    }

    // Radius shrinks toward the tip -> tapered strands.
    float r = lerp(0.5, 0.16, thinness) * saturate(1.0 - t * 0.85);

    // Dynamic distance thickness adjustment: keep strands visible and reduce aliasing
    // by ensuring the strand radius in cell space doesn't shrink below 0.25 of the screen pixel size.
    r = max(r, min(pixelSize * 0.25, 0.4));

    // Thickening at grazing angles based on flowStretch to prevent gap separation (scales down towards tips to preserve tapering)
    r *= 1.0 + min(max(0.0, flowStretch - 1.0), 10.0) * 0.06 * saturate(1.0 - t * 0.85);

    float2 dir = FurSafeDir(flowDir, float2(1.0, 0.0));
    float2 p = f - center;
    float halfLen = r * max(0.0, flowStretch - 1.0);
    p -= dir * clamp(dot(p, dir), -halfLen, halfLen);

    float d = length(p);

    float strand = 1.0 - smoothstep(r * 0.55, r, d);

    // Per-strand max height, optionally clumped by a low-frequency field.
    // Decoupled from density to keep clumping frequency consistent at high density,
    // and scaled to support the full 0..4 clumping slider range with up to 85% strength.
    float clump = lerp(1.0, FurValueNoise(uv * 35.0), saturate(clumping * 0.25) * 0.85);
    float h = lerp(1.0, FurHash21(id + 11.5), jitter) * clump;
    float alive  = step(t, h);
    return strand * alive;
}

// ----------------------------------------------------------------------------
// Kajiya-Kay anisotropic highlight along strand tangent T (world space).
// ----------------------------------------------------------------------------
float FurKajiyaKay(float3 T, float3 L, float3 V, float exponent)
{
    float3 H     = normalize(L + V);
    float  dotTH = dot(T, H);
    float  sinTH = sqrt(saturate(1.0 - dotTH * dotTH));
    return pow(sinTH, exponent);
}

#endif // FURBRUSH_COMMON_INCLUDED
