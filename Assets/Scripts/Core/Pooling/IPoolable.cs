namespace BlastFrame.Core.Pooling
{
    /// <summary>Implemented by every pooled object. The pool calls OnSpawn when handed out and
    /// the object returns itself via OnDespawn — callers never manually return objects.</summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }
}
