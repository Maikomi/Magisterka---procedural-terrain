using UnityEngine;
using System;
using System.Diagnostics;
using System.IO;

public class metadata_json : MonoBehaviour
{
    [Serializable]
    public class PlantSpeciesLegend
    {
        public string plantName;
        public string colorHex;
        public int colorIndex;
        public float seedRadius;

        public PlantSpeciesLegend(string plantName, string colorHex, int colorIndex, float seedRadius)
        {
            this.plantName = plantName;
            this.colorHex = colorHex;
            this.colorIndex = colorIndex;
            this.seedRadius = seedRadius;
        }
    }

    [Serializable]
    public class TerrainAnalysisMetadata
    {
        public string terrainName;

        public int terrainResolution;
        public float terrainWidth;
        public float terrainLength;
        public float terrainHeight;

        public float latitude;
        public int dayOfYear;
        public int sampledDays;
        public int annualSamples;
        public int timeSamples;

        public int hourStart;
        public int hourEnd;
        public float hourStep;

        public int maxDistanceSamples;

        public bool generateHeightMap;
        public bool generateSlopeMap;
        public bool generateAspectMap;
        public bool generateAspectColorMap;
        public bool generateDailySolarExposure;
        public bool generateAnnualSolarExposure;
        public bool generateMoistureMap;
        public bool generatePlantSuitabilityPreviews;
        public bool generateDominantSpeciesMap;
        public bool generateSeedMap;
        public bool generateGrassMap;
        public bool generateGrassPreview;
        public bool usePoissonDiscSeedDistribution;

        public float seedSuitabilityThreshold;
        public int seedLocalMaximumWindowSize;
        public float seedDensityPower;
        public float seedProbabilityPower;
        public int poissonCandidatesPerPoint;
        public float poissonRadiusMultiplier;
        public float heightWeight;
        public float slopeWeight;
        public float exposureWeight;
        public float moistureWeight;

        public PlantSpeciesLegend[] plantSpeciesLegend;

        public float executionTimeMs;
        public string generationDate;
    }

    public Terrain terrain;
    //public SaveTerrainMapsPNG terrainMapGenerator;
    public DailySolarExposure solarExposureGenerator;
    public map_analysis mapAnalysisGenerator;
    public map_manager mapManager;
    public plant_analysis plantAnalysisGenerator;

    public TerrainAnalysisMetadata meta = new TerrainAnalysisMetadata();

    void Start()
    {
        map_helper.EnsureRunFolder();
        OutputJSON();
    }

    public void OutputJSON()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        FillMetadata();

        stopwatch.Stop();
        meta.executionTimeMs = (float)stopwatch.Elapsed.TotalMilliseconds;

        string mapsPath = Application.dataPath + "/maps";
        Directory.CreateDirectory(mapsPath);

        string json = JsonUtility.ToJson(meta, true);
        string metadataFilePath = mapsPath + "/metadata.json";
        File.WriteAllText(metadataFilePath, json);
        map_helper.CopyToRunFolder(metadataFilePath);

        UnityEngine.Debug.Log($"Metadata json saved in {meta.executionTimeMs:F2} ms.");
    }

    void FillMetadata()
    {
        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        // if (terrainMapGenerator == null)
        // {
        //     terrainMapGenerator = FindAnyObjectByType<SaveTerrainMapsPNG>();
        // }

        if (solarExposureGenerator == null)
        {
            solarExposureGenerator = FindAnyObjectByType<DailySolarExposure>();
        }

        if (mapAnalysisGenerator == null)
        {
            mapAnalysisGenerator = FindAnyObjectByType<map_analysis>();
        }

        if (mapManager == null)
        {
            mapManager = FindAnyObjectByType<map_manager>();
        }

        if (plantAnalysisGenerator == null)
        {
            plantAnalysisGenerator = FindAnyObjectByType<plant_analysis>();
        }

        if (terrain != null)
        {
            TerrainData data = terrain.terrainData;

            meta.terrainName = data != null ? data.name : terrain.name;
            meta.terrainResolution = data != null ? data.heightmapResolution : 0;
            meta.terrainWidth = data != null ? data.size.x : 0f;
            meta.terrainLength = data != null ? data.size.z : 0f;
            meta.terrainHeight = data != null ? data.size.y : 0f;
        }

        if (solarExposureGenerator != null)
        {
            meta.latitude = solarExposureGenerator.latitude;
            meta.dayOfYear = solarExposureGenerator.dayOfYear;
            meta.sampledDays = 1;
            meta.annualSamples = solarExposureGenerator.annualSamples;
            meta.timeSamples = CountTimeSamples(
                solarExposureGenerator.hourStart,
                solarExposureGenerator.hourEnd,
                solarExposureGenerator.hourStep
            );
            meta.hourStart = solarExposureGenerator.hourStart;
            meta.hourEnd = solarExposureGenerator.hourEnd;
            meta.hourStep = solarExposureGenerator.hourStep;
            meta.maxDistanceSamples = solarExposureGenerator.maxDistanceSamples;
            meta.generateDailySolarExposure = solarExposureGenerator.generateDailySolarExposure;
            meta.generateAnnualSolarExposure = solarExposureGenerator.generateAnnualSolarExposure;
        }

        if (mapManager != null)
        {
            meta.generateHeightMap = mapManager.generateHeightMap;
            meta.generateSlopeMap = mapManager.generateSlopeMap;
            meta.generateAspectMap = mapManager.generateAspectMap;
            meta.generateMoistureMap = mapManager.generateMoistureMap;
            meta.generatePlantSuitabilityPreviews = mapManager.generatePlantSuitabilityPreviews;
            meta.generateDominantSpeciesMap = mapManager.generateDominantSpeciesMap;
            meta.generateSeedMap = mapManager.generateSeedMap;
            meta.generateGrassMap = mapManager.generateGrassMap;
            meta.generateGrassPreview = mapManager.generateGrassPreview;
        }

        if (plantAnalysisGenerator != null)
        {
            meta.seedSuitabilityThreshold = plantAnalysisGenerator.seedSuitabilityThreshold;
            meta.seedLocalMaximumWindowSize = plantAnalysisGenerator.seedLocalMaximumWindowSize;
            meta.seedDensityPower = plantAnalysisGenerator.seedDensityPower;
            meta.seedProbabilityPower = plantAnalysisGenerator.seedDensityPower;
            meta.usePoissonDiscSeedDistribution = plantAnalysisGenerator.usePoissonDiscSeedDistribution;
            meta.poissonCandidatesPerPoint = plantAnalysisGenerator.poissonCandidatesPerPoint;
            meta.poissonRadiusMultiplier = plantAnalysisGenerator.poissonRadiusMultiplier;
            meta.heightWeight = plantAnalysisGenerator.heightWeight;
            meta.slopeWeight = plantAnalysisGenerator.slopeWeight;
            meta.exposureWeight = plantAnalysisGenerator.exposureWeight;
            meta.moistureWeight = plantAnalysisGenerator.moistureWeight;
        }

        if (mapManager != null && mapManager.species != null)
        {
            meta.plantSpeciesLegend = new PlantSpeciesLegend[mapManager.species.Count];

            for (int i = 0; i < mapManager.species.Count; i++)
            {
                Species species = mapManager.species[i];
                if (species == null)
                {
                    continue;
                }

                string colorHex = ColorUtility.ToHtmlStringRGB(species.color);
                meta.plantSpeciesLegend[i] = new PlantSpeciesLegend(
                    species.plantName,
                    colorHex,
                    i,
                    species.seedRadius
                );
            }
        }

        meta.generationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    int CountTimeSamples(int hourStart, int hourEnd, float hourStep)
    {
        if (hourStep <= 0f)
        {
            return 0;
        }

        int samples = 0;

        for (float hour = hourStart; hour <= hourEnd; hour += hourStep)
        {
            samples++;
        }

        return samples;
    }
}
