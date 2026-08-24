using UnityEngine;

public class grass_analysis : MonoBehaviour
{
    [Header("Grass Conditions")]

    [Range(0f, 1f)]
    public float grassMaxHeight = 0.8f;

    [Range(0f, 1f)]
    public float grassMaxSlope = 0.8f;

    [Range(0f, 1f)]
    public float grassMinMoisture = 0.2f;

    public void GenerateGrassMap(
        MapInputData inputData,
        string grassMapFileName,
        bool generatePreview)
    {
        if (inputData == null || !inputData.IsValid)
        {
            Debug.LogWarning(
                "Cannot generate Grass Map. Input data is invalid."
            );

            return;
        }

        Texture2D grassMap =
            map_helper.CreateFloatMap(inputData.resolution);

        int grassPixels = 0;
        int rockPixels = 0;

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

                float moisture =
                    inputData.GetMoisture(
                        x,
                        y,
                        normX,
                        normY
                    );

                bool hasGrass =
                    height <= grassMaxHeight &&
                    slope <= grassMaxSlope &&
                    moisture >= grassMinMoisture;

                float value = hasGrass ? 1f : 0f;

                map_helper.SetGrayscalePixel(
                    grassMap,
                    x,
                    y,
                    value
                );

                if (hasGrass)
                {
                    grassPixels++;
                }
                else
                {
                    rockPixels++;
                }
            }
        );

        grassMap.Apply();

        map_helper.SaveExr(
            grassMap,
            grassMapFileName
        );

        if (generatePreview)
        {
            map_helper.SavePng(
                grassMap,
                grassMapFileName + "_preview"
            );
        }

        float grassPercentage =
            (float)grassPixels /
            (grassPixels + rockPixels) *
            100f;

        Debug.Log(
            $"Grass Map generated. " +
            $"Grass: {grassPercentage:F2}% | " +
            $"Rock: {100f - grassPercentage:F2}%"
        );
    }
}