using System;

/// <summary>동기 거래 도중의 저장 요청을 모아 가장 바깥 거래가 끝날 때 한 번 실행한다.</summary>
public sealed class ProsperitySaveGate
{
    private int _depth;
    private bool _pending;
    private Action _flush;

    public IDisposable Begin(Action flush)
    {
        if (flush == null) throw new ArgumentNullException(nameof(flush));
        if (_depth == 0) _flush = flush;
        _depth++;
        return new Scope(this);
    }

    public bool TryDefer()
    {
        if (_depth == 0) return false;
        _pending = true;
        return true;
    }

    private void End()
    {
        _depth--;
        if (_depth != 0) return;
        var flush = _flush;
        bool pending = _pending;
        _flush = null;
        _pending = false;
        if (pending) flush();
    }

    private sealed class Scope : IDisposable
    {
        private ProsperitySaveGate _owner;
        public Scope(ProsperitySaveGate owner) => _owner = owner;
        public void Dispose()
        {
            if (_owner == null) return;
            var owner = _owner;
            _owner = null;
            owner.End();
        }
    }
}
