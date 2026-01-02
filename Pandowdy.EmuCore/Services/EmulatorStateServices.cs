using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.Services
{
    public record StateSnapshot(ushort PC, byte SP, ulong Cycles, int? LineNumber, bool IsRunning, bool IsPaused);


    public sealed class EmulatorStateProvider : IEmulatorState
    {
        private readonly System.Reactive.Subjects.BehaviorSubject<StateSnapshot> _subject = new(new StateSnapshot(0, 0, 0, null, false, false));
        public IObservable<StateSnapshot> Stream => _subject;
        public StateSnapshot GetCurrent() => _subject.Value;
        public void Update(StateSnapshot snapshot) => _subject.OnNext(snapshot);
        public void RequestPause() { /* placeholder */ }
        public void RequestContinue() { /* placeholder */ }
        public void RequestStep() { /* placeholder */ }
    }


}
