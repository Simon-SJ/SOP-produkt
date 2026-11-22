using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace produkt
{
    // X og O er spillerne, N er et tomt felt og L er et felt, der er låst i ét træk (kan ikke bruges lige nu)
    enum Player { X, O, N, L }

    public class krydsOgBolle
    {
        private Player m_currentPlayer;
        private readonly Player m_aiPlayer = Player.X;
        private bool m_useMiniMax;
        private bool m_useAlfaBetaPruning;
        private gameBoard m_board;
        private bool m_IsRunning;
        private Stopwatch m_time;

        private readonly double m_lockProbability;
        private readonly bool m_aiAccountsForLocks;
        private readonly Random m_rng;
        private readonly ExpectiSolver m_solver;
        private string m_lastEvent = "";

        /// <param name="useMiniMax"> true = computeren (X) spiller med, false = to mennesker </param>
        /// <param name="useAlfaBetaPruning"> bruges kun af almindelig minimax (uden tilfældighed) </param>
        /// <param name="lockProbability"> sandsynlighed (0 til 1) for at et tilfældigt tomt felt bliver låst for modstanderens næste træk </param>
        /// <param name="aiAccountsForLocks"> true = AI bruger expectiminimax og tager højde for låsene, false = almindelig minimax der ignorerer dem </param>
        /// <param name="seed"> valgfrit frø til tilfældighederne (null = tilfældigt frø) </param>
        public krydsOgBolle(
            bool useMiniMax = false, 
            bool useAlfaBetaPruning = false, 
            double lockProbability = 0.0, 
            bool aiAccountsForLocks = true, 
            int? seed = null)
        {
            if (lockProbability < 0.0 || lockProbability > 1.0)
                throw new ArgumentOutOfRangeException(nameof(lockProbability), "skal ligge mellem 0 og 1");

            m_IsRunning = true;
            m_board = new(useAlfaBetaPruning);
            m_currentPlayer = Player.O;
            m_useMiniMax = useMiniMax;
            m_useAlfaBetaPruning = useAlfaBetaPruning;
            m_lockProbability = lockProbability;
            m_aiAccountsForLocks = aiAccountsForLocks;
            m_rng = seed.HasValue ? new Random(seed.Value) : new Random();
            // Den "naive" AI kender reglen om, at en lås kun gælder ét træk, men regner ikke med fremtidige låse (p = 0)
            m_solver = new ExpectiSolver(aiAccountsForLocks ? lockProbability : 0.0);
            m_time = new Stopwatch();
            m_time.Stop();
            Run();
        }

        public void Draw()
        {
            Console.WriteLine($"it's {m_currentPlayer}'s turn.");
            m_board.Draw();
            if (m_lastEvent != "")
            {
                Console.WriteLine(m_lastEvent);
                m_lastEvent = "";
            }
        }

        public void Run()
        {
            while (m_IsRunning)
            {
                Console.Clear();
                Draw();

                if (m_useMiniMax && m_currentPlayer == m_aiPlayer)
                {
                    m_time.Reset();
                    m_time.Start();
                    var (r, c) = m_lockProbability > 0
                        ? m_board.ExpectiBestMove(m_aiPlayer, m_solver)
                        : m_board.BestMove(m_aiPlayer);
                    m_board.Place(r, c, m_currentPlayer);
                    m_time.Stop();
                }
                else
                {
                    HumanMove();
                }

                m_board.ClearLocks();   // en lås gælder kun ét træk

                Player winner = m_board.Winner();
                if (winner != Player.N || m_board.IsFull())
                {
                    EndGame(winner);
                    return;
                }

                // Tilfældig hændelse: med sandsynlighed p bliver et tilfældigt tomt felt låst for modstanderens næste træk.
                // Der skal være mindst to tomme felter, så modstanderen altid har et lovligt træk.
                var cells = m_board.EmptyCells();
                if (m_lockProbability > 0 && cells.Count >= 2 && m_rng.NextDouble() < m_lockProbability)
                {
                    var (lr, lc) = cells[m_rng.Next(cells.Count)];
                    m_board.Place(lr, lc, Player.L);
                    m_lastEvent = $"Random event: square {(char)('A' + lc)}{lr} is locked for the next move!";
                }

                m_currentPlayer = m_currentPlayer == Player.X ? Player.O : Player.X;
            }
        }

        private void EndGame(Player winner)
        {
            Console.Clear();
            m_board.Draw();
            if (m_lastEvent != "") Console.WriteLine(m_lastEvent);
            Console.WriteLine(winner != Player.N ? $"{winner} wins!" : "It's a draw!");
            m_IsRunning = false;
        }

        private void HumanMove()
        {
            while (true)
            {
                Console.WriteLine($"AI move took {m_time.ElapsedMilliseconds} ms");
                Console.WriteLine("Please write the move (eg. A1):");
                string input = Console.ReadLine() ?? "";
                var (success, msg) = m_board.move(input, m_currentPlayer);
                if (success) return;
                Console.WriteLine(msg);
            }
        }

        // Test 1: søgning fra det tomme bræt (kontrolværdi: 549.945 uden beskæring)

        public static void Benchmark()
        {
            foreach (bool pruning in new[] { false, true })
            {
                var board = new gameBoard(pruning);
                var sw = Stopwatch.StartNew();
                board.BestMove(Player.X);
                sw.Stop();
                Console.WriteLine($"Alfa-beta: {pruning,-5} | positioner: {board.NodesVisited,8} | tid: {sw.ElapsedMilliseconds} ms");
            }
        }

        //  Test 2: AI mod sig selv + måling af positioner og tid
        /// <summary>
        /// Få to AI'er til at spille mod hinanden, og mål hvor mange positioner de besøger og hvor lang tid det tager.
        /// </summary>
        /// <param name="games"> antal spil der bliver spillet </param>
        /// <param name="randomPlies"> antal første halvtræk, der vælges tilfældigt </param>
        /// <param name="seed"> fast frø, så forsøget kan gentages med samme resultat </param>
        /// <param name="folder"> mappe til CSV-filerne </param>
        public static void SelfPlayExperiment(int games = 1000, int randomPlies = 2, int seed = 42, string folder = "resultater")
        {
            var rng = new Random(seed);
            var dk = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            dk.NumberFormat.NumberDecimalSeparator = ",";   // så dansk Excel kan åbne filerne

            // [0] = uden alfa-beta, [1] = med alfa-beta. Andet indeks = antal tomme felter før trækket (1..9)
            var nodesBy = new Stat[2, 10];
            var timeBy = new Stat[2, 10];
            var nodesAll = new Stat[2];
            var timeAll = new Stat[2];
            for (int p = 0; p < 2; p++)
            {
                nodesAll[p] = new Stat();
                timeAll[p] = new Stat();
                for (int e = 0; e < 10; e++) { nodesBy[p, e] = new Stat(); timeBy[p, e] = new Stat(); }
            }

            int xWins = 0, oWins = 0, draws = 0, mismatches = 0, totalMoves = 0;

            Directory.CreateDirectory(folder);
            using var raw = new StreamWriter(Path.Combine(folder, "raa_data.csv"), false, new UTF8Encoding(true));
            raw.WriteLine("spil;halvtraek;tomme_felter;alfa_beta;positioner;tid_ms");

            // Opvarmning, så JIT-kompilering ikke ender i målingerne
            for (int i = 0; i < 20; i++)
            {
                new gameBoard(false).BestMove(Player.X);
                new gameBoard(true).BestMove(Player.X);
            }

            Console.WriteLine($"Kører {games} spil (AI mod AI), {randomPlies} tilfældige første halvtræk, seed {seed} ...");

            for (int g = 1; g <= games; g++)
            {
                var plain = new gameBoard(false);
                var pruned = new gameBoard(true);
                Player current = Player.X;
                int ply = 0;

                while (true)
                {
                    int empties = 9 - ply;

                    // Skift rækkefølge hvert andet spil, så målingen ikke favoriserer den ene
                    bool prunedFirst = g % 2 == 0;
                    Result first = Measure(prunedFirst ? pruned : plain, current);
                    Result second = Measure(prunedFirst ? plain : pruned, current);
                    Result resPruned = prunedFirst ? first : second;
                    Result resPlain = prunedFirst ? second : first;

                    Record(0, resPlain);
                    Record(1, resPruned);
                    totalMoves++;
                    if (resPlain.Move != resPruned.Move) mismatches++;

                    (int row, int col) move;
                    if (ply < randomPlies)
                    {
                        var cells = pruned.EmptyCells();
                        move = cells[rng.Next(cells.Count)];
                    }
                    else
                    {
                        move = resPruned.Move;
                    }

                    plain.Place(move.row, move.col, current);
                    pruned.Place(move.row, move.col, current);

                    Player winner = plain.Winner();
                    if (winner != Player.N || plain.IsFull())
                    {
                        if (winner == Player.X) xWins++;
                        else if (winner == Player.O) oWins++;
                        else draws++;
                        break;
                    }
                    current = gameBoard.Opponent(current);
                    ply++;

                    void Record(int p, Result r)
                    {
                        nodesBy[p, empties].Add(r.Nodes);
                        timeBy[p, empties].Add(r.Ms);
                        nodesAll[p].Add(r.Nodes);
                        timeAll[p].Add(r.Ms);
                        raw.WriteLine(string.Join(";", g, ply + 1, empties, p == 1 ? "med" : "uden",
                            r.Nodes, r.Ms.ToString("F4", dk)));
                    }
                }
            }

            // ---------- Samlet oversigt ----------
            using (var sum = new StreamWriter(Path.Combine(folder, "samlet.csv"), false, new UTF8Encoding(true)))
            {
                sum.WriteLine("alfa_beta;antal_soegninger;positioner_min;positioner_max;positioner_gns;tid_min_ms;tid_max_ms;tid_gns_ms");
                for (int p = 0; p < 2; p++)
                    sum.WriteLine(string.Join(";", p == 1 ? "med" : "uden", nodesAll[p].Count,
                        nodesAll[p].Min.ToString("F0", dk), nodesAll[p].Max.ToString("F0", dk), nodesAll[p].Avg.ToString("F2", dk),
                        timeAll[p].Min.ToString("F4", dk), timeAll[p].Max.ToString("F4", dk), timeAll[p].Avg.ToString("F4", dk)));
            }

            // ---------- Pr. antal tomme felter ----------
            using (var perEmpty = new StreamWriter(Path.Combine(folder, "pr_tomme_felter.csv"), false, new UTF8Encoding(true)))
            {
                perEmpty.WriteLine("tomme_felter;antal;" +
                    "uden_pos_min;uden_pos_max;uden_pos_gns;uden_tid_min;uden_tid_max;uden_tid_gns;" +
                    "med_pos_min;med_pos_max;med_pos_gns;med_tid_min;med_tid_max;med_tid_gns;besparelse_pct");
                for (int e = 9; e >= 1; e--)
                {
                    Stat nu = nodesBy[0, e], nm = nodesBy[1, e], tu = timeBy[0, e], tm = timeBy[1, e];
                    if (nu.Count == 0) continue;
                    double saving = 100.0 * (1.0 - nm.Avg / nu.Avg);
                    perEmpty.WriteLine(string.Join(";", e, nu.Count,
                        nu.Min.ToString("F0", dk), nu.Max.ToString("F0", dk), nu.Avg.ToString("F2", dk),
                        tu.Min.ToString("F4", dk), tu.Max.ToString("F4", dk), tu.Avg.ToString("F4", dk),
                        nm.Min.ToString("F0", dk), nm.Max.ToString("F0", dk), nm.Avg.ToString("F2", dk),
                        tm.Min.ToString("F4", dk), tm.Max.ToString("F4", dk), tm.Avg.ToString("F4", dk),
                        saving.ToString("F1", dk)));
                }
            }

            // ---------- Udskrift i konsollen ----------
            Console.WriteLine();
            Console.WriteLine($"Spil: {games} | X vinder: {xWins} | O vinder: {oWins} | uafgjort: {draws}");
            Console.WriteLine($"Træk i alt: {totalMoves} | forskellige valgte træk med/uden alfa-beta: {mismatches} (skal være 0)");
            Console.WriteLine();
            Console.WriteLine("SAMLET (alle søgninger)");
            Console.WriteLine($"{"",-12}{"pos min",10}{"pos max",10}{"pos gns",12}{"tid min",11}{"tid max",11}{"tid gns",11}   (tid i ms)");
            for (int p = 0; p < 2; p++)
                Console.WriteLine($"{(p == 1 ? "Med alfa-beta" : "Uden"),-13}" +
                    $"{nodesAll[p].Min,9:F0} {nodesAll[p].Max,9:F0} {nodesAll[p].Avg,11:F1} " +
                    $"{timeAll[p].Min,10:F4} {timeAll[p].Max,10:F4} {timeAll[p].Avg,10:F4}");

            Console.WriteLine();
            Console.WriteLine("PR. ANTAL TOMME FELTER (gennemsnit)");
            Console.WriteLine($"{"tomme",6}{"antal",8}{"pos uden",12}{"pos med",12}{"besparelse",12}{"tid uden",11}{"tid med",11}");
            for (int e = 9; e >= 1; e--)
            {
                if (nodesBy[0, e].Count == 0) continue;
                double saving = 100.0 * (1.0 - nodesBy[1, e].Avg / nodesBy[0, e].Avg);
                Console.WriteLine($"{e,6}{nodesBy[0, e].Count,8}{nodesBy[0, e].Avg,12:F1}{nodesBy[1, e].Avg,12:F1}{saving,11:F1}%" +
                    $"{timeBy[0, e].Avg,11:F4}{timeBy[1, e].Avg,11:F4}");
            }
            Console.WriteLine();
            Console.WriteLine($"Filer gemt i: {Path.GetFullPath(folder)}");
        }

        public static void ValidateExpectiMinimax(int positions = 20000, int seed = 1)
        {
            var rng = new Random(seed);
            var solver = new ExpectiSolver(0.0);
            int tested = 0, mismatches = 0;

            while (tested < positions)
            {
                var board = new gameBoard();
                Player current = rng.Next(2) == 0 ? Player.X : Player.O;
                int plies = rng.Next(0, 8);
                bool valid = true;

                for (int i = 0; i < plies; i++)
                {
                    var cells = board.EmptyCells();
                    if (cells.Count == 0) { valid = false; break; }
                    var (r, c) = cells[rng.Next(cells.Count)];
                    board.Place(r, c, current);
                    if (board.Winner() != Player.N) { valid = false; break; }
                    current = gameBoard.Opponent(current);
                }
                if (!valid || board.EmptyCells().Count == 0) continue;

                var a = board.BestMove(current);
                var b = board.ExpectiBestMove(current, solver);
                tested++;
                if (a != b) mismatches++;
            }
            Console.WriteLine($"Validering: {tested} stillinger, {mismatches} forskellige træk (skal være 0)");
        }

        public static void RandomnessTournament(
            int gamesPerSetting = 2000, 
            int seed = 42,
            double[]? probabilities = null, 
            string folder = "resultater"
            )
        {
            probabilities ??= new[] { 0.0, 0.1, 0.25, 0.5, 0.75, 1.0 };
            var rng = new Random(seed);
            var dk = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            dk.NumberFormat.NumberDecimalSeparator = ",";

            var naive = new ExpectiSolver(0.0);

            Directory.CreateDirectory(folder);
            using var csv = new StreamWriter(Path.Combine(folder, "turnering.csv"), false, new UTF8Encoding(true));
            csv.WriteLine("laas_sandsynlighed;spil;smart_sejre;uafgjort;smart_nederlag;smart_gns_resultat;ci95_nedre;ci95_oevre;" +
                          "unikke_tilstande;teori_forventet_vaerdi;simuleret_gns_vaerdi;simuleret_stdfejl");

            Console.WriteLine($"Turnering: {gamesPerSetting} spil pr. sandsynlighed, seed {seed}");
            Console.WriteLine();
            Console.WriteLine("SMART (expectiminimax) MOD NAIV (minimax der ignorerer låse)");
            Console.WriteLine($"{"p",6}{"spil",7}{"sejre",8}{"uafgjort",10}{"nederlag",10}{"gns",9}{"95% CI",22}");

            var validation = new List<string>();

            foreach (double p in probabilities)
            {
                var smart = p == 0.0 ? naive : new ExpectiSolver(p);

                // ---- smart mod naiv ----
                int wins = 0, draws = 0, losses = 0;
                var outcome = new Stat();   // +1 sejr, 0 uafgjort, -1 nederlag (set fra smarts side)
                for (int g = 0; g < gamesPerSetting; g++)
                {
                    bool smartIsX = g % 2 == 0;
                    var (winner, _) = PlayLockedGame(smartIsX ? smart : naive, smartIsX ? naive : smart, p, rng);
                    Player smartPlayer = smartIsX ? Player.X : Player.O;
                    int res = winner == Player.N ? 0 : (winner == smartPlayer ? 1 : -1);
                    if (res == 1) wins++; else if (res == 0) draws++; else losses++;
                    outcome.Add(res);
                }
                double se = outcome.StdErr;
                double lo = outcome.Avg - 1.96 * se, hi = outcome.Avg + 1.96 * se;

                // ---- smart mod smart: simuleret gennemsnit kontra teoretisk forventet værdi ----
                var valueStat = new Stat();
                for (int g = 0; g < gamesPerSetting; g++)
                {
                    var (winner, pieces) = PlayLockedGame(smart, smart, p, rng);
                    double score = winner == Player.N ? 0 : (winner == Player.X ? 1 : -1) * (10 - pieces);
                    valueStat.Add(score);
                }
                double theory = new gameBoard().ExpectedValueX(smart);

                csv.WriteLine(string.Join(";", p.ToString("F2", dk), gamesPerSetting, wins, draws, losses,
                    outcome.Avg.ToString("F4", dk), lo.ToString("F4", dk), hi.ToString("F4", dk),
                    smart.StatesComputed, theory.ToString("F4", dk), valueStat.Avg.ToString("F4", dk), valueStat.StdErr.ToString("F4", dk)));

                Console.WriteLine($"{p,6:F2}{gamesPerSetting,7}{wins,8}{draws,10}{losses,10}{outcome.Avg,9:F4}   [{lo,8:F4}; {hi,8:F4}]");
                validation.Add($"{p,6:F2}{smart.StatesComputed,14}{theory,14:F4}{valueStat.Avg,14:F4}{valueStat.StdErr,12:F4}");
            }

            Console.WriteLine();
            Console.WriteLine("SMART MOD SMART: teori kontra simulation (score = ±(10 - antal brikker), uafgjort = 0)");
            Console.WriteLine($"{"p",6}{"tilstande",14}{"teori E",14}{"simuleret",14}{"std.fejl",12}");
            foreach (var line in validation) Console.WriteLine(line);
            Console.WriteLine();
            Console.WriteLine($"Filer gemt i: {Path.GetFullPath(folder)}");
        }

        // Ét spil med tilfældige låse. Returnerer vinder (N = uafgjort) og antal brikker på brættet til sidst.
        private static (Player winner, int pieces) PlayLockedGame(ExpectiSolver xAgent, ExpectiSolver oAgent, double lockP, Random rng)
        {
            var board = new gameBoard();
            Player current = Player.X;

            while (true)
            {
                ExpectiSolver agent = current == Player.X ? xAgent : oAgent;
                var (r, c) = board.ExpectiBestMove(current, agent);
                board.Place(r, c, current);
                board.ClearLocks();   // en lås gælder kun ét træk

                Player winner = board.Winner();
                if (winner != Player.N) return (winner, board.PieceCount());
                if (board.IsFull()) return (Player.N, board.PieceCount());

                // Tilfældig hændelse (kræver mindst to tomme felter, så modstanderen har et lovligt træk)
                var cells = board.EmptyCells();
                if (lockP > 0 && cells.Count >= 2 && rng.NextDouble() < lockP)
                {
                    var (lr, lc) = cells[rng.Next(cells.Count)];
                    board.Place(lr, lc, Player.L);
                }
                current = gameBoard.Opponent(current);
            }
        }

        // Én søgning: returnerer valgt træk, antal besøgte positioner og tid i millisekunder
        private static Result Measure(gameBoard board, Player player)
        {
            long start = Stopwatch.GetTimestamp();
            var move = board.BestMove(player);
            long end = Stopwatch.GetTimestamp();
            double ms = (end - start) * 1000.0 / Stopwatch.Frequency;
            return new Result(move, board.NodesVisited, ms);
        }

        private readonly record struct Result((int row, int col) Move, int Nodes, double Ms);

        // Holder min, max, sum og sum af kvadrater, så gennemsnit og standardfejl kan beregnes
        private class Stat
        {
            public long Count;
            public double Sum;
            public double SumSq;
            public double Min = double.MaxValue;
            public double Max = double.MinValue;
            public double Avg => Count == 0 ? 0 : Sum / Count;
            public double Variance => Count < 2 ? 0 : (SumSq - Sum * Sum / Count) / (Count - 1);
            public double StdErr => Count < 2 ? 0 : Math.Sqrt(Variance / Count);

            public void Add(double v)
            {
                Count++;
                Sum += v;
                SumSq += v * v;
                if (v < Min) Min = v;
                if (v > Max) Max = v;
            }
        }

        // Husker værdien af hver stilling (set fra X's side), så den kun beregnes én gang.
        // Nøgle = de 9 felter (X/O/tom) + hvis tur det er + hvilket felt der er låst. Gælder kun for én værdi af p.
        private class ExpectiSolver
        {
            public const int KeyCount = 19683 * 2 * 10;   // 3^9 stillinger * 2 spillere * (intet eller ét af 9 felter låst)
            public readonly double P;
            public readonly double[] Value = new double[KeyCount];
            public readonly bool[] Known = new bool[KeyCount];
            public long StatesComputed;

            public ExpectiSolver(double p) { P = p; }
        }

        private class gameBoard
        {
            private Player[,] m_state = {
                { Player.N, Player.N, Player.N },
                { Player.N, Player.N, Player.N },
                { Player.N, Player.N, Player.N }
            };

            private int m_aiMoveCount;
            private bool m_useAlfaBetaPruning;

            public gameBoard(bool useAlfaBetaPruning = false)
            {
                m_useAlfaBetaPruning = useAlfaBetaPruning;
            }

            // Human input
            public (bool, string) move(string move, Player player)
            {
                if (move.Length != 2) return (false, "incorrect move, must be one letter and one number");
                move = move.ToLower();

                int col = move[0] - 'a';   // A,B,C -> 0,1,2
                int row = move[1] - '0';   // 0,1,2

                if (col < 0 || col > 2) return (false, $"invalid move, first arg was not A, B or C, but was {move[0]}");
                if (row < 0 || row > 2) return (false, $"invalid move, second arg was not 0, 1 or 2, but was {move[1]}");
                if (m_state[row, col] == Player.L) return (false, "that square is locked");
                if (m_state[row, col] != Player.N) return (false, "that square is already taken");

                m_state[row, col] = player;
                return (true, "success!");
            }

            public void Place(int row, int col, Player player) => m_state[row, col] = player;

            public List<(int row, int col)> EmptyCells()
            {
                var cells = new List<(int row, int col)>();
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                        if (m_state[r, c] == Player.N) cells.Add((r, c));
                return cells;
            }

            // Låste felter gælder kun ét træk: åbn dem igen
            public void ClearLocks()
            {
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                        if (m_state[r, c] == Player.L) m_state[r, c] = Player.N;
            }

            // Antal brikker (X og O) på brættet. Låste felter tæller ikke med.
            public int PieceCount()
            {
                int n = 0;
                foreach (var p in m_state)
                    if (p == Player.X || p == Player.O) n++;
                return n;
            }

            // Game state
            private static bool IsPiece(Player p) => p == Player.X || p == Player.O;

            public Player Winner()
            {
                for (int i = 0; i < 3; i++)
                {
                    if (IsPiece(m_state[i, 0]) && m_state[i, 0] == m_state[i, 1] && m_state[i, 1] == m_state[i, 2])
                        return m_state[i, 0];
                    if (IsPiece(m_state[0, i]) && m_state[0, i] == m_state[1, i] && m_state[1, i] == m_state[2, i])
                        return m_state[0, i];
                }
                if (IsPiece(m_state[1, 1]))
                {
                    if (m_state[0, 0] == m_state[1, 1] && m_state[1, 1] == m_state[2, 2]) return m_state[1, 1];
                    if (m_state[0, 2] == m_state[1, 1] && m_state[1, 1] == m_state[2, 0]) return m_state[1, 1];
                }
                return Player.N;
            }

            // Sand hvis der ikke er flere felter, man kan spille på (fuldt bræt eller låste felter)
            public bool IsFull()
            {
                foreach (var p in m_state)
                    if (p == Player.N) return false;
                return true;
            }

            // Minimax
            public static Player Opponent(Player p) => p == Player.X ? Player.O : Player.X;

            // turn = whose move it is in this position, ai = the player we're maximizing for
            public int NodesVisited => m_aiMoveCount;

            public (int row, int col) BestMove(Player ai)
            {
                int bestScore = int.MinValue;
                (int, int) bestMove = (-1, -1);
                int alpha = int.MinValue;
                int beta = int.MaxValue;
                m_aiMoveCount = 0; // nulstil én gang, før løkken

                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        if (m_state[r, c] != Player.N) continue;

                        m_state[r, c] = ai;
                        int score = Minimax(Opponent(ai), ai, 1, alpha, beta);
                        m_state[r, c] = Player.N;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestMove = (r, c);
                        }
                        alpha = Math.Max(alpha, bestScore);
                    }
                return bestMove;
            }

            private int Minimax(Player turn, Player ai, int depth, int alpha, int beta)
            {
                m_aiMoveCount++; // tæl hver besøgt position (både max og min)

                Player winner = Winner();
                if (winner == ai) return 10 - depth;
                if (winner != Player.N) return depth - 10;
                if (IsFull()) return 0;

                bool maximizing = turn == ai;

                if (maximizing)
                {
                    int best = int.MinValue;
                    for (int r = 0; r < 3; r++)
                        for (int c = 0; c < 3; c++)
                        {
                            if (m_state[r, c] != Player.N) continue;

                            m_state[r, c] = turn;
                            int score = Minimax(Opponent(turn), ai, depth + 1, alpha, beta);
                            m_state[r, c] = Player.N;

                            best = Math.Max(best, score);
                            alpha = Math.Max(alpha, best);
                            if (m_useAlfaBetaPruning && beta <= alpha) return best;
                        }
                    return best;
                }
                else
                {
                    int best = int.MaxValue;
                    for (int r = 0; r < 3; r++)
                        for (int c = 0; c < 3; c++)
                        {
                            if (m_state[r, c] != Player.N) continue;

                            m_state[r, c] = turn;
                            int score = Minimax(Opponent(turn), ai, depth + 1, alpha, beta);
                            m_state[r, c] = Player.N;

                            best = Math.Min(best, score);
                            beta = Math.Min(beta, best);
                            if (m_useAlfaBetaPruning && beta <= alpha) return best;
                        }
                    return best;
                }
            }

            private const double Eps = 1e-9;

            public (int row, int col) ExpectiBestMove(Player player, ExpectiSolver solver)
            {
                // Felter, der er låst lige nu, kan ikke spilles i dette træk. Derefter er de åbne igen,
                // så de åbnes midlertidigt, mens fremtiden regnes ud.
                var locked = new List<(int row, int col)>();
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                        if (m_state[r, c] == Player.L) { locked.Add((r, c)); m_state[r, c] = Player.N; }

                bool maximizing = player == Player.X;
                double bestValue = maximizing ? double.NegativeInfinity : double.PositiveInfinity;
                (int, int) bestMove = (-1, -1);
                int pieces = PieceCount();

                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        if (m_state[r, c] != Player.N || locked.Contains((r, c))) continue;

                        m_state[r, c] = player;
                        double v = AfterMove(player, solver, pieces + 1);
                        m_state[r, c] = Player.N;

                        // Første strengt bedre træk vinder (som i almindelig minimax)
                        if (maximizing ? v > bestValue + Eps : v < bestValue - Eps)
                        {
                            bestValue = v;
                            bestMove = (r, c);
                        }
                    }

                foreach (var (r, c) in locked) m_state[r, c] = Player.L;
                return bestMove;
            }

            // Teoretisk forventet værdi (set fra X) i den nuværende stilling, hvor X skal trække
            public double ExpectedValueX(ExpectiSolver solver) => MoveValue(Player.X, solver, PieceCount(), -1);

            // Stillingen lige efter at 'mover' har trukket: slutværdi eller chance-knude
            private double AfterMove(Player mover, ExpectiSolver s, int pieces)
            {
                Player winner = Winner();
                if (winner != Player.N) return (winner == Player.X ? 1 : -1) * (10 - pieces);

                int empties = 0;
                foreach (var p in m_state) if (p == Player.N) empties++;
                if (empties == 0) return 0;   // fuldt bræt: uafgjort

                Player next = Opponent(mover);
                double noLock = MoveValue(next, s, pieces, -1);
                if (s.P <= 0 || empties < 2) return noLock;

                double lockSum = 0;
                for (int i = 0; i < 9; i++)
                {
                    if (m_state[i / 3, i % 3] != Player.N) continue;
                    lockSum += MoveValue(next, s, pieces, i);   // 'next' må ikke spille på felt i
                }
                return (1 - s.P) * noLock + s.P * lockSum / empties;
            }

            // Værdien af en stilling hvor 'turn' skal trække (max for X, min for O).
            // skip = indeks (0..8) på et felt, der er låst i dette træk, eller -1.
            private double MoveValue(Player turn, ExpectiSolver s, int pieces, int skip)
            {
                int key = Encode(turn, skip);
                if (s.Known[key]) return s.Value[key];

                bool maximizing = turn == Player.X;
                double best = maximizing ? double.NegativeInfinity : double.PositiveInfinity;
                bool anyMove = false;

                for (int i = 0; i < 9; i++)
                {
                    int r = i / 3, c = i % 3;
                    if (m_state[r, c] != Player.N || i == skip) continue;
                    anyMove = true;

                    m_state[r, c] = turn;
                    double v = AfterMove(turn, s, pieces + 1);
                    m_state[r, c] = Player.N;

                    best = maximizing ? Math.Max(best, v) : Math.Min(best, v);
                }
                if (!anyMove) best = 0;

                s.Known[key] = true;
                s.Value[key] = best;
                s.StatesComputed++;
                return best;
            }

            private int Encode(Player turn, int skip)
            {
                int key = 0;
                foreach (var p in m_state) key = key * 3 + (int)p;   // X=0, O=1, N=2 (der er ingen L i regnetræet)
                key = key * 2 + (turn == Player.X ? 0 : 1);
                return key * 10 + (skip + 1);
            }

            // Drawing
            public void Draw()
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine("┃A┃B┃C┃");
                Console.WriteLine("┠─╂─╂─╂─");
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        string symbol = m_state[i, j] == Player.N ? " "
                                      : m_state[i, j] == Player.L ? "#"
                                      : m_state[i, j].ToString();
                        Console.Write($"┃{symbol}");
                    }
                    Console.WriteLine($"┃{i}");
                    if (i < 2) Console.WriteLine("┠─╂─╂─╂─");
                }
                Console.WriteLine("┖─┸─┸─┸─");
                Console.WriteLine($"AI moves considered: {m_aiMoveCount}");
            }
        }
    }
}