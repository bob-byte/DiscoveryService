Discovery Service
=================

Finds IP addresses within local network.


Test Cases
==========

* make sure big TCP responses pass through: hundreds of groups
* to check the case when network interface is turned off and on again:
  do we resume listening on that interface ( TCP listener ) ?


TODO
====

* to keep only one reference to ServiceProfile. Currently it is present in Client, Serivce and ServiceDiscovery

* to make sure TCP socket options are working ok

* in case ip or port of "known IP" changes, we have to remove that IP from knownIPs and let DS to discover the new address on the next check
  KAD request is supposed to raise exception if it fails to connect.

* to add events:
    - on groups change
    - on tcp port change

* to add function for checking what IP versions are supported: 4 and 6
