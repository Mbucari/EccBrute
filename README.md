# EccBrute
Crack 33-42 bit ECC private keys using Baby Step Giant Step. I was able to crack 46 private keys on a 40-bit curve in under 15 seconds using 8 cores on an i7-6700K.

This implementation is limited to curves with primes of 33-42 bits because:
1. That's all I needed
2. I sped up the modular product implementation in a way that only works for moduli of those sizes.

**Caveat!  This only works for curves with cofactor = 1.**

Create a work.ini file in the EccBrut directory that follows the below format.

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
publickeys=[Comma-deliminated list of public keys to crack. Keys should be a base-64 encoded, big-endian byte array of the X coordinate followed by the Y coordinate.]
```
