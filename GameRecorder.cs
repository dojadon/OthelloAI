using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OthelloAI
{
    class GameRecorder
    {
        public string FilePath { get; }
        public Board[] Boards { get; } = new Board[60];
         int Index { get; set; }

        public void Stack(Board board)
        {
            Boards[Index++] = board;
        }

        public void Clear()
        {
            Index = 0;
        }

        public void Save()
        {
            using var writer = new BinaryWriter(new FileStream(FilePath, FileMode.Append));

            writer.Write(Index);
            for(int i = 0; i < Index; i++)
            {
                writer.Write(Boards[i].bitB);
                writer.Write(Boards[i].bitW);
            }
        }
    }
}
