#ifndef FURBRUSH_CARD_INCLUDED
#define FURBRUSH_CARD_INCLUDED

// Method C: alpha fur cards. Quads inherit the base surface color (sampled at the stored
// base-mesh UV in TEXCOORD2), fade root->tip, and sway in the wind by their root->tip weight
// (UV.y). Strand alpha is procedural by default, or from a supplied texture (_ALPHATEX).

#include "FurPipeline.hlsl"
#include "FurCommon.hlsl"

TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
TEXTURE2D(_SmoothnessMap);  SAMPLER(sampler_SmoothnessMap);
TEXTURE2D(_FurAlpha);  SAMPLER(sampler_FurAlpha);
TEXTURE2D(_FurControlMap); SAMPLER(sampler_FurControlMap);
TEXTURE2D(_FurWidthMap); SAMPLER(sampler_FurWidthMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float4 _RootColor;
    float4 _TipColor;
    float  _AlbedoInfluence;
    float  _RootAO;
    float  _Smoothness;
    float  _RoughnessInput;
    float  _SmoothnessChannel;
    float  _UseSmoothnessMap;
    float  _AnisoSpecular;
    float  _Rim;
    float  _ShadowOpacity;
    float  _CardStrands;
    float  _Thinness;
    float  _Cutoff;
    float  _AlphaFlipY;
    float  _AlphaMipBias;
    float  _UseAlphaTex;
    float  _AlphaFromLuminance;
    float  _AlphaDither;
    float  _ColorVar;
    float  _CombStrength;
    float  _CombSurfaceAdherence;
    float  _CombTipBend;
    float4 _Gravity;
    float  _GravityStrength;
    float  _MaxLength;
    float  _LengthScale;
    float  _AlbedoProjection;
    float  _CombJitter;
    float  _HasControlMap;
    float  _HasWidthMap;
    float  _CardBillboard;   // 0 = fixed (view-independent), 1 = billboard to camera
    float  _DiffuseWrap;     // 1 = established wrap-lit look, 0 = Lambert (matches Lit's shading)
CBUFFER_END

struct CardAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
    float4 uv1        : TEXCOORD1; // halfW, height, yaw, unused legacy slot
    float4 baseUV     : TEXCOORD2;
    // xy = control-map UV. zw = baked alpha-atlas cell (cellIndex, gridColumns) — written only by
    // the Bake Cards step; dynamic meshes store Vector2 here so zw reads as 0 (= no atlas).
    float4 controlUV  : TEXCOORD3;
};

struct CardDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float4 controlUV  : TEXCOORD2;
};

// Width axis perpendicular to the strand's curve direction, continuously varying as the strand
// bends (comb + gravity can curl it all the way back over itself, curveDir -> -up). A rotation
// built from cross(from,to) (Rodrigues' formula) is numerically unstable as 'from' and 'to'
// approach anti-parallel (the 1/(1+dot) term blows up) and snaps to an arbitrary fallback exactly
// at the cutoff -> a visible kink in the card right where combing lays the tip flat. Gram-Schmidt
// projection has no such singularity (only degenerates when 'primary' is parallel to curveDir,
// handled below), so the ribbon orientation stays smooth through a full 180 degree curl.
float3 StableWidthAxis(float3 primary, float3 curveDir)
{
    float3 proj = primary - curveDir * dot(primary, curveDir);
    float lenSq = dot(proj, proj);
    if (lenSq > 1e-6) return proj * rsqrt(lenSq);
    float3 fallback = (abs(curveDir.y) < 0.99) ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    return normalize(cross(curveDir, fallback));
}

float3 CardScaleAndApplyCombAndGravity(float3 rootOS, float3 normalOS, float4 tangentOS, float4 uv1, float2 uv, float2 controlUV, float4 baseUV, out float3 strandDirWS)
{
    float halfW = uv1.x;
    float height = uv1.y;
    float yaw = uv1.z;

    // Sample control map at control UV
    float4 ctrlSample = SAMPLE_TEXTURE2D_LOD(_FurControlMap, sampler_FurControlMap, controlUV, 0);
    FurControl ctrl = DecodeFurControl(ctrlSample, _HasControlMap);

    float widthLoc = 1.0;
    if (_HasWidthMap > 0.5)
    {
        widthLoc = SAMPLE_TEXTURE2D_LOD(_FurWidthMap, sampler_FurWidthMap, controlUV, 0).r;
    }

    // Card height is driven SOLELY by the per-card mesh height (uv1.y — set at scatter time and
    // edited by the per-card Card Height brush) times coverage and the global scale. It no longer
    // reads the control map's R (length) channel: editing card height therefore can't disturb the
    // shared comb map, and there's no global-scale↔map compensation left to fall out of sync.
    // (R stays in use for shell/strands, which have no per-element height of their own.)
    float scaledLength = ctrl.mask * _LengthScale;
    float fullHeight = height * scaledLength;

    // Default (uncombed) width axis: the ACTUAL tangent baked from the brush drag direction at
    // scatter time (the card authoring tool stores it as the mesh TANGENT). Re-orthonormalize against the
    // surface normal for safety (skinning can tilt it slightly)
    float3 up = normalize(normalOS);
    float3 draggedTangent = tangentOS.xyz - up * dot(tangentOS.xyz, up);
    float draggedLenSq = dot(draggedTangent, draggedTangent);
    float3 t0_drag = draggedLenSq > 1e-8 ? draggedTangent * rsqrt(draggedLenSq) : normalize(cross(up, float3(1.0, 0.0, 0.0)));

    // Reconstruct base TBN frame. If baseUV.w is 0.0, we fall back to GetStableBasis (legacy/untangented).
    // Otherwise, we rotate the visual tangent t0_drag by theta around the normal up.
    // This is 100% invariant under bone skinning since normalOS and tangentOS rotate together.
    float3 t0_base, b0_base;
    if (baseUV.w == 0.0)
    {
        GetStableBasis(up, t0_base, b0_base);
    }
    else
    {
        float theta = baseUV.z;
        float tangentSign = baseUV.w;
        float3 bitangent = cross(up, t0_drag);
        t0_base = normalize(t0_drag * cos(theta) + bitangent * sin(theta));
        b0_base = normalize(cross(up, t0_base) * tangentSign);
    }

    // 'yaw' is now purely the Yaw Jitter / cross-quad-plane-offset rotation applied ON TOP of the
    // drag-following base — at Yaw Jitter = 0 a card sits exactly along the stroke.
    float rad = yaw * (3.14159265 / 180.0);
    float cosY = cos(rad);
    float sinY = sin(rad);
    float3 t0_scatter = t0_drag * cosY + cross(up, t0_drag) * sinY + up * dot(up, t0_drag) * (1.0 - cosY);
    t0_scatter = normalize(t0_scatter);

    // Flow direction in object space
    float3 flowOS = t0_base * ctrl.comb.x + b0_base * ctrl.comb.y;
    float flowLen = saturate(length(ctrl.comb));

    // Flow magnitude IS the painted Hair Flatness value: 0 = full-height upright card,
    // 1 = fully laid along the surface. A previous 0.85 baseline forced even flatness=0.03
    // almost flat while providing almost no lateral bend, collapsing a 4.6 cm card into a
    // short, wide patch. Keep the mapping direct so the displayed card height stays meaningful.
    float surfaceAdherence = saturate(flowLen * _CombSurfaceAdherence);

    float normalLift = lerp(1.0, 0.0, surfaceAdherence);

    float3 t0 = t0_scatter;
    if (flowLen > 0.001)
    {
        float3 flowDirOS = flowOS / flowLen;
        // Width axis perpendicular to both 'up' and the LIVE painted comb direction. No extra yaw
        // rotation here: once the artist has combed an area, the comb direction alone must decide
        // the orientation, or every card would carry its own baked-in twist on top of the comb —
        // exactly the crooked/sideways look this fixes.
        float3 t0_flow = normalize(cross(up, flowDirOS));

        // Smoothly blend from the scattered tangent to the flow-based tangent in the brush falloff region
        t0 = normalize(lerp(t0_scatter, t0_flow, saturate(flowLen * 10.0)));
    }

    float3 b0 = normalize(cross(up, t0));

    // Combing randomness noise
    float2 id = floor(controlUV * 256.0);
    float2 combRnd = FurHash22(id + 37.1) * 2.0 - 1.0;
    float combJitter = min(_CombJitter, 0.25);
    float3 combNoiseOS = (t0 * combRnd.x + b0 * combRnd.y) * combJitter;
    float3 combLocal = (t0_base * ctrl.comb.x + b0_base * ctrl.comb.y) * (_CombStrength * 0.38) + combNoiseOS * flowLen;

    float3 gravityT = _Gravity.xyz - up * dot(_Gravity.xyz, up);

    // Secondary cross-quad plane: useful for volume while the fur stands, but when the card is
    // combed FLAT it is exactly the plane that faces the viewer with its EDGE ("bokiem") — fade it
    // out with surface adherence so the flat-lying primary plane dominates.
    float planeScale = 1.0;
    if (tangentOS.w > 0.5)
    {
        planeScale = lerp(1.0, 0.015, surfaceAdherence);
    }

    float v = saturate(uv.y);
    float taper = 1.0;
    float H = fullHeight * planeScale;

    // Curve the strand along the surface normal + comb/gravity bend. Fade gravity out where the
    // artist has combed, so the comb fully controls the lay direction and combing AGAINST the
    // natural (gravity) grain lays the fur just as well in the opposite direction.
    float gravFade = _GravityStrength * (1.0 - surfaceAdherence);

    float3 activeFlow = combLocal;
    if (dot(activeFlow, activeFlow) < 1e-4 && surfaceAdherence > 0.01)
    {
        activeFlow = b0 * (_CombStrength * 0.38) * surfaceAdherence;
    }
    
    float3 bendInput = activeFlow + gravityT * gravFade;
    float bendLimit = lerp(1.05, 0.62, surfaceAdherence);
    float bendLen = length(bendInput);
    if (bendLen > bendLimit) bendInput *= bendLimit / bendLen;
    float3 bendLocal = bendInput * ctrl.mask * H;   // H already carries the per-card height (see scaledLength)

    // Smooth cubic Bézier bend. P1 fixes the root tangent to the surface normal; P2 fixes the tip
    // tangent to the groom direction. Bend Segments sample this one continuous curve, so adding
    // subdivisions genuinely makes a smoother arc instead of resolving a kink concentrated at
    // the root or tip. Card Bend blends between the straight chord and the Bézier curve.
    float3 tipOffset = up * (H * normalLift) + bendLocal;
    float tipOffsetLen = length(tipOffset);
    if (tipOffsetLen > H && H > 1e-5) tipOffset *= H / tipOffsetLen;

    float3 tipAxisRaw = up * normalLift + bendInput;
    float3 strandAxis = length(tipAxisRaw) > 1e-5 ? normalize(tipAxisRaw) : up;
    float handleLength = H / 3.0;
    float3 p1 = up * handleLength;
    float3 p2 = tipOffset - strandAxis * handleLength;
    float oneMinusV = 1.0 - v;
    float3 bezierOffset =
        3.0 * oneMinusV * oneMinusV * v * p1 +
        3.0 * oneMinusV * v * v * p2 +
        v * v * v * tipOffset;
    float3 straightOffset = tipOffset * v;
    float3 centerOffset = lerp(straightOffset, bezierOffset, saturate(_CombTipBend));

    // Width axis: one direction shared by the ribbon, perpendicular to the smooth tip tangent.

    float3 widthDir;
    if (_CardBillboard > 0.5)
    {
        float3 rootWS  = TransformObjectToWorld(rootOS);
        float3 viewOS  = TransformWorldToObjectDir(_WorldSpaceCameraPos.xyz - rootWS);
        float3 bw = cross(strandAxis, viewOS);
        float  bwLen = length(bw);
        widthDir = bwLen > 1e-4 ? bw / bwLen : StableWidthAxis(t0, strandAxis);
        // A combed-FLAT card must face the surface (like a sticker), not the camera — otherwise
        // from the side you see its edge. Blend billboard -> flat with surface adherence.
        widthDir = normalize(lerp(widthDir, StableWidthAxis(t0, strandAxis), surfaceAdherence));
    }
    else
    {
        widthDir = StableWidthAxis(t0, strandAxis);   // perpendicular to the strand, no kink at full curl
    }
    if (tangentOS.w > 0.5) widthDir = normalize(cross(strandAxis, widthDir)); // 2nd cross-quad plane
    float3 surfaceWidthDir = lerp(widthDir, t0, surfaceAdherence);
    widthDir = dot(surfaceWidthDir, surfaceWidthDir) > 1e-6 ? normalize(surfaceWidthDir) : t0;

    // The ribbon width must ALWAYS stay in the root surface plane. Blending this correction by
    // flowLen made low-flatness cards keep a normal component in their width axis. One root edge
    // then plunged into the body and the clearance correction lifted it visibly above the skin.
    float3 widthDirTangent = widthDir - up * dot(widthDir, up);
    float lenW = length(widthDirTangent);
    if (lenW > 1e-5)
    {
        widthDir = widthDirTangent / lenW;
    }
    else
    {
        widthDir = t0;
    }

    float3 wProj = widthDir * (uv.x - 0.5) * 2.0 * halfW * taper * planeScale * widthLoc;
    float3 offset = centerOffset + wProj;
    
    // Dynamic skin clearance based on width offset to prevent edges from penetrating curved
    // meshes. Proportional but CAPPED: the old 2.5%-of-height + 12%-of-width-offset grew
    // linearly, so tall/wide cards hovered visibly above the skin (a >1 cm root gap on long
    // fur). Height-relative units keep it correct on scaled rigs.
    float distFromCenter = length(wProj);
    float skinClearance = min(max(fullHeight * 0.01, 0.00015) + distFromCenter * 0.05,
                              fullHeight * 0.025);
    // Root vertices are the attachment line and must remain exactly on the geometry. Fade the
    // anti-intersection clearance in over the lower fifth of the card instead of translating the
    // root itself away from the surface.
    skinClearance *= smoothstep(0.0, 0.2, v);
    
    float normalOffset = dot(offset, up);
    if (normalOffset < skinClearance)
    {
        offset += up * (skinClearance - normalOffset);
    }
    strandDirWS = TransformObjectToWorldDir(strandAxis);
    return rootOS + offset;
}

float3 CardScaleAndApplyCombAndGravity(float3 rootOS, float3 normalOS, float4 tangentOS, float4 uv1, float2 uv, float2 controlUV, float4 baseUV)
{
    float3 dummy;
    return CardScaleAndApplyCombAndGravity(rootOS, normalOS, tangentOS, uv1, uv, controlUV, baseUV, dummy);
}

float CardAlpha(float2 uv, float4 controlUV)
{
    uv.y = lerp(uv.y, 1.0 - uv.y, saturate(_AlphaFlipY));
#ifdef _ALPHATEX
    float2 auv = uv;
    // Baked alpha atlas: controlUV.zw = (cellIndex, gridColumns) of a square grid packed at bake
    // time so the whole card set renders with ONE material. Data-driven (w = 0 on dynamic meshes)
    // so no extra shader variant is needed. The slight inset guards against cross-cell bleeding
    // from mipmapping at the shared cell borders.
    if (controlUV.w > 0.5)
    {
        float colsA = controlUV.w;
        float idxA  = controlUV.z;
        float2 cell = float2(fmod(idxA, colsA), floor(idxA / colsA));
        float2 inner = clamp(saturate(uv), 0.002, 0.998);
        auv = (cell + inner) / colsA;
    }
    float4 alphaSample = SAMPLE_TEXTURE2D_BIAS(_FurAlpha, sampler_FurAlpha, auv, _AlphaMipBias);
    #ifdef _ALPHALUM
        return dot(alphaSample.rgb, float3(0.299, 0.587, 0.114));
    #else
        return alphaSample.a;
    #endif
#else
    float cols   = max(1.0, _CardStrands);
    float x      = uv.x * cols;
    float id     = floor(x);
    float fx     = frac(x);
    float center = 0.5 + (FurHash11(id) - 0.5) * 0.4;
    
    float widthLoc = 1.0;
    if (_HasWidthMap > 0.5)
    {
        widthLoc = SAMPLE_TEXTURE2D_LOD(_FurWidthMap, sampler_FurWidthMap, controlUV.xy, 0).r;
    }

    float hw     = lerp(0.45, 0.06, _Thinness) * (1.0 - uv.y * 0.7) * widthLoc;
    float d      = abs(fx - center);
    float a      = 1.0 - smoothstep(hw * 0.6, hw, d);
    float h      = lerp(0.55, 1.0, FurHash11(id + 3.1));
    a *= step(uv.y, h);
    return a;
#endif
}

float GetBayerDither4x4(float2 screenPos)
{
    const float4x4 bayer = float4x4(
        0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
        12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
        3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
        15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
    );
    int2 idx = int2(floor(screenPos)) % 4;
    return bayer[idx.x][idx.y];
}

void CardClip(float2 uv, float4 controlUV, float4 positionCS)
{
    float alpha = CardAlpha(uv, controlUV);
    // EDGE-LIMITED dithering. The old form jittered the cutoff by a full ±0.5*_AlphaDither, which
    // at _AlphaDither=1 stipples the WHOLE card face (every texel whose alpha lands anywhere in the
    // dither band), not just the strand silhouette. Scaling the jitter by the per-pixel alpha
    // gradient (fwidth) confines the dither to the ~1px transition around each strand edge, giving
    // soft anti-aliased edges while solid and empty regions stay clean.
    float aa = max(fwidth(alpha), 1e-5);
    float dither = GetBayerDither4x4(positionCS.xy) - 0.5;
    float jitter = dither * _AlphaDither * aa * 2.0;
    clip(alpha - (_Cutoff + jitter));
}

void CardClipShadowOpacity(float4 positionCS)
{
    float opacity = saturate(_ShadowOpacity);
    if (opacity < 0.999)
    {
        float dither = GetBayerDither4x4(positionCS.xy);
        clip(opacity - dither);
    }
}

half SampleCardSmoothness(float2 buv)
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
#endif
    return saturate(s);
}

#ifdef FURBRUSH_FORWARD_LIGHTING
#ifndef FURBRUSH_HDRP_COMPAT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

struct CardVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float2 baseUV     : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
    float3 normalWS   : TEXCOORD3;
    float4 tangentWS  : TEXCOORD4;
    float  fogFactor  : TEXCOORD5;
    float3 rootOS     : TEXCOORD6;
    float4 controlUV  : TEXCOORD7;
    float3 strandDirWS : TEXCOORD8;
};

CardVaryings CardVert(CardAttributes IN)
{
    CardVaryings o = (CardVaryings)0;
    float3 strandDirWS;
    float3 posOS = CardScaleAndApplyCombAndGravity(IN.positionOS.xyz, IN.normalOS, IN.tangentOS, IN.uv1, IN.uv, IN.controlUV.xy, IN.baseUV, strandDirWS);
    float3 positionWS = TransformObjectToWorld(posOS);
    o.positionCS = TransformWorldToHClip(positionWS);
    o.positionWS = positionWS;
    o.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
    o.tangentWS  = float4(TransformObjectToWorldDir(IN.tangentOS.xyz), IN.tangentOS.w);
    o.uv         = IN.uv;
    o.baseUV     = IN.baseUV;
    o.rootOS     = IN.positionOS.xyz;
    o.fogFactor  = ComputeFogFactor(o.positionCS.z);
    o.controlUV  = IN.controlUV;
    o.strandDirWS = strandDirWS;
    return o;
}

half3 ShadeCardLight(Light light, half3 albedo, float3 N, float3 T, float3 V, half gloss, float strandT)
{
    float3 L = light.direction;
    float3 radiance = light.color * (light.shadowAttenuation * light.distanceAttenuation);
    float wrap = FurDiffuseLightingTerm(dot(N, L), _DiffuseWrap);

    float3 T1 = normalize(T + 0.08 * N);
    float3 T2 = normalize(T - 0.12 * N);
    float spec1 = FurKajiyaKay(T1, L, V, lerp(45.0, 300.0, gloss));
    float spec2 = FurKajiyaKay(T2, L, V, lerp(15.0, 100.0, gloss));
    float glossMultiplier = lerp(0.5, 2.0, gloss);
    half3 specColor = FurAnisotropicSpecular(albedo, spec1, spec2, _AnisoSpecular, glossMultiplier);

    float back = saturate(dot(-L, V)) * _Rim * strandT * FurTransmissionLightingScale();
    return (albedo * (wrap + back) + specColor) * radiance;
}

half4 CardFrag(CardVaryings IN) : SV_Target
{
    CardClip(IN.uv, IN.controlUV, IN.positionCS);

    float2 buv = IN.baseUV * _BaseMap_ST.xy + _BaseMap_ST.zw;
    half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, buv) * _BaseColor;

    half3 tint   = FurRootTipTint(_RootColor.rgb, _TipColor.rgb, IN.uv.y);
    half3 albedo = lerp(tint, baseTex.rgb * tint, _AlbedoInfluence);   // inherit the surface albedo
    albedo *= lerp(1.0 - 0.4 * _RootAO, 1.0, IN.uv.y);
    float2 cvCell = floor(IN.rootOS.xy * 128.0 + IN.rootOS.z * 64.0);  // uniform per-card color variation
    albedo *= 1.0 + (half3(FurHash21(cvCell + 1.3), FurHash21(cvCell + 7.7), FurHash21(cvCell + 3.2)) - 0.5) * _ColorVar;

    half gloss = SampleCardSmoothness(buv);
    float3 N = normalize(IN.normalWS);
    float3 T = IN.strandDirWS;
    T = dot(T, T) > 1e-5 ? normalize(T) : normalize(cross(N, float3(0.0, 1.0, 0.0) + 1e-3));
    float3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    half3 color = ShadeCardLight(mainLight, albedo, N, T, V, gloss, IN.uv.y);

#if defined(_ADDITIONAL_LIGHTS) || defined(FURBRUSH_HDRP_COMPAT)
    uint count = GetAdditionalLightsCount();
    for (uint li = 0u; li < count; li++)
    {
        Light al = GetAdditionalLight(li, IN.positionWS);
        color += ShadeCardLight(al, albedo, N, T, V, gloss, IN.uv.y);
    }
#endif

    color += SampleSH(N) * albedo;

    // Subtle, grazing-only env reflection — see the shell shader note: a broad probe/skybox term
    // recolours the fur (blue sky -> blue sheen). Fresnel-gate it to a faint rim sheen.
    half3 envRefl = GlossyEnvironmentReflection(reflect(-V, N), 1.0 - gloss, 1.0);
    half fresnel = pow(1.0 - saturate(dot(N, V)), 4.0);
    color += envRefl * (gloss * gloss * fresnel * 0.2);

    color = MixFog(color, IN.fogFactor);
    return half4(color, 1.0);
}
#endif // FURBRUSH_FORWARD_LIGHTING

#endif // FURBRUSH_CARD_INCLUDED
