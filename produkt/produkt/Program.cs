namespace produkt
{
    internal class Program
    {

        static void Main(string[] args)
        {
            //new krydsOgBolle(useMiniMax: true, lockProbability: 0.25);

            //krydsOgBolle.ValidateExpectiMinimax();
            krydsOgBolle.SelfPlayExperiment(20000);
            //krydsOgBolle.RandomnessTournament(gamesPerSetting: 20000);
        }
    }
}
