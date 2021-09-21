# EccBrute
Brute force 33-63 bit ECC private keys. I was able to check the entire keyspace of a 40-bit curve in under 24 hours using 8 cores on an i7-6700K.

Create a work.ini file in the EecBrut directory that follows the below format.

```INI
q=[EC Q parameter (aka modulus / prime)]
a=[EC a parameter]
b=[EC b parameter]
gx=[Generator point X coordinate]
gy=[Generator point Y coordinate]
order=[EC order (not currently used for anything, but the largest private key should not be larger than this)]
start=[First private key to test]
end=[Last private key to test]
threads=[# of worker threads to spawn]
publickeys=[Comma-deliminated list of public keys to crack. Keys should be a base-64 encoded, big-endien byte array of the X coordinate followed by the Y coordinate.
```
