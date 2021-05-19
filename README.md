Discovery Service
=================

Finds IP addresses within local network, maintains the list of IPs and groups 
found within LAN.


How it works
============

1. It sends UDP multicast message to port 17500
2. Remote Discovery Service instances receive UDP datagrams and respond with TCP connection
3. Discovery Service accepts TCP connection on port 17500 and saves groups and source IP


Test Cases
==========

* make sure big TCP responses pass through: hundreds of groups
* to check the case when network interface is turned off and on again:
  do we resume listening on that interface ( TCP listener ) ?


TODO
====

* in case ip or port of "known IP" changes, we have to remove that IP 
  from knownIPs and let DS to discover the new address on the next check
  KAD request is supposed to raise exception if it fails to connect.

* to add events:
    - on groups change
    - on tcp port change

