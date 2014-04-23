// Learn more about F# at http://fsharp.net. See the 'F# Tutorial' project
// for more guidance on F# programming.
#load "MillerRabin.fs"
#load "Common.fs"
#load "Paillier.fs"
#load "DJN.fs"

open CryptoLib.Common
open CryptoLib.Paillier 
open CryptoLib.DJN 

let SimpleTest(s,k,total) = 
    let pl = new Paillier()   
    let djn = new DJN(2)
    let (pk,sk) = pl.KeyGen(k,s)
    let n,_,_,_ = pk
    let rng = new System.Random()
    let mutable sum = 0
    let mutable allVotes = Array.zeroCreate total 
    for i=0 to total-1 do
         let m=rng.Next()
         let v = (m%2)
         sum<-sum+v
         let c = djn.vote (bigint v) pk 
         allVotes.[i]<-c
    let encRes = djn.aggregate allVotes pk
    let res = pl.Decrypt(encRes,sk,pk)
    printfn "%A %A" sum res
        
let SimpleMTest(s,k,total,cnds) = 
    let pl = new Paillier()   
    let (pk,sk) = pl.KeyGen(k,s)
    let n,s,_,_ = pk
    let ns = bigint.Pow(n,s)
    let M = bigint.Pow (2I,int((k*s)/cnds+1))
    let rng = new System.Random()
    let djn = new DJN(cnds)
    let sum = Array.init cnds (fun x->0) 
    let allVotes = Array.zeroCreate total 
    let allProofs = Array.init total (fun _ -> [],0I,[],[])
    for i=0 to total-1 do
         let m=rng.Next()
         let v = m%cnds 
         let c,p = djn.mvote v pk 
         sum.[v]<-(sum.[v]+1)
         allVotes.[i]<-c
         allProofs.[i]<-p
    for i=0 to total-1 do
        let res = djn.checkProof allVotes.[i] allProofs.[i] pk
        show res
    let encRes = djn.aggregate allVotes pk
    let res = djn.decrypt encRes pk sk 
    printfn "%A \n %A" sum res
     

 //SimpleMTest(1,64,1000,6)
 //