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

    [Header("Suitability Weights")]
    [Range(0f, 10f)] public float heightWeight = 1f;
    [Range(0f, 10f)] public float slopeWeight = 1f;
    [Range(0f, 10f)] public float exposureWeight = 1f;
    [Range(0f, 10f)] public float moistureWeight = 1f;

    public float GetTotalSuitabilityWeight()
    {
        float totalWeight =
            heightWeight +
            slopeWeight +
            exposureWeight +
            moistureWeight;

        return totalWeight > 0f ? totalWeight : 4f;
    }

    public string seedMapFileName = "SeedMap";

    [Range(0f, 1f)]
    public float seedSuitabilityThreshold = 0.30f;

    public bool usePoissonDiscSeedDistribution = true;

    public int seedLocalMaximumWindowSize = 3;

    [Min(0.01f)]
    public float seedDensityPower = 2.0f;

    [Min(1)]
    public int poissonCandidatesPerPoint = 20;

    [Min(0.01f)]
    public float poissonRadiusMultiplier = 2.0f;
    static readonly System.Random SeedRandom = new System.Random();
    public readonly List<Seed> lastGeneratedSeeds =
        new List<Seed>();

    public string seedsSaveFileName = "SeedMap.json";

    // ============================================================
    // DOMINANT SPECIES MAP
    // ============================================================

    public void GenerateDominantSpeciesMap(
        MapInputData inputData,
        List<Species> species,
        string dominantSpeciesMapFileName,
        bool generatePreview)
    {
        if (species == null || species.Count == 0)
        {
            Debug.LogWarning("No plant preferences available for dominant species map generation.");
            return;
        }

        Texture2D dominantSpeciesMap = map_helper.CreateFloatMap(inputData.resolution);

        map_helper.ForEachPixel(
            inputData.resolution,
            (x, y, normX, normY) =>
            {
                float height = inputData.GetHeight(x, y, normX, normY);
                float slope = inputData.GetSlope(normX, normY);
                float exposure = inputData.GetExposure(normX, normY);
                float moisture = inputData.GetMoisture(x, y, normX, normY);
                float[] suitabilities = new float[species.Count];

                for (int i = 0; i < species.Count; i++)
                {
                    if (species[i] != null)
                    {
                        suitabilities[i] =
                            species[i].CalculateSuitability(
                                height,
                                slope,
                                exposure,
                                moisture,
                                heightWeight,
                                slopeWeight,
                                exposureWeight,
                                moistureWeight
                            );
                    }
                    else
                    {
                        suitabilities[i] = 0f;
                    }
                }

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

                float speciesValue = species.Count > 1 ? (float)dominantIndex / (species.Count - 1) : 0f;
                map_helper.SetGrayscalePixel(dominantSpeciesMap, x, y, speciesValue);
            }
        );

        dominantSpeciesMap.Apply();
        map_helper.SaveExr(dominantSpeciesMap, dominantSpeciesMapFileName);

        if (generatePreview)
        {
            string dominantSpeciesPreviewFileName = dominantSpeciesMapFileName + "_preview";
            map_helper.SavePng(dominantSpeciesMap, dominantSpeciesPreviewFileName);
            map_helper.SaveDominantSpeciesMapPng(
                dominantSpeciesMap,
                species,
                dominantSpeciesMapFileName,
                inputData,
                heightWeight,
                slopeWeight,
                exposureWeight,
                moistureWeight
            );
        }
    }

    // ============================================================
    // SAVE SEEDS
    // ============================================================

    public void SaveSeedsToJson()
    {
        SeedSaveFile saveFile = new SeedSaveFile();

        foreach (Seed seed in lastGeneratedSeeds)
        {
            if (seed == null || seed.species == null)
            {
                continue;
            }

            SeedSaveData seedData =
                new SeedSaveData
                {
                    plantName =
                        seed.species.plantName,

                    x = seed.pixel.x,
                    y = seed.pixel.y,

                    suitability =
                        seed.suitability
                };

            saveFile.seeds.Add(seedData);
        }

        string mapsPath = map_helper.GetMapsPath();

        if (!Directory.Exists(mapsPath))
        {
            Directory.CreateDirectory(mapsPath);
        }

        string filePath = Path.Combine(mapsPath, seedsSaveFileName);
        string json = JsonUtility.ToJson(saveFile, true);
        File.WriteAllText(filePath, json);
        map_helper.CopyToRunFolder(filePath);

        Debug.Log($"Saved {saveFile.seeds.Count} seeds to: {filePath}");
    }

    // ============================================================
    // LOAD SEEDS
    // ============================================================

    public bool LoadSeedsFromJson(List<Species> species)
    {
        string mapsPath = map_helper.GetMapsPath();

        string filePath =
            Path.Combine(
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

        SeedSaveFile saveFile = JsonUtility.FromJson<SeedSaveFile>(json);

        if (saveFile == null || saveFile.seeds == null)
        {
            Debug.LogWarning("Seed file is empty or invalid.");

            return false;
        }

        lastGeneratedSeeds.Clear();

        foreach (SeedSaveData seedData in saveFile.seeds)
        {
            Species speciesData =
                species.Find(
                    s =>
                        s != null &&
                        s.plantName ==
                        seedData.plantName
                );

            if (speciesData == null)
            {
                Debug.LogWarning($"Species '{seedData.plantName}' " + $"was not found. Seed skipped.");

                continue;
            }

            Seed seed =
                new Seed(
                    speciesData,
                    new Vector2Int(
                        seedData.x,
                        seedData.y
                    ),
                    seedData.suitability
                );

            lastGeneratedSeeds.Add(seed);
        }

        Debug.Log(
            $"Loaded {lastGeneratedSeeds.Count} seeds."
        );

        return true;
    }

    // ============================================================
    // GENERATE SEED MAP
    // ============================================================

    public void GenerateSeedMap(
        MapInputData inputData,
        List<Species> species,
        string seedMapFileName,
        bool generatePreview)
    {
        if (species == null || species.Count == 0)
        {
            Debug.LogWarning("No plant preferences available for seed map generation.");
            return;
        }

        lastGeneratedSeeds.Clear();

        List<Seed> candidates =
            usePoissonDiscSeedDistribution
                ? GeneratePoissonSeedCandidates(
                    inputData,
                    species
                )
                : GenerateLocalMaximumSeedCandidates(
                    inputData,
                    species
                );

        ShuffleCandidates(candidates);

        Texture2D seedMap =
            new Texture2D(
                inputData.resolution,
                inputData.resolution,
                TextureFormat.RGBAFloat,
                false
            );

        bool[,] blockedPixels =
            new bool[
                inputData.resolution,
                inputData.resolution
            ];

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

            // ----------------------------------------------------
            // MAX SEED COUNT
            // ----------------------------------------------------

            if (seed.species.maxSeedCountPerSpecies > 0 &&
                speciesSeedCount >= seed.species.maxSeedCountPerSpecies)
            {
                continue;
            }

            // ----------------------------------------------------
            // SPATIAL BLOCKING
            // ----------------------------------------------------

            if (
                IsSeedBlocked(
                    blockedPixels,
                    seed.pixel,
                    seed.species.seedRadius,
                    inputData
                )
            )
            {
                continue;
            }

            seedMap.SetPixel(
                seed.pixel.x,
                seed.pixel.y,
                seed.species.color
            );

            lastGeneratedSeeds.Add(seed);

            seedsPerSpecies[seed.species] =
                speciesSeedCount + 1;

            BlockSeedArea(
                blockedPixels,
                seed.pixel,
                seed.species.seedRadius,
                inputData
            );
        }

        seedMap.Apply();

        map_helper.SaveExr(
            seedMap,
            seedMapFileName
        );

        SaveSeedsToJson();

        if (generatePreview)
        {
            string seedMapPreviewFileName =
                seedMapFileName + "_preview";

            map_helper.SaveSeedMapPng(
                seedMap,
                seedMapPreviewFileName,
                inputData,
                species,
                lastGeneratedSeeds,
                heightWeight,
                slopeWeight,
                exposureWeight,
                moistureWeight
            );
        }
    }

    // ============================================================
    // SHUFFLE
    // ============================================================

    void ShuffleCandidates(
        List<Seed> candidates)
    {
        // Fisher-Yates shuffle.
        //
        // Każdy zaakceptowany kandydat ma taką samą szansę
        // wejścia do końcowego zbioru.
        //
        // Wpływ suitability pozostaje w
        // TryAddSeedCandidate().

        for (int i = candidates.Count - 1;
             i > 0;
             i--)
        {
            int j =
                SeedRandom.Next(i + 1);

            Seed temp =
                candidates[i];

            candidates[i] =
                candidates[j];

            candidates[j] =
                temp;
        }
    }


    // ============================================================
    // LOCAL MAXIMUM
    // ============================================================

    List<Seed> GenerateLocalMaximumSeedCandidates(
        MapInputData inputData,
        List<Species> species)
    {
        List<Seed> candidates =
            new List<Seed>();

        map_helper.ForEachPixel(
            inputData.resolution,
            (x, y, normX, normY) =>
            {
                var dominantInfo =
                    GetDominantSpeciesInfo(
                        inputData,
                        species,
                        x,
                        y,
                        normX,
                        normY
                    );

                if (
                    dominantInfo.Item1 == null ||
                    dominantInfo.Item2 <=
                        seedSuitabilityThreshold
                )
                {
                    return;
                }

                if (
                    !IsLocalMaximum(
                        inputData,
                        species,
                        x,
                        y,
                        dominantInfo.Item2
                    )
                )
                {
                    return;
                }

                TryAddSeedCandidate(
                    candidates,
                    dominantInfo.Item1,
                    new Vector2Int(x, y),
                    dominantInfo.Item2
                );
            }
        );

        return candidates;
    }


    // ============================================================
    // POISSON SEED CANDIDATES
    // ============================================================

    List<Seed> GeneratePoissonSeedCandidates(
        MapInputData inputData,
        List<Species> species)
    {
        List<Seed> candidates =
            new List<Seed>();

        float poissonRadius =
            GetPoissonRadiusInPixels(
                inputData,
                species
            );

        List<Vector2> poissonPoints =
            GeneratePoissonPoints(
                inputData.resolution,
                poissonRadius,
                poissonCandidatesPerPoint
            );

        foreach (Vector2 point
                 in poissonPoints)
        {
            int x =
                Mathf.Clamp(
                    Mathf.RoundToInt(point.x),
                    0,
                    inputData.resolution - 1
                );

            int y =
                Mathf.Clamp(
                    Mathf.RoundToInt(point.y),
                    0,
                    inputData.resolution - 1
                );

            float normX =
                inputData.resolution > 1
                    ? (float)x /
                      (inputData.resolution - 1)
                    : 0f;

            float normY =
                inputData.resolution > 1
                    ? (float)y /
                      (inputData.resolution - 1)
                    : 0f;


            // ----------------------------------------------------
            // KAŻDY GATUNEK MA WŁASNĄ SZANSĘ
            // ----------------------------------------------------

            for (int i = 0;
                 i < species.Count;
                 i++)
            {
                Species plant =
                    species[i];

                if (plant == null)
                {
                    continue;
                }

                float suitability =
                    plant.CalculateSuitability(
                        inputData.GetHeight(
                            x,
                            y,
                            normX,
                            normY
                        ),

                        inputData.GetSlope(
                            normX,
                            normY
                        ),

                        inputData.GetExposure(
                            normX,
                            normY
                        ),

                        inputData.GetMoisture(
                            x,
                            y,
                            normX,
                            normY
                        ),

                        heightWeight,
                        slopeWeight,
                        exposureWeight,
                        moistureWeight
                    );

                // ------------------------------------------------
                // THRESHOLD
                // ------------------------------------------------

                if (
                    suitability <
                    seedSuitabilityThreshold
                )
                {
                    continue;
                }

                TryAddSeedCandidate(
                    candidates,
                    plant,
                    new Vector2Int(x, y),
                    suitability
                );
            }
        }

        Debug.Log(
            $"Generated {candidates.Count} seed candidates from " +
            $"{poissonPoints.Count} Poisson disc points."
        );

        return candidates;
    }

    // ============================================================
    // SUITABILITY -> DENSITY
    // ============================================================

    void TryAddSeedCandidate(
        List<Seed> candidates,
        Species species,
        Vector2Int pixel,
        float suitability)
    {
        // Suitability nadal określa przydatność terenu.
        //
        // Threshold określa minimalną przydatność.
        //
        // Następnie suitability jest przekształcana
        // na prawdopodobieństwo wystąpienia seeda.

        float normalizedSuitability =
            Mathf.InverseLerp(
                seedSuitabilityThreshold,
                1f,
                suitability
            );

        float density =
            Mathf.Pow(
                normalizedSuitability,
                seedDensityPower
            );

        if (
            (float)SeedRandom.NextDouble()
            < density
        )
        {
            candidates.Add(
                new Seed(
                    species,
                    pixel,
                    suitability
                )
            );
        }
    }


    // ============================================================
    // POISSON DISC
    // ============================================================

    List<Vector2> GeneratePoissonPoints(
        int resolution,
        float radius,
        int candidatesPerPoint)
    {
        List<Vector2> points =
            new List<Vector2>();

        if (resolution <= 0)
        {
            return points;
        }

        float effectiveRadius =
            Mathf.Max(
                1f,
                radius
            );

        float cellSize =
            effectiveRadius /
            Mathf.Sqrt(2f);

        int gridSize =
            Mathf.CeilToInt(
                resolution /
                cellSize
            );

        Vector2[,] grid =
            new Vector2[
                gridSize,
                gridSize
            ];

        bool[,] occupiedGrid =
            new bool[
                gridSize,
                gridSize
            ];

        List<Vector2> activePoints =
            new List<Vector2>();

        Vector2 firstPoint =
            new Vector2(
                RandomRange(
                    0f,
                    resolution - 1
                ),

                RandomRange(
                    0f,
                    resolution - 1
                )
            );

        AddPoissonPoint(
            points,
            activePoints,
            grid,
            occupiedGrid,
            firstPoint,
            cellSize
        );

        while (
            activePoints.Count > 0
        )
        {
            int activeIndex =
                SeedRandom.Next(
                    activePoints.Count
                );

            Vector2 activePoint =
                activePoints[
                    activeIndex
                ];

            bool foundCandidate =
                false;

            for (
                int i = 0;
                i < candidatesPerPoint;
                i++
            )
            {
                float angle =
                    RandomRange(
                        0f,
                        Mathf.PI * 2f
                    );

                float distance =
                    RandomRange(
                        effectiveRadius,
                        effectiveRadius * 2f
                    );

                Vector2 candidate =
                    activePoint +
                    new Vector2(
                        Mathf.Cos(angle),
                        Mathf.Sin(angle)
                    ) * distance;

                if (
                    !IsPoissonPointValid(
                        candidate,
                        resolution,
                        effectiveRadius,
                        cellSize,
                        grid,
                        occupiedGrid
                    )
                )
                {
                    continue;
                }

                AddPoissonPoint(
                    points,
                    activePoints,
                    grid,
                    occupiedGrid,
                    candidate,
                    cellSize
                );

                foundCandidate = true;
                break;
            }

            if (!foundCandidate)
            {
                activePoints.RemoveAt(
                    activeIndex
                );
            }
        }

        return points;
    }


    void AddPoissonPoint(
        List<Vector2> points,
        List<Vector2> activePoints,
        Vector2[,] grid,
        bool[,] occupiedGrid,
        Vector2 point,
        float cellSize)
    {
        points.Add(point);

        activePoints.Add(point);

        int gridX =
            Mathf.FloorToInt(
                point.x / cellSize
            );

        int gridY =
            Mathf.FloorToInt(
                point.y / cellSize
            );

        grid[gridX, gridY] =
            point;

        occupiedGrid[
            gridX,
            gridY
        ] = true;
    }


    bool IsPoissonPointValid(
        Vector2 point,
        int resolution,
        float radius,
        float cellSize,
        Vector2[,] grid,
        bool[,] occupiedGrid)
    {
        if (
            point.x < 0f ||
            point.x > resolution - 1 ||
            point.y < 0f ||
            point.y > resolution - 1
        )
        {
            return false;
        }

        int gridX =
            Mathf.FloorToInt(
                point.x / cellSize
            );

        int gridY =
            Mathf.FloorToInt(
                point.y / cellSize
            );

        int minX =
            Mathf.Max(
                0,
                gridX - 2
            );

        int maxX =
            Mathf.Min(
                grid.GetLength(0) - 1,
                gridX + 2
            );

        int minY =
            Mathf.Max(
                0,
                gridY - 2
            );

        int maxY =
            Mathf.Min(
                grid.GetLength(1) - 1,
                gridY + 2
            );

        float radiusSquared =
            radius * radius;

        for (
            int y = minY;
            y <= maxY;
            y++
        )
        {
            for (
                int x = minX;
                x <= maxX;
                x++
            )
            {
                Vector2 neighbor =
                    grid[x, y];

                if (
                    !occupiedGrid[x, y]
                )
                {
                    continue;
                }

                if (
                    (neighbor - point)
                    .sqrMagnitude
                    < radiusSquared
                )
                {
                    return false;
                }
            }
        }

        return true;
    }


    // ============================================================
    // POISSON RADIUS
    // ============================================================

    float GetPoissonRadiusInPixels(
        MapInputData inputData,
        List<Species> species)
    {
        float minRadiusMeters =
            float.MaxValue;

        foreach (
            Species plant
            in species
        )
        {
            if (
                plant == null ||
                plant.seedRadius <= 0f
            )
            {
                continue;
            }

            minRadiusMeters =
                Mathf.Min(
                    minRadiusMeters,
                    plant.seedRadius
                );
        }

        if (
            minRadiusMeters ==
            float.MaxValue
        )
        {
            minRadiusMeters =
                map_helper.GetMetersPerPixel(
                    inputData
                );
        }

        // Poisson jest używany jako etap
        // wstępnego próbkowania.
        //
        // Faktyczny minimalny dystans między
        // zaakceptowanymi seedami jest później
        // kontrolowany przez BlockSeedArea()
        // na podstawie seedRadius konkretnego gatunku.

        return GetSeedRadiusInPixels(
            minRadiusMeters *
            poissonRadiusMultiplier,
            inputData
        );
    }


    float RandomRange(
        float min,
        float max)
    {
        return min +
            (float)SeedRandom.NextDouble() *
            (max - min);
    }


    // ============================================================
    // PLANT SUITABILITY MAPS
    // ============================================================

    public void GeneratePlantSuitabilityMaps(
        MapInputData inputData,
        List<Species> species,
        bool generatePlantSuitabilityPreviews)
    {
        if (species == null)
        {
            return;
        }

        for (
            int i = 0;
            i < species.Count;
            i++
        )
        {
            Species plant =
                species[i];

            if (
                plant == null ||
                !plant.generateSuitabilityMap
            )
            {
                continue;
            }

            Texture2D suitabilityMap =
                GeneratePlantSuitabilityMap(
                    inputData,
                    plant
                );

            map_helper.SaveExr(
                suitabilityMap,
                $"{plant.plantName}_suitability"
            );

            if (
                generatePlantSuitabilityPreviews
            )
            {
                map_helper.SavePng(
                    suitabilityMap,
                    $"{plant.plantName}_suitability_preview"
                );
            }

            Debug.Log(
                $"{plant.plantName} suitability map saved!"
            );
        }
    }


    Texture2D GeneratePlantSuitabilityMap(
        MapInputData inputData,
        Species plant)
    {
        Texture2D suitabilityMap =
            map_helper.CreateFloatMap(
                inputData.resolution
            );

        map_helper.ForEachPixel(
            inputData.resolution,
            (x, y, normX, normY) =>
            {
                float height =
                    inputData.GetHeight(
                        x,
                        y,
                        normX,
                        normY
                    );

                float slope =
                    inputData.GetSlope(
                        normX,
                        normY
                    );

                float exposure =
                    inputData.GetExposure(
                        normX,
                        normY
                    );

                float moisture =
                    inputData.GetMoisture(
                        x,
                        y,
                        normX,
                        normY
                    );

                float suitability =
                    plant.CalculateSuitability(
                        height,
                        slope,
                        exposure,
                        moisture,
                        heightWeight,
                        slopeWeight,
                        exposureWeight,
                        moistureWeight
                    );

                map_helper.SetGrayscalePixel(
                    suitabilityMap,
                    x,
                    y,
                    suitability
                );
            }
        );

        suitabilityMap.Apply();

        return suitabilityMap;
    }


    // ============================================================
    // DOMINANT SPECIES MAP - STATIC
    // ============================================================

    public static Texture2D GenerateDominantSpeciesMap(
        MapInputData inputData,
        List<Species> species,
        float heightWeight = 1f,
        float slopeWeight = 1f,
        float exposureWeight = 1f,
        float moistureWeight = 1f)
    {
        if (
            species == null ||
            species.Count == 0
        )
        {
            Debug.LogWarning(
                "No plant preferences available for dominant species map generation."
            );

            return map_helper.CreateFloatMap(
                inputData.resolution
            );
        }

        Texture2D dominantSpeciesMap =
            map_helper.CreateFloatMap(
                inputData.resolution
            );

        map_helper.ForEachPixel(
            inputData.resolution,
            (x, y, normX, normY) =>
            {
                float height =
                    inputData.GetHeight(
                        x,
                        y,
                        normX,
                        normY
                    );

                float slope =
                    inputData.GetSlope(
                        normX,
                        normY
                    );

                float exposure =
                    inputData.GetExposure(
                        normX,
                        normY
                    );

                float moisture =
                    inputData.GetMoisture(
                        x,
                        y,
                        normX,
                        normY
                    );

                float[] suitabilities =
                    new float[
                        species.Count
                    ];

                for (
                    int i = 0;
                    i < species.Count;
                    i++
                )
                {
                    if (
                        species[i] != null
                    )
                    {
                        suitabilities[i] =
                            species[i]
                            .CalculateSuitability(
                                height,
                                slope,
                                exposure,
                                moisture,
                                heightWeight,
                                slopeWeight,
                                exposureWeight,
                                moistureWeight
                            );
                    }
                    else
                    {
                        suitabilities[i] = 0f;
                    }
                }

                int dominantIndex = 0;

                float maxSuitability =
                    suitabilities[0];

                for (
                    int i = 1;
                    i < suitabilities.Length;
                    i++
                )
                {
                    if (
                        suitabilities[i] >
                        maxSuitability
                    )
                    {
                        maxSuitability =
                            suitabilities[i];

                        dominantIndex = i;
                    }
                }

                float speciesValue =
                    species.Count > 1
                        ? (float)dominantIndex /
                          (species.Count - 1)
                        : 0f;

                map_helper.SetGrayscalePixel(
                    dominantSpeciesMap,
                    x,
                    y,
                    speciesValue
                );
            }
        );

        dominantSpeciesMap.Apply();

        return dominantSpeciesMap;
    }


    // ============================================================
    // DOMINANT SPECIES MAP - COLORED
    // ============================================================

    public static Texture2D GenerateDominantSpeciesMapColored(
        MapInputData inputData,
        List<Species> species,
        float heightWeight = 1f,
        float slopeWeight = 1f,
        float exposureWeight = 1f,
        float moistureWeight = 1f)
    {
        if (
            species == null ||
            species.Count == 0
        )
        {
            Debug.LogWarning(
                "No plant preferences available for dominant species map generation."
            );

            return map_helper.CreateFloatMap(
                inputData.resolution
            );
        }

        Texture2D coloredMap =
            new Texture2D(
                inputData.resolution,
                inputData.resolution,
                TextureFormat.RGBA32,
                false
            );

        map_helper.ForEachPixel(
            inputData.resolution,
            (x, y, normX, normY) =>
            {
                float height =
                    inputData.GetHeight(
                        x,
                        y,
                        normX,
                        normY
                    );

                float slope =
                    inputData.GetSlope(
                        normX,
                        normY
                    );

                float exposure =
                    inputData.GetExposure(
                        normX,
                        normY
                    );

                float moisture =
                    inputData.GetMoisture(
                        x,
                        y,
                        normX,
                        normY
                    );

                float[] suitabilities =
                    new float[
                        species.Count
                    ];

                for (
                    int i = 0;
                    i < species.Count;
                    i++
                )
                {
                    if (
                        species[i] != null
                    )
                    {
                        suitabilities[i] =
                            species[i]
                            .CalculateSuitability(
                                height,
                                slope,
                                exposure,
                                moisture,
                                heightWeight,
                                slopeWeight,
                                exposureWeight,
                                moistureWeight
                            );
                    }
                    else
                    {
                        suitabilities[i] = 0f;
                    }
                }

                int dominantIndex = 0;

                float maxSuitability =
                    suitabilities[0];

                for (
                    int i = 1;
                    i < suitabilities.Length;
                    i++
                )
                {
                    if (
                        suitabilities[i] >
                        maxSuitability
                    )
                    {
                        maxSuitability =
                            suitabilities[i];

                        dominantIndex = i;
                    }
                }

                Color plantColor =
                    species[dominantIndex] != null
                        ? species[dominantIndex].color
                        : Color.white;

                coloredMap.SetPixel(
                    x,
                    y,
                    plantColor
                );
            }
        );

        coloredMap.Apply();

        return coloredMap;
    }


    // ============================================================
    // DOMINANT SPECIES INFO
    // ============================================================

    (Species, float) GetDominantSpeciesInfo(
        MapInputData inputData,
        List<Species> species,
        int x,
        int y,
        float normX,
        float normY)
    {
        if (
            species == null ||
            species.Count == 0
        )
        {
            return (null, 0f);
        }

        int dominantIndex = -1;

        float maxSuitability =
            float.MinValue;

        for (
            int i = 0;
            i < species.Count;
            i++
        )
        {
            Species plant =
                species[i];

            if (plant == null)
            {
                continue;
            }

            float suitability =
                plant.CalculateSuitability(
                    inputData.GetHeight(
                        x,
                        y,
                        normX,
                        normY
                    ),

                    inputData.GetSlope(
                        normX,
                        normY
                    ),

                    inputData.GetExposure(
                        normX,
                        normY
                    ),

                    inputData.GetMoisture(
                        x,
                        y,
                        normX,
                        normY
                    ),

                    heightWeight,
                    slopeWeight,
                    exposureWeight,
                    moistureWeight
                );

            if (
                suitability >
                maxSuitability
            )
            {
                maxSuitability =
                    suitability;

                dominantIndex = i;
            }
        }

        if (dominantIndex < 0)
        {
            return (null, 0f);
        }

        Species dominantPlant =
            species[dominantIndex];

        return (
            dominantPlant,
            maxSuitability
        );
    }


    // ============================================================
    // LOCAL MAXIMUM
    // ============================================================

    bool IsLocalMaximum(
        MapInputData inputData,
        List<Species> species,
        int x,
        int y,
        float suitability)
    {
        int windowRadius =
            Mathf.Max(
                1,
                seedLocalMaximumWindowSize / 2
            );

        for (
            int offsetY = -windowRadius;
            offsetY <= windowRadius;
            offsetY++
        )
        {
            int neighborY =
                y + offsetY;

            if (
                neighborY < 0 ||
                neighborY >=
                    inputData.resolution
            )
            {
                continue;
            }

            for (
                int offsetX = -windowRadius;
                offsetX <= windowRadius;
                offsetX++
            )
            {
                int neighborX =
                    x + offsetX;

                if (
                    offsetX == 0 &&
                    offsetY == 0
                )
                {
                    continue;
                }

                if (
                    neighborX < 0 ||
                    neighborX >=
                        inputData.resolution
                )
                {
                    continue;
                }

                float normX =
                    inputData.resolution > 1
                        ? (float)neighborX /
                          (inputData.resolution - 1)
                        : 0f;

                float normY =
                    inputData.resolution > 1
                        ? (float)neighborY /
                          (inputData.resolution - 1)
                        : 0f;

                var neighborInfo =
                    GetDominantSpeciesInfo(
                        inputData,
                        species,
                        neighborX,
                        neighborY,
                        normX,
                        normY
                    );

                if (
                    neighborInfo.Item2 >=
                    suitability
                )
                {
                    return false;
                }
            }
        }

        return true;
    }


    // ============================================================
    // SEED BLOCKING
    // ============================================================

    bool IsSeedBlocked(
        bool[,] blockedPixels,
        Vector2Int pixel,
        float seedRadiusMeters,
        MapInputData inputData)
    {
        int pixelRadius =
            GetSeedRadiusInPixels(
                seedRadiusMeters,
                inputData
            );

        int minX =
            Mathf.Max(
                0,
                pixel.x - pixelRadius
            );

        int maxX =
            Mathf.Min(
                inputData.resolution - 1,
                pixel.x + pixelRadius
            );

        int minY =
            Mathf.Max(
                0,
                pixel.y - pixelRadius
            );

        int maxY =
            Mathf.Min(
                inputData.resolution - 1,
                pixel.y + pixelRadius
            );

        int radiusSquared =
            pixelRadius *
            pixelRadius;

        for (
            int y = minY;
            y <= maxY;
            y++
        )
        {
            for (
                int x = minX;
                x <= maxX;
                x++
            )
            {
                int dx =
                    x - pixel.x;

                int dy =
                    y - pixel.y;

                if (
                    dx * dx +
                    dy * dy >
                    radiusSquared
                )
                {
                    continue;
                }

                if (
                    blockedPixels[x, y]
                )
                {
                    return true;
                }
            }
        }

        return false;
    }


    void BlockSeedArea(
        bool[,] blockedPixels,
        Vector2Int pixel,
        float seedRadiusMeters,
        MapInputData inputData)
    {
        int pixelRadius =
            GetSeedRadiusInPixels(
                seedRadiusMeters,
                inputData
            );

        int minX =
            Mathf.Max(
                0,
                pixel.x - pixelRadius
            );

        int maxX =
            Mathf.Min(
                inputData.resolution - 1,
                pixel.x + pixelRadius
            );

        int minY =
            Mathf.Max(
                0,
                pixel.y - pixelRadius
            );

        int maxY =
            Mathf.Min(
                inputData.resolution - 1,
                pixel.y + pixelRadius
            );

        int radiusSquared =
            pixelRadius *
            pixelRadius;

        for (
            int y = minY;
            y <= maxY;
            y++
        )
        {
            for (
                int x = minX;
                x <= maxX;
                x++
            )
            {
                int dx =
                    x - pixel.x;

                int dy =
                    y - pixel.y;

                if (
                    dx * dx +
                    dy * dy <=
                    radiusSquared
                )
                {
                    blockedPixels[x, y] =
                        true;
                }
            }
        }
    }


    // ============================================================
    // SEED RADIUS -> PIXELS
    // ============================================================

    int GetSeedRadiusInPixels(
        float seedRadiusMeters,
        MapInputData inputData)
    {
        if (
            seedRadiusMeters <= 0f
        )
        {
            return 0;
        }

        float metersPerPixel =
            map_helper.GetMetersPerPixel(
                inputData
            );

        return Mathf.Max(
            1,
            Mathf.CeilToInt(
                seedRadiusMeters /
                metersPerPixel
            )
        );
    }
}