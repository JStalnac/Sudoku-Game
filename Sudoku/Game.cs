using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Sudoku;
using Sudoku.Model;

namespace Sudoku
{
    public class Game
    {
        public SudokuBoard Sudoku { get; private set; } = new SudokuBoard();

        public IReadOnlyCollection<Cell.RCPosition> LockedCells { get; private set; }

        private Dictionary < string, (string, Action) > Commands { get; } = new Dictionary < string, (string, Action) > ();

        public UIThread UI { get; private set; }

        public async Task Start()
        {
            // Set console title
            string originalTitle = Console.Title;
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.CursorVisible = true;
                Console.Title = originalTitle;
            };

            Console.Title = "Sudoku";

            // Commands
            CreateCommands();

            // Generate sudoku
            RegeneratePuzzle(51);

            // Start threads
            UI = new UIThread(this);

            var ui = new Thread(UI.Start);
            var input = new Thread(InputThreadStart);

            ui.Start();
            input.Start();

            await Task.Delay(100);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Sudoku");
            sb.AppendLine(new String('-', 10));
            sb.AppendLine("Press c and enter 'help' for help.");
            UI.DisplayText(sb.ToString());

            await Task.Delay(-1);
        }

        public void RegeneratePuzzle(int removedCells, bool ignoreEmpty = false)
        {
            // Create a new sudoku board
            while (!Sudoku.Solver.SolveThePuzzle())
            {
                Console.WriteLine("Failed to generate a puzzle. Retrying...");
                Thread.Sleep(10);
            }

            // Remove some cells
            RemoveCells(removedCells, ignoreEmpty);

            // Get locked cells
            var locked = new List<Cell.RCPosition>();
            for (int row = 1; row < 10; row++)
            {
                for (int col = 1; col < 10; col++)
                {
                    // Get all the now empty cells
                    var cell = Sudoku.GetCell(new Cell.RCPosition(row, col));
                    if (cell.Value != -1)
                        locked.Add(cell.Position);
                }
            }
            LockedCells = locked.AsReadOnly();
        }

        public void RemoveCells(int amount, bool ignoreEmpty = false)
        {
            if (amount < 0 || amount > 81)
                throw new ArgumentOutOfRangeException(nameof(amount));

            // Generates an unique random
            Random r = new Random(Guid.NewGuid().GetHashCode());

            for (int i = 0; i < amount; i++)
            {
                int index = r.Next(0, Sudoku.TOTAL_CELLS);

                if (!ignoreEmpty)
                {
                    var cell = Sudoku.GetCell(index);
                    if (cell.Value == -1)
                    {
                        i--;
                        continue;
                    }
                }
                Sudoku.SetCellValue(-1, index);
            }
        }

        public void InputThreadStart()
        {
            while (true)
            {
                var key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.UpArrow)
                    UI.MoveCursor(MoveDirection.Up);
                else if (key.Key == ConsoleKey.DownArrow)
                    UI.MoveCursor(MoveDirection.Down);
                else if (key.Key == ConsoleKey.LeftArrow)
                    UI.MoveCursor(MoveDirection.Left);
                else if (key.Key == ConsoleKey.RightArrow)
                    UI.MoveCursor(MoveDirection.Right);

                // Commands
                else if (key.KeyChar == 'c')
                {
                    try
                    {
                        UI.DisplayText("Enter command:\n> ");
                        Console.CursorVisible = true;
                        string commandString = Console.ReadLine();

                        if (!String.IsNullOrWhiteSpace(commandString) && !String.IsNullOrEmpty(commandString))
                        {
                            var cmd = Commands.SingleOrDefault(x => commandString.StartsWith(x.Key));

                            if (cmd.Key is null)
                                UI.DisplayText("Error: Unknown command\n", fore : ConsoleColor.Red);
                            else
                            {
                                try
                                {
                                    cmd.Value.Item2.Invoke();
                                }
                                catch (Exception ex)
                                {
                                    UI.DisplayText($"Command failed: {ex.GetType()}: {ex.Message}", fore : ConsoleColor.Red);

                                    string path = $"{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.error";
                                    File.WriteAllText(path, ex.ToString());
                                }
                            }
                        }
                    }
                    finally
                    {
                        Console.CursorVisible = false;
                    }
                }

                int value = (int)Char.GetNumericValue(key.KeyChar);
                if (value != -1)
                {
                    // Number key pressed

                    // Get the selected cell
                    var pos = UIThread.CursorPosition;
                    var cell = Sudoku.GetCell(pos);
                    if (LockedCells.Any(x => x.Column == cell.Position.Column & x.Row == cell.Position.Row))
                    {
                        // Locked cell
                        string message = "This cell is locked\n";
                        if (!UI.Text.EndsWith(message))
                        {
                            UI.DisplayText(message);
                        }
                    }
                    else
                    {
                        // Empty cell c:
                        if (value == 0)
                            Sudoku.SetCellValue(-1, cell.Index);
                        else
                            Sudoku.SetCellValue(value, cell.Index);

                        UI.WriteSudoku();

                        if (Sudoku.IsBoardFilled())
                            UI.DisplayText("The board is filled! You can now check if it's correct with 'check'\n");
                    }
                }
            }
        }

        private void CreateCommands()
        {
            Commands.Add("clear", ("Clears the console",
                new Action(() =>
                {
                    UI.ClearConsole();
                    UI.WriteSudoku();
                })));
            Commands.Add("exit", ("Exits the game",
                new Action(() =>
                {
                    UI.DisplayText("Are you sure you want to exit? (Y/n)\n");

                    ConsoleKeyInfo answer;
                    bool valid = false;
                    do
                    {
                        answer = Console.ReadKey(true);
                        // Valid key
                        if (answer.Key == ConsoleKey.Enter || answer.Key == ConsoleKey.Y || answer.Key == ConsoleKey.N)
                            valid = true;
                    } while (!valid);

                    if (answer.Key == ConsoleKey.Enter || answer.Key == ConsoleKey.Y)
                        Environment.Exit(0);
                })));
            Commands.Add("help", ("Shows this help message",
                new Action(() =>
                {
                    var sb = new System.Text.StringBuilder();

                    // Help
                    sb.AppendLine();
                    sb.AppendLine("Sudoku Help");
                    sb.AppendLine(new String('-', 10));
                    sb.AppendLine("Use the arrow keys to move the cursor");
                    sb.AppendLine("Use keys 1-9 to set the value of a tile and 0 to erase.");
                    sb.AppendLine();

                    // Rules
                    sb.AppendLine("Rules");
                    sb.AppendLine(new String('-', 10));
                    sb.AppendLine("Each column, row and 3x3 box must contain numbers from 1-9");
                    sb.AppendLine();

                    if (Commands.Any())
                    {
                        sb.AppendLine("Commands");
                        sb.AppendLine("Press c to enter command mode");
                        sb.Append("\n\n");
                        string infoText = "Command name: Description";
                        sb.AppendLine(infoText);
                        sb.AppendLine(new String('-', infoText.Length));
                        foreach (var cmd in Commands)
                            sb.AppendLine($"{cmd.Key}: {(String.IsNullOrEmpty(cmd.Value.Item1) || String.IsNullOrWhiteSpace(cmd.Value.Item1) ? "No description provided." : cmd.Value.Item1)}");
                    }
                    else
                        sb.Append("No commands.");
                    sb.AppendLine();

                    UI.DisplayText(sb.ToString());
                })));
            Commands.Add("regen", ("Regenerates the board. This will erase the current board",
                new Action(() =>
                {
                    Sudoku.Clear();
                    Sudoku.Solver.SolveThePuzzle();
                    RemoveCells(51);
                    UI.WriteSudoku();
                })));
            Commands.Add("check", ("Checks the board",
                new Action(() =>
                {
                    if (Sudoku.IsBoardFilled() && Sudoku.Solver.CheckTableStateIsValid())
                        UI.DisplayText("Congratulations, the puzzle is successfully solved. You can regenerate the puzzle with 'regen'\n", fore : ConsoleColor.Green);
                    else if (Sudoku.IsBoardFilled() && !Sudoku.Solver.CheckTableStateIsValid())
                        UI.DisplayText("Sorry, the puzzle is not solved correctly.\n", fore : ConsoleColor.Red);
                    else if (!Sudoku.IsBoardFilled() && Sudoku.Solver.CheckTableStateIsValid())
                        UI.DisplayText("The current state of the puzzle is correct, but not completed yet.\n");
                    else
                        UI.DisplayText("Sorry, the current state of the puzzle is incorrect, and not completed yet.\n", fore : ConsoleColor.Red);
                })));
            Commands.Add("save", ("Saves the game",
                new Action(() =>
                {
                    if (File.Exists("game.save"))
                    {
                        UI.DisplayText("An existing save was found. Are you sure you want to rewrite it? (y/N)\n", fore : ConsoleColor.Yellow);

                        ConsoleKeyInfo answer;
                        bool valid = false;
                        do
                        {
                            answer = Console.ReadKey(true);
                            // Valid key
                            if (answer.Key == ConsoleKey.Enter || answer.Key == ConsoleKey.Y || answer.Key == ConsoleKey.N)
                                valid = true;
                        } while (!valid);

                        if (answer.Key == ConsoleKey.Enter || answer.Key == ConsoleKey.N)
                        {
                            UI.DisplayText("Cancelled\n");
                            return;
                        }
                    }

                    // Not a safe way to save stuff. It can be edited. Feel free to do so if you want to
                    var bf = new BinaryFormatter();
                    var stream = new FileStream("game.save", FileMode.Create);
                    bf.Serialize(stream, Sudoku);
                    stream.Close();

                    UI.DisplayText("Game saved!\n", fore : ConsoleColor.Green);
                })));
            Commands.Add("load", ("Loads a saved game",
                new Action(() =>
                {
                    if (!File.Exists("game.save"))
                    {
                        UI.DisplayText("No saved game was found.\n", fore : ConsoleColor.Red);
                        return;
                    }

                    var stream = new FileStream("game.save", FileMode.Open);
                    try
                    {
                        var bf = new BinaryFormatter();
                        Sudoku = (SudokuBoard)bf.Deserialize(stream);
                    }
                    catch (Exception)
                    {
                        UI.DisplayText("Game save format error.", fore : ConsoleColor.Red);
                        throw;
                    }
                    finally
                    {
                        stream.Close();
                    }

                    UI.DisplayText("Game loaded!\n", fore : ConsoleColor.Green);
                    UI.WriteSudoku();
                })));
        }
    }
}