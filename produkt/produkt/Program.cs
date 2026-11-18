namespace produkt
{
    internal class Program
    {

        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            bool useAlphaBetaPruning = false;

            krydsOgBolle.Benchmark();
            krydsOgBolle.SelfPlayExperiment(games: 1000, randomPlies: 2);
            //krydsOgBolle game = new krydsOgBolle(true, useAlphaBetaPruning);
        }
    }
}
