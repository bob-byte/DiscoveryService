using System;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace LUC.DiscoveryService
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
                sslStream.AuthenticateAsServer(certificate, true, System.Security.Authentication.SslProtocols.Tls12, true);
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
            SslStream sslStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false, new RemoteCertificateValidationCallback(ValidateServerCertificate));
            
            sslStream.AuthenticateAsServer(certificate, clientCertificateRequired: true, checkCertificateRevocation: true);

            Int32 countDataToReadAtTime = 256;
            Byte[] buffer = new Byte[countDataToReadAtTime];

            sslStream.ReadTimeout = timeout;
            sslStream.Read(buffer, 0, countDataToReadAtTime);

            return buffer;
        }

        private Boolean ValidateServerCertificate(Object sender, X509Certificate certificate, 
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            Boolean result = false;

            switch(sslPolicyErrors)
            {
                case SslPolicyErrors.None:
                    {
                        result = true;
                        break;
                    }

                case SslPolicyErrors.RemoteCertificateNameMismatch:
                    {
                        X509Certificate2 certificate2 = new X509Certificate2(certificate);
                        String certificateHolder = certificate2.GetNameInfo(X509NameType.SimpleName, false);
                        String cleanName = certificateHolder.Substring(certificateHolder.LastIndexOf('*') + 1);
                        String[] addresses = null/*{ serverAddress, serverSniName }*/;

                        //if the ending of the SNI and serverName do match the common name of the certificate2, fail
                        result = addresses.Where(item => item.EndsWith(cleanName)).Count() == addresses.Count();

                        break;
                    }

                default:
                    {
                        result = false;
                        break;
                    }
            }

            return result;
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
