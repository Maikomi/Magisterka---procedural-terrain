using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;


public class map_manager : MonoBehaviour
{
    public Terrain terrain;
    private vegetation_performance_profiler profiler;
    public map_analysis mapAnalysis;
    public plant_analysis plantAnalysis;
    public grass_analysis grassAnalysis;

    public plant_placement plantPlacement;
    public plant_competition plantCompetition;
    public DailySolarExposure solar_exposure;
    [Header("Place Plants")]
    public bool placePlants = true;

    [Header("Maps To Generate")]
    public bool generateHeightMap = true;
    public bool generateSlopeMap = true;
    public bool generateAspectMap = true;
    public DailySolarExposure solarExposureGenerator;
    public bool generateMoistureMap = true;
    public bool generatePlantSuitabilityPreviews = true;
    public bool generateSeedMap = true;

    public string seedMapFileName = "SeedMap";
    public bool generateDominantSpeciesMap = true;
    public string dominantSpeciesMapFileName = "DominantSpeciesMap";
    public bool generateGrassMap = true;
    public string grassMapFileName = "GrassMap";
    public bool generateGrassPreview = true;

    [Header("Input Maps")]
    public string heightMapFileName = "heightmap.exr";
    public string slopeMapFileName = "slopemap.exr";
    public string aspectMapFileName = "aspectmap.exr";
    public string annualSolarExposureMapFileName = "";

    [Header("Moisture Output")]
    public bool generateMoisturePreview = true;
    public string moistureMapFileName = "moistureMap";
    public string moisturePreviewFileName = "MoisturePreview";

    public List<Species> species = new List<Species>
    {
       new Species(
            "swierk",
            new Vector2(0.3f, 0.9f), // wysokość
            new Vector2(0.2f, 0.7f), // nachylenie
            new Vector2(0.2f, 0.8f), // ekspozycja
            new Vector2(0.5f, 1.0f), // wilgotność
            5f,                     // seedRadius
            1f,                     // growthRate
            15f,                    // maxRadius
            1f,                     // competitiveness
            1f,                     // shadePreference
            new Color(0.2f, 0.4f, 0.8f, 1f)
        ),
        new Species("swierk", new Vector2(0.6f, 0.8f), new Vector2(0.55f, 0.65f), new Vector2( 0.45f, 0.5f), new Vector2(0.7f, 0.85f), 5f, 1f, 12f, 1f, 1f, new Color(0.2f, 0.4f, 0.8f, 1f)),
        new Species("krzak", new Vector2(0.45f, 0.5f), new Vector2(0.15f, 0.25f), new Vector2(0.55f, 0.65f), new Vector2(0.4f, 0.6f), 1f, 1f, 5f, 1f, 1f, new Color(1f, 0.9f, 0.2f, 1f))
    };

    void Awake()
    {
        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        if (mapAnalysis == null)
        {
            mapAnalysis = FindAnyObjectByType<map_analysis>();
        }

        if (plantAnalysis == null)
        {
            plantAnalysis = FindAnyObjectByType<plant_analysis>();
        }

        if (grassAnalysis == null)
        {
            grassAnalysis = FindAnyObjectByType<grass_analysis>();
        }

        if (plantCompetition == null)
        {
            plantCompetition = FindAnyObjectByType<plant_competition>();
        }

        if (plantPlacement == null)
        {
            plantPlacement = FindAnyObjectByType<plant_placement>();
        }

        if (solar_exposure == null)
        {
            solar_exposure = FindAnyObjectByType<DailySolarExposure>();
        }
        profiler = GetComponent<vegetation_performance_profiler>();

        if (profiler == null)
        {
            profiler = gameObject.AddComponent<vegetation_performance_profiler>();
        }
    }

    IEnumerator Start()
    {
        yield return null;
        GenerateSelectedMaps();
    }
    public string GetAnnualSolarExposurePath(string mapsPath)
    {
        if (!string.IsNullOrWhiteSpace(annualSolarExposureMapFileName))
        {
            return Path.Combine(mapsPath, annualSolarExposureMapFileName);
        }

        int sampleCount = Mathf.Clamp(solar_exposure.annualSamples, 1, 365);
        Debug.Log($"Sample count for annual solar exposure: {sampleCount}");
        return Path.Combine(mapsPath, $"AnnualSolarExposure_{sampleCount}.exr");
    }
    void GenerateSelectedMaps()
    {
        // Create timestamp folder at the start for all exports
        string runFolderPath = map_helper.EnsureRunFolder();

        if (generateHeightMap)
        {
            profiler.StartStage("Height Map");
            mapAnalysis.GenerateHeightMap(terrain.terrainData, Path.GetFileNameWithoutExtension(heightMapFileName));
            profiler.StopStage();
        }
        if (generateSlopeMap)
        {
            profiler.StartStage("Slope Map");
            mapAnalysis.GenerateSlopeMap(terrain.terrainData, Path.GetFileNameWithoutExtension(slopeMapFileName));
            profiler.StopStage();
        }
        if (generateAspectMap)
        {
            profiler.StartStage("Aspect Map");
            mapAnalysis.GenerateAspectMap(terrain.terrainData, Path.GetFileNameWithoutExtension(aspectMapFileName));
            profiler.StopStage();
        }
        if (generateMoistureMap)
        {
            MapInputData inputData = PrepareInputData();
            if (inputData.IsValid)
            {
                Debug.Log($"mapAnalysis = {mapAnalysis}");
                Debug.Log($"solar_exposure = {solar_exposure}");
                Debug.Log($"solarExposureGenerator = {solarExposureGenerator}");
                Debug.Log($"terrain = {terrain}");
                profiler.StartStage("Moisture Map");
                mapAnalysis.GenerateMoistureMap(inputData, moistureMapFileName, generateMoisturePreview);
                profiler.StopStage();
            }
        }

        if (generateGrassMap)
        {
            MapInputData inputData = PrepareInputData();

            if (inputData.IsValid)
            {
                if (grassAnalysis == null)
                {
                    Debug.LogError(
                        "Grass Analysis is not assigned."
                    );

                    return;
                }

                profiler.StartStage("Grass Map");
                grassAnalysis.GenerateGrassMap(
                    inputData,
                    grassMapFileName,
                    generateGrassPreview
                );
                profiler.StopStage();
            }
        }

        if (generatePlantSuitabilityPreviews)
        {
            MapInputData inputData = PrepareInputData();
            if (inputData.IsValid)
            {
                profiler.StartStage("Plant Suitability Previews");
                plantAnalysis.GeneratePlantSuitabilityMaps(inputData, species, generatePlantSuitabilityPreviews);
                profiler.StopStage();
            }
        }
        if (generateSeedMap)
        {
            MapInputData inputData = PrepareInputData();
            if (inputData.IsValid)
            {
                profiler.StartStage("Seed Map");
                plantAnalysis.GenerateSeedMap(inputData, species, seedMapFileName, generateSeedMap);
                profiler.StopStage();
            }
        }
        if (generateDominantSpeciesMap)
        {
            MapInputData inputData = PrepareInputData();
            if (inputData.IsValid)
            {
                profiler.StartStage("Dominant Species Map");
                plantAnalysis.GenerateDominantSpeciesMap(inputData, species, dominantSpeciesMapFileName, generateDominantSpeciesMap);
                profiler.StopStage();
            }
        }
        if (placePlants)
        {
            if (plantCompetition == null)
            {
                Debug.LogError("Plant Competition is not assigned.");
                return;
            }

            if (plantPlacement == null)
            {
                Debug.LogError("Plant placement is not assigned.");
                return;
            }

            plantCompetition.RunCompetition();

            plantPlacement.plantCompetition = plantCompetition;

            if (plantPlacement.plantAnalysis == null)
            {
                plantPlacement.plantAnalysis = plantAnalysis;
            }

            if (plantPlacement.terrain == null)
            {
                plantPlacement.terrain = terrain;
            }

            plantPlacement.PlacePlants();
        }

        profiler.PrintReport();

        // Export performance data to the timestamp folder
        string csvPath = Path.Combine(runFolderPath, "vegetation_performance.csv");
        string jsonPath = Path.Combine(runFolderPath, "vegetation_performance.json");

        profiler.ExportCSV(csvPath);
        profiler.ExportJSON(jsonPath);
    }

    MapInputData PrepareInputData()
    {
        string mapsPath = map_helper.GetMapsPath();
        Texture2D heightMap = map_helper.LoadMap(Path.Combine(mapsPath, heightMapFileName));
        Texture2D slopeMap = map_helper.LoadMap(Path.Combine(mapsPath, slopeMapFileName));
        Texture2D exposureMap = map_helper.LoadMap(GetAnnualSolarExposurePath(mapsPath));
        Texture2D moistureMap = map_helper.LoadMap(Path.Combine(mapsPath, $"{moistureMapFileName}.exr"));

        if (exposureMap == null)
        {
            Debug.LogWarning("Annual solar exposure map was not found. Map analysis was not generated.");
            //return MapInputData.Invalid;
        }

        TerrainData terrainData = terrain != null ? terrain.terrainData : null;

        if (heightMap == null && terrainData == null)
        {
            Debug.LogWarning("Height map was not found and no terrain is available. Map analysis was not generated.");
            //return MapInputData.Invalid;
        }

        if (slopeMap == null && terrainData == null)
        {
            Debug.LogWarning("Slope map was not found and no terrain is available. Map analysis was not generated.");
            //return MapInputData.Invalid;
        }

        int resolution = map_helper.GetOutputResolution(terrainData, heightMap, slopeMap, exposureMap);

        if (resolution <= 0)
        {
            Debug.LogWarning("Could not determine map analysis resolution.");
            //return MapInputData.Invalid;
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
}

