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
