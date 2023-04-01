using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace OthelloAI
{
    public class EdaxRunner
    {
        public const string MODE_2 = "mode 2";
        public const string GAME_OVER = "*** Game Over ***";

        public LinkedList<string> Log { get; } = new LinkedList<string>();

        public HashSet<Board> Boards { get; } = new HashSet<Board>();

        public int Count { get; set; }

        void DataReceived(string data, StreamWriter writer)
        {
            if (data == null) return;

            Log.AddFirst(data);

            if (Log.Count > 12)
            {
                Log.RemoveLast();
            }

            if (data.Contains(GAME_OVER))
            {
                Count++;

                string[] lines = Log.ToArray()[3..11];

                int[] discs = lines.SelectMany(s => s.Split("|")[1..9].Select(t => int.TryParse(t, out int i) ? i : 0)).ToArray();
                int[] moves = discs.Select((x, i) => (x, i)).Where(t => t.x > 0).OrderBy(t => t.x).Select(t => t.i).ToArray();

                writer.WriteLine(string.Join(",", moves));

                Board board = Board.Init.ColorFliped();
                int color = 1;
                foreach (var m in moves)
                {
                    ulong move = 1UL << m;

                    if ((board.GetMoves(color) & move) != 0)
                    {
                        board = board.Reversed(1UL << m, color);
                        color = -color;
                    }
                    else if ((board.GetMoves(-color) & move) != 0)
                    {
                        board = board.Reversed(1UL << m, -color);
                    }
                    else
                    {
                        Console.WriteLine("Parse Error");
                    }
                }

                Boards.Add(board);

                Console.WriteLine($"{Count}, {Boards.Count}");
            }
        }

        public void StartEdax(string path)
        {
            using StreamWriter writer = new("log.txt");

            using var ctoken = new CancellationTokenSource();

            var edax_process = new Process();
            edax_process.StartInfo.FileName = path;
            edax_process.StartInfo.UseShellExecute = false;
            edax_process.StartInfo.RedirectStandardError = true;
            edax_process.StartInfo.RedirectStandardOutput = true;
            edax_process.StartInfo.RedirectStandardInput = true;
            edax_process.StartInfo.CreateNoWindow = true;

            edax_process.OutputDataReceived += (sender, ev) =>
            {
                DataReceived(ev.Data, writer);
                writer.Flush();
            };

            edax_process.ErrorDataReceived += (sender, ev) =>
            {
                Console.WriteLine($"stderr={ev.Data}");
            };
            edax_process.Exited += (sender, ev) =>
            {
                Console.WriteLine($"exited");
                ctoken.Cancel();
            };

            edax_process.Start();

            edax_process.BeginErrorReadLine();
            edax_process.BeginOutputReadLine();

            edax_process.StandardInput.WriteLine(MODE_2);

            ctoken.Token.WaitHandle.WaitOne();
        }
    }
}
