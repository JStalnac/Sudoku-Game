using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Sudoku.Model;

namespace Sudoku
{
    public class UIThread
    {
        private Game Game { get; set; }

        private ConcurrentQueue<Action> EventQueue { get; } = new ConcurrentQueue<Action>();

        public string Text { get; private set; } = "";

        public static Cell.RCPosition CursorPosition { get; private set; } = new Cell.RCPosition(1, 1);

        public UIThread(Game game)
        {
            Game = game;
        }

        public void Start()
        {
            Console.CursorVisible = false;
            Console.Clear();
            WriteSudoku();

            while (true)
            {
                while (!EventQueue.IsEmpty)
                    if (EventQueue.TryDequeue(out Action action))
                        action.Invoke();

                Thread.Sleep(5);
            }
        }

        /// <summary>
        /// Queues an action to be run on the UI thread
        /// </summary>
        /// <param name="action"></param>
        public void QueueAction(Action action)
        {
            if (action is null)
                throw new ArgumentNullException(nameof(action));
            EventQueue.Enqueue(action);
        }

        /// <summary>
        /// Displays the sudoku board
        /// </summary>
        public void WriteSudoku()
        {
            QueueAction(() =>
            {
                // Position to return back to.
                int ctop = Console.CursorTop;

                // Reset the console
                Console.ResetColor();
                Console.SetCursorPosition(0, 0);

                // Draw the sudoku board.
                for (int row = 0; row < 9; row++)
                {
                    // Row seperators
                    if (row % 3 == 0 || row == 9)
                        AppendRowSeperator();

                    for (int col = 0; col < 9; col++)
                    {
                        // Column start seperator
                        if (col == 0)
                            Console.Write("| ");

                        // Get a cell
                        var cell = Game.Sudoku.GetCell(new Cell.RCPosition(row + 1, col + 1));

                        // Display the number, or a dot if the cell is empty
                        string s = cell.Value != -1 ? cell.Value.ToString() : ".";
                        // How should the number end? Column seperator or just an empty 
                        // space for the next number?
                        string end = col % 3 == 2 ? " | " : " ";

                        // Color for the selected cell
                        if (row == (CursorPosition.Row - 1) & col == (CursorPosition.Column - 1))
                        {
                            Console.BackgroundColor = ConsoleColor.White;
                            Console.ForegroundColor = ConsoleColor.Black;
                        }

                        // Write cell
                        Console.Write(s);
                        Console.ResetColor();
                        Console.Write(end);
                    }

                    Console.WriteLine();
                }

                // Bottom seperator
                AppendRowSeperator();
                Console.Write("\n\n");

                // Back to the old position
                if (Console.CursorTop < ctop)
                    Console.CursorTop = ctop;
            });
        }

        private void AppendRowSeperator()
        {
            Console.WriteLine("+ ----- + ----- + ----- +");
        }

        /// <summary>
        /// Moves the white cursor
        /// </summary>
        /// <param name="direction"></param>
        public void MoveCursor(MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Up:
                    if (CursorPosition.Row != 1)
                        CursorPosition = new Cell.RCPosition(CursorPosition.Row - 1, CursorPosition.Column);
                    break;
                case MoveDirection.Down:
                    if (CursorPosition.Row != 9)
                        CursorPosition = new Cell.RCPosition(CursorPosition.Row + 1, CursorPosition.Column);
                    break;
                case MoveDirection.Left:
                    if (CursorPosition.Column != 1)
                        CursorPosition = new Cell.RCPosition(CursorPosition.Row, CursorPosition.Column - 1);
                    break;
                case MoveDirection.Right:
                    if (CursorPosition.Column != 9)
                        CursorPosition = new Cell.RCPosition(CursorPosition.Row, CursorPosition.Column + 1);
                    break;
            }
            WriteSudoku();
        }

        /// <summary>
        /// Writes text to the console. Doesn't add a new line.
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="fore">Foreground color</param>
        /// <param name="back">Background color</param>
        public void DisplayText(string text, ConsoleColor fore = ConsoleColor.Gray, ConsoleColor back = ConsoleColor.Black)
        {
            QueueAction(() =>
            {
                Console.ForegroundColor = fore;
                Console.BackgroundColor = back;
                Console.Write(text);
                Text += text;
                Console.ResetColor();
            });
        }

        /// <summary>
        /// Resets the display without removing the sudoku board.
        /// </summary>
        public void ResetDisplay()
        {
            QueueAction(() =>
            {
                Console.Clear();
                Text = "";
                WriteSudoku();
            });
        }

        /// <summary>
        /// Clears the console and removes the sudoku board. Equivalent to Console.Clear()
        /// </summary>
        public void ClearConsole()
        {
            QueueAction(() =>
            {
                Console.ResetColor();
                Console.Clear();
                Text = "";
            });
        }
    }
}