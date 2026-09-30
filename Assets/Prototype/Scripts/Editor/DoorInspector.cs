using UnityEditor;
using UnityEngine;

namespace OneMoreThing.Editor
{
    [CustomEditor(typeof(DoorInteractable)), CanEditMultipleObjects]
    public sealed class DoorInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swingDirection"));
            var opening = serializedObject.FindProperty("openPercent");
            var direction = serializedObject.FindProperty("swingDirection");
            EditorGUILayout.Slider(opening, direction.hasMultipleDifferentValues || direction.enumValueIndex == 2 ? -100 : 0, 100, new GUIContent("Open %"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swingSpeed"));
            if (serializedObject.ApplyModifiedProperties() && !Application.isPlaying)
                foreach (DoorInteractable door in targets)
                {
                    Undo.RecordObject(door.transform, "Preview door opening");
                    door.ApplyStartingPose();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(door.transform);
                }
            if (!Application.isPlaying && GUILayout.Button("Use current pose as closed"))
                foreach (DoorInteractable door in targets)
                {
                    Undo.RecordObjects(new Object[] { door, door.transform }, "Set closed door pose");
                    door.ConfigureClosedPose(door.transform.localRotation, 0f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(door);
                }
        }
    }
}
