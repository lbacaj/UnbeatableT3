using LouieBacajT3.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace LouieBacajT3
{
    public enum GameMode
    {
        HumanVsHuman,
        HumanVsAi,
        AiVsHuman
    }

    public class TicTacToe
    {
        public bool IsGameOver { get; private set; }

        public bool IsDraw { get; private set; }

        public GameMode Mode { get; set; }

        public Player P1 { get; set; }

        public Player P2 { get; set; }

        public int[] GameGrid { get; private set; }

        private int _movesLeft = 9;

        public TicTacToe()
        {
            GameGrid = new int[9];

            for (var i = 0; i < GameGrid.Length; i++)
            {
                GameGrid[i] = -1;
            }
        }

        public TicTacToe(TicTacToe copyConstructor)
        {
            IsGameOver = copyConstructor.IsGameOver;
            IsDraw = copyConstructor.IsDraw;
            P1 = copyConstructor.P1;
            P2 = copyConstructor.P2;
            Mode = copyConstructor.Mode;
            _movesLeft = copyConstructor._movesLeft;
            Array.Copy(copyConstructor.GameGrid, GameGrid, copyConstructor.GameGrid.Length);
        }

        /// <summary>
        /// Places a marker on the grid for a given position
        /// </summary>
        /// <param name="player">The player number should be 0 or 1</param>
        /// <param name="position">The position where to place the marker, should be between 0 and 9</param>
        /// <returns>True if a winner was found</returns>
        public bool PlaceMarkerCheckWinner(int player, int position)
        {
            int convertedPlayer = player == 0 ? 2 : 1;

            if (IsGameOver)
                return false;

            PlaceMarker(player, position);

            if (CheckWinner(GameGrid, convertedPlayer))
            {
                IsGameOver = true;
                return true;
            }
            else
                return false;

        }

        private bool CheckWinner(int[] gameGrid, int player)
        {
            if ((gameGrid[0] == gameGrid[1] && gameGrid[1] == gameGrid[2] && gameGrid[0] == player)
                || (gameGrid[3] == gameGrid[4] && gameGrid[4] == gameGrid[5] && gameGrid[3] == player)
                || (gameGrid[6] == gameGrid[7] && gameGrid[7] == gameGrid[8] && gameGrid[6] == player)
                || (gameGrid[0] == gameGrid[3] && gameGrid[3] == gameGrid[6] && gameGrid[0] == player)
                || (gameGrid[1] == gameGrid[4] && gameGrid[4] == gameGrid[7] && gameGrid[1] == player)
                || (gameGrid[2] == gameGrid[5] && gameGrid[5] == gameGrid[8] && gameGrid[2] == player)
                || (gameGrid[0] == gameGrid[4] && gameGrid[4] == gameGrid[8] && gameGrid[0] == player)
                || (gameGrid[2] == gameGrid[4] && gameGrid[4] == gameGrid[6] && gameGrid[2] == player))
            {
                return true;
            }
            else
                return false;
        }


        /// <summary>
        /// Places a marker at the given position for the given player as long as the position is marked as -1
        /// </summary>
        /// <param name="player">The player number should be 0 or 1</param>
        /// <param name="position">The position where to place the marker, should be between 0 and 9</param>
        /// <returns>True if the marker position was not already taken</returns>
        private bool PlaceMarker(int player, int position)
        {
            int convertedPlayer = player == 0 ? 2 : 1;

            _movesLeft -= 1;

            if (_movesLeft <= 0)
            {
                IsGameOver = true;
                IsDraw = true;
                return false;
            }

            if (position > GameGrid.Length)
                return false;
            if (GameGrid[position] != -1)
                return false;

            GameGrid[position] = convertedPlayer;

            return true;
        }



        // negamax search
        // http://en.wikipedia.org/wiki/Negamax
        public int[] NegaMax(int[] gameGrid, int player = 1)
        {
            int move = -1;

            List<int> availableMoves = new List<int>();
            for (int i = 0; i < gameGrid.Length; i++)
            {
                if (gameGrid[i] == -1)
                    availableMoves.Add(i);
            }

            int reward = EvaluateState(gameGrid);
            if (reward != 0 || availableMoves.Count < 1)
                return (new int[] { reward, move });

            int bestValue = 0;

            if (player == 1)
            {
                //cpu player is min
                bestValue = Int32.MinValue;
                for (int i = 0; i < availableMoves.Count; i++)
                {
                    int[] tmpState = new int[9];
                    Array.Copy(gameGrid, tmpState, gameGrid.Length);

                    tmpState[availableMoves[i]] = player;

                    int val = NegaMax(tmpState, 2)[0];
                    if (val > bestValue)
                    {
                        bestValue = val;
                        move = availableMoves[i];
                    }
                }
            }
            else
            {
                //human player is Max
                bestValue = Int32.MaxValue;
                for (int i = 0; i < availableMoves.Count; i++)
                {
                    int[] tmpState = new int[9];
                    Array.Copy(gameGrid, tmpState, gameGrid.Length);
                    tmpState[availableMoves[i]] = player;
                    int val = NegaMax(tmpState, 1)[0];
                    if (val < bestValue)
                    {
                        bestValue = val;
                        move = availableMoves[i];
                    }
                }
            }
            return (new int[] { bestValue, move });
        }

        private int EvaluateState(int[] gameGrid)
        {
            if (CheckWinner(gameGrid, 1))
                return 1;
            else if (CheckWinner(gameGrid, 2))
                return -1;
            else
                return 0;
        }


    }

}