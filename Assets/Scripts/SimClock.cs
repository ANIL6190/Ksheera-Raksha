using UnityEngine;
using UnityEngine.UI;

public class SimClock : MonoBehaviour
{
    public static SimClock Instance { get; private set; }

    [Header("Simulation Timing")]
    [Tooltip("Current playback position in simulated hours.")]
    public float simTimeHours = 0f;

    [Tooltip("Total duration of the simulation in hours.")]
    public float simDurationHours = 12f;

    [Tooltip("How many real seconds to play back the full simulation (Increase to slow down!).")]
    public float realSecondsForFullRun = 60f; // Default: 60 real seconds for full 12-hour run

    [Tooltip("Playback Speed Multiplier (1 = Normal speed, 0.5 = Half speed, 0.2 = Extra slow).")]
    [Range(0.05f, 5.0f)]
    public float speedMultiplier = 1.0f;

    [Header("Playback State")]
    public bool isPlaying = false;

    [Header("UI References (Optional)")]
    public Slider scrubBar;

    private bool isScrubbing = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (scrubBar != null)
        {
            scrubBar.minValue = 0f;
            scrubBar.maxValue = simDurationHours;
            scrubBar.value = simTimeHours;
            scrubBar.onValueChanged.AddListener(OnScrub);
        }
    }

    private void Update()
    {
        if (isPlaying && !isScrubbing)
        {
            float baseHoursPerSecond = simDurationHours / Mathf.Max(realSecondsForFullRun, 1.0f);
            simTimeHours += Time.deltaTime * baseHoursPerSecond * speedMultiplier;

            if (simTimeHours >= simDurationHours)
            {
                simTimeHours = simDurationHours;
                isPlaying = false;
            }

            if (scrubBar != null)
            {
                scrubBar.SetValueWithoutNotify(simTimeHours);
            }
        }
    }

    public void Play()
    {
        if (simTimeHours >= simDurationHours)
        {
            simTimeHours = 0f;
        }
        isPlaying = true;
    }

    public void Pause()
    {
        isPlaying = false;
    }

    public void TogglePlayPause()
    {
        if (isPlaying)
            Pause();
        else
            Play();
    }

    public void Reset()
    {
        simTimeHours = 0f;
        isPlaying = false;

        if (scrubBar != null)
        {
            scrubBar.SetValueWithoutNotify(simTimeHours);
        }
    }

    public void OnScrub(float value)
    {
        simTimeHours = Mathf.Clamp(value, 0f, simDurationHours);
    }

    public void OnPointerDownScrub()
    {
        isScrubbing = true;
    }

    public void OnPointerUpScrub()
    {
        isScrubbing = false;
    }
}
