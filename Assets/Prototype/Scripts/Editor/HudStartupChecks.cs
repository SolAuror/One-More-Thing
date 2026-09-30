using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

namespace OneMoreThing.Editor
{
    [InitializeOnLoad]
    public static class HudStartupChecks
    {
        private const string BatchKey = "OneMoreThing.MorningChangeBatch";
        private static double nextCheck;

        static HudStartupChecks() => EditorApplication.playModeStateChanged += change =>
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(BatchKey, false)) return;
            nextCheck = EditorApplication.timeSinceStartup + 1.5;
            EditorApplication.update += CheckBatch;
        };

        public static void RunMorningBatch()
        {
            EditorSceneManager.OpenScene(HouseFeelTools.ScenePath);
            SessionState.SetBool(BatchKey, true);
            EditorApplication.EnterPlaymode();
        }

        private static void CheckBatch()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            EditorApplication.update -= CheckBatch;
            try
            {
                var hud = UnityEngine.Object.FindFirstObjectByType<RoutineHUD>();
                var plants = UnityEngine.Object.FindObjectsByType<TaskInteractable>(FindObjectsSortMode.None)
                    .Where(t => t.taskId == "plant").ToArray();
                if (plants.Length != 16 || plants.Any(t => t.session != hud.session || t.plantRequests.Length != 2))
                    throw new InvalidOperationException("The run does not include all sixteen watering plants.");
                if (!plants.Contains(hud.session.PlantTarget) || !hud.session.PlantTarget.plantRequests.Contains(hud.session.PlantRequest)
                    || plants.Count(t => t.CanInteract) != 1)
                    throw new InvalidOperationException("The selected plant or its message hint is invalid.");
                var phone = UnityEngine.Object.FindObjectsByType<TaskInteractable>(FindObjectsSortMode.None).Single(t => t.taskId == "phone");
                var camera = hud.interactor.viewCamera;
                if (Vector3.Angle(camera.transform.forward, phone.GetComponent<Renderer>().bounds.center - camera.transform.position) > 5f)
                    throw new InvalidOperationException("The opening view is not aimed at the bedside phone.");
                CheckStart();
                Debug.Log("MORNING_CHANGE_BATCH_PASSED: sixteen plants eligible; matching phone hint; bedside alarm starts the run.");
                SessionState.SetBool(BatchKey, false);
                if (Application.isBatchMode) EditorApplication.Exit(0);
                else EditorApplication.ExitPlaymode();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                SessionState.SetBool(BatchKey, false);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else EditorApplication.ExitPlaymode();
            }
        }
        [MenuItem("One More Thing/Repair HUD references")]
        public static void Repair()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before repairing scene references.");
            var hud = UnityEngine.Object.FindFirstObjectByType<RoutineHUD>();
            if (hud == null) throw new InvalidOperationException("Open the house scene first.");
            Undo.RecordObject(hud, "Repair morning HUD references");
            hud.ResolveReferences();
            if (!hud.HasRequiredReferences) throw new InvalidOperationException("Required HUD objects are missing.");
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorSceneManager.SaveScene(hud.gameObject.scene);
            Debug.Log("HUD_REFERENCES_REPAIRED: all required labels and buttons are assigned.");
        }

        [MenuItem("One More Thing/Check bedside alarm start")]
        public static void CheckStart()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode at the morning screen first.");
            var hud = UnityEngine.Object.FindFirstObjectByType<RoutineHUD>();
            if (hud == null || !hud.enabled || !hud.HasRequiredReferences || hud.modal.activeInHierarchy
                || hud.session.State.Phase != RunPhase.Ready)
                throw new InvalidOperationException("The morning screen did not initialize correctly.");
            if (!hud.session.CanLook || !hud.interactor.Prompt.Contains("silence"))
                throw new InvalidOperationException("The bedside alarm prompt is not available.");
            hud.interactor.SilenceAlarm();
            if (!hud.session.CanControl) throw new InvalidOperationException("Silencing the alarm did not enable gameplay.");
            Debug.Log("HUD_STARTUP_CHECK_PASSED: bedside alarm initialized and enabled gameplay.");
        }
    }
}
