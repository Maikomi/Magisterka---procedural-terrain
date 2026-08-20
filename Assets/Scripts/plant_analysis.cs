using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;

[Serializable]
public class SeedSaveData
{
    public string plantName;
    public int x;
    public int y;
    public float suitability;
}

[Serializable]
public class SeedSaveFile
{
    public List<SeedSaveData> seeds = new List<SeedSaveData>();
}
public class plant_analysis : MonoBehaviour
{
    [Header("Seed Map")]

    public string seedMapFileName = "SeedMap";
    [Range(0f, 1f)] public float seedSuitabilityThreshold = 0.65f;
    public int seedLocalMaximumWindowSize = 10;
    public float seedProbabilityPower = 3f;
    static readonly System.Random SeedRandom = new System.Random();
    public readonly List<Seed> lastGeneratedSeeds = new List<Seed>();
    public string seedsSaveFileName = "SeedMap.json";

    public void GenerateDominantSpeciesMap(MapInputData inputData, List<Species> species, string dominantSpeciesMapFileName, bool generatePreview)
    {
        if (species == null || species.Count == 0)
        {
            Debug.LogWarning("No plant preferences available for dominant species map generation.");
            return;
        }

        Texture2D dominantSpeciesMap = map_helper.CreateFloatMap(inputData.resolution);

        map_helper.ForEachPixel(inputData.resolution, (x, y, normX, normY) =>
        {
            float height = inputData.GetHeight(x, y, normX, normY);
            float slope = inputData.GetSlope(normX, normY);
            float exposure = inputData.GetExposure(normX, normY);
            float moisture = inputData.GetMoisture(x, y, normX, normY);

            // Calculate suitability for each plant
            float[] suitabilities = new float[species.Count];
            for (int i = 0; i < species.Count; i++)
            {
                if (species[i] != null)
                {
                    suitabilities[i] = species[i].CalculateSuitability(height, slope, exposure, moisture);
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
            float speciesValue = species.Count > 1 ? (float)dominantIndex / (species.Count - 1) : 0f;
            map_helper.SetGrayscalePixel(dominantSpeciesMap, x, y, speciesValue);
        });

        dominantSpeciesMap.Apply();
        map_helper.SaveExr(dominantSpeciesMap, dominantSpeciesMapFileName);
        if (generatePreview)
        {
            string dominantSpeciesPreviewFileName = dominantSpeciesMapFileName + "_preview";
            map_helper.SavePng(dominantSpeciesMap, dominantSpeciesPreviewFileName);
            map_helper.SaveDominantSpeciesMapPng(dominantSpeciesMap, species, dominantSpeciesMapFileName, inputData);
        }
    }

    public void SaveSeedsToJson()
    {
        SeedSaveFile saveFile = new SeedSaveFile();

        foreach (Seed seed in lastGeneratedSeeds)
        {
            if (seed == null || seed.species == null)
            {
                continue;
            }

            SeedSaveData seedData = new SeedSaveData
            {
                plantName = seed.species.plantName,
                x = seed.pixel.x,
                y = seed.pixel.y,
                suitability = seed.suitability
            };

            saveFile.seeds.Add(seedData);
        }

        string mapsPath = map_helper.GetMapsPath();

        if (!Directory.Exists(mapsPath))
        {
            Directory.CreateDirectory(mapsPath);
        }

        string filePath = Path.Combine(
            mapsPath,
            seedsSaveFileName
        );

        string json = JsonUtility.ToJson(saveFile, true);

        File.WriteAllText(filePath, json);

        Debug.Log(
            $"Saved {saveFile.seeds.Count} seeds to: {filePath}"
        );
    }

    public bool LoadSeedsFromJson(List<Species> species)
    {
        string mapsPath = map_helper.GetMapsPath();

        string filePath = Path.Combine(
            mapsPath,
            seedsSaveFileName
        );

        if (!File.Exists(filePath))
        {
            Debug.LogWarning(
                $"Seed file was not found: {filePath}"
            );

            return false;
        }

        string json = File.ReadAllText(filePath);

        SeedSaveFile saveFile =
            JsonUtility.FromJson<SeedSaveFile>(json);

        if (saveFile == null || saveFile.seeds == null)
        {
            Debug.LogWarning(
                "Seed file is empty or invalid."
            );

            return false;
        }

        lastGeneratedSeeds.Clear();

        foreach (SeedSaveData seedData in saveFile.seeds)
        {
            Species speciesData = species.Find(
                s => s != null &&
                    s.plantName == seedData.plantName
            );

            if (speciesData == null)
            {
                Debug.LogWarning(
                    $"Species '{seedData.plantName}' " +
                    $"was not found. Seed skipped."
                );

                continue;
            }

            Seed seed = new Seed(
                speciesData,
                new Vector2Int(seedData.x, seedData.y),
                seedData.suitability
            );

            lastGeneratedSeeds.Add(seed);
        }

        Debug.Log(
            $"Loaded {lastGeneratedSeeds.Count} seeds."
        );

        return true;
    }
    public void GenerateSeedMap(MapInputData inputData, List<Species> species, string seedMapFileName, bool generatePreview)
    {
        if (species == null || species.Count == 0)
        {
            Debug.LogWarning("No plant preferences available for seed map generation.");
            return;
        }

        List<Seed> candidates = new List<Seed>();
        lastGeneratedSeeds.Clear();

        map_helper.ForEachPixel(inputData.resolution, (x, y, normX, normY) =>
        {
            var dominantInfo = GetDominantSpeciesInfo(inputData, species, x, y, normX, normY);

            if (dominantInfo.Item1 == null || dominantInfo.Item2 <= seedSuitabilityThreshold)
            {
                return;
            }

            if (!IsLocalMaximum(inputData, species, x, y, dominantInfo.Item2))
            {
                return;
            }

            float probability = Mathf.Pow(dominantInfo.Item2, seedProbabilityPower);
            if ((float)SeedRandom.NextDouble() < probability)
            {
                candidates.Add(new Seed(dominantInfo.Item1, new Vector2Int(x, y), dominantInfo.Item2));
            }
        });

        candidates.Sort((left, right) => right.suitability.CompareTo(left.suitability));

        Texture2D seedMap = new Texture2D(inputData.resolution, inputData.resolution, TextureFormat.RGBAFloat, false);
        bool[,] blockedPixels = new bool[inputData.resolution, inputData.resolution];
        Dictionary<Species, int> seedsPerSpecies = new Dictionary<Species, int>();

        for (int i = 0; i < candidates.Count; i++)
        {
            Seed seed = candidates[i];

            if (seed == null || seed.species == null)
            {
                continue;
            }

            if (!seedsPerSpecies.TryGetValue(seed.species, out int speciesSeedCount))
            {
                speciesSeedCount = 0;
            }

            if (seed.species.maxSeedCountPerSpecies > 0 && speciesSeedCount >= seed.species.maxSeedCountPerSpecies)
            {
                continue;
            }

            if (IsSeedBlocked(blockedPixels, seed.pixel, seed.species.seedRadius, inputData))
            {
                continue;
            }

            seedMap.SetPixel(seed.pixel.x, seed.pixel.y, seed.species.color);
            lastGeneratedSeeds.Add(seed);
            seedsPerSpecies[seed.species] = speciesSeedCount + 1;
            BlockSeedArea(blockedPixels, seed.pixel, seed.species.seedRadius, inputData);
        }

        seedMap.Apply();
        map_helper.SaveExr(seedMap, seedMapFileName);
        SaveSeedsToJson();
        if (generatePreview)
        {
            string seedMapPreviewFileName = seedMapFileName + "_preview";
            map_helper.SaveSeedMapPng(seedMap, seedMapPreviewFileName, inputData, species, lastGeneratedSeeds);
        }

    }

    public void GeneratePlantSuitabilityMaps(MapInputData inputData, List<Species> species, bool generatePlantSuitabilityPreviews)
    {
        if (species == null)
        {
            return;
        }

        for (int i = 0; i < species.Count; i++)
        {
            Species plant = species[i];

            if (plant == null || !plant.generateSuitabilityMap)
            {
                continue;
            }

            Texture2D suitabilityMap = GeneratePlantSuitabilityMap(inputData, plant);

            map_helper.SaveExr(suitabilityMap, $"{plant.plantName}_suitability");

            if (generatePlantSuitabilityPreviews)
            {
                map_helper.SavePng(suitabilityMap, $"{plant.plantName}_suitability_preview");
            }

            Debug.Log($"{plant.plantName} suitability map saved!");
        }
    }

    Texture2D GeneratePlantSuitabilityMap(MapInputData inputData, Species plant)
    {
        Texture2D suitabilityMap = map_helper.CreateFloatMap(inputData.resolution);

        map_helper.ForEachPixel(inputData.resolution, (x, y, normX, normY) =>
        {
            float height = inputData.GetHeight(x, y, normX, normY);
            float slope = inputData.GetSlope(normX, normY);
            float exposure = inputData.GetExposure(normX, normY);
            float moisture = inputData.GetMoisture(x, y, normX, normY);
            float suitability = plant.CalculateSuitability(height, slope, exposure, moisture);

            map_helper.SetGrayscalePixel(suitabilityMap, x, y, suitability);
        });

        suitabilityMap.Apply();
        return suitabilityMap;
    }

    public static Texture2D GenerateDominantSpeciesMap(MapInputData inputData, List<Species> species)
    {
        if (species == null || species.Count == 0)
        {
            Debug.LogWarning("No plant preferences available for dominant species map generation.");
            return map_helper.CreateFloatMap(inputData.resolution);
        }

        Texture2D dominantSpeciesMap = map_helper.CreateFloatMap(inputData.resolution);

        map_helper.ForEachPixel(inputData.resolution, (x, y, normX, normY) =>
        {
            float height = inputData.GetHeight(x, y, normX, normY);
            float slope = inputData.GetSlope(normX, normY);
            float exposure = inputData.GetExposure(normX, normY);
            float moisture = inputData.GetMoisture(x, y, normX, normY);

            // Calculate suitability for each plant
            float[] suitabilities = new float[species.Count];
            for (int i = 0; i < species.Count; i++)
            {
                if (species[i] != null)
                {
                    suitabilities[i] = species[i].CalculateSuitability(height, slope, exposure, moisture);
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
            float speciesValue = species.Count > 1 ? (float)dominantIndex / (species.Count - 1) : 0f;
            map_helper.SetGrayscalePixel(dominantSpeciesMap, x, y, speciesValue);
        });

        dominantSpeciesMap.Apply();
        return dominantSpeciesMap;
    }

    public static Texture2D GenerateDominantSpeciesMapColored(MapInputData inputData, List<Species> species)
    {
        if (species == null || species.Count == 0)
        {
            Debug.LogWarning("No plant preferences available for dominant species map generation.");
            return map_helper.CreateFloatMap(inputData.resolution);
        }

        Texture2D coloredMap = new Texture2D(inputData.resolution, inputData.resolution, TextureFormat.RGBA32, false);

        map_helper.ForEachPixel(inputData.resolution, (x, y, normX, normY) =>
        {
            float height = inputData.GetHeight(x, y, normX, normY);
            float slope = inputData.GetSlope(normX, normY);
            float exposure = inputData.GetExposure(normX, normY);
            float moisture = inputData.GetMoisture(x, y, normX, normY);

            // Calculate suitability for each plant
            float[] suitabilities = new float[species.Count];
            for (int i = 0; i < species.Count; i++)
            {
                if (species[i] != null)
                {
                    suitabilities[i] = species[i].CalculateSuitability(height, slope, exposure, moisture);
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
            Color plantColor = species[dominantIndex] != null
                ? species[dominantIndex].color
                : Color.white;

            coloredMap.SetPixel(x, y, plantColor);
        });

        coloredMap.Apply();
        return coloredMap;
    }


    (Species, float) GetDominantSpeciesInfo(MapInputData inputData, List<Species> species, int x, int y, float normX, float normY)
    {
        if (species == null || species.Count == 0)
        {
            return (null, 0f);
        }

        int dominantIndex = -1;
        float maxSuitability = float.MinValue;

        for (int i = 0; i < species.Count; i++)
        {
            Species plant = species[i];
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
            return (null, 0f);
        }

        Species dominantPlant = species[dominantIndex];
        return (species[dominantIndex], maxSuitability);
    }

    bool IsLocalMaximum(MapInputData inputData, List<Species> species, int x, int y, float suitability)
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
                var neighborInfo = GetDominantSpeciesInfo(inputData, species, neighborX, neighborY, normX, normY);

                if (neighborInfo.Item2 >= suitability)
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

        float metersPerPixel = map_helper.GetMetersPerPixel(inputData);
        return Mathf.Max(1, Mathf.CeilToInt(seedRadiusMeters / metersPerPixel));
    }
}