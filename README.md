# tvdp-simpleinjector-interception

The project is an adaptation of interception example 
code found here: https://simpleinjector.readthedocs.io/en/latest/InterceptionExtensions.html

This version is based on proxy generation by Castle.Core.
It improves on the original in the following ways:
1.Generates 1 proxy that services multiple interceptors, instead of a seperate proxy for every interceptor.
2.Has the possibility to set certain options for the proxy (like adding attributes)
3.Offers the possibility to supply an explict order for the interceptors, so that the 
	order of interception can be decoupled from the order of registration.
4.Has methods to register fake implementations of services. All functionality would
	then need to be supplied by interceptors.

version 0.9.1
	- Upgraded minimum reequired SimpleInjector version to 5.0.1 due to a dependency load bug with version 5.0.0