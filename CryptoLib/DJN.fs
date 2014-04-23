namespace CryptoLib

module DJN = 

    open CryptoLib.Common
    open CryptoLib.Paillier

    type DJN(numCands) =
        let nc = numCands
        let M=103I
        
        let createWitnesses encVote n ns =
            let ns1 = ns * n
            let mutable arr = Array.init nc (fun _ -> 0I) 
            for c = 0 to nc-1 do
                let b =  modulo (bigint.Pow(M,c)) ns
                arr.[c] <- modulo (encVote *invmod(powmod((n+1I),b,ns1),ns1)) ns1
            arr

        let swap (arr:bigint[],a:int, b:int) = 
            let mutable t=arr.[b]
            arr.[b]<-arr.[a]
            arr.[a]<-t 
            let ls = arr|>Array.toList
            ls

        let extractRes res=
            let max = int(System.Math.Log(float res,float M))+1
            let mutable num = res
            let mutable votes = Array.zeroCreate max 
            let mutable i = 0
            while num > 0I do
                let v = num%M
                votes.[i]<-v
                num<-num/M
                i<-i+1
            votes

        member d.vote choice pk = 
            let enc = new Paillier()
            let n,s,_,_ = pk
            let encVote = enc.Encrypt(choice,pk,s)
            encVote

        member d.aggregate votes pk =
            let enc = new Paillier()
            let n,s,_,_ = pk
            let encRes = votes|>Seq.skip 1
                              |>Seq.fold (fun c1 c2 -> enc.Mult(c1,c2,pk,s)) (votes|>Seq.head)
            encRes

        member d.mvote choice pk = 
            let enc = new Paillier()
            let n,s,_,_ = pk
            let ns = bigint.Pow(n,s)
            let ns1 = ns*n

            let ballot =  modulo (bigint.Pow(M,choice)) ns
            let r = randomGCD(n)
            let encVote = enc.Encrypt(ballot,pk,s,r) 
            let mutable arr = createWitnesses encVote n ns
            
            let ls = swap(arr,choice,0) 
            let proof = enc.ProveNsPowerWID(ls,r,pk)

            let (offers, challenge, zs, cs)=proof
            let loffers = swap(offers,0,choice) 
            let lzs = swap(zs,0,choice) 
            let lcs = swap(cs,0,choice) 

            encVote,(loffers|>List.toArray, challenge, lzs|>List.toArray, lcs|>List.toArray)

        member d.checkProof encVote proof pk =
            let enc = new Paillier()
            let n,s,_,_ = pk
            let ns = bigint.Pow(n,s)
            let (offers, challenge, zs, cs)=proof
            let ls = createWitnesses encVote n ns
            enc.ValidateNsPowerWID(ls,offers, challenge, zs, cs, pk)

        member d.decrypt cres pk sk = 
            let dec = new Paillier()
            let res = dec.Decrypt(cres,sk,pk)
            extractRes res

        static member decryptShare cres pk sk = 
            ThresPaillier.DecryptShare(cres,sk,pk)

        member d.combine dshares pk =
            let tp = new ThresPaillier(6,10)
            let x = tp.CombineShares(dshares,pk)
            extractRes x

        