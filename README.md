# EccBrute
Brute force 33-42 bit ECC private keys. I was able to check the entire keyspace of a 40-bit curve in under 18 hours using 8 cores on an i7-6700K.

The speed of this program comes from the fast mulmod algorithm. Mulmod, or the modular product, is a * b mod q.  When a * b overflows 64 bits, the modulus can't be computed in a single instruction.  This mulmod implementation uses Math.BigMul to calculate and store the product in two qwords, then calculates the modulus in two steps.  This only works if bitlength(a * b) + bitlength(q) <= 128. In practice, this means it will only work if the curve's Q parameter is 42 bits or less. This will work for Qs smaller than 33 bits, but in those cases the product would fit in a qword and you can use the language's built-in mod operator. Elliptic curve point addition requires 11 mulmood operations, and the speed of that operation makes or breakes the brute force time. 

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
Progress is continually saved to work.json, and you may resume a job any time. Cracked keys are stored in the work.json.
