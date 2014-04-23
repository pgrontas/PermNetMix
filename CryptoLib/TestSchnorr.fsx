// Learn more about F# at http://fsharp.net. See the 'F# Tutorial' project
// for more guidance on F# programming.

#load "MillerRabin.fs"
#load "Common.fs"
#load "ElGamal.fs"
#load "Schnorr.fs"

open CryptoLib.Common
open CryptoLib.ElGamal
open CryptoLib.Schnorr


//let eg1 = new ElGamal()
//let (pk1,sk1) = eg1.KeyGen(32)
//let s = new Schnorr()
//let proof = s.prove(sk1,pk1)
//let ok = s.verify(proof)

let eg2 = new ElGamal()
let n=12
let (pk2,sk2) = eg2.KeyGen(512)
let pks = List.init n (fun _ -> fst(eg2.KeyGen(32)))
let sw = new SchnorrWID()
let proof2 = sw.prove(sk2,(pk2::pks))
let ok2 = sw.verify(proof2)