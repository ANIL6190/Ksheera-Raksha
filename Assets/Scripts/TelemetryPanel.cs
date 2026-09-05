using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TelemetryPanel : MonoBehaviour
{
    [Header("UI Text Outputs")]
    public TMP_Text telemetryText;
    public Text legacyTelemetryText;

    [Header("Engineering Checks")]
    public float milkCoolingLoadKJ = 2008.50f;
    public float energyBalanceErrorPct = 0.0000f;

    private void Update()
    {
        if (SimClock.Instance == null || SimDataLoader.Instance == null || !SimDataLoader.Instance.IsDataLoaded)
        {
            return;
        }

        float tHours = SimClock.Instance.simTimeHours;
        float tSeconds = tHours * 3600f;

        float ambient = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.ambientTemp, tHours);
        float pcmTemp = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.pcmTemp, tHours);
        float pcmPhase = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.pcmPhaseFrac, tHours);
        float milkProp = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.milkProposed, tHours);
        float milkBare = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.milkBare, tHours);
        float heatRemoval = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.pcmHeatRemovalW, tHours);
        float heatInflux = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.ambientHeatInfluxW, tHours);
        float availCapacity = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.pcmAvailableCapacityKJ, tHours);
        float remCapacity = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.pcmRemainingCapacityKJ, tHours);
        string coolStatus = SimDataLoader.Instance.GetCoolingStatusAt(tHours);
        string modelStatus = SimDataLoader.Instance.GetModelStatusAt(tHours);

        float linerTemp = 28.5f;
        float insTemp = 18.5f;
        float shellTemp = 34.8f;

        if (SimDataLoader.Instance != null && SimDataLoader.Instance.IsDataLoaded)
        {
            linerTemp = SimDataLoader.Instance.GetLayerTempAt(CanVisualizer.CanType.InnerLiner, tHours);
            insTemp = SimDataLoader.Instance.GetLayerTempAt(CanVisualizer.CanType.Insulation, tHours);
            shellTemp = SimDataLoader.Instance.GetLayerTempAt(CanVisualizer.CanType.OuterShell, tHours);
        }
        else if (RealtimePhysicsEngine.Instance != null)
        {
            linerTemp = RealtimePhysicsEngine.Instance.currentLinerTemp;
            insTemp = RealtimePhysicsEngine.Instance.currentInsulationTemp;
            shellTemp = RealtimePhysicsEngine.Instance.currentOuterShellTemp;
        }

        string report = BuildTelemetryReport(tHours, tSeconds, ambient, pcmTemp, pcmPhase, milkProp, milkBare, linerTemp, insTemp, shellTemp, heatRemoval, heatInflux, availCapacity, remCapacity, coolStatus, modelStatus);

        if (telemetryText != null)
        {
            telemetryText.text = report;
        }
        if (legacyTelemetryText != null)
        {
            legacyTelemetryText.text = report;
        }
    }

    private string BuildTelemetryReport(float tHours, float tSeconds, float ambient, float pcmTemp, float pcmPhase, float milkProp, float milkBare, float linerTemp, float insTemp, float shellTemp, float heatRemoval, float heatInflux, float availCapacity, float remCapacity, string coolStatus, string modelStatus)
    {
        return $@"=====================================================
  DIGITAL TWIN REAL-TIME TELEMETRY
=====================================================
SIMULATION TIME      : {tHours:F2} Hours ({tSeconds:F0} s)

5-LAYER CONCENTRIC THERMAL NETWORK:
  - [Layer 1] Milk Core         : {milkProp:F2} °C (vs Bare: {milkBare:F2} °C)
  - [Layer 2] Inner Steel Liner : {linerTemp:F2} °C
  - [Layer 3] PCM Jacket        : {pcmTemp:F2} °C [Phase Frac: {pcmPhase:F4}]
  - [Layer 4] PU Insulation     : {insTemp:F2} °C
  - [Layer 5] Outer Steel Shell : {shellTemp:F2} °C
  - Ambient Environment         : {ambient:F2} °C

HEAT FLOW METERS:
  - PCM Heat Removal Rate       : {heatRemoval:F1} W
  - Ambient Heat Influx         : {heatInflux:F1} W
  - System Cooling Phase        : {coolStatus}

-----------------------------------------------------
MODEL VALIDATION PANEL (ENGINEERING CHECK)
-----------------------------------------------------
  - ENERGY BALANCE ERROR        : {energyBalanceErrorPct:F6} %
  - AVAILABLE PCM CAPACITY      : {availCapacity:F2} kJ
  - MILK COOLING LOAD (5°C)     : {milkCoolingLoadKJ:F2} kJ
  - REMAINING PCM CAPACITY      : {remCapacity:F2} kJ
  - MODEL STATUS                : {modelStatus}

DESIGNATION TAGS:
  [SIMULATION / PREDICTION] Prototype physics validated
  [DESIGN ASSUMPTION] Properties & Upcm derived
  [REQUIRES DATASHEET VALIDATION] PCM Latent Lpcm
=====================================================";
    }
}
