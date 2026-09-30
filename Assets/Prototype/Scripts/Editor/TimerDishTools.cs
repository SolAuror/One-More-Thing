using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace OneMoreThing.Editor
{
    public static class TimerDishTools
    {
        public static bool OnDiningTable(Renderer renderer)
        {
            Vector3 p = renderer.bounds.center;
            return p.x > .4f && p.x < 2.5f && p.z > 7.3f && p.z < 9.5f && p.y > .9f && p.y < 1.2f;
        }
        [MenuItem("One More Thing/Split dining dish pickups")]
        public static void Apply()
        {
            if (Application.isPlaying || EditorSceneManager.GetActiveScene().path != HouseFeelTools.ScenePath)
                throw new InvalidOperationException("Open HouseLevel in edit mode.");
            var session = Object.FindFirstObjectByType<RoutineSession>();
            var dishes = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(OnDiningTable)
                .Where(r => r.name.StartsWith("Cup_") || r.name.StartsWith("Fork") || r.name.StartsWith("Plate_01"))
                .OrderBy(r => r.name, StringComparer.Ordinal).ToArray();
            if (dishes.Length != 6) throw new InvalidOperationException("Expected the six audited dining dishes; found " + dishes.Length);
            var group = Object.FindObjectsByType<TaskInteractable>(FindObjectsSortMode.None).FirstOrDefault(t => t.taskId == "dish_dining");
            if (group != null && group.transform.childCount != 0)
                throw new InvalidOperationException("Dining group has unexpected children; preserve them before replacing it.");
            Undo.RecordObject(session.definition, "Separate dining dish requirements");
            var definitions = session.definition.tasks.Where(t => t.id != "dish_dining" && !t.id.StartsWith("dish_dining_")).ToList();
            var ids = new string[dishes.Length];
            for (int i = 0; i < dishes.Length; i++)
            {
                var go = dishes[i].gameObject;
                var task = go.GetComponent<TaskInteractable>();
                if (task == null) task = Undo.AddComponent<TaskInteractable>(go);
                Undo.RecordObject(task, "Individual dining dish");
                ids[i] = "dish_dining_" + (i + 1);
                task.session = session; task.taskId = ids[i]; task.hideOnCompletion = go;
                task.onCompleted = new UnityEvent();
                definitions.Add(new RoutineTask { id = ids[i], title = "Collect dining " + ItemNames.For(task).ToLowerInvariant(),
                    thought = "This belongs by the sink.", seconds = .35f, completionTimeCost = 2f,
                    showInSummary = false, reveals = new[] { "cup" } });
                PrefabUtility.RecordPrefabInstancePropertyModifications(task);
            }
            session.definition.tasks = definitions.ToArray();
            var washing = definitions.Single(t => t.id == "cup");
            washing.prerequisites = washing.prerequisites.Where(id => id != "dish_dining" && !id.StartsWith("dish_dining_")).Concat(ids).ToArray();
            washing.prerequisiteHint = "Gather the dishes from the living room, bedroom and each place at the dining table";
            if (group != null)
            {
                // This object contains only the broad group collider and its pickup scripts.
                Undo.DestroyObjectImmediate(group.gameObject);
            }
            EditorUtility.SetDirty(session.definition);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("DINING_PICKUPS_SPLIT: six individual dishes; fruit bowl excluded; sink requires each pickup.");
        }
        [MenuItem("One More Thing/Audit dining dishes")]
        public static void Audit()
        {
            var report = new StringBuilder();
            foreach (var task in Object.FindObjectsByType<TaskInteractable>(FindObjectsSortMode.None).Where(t => t.taskId == "dish_dining" || t.taskId == "cup"))
            {
                report.AppendLine(task.taskId + " on " + HouseFeelTools.PathOf(task.transform));
                for (int i = 0; i < task.onCompleted.GetPersistentEventCount(); i++)
                    report.AppendLine(" CALLBACK " + task.onCompleted.GetPersistentTarget(i).name + " / " + task.onCompleted.GetPersistentMethodName(i));
            }
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                Vector3 p = renderer.bounds.center;
                if (p.x < .4f || p.x > 2.5f || p.z < 7.3f || p.z > 9.5f || p.y < .6f || p.y > 1.6f) continue;
                report.AppendLine(HouseFeelTools.PathOf(renderer.transform) + " @ " + p.ToString("F3") + " size " + renderer.bounds.size.ToString("F3"));
            }
            File.WriteAllText(Path.GetFullPath(Application.dataPath + "/../../../dining-audit.txt"), report.ToString());
            Debug.Log("DINING_AUDIT_COMPLETE");
        }
    }
}
