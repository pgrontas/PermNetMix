namespace CryptoLib

module ElGamal = 

    open System.Numerics
    open CryptoLib.MillerRabin
    open CryptoLib.Common

    type ElGamal() =  
        member o.KeyGen (k:int) =
            let (p,q) = getSafePrimes(k)
            let mutable g = getRandom(p)
            while (g=2I || g=p-1I) do
                g<-getRandom(p)
            g<-powmod(g, 2I, p)
            let mutable x = getRandom(q)
            while (x=0I) do
                x<-getRandom(q)
            let y = powmod(g,x,p)
            let common = (g,p,q)
            ((common,y),(common,x))

        member o.mult(c1,c2,pk) = 
            let ((_,p,_),_) = pk
            let (G1,M1) = c1
            let (G2,M2) = c2
            (modulo (G1*G2) p,modulo (M1*M2) p)

        member o.Encrypt (m,pk,?r0) =
            let ((g,p,q),y) = pk
            let r = defaultArg r0 (getRandom(q))
            let G = powmod(g,r,p)
            let Y = powmod(y,r,p)
            let M = modulo (m*Y) p
            (G,M)

        member o.Decrypt (c,sk) =
            let ((_,p,_),x) = sk
            let (G,M) = c
            let v = M * invmod(powmod(G,x,p),p)
            modulo v p

        member o.ReEncrypt (c,pk,?rnd) = 
           let v = 
                match rnd with
                    | None ->  o.mult(c,o.Encrypt(1I,pk),pk)
                    | Some r -> o.mult(c,o.Encrypt(1I,pk,r),pk)
           v

    type ThresElGamal(t,n) =
        inherit ElGamal()
    
        let ShamirShare(s,p,t,n) = 
            let polynomial = s::List.init (t-1) (fun _ -> getRandom(p))
            let eval pol p x =
                let sm = pol|>List.mapi (fun i a -> a*powmod(x,bigint i,p))
                            |>List.sum
                modulo sm p

            let points = List.init n (fun i -> bigint (i+1) )
            let shares = 
                List.zip points
                         (points|>List.map (fun x -> eval polynomial p x))
            shares

        member o.KeyShareGen (k:int) =
             let ((common,y),(_,x)) = o.KeyGen(k)
             let  (g,p,q) = common
             let shares = ShamirShare(x,q,t,n)
             ((common,y),(common,shares))
    
        member o.DecryptShare(c,share,pk) = 
            let (g,p,q),y = pk
            let (G,M) = c
            let (x,px) = share
            (x,powmod(G,px,p))

        member o.CombineShares(c,ds,pk) = 
            let (g,p,q),y = pk
            let (G,M) = c
            let k = ds|>Array.length 

            let mutable pr=1I        
            for i=0 to k-1 do
                let mutable prod=1I
                let mutable (x,h) = ds.[i]
                for j=0 to k-1 do
                    if (not(i=j)) then
                        let mutable (a,b) = ds.[j]
                        prod<-prod*(a*invmod(a-x,q))
                        prod<-modulo prod q
                pr<-pr*powmod(h,prod,p)
                pr<-modulo pr p
            let v = M * invmod(pr,p)
            modulo v p
