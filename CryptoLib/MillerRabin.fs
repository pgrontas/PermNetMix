﻿
namespace CryptoLib

module MillerRabin = 
    open System.Numerics

    ///This implementation is based on the Miller-Rabin Haskell implementation 
    ///from http://www.haskell.org/haskellwiki/Testing_primality
    let pow' mul sq x' n' = 
        let rec f x n y = 
            if n = 1I then
                mul x y
            else
                let (q,r) = BigInteger.DivRem(n, 2I)
                let x2 = sq x
                if r = 0I then
                    f x2 q y
                else
                    f x2 q (mul x y)
        f x' n' 1I
        
    let mulMod (a :bigint) b c = (b * c) % a
    let squareMod (a :bigint) b = (b * b) % a
    let powMod m = pow' (mulMod m) (squareMod m)
    let iterate f = Seq.unfold(fun x -> let fx = f x in Some(x,fx))

    ///See: http://en.wikipedia.org/wiki/Miller%E2%80%93Rabin_primality_test
    let millerRabinPrimality n a =
        let find2km n = 
            let rec f k m = 
                let (q,r) = BigInteger.DivRem(m, 2I)
                if r = 1I then
                    (k,m)
                else
                    f (k+1I) q
            f 0I n
        let n' = n - 1I
        let iter = Seq.tryPick(fun x -> if x = 1I then Some(false) elif x = n' then Some(true) else None)
        let (k,m) = find2km n'
        let b0 = powMod n a m

        match (a,n) with
            | _ when a <= 1I && a >= n' -> 
                failwith (sprintf "millerRabinPrimality: a out of range (%A for %A)" a n)
            | _ when b0 = 1I || b0 = n' -> true
            | _  -> b0 
                     |> iterate (squareMod n) 
                     |> Seq.take(int k)
                     |> Seq.skip 1 
                     |> iter 
                     |> Option.exists id 

    ///For Miller-Rabin the witnesses need to be selected at random from the interval [2, n - 2]. 
    ///More witnesses => better accuracy of the test.
    ///Also, remember that if Miller-Rabin returns true, then the number is _probable_ prime. 
    ///If it returns false the number is composite.
    let isPrimeW witnesses = function
        | n when n < 2I -> false
        | n when n = 2I -> true
        | n when n = 3I -> true
        | n when n % 2I = 0I -> false
        | n             -> witnesses |> Seq.forall(millerRabinPrimality n)


    let isPrime = isPrimeW [2I;3I;5I;7I;11I;13I;17I;19I] 