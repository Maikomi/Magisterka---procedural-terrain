using UnityEngine;
using UnityEngine.Profiling;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.Profiling;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class vegetation_performance_profiler : MonoBehaviour
{
    [Serializable]
    public class Measurement
    {
        public string stageName;

        public double timeMs;
        public double plantsPerSecond;
        public double averageTimePerPlantMs;

        public long managedMemoryBefore;
        public long managedMemoryAfter;
        public long managedMemoryDelta;

        public long unityMemoryBefore;
        public long unityMemoryAfter;
        public long unityMemoryDelta;
        public long peakUnityMemory;

        public int placedPlants;

        // Rendering counters from the most recently completed/rendered frame.
        // They are intentionally stored as sampled frame statistics, not as
        // "cost of the algorithm" statistics.
        public long frameDrawCalls;
        public long frameTriangles;
        public long frameVertices;

        // Scene/vegetation object statistics.
        public int activeGameObjects;
        public int activeRenderers;
        public int meshRenderers;
        public int skinnedMeshRenderers;
        public int uniqueMeshes;
        public int uniqueMaterials;
    }

    [Serializable]
    public class MeasurementData
    {
        public string stageName;

        public double timeMs;
        public int placedPlants;
        public double averageTimePerPlantMs;
        public double plantsPerSecond;

        public double managedMemoryBeforeMB;
        public double managedMemoryAfterMB;
        public double managedMemoryDeltaMB;

        public double unityMemoryBeforeMB;
        public double unityMemoryAfterMB;
        public double unityMemoryDeltaMB;
        public double peakUnityMemoryMB;

        public long frameDrawCalls;
        public long frameTriangles;
        public long frameVertices;

        public int activeGameObjects;
        public int activeRenderers;
        public int meshRenderers;
        public int skinnedMeshRenderers;
        public int uniqueMeshes;
        public int uniqueMaterials;

        public double trianglesPerPlant;
        public double verticesPerPlant;
        public double memoryDeltaPerPlantMB;
    }

    [Serializable]
    public class PerformanceReportData
    {
        public List<MeasurementData> measurements = new List<MeasurementData>();
        public double totalTimeMs;
        public string generationDate;
    }

    [Serializable]
    class SceneRenderingStats
    {
        public int activeGameObjects;
        public int activeRenderers;
        public int meshRenderers;
        public int skinnedMeshRenderers;
        public int uniqueMeshes;
        public int uniqueMaterials;
    }

    private readonly List<Measurement> measurements = new List<Measurement>();

    private Stopwatch stopwatch;
    private string currentStage;

    private long currentManagedMemoryBefore;
    private long currentUnityMemoryBefore;
    private long peakUnityMemory;

    private int placedPlants;

    // ProfilerRecorder values are sampled from completed Unity frames.
    // They are useful for validating rendering cost, but they should not be
    // interpreted as the CPU time of PlacePlants().
    private ProfilerRecorder drawCallsRecorder;
    private ProfilerRecorder trianglesRecorder;
    private ProfilerRecorder verticesRecorder;
    private bool renderingRecordersStarted;

    private void OnEnable()
    {
        StartRenderingRecorders();
    }

    private void OnDisable()
    {
        StopRenderingRecorders();
    }

    private void StartRenderingRecorders()
    {
        StopRenderingRecorders();

#if UNITY_2020_2_OR_NEWER
        try
        {
            drawCallsRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Draw Calls Count",
                1
            );

            trianglesRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Triangles Count",
                1
            );

            verticesRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Render,
                "Vertices Count",
                1
            );

            renderingRecordersStarted = true;
        }
        catch (Exception exception)
        {
            renderingRecordersStarted = false;

            UnityEngine.Debug.LogWarning(
                "[VegetationProfiler] Could not start rendering ProfilerRecorders. " +
                "Rendering counters will be reported as -1. " +
                exception.Message
            );
        }
#endif
    }

    private void StopRenderingRecorders()
    {
#if UNITY_2020_2_OR_NEWER
        if (drawCallsRecorder.Valid)
            drawCallsRecorder.Dispose();

        if (trianglesRecorder.Valid)
            trianglesRecorder.Dispose();

        if (verticesRecorder.Valid)
            verticesRecorder.Dispose();
#endif

        renderingRecordersStarted = false;
    }

    public void StartStage(string stageName)
    {
        if (stopwatch != null && stopwatch.IsRunning)
        {
            UnityEngine.Debug.LogWarning(
                $"[VegetationProfiler] Previous stage '{currentStage}' was not stopped."
            );

            StopStage();
        }

        currentStage = stageName;

        currentManagedMemoryBefore = GC.GetTotalMemory(false);
        currentUnityMemoryBefore = Profiler.GetTotalAllocatedMemoryLong();
        peakUnityMemory = currentUnityMemoryBefore;

        placedPlants = 0;

        stopwatch = Stopwatch.StartNew();

        UnityEngine.Debug.Log(
            $"[VegetationProfiler] START: {stageName}"
        );
    }

    public void RegisterPlacedPlant()
    {
        placedPlants++;
        UpdatePeakMemory();
    }

    public Measurement StopStage()
    {
        if (stopwatch == null || !stopwatch.IsRunning)
        {
            UnityEngine.Debug.LogWarning(
                "[VegetationProfiler] StopStage() called without active stage."
            );

            return null;
        }

        UpdatePeakMemory();
        stopwatch.Stop();

        long managedMemoryAfter = GC.GetTotalMemory(false);
        long unityMemoryAfter = Profiler.GetTotalAllocatedMemoryLong();

        if (unityMemoryAfter > peakUnityMemory)
            peakUnityMemory = unityMemoryAfter;

        GetLatestRenderingStats(
            out long frameDrawCalls,
            out long frameTriangles,
            out long frameVertices
        );

        SceneRenderingStats sceneStats = GetSceneRenderingStats();

        Measurement measurement = new Measurement();

        measurement.stageName = currentStage;
        measurement.timeMs = stopwatch.Elapsed.TotalMilliseconds;
        measurement.placedPlants = placedPlants;

        measurement.averageTimePerPlantMs =
            GetAverageTimePerPlant(placedPlants, measurement.timeMs);

        measurement.plantsPerSecond =
            GetPlantsPerSecond(placedPlants, measurement.timeMs);

        measurement.managedMemoryBefore = currentManagedMemoryBefore;
        measurement.managedMemoryAfter = managedMemoryAfter;
        measurement.managedMemoryDelta =
            managedMemoryAfter - currentManagedMemoryBefore;

        measurement.unityMemoryBefore = currentUnityMemoryBefore;
        measurement.unityMemoryAfter = unityMemoryAfter;
        measurement.unityMemoryDelta =
            unityMemoryAfter - currentUnityMemoryBefore;
        measurement.peakUnityMemory = peakUnityMemory;

        measurement.frameDrawCalls = frameDrawCalls;
        measurement.frameTriangles = frameTriangles;
        measurement.frameVertices = frameVertices;

        measurement.activeGameObjects = sceneStats.activeGameObjects;
        measurement.activeRenderers = sceneStats.activeRenderers;
        measurement.meshRenderers = sceneStats.meshRenderers;
        measurement.skinnedMeshRenderers = sceneStats.skinnedMeshRenderers;
        measurement.uniqueMeshes = sceneStats.uniqueMeshes;
        measurement.uniqueMaterials = sceneStats.uniqueMaterials;

        measurements.Add(measurement);

        UnityEngine.Debug.Log(
            $"[VegetationProfiler] END: {currentStage} | " +
            $"Time: {measurement.timeMs:F2} ms | " +
            $"Plants: {measurement.placedPlants} | " +
            $"Plants/s: {measurement.plantsPerSecond:F2} | " +
            $"Unity memory delta: {BytesToMB(measurement.unityMemoryDelta):F2} MB | " +
            $"Peak: {BytesToMB(measurement.peakUnityMemory):F2} MB | " +
            $"Frame Draw Calls: {FormatCounter(measurement.frameDrawCalls)} | " +
            $"Frame Triangles: {FormatCounter(measurement.frameTriangles)} | " +
            $"Renderers: {measurement.activeRenderers} | " +
            $"Unique Meshes: {measurement.uniqueMeshes} | " +
            $"Unique Materials: {measurement.uniqueMaterials}"
        );

        stopwatch = null;
        currentStage = null;

        return measurement;
    }

    public void UpdatePeakMemory()
    {
        long currentMemory = Profiler.GetTotalAllocatedMemoryLong();

        if (currentMemory > peakUnityMemory)
            peakUnityMemory = currentMemory;
    }

    public List<Measurement> GetMeasurements()
    {
        return measurements;
    }

    public void Clear()
    {
        measurements.Clear();

        stopwatch = null;
        currentStage = null;

        placedPlants = 0;
        peakUnityMemory = 0;
    }

    public void PrintReport()
    {
        if (measurements.Count == 0)
        {
            UnityEngine.Debug.Log(
                "[VegetationProfiler] No measurements available."
            );

            return;
        }

        double totalTime = 0;
        System.Text.StringBuilder report =
            new System.Text.StringBuilder();

        report.AppendLine("");
        report.AppendLine("==============================================================");
        report.AppendLine("             VEGETATION PERFORMANCE REPORT");
        report.AppendLine("==============================================================");
        report.AppendLine("");

        foreach (Measurement measurement in measurements)
        {
            totalTime += measurement.timeMs;

            report.AppendLine($"Stage: {measurement.stageName}");
            report.AppendLine($"Time: {measurement.timeMs:F2} ms");
            report.AppendLine($"Plants placed: {measurement.placedPlants}");
            report.AppendLine($"Average time per plant: {measurement.averageTimePerPlantMs:F4} ms");
            report.AppendLine($"Plants per second: {measurement.plantsPerSecond:F2}");

            report.AppendLine(
                $"Managed memory delta: {BytesToMB(measurement.managedMemoryDelta):F2} MB"
            );

            report.AppendLine(
                $"Unity memory delta: {BytesToMB(measurement.unityMemoryDelta):F2} MB"
            );

            report.AppendLine(
                $"Peak Unity memory: {BytesToMB(measurement.peakUnityMemory):F2} MB"
            );

            report.AppendLine(
                $"Frame Draw Calls: {FormatCounter(measurement.frameDrawCalls)}"
            );

            report.AppendLine(
                $"Frame Triangles: {FormatCounter(measurement.frameTriangles)}"
            );

            report.AppendLine(
                $"Frame Vertices: {FormatCounter(measurement.frameVertices)}"
            );

            report.AppendLine(
                $"Active GameObjects: {measurement.activeGameObjects}"
            );

            report.AppendLine(
                $"Active Renderers: {measurement.activeRenderers}"
            );

            report.AppendLine(
                $"Mesh Renderers: {measurement.meshRenderers}"
            );

            report.AppendLine(
                $"Skinned Mesh Renderers: {measurement.skinnedMeshRenderers}"
            );

            report.AppendLine(
                $"Unique Meshes: {measurement.uniqueMeshes}"
            );

            report.AppendLine(
                $"Unique Materials: {measurement.uniqueMaterials}"
            );

            report.AppendLine(
                $"Triangles per plant: {GetTrianglesPerPlant(measurement):F2}"
            );

            report.AppendLine(
                $"Vertices per plant: {GetVerticesPerPlant(measurement):F2}"
            );

            report.AppendLine(
                $"Memory delta per plant: {GetMemoryPerPlantMB(measurement):F4} MB"
            );

            report.AppendLine("--------------------------------------------------------------");
        }

        report.AppendLine($"TOTAL TIME: {totalTime:F2} ms");
        report.AppendLine("==============================================================");

        UnityEngine.Debug.Log(report.ToString());
    }

    public void ExportCSV(string filePath)
    {
        using (StreamWriter writer = new StreamWriter(filePath, false))
        {
            writer.WriteLine(
                "Stage," +
                "Time_ms," +
                "Plants_Placed," +
                "Average_Time_Per_Plant_ms," +
                "Plants_Per_Second," +
                "ManagedMemoryBefore_MB," +
                "ManagedMemoryAfter_MB," +
                "ManagedMemoryDelta_MB," +
                "UnityMemoryBefore_MB," +
                "UnityMemoryAfter_MB," +
                "UnityMemoryDelta_MB," +
                "PeakUnityMemory_MB," +
                "FrameDrawCalls," +
                "FrameTriangles," +
                "FrameVertices," +
                "ActiveGameObjects," +
                "ActiveRenderers," +
                "MeshRenderers," +
                "SkinnedMeshRenderers," +
                "UniqueMeshes," +
                "UniqueMaterials," +
                "TrianglesPerPlant," +
                "VerticesPerPlant," +
                "MemoryDeltaPerPlant_MB"
            );

            foreach (Measurement measurement in measurements)
            {
                writer.WriteLine(
                    $"{EscapeCSV(measurement.stageName)}," +
                    $"{measurement.timeMs:F3}," +
                    $"{measurement.placedPlants}," +
                    $"{measurement.averageTimePerPlantMs:F4}," +
                    $"{measurement.plantsPerSecond:F4}," +
                    $"{BytesToMB(measurement.managedMemoryBefore):F3}," +
                    $"{BytesToMB(measurement.managedMemoryAfter):F3}," +
                    $"{BytesToMB(measurement.managedMemoryDelta):F3}," +
                    $"{BytesToMB(measurement.unityMemoryBefore):F3}," +
                    $"{BytesToMB(measurement.unityMemoryAfter):F3}," +
                    $"{BytesToMB(measurement.unityMemoryDelta):F3}," +
                    $"{BytesToMB(measurement.peakUnityMemory):F3}," +
                    $"{measurement.frameDrawCalls}," +
                    $"{measurement.frameTriangles}," +
                    $"{measurement.frameVertices}," +
                    $"{measurement.activeGameObjects}," +
                    $"{measurement.activeRenderers}," +
                    $"{measurement.meshRenderers}," +
                    $"{measurement.skinnedMeshRenderers}," +
                    $"{measurement.uniqueMeshes}," +
                    $"{measurement.uniqueMaterials}," +
                    $"{GetTrianglesPerPlant(measurement):F4}," +
                    $"{GetVerticesPerPlant(measurement):F4}," +
                    $"{GetMemoryPerPlantMB(measurement):F6}"
                );
            }
        }

        UnityEngine.Debug.Log(
            $"[VegetationProfiler] CSV exported to:\n{filePath}"
        );
    }

    public void ExportCSVToProject(string fileName)
    {
        string path = Path.Combine(
            Application.dataPath,
            "..",
            fileName
        );

        path = Path.GetFullPath(path);
        ExportCSV(path);

#if UNITY_EDITOR
        AssetDatabase.Refresh();
#endif
    }

    public void ExportJSON(string filePath)
    {
        if (measurements.Count == 0)
        {
            UnityEngine.Debug.LogWarning(
                "[VegetationProfiler] No measurements available for JSON export."
            );
            return;
        }

        double totalTime = 0;
        PerformanceReportData reportData = new PerformanceReportData();
        reportData.generationDate =
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (Measurement measurement in measurements)
        {
            totalTime += measurement.timeMs;

            MeasurementData measurementData = new MeasurementData
            {
                stageName = measurement.stageName,
                timeMs = measurement.timeMs,
                placedPlants = measurement.placedPlants,
                averageTimePerPlantMs = measurement.averageTimePerPlantMs,
                plantsPerSecond = measurement.plantsPerSecond,

                managedMemoryBeforeMB = BytesToMB(measurement.managedMemoryBefore),
                managedMemoryAfterMB = BytesToMB(measurement.managedMemoryAfter),
                managedMemoryDeltaMB = BytesToMB(measurement.managedMemoryDelta),

                unityMemoryBeforeMB = BytesToMB(measurement.unityMemoryBefore),
                unityMemoryAfterMB = BytesToMB(measurement.unityMemoryAfter),
                unityMemoryDeltaMB = BytesToMB(measurement.unityMemoryDelta),
                peakUnityMemoryMB = BytesToMB(measurement.peakUnityMemory),

                frameDrawCalls = measurement.frameDrawCalls,
                frameTriangles = measurement.frameTriangles,
                frameVertices = measurement.frameVertices,

                activeGameObjects = measurement.activeGameObjects,
                activeRenderers = measurement.activeRenderers,
                meshRenderers = measurement.meshRenderers,
                skinnedMeshRenderers = measurement.skinnedMeshRenderers,
                uniqueMeshes = measurement.uniqueMeshes,
                uniqueMaterials = measurement.uniqueMaterials,

                trianglesPerPlant = GetTrianglesPerPlant(measurement),
                verticesPerPlant = GetVerticesPerPlant(measurement),
                memoryDeltaPerPlantMB = GetMemoryPerPlantMB(measurement)
            };

            reportData.measurements.Add(measurementData);
        }

        reportData.totalTimeMs = totalTime;

        string json = JsonUtility.ToJson(reportData, true);
        File.WriteAllText(filePath, json);

        UnityEngine.Debug.Log(
            $"[VegetationProfiler] JSON exported to:\n{filePath}"
        );
    }

    private void GetLatestRenderingStats(
        out long drawCalls,
        out long triangles,
        out long vertices)
    {
        drawCalls = -1;
        triangles = -1;
        vertices = -1;

#if UNITY_2020_2_OR_NEWER
        if (!renderingRecordersStarted)
            return;

        drawCalls = GetRecorderValue(drawCallsRecorder);
        triangles = GetRecorderValue(trianglesRecorder);
        vertices = GetRecorderValue(verticesRecorder);
#endif
    }

    private long GetRecorderValue(ProfilerRecorder recorder)
    {
#if UNITY_2020_2_OR_NEWER
        if (!recorder.Valid || recorder.Count == 0)
            return -1;

        return recorder.LastValue;
#else
        return -1;
#endif
    }

    private SceneRenderingStats GetSceneRenderingStats()
    {
        SceneRenderingStats stats = new SceneRenderingStats();

        HashSet<Mesh> meshes = new HashSet<Mesh>();
        HashSet<Material> materials = new HashSet<Material>();

        Renderer[] renderers = FindObjectsOfType<Renderer>(true);
        stats.activeRenderers = 0;

        HashSet<GameObject> gameObjects = new HashSet<GameObject>();

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy)
                continue;

            stats.activeRenderers++;
            gameObjects.Add(renderer.gameObject);

            MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                meshes.Add(meshFilter.sharedMesh);
                stats.meshRenderers++;
            }

            SkinnedMeshRenderer skinnedRenderer =
                renderer as SkinnedMeshRenderer;

            if (skinnedRenderer != null)
            {
                if (skinnedRenderer.sharedMesh != null)
                    meshes.Add(skinnedRenderer.sharedMesh);

                stats.skinnedMeshRenderers++;
            }

            Material[] sharedMaterials = renderer.sharedMaterials;
            foreach (Material material in sharedMaterials)
            {
                if (material != null)
                    materials.Add(material);
            }
        }

        stats.activeGameObjects = gameObjects.Count;
        stats.uniqueMeshes = meshes.Count;
        stats.uniqueMaterials = materials.Count;

        return stats;
    }

    private double GetAverageTimePerPlant(int plantCount, double time)
    {
        if (plantCount <= 0)
            return 0;

        return time / plantCount;
    }

    private double GetPlantsPerSecond(int plantCount, double timeMs)
    {
        if (plantCount <= 0 || timeMs <= 0)
            return 0;

        return plantCount / (timeMs / 1000.0);
    }

    private double GetTrianglesPerPlant(Measurement measurement)
    {
        if (measurement.placedPlants <= 0 || measurement.frameTriangles < 0)
            return -1;

        return (double)measurement.frameTriangles /
               measurement.placedPlants;
    }

    private double GetVerticesPerPlant(Measurement measurement)
    {
        if (measurement.placedPlants <= 0 || measurement.frameVertices < 0)
            return -1;

        return (double)measurement.frameVertices /
               measurement.placedPlants;
    }

    private double GetMemoryPerPlantMB(Measurement measurement)
    {
        if (measurement.placedPlants <= 0)
            return 0;

        return BytesToMB(measurement.unityMemoryDelta) /
               measurement.placedPlants;
    }

    private string FormatCounter(long value)
    {
        return value < 0 ? "N/A" : value.ToString();
    }

    private string EscapeCSV(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(",") || value.Contains("\"") ||
            value.Contains("\n") || value.Contains("\r"))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    private double BytesToMB(long bytes)
    {
        return bytes / (1024.0 * 1024.0);
    }
}
