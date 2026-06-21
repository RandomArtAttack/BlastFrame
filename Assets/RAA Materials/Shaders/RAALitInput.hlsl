#ifndef RAA_LIT_PROJECTION_INPUT_INCLUDED
#define RAA_LIT_PROJECTION_INPUT_INCLUDED

// =================================================================================================
// VERBATIM COPY of URP 17.3.0 Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl
// with two RAA additions, both clearly marked with "RAA:":
//   1. `float4 _ProjectionOffset;` added to the UnityPerMaterial CBUFFER (kept in the CBUFFER so the
//      shader stays SRP-Batcher compatible).
//   2. Triplanar projection helpers + RAA_InitializeProjectedLitSurfaceData(), appended at the end.
// The original InitializeStandardLitSurfaceData() is left UNTOUCHED so every stock URP pass that
// includes this file (Shadow/Depth/GBuffer/Meta/2D/MotionVectors) compiles exactly as before.
// NOTE: this is a fork — it is locked to URP 17.3.0 and will NOT track URP package updates.
// =================================================================================================

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

#if defined(_DETAIL_MULX2) || defined(_DETAIL_SCALED)
#define _DETAIL
#endif

// NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
float4 _BaseMap_TexelSize;
float4 _DetailAlbedoMap_ST;
half4 _BaseColor;
half4 _SpecColor;
half4 _EmissionColor;
half _Cutoff;
half _Smoothness;
half _Metallic;
half _BumpScale;
half _Parallax;
half _OcclusionStrength;
half _ClearCoatMask;
half _ClearCoatSmoothness;
half _DetailAlbedoMapScale;
half _DetailNormalMapScale;
half _Surface;
float4 _ProjectionOffset;     // RAA: per-object world-space offset applied before triplanar projection
float4 _ProjectionWorldScale; // RAA: world units per texture tile, per axis (xyz). Default 4,4,4.
UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

// NOTE: Do not ifdef the properties for dots instancing, but ifdef the actual usage.
// Otherwise you might break CPU-side as property constant-buffer offsets change per variant.
// NOTE: Dots instancing is orthogonal to the constant buffer above.
#ifdef UNITY_DOTS_INSTANCING_ENABLED

UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _SpecColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
    UNITY_DOTS_INSTANCED_PROP(float , _Smoothness)
    UNITY_DOTS_INSTANCED_PROP(float , _Metallic)
    UNITY_DOTS_INSTANCED_PROP(float , _BumpScale)
    UNITY_DOTS_INSTANCED_PROP(float , _Parallax)
    UNITY_DOTS_INSTANCED_PROP(float , _OcclusionStrength)
    UNITY_DOTS_INSTANCED_PROP(float , _ClearCoatMask)
    UNITY_DOTS_INSTANCED_PROP(float , _ClearCoatSmoothness)
    UNITY_DOTS_INSTANCED_PROP(float , _DetailAlbedoMapScale)
    UNITY_DOTS_INSTANCED_PROP(float , _DetailNormalMapScale)
    UNITY_DOTS_INSTANCED_PROP(float , _Surface)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

// Here, we want to avoid overriding a property like e.g. _BaseColor with something like this:
// #define _BaseColor UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor0)
//
// It would be simpler, but it can cause the compiler to regenerate the property loading code for each use of _BaseColor.
//
// To avoid this, the property loads are cached in some static values at the beginning of the shader.
// The properties such as _BaseColor are then overridden so that it expand directly to the static value like this:
// #define _BaseColor unity_DOTS_Sampled_BaseColor
//
// This simple fix happened to improve GPU performances by ~10% on Meta Quest 2 with URP on some scenes.
static float4 unity_DOTS_Sampled_BaseColor;
static float4 unity_DOTS_Sampled_SpecColor;
static float4 unity_DOTS_Sampled_EmissionColor;
static float  unity_DOTS_Sampled_Cutoff;
static float  unity_DOTS_Sampled_Smoothness;
static float  unity_DOTS_Sampled_Metallic;
static float  unity_DOTS_Sampled_BumpScale;
static float  unity_DOTS_Sampled_Parallax;
static float  unity_DOTS_Sampled_OcclusionStrength;
static float  unity_DOTS_Sampled_ClearCoatMask;
static float  unity_DOTS_Sampled_ClearCoatSmoothness;
static float  unity_DOTS_Sampled_DetailAlbedoMapScale;
static float  unity_DOTS_Sampled_DetailNormalMapScale;
static float  unity_DOTS_Sampled_Surface;

void SetupDOTSLitMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_SpecColor            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _SpecColor);
    unity_DOTS_Sampled_EmissionColor        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor);
    unity_DOTS_Sampled_Cutoff               = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Cutoff);
    unity_DOTS_Sampled_Smoothness           = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Smoothness);
    unity_DOTS_Sampled_Metallic             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Metallic);
    unity_DOTS_Sampled_BumpScale            = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _BumpScale);
    unity_DOTS_Sampled_Parallax             = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Parallax);
    unity_DOTS_Sampled_OcclusionStrength    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _OcclusionStrength);
    unity_DOTS_Sampled_ClearCoatMask        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ClearCoatMask);
    unity_DOTS_Sampled_ClearCoatSmoothness  = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _ClearCoatSmoothness);
    unity_DOTS_Sampled_DetailAlbedoMapScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _DetailAlbedoMapScale);
    unity_DOTS_Sampled_DetailNormalMapScale = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _DetailNormalMapScale);
    unity_DOTS_Sampled_Surface              = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Surface);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSLitMaterialPropertyCaches()

#define _BaseColor              unity_DOTS_Sampled_BaseColor
#define _SpecColor              unity_DOTS_Sampled_SpecColor
#define _EmissionColor          unity_DOTS_Sampled_EmissionColor
#define _Cutoff                 unity_DOTS_Sampled_Cutoff
#define _Smoothness             unity_DOTS_Sampled_Smoothness
#define _Metallic               unity_DOTS_Sampled_Metallic
#define _BumpScale              unity_DOTS_Sampled_BumpScale
#define _Parallax               unity_DOTS_Sampled_Parallax
#define _OcclusionStrength      unity_DOTS_Sampled_OcclusionStrength
#define _ClearCoatMask          unity_DOTS_Sampled_ClearCoatMask
#define _ClearCoatSmoothness    unity_DOTS_Sampled_ClearCoatSmoothness
#define _DetailAlbedoMapScale   unity_DOTS_Sampled_DetailAlbedoMapScale
#define _DetailNormalMapScale   unity_DOTS_Sampled_DetailNormalMapScale
#define _Surface                unity_DOTS_Sampled_Surface

#endif

TEXTURE2D(_ParallaxMap);        SAMPLER(sampler_ParallaxMap);
TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_DetailMask);         SAMPLER(sampler_DetailMask);
TEXTURE2D(_DetailAlbedoMap);    SAMPLER(sampler_DetailAlbedoMap);
TEXTURE2D(_DetailNormalMap);    SAMPLER(sampler_DetailNormalMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_SpecGlossMap);       SAMPLER(sampler_SpecGlossMap);
TEXTURE2D(_ClearCoatMap);       SAMPLER(sampler_ClearCoatMap);

#ifdef _SPECULAR_SETUP
    #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv)
#else
    #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv)
#endif

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;

#ifdef _METALLICSPECGLOSSMAP
    specGloss = half4(SAMPLE_METALLICSPECULAR(uv));
    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _Smoothness;
    #else
        specGloss.a *= _Smoothness;
    #endif
#else // _METALLICSPECGLOSSMAP
    #if _SPECULAR_SETUP
        specGloss.rgb = _SpecColor.rgb;
    #else
        specGloss.rgb = _Metallic.rrr;
    #endif

    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _Smoothness;
    #else
        specGloss.a = _Smoothness;
    #endif
#endif

    return specGloss;
}

half SampleOcclusion(float2 uv)
{
    #ifdef _OCCLUSIONMAP
        half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
        return LerpWhiteTo(occ, _OcclusionStrength);
    #else
        return half(1.0);
    #endif
}


// Returns clear coat parameters
// .x/.r == mask
// .y/.g == smoothness
half2 SampleClearCoat(float2 uv)
{
#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    half2 clearCoatMaskSmoothness = half2(_ClearCoatMask, _ClearCoatSmoothness);

#if defined(_CLEARCOATMAP)
    clearCoatMaskSmoothness *= SAMPLE_TEXTURE2D(_ClearCoatMap, sampler_ClearCoatMap, uv).rg;
#endif

    return clearCoatMaskSmoothness;
#else
    return half2(0.0, 1.0);
#endif  // _CLEARCOAT
}

void ApplyPerPixelDisplacement(half3 viewDirTS, inout float2 uv)
{
#if defined(_PARALLAXMAP)
    uv += ParallaxMapping(TEXTURE2D_ARGS(_ParallaxMap, sampler_ParallaxMap), viewDirTS, _Parallax, uv);
#endif
}

// Used for scaling detail albedo. Main features:
// - Depending if detailAlbedo brightens or darkens, scale magnifies effect.
// - No effect is applied if detailAlbedo is 0.5.
half3 ScaleDetailAlbedo(half3 detailAlbedo, half scale)
{
    // detailAlbedo = detailAlbedo * 2.0h - 1.0h;
    // detailAlbedo *= _DetailAlbedoMapScale;
    // detailAlbedo = detailAlbedo * 0.5h + 0.5h;
    // return detailAlbedo * 2.0f;

    // A bit more optimized
    return half(2.0) * detailAlbedo * scale - scale + half(1.0);
}

half3 ApplyDetailAlbedo(float2 detailUv, half3 albedo, half detailMask)
{
#if defined(_DETAIL)
    half3 detailAlbedo = SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, detailUv).rgb;

    // In order to have same performance as builtin, we do scaling only if scale is not 1.0 (Scaled version has 6 additional instructions)
#if defined(_DETAIL_SCALED)
    detailAlbedo = ScaleDetailAlbedo(detailAlbedo, _DetailAlbedoMapScale);
#else
    detailAlbedo = half(2.0) * detailAlbedo;
#endif

    return albedo * LerpWhiteTo(detailAlbedo, detailMask);
#else
    return albedo;
#endif
}

half3 ApplyDetailNormal(float2 detailUv, half3 normalTS, half detailMask)
{
#if defined(_DETAIL)
#if BUMP_SCALE_NOT_SUPPORTED
    half3 detailNormalTS = UnpackNormal(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, detailUv));
#else
    half3 detailNormalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, detailUv), _DetailNormalMapScale);
#endif

    // With UNITY_NO_DXT5nm unpacked vector is not normalized for BlendNormalRNM
    // For visual consistancy we going to do in all cases
    detailNormalTS = normalize(detailNormalTS);

    return lerp(normalTS, BlendNormalRNM(normalTS, detailNormalTS), detailMask); // todo: detailMask should lerp the angle of the quaternion rotation, not the normals
#else
    return normalTS;
#endif
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.albedo = AlphaModulate(outSurfaceData.albedo, outSurfaceData.alpha);

#if _SPECULAR_SETUP
    outSurfaceData.metallic = half(1.0);
    outSurfaceData.specular = specGloss.rgb;
#else
    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0, 0.0, 0.0);
#endif

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = SampleOcclusion(uv);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));

#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    half2 clearCoat = SampleClearCoat(uv);
    outSurfaceData.clearCoatMask       = clearCoat.r;
    outSurfaceData.clearCoatSmoothness = clearCoat.g;
#else
    outSurfaceData.clearCoatMask       = half(0.0);
    outSurfaceData.clearCoatSmoothness = half(0.0);
#endif

#if defined(_DETAIL)
    half detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, uv).a;
    float2 detailUv = uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
    outSurfaceData.albedo = ApplyDetailAlbedo(detailUv, outSurfaceData.albedo, detailMask);
    outSurfaceData.normalTS = ApplyDetailNormal(detailUv, outSurfaceData.normalTS, detailMask);
#endif
}

// =================================================================================================
// RAA: World-space triplanar projection
// -------------------------------------------------------------------------------------------------
// _ProjectionWorldScale.xyz = world units one full texture tile spans on each axis (default 4,4,4;
// a 2048px texture over 4 units => 512 texels/unit). A smaller value packs the tile into fewer units
// => denser on that axis (e.g. 4,4,1 makes the Z axis repeat every 1 unit). _ProjectionOffset slides
// the projection per object.
// =================================================================================================
float3 RAA_ProjectedPos(float3 positionWS)
{
    float3 scale = max(_ProjectionWorldScale.xyz, float3(1e-4, 1e-4, 1e-4)); // units per tile; guard /0
    return (positionWS + _ProjectionOffset.xyz) / scale;
}

float3 RAA_TriplanarWeights(float3 normalWS)
{
    float3 w = pow(abs(normalWS), 4.0);
    return w / max(dot(w, float3(1.0, 1.0, 1.0)), 1e-4);
}

half4 RAA_SampleTri(TEXTURE2D_PARAM(tex, smp), float3 p, float3 w)
{
    half4 x = SAMPLE_TEXTURE2D(tex, smp, p.zy);
    half4 y = SAMPLE_TEXTURE2D(tex, smp, p.xz);
    half4 z = SAMPLE_TEXTURE2D(tex, smp, p.xy);
    return x * w.x + y * w.y + z * w.z;
}

// Whiteout-blended triplanar normal map -> world space (Ben Golus formulation). Samples the bump
// map on the SAME three planes as every other map (p.zy / p.xz / p.xy) so the detail lines up with
// the albedo, then folds the geometric world normal `n` into each tangent normal before blending so
// the perturbed result stays anchored to the actual surface (this is what the previous cheap
// sign-flip version got wrong — normals appeared detached from the colour).
half3 RAA_TriplanarNormalWS(float3 p, float3 n, float3 w, half scale)
{
    half3 tnormalX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, p.zy), scale);
    half3 tnormalY = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, p.xz), scale);
    half3 tnormalZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, p.xy), scale);

    // Fold the world normal in (whiteout), keeping each plane's sign correct.
    tnormalX = half3(tnormalX.xy + n.zy, abs(tnormalX.z) * n.x);
    tnormalY = half3(tnormalY.xy + n.xz, abs(tnormalY.z) * n.y);
    tnormalZ = half3(tnormalZ.xy + n.xy, abs(tnormalZ.z) * n.z);

    // Swizzle each back to world orientation and blend.
    return normalize(tnormalX.zyx * w.x + tnormalY.xzy * w.y + tnormalZ.xyz * w.z);
}

half4 RAA_SampleMetallicSpecGlossTri(float3 p, float3 w, half albedoAlpha)
{
    half4 specGloss;
#ifdef _METALLICSPECGLOSSMAP
    #ifdef _SPECULAR_SETUP
        specGloss = RAA_SampleTri(TEXTURE2D_ARGS(_SpecGlossMap, sampler_SpecGlossMap), p, w);
    #else
        specGloss = RAA_SampleTri(TEXTURE2D_ARGS(_MetallicGlossMap, sampler_MetallicGlossMap), p, w);
    #endif
    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _Smoothness;
    #else
        specGloss.a *= _Smoothness;
    #endif
#else
    #if _SPECULAR_SETUP
        specGloss.rgb = _SpecColor.rgb;
    #else
        specGloss.rgb = _Metallic.rrr;
    #endif
    #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        specGloss.a = albedoAlpha * _Smoothness;
    #else
        specGloss.a = _Smoothness;
    #endif
#endif
    return specGloss;
}

// Object's world-space basis with SCALE REMOVED: each normalized column of ObjectToWorld is a pure
// rotation axis; plus the object origin in world space. This lets object-space projection follow the
// object's rotation and position while keeping WORLD-unit texel density (scale is ignored).
// (Assumes no shear — i.e. uniform/axis-aligned scale, the normal blockout case.)
void RAA_ObjectBasis(out float3 axisX, out float3 axisY, out float3 axisZ, out float3 originWS)
{
    float4x4 o2w = GetObjectToWorldMatrix();
    axisX    = normalize(o2w._m00_m10_m20);
    axisY    = normalize(o2w._m01_m11_m21);
    axisZ    = normalize(o2w._m02_m12_m22);
    originWS = o2w._m03_m13_m23;
}

// Resolves the projection space. WORLD (default): projects in world space — texture is fixed in the
// world and the object slides through it when moved/rotated. OBJECT (_RAA_OBJECT_SPACE): projects in
// the object's rotation frame about its origin with SCALE IGNORED — the texture follows the object's
// rotation and position but keeps a constant world-unit density regardless of object scale. Outputs
// the projected+scaled sample position `p`, blend weights `w`, and the projection-space normal `nProj`.
void RAA_GetProjection(float3 positionWS, float3 normalWSGeom, out float3 p, out float3 w, out float3 nProj)
{
#if defined(_RAA_OBJECT_SPACE)
    float3 ax, ay, az, originWS;
    RAA_ObjectBasis(ax, ay, az, originWS);
    float3 rel = positionWS - originWS;
    float3 basePos = float3(dot(rel, ax), dot(rel, ay), dot(rel, az));   // world units, scale-free
    nProj = normalize(float3(dot(normalWSGeom, ax), dot(normalWSGeom, ay), dot(normalWSGeom, az)));
#else
    float3 basePos = positionWS;
    nProj = normalize(normalWSGeom);
#endif
    p = RAA_ProjectedPos(basePos);
    w = RAA_TriplanarWeights(nProj);
}

// Triplanar normal map -> WORLD-space shading normal, honoring the projection space. In object mode
// the whiteout blend runs in the object's (scale-free) rotation frame, then rotates back to world.
half3 RAA_ProjectedNormalWS(float3 p, float3 nProj, float3 w, half scale)
{
    half3 n = RAA_TriplanarNormalWS(p, nProj, w, scale);
#if defined(_RAA_OBJECT_SPACE)
    float3 ax, ay, az, originWS;
    RAA_ObjectBasis(ax, ay, az, originWS);
    return normalize(n.x * ax + n.y * ay + n.z * az);                   // frame -> world
#else
    return n;
#endif
}

// Triplanar twin of InitializeStandardLitSurfaceData. Fills the same SurfaceData fields, but samples
// every map by triplanar projection (world or object space, see RAA_GetProjection). Returns the
// final WORLD normal separately (the caller writes it into InputData.normalWS, bypassing the
// tangent-space path). Detail maps and parallax are intentionally skipped in projected mode.
inline void RAA_InitializeProjectedLitSurfaceData(float2 uv, float3 positionWS, float3 normalWSGeom,
                                                  out SurfaceData outSurfaceData, out float3 outNormalWS)
{
    outSurfaceData = (SurfaceData)0;

    float3 p, w, nProj;
    RAA_GetProjection(positionWS, normalWSGeom, p, w, nProj);

    half4 albedoAlpha = RAA_SampleTri(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), p, w);
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);

    half4 specGloss = RAA_SampleMetallicSpecGlossTri(p, w, albedoAlpha.a);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.albedo = AlphaModulate(outSurfaceData.albedo, outSurfaceData.alpha);

#if _SPECULAR_SETUP
    outSurfaceData.metallic = half(1.0);
    outSurfaceData.specular = specGloss.rgb;
#else
    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0, 0.0, 0.0);
#endif

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS   = half3(0.0, 0.0, 1.0); // unused — world normal returned via outNormalWS

#ifdef _OCCLUSIONMAP
    outSurfaceData.occlusion = LerpWhiteTo(RAA_SampleTri(TEXTURE2D_ARGS(_OcclusionMap, sampler_OcclusionMap), p, w).g, _OcclusionStrength);
#else
    outSurfaceData.occlusion = half(1.0);
#endif

#ifdef _EMISSION
    outSurfaceData.emission = RAA_SampleTri(TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap), p, w).rgb * _EmissionColor.rgb;
#else
    outSurfaceData.emission = half3(0.0, 0.0, 0.0);
#endif

#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    outSurfaceData.clearCoatMask       = _ClearCoatMask;
    outSurfaceData.clearCoatSmoothness = _ClearCoatSmoothness;
#else
    outSurfaceData.clearCoatMask       = half(0.0);
    outSurfaceData.clearCoatSmoothness = half(0.0);
#endif

#if defined(_NORMALMAP)
    outNormalWS = RAA_ProjectedNormalWS(p, nProj, w, _BumpScale);
#else
    outNormalWS = normalize(normalWSGeom);
#endif
}

#endif // RAA_LIT_PROJECTION_INPUT_INCLUDED
