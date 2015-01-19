
namespace DistributedSystems
{
    public interface MutualExclusionAlgorithm
    {
        // --- Public Properties -----------------------------------------
        bool HasToken { get; set; }

        // --- Public Methods -----------------------------------------
        void Start(int? Value = null);
        void Acquire(string ip = null);
        void Release();
    }
}
