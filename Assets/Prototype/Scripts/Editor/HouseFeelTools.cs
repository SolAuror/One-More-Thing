using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Sol.Grab;
using Sol.Outline;
using Object = UnityEngine.Object;

namespace OneMoreThing.Editor
{
    public static class HouseFeelTools
    {
        public const string ScenePath = "Assets/Prototype/Scenes/HouseLevel.unity";
        public static string ReportPath => Path.GetFullPath(Application.dataPath + "/../../../house-feel-audit.txt");
        public static void AuditBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var report = new StringBuilder();
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Renderer>() == null && t.GetComponent<DoorInteractable>() == null) continue;
                report.AppendLine(PathOf(t) + " | pos=" + t.position.ToString("F3") + " rot=" + t.eulerAngles.ToString("F2")
                    + " local=" + t.localEulerAngles.ToString("F2") + " | " + string.Join(",", t.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().Name)));
                if (t.GetComponent<TaskInteractable>() is TaskInteractable task) report.AppendLine(" TASK " + task.taskId + " / " + task.locationDescription);
            }
            File.WriteAllText(ReportPath, report.ToString());
            Debug.Log("HOUSE_AUDIT_COMPLETE: " + ReportPath);
        }
        public static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
        public static void ApplyBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Apply();
        }
        [MenuItem("One More Thing/Apply house interaction refinements")]
        public static void Apply()
        {
            if (Application.isPlaying || EditorSceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open HouseLevel in edit mode first.");
            var doors = Object.FindObjectsByType<DoorInteractable>(FindObjectsSortMode.None);
            // These frames face along the house's X axis. The handle-side wall fixes the closed heading.
            foreach (var door in doors)
            {
                // A persisted pose is an intentional authoring choice. Only migrate the legacy scene once.
                if (door.HasConfiguredPose) continue;
                Quaternion authored = door.transform.rotation;
                float yaw = door.transform.position.x < -5f ? 0f : 180f;
                float angle = Mathf.DeltaAngle(yaw, authored.eulerAngles.y);
                float percent = door.swingDirection == DoorInteractable.SwingDirection.Both ? angle : Mathf.Abs(angle);
                door.ConfigureClosedPose(Quaternion.Inverse(door.transform.parent.rotation) * Quaternion.Euler(0f, yaw, 0f), Mathf.Clamp(percent, -100f, 100f));
                if (Quaternion.Angle(door.transform.rotation, authored) > .1f)
                    Debug.Log("Door starting pose limited to its allowed arc: " + PathOf(door.transform) + " / " + angle.ToString("F2") + " degrees.");
                PrefabUtility.RecordPrefabInstancePropertyModifications(door);
                PrefabUtility.RecordPrefabInstancePropertyModifications(door.transform);
                EditorUtility.SetDirty(door);
            }
            int grabs = 0, outlines = 0;
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!IsLooseProp(t)) continue;
                var go = t.gameObject;
                foreach (var mesh in go.GetComponentsInChildren<MeshCollider>())
                { mesh.convex = true; PrefabUtility.RecordPrefabInstancePropertyModifications(mesh); }
                if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
                bool newBody = go.GetComponent<Rigidbody>() == null;
                var body = go.GetComponent<Rigidbody>();
                if (body == null) body = go.AddComponent<Rigidbody>();
                if (newBody) { body.isKinematic = true; body.useGravity = true; body.mass = .4f; }
                var grabbable = go.GetComponent<GrabbableComponent>();
                if (grabbable == null) grabbable = go.AddComponent<GrabbableComponent>();
                if (newBody) grabbable.wakeOnFirstGrab = true;
                if (go.GetComponent<GrabInteractable>() == null) { go.AddComponent<GrabInteractable>(); grabs++; }
                if (go.GetComponent<OutlineComponent>() == null)
                {
                    var outline = go.AddComponent<OutlineComponent>();
                    outline.outlineColor = new Color(.38f, .94f, .78f); outline.outlineWidth = 2f; outlines++;
                }
                foreach (var child in go.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(grabbable);
                EditorUtility.SetDirty(go);
            }
            ConfigurePlants();
            var definition = Object.FindFirstObjectByType<RoutineSession>().definition;
            foreach (var task in definition.tasks)
                task.thought = task.id switch {
                    "cup" => "A clear sink would be a nice thing to leave behind.",
                    "laundry" => "Someone else will need a clean towel later.",
                    "books" => "Those books were standing a moment ago.",
                    "plant" => "Which one did Alex mention?",
                    "email" => "Alex left me a note in the inbox.",
                    "dish_living" => "This mug has travelled quite far from the kitchen.",
                    "dish_dining" => "Breakfast left a little evidence.",
                    "dish_bedroom" => "That mug definitely belongs downstairs.",
                    "laundry_downstairs" => "The bathroom floor isn't really a laundry basket.",
                    "laundry_upstairs" => "Another towel. Of course.",
                    _ => task.thought };
            definition.phoneMessages.First(m => m.taskId == "message_plans").body = "Still on for tonight?\nI'll cook if there's a little room in the kitchen.";
            definition.phoneMessages.First(m => m.taskId == "message_photo").body = "Miso tried to use the books as stairs again.\nI should probably apologise to the shelf.";
            EditorUtility.SetDirty(definition);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log($"HOUSE_FEEL_APPLIED: {doors.Length} doors; {grabs} new grab adapters; {outlines} new outlines; sixteen plant request lists.");
        }

        private readonly struct PlantClue
        {
            public readonly string type, location, landmark;
            public readonly Vector3 position;
            public PlantClue(string type, float x, float y, float z, string location, string landmark)
            { this.type = type; position = new Vector3(x, y, z); this.location = location; this.landmark = landmark; }
        }

        // Positions identify scene instances, including duplicate prefab names. The two clues for each
        // describe the same pot from different directions; email and text use one shared choice.
        private static readonly PlantClue[] PlantClues = {
            new("Flower_07",  3.432f, .112f,  5.387f, "downstairs, beside the purple pot near the dining doorway", "the purple square pot near the downstairs dining doorway"),
            new("Flower_07", -3.402f, .133f, -1.422f, "downstairs, beside the bathroom shower", "the downstairs shower"),
            new("Flower_03",  3.421f, .112f, -1.415f, "downstairs, by the living-room books", "the living-room bookshelves"),
            new("Flower_03",  3.360f, .105f, 10.392f, "downstairs, at the far end of the dining room", "the far dining-room wall"),
            new("Flower_04",  3.518f, .093f,  5.990f, "downstairs, beside the grey broad-leaf pot near the dining doorway", "the grey broad-leaf pot near the downstairs dining doorway"),
            new("Flower_04", -5.899f, .112f,  3.570f, "downstairs, along the wall near the bathroom", "the downstairs bathroom doorway"),
            new("Flower_07", -5.929f, .112f,  5.352f, "downstairs, along the wall beyond the bathroom", "the purple hallway pot"),
            new("Flower_03", -2.757f, .112f,  6.132f, "downstairs, below the cluster of wall pictures", "the downstairs picture wall"),
            new("Flower_04", -3.369f, .112f,  2.321f, "downstairs, beside the bathroom sink", "the downstairs bathroom mirror"),
            new("Flower_04", -5.893f, 2.622f, -1.412f, "upstairs, along the bedroom wall near the bed", "the bed in the upstairs bedroom"),
            new("Flower_07", -5.906f, 2.622f,  2.809f, "upstairs, near the bedroom TV", "the upstairs bedroom TV"),
            new("Flower_03",  -.463f, 2.622f,  2.919f, "upstairs, beside the bedroom mirror", "the upstairs bedroom mirror"),
            new("Flower_07", -5.938f, 2.622f,  6.056f, "upstairs, by the sofa", "the upstairs sitting-area sofa"),
            new("Flower_07", -5.928f, 2.622f, 10.452f, "upstairs, beside the fireplace", "the upstairs fireplace"),
            new("Flower_04",  3.395f, 2.609f,  6.072f, "upstairs, nearer the far bedroom of the two purple pots", "the far upstairs bedroom"),
            new("Flower_04",  3.371f, 2.622f,  5.406f, "upstairs, nearer the bathroom of the two purple pots", "the upstairs bathroom")
        };

        [MenuItem("One More Thing/Configure all watering plants")]
        public static void ConfigurePlants()
        {
            if (Application.isPlaying || EditorSceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open HouseLevel in edit mode first.");
            var session = Object.FindFirstObjectByType<RoutineSession>();
            var flowers = EditorSceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name.StartsWith("Flower_03", StringComparison.Ordinal)
                    || t.name.StartsWith("Flower_04", StringComparison.Ordinal)
                    || t.name.StartsWith("Flower_07", StringComparison.Ordinal))
                .Where(t => t.GetComponent<Renderer>() != null).ToArray();
            if (flowers.Length != PlantClues.Length)
                throw new InvalidOperationException($"Expected {PlantClues.Length} watering plants; found {flowers.Length}.");
            foreach (var clue in PlantClues)
            {
                var matches = flowers.Where(t => t.name.StartsWith(clue.type, StringComparison.Ordinal)
                    && Vector3.Distance(t.position, clue.position) < .2f).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("Could not uniquely locate " + clue.type + " at " + clue.position);
                var task = matches[0].GetComponent<TaskInteractable>();
                if (task == null) task = Undo.AddComponent<TaskInteractable>(matches[0].gameObject);
                Undo.RecordObject(task, "Configure watering plant");
                task.session = session;
                task.taskId = "plant";
                task.locationDescription = clue.location;
                string shape = clue.type switch { "Flower_03" => "orange-pot tree", "Flower_04" => "purple square pot", _ => "grey broad-leaf pot" };
                task.plantRequests = new[] {
                    "Could you water the " + shape + " " + clue.location + "?",
                    "The " + shape + " by " + clue.landmark + " could use a drink." };
                PrefabUtility.RecordPrefabInstancePropertyModifications(task);
                EditorUtility.SetDirty(task);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("WATERING_PLANTS_CONFIGURED: " + flowers.Length + " individual plants and message clues.");
        }

        public static void ConfigurePlantsBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            ConfigurePlants();
            AimOpeningAtPhone();
        }

        [MenuItem("One More Thing/Aim opening at bedside phone")]
        public static void AimOpeningAtPhone()
        {
            if (Application.isPlaying || EditorSceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open HouseLevel in edit mode first.");
            var player = Object.FindFirstObjectByType<FirstPersonController>();
            var phone = Object.FindObjectsByType<TaskInteractable>(FindObjectsSortMode.None).Single(t => t.taskId == "phone");
            var camera = player.viewCamera;
            var target = phone.GetComponent<Renderer>().bounds.center;
            var direction = target - camera.transform.position;
            var horizontal = new Vector3(direction.x, 0f, direction.z);
            Undo.RecordObject(player.transform, "Face bedside phone at start");
            Undo.RecordObject(camera.transform, "Look at bedside phone at start");
            player.transform.rotation = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
            camera.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(direction.y, horizontal.magnitude) * Mathf.Rad2Deg, 0f, 0f);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("BEDSIDE_OPENING_AIMED: player looks at the ringing phone.");
        }
        public static bool IsLooseProp(Transform t)
        {
            var task = t.GetComponent<TaskInteractable>();
            if (task != null && (task.taskId == "keys" || task.taskId == "phone" || task.taskId == "bag"
                || task.taskId.StartsWith("dish_") || task.taskId.StartsWith("laundry_"))) return true;
            if (t.GetComponent<MeshRenderer>() == null || t.GetComponentInParent<DoorInteractable>() != null
                || t.GetComponentInParent<DeviceInteractable>() != null) return false;
            if (t.parent != null && (t.parent.GetComponentInParent<Rigidbody>() != null || t.parent.GetComponentInParent<TaskInteractable>() != null)) return false;
            if (t.GetComponent<Rigidbody>() != null || t.GetComponent<GrabbableComponent>() != null) return true;
            string[] prefixes = { "Book_", "NoteBook", "Box", "Boat", "Bottle", "Candle", "Cup", "CoffeeCup", "Flower_", "Fork", "Knife", "Spoon", "Kettle", "Pan", "Pot", "Plate", "KubikRubik", "PC_Keyboard", "PC_Mouse", "PhotoFrame", "Pillow", "Slippers", "SoapBottle", "Toothbrush", "GlassToothbrush", "ToiletPaper", "TV_Remote", "Apple", "Orange", "Pear", "Cake", "Donut", "Egg", "Pizza", "Pumpkin", "Sandwich", "Steak", "Trash_", "Garbage" };
            return t.name == "Bedroom mug" || prefixes.Any(prefix => t.name.StartsWith(prefix, StringComparison.Ordinal));
        }
        public static void RenderPlantsBatch()
        {
            foreach (string name in new[] { "Flower_03", "Flower_04", "Flower_07" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Third Party Assets/LowPolyInterior/Prefabs/Flowers/" + name + ".prefab");
                var preview = new PreviewRenderUtility();
                try
                {
                    var instance = Object.Instantiate(prefab);
                    preview.AddSingleGO(instance);
                    var bounds = instance.GetComponentInChildren<Renderer>().bounds;
                    preview.camera.transform.position = bounds.center + new Vector3(1f, .25f, -1.8f) * bounds.size.magnitude;
                    preview.camera.transform.LookAt(bounds.center);
                    preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 100;
                    preview.camera.fieldOfView = 35;
                    preview.lights[0].intensity = 1.4f; preview.lights[0].transform.rotation = Quaternion.Euler(35, 35, 0);
                    preview.lights[1].intensity = 1f;
                    preview.BeginStaticPreview(new Rect(0, 0, 512, 512));
                    preview.Render(true);
                    var image = preview.EndStaticPreview();
                    File.WriteAllBytes(Path.GetFullPath(Application.dataPath + "/../../../" + name + ".png"), image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
                finally { preview.Cleanup(); }
            }
        }
    }
}
