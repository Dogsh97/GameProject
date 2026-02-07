using System;

namespace Game.Core.StateMachine
{
    public class StateMachine
    {
        public IState Current { get; private set; }

        public void Set(IState next)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (ReferenceEquals(Current, next)) return;

            Current?.Exit();
            Current = next;
            Current.Enter();
        }

        public void Tick()
        {
            Current?.Tick();
        }
    }
}
