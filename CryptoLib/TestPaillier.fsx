// Learn more about F# at http://fsharp.net. See the 'F# Tutorial' project
// for more guidance on F# programming.

#load "MillerRabin.fs"
#load "Common.fs"
#load "Paillier.fs"

open CryptoLib.Common
open CryptoLib.Paillier 

let SimpleTest(s,k) = 
    let pl = new Paillier()   
    let (pk,sk) = pl.KeyGen(k,s)
    show (pk,sk)
    let (n,s,_,_) = pk
    let ns = bigint.Pow(n,s)
    let m=getRandom(ns)
    let mutable r =  (getRandom(n))
    while (bigint.GreatestCommonDivisor(r,n)>1I) do
        r<-getRandom(n)
    let c = pl.Encrypt(m,pk,s,r)
    let ok = pl.ProveEncryption(c,m,pk,r)
    let m' = pl.Decrypt(c,sk,pk,s)
    let c1 = pl.ReEncrypt(c,pk,s)
    let m1 = pl.Decrypt(c1,sk,pk,s)
    m,ok,m',m1

let testProofs(s,k,outOfk) =
    let pl = new Paillier()    
    let (pk,sk) = pl.KeyGen(k,s)
    let (n,s,_,_) = pk    
    let ns = bigint.Pow(n,s)
    let ns1 = bigint.Pow(n,s+1) 

    let mutable all = true
    for i=1 to 100 do
        let r =  randomGCD n
        let m = getRandom(ns)
        let rest = List.init (outOfk-1) (fun _ -> randomGCD(ns1))

        let cipher = pl.Encrypt(m,pk,s,r)
        let cipher' = pl.Encrypt(0I,pk,s,r)
        //let cipher = pl.Encrypt(0I,pk,s,r)        
        let allc = cipher::rest
        let allc' =  cipher'::rest

        let (offers,c,zs,cs) = pl.ProveEncryptionWID(m,allc,pk,r)
        //let (offers,c,zs,cs) = pl.ProveNsPowerWID(allc,r,pk)
        let ok = pl.ValidateNsPowerWID(allc',offers,c,zs,cs,pk) 
        show ok
        all <- all && ok
    all 

let ThresTest(s,k) = 
    let nt=10
    let tr=6
    let teg = new ThresPaillier(tr,nt)
    let (pk,shares) = teg.KeyShareGen(k,s)
    let n,s,D,ssp = pk
    
    let ns = bigint.Pow(n,s)
    let m=getRandom(ns)    
    let c = teg.Encrypt(m,pk,s)

    let rng = new System.Random()
    let decQuorum = Array.init tr (fun i -> (0I,0I))
    for i=0 to tr-1 do
        let mutable v = rng.Next(nt-1)  
        let  sv = ref (shares.[v])
        while (decQuorum|>Array.exists(fun x-> x = !sv)) do
            v <- rng.Next(nt-1) 
            sv := shares.[v]
        decQuorum.[i] <- shares.[v]
  
    let ds = decQuorum|>Array.map (fun v -> ThresPaillier.DecryptShare(c,v,pk))
    let m'=teg.CombineShares(ds,pk)
    (m,m') 


let testPrime(k) =
    let pl = new Paillier() 
    let (pk,sk) = pl.KeyGen(k,1)
    show (pk,sk)
    let (p,q) = getSafePrimes(k)
    show (p,q)

let probCoprime k = 
    let p = getRandomPrime(k)
    let q = getRandomPrime(k)
    let n = p*q
    let fn = (p-1I)*(q-1I)
    1.0+1.0/float n - 1.0/float p - 1.0/float q
