using UnityEngine;
using System.Collections;
using System.IO;

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

    [Header("Output")]
    public bool generateMoisturePreview = true;
    public string moistureMapFileName = "moistureMap";
    public string moisturePreviewFileName = "MoisturePreview";

    IEnumerator Start()
    {
        yield return null;

        if (!generateMoistureMap)
        {
            Debug.Log("Moisture map generation is disabled.");
            yield break;
        }

        GenerateMoistureMap();
    }

    void GenerateMoistureMap()
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

        string mapsPath = Application.dataPath + "/maps";
        Directory.CreateDirectory(mapsPath);

        Texture2D heightMap = LoadMap(Path.Combine(mapsPath, heightMapFileName));
        Texture2D slopeMap = LoadMap(Path.Combine(mapsPath, slopeMapFileName));
        Texture2D exposureMap = LoadMap(GetAnnualSolarExposurePath(mapsPath));

        if (exposureMap == null)
        {
            Debug.LogWarning("Annual solar exposure map was not found. Moisture map was not generated.");
            return;
        }

        TerrainData data = terrain != null ? terrain.terrainData : null;
        float[,] heights = null;

        if (heightMap == null && data == null)
        {
            Debug.LogWarning("Height map was not found and no terrain is available. Moisture map was not generated.");
            return;
        }

        if (slopeMap == null && data == null)
        {
            Debug.LogWarning("Slope map was not found and no terrain is available. Moisture map was not generated.");
            return;
        }

        if ((heightMap == null || slopeMap == null) && data != null)
        {
            heights = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
        }

        int res = GetOutputResolution(data, heightMap, slopeMap, exposureMap);

        if (res <= 0)
        {
            Debug.LogWarning("Could not determine moisture map resolution.");
            return;
        }

        Texture2D moistureMap = new Texture2D(res, res, TextureFormat.RGBAFloat, false);
        Texture2D moisturePreview = generateMoisturePreview ? new Texture2D(res, res, TextureFormat.RGB24, false) : null;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float normX = res > 1 ? (float)x / (res - 1) : 0f;
                float normY = res > 1 ? (float)y / (res - 1) : 0f;

                float height = GetHeightValue(heightMap, heights, x, y, normX, normY);
                float slope = GetSlopeValue(slopeMap, data, normX, normY);
                float exposure = GetMapValue(exposureMap, normX, normY);

                float moisture =
                    0.3f * (1f - height)
                    + 0.2f * (1f - slope)
                    + 0.5f * (1f - exposure);

                moisture = Mathf.Clamp01(moisture);
                Color moistureColor = new Color(moisture, moisture, moisture, 1f);

                moistureMap.SetPixel(x, y, moistureColor);

                if (moisturePreview != null)
                {
                    moisturePreview.SetPixel(x, y, moistureColor);
                }
            }
        }

        moistureMap.Apply();
        File.WriteAllBytes(
            Path.Combine(mapsPath, $"{moistureMapFileName}.exr"),
            moistureMap.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat)
        );

        if (moisturePreview != null)
        {
            moisturePreview.Apply();
            File.WriteAllBytes(
                Path.Combine(mapsPath, $"{moisturePreviewFileName}.png"),
                moisturePreview.EncodeToPNG()
            );
        }

        Debug.Log("Moisture map saved!");
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

    int GetOutputResolution(TerrainData data, Texture2D heightMap, Texture2D slopeMap, Texture2D exposureMap)
    {
        if (data != null)
        {
            return data.heightmapResolution;
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

    float GetHeightValue(Texture2D heightMap, float[,] heights, int x, int y, float normX, float normY)
    {
        if (heightMap != null)
        {
            return GetMapValue(heightMap, normX, normY);
        }

        if (heights != null)
        {
            int heightY = Mathf.Clamp(y, 0, heights.GetLength(0) - 1);
            int heightX = Mathf.Clamp(x, 0, heights.GetLength(1) - 1);
            return Mathf.Clamp01(heights[heightY, heightX]);
        }

        return 0f;
    }

    float GetSlopeValue(Texture2D slopeMap, TerrainData data, float normX, float normY)
    {
        if (slopeMap != null)
        {
            return GetMapValue(slopeMap, normX, normY);
        }

        if (data != null)
        {
            return Mathf.Clamp01(data.GetSteepness(normX, normY) / 90f);
        }

        return 0f;
    }

    float GetMapValue(Texture2D map, float normX, float normY)
    {
        if (map == null)
        {
            return 0f;
        }

        return Mathf.Clamp01(map.GetPixelBilinear(normX, normY).r);
    }
}
