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
        hvis m_lockProbability er større end 0 så:
            hvis algurithmen tager højde for låse, der flyttersig:
                brug ExpectiBestMove til at finde computerens bedste træk og gemme beregningerne til senere.
            ellers:
                brug minimax algoritmen til at finde computerens bedste træk.
        ellers:
            brug minimax algoritmen til at finde computerens bedste træk.
            lav computerens træk.
    
    hvis der er en lås, så fjern den igen (så den kun varer ét træk).

    hvis der er en vinder eller brættet er fyldt op:
        spillet er slut.
    ellers:
        hvis m_lockProbability er størrer end 0 og der er mere end 2 tomme felter så:
            brug sandsynlighed til at bestemm om der skal placeres en ny lås

        skift til den anden spiller's tur.
ellers:
    skriv i konsollen hvem der vandt eller om det blev uafgjort.
*/

