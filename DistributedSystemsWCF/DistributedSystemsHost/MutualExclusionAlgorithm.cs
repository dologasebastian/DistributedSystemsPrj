
namespace DistributedSystems
{
    public interface MutualExclusionAlgorithm
    {
        // --- Public Properties -----------------------------------------
        bool HasToken { get; set; }

        // --- Public Methods -----------------------------------------
        void Start(int? Value = null);
        void Acquire(System.Tuple<long, string> receivedLC = null);
        void Release();
    }
}
