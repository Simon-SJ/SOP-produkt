namespace produkt
{
    enum Player { X, O, N }

    public class krydsOgBolle
    {
        private Player m_currentPlayer;
        private readonly Player m_aiPlayer = Player.X;
        private bool m_useMiniMax;
        private gameBoard m_board;
        private bool m_IsRunning;

        public krydsOgBolle(bool useMiniMax = false)
        {
            m_IsRunning = true;
            m_board = new();
            m_currentPlayer = Player.O;
            m_useMiniMax = useMiniMax;
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
                    var (r, c) = m_board.BestMove(m_aiPlayer);
                    m_board.Place(r, c, m_currentPlayer);
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
                Console.WriteLine("Please write the move (eg. A1):");
                string input = Console.ReadLine() ?? "";
                var (success, msg) = m_board.move(input, m_currentPlayer);
                if (success) return;
                Console.WriteLine(msg);
            }
        }

        private class gameBoard
        {
            private Player[,] m_state = {
                { Player.N, Player.N, Player.N },
                { Player.N, Player.N, Player.N },
                { Player.N, Player.N, Player.N }
            };

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
            private static Player Opponent(Player p) => p == Player.X ? Player.O : Player.X;

            // turn = whose move it is in this position, ai = the player we're maximizing for
            public (int row, int col) BestMove(Player ai)
            {
                int bestScore = int.MinValue;
                (int, int) bestMove = (-1, -1);
                int alpha = int.MinValue;
                int beta = int.MaxValue;

                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        if (m_state[r, c] != Player.N) continue;

                        m_state[r, c] = ai;
                        int score = Minimax(Opponent(ai), ai, 1, alpha, beta);
                        m_state[r, c] = Player.N;   // undo

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
                            if (beta <= alpha) return best;   // prune
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
                            if (beta <= alpha) return best;   // prune
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
            }
        }
    }
}