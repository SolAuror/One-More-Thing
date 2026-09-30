using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneMoreThing //this script is for the audio feedback the player receives when completing tasks, receiving messages, and interacting with the sink and washer. 
                        //it plays different sounds for each of these events, and also manages the state of the dishes and washer label.
{
    public sealed class UserFeedback : MonoBehaviour
    {
        public RoutineSession session;
        private AudioClip pickup, complete, message, water, washer;
        private AudioSource cues, alarmSource, waterSource, washerSource;
        private float nextAlarm;
        private readonly List<GameObject> dishes = new();
        private int received;
        private TextMesh washerLabel;
        private void Start()
        {
            pickup = Tone("Soft pickup", .12f, 710, false);
            complete = Tone("Task complete", .28f, 490, false);
            message = Tone("Phone notification", .24f, 930, false);
            water = Tone("Running water", 1, 140, true);
            washer = Tone("Washer motor", 1, 60, false, true);
            cues = Source("Personal cues", FindFirstObjectByType<FirstPersonController>().viewCamera.transform, false, 0);
            var phone = FindObjectsByType<TaskInteractable>(FindObjectsSortMode.None).FirstOrDefault(t => t.taskId == "phone");
            if (phone != null) alarmSource = Source("Bedside phone alarm", phone.transform, false, 1);
            foreach (var task in FindObjectsByType<TaskInteractable>(FindObjectsSortMode.None))
            {
                if (task.taskId == "cup")
                {
                    waterSource = Source("Sink water", task.transform, true, 1); waterSource.clip = water;
                    for (int i = 0; i < (task.onCompleted?.GetPersistentEventCount() ?? 0); i++)
                        if (task.onCompleted.GetPersistentTarget(i) is GameObject prop && prop != task.gameObject) dishes.Add(prop);
                }
                if (task.taskId == "laundry")
                {
                    washerSource = Source("Washer motor", task.transform, true, 1); washerSource.clip = washer;
                    var label = new GameObject("Washer status"); label.transform.SetParent(task.transform, false);
                    label.transform.position = task.GetComponent<Collider>().bounds.center + new Vector3(.34f,.3f,0);
                    label.transform.rotation = Quaternion.Euler(0,-90,0);
                    washerLabel = label.AddComponent<TextMesh>(); washerLabel.fontSize = 40; washerLabel.characterSize = .009f;
                    washerLabel.anchor = TextAnchor.MiddleCenter; washerLabel.color = new Color(.5f,1f,.74f); washerLabel.text = "";
                }
            }
            session.State.TaskCompleted += Completed;
        }
        private static AudioSource Source(string name, Transform parent, bool loop, float spatial)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = loop; source.spatialBlend = spatial;
            source.volume = loop ? .17f : .24f; source.minDistance = 1; source.maxDistance = 8; source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }
        private static AudioClip Tone(string name, float duration, float frequency, bool noise, bool loop = false)
        {
            const int sampleRate = 22050;
            var samples = new float[Mathf.RoundToInt(duration * sampleRate)]; var random = new System.Random(17);
            float smooth = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)sampleRate;
                smooth = Mathf.Lerp(smooth, (float)random.NextDouble() * 2 - 1, .23f);
                float envelope = loop || noise ? 1 : Mathf.Sin(Mathf.PI * t / duration) * Mathf.Exp(-t * 7);
                samples[i] = (noise ? smooth : Mathf.Sin(2 * Mathf.PI * frequency * t) * .6f + Mathf.Sin(2 * Mathf.PI * frequency * 2 * t) * .15f) * envelope;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false); clip.SetData(samples, 0); return clip;
        }
        private void Completed(RoutineState.TaskState task, float cost)
        {
            cues.PlayOneShot(task.Definition.seconds <= .4f ? pickup : complete);
        }
        private void Update()
        {
            if (session?.State == null || cues == null) return;
            if (session.State.Phase == RunPhase.Ready && alarmSource != null && Time.unscaledTime >= nextAlarm)
            { alarmSource.PlayOneShot(message); nextAlarm = Time.unscaledTime + 2.5f; }
            int arrived = session.State.Events.Count(e => e.Kind == "message_received");
            if (arrived > received && session.HasPhone && session.IsRunning) cues.PlayOneShot(message);
            received = arrived;
            var sink = session.State.Find("cup");
            if (sink != null)
            {
                int cleaned = Mathf.FloorToInt(Mathf.Clamp01(sink.Progress / sink.Definition.seconds) * dishes.Count);
                for (int i = 0; i < cleaned; i++) if (dishes[i] != null) dishes[i].SetActive(false);
            }
            SetLoop(waterSource, session.IsRunning && session.State.ActiveTaskId == "cup");
            bool running = session.State.Find("laundry")?.Completed == true;
            SetLoop(washerSource, session.IsRunning && running);
            if (washerLabel != null) washerLabel.text = running ? "RUNNING" : "";
            if (!session.IsRunning) cues.Stop();
        }
        private static void SetLoop(AudioSource source, bool playing)
        {
            if (source == null) return;
            if (playing && !source.isPlaying) source.Play();
            if (!playing && source.isPlaying) source.Stop();
        }
        private void OnDestroy()
        {
            if (session?.State != null) session.State.TaskCompleted -= Completed;
            foreach (var clip in new[] { pickup, complete, message, water, washer }) if (clip != null) Destroy(clip);
            if (cues != null) Destroy(cues.gameObject);
            if (alarmSource != null) Destroy(alarmSource.gameObject);
        }
    }
}
