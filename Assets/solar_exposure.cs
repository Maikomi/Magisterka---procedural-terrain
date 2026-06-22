using UnityEngine;
using System.IO;

public class DailySolarExposure : MonoBehaviour
{
    public Terrain terrain;

    [Header("Location")]
    public float latitude = 45f;
    [Header("Time")]
    public int dayOfYear = 15; 

    [Header("Sampling")]
    public int hourStart = 5;
    public int hourEnd = 21;
    public float hourStep = 1f;

    [Header("Raymarch")]
    public int maxDistanceSamples = 32;

    void Start()
    {
        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        TerrainData data = terrain.terrainData;

        int res = data.heightmapResolution;

        float[,] heights = data.GetHeights(0, 0, res, res);

        //Texture2D exposureMap = new Texture2D(res, res, TextureFormat.RGB24, false);
        Texture2D exposureMap = new Texture2D(res, res, TextureFormat.RFloat, false);

        float terrainWidth = data.size.x;
        float terrainHeight = data.size.y;

        float metersPerPixel =
            terrainWidth / res;

        // =========================
        // MAX POSSIBLE LIGHT
        // =========================

        float maxPossibleLight = 0f;
        float annualMaxPossibleLight = 0f;
        float[,] annualExposure = new float[res, res];

        for (int i = 0; i < 12; i++)
        {
            int currentDay = 15 + i * 30;

            for (
                float hour = hourStart;
                hour <= hourEnd;
                hour += hourStep
            )
            {
                GetSunPosition(
                    latitude,
                    currentDay,
                    hour,
                    out float azimuthDeg,
                    out float elevationDeg
                );

                if (elevationDeg <= 0f)
                    continue;

                float intensity = Mathf.Sin(elevationDeg * Mathf.Deg2Rad);

                annualMaxPossibleLight += intensity;
            }
        }

        // =========================
        // LICZENIE EXPOSURE
        // =========================

        for (int i = 0; i < 12; i++)
        {
            int currentDay = 1 + i * 30;

            if( currentDay > 365)
                currentDay = 365;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float totalLight = annualExposure[x, y]; ;

                    float currentHeight = heights[y, x] * terrainHeight;

                    // =========================
                    // próbki czasu
                    // =========================

                    for (
                        float hour = hourStart;
                        hour <= hourEnd;
                        hour += hourStep
                    )
                    {
                        // pozycja słońca
                        GetSunPosition(latitude, dayOfYear, hour, out float azimuthDeg, out float elevationDeg);

                        // słońce pod horyzontem
                        if (elevationDeg <= 0f)
                            continue;

                        // intensywność światła
                        float intensity = Mathf.Sin(elevationDeg * Mathf.Deg2Rad);

                        // kierunek słońca
                        float azimuthRad = azimuthDeg * Mathf.Deg2Rad;

                        float dirX = Mathf.Sin(azimuthRad);
                        float dirY = Mathf.Cos(azimuthRad);

                        bool blocked = false;

                        // =========================
                        // raymarch terrain
                        // =========================

                        for (int step = 1; step < maxDistanceSamples; step++)
                        {
                            int sampleX = Mathf.RoundToInt(x + dirX * step);

                            int sampleY = Mathf.RoundToInt(y + dirY * step);

                            if (sampleX < 0 || sampleX >= res || sampleY < 0 || sampleY >= res)
                            {
                                break;
                            }

                            float sampleHeight = heights[sampleY, sampleX] * terrainHeight;

                            float horizontalDistance = step * metersPerPixel;

                            float terrainAngle = Mathf.Atan2(sampleHeight - currentHeight, horizontalDistance) * Mathf.Rad2Deg;

                            // teren zasłania słońce
                            if (terrainAngle > elevationDeg)
                            {
                                blocked = true;
                                break;
                            }
                        }

                        // jeśli światło dociera
                        if (!blocked)
                        {
                            totalLight += intensity;
                        }
                    }

                    // normalizacja
                    float normalized = totalLight / maxPossibleLight;

                    // contrast boost
                    normalized = Mathf.Pow(normalized, 0.25f);

                    //exposureMap.SetPixel(x, y, new Color(normalized, normalized, normalized));
                    annualExposure[x, y] += totalLight;
                }
            } 
        }

        exposureMap.Apply();
        //byte[] bytes =exposureMap.EncodeToPNG();
        byte[] bytes = exposureMap.EncodeToEXR();
        //File.WriteAllBytes(Application.dataPath + $"/maps/daily_solar_exposure_{dayOfYear}.png", bytes);
        File.WriteAllBytes(Application.dataPath + $"/maps/daily_solar_exposure.exr", bytes);

        Debug.Log("Solar exposure map saved!");
    }

    // =========================
    // ASTRONOMIA
    // =========================

    void GetSunPosition(
        float latitudeDeg,
        int dayOfYear,
        float hour,
        out float azimuth,
        out float elevation
    )
    {
        float latitude = latitudeDeg * Mathf.Deg2Rad;

        // deklinacja słońca
        float declination = 23.45f * Mathf.Sin(Mathf.Deg2Rad * (360f / 365f) * (dayOfYear - 81));

        float declRad = declination * Mathf.Deg2Rad;

        // hour angle
        float hourAngle = 15f * (hour - 12f);

        float hourRad = hourAngle * Mathf.Deg2Rad;

        // elevation
        float sinElevation =
            Mathf.Sin(latitude) *
            Mathf.Sin(declRad)
            +
            Mathf.Cos(latitude) *
            Mathf.Cos(declRad) *
            Mathf.Cos(hourRad);

        elevation =
            Mathf.Asin(sinElevation)
            * Mathf.Rad2Deg;

        // azimuth
        float cosAzimuth =
            (
                Mathf.Sin(declRad)
                -
                Mathf.Sin(latitude)
                * sinElevation
            )
            /
            (
                Mathf.Cos(latitude)
                * Mathf.Cos(
                    elevation * Mathf.Deg2Rad
                )
            );

        cosAzimuth =
            Mathf.Clamp(cosAzimuth, -1f, 1f);

        azimuth =
            Mathf.Acos(cosAzimuth)
            * Mathf.Rad2Deg;

        // rano / popołudnie
        if (hour > 12f)
        {
            azimuth = 360f - azimuth;
        }
    }

}