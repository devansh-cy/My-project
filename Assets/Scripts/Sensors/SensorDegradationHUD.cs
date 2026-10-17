using System.Collections.Generic;
using UnityEngine;
using DroneTrainer.Core.Events;
using DroneTrainer.Scenarios;

namespace DroneTrainer.Sensors
{
    /// <summary>
    /// Tactical HUD displaying on-screen sensor degradation indicators,
    /// active electronic warfare (RF Jamming) alerts, dynamic radar PPI scope with clutter dots,
    /// and optical noise / FLIR thermal mode indicators.
    /// </summary>
    public class SensorDegradationHUD : MonoBehaviour
    {
        [Header("Active Environmental Profile")]
        [SerializeField] private SensorProfile sensorProfile = new SensorProfile();
        [SerializeField] private bool showRadarScope = true;
        [SerializeField] private bool showJammingBanner = true;

        [Header("References")]
        [SerializeField] private RadarSim radarSim;
        [SerializeField] private EOCameraSim eoCameraSim;

        // UI Textures & Styles
        private Texture2D radarScopeBg;
        private Texture2D sweepLineTexture;
        private Texture2D clutterDotTexture;
        private Texture2D targetBlipTexture;
        private GUIStyle alertBannerStyle;
        private GUIStyle labelStyle;
        private bool stylesInitialized = false;

        // Active contacts for radar scope
        private readonly List<Contact> activeContacts = new List<Contact>();
        private float sweepAngle = 0f;

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
            // Sync with active radar or camera profile if available
            if (radarSim != null && radarSim.ActiveProfile != null)
            {
                sensorProfile = radarSim.ActiveProfile;
            }
            else if (eoCameraSim != null && eoCameraSim.ActiveProfile != null)
            {
                sensorProfile = eoCameraSim.ActiveProfile;
            }

            // Animate radar sweep
            sweepAngle = (sweepAngle + 180f * Time.deltaTime) % 360f;

            // Prune old contacts for radar scope display
            float now = Time.time;
            activeContacts.RemoveAll(c => (now - c.timestamp) > 2.5f);
        }

        private void OnContactDetected(ContactDetectedEvent evt)
        {
            if (evt.Contact == null) return;
            activeContacts.RemoveAll(c => c.id == evt.Contact.id);
            activeContacts.Add(evt.Contact);
        }

        #region HUD Rendering

        private void InitializeStyles()
        {
            if (stylesInitialized) return;

            radarScopeBg = MakeCircularTexture(160, new Color(0.02f, 0.08f, 0.05f, 0.85f), new Color(0f, 0.6f, 0.3f, 1f));
            sweepLineTexture = MakeSolidTexture(2, 2, new Color(0.2f, 1f, 0.4f, 0.9f));
            clutterDotTexture = MakeSolidTexture(3, 3, new Color(0.9f, 0.7f, 0.2f, 0.7f));
            targetBlipTexture = MakeSolidTexture(5, 5, new Color(1f, 0.2f, 0.2f, 1f));

            alertBannerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            alertBannerStyle.normal.textColor = Color.white;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = new Color(0f, 0.9f, 0.5f);

            stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            float screenW = Screen.width;

            // 1. ELECTRONIC WARFARE JAMMING ALERT BANNER
            if (showJammingBanner && sensorProfile.rfJammingLevel > 0.05f)
            {
                DrawJammingBanner(screenW);
            }

            // 2. RADAR PPI MINI-SCOPE
            if (showRadarScope)
            {
                DrawRadarMiniScope(screenW - 190f, 20f);
            }

            // 3. OPTICAL / THERMAL STATUS
            DrawSensorStatusWidget(20f, 20f);
        }

        private void DrawJammingBanner(float screenW)
        {
            float bannerW = 420f;
            float bannerH = 34f;
            float bannerX = (screenW - bannerW) * 0.5f;
            float bannerY = 15f;

            // Pulsing amber/red alert effect
            float pulse = Mathf.PingPong(Time.time * 3f, 1f);
            Color alertBg = Color.Lerp(new Color(0.6f, 0.1f, 0.1f, 0.9f), new Color(0.9f, 0.3f, 0.0f, 0.95f), pulse);

            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = alertBg;

            string jamPercent = $"{sensorProfile.rfJammingLevel:P0}";
            string bannerText = $"[ ⚠ EW ELECTRONIC ATTACK: ACTIVE RF JAMMING {jamPercent} ]";

            GUI.Box(new Rect(bannerX, bannerY, bannerW, bannerH), bannerText, alertBannerStyle);
            GUI.backgroundColor = oldColor;
        }

        private void DrawRadarMiniScope(float x, float y)
        {
            float size = 160f;
            Rect scopeRect = new Rect(x, y, size, size);

            // Scope Background
            GUI.DrawTexture(scopeRect, radarScopeBg);

            // Center Origin
            Vector2 center = new Vector2(x + size * 0.5f, y + size * 0.5f);

            // Rotating Sweep Line
            float rad = sweepAngle * Mathf.Deg2Rad;
            Vector2 sweepEnd = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (size * 0.48f);
            DrawLine(center, sweepEnd, new Color(0.2f, 1f, 0.4f, 0.8f), 2f);

            // Draw Real Target Blips
            for (int i = 0; i < activeContacts.Count; i++)
            {
                var contact = activeContacts[i];
                if (contact.isFalseAlarm) continue;

                // Project 3D position onto 2D scope (max range ~1500m)
                Vector3 pos = contact.estimatedPosition;
                float normalizedX = Mathf.Clamp(pos.x / 1500f, -1f, 1f);
                float normalizedZ = Mathf.Clamp(pos.z / 1500f, -1f, 1f);

                float blipX = center.x + (normalizedX * size * 0.45f) - 2.5f;
                float blipY = center.y - (normalizedZ * size * 0.45f) - 2.5f;

                GUI.color = contact.isFalseAlarm ? Color.yellow : Color.red;
                GUI.DrawTexture(new Rect(blipX, blipY, 5, 5), targetBlipTexture);
            }

            // Draw Pseudo-Random Clutter Dots based on radarClutter level
            int clutterCount = Mathf.RoundToInt(sensorProfile.radarClutter * 14f);
            System.Random pseudoRng = new System.Random(42); // fixed seed for stationary ground clutter
            for (int c = 0; c < clutterCount; c++)
            {
                float cx = (float)(pseudoRng.NextDouble() * 2.0 - 1.0);
                float cy = (float)(pseudoRng.NextDouble() * 2.0 - 1.0);
                if (cx * cx + cy * cy < 0.85f)
                {
                    float px = center.x + (cx * size * 0.42f);
                    float py = center.y + (cy * size * 0.42f);
                    GUI.color = new Color(0.9f, 0.75f, 0.2f, 0.6f);
                    GUI.DrawTexture(new Rect(px, py, 3, 3), clutterDotTexture);
                }
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 5, y + size + 2, size, 20), $"RADAR: {activeContacts.Count} TRACKS", labelStyle);
        }

        private void DrawSensorStatusWidget(float x, float y)
        {
            float width = 230f;
            float height = 75f;
            Rect rect = new Rect(x, y, width, height);

            GUI.Box(rect, GUIContent.none);

            string bandLabel = (eoCameraSim != null && eoCameraSim.SpectralBand == CameraSpectralBand.InfraredThermalFLIR)
                ? "<color=#ffaa00>FLIR THERMAL IR</color>"
                : "<color=#00e5ff>DAYLIGHT OPTICAL</color>";

            string visLabel = $"{sensorProfile.opticalVisibility:P0}";
            string noiseLabel = $"{sensorProfile.radarNoise:P0}";
            string clutterLabel = $"{sensorProfile.radarClutter:P0}";

            string content = $"<b>SENSOR SUITE STATUS</b>\n" +
                             $"Optics: {bandLabel} (Vis: {visLabel})\n" +
                             $"Radar Noise Floor: {noiseLabel} | Clutter: {clutterLabel}";

            GUI.Label(new Rect(x + 8, y + 6, width - 16, height - 12), content, labelStyle);
        }

        #endregion

        #region Primitives

        private Texture2D MakeSolidTexture(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        private Texture2D MakeCircularTexture(int size, Color fill, Color border)
        {
            Texture2D tex = new Texture2D(size, size);
            float radius = size * 0.48f;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist > radius)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else if (dist >= radius - 2f)
                    {
                        tex.SetPixel(x, y, border);
                    }
                    else
                    {
                        tex.SetPixel(x, y, fill);
                    }
                }
            }
            tex.Apply();
            return tex;
        }

        private void DrawLine(Vector2 pointA, Vector2 pointB, Color color, float width)
        {
            Matrix4x4 savedMatrix = GUI.matrix;
            Color savedColor = GUI.color;
            GUI.color = color;

            float angle = Vector2.Angle(Vector2.right, pointB - pointA);
            if (pointA.y > pointB.y) angle = -angle;

            GUIUtility.RotateAroundPivot(angle, pointA);
            GUI.DrawTexture(new Rect(pointA.x, pointA.y - width * 0.5f, (pointB - pointA).magnitude, width), Texture2D.whiteTexture);

            GUI.matrix = savedMatrix;
            GUI.color = savedColor;
        }

        #endregion
    }
}
