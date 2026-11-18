/*
 * når minimax() kaldes, bliver der givet 5 parametre:
 * 1. turn: den spiller, der skal lave et træk (enten AI eller modstander)
 * 2. ai: den spiller, der er AI (enten X eller O)
 * 3. depth: hvor dybt vi er i træet (starter på 1)
 * 4. alpha: den bedste score, som AI kan garantere på dette tidspunkt
 * 5. beta: den bedste score, som modstanderen kan garantere på dette tidspunkt
 * 
hvis det nuvernede bræt er en vinder:
    hvis vinder er AI:
        * jo dybere, jo mindre score, dette er fordi vi vil vinde hurtigere
        returner 10 - depth 

    ellers hvis vinder er modstander:
        * jo dybere, jo mindre score, dette er fordi vi vil tabe langsommere
        retuner debth - 10 (jo dybere, jo mindre score)

    ellers hvis brættet er fyldt op:
        returner 0 (uafgjort)
ellers:
    
    hvis det er AI's tur: ( vi vil maksimere score )
        best = -∞
        for hver mulig træk:
            lav træk
            score = minimax(modstander, ai, depth + 1, alpha, beta)
            fortryd træk
            best = max(best, score)
            alpha = max(alpha, best)
            hvis beta <= alpha:
                returner best (alfa-beta pruning)
        returner best
    ellers (det er modstanderens tur, vi vil minimere score):
        best = +∞
        for hver mulig træk:
            lav træk
            score = minimax(ai, modstander, depth + 1, alpha, beta)
            fortryd træk
            best = min(best, score)
            beta = min(beta, best)
            hvis beta er mindre eller lig med alpha:
                returner best (alfa-beta pruning)
        returner best
*/

