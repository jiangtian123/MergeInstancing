#ifndef CUSTOM_INSTANCE_INCLUDED
#define CUSTOM_INSTANCE_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#pragma multi_compile _ CUSTOM_INSTANCING_ON
#pragma multi_compile _ CUSTOM_LIGHTMAP_ON
float4 instance_SHAr;
float4 instance_SHAg;
float4 instance_SHAb;
#ifdef CUSTOM_INSTANCING_ON
    UNITY_INSTANCING_BUFFER_START(CustomInstanceMaterial)
        UNITY_DEFINE_INSTANCED_PROP(float, _lightMapIndex)
        UNITY_DEFINE_INSTANCED_PROP(float4, _LightScaleOffset)
    UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
    #define LightMapIndex UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,_lightMapIndex)
    #define LightScaleOffset UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial,_LightScaleOffset)
    TEXTURE2D_ARRAY(_InstanceLightMapArray);        SAMPLER(sampler_InstanceLightMapArray);
    TEXTURE2D_ARRAY(_InstanceShadowMaskArray);      SAMPLER(sampler_InstanceShadowMaskArray);
#endif

#ifdef CUSTOM_INSTANCING_ON

    #define LIGHTMAP_NAME _InstanceLightMapArray
    #define LIGHTMAP_INDIRECTION_NAME unity_LightmapsInd
    #define LIGHTMAP_SAMPLER_NAME sampler_InstanceLightMapArray
    #define LIGHTMAP_SAMPLE_EXTRA_ARGS staticLightmapUV, 0
    #define TEXTURE2D_LIGHTMAP_PARAM TEXTURE2D_ARRAY_PARAM
#endif
//staticLmName 是UV
//shName
#ifdef CUSTOM_INSTANCING_ON
    #ifdef CUSTOM_LIGHTMAP_ON
        #define GETGI(staticLmName, shName, normalWSName) GetLightData(staticLmName, LightMapIndex, normalWSName)
        #define GETGIDATA(lightmapUV, OUT) OUT.xy = lightmapUV.xy * LightScaleOffset.xy + LightScaleOffset.zw;
        #define GETOUTPUT_SH(normalWS, OUT)
        #define LIGHTMAP_OR_SH(lmName, shName, index) float2 lmName : TEXCOORD##index
    #else
        #define GETGI(staticLmName, shName, normalWSName) InstanceSampleSHPixel(normalWSName)
        #define GETGIDATA(lightmapUV, OUT)
        #define GETOUTPUT_SH(normalWS, OUT)  OUT.xyz = InstanceSampleShVertex(normalWS);
        #define LIGHTMAP_OR_SH(lmName, shName, index) half3 shName : TEXCOORD##index
    #endif
#else
    #define GETGI(staticLmName, shName, normalWSName) SAMPLE_GI(staticLmName, shName, normalWSName)
    #define GETGIDATA(lightmapUV, OUT) OUTPUT_LIGHTMAP_UV(lightmapUV,unity_LightmapST, OUT)
    #define GETOUTPUT_SH(normalWS, OUT) OUTPUT_SH(normalWS, OUT)
    #define LIGHTMAP_OR_SH(lmName, shName, index) DECLARE_LIGHTMAP_OR_SH(lmName, shName, index)
#endif


#ifdef CUSTOM_LIGHTMAP_ON
    #define _LIGHTMAP_ON
#endif
#ifdef CUSTOM_LIGHTMAP_ON
    #define _SHADOWMASK
#endif


#ifdef CUSTOM_INSTANCING_ON
    #if defined(_SHADOWMASK) && defined(_LIGHTMAP_ON)
        #define GETSHADOWMASK(uv) SAMPLE_TEXTURE2D_ARRAY(_InstanceShadowMaskArray,sampler_InstanceShadowMaskArray,uv,LightMapIndex);
    #else
        #define GETSHADOWMASK(uv) SAMPLE_SHADOWMASK(uv)
    #endif
#else
    #define GETSHADOWMASK(uv) SAMPLE_SHADOWMASK(uv)
#endif

half3 InstanceSampleSHPixel(half3 normalWS)
{
    half3 res = SHEvalLinearL0L1(normalWS, instance_SHAr, instance_SHAg, instance_SHAb);
    #ifdef UNITY_COLORSPACE_GAMMA
        res = LinearToSRGB(res);
    #endif
    return res;
}

half3 InstanceSampleShVertex(float3 normal)
{
    return half3(0,0,0);
}
real3 InstanceSampleSingleLightmap(TEXTURE2D_LIGHTMAP_PARAM(lightmapTex, lightmapSampler), float2 uv,float index, float4 transform, bool encodedLightmap, real4 decodeInstructions)
{
    //transform is scale and bias
    uv = uv * transform.xy + transform.zw;
    real3 illuminance = real3(0.0, 0.0, 0.0);
    // Remark: baked lightmap is RGBM for now, dynamic lightmap is RGB9E5
    if (encodedLightmap)
    {
        real4 encodedIlluminance = SAMPLE_TEXTURE2D_ARRAY(lightmapTex, lightmapSampler, uv,index).rgba;
        illuminance = DecodeLightmap(encodedIlluminance, decodeInstructions);
    }
    else
    {
        illuminance = SAMPLE_TEXTURE2D_ARRAY(lightmapTex, lightmapSampler, uv,index).rgb;
    }
    return illuminance;
}


void SampleDirectionalLightmap(TEXTURE2D_LIGHTMAP_PARAM(lightmapTex, lightmapSampler), TEXTURE2D_LIGHTMAP_PARAM(lightmapDirTex, lightmapDirSampler), float2 uv,float index, float4 transform,
    float3 normalWS, float3 backNormalWS, bool encodedLightmap, real4 decodeInstructions, inout real3 bakeDiffuseLighting, inout real3 backBakeDiffuseLighting)
{
    // In directional mode Enlighten bakes dominant light direction
    // in a way, that using it for half Lambert and then dividing by a "rebalancing coefficient"
    // gives a result close to plain diffuse response lightmaps, but normalmapped.

    // Note that dir is not unit length on purpose. Its length is "directionality", like
    // for the directional specular lightmaps.

    // transform is scale and bias
    uv = uv * transform.xy + transform.zw;

    real4 direction = SAMPLE_TEXTURE2D_ARRAY(lightmapDirTex, lightmapDirSampler, uv,index);
    // Remark: baked lightmap is RGBM for now, dynamic lightmap is RGB9E5
    real3 illuminance = real3(0.0, 0.0, 0.0);
    if (encodedLightmap)
    {
        real4 encodedIlluminance = SAMPLE_TEXTURE2D_ARRAY(lightmapTex, lightmapSampler, uv,index).rgba;
        illuminance = DecodeLightmap(encodedIlluminance, decodeInstructions);
    }
    else
    {
        illuminance = SAMPLE_TEXTURE2D_ARRAY(lightmapTex, lightmapSampler, uv,index).rgb;
    }

    real halfLambert = dot(normalWS, direction.xyz - 0.5) + 0.5;
    bakeDiffuseLighting += illuminance * halfLambert / max(1e-4, direction.w);

    real backHalfLambert = dot(backNormalWS, direction.xyz - 0.5) + 0.5;
    backBakeDiffuseLighting += illuminance * backHalfLambert / max(1e-4, direction.w);
}


real3 InstanceSampleDirectionalLightmap(TEXTURE2D_LIGHTMAP_PARAM(lightmapTex, lightmapSampler), TEXTURE2D_LIGHTMAP_PARAM(lightmapDirTex, lightmapDirSampler), float2 uv,float index, float4 transform,
    float3 normalWS, bool encodedLightmap, real4 decodeInstructions)
{
    float3 backNormalWSUnused = 0.0;
    real3 bakeDiffuseLighting = 0.0;
    real3 backBakeDiffuseLightingUnused = 0.0;
    SampleDirectionalLightmap(TEXTURE2D_LIGHTMAP_ARGS(lightmapTex, lightmapSampler), TEXTURE2D_LIGHTMAP_ARGS(lightmapDirTex, lightmapDirSampler), uv,index, transform,
                                normalWS, backNormalWSUnused, encodedLightmap, decodeInstructions, bakeDiffuseLighting, backBakeDiffuseLightingUnused);

    return bakeDiffuseLighting;
}


half3 GetLightData(float2 staticLightmapUV, float index, half3 normalWS)
{
    #ifdef UNITY_LIGHTMAP_FULL_HDR
    bool encodedLightmap = false;
    #else
    bool encodedLightmap = true;
    #endif
    half4 decodeInstructions = half4(LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h);

    // The shader library sample lightmap functions transform the lightmap uv coords to apply bias and scale.
    // However, universal pipeline already transformed those coords in vertex. We pass half4(1, 1, 0, 0) and
    // the compiler will optimize the transform away.
    half4 transformCoords = half4(1, 1, 0, 0);

    float3 diffuseLighting = 0;
    
    #if defined(CUSTOM_LIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED)
    diffuseLighting = InstanceSampleDirectionalLightmap(TEXTURE2D_LIGHTMAP_ARGS(LIGHTMAP_NAME, LIGHTMAP_SAMPLER_NAME),
        TEXTURE2D_LIGHTMAP_ARGS(LIGHTMAP_INDIRECTION_NAME, LIGHTMAP_SAMPLER_NAME),
        staticLightmapUV,index, transformCoords, normalWS, encodedLightmap, decodeInstructions);
    #elif defined(CUSTOM_LIGHTMAP_ON)
    diffuseLighting = InstanceSampleSingleLightmap(TEXTURE2D_LIGHTMAP_ARGS(LIGHTMAP_NAME, LIGHTMAP_SAMPLER_NAME),staticLightmapUV,index , transformCoords, encodedLightmap, decodeInstructions);
    #endif
    return diffuseLighting;
}
#endif