using UnityEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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

        public long managedMemoryBefore;
        public long managedMemoryAfter;
        public long managedMemoryDelta;

        public long unityMemoryBefore;
        public long unityMemoryAfter;
        public long unityMemoryDelta;
        public long peakUnityMemory;

        public int placedPlants;

        public int drawCallsBefore;
        public int drawCallsAfter;

        public int trianglesBefore;
        public int trianglesAfter;

        public int verticesBefore;
        public int verticesAfter;
    }

    [Serializable]
    public class MeasurementData
    {
        public string stageName;
        public double timeMs;
        public int placedPlants;
        public double averageTimePerPlantMs;
        public double managedMemoryBeforeMB;
        public double managedMemoryAfterMB;
        public double managedMemoryDeltaMB;
        public double unityMemoryBeforeMB;
        public double unityMemoryAfterMB;
        public double unityMemoryDeltaMB;
        public double peakUnityMemoryMB;
        public int drawCallsBefore;
        public int drawCallsAfter;
        public int drawCallsDelta;
        public int trianglesBefore;
        public int trianglesAfter;
        public int trianglesDelta;
        public int verticesBefore;
        public int verticesAfter;
        public int verticesDelta;
    }

    [Serializable]
    public class PerformanceReportData
    {
        public List<MeasurementData> measurements = new List<MeasurementData>();
        public double totalTimeMs;
        public string generationDate;
    }

    List<Measurement> measurements =
        new List<Measurement>();

    Stopwatch stopwatch;

    string currentStage;

    long currentManagedMemoryBefore;
    long currentUnityMemoryBefore;

    long peakUnityMemory;

    int placedPlants;

    int drawCallsBefore;
    int trianglesBefore;
    int verticesBefore;

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

        currentManagedMemoryBefore =
            GC.GetTotalMemory(false);

        currentUnityMemoryBefore =
            UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

        peakUnityMemory =
            currentUnityMemoryBefore;

        placedPlants = 0;

        GetRenderingStats(
            out drawCallsBefore,
            out trianglesBefore,
            out verticesBefore
        );

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

        long managedMemoryAfter =
            GC.GetTotalMemory(false);

        long unityMemoryAfter =
            UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

        if (unityMemoryAfter > peakUnityMemory)
        {
            peakUnityMemory = unityMemoryAfter;
        }

        int drawCallsAfter;
        int trianglesAfter;
        int verticesAfter;

        GetRenderingStats(
            out drawCallsAfter,
            out trianglesAfter,
            out verticesAfter
        );

        Measurement measurement = new Measurement();

        measurement.stageName = currentStage;

        measurement.timeMs =
            stopwatch.Elapsed.TotalMilliseconds;

        measurement.managedMemoryBefore =
            currentManagedMemoryBefore;

        measurement.managedMemoryAfter =
            managedMemoryAfter;

        measurement.managedMemoryDelta =
            managedMemoryAfter -
            currentManagedMemoryBefore;

        measurement.unityMemoryBefore =
            currentUnityMemoryBefore;

        measurement.unityMemoryAfter =
            unityMemoryAfter;

        measurement.unityMemoryDelta =
            unityMemoryAfter -
            currentUnityMemoryBefore;

        measurement.peakUnityMemory =
            peakUnityMemory;

        measurement.placedPlants =
            placedPlants;

        measurement.drawCallsBefore =
            drawCallsBefore;

        measurement.drawCallsAfter =
            drawCallsAfter;

        measurement.trianglesBefore =
            trianglesBefore;

        measurement.trianglesAfter =
            trianglesAfter;

        measurement.verticesBefore =
            verticesBefore;

        measurement.verticesAfter =
            verticesAfter;

        measurements.Add(measurement);

        UnityEngine.Debug.Log(
            $"[VegetationProfiler] END: {currentStage} | " +
            $"Time: {measurement.timeMs:F2} ms | " +
            $"Plants: {measurement.placedPlants} | " +
            $"Memory: {BytesToMB(measurement.unityMemoryDelta):F2} MB | " +
            $"Peak: {BytesToMB(measurement.peakUnityMemory):F2} MB | " +
            $"Draw Calls: {GetDrawCallDelta(measurement)}"
        );

        stopwatch = null;
        currentStage = null;

        return measurement;
    }

    public void UpdatePeakMemory()
    {
        long currentMemory =
            UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

        if (currentMemory > peakUnityMemory)
        {
            peakUnityMemory = currentMemory;
        }
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
        report.AppendLine(
            "=============================================================="
        );

        report.AppendLine(
            "             VEGETATION PERFORMANCE REPORT"
        );

        report.AppendLine(
            "=============================================================="
        );

        report.AppendLine("");

        foreach (Measurement measurement in measurements)
        {
            totalTime += measurement.timeMs;

            report.AppendLine(
                $"Stage: {measurement.stageName}"
            );

            report.AppendLine(
                $"Time: {measurement.timeMs:F2} ms"
            );

            report.AppendLine(
                $"Plants placed: {measurement.placedPlants}"
            );

            report.AppendLine(
                $"Average time per plant: {GetAverageTimePerPlant(measurement):F4} ms"
            );

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
                $"Draw Calls before: {measurement.drawCallsBefore}"
            );

            report.AppendLine(
                $"Draw Calls after: {measurement.drawCallsAfter}"
            );

            report.AppendLine(
                $"Draw Calls delta: {GetDrawCallDelta(measurement)}"
            );

            report.AppendLine(
                $"Triangles delta: {GetTriangleDelta(measurement)}"
            );

            report.AppendLine(
                $"Vertices delta: {GetVertexDelta(measurement)}"
            );

            report.AppendLine(
                "--------------------------------------------------------------"
            );
        }

        report.AppendLine(
            $"TOTAL TIME: {totalTime:F2} ms"
        );

        report.AppendLine(
            "=============================================================="
        );

        UnityEngine.Debug.Log(report.ToString());
    }

    public void ExportCSV(string filePath)
    {
        using (StreamWriter writer =
               new StreamWriter(filePath, false))
        {
            writer.WriteLine(
                "Stage," +
                "Time_ms," +
                "Plants_Placed," +
                "Average_Time_Per_Plant_ms," +
                "ManagedMemoryBefore_MB," +
                "ManagedMemoryAfter_MB," +
                "ManagedMemoryDelta_MB," +
                "UnityMemoryBefore_MB," +
                "UnityMemoryAfter_MB," +
                "UnityMemoryDelta_MB," +
                "PeakUnityMemory_MB," +
                "DrawCallsBefore," +
                "DrawCallsAfter," +
                "DrawCallsDelta," +
                "TrianglesBefore," +
                "TrianglesAfter," +
                "TrianglesDelta," +
                "VerticesBefore," +
                "VerticesAfter," +
                "VerticesDelta"
            );

            foreach (Measurement measurement in measurements)
            {
                writer.WriteLine(
                    $"{measurement.stageName}," +
                    $"{measurement.timeMs:F3}," +
                    $"{measurement.placedPlants}," +
                    $"{GetAverageTimePerPlant(measurement):F4}," +
                    $"{BytesToMB(measurement.managedMemoryBefore):F3}," +
                    $"{BytesToMB(measurement.managedMemoryAfter):F3}," +
                    $"{BytesToMB(measurement.managedMemoryDelta):F3}," +
                    $"{BytesToMB(measurement.unityMemoryBefore):F3}," +
                    $"{BytesToMB(measurement.unityMemoryAfter):F3}," +
                    $"{BytesToMB(measurement.unityMemoryDelta):F3}," +
                    $"{BytesToMB(measurement.peakUnityMemory):F3}," +
                    $"{measurement.drawCallsBefore}," +
                    $"{measurement.drawCallsAfter}," +
                    $"{GetDrawCallDelta(measurement)}," +
                    $"{measurement.trianglesBefore}," +
                    $"{measurement.trianglesAfter}," +
                    $"{GetTriangleDelta(measurement)}," +
                    $"{measurement.verticesBefore}," +
                    $"{measurement.verticesAfter}," +
                    $"{GetVertexDelta(measurement)}"
                );
            }
        }

        UnityEngine.Debug.Log(
            $"[VegetationProfiler] CSV exported to:\n{filePath}"
        );
    }

    public void ExportCSVToProject(string fileName)
    {
        string path =
            Path.Combine(
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
        reportData.generationDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        foreach (Measurement measurement in measurements)
        {
            totalTime += measurement.timeMs;

            MeasurementData measurementData = new MeasurementData
            {
                stageName = measurement.stageName,
                timeMs = measurement.timeMs,
                placedPlants = measurement.placedPlants,
                averageTimePerPlantMs = GetAverageTimePerPlant(measurement),
                managedMemoryBeforeMB = BytesToMB(measurement.managedMemoryBefore),
                managedMemoryAfterMB = BytesToMB(measurement.managedMemoryAfter),
                managedMemoryDeltaMB = BytesToMB(measurement.managedMemoryDelta),
                unityMemoryBeforeMB = BytesToMB(measurement.unityMemoryBefore),
                unityMemoryAfterMB = BytesToMB(measurement.unityMemoryAfter),
                unityMemoryDeltaMB = BytesToMB(measurement.unityMemoryDelta),
                peakUnityMemoryMB = BytesToMB(measurement.peakUnityMemory),
                drawCallsBefore = measurement.drawCallsBefore,
                drawCallsAfter = measurement.drawCallsAfter,
                drawCallsDelta = GetDrawCallDelta(measurement),
                trianglesBefore = measurement.trianglesBefore,
                trianglesAfter = measurement.trianglesAfter,
                trianglesDelta = GetTriangleDelta(measurement),
                verticesBefore = measurement.verticesBefore,
                verticesAfter = measurement.verticesAfter,
                verticesDelta = GetVertexDelta(measurement)
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

    int GetDrawCallDelta(Measurement measurement)
    {
        return measurement.drawCallsAfter -
               measurement.drawCallsBefore;
    }

    int GetTriangleDelta(Measurement measurement)
    {
        return measurement.trianglesAfter -
               measurement.trianglesBefore;
    }

    int GetVertexDelta(Measurement measurement)
    {
        return measurement.verticesAfter -
               measurement.verticesBefore;
    }

    double GetAverageTimePerPlant(Measurement measurement)
    {
        if (measurement.placedPlants <= 0)
        {
            return 0;
        }

        return measurement.timeMs /
               measurement.placedPlants;
    }

    double BytesToMB(long bytes)
    {
        return bytes /
               (1024.0 * 1024.0);
    }

    void GetRenderingStats(
        out int drawCalls,
        out int triangles,
        out int vertices)
    {
        drawCalls = 0;
        triangles = 0;
        vertices = 0;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            drawCalls = UnityStats.drawCalls;
            triangles = UnityStats.triangles;
            vertices = UnityStats.vertices;
        }
#endif
    }
}