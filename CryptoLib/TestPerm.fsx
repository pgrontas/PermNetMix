#load "MillerRabin.fs"
#load "Common.fs"
#load "ElGamal.fs"
#load "Schnorr.fs"
#load "PermNet.fs"

open CryptoLib.PermNet
open CryptoLib.ElGamal 
open CryptoLib.Common
open CryptoLib.Schnorr

let testPEP() = 
    let eg = new ElGamal()
    let (pk,sk) = eg.KeyGen(32)
    let ((g,p,q),y) = pk
    let c1 = eg.Encrypt(32I,pk)
    let r = getRandom(q)
    let c2 = eg.ReEncrypt(c1,pk,r)
    let proof = PEProof c1 c2 pk r
    printfn "%A" proof
    let sn = new Schnorr()
    sn.verify(proof)

let testDPEP() = 
    let eg = new ElGamal()
    let (pk,sk) = eg.KeyGen(32)
    let ((g,p,q),y) = pk
    let c0 = eg.Encrypt(32I,pk)
    let r = getRandom(q)
    let c1 = eg.ReEncrypt(c0,pk,r)
    let c2 = (getRandom(p),getRandom(p))
    let proof1 = DISPEProof c0 c1 c2 pk r
    let proof2 = DISPEProof c0 c2 c1 pk r
    let sn = new SchnorrWID()
    sn.verify(proof1), sn.verify(proof2)

let test() = 
    let eg = new ElGamal()
    let (pk,sk) = eg.KeyGen(128)
    let c1 = eg.Encrypt(32I,pk)
    let c2 = eg.Encrypt(45I,pk)
    let ((c1',c2'),(p1,p2)) = switchingGate c1 c2 pk
    let m1' = eg.Decrypt(c1',sk)
    let m2' = eg.Decrypt(c2',sk)
    printfn "%A" (m1',m2')

let testn n =
    let eg = new ElGamal()
    let (pk,sk) = eg.KeyGen(256)
    let rnd = new System.Random()
    let ls = List.init n (fun _ -> getRandom(1024I))
    let cs = ls|>List.map (fun m -> eg.Encrypt(m,pk))
    let cs',proofs = PEProof cs pk
    let ls' = cs'|>List.map (fun c -> eg.Decrypt(c,sk))
    printfn "%A" (ls,ls')