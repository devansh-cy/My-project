using System;
using System.Collections.Generic;
using UnityEngine;
using DroneTrainer.Logging;
using DroneTrainer.Scenarios;
using DroneTrainer.Trainee;
using DroneTrainer.Sensors;

namespace DroneTrainer.Scoring
{
    /// <summary>
    /// Detailed score evaluation record for a single detected contact.
    /// </summary>
    [Serializable]
    public class ContactScoreRecord
    {
        public string contactOrDroneId;
        public string groundTruthThreatType;
        public bool isHostile;
        public bool isFalseAlarm;

        public float firstAppearanceTime;
        public float firstTraineeActionTime;
        public float detectionLatencySeconds;
        public float detectionScore; // 0 - 100

        public TraineeClassification traineeClassification;
        public ClassificationMatchResult classificationResult;
        public float classificationScore; // 0 - 100

        public ResponseOption traineeResponse;
        public float engagementScore; // -100 - 100
        public string roeNotes;

        public float compositeScore; // 0 - 100
    }

    /// <summary>
    /// Comprehensive scoring report for a complete training session.
    /// </summary>
    [Serializable]
    public class SessionScoreReport
    {
        public string sessionId;
        public string scenarioId;
        public string traineeId;

        public float overallScorePercentage; // 0 - 100%
        public float averageDetectionScore;
        public float averageClassificationScore;
        public float averageEngagementScore;

        public int totalContactsEvaluated;
        public int correctClassifications;
        public int partialClassifications;
        public int wrongClassifications;
        public int roeViolations;

        public List<ContactScoreRecord> contactRecords = new List<ContactScoreRecord>();

        public string ToJson(bool prettyPrint = true)
        {
            return JsonUtility.ToJson(this, prettyPrint);
        }
    }

    /// <summary>
    /// Decision-tree scoring engine that computes detection response latency,
    /// classification fidelity, and Rules of Engagement (ROE) compliance using
    /// a JSON-driven configuration.
    /// </summary>
    public class DecisionTreeScorer
    {
        private readonly ScoringConfig config;

        public DecisionTreeScorer(ScoringConfig configuration = null)
        {
            config = configuration ?? ScoringConfig.Default;
            config.Validate();
        }

        /// <summary>
        /// Evaluates a completed session telemetry log and computes all decision-tree scores.
        /// </summary>
        public SessionScoreReport EvaluateSession(SessionLog session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            var report = new SessionScoreReport
            {
                sessionId = session.sessionId,
                scenarioId = session.scenarioId,
                traineeId = session.traineeId
            };

            // Index ground truth drone data
            var droneTruthMap = new Dictionary<string, DroneSpawnedEvent>();
            var contactTruthMap = new Dictionary<string, string>(); // ContactID -> DroneID
            var falseAlarmContacts = new HashSet<string>();
            var firstSeenTimes = new Dictionary<string, float>();

            // Parse entries
            for (int i = 0; i < session.entries.Count; i++)
            {
                var entry = session.entries[i];

                if (entry.eventType == nameof(DroneSpawnedEvent) && !string.IsNullOrEmpty(entry.payloadJson))
                {
                    try
                    {
                        var spawnEvt = JsonUtility.FromJson<DroneSpawnedEvent>(entry.payloadJson);
                        droneTruthMap[spawnEvt.DroneId] = spawnEvt;
                        if (!firstSeenTimes.ContainsKey(spawnEvt.DroneId))
                        {
                            firstSeenTimes[spawnEvt.DroneId] = spawnEvt.Timestamp;
                        }
                    }
                    catch { }
                }
                else if (entry.eventType == nameof(ContactDetectedEvent) && !string.IsNullOrEmpty(entry.payloadJson))
                {
                    try
                    {
                        var contact = JsonUtility.FromJson<Contact>(entry.payloadJson);
                        if (contact != null)
                        {
                            if (contact.isFalseAlarm)
                            {
                                falseAlarmContacts.Add(contact.id);
                            }
                            else if (!string.IsNullOrEmpty(contact.trueDroneId))
                            {
                                contactTruthMap[contact.id] = contact.trueDroneId;
                            }

                            if (!firstSeenTimes.ContainsKey(contact.id))
                            {
                                firstSeenTimes[contact.id] = contact.timestamp;
                            }
                        }
                    }
                    catch { }
                }
            }

            // Group trainee actions by contact ID
            var actionsPerContact = new Dictionary<string, List<TraineeActionEvent>>();
            for (int i = 0; i < session.entries.Count; i++)
            {
                var entry = session.entries[i];
                if (entry.eventType == nameof(TraineeActionEvent) && !string.IsNullOrEmpty(entry.payloadJson))
                {
                    try
                    {
                        var action = JsonUtility.FromJson<TraineeActionEvent>(entry.payloadJson);
                        if (!actionsPerContact.TryGetValue(action.ContactId, out var actionList))
                        {
                            actionList = new List<TraineeActionEvent>();
                            actionsPerContact[action.ContactId] = actionList;
                        }
                        actionList.Add(action);
                    }
                    catch { }
                }
            }

            // Group contacts by true drone ID
            var droneToContactsMap = new Dictionary<string, List<string>>();
            foreach (var kvp in contactTruthMap)
            {
                if (!droneToContactsMap.TryGetValue(kvp.Value, out var cList))
                {
                    cList = new List<string>();
                    droneToContactsMap[kvp.Value] = cList;
                }
                cList.Add(kvp.Key);
            }

            var targetsToEvaluate = new List<(string targetId, DroneSpawnedEvent? spawnData, bool isFalseAlarm, float appearanceTime, List<TraineeActionEvent> actions)>();

            if (droneTruthMap.Count > 0)
            {
                // Primary evaluation: Evaluate each spawned threat drone
                foreach (var kvp in droneTruthMap)
                {
                    string droneId = kvp.Key;
                    var spawn = kvp.Value;

                    var combinedActions = new List<TraineeActionEvent>();
                    if (actionsPerContact.TryGetValue(droneId, out var directActions))
                    {
                        combinedActions.AddRange(directActions);
                    }

                    float earliestSeen = spawn.Timestamp;
                    if (droneToContactsMap.TryGetValue(droneId, out var contactIds))
                    {
                        for (int c = 0; c < contactIds.Count; c++)
                        {
                            string cId = contactIds[c];
                            if (actionsPerContact.TryGetValue(cId, out var cActions))
                            {
                                combinedActions.AddRange(cActions);
                            }
                            if (firstSeenTimes.TryGetValue(cId, out float t) && (earliestSeen <= 0f || t < earliestSeen))
                            {
                                earliestSeen = t;
                            }
                        }
                    }

                    combinedActions.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                    targetsToEvaluate.Add((droneId, spawn, false, earliestSeen, combinedActions));
                }

                // Also evaluate any false alarm that the trainee mistakenly acted upon (ROE check)
                foreach (var falseId in falseAlarmContacts)
                {
                    if (actionsPerContact.TryGetValue(falseId, out var falseActions) && falseActions.Count > 0)
                    {
                        firstSeenTimes.TryGetValue(falseId, out float appTime);
                        targetsToEvaluate.Add((falseId, null, true, appTime, falseActions));
                    }
                }
            }
            else
            {
                // Fallback for sessions without spawn events
                var allEvaluatedTargets = new HashSet<string>(actionsPerContact.Keys);
                foreach (var kvp in contactTruthMap) allEvaluatedTargets.Add(kvp.Key);

                foreach (string contactId in allEvaluatedTargets)
                {
                    actionsPerContact.TryGetValue(contactId, out var contactActions);
                    bool isFalseAlarm = falseAlarmContacts.Contains(contactId);
                    DroneSpawnedEvent? spawnData = null;
                    if (contactTruthMap.TryGetValue(contactId, out string trueDroneId) && droneTruthMap.TryGetValue(trueDroneId, out var spawn))
                    {
                        spawnData = spawn;
                    }
                    firstSeenTimes.TryGetValue(contactId, out float appearanceTime);
                    targetsToEvaluate.Add((contactId, spawnData, isFalseAlarm, appearanceTime, contactActions));
                }
            }

            float totalDetection = 0f;
            float totalClass = 0f;
            float totalEngagement = 0f;

            foreach (var target in targetsToEvaluate)
            {
                ContactScoreRecord record = EvaluateContact(target.targetId, target.spawnData, target.isFalseAlarm, target.appearanceTime, target.actions);
                report.contactRecords.Add(record);

                totalDetection += record.detectionScore;
                totalClass += record.classificationScore;
                totalEngagement += Mathf.Max(0f, record.engagementScore);

                if (record.classificationResult == ClassificationMatchResult.Correct) report.correctClassifications++;
                else if (record.classificationResult == ClassificationMatchResult.Partial) report.partialClassifications++;
                else report.wrongClassifications++;

                if (record.engagementScore < 0f) report.roeViolations++;
            }

            int count = report.contactRecords.Count;
            report.totalContactsEvaluated = count;

            if (count > 0)
            {
                report.averageDetectionScore = totalDetection / count;
                report.averageClassificationScore = totalClass / count;
                report.averageEngagementScore = totalEngagement / count;

                report.overallScorePercentage = Mathf.Clamp(
                    report.averageDetectionScore * config.detectionTimeWeight +
                    report.averageClassificationScore * config.classificationWeight +
                    report.averageEngagementScore * config.engagementWeight,
                    0f, 100f
                );
            }

            return report;
        }

        /// <summary>
        /// Scores an individual contact interaction across detection time, classification, and engagement.
        /// </summary>
        public ContactScoreRecord EvaluateContact(string targetId, DroneSpawnedEvent? truth, bool isFalseAlarm, 
                                                   float appearanceTime, List<TraineeActionEvent> actions)
        {
            var record = new ContactScoreRecord
            {
                contactOrDroneId = targetId,
                firstAppearanceTime = appearanceTime,
                isFalseAlarm = isFalseAlarm,
                isHostile = truth.HasValue ? truth.Value.IsHostile : false,
                groundTruthThreatType = truth.HasValue ? truth.Value.ThreatType.ToString() : (isFalseAlarm ? "ClutterNoise" : "Unknown")
            };

            // 1. Detection Time Scoring
            if (actions != null && actions.Count > 0)
            {
                record.firstTraineeActionTime = actions[0].Timestamp;
                record.detectionLatencySeconds = Mathf.Max(0f, record.firstTraineeActionTime - appearanceTime);
                record.detectionScore = ComputeDetectionTimeScore(record.detectionLatencySeconds);
            }
            else
            {
                record.firstTraineeActionTime = -1f;
                record.detectionLatencySeconds = 999f;
                record.detectionScore = 0f;
            }

            // Extract final classification and engagement chosen by operator
            record.traineeClassification = TraineeClassification.Unknown;
            record.traineeResponse = ResponseOption.Monitor;

            if (actions != null)
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    if (actions[i].ActionType == TraineeActionType.ClassifyTarget)
                    {
                        record.traineeClassification = actions[i].Classification;
                    }
                    if (actions[i].ActionType == TraineeActionType.ExecuteResponse)
                    {
                        record.traineeResponse = actions[i].Response;
                    }
                }
            }

            // 2. Classification Scoring
            record.classificationResult = EvaluateClassificationMatch(truth, isFalseAlarm, record.traineeClassification);
            record.classificationScore = ComputeClassificationScore(record.classificationResult);

            // 3. Rules of Engagement (ROE) Scoring
            record.engagementScore = EvaluateEngagementRoe(truth, isFalseAlarm, record.traineeResponse, out record.roeNotes);

            // 4. Weighted Composite Score
            float rawComposite = (record.detectionScore * config.detectionTimeWeight) +
                                 (record.classificationScore * config.classificationWeight) +
                                 (Mathf.Max(0f, record.engagementScore) * config.engagementWeight);

            if (record.engagementScore < 0f)
            {
                rawComposite = Mathf.Max(0f, rawComposite + record.engagementScore * 0.5f);
            }

            record.compositeScore = Mathf.Clamp(rawComposite, 0f, 100f);
            return record;
        }

        #region Scoring Decision-Tree Branches

        public float ComputeDetectionTimeScore(float latencySeconds)
        {
            if (latencySeconds <= config.optimalDetectionTimeSeconds)
            {
                return 100f;
            }

            if (latencySeconds >= config.maxDetectionTimeoutSeconds)
            {
                return 0f;
            }

            // Linear interpolation between optimal and timeout
            float t = (latencySeconds - config.optimalDetectionTimeSeconds) / 
                      (config.maxDetectionTimeoutSeconds - config.optimalDetectionTimeSeconds);
            return Mathf.Clamp((1f - t) * 100f, 0f, 100f);
        }

        public ClassificationMatchResult EvaluateClassificationMatch(DroneSpawnedEvent? truth, bool isFalseAlarm, TraineeClassification traineeClass)
        {
            if (isFalseAlarm)
            {
                // Classifying noise as a drone is a false alarm misidentification
                return traineeClass == TraineeClassification.Unknown 
                    ? ClassificationMatchResult.Correct 
                    : ClassificationMatchResult.FalseAlarmMisidentified;
            }

            if (!truth.HasValue || traineeClass == TraineeClassification.Unknown)
            {
                return ClassificationMatchResult.Wrong;
            }

            ThreatType threat = truth.Value.ThreatType;

            switch (threat)
            {
                case ThreatType.CommercialQuadcopter:
                case ThreatType.TacticalQuadcopter:
                case ThreatType.HeavyPayloadOctocopter:
                    if (traineeClass == TraineeClassification.Quadcopter || traineeClass == TraineeClassification.Swarm)
                        return ClassificationMatchResult.Correct;
                    if (traineeClass == TraineeClassification.LoiteringMunition)
                        return ClassificationMatchResult.Partial;
                    return ClassificationMatchResult.Wrong;

                case ThreatType.FixedWingRecon:
                    if (traineeClass == TraineeClassification.FixedWing)
                        return ClassificationMatchResult.Correct;
                    if (traineeClass == TraineeClassification.LoiteringMunition)
                        return ClassificationMatchResult.Partial;
                    return ClassificationMatchResult.Wrong;

                case ThreatType.FPVKamikaze:
                    if (traineeClass == TraineeClassification.LoiteringMunition || traineeClass == TraineeClassification.Quadcopter)
                        return ClassificationMatchResult.Correct;
                    if (traineeClass == TraineeClassification.FixedWing)
                        return ClassificationMatchResult.Partial;
                    return ClassificationMatchResult.Wrong;

                case ThreatType.BirdDecoy:
                    if (traineeClass == TraineeClassification.Bird)
                        return ClassificationMatchResult.Correct;
                    return ClassificationMatchResult.Wrong;

                case ThreatType.CivilianUltralight:
                    if (traineeClass == TraineeClassification.FixedWing)
                        return ClassificationMatchResult.Correct;
                    return ClassificationMatchResult.Wrong;

                default:
                    return ClassificationMatchResult.Wrong;
            }
        }

        public float ComputeClassificationScore(ClassificationMatchResult match)
        {
            switch (match)
            {
                case ClassificationMatchResult.Correct:
                    return config.correctClassificationPoints;
                case ClassificationMatchResult.Partial:
                    return config.partialClassificationPoints;
                case ClassificationMatchResult.FalseAlarmMisidentified:
                    return config.falseAlarmPenaltyPoints;
                case ClassificationMatchResult.Wrong:
                default:
                    return config.wrongClassificationPoints;
            }
        }

        public float EvaluateEngagementRoe(DroneSpawnedEvent? truth, bool isFalseAlarm, ResponseOption response, out string roeNotes)
        {
            if (isFalseAlarm)
            {
                if (response == ResponseOption.Engage)
                {
                    roeNotes = "ROE Violation: Wasted kinetic munition on phantom radar clutter.";
                    return -50f;
                }
                roeNotes = "Correct: Ignored or monitored sensor clutter.";
                return 100f;
            }

            if (!truth.HasValue)
            {
                roeNotes = "Target unconfirmed.";
                return 50f;
            }

            bool isHostile = truth.Value.IsHostile;
            ThreatType threatType = truth.Value.ThreatType;

            if (isHostile)
            {
                switch (response)
                {
                    case ResponseOption.Engage:
                    case ResponseOption.Jam:
                        roeNotes = "ROE Adherence: Successfully authorized countermeasure against confirmed hostile.";
                        return config.successfulHostileNeutralizationPoints;

                    case ResponseOption.Alert:
                        roeNotes = "Acceptable: Sounded defense alert for inbound threat.";
                        return config.timelyAlertPoints;

                    case ResponseOption.Monitor:
                    default:
                        roeNotes = "ROE Failure: Remained passive against hostile airborne threat.";
                        return config.unengagedLethalThreatPenalty;
                }
            }
            else
            {
                // Non-hostile target (Bird decoy or Civilian)
                if (threatType == ThreatType.BirdDecoy)
                {
                    if (response == ResponseOption.Engage)
                    {
                        roeNotes = "Severe ROE Breach: Engaged harmless wildlife decoy with kinetic weaponry.";
                        return config.birdDecoyEngagementPenalty;
                    }
                    if (response == ResponseOption.Jam)
                    {
                        roeNotes = "Sub-optimal: Unnecessary RF jamming directed at biological track.";
                        return 40f;
                    }
                    roeNotes = "ROE Adherence: Correctly withheld fire against biological decoy.";
                    return config.correctNonThreatMonitorPoints;
                }

                if (threatType == ThreatType.CivilianUltralight)
                {
                    if (response == ResponseOption.Engage)
                    {
                        roeNotes = "Critical ROE Violation: Collateral damage against civilian aircraft!";
                        return config.civilianEngagementPenalty;
                    }
                    if (response == ResponseOption.Alert)
                    {
                        roeNotes = "Good: Issued alert/warning for civilian airspace encroachment.";
                        return 85f;
                    }
                    roeNotes = "ROE Adherence: Monitored civilian aircraft without escalation.";
                    return config.correctNonThreatMonitorPoints;
                }

                roeNotes = "Monitored non-threat.";
                return config.correctNonThreatMonitorPoints;
            }
        }

        #endregion
    }
}
