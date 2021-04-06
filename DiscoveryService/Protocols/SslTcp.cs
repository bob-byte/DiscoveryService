using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace DiscoveryServices.Protocols
{
    class SslTcp
    {
        private TcpListener listener;

        public void Send(IPEndPoint endPoint, X509Certificate certificate, Byte[] sendBuf)
        {
            TcpClient client = new TcpClient(endPoint);

            SslStream sslStream = new SslStream(client.GetStream());
            try
            {
                sslStream.AuthenticateAsServer(certificate, true, true);
                sslStream.Write(sendBuf);
            }
            catch
            {
                throw;
            }
            finally
            {
                sslStream.Close();
                client.Close();
            }
        }

        public void StartListening(IPEndPoint endPoint)
        {
            listener = new TcpListener(endPoint);
            listener.Start();
        }

        public Byte[] Message(X509Certificate certificate, Int32 timeout)
        {
            var client = listener.AcceptTcpClient();
            SslStream sslStream = new SslStream(client.GetStream());
            sslStream.AuthenticateAsServer(certificate, clientCertificateRequired: true, checkCertificateRevocation: true);

            Int32 countDataToReadAtTime = 256;
            Byte[] buffer = new Byte[countDataToReadAtTime];

            sslStream.ReadTimeout = timeout;
            sslStream.Read(buffer, 0, countDataToReadAtTime);

            return buffer;
        }

        public void StopListening()
        {
            if(listener != null)
            {
                listener.Stop();
            }
            else
            {
                throw new NullReferenceException("Firstly you need to call method StartListening of this object");
            }
        }
    }
}
