namespace CryptoLib

module Schnorr = 

    open System.Numerics
    open CryptoLib.Common

    type Schnorr() = 
        member o.prove(sk,pk) = 
            let ((g,p,q),y) = pk
            let ((g,p,q),x) = sk
            let t = getRandom(q)
            let offer = powmod(g,t,p)
            let challenge = getRandom(q)
            let response = modulo (t+challenge*x) q
            (offer, challenge, response, pk)

        member o.verify(proof) = 
            let (offer, challenge, response, pk) = proof
            let ((g,p,q),y) = pk
            let a = powmod(g,response,p)
            let b = offer * powmod(y,challenge,p)
            a = modulo b p

        member o.batchVerify(proofs) = 
             proofs|>List.map (fun p -> o.verify(p))
                   |>List.fold (fun a b -> a && b) true 


    type SchnorrWID() =
        member o.prove(sk,pks) = 
            let pk = pks|>List.head
            let ((g,p,q),y) = pk
            let ((g,p,q),x) = sk
            let t = getRandom(q)
            let offer = powmod(g,t,p)
        
            //extract structure of keys
            let rest = pks|>List.tail|>List.toArray
            let n = (rest|>Array.length)        
            let ys = (Array.unzip>>snd) rest
            let gs,ps,qs = (Array.unzip>>fst>>Array.unzip3) rest

            //dummy proofs
            let cs = Array.init n ( fun i -> getRandom(qs.[i]) ) 
            let ts = Array.init n ( fun i -> getRandom(qs.[i]) ) 
            let offers = Array.init n (fun i -> modulo (powmod(gs.[i], ts.[i], ps.[i]) * invmod(powmod(ys.[i], cs.[i], ps.[i]), ps.[i])) ps.[i] )

            //the real response
            let c = getRandom(q)
            let challenge = modulo (c - (cs|>Array.sum)) q
            let response = modulo (t+challenge*x) q

            //return values
            let (os,cs,rs,pks) = (offer::(offers|>Array.toList), challenge::(cs|>Array.toList), response::(ts|>Array.toList), pks)
            Seq.ofList(os), Seq.ofList(cs), Seq.ofList(rs), Seq.ofList(pks)

        member o.verify(proof) =  
       
            let (offers, challenges, responses, pks) = proof
            let ls = zip4 offers challenges responses pks
            ls|>Seq.map (fun (o, c, r, ((g,p,_),y)) -> 
                                    let o' = o * powmod(y,c,p)
                                    powmod(g,r,p) = modulo o' p)
              |>Seq.fold (fun a b -> a && b) true
         
