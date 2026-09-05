using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class SimDataLoader : MonoBehaviour
{
    public static SimDataLoader Instance { get; private set; }

    [Header("CSV File Settings")]
    public string fileName = "sim_data.csv";

    [Header("Parsed Telemetry Lists")]
    public List<float> timeSeconds = new List<float>();
    public List<float> timeHours = new List<float>();
    public List<float> milkProposed = new List<float>();
    public List<float> milkBare = new List<float>();
    public List<float> pcmTemp = new List<float>();
    public List<float> pcmPhaseFrac = new List<float>();
    public List<float> pcmHeatRemovalW = new List<float>();
    public List<float> ambientHeatInfluxW = new List<float>();
    public List<float> pcmRemainingCapacityKJ = new List<float>();
    public List<float> pcmAvailableCapacityKJ = new List<float>();
    public List<float> ambientTemp = new List<float>();
    public List<string> coolingStatus = new List<string>();
    public List<string> modelStatus = new List<string>();

    public bool IsDataLoaded { get; private set; } = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        LoadCSV();
    }

    public void LoadCSV()
    {
        ClearAllLists();

        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SimDataLoader] CSV file not found at: {path}. Please place '{fileName}' inside Assets/StreamingAssets/");
            IsDataLoaded = false;
            return;
        }

        try
        {
            string[] lines = File.ReadAllLines(path);
            if (lines.Length <= 1)
            {
                Debug.LogWarning($"[SimDataLoader] CSV file at {path} is empty or has only header.");
                IsDataLoaded = false;
                return;
            }

            // Parse Header to dynamically map column indices
            string[] headers = lines[0].Split(',');
            Dictionary<string, int> colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headers.Length; c++)
            {
                string key = headers[c].Trim();
                if (!colMap.ContainsKey(key))
                {
                    colMap.Add(key, c);
                }
            }

            // Column Index Helper
            int GetCol(string name, int defaultIdx) => colMap.TryGetValue(name, out int idx) ? idx : defaultIdx;

            int idxTimeS = GetCol("Time_s", 0);
            int idxTimeHr = GetCol("Time_hr", GetCol("time_h", 2));
            int idxMilkProp = GetCol("Milk_Temp_C", GetCol("milk_temp_proposed", 4));
            int idxMilkBare = GetCol("Milk_Temp_Bare_C", GetCol("milk_temp_bare", -1));
            int idxPcmTemp = GetCol("PCM_Temp_C", GetCol("pcm_temp", 5));
            int idxPhaseFrac = GetCol("PCM_Phase_Fraction", GetCol("pcm_phase_frac", 6));
            int idxPcmHeatRem = GetCol("PCM_Heat_Removal_W", 7);
            int idxAmbInflux = GetCol("Ambient_Heat_Influx_W", GetCol("heat_influx_w", 8));
            int idxPcmRemaining = GetCol("PCM_Remaining_Capacity_kJ", GetCol("pcm_capacity_kj", 11));
            int idxPcmAvailable = GetCol("PCM_Available_Capacity_kJ", 10);
            int idxAmbientTemp = GetCol("Ambient_Temp_C", GetCol("ambient_temp", 3));
            int idxCoolingStat = GetCol("Cooling_Status", -1);
            int idxModelStat = GetCol("Model_Status", 18);

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] cols = line.Split(',');
                if (cols.Length < 3) continue;

                float tSec = ParseFloat(cols, idxTimeS, 0f);
                float tHr = colMap.ContainsKey("Time_hr") || colMap.ContainsKey("time_h") ? ParseFloat(cols, idxTimeHr, tSec / 3600f) : tSec / 3600f;
                float milkP = ParseFloat(cols, idxMilkProp, 30f);
                float pcmT = ParseFloat(cols, idxPcmTemp, 2f);
                float phaseF = ParseFloat(cols, idxPhaseFrac, 0f);
                float pcmHeatW = ParseFloat(cols, idxPcmHeatRem, 0f);
                float ambInfluxW = ParseFloat(cols, idxAmbInflux, 0f);
                float pcmRemKJ = ParseFloat(cols, idxPcmRemaining, 0f);
                float pcmAvailKJ = ParseFloat(cols, idxPcmAvailable, 768f);
                float ambT = ParseFloat(cols, idxAmbientTemp, 35f);

                string coolStat = idxCoolingStat >= 0 && idxCoolingStat < cols.Length ? cols[idxCoolingStat].Trim() : "NOMINAL";
                string modelStat = idxModelStat >= 0 && idxModelStat < cols.Length ? cols[idxModelStat].Trim() : "PCM CAPACITY INSUFFICIENT";

                // Bare Can Temperature: If explicitly present in CSV use it, otherwise model uninsulated transient warming
                float milkB;
                if (idxMilkBare >= 0 && idxMilkBare < cols.Length)
                {
                    milkB = ParseFloat(cols, idxMilkBare, 30f);
                }
                else
                {
                    // Lumped thermal model for uninsulated bare can warming to ambient (35°C)
                    // T_bare(t) = T_amb - (T_amb - T_init) * exp(-k * t)
                    float k = 0.0012f; // Bare uninsulated thermal constant (~30°C to 34°C within 1-2 hours)
                    milkB = ambT - (ambT - 30.0f) * Mathf.Exp(-k * tSec);
                }

                timeSeconds.Add(tSec);
                timeHours.Add(tHr);
                milkProposed.Add(milkP);
                milkBare.Add(milkB);
                pcmTemp.Add(pcmT);
                pcmPhaseFrac.Add(phaseF);
                pcmHeatRemovalW.Add(pcmHeatW);
                ambientHeatInfluxW.Add(ambInfluxW);
                pcmRemainingCapacityKJ.Add(pcmRemKJ);
                pcmAvailableCapacityKJ.Add(pcmAvailKJ);
                ambientTemp.Add(ambT);
                coolingStatus.Add(coolStat);
                modelStatus.Add(modelStat);
            }

            IsDataLoaded = timeHours.Count > 0;
            Debug.Log($"[SimDataLoader] Successfully parsed {timeHours.Count} data points from {fileName}. Duration: {timeHours[timeHours.Count - 1]:F2} hours ({timeSeconds[timeSeconds.Count - 1]:F0} s).");

            if (SimClock.Instance != null && IsDataLoaded)
            {
                SimClock.Instance.simDurationHours = timeHours[timeHours.Count - 1];
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SimDataLoader] Error reading CSV {path}: {ex.Message}");
            IsDataLoaded = false;
        }
    }

    private float ParseFloat(string[] cols, int idx, float fallback)
    {
        if (idx >= 0 && idx < cols.Length)
        {
            if (float.TryParse(cols[idx].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }
        }
        return fallback;
    }

    private void ClearAllLists()
    {
        timeSeconds.Clear();
        timeHours.Clear();
        milkProposed.Clear();
        milkBare.Clear();
        pcmTemp.Clear();
        pcmPhaseFrac.Clear();
        pcmHeatRemovalW.Clear();
        ambientHeatInfluxW.Clear();
        pcmRemainingCapacityKJ.Clear();
        pcmAvailableCapacityKJ.Clear();
        ambientTemp.Clear();
        coolingStatus.Clear();
        modelStatus.Clear();
    }

    public float GetValueAt(List<float> series, float tHours)
    {
        if (series == null || series.Count == 0 || timeHours.Count == 0) return 0f;
        if (tHours <= timeHours[0]) return series[0];
        if (tHours >= timeHours[timeHours.Count - 1]) return series[series.Count - 1];

        for (int i = 0; i < timeHours.Count - 1; i++)
        {
            if (tHours >= timeHours[i] && tHours <= timeHours[i + 1])
            {
                float timeSpan = timeHours[i + 1] - timeHours[i];
                if (Mathf.Approximately(timeSpan, 0f)) return series[i];

                float frac = (tHours - timeHours[i]) / timeSpan;
                return Mathf.Lerp(series[i], series[i + 1], frac);
            }
        }

        return series[series.Count - 1];
    }

    public string GetCoolingStatusAt(float tHours)
    {
        return GetStringAt(coolingStatus, tHours, "NOMINAL");
    }

    public string GetModelStatusAt(float tHours)
    {
        return GetStringAt(modelStatus, tHours, "PCM CAPACITY INSUFFICIENT");
    }

    private string GetStringAt(List<string> list, float tHours, string fallback)
    {
        if (list == null || list.Count == 0 || timeHours.Count == 0) return fallback;
        if (tHours <= timeHours[0]) return list[0];
        if (tHours >= timeHours[timeHours.Count - 1]) return list[list.Count - 1];

        for (int i = 0; i < timeHours.Count - 1; i++)
        {
            if (tHours >= timeHours[i] && tHours <= timeHours[i + 1])
            {
                return list[i];
            }
        }

        return list[list.Count - 1];
    }

    // Backward compatibility alias
    public float GetTempAt(List<float> series, float tHours) => GetValueAt(series, tHours);

    public float GetLayerTempAt(CanVisualizer.CanType type, float tHours)
    {
        float tMilk = GetValueAt(milkProposed, tHours);
        float tPcm = GetValueAt(pcmTemp, tHours);
        float tAmb = GetValueAt(ambientTemp, tHours);
        float qPcm = GetValueAt(pcmHeatRemovalW, tHours);

        switch (type)
        {
            case CanVisualizer.CanType.Bare:
                return GetValueAt(milkBare, tHours);
            case CanVisualizer.CanType.Proposed:
                return tMilk;
            case CanVisualizer.CanType.PCM:
                return tPcm;
            case CanVisualizer.CanType.InnerLiner:
                float deltaTLiner = qPcm / (30.0f * 0.85f);
                return Mathf.Max(tPcm, tMilk - deltaTLiner);
            case CanVisualizer.CanType.Insulation:
                return tPcm + (tAmb - tPcm) * 0.5f;
            case CanVisualizer.CanType.OuterShell:
                return Mathf.Max(tPcm, tAmb - 0.2f);
            default:
                return tMilk;
        }
    }
}
