// 가성비 리얼리스틱 PBR 셰이더 (URP 14)
// 핵심 최적화:
//  - MaskMap 1장(R:Metallic / G:Occlusion / B:Detail Mask / A:Smoothness)으로 텍스처 샘플 절감
//  - shader_feature 로 안 쓰는 기능은 변형에서 제거 → 비용 0
//  - URP 내장 PBR(UniversalFragmentPBR) 재사용으로 라이트/그림자/GI/포그 최적 경로 사용
Shader "Game/Realistic Lit"
{
    Properties
    {
        [MainColor] _BaseColor       ("Base Color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap       ("Base Map (RGB) / Alpha", 2D) = "white" {}

        [Toggle(_NORMALMAP)] _UseNormalMap ("Use Normal Map", Float) = 0
        [Normal] _BumpMap            ("Normal Map", 2D) = "bump" {}
        _BumpScale                   ("Normal Scale", Range(0,2)) = 1.0

        [Toggle(_MASKMAP)] _UseMaskMap ("Use Mask Map (R:Metal G:AO B:Detail A:Smooth)", Float) = 0
        _MaskMap                     ("Mask Map", 2D) = "white" {}

        _Metallic                    ("Metallic", Range(0,1)) = 0.0
        _Smoothness                  ("Smoothness", Range(0,1)) = 0.5
        _OcclusionStrength           ("Occlusion Strength", Range(0,1)) = 1.0

        [Header(Micro Detail)]
        [Toggle(_DETAIL)] _UseDetail ("Use Detail Normal", Float) = 0
        [Normal] _DetailNormalMap    ("Detail Normal", 2D) = "bump" {}
        _DetailNormalScale           ("Detail Strength", Range(0,2)) = 1.0
        _DetailTiling                ("Detail Tiling", Float) = 8.0

        [Header(Emission)]
        [Toggle(_EMISSION)] _UseEmission ("Use Emission", Float) = 0
        [HDR] _EmissionColor         ("Emission Color", Color) = (0,0,0,1)
        _EmissionMap                 ("Emission Map", 2D) = "white" {}

        [Header(Surface)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff                      ("Alpha Cutoff", Range(0,1)) = 0.5

        // 표준 렌더 상태 (URP 머티리얼 인스펙터 호환)
        [HideInInspector] _Cull      ("__cull", Float) = 2.0
        [HideInInspector] _Surface   ("__surface", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 300

        // ---------------------------------------------------------------------
        // Forward Lit : 메인 라이팅 패스
        // ---------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            // 머티리얼 키워드
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _MASKMAP
            #pragma shader_feature_local _DETAIL
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _ALPHATEST_ON

            // URP 라이팅 키워드 (가성비: 사용되는 변형만 컴파일됨)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #include "Realistic_LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                float2 lightmapUV   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                half3  normalWS     : TEXCOORD2;
                half4  tangentWS    : TEXCOORD3;   // xyz: tangent, w: sign
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 4);
                half  fogFactor     : TEXCOORD5;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD6;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs  = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv         = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS   = normInputs.normalWS;

                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = half4(normInputs.tangentWS, sign);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);

                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord = GetShadowCoord(posInputs);
                #endif

                return output;
            }

            // 표면 데이터 구성 (텍스처 샘플 집중)
            void InitSurfaceData(Varyings input, out SurfaceData surf)
            {
                surf = (SurfaceData)0;

                half4 albedo = SampleBaseMap(input.uv) * _BaseColor;
                surf.alpha = AlphaClipTest(albedo.a);
                surf.albedo = albedo.rgb;

                // 마스크맵 한 번 샘플로 메탈릭/AO/디테일/스무스니스 동시 취득
                half metallic, occlusion, smoothness, detailMask;
                SampleMask(input.uv, metallic, occlusion, detailMask, smoothness);
                surf.metallic   = metallic;
                surf.occlusion  = occlusion;
                surf.smoothness = smoothness;
                surf.specular   = half3(0,0,0);

                surf.normalTS = SampleNormalTS(input.uv, detailMask);
                // URP 내장 SampleEmission: 내부에서 _EMISSION 키워드 검사
                surf.emission = SampleEmission(input.uv, _EmissionColor.rgb,
                    TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
            }

            void InitInputData(Varyings input, half3 normalTS, out InputData inputData)
            {
                inputData = (InputData)0;
                inputData.positionWS = input.positionWS;

                #if defined(_NORMALMAP) || defined(_DETAIL)
                    float sgn = input.tangentWS.w;
                    half3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
                    half3x3 tbn = half3x3(input.tangentWS.xyz, bitangent, input.normalWS.xyz);
                    inputData.normalWS = TransformTangentToWorld(normalTS, tbn);
                #else
                    inputData.normalWS = input.normalWS;
                #endif
                inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    inputData.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0,0,0,0);
                #endif

                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = half3(0,0,0);
                inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                SurfaceData surfaceData;
                InitSurfaceData(input, surfaceData);

                InputData inputData;
                InitInputData(input, surfaceData.normalTS, inputData);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = surfaceData.alpha;
                return color;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        // ShadowCaster : 그림자 투영
        // ---------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Realistic_LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        // DepthOnly : 뎁스 프리패스
        // ---------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Realistic_LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        // DepthNormals : SSAO/뎁스노멀용
        // ---------------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Realistic_LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
