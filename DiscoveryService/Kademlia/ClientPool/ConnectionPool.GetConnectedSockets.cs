using DiscoveryServices.Common;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DiscoveryServices.Kademlia.ClientPool
{
    partial class ConnectionPool
    {
        private async ValueTask<Socket> CreatedOrTakenSocketAsync(EndPoint remoteEndPoint, TimeSpan timeoutToConnect, TimeSpan timeWaitToReturnToPool, IoBehavior ioBehavior, CancellationToken cancellationToken = default)
        {
            (Boolean isTaken, Socket desiredSocket) = await TryTakeLeasedSocketAsync(remoteEndPoint, ioBehavior, timeWaitToReturnToPool, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);

            if (isTaken)
            {
                desiredSocket = await TakenSocketWithRecoveredConnectionAsync(remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket).
                    ConfigureAwait(false);
            }
            else
            {
                //desiredSocket may not be in pool, because it can be removed 
                //by ConnectionPool.TryRecoverAllConnectionsAsync or disposed or is not created
                isTaken = m_sockets.TryRemove(remoteEndPoint, out desiredSocket);

                if (isTaken)
                {
                    await TakeFromPoolAsync(desiredSocket, ioBehavior, timeWaitToReturnToPool, cancellationToken).ConfigureAwait(false);

                    desiredSocket = await ConnectedSocketAsync(remoteEndPoint, timeoutToConnect, ioBehavior, desiredSocket, createNewSocketIfDisposed: true).
                        ConfigureAwait(false);
                }
                else
                {
                    desiredSocket = await CreatedConnectedSocketAsync(remoteEndPoint, timeoutToConnect, ioBehavior).ConfigureAwait(false);

                    await TakeFromPoolAsync(desiredSocket, ioBehavior, timeWaitToReturnToPool, cancellationToken).ConfigureAwait(false);
                }

                AddLeasedSocket(remoteEndPoint, desiredSocket);
            }

            return desiredSocket;
        }

        private async ValueTask<Socket> ConnectedSocketAsync(EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IoBehavior ioBehavior, Socket socket, Boolean createNewSocketIfDisposed, Boolean handleRemoteException = true, CancellationToken cancellationToken = default)
        {
            Socket connectedSocket = socket;
            if (socket != null)
            {
                try
                {
                    connectedSocket.VerifyConnected();
                }
                catch (SocketException)
                {
                    try
                    {
                        await connectedSocket.DsConnectAsync(remoteEndPoint, timeoutToConnect, ioBehavior, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                    }
                    catch (SocketException)
                    {
                        try
                        {
                            connectedSocket = socket.NewSimilarSocket(socket.StateInPool);

                            await connectedSocket.DsConnectAsync(remoteEndPoint, timeoutToConnect, ioBehavior, cancellationToken).
                                ConfigureAwait(false);
                        }
                        catch (SocketException ex)
                        {
                            if ( handleRemoteException )
                            {
                                HandleRemoteException(ex, connectedSocket);
                            }
                        }
                        catch (TimeoutException ex)
                        {
                            if (handleRemoteException)
                            {
                                HandleRemoteException(ex, connectedSocket);
                            }
                        }
                    }
                    catch (TimeoutException ex)
                    {
                        if (handleRemoteException)
                        {
                            HandleRemoteException(ex, connectedSocket);
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        if (createNewSocketIfDisposed)
                        {
                            connectedSocket = socket.NewSimilarSocket(SocketStateInPool.TakenFromPool);
                            await connectedSocket.DsConnectAsync(remoteEndPoint, timeoutToConnect, ioBehavior, cancellationToken).
                                ConfigureAwait(false);
                        }
                        else if (handleRemoteException)
                        {
                            HandleRemoteException(ex, connectedSocket);
                        }
                    }
                }
                catch (ObjectDisposedException ex)
                {
                    if (createNewSocketIfDisposed)
                    {
                        connectedSocket = socket.NewSimilarSocket(SocketStateInPool.TakenFromPool);
                        await connectedSocket.DsConnectAsync(remoteEndPoint, timeoutToConnect, ioBehavior, cancellationToken).
                            ConfigureAwait(false);
                    }
                    else if (handleRemoteException)
                    {
                        HandleRemoteException(ex, connectedSocket);
                    }
                }
            }
            else
            {
                connectedSocket = await CreatedConnectedSocketAsync(remoteEndPoint, timeoutToConnect, ioBehavior, cancellationToken).ConfigureAwait(false);
            }

            return connectedSocket;
        }

        private async ValueTask<Socket> CreatedConnectedSocketAsync(EndPoint remoteEndPoint, TimeSpan timeoutToConnect, IoBehavior ioBehavior, CancellationToken cancellationToken = default)
        {
            var connectedSocket = new Socket( remoteEndPoint, s_instance );
            try
            {
                await connectedSocket.DsConnectAsync(remoteEndPoint, timeoutToConnect, ioBehavior, cancellationToken).
                    ConfigureAwait(false);
            }
            catch (SocketException)
            {
                ReleaseSocketSemaphore();
                connectedSocket.DisposeUnmanagedResourcesAndSetIsFailed();

                throw;
            }
            catch (TimeoutException)
            {
                ReleaseSocketSemaphore();
                connectedSocket.DisposeUnmanagedResourcesAndSetIsFailed();

                throw;
            }

            return connectedSocket;
        }        
    }
}
