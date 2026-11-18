/*
 * Brættet er repræsenteret som en 2D-array af Player enum værdier.
bræt = {
    { N, N, N },
    { N, N, N },
    { N, N, N }
}

mens spillet kører:
    tegn brættet i konsollen.

    hvis m_currentPlayer er lig med spilleren så:
        få et input fra spilleren.
        hvis det er ugyldigt så:
            spørg spilleren igen.
        ellers:
            lav spillerens træk.
   
    ellers: 
        brug minimax algoritmen til at finde computerens bedste træk.
        lav computerens træk.
    
    hvis der er en vinder eller brættet er fyldt op:
        spillet er slut.
    ellers:
        skift til den anden spiller's tur.
ellers:
    skriv i konsollen hvem der vandt eller om det blev uafgjort.
*/

