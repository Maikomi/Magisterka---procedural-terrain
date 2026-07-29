using UnityEngine;
using System;
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
        public bool generateDominantSpeciesMap;
        public bool generateSeedMap;

        public float seedSuitabilityThreshold;
        public int seedLocalMaximumWindowSize;
        public float seedProbabilityPower;

        public PlantSpeciesLegend[] plantSpeciesLegend;

        public string generationDate;
    }

    public Terrain terrain;
    //public SaveTerrainMapsPNG terrainMapGenerator;
    public DailySolarExposure solarExposureGenerator;
    public map_analysis mapAnalysisGenerator;

    public TerrainAnalysisMetadata meta = new TerrainAnalysisMetadata();

    void Start()
    {
        OutputJSON();
    }

    public void OutputJSON()
    {
        FillMetadata();

        string mapsPath = Application.dataPath + "/maps";
        Directory.CreateDirectory(mapsPath);

        string json = JsonUtility.ToJson(meta, true);
        File.WriteAllText(mapsPath + "/metadata.json", json);

        Debug.Log("Metadata json saved!");
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

        // if (terrainMapGenerator != null)
        // {
        //     meta.generateHeightMap = terrainMapGenerator.generateHeightMap;
        //     meta.generateSlopeMap = terrainMapGenerator.generateSlopeMap;
        //     meta.generateAspectMap = terrainMapGenerator.generateAspectMap;
        //     meta.generateAspectColorMap = terrainMapGenerator.generateAspectColorMap;
        // }

        // if (mapAnalysisGenerator != null)
        // {
        //     meta.generateDominantSpeciesMap = mapAnalysisGenerator.generateDominantSpeciesMap;
        //     meta.generateSeedMap = mapAnalysisGenerator.generateSeedMap;
        //     meta.seedSuitabilityThreshold = mapAnalysisGenerator.seedSuitabilityThreshold;
        //     meta.seedLocalMaximumWindowSize = mapAnalysisGenerator.seedLocalMaximumWindowSize;
        //     meta.seedProbabilityPower = mapAnalysisGenerator.seedProbabilityPower;

        //     if (mapAnalysisGenerator.plantPreferences != null)
        //     {
        //         meta.plantSpeciesLegend = new PlantSpeciesLegend[mapAnalysisGenerator.plantPreferences.Length];
        //         for (int i = 0; i < mapAnalysisGenerator.plantPreferences.Length; i++)
        //         {
        //             PlantPreference plant = mapAnalysisGenerator.plantPreferences[i];
        //             if (plant != null)
        //             {
        //                 string colorHex = ColorUtility.ToHtmlStringRGB(plant.plantColor);
        //                 meta.plantSpeciesLegend[i] = new PlantSpeciesLegend(plant.plantName, colorHex, i, plant.seedRadius);
        //             }
        //         }
        //     }
        // }

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
