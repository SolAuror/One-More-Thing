using System;
using UnityEngine;

namespace OneMoreThing
{
    [Serializable]
    public sealed class ThoughtCue
    {
        [Min(0f)] public float afterSeconds = 15f;
        public string taskId;
    }
    [Serializable]
    public sealed class PhoneMessage
    {
        public string taskId;
        public string sender;
        [TextArea] public string body;
        [Min(0f)] public float afterPickupSeconds = 15f;
    }
    [Serializable]
    public sealed class RoutineTask
    {
        public string id;
        public string title;
        [TextArea] public string thought;
        public bool required;
        public bool repeatable;
        [Tooltip("Real seconds the player holds E. Independent of the countdown's rate.")]
        [Min(0.1f)] public float seconds = 0.8f;
        [Tooltip("Extra countdown seconds spent once, when this task finishes.")]
        [Min(0f)] public float completionTimeCost;
        [Tooltip("All these tasks must be complete before work can begin.")]
        public string[] prerequisites = Array.Empty<string>();
        public string prerequisiteHint = "Complete the earlier steps";
        public bool deviceOnly;
        public bool waitForNotification;
        public bool showInSummary = true;
        [Tooltip("Thoughts introduced when this task is completed.")]
        public string[] reveals = Array.Empty<string>();
    }

    [CreateAssetMenu(menuName = "One More Thing/Morning routine")]
    public sealed class RoutineDefinition : ScriptableObject
    {
        [Min(1f)] public float duration = 300f;
        [Range(1, 6)] public int visibleThoughts = 3;
        public RoutineTask[] tasks = Array.Empty<RoutineTask>();
        [Tooltip("One-time reminders that compete for attention during the run.")]
        public ThoughtCue[] cues = Array.Empty<ThoughtCue>();
        public PhoneMessage[] phoneMessages = Array.Empty<PhoneMessage>();
    }
}
