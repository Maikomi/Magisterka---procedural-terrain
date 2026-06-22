using UnityEngine;
using System.IO;

public class SaveTerrainMapsPNG : MonoBehaviour
{
    public Terrain terrain;

    void Start()
    {
        // Jeśli nie przypisano Terrain w Inspectorze
        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        TerrainData data = terrain.terrainData;

        int res = data.heightmapResolution;

        float[,] heights = data.GetHeights(0, 0, res, res);

        float terrainWidth = data.size.x;
        float terrainHeight = data.size.y;

        float metersPerPixel = terrainWidth / res;
        Debug.Log($"ressolution: {res}, width: {terrainWidth}, height: {terrainHeight}");


        Texture2D heightMap = new Texture2D(res, res, TextureFormat.RGB24, false);
        Texture2D heightMapEXR = new Texture2D(res, res, TextureFormat.RGBAFloat, false);
        Texture2D slopeMap = new Texture2D(res, res, TextureFormat.RGB24, false);
        Texture2D slopeMapEXR = new Texture2D(res, res, TextureFormat.RGBAFloat, false);
        Texture2D aspectMap = new Texture2D(res, res, TextureFormat.RGB24, false);
        Texture2D aspectMapEXR = new Texture2D(res, res, TextureFormat.RGBAFloat, false);
        Texture2D aspectColorMap = new Texture2D(res, res, TextureFormat.RGB24, false);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                // heightmap
                float h = heights[y, x];
                heightMap.SetPixel(x, y, new Color(h, h, h));
                heightMapEXR.SetPixel(x, y, new Color(h, 0, 0, 1));

                // slopemap
                float normX = (float)x / (res - 1);
                float normY = (float)y / (res - 1);

                float slope = data.GetSteepness(normX, normY) / 90f; // Normalize slope to [0, 1]
                float slopeEXR = data.GetSteepness(normX, normY); // Keep slope in degrees for EXR

                slopeMap.SetPixel(x, y, new Color(slope, slope, slope));
                slopeMapEXR.SetPixel(x, y, new Color(slopeEXR, 0, 0, 1));

                //aspectmap

                if (x > 0 && x < res - 1 && y > 0 && y < res - 1) //pomijamy krawędzie, bo tam nie można obliczyć nachylenia
                {
                    // różnice wysokości w osiach x i y
                    float dx = heights[y, x + 1] - heights[y, x - 1];
                    float dy = heights[y + 1, x] - heights[y - 1, x];

                    // kąt w radianach
                    float aspect = Mathf.Atan2(dy, dx);

                    // zamiana z [-PI, PI] -> [0, 1]
                    aspect = (aspect + Mathf.PI) / (2f * Mathf.PI);

                    //bawimy się kolorkami
                    float R = Mathf.Cos(aspect);
                    float G = Mathf.Sin(aspect);

                    aspectMap.SetPixel(x, y, new Color(aspect, aspect, aspect));
                    aspectColorMap.SetPixel(x, y, new Color(R, G, aspect));
                    aspectMapEXR.SetPixel(x, y, new Color(aspect, 0, 0, 1));
                }
                else
                {
                    aspectMap.SetPixel(x, y, Color.black);
                }
            }
        }

        heightMap.Apply();
        heightMapEXR.Apply();
        byte[] heightBytes = heightMap.EncodeToPNG();
        byte[] heightBytesEXR = heightMapEXR.EncodeToEXR();
        File.WriteAllBytes(Application.dataPath + "/maps/heightmap.png", heightBytes);
        File.WriteAllBytes(Application.dataPath + "/maps/heightmap.exr", heightBytesEXR);

        slopeMap.Apply();
        slopeMapEXR.Apply();
        byte[] slopeBytes = slopeMap.EncodeToPNG();
        byte[] slopeBytesEXR = slopeMap.EncodeToEXR();
        File.WriteAllBytes(Application.dataPath + "/maps/slopemap.png", slopeBytes);
        File.WriteAllBytes(Application.dataPath + "/maps/slopemap.exr", slopeBytesEXR);

        aspectMap.Apply();
        aspectMapEXR.Apply();
        byte[] aspectBytes = aspectMap.EncodeToPNG();
        byte[] aspectBytesEXR = aspectMap.EncodeToEXR();
        File.WriteAllBytes(Application.dataPath + "/maps/aspectmap.png", aspectBytes);
        File.WriteAllBytes(Application.dataPath + "/maps/aspectmap.exr", aspectBytesEXR);

        aspectColorMap.Apply();
        byte[] aspectColorBytes = aspectColorMap.EncodeToPNG();
        File.WriteAllBytes(Application.dataPath + "/maps/aspectcolormap.png", aspectColorBytes);

        Debug.Log("Saved!");
    }
}