using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using System;
using LUC.Interfaces;
using LUC.DiscoveryService.Kademlia.ClientPool;

namespace LUC.DiscoveryService.Kademlia
{
    public class Node : INode
    {
        private static ILoggingService log;
        private readonly UInt32 protocolVersion;
        private readonly KademliaOperation kademliaOperation;

        public Contact OurContact { get { return ourContact; } set { ourContact = value; } }
        public IBucketList BucketList { get { return bucketList; } set { bucketList = value; } }
        public IStorage Storage { get { return storage; } set { storage = value; } }
        public IStorage CacheStorage { get { return cacheStorage; } set { cacheStorage = value; } }

        [JsonIgnore]
        public Dht Dht { get { return dht; } set { dht = value; } }

        protected Contact ourContact;
        protected IBucketList bucketList;
        protected IStorage storage;
        protected IStorage cacheStorage;
        protected Dht dht;

        /// <summary>
        /// For serialization.
        /// </summary>
        public Node()
        {
        }

        /// <summary>
        /// If cache storage is not explicity provided, we use an in-memory virtual storage.
        /// </summary>
        public Node(Contact contact, UInt16 protocolVersion, ILoggingService loggingService, 
            IStorage storage, IStorage cacheStorage = null)
        {
            ourContact = contact;
            bucketList = new BucketList(contact);
            this.storage = storage;
            this.cacheStorage = cacheStorage;

            if (cacheStorage == null)
            {
                this.cacheStorage = new VirtualStorage();
            }

            this.protocolVersion = protocolVersion;

            log = loggingService;
            kademliaOperation = new KademliaOperation(loggingService, protocolVersion);
        }

        /// <summary>
        /// Someone is pinging us.  Register the contact and respond.
        /// </summary>
        /// <param name="sender">
        /// Current peer
        /// </param>
        /// <returns>
        /// Current peer
        /// </returns>
        public Contact Ping(Contact sender)
        {
            Validate.IsFalse<SendingQueryToSelfException>(sender.ID == ourContact.ID, "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);
            bucketList.AddContact(sender);

            return ourContact;
        }

        /// <summary>
        /// Insturcts a node to store a (<paramref name="key"/>, <paramref name="val"/>) pair for later retrieval. 
        /// “To store a (<paramref name="key"/>, <paramref name="val"/>) pair, a participant locates the k closest nodes to the key and sends them STORE RPCS.” 
        /// The participant does this by inspecting its own k-closest nodes to the key.
        /// Store a key-value pair in the republish or cache storage.
        /// </summary>
        /// <param name="sender">
        /// Current peer
        /// </param>
        /// <param name="key">
        /// 160-bit <seealso cref="ID"/> which we want to store
        /// </param>
        /// <param name="val">
        /// Value of the peer which we want to store
        /// </param>
        /// <param name="isCached">
        /// Whether cach a (<paramref name="key"/>, <paramref name="val"/>) pair
        /// </param>
        /// <param name="expirationTimeSec">
        /// Time of expiration cach in seconds
        /// </param>
        /// <returns>
        /// Information about the errors which maybe happened
        /// </returns>
        public void Store(Contact sender, ID key, string val, bool isCached = false, int expirationTimeSec = 0)
        {
            Validate.IsFalse<SendingQueryToSelfException>(sender.ID == ourContact.ID, "Sender should not be ourself!");
            bucketList.AddContact(sender);

            if (isCached)
            {
                cacheStorage.Set(key, val, expirationTimeSec);
            }
            else
            {
                SendKeyValuesIfNewContact(sender);
                storage.Set(key, val, Constants.EXPIRATION_TIME_SECONDS);
            }
        }

        /// <summary>
        /// This operation has two purposes:
        /// <list type="bullet">
        /// <item>
        /// A peer can issue this RPC(remote procedure call) on contacts it knows about, updating its own list of "close" peers
        /// </item>
        /// <item>
        /// A peer may issue this RPC to discover other peers on the network
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="sender">
        /// Current peer
        /// </param>
        /// <param name="key">
        /// 160-bit ID near which you want to get list of contacts
        /// </param>
        /// <returns>
        /// <see cref="Constants.K"/> nodes the recipient of the RPC  knows about closest to the target <paramref name="key"/>. 
        /// Contacts can come from a single k-bucket, or they may come from multiple k-buckets if the closest k-bucket is not full. 
        /// In any case, the RPC recipient must return k items 
        /// (unless there are fewer than k nodes in all its k-buckets combined, in which case it returns every node it knows about).
        /// Also this operation returns <seealso cref="RpcError"/> to inform about the errors which maybe happened
        /// </returns>
        //TODO it shouldn't return val
        public (List<Contact> contacts, string val) FindNode(Contact sender, ID key)
        {
            Validate.IsFalse<SendingQueryToSelfException>(sender.ID == ourContact.ID, "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);
            bucketList.AddContact(sender);

            // Exclude sender.
            var contacts = bucketList.GetCloseContacts(key, sender.ID);

            return (contacts, null);
        }

        /// <summary>
        /// Attempt to find the value in the peer network. Also this operation has two purposes:
        /// <list type="bullet">
        /// <item>
        /// A peer can issue this RPC(remote procedure call) on contacts it knows about, updating its own list of "close" peers
        /// </item>
        /// <item>
        /// A peer may issue this RPC to discover other peers on the network
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="sender">
        /// Current peer
        /// </param>
        /// <param name="key">
        /// 160-bit ID near which you want to get list of contacts
        /// </param>
        /// <returns>
        /// Returns either a list of close contacts or a the value, if the node's storage contains the value for the key.
        /// </returns>
        public (List<Contact> contacts, string val) FindValue(Contact sender, ID key)
        {
            Validate.IsFalse<SendingQueryToSelfException>(sender.ID == ourContact.ID, "Sender should not be ourself!");
            SendKeyValuesIfNewContact(sender);
            bucketList.AddContact(sender);

            if (storage.Contains(key))
            {
                return (null, storage.Get(key));
            }
            else if (CacheStorage.Contains(key))
            {
                return (null, CacheStorage.Get(key));
            }
            else
            {
                // Exclude sender.
                return (bucketList.GetCloseContacts(key, sender.ID), null);
            }
        }

#if DEBUG           // For unit testing
        public void SimpleStore(ID key, string val)
        {
            storage.Set(key, val);
        }
#endif

        /// <summary>
        /// For a new contact, we store values to that contact whose keys ^ ourContact are less than stored keys ^ [otherContacts].
        /// </summary>
        protected void SendKeyValuesIfNewContact(Contact sender)
        {
            List<Contact> contacts = new List<Contact>();

            if (IsNewContact(sender))
            {
                lock (bucketList)
                {
                    // Clone so we can release the lock.
                    contacts = new List<Contact>(bucketList.Buckets.SelectMany(b => b.Contacts));
                }

                if (contacts.Count() > 0)
                {
                    // and our distance to the key < any other contact's distance to the key...
                    storage.Keys.AsParallel().ForEach(k =>
                    {
                    // our min distance to the contact.
                    var distance = contacts.Min(c => k ^ c.ID);

                    // If our contact is closer, store the contact on its node.
                    if ((k ^ ourContact.ID) < distance)
                        {
                            var error = Store(ourContact, new ID(k), storage.Get(k), sender.local_IpAddresses);
                            dht?.HandleError(error, sender);
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Returns true if the contact isn't in the bucket list or the pending contacts list.
        /// </summary>
        protected bool IsNewContact(Contact sender)
        {
            bool ret;

            lock (bucketList)
            {
                // If we have a new contact...
                ret = bucketList.ContactExists(sender);
            }

            if (dht != null)            // for unit testing, dht may be null
            {
                lock (dht.PendingContacts)
                {
                    ret |= dht.PendingContacts.ContainsBy(sender, c => c.ID);
                }
            }

            return !ret;
        }

        /// <inheritdoc/>
        public RpcError Ping(Contact sender, EndPoint endPointToPing) =>


        private void ClientStart<TResponse>(EndPoint remoteEndPoint, Request request, out TResponse response)
            where TResponse : Response, new()
        {
            response = null;
            var requestBytes = request.ToByteArray();

            var client = connectionPool.SocketAsync(remoteEndPoint, Constants.ConnectTimeout, IOBehavior.Synchronous, Constants.TimeWaitReturnToPool).GetAwaiter().GetResult();

            try
            {
                Boolean isReceived = false;

                SendWithAvoidErrorsInNetwork(requestBytes, Constants.SendTimeout, Constants.ConnectTimeout, 
                    ref client, out var isSent);
                if (isSent)
                {
                    Thread.Sleep(Constants.TimeWaitResponse);

                    var bytesOfResponse = client.Receive(Constants.ReceiveTimeout, out isReceived);
                    if (isReceived)
                    {
                        response = new TResponse();
                        response.Read(bytesOfResponse);
                    }
                }

                if(!isSent || !isReceived)
                {
                    throw new TimeoutException();
                }
            }
            finally
            {
                client.ReturnToPoolAsync(IOBehavior.Synchronous).ConfigureAwait(continueOnCapturedContext: false);
            }
        }

        public static void SendWithAvoidErrorsInNetwork(Byte[] bytesToSend, TimeSpan timeoutToSend, TimeSpan timeoutToConnect, 
            ref ConnectionPoolSocket client, out Boolean isSent)
        {
            client.Send(bytesToSend, timeoutToSend, out isSent);

            if (!isSent)
            {
                client = new ConnectionPoolSocket(client.Id.AddressFamily, SocketType.Stream, ProtocolType.Tcp, client.Id, client.Pool, client.Log);
                client.Connect(client.Id, timeoutToConnect, out var isConnected);

                if(isConnected)
                {
                    client.Send(bytesToSend, timeoutToSend, out isSent);
                }
            }
        }

        ///<inheritdoc/>
        public RpcError Store(Contact sender, ID key, string val, bool isCached = false, int expirationTimeSec = 0)
        {
            ID id = ID.RandomID;
            var request = new StoreRequest
            {
                Sender = sender.ID.Value,
                KeyToStore = key.Value,
                Value = val,
                IsCached = isCached,
                ExpirationTimeSec = expirationTimeSec,
                RandomID = id.Value,
                MessageOperation = MessageOperation.Store
            };

            var remoteContact = DiscoveryService.KnownContacts(protocolVersion).Single(c => c.ID == key);
            
            ErrorResponse peerError = null;
            StoreResponse response = null;
            Boolean timeout;

            try
            {
                ClientStart(remoteContact.EndPoint, request, out response);
                timeout = false;

                log.LogInfo($"The response is received:\n{response}");
            }
            catch (TimeoutException ex)
            {
                timeout = true;
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
            }
            catch (Exception ex)
            {
                timeout = false;
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
            }

            return RpcError(id, response, timeout, peerError);
        }

        /// <inheritdoc/>
        public (List<Contact> contacts, RpcError error) FindNode(Contact sender, ID keyToFindContacts/*, IPAddress host, Int32 tcpPort*/)
        {
            ID id = ID.RandomID;
            Boolean timeoutError = false;
            var request = new FindNodeRequest
            {
                Sender = sender.ID.Value,
                ContactId = keyToFindContacts.Value,
                RandomID = id.Value,
                MessageOperation = MessageOperation.FindNode
            };
            FindNodeResponse response = null;
            var remoteContact = DiscoveryService.KnownContacts(protocolVersion).Single(c => c.ID == keyToFindContacts);
            ErrorResponse peerError = null;
            List<Contact> closeContactsToKey = null;

            try
            {
                ClientStart(remoteContact.EndPoint, request, out response);

                //get close contacts near key
                closeContactsToKey = response.CloseSenderContacts.ToList();

                log.LogInfo($"The response is received:\n{response}");
            }
            catch (SocketException ex)
            {
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                timeoutError = true;
            }

            return (closeContactsToKey ?? EmptyContactList(), RpcError(id, response, timeoutError, peerError));
        }

        protected List<Contact> EmptyContactList()
        {
            return new List<Contact>();
        }

        /// <inheritdoc/>
        public (List<Contact> contacts, string val, RpcError error) FindValue(Contact sender, ID keyToFindContact)
        {
            ID id = ID.RandomID;
            bool timeoutError = false;

            var request = new FindValueRequest
            {
                ContactId = keyToFindContact.Value,
                MessageOperation = MessageOperation.FindValue,
                Sender = sender.ID.Value,
                RandomID = id.Value,
            };
            
            var remoteContact = DiscoveryService.KnownContacts(protocolVersion).Single(c => c.ID == keyToFindContact);
            FindValueResponse response = null;
            List<Contact> closeContactsToKey = null;
            ErrorResponse peerError = new ErrorResponse();

            try
            {
                ClientStart(remoteContact.EndPoint, request, out response);

                //get close contacts near key
                closeContactsToKey = response?.CloseContactsToRepsonsingPeer.ToList()/*.Select(val => new Contact(Protocol.InstantiateProtocol(val.Protocol, val.ProtocolName), new ID(val.Contact))).ToList()*/;

                log.LogInfo($"The response is received:\n{response}");
                // Return only contacts with supported protocols.
                //return (contacts?.Where(c => c.Protocol != null).ToList(), ret.Value, GetRpcError(id, ret, timeoutError, error));
            }
            catch (TimeoutException ex)
            {
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                timeoutError = true;
            }
            catch (Exception ex)
            {
                peerError = new ErrorResponse
                {
                    ErrorMessage = ex.Message
                };
                timeoutError = false;
            }

            return (closeContactsToKey ?? EmptyContactList(), response?.ValueInResponsingPeer, RpcError(id, response, timeoutError, peerError));
        }

        protected RpcError RpcError(ID id, Response resp, bool timeoutError, ErrorResponse peerError)
        {
            RpcError rpcError = new RpcError
            {
                TimeoutError = timeoutError,
                PeerError = peerError != null,
                PeerErrorMessage = peerError?.ErrorMessage
            };

            if((resp != null) && (id != new ID(bi: default)))
            {
                rpcError.IDMismatchError = id != resp.RandomID;
            }
            else
            {
                rpcError.IDMismatchError = false;
            }

            return rpcError;
        }



    }
}