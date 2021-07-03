using LUC.Interfaces;
using System;
using System.ComponentModel.Composition;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LUC.DiscoveryService
{
    class DiscoveryServiceSocket : Socket
    {
        private readonly Object lockerConnect = new Object();
        private readonly AutoResetEvent connectDone = new AutoResetEvent(initialState: false);

        private readonly Object lockerReceive = new Object();
        private readonly AutoResetEvent receiveDone = new AutoResetEvent(initialState: false);

        private readonly Object lockerSend = new Object();
        private readonly AutoResetEvent sendDone = new AutoResetEvent(initialState: false);

        [Import(typeof(ILoggingService))]
        public ILoggingService LoggingService { get; set; }

        /// <inheritdoc/>
        public DiscoveryServiceSocket(SocketType socketType, ProtocolType protocolType)
            : base(socketType, protocolType)
        {
            ;//do nothing
        }

        /// <inheritdoc/>
        public DiscoveryServiceSocket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
            : base(addressFamily, socketType, protocolType)
        {
            ;//do nothing
        }

        public void Connect(EndPoint remoteEndPoint, TimeSpan timeout, out Boolean isConnected)
        {
            lock(lockerConnect)
            {
                BeginConnect(remoteEndPoint, new AsyncCallback(ConnectCallback), this);
                isConnected = connectDone.WaitOne(timeout);
            }
        }

        private void ConnectCallback(IAsyncResult asyncResult)
        {
            Socket client = null;
            try
            {
                //Retrieve the socket from the state object
                client = (Socket)asyncResult.AsyncState;

                //Complete the connection
                client.EndConnect(asyncResult);
                //LoggingService.LogInfo($"Socket connected to {client.RemoteEndPoint}");

                //Signal that the connection has been made
                connectDone.Set();
            }
            catch(Exception e)
            {
                //LoggingService.LogInfo($"Failed to connect to {client?.RemoteEndPoint}: {e.Message}");
            }
        }

        public Byte[] Receive(TimeSpan timeout, out Boolean isReceived)
        {
            lock(lockerReceive)
            {
                StateObjectForReceivingData stateReceiving = new StateObjectForReceivingData
                {
                    WorkSocket = this
                };

                BeginReceive(stateReceiving.Buffer, offset: 0, stateReceiving.BufferSize,
                SocketFlags.None, new AsyncCallback(ReceiveCallback), stateReceiving);

                isReceived = receiveDone.WaitOne(timeout);
                return stateReceiving.ResultMessage.ToArray();
            }
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            StateObjectForReceivingData stateReceiving = (StateObjectForReceivingData)asyncResult.AsyncState;
            var client = stateReceiving.WorkSocket;

            try
            {
                //Read data from the remote device
                var bytesRead = client.EndReceive(asyncResult);

                if (bytesRead > 0)
                {
                    //There might be more data, so store the data received so far
                    stateReceiving.ResultMessage.AddRange(stateReceiving.Buffer);

                    //Get the rest of the data
                    client.BeginReceive(stateReceiving.Buffer, offset: 0, stateReceiving.BufferSize, SocketFlags.None, new AsyncCallback(ReceiveCallback), stateReceiving);
                }
                else
                {
                    //All the data has arrived; put it in response
                    receiveDone.Set();
                }
            }
            catch (Exception ex)
            {
                //LoggingService.LogInfo($"Exception occurred during send operation: {ex.Message}");
            }            
        }

        public void Send(Byte[] bytesToSend, TimeSpan timeout, out Boolean isSent)
        {
            lock(lockerSend)
            {
                //Begin sending the data to the remote device
                BeginSend(bytesToSend, offset: 0, bytesToSend.Length, SocketFlags.None, new AsyncCallback(SendCallback), this);
                isSent = sendDone.WaitOne(timeout);
            }
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            try
            {
                //Retrieve the socket from the state object
                Socket client = (Socket)asyncResult.AsyncState;

                //Complete sending the data to the remote device
                var bytesSent = client.EndSend(asyncResult);
                //LoggingService.LogInfo($"Sent {bytesSent} bytes to {client.RemoteEndPoint}");

                sendDone.Set();
            }
            catch(Exception ex)
            {
                //LoggingService.LogInfo($"Exception occurred during send operation: {ex.Message}");
            }
        }
    }
}
