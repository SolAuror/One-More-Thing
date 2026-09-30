using System.Linq;
using UnityEngine;

namespace OneMoreThing
{
    public sealed class ExitInteractable : MonoBehaviour
    {
        public RoutineSession session;
        public void TryLeave()
        {
            if (session == null || !session.CanControl || session.State.TryLeave()) return;
            session.Notify("Before I go: " + string.Join(", ", session.State.Tasks
                .Where(t => t.Definition.required && !t.Completed).Select(t => t.Definition.title)));
        }
    }
}
