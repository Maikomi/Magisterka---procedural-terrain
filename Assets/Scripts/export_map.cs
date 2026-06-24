using UnityEngine;
using System.IO;

public class SaveTerrainMapsPNG : MonoBehaviour
{
    public Terrain terrain;

    [Header("Maps To Generate")]
    public bool generateHeightMap = true;
    public bool generateSlopeMap = true;
    public bool generateAspectMap = true;
    public bool generateAspectColorMap = true;

    void Start()
    {
        if (!generateHeightMap && !generateSlopeMap && !generateAspectMap && !generateAspectColorMap)
        {
            Debug.Log("No terrain maps selected for generation.");
            return;
        }

        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        TerrainData data = terrain.terrainData;

        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);

        float terrainWidth = data.size.x;
        float terrainHeight = data.size.y;

        Debug.Log($"Resolution: {res}, width: {terrainWidth}, height: {terrainHeight}");

        Texture2D heightMap = generateHeightMap ? new Texture2D(res, res, TextureFormat.RGB24, false) : null;
        Texture2D heightMapEXR = generateHeightMap ? new Texture2D(res, res, TextureFormat.RGBAFloat, false) : null;
        Texture2D slopeMap = generateSlopeMap ? new Texture2D(res, res, TextureFormat.RGB24, false) : null;
        Texture2D slopeMapEXR = generateSlopeMap ? new Texture2D(res, res, TextureFormat.RGBAFloat, false) : null;
        Texture2D aspectMap = generateAspectMap ? new Texture2D(res, res, TextureFormat.RGB24, false) : null;
        Texture2D aspectMapEXR = generateAspectMap ? new Texture2D(res, res, TextureFormat.RGBAFloat, false) : null;
        Texture2D aspectColorMap = generateAspectColorMap ? new Texture2D(res, res, TextureFormat.RGB24, false) : null;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                if (generateHeightMap)
                {
                    float h = heights[y, x];
                    heightMap.SetPixel(x, y, new Color(h, h, h));
                    heightMapEXR.SetPixel(x, y, new Color(h, h, h, 1f));
                }

                if (generateSlopeMap)
                {
                    float normX = (float)x / (res - 1);
                    float normY = (float)y / (res - 1);

                    float slope = data.GetSteepness(normX, normY) / 90f;

                    slopeMap.SetPixel(x, y, new Color(slope, slope, slope));
                    slopeMapEXR.SetPixel(x, y, new Color(slope, slope, slope, 1f));
                }

                if (generateAspectMap || generateAspectColorMap)
                {
                    if (x > 0 && x < res - 1 && y > 0 && y < res - 1)
                    {
                        float dx = heights[y, x + 1] - heights[y, x - 1];
                        float dy = heights[y + 1, x] - heights[y - 1, x];

                        float aspect = Mathf.Atan2(dy, dx);
                        aspect = (aspect + Mathf.PI) / (2f * Mathf.PI);

                        if (generateAspectMap)
                        {
                            aspectMap.SetPixel(x, y, new Color(aspect, aspect, aspect));
                            aspectMapEXR.SetPixel(x, y, new Color(aspect, aspect, aspect, 1f));
                        }

                        if (generateAspectColorMap)
                        {
                            float r = Mathf.Cos(aspect);
                            float g = Mathf.Sin(aspect);
                            aspectColorMap.SetPixel(x, y, new Color(r, g, aspect));
                        }
                    }
                    else
                    {
                        if (generateAspectMap)
                        {
                            aspectMap.SetPixel(x, y, Color.black);
                            aspectMapEXR.SetPixel(x, y, Color.black);
                        }

                        if (generateAspectColorMap)
                        {
                            aspectColorMap.SetPixel(x, y, Color.black);
                        }
                    }
                }
            }
        }

        Directory.CreateDirectory(Application.dataPath + "/maps");

        if (generateHeightMap)
        {
            SavePngAndExr(heightMap, heightMapEXR, "heightmap");
        }

        if (generateSlopeMap)
        {
            SavePngAndExr(slopeMap, slopeMapEXR, "slopemap");
        }

        if (generateAspectMap)
        {
            SavePngAndExr(aspectMap, aspectMapEXR, "aspectmap");
        }

        if (generateAspectColorMap)
        {
            aspectColorMap.Apply();
            byte[] aspectColorBytes = aspectColorMap.EncodeToPNG();
            File.WriteAllBytes(Application.dataPath + "/maps/aspectcolormap.png", aspectColorBytes);
        }

        Debug.Log("Selected terrain maps saved!");
    }

    void SavePngAndExr(Texture2D pngMap, Texture2D exrMap, string fileName)
    {
        pngMap.Apply();
        exrMap.Apply();

        byte[] pngBytes = pngMap.EncodeToPNG();
        byte[] exrBytes = exrMap.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);

        File.WriteAllBytes(Application.dataPath + $"/maps/{fileName}.png", pngBytes);
        File.WriteAllBytes(Application.dataPath + $"/maps/{fileName}.exr", exrBytes);
    }
}
