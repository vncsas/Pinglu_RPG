#ifndef FURBRUSH_SHELL_INCLUDED
#define FURBRUSH_SHELL_INCLUDED

#include "FurPipeline.hlsl"
#include "FurCommon.hlsl"

// ----------------------------------------------------------------------------
// Material data (shared by every pass)
// ----------------------------------------------------------------------------
TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
TEXTURE2D(_SmoothnessMap);  SAMPLER(sampler_SmoothnessMap);
TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
TEXTURE2D(_FurControlMap);  SAMPLER(sampler_FurControlMap);
TEXTURE2D(_FurWidthMap);    SAMPLER(sampler_FurWidthMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float  _Smoothness;
    float  _RoughnessInput;
    float  _SmoothnessChannel;
    float  _UseSmoothnessMap;
    float  _UseNormalMap;
    float  _MaxLength;
    float  _LengthScale;
    float  _Density;
    float  _Thinness;
    float  _LengthJitter;
    float  _Clumping;
    float  _DirRandom;
    float  _ColorVar;
    float4 _RootColor;
    float4 _TipColor;
    float  _AlbedoInfluence;
    float  _RootAO;
    float  _AnisoSpecular;
    float  _Rim;
    float  _ShadowOpacity;
    float4 _Gravity;
    float  _GravityStrength;
    float  _CombStrength;
    float  _CombSurfaceAdherence;
    float  _ShellCount;
    float  _ParallaxScale;
    float  _SilhouetteStretch;
    float  _EnableSilhouetteCorrection;
    float  _SilhouetteTilt;
    float  _CombJitter;
    float  _CombUprightness;
    float  _HasControlMap;
    float  _HasWidthMap;
    float  _DiffuseWrap;     // 1 = established wrap-lit look, 0 = Lambert (matches Lit's shading)
CBUFFER_END

#define FUR_CLIP_THRESHOLD 0.5

struct FurAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0; // albedo / strand UV
    float2 uvControl  : TEXCOORD1; // unique (lightmap) UV for the control map
    float2 shell      : TEXCOORD2; // x = layer t (0 on the base mesh), y = shell index
    float4 controlTangentFrame : TEXCOORD3; // UV1 tangent relative to skinned UV0 TANGENT
};

// Shared vertex extrusion. controlUV (UV1 when _CONTROL_UV1) drives only the painted
// mask/length/comb; albedoUV (UV0) drives the base map and procedural strand pattern.
void FurExtrude(FurAttributes IN, out float3 positionOS, out float3 normalOS,
                out float2 controlUV, out float2 albedoUV, out float t, out FurControl ctrl, out float3 strandDirWS)
{
    albedoUV  = IN.uv;
    controlUV = IN.uv;
#ifdef _CONTROL_UV1
    controlUV = IN.uvControl;
#endif
    t = IN.shell.x;

    float4 cs = SAMPLE_TEXTURE2D_LOD(_FurControlMap, sampler_FurControlMap, controlUV, 0);
    ctrl = DecodeFurControl(cs, _HasControlMap);

    float len = ctrl.length * _MaxLength * _LengthScale * ctrl.mask;
    
    // Compress shell height in heavily combed areas to lie flat against the skin.
    float combLen = length(ctrl.comb);
    float combSurface = saturate(pow(saturate(combLen), 0.35) * _CombSurfaceAdherence);
    len *= lerp(1.0, 0.45, combSurface);

    float2 id = floor(albedoUV * _Density);
    len *= lerp(1.0, FurHash21(id + 7.0), _LengthJitter);

    float3 nOS = normalize(IN.normalOS);
    float3 tOS = normalize(IN.tangentOS.xyz);
    tOS = normalize(tOS - nOS * dot(tOS, nOS));
    float tangentSign = IN.tangentOS.w;
#ifdef _CONTROL_UV1
    if (IN.controlTangentFrame.w > 0.5)
    {
        float3 perpendicularOS = normalize(cross(nOS, tOS));
        tOS = normalize(tOS * IN.controlTangentFrame.x
                      + perpendicularOS * IN.controlTangentFrame.y);
        tangentSign = IN.controlTangentFrame.z;
    }
#endif
    float3 bOS = normalize(cross(nOS, tOS) * tangentSign);

    float combControl = saturate(combLen);
    float combFollow = smoothstep(0.02, 0.60, combControl);
    float2 rnd = FurHash22(id + 19.3) * 2.0 - 1.0;          // per-strand random lean
    float3 randOS = (tOS * rnd.x + bOS * rnd.y) * _DirRandom;
    // randOS *= 1.0 - combFollow;
    
    // Keep comb randomness subtle: high values fight painted grooming and kink the tips.
    float2 combRnd = FurHash22(id + 37.1) * 2.0 - 1.0;
    float combJitter = min(_CombJitter, 0.25);
    float3 combNoiseOS = (tOS * combRnd.x + bOS * combRnd.y) * combJitter;
    float3 combOS = (tOS * ctrl.comb.x + bOS * ctrl.comb.y) * _CombStrength + combNoiseOS * length(ctrl.comb);
    
    // Combine gravity, random lean, and comb into a single tangent bend vector.
    // Project gravity onto the tangent plane so it only causes sideways lean.
    float3 gravityT = _Gravity.xyz - nOS * dot(_Gravity.xyz, nOS);
    
    // Fade gravity where combed so the comb fully controls the lay direction (combing against the
    // natural gravity grain lays just as well the other way).
    // Grooming should damp gravity, not switch it off. A hard zero made the control appear to
    // work only on a few exposed parts (typically legs) where no comb data was painted.
    float gravFade = _GravityStrength * lerp(1.0, 0.35, combFollow);
    float gravityNormal = dot(_Gravity.xyz, nOS);

    float3 silhouetteTiltOS = float3(0.0, 0.0, 0.0);
    float extrudeScale = 1.0;
    if (_EnableSilhouetteCorrection > 0.5)
    {
        float3 baseWS = TransformObjectToWorld(IN.positionOS.xyz);
        float3 viewWS = normalize(_WorldSpaceCameraPos.xyz - baseWS);
        float3 viewOS = normalize(TransformWorldToObjectDir(viewWS));
        float3 viewT = viewOS - nOS * dot(viewOS, nOS);
        float viewLenSq = dot(viewT, viewT);
        if (viewLenSq > 1e-8)
        {
            float3 viewTiltOS = viewT * rsqrt(viewLenSq);
            float grazing = 1.0 - saturate(dot(nOS, viewOS));
            grazing = grazing * grazing;
            float combDamping = lerp(1.0, 0.7, combControl);
            float tipGuard = lerp(1.0, 0.65, saturate(t));
            float tiltScale = grazing * 0.45 * combDamping * tipGuard;
            silhouetteTiltOS = viewTiltOS * (_SilhouetteTilt * tiltScale);

            // Silhouette Stand-Up: flat/combed fur automatically stands up slightly at grazing angles
            // to add fluffiness when viewed from the side. Controlled by the Tilt slider.
            float standUpDamp = 1.0 - grazing * saturate(abs(_SilhouetteTilt) * 0.25);
            combOS *= standUpDamp;
            randOS *= standUpDamp;
            gravityT *= standUpDamp;

            // Silhouette Extrusion: physically expand the shell layer spacing on the silhouette
            // to make the fur look thicker and fluffier. Controlled by the Silhouette Stretch slider.
            // The old response was almost imperceptible at the useful 1–5 slider range. Keep the
            // correction bounded, but make it engage earlier so it can bridge shell dots at grazing
            // angles without requiring extreme values.
            extrudeScale = 1.0 + grazing * saturate(_SilhouetteStretch / 14.0) * 0.9;
        }
    }

    // Gravity should be visible through the coat, not only on the last shell. Keep comb/random
    // curvature tip-weighted, while gravity ramps linearly with layer height.
    float3 baseBend = combOS + randOS + silhouetteTiltOS;
    float3 bendVector = baseBend * (t * t) + gravityT * gravFade * t;
    float maxBend = lerp(1.35, 1.05, combSurface);
    float bendLen = length(bendVector);
    if (bendLen > maxBend)
    {
        bendVector *= maxBend / bendLen;
    }
    
    // Conserve length: scale the normal offset down as the tangent bend increases
    float bendMagSq = dot(bendVector, bendVector);
    float normalScale = sqrt(saturate(1.0 - bendMagSq));
    normalScale = lerp(normalScale, 1.0, _CombUprightness);
    normalScale = lerp(normalScale, 0.08, combSurface);
    // Where gravity is nearly parallel to the surface normal there is little tangent bend to see;
    // let it compress/extend the shell spacing slightly instead, so the control remains visible
    // on broad upper and lower surfaces as well as on side-facing limbs.
    normalScale *= saturate(1.0 + gravityNormal * gravFade * 0.35 * t);

    float3 bendOS = bendVector * len;
    positionOS = IN.positionOS.xyz + nOS * len * t * normalScale * extrudeScale + bendOS;
    normalOS   = nOS;

    // Hair direction in object space: dimensionless normal component + tangent bend component.
    float3 dirOS = nOS * max(1e-4, t * normalScale) + bendVector;
    strandDirWS = TransformObjectToWorldDir(dirOS);

}


float2 FurStrandFlowDir(float2 projectionDir, float2 comb)
{
    float combLen = length(comb);
    float2 combDir = combLen > 1e-4 ? comb / combLen : projectionDir;
    return FurSafeDir(lerp(projectionDir, combDir, saturate(combLen)), float2(1.0, 0.0));
}

// Maps a world-space direction into UV space through the exact local tangent->UV Jacobian
// (from screen-space derivatives). Unlike projecting onto normalized T/B, this respects
// anisotropic texel density and UV shear, so the mapped direction isn't skewed on stretched
// or sheared UV charts. Returns 'fallback' where the derivatives are degenerate.
float2 FurWorldDirToUV(float3 dirWS, float3 dPdx, float3 dPdy, float2 dUdx, float2 dUdy, float2 fallback)
{
    float dxx = dot(dPdx, dPdx);
    float dxy = dot(dPdx, dPdy);
    float dyy = dot(dPdy, dPdy);
    float det = dxx * dyy - dxy * dxy;
    if (det < 1e-14) return fallback;
    float wa = dot(dirWS, dPdx);
    float wb = dot(dirWS, dPdy);
    float2 uvDir = ((wa * dyy - wb * dxy) * dUdx + (wb * dxx - wa * dxy) * dUdy) / det;
    return dot(uvDir, uvDir) > 1e-12 ? uvDir : fallback;
}

float2 FurSurfaceDirToUV(float3 dirWS, float3 N, float3 positionWS, float2 rawUV, float2 fallback)
{
#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)
    float3 surfaceDir = dirWS - N * dot(dirWS, N);
    float surfaceLen = length(surfaceDir);
    if (surfaceLen < 1e-5) return fallback;

    float2 uvDir = FurWorldDirToUV(surfaceDir / surfaceLen, ddx(positionWS), ddy(positionWS),
                                   ddx(rawUV), ddy(rawUV), fallback);
    return FurSafeDir(uvDir, fallback);
#else
    return fallback;
#endif
}



// On-surface trace/stretch direction for a strand, in UV space. Standing hairs use the classic
// view-parallax direction (grazing-angle gap fill); leaning hairs follow their own PHYSICAL
// axis (strandDirWS from the vertex stage: comb + gravity + random lean + the t^2 curvature),
// so the elongation lines up with where the hair actually points — including tips and bends.
// Fragment-only (uses screen derivatives).
float2 FurStrandScreenDir(float3 strandDirWS, float3 N, float3 V,
                          float3 positionWS, float2 rawUV, float2 tangentFallback)
{
#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)
    float3 H = normalize(strandDirWS);
    float3 blended;
    if (_EnableSilhouetteCorrection > 0.5)
    {
        // Project the 3D hair direction H onto the view plane (screen space).
        float3 screenHairDir = H - V * dot(H, V);
        // The view-parallax direction (screen projection of surface normal).
        float3 viewLat = V - N * dot(V, N);
        // Calculate grazing angle factor (silhouette).
        float grazing = 1.0 - saturate(abs(dot(N, V)));
        float grazingWeight = grazing * grazing;
        // Płynne przejście: na wprost kamery rozciągamy wzdłuż ekranowego kierunku włosa,
        // a na sylwetce (krawędziach) rozciągamy wzdłuż rzutu kamery (parallax/viewLat),
        // co gwarantuje pionowe mostkowanie warstw.
        blended = lerp(screenHairDir, viewLat, grazingWeight);
    }
    else
    {
        // When silhouette correction is off, we only project onto the surface tangent plane
        // to avoid any view-dependent stretching.
        blended = H - N * dot(H, N);
    }

    float bl = length(blended);
    if (bl < 1e-5) return tangentFallback;

    float2 uvDir = FurWorldDirToUV(blended / bl, ddx(positionWS), ddy(positionWS),
                                   ddx(rawUV), ddy(rawUV), tangentFallback);
    return FurSafeDir(uvDir, tangentFallback);
#else
    return tangentFallback;
#endif
}

float2 FurViewProjectedStrandUV(float2 uv, float t, FurControl ctrl, float3 strandDirWS,
                                float3 positionWS, float3 normalWS, float4 tangentWS,
                                out float2 projectionDir, out float projectionAmount)
{
    float3 N = normalize(normalWS);
    float3 T = tangentWS.xyz;
    T = dot(T, T) > 1e-5 ? normalize(T) : normalize(cross(N, float3(0.0, 1.0, 0.0) + 1e-3));
    float3 B = normalize(cross(N, T) * tangentWS.w);
    float3 V = normalize(GetWorldSpaceViewDir(positionWS));
    float3 vTS = float3(dot(V, T), dot(V, B), dot(V, N));

    float denom = max(abs(vTS.z), 0.08);
    float2 projection = vTS.xy / denom;
    projectionAmount = min(length(projection), 6.0);

    // Trace along the strand's physical screen direction (view for standing hairs, the hair's
    // own axis when leaning), mapped through the exact tangent->UV Jacobian — so the per-shell
    // pattern shear leans exactly the way the hair geometry does, incl. at tips and on bends,
    // and doesn't skew on anisotropic UV charts.
    float2 traceDir = FurSafeDir(projection, float2(1.0, 0.0));
    projectionDir = FurStrandScreenDir(strandDirWS, N, V, positionWS, uv, traceDir);

    // Modest single-sample trace, used by the DEPTH passes only (the forward pass does a proper
    // 4-tap mini ray-march instead — a long single-sample shear just smears the pattern).
    float lengthWeight = lerp(0.35, 1.0, saturate(ctrl.length)) * ctrl.mask;
    float combFade = lerp(1.0, 0.65, saturate(length(ctrl.comb)));
    float tipDamping = lerp(1.0, 0.8, saturate(t));
    float offsetCells = projectionAmount * (_ParallaxScale * lengthWeight * t * tipDamping * combFade * 2.0);
    offsetCells = min(offsetCells, 16.0);

    return uv + traceDir * (offsetCells / max(_Density, 1.0));
}

// Coverage + control mask, used for alpha clipping
float FurCoverage(float2 strandUV, float controlMask, float t, float2 flowDir, float flowStretch, float widthLoc, float lengthLoc)
{
    float2 cell = strandUV * _Density;
    float2 id = floor(cell);
    float2 f = frac(cell);

    float r = lerp(0.5, 0.16, _Thinness) * saturate(1.0 - t * 0.85) * widthLoc;
    float pixelSize = 0.0;
#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)
    float2 dX = ddx(cell);
    float2 dY = ddy(cell);
    pixelSize = max(length(dX), length(dY));
#endif
    r = max(r, min(pixelSize * 0.25, 0.4));

    float physicalLength = lengthLoc * _MaxLength * _LengthScale * controlMask;
    float lengthFactor = saturate(physicalLength / 0.02);

    float maxStrand = 0.0;
    float safeJitterRange = 0.35;

    float sideStretch = min(max(0.0, flowStretch - 1.0), 72.0);
    float r_stretched = r * (1.0 + sideStretch * 0.06 * saturate(1.0 - t * 0.55));
    float halfLen = r_stretched * sideStretch;
    float2 dir = FurSafeDir(flowDir, float2(1.0, 0.0));

    // If there is stretching, sample a 3x3 cell grid to prevent clipping at cell boundaries.
    if (sideStretch > 1e-4)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            [unroll]
            for (int y = -1; y <= 1; y++)
            {
                float2 offset = float2(x, y);
                float2 nid = id + offset;
                
                float2 ncenter = 0.5 + (FurHash22(nid) - 0.5) * safeJitterRange;
                if (_Clumping > 0.01)
                {
                    float2 clumpId = floor(cell * 0.1);
                    float clumpActive = step(0.15, FurHash21(clumpId + 83.2));
                    if (clumpActive > 0.5)
                    {
                        float2 clumpCenter = (clumpId + FurHash22(clumpId + 13.7)) * 10.0;
                        float2 toClump = clumpCenter - (nid + ncenter);
                        float clumpRandStrength = FurHash21(clumpId + 71.3);
                        float pullStrength = _Clumping * lerp(0.3, 1.7, clumpRandStrength);
                        float strandRand = FurHash21(nid + 42.1);
                        float pull = saturate(pullStrength * 0.38 * lerp(0.4, 1.6, strandRand)) * t * lengthFactor;
                        ncenter += toClump * pull;
                    }
                }

                float2 p = f - (ncenter + offset);
                p -= dir * clamp(dot(p, dir), -halfLen, halfLen);

                float strand = 1.0 - smoothstep(r_stretched * 0.55, r_stretched, length(p));
                float h = lerp(1.0, FurHash21(nid + 11.5), _LengthJitter);
                if (_Clumping > 0.01)
                    h *= lerp(1.0, FurValueNoise(strandUV * 35.0), saturate(_Clumping * 0.5) * 0.92);
                float alive = step(t, h);
                maxStrand = max(maxStrand, strand * alive);
            }
        }
    }
    else
    {
        // Fast path for non-stretched strands.
        float2 center = 0.5 + (FurHash22(id) - 0.5) * safeJitterRange;
        if (_Clumping > 0.01)
        {
            float2 clumpId = floor(cell * 0.1);
            float clumpActive = step(0.15, FurHash21(clumpId + 83.2));
            if (clumpActive > 0.5)
            {
                float2 clumpCenter = (clumpId + FurHash22(clumpId + 13.7)) * 10.0;
                float2 toClump = clumpCenter - (id + center);
                float clumpRandStrength = FurHash21(clumpId + 71.3);
                float pullStrength = _Clumping * lerp(0.3, 1.7, clumpRandStrength);
                float strandRand = FurHash21(id + 42.1);
                float pull = saturate(pullStrength * 0.38 * lerp(0.4, 1.6, strandRand)) * t * lengthFactor;
                center += toClump * pull;
            }
        }

        float2 p = f - center;
        float strand = 1.0 - smoothstep(r * 0.55, r, length(p));
        float h = lerp(1.0, FurHash21(id + 11.5), _LengthJitter);
        if (_Clumping > 0.01)
            h *= lerp(1.0, FurValueNoise(strandUV * 35.0), saturate(_Clumping * 0.5) * 0.92);
        float alive = step(t, h);
        maxStrand = strand * alive;
    }

    return ((t < 1e-4) ? 1.0 : maxStrand) * controlMask;
}

void FurClipFlow(float2 strandUV, float controlMask, float t, float4 positionCS, float2 flowDir, float flowStretch, float widthLoc, float lengthLoc)
{
    // A fixed, subtle screen-space dither blends discrete shell transitions. This is an internal
    // quality treatment rather than an artist control. The noise phase MUST differ per shell
    // layer (t * _ShellCount as IGN's "frame" index):
    // a single per-pixel value clipped the SAME pixels in every layer, so the holes lined up
    // into see-through tunnels through the whole fur volume at grazing/close-up views.
    float dither = InterleavedGradientNoise(positionCS.xy, t * _ShellCount);
    float shellSpacing = 1.0 / max(4.0, _ShellCount);
    // Keep layer blending sub-shell. A larger height jitter turns the procedural strand mask into
    // isolated bright pinpoints, most obvious around high-contrast details such as eyes.
    float t_dithered = saturate(t + (dither - 0.5) * shellSpacing * 0.65);
    float cov = FurCoverage(strandUV, controlMask, t_dithered, flowDir, flowStretch, widthLoc, lengthLoc);
    // Edge-limit the threshold jitter to the coverage transition band. Applied everywhere it made
    // solid strand cores AND empty gaps stipple too (the "dither over the whole plane" look);
    // confining it to |cov-0.5| near zero keeps flat areas clean and only softens strand edges.
    float edgeBand = saturate(1.0 - abs(cov - FUR_CLIP_THRESHOLD) * 4.0);
    float thresholdJitter = (dither - 0.5) * 0.12 * edgeBand;
    clip(cov - (FUR_CLIP_THRESHOLD + thresholdJitter));
}

void FurClip(float2 strandUV, float controlMask, float t, float4 positionCS, float widthLoc, float lengthLoc)
{
    FurClipFlow(strandUV, controlMask, t, positionCS, float2(1.0, 0.0), 1.0, widthLoc, lengthLoc);
}

void FurClipShadowOpacity(float4 positionCS)
{
    float opacity = saturate(_ShadowOpacity);
    if (opacity < 0.999)
    {
        float dither = InterleavedGradientNoise(positionCS.xy, 0);
        clip(opacity - dither);
    }
}

// The stretch direction + projection amount are taken from FurViewProjectedStrandUV's outputs
// (every caller computes them immediately before) instead of being re-derived here — the old
// version rebuilt the full tangent frame and ran FurStrandScreenDir a second time per fragment,
// per shell, per pass.
void FurClipSilhouette(float2 strandUV, float controlMask, float2 comb, float t, float4 positionCS,
                       float3 positionWS, float3 strandDirWS,
                       float2 projectionDir, float projectionAmount, float widthLoc, float lengthLoc)
{
    float flowStretch = 1.0;
    FurClipFlow(strandUV, controlMask, t, positionCS, projectionDir, flowStretch, widthLoc, lengthLoc);
}

// ============================================================================
// Forward (lit) pass — only compiled when the pass asks for it.
// ============================================================================
#ifdef FURBRUSH_FORWARD_LIGHTING
#ifndef FURBRUSH_HDRP_COMPAT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

struct FurVaryings
{
    float4 positionCS : SV_POSITION;
    float2 controlUV  : TEXCOORD0;
    float2 strandUV   : TEXCOORD1; // UV0: base map + procedural strand pattern
    float  t          : TEXCOORD2;
    float3 positionWS : TEXCOORD3;
    float3 normalWS   : TEXCOORD4;
    float4 tangentWS  : TEXCOORD5; // xyz dir, w = sign
    float  fogFactor  : TEXCOORD6;
    float3 strandDirWS : TEXCOORD7;
};

FurVaryings FurVertForward(FurAttributes IN)
{
    FurVaryings o = (FurVaryings)0;

    float3 posOS, nOS; float2 cuv, auv; float t; FurControl ctrl;
    FurExtrude(IN, posOS, nOS, cuv, auv, t, ctrl, o.strandDirWS);

    VertexPositionInputs vpi = GetVertexPositionInputs(posOS);
    VertexNormalInputs vni   = GetVertexNormalInputs(nOS, IN.tangentOS);

    o.positionCS = vpi.positionCS;
    o.positionWS = vpi.positionWS;
    o.normalWS   = vni.normalWS;
    o.tangentWS  = float4(vni.tangentWS, IN.tangentOS.w);
    o.controlUV  = cuv;
    o.strandUV   = auv;
    o.t          = t;
    o.fogFactor  = ComputeFogFactor(vpi.positionCS.z);
    return o;
}

half SampleSmoothness(float2 buv)
{
    half s = _Smoothness;
#ifdef _SMOOTHNESSMAP
    float4 m = SAMPLE_TEXTURE2D(_SmoothnessMap, sampler_SmoothnessMap, buv);
    float4 sel = float4(_SmoothnessChannel < 0.5,
                        _SmoothnessChannel >= 0.5 && _SmoothnessChannel < 1.5,
                        _SmoothnessChannel >= 1.5 && _SmoothnessChannel < 2.5,
                        _SmoothnessChannel >= 2.5);
    s = dot(m, sel);
    #ifdef _ROUGHNESS_INPUT
        s = 1.0 - s;
    #endif
    // The slider acts as a gloss FLOOR under the map — the map used to fully replace it, which
    // made the Lighting > Smoothness slider do literally nothing on any model with a
    // metallic/smoothness texture.
    s = max(s, (half)_Smoothness);
#endif
    return s;
}

float3 ApplyNormalMap(float2 buv, float3 normalWS, float4 tangentWS)
{
#ifdef _NORMALMAP
    half3 n = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, buv));
    float3 bitangent = cross(normalWS, tangentWS.xyz) * tangentWS.w;
    float3x3 tbn = float3x3(tangentWS.xyz, bitangent, normalWS);
    return normalize(mul(n, tbn));
#else
    return normalize(normalWS);
#endif
}

half3 ShadeFurLight(Light light, half3 albedo, float3 N, float3 T, float3 V, half gloss, float t)
{
    float3 L = light.direction;
    float3 radiance = light.color * (light.shadowAttenuation * light.distanceAttenuation);

    float wrap = FurDiffuseLightingTerm(dot(N, L), _DiffuseWrap);
    // HDRP hair is dielectric: smoothness reshapes the fibre highlight, but must not remove
    // the coat's diffuse albedo as metallic workflow shading would. Keep the established URP
    // response unchanged.
#if defined(FURBRUSH_HDRP_COMPAT)
    float diffuseScale = 1.0;
#else
    float diffuseScale = lerp(1.0, 0.7, gloss * gloss);
#endif

    // Dual-highlight tangent shifts (primary shifted toward tip, secondary toward root)
    float3 T1 = normalize(T + 0.08 * N);
    float3 T2 = normalize(T - 0.12 * N);

    // Primary specular: sharp, untinted/white highlight shifted toward the tip
    float spec1 = FurKajiyaKay(T1, L, V, lerp(45.0, 300.0, gloss));

    // Secondary specular: broad, tinted highlight shifted toward the root
    float spec2 = FurKajiyaKay(T2, L, V, lerp(15.0, 100.0, gloss));

    float glossMultiplier = lerp(0.5, 2.0, gloss);
    half3 specColor = FurAnisotropicSpecular(albedo, spec1, spec2, _AnisoSpecular, glossMultiplier);

    // Transmission toward the tip: true backlight plus a fresnel rim so the slider is visible
    // from any angle (the old dot(-L,V)-only term was ~zero unless looking against the light).
    // Quadratic gain: low values match the old response, but the top of the slider now really
    // glows (max ≈ 2.5x the old strength).
    float fresnelRim = pow(1.0 - saturate(dot(N, V)), 2.0);
    float back = (saturate(dot(-L, V)) * 0.7 + fresnelRim * 0.6)
               * _Rim * (1.0 + _Rim * 1.5) * t * FurTransmissionLightingScale();

    return (albedo * ((wrap + back) * diffuseScale) + specColor) * radiance;
}

half4 FurFragForward(FurVaryings IN) : SV_Target
{
    float t  = IN.t;
    FurControl ctrl = DecodeFurControl(SAMPLE_TEXTURE2D(_FurControlMap, sampler_FurControlMap, IN.controlUV), _HasControlMap);
    
    float widthLoc = 1.0;
    if (_HasWidthMap > 0.5)
    {
        widthLoc = SAMPLE_TEXTURE2D(_FurWidthMap, sampler_FurWidthMap, IN.controlUV).r;
    }

    // --- Silhouette continuity: 4-tap mini ray-march through the shell volume. The eye ray
    // pierces the shells ABOVE this fragment at laterally shifted spots; sampling the strand
    // field (pure ALU, no textures) along that path shows the actual strands the ray would hit —
    // thin continuous hairs at grazing angles instead of dots (no trace) or a smeared hatch
    // (one long single-sample shear). _ParallaxScale scales the physical ray span.
    float3 Nv = normalize(IN.normalWS);
    float3 Vv = normalize(GetWorldSpaceViewDir(IN.positionWS));
    float vN = max(abs(dot(Vv, Nv)), 0.08);

    // Per-layer noise phase — see FurClipFlow: correlated dither across layers punched aligned
    // see-through tunnels through the shell volume at grazing angles.
    float dither = InterleavedGradientNoise(IN.positionCS.xy, t * _ShellCount);
    float shellSpacing = 1.0 / max(4.0, _ShellCount);
    float tD = saturate(t + (dither - 0.5) * shellSpacing * 0.65);

    float2 rayUV = float2(0.0, 0.0);
    float perLayerCells = 0.0;
#if defined(SHADER_API_D3D11) || defined(SHADER_API_GLCORE) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN)
    // Match the vertex stage's comb-flatten shortening (FurExtrude: len *= lerp(1,0.45,combSurface)).
    // Without it the parallax ray over-reaches on combed areas, biasing the sampled hit toward the
    // tips -> combed fur wrongly picked up _TipColor (the "white streak on combed hair" artifact).
    float combSurfaceF = saturate(length(ctrl.comb) * _CombSurfaceAdherence);
    float lenWorld = ctrl.length * _MaxLength * _LengthScale * ctrl.mask * lerp(1.0, 0.45, combSurfaceF);
    float3 spanWS = (Vv - Nv * dot(Vv, Nv)) * ((lenWorld * _ParallaxScale) / vN);   // per unit t
    rayUV = FurWorldDirToUV(spanWS, ddx(IN.positionWS), ddy(IN.positionWS),
                            ddx(IN.strandUV), ddy(IN.strandUV), float2(0.0, 0.0));
    // The see-through gap sits between NEIGHBOURING shell layers, so the quantity that matters is
    // the lateral shift per one layer step (in strand cells). Clamp it so extreme grazing never
    // samples further than ~3 cells per layer (island-bleed / smear guard).
    perLayerCells = length(rayUV) * shellSpacing * _Density;
    if (perLayerCells > 3.0) { rayUV *= 3.0 / perLayerCells; perLayerCells = 3.0; }
#endif

    float2 strandFlowDir = FurStrandScreenDir(IN.strandDirWS, Nv, Vv, IN.positionWS, IN.strandUV,
                                            FurSafeDir(rayUV, float2(1.0, 0.0)));
    float3 strandLat = IN.strandDirWS - Nv * dot(IN.strandDirWS, Nv);
    float flowControl = max(saturate(length(ctrl.comb)), saturate(length(strandLat) * 1.5));
    float baseFiberStretch = lerp(1.0, 2.0, flowControl);

    // Bridge to the neighbouring layers only: Silhouette Stretch controls how far the forward
    // pass searches. The tap elongation itself follows the strand/comb direction.
    float projectionStrength = (_EnableSilhouetteCorrection > 0.5) ? min(max(_SilhouetteStretch, 0.0), 20.0) : 0.0;
    // sqrt-boosted response so low/mid slider values already bridge several layers and elongate the
    // taps meaningfully — the old near-linear mapping made 1..5 barely reach the next shell. The top
    // (20) is essentially unchanged, so the max look doesn't gain new smear.
    float projection01 = sqrt(saturate(projectionStrength / 20.0));
    float bridgeLayers = projectionStrength > 1e-3 ? lerp(2.5, 16.0, projection01) : 0.0;
    float tRange = min(bridgeLayers * shellSpacing, 1.0 - tD);
    float projectionGain = projectionStrength > 1e-3 ? (0.8 + projection01 * 26.0) : 0.0;
    float tapStretch = 1.0;

    // Seed the raymarch with the EXACT sample the DepthOnly/shadow passes clip on (plain
    // strandUV at tD). The bridging taps below start at tD + tRange*dither*0.25, so with
    // silhouette correction on they can MISS a strand the depth prepass kept — the prepass
    // then holds a "phantom" shell depth with no colour behind it, the body gets z-rejected
    // and the SKY shows through the mesh. Seeding guarantees forward coverage >= depth
    // coverage, so every depth-kept pixel is also coloured (the clip threshold below is
    // monotonic in cov: 1 - 0.15*4 > 0).
    float cov = FurCoverage(IN.strandUV, ctrl.mask, tD, strandFlowDir, 1.0, widthLoc, ctrl.length);
    float2 hitUV = IN.strandUV;
    float hitT = tD;
    [unroll]
    for (int k = 0; k < 4; k++)
    {
        // Jitter the sample heights to convert discrete shell slicing into a smooth, fuzzy transition.
        float tk = tD + tRange * ((k + dither) * 0.25);
        float2 uvk = IN.strandUV + rayUV * (tk - tD);
        float c = FurCoverage(uvk, ctrl.mask, tk, strandFlowDir, tapStretch, widthLoc, ctrl.length);
        if (c > cov) { cov = c; hitUV = uvk; hitT = tk; }
    }
    float edgeBand = saturate(1.0 - abs(cov - FUR_CLIP_THRESHOLD) * 4.0);
    clip(cov - (FUR_CLIP_THRESHOLD + (dither - 0.5) * 0.12 * edgeBand));

    float2 buv = IN.strandUV * _BaseMap_ST.xy + _BaseMap_ST.zw;
    half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, buv) * _BaseColor;

    half3 tint   = FurRootTipTint(_RootColor.rgb, _TipColor.rgb, hitT); // localized root/tip shade
    half3 albedo = lerp(tint, baseTex.rgb * tint, _AlbedoInfluence);    // inherit the surface albedo
    albedo *= lerp(1.0 - 0.4 * _RootAO, 1.0, hitT);                     // extra root occlusion
    float2 cvCell = floor(hitUV * _Density);                            // per-strand color variation
    albedo *= 1.0 + (half3(FurHash21(cvCell + 1.3), FurHash21(cvCell + 7.7), FurHash21(cvCell + 3.2)) - 0.5) * _ColorVar;

    half gloss = SampleSmoothness(buv);
    float3 N = ApplyNormalMap(buv, IN.normalWS, IN.tangentWS);
    float3 T = IN.strandDirWS;
    T = dot(T, T) > 1e-5 ? normalize(T) : normalize(cross(N, float3(0.0, 1.0, 0.0) + 1e-3));
    float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    half3 color = ShadeFurLight(mainLight, albedo, N, T, V, gloss, hitT);

#if defined(_ADDITIONAL_LIGHTS) || defined(FURBRUSH_HDRP_COMPAT)
    uint count = GetAdditionalLightsCount();
    for (uint li = 0u; li < count; li++)
    {
        Light al = GetAdditionalLight(li, IN.positionWS);
        color += ShadeFurLight(al, albedo, N, T, V, gloss, hitT);
    }
#endif

    color += SampleSH(N) * albedo;                          // ambient / GI

    // Environment reflection, kept subtle and grazing-only. Broadly adding the probe/skybox colour
    // tinted the whole coat (e.g. a blue sky turned the highlights blue — the "gloss changes colour"
    // artifact); gating it by fresnel keeps a faint rim sheen without recolouring the fur.
    half3 envRefl = GlossyEnvironmentReflection(reflect(-V, N), 1.0 - gloss, 1.0);
    half fresnel = pow(1.0 - saturate(dot(N, V)), 4.0);
    // gloss^2 keeps it dead at low smoothness; 0.35 makes the top of the slider visibly sheen
    // (still fresnel-gated, so no whole-coat sky tint).
    color += envRefl * (gloss * gloss * fresnel * 0.35);

    color = MixFog(color, IN.fogFactor);
    return half4(color, 1.0);
}
#endif // FURBRUSH_FORWARD_LIGHTING

// ============================================================================
// Depth / shadow varyings + vertex (lightweight, shared by shadow & depth passes)
// ============================================================================
struct FurDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 controlUV  : TEXCOORD0;
    float2 strandUV   : TEXCOORD1;
    float  t          : TEXCOORD2;
    float3 normalWS   : TEXCOORD3;
    float3 positionWS : TEXCOORD4;
    float4 tangentWS  : TEXCOORD5;
    float3 strandDirWS : TEXCOORD6;
};

#endif // FURBRUSH_SHELL_INCLUDED
