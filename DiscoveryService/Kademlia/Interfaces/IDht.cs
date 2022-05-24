using LUC.Interfaces.Discoveries;

namespace DiscoveryServices.Kademlia.Interfaces
{
    interface IDht
    {
        Node Node { get; set; }
        void DelayEviction( IContact toEvict, IContact toReplace );
        void AddToPending( IContact pending );
    }
}
