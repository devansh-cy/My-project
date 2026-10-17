using System;
using System.Collections.Generic;
using UnityEngine;
using DroneTrainer.Logging;
using DroneTrainer.Scoring;
using DroneTrainer.Sensors;
using DroneTrainer.Trainee;

namespace DroneTrainer.AAR
{
    /// <summary>
    /// Interactive In-Game After-Action Review (AAR) Dashboard.
    /// Provides session scorecards, interactive event timelines, per-contact decision breakdowns,
    /// metric trends across historical sessions, trainee/unit profiles, and CSV exporting.
    /// </summary>
    public class AARDashboardUI : MonoBehaviour
    {
        [Header("Display & Input Settings")]
        [SerializeField] private bool isVisible = false;
        [SerializeField] private KeyCode toggleAarKey = KeyCode.F1;
        [SerializeField] private int trendSessionWindow = 10;

        [Header("References")]
        [SerializeField] private EventLogger eventLogger;
        [SerializeField] private string unitId = "1st C-UAS Defense Battery";

        // Runtime state
        private AARHistoryManager historyManager;
        private DecisionTreeScorer scorer;
        private SessionScoreReport currentReport;
        private SessionLog currentLog;
        private MetricTrendSummary activeTrends;

        private int selectedTab = 0;
        private readonly string[] tabLabels = new string[] 
        { 
            "Scorecard & Gauges", 
            "Session Timeline", 
            "Target Analysis", 
            "Decision Tree Paths",
            "Historical Trends & Profiles", 
            "Export Reports" 
        };

        private Vector2 timelineScroll = Vector2.zero;
        private Vector2 contactTableScroll = Vector2.zero;
        private Vector2 decisionTreeScroll = Vector2.zero;
        private Vector2 historyScroll = Vector2.zero;
        private string exportStatusMessage = "";

        // GUI Styles
        private GUIStyle windowStyle;
        private GUIStyle tabButtonStyle;
        private GUIStyle tabButtonActiveStyle;
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle cardStyle;
        private GUIStyle labelSmallStyle;
        private bool stylesInitialized = false;

        public bool IsVisible
        {
            get => isVisible;
            set => isVisible = value;
        }

        private void Awake()
        {
            historyManager = new AARHistoryManager();
            scorer = new DecisionTreeScorer();

            if (eventLogger == null)
            {
                eventLogger = FindAnyObjectByType<EventLogger>();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleAarKey))
            {
                ToggleDashboard();
            }
        }

        [ContextMenu("Toggle AAR Dashboard")]
        public void ToggleDashboard()
        {
            isVisible = !isVisible;
            if (isVisible)
            {
                RefreshAarData();
            }
        }

        /// <summary>
        /// Populates the AAR dashboard using current session telemetry or demo data.
        /// </summary>
        public void RefreshAarData()
        {
            if (eventLogger != null && eventLogger.ActiveSession != null)
            {
                currentLog = eventLogger.ActiveSession;
            }
            else
            {
                currentLog = CreateMockSessionForDemo();
            }

            currentReport = scorer.EvaluateSession(currentLog);
            historyManager.LoadDatabase();
            activeTrends = historyManager.ComputeTrends(trendSessionWindow, currentReport.traineeId);
        }

        /// <summary>
        /// Finalizes and records the active session into the history database.
        /// </summary>
        [ContextMenu("Finalize & Record Current Session")]
        public void RecordCurrentSession()
        {
            RefreshAarData();
            if (currentReport != null && currentLog != null)
            {
                historyManager.RecordSession(currentReport, currentLog.totalDurationSeconds, unitId);
                activeTrends = historyManager.ComputeTrends(trendSessionWindow, currentReport.traineeId);
                exportStatusMessage = $"Session '{currentReport.sessionId}' successfully archived to local history database.";
            }
        }

        #region OnGUI Rendering

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = MakeTexture(2, 2, new Color(0.06f, 0.08f, 0.12f, 0.96f));

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                richText = true
            };
            headerStyle.normal.textColor = new Color(0f, 0.95f, 1f);

            subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                richText = true
            };
            subHeaderStyle.normal.textColor = Color.white;

            labelSmallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true
            };
            labelSmallStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            cardStyle = new GUIStyle(GUI.skin.box);
            cardStyle.normal.background = MakeTexture(2, 2, new Color(0.10f, 0.13f, 0.18f, 0.95f));

            tabButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            tabButtonStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

            tabButtonActiveStyle = new GUIStyle(tabButtonStyle);
            tabButtonActiveStyle.normal.background = MakeTexture(2, 2, new Color(0f, 0.55f, 0.85f, 1f));
            tabButtonActiveStyle.normal.textColor = Color.white;

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!isVisible) return;

            InitializeStyles();

            float screenW = Screen.width;
            float screenH = Screen.height;

            float winW = Mathf.Min(1180f, screenW - 30f);
            float winH = Mathf.Min(720f, screenH - 40f);
            float winX = (screenW - winW) * 0.5f;
            float winY = (screenH - winH) * 0.5f;

            Rect windowRect = new Rect(winX, winY, winW, winH);
            GUILayout.BeginArea(windowRect, "AFTER-ACTION REVIEW (AAR) DASHBOARD - C-UAS SIMULATOR (Press F1 to Close)", windowStyle);
            GUILayout.Space(25);

            // Tab Navigation Bar
            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabLabels.Length; i++)
            {
                GUIStyle btnStyle = (selectedTab == i) ? tabButtonActiveStyle : tabButtonStyle;
                if (GUILayout.Button(tabLabels[i], btnStyle, GUILayout.Height(32)))
                {
                    selectedTab = i;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(12);

            // Active Tab Content
            switch (selectedTab)
            {
                case 0:
                    DrawScorecardTab(winW - 20f, winH - 85f);
                    break;
                case 1:
                    DrawTimelineTab(winW - 20f, winH - 85f);
                    break;
                case 2:
                    DrawPerContactBreakdownTab(winW - 20f, winH - 85f);
                    break;
                case 3:
                    DrawDecisionTreePathsTab(winW - 20f, winH - 85f);
                    break;
                case 4:
                    DrawTrendsAndProfilesTab(winW - 20f, winH - 85f);
                    break;
                case 5:
                    DrawExportTab(winW - 20f, winH - 85f);
                    break;
            }

            GUILayout.EndArea();
        }

        #endregion

        #region Tab 0: Scorecard & Visual Gauges

        private void DrawScorecardTab(float width, float height)
        {
            if (currentReport == null) return;

            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));

            // Summary Header Card
            GUILayout.BeginVertical(cardStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"SESSION: <b><color=#00e5ff>{currentReport.sessionId}</color></b> | Scenario: {currentReport.scenarioId}", headerStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Trainee: <b>{currentReport.traineeId}</b> | Unit: {unitId}", subHeaderStyle);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Master Score Gauge
            GUILayout.BeginVertical(cardStyle);
            GUILayout.Label("OVERALL OPERATOR PERFORMANCE RATING", headerStyle);
            GUILayout.Space(4);
            DrawProgressBar("COMPOSITE SCORE", currentReport.overallScorePercentage, 100f, GetScoreColor(currentReport.overallScorePercentage));
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Metric Breakdown Gauges
            GUILayout.BeginHorizontal();

            // Metric 1: Detection Speed
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width * 0.32f));
            GUILayout.Label("DETECTION REACTION", headerStyle);
            GUILayout.Label($"Score: <b>{currentReport.averageDetectionScore:F1} / 100</b>", subHeaderStyle);
            DrawProgressBar("Latency Metric", currentReport.averageDetectionScore, 100f, GetScoreColor(currentReport.averageDetectionScore));
            GUILayout.Label("Measures reaction latency from initial radar/EO contact appearance to first operator target selection.", labelSmallStyle);
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Metric 2: Classification Accuracy
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width * 0.32f));
            GUILayout.Label("CLASSIFICATION FIDELITY", headerStyle);
            GUILayout.Label($"Score: <b>{currentReport.averageClassificationScore:F1} / 100</b>", subHeaderStyle);
            DrawProgressBar("Accuracy Metric", currentReport.averageClassificationScore, 100f, GetScoreColor(currentReport.averageClassificationScore));
            GUILayout.Label($"Exact: <color=#55ff55>{currentReport.correctClassifications}</color> | Partial: <color=#ffdd33>{currentReport.partialClassifications}</color> | Wrong: <color=#ff3333>{currentReport.wrongClassifications}</color>", labelSmallStyle);
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // Metric 3: Engagement & ROE
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width * 0.32f));
            GUILayout.Label("RULES OF ENGAGEMENT (ROE)", headerStyle);
            GUILayout.Label($"Score: <b>{currentReport.averageEngagementScore:F1} / 100</b>", subHeaderStyle);
            DrawProgressBar("ROE Compliance", currentReport.averageEngagementScore, 100f, GetScoreColor(currentReport.averageEngagementScore));
            GUILayout.Label($"ROE Violations: <b>{(currentReport.roeViolations > 0 ? $"<color=#ff3333>{currentReport.roeViolations}</color>" : "<color=#55ff55>0</color>")}</b>", labelSmallStyle);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        #endregion

        #region Tab 1: Interactive Event Timeline

        private void DrawTimelineTab(float width, float height)
        {
            if (currentLog == null) return;

            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label($"SESSION EVENT TIMELINE ({currentLog.entries.Count} events)", headerStyle);

            timelineScroll = GUILayout.BeginScrollView(timelineScroll, cardStyle, GUILayout.Height(height - 40f));

            for (int i = 0; i < currentLog.entries.Count; i++)
            {
                var entry = currentLog.entries[i];
                string badgeColor = GetEventBadgeColor(entry.eventType);

                GUILayout.BeginHorizontal();
                GUILayout.Label($"<color=#00e5ff>[{entry.timestamp:F2}s]</color>", GUILayout.Width(75));
                GUILayout.Label($"<color={badgeColor}><b>[{entry.eventType}]</b></color>", GUILayout.Width(180));
                GUILayout.Label(entry.summary, labelSmallStyle);
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private string GetEventBadgeColor(string eventType)
        {
            switch (eventType)
            {
                case nameof(DroneSpawnedEvent): return "#ff9900";
                case nameof(DroneEnteredRangeEvent): return "#ffdd33";
                case nameof(ContactDetectedEvent): return "#00e5ff";
                case nameof(TraineeActionEvent): return "#33ff77";
                case nameof(DroneNeutralizedEvent): return "#cc66ff";
                case nameof(DroneImpactEvent): return "#ff3333";
                default: return "#cccccc";
            }
        }

        #endregion

        #region Tab 2: Per-Contact Breakdown Table

        private void DrawPerContactBreakdownTab(float width, float height)
        {
            if (currentReport == null) return;

            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label($"TARGET INTERACTION BREAKDOWN ({currentReport.contactRecords.Count} evaluated contacts)", headerStyle);

            // Table Header Bar
            GUILayout.BeginHorizontal(cardStyle);
            GUILayout.Label("Target / Contact", subHeaderStyle, GUILayout.Width(130));
            GUILayout.Label("Truth Type", subHeaderStyle, GUILayout.Width(140));
            GUILayout.Label("Detected", subHeaderStyle, GUILayout.Width(80));
            GUILayout.Label("Classified As", subHeaderStyle, GUILayout.Width(130));
            GUILayout.Label("Decision", subHeaderStyle, GUILayout.Width(90));
            GUILayout.Label("Correct?", subHeaderStyle, GUILayout.Width(120));
            GUILayout.Label("Latency", subHeaderStyle, GUILayout.Width(70));
            GUILayout.Label("Score", subHeaderStyle, GUILayout.Width(70));
            GUILayout.Label("ROE Assessment", subHeaderStyle);
            GUILayout.EndHorizontal();

            contactTableScroll = GUILayout.BeginScrollView(contactTableScroll, GUILayout.Height(height - 75f));

            for (int i = 0; i < currentReport.contactRecords.Count; i++)
            {
                var r = currentReport.contactRecords[i];
                string matchColor = GetMatchColor(r.classificationResult);
                string scoreColor = ColorToHex(GetScoreColor(r.compositeScore));

                GUILayout.BeginHorizontal(cardStyle);
                GUILayout.Label($"<b>{r.contactOrDroneId}</b>", labelSmallStyle, GUILayout.Width(130));
                GUILayout.Label(r.groundTruthThreatType, labelSmallStyle, GUILayout.Width(140));
                GUILayout.Label($"{r.firstAppearanceTime:F1}s", labelSmallStyle, GUILayout.Width(80));
                GUILayout.Label(r.traineeClassification.ToString(), labelSmallStyle, GUILayout.Width(130));
                GUILayout.Label(r.traineeResponse.ToString(), labelSmallStyle, GUILayout.Width(90));
                GUILayout.Label($"<color={matchColor}><b>{r.classificationResult}</b></color>", labelSmallStyle, GUILayout.Width(120));
                GUILayout.Label(r.detectionLatencySeconds < 900 ? $"{r.detectionLatencySeconds:F1}s" : "N/A", labelSmallStyle, GUILayout.Width(70));
                GUILayout.Label($"<color={scoreColor}><b>{r.compositeScore:F0}%</b></color>", labelSmallStyle, GUILayout.Width(70));
                GUILayout.Label(r.roeNotes, labelSmallStyle);
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private string GetMatchColor(ClassificationMatchResult match)
        {
            switch (match)
            {
                case ClassificationMatchResult.Correct: return "#55ff55";
                case ClassificationMatchResult.Partial: return "#ffdd33";
                case ClassificationMatchResult.FalseAlarmMisidentified: return "#ff8800";
                case ClassificationMatchResult.Wrong:
                default: return "#ff3333";
            }
        }

        #endregion

        #region Tab 3: Visual Decision Tree Branching Paths

        private void DrawDecisionTreePathsTab(float width, float height)
        {
            if (currentReport == null) return;

            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label("DECISION TREE EVALUATION: DETECTED? → CLASSIFIED? → ENGAGEMENT DECISION?", headerStyle);
            GUILayout.Label("Visualizes the sequential operational decision tree executed by the trainee for each airborne contact.", labelSmallStyle);
            GUILayout.Space(6);

            decisionTreeScroll = GUILayout.BeginScrollView(decisionTreeScroll, GUILayout.Height(height - 50f));

            for (int i = 0; i < currentReport.contactRecords.Count; i++)
            {
                var r = currentReport.contactRecords[i];
                DrawContactDecisionTreeCard(r);
                GUILayout.Space(8);
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawContactDecisionTreeCard(ContactScoreRecord r)
        {
            GUILayout.BeginVertical(cardStyle);

            // Contact Header Bar
            GUILayout.BeginHorizontal();
            string threatBadge = r.isHostile ? "<color=#ff4444>[HOSTILE THREAT]</color>" : "<color=#55ff55>[NON-HOSTILE / DECOY]</color>";
            if (r.isFalseAlarm) threatBadge = "<color=#ffaa00>[PHANTOM CLUTTER BLIP]</color>";

            GUILayout.Label($"<b>TARGET TRACK: <color=#00e5ff>{r.contactOrDroneId}</color></b> | Truth: <b>{r.groundTruthThreatType}</b> {threatBadge}", subHeaderStyle);
            GUILayout.FlexibleSpace();
            string finalOutcome = (r.compositeScore >= 75f) ? "<color=#55ff55>PASS</color>" : (r.compositeScore >= 50f ? "<color=#ffdd33>PARTIAL</color>" : "<color=#ff3333>FAIL</color>");
            GUILayout.Label($"DECISION-TREE OUTCOME: <b>{finalOutcome}</b> | Score: <b>{r.compositeScore:F0}%</b>", subHeaderStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // BRANCH 1: DETECTION PHASE
            bool detectedPass = (r.detectionScore >= 50f);
            string detectBadge = detectedPass ? "<color=#55ff55>[PASS]</color>" : "<color=#ff3333>[FAIL - TIMED OUT]</color>";
            string latencyInfo = r.detectionLatencySeconds < 900 ? $"{r.detectionLatencySeconds:F1}s (Score: {r.detectionScore:F0} pts)" : "Target Undetected / Ignored (0 pts)";
            GUILayout.Label($"  ├── <b>STEP 1: DETECTED?</b> ──▶ {detectBadge} First Acquired: {latencyInfo}", labelSmallStyle);

            // BRANCH 2: CLASSIFICATION PHASE
            bool classPass = (r.classificationResult == ClassificationMatchResult.Correct);
            bool classPartial = (r.classificationResult == ClassificationMatchResult.Partial);
            string classBadge = classPass ? "<color=#55ff55>[PASS]</color>" : (classPartial ? "<color=#ffdd33>[PARTIAL CREDIT]</color>" : "<color=#ff3333>[FAIL - WRONG TYPE]</color>");
            GUILayout.Label($"  ├── <b>STEP 2: CLASSIFIED?</b> ──▶ {classBadge} Trainee Input: \"<b>{r.traineeClassification}</b>\" vs Ground Truth: \"<b>{r.groundTruthThreatType}</b>\" ({r.classificationScore:F0} pts)", labelSmallStyle);

            // BRANCH 3: ENGAGEMENT & ROE PHASE
            bool roePass = (r.engagementScore > 0f);
            string roeBadge = roePass ? "<color=#55ff55>[PASS - ROE ADHERED]</color>" : "<color=#ff3333>[FAIL - ROE VIOLATION]</color>";
            GUILayout.Label($"  └── <b>STEP 3: DECISION (ROE)?</b> ──▶ {roeBadge} Action: \"<b>{r.traineeResponse}</b>\" | Assessment: {r.roeNotes} ({r.engagementScore:F0} pts)", labelSmallStyle);

            GUILayout.EndVertical();
        }

        #endregion

        #region Tab 3: Historical Trends & Profiles

        private void DrawTrendsAndProfilesTab(float width, float height)
        {
            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));

            GUILayout.BeginHorizontal();

            // LEFT COLUMN: Trends Over Last N Sessions
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width * 0.58f), GUILayout.Height(height - 15f));
            GUILayout.Label($"TRENDS OVER LAST {activeTrends.sessionCount} SESSIONS (Trainee: {currentReport?.traineeId})", headerStyle);
            GUILayout.Space(8);

            string deltaStr = activeTrends.scoreDeltaFirstToLast >= 0 
                ? $"<color=#55ff55>+{activeTrends.scoreDeltaFirstToLast:F1}% (IMPROVING)</color>" 
                : $"<color=#ff3333>{activeTrends.scoreDeltaFirstToLast:F1}% (REGRESSING)</color>";

            GUILayout.Label($"Performance Trajectory: <b>{deltaStr}</b>", subHeaderStyle);
            GUILayout.Space(6);

            DrawProgressBar("Moving Avg Overall Score", activeTrends.avgOverallScore, 100f, GetScoreColor(activeTrends.avgOverallScore));
            DrawProgressBar("Moving Avg Detection Speed", activeTrends.avgDetectionScore, 100f, GetScoreColor(activeTrends.avgDetectionScore));
            DrawProgressBar("Moving Avg Classification", activeTrends.avgClassificationScore, 100f, GetScoreColor(activeTrends.avgClassificationScore));
            DrawProgressBar("Moving Avg Engagement / ROE", activeTrends.avgEngagementScore, 100f, GetScoreColor(activeTrends.avgEngagementScore));

            GUILayout.Space(10);
            GUILayout.Label($"Classification Accuracy Rate: <b>{activeTrends.classificationAccuracyPct:F1}%</b>", subHeaderStyle);
            GUILayout.Label($"Cumulative ROE Breaches in Window: <b><color=#ff5555>{activeTrends.totalRoeViolations}</color></b>", subHeaderStyle);

            GUILayout.EndVertical();

            GUILayout.Space(12);

            // RIGHT COLUMN: Trainee & Unit Profiles
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width * 0.38f), GUILayout.Height(height - 15f));
            GUILayout.Label("TRAINEE & UNIT PROFILES", headerStyle);
            GUILayout.Space(6);

            historyScroll = GUILayout.BeginScrollView(historyScroll);

            if (historyManager.Database.profiles.Count == 0)
            {
                GUILayout.Label("No archived profiles yet. Finalize this session to save history.", labelSmallStyle);
            }
            else
            {
                for (int i = 0; i < historyManager.Database.profiles.Count; i++)
                {
                    var p = historyManager.Database.profiles[i];
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.Label($"<b>{p.traineeId}</b> ({p.unitId})", subHeaderStyle);
                    GUILayout.Label($"Sessions Completed: <b>{p.totalSessionsCompleted}</b> | Peak Score: <b>{p.highestScore:F1}%</b>", labelSmallStyle);
                    GUILayout.Label($"Career Avg: <b>{p.averageOverallScore:F1}%</b> | ROE Violations: <b>{p.totalRoeViolations}</b>", labelSmallStyle);
                    GUILayout.EndVertical();
                    GUILayout.Space(4);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        #endregion

        #region Tab 4: Export Reports to CSV

        private void DrawExportTab(float width, float height)
        {
            GUILayout.BeginVertical(cardStyle, GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label("AFTER-ACTION REVIEW EXPORT SUITE", headerStyle);
            GUILayout.Label("Export detailed session score records and historical trends to CSV for military debriefs, training audit logs, and external spreadsheets.", labelSmallStyle);
            GUILayout.Space(15);

            if (GUILayout.Button("EXPORT CURRENT SESSION BREAKDOWN (CSV)", tabButtonActiveStyle, GUILayout.Height(40)))
            {
                if (currentReport != null)
                {
                    string path = historyManager.ExportSessionToCsv(currentReport);
                    exportStatusMessage = $"Session CSV successfully exported to:\n{path}";
                }
                else
                {
                    exportStatusMessage = "Cannot export: No session report loaded.";
                }
            }

            GUILayout.Space(10);

            if (GUILayout.Button("EXPORT COMPLETE HISTORICAL DATABASE (CSV)", tabButtonStyle, GUILayout.Height(40)))
            {
                string path = historyManager.ExportHistoricalSummaryToCsv();
                exportStatusMessage = $"Historical Summary CSV successfully exported to:\n{path}";
            }

            GUILayout.Space(10);

            if (GUILayout.Button("ARCHIVE CURRENT SESSION TO HISTORY DATABASE", tabButtonStyle, GUILayout.Height(36)))
            {
                RecordCurrentSession();
            }

            GUILayout.Space(20);

            if (!string.IsNullOrEmpty(exportStatusMessage))
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"<color=#55ff55><b>STATUS NOTIFICATION:</b></color>\n{exportStatusMessage}", labelSmallStyle);
                GUILayout.EndVertical();
            }

            GUILayout.EndVertical();
        }

        #endregion

        #region Helpers & UI Drawing Primitives

        private void DrawProgressBar(string label, float value, float maxValue, Color fillColor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, labelSmallStyle, GUILayout.Width(180));

            Rect barRect = GUILayoutUtility.GetRect(250f, 18f);
            GUI.Box(barRect, GUIContent.none);

            float fillRatio = Mathf.Clamp01(value / Mathf.Max(1f, maxValue));
            Rect fillRect = new Rect(barRect.x + 1, barRect.y + 1, (barRect.width - 2) * fillRatio, barRect.height - 2);

            Color oldColor = GUI.color;
            GUI.color = fillColor;
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = oldColor;

            GUILayout.Label($"<b>{value:F1}%</b>", labelSmallStyle, GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        private Color GetScoreColor(float score)
        {
            if (score >= 80f) return new Color(0.2f, 0.9f, 0.3f);
            if (score >= 60f) return new Color(0.95f, 0.85f, 0.2f);
            return new Color(0.95f, 0.25f, 0.25f);
        }

        private string ColorToHex(Color color)
        {
            return $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++) pix[i] = color;
            Texture2D tex = new Texture2D(width, height);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private SessionLog CreateMockSessionForDemo()
        {
            var log = new SessionLog("SESSION-DEMO-01", "scenario_01_single_quad_day", "OPERATOR-TRAINEE");
            log.AddEntry(1.0f, nameof(DroneSpawnedEvent), "UAV-ALPHA-01", "Drone spawned at range 750m", 
                "{\"DroneId\":\"UAV-ALPHA-01\",\"ThreatType\":0,\"IsHostile\":true}");
            log.AddEntry(2.5f, nameof(ContactDetectedEvent), "UAV-ALPHA-01", "Radar detection established", 
                "{\"id\":\"UAV-ALPHA-01\",\"sensorType\":0,\"confidence\":0.88,\"isFalseAlarm\":false,\"trueDroneId\":\"UAV-ALPHA-01\"}");
            log.AddEntry(5.1f, nameof(TraineeActionEvent), "UAV-ALPHA-01", "Operator classified as Quadcopter", 
                "{\"Timestamp\":5.1,\"ContactId\":\"UAV-ALPHA-01\",\"ActionType\":1,\"Classification\":2,\"ThreatLevel\":3,\"Response\":0}");
            log.AddEntry(7.4f, nameof(TraineeActionEvent), "UAV-ALPHA-01", "Operator authorized Jamming countermeasure", 
                "{\"Timestamp\":7.4,\"ContactId\":\"UAV-ALPHA-01\",\"ActionType\":3,\"Classification\":2,\"ThreatLevel\":4,\"Response\":2}");
            log.AddEntry(8.8f, nameof(DroneNeutralizedEvent), "UAV-ALPHA-01", "Threat neutralized by RF Jammer", 
                "{\"DroneId\":\"UAV-ALPHA-01\",\"NeutralizedByEffector\":\"RF-Jammer\"}");
            log.FinalizeSession(15.0f);
            return log;
        }

        #endregion
    }
}
