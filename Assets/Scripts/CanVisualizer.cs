using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[ExecuteAlways]
public class CanVisualizer : MonoBehaviour
{
    public enum CanType
    {
        Bare,
        Proposed,      // Milk Core
        PCM,           // PCM Jacket
        InnerLiner,    // Steel Liner
        Insulation,    // Polyurethane Insulation
        OuterShell     // Outer Steel Shell
    }

    [Header("Configuration")]
    public CanType canType = CanType.Bare;
    public string labelPrefix = "Milk: ";

    [Header("Visual Mapping")]
    public Renderer canRenderer;
    public TMP_Text tmpTempLabel;
    public Text legacyTempLabel;
    public TextMesh world3DTempLabel;

    [Header("Temperature Scale (°C)")]
    public float minTemp = 4.0f;   // Coldest (Blue)
    public float midTemp = 15.0f;  // Warm (Green/Yellow)
    public float maxTemp = 35.0f;  // Hot (Red)

    [Header("Color Gradient")]
    public Color coldColor = new Color(0.0f, 0.45f, 1.0f); // Blue
    public Color midColor = new Color(0.2f, 0.8f, 0.2f);   // Green/Yellow
    public Color hotColor = new Color(1.0f, 0.15f, 0.1f);  // Red

    private MaterialPropertyBlock propBlock;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        AutoFindReferences();
    }

    private void OnValidate()
    {
        AutoFindReferences();
    }

    public void AutoFindReferences()
    {
        if (propBlock == null)
        {
            propBlock = new MaterialPropertyBlock();
        }
        if (canRenderer == null)
        {
            canRenderer = GetComponent<Renderer>();
        }
        if (world3DTempLabel == null)
        {
            world3DTempLabel = GetComponentInChildren<TextMesh>();
        }
        if (tmpTempLabel == null)
        {
            tmpTempLabel = GetComponentInChildren<TMP_Text>();
            if (tmpTempLabel == null && transform.parent != null)
            {
                tmpTempLabel = transform.parent.GetComponentInChildren<TMP_Text>();
            }
        }
        if (legacyTempLabel == null)
        {
            legacyTempLabel = GetComponentInChildren<Text>();
            if (legacyTempLabel == null && transform.parent != null)
            {
                legacyTempLabel = transform.parent.GetComponentInChildren<Text>();
            }
        }
        SetDefaultPrefix();
    }

    public void SetDefaultPrefix()
    {
        if (string.IsNullOrEmpty(labelPrefix) || labelPrefix == "Milk: ")
        {
            switch (canType)
            {
                case CanType.Bare:
                    labelPrefix = "Bare Milk: ";
                    break;
                case CanType.Proposed:
                    labelPrefix = "Milk Core: ";
                    break;
                case CanType.InnerLiner:
                    labelPrefix = "Inner Liner: ";
                    break;
                case CanType.PCM:
                    labelPrefix = "PCM Jacket: ";
                    break;
                case CanType.Insulation:
                    labelPrefix = "PU Insulation: ";
                    break;
                case CanType.OuterShell:
                    labelPrefix = "Outer Shell: ";
                    break;
            }
        }
    }

    private void Update()
    {
        float currentTemp = 30.0f;

        // 1. Primary: Direct CSV Data Playback via SimDataLoader
        if (SimClock.Instance != null && SimDataLoader.Instance != null && SimDataLoader.Instance.IsDataLoaded)
        {
            float currentTime = SimClock.Instance.simTimeHours;
            currentTemp = SimDataLoader.Instance.GetLayerTempAt(canType, currentTime);
        }
        else if (SimDataLoader.Instance != null && SimDataLoader.Instance.IsDataLoaded)
        {
            currentTemp = SimDataLoader.Instance.GetLayerTempAt(canType, 0f);
        }
        // 2. Secondary fallback if physics engine instance is present
        else if (RealtimePhysicsEngine.Instance != null)
        {
            switch (canType)
            {
                case CanType.Bare:
                    currentTemp = RealtimePhysicsEngine.Instance.currentMilkTempBare;
                    break;
                case CanType.Proposed:
                    currentTemp = RealtimePhysicsEngine.Instance.currentMilkTempProp;
                    break;
                case CanType.PCM:
                    currentTemp = RealtimePhysicsEngine.Instance.currentPcmTemp;
                    break;
                case CanType.InnerLiner:
                    currentTemp = RealtimePhysicsEngine.Instance.currentLinerTemp;
                    break;
                case CanType.Insulation:
                    currentTemp = RealtimePhysicsEngine.Instance.currentInsulationTemp;
                    break;
                case CanType.OuterShell:
                    currentTemp = RealtimePhysicsEngine.Instance.currentOuterShellTemp;
                    break;
            }
        }
        else
        {
            // Exact initial thermodynamic state at t = 0 (Milk = 30°C, PCM = 2°C, Amb = 35°C)
            switch (canType)
            {
                case CanType.Bare:
                case CanType.Proposed:
                    currentTemp = 30.0f;
                    break;
                case CanType.InnerLiner:
                    currentTemp = 2.0f;  // Bound T_liner = T_milk - Q_pcm/(h_conv * A) at t=0 (714W extraction)
                    break;
                case CanType.PCM:
                    currentTemp = 2.0f;  // Initial solid PCM jacket temp at t=0
                    break;
                case CanType.Insulation:
                    currentTemp = 18.5f; // Mid-layer insulation temp T_pcm + (T_amb - T_pcm)/2 at t=0
                    break;
                case CanType.OuterShell:
                    currentTemp = 34.8f; // Outer steel shell temp T_amb - 0.2°C at t=0
                    break;
            }
        }

        Color targetColor = EvaluateTemperatureColor(currentTemp);
        ApplyColor(targetColor);
        UpdateTextReadout(currentTemp);
    }

    private List<float> GetSeriesForType(CanType type)
    {
        if (SimDataLoader.Instance == null) return null;
        switch (type)
        {
            case CanType.Bare:
                return SimDataLoader.Instance.milkBare;
            case CanType.Proposed:
                return SimDataLoader.Instance.milkProposed;
            case CanType.PCM:
                return SimDataLoader.Instance.pcmTemp;
            default:
                return SimDataLoader.Instance.milkBare;
        }
    }

    private Color EvaluateTemperatureColor(float temp)
    {
        if (temp <= midTemp)
        {
            float subFrac = Mathf.InverseLerp(minTemp, midTemp, temp);
            return Color.Lerp(coldColor, midColor, subFrac);
        }
        else
        {
            float subFrac = Mathf.InverseLerp(midTemp, maxTemp, temp);
            return Color.Lerp(midColor, hotColor, subFrac);
        }
    }

    private void ApplyColor(Color color)
    {
        if (canRenderer != null)
        {
            if (propBlock == null)
            {
                propBlock = new MaterialPropertyBlock();
            }
            canRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", color);
            propBlock.SetColor("_BaseColor", color); // For URP Lit Shader
            canRenderer.SetPropertyBlock(propBlock);
        }
    }

    private void UpdateTextReadout(float temp)
    {
        string textContent = $"{labelPrefix}{temp:F1} °C";

        if (tmpTempLabel != null)
        {
            tmpTempLabel.text = textContent;
        }

        if (legacyTempLabel != null)
        {
            legacyTempLabel.text = textContent;
        }

        if (world3DTempLabel != null)
        {
            try
            {
                world3DTempLabel.text = textContent;

                // Billboard: Make 3D text face the main camera so it is ALWAYS clearly readable from any angle
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    world3DTempLabel.transform.rotation = Quaternion.LookRotation(world3DTempLabel.transform.position - mainCam.transform.position);
                }
            }
            catch (MissingComponentException)
            {
                world3DTempLabel = null;
            }
        }
    }
}
