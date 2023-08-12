using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using System.Security.Cryptography;
using Microsoft.VisualBasic;

using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

public class MyBot : IChessBot
{
	int[] pieceValues = { 100, 320, 330, 500, 900, 10000 };
	Board board;
	int thinkTime;
	Timer timer;
	bool stop;
	int tableSize = 16;
	TranspositionTable table;
	Move currentBestMove;
	int currentBestEval;
	Move bestMove;
	int bestEval;

	public MyBot()
	{
		this.table = new TranspositionTable(tableSize);
	}

	public Move Think(Board board, Timer timer)
	{
		this.table.update(board);
		this.timer = timer;

		this.board = board;
		Console.Write("EvilBot");
		Console.WriteLine("eval " + Evaluation().ToString());
		int expectedGameLength = Math.Max(50 - board.PlyCount / 2, 20);
		this.thinkTime = timer.IncrementMilliseconds + timer.MillisecondsRemaining / expectedGameLength;
		stop = false;

		return startSearch();

	}



	int Search(int depth, int alpha = -30000, int beta = 30000, bool root = false)
	{
		Move[] moves = sortMoves(depth <= 0);


		if (depth <= 0)
		{
			//return Quiesce(alpha, beta);

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

		//this.table.update(board);

		int tableEval = table.searchPos(depth, alpha, beta);
		if (tableEval != int.MinValue)
		{
			return tableEval;
		}

		Move bestMovePos = moves[0];

		int evalMode = 1;
		if (root)
		{
			if (depth == 1)
			{
				Console.WriteLine(board.CreateDiagram());

			}
			Console.WriteLine();
		}

		foreach (Move move in moves)
		{
			board.MakeMove(move);
			int eval = -Search(depth - 1, -beta, -alpha);
			board.UndoMove(move);

			if (root)
			{
				Console.WriteLine(move.ToString() + "    eval " + eval.ToString() + "    alpha " + alpha.ToString() + "    beta " + beta.ToString());
			}

			if (root && eval > currentBestEval)
			{
				currentBestEval = eval;
				currentBestMove = move;
			}
			//alpha beta pruning
			if (eval >= beta)
			{
				//this.table.update(board);

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
		//this.table.update(board);

		table.store(depth, evalMode, alpha, bestMovePos);

		return alpha;
	}

	Move[] sortMoves(bool onlyCaptures)
	{
		Move[] moves = board.GetLegalMoves(onlyCaptures);
		Dictionary<Move, int> movesScore = new Dictionary<Move, int>();

		foreach (Move move in board.GetLegalMoves())
		{
			int score = 0;

			if (move.IsCapture)
			{
				score += 10 * pieceValues[((int)move.CapturePieceType) - 1] - pieceValues[((int)move.MovePieceType) - 1];

			}

			movesScore.Add(move, -score);
		}

		return moves.OrderBy(move => movesScore[move]).ToArray();
	}



	Move startSearch()
	{
		int maxDepth = 50;
		bestEval = -30000;
		bestMove = Move.NullMove;

		currentBestEval = -30000;
		currentBestMove = Move.NullMove;
		int alpha = -30000;
		int beta = 30000;

		for (int depth = 1; depth <= maxDepth; depth++)
		{
			Search(depth, -beta, -alpha, true);

			if (!stop || currentBestEval > bestEval)
			{
				bestMove = currentBestMove;
				bestEval = currentBestEval;

			}



			//alpha beta pruning

			if (timer.MillisecondsElapsedThisTurn > thinkTime / 2)
			{
				Console.WriteLine("MyBot");
				Console.WriteLine("time: " + timer.MillisecondsElapsedThisTurn.ToString() + " / " + thinkTime.ToString());
				Console.WriteLine("depth: " + depth.ToString());
				Console.WriteLine(System.Runtime.InteropServices.Marshal.SizeOf<HashEntry>());
				Console.WriteLine();
				stop = true;
				break;
			}

		}


		return bestMove;

	}


	int Evaluation()
	{

		if (board.IsInCheckmate())
		{
			if (board.IsWhiteToMove)
			{
				return -25000;
			}
			else
			{
				return 25000;
			}
		}
		if (board.IsDraw())
		{
			return 0;
		}

		int eval = 0;
		PieceList[] pieces = board.GetAllPieceLists();

		for (int i = 0; i < 6; i++)
		{
			eval += pieceValues[i] * (pieces[i].Count - pieces[i + 6].Count);
		}
		eval += numAttackedSquares();

		return eval * ((board.IsWhiteToMove) ? 1 : -1);
	}

	int numAttackedSquares()
	{
		int sum = 0;

		foreach (PieceList pieceList in board.GetAllPieceLists())
		{
			foreach (Piece piece in pieceList)
			{
				if (piece.PieceType != PieceType.Queen && piece.PieceType != PieceType.King)
				{
					ulong bb = BitboardHelper.GetPieceAttacks(piece.PieceType, piece.Square, board, piece.IsWhite);
					sum += BitboardHelper.GetNumberOfSetBits(bb) * (piece.IsWhite ? 1 : -1);

				}
			}

		}

		return sum;
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
		this.size = sizeMB * 1024 * 1024 / Marshal.SizeOf<HashEntry>();
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
		if (depth > hashEntry.depth)
		{
			return int.MinValue;
		}

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
}
