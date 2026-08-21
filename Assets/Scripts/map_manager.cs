using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;

public class map_manager : MonoBehaviour
{
    public Terrain terrain;

    public map_analysis mapAnalysis;
    public plant_analysis plantAnalysis;

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
        new Species("buk", 0.1f, 0.0f, 0.5f, 0.6f, 7f, 1f, 15f, 1f, 1f, new Color(0.2f, 0.8f, 0.2f, 1f)),
        new Species("swierk", 0.7f, 0.6f, 0.5f, 0.7f, 5f, 1f, 12f, 1f, 1f, new Color(0.2f, 0.4f, 0.8f, 1f)),
        new Species("krzak", 0.5f, 0.2f, 0.6f, 0.4f, 1f, 1f, 5f, 1f, 1f, new Color(1f, 0.9f, 0.2f, 1f))
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
        if (generateHeightMap)
        {
            mapAnalysis.GenerateHeightMap(terrain.terrainData, Path.GetFileNameWithoutExtension(heightMapFileName));
        }
        if (generateSlopeMap)
        {
            mapAnalysis.GenerateSlopeMap(terrain.terrainData, Path.GetFileNameWithoutExtension(slopeMapFileName));
        }
        if (generateAspectMap)
        {
            mapAnalysis.GenerateAspectMap(terrain.terrainData, Path.GetFileNameWithoutExtension(aspectMapFileName));
        }
        if (generateMoistureMap)
        {
            MapInputData inputData = PrepareInputData();
            if (inputData.IsValid)
            {
                mapAnalysis.GenerateMoistureMap(inputData, moistureMapFileName, generateMoisturePreview);
            }
        }
        if (generatePlantSuitabilityPreviews)
        {
            MapInputData inputData = PrepareInputData();
            if (inputData.IsValid)
            {
                plantAnalysis.GeneratePlantSuitabilityMaps(inputData, species, generatePlantSuitabilityPreviews);
            }
        }
        if (generateSeedMap)
        {
            MapInputData inputData = PrepareInputData();
            if (inputData.IsValid)
            {
                plantAnalysis.GenerateSeedMap(inputData, species, seedMapFileName, generateSeedMap);
            }
        }
        if (generateDominantSpeciesMap)
        {
            MapInputData inputData = PrepareInputData();
            if (inputData.IsValid)
            {
                plantAnalysis.GenerateDominantSpeciesMap(inputData, species, dominantSpeciesMapFileName, generateDominantSpeciesMap);
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

