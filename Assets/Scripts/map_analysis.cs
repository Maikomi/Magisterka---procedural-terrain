using UnityEngine;
using System.Collections;
using System.IO;
using System;

public class map_analysis : MonoBehaviour
{
    public Terrain terrain;
    public DailySolarExposure solarExposureGenerator;

    [Header("Maps To Generate")]
    public bool generateMoistureMap = true;

    [Header("Input Maps")]
    public string heightMapFileName = "heightmap.exr";
    public string slopeMapFileName = "slopemap.exr";
    public string annualSolarExposureMapFileName = "";
    public int annualSamples = 12;

    [Header("Moisture Output")]
    public bool generateMoisturePreview = true;
    public string moistureMapFileName = "moistureMap";
    public string moisturePreviewFileName = "MoisturePreview";

    [Header("Plant Preferences")]
    public bool generatePlantSuitabilityPreviews = true;
    public PlantPreference[] plantPreferences =
    {
        new PlantPreference("buk", 0.1f, 0.0f, 0.5f, 0.6f),
        new PlantPreference("swierk", 0.7f, 0.6f, 0.5f, 0.7f),
        new PlantPreference("trawa", 0.5f, 0.2f, 0.6f, 0.4f)
    };

    IEnumerator Start()
    {
        yield return null;

        if (!HasAnyMapToGenerate())
        {
            Debug.Log("Map analysis generation is disabled.");
            yield break;
        }

        GenerateSelectedMaps();
    }

    void GenerateSelectedMaps()
    {
        MapInputData inputData = PrepareInputData();

        if (!inputData.IsValid)
        {
            return;
        }

        if (generateMoistureMap)
        {
            Texture2D moistureMap = GenerateMoistureMap(inputData);
            SaveExr(moistureMap, moistureMapFileName);

            if (generateMoisturePreview)
            {
                SavePng(moistureMap, moisturePreviewFileName);
            }

            inputData.moistureMap = moistureMap;
            Debug.Log("Moisture map saved!");
        }

        GeneratePlantSuitabilityMaps(inputData);
    }

    MapInputData PrepareInputData()
    {
        ResolveSceneReferences();

        string mapsPath = GetMapsPath();
        Texture2D heightMap = LoadMap(Path.Combine(mapsPath, heightMapFileName));
        Texture2D slopeMap = LoadMap(Path.Combine(mapsPath, slopeMapFileName));
        Texture2D exposureMap = LoadMap(GetAnnualSolarExposurePath(mapsPath));
        Texture2D moistureMap = LoadMap(Path.Combine(mapsPath, $"{moistureMapFileName}.exr"));

        if (exposureMap == null)
        {
            Debug.LogWarning("Annual solar exposure map was not found. Map analysis was not generated.");
            return MapInputData.Invalid;
        }

        TerrainData terrainData = terrain != null ? terrain.terrainData : null;

        if (heightMap == null && terrainData == null)
        {
            Debug.LogWarning("Height map was not found and no terrain is available. Map analysis was not generated.");
            return MapInputData.Invalid;
        }

        if (slopeMap == null && terrainData == null)
        {
            Debug.LogWarning("Slope map was not found and no terrain is available. Map analysis was not generated.");
            return MapInputData.Invalid;
        }

        int resolution = GetOutputResolution(terrainData, heightMap, slopeMap, exposureMap);

        if (resolution <= 0)
        {
            Debug.LogWarning("Could not determine map analysis resolution.");
            return MapInputData.Invalid;
        }

        float[,] terrainHeights = null;

        if ((heightMap == null || slopeMap == null) && terrainData != null)
        {
            terrainHeights = terrainData.GetHeights(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution);
        }

        return new MapInputData(
            resolution,
            terrainData,
            terrainHeights,
            heightMap,
            slopeMap,
            exposureMap,
            moistureMap
        );
    }

    Texture2D GenerateMoistureMap(MapInputData inputData)
    {
        Texture2D moistureMap = CreateFloatMap(inputData.resolution);

        ForEachPixel(inputData.resolution, (x, y, normX, normY) =>
        {
            float moisture = CalculateMoisture(inputData.GetHeight(x, y, normX, normY), inputData.GetSlope(normX, normY), inputData.GetExposure(normX, normY));
            SetGrayscalePixel(moistureMap, x, y, moisture);
        });

        moistureMap.Apply();
        return moistureMap;
    }

    void GeneratePlantSuitabilityMaps(MapInputData inputData)
    {
        if (plantPreferences == null)
        {
            return;
        }

        for (int i = 0; i < plantPreferences.Length; i++)
        {
            PlantPreference plant = plantPreferences[i];

            if (plant == null || !plant.generateSuitabilityMap)
            {
                continue;
            }

            Texture2D suitabilityMap = GeneratePlantSuitabilityMap(inputData, plant);
            string fileName = $"{SanitizeFileName(plant.plantName)}SuitabilityMap";

            SaveExr(suitabilityMap, fileName);

            if (generatePlantSuitabilityPreviews)
            {
                SavePng(suitabilityMap, fileName);
            }

            Debug.Log($"{plant.plantName} suitability map saved!");
        }
    }

    Texture2D GeneratePlantSuitabilityMap(MapInputData inputData, PlantPreference plant)
    {
        Texture2D suitabilityMap = CreateFloatMap(inputData.resolution);

        ForEachPixel(inputData.resolution, (x, y, normX, normY) =>
        {
            float height = inputData.GetHeight(x, y, normX, normY);
            float slope = inputData.GetSlope(normX, normY);
            float exposure = inputData.GetExposure(normX, normY);
            float moisture = inputData.GetMoisture(x, y, normX, normY);
            float suitability = plant.CalculateSuitability(height, slope, exposure, moisture);

            SetGrayscalePixel(suitabilityMap, x, y, suitability);
        });

        suitabilityMap.Apply();
        return suitabilityMap;
    }

    float CalculateMoisture(float height, float slope, float exposure)
    {
        return Mathf.Clamp01(
            0.3f * (1f - height)
            + 0.2f * (1f - slope)
            + 0.5f * (1f - exposure)
        );
    }

    void ResolveSceneReferences()
    {
        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        if (solarExposureGenerator == null)
        {
            solarExposureGenerator = FindAnyObjectByType<DailySolarExposure>();
        }

        if (solarExposureGenerator != null)
        {
            annualSamples = Mathf.Clamp(solarExposureGenerator.annualSamples, 1, 365);
        }
    }

    bool HasAnyMapToGenerate()
    {
        if (generateMoistureMap)
        {
            return true;
        }

        if (plantPreferences == null)
        {
            return false;
        }

        for (int i = 0; i < plantPreferences.Length; i++)
        {
            if (plantPreferences[i] != null && plantPreferences[i].generateSuitabilityMap)
            {
                return true;
            }
        }

        return false;
    }

    string GetMapsPath()
    {
        string mapsPath = Application.dataPath + "/maps";
        Directory.CreateDirectory(mapsPath);
        return mapsPath;
    }

    string GetAnnualSolarExposurePath(string mapsPath)
    {
        if (!string.IsNullOrWhiteSpace(annualSolarExposureMapFileName))
        {
            return Path.Combine(mapsPath, annualSolarExposureMapFileName);
        }

        int sampleCount = Mathf.Clamp(annualSamples, 1, 365);
        return Path.Combine(mapsPath, $"AnnualSolarExposure_{sampleCount}.exr");
    }

    Texture2D LoadMap(string path)
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

    int GetOutputResolution(TerrainData terrainData, Texture2D heightMap, Texture2D slopeMap, Texture2D exposureMap)
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

    Texture2D CreateFloatMap(int resolution)
    {
        return new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false);
    }

    void SaveExr(Texture2D map, string fileName)
    {
        File.WriteAllBytes(
            Path.Combine(GetMapsPath(), $"{fileName}.exr"),
            map.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat)
        );
    }

    void SavePng(Texture2D map, string fileName)
    {
        File.WriteAllBytes(
            Path.Combine(GetMapsPath(), $"{fileName}.png"),
            map.EncodeToPNG()
        );
    }

    void SetGrayscalePixel(Texture2D map, int x, int y, float value)
    {
        float normalizedValue = Mathf.Clamp01(value);
        map.SetPixel(x, y, new Color(normalizedValue, normalizedValue, normalizedValue, 1f));
    }

    void ForEachPixel(int resolution, PixelAction action)
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

    string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "Plant";
        }

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName;
    }

    delegate void PixelAction(int x, int y, float normX, float normY);

    class MapInputData
    {
        public static readonly MapInputData Invalid = new MapInputData();

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

        MapInputData()
        {
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
}
