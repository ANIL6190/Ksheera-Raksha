using UnityEngine;

public class RealtimePhysicsEngine : MonoBehaviour
{
    public static RealtimePhysicsEngine Instance { get; private set; }

    public enum SimulationMode
    {
        LivePhysicsSolver,
        CSVPlayback
    }

    [Header("Simulation Mode")]
    public SimulationMode mode = SimulationMode.LivePhysicsSolver;

    [Header("Live Physics Parameters (Inspector Adjustable)")]
    [Tooltip("Ambient Environment Temperature (°C)")]
    public float ambientTemp = 35.0f;

    [Tooltip("Initial Milk Temperature (°C)")]
    public float initialMilkTemp = 30.0f;

    [Tooltip("Initial PCM Temperature (°C)")]
    public float initialPcmTemp = 2.0f;

    [Tooltip("Milk Volume (Liters)")]
    public float milkVolumeLiters = 20.0f;

    [Tooltip("PCM Mass (kg)")]
    public float pcmMassKg = 3.0f;

    [Tooltip("Polyurethane Insulation Thickness (mm)")]
    public float insulationThicknessMm = 20.0f;

    [Header("Material Physical Constants")]
    public float milkDensity = 1030.0f;       // kg/m3
    public float milkCp = 3900.0f;            // J/(kg*K)
    public float pcmLatentHeat = 250000.0f;   // J/kg (250 kJ/kg)
    public float pcmCpSolid = 2000.0f;        // J/(kg*K)
    public float pcmCpLiquid = 2200.0f;       // J/(kg*K)
    public float pcmMeltTemp = 5.0f;          // °C
    public float insConductivity = 0.025f;    // W/(m*K) Polyurethane
    public float surfaceArea = 0.85f;         // m2 Can surface area

    [Header("Live Calculated States")]
    public float currentMilkTempProp = 30.0f;
    public float currentMilkTempBare = 30.0f;
    public float currentLinerTemp = 28.5f;       // Inner Steel Liner Temp (°C)
    public float currentPcmTemp = 2.0f;           // PCM Jacket Temp (°C)
    public float currentInsulationTemp = 18.5f;   // Insulation Layer Temp (°C)
    public float currentOuterShellTemp = 34.8f;   // Outer Steel Shell Temp (°C)
    public float currentPcmPhaseFrac = 0.0f;  // 0.0 = Solid, 1.0 = Liquid
    public float currentHeatRemovalW = 0.0f;
    public float currentAmbientInfluxW = 0.0f;
    public float currentRemainingPcmKJ = 768.0f;
    public float currentFluidTopTemp = 30.0f;
    public float currentFluidBottomTemp = 30.0f;

    private float milkMassKg;
    private float milkHeatCapacity; // J/K
    private float totalPcmLatentCapacityJ;
    private float currentPcmEnthalpyJ;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializePhysics();
    }

    public void InitializePhysics()
    {
        milkMassKg = milkDensity * (milkVolumeLiters * 0.001f); // 20.6 kg
        milkHeatCapacity = milkMassKg * milkCp;                  // 80,340 J/K
        totalPcmLatentCapacityJ = pcmMassKg * pcmLatentHeat;     // 750,000 J (750 kJ)
        currentRemainingPcmKJ = totalPcmLatentCapacityJ / 1000.0f;

        currentMilkTempProp = initialMilkTemp;
        currentMilkTempBare = initialMilkTemp;
        currentPcmTemp = initialPcmTemp;
        currentPcmPhaseFrac = 0.0f;
        currentPcmEnthalpyJ = 0.0f;

        currentFluidBottomTemp = initialMilkTemp;
        currentFluidTopTemp = initialMilkTemp;

        UpdateConcentricLayerTemps();
    }

    private void Update()
    {
        if (SimClock.Instance == null || !SimClock.Instance.isPlaying) return;

        if (mode == SimulationMode.LivePhysicsSolver)
        {
            StepLivePhysics(Time.deltaTime * (SimClock.Instance.simDurationHours / SimClock.Instance.realSecondsForFullRun) * 3600.0f);
        }
        else if (mode == SimulationMode.CSVPlayback && SimDataLoader.Instance != null && SimDataLoader.Instance.IsDataLoaded)
        {
            float tHours = SimClock.Instance.simTimeHours;
            currentMilkTempProp = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.milkProposed, tHours);
            currentMilkTempBare = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.milkBare, tHours);
            currentPcmTemp = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.pcmTemp, tHours);
            currentPcmPhaseFrac = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.pcmPhaseFrac, tHours);
            currentHeatRemovalW = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.pcmHeatRemovalW, tHours);
            currentAmbientInfluxW = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.ambientHeatInfluxW, tHours);
            currentRemainingPcmKJ = SimDataLoader.Instance.GetValueAt(SimDataLoader.Instance.pcmRemainingCapacityKJ, tHours);

            // Calculate buoyancy fluid stratification (bottom cold, top warm)
            currentFluidBottomTemp = Mathf.Max(4.0f, currentMilkTempProp - 1.5f);
            currentFluidTopTemp = currentMilkTempProp + 1.2f;

            UpdateConcentricLayerTemps();
        }
    }

    private void StepLivePhysics(float dtSeconds)
    {
        if (dtSeconds <= 0f) return;

        // 1. Bare Uninsulated Can Transient Heat Up to Ambient (35°C)
        float uBare = 15.0f; // W/(m2*K) overall convective coefficient for bare metal
        float qBareW = uBare * surfaceArea * (ambientTemp - currentMilkTempBare);
        currentMilkTempBare += (qBareW / milkHeatCapacity) * dtSeconds;
        currentMilkTempBare = Mathf.Min(currentMilkTempBare, ambientTemp);

        // 2. Proposed Can Insulation Resistance & Heat Influx
        float rIns = (insulationThicknessMm * 0.001f) / (insConductivity * surfaceArea); // K/W
        float uIns = 1.0f / Mathf.Max(rIns, 0.001f);
        currentAmbientInfluxW = uIns * (ambientTemp - currentPcmTemp);

        // 3. Milk to PCM Heat Extraction
        float hConvLiner = 30.0f; // W/(m2*K)
        float qPcmW = hConvLiner * surfaceArea * Mathf.Max(0f, currentMilkTempProp - currentPcmTemp);
        currentHeatRemovalW = qPcmW;

        // 4. Update Milk Temperature ODE
        float dTmilk = (-qPcmW / milkHeatCapacity) * dtSeconds;
        currentMilkTempProp += dTmilk;
        currentMilkTempProp = Mathf.Max(currentMilkTempProp, currentPcmTemp);

        // 5. PCM Enthalpy & Phase Change Update
        float netPcmHeatFlowW = qPcmW + currentAmbientInfluxW;
        currentPcmEnthalpyJ += netPcmHeatFlowW * dtSeconds;

        if (currentPcmEnthalpyJ <= totalPcmLatentCapacityJ)
        {
            // Latent Melting Phase (Phase change at ~5°C)
            currentPcmPhaseFrac = Mathf.Clamp01(currentPcmEnthalpyJ / totalPcmLatentCapacityJ);
            currentPcmTemp = pcmMeltTemp + (currentPcmPhaseFrac * 0.5f); // Holds near 5°C during melt
        }
        else
        {
            // Fully Liquid PCM - Sensible Heating after phase change completion
            currentPcmPhaseFrac = 1.0f;
            float excessHeatJ = currentPcmEnthalpyJ - totalPcmLatentCapacityJ;
            currentPcmTemp = pcmMeltTemp + (excessHeatJ / (pcmMassKg * pcmCpLiquid));
            currentPcmTemp = Mathf.Min(currentPcmTemp, ambientTemp);
        }

        currentRemainingPcmKJ = Mathf.Max(0f, (totalPcmLatentCapacityJ - currentPcmEnthalpyJ) / 1000.0f);

        // Natural Convection Thermal Stratification (Top vs Bottom Fluid)
        currentFluidBottomTemp = Mathf.Max(4.0f, currentMilkTempProp - 1.8f * (1.0f - currentPcmPhaseFrac));
        currentFluidTopTemp = currentMilkTempProp + 1.5f;

        UpdateConcentricLayerTemps();
    }

    private void UpdateConcentricLayerTemps()
    {
        // 1. Inner Stainless Steel Liner Temperature (T_liner = T_milk - Q_pcm / (h_conv * A_liner))
        float hConvLiner = 30.0f; // W/(m2*K)
        float deltaTLiner = currentHeatRemovalW / (hConvLiner * surfaceArea);
        currentLinerTemp = Mathf.Max(currentPcmTemp, currentMilkTempProp - deltaTLiner);

        // 2. Polyurethane Insulation Mid-layer Temperature
        currentInsulationTemp = currentPcmTemp + (ambientTemp - currentPcmTemp) * 0.5f;

        // 3. Outer Stainless Steel Shell Surface Temperature
        currentOuterShellTemp = Mathf.Max(currentInsulationTemp, ambientTemp - 0.2f);
    }

    public void ResetSimulation()
    {
        InitializePhysics();
    }
}
