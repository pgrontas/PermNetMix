namespace CryptoLib

module Common =

    open System.Security.Cryptography
    open System.Numerics
    open CryptoLib.MillerRabin 
     
    let closestPowerOf2 n =
        let x = System.Math.Log(float n,2.0)
        if System.Math.Floor(x) = System.Math.Ceiling(x) then
            n
        else
            int(System.Math.Pow(2.0,System.Math.Ceiling(x)))

    let show x = printfn "%A" x

    let zip4 s1 s2 s3 s4 = Seq.map2 (fun (a,b)(c,d) ->a,b,c,d) (Seq.zip s1 s2)(Seq.zip s3 s4)            

    let inline modulo n m =
        let mod' = n % m
        if sign mod' >= 0 then mod'
        else abs m + mod'
         

    let lcm (a:bigint) (b:bigint) =
        (a*b)/bigint.GreatestCommonDivisor(a,b)
    
    let factorial n =
        let mutable prod = 1I
        for i=1 to n do
            prod <- prod*bigint i
        prod

    let binomial (n:bigint, k:bigint) =
        let mutable prod = 1I
        let mutable i = 1I
        while (i<=k) do
            prod <- prod * (n-k+i)/i
            i<-i+1I
        prod

    let extGCD (a : bigint) (b : bigint) =    
        let rec inner (r'', s'', t'') (r', s', t') = 
            let step () = 
                let q = r'' / r'
                let r = r'' - q*r'
                let s = s'' - q*s'
                let t = t'' - q*t'
                (r, s, t)
            if r' = 0I then (r'', s'', t'')
            else inner (r', s', t') (step())
        inner (a, 1I, 0I) (b, 0I, 1I)

    let inline invmod (a:bigint, n:bigint) =
        let a'= modulo a n
        let (x,y,z) = extGCD a' n 
        modulo y n 

    let zipWith f ls l's =
        let rec inner ls l's cont =
            match ls, l's with
            | [], []         -> cont []
            | l::ls, l'::l's -> inner ls l's (fun r -> (f l l')::r |> cont)
            | _              -> failwith "Lists don't have same length"
        inner ls l's (fun r -> r)

    let zipWith3 f ls l's l''s =
        let rec inner ls l's l''s cont =
            match ls, l's, l''s with
            | [], [], []                -> cont []
            | l::ls, l'::l's, l''::l''s -> inner ls l's l''s (fun r -> (f l (f l' l''))::r |> cont)
            | _                         -> failwith "Lists don't have same length"
        inner ls l's l''s (fun r -> r)

    let crt bs ns =
        let N = ns |> List.fold (*) 1I
        let s = List.zip bs ns
                    |>List.map (fun (b,n) -> b * N * invmod(N/n,n)/n)
                    |>List.sum 
        modulo s N

    let inline powmod (v:bigint, e:bigint, m:bigint) =
        let res = BigInteger.ModPow(v,e,m)
        modulo res m  

    let getRandomBits (k:int) = 
        let rng = new RNGCryptoServiceProvider()
        let randomBytes : byte array = Array.zeroCreate (k/8)
        rng.GetBytes(randomBytes)
        new BigInteger(randomBytes)

    let getRandom (n:bigint) =
        let x = int(BigInteger.Log(n,2.0))+1
        let k = if x<8 then 8 else x
        let v = getRandomBits(k)
        modulo v n

    let randomGCD n =
        let mutable r=getRandom(n)
        while bigint.GreatestCommonDivisor(r,n)>1I do 
            r<-getRandom(n)
        r

    let getRandomPrime (k:int) = 
        let mutable ok = false
        let mutable num = 0I 
        while (not(ok)) do   
            num <- getRandomBits(k)
            ok <- isPrime(num)
        num

    let getSafePrimes (k:int) =
        let mutable p = getRandomPrime(k)
        let mutable q = (p-1I)/2I
        let mutable ok = isPrime(q)
        while (not(ok)) do
            p <- getRandomPrime(k)
            q <- (p-1I)/2I
            ok<-isPrime(p)
        (p,q)

    let getSafePrimes2 (k:int) =
        let mutable q = getRandomPrime(k-1)
        let mutable p = 2I*q+1I
        let mutable ok = isPrime(p)
        while (not(ok)) do
            q <- getRandomPrime(k-1)
            p <- 2I*q+1I
            ok<-isPrime(p)
        (p,q)
    