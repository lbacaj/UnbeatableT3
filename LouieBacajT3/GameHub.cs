using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using LouieBacajT3.Models;
using System.Threading.Tasks;

namespace LouieBacajT3
{
    public class GameHub : Hub
    {
        /// <summary>
        /// A basic internal object used to synchronize connected players during critical sections.
        /// </summary>
        private static object lockObject = new object();

        /// <summary>
        /// Total games played.
        /// </summary>
        private static int totalGames = 0;

        /// <summary>
        /// The private global list of clients.
        /// </summary>
        private static readonly List<Player> listOfPlayers = new List<Player>();

        /// <summary>
        /// The internal list of games and their states
        /// </summary>
        private static readonly List<TicTacToe> listOfGames = new List<TicTacToe>();

        /// <summary>
        /// Am internal global random number generater.
        /// </summary>
        private static readonly Random random = new Random();

        /// <summary>
        /// Handles player disconnects.
        /// First gets the current game from the listOfGames, Then handles any clients without games or removes the current game from the listOfGames.
        /// Then get the current player and handle their disconnect.
        /// </summary>
        public override Task OnDisconnected(bool stopCalled)
        {
            //get the current game based on middleware Context object connection ID.
            var currentGame = listOfGames.FirstOrDefault(x => x.P1.ConnectionId == Context.ConnectionId || x.P2.ConnectionId == Context.ConnectionId);
            if (currentGame == null)
            {
                PlayerWithoutGame();
                return null;
            }
            else
                listOfGames.Remove(currentGame);

            var currentPlayer = currentGame.P1.ConnectionId == Context.ConnectionId ? currentGame.P1 : currentGame.P2;

            if (currentPlayer == null) return null;

            listOfPlayers.Remove(currentPlayer);
            if (currentPlayer.Opponent != null)
            {
                UpdateAllClients();
                return Clients.Client(currentPlayer.Opponent.ConnectionId).opponentDisconnected(currentPlayer.Name);
            }
            return null;
        }

        /// <summary>
        /// Updates everyone on new player connections.
        /// </summary>
        /// <returns></returns>
        public override Task OnConnected()
        {
            return UpdateAllClients();
        }

        /// <summary>
        /// Registers players and adds them to the global list.
        /// </summary>
        /// <param name="playerName">The player name</param>
        public void RegisterClient(string playerName)
        {
            lock (lockObject)
            {
                var client = listOfPlayers.FirstOrDefault(x => x.ConnectionId == Context.ConnectionId);
                if (client == null)
                {
                    client = new Player { ConnectionId = Context.ConnectionId, Name = playerName };
                    listOfPlayers.Add(client);
                }

                client.IsPlaying = false;
            }

            UpdateAllClients();
            Clients.Client(Context.ConnectionId).registerComplete();
        }

        /// <summary>
        /// A human player places a marker on the board.
        /// </summary>
        /// <param name="position">The position where to place the marker</param>
        public void HumanPlaceMarker(int position)
        {
            // Find the current game fromt he main list of games.
            var game = listOfGames.FirstOrDefault(x => x.P1.ConnectionId == Context.ConnectionId || x.P2.ConnectionId == Context.ConnectionId);

            if (game == null || game.IsGameOver) return;

            int marker =  GetCurrentPlayerMarker(game);
            var player = marker == 0 ? game.P1 : game.P2;

            if (player.WaitingForMove) return;

            NotifyPlayersOfMarkerPlacement(position, game, player);

            if (game.PlaceMarkerCheckWinner(marker, position))
            {
                NotifyWinner(game, player);
                CleanPlayersUp(game);
            }

            if (game.IsGameOver && game.IsDraw)
            {
                NotifyDraw(game);
                CleanPlayersUp(game);
            }

            if (!game.IsGameOver)
            {
                GameIsntOver(player);
                if (game.Mode == GameMode.HumanVsAi || game.Mode == GameMode.AiVsHuman)
                    AiPlaceMarker(game.NegaMax(game.GameGrid)[1], game);
            }

            UpdateAllClients();
        }


        /// <summary>
        /// Start a new game depending ont he game mode.
        /// Find the player from the connection ID then proceed to find them someone to play.
        /// </summary>
        /// <param name="gameMode">The game mode.. e.g Human vs Ai, Human vs Human</param>
        public void NewGame(int gameMode)
        {
            var mode = (GameMode)gameMode;
            var player = listOfPlayers.FirstOrDefault(x => x.ConnectionId == Context.ConnectionId);

            if (player == null) return;
            player.LookingForOpponent = true;

            var cpuOpponent = new Player { Name = "The Big Bad CPU", ConnectionId = "CPU" };

            switch (mode)
            {   

                case GameMode.HumanVsAi:
                    SetPlayersAsBusy(player, cpuOpponent);

                    player.Symbol = "X";
                    cpuOpponent.Symbol = "O";

                    // Notify human player that a game was found
                    Clients.Client(Context.ConnectionId).foundOpponent(cpuOpponent.Name, "/Content/Images/LouieT3X.png");

                    player.WaitingForMove = false;
                    cpuOpponent.WaitingForMove = true;

                    Clients.Client(player.ConnectionId).waitingForMarkerPlacement(cpuOpponent.Name);
                

                    lock (lockObject)
                    {
                        listOfGames.Add(new TicTacToe { P1 = player, P2 = cpuOpponent, Mode = mode });
                    }

                    UpdateAllClients();
                    break;


                case GameMode.AiVsHuman:
                    SetPlayersAsBusy(player, cpuOpponent);

                    player.Symbol = "O";
                    cpuOpponent.Symbol = "X";

                    // Notify human player that a game was found
                    Clients.Client(Context.ConnectionId).foundOpponent(cpuOpponent.Name, "/Content/Images/LouieT3O.png");

                    player.WaitingForMove = true;
                    cpuOpponent.WaitingForMove = false;

                    Clients.Client(player.ConnectionId).waitingForOpponent(cpuOpponent.Name);

                    //create a new game and add it to the game list
                    var game = new TicTacToe { P1 = player, P2 = cpuOpponent, Mode = mode };
                    lock (lockObject)
                    {
                        listOfGames.Add(game);
                    }

                    //since AI goes first let CPU make a move..
                    AiPlaceMarker(game.NegaMax(game.GameGrid)[1], game);

                    UpdateAllClients();
                    break;


                default: // human vs human

                    // Look for a random opponent if there's more than one looking for a game
                    var humanOpponent = listOfPlayers.Where(x => x.ConnectionId != Context.ConnectionId && x.LookingForOpponent && !x.IsPlaying).OrderBy(x => Guid.NewGuid()).FirstOrDefault();
                    if (humanOpponent == null)
                    {
                        Clients.Client(Context.ConnectionId).noOpponents();
                        return;
                    }

                    SetPlayersAsBusy(player, humanOpponent);

                    player.Symbol = "X";
                    humanOpponent.Symbol = "O";

                    Clients.Client(Context.ConnectionId).foundOpponent(humanOpponent.Name, "/Content/Images/LouieT3X.png");
                    Clients.Client(humanOpponent.ConnectionId).foundOpponent(player.Name, "/Content/Images/LouieT3O.png");

                    if (random.Next(0, 5000) % 2 == 0)
                    {
                        player.WaitingForMove = false;
                        humanOpponent.WaitingForMove = true;

                        Clients.Client(player.ConnectionId).waitingForMarkerPlacement(humanOpponent.Name);
                        Clients.Client(humanOpponent.ConnectionId).waitingForOpponent(humanOpponent.Name);
                    }
                    else
                    {
                        player.WaitingForMove = true;
                        humanOpponent.WaitingForMove = false;

                        Clients.Client(humanOpponent.ConnectionId).waitingForMarkerPlacement(humanOpponent.Name);
                        Clients.Client(player.ConnectionId).waitingForOpponent(humanOpponent.Name);
                    }

                    lock (lockObject)
                    {
                        listOfGames.Add(new TicTacToe { P1 = player, P2 = humanOpponent, Mode = mode });
                    }

                    UpdateAllClients();
                    break;
            }
        }



        #region Internal Functions
        private void AiPlaceMarker(int position, TicTacToe game)
        {
            if (game == null || game.IsGameOver) return;

            int marker = 1;

            var player = marker == 0 ? game.P1 : game.P2;

            if (player.WaitingForMove) return;

            NotifyPlayersOfMarkerPlacement(position, game, player);

            if (game.PlaceMarkerCheckWinner(marker, position))
            {
                NotifyWinner(game, player);
                CleanPlayersUp(game);
            }

            if (game.IsGameOver && game.IsDraw)
            {
                NotifyDraw(game);
                CleanPlayersUp(game);
            }

            if (!game.IsGameOver)
            {
                GameIsntOver(player);
            }

            UpdateAllClients();
        }

        private void PlayerWithoutGame()
        {
            var playerWithoutGame = listOfPlayers.FirstOrDefault(x => x.ConnectionId == Context.ConnectionId);
            if (playerWithoutGame != null)
            {
                listOfPlayers.Remove(playerWithoutGame);

                UpdateAllClients();
            }
        }

        private int GetCurrentPlayerMarker(TicTacToe game)
        {
            int marker = 0;

            if (game.P2.ConnectionId == Context.ConnectionId)
            {
                marker = 1;
            }
            return marker;
        }

        private void NotifyWinner(TicTacToe game, Player player)
        {
            listOfGames.Remove(game);
            totalGames += 1;
            Clients.Client(game.P1.ConnectionId).gameOver(player.Name);
            Clients.Client(game.P2.ConnectionId).gameOver(player.Name);
        }

        private void NotifyDraw(TicTacToe game)
        {
            listOfGames.Remove(game);
            totalGames += 1;
            Clients.Client(game.P1.ConnectionId).gameOver("Game ended in a draw!");
            Clients.Client(game.P2.ConnectionId).gameOver("Game ended in a draw!");
        }

        private void CleanPlayersUp(TicTacToe game)
        {
            game.P1.IsPlaying = false;
            game.P1.Opponent = null;
            game.P1.WaitingForMove = false;
            game.P1.Symbol = String.Empty;

            game.P2.IsPlaying = false;
            game.P2.Opponent = null;
            game.P2.WaitingForMove = false;
            game.P2.Symbol = String.Empty;

        }


        private void GameIsntOver(Player player)
        {
            player.WaitingForMove = !player.WaitingForMove;
            player.Opponent.WaitingForMove = !player.Opponent.WaitingForMove;

            Clients.Client(player.Opponent.ConnectionId).waitingForMarkerPlacement(player.Name);
        }

        private void NotifyPlayersOfMarkerPlacement(int position, TicTacToe game, Player player)
        {
            Clients.Client(game.P1.ConnectionId).addMarkerPlacement(new GameInfo { OpponentName = player.Name, MarkerPosition = position, Symbol = player.Symbol });
            Clients.Client(game.P2.ConnectionId).addMarkerPlacement(new GameInfo { OpponentName = player.Name, MarkerPosition = position, Symbol = player.Symbol });
        }

        private Task UpdateAllClients()
        {
            return Clients.All.refreshAmountOfPlayers(new { totalGamesPlayed = totalGames, amountOfGames = listOfGames.Count, amountOfClients = listOfPlayers.Count });
        }

        private static void SetPlayersAsBusy(Player player, Player cpuOpponent)
        {
            player.IsPlaying = true;
            player.LookingForOpponent = false;
            cpuOpponent.IsPlaying = true;
            cpuOpponent.LookingForOpponent = false;
            player.Opponent = cpuOpponent;
            cpuOpponent.Opponent = player;
        }
        #endregion

    }
}