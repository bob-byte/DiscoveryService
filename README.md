Discovery Service
=================

It was developed a Discovery Service in Visual Studio 2022 .NET Framework that allows clients to define local network nodes using 
an optimized pool of TCP connections and multicast of UDP messages. This project uses the Kademlia protocol to update collected data, 
and quickly and safely download files from set of nodes.
To present the work of this service, it was integrated with the Light Upon Cloud file sharing service, console and Docker-compose 
projects were implemented. An installation of the file exchange application was fixed and also added adjustments to a file synchronization, 
logging in and thread safety.


How it works
============

1. It sends periodical UDP multicast message to port 17500 every 1 minute until it finds at least 1 contact (node), which uses DS.
2. Remote Discovery Service instances receive UDP datagrams and respond with TCP connection.
3. Discovery Service accepts TCP connection on port 17500 and saves groups, source IP and node ID.
4. Every 30 minutes DS updates info about found contacts using Kademlia protocol.
5. When DS found few nodes, you can download some specific file from a list of contacts.


For more information, see "Documentation.docx"
==============================================