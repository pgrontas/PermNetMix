namespace CryptoLib

module PermNet = 

    open CryptoLib.Common
    open CryptoLib.ElGamal
    open CryptoLib.Schnorr

    let splitList list = List.foldBack (fun x (l,r) -> x::r, l) list ([],[])

    //Prove that ciphertexts c1 and c2 encrypt the same plaintext under public key pk
    //The prover must know the reencryption factor r
    let genBlindedKey c1 c2 pk =
        let ((g,p,q),y) = pk
        let (g1,m1) = c1
        let (g2,m2) = c2
        let z1 = getRandom(q)
        let z2 = getRandom(q)
        let G = modulo (powmod(y,z2,p)*powmod(g,z1,p)) p
        let Y = modulo (powmod(g2*invmod(g1,p),z1,p)*powmod(m2*invmod(m1,p),z2,p)) p
        (G,Y)

    let PEProof c1 c2 pk r =
        let ((g,p,q),y) = pk
        let (G,Y) = genBlindedKey c1 c2 pk
        let PK = ((G,p,q),Y)
        let SK = ((G,p,q),r)
        let sn = new Schnorr()
        sn.prove(SK,PK)

    //Disjunctive Plaintext Equivalence Proof
    //Prove that c is reencrypted either as rc1 or as rc2 with reencryption factor r
    //By convention c is indeed reencrypted as rc1
    let DISPEProof c rc1 rc2 pk r =
        let ((g,p,q),y) = pk
        let snw = new SchnorrWID()
        let (G1,Y1) = genBlindedKey c rc1 pk
        let (G2,Y2) = genBlindedKey c rc2 pk
        let PK1 = ((G1,p,q),Y1)
        let PK2 = ((G2,p,q),Y2)
        let SK = ((G1,p,q),r)
        snw.prove(SK,[PK1;PK2])

    let switchingGate c1 c2 pk =
        let ((_,_,q),_) = pk
        let eg = new ElGamal()
        let r1 = getRandom(q)
        let r2 = getRandom(q)
        let rc1 = eg.ReEncrypt(c1,pk,r1)
        let rc2 = eg.ReEncrypt(c2,pk,r2)    
        let dpep = DISPEProof c1 rc1 rc2 pk r1 
    
        let c = eg.mult(c1,c2,pk)
        let rc = eg.mult(rc1,rc2,pk)
        let pep = PEProof c rc pk (r1+r2)
    
        let b = getRandom(bigint (System.Int32.MaxValue))
        if b%2I = 0I then
            (rc1,rc2), (pep,dpep)
        else
            (rc2,rc1), (pep,dpep)
    
   
    let rec permNet(ls, pk)=  
        let n = ls|>List.length
        if n = 2 then
            let (c1,c2),(p1,p2) = switchingGate ls.[0] ls.[1] pk
            [c1;c2],[(p1,p2)]
        else
            let x = System.Math.Log(float n,2.0)
            let (ls1,ls2) = splitList ls 
            let ls1', proofs1 = permNet(ls1,pk) 
            let ls2', proofs2 = permNet(ls2,pk) 
            ls1'@ls2', proofs1@proofs2
    
    let mix(ls,pk) =
       let xs = List.ofSeq(ls)
       let ls1,ls2 = permNet(xs, pk)
       Seq.ofList(ls1),Seq.ofList(ls2)

    let verify(xs) =
        let proofs = List.ofSeq(xs)
        let v1 = new Schnorr()
        let v2 = new SchnorrWID()
        proofs|>List.map (fun (p1,p2) -> v1.verify(p1) && v2.verify(p2))
              |>List.fold (fun a b -> a && b)true