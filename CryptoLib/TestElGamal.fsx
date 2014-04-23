// Learn more about F# at http://fsharp.net. See the 'F# Tutorial' project
// for more guidance on F# programming.

#load "MillerRabin.fs"
#load "Common.fs"
#load "ElGamal.fs"
#load "Schnorr.fs"

open CryptoLib.Common
open CryptoLib.ElGamal
open CryptoLib.Schnorr

let SimpleTest(k) = 
    let eg = new ElGamal()
    let (pk,sk) = eg.KeyGen(k)
    let ((_,p,_),_) = pk
    let m=getRandom(p)
    let c = eg.Encrypt(m,pk)
    let m' = eg.Decrypt(c,sk)
    let c1 = eg.ReEncrypt(c,pk)
    let m1 = eg.Decrypt(c1,sk)
    m,m',m1

let ThresTest(k) = 
    let n=10
    let t=6
    let teg = new ThresElGamal(t,n)
    let (pk,(_,shares)) = teg.KeyShareGen(k)
    let ((_,p,_),_) = pk
    printfn "%A" pk
    let m=getRandom(p)
    let c = teg.Encrypt(m,pk)

    let rng = new System.Random()
    let decQuorum = Array.init t (fun i -> (0I,0I))
    for i=0 to t-1 do
        let mutable v = rng.Next(n-1) 
        let  sv = ref (shares.[v])
        while (decQuorum|>Array.exists(fun x-> x = !sv)) do
            v <- rng.Next(n-1) 
            sv := shares.[v]
        decQuorum.[i] <- shares.[v]
        
    let ds = decQuorum|>Array.map (fun v -> teg.DecryptShare(c,v,pk))
    let m'=teg.CombineShares(c,ds,pk)
    (m,m')