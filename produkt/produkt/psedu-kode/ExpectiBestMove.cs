/*
1    /*
2    * ExpectiBestMove(player, solver) finder det bedste træk for 'player' (X eller O)
3    * ved at tage højde for tilfældige låse (sandsynligheden s.P for at et tilfældigt
4    * tomt felt låses for næste spiller) og gemme beregnede stillinger i 'solver'.
5    
6    funktion ExpectiBestMove(player, solver):
7        opret liste 'låsteFelter'
8    
9        for hver række r fra 0 til 2:
10            for hver kolonne c fra 0 til 2:
11                hvis bræt[r][c] er L så:
12                    tilføj (r, c) til 'låsteFelter'
13                    sæt bræt[r][c] = N   (åbn midlertidigt mens fremtiden regnes ud)
14    
15        maximizing = (player er lig med X)
16        bestValue = hvis maximizing så -∞ ellers +∞
17        bestMove = (-1, -1)
18        pieces = antal brikker på brættet lige nu
19    
20        for hver række r fra 0 til 2:
21            for hver kolonne c fra 0 til 2:
22                hvis bræt[r][c] ikke er N eller (r, c) er i 'låsteFelter' så:
23                    spring over (fortsæt til næste felt)
24    
25                sæt bræt[r][c] = player
26                v = AfterMove(player, solver, pieces + 1)
27                sæt bræt[r][c] = N   (fortryd træk)
28    
29                hvis maximizing så:
30                    hvis v er større end bestValue + Eps så:
31                        bestValue = v
32                        bestMove = (r, c)
33                ellers:
34                    hvis v er mindre end bestValue - Eps så:
35                        bestValue = v
36                        bestMove = (r, c)
37    
38        for hver (r, c) i 'låsteFelter':
39            sæt bræt[r][c] = L   (genskab de nuværende låse)
40    
41        returner bestMove
42    */


/*
* ExpectedValueX(solver) beregner den teoretiske forventede værdi (set fra X)
* i den nuværende stilling, hvor X skal trække, og hvor intet felt er låst på forhånd.

funktion ExpectedValueX(solver):
    returner MoveValue(X, solver, antal brikker på brættet, -1)
*/

/*
* AfterMove(mover, s, pieces) evaluerer stillingen lige efter at 'mover' har trukket:
* enten en slutværdi (vinder/uafgjort) eller en chance-knude over mulige låse.

funktion AfterMove(mover, s, pieces):
    vinder = findVinder()
    hvis vinder ikke er N så:
        hvis vinder er lig med X så:
            returner 1 * (10 - pieces)
        ellers:
            returner -1 * (10 - pieces)

    empties = antal tomme felter (N) på brættet
    hvis empties er lig med 0 så:
        returner 0   (fuldt bræt: uafgjort)

    next = modstanderen til mover
    noLock = MoveValue(next, s, pieces, -1)

    hvis s.P er mindre end eller lig med 0 eller empties er mindre end 2 så:
        returner noLock

    lockSum = 0
    for hver indeks i fra 0 til 8:
        r = i heltalsdivideret med 3
        c = i modulo 3

        hvis bræt[r][c] ikke er N så:
            spring over

        lockSum = lockSum + MoveValue(next, s, pieces, i)   ('next' må ikke spille på felt i)

    returner (1 - s.P) * noLock + s.P * (lockSum / empties)
*/

/*
* MoveValue(turn, s, pieces, skip) finder værdien af en stilling hvor 'turn' skal trække
* (maximerer for X, minimerer for O).
* 'skip' er indekset (0..8) på et felt der er låst i dette træk, eller -1 hvis intet felt er spærret.

funktion MoveValue(turn, s, pieces, skip):
    key = Encode(turn, skip)
    hvis s.Known[key] er sand så:
        returner s.Value[key]   (brug gemt beregning)

    maximizing = (turn er lig med X)
    best = hvis maximizing så -∞ ellers +∞
    anyMove = falsk

    for hver indeks i fra 0 til 8:
        r = i heltalsdivideret med 3
        c = i modulo 3

        hvis bræt[r][c] ikke er N eller i er lig med skip så:
            spring over

        anyMove = sand
        sæt bræt[r][c] = turn
        v = AfterMove(turn, s, pieces + 1)
        sæt bræt[r][c] = N   (fortryd træk)

        hvis maximizing så:
            best = max(best, v)
        ellers:
            best = min(best, v)

    hvis anyMove er falsk så:
        best = 0

    s.Known[key] = sand
    s.Value[key] = best
    s.StatesComputed = s.StatesComputed + 1
    returner best
*/

/*
* Encode(turn, skip) laver et unikt heltal (nøgle) til opslag/lagring i 'solver'
* ud fra brættets felter, hvem der har tur, og hvilket felt (hvis noget) der er låst.

funktion Encode(turn, skip):
    key = 0
    for hver celle p i brættet:
        key = key * 3 + værdiFor(p)   (X = 0, O = 1, N = 2; ingen L i regnetræet)

    turnKode = hvis turn er lig med X så 0 ellers 1
    key = key * 2 + turnKode
    returner key * 10 + (skip + 1)
*/