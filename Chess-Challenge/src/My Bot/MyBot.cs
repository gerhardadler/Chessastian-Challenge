﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using ChessChallenge.API;
using ChessChallenge.Application;

// Define a class to represent the game state
public class GameState
{
    // Define properties to represent the game state and its evaluation
    public int Depth { get; set; }
    public int Eval { get; set; }
    public Move BestMove { get; set; }
    // You can add other properties as needed based on your specific game.
    public GameState(int depth, int evaluation, Move bestMove)
    {
        Depth = depth;
        Eval = evaluation;
        BestMove = bestMove;
    }

}

public class TranspositionTable
{
    private Dictionary<ulong, GameState> table;

    public TranspositionTable()
    {
        table = new Dictionary<ulong, GameState>();
    }

    public void Add(ulong key, GameState gameState)
    {
        if (!table.ContainsKey(key))
        {
            table[key] = gameState;
        }
    }

    public GameState? Lookup(ulong key)
    {
        if (table.ContainsKey(key))
        {
            return table[key];
        }
        return null;
    }
}

public class MyBot : IChessBot
{
    int infinity = 999999;
    int[] pieceValues = { 100, 300, 300, 500, 900, 10000 };

    Move bestMove;
    int evaluatedPositions;
    int evaluatedCapturePositions;
    int finishedDepth;
    Timer botTimer;
    int allocatedTimeMs;

    TranspositionTable transpositionTable = new();

    public Move Think(Board board, Timer timer)
    {
        botTimer = timer;
        allocatedTimeMs = 3000;

        finishedDepth = 0;
        transpositionTable = new();
        bestMove = Move.NullMove;
        evaluatedPositions = 0;
        evaluatedCapturePositions = 0;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        IterativeSearch(board);
        watch.Stop();
        Console.WriteLine("Iterative search: " + watch.ElapsedMilliseconds + " - " + finishedDepth + " - " + bestMove);
        
        // allocatedTimeMs = 1000000000;
        // transpositionTable = new();
        // bestMove = Move.NullMove;
        // evaluatedPositions = 0;
        // evaluatedCapturePositions = 0;
        // watch = System.Diagnostics.Stopwatch.StartNew();
        // Search(board, 6, -infinity, infinity, true, null);
        // watch.Stop();
        // Console.WriteLine("Normal search: " + watch.ElapsedMilliseconds + " - " + 6 + " - " + bestMove);


        // Console.WriteLine(Evaluate(board));
        // Console.WriteLine(bestMove);
        // Console.WriteLine(timer.MillisecondsElapsedThisTurn);
        return bestMove;
    }

    int GetPieceValue(PieceType pieceType)
    {
        return pieceValues[(int)(pieceType - 1)];
    }

    void Abort()
    {
        allocatedTimeMs = 0;
    }

    bool DoAbort()
    {
        if (botTimer.MillisecondsElapsedThisTurn >= allocatedTimeMs) {
            return true;
        }
        return false;
    }


    Move IterativeSearch(Board board)
    {
        Move? previousBestMove = null;
        int depth = 1;
        while (!DoAbort())
        {
            Search(board, depth, -infinity, infinity, true, previousBestMove);
            previousBestMove = bestMove;
            depth++;
        }
        finishedDepth = depth - 1;
        return bestMove;
    }

    int Search(Board board, int depth, int alpha, int beta, bool isRoot, Move? previousBestMove)
    {
        if (DoAbort()) return alpha;
        // plus one to make checkmate better than not moving
        if (board.IsInCheckmate()) return -infinity + 1;
        if (board.IsDraw()) return 0;


        if (depth == 0)
        {
            // evaluatedPositions += 1;
            return SearchOnlyCaptures(board, -infinity, infinity);
        }

        GameState? gameState = transpositionTable.Lookup(board.ZobristKey);
        if (gameState != null && gameState.Depth >= depth)
        {
            if (isRoot) bestMove = gameState.BestMove;

            return gameState.Eval;
        }

        Move[] moves = board.GetLegalMoves();

        OrderMoves(moves, board, previousBestMove);

        foreach (Move move in moves)
        {
            int eval;
            board.MakeMove(move);
            eval = -Search(board, depth - 1, -beta, -alpha, false, previousBestMove);
            board.UndoMove(move);

            if (eval >= beta) return beta;
            if (eval > alpha)
            {
                if (isRoot) bestMove = move;
                alpha = eval;
            }
        }
        transpositionTable.Add(board.ZobristKey, new GameState(depth, alpha, bestMove));
        return alpha;
    }


    int SearchOnlyCaptures(Board board, int alpha, int beta)
    {
        // evaluatedCapturePositions += 1;
        int stand_pat = Evaluate(board);
        if (stand_pat >= beta) return beta;
        alpha = Math.Max(alpha, stand_pat);

        Move[] moves = board.GetLegalMoves(true);

        OrderMoves(moves, board, null);

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int eval = -SearchOnlyCaptures(board, -beta, -alpha);
            board.UndoMove(move);

            if (eval >= beta) return beta;
            alpha = Math.Max(alpha, eval);
        }
        return alpha;
    }


    void OrderMoves(Move[] moves, Board board, Move? previousBestMove)
    {
        int[] moveWeights = new int[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];
            if (move == previousBestMove)
            {
                moveWeights[i] = -infinity;
                continue;
            }
            int moveScoreGuess = 0;

            if (move.IsPromotion) moveScoreGuess += 700;
            if (move.IsCastles) moveScoreGuess += 200;
            if (move.IsCapture) moveScoreGuess += GetPieceValue(move.CapturePieceType) - GetPieceValue(move.MovePieceType) / 10;
            if (board.SquareIsAttackedByOpponent(move.TargetSquare)) moveScoreGuess -= GetPieceValue(move.MovePieceType) / 5;
            moveScoreGuess *= -1;

            moveWeights[i] = moveScoreGuess;
        }
        Array.Sort(moveWeights, moves);
    }


    int Evaluate(Board board)
    {
        Move[] moves = board.GetLegalMoves();

        int eval = 0;
        PieceList[] pieces = board.GetAllPieceLists();
        int index = 0;
        int colorMultiplyer = board.IsWhiteToMove ? 1 : -1;
        foreach (PieceList pieceList in pieces)
        {
            if (index == 6) colorMultiplyer *= -1;
            eval += pieceList.Count * pieceValues[index % 6] * colorMultiplyer;
            index++;
        }

        eval += moves.Length * 2;

        return eval;
    }
}