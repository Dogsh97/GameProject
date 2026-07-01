#ifndef REALISTIC_LIT_INPUT_INCLUDED
#define REALISTIC_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// SurfaceInput.hlsl 가 _BaseMap / _BumpMap / _EmissionMap 와 샘플러,
// SampleAlbedoAlpha / SampleNormal / SampleEmission / Alpha 를 이미 선언함.
// 여기서는 추가 텍스처와 머티리얼 상수만 선언한다.
TEXTURE2D(_MaskMap);            SAMPLER(sampler_MaskMap);
TEXTURE2D(_DetailNormalMap);   SAMPLER(sampler_DetailNormalMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half4  _EmissionColor;
    half   _Cutoff;
    half   _BumpScale;
    half   _Metallic;
    half   _Smoothness;
    half   _OcclusionStrength;
    half   _DetailNormalScale;
    half   _DetailTiling;
    half   _Cull;
    half   _Surface;
CBUFFER_END

// ---- 가성비 헬퍼 ---------------------------------------------------------

half4 SampleBaseMap(float2 uv)
{
    return SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
}

half AlphaClipTest(half alpha)
{
#if defined(_ALPHATEST_ON)
    clip(alpha - _Cutoff);
#endif
    return alpha;
}

// 마스크맵 1회 샘플로 4개 값을 동시에 취득 (R:Metallic G:AO B:Detail A:Smoothness)
void SampleMask(float2 uv, out half metallic, out half occlusion, out half detailMask, out half smoothness)
{
#if defined(_MASKMAP)
    half4 m = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
    metallic   = m.r * _Metallic;
    occlusion  = lerp(1.0h, m.g, _OcclusionStrength);
    detailMask = m.b;
    smoothness = m.a * _Smoothness;
#else
    metallic   = _Metallic;
    occlusion  = 1.0h;
    detailMask = 1.0h;
    smoothness = _Smoothness;
#endif
}

half3 SampleNormalTS(float2 uv, half detailMask)
{
    half3 n = half3(0.0h, 0.0h, 1.0h);
#if defined(_NORMALMAP)
    n = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
#endif
#if defined(_DETAIL)
    half3 d = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, uv * _DetailTiling),
        _DetailNormalScale * detailMask);
    // RNM 블렌딩: 베이스 노멀 디테일을 자연스럽게 합성
    n = normalize(half3(n.xy + d.xy, n.z * d.z));
#endif
    return n;
}

#endif // REALISTIC_LIT_INPUT_INCLUDED
