using DiscoveryServices.Protocols;
using System;
using System.Net;

namespace DiscoveryServices
{
    class Server
    {
        private const Int32 Port = 17500;

        public void ListenBroadcast(out IPEndPoint endPoint)
        {
            Broadcast udpClient = new Broadcast(Port);
            udpClient.Listen(out endPoint);
        }

        //private Boolean isCreated;
        //private List<String> ipsOfGroups;
        //private String descriptionDevice;
        //String pathToFileDescOfAllClients;
        //private String ipNetwork;
        //public String Id { get; set; }

        //public Server(String pathDescPC, String pathDescClients, String ipNetwork, params String[] ipsOfGroups)
        //{
        //    pathToFileDescOfAllClients = pathDescClients;
        //    this.ipNetwork = ipNetwork;
        //    if(File.Exists(pathDescPC))
        //    {
        //        var reader = new StreamReader(pathDescPC);
        //        var hasId = reader.ReadLine().Contains("MachineId");

        //        if(!hasId)
        //        {
        //            GenerateNewFileForPC(pathDescPC, ipsOfGroups);
        //        }
        //    }
        //    else
        //    {
        //        GenerateNewFileForPC(pathDescPC, ipsOfGroups);
        //    }
        //}

        //private void GenerateNewFileForPC(String path, String[] ipsOfGroups)
        //{
        //    using (var writer = new StreamWriter(path))
        //    {
        //        StringBuilder lineForWriting = new StringBuilder($"MachineId={GetUniqueId()};");

        //        if (ipsOfGroups != null)
        //        {
        //            lineForWriting.Append("Groups=");

        //            foreach (var ipGroup in ipsOfGroups)
        //            {
        //                writer.Write($"{ipGroup};");
        //            }

        //            this.ipsOfGroups.AddRange(ipsOfGroups);
        //        }

        //        descriptionDevice = lineForWriting.ToString();

        //        writer.Write(descriptionDevice);
        //    }
        //}





        //Int32 GetPort()
        //{

        //}





        //Byte[] GetBroadcastMessageForAll(String Id)
        //{
        //    var groupsSupported = GetGroupsSupported(pathToFileDescOfAllClients, 2);
        //    StringBuilder message = new StringBuilder($"Group=UDP&The_Groups_Supported=");
        //    foreach (var group in groupsSupported)
        //    {
        //        message.Append($"{group};");
        //    }
        //    message.Append($"&TCP_Port={PORT}&Random_Identifier={Id}");
        //    var bytes = Encoding.UTF8.GetBytes(message.ToString());

        //    return bytes;
        //}



        //static Server()
        //{
        //    if (!isCreated)
        //    {
        //        CreateFileDescriptionClients();
        //    }

        //    Tcp tcpListener = new Tcp();
        //    Tcp.Listen(ref endPoint);
        //    tcpListener.Send();


        //    Broadcast udpClient = new Udp();
        //    Byte[] bytes;
        //    IPEndPoint endPoint;
        //    udpClient.Listen(out endPoint, out bytes);
        //    udpClient.SendBroadcast()

        //}

        //public void GetChunk()
        //{
        //    if(Client.IsAuthentificated)
        //    {

        //    }
        //}

        //void IsAuthentificated()
        //{

        //}

        //Char separator;

        //private void CreateFileDescriptionClients(String nameFile, Char separator)
        //{
        //    //If we set false - creating new file
        //    using(StreamWriter writer = new StreamWriter(nameFile, false))
        //    {
        //        writer.WriteLine($"Id of the machine{separator}Group");
        //    }
        //    this.separator = separator;
        //}




    }
}
