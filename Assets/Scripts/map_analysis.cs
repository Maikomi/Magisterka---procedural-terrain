using UnityEngine;
using System.Collections;
using System.Collections.Generic;
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
        new PlantPreference("buk", 0.1f, 0.0f, 0.5f, 0.6f, new Color(0.2f, 0.8f, 0.2f, 1f), 7f),
        new PlantPreference("swierk", 0.7f, 0.6f, 0.5f, 0.7f, new Color(0.2f, 0.4f, 0.8f, 1f), 5f),
        new PlantPreference("trawa", 0.5f, 0.2f, 0.6f, 0.4f, new Color(1f, 0.9f, 0.2f, 1f), 1f)
    };

    [Header("Dominant Species Map")]
    public bool generateDominantSpeciesMap = true;

    [Header("Seed Map")]
    public bool generateSeedMap = true;
    public string seedMapFileName = "SeedMap";
    [Range(0f, 1f)] public float seedSuitabilityThreshold = 0.65f;
    public int seedLocalMaximumWindowSize = 10;
    public float seedProbabilityPower = 3f;
    static readonly System.Random SeedRandom = new System.Random();
    readonly List<Seed> lastGeneratedSeeds = new List<Seed>();

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

        if (generateDominantSpeciesMap)
        {
            Texture2D dominantSpeciesMap = GenerateDominantSpeciesMap(inputData);
            SaveExr(dominantSpeciesMap, "DominantSpeciesMap");
            SaveDominantSpeciesMapPng(dominantSpeciesMap, "DominantSpeciesMap", inputData);
            Debug.Log("Dominant species map saved!");
        }

        if (generateSeedMap)
        {
            Texture2D seedMap = GenerateSeedMap(inputData);
            SaveExr(seedMap, seedMapFileName);
            SaveSeedMapPng(seedMap, seedMapFileName, inputData);
            Debug.Log("Seed map saved!");
        }
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

    Texture2D GenerateDominantSpeciesMap(MapInputData inputData)
    {
        if (plantPreferences == null || plantPreferences.Length == 0)
        {
            Debug.LogWarning("No plant preferences available for dominant species map generation.");
            return CreateFloatMap(inputData.resolution);
        }

        Texture2D dominantSpeciesMap = CreateFloatMap(inputData.resolution);

        ForEachPixel(inputData.resolution, (x, y, normX, normY) =>
        {
            float height = inputData.GetHeight(x, y, normX, normY);
            float slope = inputData.GetSlope(normX, normY);
            float exposure = inputData.GetExposure(normX, normY);
            float moisture = inputData.GetMoisture(x, y, normX, normY);

            // Calculate suitability for each plant
            float[] suitabilities = new float[plantPreferences.Length];
            for (int i = 0; i < plantPreferences.Length; i++)
            {
                if (plantPreferences[i] != null)
                {
                    suitabilities[i] = plantPreferences[i].CalculateSuitability(height, slope, exposure, moisture);
                }
                else
                {
                    suitabilities[i] = 0f;
                }
            }

            // Find dominant species (highest suitability)
            int dominantIndex = 0;
            float maxSuitability = suitabilities[0];

            for (int i = 1; i < suitabilities.Length; i++)
            {
                if (suitabilities[i] > maxSuitability)
                {
                    maxSuitability = suitabilities[i];
                    dominantIndex = i;
                }
            }

            // Encode species index as grayscale value (0-1 range)
            float speciesValue = plantPreferences.Length > 1 ? (float)dominantIndex / (plantPreferences.Length - 1) : 0f;
            SetGrayscalePixel(dominantSpeciesMap, x, y, speciesValue);
        });

        dominantSpeciesMap.Apply();
        return dominantSpeciesMap;
    }

    Texture2D GenerateDominantSpeciesMapColored(MapInputData inputData)
    {
        if (plantPreferences == null || plantPreferences.Length == 0)
        {
            Debug.LogWarning("No plant preferences available for dominant species map generation.");
            return CreateFloatMap(inputData.resolution);
        }

        Texture2D coloredMap = new Texture2D(inputData.resolution, inputData.resolution, TextureFormat.RGBA32, false);

        ForEachPixel(inputData.resolution, (x, y, normX, normY) =>
        {
            float height = inputData.GetHeight(x, y, normX, normY);
            float slope = inputData.GetSlope(normX, normY);
            float exposure = inputData.GetExposure(normX, normY);
            float moisture = inputData.GetMoisture(x, y, normX, normY);

            // Calculate suitability for each plant
            float[] suitabilities = new float[plantPreferences.Length];
            for (int i = 0; i < plantPreferences.Length; i++)
            {
                if (plantPreferences[i] != null)
                {
                    suitabilities[i] = plantPreferences[i].CalculateSuitability(height, slope, exposure, moisture);
                }
                else
                {
                    suitabilities[i] = 0f;
                }
            }

            // Find dominant species (highest suitability)
            int dominantIndex = 0;
            float maxSuitability = suitabilities[0];

            for (int i = 1; i < suitabilities.Length; i++)
            {
                if (suitabilities[i] > maxSuitability)
                {
                    maxSuitability = suitabilities[i];
                    dominantIndex = i;
                }
            }

            // Get color for dominant species
            Color plantColor = plantPreferences[dominantIndex] != null 
                ? plantPreferences[dominantIndex].plantColor 
                : Color.white;

            coloredMap.SetPixel(x, y, plantColor);
        });

        coloredMap.Apply();
        return coloredMap;
    }

    Texture2D GenerateSeedMap(MapInputData inputData)
    {
        if (plantPreferences == null || plantPreferences.Length == 0)
        {
            Debug.LogWarning("No plant preferences available for seed map generation.");
            return CreateColorMap(inputData.resolution);
        }

        List<Seed> candidates = new List<Seed>();
        lastGeneratedSeeds.Clear();

        ForEachPixel(inputData.resolution, (x, y, normX, normY) =>
        {
            DominantSpeciesInfo dominantInfo = GetDominantSpeciesInfo(inputData, x, y, normX, normY);

            if (dominantInfo.species == null || dominantInfo.suitability <= seedSuitabilityThreshold)
            {
                return;
            }

            if (!IsLocalMaximum(inputData, x, y, dominantInfo.suitability))
            {
                return;
            }

            float probability = Mathf.Pow(dominantInfo.suitability, seedProbabilityPower);
            if ((float)SeedRandom.NextDouble() < probability)
            {
                candidates.Add(new Seed(dominantInfo.species, new Vector2Int(x, y), dominantInfo.suitability));
            }
        });

        candidates.Sort((left, right) => right.suitability.CompareTo(left.suitability));

        Texture2D seedMap = CreateColorMap(inputData.resolution);
        bool[,] blockedPixels = new bool[inputData.resolution, inputData.resolution];

        for (int i = 0; i < candidates.Count; i++)
        {
            Seed seed = candidates[i];

            if (IsSeedBlocked(blockedPixels, seed.pixel, seed.species.seedRadius, inputData))
            {
                continue;
            }

            seedMap.SetPixel(seed.pixel.x, seed.pixel.y, seed.species.color);
            lastGeneratedSeeds.Add(seed);
            BlockSeedArea(blockedPixels, seed.pixel, seed.species.seedRadius, inputData);
        }

        seedMap.Apply();
        return seedMap;
    }

    Texture2D CreateColorMap(int resolution)
    {
        return new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false);
    }

    DominantSpeciesInfo GetDominantSpeciesInfo(MapInputData inputData, int x, int y, float normX, float normY)
    {
        if (plantPreferences == null || plantPreferences.Length == 0)
        {
            return DominantSpeciesInfo.Empty;
        }

        int dominantIndex = -1;
        float maxSuitability = float.MinValue;

        for (int i = 0; i < plantPreferences.Length; i++)
        {
            PlantPreference plant = plantPreferences[i];
            if (plant == null)
            {
                continue;
            }

            float suitability = plant.CalculateSuitability(
                inputData.GetHeight(x, y, normX, normY),
                inputData.GetSlope(normX, normY),
                inputData.GetExposure(normX, normY),
                inputData.GetMoisture(x, y, normX, normY)
            );

            if (suitability > maxSuitability)
            {
                maxSuitability = suitability;
                dominantIndex = i;
            }
        }

        if (dominantIndex < 0)
        {
            return DominantSpeciesInfo.Empty;
        }

        PlantPreference dominantPlant = plantPreferences[dominantIndex];
        Species species = new Species(
            dominantPlant.plantName,
            dominantPlant.plantColor,
            dominantPlant.seedRadius,
            dominantIndex
        );

        return new DominantSpeciesInfo(species, maxSuitability);
    }

    bool IsLocalMaximum(MapInputData inputData, int x, int y, float suitability)
    {
        int windowRadius = Mathf.Max(1, seedLocalMaximumWindowSize / 2);

        for (int offsetY = -windowRadius; offsetY <= windowRadius; offsetY++)
        {
            int neighborY = y + offsetY;

            if (neighborY < 0 || neighborY >= inputData.resolution)
            {
                continue;
            }

            for (int offsetX = -windowRadius; offsetX <= windowRadius; offsetX++)
            {
                int neighborX = x + offsetX;

                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                if (neighborX < 0 || neighborX >= inputData.resolution)
                {
                    continue;
                }

                float normX = inputData.resolution > 1 ? (float)neighborX / (inputData.resolution - 1) : 0f;
                float normY = inputData.resolution > 1 ? (float)neighborY / (inputData.resolution - 1) : 0f;
                DominantSpeciesInfo neighborInfo = GetDominantSpeciesInfo(inputData, neighborX, neighborY, normX, normY);

                if (neighborInfo.suitability >= suitability)
                {
                    return false;
                }
            }
        }

        return true;
    }

    bool IsSeedBlocked(bool[,] blockedPixels, Vector2Int pixel, float seedRadiusMeters, MapInputData inputData)
    {
        int pixelRadius = GetSeedRadiusInPixels(seedRadiusMeters, inputData);
        int minX = Mathf.Max(0, pixel.x - pixelRadius);
        int maxX = Mathf.Min(inputData.resolution - 1, pixel.x + pixelRadius);
        int minY = Mathf.Max(0, pixel.y - pixelRadius);
        int maxY = Mathf.Min(inputData.resolution - 1, pixel.y + pixelRadius);
        int radiusSquared = pixelRadius * pixelRadius;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - pixel.x;
                int dy = y - pixel.y;

                if (dx * dx + dy * dy > radiusSquared)
                {
                    continue;
                }

                if (blockedPixels[x, y])
                {
                    return true;
                }
            }
        }

        return false;
    }

    void BlockSeedArea(bool[,] blockedPixels, Vector2Int pixel, float seedRadiusMeters, MapInputData inputData)
    {
        int pixelRadius = GetSeedRadiusInPixels(seedRadiusMeters, inputData);
        int minX = Mathf.Max(0, pixel.x - pixelRadius);
        int maxX = Mathf.Min(inputData.resolution - 1, pixel.x + pixelRadius);
        int minY = Mathf.Max(0, pixel.y - pixelRadius);
        int maxY = Mathf.Min(inputData.resolution - 1, pixel.y + pixelRadius);
        int radiusSquared = pixelRadius * pixelRadius;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x - pixel.x;
                int dy = y - pixel.y;

                if (dx * dx + dy * dy <= radiusSquared)
                {
                    blockedPixels[x, y] = true;
                }
            }
        }
    }

    int GetSeedRadiusInPixels(float seedRadiusMeters, MapInputData inputData)
    {
        if (seedRadiusMeters <= 0f)
        {
            return 0;
        }

        float metersPerPixel = GetMetersPerPixel(inputData);
        return Mathf.Max(1, Mathf.CeilToInt(seedRadiusMeters / metersPerPixel));
    }

    float GetMetersPerPixel(MapInputData inputData)
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

    Species CreateSpecies(PlantPreference plant, int index)
    {
        if (plant == null)
        {
            return null;
        }

        return new Species(plant.plantName, plant.plantColor, plant.seedRadius, index);
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

        if (generateDominantSpeciesMap)
        {
            return true;
        }

        if (generateSeedMap)
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

    void SaveSeedMapPng(Texture2D seedMap, string fileName, MapInputData inputData)
    {
        Texture2D dominantSpeciesMap = GenerateDominantSpeciesMapColored(inputData);
        Texture2D overlayMap = new Texture2D(dominantSpeciesMap.width, dominantSpeciesMap.height, TextureFormat.RGBA32, false);

        overlayMap.SetPixels(dominantSpeciesMap.GetPixels());
        DrawSeedDots(overlayMap);
        overlayMap.Apply();

        File.WriteAllBytes(
            Path.Combine(GetMapsPath(), $"{fileName}.png"),
            overlayMap.EncodeToPNG()
        );

        Destroy(dominantSpeciesMap);
        Destroy(overlayMap);
    }

    void SaveDominantSpeciesMapPng(Texture2D grayscaleMap, string fileName, MapInputData inputData)
    {
        Texture2D coloredMap = GenerateDominantSpeciesMapColored(inputData);

        File.WriteAllBytes(
            Path.Combine(GetMapsPath(), $"{fileName}_colored.png"),
            coloredMap.EncodeToPNG()
        );

        Destroy(coloredMap);
    }

    void DrawSeedDots(Texture2D map)
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

    void DrawFilledCircle(Texture2D map, int centerX, int centerY, int radius, Color color)
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

    int GetSeedDotRadius(float seedRadiusMeters)
    {
        return Mathf.Clamp(Mathf.CeilToInt(seedRadiusMeters * 0.15f), 1, 4);
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

    [Serializable]
    class Species
    {
        public string plantName;
        public Color color;
        public float seedRadius;
        public int index;

        public Species(string plantName, Color color, float seedRadius, int index)
        {
            this.plantName = plantName;
            this.color = color;
            this.seedRadius = seedRadius;
            this.index = index;
        }
    }

    [Serializable]
    class Seed
    {
        public Species species;
        public Vector2Int pixel;
        public float suitability;

        public Seed(Species species, Vector2Int pixel, float suitability)
        {
            this.species = species;
            this.pixel = pixel;
            this.suitability = suitability;
        }
    }

    struct DominantSpeciesInfo
    {
        public static readonly DominantSpeciesInfo Empty = new DominantSpeciesInfo(null, 0f);

        public readonly Species species;
        public readonly float suitability;

        public DominantSpeciesInfo(Species species, float suitability)
        {
            this.species = species;
            this.suitability = suitability;
        }
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
