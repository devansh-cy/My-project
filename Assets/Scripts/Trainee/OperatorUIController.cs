using System;
using System.Collections.Generic;
using UnityEngine;
using DroneTrainer.Core.Events;
using DroneTrainer.Drones;
using DroneTrainer.Sensors;

namespace DroneTrainer.Trainee
{
    /// <summary>
    /// Tracks operator assessments and tactical state for an individual contact.
    /// </summary>
    [Serializable]
    public class ContactAssessment
    {
        public Contact latestContact;
        public TraineeClassification classification = TraineeClassification.Unknown;
        public ThreatLevel threatLevel = ThreatLevel.None;
        public ResponseOption activeResponse = ResponseOption.Monitor;
        public float firstDetectedTime;
        public float lastSeenTime;

        public ContactAssessment(Contact contact, float time)
        {
            latestContact = contact;
            firstDetectedTime = time;
            lastSeenTime = time;
        }
    }

    /// <summary>
    /// Operator Command & Control (C2) Tactical Console.
    /// Features:
    /// - Live Contact Management & Radar/Optical Track Ingestion
    /// - NATO/DoD UAS Mil-Spec Group 1/2/3 classification
    /// - Collateral-aware ROE Response Authorization (Jammer, Net, Kinetic, Laser)
    /// - Real-time Picture-in-Picture (PiP) Thermal FLIR Gimbal Camera Viewport
    /// - Procedural Effector Visual & Drone Tumble/Crash Activation
    /// </summary>
    public class OperatorUIController : MonoBehaviour
    {
        public static OperatorUIController Instance { get; private set; }

        [Header("Tactical C2 UI Settings")]
        [SerializeField] private bool enableOnScreenC2Console = true;
        [SerializeField] private float staleContactTimeoutSeconds = 12f;
        [SerializeField] private KeyCode toggleUiKey = KeyCode.Tab;

        public void SetConsoleVisible(bool visible) => enableOnScreenC2Console = visible;
        public bool IsConsoleVisible => enableOnScreenC2Console;

        // Active contacts dictionary keyed by contact ID
        private readonly Dictionary<string, ContactAssessment> trackedAssessments = new Dictionary<string, ContactAssessment>();
        private readonly List<string> contactIdOrder = new List<string>();

        // Currently selected contact
        private string selectedContactId = null;
        private Vector2 contactScrollPos = Vector2.zero;

        // UI Styles
        private GUIStyle windowStyle;
        private GUIStyle headerStyle;
        private GUIStyle buttonNormalStyle;
        private GUIStyle buttonSelectedStyle;
        private GUIStyle statusStyle;
        private GUIStyle flirBoxStyle;
        private bool stylesInitialized = false;

        public string SelectedContactId => selectedContactId;
        public IReadOnlyDictionary<string, ContactAssessment> TrackedAssessments => trackedAssessments;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ContactDetectedEvent>(OnContactDetected);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ContactDetectedEvent>(OnContactDetected);
        }

        private void Update()
        {
            if (DroneTrainer.Core.InputHelper.WasKeyPressed(toggleUiKey))
            {
                enableOnScreenC2Console = !enableOnScreenC2Console;
            }

            PruneStaleContacts();
            UpdateGimbalCameraTracking();
        }

        private void UpdateGimbalCameraTracking()
        {
            if (FLIRGimbalCamera.Instance == null) return;

            if (!string.IsNullOrEmpty(selectedContactId) && trackedAssessments.TryGetValue(selectedContactId, out var assessment))
            {
                DroneAgent targetDrone = FindTargetDrone(assessment.latestContact);
                FLIRGimbalCamera.Instance.TrackWorldPosition(
                    assessment.latestContact.estimatedPosition,
                    targetDrone != null ? targetDrone.transform : null
                );
            }
            else
            {
                FLIRGimbalCamera.Instance.ClearTarget();
            }
        }

        #region Event Handling & Contact Ingestion

        private void OnContactDetected(ContactDetectedEvent evt)
        {
            if (evt.Contact == null || string.IsNullOrEmpty(evt.Contact.id)) return;

            string id = evt.Contact.id;
            float now = evt.SimulationTime;

            if (trackedAssessments.TryGetValue(id, out var assessment))
            {
                assessment.latestContact = evt.Contact;
                assessment.lastSeenTime = now;
            }
            else
            {
                assessment = new ContactAssessment(evt.Contact, now);
                trackedAssessments[id] = assessment;

                if (!evt.Contact.isFalseAlarm)
                {
                    // Prioritize real aerial tracks at the top
                    int firstClutter = contactIdOrder.FindIndex(cid => trackedAssessments.TryGetValue(cid, out var a) && a.latestContact.isFalseAlarm);
                    if (firstClutter >= 0) contactIdOrder.Insert(firstClutter, id);
                    else contactIdOrder.Insert(0, id);

                    // Auto-lock real drone if none selected or currently locked on clutter
                    if (string.IsNullOrEmpty(selectedContactId))
                    {
                        SelectContact(id);
                    }
                    else if (trackedAssessments.TryGetValue(selectedContactId, out var curAss) && curAss.latestContact.isFalseAlarm)
                    {
                        SelectContact(id);
                    }
                }
                else
                {
                    contactIdOrder.Add(id);
                }
            }
        }

        private void PruneStaleContacts()
        {
            float now = Time.time;
            for (int i = contactIdOrder.Count - 1; i >= 0; i--)
            {
                string id = contactIdOrder[i];
                if (trackedAssessments.TryGetValue(id, out var assessment))
                {
                    float timeout = assessment.latestContact.isFalseAlarm ? 2.5f : staleContactTimeoutSeconds;
                    if (now - assessment.lastSeenTime > timeout)
                    {
                        trackedAssessments.Remove(id);
                        contactIdOrder.RemoveAt(i);
                        if (selectedContactId == id)
                        {
                            selectedContactId = contactIdOrder.Count > 0 ? contactIdOrder[0] : null;
                        }
                    }
                }
            }
        }

        #endregion

        #region Operator Actions & Countermeasures

        public void SelectContact(string contactId)
        {
            if (!trackedAssessments.ContainsKey(contactId)) return;
            selectedContactId = contactId;

            ContactAssessment assessment = trackedAssessments[contactId];
            PublishTraineeAction(
                contactId: contactId,
                actionType: TraineeActionType.SelectContact,
                classification: assessment.classification,
                threatLevel: assessment.threatLevel,
                response: assessment.activeResponse,
                details: $"Selected contact {contactId} ({assessment.latestContact.sensorType})"
            );
        }

        public void SetClassification(TraineeClassification classification)
        {
            if (string.IsNullOrEmpty(selectedContactId) || !trackedAssessments.TryGetValue(selectedContactId, out var assessment)) return;

            assessment.classification = classification;
            PublishTraineeAction(
                contactId: selectedContactId,
                actionType: TraineeActionType.ClassifyTarget,
                classification: classification,
                threatLevel: assessment.threatLevel,
                response: assessment.activeResponse,
                details: $"Classified {selectedContactId} as {classification}"
            );
        }

        public void SetThreatLevel(ThreatLevel threatLevel)
        {
            if (string.IsNullOrEmpty(selectedContactId) || !trackedAssessments.TryGetValue(selectedContactId, out var assessment)) return;

            assessment.threatLevel = threatLevel;
            PublishTraineeAction(
                contactId: selectedContactId,
                actionType: TraineeActionType.SetThreatLevel,
                classification: assessment.classification,
                threatLevel: threatLevel,
                response: assessment.activeResponse,
                details: $"Assigned threat level {threatLevel} to {selectedContactId}"
            );
        }

        /// <summary>
        /// Phase 1 & 3: Authorizes tactical countermeasure (Monitor, Alert, Jammer, Net, Kinetic, Laser).
        /// Deepens engagement scoring and fires procedural VFX with aerodynamic crash tumbling.
        /// </summary>
        public void ExecuteResponse(ResponseOption response)
        {
            if (string.IsNullOrEmpty(selectedContactId) || !trackedAssessments.TryGetValue(selectedContactId, out var assessment))
            {
                Debug.LogWarning("[OperatorUI] Cannot execute response: No contact selected.");
                return;
            }

            assessment.activeResponse = response;
            Contact contact = assessment.latestContact;
            DroneAgent targetDrone = FindTargetDrone(contact);

            // Phase 1: Trigger procedural effector visual
            if (EffectorVisualController.Instance != null)
            {
                EffectorVisualController.Instance.TriggerEffector(
                    response,
                    contact.estimatedPosition,
                    targetDrone != null ? targetDrone.gameObject : null
                );
            }

            // Phase 1: Neutralize hostile drone target with aerodynamic tumble & smoke
            if ((response == ResponseOption.Jam || response == ResponseOption.Net || 
                 response == ResponseOption.Kinetic || response == ResponseOption.Laser) && targetDrone != null)
            {
                targetDrone.Neutralize(response);
            }

            PublishTraineeAction(
                contactId: selectedContactId,
                actionType: TraineeActionType.ExecuteResponse,
                classification: assessment.classification,
                threatLevel: assessment.threatLevel,
                response: response,
                details: $"Operator authorized {response} countermeasure."
            );
        }

        private DroneAgent FindTargetDrone(Contact contact)
        {
            if (contact == null) return null;
            var allDrones = FindObjectsByType<DroneAgent>(FindObjectsSortMode.None);

            if (!string.IsNullOrEmpty(contact.trueDroneId))
            {
                foreach (var d in allDrones)
                {
                    if (d.name.Contains(contact.trueDroneId) || d.name == contact.trueDroneId)
                        return d;
                }
            }

            // Fallback proximity match
            DroneAgent closest = null;
            float minDist = 40f;
            foreach (var d in allDrones)
            {
                float dist = Vector3.Distance(d.transform.position, contact.estimatedPosition);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = d;
                }
            }
            return closest;
        }

        private void PublishTraineeAction(string contactId, TraineeActionType actionType, 
                                          TraineeClassification classification, ThreatLevel threatLevel, 
                                          ResponseOption response, string details)
        {
            float simTime = Time.time;
            TraineeActionEvent actionEvent = new TraineeActionEvent(
                timestamp: simTime,
                contactId: contactId,
                actionType: actionType,
                classification: classification,
                threatLevel: threatLevel,
                response: response,
                details: details
            );

            EventBus.Publish(actionEvent);
            Debug.Log(actionEvent.ToString());
        }

        #endregion

        #region Built-in Tactical C2 GUI Console

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = MakeTexture(2, 2, new Color(0.06f, 0.08f, 0.12f, 0.94f));

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = new Color(0f, 0.9f, 1f);

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                richText = true
            };
            statusStyle.normal.textColor = Color.white;

            buttonNormalStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            buttonNormalStyle.normal.textColor = Color.white;

            buttonSelectedStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            buttonSelectedStyle.normal.background = MakeTexture(2, 2, new Color(0f, 0.55f, 0.85f, 1f));
            buttonSelectedStyle.normal.textColor = Color.white;

            flirBoxStyle = new GUIStyle(GUI.skin.box);
            flirBoxStyle.normal.background = MakeTexture(2, 2, new Color(0.04f, 0.06f, 0.05f, 1f));

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!enableOnScreenC2Console) return;

            InitializeStyles();

            float screenW = Screen.width;
            float screenH = Screen.height;

            float windowW = Mathf.Min(1180f, screenW - 20f);
            float windowH = 345f;
            float windowX = (screenW - windowW) * 0.5f;
            float windowY = screenH - windowH - 15f;

            Rect consoleRect = new Rect(windowX, windowY, windowW, windowH);
            GUILayout.BeginArea(consoleRect, "OPERATOR C-UAS COMMAND & CONTROL CONSOLE (TAB to Toggle)", windowStyle);
            GUILayout.Space(22);

            GUILayout.BeginHorizontal();

            // 1. LEFT COLUMN: Active Track List
            DrawContactListPanel(windowW * 0.28f, windowH - 35f);

            GUILayout.Space(10);

            // 2. CENTER & RIGHT: Target Telemetry, Controls & Live PiP FLIR Viewport
            DrawClassificationAndResponsePanel(windowW * 0.70f, windowH - 35f);

            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawContactListPanel(float width, float height)
        {
            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label($"ACTIVE CONTACTS ({trackedAssessments.Count})", headerStyle);

            contactScrollPos = GUILayout.BeginScrollView(contactScrollPos, GUILayout.Height(height - 35f));

            if (contactIdOrder.Count == 0)
            {
                GUILayout.Label("<color=#888888>No active sensor contacts detected.</color>", statusStyle);
            }
            else
            {
                for (int i = 0; i < contactIdOrder.Count; i++)
                {
                    string id = contactIdOrder[i];
                    if (!trackedAssessments.TryGetValue(id, out var assessment)) continue;

                    Contact c = assessment.latestContact;
                    bool isSelected = (id == selectedContactId);
                    GUIStyle btnStyle = isSelected ? buttonSelectedStyle : buttonNormalStyle;

                    string threatColor = GetThreatHexColor(assessment.threatLevel);
                    string trackTypeTag = c.isFalseAlarm 
                        ? "<color=#ffaa00>[CLUTTER]</color>" 
                        : $"<color=#00e5ff>[{GetMilSpecShort(c)}]</color>";

                    string label = $"{trackTypeTag} [{c.sensorType}] <b>{c.id}</b>\nClass: {assessment.classification} | <color={threatColor}>{assessment.threatLevel}</color> | Conf: {c.confidence:P0}";

                    if (GUILayout.Button(label, btnStyle, GUILayout.Height(46)))
                    {
                        SelectContact(id);
                    }
                    GUILayout.Space(2);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawClassificationAndResponsePanel(float width, float height)
        {
            GUILayout.BeginVertical(GUILayout.Width(width), GUILayout.Height(height));

            if (string.IsNullOrEmpty(selectedContactId) || !trackedAssessments.TryGetValue(selectedContactId, out var assessment))
            {
                GUILayout.Label("NO CONTACT SELECTED", headerStyle);
                GUILayout.Label("Select an aerial or radar track from the list to acquire FLIR lock, inspect telemetry and authorize countermeasures.", statusStyle);
                GUILayout.EndVertical();
                return;
            }

            Contact contact = assessment.latestContact;
            DroneAgent targetDrone = FindTargetDrone(contact);

            // Phase 5: Mil-Spec Group Label
            string milSpecTitle = GetMilSpecFull(targetDrone, contact);

            // Top Telemetry Header Bar
            GUILayout.BeginHorizontal();
            string targetBadge = contact.isFalseAlarm ? "<color=#ffaa00>[CLUTTER NOISE]</color>" : $"<color=#00e5ff>[{milSpecTitle}]</color>";
            GUILayout.Label($"LOCK: {targetBadge} <b>{contact.id}</b> ({contact.sensorType})", headerStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Pos: ({contact.estimatedPosition.x:F0}, {contact.estimatedPosition.y:F0}, {contact.estimatedPosition.z:F0})m | Spd: {contact.estimatedVelocity.magnitude:F1}m/s | SNR: {contact.snrDb:F1}dB", statusStyle);
            GUILayout.EndHorizontal();

            // Phase 3: Urban ROE Advisory Banner
            bool isUrbanZone = FindAnyObjectByType<DroneTrainer.Environment.UrbanEnvironmentBuilder>() != null;
            if (isUrbanZone)
            {
                GUILayout.Label("<color=#55ffaa><b>ROE DIRECTIVE: URBAN SECTOR</b> — Non-kinetic (Jammer/Net) authorized. Kinetic strikes cause severe collateral violation!</color>", statusStyle);
            }

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();

            // LEFT SUB-COLUMN: Controls (Classification, Threat, Responses)
            float controlsWidth = width - 230f;
            GUILayout.BeginVertical(GUILayout.Width(controlsWidth));

            // 1. CLASSIFICATION ROW
            GUILayout.Label("1. TARGET CLASSIFICATION:", headerStyle);
            GUILayout.BeginHorizontal();
            TraineeClassification[] classes = (TraineeClassification[])Enum.GetValues(typeof(TraineeClassification));
            foreach (var cls in classes)
            {
                bool isCurrent = (assessment.classification == cls);
                GUIStyle style = isCurrent ? buttonSelectedStyle : buttonNormalStyle;
                if (GUILayout.Button(cls.ToString(), style, GUILayout.Height(28)))
                {
                    SetClassification(cls);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 2. THREAT SEVERITY ROW
            GUILayout.Label("2. THREAT SEVERITY LEVEL:", headerStyle);
            GUILayout.BeginHorizontal();
            ThreatLevel[] levels = (ThreatLevel[])Enum.GetValues(typeof(ThreatLevel));
            foreach (var lvl in levels)
            {
                bool isCurrent = (assessment.threatLevel == lvl);
                GUI.color = GetThreatColor(lvl);
                GUIStyle style = isCurrent ? buttonSelectedStyle : buttonNormalStyle;
                if (GUILayout.Button(lvl.ToString(), style, GUILayout.Height(26)))
                {
                    SetThreatLevel(lvl);
                }
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // 3. TACTICAL COUNTERMEASURES (Phase 1 & 3)
            GUILayout.Label("3. COUNTERMEASURE AUTHORIZATION:", headerStyle);
            GUILayout.BeginHorizontal();

            ResponseOption[] distinctOptions = new[]
            {
                ResponseOption.Monitor,
                ResponseOption.Alert,
                ResponseOption.Jam,
                ResponseOption.Net,
                ResponseOption.Kinetic,
                ResponseOption.Laser
            };

            foreach (var resp in distinctOptions)
            {
                bool isCurrent = (assessment.activeResponse == resp);
                GUI.color = GetResponseColor(resp);
                GUIStyle style = isCurrent ? buttonSelectedStyle : buttonNormalStyle;

                string buttonTitle = GetResponseButtonText(resp, isCurrent);
                if (GUILayout.Button(buttonTitle, style, GUILayout.Height(34)))
                {
                    ExecuteResponse(resp);
                }
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.Space(8);

            // RIGHT SUB-COLUMN: Phase 2 Picture-in-Picture (PiP) Thermal FLIR Viewport
            DrawFlirViewport(215f, 220f, contact);

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        /// <summary>
        /// Phase 2: Live Picture-in-Picture (PiP) Thermal FLIR Viewport with authentic military HUD reticle.
        /// </summary>
        private void DrawFlirViewport(float vpWidth, float vpHeight, Contact contact)
        {
            GUILayout.BeginVertical(GUILayout.Width(vpWidth), GUILayout.Height(vpHeight));
            GUILayout.Label("LIVE FLIR THERMAL GIMBAL", headerStyle);

            Rect flirRect = GUILayoutUtility.GetRect(vpWidth, vpHeight - 24f, flirBoxStyle);

            if (FLIRGimbalCamera.Instance != null && FLIRGimbalCamera.Instance.FlirTexture != null)
            {
                GUI.DrawTexture(flirRect, FLIRGimbalCamera.Instance.FlirTexture, ScaleMode.StretchToFill);

                // Draw Military FLIR HUD Reticle
                bool hasLock = FLIRGimbalCamera.Instance.HasLock;
                string lockBadge = hasLock 
                    ? "<color=#00ff88><b>[TRACK LOCK: ACQUIRED]</b></color>" 
                    : "<color=#ffaa00>[SLEWING GIMBAL]</color>";

                // Top Reticle Header
                GUI.Label(new Rect(flirRect.x + 6, flirRect.y + 4, flirRect.width - 12, 18), lockBadge, statusStyle);
                GUI.Label(new Rect(flirRect.x + 6, flirRect.y + 20, flirRect.width - 12, 16), "<color=#88ff88>MODE: FLIR W-HOT (8-14μm)</color>", statusStyle);

                // Center Reticle Crosshairs (+)
                float cx = flirRect.x + flirRect.width * 0.5f;
                float cy = flirRect.y + flirRect.height * 0.5f;
                GUI.color = hasLock ? new Color(0f, 1f, 0.4f, 0.9f) : new Color(1f, 0.7f, 0f, 0.8f);
                GUI.Box(new Rect(cx - 15, cy - 1, 30, 2), GUIContent.none);
                GUI.Box(new Rect(cx - 1, cy - 15, 2, 30), GUIContent.none);

                // Corner Brackets
                GUI.Box(new Rect(cx - 24, cy - 24, 8, 2), GUIContent.none);
                GUI.Box(new Rect(cx - 24, cy - 24, 2, 8), GUIContent.none);
                GUI.Box(new Rect(cx + 16, cy - 24, 8, 2), GUIContent.none);
                GUI.Box(new Rect(cx + 22, cy - 24, 2, 8), GUIContent.none);
                GUI.Box(new Rect(cx - 24, cy + 22, 8, 2), GUIContent.none);
                GUI.Box(new Rect(cx - 24, cy + 16, 2, 8), GUIContent.none);
                GUI.Box(new Rect(cx + 16, cy + 22, 8, 2), GUIContent.none);
                GUI.Box(new Rect(cx + 22, cy + 16, 2, 8), GUIContent.none);
                GUI.color = Color.white;

                // Telemetry Footer
                string azEl = $"AZ:{FLIRGimbalCamera.Instance.Azimuth:+000;-000}° EL:{FLIRGimbalCamera.Instance.Elevation:+00;-00}°";
                string rng = $"RNG:{FLIRGimbalCamera.Instance.SlantRange:F0}m";
                GUI.Label(new Rect(flirRect.x + 6, flirRect.yMax - 34, flirRect.width - 12, 16), $"<color=#00e5ff>{azEl}</color>", statusStyle);
                GUI.Label(new Rect(flirRect.x + 6, flirRect.yMax - 18, flirRect.width - 12, 16), $"<color=#00e5ff>{rng} | FOV: 4.2x</color>", statusStyle);
            }
            else
            {
                GUI.Label(new Rect(flirRect.x + 10, flirRect.y + 40, flirRect.width - 20, 40), "<color=#888888>[STANDBY: INITIALIZING EO/IR TURRET FEED]</color>", statusStyle);
            }

            GUILayout.EndVertical();
        }

        private string GetMilSpecShort(Contact c)
        {
            if (c.isFalseAlarm) return "CLUTTER";
            return "GRP 1";
        }

        private string GetMilSpecFull(DroneAgent agent, Contact contact)
        {
            if (contact.isFalseAlarm) return "RADAR NOISE / CLUTTER";
            if (agent != null)
            {
                switch (agent.MilSpecGroup)
                {
                    case UasMilSpecGroup.Group1_Micro: return "GROUP 1: MICRO (<20 lbs)";
                    case UasMilSpecGroup.Group2_Tactical: return "GROUP 2: TACTICAL (21-55 lbs)";
                    case UasMilSpecGroup.Group3_Medium: return "GROUP 3: MEDIUM (56-1320 lbs)";
                    case UasMilSpecGroup.BiologicalDecoy: return "BIOLOGICAL WILDLIFE DECOY";
                }
            }
            return "GROUP 1: MICRO TACTICAL UAS";
        }

        private string GetResponseButtonText(ResponseOption option, bool isCurrent)
        {
            string prefix = isCurrent ? "● " : "";
            switch (option)
            {
                case ResponseOption.Monitor: return $"{prefix}Monitor";
                case ResponseOption.Alert: return $"{prefix}Alert";
                case ResponseOption.Jam: return $"{prefix}Jam (EW)";
                case ResponseOption.Net: return $"{prefix}Net (Catch)";
                case ResponseOption.Kinetic: return $"{prefix}Kinetic (Strike)";
                case ResponseOption.Laser: return $"{prefix}Laser (DEW)";
                default: return $"{prefix}{option}";
            }
        }

        private Color GetThreatColor(ThreatLevel level)
        {
            switch (level)
            {
                case ThreatLevel.None: return new Color(0.7f, 0.7f, 0.7f);
                case ThreatLevel.Low: return new Color(0.4f, 0.9f, 0.4f);
                case ThreatLevel.Medium: return new Color(1f, 0.85f, 0.2f);
                case ThreatLevel.High: return new Color(1f, 0.5f, 0.1f);
                case ThreatLevel.Critical: return new Color(1f, 0.2f, 0.2f);
                default: return Color.white;
            }
        }

        private string GetThreatHexColor(ThreatLevel level)
        {
            switch (level)
            {
                case ThreatLevel.None: return "#aaaaaa";
                case ThreatLevel.Low: return "#55ff55";
                case ThreatLevel.Medium: return "#ffdd33";
                case ThreatLevel.High: return "#ff8822";
                case ThreatLevel.Critical: return "#ff3333";
                default: return "#ffffff";
            }
        }

        private Color GetResponseColor(ResponseOption response)
        {
            switch (response)
            {
                case ResponseOption.Monitor: return new Color(0.6f, 0.8f, 1f);
                case ResponseOption.Alert: return new Color(1f, 0.85f, 0.2f);
                case ResponseOption.Jam: return new Color(0.9f, 0.4f, 1f);
                case ResponseOption.Net: return new Color(0.1f, 0.9f, 0.8f);
                case ResponseOption.Kinetic: return new Color(1f, 0.25f, 0.25f);
                case ResponseOption.Laser: return new Color(1f, 0.75f, 0.1f);
                default: return Color.white;
            }
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
