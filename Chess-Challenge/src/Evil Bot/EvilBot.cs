using ChessChallenge.API;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;

namespace ChessChallenge.Example
{
	// A simple bot that can spot mate in one, and always captures the most valuable piece it can.
	// Plays randomly otherwise.
	public class EvilBot : IChessBot
	{

		int[] pieceValues = { 100, 320, 330, 500, 900, 10000 };
		Board board;
		int thinkTime,
			tableSize = 256,
			currentBestEval,
			bestEval;
		ulong size = 11184810;
		Timer timer;
		bool stop;
		Move currentBestMove, bestMove;
		int[,] pst = new int[64, 7];

		public (ulong, int, int, int, Move)[] hashEntries = new (ulong, int, int, int, Move)[11184810];

		//public HashEntry[] hashEntries = new HashEntry[11184810];

		public EvilBot()
		{

		}

		public Move Think(Board board, ChessChallenge.API.Timer timer)
		{
			this.timer = timer;

			this.board = board;
			thinkTime = timer.IncrementMilliseconds + timer.MillisecondsRemaining / 30;
			stop = false;


			bestMove = Move.NullMove;
			int maxDepth = 50,
				bestEval = -30000,
				alpha = -30000,
				beta = 30000,
				windowSize = 50;

			for (int depth = 1; depth <= maxDepth; depth++)
			{
				currentBestEval = -30000;
				currentBestMove = Move.NullMove;
				Search(depth, -beta, -alpha, true);

				if (stop)
				{

					Console.WriteLine("EvilBot");
					Console.WriteLine("time: " + timer.MillisecondsElapsedThisTurn.ToString() + " / " + thinkTime.ToString());
					Console.WriteLine("depth: " + depth.ToString());
					Console.WriteLine();

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

			/*
			Console.WriteLine("MyBot");
			Console.WriteLine("time: " + timer.MillisecondsElapsedThisTurn.ToString() + " / " + thinkTime.ToString());
			Console.WriteLine();
			*/

			return bestMove;

		}



		int Search(int depth, int alpha, int beta, bool root)
		{
			int tableEval = int.MinValue;
			ulong index = board.ZobristKey % size;
			var hashEntry = hashEntries[index];

			if (hashEntry.Item1 == board.ZobristKey && hashEntry.Item2 >= depth)
			{
				int flag = hashEntry.Item4;
				if (flag == 0) // exact evaluation
					tableEval = hashEntry.Item3;
				if (flag == 1 && hashEntry.Item3 <= alpha) // alpha evaluation
					tableEval = alpha;
				if (flag == 2 && hashEntry.Item3 >= beta) // beta evaluation
					tableEval = beta;
			}

			int evalMode = 1;
			if (tableEval != int.MinValue)
			{
				if (root && tableEval > currentBestEval)
				{
					currentBestEval = tableEval;
					currentBestMove = GetMove();
				}

				return tableEval;
			}

			Move[] moves = board.GetLegalMoves(depth <= 0);
			int[] scores = new int[moves.Length];

			for (int i = 0; i < scores.Length; i++)
			{
				Move move = moves[i];
				int score = 0;

				if (move.IsCapture)
					score += 10 * pieceValues[(int)move.CapturePieceType - 1] - pieceValues[(int)move.MovePieceType - 1];

				if (move == GetMove())
					score = 100000;


				scores[i] = -score;
			}
			Array.Sort(scores, moves);

			if (depth <= 0)
			{
				int delta = 1000,
					stand_pat = Evaluation();

				if (stand_pat >= beta)
					return beta;
				if (stand_pat < alpha - delta)
					return alpha;
				if (alpha < stand_pat)
					alpha = stand_pat;

				if (moves.Length == 0)
					return alpha;


			}


			if (board.IsDraw())
				return 0;

			if (moves.Length == 0)
				return -25000 + board.PlyCount;

			Move bestMovePos = moves[0];

			foreach (Move move in moves)
			{
				board.MakeMove(move);
				int eval = -Search(depth - 1, -beta, -alpha, false);
				board.UndoMove(move);


				if (root && eval > currentBestEval)
				{
					currentBestEval = eval;
					currentBestMove = move;
				}
				//alpha beta pruning
				if (eval >= beta)
				{
					Store(depth, 2, beta, bestMovePos);

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

			Store(depth, evalMode, alpha, bestMovePos);

			return alpha;
		}


		int Evaluation()
		{
			// TODO : move evaluation function in search function to save tokens

			int eval = 0;
			PieceList[] pieces = board.GetAllPieceLists();

			for (int i = 0; i < 6; i++)
				eval += pieceValues[i] * (pieces[i].Count - pieces[i + 6].Count);
			/*
			foreach (PieceList pieceList in pieces)
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
						eval += 10; 

			}
			*/

			eval *= board.IsWhiteToMove ? 1 : -1;

			eval += board.GetLegalMoves().Length;
			if (board.TrySkipTurn())
			{
				eval -= board.GetLegalMoves().Length;
				board.UndoSkipTurn();
			}

			return eval;
		}

		public void Store(int depth, int flag, int eval, Move move)
		{
			ulong index = board.ZobristKey % size;
			hashEntries[index] = (board.ZobristKey, Math.Min(depth, 1), flag, eval, move);
		}



		public Move GetMove()
		{
			ulong index = board.ZobristKey % size;
			var hashEntry = hashEntries[index];
			return hashEntry.Item5;


		}
	}




}