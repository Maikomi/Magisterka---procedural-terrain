using UnityEngine;
using System.IO;

public class DailySolarExposure : MonoBehaviour
{
    public Terrain terrain;

    [Header("Maps To Generate")]
    public bool generateDailySolarExposure = true;
    public bool generateAnnualSolarExposure = true;

    [Header("Location")]
    public float latitude = 45f;

    [Header("Time")]
    public int dayOfYear = 15;
    public int hourStart = 5;
    public int hourEnd = 21;
    public float hourStep = 1f;

    [Header("Annual Sampling")]
    public int annualSamples = 12;

    [Header("Raymarch")]
    public int maxDistanceSamples = 32;

    void Start()
    {
        if (!generateDailySolarExposure && !generateAnnualSolarExposure)
        {
            Debug.Log("Solar exposure map generation is disabled.");
            return;
        }

        if (terrain == null)
        {
            terrain = Terrain.activeTerrain;
        }

        if (terrain == null)
        {
            Debug.LogWarning("No terrain found for solar exposure generation.");
            return;
        }

        TerrainData data = terrain.terrainData;

        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);

        float terrainWidth = data.size.x;
        float terrainHeight = data.size.y;
        float metersPerPixel = terrainWidth / res;

        Directory.CreateDirectory(Application.dataPath + "/maps");

        if (generateDailySolarExposure)
        {
            Texture2D dailyExposureMap = GenerateSolarExposureMap(
                dayOfYear,
                heights,
                terrainHeight,
                metersPerPixel,
                res
            );

            SaveExposureMap(dailyExposureMap, $"daily_{dayOfYear}_solar_exposure");

            Debug.Log($"Solar exposure map for {dayOfYear} saved!");
        }

        if (generateAnnualSolarExposure)
        {
            int annualSampleCount = GetAnnualSampleCount();
            Texture2D annualExposureMap = GenerateAnnualSolarExposureMap(
                heights,
                terrainHeight,
                metersPerPixel,
                res,
                annualSampleCount
            );

            SaveExposureMap(annualExposureMap, $"AnnualSolarExposure_{annualSampleCount}");

            Debug.Log($"Annual solar exposure map saved for {annualSampleCount} samples!");
        }
    }

    Texture2D GenerateSolarExposureMap(
        int targetDayOfYear,
        float[,] heights,
        float terrainHeight,
        float metersPerPixel,
        int res
    )
    {
        Texture2D exposureMap = new Texture2D(res, res, TextureFormat.RGBAFloat, false);

        float maxPossibleLight = CalculateMaxPossibleLight(targetDayOfYear);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float totalLight = CalculateExposureForPixel(
                    x,
                    y,
                    targetDayOfYear,
                    heights,
                    terrainHeight,
                    metersPerPixel,
                    res
                );

                float normalized = maxPossibleLight > 0f ? totalLight / maxPossibleLight : 0f;
                normalized = NormalizeExposure(normalized);

                exposureMap.SetPixel(x, y, new Color(normalized, normalized, normalized, 1f));
            }
        }

        exposureMap.Apply();
        return exposureMap;
    }

    Texture2D GenerateAnnualSolarExposureMap(
        float[,] heights,
        float terrainHeight,
        float metersPerPixel,
        int res,
        int samples
    )
    {
        int dayStep = Mathf.Max(1, Mathf.FloorToInt(365f / samples));
        float[,] exposureTotals = new float[res, res];

        for (int sample = 0; sample < samples; sample++)
        {
            int sampledDay = Mathf.Clamp(1 + sample * dayStep, 1, 365);
            float maxPossibleLight = CalculateMaxPossibleLight(sampledDay);

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float totalLight = CalculateExposureForPixel(
                        x,
                        y,
                        sampledDay,
                        heights,
                        terrainHeight,
                        metersPerPixel,
                        res
                    );

                    float normalized = maxPossibleLight > 0f ? totalLight / maxPossibleLight : 0f;
                    normalized = NormalizeExposure(normalized);
                    exposureTotals[y, x] += normalized;
                }
            }
        }

        Texture2D annualExposureMap = new Texture2D(res, res, TextureFormat.RGBAFloat, false);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float averaged = exposureTotals[y, x] / samples;
                annualExposureMap.SetPixel(x, y, new Color(averaged, averaged, averaged, 1f));
            }
        }

        annualExposureMap.Apply();
        return annualExposureMap;
    }

    int GetAnnualSampleCount()
    {
        int samples = Mathf.Clamp(annualSamples, 1, 365);

        if (samples != annualSamples)
        {
            Debug.LogWarning($"annualSamples should be between 1 and 365. Using {samples}.");
        }

        return samples;
    }

    float NormalizeExposure(float exposure)
    {
        return Mathf.Pow(Mathf.Clamp01(exposure), 0.25f);
    }

    void SaveExposureMap(Texture2D exposureMap, string fileName)
    {
        byte[] bytes = exposureMap.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
        File.WriteAllBytes(Application.dataPath + $"/maps/{fileName}.exr", bytes);
    }

    float CalculateMaxPossibleLight(int targetDayOfYear)
    {
        float maxPossibleLight = 0f;

        for (float hour = hourStart; hour <= hourEnd; hour += hourStep)
        {
            GetSunPosition(latitude, targetDayOfYear, hour, out float azimuthDeg, out float elevationDeg);

            if (elevationDeg <= 0f)
                continue;

            maxPossibleLight += Mathf.Sin(elevationDeg * Mathf.Deg2Rad);
        }

        return maxPossibleLight;
    }

    float CalculateExposureForPixel(
        int x,
        int y,
        int targetDayOfYear,
        float[,] heights,
        float terrainHeight,
        float metersPerPixel,
        int res
    )
    {
        float totalLight = 0f;
        float currentHeight = heights[y, x] * terrainHeight;

        for (float hour = hourStart; hour <= hourEnd; hour += hourStep)
        {
            GetSunPosition(latitude, targetDayOfYear, hour, out float azimuthDeg, out float elevationDeg);

            if (elevationDeg <= 0f)
                continue;

            float intensity = Mathf.Sin(elevationDeg * Mathf.Deg2Rad);
            float azimuthRad = azimuthDeg * Mathf.Deg2Rad;

            float dirX = Mathf.Sin(azimuthRad);
            float dirY = Mathf.Cos(azimuthRad);

            if (!IsBlockedByTerrain(x, y, dirX, dirY, elevationDeg, currentHeight, heights, terrainHeight, metersPerPixel, res))
            {
                totalLight += intensity;
            }
        }

        return totalLight;
    }

    bool IsBlockedByTerrain(
        int x,
        int y,
        float dirX,
        float dirY,
        float elevationDeg,
        float currentHeight,
        float[,] heights,
        float terrainHeight,
        float metersPerPixel,
        int res
    )
    {
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

            if (terrainAngle > elevationDeg)
            {
                return true;
            }
        }

        return false;
    }

    void GetSunPosition(
        float latitudeDeg,
        int dayOfYear,
        float hour,
        out float azimuth,
        out float elevation
    )
    {
        float latitude = latitudeDeg * Mathf.Deg2Rad;

        float declination = 23.45f * Mathf.Sin(Mathf.Deg2Rad * (360f / 365f) * (dayOfYear - 81));
        float declRad = declination * Mathf.Deg2Rad;

        float hourAngle = 15f * (hour - 12f);
        float hourRad = hourAngle * Mathf.Deg2Rad;

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

        if (hour > 12f)
        {
            azimuth = 360f - azimuth;
        }
    }
}
