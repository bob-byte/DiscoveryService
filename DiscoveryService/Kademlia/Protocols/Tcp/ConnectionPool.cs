using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Numerics;

namespace LUC.DiscoveryService.Kademlia.Protocols.Tcp
{
    class ConnectionPool<T>
        where T: EndPoint
    {
        private readonly ConcurrentDictionary<BigInteger, DiscoveryServiceSocket> availableSockets;

        private readonly IEqualityComparer<T> comparer;

        public ConnectionPool(Int32 poolMaxSize, IEqualityComparer<T> comparer)
        {
            availableSockets = new ConcurrentDictionary<BigInteger, DiscoveryServiceSocket>(Constants.MAX_THREADS, poolMaxSize);
            PoolMaxSize = poolMaxSize;
            this.comparer = comparer;
        }

        public Int32 PoolMaxSize { get; }

        public TimeSpan TimeoutToConnect { get; }

        public Boolean IsPoolFull() =>
            availableSockets.Count == PoolMaxSize;

        public void PutSocketInPool(DiscoveryServiceSocket client, BigInteger idOfClient, T remoteWantConnectPoint, 
            TimeSpan timeoutToConnect, out Boolean isNowAdded, out Boolean isRewritenClient, out Boolean isConnected)
        {
            if (!IsPoolFull())
            {
                if(!availableSockets.ContainsKey(idOfClient))
                {
                    isRewritenClient = false;
                }
                else
                {
                    if(client.Connected)
                    {
                        var remoteConnectedPoint = client.RemoteEndPoint as T;

                        if(comparer.Equals(remoteConnectedPoint, remoteWantConnectPoint))
                        {
                            isConnected = true;
                            isNowAdded = false;
                            isRewritenClient = false;

                            return;
                        }
                    }

                    availableSockets.TryRemove(idOfClient, out _);
                    isRewritenClient = true;
                }


                isNowAdded = availableSockets.TryAdd(idOfClient, client);
                Connect(idOfClient, remoteWantConnectPoint, timeoutToConnect, out isConnected);
            }
            else
            {
                isNowAdded = false;
                isRewritenClient = false;
                isConnected = false;
            }
        }

        private void Connect(BigInteger idOfClient, EndPoint remoteEndPoint, TimeSpan timeoutToConnect, out Boolean isConnected)
        {
            var client = availableSockets[idOfClient];
            client.Connect(remoteEndPoint, timeoutToConnect, out isConnected);
        }

        public void TakeClient(BigInteger idOfClient, T remoteEndPoint, TimeSpan timeoutToConnect, 
            out DiscoveryServiceSocket searchedClient, out Boolean isConnected, out Boolean isInPool)
        {
            isInPool = availableSockets.ContainsKey(idOfClient);

            if (isInPool)
            {
                searchedClient = availableSockets[idOfClient];
                var remoteEndPointInPool = searchedClient.RemoteEndPoint as T;

                if(!comparer.Equals(remoteEndPoint, remoteEndPointInPool))
                {
                    Connect(idOfClient, remoteEndPoint, timeoutToConnect, out isConnected);
                }
                else
                {
                    isConnected = searchedClient.Connected;
                }
            }
            else
            {
                searchedClient = null;
                isConnected = false;
                isInPool = false;
            }
        }
    }
}
