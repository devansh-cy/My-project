using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DroneTrainer.Drones;
using DroneTrainer.Sensors;
using DroneTrainer.Trainee;

namespace DroneTrainer.Scenarios
{
    /// <summary>
    /// Phase 5: White-Cell Exercise Controller & Anomaly Injector.
    /// Empowers the unit-level military instructor to inject live tactical anomalies into the simulation:
    /// 1. Sensor Blackout / Adversary EW Jamming (blinds radar and RF sensors)
    /// 2. Surprise Fog Wave (sudden meteorological degradation dropping optical visibility)
    /// 3. Surprise Decoy Wave (spawns biological bird decoys / chaff radar blips)
    /// 4. Swarm Tactical Pincer Reroute (triggers high-altitude decoy and low urban canyon flank)
    /// Toggled via the F2 hotkey or on-screen instructor badge.
    /// </summary>
    public class InstructorWhiteCellController : MonoBehaviour
    {
        public static InstructorWhiteCellController Instance { get; private set; }

        [Header("White Cell Settings")]
        [SerializeField] private bool showWhiteCellConsole = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.F2;

        [Header("Active Injections State")]
        private bool isEwBlackoutActive = false;
        private float ewBlackoutTimer = 0f;
        private bool isSurpriseFogActive = false;
        private bool isDecoyWaveSpawned = false;

        private float savedFogDensity = 0.005f;
        private bool savedFogEnabled = false;
        private Color savedFogColor = Color.gray;

        private string activeBannerMessage = "";
        private float bannerTimer = 0f;

        // UI Styles
        private GUIStyle windowStyle;
        private GUIStyle headerStyle;
        private GUIStyle buttonStyle;
        private GUIStyle activeButtonStyle;
        private GUIStyle statusStyle;
        private bool stylesInitialized = false;

        public bool IsEwBlackoutActive => isEwBlackoutActive;
        public bool IsSurpriseFogActive => isSurpriseFogActive;

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
            savedFogEnabled = RenderSettings.fog;
            savedFogDensity = RenderSettings.fogDensity;
            savedFogColor = RenderSettings.fogColor;
        }

        private void Update()
        {
            if (DroneTrainer.Core.InputHelper.WasKeyPressed(toggleKey))
            {
                showWhiteCellConsole = !showWhiteCellConsole;
            }

            if (isEwBlackoutActive)
            {
                ewBlackoutTimer -= Time.deltaTime;
                if (ewBlackoutTimer <= 0f)
                {
                    EndEwBlackout();
                }
            }

            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
                if (bannerTimer <= 0f) activeBannerMessage = "";
            }
        }

        #region Anomaly Injections

        /// <summary>
        /// Injects 15-second electronic warfare (EW) barrage blinding radar and RF sensors.
        /// </summary>
        public void InjectEwBlackout(float durationSeconds = 15f)
        {
            isEwBlackoutActive = true;
            ewBlackoutTimer = durationSeconds;

            ApplySensorDegradation(rfJamming: 0.98f, radarNoise: 0.85f, radarClutter: 0.90f);
            ShowBanner($"[WHITE CELL ANOMALY] ADVERSARY EW BARRAGE: SENSOR BLACKOUT ACTIVE ({durationSeconds:F0}s)", 5f);
            Debug.LogWarning($"[WhiteCell] Injected Adversary EW Jamming Blackout for {durationSeconds} seconds.");
        }

        public void EndEwBlackout()
        {
            isEwBlackoutActive = false;
            ewBlackoutTimer = 0f;
            ApplySensorDegradation(rfJamming: 0.0f, radarNoise: 0.05f, radarClutter: 0.10f);
            ShowBanner("[WHITE CELL] EW BARRAGE CLEARED: SENSORS RESTORED TO NOMINAL", 3f);
        }

        /// <summary>
        /// Injects sudden dense fog wave blinding electro-optical cameras and reducing visibility.
        /// </summary>
        public void ToggleSurpriseFog()
        {
            isSurpriseFogActive = !isSurpriseFogActive;

            if (isSurpriseFogActive)
            {
                RenderSettings.fog = true;
                RenderSettings.fogDensity = 0.045f;
                RenderSettings.fogColor = new Color(0.45f, 0.48f, 0.52f, 1f);

                var cameras = FindObjectsByType<EOCameraSim>(FindObjectsSortMode.None);
                foreach (var cam in cameras)
                {
                    if (cam.ActiveProfile != null) cam.ActiveProfile.opticalVisibility = 0.15f;
                }

                ShowBanner("[WHITE CELL ANOMALY] SUDDEN ADVERSE WEATHER: DENSE FOG WAVE INJECTED", 4f);
            }
            else
            {
                RenderSettings.fog = savedFogEnabled;
                RenderSettings.fogDensity = savedFogDensity;
                RenderSettings.fogColor = savedFogColor;

                var cameras = FindObjectsByType<EOCameraSim>(FindObjectsSortMode.None);
                foreach (var cam in cameras)
                {
                    if (cam.ActiveProfile != null) cam.ActiveProfile.opticalVisibility = 1.0f;
                }

                ShowBanner("[WHITE CELL] WEATHER NORMALIZED: OPTICAL CONDITIONS RESTORED", 3f);
            }
        }

        /// <summary>
        /// Spawns a flock of biological bird decoys with animated wings to test trainee identification and ROE adherence.
        /// </summary>
        public void SpawnDecoyWave(int count = 5)
        {
            isDecoyWaveSpawned = true;
            var tower = GameObject.Find("SensorTower") ?? GameObject.Find("FOB_Watchtower");
            Vector3 origin = tower != null ? tower.transform.position : Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = origin + new Vector3(
                    Random.Range(-150f, 150f),
                    Random.Range(25f, 50f),
                    Random.Range(-150f, 150f)
                );

                GameObject birdObj = new GameObject($"Decoy_Bird_{i + 1}");
                birdObj.transform.position = spawnPos;

                var drone = birdObj.AddComponent<DroneAgent>();
                drone.MilSpecGroup = UasMilSpecGroup.BiologicalDecoy;
                drone.SetFlightMode(DroneFlightMode.Orbit);
                drone.SetOrbitParameters(origin + Vector3.up * 35f, Random.Range(60f, 120f), i % 2 == 0);

                birdObj.AddComponent<ProceduralBirdModel>();
            }

            ShowBanner($"[WHITE CELL ANOMALY] DECOY WAVE INJECTED: {count} BIOLOGICAL DECOYS IN AIRSPACE", 4f);
        }

        /// <summary>
        /// Triggers tactical pincer maneuver in active swarm (decoy high approach + low urban flank).
        /// </summary>
        public void TriggerSwarmPincer()
        {
            if (SwarmController.Instance != null)
            {
                SwarmController.Instance.TriggerPincerManeuver();
                ShowBanner("[WHITE CELL ANOMALY] SWARM PINCER REROUTE: DUAL-AXIS INGRESS TRIGGERED", 4f);
            }
            else
            {
                ShowBanner("[WHITE CELL WARNING] NO ACTIVE SWARM CONTROLLER IN SCENE", 3f);
            }
        }

        public void ResetAllAnomalies()
        {
            if (isEwBlackoutActive) EndEwBlackout();
            if (isSurpriseFogActive) ToggleSurpriseFog();
            ShowBanner("[WHITE CELL] ALL ANOMALIES RESET TO SCENARIO BASELINE", 3f);
        }

        private void ApplySensorDegradation(float rfJamming, float radarNoise, float radarClutter)
        {
            var radars = FindObjectsByType<RadarSim>(FindObjectsSortMode.None);
            foreach (var r in radars)
            {
                if (r.ActiveProfile != null)
                {
                    r.ActiveProfile.rfJammingLevel = rfJamming;
                    r.ActiveProfile.radarNoise = radarNoise;
                    r.ActiveProfile.radarClutter = radarClutter;
                }
            }

            var huds = FindObjectsByType<SensorDegradationHUD>(FindObjectsSortMode.None);
            foreach (var h in huds)
            {
                // Trigger HUD visual alert
            }
        }

        private void ShowBanner(string message, float duration)
        {
            activeBannerMessage = message;
            bannerTimer = duration;
        }

        #endregion

        #region OnGUI White Cell Console

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = MakeTexture(2, 2, new Color(0.12f, 0.10f, 0.04f, 0.94f));

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = new Color(1f, 0.82f, 0.2f);

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true
            };
            statusStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            buttonStyle.normal.textColor = Color.white;

            activeButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            activeButtonStyle.normal.background = MakeTexture(2, 2, new Color(0.8f, 0.35f, 0f, 1f));
            activeButtonStyle.normal.textColor = Color.white;

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            // 1. Top Instructor Badge / Toggle Button
            if (GUI.Button(new Rect(10, 10, 220, 26), showWhiteCellConsole ? "▼ [F2] INSTRUCTOR CONSOLE" : "▶ [F2] INSTRUCTOR WHITE CELL", buttonStyle))
            {
                showWhiteCellConsole = !showWhiteCellConsole;
            }

            // 2. High-Priority Alert Banner across top center
            if (!string.IsNullOrEmpty(activeBannerMessage))
            {
                GUI.color = new Color(1f, 0.85f, 0.1f, 0.95f);
                Rect bannerRect = new Rect((Screen.width - 650f) * 0.5f, 12, 650f, 32);
                GUI.Box(bannerRect, activeBannerMessage, windowStyle);
                GUI.color = Color.white;
            }

            // 3. Floating White Cell Console Panel
            if (showWhiteCellConsole)
            {
                Rect panelRect = new Rect(10, 42, 340, 310);
                GUILayout.BeginArea(panelRect, "WHITE CELL: TACTICAL ANOMALY INJECTOR", windowStyle);
                GUILayout.Space(22);

                GUILayout.Label("SCENARIO RANDOMIZATION & EW INJECTIONS", headerStyle);
                GUILayout.Space(4);

                // Anomaly 1: Sensor Blackout
                GUIStyle ewStyle = isEwBlackoutActive ? activeButtonStyle : buttonStyle;
                string ewTitle = isEwBlackoutActive 
                    ? $"[ACTIVE] EW BLACKOUT ({ewBlackoutTimer:F1}s)" 
                    : "⚡ INJECT SENSOR BLACKOUT (15s EW)";
                if (GUILayout.Button(ewTitle, ewStyle, GUILayout.Height(32)))
                {
                    if (isEwBlackoutActive) EndEwBlackout();
                    else InjectEwBlackout(15f);
                }

                GUILayout.Space(4);

                // Anomaly 2: Surprise Fog Wave
                GUIStyle fogStyle = isSurpriseFogActive ? activeButtonStyle : buttonStyle;
                string fogTitle = isSurpriseFogActive 
                    ? "[ACTIVE] CLEAR SURPRISE FOG" 
                    : "🌫 INJECT SURPRISE FOG WAVE";
                if (GUILayout.Button(fogTitle, fogStyle, GUILayout.Height(32)))
                {
                    ToggleSurpriseFog();
                }

                GUILayout.Space(4);

                // Anomaly 3: Biological Decoy Flock
                if (GUILayout.Button("🐦 SPAWN SURPRISE DECOY FLOCK", buttonStyle, GUILayout.Height(32)))
                {
                    SpawnDecoyWave(5);
                }

                GUILayout.Space(4);

                // Anomaly 4: Swarm Pincer Reroute
                if (GUILayout.Button("⚔ TRIGGER SWARM PINCER REROUTE", buttonStyle, GUILayout.Height(32)))
                {
                    TriggerSwarmPincer();
                }

                GUILayout.Space(8);

                // Reset Baseline
                GUI.color = new Color(0.9f, 0.3f, 0.3f);
                if (GUILayout.Button("↺ RESET ALL ANOMALIES", buttonStyle, GUILayout.Height(26)))
                {
                    ResetAllAnomalies();
                }
                GUI.color = Color.white;

                GUILayout.Space(4);
                GUILayout.Label("<color=#aaaaaa>Press F2 to hide | Real-time C-UAS stress testing</color>", statusStyle);

                GUILayout.EndArea();
            }
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            Texture2D tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        #endregion
    }
}
