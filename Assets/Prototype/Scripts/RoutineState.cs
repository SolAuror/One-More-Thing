using System;
using System.Collections.Generic;
using System.Linq;

namespace OneMoreThing
{
    public enum RunPhase { Ready, Playing, Paused, Succeeded, TimedOut }

    // Plain C# state keeps task timing, interruption and results independent of the UI.
    public sealed class RoutineState
    {
        public sealed class TaskState
        {
            public RoutineTask Definition { get; }
            public float Progress { get; internal set; }
            public bool Discovered { get; internal set; }
            public bool Available { get; internal set; }
            public bool Completed => Progress >= Definition.seconds;
            public int CompletionCount { get; internal set; }
            public bool EverCompleted => CompletionCount > 0;
            public TaskState(RoutineTask definition) { Definition = definition; Available = !definition.waitForNotification; }
        }

        public readonly struct RunEvent
        {
            public readonly float Elapsed;
            public readonly float RealElapsed;
            public readonly string Kind;
            public readonly string TaskId;
            public readonly float Value;
            public RunEvent(float elapsed, float realElapsed, string kind, string taskId, float value)
            { Elapsed = elapsed; RealElapsed = realElapsed; Kind = kind; TaskId = taskId; Value = value; }
        }

        private readonly Dictionary<string, TaskState> byId = new();
        private readonly List<TaskState> tasks = new();
        private readonly List<TaskState> thoughts = new();
        private readonly List<RunEvent> events = new();
        private readonly float duration;
        private string activeTask;
        public string ActiveTaskId => activeTask;
        private string previousTask;
        public const float IdleDelay = 1.5f;
        private float idleGrace;

        public IReadOnlyList<TaskState> Tasks => tasks;
        public IReadOnlyList<TaskState> Thoughts => thoughts;
        public IReadOnlyList<RunEvent> Events => events;
        public RunPhase Phase { get; private set; } = RunPhase.Ready;
        public float Remaining { get; private set; }
        public float Elapsed => duration - Remaining;
        public float RealElapsed { get; private set; }
        public float ClockRate { get; private set; }
        public string ClockSource { get; private set; } = "";
        public int Switches { get; private set; }
        public int Interruptions { get; private set; }
        public event Action Changed;
        public event Action<TaskState, float> TaskCompleted;

        public RoutineState(RoutineDefinition definition)
        {
            if (definition == null || definition.duration <= 0f)
                throw new ArgumentException("A routine needs a positive duration.");
            duration = Remaining = definition.duration;
            foreach (var task in definition.tasks)
            {
                if (task == null || string.IsNullOrWhiteSpace(task.id) || task.seconds <= 0f || task.completionTimeCost < 0f || byId.ContainsKey(task.id))
                    throw new ArgumentException("Task IDs must be unique and non-empty; durations must be positive.");
                var state = new TaskState(task);
                byId.Add(task.id, state);
                tasks.Add(state);
            }
            foreach (var task in tasks)
                foreach (var reveal in task.Definition.reveals ?? Array.Empty<string>())
                    if (!byId.ContainsKey(reveal)) throw new ArgumentException("Unknown revealed task: " + reveal);
            foreach (var task in tasks)
                foreach (var prerequisite in task.Definition.prerequisites ?? Array.Empty<string>())
                    if (!byId.ContainsKey(prerequisite)) throw new ArgumentException("Unknown prerequisite: " + prerequisite);
            var visited = new HashSet<string>();
            foreach (var task in tasks) ValidateDependencies(task.Definition.id, visited, new HashSet<string>());
            var messageIds = new HashSet<string>();
            foreach (var message in definition.phoneMessages ?? Array.Empty<PhoneMessage>())
                if (message == null || !byId.ContainsKey(message.taskId) || !messageIds.Add(message.taskId)
                    || !byId[message.taskId].Definition.waitForNotification || message.afterPickupSeconds < 0f)
                    throw new ArgumentException("Phone messages need unique, notification-gated tasks and non-negative arrival times.");
            foreach (var cue in definition.cues ?? Array.Empty<ThoughtCue>())
                if (cue == null || !byId.ContainsKey(cue.taskId) || cue.afterSeconds < 0f)
                    throw new ArgumentException("Thought cues must reference a task and a non-negative time.");
            if (!tasks.Exists(t => t.Definition.required))
                throw new ArgumentException("A routine needs at least one required task.");
            foreach (var task in tasks)
                if (task.Definition.required) Discover(task.Definition.id);
        }

        public TaskState Find(string id) => id != null && byId.TryGetValue(id, out var task) ? task : null;
        public bool CanLeave => tasks.TrueForAll(t => !t.Definition.required || t.Completed);
        public bool CanWork(string id)
        {
            var task = Find(id);
            return task != null && task.Available && !task.Completed
                && (task.Definition.prerequisites ?? Array.Empty<string>()).All(p => byId[p].Completed);
        }

        public bool RepeatTask(string id)
        {
            var task = Find(id);
            if (Phase != RunPhase.Playing || task == null || !task.Definition.repeatable || !task.Completed) return false;
            task.Progress = 0f;
            if (!thoughts.Contains(task)) thoughts.Insert(0, task);
            Record("repeated_task", id);
            Changed?.Invoke();
            return true;
        }

        public int CollectedRequirements(string id) => (Find(id)?.Definition.prerequisites ?? Array.Empty<string>()).Count(p => byId[p].Completed);
        public int RequirementCount(string id) => Find(id)?.Definition.prerequisites?.Length ?? 0;
        public string RequirementStatus(string id) => CollectedRequirements(id) + "/" + RequirementCount(id);
        public string BlockReason(string id)
        {
            var task = Find(id);
            if (task == null) return "Unavailable";
            if (!task.Available) return "No new message yet";
            return task.Definition.prerequisiteHint + " (" + RequirementStatus(id) + ")";
        }

        public void ReleaseTask(string id)
        {
            if (Phase != RunPhase.Playing) return;
            var task = Find(id);
            if (task == null || task.Available) return;
            task.Available = true;
            Record("message_received", id);
            Discover(id);
            Changed?.Invoke();
        }

        private void ValidateDependencies(string id, HashSet<string> visited, HashSet<string> path)
        {
            if (visited.Contains(id)) return;
            if (!path.Add(id)) throw new ArgumentException("Circular task prerequisites at: " + id);
            foreach (var parent in byId[id].Definition.prerequisites ?? Array.Empty<string>()) ValidateDependencies(parent, visited, path);
            path.Remove(id);
            visited.Add(id);
        }

        public void Start()
        {
            if (Phase != RunPhase.Ready) return;
            Phase = RunPhase.Playing;
            Record("started", "");
            Changed?.Invoke();
        }

        public void SetPaused(bool paused)
        {
            if (paused && Phase == RunPhase.Playing) { StopWork(); SetClockRate(1f, ""); Phase = RunPhase.Paused; }
            else if (!paused && Phase == RunPhase.Paused) Phase = RunPhase.Playing;
            else return;
            Record(paused ? "paused" : "resumed", "");
            Changed?.Invoke();
        }

        public void Discover(string id)
        {
            var task = Find(id);
            if (task == null || !task.Available || task.Completed || task.Discovered || Phase == RunPhase.Succeeded || Phase == RunPhase.TimedOut) return;
            task.Discovered = true;
            thoughts.Insert(0, task);
            Record("noticed", id);
            Changed?.Invoke();
        }

        // Work advances in real seconds; the countdown can run faster and charge a completion cost.
        // Split the frame at completion so long frames and short frames spend the same time.
        public void Tick(float delta, string workingTaskId = null, float clockRate = 1f, string clockSource = "", bool isMoving = true, bool isInteracting = false)
        {
            if (Phase != RunPhase.Playing || delta <= 0f || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            if (float.IsNaN(clockRate) || float.IsInfinity(clockRate)) clockRate = 1f;
            // Only an idle observer gets free time. Working, handling things and attending to screens
            // immediately resume the countdown, even when the character is standing still.
            bool active = isMoving || isInteracting || CanWork(workingTaskId)
                || clockRate > 1f || !string.IsNullOrEmpty(clockSource);
            float runningDelta = active ? delta : Math.Min(delta, idleGrace);
            idleGrace = active ? IdleDelay : Math.Max(0f, idleGrace - delta);
            if (runningDelta > 0f) TickSlice(runningDelta, workingTaskId, Math.Max(1f, clockRate), clockSource ?? "");
            if (delta > runningDelta) TickSlice(delta - runningDelta, workingTaskId, 0f, "");
            if (!active && idleGrace <= 0f && Phase == RunPhase.Playing) SetClockRate(0f, "");
        }

        private void TickSlice(float delta, string workingTaskId, float clockRate, string clockSource)
        {
            if (Phase != RunPhase.Playing) return;
            SetClockRate(clockRate, clockSource);
            var task = Find(workingTaskId);
            if (!CanWork(workingTaskId)) workingTaskId = null;
            if (activeTask != workingTaskId)
            {
                StopWork();
                if (workingTaskId != null)
                {
                    Discover(workingTaskId);
                    if (previousTask != null && previousTask != workingTaskId) Switches++;
                    previousTask = activeTask = workingTaskId;
                    thoughts.Remove(task);
                    thoughts.Insert(0, task);
                    Record(task.Progress > 0f ? "resumed_task" : "started_task", workingTaskId);
                    Changed?.Invoke();
                }
            }
            float slice = Math.Min(delta, TimeToDeadline());
            if (activeTask != null) slice = Math.Min(slice, task.Definition.seconds - task.Progress);
            AdvanceClock(slice);
            if (activeTask != null)
            {
                task.Progress = Math.Min(task.Definition.seconds, task.Progress + slice);
                if (task.Completed)
                {
                    task.CompletionCount++;
                    Record("completed", activeTask);
                    activeTask = null;
                    thoughts.Remove(task);
                    float cost = Math.Min(Remaining, task.Definition.completionTimeCost);
                    Remaining -= cost;
                    if (cost > 0f) Record("task_time_cost", task.Definition.id, cost);
                    foreach (var reveal in task.Definition.reveals ?? Array.Empty<string>()) Discover(reveal);
                    TaskCompleted?.Invoke(task, cost);
                    Changed?.Invoke();
                }
            }
            AdvanceClock(Math.Min(delta - slice, TimeToDeadline()));
            if (Remaining <= 0f)
            {
                StopWork();
                SetClockRate(1f, "");
                Phase = RunPhase.TimedOut;
                Record("timed_out", "");
                Changed?.Invoke();
            }
        }

        public bool TryLeave()
        {
            if (Phase != RunPhase.Playing || !CanLeave) return false;
            StopWork();
            SetClockRate(1f, "");
            Phase = RunPhase.Succeeded;
            Record("left_house", "");
            Changed?.Invoke();
            return true;
        }

        private void StopWork()
        {
            if (activeTask == null) return;
            Interruptions++;
            Record("interrupted", activeTask);
            activeTask = null;
        }

        private void AdvanceClock(float seconds)
        {
            Remaining = Math.Max(0f, Remaining - seconds * ClockRate);
            RealElapsed += seconds;
        }

        private float TimeToDeadline() => Remaining <= 0f ? 0f : ClockRate > 0f ? Remaining / ClockRate : float.PositiveInfinity;

        private void SetClockRate(float rate, string source)
        {
            if (rate <= 1f) source = "";
            if (ClockRate == rate && ClockSource == source) return;
            ClockRate = rate;
            ClockSource = source;
            Record("clock_rate", source, rate);
        }

        private void Record(string kind, string id, float value = 0f) => events.Add(new RunEvent(Elapsed, RealElapsed, kind, id, value));
    }
}
