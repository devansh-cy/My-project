using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DroneTrainer.UI;
using DroneTrainer.AAR;
using DroneTrainer.Logging;
using DroneTrainer.Trainee;
using DroneTrainer.Sensors;
using DroneTrainer.Drones;
using DroneTrainer.Environment;
using DroneTrainer.Scenarios.Procedural;

namespace DroneTrainer.EditorTools
{
    /// <summary>
    /// Turnkey one-click scene setup tool.
    /// Configures the active Unity scene with all C-UAS simulation systems,
    /// sensors, swarm spawner, environment lighting, and urban city buildings.
    /// </summary>
    public static class AutoSetupSimulationScene
    {
        [MenuItem("C-UAS Simulator/⚡ Turnkey Setup Current Scene with All Systems", false, 1)]
        public static void SetupScene()
        {
            Debug.Log("<color=#00e5ff><b>=== CONFIGURING C-UAS SIMULATION SCENE ===</b></color>");

            // 1. Ensure Directional Light exists
            Light dirLight = Object.FindFirstObjectByType<Light>();
            if (dirLight == null)
            {
                GameObject lightGo = new GameObject("Directional Light");
                dirLight = lightGo.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // 2. Setup Camera
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                mainCam = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
            }
            mainCam.transform.position = new Vector3(0f, 25f, -60f);
            mainCam.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            mainCam.farClipPlane = 3500f;

            // 3. Setup Ground Plane
            GameObject ground = GameObject.Find("GroundPlane");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "GroundPlane";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(150f, 1f, 150f);

                var rend = ground.GetComponent<Renderer>();
                if (rend != null)
                {
                    var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
                    mat.color = new Color(0.22f, 0.25f, 0.28f);
                    rend.sharedMaterial = mat;
                }
            }

            // 4. Setup Central Simulation Manager
            GameObject mgr = GameObject.Find("_SimulationManager");
            if (mgr == null)
            {
                mgr = new GameObject("_SimulationManager");
            }

            var mainMenu = GetOrAddComponent<MainMenuController>(mgr);
            var diffCtrl = GetOrAddComponent<DifficultyController>(mgr);
            var logger = GetOrAddComponent<EventLogger>(mgr);
            var aar = GetOrAddComponent<AARDashboardUI>(mgr);
            var opUi = GetOrAddComponent<OperatorUIController>(mgr);
            var hud = GetOrAddComponent<SensorDegradationHUD>(mgr);
            var envLight = GetOrAddComponent<EnvironmentLightingController>(mgr);
            var urbanBuilder = GetOrAddComponent<UrbanEnvironmentBuilder>(mgr);

            // 5. Setup Sensor Tower
            GameObject tower = GameObject.Find("SensorTower");
            if (tower == null)
            {
                tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tower.name = "SensorTower";
                tower.transform.position = new Vector3(0f, 8f, 0f);
                tower.transform.localScale = new Vector3(3f, 8f, 3f);
            }

            var radar = GetOrAddComponent<RadarSim>(tower);
            var eoCam = GetOrAddComponent<EOCameraSim>(tower);

            // 6. Setup Swarm Spawner
            GameObject spawner = GameObject.Find("SwarmSpawner");
            if (spawner == null)
            {
                spawner = new GameObject("SwarmSpawner");
                spawner.transform.position = new Vector3(0f, 45f, 650f);
            }

            var pool = GetOrAddComponent<DronePool>(spawner);
            var swarm = GetOrAddComponent<SwarmController>(spawner);

            // 7. Build Urban 3D Environment (Building blocks)
            urbanBuilder.BuildEnvironment();

            // 8. Mark scene dirty to save changes
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=#55ff55><b>=== SCENE SETUP COMPLETE! ===</b></color>\n" +
                      "All systems, sensors, swarm spawner, urban environment, and UI consoles are wired up.\n" +
                      "<b>Press the Play ▶ button now to start the simulator!</b>");

            EditorUtility.DisplayDialog(
                "C-UAS Simulator Setup Complete!",
                "Scene configured successfully with:\n" +
                "• Sensor Tower (Radar + EO/IR Camera)\n" +
                "• Swarm Spawner (Boids Flocking + DronePool)\n" +
                "• Urban 3D City Environment\n" +
                "• Operator C2 Console (TAB)\n" +
                "• AAR Dashboard (F1)\n\n" +
                "Click OK, then press the PLAY button at the top to run the simulation!",
                "Let's Go!"
            );
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }
    }
}
