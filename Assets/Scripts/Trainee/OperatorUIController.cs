using System;
using System.Collections.Generic;
using UnityEngine;
using DroneTrainer.Core.Events;
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
    /// Operator Command & Control (C2) User Interface.
    /// Manages real-time contact selection, classification panels (Bird, Quadcopter, Fixed-wing,
    /// Loitering munition, Swarm, Unknown), threat severity ratings, and tactical response execution
    /// (Monitor, Alert, Jam, Engage).
    /// Publishes timestamped TraineeActionEvent payloads to the EventBus on every interaction.
    /// Includes an immediate-mode tactical C2 GUI for turnkey in-editor and standalone testing.
    /// </summary>
    public class OperatorUIController : MonoBehaviour
    {
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
        private Vector2 detailsScrollPos = Vector2.zero;

        // UI Styles
        private GUIStyle windowStyle;
        private GUIStyle headerStyle;
        private GUIStyle buttonNormalStyle;
        private GUIStyle buttonSelectedStyle;
        private GUIStyle statusStyle;
        private bool stylesInitialized = false;

        public string SelectedContactId => selectedContactId;
        public IReadOnlyDictionary<string, ContactAssessment> TrackedAssessments => trackedAssessments;

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
                contactIdOrder.Insert(0, id);

                // Auto-select first contact if none selected
                if (string.IsNullOrEmpty(selectedContactId))
                {
                    SelectContact(id);
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
                    if (now - assessment.lastSeenTime > staleContactTimeoutSeconds)
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

        #region Trainee Actions & Event Publishing

        /// <summary>
        /// Selects a contact for active inspection and opens its classification/response panel.
        /// </summary>
        public void SelectContact(string contactId)
        {
            if (string.IsNullOrEmpty(contactId) || !trackedAssessments.ContainsKey(contactId))
            {
                selectedContactId = null;
                return;
            }

            selectedContactId = contactId;
            var assessment = trackedAssessments[contactId];

            PublishTraineeAction(
                contactId: contactId,
                actionType: TraineeActionType.SelectContact,
                classification: assessment.classification,
                threatLevel: assessment.threatLevel,
                response: assessment.activeResponse,
                details: $"Selected contact {contactId} ({assessment.latestContact.sensorType})"
            );
        }

        /// <summary>
        /// Applies an operator target classification to the currently selected contact.
        /// </summary>
        public void SetClassification(TraineeClassification classification)
        {
            if (string.IsNullOrEmpty(selectedContactId) || !trackedAssessments.TryGetValue(selectedContactId, out var assessment))
            {
                Debug.LogWarning("[OperatorUI] Cannot classify target: No contact selected.");
                return;
            }

            assessment.classification = classification;

            PublishTraineeAction(
                contactId: selectedContactId,
                actionType: TraineeActionType.ClassifyTarget,
                classification: classification,
                threatLevel: assessment.threatLevel,
                response: assessment.activeResponse,
                details: $"Operator classified contact as {classification}"
            );
        }

        /// <summary>
        /// Assigns a threat severity rating to the currently selected contact.
        /// </summary>
        public void SetThreatLevel(ThreatLevel level)
        {
            if (string.IsNullOrEmpty(selectedContactId) || !trackedAssessments.TryGetValue(selectedContactId, out var assessment))
            {
                Debug.LogWarning("[OperatorUI] Cannot set threat level: No contact selected.");
                return;
            }

            assessment.threatLevel = level;

            PublishTraineeAction(
                contactId: selectedContactId,
                actionType: TraineeActionType.SetThreatLevel,
                classification: assessment.classification,
                threatLevel: level,
                response: assessment.activeResponse,
                details: $"Operator set threat level to {level}"
            );
        }

        /// <summary>
        /// Executes a tactical response (Monitor, Alert, Jam, Engage) against the selected contact.
        /// </summary>
        public void ExecuteResponse(ResponseOption response)
        {
            if (string.IsNullOrEmpty(selectedContactId) || !trackedAssessments.TryGetValue(selectedContactId, out var assessment))
            {
                Debug.LogWarning("[OperatorUI] Cannot execute response: No contact selected.");
                return;
            }

            assessment.activeResponse = response;

            PublishTraineeAction(
                contactId: selectedContactId,
                actionType: TraineeActionType.ExecuteResponse,
                classification: assessment.classification,
                threatLevel: assessment.threatLevel,
                response: response,
                details: $"Operator executed response: {response}"
            );
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
            windowStyle.normal.background = MakeTexture(2, 2, new Color(0.08f, 0.10f, 0.14f, 0.92f));

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
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
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            buttonNormalStyle.normal.textColor = Color.white;

            buttonSelectedStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            buttonSelectedStyle.normal.background = MakeTexture(2, 2, new Color(0f, 0.6f, 0.8f, 1f));
            buttonSelectedStyle.normal.textColor = Color.white;

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            if (!enableOnScreenC2Console) return;

            InitializeStyles();

            float screenW = Screen.width;
            float screenH = Screen.height;

            // Draw Operator C2 Console Window across bottom third
            float windowW = Mathf.Min(1100f, screenW - 40f);
            float windowH = 310f;
            float windowX = (screenW - windowW) * 0.5f;
            float windowY = screenH - windowH - 20f;

            Rect consoleRect = new Rect(windowX, windowY, windowW, windowH);
            GUILayout.BeginArea(consoleRect, "OPERATOR C-UAS COMMAND & CONTROL CONSOLE (Press TAB to Toggle)", windowStyle);
            GUILayout.Space(25);

            GUILayout.BeginHorizontal();

            // 1. LEFT COLUMN: Active Track List
            DrawContactListPanel(windowW * 0.32f, windowH - 45f);

            GUILayout.Space(12);

            // 2. CENTER & RIGHT COLUMN: Target Classification & Response Panel
            DrawClassificationAndResponsePanel(windowW * 0.65f, windowH - 45f);

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
                    string label = $"[{c.sensorType}] {c.id}\nClass: {assessment.classification} | <color={threatColor}>{assessment.threatLevel}</color> | Conf: {c.confidence:P0}";

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
                GUILayout.Label("Select a radar or optical track from the active contact list to inspect telemetry and authorize countermeasures.", statusStyle);
                GUILayout.EndVertical();
                return;
            }

            Contact contact = assessment.latestContact;

            // Header Telemetry Bar
            GUILayout.BeginHorizontal();
            GUILayout.Label($"TARGET LOCK: <color=#00e5ff>{contact.id}</color> ({contact.sensorType})", headerStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Est Pos: ({contact.estimatedPosition.x:F0}, {contact.estimatedPosition.y:F0}, {contact.estimatedPosition.z:F0}) m | Speed: {contact.estimatedVelocity.magnitude:F1} m/s | SNR: {contact.snrDb:F1} dB", statusStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // 1. CLASSIFICATION ROW
            GUILayout.Label("1. TARGET CLASSIFICATION:", headerStyle);
            GUILayout.BeginHorizontal();
            TraineeClassification[] classes = (TraineeClassification[])Enum.GetValues(typeof(TraineeClassification));
            foreach (var cls in classes)
            {
                bool isCurrent = (assessment.classification == cls);
                GUIStyle style = isCurrent ? buttonSelectedStyle : buttonNormalStyle;

                if (GUILayout.Button(cls.ToString(), style, GUILayout.Height(30)))
                {
                    SetClassification(cls);
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // 2. THREAT LEVEL ROW
            GUILayout.Label("2. THREAT SEVERITY LEVEL:", headerStyle);
            GUILayout.BeginHorizontal();
            ThreatLevel[] levels = (ThreatLevel[])Enum.GetValues(typeof(ThreatLevel));
            foreach (var lvl in levels)
            {
                bool isCurrent = (assessment.threatLevel == lvl);
                GUI.color = GetThreatColor(lvl);
                GUIStyle style = isCurrent ? buttonSelectedStyle : buttonNormalStyle;

                if (GUILayout.Button(lvl.ToString(), style, GUILayout.Height(28)))
                {
                    SetThreatLevel(lvl);
                }
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 3. TACTICAL RESPONSE ROW
            GUILayout.Label("3. COUNTERMEASURE ENGAGEMENT RESPONSE:", headerStyle);
            GUILayout.BeginHorizontal();
            ResponseOption[] responses = (ResponseOption[])Enum.GetValues(typeof(ResponseOption));
            foreach (var resp in responses)
            {
                bool isCurrent = (assessment.activeResponse == resp);
                GUI.color = GetResponseColor(resp);
                GUIStyle style = isCurrent ? buttonSelectedStyle : buttonNormalStyle;

                string buttonTitle = isCurrent ? $"[ACTIVE: {resp}]" : resp.ToString();
                if (GUILayout.Button(buttonTitle, style, GUILayout.Height(36)))
                {
                    ExecuteResponse(resp);
                }
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
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
                case ResponseOption.Jam: return new Color(1f, 0.5f, 0.9f);
                case ResponseOption.Engage: return new Color(1f, 0.25f, 0.25f);
                default: return Color.white;
            }
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        #endregion
    }
}
