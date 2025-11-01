using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

namespace KlotskiDecisionTree
{
    [System.Serializable]
    public class Block
    {
        public int Id { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public Block(int id, int width, int height, int x, int y)
        {
            Id = id;
            Width = width;
            Height = height;
            X = x;
            Y = y;
        }

        public Block Clone()
        {
            return new Block(Id, Width, Height, X, Y);
        }
    }

    [System.Serializable]
    public class Board
    {
        public int Rows { get; set; }
        public int Columns { get; set; }
        public List<Block> Blocks { get; set; }
        public bool PinsEnabled { get; set; }
        public int? WinningBlockId { get; set; }
        public int? WinningX { get; set; }
        public int? WinningY { get; set; }
        public int ExitWidth { get; set; } = 1;

        public Board(int rows, int columns, bool pinsEnabled)
        {
            Rows = rows;
            Columns = columns;
            PinsEnabled = pinsEnabled;
            Blocks = new List<Block>();
        }

        public void AddBlock(Block block)
        {
            Blocks.Add(block);
        }

        public Board Clone()
        {
            var newBoard = new Board(Rows, Columns, PinsEnabled)
            {
                WinningBlockId = WinningBlockId,
                WinningX = WinningX,
                WinningY = WinningY,
                ExitWidth = ExitWidth
            };
            foreach (var block in Blocks)
            {
                newBoard.AddBlock(block.Clone());
            }
            return newBoard;
        }

        public string GetHash()
        {
            var sb = new StringBuilder();
            foreach (var block in Blocks.OrderBy(b => b.Id))
            {
                sb.Append($"{block.Id}:{block.X},{block.Y};");
            }
            return sb.ToString();
        }

        public bool IsWinning()
        {
            if (!WinningBlockId.HasValue || !WinningX.HasValue || !WinningY.HasValue) return false;
            var winningBlock = Blocks.FirstOrDefault(b => b.Id == WinningBlockId.Value);
            if (winningBlock == null) return false;
            return winningBlock.X == WinningX.Value && winningBlock.Y == WinningY.Value;
        }

        private bool IsAreaFree(int x, int y, int width, int height, Block movingBlock)
        {
            if (x < 0 || y < 0 || (x + width > Columns && !(x == WinningX && y == WinningY && movingBlock.Id == WinningBlockId)) ||
                (y + height > Rows && !(y == WinningY && movingBlock.Id == WinningBlockId))) return false;

            foreach (var block in Blocks)
            {
                if (block.Id == movingBlock.Id) continue;
                if (x < block.X + block.Width && x + width > block.X &&
                    y < block.Y + block.Height && y + height > block.Y)
                {
                    return false;
                }
            }
            return true;
        }

        private IEnumerable<(Block block, int dx, int dy)> GetPossibleMoves(Block block)
        {
            var directions = new List<(int dx, int dy)>();

            if (PinsEnabled)
            {
                if (block.Width > block.Height)
                {
                    directions.Add((-1, 0));
                    directions.Add((1, 0));
                }
                else if (block.Height > block.Width)
                {
                    directions.Add((0, -1));
                    directions.Add((0, 1));
                }
                else
                {
                    directions.Add((-1, 0));
                    directions.Add((1, 0));
                    directions.Add((0, -1));
                    directions.Add((0, 1));
                }
            }
            else
            {
                directions.Add((-1, 0));
                directions.Add((1, 0));
                directions.Add((0, -1));
                directions.Add((0, 1));
            }

            foreach (var (dx, dy) in directions)
            {
                var newX = block.X + dx;
                var newY = block.Y + dy;
                if (IsAreaFree(newX, newY, block.Width, block.Height, block))
                {
                    if (block.Id == WinningBlockId && newX == WinningX && newY == WinningY && ExitWidth >= block.Width)
                    {
                        yield return (block, dx, dy);
                    }
                    else if (newX >= 0 && newY >= 0 && newX + block.Width <= Columns && newY + block.Height <= Rows)
                    {
                        yield return (block, dx, dy);
                    }
                }
            }
        }

        public IEnumerable<Board> GetNextStates()
        {
            foreach (var block in Blocks)
            {
                foreach (var (b, dx, dy) in GetPossibleMoves(block))
                {
                    var newBoard = Clone();
                    var newBlock = newBoard.Blocks.First(nb => nb.Id == b.Id);
                    newBlock.X += dx;
                    newBlock.Y += dy;
                    yield return newBoard;
                }
            }
        }
    }

    [System.Serializable]
    public class GraphNode
    {
        public string StateHash { get; set; }
        public Board Board { get; set; }
        public List<(GraphNode child, string moveDescription)> Children { get; set; } = new List<(GraphNode, string)>();
        public bool IsWinning { get; set; }
        public bool IsStarting { get; set; }

        public GraphNode(Board board)
        {
            Board = board;
            StateHash = board.GetHash();
            IsWinning = board.IsWinning();
        }
    }

    public class DecisionGraphBuilder
    {
        public GraphNode BuildGraph(Board initialBoard)
        {
            var root = new GraphNode(initialBoard) { IsStarting = true };
            var allNodes = new Dictionary<string, GraphNode> { { root.StateHash, root } };
            var queue = new Queue<GraphNode>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var nextBoard in current.Board.GetNextStates())
                {
                    var nextHash = nextBoard.GetHash();
                    GraphNode nextNode;
                    if (!allNodes.TryGetValue(nextHash, out nextNode))
                    {
                        nextNode = new GraphNode(nextBoard);
                        allNodes[nextHash] = nextNode;
                        queue.Enqueue(nextNode);
                    }
                    if (nextHash != current.StateHash)
                    {
                        var moveDesc = GetMoveDescription(current.Board, nextBoard);
                        current.Children.Add((nextNode, moveDesc));
                    }
                }
            }

            return root;
        }

        private string GetMoveDescription(Board prev, Board next)
        {
            for (int i = 0; i < prev.Blocks.Count; i++)
            {
                var prevBlock = prev.Blocks[i];
                var nextBlock = next.Blocks[i];
                if (prevBlock.X != nextBlock.X || prevBlock.Y != nextBlock.Y)
                {
                    var dx = nextBlock.X - prevBlock.X;
                    var dy = nextBlock.Y - prevBlock.Y;
                    var direction = dx < 0 ? "left" : dx > 0 ? "right" : dy < 0 ? "up" : "down";
                    return $"Block {prevBlock.Id} moved {direction}";
                }
            }
            return "Unknown move";
        }
    }
}