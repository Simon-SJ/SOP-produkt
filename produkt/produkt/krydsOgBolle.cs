using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace produkt
{
    enum Player { X, O, N }

    public class krydsOgBolle
    {
        private Player m_currentPlayer;
        private readonly Player m_aiPlayer = Player.X;
        private bool m_useMiniMax;
        private bool m_useAlfaBetaPruning;
        private gameBoard m_board;
        private bool m_IsRunning;
        private Stopwatch m_time;


        public krydsOgBolle(bool useMiniMax = false, bool useAlfaBetaPruning = false)
        {
            m_IsRunning = true;
            m_board = new(useAlfaBetaPruning);
            m_currentPlayer = Player.O;
            m_useMiniMax = useMiniMax;
            m_useAlfaBetaPruning = useAlfaBetaPruning;
            m_time = new Stopwatch();
            m_time.Stop();
            Run();
        }

        public void Draw()
        {
            Console.WriteLine($"it's {m_currentPlayer}'s turn.");
            m_board.Draw();
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
                    var (r, c) = m_board.BestMove(m_aiPlayer);
                    m_board.Place(r, c, m_currentPlayer);
                    m_time.Stop();
                }
                else
                {
                    HumanMove();
                }

                Player winner = m_board.Winner();
                if (winner != Player.N || m_board.IsFull())
                {
                    Console.Clear();
                    m_board.Draw();
                    Console.WriteLine(winner != Player.N ? $"{winner} wins!" : "It's a draw!");
                    m_IsRunning = false;
                    return;
                }

                m_currentPlayer = m_currentPlayer == Player.X ? Player.O : Player.X;
            }
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

        // Holder min, max og sum, så gennemsnittet kan beregnes
        private class Stat
        {
            public long Count;
            public double Sum;
            public double Min = double.MaxValue;
            public double Max = double.MinValue;
            public double Avg => Count == 0 ? 0 : Sum / Count;

            public void Add(double v)
            {
                Count++;
                Sum += v;
                if (v < Min) Min = v;
                if (v > Max) Max = v;
            }
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

            // Game state
            public Player Winner()
            {
                for (int i = 0; i < 3; i++)
                {
                    if (m_state[i, 0] != Player.N && m_state[i, 0] == m_state[i, 1] && m_state[i, 1] == m_state[i, 2])
                        return m_state[i, 0];
                    if (m_state[0, i] != Player.N && m_state[0, i] == m_state[1, i] && m_state[1, i] == m_state[2, i])
                        return m_state[0, i];
                }
                if (m_state[1, 1] != Player.N)
                {
                    if (m_state[0, 0] == m_state[1, 1] && m_state[1, 1] == m_state[2, 2]) return m_state[1, 1];
                    if (m_state[0, 2] == m_state[1, 1] && m_state[1, 1] == m_state[2, 0]) return m_state[1, 1];
                }
                return Player.N;
            }

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
                        string symbol = m_state[i, j] == Player.N ? " " : m_state[i, j].ToString();
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