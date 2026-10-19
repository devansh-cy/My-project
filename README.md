# AI-Enabled Drone & Counter-Drone Threat Simulation Trainer (C-UAS Simulator)

[![Unity Version](https://img.shields.io/badge/Unity-6.6%20(6000.6.6f1)-blue.svg?logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/Render%20Pipeline-URP-orange.svg)](https://unity.com/srp/universal-render-pipeline)
[![Platform](https://img.shields.io/badge/Platform-Windows%20Standalone%20x64-green.svg)](https://microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-lightgrey.svg)](LICENSE)

An enterprise-grade, desktop-based virtual training simulator designed for defense personnel, security operators, and air defense units. The system trains and evaluates operators in **detecting, classifying, and mitigating hostile airborne drone threats and coordinated swarms** in complex urban and rural operational environments.

---

## 🎯 Executive Overview

Modern asymmetric aerial warfare demands rapid target acquisition, precise drone classification, and strict adherence to Rules of Engagement (RoE). The **C-UAS Simulation Trainer** replicates real-world sensor modalities, autonomous drone kinematics, adversarial AI swarm tactics, and operator command-and-control workflows.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           OPERATIONAL WORKFLOW                              │
├─────────────────┬───────────────────────────┬───────────────────────────────┤
│ 1. SENSOR SCAN  │ 2. C2 OPERATOR EVALUATION │ 3. AFTER-ACTION REVIEW (AAR)  │
│                 │                           │                               │
│ • Radar Sweep   │ • Step 1: Detect Target   │ • Decision-Tree Scorecard     │
│ • Thermal FLIR  │ • Step 2: Classify Threat │ • Sequential Event Timeline   │
│ • Atmospheric   │ • Step 3: Authorize RoE   │ • Trainee Historical Trends   │
│   Degradation   │   (Jam / Net / Intercept) │ • CSV Telemetry Export        │
└─────────────────┴───────────────────────────┴───────────────────────────────┘
```

---

## ⚡ Core Feature Matrix

### 1. Multi-Sensor Physics Simulation
* **Pulse-Doppler Radar Simulator (`RadarSim.cs`)**:
  * Physics-based Radar Range Equation calculation.
  * Real-time detection probability ($P_d$) factoring in Radar Cross Section (RCS) from $0.005\,\text{m}^2$ (micro-quads) to $1.5\,\text{m}^2$ (fixed-wing recon).
  * Dynamic clutter noise generation with atmospheric false alarm modeling.
  * Persistent track association to eliminate track churn.
* **Electro-Optical & Thermal FLIR Camera (`EOCameraSim.cs`)**:
  * Simulated visual and long-wave infrared (LWIR) optical scans.
  * Angular field-of-view (FOV) clipping, line-of-sight occlusion raycasting through urban buildings.
  * Atmospheric visibility degradation from fog, haze, dawn/dusk, and night lighting.
* **Sensor Degradation HUD (`SensorDegradationHUD.cs`)**:
  * Real-time radar Plan Position Indicator (PPI) scope with sweep animation.
  * Active electronic warfare (EW) jamming alerts and visual noise overlays.

---

### 2. Autonomous Drone Swarms & Adversarial AI
* **3D Craig Reynolds Boids Flocking (`SwarmController.cs`)**:
  * Distributed swarm algorithms for 5–50 coordinated drones (Separation, Alignment, Cohesion).
  * Ingress waypoint corridors weaving through low-altitude urban street canyons.
  * Zero-allocation memory management via custom object pool (`DronePool.cs`).
* **Adversarial Swarm Intelligence (`AdversarialSwarmAgent.cs`)**:
  * Monitors the operator's surveillance camera orientation and attention cone.
  * When locked under direct surveillance, drones dynamically scatter and flank behind buildings to exploit radar blind spots and break visual line-of-sight.
* **Procedural 3D Threat Models**:
  * Procedurally synthesized quadcopters, hexacopters, fixed-wing drones, and biological bird decoys with animated flapping wings (`ProceduralDroneModel.cs`, `ProceduralBirdModel.cs`).

---

### 3. Tactical Operator C2 Console (`OperatorUIController.cs`)
* Accessible instantly in-simulation via **`TAB`**.
* **Real-Time Track Table**:
  * Highlights verified aerial targets (`[AERIAL TRACK]`) pinned at the top.
  * Filters and isolates transient clutter blips (`[CLUTTER NOISE]`) with 2.5s auto-expiration.
  * Automatic target acquisition lock.
* **Target Classification**:
  * Classify contacts as `Quadcopter`, `FixedWing`, `LoiteringMunition`, `BirdDecoy`, or `Swarm`.
* **Threat Severity Assessment**:
  * Assign threat ratings from `None` / `Low` to `Critical`.
* **Countermeasure Authorization**:
  * Execute RoE-compliant responses: `ElectronicJammer`, `NetCatcher`, `DirectedEnergyLaser`, `KineticInterceptor`, or passive `Monitor`.

---

### 4. 3-Step Decision-Tree Scoring Engine (`DecisionTreeScorer.cs`)
Unlike arbitrary weighted formulas, performance is evaluated sequentially using military air defense doctrine:

```
[AIRBORNE CONTACT DETECTED]
         │
         ├── STEP 1: DETECTED IN TIME? ─────────▶ Optimal < 4.0s (35% weight)
         │
         ├── STEP 2: CLASSIFIED CORRECTLY? ─────▶ Drone Type vs Ground Truth (30% weight)
         │
         └── STEP 3: RULES OF ENGAGEMENT (ROE)? ─▶ Hostile vs Decoy Response (35% weight)
                                                  (Pass = +50-100 pts | Violation = -60-100 pts)
```

---

### 5. In-Game After-Action Review (AAR) Dashboard (`AARDashboardUI.cs`)
* Accessible anytime via **`F1`**.
* **Scorecard & Gauges**: Circular metric visualizers for Detection Reaction, Classification Fidelity, and RoE Compliance.
* **Decision Tree Paths**: Visual tree-card breakdown for every evaluated contact showing Pass/Fail branches and latency.
* **Session Event Timeline**: Complete millisecond-accurate log of spawns, sensor locks, operator inputs, and interceptions.
* **Historical Trends & Profiles**: Longitudinal performance tracking across sessions, identifying operator strengths and weaknesses.
* **CSV Export**: One-click telemetry export for external LMS integration and defense reporting.

---

### 6. Procedural Scenarios & Adaptive Difficulty
* **Scripted Scenarios (`StreamingAssets/Scenarios/`)**:
  1. `scenario_01_single_quad_day`: Daytime perimeter breach by a lone quadcopter.
  2. `scenario_02_swarm_night`: High-threat nocturnal swarm attack against an industrial complex.
  3. `scenario_03_bird_decoy_hostile_mix`: Dense fog coastal defense with migratory bird decoys and evasive FPV kamikazes.
* **Deterministic Procedural Generator (`ScenarioGenerator.cs`)**:
  * Reproducible scenario generation from integer seeds.
* **Adaptive AI Difficulty Controller (`DifficultyController.cs`)**:
  * Reads operator performance history and algorithmically generates scenarios targeting individual trainee weaknesses (e.g., boosting decoy ratios for operators prone to trigger-happy RoE violations).

---

## 🎮 Keyboard Controls

| Key | Function |
| :---: | :--- |
| **`TAB`** | Toggle **Operator C2 Command & Control Console** (Target list, Classification, Countermeasures) |
| **`F1`** | Toggle **After-Action Review (AAR) Dashboard** (Scorecard, Decision trees, Historical trends) |
| **`ESC`** | Toggle **Mission Command Briefing Menu** (Scenario selection & generation) |

---

## 🚀 Quick Start Guide

### Prerequisites
* **Operating System**: Windows 10/11 64-bit
* **Unity Version**: **Unity 6 (6000.6.6f1)** or later
* **Render Pipeline**: **Universal Render Pipeline (URP)**

### Installation
1. Clone the repository:
   ```bash
   git clone https://github.com/devansh-cy/My-project.git
   ```
2. Open **Unity Hub**, click **Add**, and select the `My project` directory.
3. Open the project with **Unity 6 (6000.6.6f1)**.

### Running the Simulator (1-Click Turnkey Setup)
1. Open the project in Unity.
2. In Unity's top menu bar, click:
   ```
   C-UAS Simulator  ──▶  ⚡ Turnkey Setup Current Scene with All Systems
   ```
3. The tool will automatically populate the active scene with:
   * Sensor Tower (Radar + EO/IR Optical/Thermal Camera)
   * Swarm Spawner (Boids controller + DronePool)
   * Urban City Buildings & Ground Terrain
   * Central Simulation Manager (AAR, Logging, Operator C2, Lighting)
4. Click the **Play (▶)** button in Unity.
5. Select a mission and click **`LAUNCH SIMULATION ENGAGEMENT`**!

---

## 📁 Project Architecture

```
Assets/
├── Editor/
│   ├── AutoSetupSimulationScene.cs   # 1-click turnkey scene configuration
│   ├── Build/BuildScript.cs          # Headless Windows standalone build pipeline
│   └── Tests/                        # EditMode unit tests for scoring & determinism
├── Scenes/
│   └── SampleScene.unity             # Pre-configured ready-to-run simulation scene
├── Scripts/
│   ├── AAR/                          # In-game AAR Dashboard, trends, and CSV export
│   ├── Core/                         # Lightweight EventBus pub/sub & InputHelper
│   ├── Drones/                       # Swarm Boids, DroneAgent, Adversarial AI, DronePool
│   ├── Environment/                  # Urban/Rural procedural builders, Diurnal lighting
│   ├── Logging/                      # Ground-truth telemetry logger & SessionLog
│   ├── Scenarios/                    # Scenario definitions, loaders, and procedural generator
│   ├── Scoring/                      # Sequential decision-tree evaluation engine
│   ├── Sensors/                      # RadarSim, EOCameraSim, and SensorDegradationHUD
│   ├── Trainee/                      # Operator C2 tactical console and action events
│   └── UI/                           # Master mission command briefing menu
└── StreamingAssets/
    └── Scenarios/                    # JSON scripted scenarios & scoring configs
```

---

## 🛠️ Standalone Build Automation

To compile a production Windows standalone x64 executable without opening the Unity editor:

```bash
Unity.exe -quit -batchmode -nographics -projectPath "path/to/My project" -executeMethod DroneTrainer.EditorTools.BuildScript.BuildWindowsStandalone
```

The compiled release will be placed in `Builds/StandaloneWindows64/`.

---

## 📄 License
This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
