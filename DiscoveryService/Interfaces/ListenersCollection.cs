using DiscoveryServices.Messages;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DiscoveryServices.Interfaces
{
    internal abstract class ListenersCollection<TEventArgs, TListener> : IDisposable
        where TEventArgs : MessageEventArgs
        where TListener : class, IDisposable
    {
        public event EventHandler<TEventArgs> MessageReceived;

        protected readonly IEnumerable<IPAddress> m_reachableIpAddresses;

        protected Boolean m_isListening;

        protected ListenersCollection( Boolean useIpv4, Boolean useIpv6, IEnumerable<IPAddress> reachableIpAddresses, Int32 listeningPort )
        {
            UseIpv4 = useIpv4;
            UseIpv6 = useIpv6;

            Listeners = new List<TListener>();
            ListeningPort = listeningPort;

            m_reachableIpAddresses = reachableIpAddresses.Where( c => ( ( c.AddressFamily == AddressFamily.InterNetwork ) && UseIpv4 ) ||
                                                                      ( ( c.AddressFamily == AddressFamily.InterNetworkV6 ) && UseIpv6 ) );
        }        

        /// <summary>
        /// Flag indicating whether Discovery Service should use IPv4 protocol.
        /// </summary>
        public Boolean UseIpv4 { get; }

        /// <summary>
        /// Flag indicating whether Discovery Service should use IPv6 protocol.
        /// </summary>
        public Boolean UseIpv6 { get; }

        public Int32 ListeningPort { get; }

        protected virtual IList<TListener> Listeners { get; }

        // To detect redundant calls
        protected virtual Boolean DisposedValue { get; set; }

        public abstract void StartMessagesReceiving();

        protected abstract void StartNextMessageReceiving( TListener listener );

        protected void InvokeMessageReceived( TEventArgs eventArgs ) => 
            MessageReceived?.Invoke( this, eventArgs );

        protected void InvokeMessageReceived( Object sender, TEventArgs eventArgs ) =>
            MessageReceived?.Invoke( sender, eventArgs );

        #region IDisposable Support
        
        public void Dispose()
        {
            Dispose( disposing: true );
            GC.SuppressFinalize( this );
        }

        // This code is added to correctly implement the disposable pattern.
        protected virtual void Dispose( Boolean disposing )
        {
            if ( !DisposedValue )
            {
                MessageReceived = null;

                foreach ( TListener receiver in Listeners )
                {
                    try
                    {
                        receiver.Dispose();
                    }
                    catch
                    {
                        // eat it.
                    }
                }

                Listeners.Clear();

                m_isListening = false;

                DisposedValue = true;
            }
        }

        #endregion
    }
}
