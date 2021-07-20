using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace OthelloAI
{
    public class Client
    {
        Board Board { get; set; } = new Board();
        Player Player { get; }
        int Stone { get; set; }

        public delegate void BoardChangedEventHandler(Board board);
        public BoardChangedEventHandler OnChangedBoard;

        public delegate void DisconnectedEventHandler();
        public DisconnectedEventHandler OnDisconnected;

        public Client(Player player)
        {
            Player = player;
        }

        private void RecieveStartMessage(string[] tokens)
        {
            Stone = int.Parse(tokens[1]);
            Console.WriteLine("Client Stone" + Stone);
        }

        private string RecieveTurnMessage(string[] tokens)
        {
            if (Stone == int.Parse(tokens[1]))
            {
                Move move = Player.DecideMove(Board, Stone);
                return $"PUT {move.x} {move.y}";
            }
            else
            {
                GC.Collect();
                return null;
            }
        }

        private void RecieveBoardMessage(string[] tokens)
        {
            int[, ] b = new int[8, 8];

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    b[i, j] = int.Parse(tokens[i * 8 + j + 1]);
                }
            }
            Board = new Board(b);
            Board.print();
            OnChangedBoard?.Invoke(Board);
        }

        private void SendMessage(StreamWriter writer, string message)
        {
            if (message == null)
                return;

            Console.WriteLine($"SEND -> {message}");
            writer.WriteLine(message);
            writer.Flush();
        }

        public void Run(string server, int port, string nickname)
        {
            TcpClient client = new TcpClient(server, port);
            NetworkStream stream = client.GetStream();

            var writer = new StreamWriter(stream);
            var reader = new StreamReader(stream);

            try
            {
                bool end = false;

                while (!end)
                {
                    string line = reader.ReadLine();
                    string[] tokens = line.Split();
                  //  Console.WriteLine("RECV <- " + line);

                    switch (tokens[0])
                    {
                        case "START":
                            RecieveStartMessage(tokens);
                            SendMessage(writer, $"NICK {nickname}");
                            break;

                        case "BOARD":
                            RecieveBoardMessage(tokens);
                            break;

                        case "TURN":
                            SendMessage(writer, RecieveTurnMessage(tokens));
                            break;

                        case "CLOSE":
                        case "END":
                            end = true;
                            break;
                    }
                }
            }
            catch(IOException e)
            {
                Console.WriteLine(e.StackTrace);
            }

            OnDisconnected?.Invoke();
        }
    }
}
