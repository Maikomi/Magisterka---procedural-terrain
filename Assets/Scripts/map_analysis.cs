using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;

public class map_analysis : MonoBehaviour
{
    public void GenerateHeightMap(TerrainData data, string fileName = "heightmap")
    {

        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);

        Texture2D heightMap = new Texture2D(res, res, TextureFormat.RGB24, false);
        Texture2D heightMapEXR = new Texture2D(res, res, TextureFormat.RGBAFloat, false);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float h = heights[y, x];
                heightMap.SetPixel(x, y, new Color(h, h, h));
                heightMapEXR.SetPixel(x, y, new Color(h, h, h, 1f));
            }
        }
        map_helper.SavePngAndExr(heightMap, heightMapEXR, fileName);
    }

    public void GenerateSlopeMap(TerrainData data, string fileName = "slopemap")
    {

        int res = data.heightmapResolution;

        Texture2D slopeMap = new Texture2D(res, res, TextureFormat.RGB24, false);
        Texture2D slopeMapEXR = new Texture2D(res, res, TextureFormat.RGBAFloat, false);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float normX = (float)x / (res - 1);
                float normY = (float)y / (res - 1);

                float slope = data.GetSteepness(normX, normY) / 90f;

                slopeMap.SetPixel(x, y, new Color(slope, slope, slope));
                slopeMapEXR.SetPixel(x, y, new Color(slope, slope, slope, 1f));
            }
        }
        map_helper.SavePngAndExr(slopeMap, slopeMapEXR, fileName);
    }

    public void GenerateAspectMap(TerrainData data, string fileName = "aspectmap")
    {

        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);

        Texture2D aspectMap = new Texture2D(res, res, TextureFormat.RGB24, false);
        Texture2D aspectMapEXR = new Texture2D(res, res, TextureFormat.RGBAFloat, false);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                if (x > 0 && x < res - 1 && y > 0 && y < res - 1)
                {
                    float dx = heights[y, x + 1] - heights[y, x - 1];
                    float dy = heights[y + 1, x] - heights[y - 1, x];

                    float aspect = Mathf.Atan2(dy, dx);
                    aspect = (aspect + Mathf.PI) / (2f * Mathf.PI);

                    aspectMap.SetPixel(x, y, new Color(aspect, aspect, aspect));
                    aspectMapEXR.SetPixel(x, y, new Color(aspect, aspect, aspect, 1f));

                }
                else
                {
                    aspectMap.SetPixel(x, y, Color.black);
                    aspectMapEXR.SetPixel(x, y, Color.black);
                }
            }
        }
        map_helper.SavePngAndExr(aspectMap, aspectMapEXR, fileName);
    }
    public void GenerateMoistureMap(MapInputData inputData, string moistureMapFileName, bool generateMoisturePreview)
    {
        Texture2D moistureMap = map_helper.CreateFloatMap(inputData.resolution);

        map_helper.ForEachPixel(inputData.resolution, (x, y, normX, normY) =>
        {
            float moisture = CalculateMoisture(inputData.GetHeight(x, y, normX, normY), inputData.GetSlope(normX, normY), inputData.GetExposure(normX, normY));
            map_helper.SetGrayscalePixel(moistureMap, x, y, moisture);
        });

        moistureMap.Apply();
        map_helper.SaveExr(moistureMap, moistureMapFileName);

        if (generateMoisturePreview)
        {
            string moisturePreviewFileName = moistureMapFileName + "_preview";
            map_helper.SavePng(moistureMap, moisturePreviewFileName);
        }
    }

    float CalculateMoisture(float height, float slope, float exposure)
    {
        return Mathf.Clamp01(
            0.3f * (1f - height)
            + 0.2f * (1f - slope)
            + 0.5f * (1f - exposure)
        );
    }
}
