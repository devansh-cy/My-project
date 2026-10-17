using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DroneTrainer.Scenarios;
using DroneTrainer.Scenarios.Procedural;
using DroneTrainer.Logging;
using DroneTrainer.AAR;

namespace DroneTrainer.UI
{
    /// <summary>
    /// Master Main Menu Controller for the C-UAS Simulator.
    /// Manages operator/unit profile inputs, scripted scenario selection,
    /// deterministic procedural scenario generation, and simulation launches.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Operator Configuration")]
        [SerializeField] private string traineeName = "OPERATOR-ALPHA";
        [SerializeField] private string unitName = "1st C-UAS Defense Battery";

        [Header("Procedural Generation Parameters")]
        [SerializeField] private int proceduralSeed = 42891;
        [SerializeField] [Range(0f, 1f)] private float proceduralDifficulty = 0.40f;
        [SerializeField] private bool useAdaptiveDifficulty = true;

        [Header("Optional Scene Navigation")]
        [SerializeField] private string simulationSceneName = "";

        [Header("References")]
        [SerializeField] private DifficultyController difficultyController;
        [SerializeField] private AARDashboardUI aarDashboard;
        [SerializeField] private EventLogger eventLogger;

        // Discovered Scripted Scenarios
        private List<ScenarioDefinition> availableScenarios = new List<ScenarioDefinition>();
        private int selectedScenarioIndex = 0;
        private ScenarioDefinition activePreviewScenario;
        private bool isProceduralMode = false;
        private bool isMenuVisible = true;

        public bool IsMenuVisible
        {
            get => isMenuVisible;
            set => isMenuVisible = value;
        }

        public void ToggleMenu() => isMenuVisible = !isMenuVisible;

        private void Update()
        {
            if (DroneTrainer.Core.InputHelper.WasKeyPressed(KeyCode.Escape))
            {
                isMenuVisible = !isMenuVisible;
            }
        }

        // UI State
        private Vector2 scenarioListScroll = Vector2.zero;
        private GUIStyle windowStyle;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle buttonStyle;
        private GUIStyle buttonActiveStyle;
        private GUIStyle cardStyle;
        private GUIStyle textLabelStyle;
        private bool stylesInitialized = false;

        public string TraineeName => traineeName;
        public string UnitName => unitName;
        public ScenarioDefinition ActiveScenario => activePreviewScenario;

        private void Awake()
        {
            // Restore saved credentials
            traineeName = PlayerPrefs.GetString("CUAS_TraineeName", traineeName);
            unitName = PlayerPrefs.GetString("CUAS_UnitName", unitName);

            if (difficultyController == null)
            {
                difficultyController = FindAnyObjectByType<DifficultyController>();
            }

            LoadAvailableScenarios();

            // Default to first scenario preview
            if (availableScenarios.Count > 0)
            {
                SelectScriptedScenario(0);
            }
            else
            {
                GenerateNewProceduralScenario();
            }
        }

        public void LoadAvailableScenarios()
        {
            availableScenarios = ScenarioLoader.LoadAllScenarios();
        }

        #region Scenario Selection & Generation

        public void SelectScriptedScenario(int index)
        {
            if (availableScenarios.Count == 0) return;

            selectedScenarioIndex = Mathf.Clamp(index, 0, availableScenarios.Count - 1);
            activePreviewScenario = availableScenarios[selectedScenarioIndex];
            isProceduralMode = false;
        }

        public void GenerateNewProceduralScenario(int? specificSeed = null)
        {
            if (specificSeed.HasValue)
            {
                proceduralSeed = specificSeed.Value;
            }
            else
            {
                proceduralSeed = UnityEngine.Random.Range(1000, 999999);
            }

            if (useAdaptiveDifficulty && difficultyController != null)
            {
                activePreviewScenario = difficultyController.GenerateNextScenario(proceduralSeed);
                proceduralDifficulty = difficultyController.CurrentDifficulty;
            }
            else
            {
                activePreviewScenario = ScenarioGenerator.Generate(proceduralSeed, proceduralDifficulty);
            }

            isProceduralMode = true;
        }

        public void LaunchSimulation()
        {
            // Save current credentials
            PlayerPrefs.SetString("CUAS_TraineeName", traineeName);
            PlayerPrefs.SetString("CUAS_UnitName", unitName);
            PlayerPrefs.Save();

            if (activePreviewScenario == null)
            {
                Debug.LogError("[MainMenu] Cannot launch: No scenario selected.");
                return;
            }

            Debug.Log($"<color=#00e5ff><b>=== LAUNCHING C-UAS SIMULATION: '{activePreviewScenario.name}' ===</b></color>\n" +
                      $"Trainee: {traineeName} | Unit: {unitName} | Threats: {activePreviewScenario.GetTotalDroneCount()}");

            // 1. Hide main menu
            isMenuVisible = false;

            // 2. Initialize EventLogger session if present
            if (eventLogger == null) eventLogger = FindAnyObjectByType<EventLogger>();
            if (eventLogger != null)
            {
                string sessionId = $"SESSION-{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                eventLogger.StartNewSession(sessionId, activePreviewScenario.id, traineeName);
            }

            // 3. Apply sensor profile to sensors and HUD
            var radar = FindAnyObjectByType<Sensors.RadarSim>();
            if (radar != null && activePreviewScenario.sensorProfile != null)
            {
                radar.ActiveProfile = activePreviewScenario.sensorProfile;
            }

            var eoCam = FindAnyObjectByType<Sensors.EOCameraSim>();
            if (eoCam != null)
            {
                if (activePreviewScenario.sensorProfile != null) eoCam.ActiveProfile = activePreviewScenario.sensorProfile;
                eoCam.ActiveTimeOfDay = activePreviewScenario.timeOfDay;
            }

            var hud = FindAnyObjectByType<Sensors.SensorDegradationHUD>();
            // HUD automatically syncs with Radar and EO profiles in Update

            // 4. Apply lighting & environment
            var envLight = FindAnyObjectByType<Environment.EnvironmentLightingController>();
            if (envLight != null)
            {
                envLight.ApplyScenarioEnvironment(activePreviewScenario);
            }

            // 5. Spawn scenario threat swarm
            var swarm = FindAnyObjectByType<Drones.SwarmController>();
            if (swarm != null)
            {
                int count = Mathf.Max(2, activePreviewScenario.GetTotalDroneCount());
                swarm.SpawnSwarm(count);
            }

            // 6. Automatically open Operator C2 Console
            var opUi = FindAnyObjectByType<Trainee.OperatorUIController>();
            if (opUi != null)
            {
                opUi.SetConsoleVisible(true);
            }

            // Load dedicated simulation scene if specified
            if (!string.IsNullOrEmpty(simulationSceneName) && SceneManager.GetSceneByName(simulationSceneName).IsValid())
            {
                SceneManager.LoadScene(simulationSceneName);
            }
        }

        #endregion

        #region Main Menu GUI Presentation

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = MakeTexture(2, 2, new Color(0.05f, 0.07f, 0.10f, 0.97f));

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                richText = true
            };
            headerStyle.normal.textColor = new Color(0f, 0.9f, 1f);

            subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                richText = true
            };
            subHeaderStyle.normal.textColor = Color.white;

            textLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true
            };
            textLabelStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            cardStyle = new GUIStyle(GUI.skin.box);
            cardStyle.normal.background = MakeTexture(2, 2, new Color(0.09f, 0.12f, 0.16f, 0.95f));

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            buttonStyle.normal.textColor = Color.white;

            buttonActiveStyle = new GUIStyle(buttonStyle);
            buttonActiveStyle.normal.background = MakeTexture(2, 2, new Color(0f, 0.6f, 0.85f, 1f));

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            if (!isMenuVisible)
            {
                GUI.color = new Color(0f, 0.9f, 1f);
                if (GUI.Button(new Rect(15, 15, 180, 32), "⚙ MISSION MENU (ESC)", buttonStyle))
                {
                    isMenuVisible = true;
                }
                GUI.color = Color.white;
                return;
            }

            float screenW = Screen.width;
            float screenH = Screen.height;

            float winW = Mathf.Min(1100f, screenW - 40f);
            float winH = Mathf.Min(680f, screenH - 40f);
            float winX = (screenW - winW) * 0.5f;
            float winY = (screenH - winH) * 0.5f;

            Rect menuRect = new Rect(winX, winY, winW, winH);
            GUILayout.BeginArea(menuRect, "AI-ENABLED C-UAS THREAT SIMULATION TRAINER - MISSION COMMAND", windowStyle);
            GUILayout.Space(25);

            // Top Operator ID Card
            DrawOperatorProfileHeader();

            GUILayout.Space(10);

            // Middle 2 Columns: Scenario Selector & Scenario Preview Card
            GUILayout.BeginHorizontal();

            // Left Column: Scenario Select & Procedural Controls
            DrawScenarioSelectionColumn(winW * 0.45f, winH - 200f);

            GUILayout.Space(12);

            // Right Column: Active Scenario Intelligence Briefing
            DrawMissionBriefingCard(winW * 0.51f, winH - 200f);

            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // Bottom Action Bar: Launch, AAR Dashboard, Reload
            DrawBottomActionBar();

            GUILayout.EndArea();
        }

        private void DrawOperatorProfileHeader()
        {
            GUILayout.BeginVertical(cardStyle);
            GUILayout.BeginHorizontal();

            GUILayout.Label("TRAINEE ID / CALLSIGN:", subHeaderStyle, GUILayout.Width(170));
            traineeName = GUILayout.TextField(traineeName, GUILayout.Width(200), GUILayout.Height(24));

            GUILayout.Space(25);

            GUILayout.Label("DEFENSE UNIT / BATTERY:", subHeaderStyle, GUILayout.Width(180));
            unitName = GUILayout.TextField(unitName, GUILayout.Width(280), GUILayout.Height(24));

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawScenarioSelectionColumn(float width, float height)
        {
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label("1. SCENARIO SELECTION", headerStyle);
            GUILayout.Space(6);

            // Scripted Scenario List
            GUILayout.Label($"SCRIPTED MISSIONS ({availableScenarios.Count})", subHeaderStyle);
            scenarioListScroll = GUILayout.BeginScrollView(scenarioListScroll, GUILayout.Height(130));

            for (int i = 0; i < availableScenarios.Count; i++)
            {
                var s = availableScenarios[i];
                bool isSelected = (!isProceduralMode && selectedScenarioIndex == i);
                GUIStyle style = isSelected ? buttonActiveStyle : buttonStyle;

                if (GUILayout.Button($"{s.name} ({s.terrain}, Diff {s.difficultyRating}/5)", style, GUILayout.Height(30)))
                {
                    SelectScriptedScenario(i);
                }
            }

            GUILayout.EndScrollView();

            GUILayout.Space(10);

            // Procedural Generator Controls
            GUILayout.Label("PROCEDURAL GENERATOR", subHeaderStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Seed: {proceduralSeed}", textLabelStyle, GUILayout.Width(110));
            if (GUILayout.Button("Roll New Seed", buttonStyle, GUILayout.Height(26)))
            {
                GenerateNewProceduralScenario();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Difficulty: {proceduralDifficulty:P0}", textLabelStyle, GUILayout.Width(110));
            proceduralDifficulty = GUILayout.HorizontalSlider(proceduralDifficulty, 0.05f, 0.95f);
            GUILayout.EndHorizontal();

            useAdaptiveDifficulty = GUILayout.Toggle(useAdaptiveDifficulty, " Enable Adaptive AI Difficulty & Weakness Biasing", textLabelStyle);

            GUILayout.Space(6);

            GUIStyle procBtnStyle = isProceduralMode ? buttonActiveStyle : buttonStyle;
            if (GUILayout.Button("GENERATE PROCEDURAL MISSION", procBtnStyle, GUILayout.Height(34)))
            {
                GenerateNewProceduralScenario(proceduralSeed);
            }

            GUILayout.EndVertical();
        }

        private void DrawMissionBriefingCard(float width, float height)
        {
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width), GUILayout.Height(height));

            if (activePreviewScenario == null)
            {
                GUILayout.Label("NO MISSION BRIEFING AVAILABLE", headerStyle);
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label("2. MISSION INTELLIGENCE BRIEFING", headerStyle);
            GUILayout.Space(4);

            GUILayout.Label($"TITLE: <b><color=#00e5ff>{activePreviewScenario.name}</color></b>", subHeaderStyle);
            GUILayout.Label($"ID: {activePreviewScenario.id} | Mode: {(isProceduralMode ? "<color=#55ff55>Procedural</color>" : "<color=#ffdd33>Scripted</color>")}", textLabelStyle);
            GUILayout.Space(6);

            GUILayout.Label(activePreviewScenario.description, textLabelStyle);
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>Terrain:</b> {activePreviewScenario.terrain}", textLabelStyle, GUILayout.Width(160));
            GUILayout.Label($"<b>Time:</b> {activePreviewScenario.timeOfDay}", textLabelStyle, GUILayout.Width(140));
            GUILayout.Label($"<b>Weather:</b> {activePreviewScenario.weather}", textLabelStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            int total = activePreviewScenario.GetTotalDroneCount();
            int hostile = activePreviewScenario.GetHostileDroneCount();
            int decoys = activePreviewScenario.GetDecoyCount();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>Total Targets:</b> {total}", textLabelStyle, GUILayout.Width(160));
            GUILayout.Label($"<b>Hostiles:</b> <color=#ff4444>{hostile}</color>", textLabelStyle, GUILayout.Width(140));
            GUILayout.Label($"<b>Decoys:</b> <color=#55ff55>{decoys}</color>", textLabelStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            SensorProfile sp = activePreviewScenario.sensorProfile;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>Radar Noise:</b> {sp.radarNoise:P0}", textLabelStyle, GUILayout.Width(160));
            GUILayout.Label($"<b>Radar Clutter:</b> {sp.radarClutter:P0}", textLabelStyle, GUILayout.Width(140));
            GUILayout.Label($"<b>RF Jamming:</b> {(sp.rfJammingLevel > 0 ? $"<color=#ff4444>{sp.rfJammingLevel:P0}</color>" : "<color=#55ff55>None</color>")}", textLabelStyle);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawBottomActionBar()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("RELOAD JSON SCENARIOS", buttonStyle, GUILayout.Width(200), GUILayout.Height(42)))
            {
                LoadAvailableScenarios();
                if (availableScenarios.Count > 0) SelectScriptedScenario(0);
            }

            GUILayout.Space(10);

            if (GUILayout.Button("OPEN AAR DASHBOARD (F1)", buttonStyle, GUILayout.Width(220), GUILayout.Height(42)))
            {
                if (aarDashboard != null)
                {
                    aarDashboard.ToggleDashboard();
                }
            }

            GUILayout.FlexibleSpace();

            GUI.color = new Color(0.1f, 0.9f, 0.3f);
            if (GUILayout.Button("LAUNCH SIMULATION ENGAGEMENT", buttonStyle, GUILayout.Width(320), GUILayout.Height(42)))
            {
                LaunchSimulation();
            }
            GUI.color = Color.white;

            GUILayout.EndHorizontal();
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        #endregion
    }
}
