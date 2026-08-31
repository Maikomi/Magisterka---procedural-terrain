using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;

public class map_helper
{
    public static string currentRunFolderPath;

    public static string GetMapsPath()
    {
        string mapsPath = Application.dataPath + "/maps";
        Directory.CreateDirectory(mapsPath);
        return mapsPath;
    }

    public static string EnsureRunFolder()
    {
        string mapsPath = GetMapsPath();
        string runFolderName = "run_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        currentRunFolderPath = Path.Combine(mapsPath, runFolderName);
        Directory.CreateDirectory(currentRunFolderPath);
        RemoveMetaFiles(currentRunFolderPath);
        return currentRunFolderPath;
    }

    public static void CopyToRunFolder(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return;
        }

        if (string.IsNullOrEmpty(currentRunFolderPath))
        {
            EnsureRunFolder();
        }

        string destinationPath = Path.Combine(currentRunFolderPath, Path.GetFileName(filePath));
        if (!File.Exists(destinationPath))
        {
            File.Copy(filePath, destinationPath, true);
        }

        RemoveMetaFiles(currentRunFolderPath);
    }

    public static void RemoveMetaFiles(string directoryPath)
    {
        if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
        {
            return;
        }

        string[] metaFiles = Directory.GetFiles(directoryPath, "*.meta", SearchOption.AllDirectories);
        foreach (string metaFile in metaFiles)
        {
            try
            {
                File.Delete(metaFile);
            }
            catch
            {
                // Ignore meta file cleanup failures.
            }
        }
    }

    public static Texture2D LoadMap(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBAFloat, false, true);
        byte[] bytes = File.ReadAllBytes(path);

        if (!texture.LoadImage(bytes))
        {
            Debug.LogWarning($"Could not load map: {path}");
            return null;
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    public static int GetOutputResolution(TerrainData terrainData, Texture2D heightMap, Texture2D slopeMap, Texture2D exposureMap)
    {
        if (terrainData != null)
        {
            return terrainData.heightmapResolution;
        }

        if (heightMap != null)
        {
            return heightMap.width;
        }

        if (slopeMap != null)
        {
            return slopeMap.width;
        }

        return exposureMap != null ? exposureMap.width : 0;
    }

    public static Texture2D CreateFloatMap(int resolution)
    {
        return new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false);
    }

    public static void SaveExr(Texture2D map, string fileName)
    {
        string outputPath = Path.Combine(GetMapsPath(), $"{fileName}.exr");
        File.WriteAllBytes(outputPath, map.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
        CopyToRunFolder(outputPath);
    }

    public static void SavePng(Texture2D map, string fileName)
    {
        string outputPath = Path.Combine(GetMapsPath(), $"{fileName}.png");
        File.WriteAllBytes(outputPath, map.EncodeToPNG());
        CopyToRunFolder(outputPath);
    }

    public static float GetMetersPerPixel(MapInputData inputData)
    {
        if (inputData.terrainData == null)
        {
            return 1f;
        }

        float resolutionDivisor = Mathf.Max(1, inputData.resolution - 1);
        float metersPerPixelX = inputData.terrainData.size.x / resolutionDivisor;
        float metersPerPixelZ = inputData.terrainData.size.z / resolutionDivisor;

        return Mathf.Max(0.0001f, (metersPerPixelX + metersPerPixelZ) * 0.5f);
    }


    public static void SaveSeedMapPng(
        Texture2D seedMap,
        string fileName,
        MapInputData inputData,
        List<Species> species,
        List<Seed> lastGeneratedSeeds,
        float heightWeight = 1f,
        float slopeWeight = 1f,
        float exposureWeight = 1f,
        float moistureWeight = 1f)
    {
        Texture2D dominantSpeciesMap = plant_analysis.GenerateDominantSpeciesMapColored(
            inputData,
            species,
            heightWeight,
            slopeWeight,
            exposureWeight,
            moistureWeight
        );
        Texture2D overlayMap = new Texture2D(dominantSpeciesMap.width, dominantSpeciesMap.height, TextureFormat.RGBA32, false);

        overlayMap.SetPixels(dominantSpeciesMap.GetPixels());
        DrawSeedDots(overlayMap, lastGeneratedSeeds);
        overlayMap.Apply();

        string outputPath = Path.Combine(GetMapsPath(), $"{fileName}.png");
        File.WriteAllBytes(outputPath, overlayMap.EncodeToPNG());
        CopyToRunFolder(outputPath);
    }

    static public void SaveDominantSpeciesMapPng(
        Texture2D grayscaleMap,
        List<Species> species,
        string fileName,
        MapInputData inputData,
        float heightWeight = 1f,
        float slopeWeight = 1f,
        float exposureWeight = 1f,
        float moistureWeight = 1f)
    {
        Texture2D coloredMap = plant_analysis.GenerateDominantSpeciesMapColored(
            inputData,
            species,
            heightWeight,
            slopeWeight,
            exposureWeight,
            moistureWeight
        );

        string outputPath = Path.Combine(GetMapsPath(), $"{fileName}_colored.png");
        File.WriteAllBytes(outputPath, coloredMap.EncodeToPNG());
        CopyToRunFolder(outputPath);

    }

    static public void DrawSeedDots(Texture2D map, List<Seed> lastGeneratedSeeds)
    {
        if (map == null || lastGeneratedSeeds.Count == 0)
        {
            return;
        }

        for (int i = 0; i < lastGeneratedSeeds.Count; i++)
        {
            Seed seed = lastGeneratedSeeds[i];
            DrawFilledCircle(map, seed.pixel.x, seed.pixel.y, Mathf.Max(1, GetSeedDotRadius(seed.species.seedRadius)), Color.black);
        }
    }

    static public void DrawFilledCircle(Texture2D map, int centerX, int centerY, int radius, Color color)
    {
        int minX = Mathf.Max(0, centerX - radius);
        int maxX = Mathf.Min(map.width - 1, centerX + radius);
        int minY = Mathf.Max(0, centerY - radius);
        int maxY = Mathf.Min(map.height - 1, centerY + radius);
        int radiusSquared = radius * radius;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - centerX;
                int dy = y - centerY;

                if (dx * dx + dy * dy <= radiusSquared)
                {
                    map.SetPixel(x, y, color);
                }
            }
        }
    }

    public static int GetSeedDotRadius(float seedRadiusMeters)
    {
        return Mathf.Clamp(Mathf.CeilToInt(seedRadiusMeters * 0.15f), 1, 4);
    }

    public static void SetGrayscalePixel(Texture2D map, int x, int y, float value)
    {
        float normalizedValue = Mathf.Clamp01(value);
        map.SetPixel(x, y, new Color(normalizedValue, normalizedValue, normalizedValue, 1f));
    }

    public static void ForEachPixel(int resolution, PixelAction action)
    {
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float normX = resolution > 1 ? (float)x / (resolution - 1) : 0f;
                float normY = resolution > 1 ? (float)y / (resolution - 1) : 0f;

                action(x, y, normX, normY);
            }
        }
    }
    public delegate void PixelAction(int x, int y, float normX, float normY);

    public static void SavePngAndExr(Texture2D pngMap, Texture2D exrMap, string fileName)
    {
        pngMap.Apply();
        exrMap.Apply();

        byte[] pngBytes = pngMap.EncodeToPNG();
        byte[] exrBytes = exrMap.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);

        string pngPath = Application.dataPath + $"/maps/{fileName}.png";
        string exrPath = Application.dataPath + $"/maps/{fileName}.exr";

        File.WriteAllBytes(pngPath, pngBytes);
        File.WriteAllBytes(exrPath, exrBytes);

        CopyToRunFolder(pngPath);
        CopyToRunFolder(exrPath);
    }
}

public class MapInputData
{
    public readonly int resolution;
    public readonly TerrainData terrainData;
    public readonly float[,] terrainHeights;
    public readonly Texture2D heightMap;
    public readonly Texture2D slopeMap;
    public readonly Texture2D exposureMap;
    public Texture2D moistureMap;

    public bool IsValid
    {
        get { return resolution > 0 && exposureMap != null; }
    }

    public MapInputData(
        int resolution,
        TerrainData terrainData,
        float[,] terrainHeights,
        Texture2D heightMap,
        Texture2D slopeMap,
        Texture2D exposureMap,
        Texture2D moistureMap
    )
    {
        this.resolution = resolution;
        this.terrainData = terrainData;
        this.terrainHeights = terrainHeights;
        this.heightMap = heightMap;
        this.slopeMap = slopeMap;
        this.exposureMap = exposureMap;
        this.moistureMap = moistureMap;
    }

    public float GetHeight(int x, int y, float normX, float normY)
    {
        if (heightMap != null)
        {
            return GetMapValue(heightMap, normX, normY);
        }

        if (terrainHeights != null)
        {
            int heightY = Mathf.Clamp(y, 0, terrainHeights.GetLength(0) - 1);
            int heightX = Mathf.Clamp(x, 0, terrainHeights.GetLength(1) - 1);
            return Mathf.Clamp01(terrainHeights[heightY, heightX]);
        }

        return 0f;
    }

    public float GetSlope(float normX, float normY)
    {
        if (slopeMap != null)
        {
            return GetMapValue(slopeMap, normX, normY);
        }

        if (terrainData != null)
        {
            return Mathf.Clamp01(terrainData.GetSteepness(normX, normY) / 90f);
        }

        return 0f;
    }

    public float GetExposure(float normX, float normY)
    {
        return GetMapValue(exposureMap, normX, normY);
    }

    public float GetMoisture(int x, int y, float normX, float normY)
    {
        if (moistureMap != null)
        {
            return GetMapValue(moistureMap, normX, normY);
        }

        float height = GetHeight(x, y, normX, normY);
        float slope = GetSlope(normX, normY);
        float exposure = GetExposure(normX, normY);

        return Mathf.Clamp01(
            0.3f * (1f - height)
            + 0.2f * (1f - slope)
            + 0.5f * (1f - exposure)
        );
    }

    static float GetMapValue(Texture2D map, float normX, float normY)
    {
        if (map == null)
        {
            return 0f;
        }

        return Mathf.Clamp01(map.GetPixelBilinear(normX, normY).r);
    }
}