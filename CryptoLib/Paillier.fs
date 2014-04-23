namespace CryptoLib

module Paillier = 

    open System.Numerics
    open CryptoLib.MillerRabin
    open CryptoLib.Common

    type Paillier() =   
        let tr = 32         //static variable for ns power proof

        member o.KeyGen (k:int,?s0) =
            let mutable p = getRandomPrime(k)
            let mutable q = getRandomPrime(k)
            let mutable n = p*q
            let mutable t = n - p - q + 1I
            while BigInteger.GreatestCommonDivisor(n,t)>1I do
                p <- getRandomPrime(k)
                q <- getRandomPrime(k)
                n <- p*q
                t <- n - p - q + 1I
           
            let l = lcm (p-1I) (q-1I)
            let s = defaultArg s0 1
            let pk = (n,s,1,1)
            let sk = l
            (pk,sk)
        
        member o.Encrypt (m,pk,?s0,?r0) =
            let n,_,_,_ = pk
            let s = defaultArg s0 1
            let ns = bigint.Pow(n,s)
            let ns1 = ns*n
            let r = defaultArg r0 (randomGCD(n))
            let c =powmod(n+1I,m,ns1) * powmod(r,ns,ns1)
            modulo c ns1
        
        member o.Decrypt(c,sk,pk,?s0) = 
            let s = defaultArg s0 1
            let n,_,_,_ = pk
            let l = sk
            let ns = bigint.Pow(n,s)
            let ns1 = ns*n
            let x = bigint.ModPow(c,l,ns1)
            let mutable i = 0I
            for j = 1 to s do
                let nj = bigint.Pow(n,j)
                let mutable t1 = (x%(nj*n)-1I)/n
                let mutable t2 = i
                let mutable sum = 0I
                for k = 2 to j do
                    sum <- sum + (binomial(t2,bigint k) * bigint.Pow(n,k-1))%nj
                    sum <-sum%nj
                t1<-modulo (t1-sum) nj
                i<-t1
            let m = i * invmod(l,ns)
            m%ns

        member o.Mult(c1,c2,pk,?s0) = 
            let s = defaultArg s0 1
            let n,_,_,_ = pk
            c1*c2 % bigint.Pow(n,s+1)

        member o.ReEncrypt(c,pk,?s0) =
            let s = defaultArg s0 1
            let n = pk
            let c1 = o.Encrypt(1I,pk,s)
            o.Mult(c,c1,pk,s)
        
        //Prove that c is an encryption of m
        member o.ProveEncryption(m,c,pk,r) =
            let (n,s,_,_)=pk
            let ns1 = bigint.Pow(n,s+1) 
            let u = modulo (c*invmod(powmod((n+1I),m,ns1),ns1)) ns1
            o.ProveNsPower(u,r,pk) 
            
        //Prove that u is a Ns power <=> an encryption of zero with randomness v
        member o.ProveNsPower(u,v,pk) =
            let (n,s,_,_)=pk
            let t = int(System.Math.Log(float n/2.0,float 2))/4
            let r = randomGCD n
            let a = o.Encrypt(0I,pk,s,r)
            let e = modulo (getRandomBits(t)) (bigint.Pow(2I,t))
            let z = modulo (r*powmod(v,e,n)) n
            let x= o.Encrypt(0I,pk,s,z)
            let ns1 = (bigint.Pow(n,s+1))  
            x=modulo (a*powmod(u,e,ns1)) ns1  &&
                bigint.GreatestCommonDivisor(u,n)=1I &&
                bigint.GreatestCommonDivisor(a,n)=1I &&
                bigint.GreatestCommonDivisor(z,n)=1I
        
         member o.ProveEncryptionWID(m,cs,pk,r) =
            let (n,s,_,_)=pk
            let ns = bigint.Pow(n,s)
            let ns1 = bigint.Pow(n,s+1)

            let c = cs|>List.head
            let rest = (cs|>List.tail)
            let u = modulo (c*invmod(powmod((n+1I),m,ns1),ns1)) ns1
            let us = u::rest
            o.ProveNsPowerWID(us,r,pk) 

         //Prove that the head of the us is a Ns power <=> an encryption of zero with randomness v
         member o.ProveNsPowerWID(us,v,pk) =
            let total = us|>List.length
            let (n,s,_,_)=pk
            let pt = bigint.Pow(2I,tr)   
            let ns = bigint.Pow(n,s)
            let ns1 = bigint.Pow(n,s+1)  
            
            //offers           
            let r = randomGCD n
            let commit = o.Encrypt(0I,pk,s,r)
            let offer = commit
            //simulator for dummy offers
            let rs = List.init (total-1) (fun _ -> randomGCD n)
            let cs = List.init (total-1) (fun _ -> randomGCD pt)
            let commits = rs|>List.map (fun r -> o.Encrypt(0I,pk,s,r))
            let offers = offer::(List.zip3 commits (us|>List.tail) cs
                                    |> List.map (fun (t,u,c) -> t * invmod(powmod(u,c, ns1), ns1))
                                    |> List.map (fun x -> modulo x ns1))
            //challenge
            let challenge = randomGCD(pt)

            //responses
            let sm = modulo (cs|>List.sum) pt
            let c1 = modulo (challenge-sm) pt 
            let z1 = modulo (r * powmod (v, c1, n)) n

            let zs = z1::rs
            let cs = c1::cs
           
            (offers|>List.toArray, challenge, zs|>List.toArray, cs|>List.toArray)
        
        member o.ValidateNsPowerWID(us,offers,challenge,zs,cs,pk)  =
            let (n,s,_,_)=pk
            let ns1 = (bigint.Pow(n,s+1))  
            let pt = bigint.Pow(2I,tr)  
            let c = modulo (Array.sum cs) pt
            
            let okChallenge =  (challenge = c)
            let ok = Array.zip zs (Array.zip3 offers cs us)
                        |> Array.map (fun (z,(t,c,u)) -> o.Encrypt(0I,pk,s,z) = modulo (t * powmod(u,c,ns1)) ns1)
                        |> Array.fold (fun x y -> x && y) true
            ok && okChallenge

    type ThresPaillier(t1,n1) =
        inherit Paillier()
        let tr = t1
        let nt = n1

        let extractFrom(x,n,s) =
           
            let mutable i=0I
            for j = 1 to s do 
                let nj = bigint.Pow(n,j)
                let mutable t1 = ((modulo x (nj*n))-1I)/n 
                let mutable t2 = i
                for k = 2 to j do 
                    i<-i-1I
                    t2<-(t2*i)%nj
                    let temp = ((t2*bigint.Pow(n,k-1))/factorial(k))%nj
                    t1<-modulo (t1-temp) nj
                i<-t1
            i

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

        member o.KeyShareGen (k:int,s:int) =
            let (p,p1) = getSafePrimes2(k)
            let (q,q1) = getSafePrimes2(k)
            let n=p*q
            let m=p1*q1
            let ns = bigint.Pow(n,s) 
            let d = crt [0I;1I] [m;ns]
         
            let D = factorial(nt)
            let ssp = m*ns
            let shares = ShamirShare(d,ssp,tr,nt)
            let pk = n,s,D,ssp
            let sk = shares
            (pk,sk)

        static member DecryptShare(c,share,pk) = 
            let (x,y) = share 
            let n,s,D,ssp = pk
            let exponent = 2I*D*y 
            x,powmod(c,exponent,bigint.Pow(n,s+1)) 

        member o.CombineShares(ds,pk) =  
            let (n,s,D,ssp) = pk
            let ns = bigint.Pow(n,s)
            let ns1 = bigint.Pow(n,s+1)

            let mutable c1 = 1I
            let lcount = (ds|>Array.length)
            for i = 0 to lcount-1 do
                let mutable (x,y)=ds.[i]
                let mutable prod=1I 
                for j = 0 to lcount-1 do
                    if (not(i=j)) then
                        let mutable (a,b)=ds.[j]
                        prod <-  prod *  (a * invmod(a-x,ssp))
                        prod <- modulo prod ssp
                prod<-modulo (prod*D*2I) ssp 
                c1<-(c1*powmod(y,prod,ns1))  
                 

            let temp = extractFrom(c1,n,s)            
            let msg = modulo (temp*invmod(4I*D*D,ns)) ns
            msg

                        
