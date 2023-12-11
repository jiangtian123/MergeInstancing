#ifndef CUSTOM_INSTANCE_LIGHT
#define CUSTOM_INSTANCE_LIGHT
#include "Packages/com.unity.MergeInstancing/Runtime/Shader/MergeInstance.hlsl"
#pragma multi_compile_local _ CUSTOM_INSTANCING_ON
#pragma multi_compile_local _ CUSTOM_LIGHTMAP_ON

#ifdef CUSTOM_LIGHTMAP_ON
    #define _LIGHTMAP_ON
#endif
#ifdef CUSTOM_LIGHTMAP_ON
    #define _SHADOWMASK
#endif

#ifdef SHADER_API_PSSL
    #define DEFAULT_UNITY_VERTEX_INPUT_INSTANCE_ID uint instanceID;
    #define UNITY_GET_INSTANCE_ID(input)    _GETINSTANCEID(input)
#else
    #define DEFAULT_UNITY_VERTEX_INPUT_INSTANCE_ID uint instanceID : SV_InstanceID;
    #define UNITY_GET_INSTANCE_ID(input)    input.instanceID
#endif

#ifndef UNITY_TRANSFER_INSTANCE_ID
    #define UNITY_TRANSFER_INSTANCE_ID(input, output)   output.instanceID = UNITY_GET_INSTANCE_ID(input)
#endif

#if !defined(UNITY_VERTEX_INPUT_INSTANCE_ID)
#   define UNITY_VERTEX_INPUT_INSTANCE_ID DEFAULT_UNITY_VERTEX_INPUT_INSTANCE_ID
#endif

#ifdef CUSTOM_INSTANCING_ON
    #define GetVertexData(vertexInputn,positionOs) vertexInput = GetInstancePositionInputs(positionOs); 
    #define OBJECT_TO_WORLD_MATRIX InstanceGetObjectToWorldMatrix()
    #define WORLD_TO_OBJECT_MATRIX InstanceGetWorldToObjectMatrix()
    #define CUSTOM_SETUP_INSTANCE_ID(input) SetUpInstanceId(UNITY_GET_INSTANCE_ID(input));
    #define GetVertexNormal(normalInput,normalOs) normalInput = GetInstanceVertexNormalInputs(normalOs);
    #define GetVertexNormalWithTang(normalInput,normalOs,tangentOS) normalInput = GetInstanceVertexNormalInputs(normalOs,tangentOS);
    //在自定义Instance渲染模式下使用了LightMap
    #ifdef _LIGHTMAP_ON
        #define LIGHTMAP_OR_SH(lmName, shName, index)   float2 lmName : TEXCOORD##index
        #define GETLIGHTMAP_UV(lightmapUV, OUT) OUT.xy = lightmapUV.xy * LightScaleOffset.xy + LightScaleOffset.zw;
        #define CUSTOM_SAMPLE_GI(staticLmName, shName, normalWSName) GetLightData(staticLmName, LightMapIndex, normalWSName)
        #define GETOUTPUT_SH(normalWS, OUT)
        #define CUSTOM_SAMPLE_SHADOWMASK(uv) SAMPLE_TEXTURE2D_ARRAY(_InstanceShadowMaskArray,sampler_InstanceShadowMaskArray,uv,LightMapIndex)
    #else
        #define LIGHTMAP_OR_SH(lmName, shName, index)   half3 shName : TEXCOORD##index
        #define GETLIGHTMAP_UV(staticLmName, shName)
        #define CUSTOM_SAMPLE_GI(staticLmName, shName, normalWSName) InstanceSampleSHPixel(shName,normalWSName)
        #define GETOUTPUT_SH(normalWS, OUT)  OUT.xyz = InstanceSampleShVertex(normalWS)
        #define CUSTOM_SAMPLE_SHADOWMASK(uv) half4(1, 1, 1, 1)
    #endif
#else
    #define GetVertexData(vertexInput,positionOs) vertexInput = GetVertexPositionInputs(positionOs);
    #define OBJECT_TO_WORLD_MATRIX GetObjectToWorldMatrix()
    #define WORLD_TO_OBJECT_MATRIX GetWorldToObjectMatrix()
    #define CUSTOM_SETUP_INSTANCE_ID(input) UNITY_SETUP_INSTANCE_ID(input)
    #define GetVertexNormal(normalInput,normalOs) normalInput = GetVertexNormalInputs(normalOs);
    #define GetVertexNormalWithTang(normalInput,normalOs,tangentOS) normalInput = GetVertexNormalInputs(normalOs,tangentOS);
    //光照计算
    #define LIGHTMAP_OR_SH(lmName, shName, index) DECLARE_LIGHTMAP_OR_SH(lmName, shName, index)
    #define GETLIGHTMAP_UV(lightmapUV, OUT) OUTPUT_LIGHTMAP_UV(lightmapUV,unity_LightmapST, OUT)
    #define CUSTOM_SAMPLE_GI(staticLmName, shName, normalWSName) SAMPLE_GI(staticLmName, shName, normalWSName)
    #define GETOUTPUT_SH(normalWS, OUT) OUTPUT_SH(normalWS, OUT)
    #define CUSTOM_SAMPLE_SHADOWMASK(uv) SAMPLE_SHADOWMASK(uv)
#endif 
#endif