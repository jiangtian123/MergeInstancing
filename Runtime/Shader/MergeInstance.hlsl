#ifndef CUNSTOM_INSTANCE_DATA
#define CUNSTOM_INSTANCE_DATA


#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
float4 instance_SHAr;
float4 instance_SHAg;
float4 instance_SHAb;
Texture2D<float4> _Matrixs;
Texture2D<float4> _LightMapOffest;
//x 是矩阵的索引，y是光照offest，z 是lightmap的索引
CBUFFER_START(InstanceIndexBuffer)
    float4 _dataIndexAndLightIndex[1024];
CBUFFER_END

TEXTURE2D_ARRAY(_InstanceLightMapArray);        SAMPLER(sampler_InstanceLightMapArray);
TEXTURE2D_ARRAY(_InstanceShadowMaskArray);      SAMPLER(sampler_InstanceShadowMaskArray);

static uint _instanceId;

#define LightScaleOffset _LightMapOffest[float2(0,_dataIndexAndLightIndex[_instanceId].y)]
#define LightMapIndex _dataIndexAndLightIndex[_instanceId].z
#define LIGHTMAP_NAME _InstanceLightMapArray
#define LIGHTMAP_INDIRECTION_NAME unity_LightmapsInd
#define LIGHTMAP_SAMPLER_NAME sampler_InstanceLightMapArray
#define TEXTURE2D_LIGHTMAP_PARAM TEXTURE2D_ARRAY_PARAM


void SetUpInstanceId(uint inputInstanceID)
{
    _instanceId = inputInstanceID;
}

float4x4 InstanceGetObjectToWorldMatrix()
{
    int matrixIndex = _dataIndexAndLightIndex[_instanceId].x;
    float4x4 objectToWorld;
    objectToWorld[0] = _Matrixs[float2(0,matrixIndex)];
    objectToWorld[1] = _Matrixs[float2(1,matrixIndex)];
    objectToWorld[2] = _Matrixs[float2(2,matrixIndex)];
    objectToWorld[3] = _Matrixs[float2(3,matrixIndex)];
    return objectToWorld;
}
float4x4 InstanceGetWorldToObjectMatrix()
{
    int matrixIndex = _dataIndexAndLightIndex[_instanceId].x;
    float4x4 worldToObject;
    worldToObject[0] = _Matrixs[float2(4,matrixIndex)];
    worldToObject[1] = _Matrixs[float2(5,matrixIndex)];
    worldToObject[2] = _Matrixs[float2(6,matrixIndex)];
    worldToObject[3] = _Matrixs[float2(7,matrixIndex)];
    return worldToObject;
}
float3 InstanceTransformObjectToWorldDir(float3 dirOS, bool doNormalize = true)
{

    float3 dirWS = mul((float3x3)InstanceGetObjectToWorldMatrix(), dirOS);
    if (doNormalize)
        return SafeNormalize(dirWS);
    return dirWS;
}

float3 InstanceTransformObjectToWorldNormal(float3 normalOS,bool doNormalize = true)
{
    // Normal need to be multiply by inverse transpose
    float3 normalWS = mul(normalOS, (float3x3)InstanceGetWorldToObjectMatrix());
    if (doNormalize)
        return SafeNormalize(normalWS);
    return normalOS;
}

VertexPositionInputs GetInstancePositionInputs(float3 positionOS)
{
    float4x4 o2w = InstanceGetObjectToWorldMatrix();
    VertexPositionInputs input;
    input.positionWS = mul(o2w, float4(positionOS, 1.0)).xyz;
    input.positionVS = TransformWorldToView(input.positionWS);
    input.positionCS = TransformWorldToHClip(input.positionWS);
    float4 ndc = input.positionCS * 0.5f;
    input.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    input.positionNDC.zw = input.positionCS.zw;
    return input;
}
VertexNormalInputs GetInstanceVertexNormalInputs(float3 normalOS)
{
    VertexNormalInputs tbn;
    tbn.tangentWS = real3(1.0, 0.0, 0.0);
    tbn.bitangentWS = real3(0.0, 1.0, 0.0);
    tbn.normalWS = InstanceTransformObjectToWorldNormal(normalOS);
    return tbn;
}

VertexNormalInputs GetInstanceVertexNormalInputs(float3 normalOS, float4 tangentOS)
{
    VertexNormalInputs tbn;

    // mikkts space compliant. only normalize when extracting normal at frag.
    real sign = real(tangentOS.w) * GetOddNegativeScale();
    tbn.normalWS = InstanceTransformObjectToWorldNormal(normalOS);
    tbn.tangentWS = real3(InstanceTransformObjectToWorldDir(tangentOS.xyz));
    tbn.bitangentWS = real3(cross(tbn.normalWS, float3(tbn.tangentWS))) * sign;
    return tbn;
}

void SampleDirectionalLightmap(TEXTURE2D_LIGHTMAP_PARAM(lightmapTex, lightmapSampler), TEXTURE2D_LIGHTMAP_PARAM(lightmapDirTex, lightmapDirSampler), float2 uv,float index, float4 transform,
    float3 normalWS, float3 backNormalWS, bool encodedLightmap, real4 decodeInstructions, inout real3 bakeDiffuseLighting, inout real3 backBakeDiffuseLighting)
{
    uv = uv * transform.xy + transform.zw;

    real4 direction = SAMPLE_TEXTURE2D_ARRAY(lightmapDirTex, lightmapDirSampler, uv,index);
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

half3 InstanceSampleSHPixel(half3 L2Term,half3 normalWS)
{
    #if defined(EVALUATE_SH_VERTEX)
        return L2Term;
    #elif defined(EVALUATE_SH_MIXED)
        half3 res = SHEvalLinearL0L1(normalWS, unity_SHAr, unity_SHAg, unity_SHAb);
    #ifdef UNITY_COLORSPACE_GAMMA
    res = LinearToSRGB(res);
    #endif
        return max(half3(0, 0, 0), res);
    #endif
    return SampleSH(normalWS);
}
half3 CuntomSampleSH(half3 normalWS)
{
    // LPPV is not supported in Ligthweight Pipeline
    real4 SHCoefficients[7];
    SHCoefficients[0] = unity_SHAr;
    SHCoefficients[1] = unity_SHAg;
    SHCoefficients[2] = unity_SHAb;
    SHCoefficients[3] = unity_SHBr;
    SHCoefficients[4] = unity_SHBg;
    SHCoefficients[5] = unity_SHBb;
    SHCoefficients[6] = unity_SHC;

    return max(half3(0, 0, 0), SampleSH9(SHCoefficients, normalWS));
}
half3 InstanceSampleShVertex(float3 normalWS)
{
    #if defined(EVALUATE_SH_VERTEX)
    return CuntomSampleSH(normalWS);
    #elif defined(EVALUATE_SH_MIXED)
    // no max since this is only L2 contribution
    return SHEvalLinearL2(normalWS, unity_SHBr, unity_SHBg, unity_SHBb, unity_SHC);
    #endif
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