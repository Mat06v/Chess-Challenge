
using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
	int[] pieceValues = { 100, 320, 330, 500, 900, 10000 };
	Board board;
	int thinkTime;
	ChessChallenge.API.Timer timer;
	bool stop;
	int tableSize = 128;
	TranspositionTable table;
	Move currentBestMove;
	int currentBestEval;
	Move bestMove;
	int bestEval;

	public MyBot()
	{
		this.table = new TranspositionTable(tableSize);

		/* //load transposition table during construction (only works for starting position)
		this.board = Board.CreateBoardFromFEN("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
		this.table.update(board);

		this.timer = new Timer(5000);
		this.thinkTime = 5000;
		startSearch();
		*/
	}

	public Move Think(Board board, ChessChallenge.API.Timer timer)
	{
		this.table.update(board);
		this.timer = timer;

		this.board = board;
		int expectedGameLength = Math.Max(50 - board.PlyCount / 2, 20);
		this.thinkTime = timer.IncrementMilliseconds + timer.MillisecondsRemaining / expectedGameLength;
		stop = false;

		return startSearch();

	}



	int Search(int depth, int alpha = -30000, int beta = 30000, bool root = false)
	{
		int tableEval = table.searchPos(depth, alpha, beta);
		if (tableEval != int.MinValue)
		{
			if (root && tableEval > currentBestEval)
			{
				currentBestEval = tableEval;
				currentBestMove = table.getMove();
			}

			return tableEval;
		}

		Move[] moves = sortMoves(depth <= 0, table.getMove());


		if (depth <= 0)
		{
			int stand_pat = Evaluation();
			if (stand_pat >= beta)
				return beta;
			if (alpha < stand_pat)
				alpha = stand_pat;

			if (moves.Length == 0)
			{
				return alpha;
			}

		}


		if (board.IsDraw())
		{
			return 0;
		}

		if (moves.Length == 0)
		{
			return -25000 - depth;
		}

	
		Move bestMovePos = moves[0];

		int evalMode = 1;
		

		foreach (Move move in moves)
		{
			board.MakeMove(move);
			int eval = -Search(depth - 1, -beta, -alpha);
			board.UndoMove(move);
			

			if (root && eval > currentBestEval)
			{
				currentBestEval = eval;
				currentBestMove = move;
			}
			//alpha beta pruning
			if (eval >= beta)
			{
				table.store(depth, 2, beta, bestMovePos);

				return beta;
			}
			if (alpha < eval)
			{
				alpha = eval;
				bestMovePos = move;
				evalMode = 0;


			}



			if (timer.MillisecondsElapsedThisTurn > thinkTime)
			{
				stop = true;

				break;
			}

		}

		table.store(depth, evalMode, alpha, bestMovePos);

		return alpha;
	}

	Move[] sortMoves(bool onlyCaptures, Move firstMove)
	{
		Move[] moves = board.GetLegalMoves(onlyCaptures);
		int[] scores = new int[moves.Length];

		for (int i = 0; i < scores.Length; i++)
		{
			int score = 0;

			if (moves[i].IsCapture)
			{
				score += 10 * pieceValues[((int)moves[i].CapturePieceType) - 1] - pieceValues[((int)moves[i].MovePieceType) - 1];

			}
			if (moves[i] == firstMove)
			{
				score = 100000;
			}

			scores[i] = -score;
		}

		Array.Sort(scores, moves);
		return moves;
	}



	Move startSearch()
	{
		int maxDepth = 50;
		bestEval = -30000;
		bestMove = Move.NullMove;
		int alpha = -30000;
		int beta = 30000;
		int windowSize = 50;


		for (int depth = 1; depth <= maxDepth; depth ++)
		{
			currentBestEval = -30000;
			currentBestMove = Move.NullMove;
			Search(depth, -beta, -alpha, true);

			if (timer.MillisecondsElapsedThisTurn > thinkTime / 2)
			{
				Console.WriteLine("MyBot");
				Console.WriteLine("time: " + timer.MillisecondsElapsedThisTurn.ToString() + " / " + thinkTime.ToString());
				Console.WriteLine("depth: " + depth.ToString());
				Console.WriteLine();
				
				stop = true;
				break;
			}

			if (!stop || currentBestEval > bestEval)
			{
				bestMove = currentBestMove;
				bestEval = currentBestEval;
				
			}


			if (bestEval >= beta || bestEval <= alpha)
			{
				alpha = -30000;
				beta = 30000;
				depth--;
			}
			else
			{
				alpha = bestEval - windowSize;
				beta = bestEval + windowSize;
			}



		}


		return bestMove;

	}


	int Evaluation()
	{

		if (board.IsInCheckmate())
			return board.IsWhiteToMove ? -25000 : 25000;

		if (board.IsDraw())
			return 0;

		int eval = 0;
		PieceList[] pieces = board.GetAllPieceLists();

		for (int i = 0; i < 6; i++)
		{
			eval += pieceValues[i] * (pieces[i].Count - pieces[i + 6].Count);
		}

		foreach (PieceList pieceList in pieces)
		{
			foreach (Piece piece in pieceList)
			{
				if (!piece.IsQueen && !piece.IsKing)
				{
					ulong bb = BitboardHelper.GetPieceAttacks(piece.PieceType, piece.Square, board, piece.IsWhite);
					eval += BitboardHelper.GetNumberOfSetBits(bb) * (piece.IsWhite ? 1 : -1);
				}
				if (piece.IsKing)
				{
					ulong bb = BitboardHelper.GetPieceAttacks(PieceType.Queen, piece.Square, board, piece.IsWhite);
					eval -= BitboardHelper.GetNumberOfSetBits(bb) * (piece.IsWhite ? 1 : -1);

					int index = piece.Square.Index;
					if ((index == 2 || index == 6) && piece.IsWhite)
						eval += 50;
					else if ((index == 58 || index == 62) && !piece.IsWhite)
						eval -= 50;
				}
				else if (false && piece.IsKnight)
				{
					eval += 10;
				}
			}

		}

		return eval * (board.IsWhiteToMove ? 1 : -1);
	}

	int manhattanDistFromCenter(Square square)
	{
		return Math.Min(square.Rank, square.File);
	}


}

public struct HashEntry
{
	public ulong zobrist;
	public int depth;
	public int flag;
	public int eval;
	public Move move;

	public HashEntry(ulong zobrist, int depth, int flag, int eval, Move move)
	{
		this.zobrist = zobrist;
		this.depth = depth;
		this.flag = flag;
		// 0 : exact
		// 1 : alpha
		// 2 : beta


		this.eval = eval;
		this.move = move;
	}
}


public class TranspositionTable
{
	Board board;
	int size;
	public HashEntry[] hashEntries;


	public TranspositionTable(int sizeMB)
	{
		this.size = sizeMB * 1024 * 1024 / 24;//Marshal.SizeOf<HashEntry>();
		Console.WriteLine("size " + size.ToString());
		this.hashEntries = new HashEntry[size];
	}

	public void update(Board board)
	{
		this.board = board;
	}

	public void store(int depth, int flag, int eval, Move move)
	{
		int index = Math.Abs(((int)board.ZobristKey)) % size;
		hashEntries[index] = new HashEntry(board.ZobristKey, Math.Min(depth, 1), flag, eval, move);
	}

	public int searchPos(int depth, int alpha, int beta)
	{
		int index = Math.Abs((int)board.ZobristKey) % size;
		HashEntry hashEntry = hashEntries[index];
		

		if (hashEntry.zobrist == board.ZobristKey && hashEntry.depth >= depth)
		{
			if (hashEntry.flag == 0) // exact evaluation
			{
				return hashEntry.eval;
			}
			if (hashEntry.flag == 1 && hashEntry.eval <= alpha) // alpha evaluation
			{
				return alpha;
			}
			if ((hashEntry.flag == 2) && hashEntry.eval >= beta) // beta evaluation
			{
				return beta;
			}
		}

		return int.MinValue;
	}

	public Move getMove()
	{
		int index = Math.Abs((int)board.ZobristKey) % size;
		HashEntry hashEntry = hashEntries[index];
		return hashEntry.move;
	}

}
