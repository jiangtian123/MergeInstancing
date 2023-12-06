using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.MergeInstancingSystem.Render
{
    public class ConvertToTexture2Darray : MonoBehaviour
    {
        private Texture2DArray m_lightMapArray;
        private Texture2DArray m_ShadowMaskArray;
        private static bool isInit = false;
        private static ConvertToTexture2Darray m_instance;

        public static ConvertToTexture2Darray Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = FindObjectOfType<ConvertToTexture2Darray>(); 
                    if (m_instance == null)
                    {
                        GameObject singletonObject = new GameObject();
                        m_instance = singletonObject.AddComponent<ConvertToTexture2Darray>();
                        singletonObject.name = "ConvertToTexture2Darray";
                    };
                    if (!isInit)
                    {
                        m_instance.ConvertToTexture2DArray();
                    }
                }
                return m_instance;
            }
        }
        private void Awake()
        {
            ConvertToTexture2DArray();
        }
        private void ConvertToTexture2DArray()
        {
            LightmapData[] lightmaps =  LightmapSettings.lightmaps;
            if (lightmaps == null || lightmaps.Length == 0)
            {
                return;
            }
            List<Texture2D> m_LightMap = new List<Texture2D>();
            List<Texture2D> m_ShadowMaskMap = new List<Texture2D>();
            for (int i = 0; i < lightmaps.Length; i++)
            {
                m_LightMap.Add(lightmaps[i].lightmapColor);
                m_ShadowMaskMap.Add(lightmaps[i].shadowMask);
            }
            var lightMap = m_LightMap.ToArray();
            var shadowMask = m_ShadowMaskMap.ToArray();
            if (lightMap.Length == 0 && shadowMask.Length == 0)
            {
                m_lightMapArray = null;
                m_ShadowMaskArray = null;
            }
            var lightMapTexture = lightMap[0];
            var shadowMaskTexture = shadowMask[0];
            Texture2DArray lightMapArray = new Texture2DArray(lightMapTexture.width, lightMapTexture.height, lightMap.Length, lightMapTexture.format, false, false);
            lightMapArray.name = lightMapTexture.name;
            Texture2DArray shadowMaskArray = new Texture2DArray(shadowMaskTexture.width, shadowMaskTexture.height, shadowMask.Length, shadowMaskTexture.format, false, false);
            shadowMaskArray.name = shadowMaskTexture.name;
            for (int i = 0; i < lightMap.Length; i++)
            {
                Graphics.CopyTexture(lightMap[i], 0, 0, lightMapArray, i, 0);
            }

            for (int i = 0; i < shadowMask.Length; i++)
            {
                Graphics.CopyTexture(shadowMask[i], 0, 0, shadowMaskArray, i, 0);
            }
            m_lightMapArray = lightMapArray;
            m_ShadowMaskArray = shadowMaskArray;
            isInit = true;
        }

        private void OnEnable()
        {
            m_lightMapArray = null;
            m_ShadowMaskArray = null;
            isInit = false;
        }

        private void OnDestroy()
        {
            isInit = false;
            m_lightMapArray = null;
            m_ShadowMaskArray = null;
        }

        public Texture2DArray GetLightMapArray()
        {
            return m_lightMapArray;
        }
        public Texture2DArray GetShadowMaskArray()
        {
            return m_ShadowMaskArray;
        }
        
    }
}